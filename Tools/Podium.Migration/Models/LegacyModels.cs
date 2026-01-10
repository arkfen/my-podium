namespace Podium.Migration.Models;

/// <summary>
/// Represents a user from MyPodiumUsers table
/// </summary>
public class LegacyUser
{
    public string Id { get; set; } = string.Empty;
    public string? UsrId { get; set; } // Some users might have this field
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
}

/// <summary>
/// Represents a race from MyPodiumRaces table
/// </summary>
public class LegacyRace
{
    public string PartitionKey { get; set; } = string.Empty; // "F1"
    public string RowKey { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Day { get; set; } // Day of the event
    public int? Month { get; set; } // Month of the event
    public int NumberRace { get; set; } // Race number (used for ordering)
    public double? NumberGP { get; set; } // GP number (differs from race number when sprints exist)
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime? Date { get; set; } // Calculated from Year, Month, Day or from Date field
    public string? P1 { get; set; } // Actual result - 1st place
    public string? P2 { get; set; } // Actual result - 2nd place
    public string? P3 { get; set; } // Actual result - 3rd place
}

/// <summary>
/// Represents a prediction from MyPodiumDreams table
/// </summary>
public class LegacyPrediction
{
    public string PartitionKey { get; set; } = string.Empty; // "F1"
    public string RowKey { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Race { get; set; }
    public string P1 { get; set; } = string.Empty;
    public string P2 { get; set; } = string.Empty;
    public string P3 { get; set; } = string.Empty;
    public int? Points { get; set; }
    public DateTime? SubmittedDate { get; set; }
}

/// <summary>
/// Represents a driver from MyPodiumDrivers table (if needed)
/// </summary>
public class LegacyDriver
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
}
