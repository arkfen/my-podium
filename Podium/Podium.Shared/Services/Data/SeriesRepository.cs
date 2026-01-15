using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISeriesRepository
{
    Task<List<Series>> GetSeriesByDisciplineAsync(string disciplineId);
    Task<Series?> GetSeriesByIdAsync(string disciplineId, string seriesId);
    Task<Series?> GetSeriesByIdOnlyAsync(string seriesId);
    Task<List<Series>> GetActiveSeriesByDisciplineAsync(string disciplineId);
    Task<Series?> CreateSeriesAsync(Series series);
    Task<Series?> UpdateSeriesAsync(Series series, string? oldDisciplineId = null);
    Task<bool> DeleteSeriesAsync(string disciplineId, string seriesId);
}

public class SeriesRepository : ISeriesRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumSeries";

    public SeriesRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Series>> GetSeriesByDisciplineAsync(string disciplineId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var series = new List<Series>();

        try
        {
            var filter = $"PartitionKey eq '{disciplineId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                series.Add(MapToSeries(entity));
            }
        }
        catch (RequestFailedException)
        {
            return series;
        }

        return series;
    }

    public async Task<Series?> GetSeriesByIdAsync(string disciplineId, string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(disciplineId, seriesId);
            return MapToSeries(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Series?> GetSeriesByIdOnlyAsync(string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // Query across all partitions to find the series by RowKey
            var filter = $"RowKey eq '{seriesId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                return MapToSeries(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<List<Series>> GetActiveSeriesByDisciplineAsync(string disciplineId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var series = new List<Series>();

        try
        {
            var filter = $"PartitionKey eq '{disciplineId}' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                series.Add(MapToSeries(entity));
            }
        }
        catch (RequestFailedException)
        {
            return series;
        }

        return series;
    }

    private static Series MapToSeries(TableEntity entity)
    {
        return new Series
        {
            Id = entity.RowKey,
            DisciplineId = entity.PartitionKey,
            Name = entity.GetString("Name") ?? string.Empty,
            DisplayName = entity.GetString("DisplayName") ?? string.Empty,
            GoverningBody = entity.GetString("GoverningBody") ?? string.Empty,
            Region = entity.GetString("Region") ?? string.Empty,
            VehicleType = entity.GetString("VehicleType") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    public async Task<Series?> CreateSeriesAsync(Series series)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            series.Id = Guid.NewGuid().ToString();
            series.CreatedDate = DateTime.UtcNow;

            var entity = new TableEntity(series.DisciplineId, series.Id)
            {
                ["Name"] = series.Name,
                ["DisplayName"] = series.DisplayName,
                ["GoverningBody"] = series.GoverningBody,
                ["Region"] = series.Region,
                ["VehicleType"] = series.VehicleType,
                ["IsActive"] = series.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(series.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.AddEntityAsync(entity);
            return series;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Series?> UpdateSeriesAsync(Series series, string? oldDisciplineId = null)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // Determine the discipline ID where the series currently exists
            var currentDisciplineId = oldDisciplineId ?? series.DisciplineId;
            
            // Check if discipline is changing (PartitionKey change required)
            bool disciplineChanged = !string.IsNullOrEmpty(oldDisciplineId) && oldDisciplineId != series.DisciplineId;

            if (disciplineChanged)
            {
                // Delete from old partition
                await tableClient.DeleteEntityAsync(oldDisciplineId, series.Id);
            }

            // Create/Update in the (new or same) partition
            var entity = new TableEntity(series.DisciplineId, series.Id)
            {
                ["Name"] = series.Name,
                ["DisplayName"] = series.DisplayName,
                ["GoverningBody"] = series.GoverningBody,
                ["Region"] = series.Region,
                ["VehicleType"] = series.VehicleType,
                ["IsActive"] = series.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(series.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return series;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteSeriesAsync(string disciplineId, string seriesId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            await tableClient.DeleteEntityAsync(disciplineId, seriesId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }
}
