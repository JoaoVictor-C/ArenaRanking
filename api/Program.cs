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

System.TimeZoneInfo.TryConvertIanaIdToWindowsId("America/Sao_Paulo", out var windowsTimeZoneId);
Environment.SetEnvironmentVariable("TZ", "America/Sao_Paulo");

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel para usar HTTPS com certificados PEM
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(System.Net.IPAddress.Any, 3002, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        
        // Caminho para os certificados
        var certPath = Path.Combine(builder.Environment.ContentRootPath, "certificado", "fullchain.pem");
        var keyPath = Path.Combine(builder.Environment.ContentRootPath, "certificado", "privkey.pem");
        
        // Verifica se os certificados existem
        if (File.Exists(certPath) && File.Exists(keyPath))
        {
            // Carrega os certificados PEM
            var certificate = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            listenOptions.UseHttps(certificate);
        }
    });
});

// Configure services
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection(nameof(MongoDbSettings)));

builder.Services.Configure<RiotApiSettings>(
    builder.Configuration.GetSection(nameof(RiotApiSettings)));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();
builder.Services.AddScoped<IOldPlayerRepository, OldPlayerRepository>();
builder.Services.AddScoped<IMigrateOldSystemService, MigrateOldSystemService>();
builder.Services.AddScoped<IRiotApiService, RiotApiService>();
builder.Services.AddScoped<IPdlHandlerService, PdlHandlerService>();
builder.Services.AddScoped<IRiotIdUpdateService, RiotIdUpdateService>();

builder.Services.AddSingleton<IScheduleService, ScheduleService>();

builder.Services.AddHostedService<RiotIdUpdateHostedService>();
// builder.Services.AddHostedService<PdlUpdateHostedService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddControllers();

// Build app
var app = builder.Build();

// Inicializar rankings na inicialização do aplicativo
using (var scope = app.Services.CreateScope())
{
    var playerRepository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
    await playerRepository.UpdateAllPlayerRankingsAsync();
}

// Configure middleware

app.UseCors("CorsPolicy");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
