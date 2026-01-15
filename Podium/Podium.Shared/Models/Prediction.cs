namespace Podium.Shared.Models;

public class Prediction
{
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
}
