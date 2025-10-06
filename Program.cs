using EAFCMatchTracker.Infrastructure.Http;
using EAFCMatchTracker.Services;
using EAFCMatchTracker.Services.Interfaces;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<ClubMatchBackgroundService>();
builder.Services.AddScoped<IClubMatchService, ClubMatchService>();

builder.Services.AddHttpClient<IEAHttpClient, EAHttpClient>()
    .ConfigureHttpClient((sp, client) =>
    {
        var cfg = sp.GetRequiredService<IConfiguration>();
        var baseUrl = cfg["EAFCSettings:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(60);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EAFCMatchTracker/1.0");
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
        new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

builder.Services.Configure<EAFCSettings>(builder.Configuration.GetSection("EAFCSettings"));

Console.WriteLine("ConnectionString:");
Console.WriteLine(builder.Configuration.GetConnectionString("Default") ?? "NULO ou n�o encontrada");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
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

var app = builder.Build();

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

var locOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (CultureNotFoundException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Invalid culture",
            message = ex.Message
        });
    }
});

AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
{
    if (eventArgs.Exception is CultureNotFoundException cex)
    {
        Console.WriteLine($"[Culture Guard] CultureNotFoundException capturada: {cex.Message}");
    }
};

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
    throw new Exception("N�o foi poss�vel conectar ao banco de dados ap�s m�ltiplas tentativas.");
}
