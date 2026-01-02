using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services;

public interface IPredictionService
{
    Task<bool> SavePredictionAsync(string userId, string eventId, List<UserPrediction> predictions);
    Task<List<UserPrediction>> GetUserPredictionsForEventAsync(string userId, string eventId);
    Task<bool> HasUserPredictedAsync(string userId, string eventId);
}

public class PredictionService : IPredictionService
{
    private readonly ITableStorageService _storageService;
    private const string USER_PREDICTIONS_TABLE = "PodiumUserPredictions";

    public PredictionService(ITableStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task<bool> SavePredictionAsync(string userId, string eventId, List<UserPrediction> predictions)
    {
        if (predictions.Count != 3)
        {
            throw new ArgumentException("Exactly 3 predictions (P1, P2, P3) are required.", nameof(predictions));
        }

        var tableClient = _storageService.GetTableClient(USER_PREDICTIONS_TABLE);
        var partitionKey = $"{userId}_{eventId}";

        try
        {
            // Save all three predictions
            foreach (var prediction in predictions)
            {
                var entity = new TableEntity(partitionKey, $"P{prediction.Position}")
                {
                    ["CompetitorId"] = prediction.CompetitorId,
                    ["CompetitorName"] = prediction.CompetitorName,
                    ["PredictedDate"] = prediction.PredictedDate,
                    ["PointsAwarded"] = prediction.PointsAwarded
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

    public async Task<List<UserPrediction>> GetUserPredictionsForEventAsync(string userId, string eventId)
    {
        var tableClient = _storageService.GetTableClient(USER_PREDICTIONS_TABLE);
        var partitionKey = $"{userId}_{eventId}";
        var predictions = new List<UserPrediction>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{partitionKey}'"))
            {
                var positionStr = entity.RowKey.Replace("P", "");
                if (int.TryParse(positionStr, out int position))
                {
                    predictions.Add(new UserPrediction
                    {
                        UserId = userId,
                        EventId = eventId,
                        Position = position,
                        CompetitorId = entity.GetString("CompetitorId") ?? string.Empty,
                        CompetitorName = entity.GetString("CompetitorName") ?? string.Empty,
                        PredictedDate = entity.GetDateTimeOffset("PredictedDate") ?? DateTimeOffset.MinValue,
                        PointsAwarded = entity.GetInt32("PointsAwarded") ?? 0
                    });
                }
            }
        }
        catch
        {
            // Return empty list if no predictions found
        }

        return predictions.OrderBy(p => p.Position).ToList();
    }

    public async Task<bool> HasUserPredictedAsync(string userId, string eventId)
    {
        var predictions = await GetUserPredictionsForEventAsync(userId, eventId);
        return predictions.Count == 3;
    }
}
