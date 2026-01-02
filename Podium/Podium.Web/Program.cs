using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Podium.Shared;
using Podium.Shared.Services.Api;
using Podium.Shared.Services.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API client
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:50242";
Console.WriteLine($"Configuring API client with base URL: {apiBaseUrl}");

builder.Services.AddScoped(sp => 
{
    var httpClient = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
    Console.WriteLine($"HttpClient created with BaseAddress: {httpClient.BaseAddress}");
    return httpClient;
});

builder.Services.AddScoped<IPodiumApiClient, PodiumApiClient>();

// Add state management
builder.Services.AddSingleton<AuthStateService>();

await builder.Build().RunAsync();
