namespace Podium.Shared.Models;

public class FavoriteSeason
{
    public string UserId { get; set; } = string.Empty;
    public string SeasonId { get; set; } = string.Empty;
    public string SeasonName { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime AddedDate { get; set; }
}
