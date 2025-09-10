using EAFCMatchTracker.Interfaces;
using EAFCMatchTracker.Services;
using Microsoft.EntityFrameworkCore;
using System;

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
        catch (Exception ex)
        {
            logger.LogWarning("Falha ao conectar ao banco, tentando novamente...");
        }
        await Task.Delay(delay);
    }
    throw new Exception("Não foi possível conectar ao banco de dados após múltiplas tentativas.");
}