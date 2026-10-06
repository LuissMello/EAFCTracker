using EAFCMatchTracker.Api.Infrastructure;
using EAFCMatchTracker.Api.Infrastructure.Dev;
using EAFCMatchTracker.Api.Security;
using EAFCMatchTracker.Application.Interfaces;
using EAFCMatchTracker.Application.Interfaces.Repositories;
using EAFCMatchTracker.Application.Interfaces.Services;
using EAFCMatchTracker.Application.Repositories;
using EAFCMatchTracker.Application.Services;
using EAFCMatchTracker.Application.Services.Analytics;
using EAFCMatchTracker.Domain.Entities;
using EAFCMatchTracker.Domain.Settings;
using EAFCMatchTracker.Infrastructure.Data;
using EAFCMatchTracker.Infrastructure.Http;

using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Porta padrão 8080 (Fly.io) — só aplica se ASPNETCORE_URLS / --urls não estiver definido
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
}

var isDevelopment = builder.Environment.IsDevelopment();

// Auxílio de teste (ver Infrastructure/Dev/DemoDataSeeder.cs): /api/dev/* só existe com banco em memória + Seed:Demo + Development.
var devSimulatorEnabled = DemoDataSeeder.SimulatorEnabled(builder.Configuration, builder.Environment);
if (devSimulatorEnabled) builder.Services.AddScoped<DevMatchSimulator>();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(m => m.FeatureProviders.Add(new DevControllerFeatureProvider(devSimulatorEnabled)))
    .AddJsonOptions(options =>
    {
        // Emite UTF-8 literal em vez de \uXXXX para acentos/caracteres especiais
        options.JsonSerializerOptions.Encoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();

// Autenticação do administrador: login (senha = Auth:ApiKey) -> token assinado de curta duração (Bearer).
// X-Api-Key continua aceito para scripts/curl. Ver ApiKeyMiddleware e AuthController.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAdminTokenService, AdminTokenService>();
builder.Services.AddSingleton<ILoginAttemptTracker, LoginAttemptTracker>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Orquestração de busca (single-flight) e modo ao vivo
builder.Services.AddSingleton<ILiveModeService, LiveModeService>();
builder.Services.AddSingleton<IFetchCoordinator, FetchCoordinator>();
// Modo somente leitura (análise local contra o banco real): nada de buscas na EA nem gravações. Ver ReadOnlyMode.cs.
var readOnly = ReadOnlyMode.IsEnabled(builder.Configuration);
if (!readOnly) builder.Services.AddHostedService<ClubMatchBackgroundService>();

// HttpClient simples para keep-alive (NÃO é o cliente da EA)
builder.Services.AddHttpClient(ClubMatchBackgroundService.KeepAliveHttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("EAFCMatchTracker-KeepAlive/1.0");
});
builder.Services.AddScoped<IClubMatchService, ClubMatchService>();

// Repositories
builder.Services.AddScoped<IClubRepository, ClubRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IGoalRepository, GoalRepository>();
builder.Services.AddScoped<IFetchRepository, FetchRepository>();
builder.Services.AddScoped<IAppSettingRepository, AppSettingRepository>();
builder.Services.AddScoped<ITrackedClubRepository, TrackedClubRepository>();
builder.Services.AddScoped<IGameVersionRepository, GameVersionRepository>();
builder.Services.AddScoped<IGoalRegistrationRepository, GoalRegistrationRepository>();

// Services
builder.Services.AddScoped<IClubService, ClubService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.AddScoped<ClubSessionService>();
builder.Services.AddScoped<ITrendsService, TrendsService>();
builder.Services.AddScoped<IGoalAnalysisService, GoalAnalysisService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IFetchService, FetchService>();

// Páginas analíticas públicas (noite de jogo, laboratório, retrospectiva): somente leitura, cache de 60 s
builder.Services.AddScoped<IGameNightService, GameNightService>();
builder.Services.AddScoped<ILabService, LabService>();
builder.Services.AddScoped<IWrappedService, WrappedService>();
builder.Services.AddScoped<IPlayerCardService, PlayerCardService>();

// Arquétipos de jogador: catálogo singleton (cache de 10 min; lê o banco por escopo próprio e cai para "vazio" se a
// tabela PlayerArchetypes não existir) + serviço de resumo/Admin
builder.Services.AddSingleton<IArchetypeCatalog, ArchetypeCatalog>();
builder.Services.AddScoped<IArchetypeService, ArchetypeService>();

// Registro de gols ao vivo/antecipado (público, com rate limit por IP): linker, serviço, busca/preview de adversários
builder.Services.AddScoped<IGoalRegistrationLinker, GoalRegistrationLinker>();
builder.Services.AddScoped<IGoalRegistrationService, GoalRegistrationService>();
builder.Services.AddScoped<IEaClubSearchClient, EaClubSearchClient>();
builder.Services.AddScoped<IOpponentSearchService, OpponentSearchService>();
builder.Services.AddScoped<IOpponentPreviewService, OpponentPreviewService>();

builder.Services.AddHttpClient<IEAHttpClient, EAHttpClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EAFCMatchTracker/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        // Validação de certificado relaxada SOMENTE em Development
        if (isDevelopment)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    });

