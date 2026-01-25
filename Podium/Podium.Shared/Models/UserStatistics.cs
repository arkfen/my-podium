namespace Podium.Shared.Models;

public class UserStatistics
{
    public string SeasonId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int? BestResultsPoints { get; set; }
    public int TotalPoints { get; set; }
    public int PredictionsCount { get; set; }
    public int ExactMatches { get; set; }
    public int OneOffMatches { get; set; }
    public int TwoOffMatches { get; set; }
    public DateTime LastUpdated { get; set; }
}
