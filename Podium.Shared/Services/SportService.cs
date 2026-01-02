using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services;

public interface ISportService
{
    Task<List<Sport>> GetAllSportsAsync();
    Task<List<Tier>> GetTiersBySportIdAsync(string sportId);
    Task<List<Season>> GetSeasonsByTierIdAsync(string tierId);
}

public class SportService : ISportService
{
    private readonly ITableStorageService _storageService;
    private const string SPORTS_TABLE = "PodiumSports";
    private const string TIERS_TABLE = "PodiumTiers";
    private const string SEASONS_TABLE = "PodiumSeasons";

    public SportService(ITableStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task<List<Sport>> GetAllSportsAsync()
    {
        var tableClient = _storageService.GetTableClient(SPORTS_TABLE);
        var sports = new List<Sport>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq 'Sport' and IsActive eq true"))
        {
            sports.Add(new Sport
            {
                Id = entity.RowKey,
                Name = entity.GetString("Name") ?? string.Empty,
                Description = entity.GetString("Description") ?? string.Empty,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            });
        }

        return sports.OrderBy(s => s.Name).ToList();
    }

    public async Task<List<Tier>> GetTiersBySportIdAsync(string sportId)
    {
        var tableClient = _storageService.GetTableClient(TIERS_TABLE);
        var tiers = new List<Tier>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{sportId}' and IsActive eq true"))
        {
            tiers.Add(new Tier
            {
                Id = entity.RowKey,
                SportId = entity.PartitionKey,
                Name = entity.GetString("Name") ?? string.Empty,
                ShortName = entity.GetString("ShortName") ?? string.Empty,
                Description = entity.GetString("Description") ?? string.Empty,
                DisplayOrder = entity.GetInt32("DisplayOrder") ?? 0,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            });
        }

        return tiers.OrderBy(t => t.DisplayOrder).ToList();
    }

    public async Task<List<Season>> GetSeasonsByTierIdAsync(string tierId)
    {
        var tableClient = _storageService.GetTableClient(SEASONS_TABLE);
        var seasons = new List<Season>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{tierId}' and IsActive eq true"))
        {
            seasons.Add(new Season
            {
                Year = entity.RowKey,
                TierId = entity.PartitionKey,
                Name = entity.GetString("Name") ?? string.Empty,
                StartDate = entity.GetDateTimeOffset("StartDate") ?? DateTimeOffset.MinValue,
                EndDate = entity.GetDateTimeOffset("EndDate") ?? DateTimeOffset.MinValue,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            });
        }

        return seasons.OrderByDescending(s => s.Year).ToList();
    }
}
