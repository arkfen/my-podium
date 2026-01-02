using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Podium.Shared;
using Podium.Shared.Services.Api;
using Podium.Shared.Services.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API client
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7002";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<IPodiumApiClient, PodiumApiClient>();

// Add state management
builder.Services.AddSingleton<AuthStateService>();

await builder.Build().RunAsync();
