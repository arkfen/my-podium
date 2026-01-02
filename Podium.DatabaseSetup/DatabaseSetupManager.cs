using Azure;
using Azure.Data.Tables;
using BCrypt.Net;

namespace Podium.DatabaseSetup;

public class DatabaseSetupManager
{
    private readonly string _connectionString;
    private readonly Dictionary<string, TableClient> _tableClients = new();

    // Table names
    private const string SPORTS_TABLE = "PodiumSports";
    private const string TIERS_TABLE = "PodiumTiers";
    private const string SEASONS_TABLE = "PodiumSeasons";
    private const string EVENTS_TABLE = "PodiumEvents";
    private const string COMPETITORS_TABLE = "PodiumCompetitors";
    private const string SEASON_PARTICIPANTS_TABLE = "PodiumSeasonParticipants";
    private const string EVENT_RESULTS_TABLE = "PodiumEventResults";
    private const string USERS_TABLE = "PodiumUsers";
    private const string USER_PREDICTIONS_TABLE = "PodiumUserPredictions";
    private const string POINTS_CONFIG_TABLE = "PodiumPointsConfig";
    private const string AUTH_SESSIONS_TABLE = "PodiumAuthSessions";
    private const string OTP_CODES_TABLE = "PodiumOTPCodes";

    public DatabaseSetupManager(string connectionString)
    {
        _connectionString = connectionString;
    }

    private TableClient GetTableClient(string tableName)
    {
        if (!_tableClients.ContainsKey(tableName))
        {
            _tableClients[tableName] = new TableClient(_connectionString, tableName);
        }
        return _tableClients[tableName];
    }

    public async Task CreateAllTablesAsync()
    {
        var tableNames = new[]
        {
            SPORTS_TABLE,
            TIERS_TABLE,
            SEASONS_TABLE,
            EVENTS_TABLE,
            COMPETITORS_TABLE,
            SEASON_PARTICIPANTS_TABLE,
            EVENT_RESULTS_TABLE,
            USERS_TABLE,
            USER_PREDICTIONS_TABLE,
            POINTS_CONFIG_TABLE,
            AUTH_SESSIONS_TABLE,
            OTP_CODES_TABLE
        };

        foreach (var tableName in tableNames)
        {
            Console.Write($"  Creating table: {tableName}...");
            var tableClient = GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            Console.WriteLine(" ✓");
        }
    }

    public async Task SeedDataAsync()
    {
        // Seed Sports
        Console.Write("  Seeding Sports...");
        var motorsportId = await SeedSportsAsync();
        Console.WriteLine(" ✓");

        // Seed Tiers
        Console.Write("  Seeding Tiers...");
        var f1TierId = await SeedTiersAsync(motorsportId);
        Console.WriteLine(" ✓");

        // Seed Seasons
        Console.Write("  Seeding Seasons...");
        await SeedSeasonsAsync(f1TierId);
        Console.WriteLine(" ✓");

        // Seed Competitors
        Console.Write("  Seeding Competitors...");
        var competitorIds = await SeedCompetitorsAsync(motorsportId);
        Console.WriteLine(" ✓");

        // Seed Season Participants
        Console.Write("  Seeding Season Participants...");
        await SeedSeasonParticipantsAsync(f1TierId, competitorIds);
        Console.WriteLine(" ✓");

        // Seed Events
        Console.Write("  Seeding Events...");
        var eventIds = await SeedEventsAsync(f1TierId);
        Console.WriteLine(" ✓");

        // Seed Points Configuration
        Console.Write("  Seeding Points Configuration...");
        await SeedPointsConfigAsync(f1TierId);
        Console.WriteLine(" ✓");

        // Seed Test Users
        Console.Write("  Seeding Test Users...");
        await SeedTestUsersAsync();
        Console.WriteLine(" ✓");
    }

    private async Task<string> SeedSportsAsync()
    {
        var tableClient = GetTableClient(SPORTS_TABLE);
        var sportId = "550e8400-e29b-41d4-a716-446655440000";

        var entity = new TableEntity("Sport", sportId)
        {
            ["Name"] = "Motorsport",
            ["Description"] = "Motor racing competitions including Formula racing series",
            ["IsActive"] = true,
            ["CreatedDate"] = DateTimeOffset.UtcNow
        };

        await tableClient.UpsertEntityAsync(entity);
        return sportId;
    }

