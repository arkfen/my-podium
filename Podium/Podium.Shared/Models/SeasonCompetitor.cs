namespace Podium.Shared.Models;

public class SeasonCompetitor
{
    public string SeasonId { get; set; } = string.Empty;
    public string CompetitorId { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public DateTime JoinDate { get; set; }
}
