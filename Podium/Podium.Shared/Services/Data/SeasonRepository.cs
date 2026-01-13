using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISeasonRepository
{
    Task<List<Season>> GetSeasonsBySeriesAsync(string seriesId);
    Task<Season?> GetSeasonByIdAsync(string seriesId, string seasonId);
    Task<Season?> GetActiveSeasonBySeriesAsync(string seriesId);
    Task<bool> SetActiveSeasonAsync(string seriesId, string seasonId);
    Task<Dictionary<string, List<Season>>> FindSeriesWithMultipleActiveSeasonsAsync();
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

    public async Task<bool> SetActiveSeasonAsync(string seriesId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // First, deactivate all seasons in this series
            var filter = $"PartitionKey eq '{seriesId}' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                entity["IsActive"] = false;
                await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            }

            // Then, activate the specified season
            var targetEntity = await tableClient.GetEntityAsync<TableEntity>(seriesId, seasonId);
            targetEntity.Value["IsActive"] = true;
            await tableClient.UpdateEntityAsync(targetEntity.Value, targetEntity.Value.ETag, TableUpdateMode.Replace);

            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<Dictionary<string, List<Season>>> FindSeriesWithMultipleActiveSeasonsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var result = new Dictionary<string, List<Season>>();

        try
        {
            var filter = "IsActive eq true";
            var activeSeasons = new List<Season>();

            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                activeSeasons.Add(MapToSeason(entity));
            }

            // Group by SeriesId and find those with more than 1 active season
            var duplicates = activeSeasons
                .GroupBy(s => s.SeriesId)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g => g.ToList());

            return duplicates;
        }
        catch (RequestFailedException)
        {
            return result;
        }
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

    private static TableEntity MapToTableEntity(Season season)
    {
        var entity = new TableEntity(season.SeriesId, season.Id)
        {
            { "Year", season.Year },
            { "Name", season.Name },
            { "IsActive", season.IsActive },
            { "StartDate", season.StartDate },
            { "EndDate", season.EndDate },
            { "CreatedDate", season.CreatedDate }
        };

        return entity;
    }
}
