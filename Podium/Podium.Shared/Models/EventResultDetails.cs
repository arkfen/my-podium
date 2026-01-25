namespace Podium.Shared.Models;

/// <summary>
/// Detailed information about an event result including the actual podium and user predictions
/// </summary>
public class EventResultDetails
{
    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public int EventNumber { get; set; }
    public DateTime EventDate { get; set; }
    
    // Actual podium result
    public string FirstPlaceId { get; set; } = string.Empty;
    public string FirstPlaceName { get; set; } = string.Empty;
    public string SecondPlaceId { get; set; } = string.Empty;
    public string SecondPlaceName { get; set; } = string.Empty;
    public string ThirdPlaceId { get; set; } = string.Empty;
    public string ThirdPlaceName { get; set; } = string.Empty;
    
    public DateTime ResultUpdatedDate { get; set; }
    
    // Top user predictions for this event
    public List<UserEventPrediction> TopPredictions { get; set; } = new();
}

/// <summary>
/// Represents a user's prediction and points for a specific event
/// </summary>
public class UserEventPrediction
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // User's prediction
    public string FirstPlaceId { get; set; } = string.Empty;
    public string FirstPlaceName { get; set; } = string.Empty;
    public string SecondPlaceId { get; set; } = string.Empty;
    public string SecondPlaceName { get; set; } = string.Empty;
    public string ThirdPlaceId { get; set; } = string.Empty;
    public string ThirdPlaceName { get; set; } = string.Empty;
    
    public int PointsEarned { get; set; }
    public DateTime SubmittedDate { get; set; }
}
