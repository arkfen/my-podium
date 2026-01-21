namespace Podium.Shared.Models;

public class PredictionWithDetails
{
    // Prediction data
    public string EventId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FirstPlaceId { get; set; } = string.Empty;
    public string FirstPlaceName { get; set; } = string.Empty;
    public string SecondPlaceId { get; set; } = string.Empty;
    public string SecondPlaceName { get; set; } = string.Empty;
    public string ThirdPlaceId { get; set; } = string.Empty;
    public string ThirdPlaceName { get; set; } = string.Empty;
    public int? PointsEarned { get; set; }
    public DateTime SubmittedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    // Event information
    public string EventName { get; set; } = string.Empty;
    public string EventDisplayName { get; set; } = string.Empty;
    public int EventNumber { get; set; }
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = string.Empty;
    public string EventStatus { get; set; } = string.Empty; // "Upcoming", "InProgress", "Completed"

    // Season information
    public string SeasonId { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public int SeasonYear { get; set; }
    public bool SeasonIsActive { get; set; }

    // Series information
    public string SeriesId { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string SeriesDisplayName { get; set; } = string.Empty;

    // Discipline information
    public string DisciplineId { get; set; } = string.Empty;
    public string DisciplineName { get; set; } = string.Empty;
    public string DisciplineDisplayName { get; set; } = string.Empty;

    // Actual event results (if event is completed)
    public string? ActualFirstPlaceId { get; set; }
    public string? ActualFirstPlaceName { get; set; }
    public string? ActualSecondPlaceId { get; set; }
    public string? ActualSecondPlaceName { get; set; }
    public string? ActualThirdPlaceId { get; set; }
    public string? ActualThirdPlaceName { get; set; }
}
