namespace Podium.Shared.Models;

public class Event
{
    public string Id { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int EventNumber { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = "Upcoming"; // "Upcoming", "InProgress", "Completed"
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }

    public bool CanAcceptPredictions => Status == "Upcoming" && EventDate > DateTime.UtcNow && IsActive;
}
