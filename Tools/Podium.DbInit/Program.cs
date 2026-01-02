using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Podium.DbInit;

class Program
{
    private static IConfiguration? _configuration;
    private static string? _storageUri;
    private static string? _accountName;
    private static string? _accountKey;

    static async Task Main(string[] args)
    {
        Console.WriteLine("===========================================");
        Console.WriteLine("Podium Database Initialization Tool");
        Console.WriteLine("===========================================\n");

        // Load configuration
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>(optional: true)
            .Build();

        _storageUri = _configuration["AzureStorage:StorageUri"];
        _accountName = _configuration["AzureStorage:AccountName"];
        _accountKey = _configuration["AzureStorage:AccountKey"];

        if (string.IsNullOrEmpty(_storageUri) || string.IsNullOrEmpty(_accountName) || string.IsNullOrEmpty(_accountKey))
        {
            Console.WriteLine("ERROR: Azure Storage connection information is missing!");
            Console.WriteLine("Please configure AzureStorage settings in appsettings.json or user secrets.");
            return;
        }

        var createTables = bool.TryParse(_configuration["Options:CreateTables"], out var ct) ? ct : true;
        var addSampleData = bool.TryParse(_configuration["Options:AddSampleData"], out var asd) ? asd : true;
        var dropExisting = bool.TryParse(_configuration["Options:DropExistingTables"], out var de) ? de : false;

        Console.WriteLine($"Storage Account: {_accountName}");
        Console.WriteLine($"Create Tables: {createTables}");
        Console.WriteLine($"Add Sample Data: {addSampleData}");
        Console.WriteLine($"Drop Existing: {dropExisting}\n");

        if (dropExisting)
        {
            Console.Write("WARNING: This will delete all existing Podium tables. Are you sure? (yes/no): ");
            var confirm = Console.ReadLine();
            if (confirm?.ToLower() != "yes")
            {
                Console.WriteLine("Operation cancelled.");
                return;
            }
        }

        try
        {
            if (dropExisting)
            {
                await DropTablesAsync();
            }

            if (createTables)
            {
                await CreateTablesAsync();
            }

            if (addSampleData)
            {
                await AddSampleDataAsync();
            }

            Console.WriteLine("\n===========================================");
            Console.WriteLine("Database initialization completed successfully!");
            Console.WriteLine("===========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
        }
    }

    static async Task DropTablesAsync()
    {
        Console.WriteLine("\n--- Dropping Existing Tables ---");

        var tableNames = new[]
        {
            "PodiumSports", "PodiumTiers", "PodiumSeasons", "PodiumCompetitors",
            "PodiumSeasonCompetitors", "PodiumEvents", "PodiumEventResults",
            "PodiumUsers", "PodiumAuthSessions", "PodiumOTPCodes",
            "PodiumPredictions", "PodiumScoringRules", "PodiumUserStatistics"
        };

        foreach (var tableName in tableNames)
        {
            try
            {
                var client = CreateTableClient(tableName);
                await client.DeleteAsync();
                Console.WriteLine($"? Dropped table: {tableName}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"  Table {tableName} does not exist (skipped)");
            }
        }
    }

    static async Task CreateTablesAsync()
    {
        Console.WriteLine("\n--- Creating Tables ---");

        var tableNames = new[]
        {
            "PodiumSports", "PodiumTiers", "PodiumSeasons", "PodiumCompetitors",
            "PodiumSeasonCompetitors", "PodiumEvents", "PodiumEventResults",
            "PodiumUsers", "PodiumAuthSessions", "PodiumOTPCodes",
            "PodiumPredictions", "PodiumScoringRules", "PodiumUserStatistics"
        };

        foreach (var tableName in tableNames)
        {
            var client = CreateTableClient(tableName);
            await client.CreateIfNotExistsAsync();
            Console.WriteLine($"? Created table: {tableName}");
        }
    }

    static async Task AddSampleDataAsync()
    {
        Console.WriteLine("\n--- Adding Sample Data ---");

        // 1. Create sample sport: Motorsport
        var sportId = Guid.NewGuid().ToString();
        var sportClient = CreateTableClient("PodiumSports");
        var sportEntity = new TableEntity("Sport", sportId)
        {
            ["Name"] = "Motorsport",
            ["DisplayName"] = "Motorsport",
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };
        await sportClient.UpsertEntityAsync(sportEntity);
        Console.WriteLine($"? Created sport: Motorsport (ID: {sportId})");

        // 2. Create sample tier: Formula 1
        var tierId = Guid.NewGuid().ToString();
        var tierClient = CreateTableClient("PodiumTiers");
        var tierEntity = new TableEntity(sportId, tierId)
        {
            ["SportId"] = sportId,
            ["Name"] = "F1",
            ["DisplayName"] = "Formula 1",
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };
        await tierClient.UpsertEntityAsync(tierEntity);
        Console.WriteLine($"? Created tier: Formula 1 (ID: {tierId})");

        // 3. Create sample season: 2025
        var seasonId = Guid.NewGuid().ToString();
        var seasonClient = CreateTableClient("PodiumSeasons");
        var seasonEntity = new TableEntity(tierId, seasonId)
        {
            ["TierId"] = tierId,
            ["Year"] = 2025,
            ["Name"] = "2025 Season",
            ["IsActive"] = true,
            ["StartDate"] = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ["EndDate"] = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            ["CreatedDate"] = DateTime.UtcNow
        };
        await seasonClient.UpsertEntityAsync(seasonEntity);
        Console.WriteLine($"? Created season: 2025 (ID: {seasonId})");

        // 4. Create scoring rules for season
        var scoringClient = CreateTableClient("PodiumScoringRules");
        var scoringEntity = new TableEntity(seasonId, "Scoring")
        {
            ["SeasonId"] = seasonId,
            ["ExactMatchPoints"] = 25,
            ["OneOffPoints"] = 18,
            ["TwoOffPoints"] = 15,
            ["CreatedDate"] = DateTime.UtcNow
        };
        await scoringClient.UpsertEntityAsync(scoringEntity);
        Console.WriteLine($"? Created scoring rules (Exact: 25, OneOff: 18, TwoOff: 15)");

        // 5. Create sample competitors
        var competitorClient = CreateTableClient("PodiumCompetitors");
        var competitorIds = new Dictionary<string, string>();
        var driverNames = new[]
        {
            "Max Verstappen", "Sergio Perez", "Lewis Hamilton", "George Russell",
            "Charles Leclerc", "Carlos Sainz", "Lando Norris", "Oscar Piastri",
            "Fernando Alonso", "Lance Stroll", "Pierre Gasly", "Esteban Ocon",
            "Valtteri Bottas", "Zhou Guanyu", "Kevin Magnussen", "Nico Hulkenberg",
            "Yuki Tsunoda", "Daniel Ricciardo", "Alexander Albon", "Logan Sargeant"
        };

        foreach (var driverName in driverNames)
        {
            var competitorId = Guid.NewGuid().ToString();
            competitorIds[driverName] = competitorId;
            
            var competitorEntity = new TableEntity(sportId, competitorId)
            {
                ["SportId"] = sportId,
                ["Name"] = driverName,
                ["ShortName"] = GetShortName(driverName),
                ["Type"] = "Individual",
                ["IsActive"] = true,
                ["CreatedDate"] = DateTime.UtcNow
            };
            await competitorClient.UpsertEntityAsync(competitorEntity);
        }
        Console.WriteLine($"? Created {driverNames.Length} competitors (F1 drivers)");

        // 6. Link competitors to season
        var seasonCompClient = CreateTableClient("PodiumSeasonCompetitors");
        foreach (var kvp in competitorIds)
        {
            var seasonCompEntity = new TableEntity(seasonId, kvp.Value)
            {
                ["SeasonId"] = seasonId,
                ["CompetitorId"] = kvp.Value,
                ["CompetitorName"] = kvp.Key,
                ["JoinDate"] = DateTime.UtcNow
            };
            await seasonCompClient.UpsertEntityAsync(seasonCompEntity);
        }
        Console.WriteLine($"? Linked competitors to 2025 season");

        // 7. Create sample events
        var eventClient = CreateTableClient("PodiumEvents");
        var events = new[]
        {
            ("Australian Grand Prix", "Melbourne", new DateTime(2025, 3, 16, 5, 0, 0, DateTimeKind.Utc)),
            ("Chinese Grand Prix", "Shanghai", new DateTime(2025, 3, 23, 7, 0, 0, DateTimeKind.Utc)),
            ("Japanese Grand Prix", "Suzuka", new DateTime(2025, 4, 6, 5, 0, 0, DateTimeKind.Utc)),
            ("Bahrain Grand Prix", "Sakhir", new DateTime(2025, 4, 13, 15, 0, 0, DateTimeKind.Utc)),
            ("Saudi Arabian Grand Prix", "Jeddah", new DateTime(2025, 4, 20, 17, 0, 0, DateTimeKind.Utc))
        };

        var eventIds = new List<string>();
        for (int i = 0; i < events.Length; i++)
        {
            var eventId = Guid.NewGuid().ToString();
            eventIds.Add(eventId);
            
            var (name, location, date) = events[i];
            var eventEntity = new TableEntity(seasonId, eventId)
            {
                ["SeasonId"] = seasonId,
                ["Name"] = name,
                ["DisplayName"] = name,
                ["EventNumber"] = i + 1,
                ["EventDate"] = date,
                ["Location"] = location,
                ["Status"] = date > DateTime.UtcNow ? "Upcoming" : "Completed",
                ["IsActive"] = true,
                ["CreatedDate"] = DateTime.UtcNow
            };
            await eventClient.UpsertEntityAsync(eventEntity);
        }
        Console.WriteLine($"? Created {events.Length} events (races)");

        // 8. Add results for first event (if it's in the past)
        if (events[0].Item3 < DateTime.UtcNow)
        {
            var resultClient = CreateTableClient("PodiumEventResults");
            var firstThreeDrivers = driverNames.Take(3).ToArray();
            var resultEntity = new TableEntity(eventIds[0], "Result")
            {
                ["EventId"] = eventIds[0],
                ["FirstPlaceId"] = competitorIds[firstThreeDrivers[0]],
                ["FirstPlaceName"] = firstThreeDrivers[0],
                ["SecondPlaceId"] = competitorIds[firstThreeDrivers[1]],
                ["SecondPlaceName"] = firstThreeDrivers[1],
                ["ThirdPlaceId"] = competitorIds[firstThreeDrivers[2]],
                ["ThirdPlaceName"] = firstThreeDrivers[2],
                ["UpdatedDate"] = DateTime.UtcNow
            };
            await resultClient.UpsertEntityAsync(resultEntity);
            Console.WriteLine($"? Added results for Australian GP");
        }

        // 9. Create sample users
        var userClient = CreateTableClient("PodiumUsers");
        var sampleUsers = new[]
        {
            ("john@example.com", "JohnDoe", "John's password"),
            ("jane@example.com", "JaneSmith", "Jane's password"),
            ("alex@example.com", "AlexRacer", "Alex's password")
        };

        var userIds = new List<string>();
        foreach (var (email, username, password) in sampleUsers)
        {
            var userId = Guid.NewGuid().ToString();
            userIds.Add(userId);
            
            var (hash, salt) = HashPassword(password);
            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);
            
            var userEntity = new TableEntity(partitionKey, rowKey)
            {
                ["UserId"] = userId,
                ["Email"] = email,
                ["Username"] = username,
                ["PasswordHash"] = hash,
                ["PasswordSalt"] = salt,
                ["PreferredAuthMethod"] = "Both",
                ["IsActive"] = true,
                ["CreatedDate"] = DateTime.UtcNow,
                ["LastLoginDate"] = null
            };
            await userClient.UpsertEntityAsync(userEntity);
        }
        Console.WriteLine($"? Created {sampleUsers.Length} sample users");

        // 10. Create sample predictions
        var predictionClient = CreateTableClient("PodiumPredictions");
        var random = new Random();
        
        // Create predictions for upcoming events
        foreach (var eventId in eventIds.Where((_, idx) => events[idx].Item3 > DateTime.UtcNow))
        {
            foreach (var userId in userIds.Take(2)) // Just first 2 users
            {
                var shuffledDrivers = competitorIds.Values.OrderBy(_ => random.Next()).Take(3).ToArray();
                var predictedDriverNames = competitorIds.Where(kvp => shuffledDrivers.Contains(kvp.Value)).Select(kvp => kvp.Key).ToArray();
                
                var predictionEntity = new TableEntity(eventId, userId)
                {
                    ["EventId"] = eventId,
                    ["UserId"] = userId,
                    ["FirstPlaceId"] = shuffledDrivers[0],
                    ["FirstPlaceName"] = predictedDriverNames[0],
                    ["SecondPlaceId"] = shuffledDrivers[1],
                    ["SecondPlaceName"] = predictedDriverNames[1],
                    ["ThirdPlaceId"] = shuffledDrivers[2],
                    ["ThirdPlaceName"] = predictedDriverNames[2],
                    ["PointsEarned"] = null,
                    ["SubmittedDate"] = DateTime.UtcNow,
                    ["UpdatedDate"] = DateTime.UtcNow
                };
                await predictionClient.UpsertEntityAsync(predictionEntity);
            }
        }
        Console.WriteLine($"? Created sample predictions for upcoming events");

        Console.WriteLine("\n--- Sample Data Summary ---");
        Console.WriteLine($"Sport: Motorsport");
        Console.WriteLine($"Tier: Formula 1");
        Console.WriteLine($"Season: 2025");
        Console.WriteLine($"Competitors: {driverNames.Length} drivers");
        Console.WriteLine($"Events: {events.Length} races");
        Console.WriteLine($"Users: {sampleUsers.Length} test accounts");
        Console.WriteLine($"\nTest Login Credentials:");
        foreach (var (email, username, password) in sampleUsers)
        {
            Console.WriteLine($"  Email: {email} | Username: {username} | Password: {password}");
        }
    }

    static TableClient CreateTableClient(string tableName)
    {
        return new TableClient(
            new Uri(_storageUri!),
            tableName,
            new TableSharedKeyCredential(_accountName!, _accountKey!));
    }

    static string GetShortName(string fullName)
    {
        var parts = fullName.Split(' ');
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}. {parts[^1]}";
        }
        return fullName;
    }

    static (string hash, string salt) HashPassword(string password)
    {
        // Generate salt
        var saltBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        var salt = Convert.ToBase64String(saltBytes);

        // Hash password with salt
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
    }
}
