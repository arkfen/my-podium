namespace Podium.Shared.Models;

public class SeasonDependencies
{
    public int EventCount { get; set; }
    public int CompetitorCount { get; set; }
    public bool HasDependencies => EventCount > 0 || CompetitorCount > 0;
}
