namespace Podium.Shared.Models;

public class StatisticsRecalculationJob
{
    public string JobId { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed
    public int TotalUsers { get; set; }
    public int ProcessedUsers { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
