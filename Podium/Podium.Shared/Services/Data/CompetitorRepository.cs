using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ICompetitorRepository
{
    Task<List<Competitor>> GetCompetitorsBySportAsync(string sportId);
    Task<List<SeasonCompetitor>> GetCompetitorsBySeasonAsync(string seasonId);
    Task<Competitor?> GetCompetitorByIdAsync(string sportId, string competitorId);
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

    public async Task<List<Competitor>> GetCompetitorsBySportAsync(string sportId)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);
        var competitors = new List<Competitor>();

        try
        {
            var filter = $"PartitionKey eq '{sportId}' and IsActive eq true";
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

    public async Task<Competitor?> GetCompetitorByIdAsync(string sportId, string competitorId)
    {
        var tableClient = _tableClientFactory.GetTableClient(CompetitorsTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(sportId, competitorId);
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
            SportId = entity.PartitionKey,
            Name = entity.GetString("Name") ?? string.Empty,
            ShortName = entity.GetString("ShortName") ?? string.Empty,
            Type = entity.GetString("Type") ?? "Individual",
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue
        };
    }

    private static SeasonCompetitor MapToSeasonCompetitor(TableEntity entity)
    {
        return new SeasonCompetitor
        {
            SeasonId = entity.PartitionKey,
            CompetitorId = entity.RowKey,
            CompetitorName = entity.GetString("CompetitorName") ?? string.Empty,
            JoinDate = entity.GetDateTimeOffset("JoinDate")?.DateTime ?? DateTime.MinValue
        };
    }
}
