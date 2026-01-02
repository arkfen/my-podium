namespace Podium.Shared.Models;

public class EventResult
{
    public string EventId { get; set; } = string.Empty;
    public string FirstPlaceId { get; set; } = string.Empty;
    public string FirstPlaceName { get; set; } = string.Empty;
    public string SecondPlaceId { get; set; } = string.Empty;
    public string SecondPlaceName { get; set; } = string.Empty;
    public string ThirdPlaceId { get; set; } = string.Empty;
    public string ThirdPlaceName { get; set; } = string.Empty;
    public DateTime UpdatedDate { get; set; }
}
