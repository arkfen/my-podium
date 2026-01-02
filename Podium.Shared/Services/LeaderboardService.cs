using Podium.Shared.Models;

namespace Podium.Shared.Services;

public interface ILeaderboardService
{
    Task<List<LeaderboardEntry>> GetSeasonLeaderboardAsync(string tierId, string seasonYear);
    Task<List<LeaderboardEntry>> GetEventLeaderboardAsync(string eventId, string tierId, string seasonYear);
}

public class LeaderboardService : ILeaderboardService
{
    private readonly ITableStorageService _storageService;
    private readonly IResultsService _resultsService;
    private readonly IEventService _eventService;
    
    private const string USERS_TABLE = "PodiumUsers";
    private const string USER_PREDICTIONS_TABLE = "PodiumUserPredictions";

    public LeaderboardService(
        ITableStorageService storageService,
        IResultsService resultsService,
        IEventService eventService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _resultsService = resultsService ?? throw new ArgumentNullException(nameof(resultsService));
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    public async Task<List<LeaderboardEntry>> GetSeasonLeaderboardAsync(string tierId, string seasonYear)
    {
        var leaderboard = new Dictionary<string, LeaderboardEntry>();
        
        // Get all users who have made predictions
        var events = await _eventService.GetEventsBySeasonAsync(tierId, seasonYear);
        var completedEvents = events.Where(e => e.IsCompleted).ToList();

        // Get all users
        var users = await GetAllUsersAsync();

        foreach (var user in users)
        {
            var userResults = await _resultsService.GetUserResultsForSeasonAsync(user.Id, tierId, seasonYear);
            
            if (userResults.Any())
            {
                var totalPoints = userResults.Sum(r => r.PointsEarned);
                var predictedEvents = userResults.Count;

                leaderboard[user.Id] = new LeaderboardEntry
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    TotalPoints = totalPoints,
                    EventsPredicted = predictedEvents,
                    Rank = 0 // Will be set after sorting
                };
            }
        }

        // Sort by points and assign ranks
        var sortedLeaderboard = leaderboard.Values
            .OrderByDescending(e => e.TotalPoints)
            .ThenBy(e => e.UserName)
            .ToList();

        int currentRank = 1;
        for (int i = 0; i < sortedLeaderboard.Count; i++)
        {
            if (i > 0 && sortedLeaderboard[i].TotalPoints < sortedLeaderboard[i - 1].TotalPoints)
            {
                currentRank = i + 1;
            }
            sortedLeaderboard[i].Rank = currentRank;
        }

        return sortedLeaderboard;
    }

    public async Task<List<LeaderboardEntry>> GetEventLeaderboardAsync(string eventId, string tierId, string seasonYear)
    {
        var leaderboard = new Dictionary<string, LeaderboardEntry>();
        
        // Get event results
        var eventResults = await _resultsService.GetEventResultsAsync(eventId);
        
        if (eventResults.Count == 0)
        {
            return new List<LeaderboardEntry>(); // No results yet
        }

        // Get all users
        var users = await GetAllUsersAsync();
        
        // Get points configuration
        var pointsConfigTableClient = _storageService.GetTableClient("PodiumPointsConfig");
        var partitionKey = $"{tierId}_{seasonYear}";
        PointsConfiguration? pointsConfig = null;

        try
        {
            var entity = await pointsConfigTableClient.GetEntityAsync<Azure.Data.Tables.TableEntity>(partitionKey, "Config");
            pointsConfig = new PointsConfiguration
            {
                TierId = tierId,
                SeasonYear = seasonYear,
                ExactPositionPoints = entity.Value.GetInt32("ExactPositionPoints") ?? 10,
                OneOffPoints = entity.Value.GetInt32("OneOffPoints") ?? 5,
                TwoOffPoints = entity.Value.GetInt32("TwoOffPoints") ?? 3,
                InPodiumPoints = entity.Value.GetInt32("InPodiumPoints") ?? 1
            };
        }
        catch
        {
            pointsConfig = new PointsConfiguration
            {
                ExactPositionPoints = 10,
                OneOffPoints = 5,
                TwoOffPoints = 3,
                InPodiumPoints = 1
            };
        }

        foreach (var user in users)
        {
            var predictionService = new PredictionService(_storageService);
            var predictions = await predictionService.GetUserPredictionsForEventAsync(user.Id, eventId);
            
            if (predictions.Count > 0)
            {
                int points = CalculateEventPoints(predictions, eventResults, pointsConfig);
                
                leaderboard[user.Id] = new LeaderboardEntry
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    TotalPoints = points,
                    EventsPredicted = 1,
                    Rank = 0
                };
            }
        }

        // Sort and rank
        var sortedLeaderboard = leaderboard.Values
            .OrderByDescending(e => e.TotalPoints)
            .ThenBy(e => e.UserName)
            .ToList();

        int currentRank = 1;
        for (int i = 0; i < sortedLeaderboard.Count; i++)
        {
            if (i > 0 && sortedLeaderboard[i].TotalPoints < sortedLeaderboard[i - 1].TotalPoints)
            {
                currentRank = i + 1;
            }
            sortedLeaderboard[i].Rank = currentRank;
        }

        return sortedLeaderboard;
    }

    private async Task<List<User>> GetAllUsersAsync()
    {
        var tableClient = _storageService.GetTableClient(USERS_TABLE);
        var users = new List<User>();

        await foreach (var entity in tableClient.QueryAsync<Azure.Data.Tables.TableEntity>(filter: $"PartitionKey eq 'User' and IsActive eq true"))
        {
            users.Add(new User
            {
                Id = entity.RowKey,
                Email = entity.GetString("Email") ?? string.Empty,
                Name = entity.GetString("Name") ?? string.Empty,
                IsActive = entity.GetBoolean("IsActive") ?? false
            });
        }

        return users;
    }

    private int CalculateEventPoints(List<UserPrediction> predictions, List<EventResult> results, PointsConfiguration config)
    {
        int totalPoints = 0;

        foreach (var prediction in predictions)
        {
            var actualResult = results.FirstOrDefault(r => r.Position == prediction.Position);
            
            if (actualResult == null)
            {
                continue;
            }

            // Exact match
            if (actualResult.CompetitorId == prediction.CompetitorId)
            {
                totalPoints += config.ExactPositionPoints;
            }
            else
            {
                // Check if competitor is in podium but different position
                var competitorActualPosition = results.FirstOrDefault(r => r.CompetitorId == prediction.CompetitorId);
                
                if (competitorActualPosition != null)
                {
                    int positionDifference = Math.Abs(competitorActualPosition.Position - prediction.Position);
                    
                    if (positionDifference == 1)
                    {
                        totalPoints += config.OneOffPoints;
                    }
                    else if (positionDifference == 2)
                    {
                        totalPoints += config.TwoOffPoints;
                    }
                    else
                    {
                        totalPoints += config.InPodiumPoints;
                    }
                }
            }
        }

        return totalPoints;
    }
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Rank { get; set; }
    public int TotalPoints { get; set; }
    public int EventsPredicted { get; set; }
}
