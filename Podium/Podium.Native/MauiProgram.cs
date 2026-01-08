using Microsoft.Extensions.Logging;
using Podium.Shared.Services.Api;
using Podium.Shared.Services.State;
using Podium.Shared.Services.Configuration;

namespace Podium.Native
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
            
            // Development configuration
            var appConfig = new AppConfiguration 
            { 
                ApiBaseUrl = "https://localhost:50242",  // Use 10.0.2.2:50242 for Android emulator
                IsDevelopment = true
            };
#else
            // Production configuration
            var appConfig = new AppConfiguration 
            { 
                ApiBaseUrl = "https://api.yourproductiondomain.com",
                IsDevelopment = false
            };
#endif

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

            return builder.Build();
        }
    }
}
