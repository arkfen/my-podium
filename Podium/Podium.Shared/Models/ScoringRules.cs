namespace Podium.Shared.Models;

public class ScoringRules
{
    public string SeasonId { get; set; } = string.Empty;
    public int ExactMatchPoints { get; set; }
    public int OneOffPoints { get; set; }
    public int TwoOffPoints { get; set; }
    public DateTime CreatedDate { get; set; }
}
