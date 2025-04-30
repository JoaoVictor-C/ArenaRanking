using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using ArenaBackend.Configs;
using ArenaBackend.Repositories;
using ArenaBackend.Services;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting;
using System;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using ArenaBackend.Factories;
using ArenaBackend.Services.Configuration;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var startupLogger = loggerFactory.CreateLogger<Program>();
DotEnvLoader.Load(startupLogger);

// Registrar o provedor de configuração
builder.Services.AddSingleton<IEnvironmentConfigProvider, EnvironmentConfigProvider>();

// Registrar serviços que dependem de configurações
builder.Services.AddScoped<IRiotApiKeyManager>(provider =>
{
    var configProvider = provider.GetRequiredService<IEnvironmentConfigProvider>();
    var logger = provider.GetRequiredService<ILogger<RiotApiKeyManager>>();
    return new RiotApiKeyManager(configProvider, logger);
});

builder.Services.AddSingleton<IMongoClient>(provider =>
{
    var configProvider = provider.GetRequiredService<IEnvironmentConfigProvider>();
    var mongoSettings = configProvider.GetMongoDbSettings();
    return new MongoClient(mongoSettings.ConnectionString);
});

builder.Services.AddSingleton<IRankingCacheService, RankingCacheService>();

builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IRiotApiService, RiotApiService>();
builder.Services.AddScoped<IPdlHandlerService, PdlHandlerService>();
builder.Services.AddScoped<IRiotIdUpdateService, RiotIdUpdateService>();
builder.Services.AddScoped<IPdlRecalculationService, PdlRecalculationService>();
builder.Services.AddScoped<ISetRegionService, SetRegionService>();
builder.Services.AddScoped<DatabaseMigrationService>();
builder.Services.AddScoped<DatabaseCloneService>();

builder.Services.AddHostedService<RankingCacheUpdateHostedService>();
builder.Services.AddHostedService<RiotIdUpdateHostedService>();
builder.Services.AddHostedService<PdlUpdateHostedService>();

builder.Services.AddHttpClient("RiotApi", client =>
{
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddSingleton<IRepositoryFactory, RepositoryFactory>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors("CorsPolicy");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Lifetime.ApplicationStarted.Register(async () => 
{
    using (var scope = app.Services.CreateScope())
    {
        var playerRepository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
        try
        {
            await playerRepository.UpdateAllPlayerRankingsAsync();
            Console.WriteLine("Rankings inicializados com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao inicializar rankings: {ex.Message}");
        }
    }
});


using (var scope = app.Services.CreateScope())
{
    var configProvider = scope.ServiceProvider.GetRequiredService<IEnvironmentConfigProvider>();
    var dbSettings = configProvider.GetMongoDbSettings();
    var shouldCloneDatabase = dbSettings.IsDevelopment;

    if (shouldCloneDatabase)
    {
        var dbCloneService = scope.ServiceProvider.GetRequiredService<DatabaseCloneService>();
        await dbCloneService.CloneProductionToTest();
        Console.WriteLine("Database clone completed successfully.");
    }
    else
    {
        Console.WriteLine("Database clone skipped based on configuration.");
    }
}

// using (var scope = app.Services.CreateScope())
// {
//     var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseMigrationService>();
//     await migrationService.MigrateRegionFields();
//     await migrationService.MigrateRecentGamesField();
// }

app.Run();