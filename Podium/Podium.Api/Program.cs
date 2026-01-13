using Podium.Shared.Services.Data;
using Podium.Shared.Services.Auth;
using Podium.Api.Endpoints;
using Podium.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();

// Configure CORS for web and mobile apps
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Development: Allow all origins for testing with mobile devices and local dev
        options.AddPolicy("AllowPodiumClients", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    }
    else
    {
        // Production: Allow specific domains
        options.AddPolicy("AllowPodiumClients", policy =>
        {
            policy.WithOrigins(
                "https://yourdomain.com",           // Your production web app domain
                "https://www.yourdomain.com"        // www variant
                // Add more production domains as needed
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
    }
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

// Register Email Service
var smtpServer = builder.Configuration["EmailSettings:SmtpServer"];
var smtpPort = builder.Configuration.GetValue<int>("EmailSettings:SmtpPort", 587);
var smtpUsername = builder.Configuration["EmailSettings:Username"];
var smtpPassword = builder.Configuration["EmailSettings:Password"];
var senderEmail = builder.Configuration["EmailSettings:SenderEmail"];
var senderName = builder.Configuration["EmailSettings:SenderName"] ?? "Podium";

if (!string.IsNullOrEmpty(smtpServer) && !string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
{
    builder.Services.AddScoped<IEmailService>(sp => 
        new EmailService(smtpServer, smtpPort, smtpUsername, smtpPassword, senderEmail ?? smtpUsername, senderName));
    Console.WriteLine("? Email service configured");
}
else
{
    Console.WriteLine("? Email service not configured - OTP codes will be logged to console only");
}

// Register repositories
builder.Services.AddScoped<IDisciplineRepository, DisciplineRepository>();
builder.Services.AddScoped<ISeriesRepository, SeriesRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ICompetitorRepository, CompetitorRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();

// Register authentication services with email callback
builder.Services.AddScoped<IAuthenticationService>(sp =>
{
    var tableClientFactory = sp.GetRequiredService<ITableClientFactory>();
    var emailService = sp.GetService<IEmailService>();
    
    Action<string, string>? emailCallback = null;
    if (emailService != null)
    {
        emailCallback = (email, code) => 
        {
            _ = emailService.SendVerificationEmailAsync(email, code);
        };
    }
    
    return new AuthenticationService(tableClientFactory, emailCallback);
});

builder.Services.AddScoped<IRegistrationService, RegistrationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseHttpsRedirection();
app.UseCors("AllowPodiumClients");

// Add a simple health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, environment = app.Environment.EnvironmentName }))
    .WithName("HealthCheck");

// Map API endpoints
app.MapAuthEndpoints();
app.MapSportEndpoints();
app.MapPredictionEndpoints();
app.MapLeaderboardEndpoints();
app.MapAdminEndpoints();

app.Run();
