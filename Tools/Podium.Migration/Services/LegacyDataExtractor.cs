using Azure;
using Azure.Data.Tables;
using Podium.Migration.Models;

namespace Podium.Migration.Services;

/// <summary>
/// Service to extract data from legacy MyPodium tables
/// </summary>
public class LegacyDataExtractor
{
    private readonly TableServiceClient _tableServiceClient;

    public LegacyDataExtractor(string storageUri, string accountName, string accountKey)
    {
        var credential = new TableSharedKeyCredential(accountName, accountKey);
        _tableServiceClient = new TableServiceClient(new Uri(storageUri), credential);
    }

    /// <summary>
    /// Safely extract integer value from entity
    /// </summary>
    private int? SafeGetInt32(TableEntity entity, string propertyName)
    {
        if (!entity.ContainsKey(propertyName))
            return null;

        var value = entity[propertyName];
        
        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return (int)longValue;
        if (value is double doubleValue)
            return (int)doubleValue;
        if (int.TryParse(value?.ToString(), out var parsed))
            return parsed;
            
        return null;
    }

    /// <summary>
    /// Safely extract double value from entity
    /// </summary>
    private double? SafeGetDouble(TableEntity entity, string propertyName)
    {
        if (!entity.ContainsKey(propertyName))
            return null;

        var value = entity[propertyName];
        
        if (value is double doubleValue)
            return doubleValue;
        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return longValue;
        if (double.TryParse(value?.ToString(), out var parsed))
            return parsed;
            
        return null;
    }

    /// <summary>
    /// Safely extract DateTime value from entity and ensure it's UTC
    /// </summary>
    private DateTime? SafeGetDateTime(TableEntity entity, string propertyName)
    {
        if (!entity.ContainsKey(propertyName))
            return null;

        var value = entity[propertyName];
        DateTime? result = null;
        
        if (value is DateTimeOffset dto)
            result = dto.UtcDateTime;
        else if (value is DateTime dt)
            result = dt;
        else if (DateTime.TryParse(value?.ToString(), out var parsed))
            result = parsed;
        
        // Ensure the DateTime is UTC
        if (result.HasValue)
        {
            if (result.Value.Kind == DateTimeKind.Unspecified)
            {
                // Assume unspecified dates are UTC
                return DateTime.SpecifyKind(result.Value, DateTimeKind.Utc);
            }
            else if (result.Value.Kind == DateTimeKind.Local)
            {
                // Convert local time to UTC
                return result.Value.ToUniversalTime();
            }
            // Already UTC
            return result.Value;
        }
            
        return null;
    }

    /// <summary>
    /// Extract all users from MyPodiumUsers table
    /// </summary>
    public async Task<List<LegacyUser>> ExtractUsersAsync()
    {
        Console.WriteLine("Extracting users from MyPodiumUsers...");
        var users = new List<LegacyUser>();
        
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("MyPodiumUsers");
            var queryResults = tableClient.QueryAsync<TableEntity>();
            
            await foreach (var entity in queryResults)
            {
                var user = new LegacyUser
                {
                    Id = entity.GetString("Id") ?? string.Empty,
                    UsrId = entity.ContainsKey("UsrId") ? entity.GetString("UsrId") : null,
                    Name = entity.GetString("Name") ?? string.Empty,
                    Email = entity.GetString("Email") ?? string.Empty,
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey
                };
                users.Add(user);
            }
            
            Console.WriteLine($"? Extracted {users.Count} users");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumUsers table not found");
        }
        
