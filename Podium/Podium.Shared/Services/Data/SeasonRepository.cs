using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISeasonRepository
{
    Task<List<Season>> GetSeasonsBySeriesAsync(string seriesId);
    Task<Season?> GetSeasonByIdAsync(string seriesId, string seasonId);
    Task<Season?> GetActiveSeasonBySeriesAsync(string seriesId);
}

public class SeasonRepository : ISeasonRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumSeasons";

    public SeasonRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Season>> GetSeasonsBySeriesAsync(string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var seasons = new List<Season>();

        try
        {
            var filter = $"PartitionKey eq '{seriesId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                seasons.Add(MapToSeason(entity));
            }
        }
        catch (RequestFailedException)
        {
            return seasons;
        }

        return seasons.OrderByDescending(s => s.Year).ToList();
    }

    public async Task<Season?> GetSeasonByIdAsync(string seriesId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(seriesId, seasonId);
            return MapToSeason(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Season?> GetActiveSeasonBySeriesAsync(string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var filter = $"PartitionKey eq '{seriesId}' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                return MapToSeason(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    private static Season MapToSeason(TableEntity entity)
    {
        return new Season
        {
            Id = entity.RowKey,
            SeriesId = entity.PartitionKey,
            Year = entity.GetInt32("Year") ?? 0,
            Name = entity.GetString("Name") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            StartDate = entity.GetDateTimeOffset("StartDate")?.DateTime ?? DateTime.MinValue,
            EndDate = entity.GetDateTimeOffset("EndDate")?.DateTime,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
