using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using ArenaBackend.Configs;
using ArenaBackend.Repositories;
using ArenaBackend.Services;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IRiotApiService, RiotApiService>();
builder.Services.AddScoped<IPdlHandlerService, PdlHandlerService>();
builder.Services.AddSingleton<IScheduleService, ScheduleService>();
builder.Services.AddHostedService<PdlUpdateHostedService>();

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

// Configure middleware

app.UseCors("CorsPolicy");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
