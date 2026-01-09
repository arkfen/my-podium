using Azure.Data.Tables;

namespace Podium.Migration;

/// <summary>
/// Helper to diagnose what's in the legacy tables
/// </summary>
public static class DiagnosticHelper
{
    public static async Task DiagnoseTablesAsync(string storageUri, string accountName, string accountKey)
    {
        var credential = new TableSharedKeyCredential(accountName, accountKey);
        var tableServiceClient = new TableServiceClient(new Uri(storageUri), credential);

        Console.WriteLine("\n??????????????????????????????????????????????????????????");
        Console.WriteLine("?              DATABASE DIAGNOSTICS                      ?");
        Console.WriteLine("??????????????????????????????????????????????????????????\n");

        // Check MyPodiumRaces
        await DiagnoseRacesTable(tableServiceClient);
        
        // Check MyPodiumDreams
        await DiagnoseDreamsTable(tableServiceClient);
        
        // Check MyPodiumUsers
        await DiagnoseUsersTable(tableServiceClient);
    }

    private static async Task DiagnoseRacesTable(TableServiceClient serviceClient)
    {
        Console.WriteLine("--- MyPodiumRaces Table ---");
        try
        {
            var tableClient = serviceClient.GetTableClient("MyPodiumRaces");
            var query2024 = tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'F1' and Year eq 2024");
            var query2025 = tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'F1' and Year eq 2025");
            
            var races2024 = new List<TableEntity>();
            var races2025 = new List<TableEntity>();
            
            await foreach (var entity in query2024)
            {
                races2024.Add(entity);
            }
            
            await foreach (var entity in query2025)
            {
                races2025.Add(entity);
            }
            
            Console.WriteLine($"2024 Races: {races2024.Count}");
            if (races2024.Any())
            {
                var firstRace = races2024.First();
                Console.WriteLine($"  Sample 2024 race properties: {string.Join(", ", firstRace.Keys)}");
                Console.WriteLine($"  Has 'Number': {firstRace.ContainsKey("Number")}");
                Console.WriteLine($"  Has 'NumberRace': {firstRace.ContainsKey("NumberRace")}");
                Console.WriteLine($"  Has 'NumberGP': {firstRace.ContainsKey("NumberGP")}");
                
                if (firstRace.ContainsKey("Number"))
                    Console.WriteLine($"  Sample Number value: {firstRace.GetInt32("Number")}");
                if (firstRace.ContainsKey("NumberRace"))
                    Console.WriteLine($"  Sample NumberRace value: {firstRace.GetInt32("NumberRace")}");
                    
                Console.WriteLine($"  Sample race: {firstRace.GetString("Name")}");
            }
            
            Console.WriteLine($"2025 Races: {races2025.Count}");
            if (races2025.Any())
            {
                var firstRace = races2025.First();
                Console.WriteLine($"  Sample 2025 race properties: {string.Join(", ", firstRace.Keys)}");
                Console.WriteLine($"  Has 'Number': {firstRace.ContainsKey("Number")}");
                Console.WriteLine($"  Has 'NumberRace': {firstRace.ContainsKey("NumberRace")}");
                Console.WriteLine($"  Has 'NumberGP': {firstRace.ContainsKey("NumberGP")}");
                
                if (firstRace.ContainsKey("NumberRace"))
                    Console.WriteLine($"  Sample NumberRace value: {firstRace.GetInt32("NumberRace")}");
                if (firstRace.ContainsKey("NumberGP"))
                    Console.WriteLine($"  Sample NumberGP value: {firstRace.GetDouble("NumberGP")}");
                    
                Console.WriteLine($"  Sample race: {firstRace.GetString("Name")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task DiagnoseDreamsTable(TableServiceClient serviceClient)
    {
        Console.WriteLine("--- MyPodiumDreams Table ---");
        try
        {
            var tableClient = serviceClient.GetTableClient("MyPodiumDreams");
            var query2024 = tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'F1' and Year eq 2024");
            var query2025 = tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'F1' and Year eq 2025");
            
            var dreams2024 = new List<TableEntity>();
            var dreams2025 = new List<TableEntity>();
            
            await foreach (var entity in query2024)
            {
                dreams2024.Add(entity);
            }
            
            await foreach (var entity in query2025)
            {
                dreams2025.Add(entity);
            }
            
            Console.WriteLine($"2024 Predictions: {dreams2024.Count}");
            if (dreams2024.Any())
            {
                var firstDream = dreams2024.First();
                Console.WriteLine($"  Sample properties: {string.Join(", ", firstDream.Keys)}");
                Console.WriteLine($"  Sample: User={firstDream.GetString("UserId")}, Race={firstDream.GetInt32("Race")}, Points={firstDream.GetInt32("Points")}");
                Console.WriteLine($"  P1={firstDream.GetString("P1")}, P2={firstDream.GetString("P2")}, P3={firstDream.GetString("P3")}");
            }
            
            Console.WriteLine($"2025 Predictions: {dreams2025.Count}");
            if (dreams2025.Any())
            {
                var firstDream = dreams2025.First();
                Console.WriteLine($"  Sample: User={firstDream.GetString("UserId")}, Race={firstDream.GetInt32("Race")}, Points={firstDream.GetInt32("Points")}");
                Console.WriteLine($"  P1={firstDream.GetString("P1")}, P2={firstDream.GetString("P2")}, P3={firstDream.GetString("P3")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error: {ex.Message}");
        }
        Console.WriteLine();
    }

    private static async Task DiagnoseUsersTable(TableServiceClient serviceClient)
    {
        Console.WriteLine("--- MyPodiumUsers Table ---");
        try
        {
            var tableClient = serviceClient.GetTableClient("MyPodiumUsers");
            var queryResults = tableClient.QueryAsync<TableEntity>();
            
            var users = new List<TableEntity>();
            await foreach (var entity in queryResults)
            {
                users.Add(entity);
            }
            
            Console.WriteLine($"Total Users: {users.Count}");
            if (users.Any())
            {
                var firstUser = users.First();
                Console.WriteLine($"  Sample properties: {string.Join(", ", firstUser.Keys)}");
                Console.WriteLine($"  Has 'Id': {firstUser.ContainsKey("Id")}");
                Console.WriteLine($"  Has 'UsrId': {firstUser.ContainsKey("UsrId")}");
                Console.WriteLine($"  Sample: Name={firstUser.GetString("Name")}, Email={firstUser.GetString("Email")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error: {ex.Message}");
        }
        Console.WriteLine();
    }
}
