namespace Podium.Migration.Data;

/// <summary>
/// F1 driver lineups for 2024 and 2025 seasons
/// </summary>
public static class F1DriverLineups
{
    /// <summary>
    /// Drivers who competed in F1 2024 season
    /// </summary>
    public static readonly Dictionary<string, DateTime> Drivers2024 = new()
    {
        // Red Bull Racing
        ["Max Verstappen"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Sergio Perez"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Ferrari
        ["Charles Leclerc"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Carlos Sainz"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Mercedes
        ["Lewis Hamilton"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["George Russell"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // McLaren
        ["Lando Norris"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Oscar Piastri"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Aston Martin
        ["Fernando Alonso"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Lance Stroll"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Alpine
        ["Pierre Gasly"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Esteban Ocon"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Williams
        ["Alexander Albon"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Logan Sargeant"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Franco Colapinto"] = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc), // Replaced Sargeant mid-season
        
        // RB (AlphaTauri/Toro Rosso)
        ["Yuki Tsunoda"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Daniel Ricciardo"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Liam Lawson"] = new DateTime(2024, 10, 1, 0, 0, 0, DateTimeKind.Utc), // Replaced Ricciardo
        
        // Stake F1 (Alfa Romeo/Sauber)
        ["Valtteri Bottas"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Zhou Guanyu"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Haas
        ["Kevin Magnussen"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Nico Hulkenberg"] = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Reserve/Practice drivers who participated
        ["Oliver Bearman"] = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), // Ferrari/Haas substitute
    };

    /// <summary>
    /// Drivers who compete in F1 2025 season
    /// </summary>
    public static readonly Dictionary<string, DateTime> Drivers2025 = new()
    {
        // Red Bull Racing
        ["Max Verstappen"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Liam Lawson"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Ferrari
        ["Charles Leclerc"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Lewis Hamilton"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Mercedes
        ["George Russell"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Andrea Kimi Antonelli"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Kimi Antonelli"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), // Alternative name
        
        // McLaren
        ["Lando Norris"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Oscar Piastri"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Aston Martin
        ["Fernando Alonso"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Lance Stroll"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Alpine
        ["Pierre Gasly"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Jack Doohan"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Williams
        ["Alexander Albon"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Carlos Sainz"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // RB (Visa Cash App RB)
        ["Yuki Tsunoda"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Isack Hadjar"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Stake F1 (Sauber)
        ["Nico Hulkenberg"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Gabriel Bortoleto"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        
        // Haas
        ["Oliver Bearman"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ["Esteban Ocon"] = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// Get join date for a driver in a specific season
    /// </summary>
    public static DateTime? GetDriverJoinDate(int year, string driverName)
    {
        var lineup = year == 2024 ? Drivers2024 : year == 2025 ? Drivers2025 : null;
        
        if (lineup == null)
            return null;
            
        // Try exact match first
        if (lineup.TryGetValue(driverName, out var joinDate))
            return joinDate;
            
        // Try case-insensitive match
        var match = lineup.FirstOrDefault(kvp => 
            string.Equals(kvp.Key, driverName, StringComparison.OrdinalIgnoreCase));
            
        return match.Key != null ? match.Value : null;
    }

    /// <summary>
    /// Check if a driver raced in a specific season
    /// </summary>
    public static bool DriverRacedInSeason(int year, string driverName)
    {
        return GetDriverJoinDate(year, driverName) != null;
    }

    /// <summary>
    /// Get all drivers for a specific season
    /// </summary>
    public static IEnumerable<(string Name, DateTime JoinDate)> GetDriversForSeason(int year)
    {
        var lineup = year == 2024 ? Drivers2024 : year == 2025 ? Drivers2025 : null;
        
        if (lineup == null)
            return Enumerable.Empty<(string, DateTime)>();
            
        return lineup.Select(kvp => (kvp.Key, kvp.Value));
    }
}
