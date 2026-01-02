using Podium.Shared.Services.Data;
using Podium.Shared.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for web and mobile apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowPodiumClients", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7001", // Web app in dev
            "http://localhost:5000"   // Web app in dev
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowPodiumClients");

// Map API endpoints
app.MapAuthEndpoints();
app.MapSportEndpoints();
app.MapPredictionEndpoints();
app.MapLeaderboardEndpoints();

app.Run();