builder.Services.Configure<EAFCSettings>(builder.Configuration.GetSection("EAFCSettings"));

// Rate limit por IP (janela fixa de 1 min) das rotas PÚBLICAS do registro de gols: busca, preview e escritas.
// Limites padrão 30/20/60 por minuto (configuráveis em RateLimits:GoalRegistration*PerMinute). Estouro -> 429 + Retry-After.
var behindFlyProxy = !string.IsNullOrEmpty(builder.Configuration["FLY_APP_NAME"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    void AddPerIpPolicy(string name, string configKey, int defaultPermits) =>
        options.AddPolicy(name, httpContext =>
        {
            var permits = builder.Configuration.GetValue<int?>(configKey) is > 0 and var configured ? configured : defaultPermits;
            return RateLimitPartition.GetFixedWindowLimiter(
                ClientIpResolver.Resolve(httpContext, behindFlyProxy),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    AddPerIpPolicy(EAFCMatchTracker.Api.Controllers.GoalRegistrationsController.SearchPolicy, "RateLimits:GoalRegistrationSearchPerMinute", 30);
    AddPerIpPolicy(EAFCMatchTracker.Api.Controllers.GoalRegistrationsController.PreviewPolicy, "RateLimits:GoalRegistrationPreviewPerMinute", 20);
    AddPerIpPolicy(EAFCMatchTracker.Api.Controllers.GoalRegistrationsController.WritePolicy, "RateLimits:GoalRegistrationWritePerMinute", 60);

    // Leituras das páginas analíticas (noite de jogo, laboratório, retrospectiva): 120/min por IP (RateLimits:AnalyticsReadPerMinute; o Laboratório faz 3 chamadas por troca de filtro e « » da noite faz 1 por clique)
    AddPerIpPolicy(EAFCMatchTracker.Api.Controllers.AnalyticsControllerBase.RateLimitPolicy, "RateLimits:AnalyticsReadPerMinute", 120);

    options.OnRejected = async (context, token) =>
    {
        var response = context.HttpContext.Response;
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Muitas requisições. Aguarde um instante e tente novamente."
        }, options: null, contentType: "application/problem+json", cancellationToken: token);
    };
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?.Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.Trim().TrimEnd('/'))
    .ToArray();

if (allowedOrigins is null || allowedOrigins.Length == 0)
{
    allowedOrigins = new[] { "https://luissmello.github.io", "http://localhost:3000" };
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()   // inclui Authorization e X-Api-Key (o preflight ecoa os headers pedidos)
                  .AllowAnyMethod()
                  .WithExposedHeaders("Retry-After", "WWW-Authenticate"); // legíveis pelo frontend (429 / 401)
        });
});

var supportedCultureNames = new[] { "pt-BR", "en-US" };
var supportedCultures = supportedCultureNames.Select(c => new CultureInfo(c)).ToList();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("pt-BR", "pt-BR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var useInMemory = builder.Configuration.GetValue<bool>("EAFCSettings:UseInMemoryDb");

builder.Services.AddDbContext<EAFCContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");

    if (useInMemory)
    {
        options.UseInMemoryDatabase("EAFC_TestDB");
        options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }
    else
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
            npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });

        if (isDevelopment)
        {
            options.EnableSensitiveDataLogging();
            options.LogTo(Console.WriteLine, LogLevel.Information);
        }
    }

    // Última barreira do modo somente leitura: recusa qualquer comando SQL de escrita.
    if (readOnly) options.AddInterceptors(new ReadOnlyCommandInterceptor());
});

