using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ITierRepository
{
    Task<List<Tier>> GetTiersBySportAsync(string sportId);
    Task<Tier?> GetTierByIdAsync(string sportId, string tierId);
    Task<List<Tier>> GetActiveTiersBySportAsync(string sportId);
}

public class TierRepository : ITierRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumTiers";

    public TierRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Tier>> GetTiersBySportAsync(string sportId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var tiers = new List<Tier>();

        try
        {
            var filter = $"PartitionKey eq '{sportId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                tiers.Add(MapToTier(entity));
            }
        }
        catch (RequestFailedException)
        {
            return tiers;
        }

        return tiers;
    }

    public async Task<Tier?> GetTierByIdAsync(string sportId, string tierId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(sportId, tierId);
            return MapToTier(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<List<Tier>> GetActiveTiersBySportAsync(string sportId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var tiers = new List<Tier>();

        try
        {
            var filter = $"PartitionKey eq '{sportId}' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                tiers.Add(MapToTier(entity));
            }
        }
        catch (RequestFailedException)
        {
            return tiers;
        }

        return tiers;
    }

    private static Tier MapToTier(TableEntity entity)
    {
        return new Tier
        {
            Id = entity.RowKey,
            SportId = entity.PartitionKey,
            Name = entity.GetString("Name") ?? string.Empty,
            DisplayName = entity.GetString("DisplayName") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
