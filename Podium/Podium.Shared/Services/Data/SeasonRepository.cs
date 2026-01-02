using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISeasonRepository
{
    Task<List<Season>> GetSeasonsByTierAsync(string tierId);
    Task<Season?> GetSeasonByIdAsync(string tierId, string seasonId);
    Task<Season?> GetActiveSeasonByTierAsync(string tierId);
}

public class SeasonRepository : ISeasonRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumSeasons";

    public SeasonRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Season>> GetSeasonsByTierAsync(string tierId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var seasons = new List<Season>();

        try
        {
            var filter = $"PartitionKey eq '{tierId}'";
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

    public async Task<Season?> GetSeasonByIdAsync(string tierId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(tierId, seasonId);
            return MapToSeason(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Season?> GetActiveSeasonByTierAsync(string tierId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var filter = $"PartitionKey eq '{tierId}' and IsActive eq true";
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
            TierId = entity.PartitionKey,
            Year = entity.GetInt32("Year") ?? 0,
            Name = entity.GetString("Name") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            StartDate = entity.GetDateTimeOffset("StartDate")?.DateTime ?? DateTime.MinValue,
            EndDate = entity.GetDateTimeOffset("EndDate")?.DateTime,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
