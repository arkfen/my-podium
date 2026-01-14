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
    Task<int> GetSeasonCountBySeriesAsync(string seriesId);
    Task<Season?> CreateSeasonAsync(Season season);
    Task<Season?> UpdateSeasonAsync(Season season, string? oldSeriesId = null);
    Task<bool> DeleteSeasonAsync(string seriesId, string seasonId);
    Task<SeasonDependencies> GetSeasonDependenciesAsync(string seasonId);
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
            StartDate = entity.GetDateTimeOffset("StartDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            EndDate = entity.GetDateTimeOffset("EndDate")?.UtcDateTime,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    private static TableEntity MapToTableEntity(Season season)
    {
        var entity = new TableEntity(season.SeriesId, season.Id)
        {
            { "Year", season.Year },
            { "Name", season.Name },
            { "IsActive", season.IsActive },
            { "StartDate", DateTime.SpecifyKind(season.StartDate, DateTimeKind.Utc) },
            { "EndDate", season.EndDate.HasValue ? DateTime.SpecifyKind(season.EndDate.Value, DateTimeKind.Utc) : (DateTime?)null },
            { "CreatedDate", DateTime.SpecifyKind(season.CreatedDate, DateTimeKind.Utc) }
        };

        return entity;
    }

    public async Task<int> GetSeasonCountBySeriesAsync(string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        int count = 0;

        try
        {
            var filter = $"PartitionKey eq '{seriesId}'";
            await foreach (var _ in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                count++;
            }
        }
        catch (RequestFailedException)
        {
            // Table doesn't exist or error - return 0
        }

        return count;
    }

    public async Task<Season?> CreateSeasonAsync(Season season)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            season.Id = Guid.NewGuid().ToString();
            season.CreatedDate = DateTime.UtcNow;

            var entity = MapToTableEntity(season);
            await tableClient.AddEntityAsync(entity);
            return season;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Season?> UpdateSeasonAsync(Season season, string? oldSeriesId = null)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // Check if series is changing (PartitionKey change required)
            bool seriesChanged = !string.IsNullOrEmpty(oldSeriesId) && oldSeriesId != season.SeriesId;

            if (seriesChanged)
            {
                // Delete from old partition
                await tableClient.DeleteEntityAsync(oldSeriesId, season.Id);
            }

            // Create/Update in the (new or same) partition
            var entity = MapToTableEntity(season);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return season;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteSeasonAsync(string seriesId, string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            await tableClient.DeleteEntityAsync(seriesId, seasonId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<SeasonDependencies> GetSeasonDependenciesAsync(string seasonId)
    {
        var dependencies = new SeasonDependencies();

        try
        {
            // Count events
            var eventsTableClient = _tableClientFactory.GetTableClient("PodiumEvents");
            var eventFilter = $"PartitionKey eq '{seasonId}'";
            await foreach (var _ in eventsTableClient.QueryAsync<TableEntity>(filter: eventFilter))
            {
                dependencies.EventCount++;
            }

            // Count competitors
            var competitorsTableClient = _tableClientFactory.GetTableClient("PodiumSeasonCompetitors");
            var competitorFilter = $"PartitionKey eq '{seasonId}'";
            await foreach (var _ in competitorsTableClient.QueryAsync<TableEntity>(filter: competitorFilter))
            {
                dependencies.CompetitorCount++;
            }
        }
        catch (RequestFailedException)
        {
            // Tables don't exist or error - return 0s
        }

        return dependencies;
    }
}
