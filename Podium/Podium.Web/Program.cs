using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Podium.Shared;
using Podium.Shared.Services.Api;
using Podium.Shared.Services.State;
using Podium.Shared.Services.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure app settings
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:50242";
var isDevelopment = builder.HostEnvironment.IsDevelopment();

var appConfig = new AppConfiguration 
{ 
    ApiBaseUrl = apiBaseUrl,
    IsDevelopment = isDevelopment
};

builder.Services.AddSingleton<IAppConfiguration>(appConfig);

// Configure HttpClient with API base URL
builder.Services.AddScoped(sp => 
{
    var config = sp.GetRequiredService<IAppConfiguration>();
    return new HttpClient { BaseAddress = new Uri(config.ApiBaseUrl) };
});

builder.Services.AddScoped<IPodiumApiClient, PodiumApiClient>();

// Add storage service for session persistence
builder.Services.AddScoped<IStorageService, BrowserStorageService>();

// Add state management with storage
builder.Services.AddScoped<AuthStateService>(sp =>
{
    var storageService = sp.GetRequiredService<IStorageService>();
    return new AuthStateService(storageService);
});

var host = builder.Build();

// Initialize auth state (restore session from storage)
var authState = host.Services.GetRequiredService<AuthStateService>();
await authState.InitializeAsync();

await host.RunAsync();
