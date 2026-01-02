namespace Podium.Shared.Services.Configuration;

public interface IAppConfiguration
{
    string ApiBaseUrl { get; }
    bool IsDevelopment { get; }
}

public class AppConfiguration : IAppConfiguration
{
    public string ApiBaseUrl { get; set; } = "https://localhost:50242";
    public bool IsDevelopment { get; set; } = true;
}
