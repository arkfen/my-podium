namespace Podium.Shared.Models;

public class Competitor
{
    public string Id { get; set; } = string.Empty;
    public string SportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Type { get; set; } = "Individual"; // "Individual" or "Team"
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
