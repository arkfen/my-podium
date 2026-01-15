namespace Podium.Shared.Models;

public class CompetitorDependencies
{
    public int SeasonCount { get; set; }
    public int ResultCount { get; set; }
    public bool HasDependencies => SeasonCount > 0 || ResultCount > 0;
}
