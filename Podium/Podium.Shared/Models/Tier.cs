namespace Podium.Shared.Models;

public class Tier
{
    public string Id { get; set; } = string.Empty;
    public string SportId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
