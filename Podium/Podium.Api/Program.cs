using Podium.Shared.Services.Data;
using Podium.Shared.Services.Auth;
using Podium.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();

// Configure CORS for web and mobile apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPodiumClients", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7001",  // Web app in dev (common port)
            "https://localhost:5001",  // Web app in dev (alternative)
            "http://localhost:5000",   // Web app in dev (HTTP)
            "https://localhost:7002",  // Web app in dev (alternative)
            "http://localhost:5002"    // Web app in dev (HTTP alternative)
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Register Azure Storage factory
var storageUri = builder.Configuration["AzureStorage:StorageUri"] 
    ?? throw new InvalidOperationException("AzureStorage:StorageUri not configured");
var accountName = builder.Configuration["AzureStorage:AccountName"] 
    ?? throw new InvalidOperationException("AzureStorage:AccountName not configured");
var accountKey = builder.Configuration["AzureStorage:AccountKey"] 
    ?? throw new InvalidOperationException("AzureStorage:AccountKey not configured");

builder.Services.AddSingleton<ITableClientFactory>(
    new TableClientFactory(storageUri, accountName, accountKey));

// Register repositories
builder.Services.AddScoped<ISportRepository, SportRepository>();
builder.Services.AddScoped<ITierRepository, TierRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ICompetitorRepository, CompetitorRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();

// Register authentication services
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseCors("AllowPodiumClients");

// Add a simple health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// Map API endpoints
app.MapAuthEndpoints();
app.MapSportEndpoints();
app.MapPredictionEndpoints();
app.MapLeaderboardEndpoints();

app.Run();
