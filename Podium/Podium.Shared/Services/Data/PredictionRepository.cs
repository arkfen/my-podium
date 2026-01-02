using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IPredictionRepository
{
    Task<Prediction?> GetPredictionAsync(string eventId, string userId);
    Task<List<Prediction>> GetPredictionsByEventAsync(string eventId);
    Task<List<Prediction>> GetPredictionsByUserAndSeasonAsync(string userId, string seasonId, List<string> eventIds);
    Task<bool> SavePredictionAsync(Prediction prediction);
}

public class PredictionRepository : IPredictionRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumPredictions";

    public PredictionRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<Prediction?> GetPredictionAsync(string eventId, string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(eventId, userId);
            return MapToPrediction(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<List<Prediction>> GetPredictionsByEventAsync(string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var predictions = new List<Prediction>();

        try
        {
            var filter = $"PartitionKey eq '{eventId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                predictions.Add(MapToPrediction(entity));
            }
        }
        catch (RequestFailedException)
        {
            return predictions;
        }

        return predictions;
    }

    public async Task<List<Prediction>> GetPredictionsByUserAndSeasonAsync(string userId, string seasonId, List<string> eventIds)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var predictions = new List<Prediction>();

        try
        {
            foreach (var eventId in eventIds)
            {
                try
                {
                    var response = await tableClient.GetEntityAsync<TableEntity>(eventId, userId);
                    predictions.Add(MapToPrediction(response.Value));
                }
                catch (RequestFailedException)
                {
                    // No prediction for this event, continue
                }
            }
        }
        catch (RequestFailedException)
        {
            return predictions;
        }

        return predictions;
    }

    public async Task<bool> SavePredictionAsync(Prediction prediction)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = new TableEntity(prediction.EventId, prediction.UserId)
            {
                ["EventId"] = prediction.EventId,
                ["UserId"] = prediction.UserId,
                ["FirstPlaceId"] = prediction.FirstPlaceId,
                ["FirstPlaceName"] = prediction.FirstPlaceName,
                ["SecondPlaceId"] = prediction.SecondPlaceId,
                ["SecondPlaceName"] = prediction.SecondPlaceName,
                ["ThirdPlaceId"] = prediction.ThirdPlaceId,
                ["ThirdPlaceName"] = prediction.ThirdPlaceName,
                ["PointsEarned"] = prediction.PointsEarned,
                ["SubmittedDate"] = prediction.SubmittedDate,
                ["UpdatedDate"] = prediction.UpdatedDate
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static Prediction MapToPrediction(TableEntity entity)
    {
        return new Prediction
        {
            EventId = entity.PartitionKey,
            UserId = entity.RowKey,
            FirstPlaceId = entity.GetString("FirstPlaceId") ?? string.Empty,
            FirstPlaceName = entity.GetString("FirstPlaceName") ?? string.Empty,
            SecondPlaceId = entity.GetString("SecondPlaceId") ?? string.Empty,
            SecondPlaceName = entity.GetString("SecondPlaceName") ?? string.Empty,
            ThirdPlaceId = entity.GetString("ThirdPlaceId") ?? string.Empty,
            ThirdPlaceName = entity.GetString("ThirdPlaceName") ?? string.Empty,
            PointsEarned = entity.GetInt32("PointsEarned"),
            SubmittedDate = entity.GetDateTimeOffset("SubmittedDate")?.DateTime ?? DateTime.MinValue,
            UpdatedDate = entity.GetDateTimeOffset("UpdatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
