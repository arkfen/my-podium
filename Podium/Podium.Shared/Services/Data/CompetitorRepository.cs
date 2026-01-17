using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ICompetitorRepository
{
    Task<List<Competitor>> GetAllCompetitorsAsync();
    Task<List<Competitor>> GetCompetitorsByTypeAsync(string type);
    Task<List<SeasonCompetitor>> GetCompetitorsBySeasonAsync(string seasonId);
    Task<Competitor?> GetCompetitorByIdAsync(string type, string competitorId);
    Task<Competitor?> GetCompetitorByIdOnlyAsync(string competitorId);
    Task<Competitor?> CreateCompetitorAsync(Competitor competitor);
    Task<Competitor?> UpdateCompetitorAsync(Competitor competitor);
    Task<bool> DeleteCompetitorAsync(string type, string competitorId);
    Task<bool> AddCompetitorToSeasonAsync(string seasonId, string competitorId, string competitorName);
    Task<bool> RemoveCompetitorFromSeasonAsync(string seasonId, string competitorId);
    Task<List<string>> GetCompetitorSeasonIdsAsync(string competitorId);
    Task<CompetitorDependencies> GetCompetitorDependenciesAsync(string competitorId);
}

public class CompetitorRepository : ICompetitorRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string CompetitorsTableName = "PodiumCompetitors";
    private const string SeasonCompetitorsTableName = "PodiumSeasonCompetitors";

    public CompetitorRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Competitor>> GetAllCompetitorsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);
        var competitors = new List<Competitor>();

        try
        {
            // Query all competitors (both Individual and Team)
            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                competitors.Add(MapToCompetitor(entity));
            }
        }
        catch (RequestFailedException)
        {
            return competitors;
        }

        return competitors.OrderBy(c => c.Name).ToList();
    }

    public async Task<List<Competitor>> GetCompetitorsByTypeAsync(string type)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);
        var competitors = new List<Competitor>();

        try
        {
            var filter = $"PartitionKey eq '{type}' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                competitors.Add(MapToCompetitor(entity));
            }
        }
        catch (RequestFailedException)
        {
            return competitors;
        }

        return competitors.OrderBy(c => c.Name).ToList();
    }

    public async Task<List<SeasonCompetitor>> GetCompetitorsBySeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(SeasonCompetitorsTableName);
        var seasonCompetitors = new List<SeasonCompetitor>();

        try
        {
            var filter = $"PartitionKey eq '{seasonId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                seasonCompetitors.Add(MapToSeasonCompetitor(entity));
            }
        }
        catch (RequestFailedException)
        {
            return seasonCompetitors;
        }

        return seasonCompetitors.OrderBy(sc => sc.CompetitorName).ToList();
    }

    public async Task<Competitor?> GetCompetitorByIdAsync(string type, string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(type, competitorId);
            return MapToCompetitor(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    private static Competitor MapToCompetitor(TableEntity entity)
    {
        return new Competitor
        {
            Id = entity.RowKey,
            Type = entity.PartitionKey, // PartitionKey is now Type ("Individual" or "Team")
            Name = entity.GetString("Name") ?? string.Empty,
            ShortName = entity.GetString("ShortName") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    private static SeasonCompetitor MapToSeasonCompetitor(TableEntity entity)
    {
        return new SeasonCompetitor
        {
            SeasonId = entity.PartitionKey,
            CompetitorId = entity.RowKey,
            CompetitorName = entity.GetString("CompetitorName") ?? string.Empty,
            JoinDate = entity.GetDateTimeOffset("JoinDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    public async Task<Competitor?> GetCompetitorByIdOnlyAsync(string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            // Query across all partitions to find the competitor by RowKey
            var filter = $"RowKey eq '{competitorId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                return MapToCompetitor(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<Competitor?> CreateCompetitorAsync(Competitor competitor)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            competitor.Id = Guid.NewGuid().ToString();
            competitor.CreatedDate = DateTime.UtcNow;

            // PartitionKey = Type ("Individual" or "Team")
            var entity = new TableEntity(competitor.Type, competitor.Id)
            {
                ["Type"] = competitor.Type,
                ["Name"] = competitor.Name,
                ["ShortName"] = competitor.ShortName,
                ["IsActive"] = competitor.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(competitor.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.AddEntityAsync(entity);
            return competitor;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Competitor?> UpdateCompetitorAsync(Competitor competitor)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            // PartitionKey = Type ("Individual" or "Team")
            var entity = new TableEntity(competitor.Type, competitor.Id)
            {
                ["Type"] = competitor.Type,
                ["Name"] = competitor.Name,
                ["ShortName"] = competitor.ShortName,
                ["IsActive"] = competitor.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(competitor.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return competitor;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteCompetitorAsync(string type, string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            await tableClient.DeleteEntityAsync(type, competitorId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> AddCompetitorToSeasonAsync(string seasonId, string competitorId, string competitorName)
    {
        var tableClient = _tableClientFactory.GetTableClient(SeasonCompetitorsTableName);

        try
        {
            var entity = new TableEntity(seasonId, competitorId)
            {
                ["SeasonId"] = seasonId,
                ["CompetitorId"] = competitorId,
                ["CompetitorName"] = competitorName,
                ["JoinDate"] = DateTime.UtcNow
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> RemoveCompetitorFromSeasonAsync(string seasonId, string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(SeasonCompetitorsTableName);

        try
        {
            await tableClient.DeleteEntityAsync(seasonId, competitorId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<List<string>> GetCompetitorSeasonIdsAsync(string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(SeasonCompetitorsTableName);
        var seasonIds = new List<string>();

        try
        {
            // Query across all partitions where RowKey (competitorId) matches
            var filter = $"RowKey eq '{competitorId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                seasonIds.Add(entity.PartitionKey); // PartitionKey is the seasonId
            }
        }
        catch (RequestFailedException)
        {
            return seasonIds;
        }

        return seasonIds;
    }

    public async Task<CompetitorDependencies> GetCompetitorDependenciesAsync(string competitorId)
    {
        var dependencies = new CompetitorDependencies();

        try
        {
            // Count season assignments
            var seasonTableClient = _tableClientFactory.GetTableClient(SeasonCompetitorsTableName);
            var seasonFilter = $"RowKey eq '{competitorId}'";
            await foreach (var _ in seasonTableClient.QueryAsync<TableEntity>(filter: seasonFilter))
            {
                dependencies.SeasonCount++;
            }

            // Count event results (check both first, second, and third place)
            var resultsTableClient = _tableClientFactory.GetTableClient("PodiumEventResults");
            var firstPlaceFilter = $"FirstPlaceId eq '{competitorId}'";
            var secondPlaceFilter = $"SecondPlaceId eq '{competitorId}'";
            var thirdPlaceFilter = $"ThirdPlaceId eq '{competitorId}'";

            await foreach (var _ in resultsTableClient.QueryAsync<TableEntity>(filter: firstPlaceFilter))
            {
                dependencies.ResultCount++;
            }
            await foreach (var _ in resultsTableClient.QueryAsync<TableEntity>(filter: secondPlaceFilter))
            {
                dependencies.ResultCount++;
            }
            await foreach (var _ in resultsTableClient.QueryAsync<TableEntity>(filter: thirdPlaceFilter))
            {
                dependencies.ResultCount++;
            }
        }
        catch (RequestFailedException)
        {
            // Tables don't exist or error - return 0s
        }

        return dependencies;
    }
}