    private async Task<string> SeedTiersAsync(string sportId)
    {
        var tableClient = GetTableClient(TIERS_TABLE);
        var f1TierId = "660e8400-e29b-41d4-a716-446655440001";

        var tiers = new[]
        {
            new TableEntity(sportId, f1TierId)
            {
                ["Name"] = "Formula 1",
                ["ShortName"] = "F1",
                ["Description"] = "FIA Formula One World Championship",
                ["DisplayOrder"] = 1,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            },
            new TableEntity(sportId, "660e8400-e29b-41d4-a716-446655440002")
            {
                ["Name"] = "Formula 2",
                ["ShortName"] = "F2",
                ["Description"] = "FIA Formula 2 Championship",
                ["DisplayOrder"] = 2,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            },
            new TableEntity(sportId, "660e8400-e29b-41d4-a716-446655440003")
            {
                ["Name"] = "Formula 3",
                ["ShortName"] = "F3",
                ["Description"] = "FIA Formula 3 Championship",
                ["DisplayOrder"] = 3,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            }
        };

        foreach (var tier in tiers)
        {
            await tableClient.UpsertEntityAsync(tier);
        }

        return f1TierId;
    }

    private async Task SeedSeasonsAsync(string f1TierId)
    {
        var tableClient = GetTableClient(SEASONS_TABLE);

        var seasons = new[]
        {
            new TableEntity(f1TierId, "2025")
            {
                ["Name"] = "2025 Season",
                ["StartDate"] = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
                ["EndDate"] = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            },
            new TableEntity(f1TierId, "2026")
            {
                ["Name"] = "2026 Season",
                ["StartDate"] = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
                ["EndDate"] = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
                ["IsActive"] = false,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            }
        };

        foreach (var season in seasons)
        {
            await tableClient.UpsertEntityAsync(season);
        }
    }

    private async Task<List<string>> SeedCompetitorsAsync(string sportId)
    {
        var tableClient = GetTableClient(COMPETITORS_TABLE);
        var competitorIds = new List<string>();

        // Sample F1 2025 drivers (top teams)
        var competitors = new[]
        {
            ("880e8400-e29b-41d4-a716-446655440001", "Max Verstappen", "VER", "1", "Red Bull Racing", "NL"),
            ("880e8400-e29b-41d4-a716-446655440002", "Sergio Pérez", "PER", "11", "Red Bull Racing", "MX"),
            ("880e8400-e29b-41d4-a716-446655440003", "Lewis Hamilton", "HAM", "44", "Ferrari", "GB"),
            ("880e8400-e29b-41d4-a716-446655440004", "Charles Leclerc", "LEC", "16", "Ferrari", "MC"),
            ("880e8400-e29b-41d4-a716-446655440005", "Lando Norris", "NOR", "4", "McLaren", "GB"),
            ("880e8400-e29b-41d4-a716-446655440006", "Oscar Piastri", "PIA", "81", "McLaren", "AU"),
            ("880e8400-e29b-41d4-a716-446655440007", "George Russell", "RUS", "63", "Mercedes", "GB"),
            ("880e8400-e29b-41d4-a716-446655440008", "Kimi Antonelli", "ANT", "12", "Mercedes", "IT"),
            ("880e8400-e29b-41d4-a716-446655440009", "Fernando Alonso", "ALO", "14", "Aston Martin", "ES"),
            ("880e8400-e29b-41d4-a716-446655440010", "Lance Stroll", "STR", "18", "Aston Martin", "CA")
        };

        foreach (var (id, name, shortName, number, team, country) in competitors)
        {
            var entity = new TableEntity(sportId, id)
            {
                ["Name"] = name,
                ["ShortName"] = shortName,
                ["Number"] = number,
                ["Team"] = team,
                ["Country"] = country,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            };

            await tableClient.UpsertEntityAsync(entity);
            competitorIds.Add(id);
        }

        return competitorIds;
    }

    private async Task SeedSeasonParticipantsAsync(string f1TierId, List<string> competitorIds)
    {
        var tableClient = GetTableClient(SEASON_PARTICIPANTS_TABLE);
        var partitionKey = $"{f1TierId}_2025";

        // Link all seeded competitors to 2025 F1 season
        var participants = new[]
        {
            (competitorIds[0], "Max Verstappen", "Red Bull Racing", "1"),
            (competitorIds[1], "Sergio Pérez", "Red Bull Racing", "11"),
            (competitorIds[2], "Lewis Hamilton", "Ferrari", "44"),
            (competitorIds[3], "Charles Leclerc", "Ferrari", "16"),
            (competitorIds[4], "Lando Norris", "McLaren", "4"),
            (competitorIds[5], "Oscar Piastri", "McLaren", "81"),
            (competitorIds[6], "George Russell", "Mercedes", "63"),
            (competitorIds[7], "Kimi Antonelli", "Mercedes", "12"),
            (competitorIds[8], "Fernando Alonso", "Aston Martin", "14"),
            (competitorIds[9], "Lance Stroll", "Aston Martin", "18")
        };

        foreach (var (competitorId, name, team, number) in participants)
        {
            var entity = new TableEntity(partitionKey, competitorId)
            {
                ["CompetitorName"] = name,
                ["Team"] = team,
                ["Number"] = number,
                ["IsActive"] = true,
                ["JoinedDate"] = DateTimeOffset.UtcNow
            };

            await tableClient.UpsertEntityAsync(entity);
        }
    }

