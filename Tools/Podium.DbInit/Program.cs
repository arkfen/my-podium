using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
            "PodiumDisciplines", "PodiumSeries", "PodiumSeasons", "PodiumCompetitors",
            "PodiumSeasonCompetitors", "PodiumEvents", "PodiumEventResults",
            "PodiumUsers", "PodiumAuthSessions", "PodiumOTPCodes",
            "PodiumPredictions", "PodiumScoringRules", "PodiumUserStatistics", "PodiumAdmins"
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
            "PodiumDisciplines", "PodiumSeries", "PodiumSeasons", "PodiumCompetitors",
            "PodiumSeasonCompetitors", "PodiumEvents", "PodiumEventResults",
            "PodiumUsers", "PodiumAuthSessions", "PodiumOTPCodes",
            "PodiumPredictions", "PodiumScoringRules", "PodiumUserStatistics", "PodiumAdmins"
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
        Console.WriteLine("\n--- Adding Motorsport Data ---");

        var disciplineClient = CreateTableClient("PodiumDisciplines");
        var seriesClient = CreateTableClient("PodiumSeries");
        var seasonClient = CreateTableClient("PodiumSeasons");
        var competitorClient = CreateTableClient("PodiumCompetitors");
        var seasonCompClient = CreateTableClient("PodiumSeasonCompetitors");
        var eventClient = CreateTableClient("PodiumEvents");
        var scoringClient = CreateTableClient("PodiumScoringRules");

        // Motorsport data structure
        var motorsportData = new[]
        {
            new { 
                Name = "Single-Seater Racing", 
                Series = new[] {
                    new { Name = "Formula 1", GoverningBody = "FIA", Region = "Global", VehicleType = "Open-wheel" },
                    new { Name = "Formula 2", GoverningBody = "FIA", Region = "Global", VehicleType = "Open-wheel" },
                    new { Name = "Formula 3", GoverningBody = "FIA", Region = "Global", VehicleType = "Open-wheel" },
                    new { Name = "IndyCar", GoverningBody = "IndyCar", Region = "USA", VehicleType = "Open-wheel" }
                }
            },
            new { 
                Name = "Touring / Stock Cars", 
                Series = new[] {
                    new { Name = "NASCAR Cup Series", GoverningBody = "NASCAR", Region = "USA", VehicleType = "Stock Car" },
                    new { Name = "British Touring Car Championship", GoverningBody = "TOCA", Region = "UK", VehicleType = "Touring Car" },
                    new { Name = "Supercars Championship", GoverningBody = "Motorsport Australia", Region = "Australia", VehicleType = "Touring Car" }
                }
            },
            new { 
                Name = "Rally", 
                Series = new[] {
                    new { Name = "World Rally Championship", GoverningBody = "FIA", Region = "Global", VehicleType = "Rally Car" },
                    new { Name = "American Rally Association", GoverningBody = "ARA", Region = "USA", VehicleType = "Rally Car" },
                    new { Name = "Dakar Rally", GoverningBody = "FIA / FIM", Region = "Global", VehicleType = "Rally Raid" }
                }
            },
            new { 
                Name = "Endurance Racing", 
                Series = new[] {
                    new { Name = "FIA World Endurance Championship", GoverningBody = "FIA", Region = "Global", VehicleType = "Prototype / GT" },
                    new { Name = "IMSA WeatherTech SportsCar Championship", GoverningBody = "IMSA", Region = "USA", VehicleType = "Prototype / GT" },
                    new { Name = "Asian Le Mans Series", GoverningBody = "ACO", Region = "Asia", VehicleType = "Prototype / GT" }
                }
            },
            new { 
                Name = "Motorcycle Racing", 
                Series = new[] {
                    new { Name = "MotoGP", GoverningBody = "FIM", Region = "Global", VehicleType = "Motorcycle" },
                    new { Name = "Moto2", GoverningBody = "FIM", Region = "Global", VehicleType = "Motorcycle" },
                    new { Name = "Moto3", GoverningBody = "FIM", Region = "Global", VehicleType = "Motorcycle" },
                    new { Name = "World Superbike Championship", GoverningBody = "FIM", Region = "Global", VehicleType = "Motorcycle" },
                    new { Name = "MotoAmerica Superbike", GoverningBody = "MotoAmerica / FIM NA", Region = "USA", VehicleType = "Motorcycle" }
                }
            },
            new { 
                Name = "Electric Racing", 
                Series = new[] {
                    new { Name = "Formula E", GoverningBody = "FIA", Region = "Global", VehicleType = "Electric Open-wheel" },
                    new { Name = "MotoE", GoverningBody = "FIM", Region = "Global", VehicleType = "Electric Motorcycle" },
                    new { Name = "Extreme E", GoverningBody = "FIA", Region = "Global", VehicleType = "Electric Off-Road SUV" }
                }
            },
            new { 
                Name = "Drag Racing", 
                Series = new[] {
                    new { Name = "NHRA Camping World Series", GoverningBody = "NHRA", Region = "USA", VehicleType = "Dragster" },
                    new { Name = "FIA European Drag Racing Championship", GoverningBody = "FIA", Region = "Europe", VehicleType = "Dragster" }
                }
            },
            new { 
                Name = "Karting", 
                Series = new[] {
                    new { Name = "CIK-FIA Karting World Championship", GoverningBody = "FIA", Region = "Global", VehicleType = "Kart" },
                    new { Name = "SKUSA SuperNationals", GoverningBody = "SKUSA", Region = "USA", VehicleType = "Kart" }
                }
            }
        };

        // Create disciplines and series
        string? singleSeaterDisciplineId = null;
        string? f1SeriesId = null;

        foreach (var discipline in motorsportData)
        {
            var disciplineId = Guid.NewGuid().ToString();
            
            // Save Single-Seater Racing ID for later use
            if (discipline.Name == "Single-Seater Racing")
            {
                singleSeaterDisciplineId = disciplineId;
            }

            var disciplineEntity = new TableEntity("Discipline", disciplineId)
            {
                ["Name"] = discipline.Name,
                ["DisplayName"] = discipline.Name,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTime.UtcNow
            };
            await disciplineClient.UpsertEntityAsync(disciplineEntity);
            Console.WriteLine($"? Created discipline: {discipline.Name}");

            // Create series for this discipline
            foreach (var series in discipline.Series)
            {
                var seriesId = Guid.NewGuid().ToString();
                
                // Save F1 Series ID for detailed sample data
                if (series.Name == "Formula 1")
                {
                    f1SeriesId = seriesId;
                }

                var seriesEntity = new TableEntity(disciplineId, seriesId)
                {
                    ["DisciplineId"] = disciplineId,
                    ["Name"] = series.Name,
                    ["DisplayName"] = series.Name,
                    ["GoverningBody"] = series.GoverningBody,
                    ["Region"] = series.Region,
                    ["VehicleType"] = series.VehicleType,
                    ["IsActive"] = true,
                    ["CreatedDate"] = DateTime.UtcNow
                };
                await seriesClient.UpsertEntityAsync(seriesEntity);
                Console.WriteLine($"  ? Created series: {series.Name}");
            }
        }

        // Add detailed F1 data
        if (f1SeriesId != null && singleSeaterDisciplineId != null)
        {
            Console.WriteLine("\n--- Adding Formula 1 2026 Season Data ---");

            // Create 2025 F1 season
            var seasonId = Guid.NewGuid().ToString();
            var seasonEntity = new TableEntity(f1SeriesId, seasonId)
            {
                ["SeriesId"] = f1SeriesId,
                ["Year"] = 2026,
                ["Name"] = "2026 Season",
                ["IsActive"] = true,
                ["StartDate"] = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                ["EndDate"] = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                ["CreatedDate"] = DateTime.UtcNow
            };
            await seasonClient.UpsertEntityAsync(seasonEntity);
            Console.WriteLine($"? Created 2026 F1 season");

            // Create scoring rules
            var scoringEntity = new TableEntity(seasonId, "Scoring")
            {
                ["SeasonId"] = seasonId,
                ["ExactMatchPoints"] = 25,
                ["OneOffPoints"] = 18,
                ["TwoOffPoints"] = 15,
                ["CreatedDate"] = DateTime.UtcNow
            };
            await scoringClient.UpsertEntityAsync(scoringEntity);
            Console.WriteLine($"? Created scoring rules");

            // Create F1 drivers
            //var driverNames = new[]
            //{
            //    "Max Verstappen", "Sergio Perez", "Lewis Hamilton", "George Russell",
            //    "Charles Leclerc", "Carlos Sainz", "Lando Norris", "Oscar Piastri",
            //    "Fernando Alonso", "Lance Stroll", "Pierre Gasly", "Esteban Ocon",
            //    "Valtteri Bottas", "Zhou Guanyu", "Kevin Magnussen", "Nico Hulkenberg",
            //    "Yuki Tsunoda", "Daniel Ricciardo", "Alexander Albon", "Logan Sargeant"
            //};

            //var competitorIds = new Dictionary<string, string>();
            //foreach (var driverName in driverNames)
            //{
            //    var competitorId = Guid.NewGuid().ToString();
            //    competitorIds[driverName] = competitorId;
                
            //    var competitorEntity = new TableEntity(singleSeaterDisciplineId, competitorId)
            //    {
            //        ["DisciplineId"] = singleSeaterDisciplineId,
            //        ["Name"] = driverName,
            //        ["ShortName"] = GetShortName(driverName),
            //        ["Type"] = "Individual",
            //        ["IsActive"] = true,
            //        ["CreatedDate"] = DateTime.UtcNow
            //    };
            //    await competitorClient.UpsertEntityAsync(competitorEntity);
            //}
            //Console.WriteLine($"? Created {driverNames.Length} F1 drivers");

            // Link competitors to season
            //foreach (var kvp in competitorIds)
            //{
            //    var seasonCompEntity = new TableEntity(seasonId, kvp.Value)
            //    {
            //        ["SeasonId"] = seasonId,
            //        ["CompetitorId"] = kvp.Value,
            //        ["CompetitorName"] = kvp.Key,
            //        ["JoinDate"] = DateTime.UtcNow
            //    };
            //    await seasonCompClient.UpsertEntityAsync(seasonCompEntity);
            //}
            //Console.WriteLine($"? Linked drivers to season");

            // Create F1 races
            var races = new[]
            {
                ("Australian Grand Prix", "Melbourne", new DateTime(2026, 3, 16, 5, 0, 0, DateTimeKind.Utc)),
                ("Chinese Grand Prix", "Shanghai", new DateTime(2026, 3, 23, 7, 0, 0, DateTimeKind.Utc)),
                ("Japanese Grand Prix", "Suzuka", new DateTime(2026, 4, 6, 5, 0, 0, DateTimeKind.Utc)),
                ("Bahrain Grand Prix", "Sakhir", new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc)),
                ("Saudi Arabian Grand Prix", "Jeddah", new DateTime(2026, 4, 20, 17, 0, 0, DateTimeKind.Utc)),
                ("Miami Grand Prix", "Miami", new DateTime(2026, 5, 4, 19, 0, 0, DateTimeKind.Utc)),
                ("Emilia Romagna Grand Prix", "Imola", new DateTime(2026, 5, 18, 13, 0, 0, DateTimeKind.Utc)),
                ("Monaco Grand Prix", "Monaco", new DateTime(2026, 5, 25, 13, 0, 0, DateTimeKind.Utc)),
                ("Spanish Grand Prix", "Barcelona", new DateTime(2026, 6, 1, 13, 0, 0, DateTimeKind.Utc)),
                ("Canadian Grand Prix", "Montreal", new DateTime(2026, 6, 15, 18, 0, 0, DateTimeKind.Utc))
            };

            for (int i = 0; i < races.Length; i++)
            {
                var eventId = Guid.NewGuid().ToString();
                var (name, location, date) = races[i];
                
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
            Console.WriteLine($"? Created {races.Length} F1 races");
        }

        // Create sample users
        Console.WriteLine("\n--- Adding Sample Users ---");
        var userClient = CreateTableClient("PodiumUsers");
        var sampleUsers = new[]
        {
            ("john@example.com", "JohnDoe", "password123"),
            ("jane@example.com", "JaneSmith", "password123"),
            ("alex@example.com", "AlexRacer", "password123")
        };

        foreach (var (email, username, password) in sampleUsers)
        {
            var userId = Guid.NewGuid().ToString();
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

        Console.WriteLine("\n--- Sample Data Summary ---");
        Console.WriteLine($"Disciplines: 8 motorsport categories");
        Console.WriteLine($"Series: Multiple racing series across disciplines");
        Console.WriteLine($"Detailed F1 2025 Season: 20 drivers, 10 races");
        Console.WriteLine($"\nTest Login Credentials:");
        foreach (var (email, username, _) in sampleUsers)
        {
            Console.WriteLine($"  Email: {email} | Username: {username} | Password: password123");
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
