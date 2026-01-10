using Azure;
using Azure.Data.Tables;
using Podium.Migration.Models;

namespace Podium.Migration.Services;

/// <summary>
/// Service to insert transformed data into new Podium tables
/// </summary>
public class PodiumDataInserter
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly bool _dryRun;

    public PodiumDataInserter(string storageUri, string accountName, string accountKey, bool dryRun = false)
    {
        var credential = new TableSharedKeyCredential(accountName, accountKey);
        _tableServiceClient = new TableServiceClient(new Uri(storageUri), credential);
        _dryRun = dryRun;
    }

    /// <summary>
    /// Ensure DateTime is UTC (required by Azure Table Storage)
    /// </summary>
    private DateTime EnsureUtc(DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified)
        {
            // Assume unspecified dates are UTC
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
        else if (dateTime.Kind == DateTimeKind.Local)
        {
            // Convert local time to UTC
            return dateTime.ToUniversalTime();
        }
        // Already UTC
        return dateTime;
    }

    private async Task<TableClient> GetTableClientAsync(string tableName)
    {
        var client = _tableServiceClient.GetTableClient(tableName);
        if (!_dryRun)
        {
            await client.CreateIfNotExistsAsync();
        }
        return client;
    }

    /// <summary>
    /// Insert discipline record
    /// </summary>
    public async Task InsertDisciplineAsync(string disciplineId, string name)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert discipline: {name}");
            return;
        }

        var client = await GetTableClientAsync("PodiumDisciplines");
        var entity = new TableEntity("Discipline", disciplineId)
        {
            ["Name"] = name,
            ["DisplayName"] = name,
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        Console.WriteLine($"? Inserted discipline: {name}");
    }

    /// <summary>
    /// Insert series record
    /// </summary>
    public async Task InsertSeriesAsync(string disciplineId, string seriesId, string name, 
        string governingBody, string region, string vehicleType)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert series: {name}");
            return;
        }

        var client = await GetTableClientAsync("PodiumSeries");
        var entity = new TableEntity(disciplineId, seriesId)
        {
            ["DisciplineId"] = disciplineId,
            ["Name"] = name,
            ["DisplayName"] = name,
            ["GoverningBody"] = governingBody,
            ["Region"] = region,
            ["VehicleType"] = vehicleType,
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        Console.WriteLine($"? Inserted series: {name}");
    }

    /// <summary>
    /// Insert season record
    /// </summary>
    public async Task InsertSeasonAsync(string seriesId, string seasonId, int year, 
        DateTime startDate, DateTime? endDate)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert season: {year}");
            return;
        }

        var client = await GetTableClientAsync("PodiumSeasons");
        var entity = new TableEntity(seriesId, seasonId)
        {
            ["SeriesId"] = seriesId,
            ["Year"] = year,
            ["Name"] = $"{year} Season",
            ["IsActive"] = year == DateTime.UtcNow.Year,
            ["StartDate"] = startDate,
            ["EndDate"] = endDate,
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        Console.WriteLine($"? Inserted season: {year}");
    }

    /// <summary>
    /// Insert scoring rules
    /// </summary>
    public async Task InsertScoringRulesAsync(string seasonId, int exactMatchPoints = 25, 
        int oneOffPoints = 18, int twoOffPoints = 15)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert scoring rules for season");
            return;
        }

        var client = await GetTableClientAsync("PodiumScoringRules");
        var entity = new TableEntity(seasonId, "Scoring")
        {
            ["SeasonId"] = seasonId,
            ["ExactMatchPoints"] = exactMatchPoints,
            ["OneOffPoints"] = oneOffPoints,
            ["TwoOffPoints"] = twoOffPoints,
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        Console.WriteLine($"? Inserted scoring rules");
    }

    /// <summary>
    /// Insert competitor (driver)
    /// </summary>
    public async Task InsertCompetitorAsync(string disciplineId, string competitorId, 
        string name, string shortName)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert competitor: {name}");
            return;
        }

        var client = await GetTableClientAsync("PodiumCompetitors");
        var entity = new TableEntity(disciplineId, competitorId)
        {
            ["DisciplineId"] = disciplineId,
            ["Name"] = name,
            ["ShortName"] = shortName,
            ["Type"] = "Individual",
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert season competitor link
    /// </summary>
    public async Task InsertSeasonCompetitorAsync(string seasonId, string competitorId, string competitorName, DateTime? joinDate = null)
    {
        if (_dryRun)
        {
            return;
        }

        var client = await GetTableClientAsync("PodiumSeasonCompetitors");
        var entity = new TableEntity(seasonId, competitorId)
        {
            ["SeasonId"] = seasonId,
            ["CompetitorId"] = competitorId,
            ["CompetitorName"] = competitorName,
            ["JoinDate"] = joinDate ?? DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert event (race)
    /// </summary>
    public async Task InsertEventAsync(string seasonId, string eventId, string name, 
        int eventNumber, DateTime eventDate, string location, string status)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert event: {name}");
            return;
        }

        var client = await GetTableClientAsync("PodiumEvents");
        
        // Ensure event date is UTC
        var utcEventDate = EnsureUtc(eventDate);
        
        var entity = new TableEntity(seasonId, eventId)
        {
            ["SeasonId"] = seasonId,
            ["Name"] = name,
            ["DisplayName"] = name,
            ["EventNumber"] = eventNumber,
            ["EventDate"] = utcEventDate,
            ["Location"] = location,
            ["Status"] = status,
            ["IsActive"] = status != "Completed",
            ["CreatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert event result
    /// </summary>
    public async Task InsertEventResultAsync(string eventId, 
        string firstPlaceId, string firstPlaceName,
        string secondPlaceId, string secondPlaceName,
        string thirdPlaceId, string thirdPlaceName)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert event result");
            return;
        }

        var client = await GetTableClientAsync("PodiumEventResults");
        var entity = new TableEntity(eventId, "Result")
        {
            ["EventId"] = eventId,
            ["FirstPlaceId"] = firstPlaceId,
            ["FirstPlaceName"] = firstPlaceName,
            ["SecondPlaceId"] = secondPlaceId,
            ["SecondPlaceName"] = secondPlaceName,
            ["ThirdPlaceId"] = thirdPlaceId,
            ["ThirdPlaceName"] = thirdPlaceName,
            ["UpdatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert user
    /// </summary>
    public async Task InsertUserAsync(string userId, string email, string username)
    {
        if (_dryRun)
        {
            Console.WriteLine($"[DRY RUN] Would insert user: {username}");
            return;
        }

        var client = await GetTableClientAsync("PodiumUsers");
        var partitionKey = userId.Substring(0, Math.Min(6, userId.Length));
        var rowKey = userId.Length > 6 ? userId.Substring(6) : userId;
        
        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["UserId"] = userId,
            ["Email"] = email,
            ["Username"] = username,
            ["PasswordHash"] = string.Empty, // No password for migrated users
            ["PasswordSalt"] = string.Empty,
            ["PreferredAuthMethod"] = "Email", // OTP only for legacy users
            ["IsActive"] = true,
            ["CreatedDate"] = DateTime.UtcNow,
            ["LastLoginDate"] = null
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert prediction
    /// </summary>
    public async Task InsertPredictionAsync(string eventId, string userId, 
        string firstPlaceId, string firstPlaceName,
        string secondPlaceId, string secondPlaceName,
        string thirdPlaceId, string thirdPlaceName,
        int? pointsEarned, DateTime submittedDate)
    {
        if (_dryRun)
        {
            return;
        }

        var client = await GetTableClientAsync("PodiumPredictions");
        
        // Ensure submitted date is UTC
        var utcSubmittedDate = EnsureUtc(submittedDate);
        
        var entity = new TableEntity(eventId, userId)
        {
            ["EventId"] = eventId,
            ["UserId"] = userId,
            ["FirstPlaceId"] = firstPlaceId,
            ["FirstPlaceName"] = firstPlaceName,
            ["SecondPlaceId"] = secondPlaceId,
            ["SecondPlaceName"] = secondPlaceName,
            ["ThirdPlaceId"] = thirdPlaceId,
            ["ThirdPlaceName"] = thirdPlaceName,
            ["PointsEarned"] = pointsEarned,
            ["SubmittedDate"] = utcSubmittedDate,
            ["UpdatedDate"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }

    /// <summary>
    /// Insert or update user statistics
    /// </summary>
    public async Task UpsertUserStatisticsAsync(string seasonId, string userId, string username,
        int totalPoints, int predictionsCount, int exactMatches, int oneOffMatches, int twoOffMatches)
    {
        if (_dryRun)
        {
            return;
        }

        var client = await GetTableClientAsync("PodiumUserStatistics");
        var entity = new TableEntity(seasonId, userId)
        {
            ["SeasonId"] = seasonId,
            ["UserId"] = userId,
            ["Username"] = username,
            ["TotalPoints"] = totalPoints,
            ["PredictionsCount"] = predictionsCount,
            ["ExactMatches"] = exactMatches,
            ["OneOffMatches"] = oneOffMatches,
            ["TwoOffMatches"] = twoOffMatches,
            ["LastUpdated"] = DateTime.UtcNow
        };
        
        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace);
    }
}
