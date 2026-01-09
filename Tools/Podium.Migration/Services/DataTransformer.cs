using Podium.Migration.Models;
using System.Security.Cryptography;

namespace Podium.Migration.Services;

/// <summary>
/// Service to transform legacy data to new schema format
/// </summary>
public class DataTransformer
{
    // Fixed IDs for consistency
    private readonly string _disciplineId = Guid.NewGuid().ToString(); // Single-Seater Racing
    private readonly string _seriesId = Guid.NewGuid().ToString(); // Formula 1
    private readonly Dictionary<int, string> _seasonIds = new(); // Year -> SeasonId
    private readonly Dictionary<string, string> _userIdMap = new(); // Legacy UserId -> New UserId
    private readonly Dictionary<string, string> _driverIdMap = new(); // Driver Name -> CompetitorId
    private readonly Dictionary<string, string> _eventIdMap = new(); // Year_RaceNumber -> EventId

    public string DisciplineId => _disciplineId;
    public string SeriesId => _seriesId;

    /// <summary>
    /// Get or create season ID for a year
    /// </summary>
    public string GetOrCreateSeasonId(int year)
    {
        if (!_seasonIds.ContainsKey(year))
        {
            _seasonIds[year] = Guid.NewGuid().ToString();
        }
        return _seasonIds[year];
    }

    /// <summary>
    /// Get or create user ID mapping
    /// </summary>
    public string GetOrCreateUserId(string legacyUserId)
    {
        if (string.IsNullOrWhiteSpace(legacyUserId))
            return string.Empty;

        if (!_userIdMap.ContainsKey(legacyUserId))
        {
            _userIdMap[legacyUserId] = Guid.NewGuid().ToString();
        }
        return _userIdMap[legacyUserId];
    }

    /// <summary>
    /// Get or create driver ID mapping
    /// </summary>
    public string GetOrCreateDriverId(string driverName)
    {
        if (string.IsNullOrWhiteSpace(driverName))
            return string.Empty;

        var normalizedName = driverName.Trim();
        if (!_driverIdMap.ContainsKey(normalizedName))
        {
            _driverIdMap[normalizedName] = Guid.NewGuid().ToString();
        }
        return _driverIdMap[normalizedName];
    }

    /// <summary>
    /// Get or create event ID mapping
    /// </summary>
    public string GetOrCreateEventId(int year, int raceNumber)
    {
        var key = $"{year}_{raceNumber}";
        if (!_eventIdMap.ContainsKey(key))
        {
            _eventIdMap[key] = Guid.NewGuid().ToString();
        }
        return _eventIdMap[key];
    }

    /// <summary>
    /// Get mapped driver ID or empty string
    /// </summary>
    public string GetDriverId(string driverName)
    {
        if (string.IsNullOrWhiteSpace(driverName))
            return string.Empty;

        var normalizedName = driverName.Trim();
        return _driverIdMap.TryGetValue(normalizedName, out var id) ? id : string.Empty;
    }

    /// <summary>
    /// Generate short name from full name
    /// </summary>
    public string GenerateShortName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}. {parts[^1]}";
        }
        return fullName;
    }

    /// <summary>
    /// Calculate points based on scoring rules
    /// </summary>
    public int? CalculatePoints(
        string predictedP1, string predictedP2, string predictedP3,
        string actualP1, string actualP2, string actualP3,
        int exactMatchPoints = 25, int oneOffPoints = 18, int twoOffPoints = 15)
    {
        // If any actual result is missing, we can't calculate points
        if (string.IsNullOrWhiteSpace(actualP1) || 
            string.IsNullOrWhiteSpace(actualP2) || 
            string.IsNullOrWhiteSpace(actualP3))
            return null;

        // Normalize names for comparison
        var pred = new[] { 
            predictedP1?.Trim() ?? "", 
            predictedP2?.Trim() ?? "", 
            predictedP3?.Trim() ?? "" 
        };
        var actual = new[] { 
            actualP1.Trim(), 
            actualP2.Trim(), 
            actualP3.Trim() 
        };

        // Check for exact match
        if (string.Equals(pred[0], actual[0], StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pred[1], actual[1], StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pred[2], actual[2], StringComparison.OrdinalIgnoreCase))
        {
            return exactMatchPoints;
        }

        // Check if all predicted drivers are in actual top 3 (one position off logic)
        var predSet = new HashSet<string>(pred, StringComparer.OrdinalIgnoreCase);
        var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);
        
        // Count how many predicted drivers are in actual results
        int correctDrivers = predSet.Intersect(actualSet).Count();
        
        if (correctDrivers == 3)
        {
            // All 3 drivers correct, just positions off
            return oneOffPoints;
        }
        else if (correctDrivers == 2)
        {
            // 2 drivers correct
            return twoOffPoints;
        }

        // Less than 2 correct drivers = 0 points
        return 0;
    }

    /// <summary>
    /// Determine event status based on date
    /// </summary>
    public string DetermineEventStatus(DateTime? eventDate)
    {
        if (eventDate == null)
            return "Completed"; // Default for legacy data

        var now = DateTime.UtcNow;
        if (eventDate.Value > now)
            return "Upcoming";
        else if (eventDate.Value.Date == now.Date)
            return "InProgress";
        else
            return "Completed";
    }

    /// <summary>
    /// Hash password (not used for legacy users, but included for completeness)
    /// </summary>
    public (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        var salt = Convert.ToBase64String(saltBytes);

        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 10000, HashAlgorithmName.SHA256, 32);
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }

    /// <summary>
    /// Get all mapped data for reporting
    /// </summary>
    public void PrintMappingSummary()
    {
        Console.WriteLine("\n--- Data Mapping Summary ---");
        Console.WriteLine($"Discipline ID (Single-Seater Racing): {_disciplineId}");
        Console.WriteLine($"Series ID (Formula 1): {_seriesId}");
        Console.WriteLine($"Seasons: {_seasonIds.Count}");
        foreach (var kvp in _seasonIds.OrderBy(x => x.Key))
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine($"Users mapped: {_userIdMap.Count}");
        Console.WriteLine($"Drivers mapped: {_driverIdMap.Count}");
        Console.WriteLine($"Events mapped: {_eventIdMap.Count}");
    }
}
