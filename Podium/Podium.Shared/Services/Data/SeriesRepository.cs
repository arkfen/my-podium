using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISeriesRepository
{
    Task<List<Series>> GetSeriesByDisciplineAsync(string disciplineId);
    Task<Series?> GetSeriesByIdAsync(string disciplineId, string seriesId);
    Task<List<Series>> GetActiveSeriesByDisciplineAsync(string disciplineId);
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
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