var healthChecks = builder.Services.AddHealthChecks();
if (!useInMemory)
{
    healthChecks.AddDbContextCheck<EAFCContext>("database");
}

var app = builder.Build();

app.UseExceptionHandler();

var adminAuth = app.Services.GetRequiredService<IAdminTokenService>();
if (!adminAuth.IsConfigured)
{
    if (isDevelopment)
    {
        app.Logger.LogWarning(
            "Auth:ApiKey (env Auth__ApiKey) não configurada em Development: rotas administrativas e de escrita " +
            "estão ABERTAS (sem login). Defina a chave para testar o fluxo real de autenticação.");
    }
    else
    {
        app.Logger.LogWarning(
            "Auth:ApiKey (env Auth__ApiKey) NÃO está configurada. Login indisponível (503) e todas as rotas " +
            "protegidas (escrita em /api/* e tudo em /api/admin e /api/maintenance) responderão 401 até que seja definida.");
    }
}

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

// Middleware de guarda de cultura
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (CultureNotFoundException)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Invalid culture",
            message = "The requested culture is not supported."
        });
    }
});

if (!useInMemory)
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        await WaitForDatabaseAsync(services);

        var db = services.GetRequiredService<EAFCContext>();
        if (readOnly)
        {
            // Não migra: só avisa se o banco estiver atrás do código (as páginas novas podem falhar nesse caso).
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Count > 0)
                app.Logger.LogWarning("Modo somente leitura: {Count} migration(s) pendente(s) no banco ({Names}); não serão aplicadas.",
                    pending.Count, string.Join(", ", pending));
        }
        else
        {
            db.Database.Migrate();
        }
    }
}

if (useInMemory)
{
    // Somente para desenvolvimento/testes: cria o esquema em memória e aplica os dados semeados (HasData:
    // edições do jogo, clubes rastreados, configurações). Sem isto o banco em memória nasce vazio.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<EAFCContext>().Database.EnsureCreated();
}

if (!readOnly) await SeedDefaultSettingsAsync(app.Services);

// Somente em memória + Seed:Demo=true (auxílio de teste): dados de demonstração
if (!readOnly && DemoDataSeeder.IsEnabled(app.Configuration))
{
    await DemoDataSeeder.SeedAsync(app.Services, app.Logger);
}

if (readOnly)
    app.Logger.LogWarning("MODO SOMENTE LEITURA ATIVO: sem busca na EA, sem migrations, sem sementes; requisições que não são GET recebem 403 e comandos SQL de escrita são recusados.");

if (isDevelopment)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

if (readOnly) app.UseMiddleware<ReadOnlyGuardMiddleware>();

app.UseMiddleware<ApiKeyMiddleware>();

app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

async Task SeedDefaultSettingsAsync(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EAFCContext>();

        var existing = (await db.AppSettings.AsNoTracking().Select(s => s.Key).ToListAsync()).ToHashSet();
        var added = false;
        foreach (var def in AppSettingEntity.Definitions.All)
        {
            if (existing.Contains(def.Key)) continue;
            db.AppSettings.Add(new AppSettingEntity { Key = def.Key, Value = def.Default.ToString(CultureInfo.InvariantCulture) });
            added = true;
        }

        if (added) await db.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Não foi possível garantir as configurações padrão no banco.");
    }
}

async Task WaitForDatabaseAsync(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<EAFCContext>();

    var retries = 10;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < retries; i++)
    {
        try
        {
            if (await db.Database.CanConnectAsync())
            {
                logger.LogInformation("Conectado ao banco de dados.");
                return;
            }
        }
        catch (Exception)
        {
            logger.LogWarning("Falha ao conectar ao banco, tentando novamente...");
        }
        await Task.Delay(delay);
    }
    throw new Exception("Não foi possível conectar ao banco de dados após múltiplas tentativas.");
}
