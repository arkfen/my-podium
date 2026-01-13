namespace Podium.Shared.Models;

public class UserDependencies
{
    public string UserId { get; set; } = string.Empty;
    public int PredictionCount { get; set; }
    public bool IsAdmin { get; set; }
    public bool HasDependencies => PredictionCount > 0 || IsAdmin;
}
