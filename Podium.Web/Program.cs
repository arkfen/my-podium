using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Podium.Web;
using Podium.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Get connection string from configuration
var connectionString = builder.Configuration["AzureStorage:ConnectionString"] ?? string.Empty;

// Register Podium services
builder.Services.AddSingleton<ITableStorageService>(sp => new TableStorageService(connectionString));
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ISportService, SportService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IResultsService, ResultsService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();

await builder.Build().RunAsync();
