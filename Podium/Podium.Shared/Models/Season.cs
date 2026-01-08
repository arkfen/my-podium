namespace Podium.Shared.Models;

public class Season
{
    public string Id { get; set; } = string.Empty;
    public string SeriesId { get; set; } = string.Empty;
    public int Year { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