    private async Task<List<string>> SeedEventsAsync(string f1TierId)
    {
        var tableClient = GetTableClient(EVENTS_TABLE);
        var partitionKey = $"{f1TierId}_2025";
        var eventIds = new List<string>();

        // Sample F1 2025 calendar (first 5 races)
        var events = new[]
        {
            ("770e8400-e29b-41d4-a716-446655440001", "Bahrain Grand Prix", "Sakhir", new DateTime(2025, 3, 16, 15, 0, 0), 1),
            ("770e8400-e29b-41d4-a716-446655440002", "Saudi Arabian Grand Prix", "Jeddah", new DateTime(2025, 3, 23, 18, 0, 0), 2),
            ("770e8400-e29b-41d4-a716-446655440003", "Australian Grand Prix", "Melbourne", new DateTime(2025, 4, 6, 5, 0, 0), 3),
            ("770e8400-e29b-41d4-a716-446655440004", "Japanese Grand Prix", "Suzuka", new DateTime(2025, 4, 13, 6, 0, 0), 4),
            ("770e8400-e29b-41d4-a716-446655440005", "Chinese Grand Prix", "Shanghai", new DateTime(2025, 4, 20, 8, 0, 0), 5)
        };

        foreach (var (id, name, location, eventDate, round) in events)
        {
            var eventDateUtc = DateTime.SpecifyKind(eventDate, DateTimeKind.Utc);
            var cutoffDate = eventDateUtc.AddMinutes(-15); // Predictions close 15 minutes before event

            var entity = new TableEntity(partitionKey, id)
            {
                ["Name"] = name,
                ["Location"] = location,
                ["EventDate"] = eventDateUtc,
                ["PredictionCutoffDate"] = cutoffDate,
                ["Round"] = round,
                ["IsCompleted"] = false,
                ["IsActive"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            };

            await tableClient.UpsertEntityAsync(entity);
            eventIds.Add(id);
        }

        return eventIds;
    }

    private async Task SeedPointsConfigAsync(string f1TierId)
    {
        var tableClient = GetTableClient(POINTS_CONFIG_TABLE);
        var partitionKey = $"{f1TierId}_2025";

        var entity = new TableEntity(partitionKey, "Config")
        {
            ["ExactPositionPoints"] = 10,
            ["OneOffPoints"] = 5,
            ["TwoOffPoints"] = 3,
            ["InPodiumPoints"] = 1,
            ["CreatedDate"] = DateTimeOffset.UtcNow
        };

        await tableClient.UpsertEntityAsync(entity);
    }

    private async Task SeedTestUsersAsync()
    {
        var tableClient = GetTableClient(USERS_TABLE);

        // Create test users with hashed passwords
        var users = new[]
        {
            ("990e8400-e29b-41d4-a716-446655440001", "test@example.com", "Test User", "password123"),
            ("990e8400-e29b-41d4-a716-446655440002", "demo@example.com", "Demo User", "demo123"),
            ("990e8400-e29b-41d4-a716-446655440003", "admin@example.com", "Admin User", "admin123")
        };

        foreach (var (id, email, name, password) in users)
        {
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 10);

            var entity = new TableEntity("User", id)
            {
                ["Email"] = email,
                ["Name"] = name,
                ["PasswordHash"] = passwordHash,
                ["IsActive"] = true,
                ["IsEmailVerified"] = true,
                ["CreatedDate"] = DateTimeOffset.UtcNow,
                ["LastLoginDate"] = DateTimeOffset.UtcNow
            };

            await tableClient.UpsertEntityAsync(entity);
        }

        Console.WriteLine();
        Console.WriteLine("  Test users created:");
        Console.WriteLine("    - test@example.com / password123");
        Console.WriteLine("    - demo@example.com / demo123");
        Console.WriteLine("    - admin@example.com / admin123");
    }
}
