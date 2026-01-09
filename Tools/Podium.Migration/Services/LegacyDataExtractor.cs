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
                var race = new LegacyRace
                {
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                    Year = entity.GetInt32("Year") ?? 0,
                    NumberRace = entity.GetInt32("NumberRace") ?? entity.GetInt32("Number") ?? 0,
                    NumberGP = entity.GetDouble("NumberGP"),
                    Name = entity.GetString("Name") ?? string.Empty,
                    Location = entity.GetString("Location"),
                    Date = entity.GetDateTimeOffset("Date")?.DateTime
                };
                races.Add(race);
            }
            
            // Sort by race number
            races = races.OrderBy(r => r.NumberRace).ToList();
            Console.WriteLine($"? Extracted {races.Count} races for {year}");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumRaces table not found");
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
            
            await foreach (var entity in queryResults)
            {
                var prediction = new LegacyPrediction
                {
                    PartitionKey = entity.PartitionKey,
                    RowKey = entity.RowKey,
                    UserId = entity.GetString("UserId") ?? string.Empty,
                    Year = entity.GetInt32("Year") ?? 0,
                    Race = entity.GetInt32("Race") ?? 0,
                    P1 = entity.GetString("P1") ?? string.Empty,
                    P2 = entity.GetString("P2") ?? string.Empty,
                    P3 = entity.GetString("P3") ?? string.Empty,
                    Points = entity.GetInt32("Points"),
                    SubmittedDate = entity.GetDateTimeOffset("SubmittedDate")?.DateTime ?? 
                                   entity.GetDateTimeOffset("Timestamp")?.DateTime
                };
                predictions.Add(prediction);
            }
            
            Console.WriteLine($"? Extracted {predictions.Count} predictions for {year}");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            Console.WriteLine("? MyPodiumDreams table not found");
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
}
