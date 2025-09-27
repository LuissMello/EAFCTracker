using EAFCMatchTracker.Interfaces;
using EAFCMatchTracker.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;   // NEW
using Microsoft.AspNetCore.Localization; // NEW
using Microsoft.Extensions.Options;      // NEW

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<ClubMatchService>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    client.Timeout = TimeSpan.FromMinutes(2);
})
.ConfigurePrimaryHttpMessageHandler(() =>
    new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddHostedService<ClubMatchBackgroundService>();

builder.Services.Configure<EAFCSettings>(builder.Configuration.GetSection("EAFCSettings"));
builder.Services.AddSingleton<IEAFCService, EAFCService>();

Console.WriteLine("ConnectionString:");
Console.WriteLine(builder.Configuration.GetConnectionString("Default") ?? "NULO ou não encontrada");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

// ===== CULTURE / LOCALIZATION GUARD (NEW) =====
var supportedCultureNames = new[] { "pt-BR", "en-US" }; // ajuste a lista conforme necessário
var supportedCultures = supportedCultureNames.Select(c => new CultureInfo(c)).ToList();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("pt-BR", "pt-BR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Você pode escolher provedores conforme sua necessidade:
    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };

    // Em versões novas do ASP.NET Core isso já é padrão,
    // mas a ideia é NUNCA deixar uma culture fora do whitelist passar.
});

// ===== DB =====
builder.Services.AddDbContext<EAFCContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
    });

    options.EnableSensitiveDataLogging();
    options.LogTo(Console.WriteLine, LogLevel.Information);
});

builder.Services.AddScoped<IClubMatchService, ClubMatchService>();

// ===== BUILD APP =====
var app = builder.Build();

// Define cultura padrão de forma global (threads novas herdarem) — NEW
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

// Aplica a request localization com whitelist — NEW
var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

// Middleware de guarda p/ CultureNotFoundException — NEW
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (CultureNotFoundException ex)
    {
        // Se alguém enviar algo como "ldlimitedforclub>b__3_18",
        // a gente responde 400 em vez de estourar a aplicação.
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Invalid culture",
            message = ex.Message
        });
    }
});

// (Opcional) Logar exceptions de cultura o mais cedo possível — NEW
AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
{
    if (eventArgs.Exception is CultureNotFoundException cex)
    {
        Console.WriteLine($"[Culture Guard] CultureNotFoundException capturada: {cex.Message}");
    }
};

// ===== MIGRATIONS / DB READY =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await WaitForDatabaseAsync(services);

    var db = services.GetRequiredService<EAFCContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowReactApp");

app.UseAuthorization();
app.MapControllers();

app.Run();

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
