namespace Podium.Shared.Models;

/// <summary>
/// Response containing the user's latest prediction with points from active seasons
/// </summary>
public class LatestScoredPredictionResponse
{
    public Prediction? Prediction { get; set; }
    public string? SeasonId { get; set; }
    public string? SeriesId { get; set; }
    public string? DisciplineId { get; set; }
}