        return users;
    }

    /// <summary>
    /// Extract F1 races for specific year
    /// </summary>
    public async Task<List<LegacyRace>> ExtractRacesAsync(int year)
    {
        Console.WriteLine($"Extracting F1 races for year {year}...");
        var races = new List<LegacyRace>();
        
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("MyPodiumRaces");
            var filter = $"PartitionKey eq 'F1' and Year eq {year}";
            var queryResults = tableClient.QueryAsync<TableEntity>(filter: filter);
            
            await foreach (var entity in queryResults)
            {
                try
                {
                    // For 2024: use "Number" field
                    // For 2025: use "NumberRace" field (with "NumberGP" being different for sprints)
                    int raceNumber = 0;
                    
                    if (year == 2024)
                    {
                        raceNumber = SafeGetInt32(entity, "Number") ?? 0;
                        Console.WriteLine($"  2024 Race: {entity.GetString("Name")}, Number={raceNumber}");
                    }
                    else // 2025 and beyond
                    {
                        raceNumber = SafeGetInt32(entity, "NumberRace") ?? SafeGetInt32(entity, "Number") ?? 0;
                        var numberGP = SafeGetDouble(entity, "NumberGP");
                        Console.WriteLine($"  2025 Race: {entity.GetString("Name")}, NumberRace={raceNumber}, NumberGP={numberGP}");
                    }
                    
                    // Extract date components (Day, Month, Year) or Date field
                    var day = SafeGetInt32(entity, "Day");
                    var month = SafeGetInt32(entity, "Month");
                    var eventYear = SafeGetInt32(entity, "Year") ?? year;
                    
                    // Construct DateTime from components if available
                    DateTime? eventDate = null;
                    if (day.HasValue && month.HasValue && day.Value > 0 && month.Value > 0)
                    {
                        try
                        {
                            // Create UTC datetime from components (assume midnight UTC as default time)
                            eventDate = new DateTime(eventYear, month.Value, day.Value, 0, 0, 0, DateTimeKind.Utc);
                            Console.WriteLine($"    Constructed date from components: {eventDate:yyyy-MM-dd}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    ? Invalid date components (Year={eventYear}, Month={month}, Day={day}): {ex.Message}");
                        }
                    }
                    
                    // Fallback to Date field if components not available or invalid
                    if (!eventDate.HasValue)
                    {
                        eventDate = SafeGetDateTime(entity, "Date");
                        if (eventDate.HasValue)
                        {
                            Console.WriteLine($"    Using Date field: {eventDate:yyyy-MM-dd}");
                        }
                    }
                    
                    var race = new LegacyRace
                    {
                        PartitionKey = entity.PartitionKey,
                        RowKey = entity.RowKey,
                        Year = eventYear,
                        Day = day,
                        Month = month,
                        NumberRace = raceNumber,
                        NumberGP = SafeGetDouble(entity, "NumberGP"),
                        Name = entity.GetString("Name") ?? string.Empty,
                        Location = entity.GetString("Location"),
                        Date = eventDate
                    };
                    races.Add(race);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ? Error processing race {entity.GetString("Name")}: {ex.Message}");
                }
            }
            
            // Sort by race number
            races = races.OrderBy(r => r.NumberRace).ToList();
            Console.WriteLine($"? Extracted {races.Count} races for {year}");
            
            if (races.Count == 0)
            {
                Console.WriteLine($"? WARNING: No races found for {year}. Check if data exists in MyPodiumRaces table.");
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumRaces table not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error extracting races: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
        
        return races;
    }

    /// <summary>
    /// Extract predictions for specific year
    /// </summary>
    public async Task<List<LegacyPrediction>> ExtractPredictionsAsync(int year)
    {
        Console.WriteLine($"Extracting predictions for year {year}...");
        var predictions = new List<LegacyPrediction>();
        
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("MyPodiumDreams");
            var filter = $"PartitionKey eq 'F1' and Year eq {year}";
            var queryResults = tableClient.QueryAsync<TableEntity>(filter: filter);
            
            var raceGroups = new Dictionary<int, int>();
            
            await foreach (var entity in queryResults)
            {
                try
                {
                    var raceNumber = SafeGetInt32(entity, "Race") ?? 0;
                    
                    var prediction = new LegacyPrediction
                    {
                        PartitionKey = entity.PartitionKey,
                        RowKey = entity.RowKey,
                        UserId = entity.GetString("UserId") ?? string.Empty,
                        Year = SafeGetInt32(entity, "Year") ?? year,
                        Race = raceNumber,
                        P1 = entity.GetString("P1") ?? string.Empty,
                        P2 = entity.GetString("P2") ?? string.Empty,
                        P3 = entity.GetString("P3") ?? string.Empty,
                        Points = SafeGetInt32(entity, "Points"),
                        SubmittedDate = SafeGetDateTime(entity, "SubmittedDate") ?? 
                                       SafeGetDateTime(entity, "Timestamp")
                    };
                    predictions.Add(prediction);
                    
                    // Count predictions per race
                    if (!raceGroups.ContainsKey(raceNumber))
                        raceGroups[raceNumber] = 0;
                    raceGroups[raceNumber]++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ? Error processing prediction: {ex.Message}");
                }
            }
            
            Console.WriteLine($"? Extracted {predictions.Count} predictions for {year}");
            if (raceGroups.Any())
            {
                Console.WriteLine($"  Predictions per race: {string.Join(", ", raceGroups.OrderBy(x => x.Key).Select(x => $"Race {x.Key}: {x.Value}"))}");
            }
            
            if (predictions.Count == 0)
            {
                Console.WriteLine($"? WARNING: No predictions found for {year}. Check if data exists in MyPodiumDreams table.");
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumDreams table not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error extracting predictions: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
        
        return predictions;
    }

    /// <summary>
    /// Extract all unique driver names from predictions
    /// </summary>
    public async Task<HashSet<string>> ExtractDriverNamesAsync(List<int> years)
    {
        Console.WriteLine("Extracting unique driver names from predictions...");
        var driverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var year in years)
        {
            var predictions = await ExtractPredictionsAsync(year);
            
            foreach (var prediction in predictions)
            {
                if (!string.IsNullOrWhiteSpace(prediction.P1))
                    driverNames.Add(prediction.P1.Trim());
                if (!string.IsNullOrWhiteSpace(prediction.P2))
                    driverNames.Add(prediction.P2.Trim());
                if (!string.IsNullOrWhiteSpace(prediction.P3))
                    driverNames.Add(prediction.P3.Trim());
            }
        }
        
        Console.WriteLine($"? Found {driverNames.Count} unique drivers");
        return driverNames;
    }

    /// <summary>
    /// Extract drivers from MyPodiumDrivers table if it exists
    /// </summary>
    public async Task<List<LegacyDriver>> ExtractDriversAsync()
    {
        Console.WriteLine("Extracting drivers from MyPodiumDrivers...");
        var drivers = new List<LegacyDriver>();
        
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("MyPodiumDrivers");
            var queryResults = tableClient.QueryAsync<TableEntity>();
            
            await foreach (var entity in queryResults)
            {
                var driver = new LegacyDriver
                {
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                    Name = entity.GetString("Name") ?? string.Empty,
                    ShortName = entity.GetString("ShortName")
                };
                drivers.Add(driver);
            }
            
            Console.WriteLine($"? Extracted {drivers.Count} drivers");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumDrivers table not found, will use names from predictions");
        }
        
        return drivers;
    }

    /// <summary>
    /// Extract race results for specific year from MyPodiumResults table
    /// </summary>
    public async Task<List<LegacyRaceResult>> ExtractRaceResultsAsync(int year)
    {
        Console.WriteLine($"Extracting race results for year {year}...");
        var results = new List<LegacyRaceResult>();
        
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("MyPodiumResults");
            var filter = $"PartitionKey eq 'F1' and Year eq {year}";
            var queryResults = tableClient.QueryAsync<TableEntity>(filter: filter);
            
            await foreach (var entity in queryResults)
            {
                try
                {
                    var raceNumber = SafeGetInt32(entity, "Race") ?? 0;
                    
                    var result = new LegacyRaceResult
                    {
                        PartitionKey = entity.PartitionKey,
                        RowKey = entity.RowKey,
                        Year = SafeGetInt32(entity, "Year") ?? year,
                        Race = raceNumber,
                        P1 = entity.GetString("P1") ?? string.Empty,
                        P2 = entity.GetString("P2") ?? string.Empty,
                        P3 = entity.GetString("P3") ?? string.Empty
                    };
                    results.Add(result);
                    
                    Console.WriteLine($"  Result for Race #{raceNumber}: 1st={result.P1}, 2nd={result.P2}, 3rd={result.P3}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ? Error processing result: {ex.Message}");
                }
            }
            
            Console.WriteLine($"? Extracted {results.Count} race results for {year}");
            
            if (results.Count == 0)
            {
                Console.WriteLine($"? WARNING: No results found for {year}. Check if data exists in MyPodiumResults table.");
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumResults table not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error extracting race results: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
        
        return results;
    }
}
