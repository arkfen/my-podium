namespace Podium.Shared.Models;

public class Series
{
    public string Id { get; set; } = string.Empty;
    public string DisciplineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GoverningBody { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}
