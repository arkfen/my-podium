using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services;

public interface IResultsService
{
    Task<List<EventResult>> GetEventResultsAsync(string eventId);
    Task<bool> SaveEventResultsAsync(string eventId, List<EventResult> results);
    Task<List<UserPredictionResult>> GetUserResultsForSeasonAsync(string userId, string tierId, string seasonYear);
}

public class ResultsService : IResultsService
{
    private readonly ITableStorageService _storageService;
    private readonly IPredictionService _predictionService;
    private readonly IEventService _eventService;
    
    private const string EVENT_RESULTS_TABLE = "PodiumEventResults";
    private const string POINTS_CONFIG_TABLE = "PodiumPointsConfig";

    public ResultsService(
        ITableStorageService storageService, 
        IPredictionService predictionService,
        IEventService eventService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _predictionService = predictionService ?? throw new ArgumentNullException(nameof(predictionService));
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    public async Task<List<EventResult>> GetEventResultsAsync(string eventId)
    {
        var tableClient = _storageService.GetTableClient(EVENT_RESULTS_TABLE);
        var results = new List<EventResult>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{eventId}'"))
        {
            var positionStr = entity.RowKey.Replace("P", "");
            if (int.TryParse(positionStr, out int position))
            {
                results.Add(new EventResult
                {
                    EventId = eventId,
                    Position = position,
                    CompetitorId = entity.GetString("CompetitorId") ?? string.Empty,
                    CompetitorName = entity.GetString("CompetitorName") ?? string.Empty,
                    RecordedDate = entity.GetDateTimeOffset("RecordedDate") ?? DateTimeOffset.MinValue
                });
            }
        }

        return results.OrderBy(r => r.Position).ToList();
    }

    public async Task<bool> SaveEventResultsAsync(string eventId, List<EventResult> results)
    {
        if (results.Count != 3)
        {
            throw new ArgumentException("Exactly 3 results (P1, P2, P3) are required.", nameof(results));
        }

        var tableClient = _storageService.GetTableClient(EVENT_RESULTS_TABLE);

        try
        {
            foreach (var result in results)
            {
                var entity = new TableEntity(eventId, $"P{result.Position}")
                {
                    ["CompetitorId"] = result.CompetitorId,
                    ["CompetitorName"] = result.CompetitorName,
                    ["RecordedDate"] = DateTimeOffset.UtcNow
                };

                await tableClient.UpsertEntityAsync(entity);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<UserPredictionResult>> GetUserResultsForSeasonAsync(string userId, string tierId, string seasonYear)
    {
        var results = new List<UserPredictionResult>();
        
        // Get all events for the season
        var events = await _eventService.GetEventsBySeasonAsync(tierId, seasonYear);
        
        // Get points configuration
        var pointsConfig = await GetPointsConfigAsync(tierId, seasonYear);

        foreach (var eventItem in events.Where(e => e.IsCompleted))
        {
            // Get user's predictions
            var predictions = await _predictionService.GetUserPredictionsForEventAsync(userId, eventItem.Id);
            
            if (predictions.Count == 0)
            {
                continue; // User didn't make predictions for this event
            }

            // Get actual results
            var actualResults = await GetEventResultsAsync(eventItem.Id);
            
            if (actualResults.Count == 0)
            {
                continue; // Results not recorded yet
            }

            // Calculate points
            int totalPoints = CalculatePoints(predictions, actualResults, pointsConfig);

            results.Add(new UserPredictionResult
            {
                EventId = eventItem.Id,
                EventName = eventItem.Name,
                EventDate = eventItem.EventDate,
                Predictions = predictions,
                ActualResults = actualResults,
                PointsEarned = totalPoints
            });
        }

        return results.OrderBy(r => r.EventDate).ToList();
    }

    private async Task<PointsConfiguration> GetPointsConfigAsync(string tierId, string seasonYear)
    {
        var tableClient = _storageService.GetTableClient(POINTS_CONFIG_TABLE);
        var partitionKey = $"{tierId}_{seasonYear}";

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>(partitionKey, "Config");
            
            return new PointsConfiguration
            {
                TierId = tierId,
                SeasonYear = seasonYear,
                ExactPositionPoints = entity.Value.GetInt32("ExactPositionPoints") ?? 10,
                OneOffPoints = entity.Value.GetInt32("OneOffPoints") ?? 5,
                TwoOffPoints = entity.Value.GetInt32("TwoOffPoints") ?? 3,
                InPodiumPoints = entity.Value.GetInt32("InPodiumPoints") ?? 1,
                CreatedDate = entity.Value.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            };
        }
        catch
        {
            // Return default configuration
            return new PointsConfiguration
            {
                TierId = tierId,
                SeasonYear = seasonYear,
                ExactPositionPoints = 10,
                OneOffPoints = 5,
                TwoOffPoints = 3,
                InPodiumPoints = 1,
                CreatedDate = DateTimeOffset.UtcNow
            };
        }
    }

    private int CalculatePoints(List<UserPrediction> predictions, List<EventResult> results, PointsConfiguration config)
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

// Helper class to return prediction results with points
public class UserPredictionResult
{
    public string EventId { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public DateTimeOffset EventDate { get; set; }
    public List<UserPrediction> Predictions { get; set; } = new();
    public List<EventResult> ActualResults { get; set; } = new();
    public int PointsEarned { get; set; }
}
