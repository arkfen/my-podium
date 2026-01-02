using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ISportRepository
{
    Task<List<Sport>> GetAllSportsAsync();
    Task<Sport?> GetSportByIdAsync(string sportId);
    Task<List<Sport>> GetActiveSportsAsync();
}

public class SportRepository : ISportRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumSports";

    public SportRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Sport>> GetAllSportsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var sports = new List<Sport>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'Sport'"))
            {
                sports.Add(MapToSport(entity));
            }
        }
        catch (RequestFailedException)
        {
            // Table doesn't exist or other error
            return sports;
        }

        return sports;
    }

    public async Task<Sport?> GetSportByIdAsync(string sportId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>("Sport", sportId);
            return MapToSport(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<List<Sport>> GetActiveSportsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var sports = new List<Sport>();

        try
        {
            var filter = "PartitionKey eq 'Sport' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                sports.Add(MapToSport(entity));
            }
        }
        catch (RequestFailedException)
        {
            return sports;
        }

        return sports;
    }

    private static Sport MapToSport(TableEntity entity)
    {
        return new Sport
        {
            Id = entity.RowKey,
            Name = entity.GetString("Name") ?? string.Empty,
            DisplayName = entity.GetString("DisplayName") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
