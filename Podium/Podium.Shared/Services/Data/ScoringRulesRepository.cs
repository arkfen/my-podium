using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IScoringRulesRepository
{
    Task<ScoringRules?> GetScoringRulesBySeasonAsync(string seasonId);
    Task<ScoringRules?> CreateOrUpdateScoringRulesAsync(ScoringRules scoringRules);
    Task<bool> DeleteScoringRulesAsync(string seasonId);
}

public class ScoringRulesRepository : IScoringRulesRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumScoringRules";
    private const string RowKeyValue = "Scoring";

    public ScoringRulesRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<ScoringRules?> GetScoringRulesBySeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(seasonId, RowKeyValue);
            return MapToScoringRules(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<ScoringRules?> CreateOrUpdateScoringRulesAsync(ScoringRules scoringRules)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            scoringRules.CreatedDate = DateTime.UtcNow;
            var entity = MapToTableEntity(scoringRules);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return scoringRules;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteScoringRulesAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            await tableClient.DeleteEntityAsync(seasonId, RowKeyValue);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static ScoringRules MapToScoringRules(TableEntity entity)
    {
        return new ScoringRules
        {
            SeasonId = entity.PartitionKey,
            ExactMatchPoints = entity.GetInt32("ExactMatchPoints") ?? 25,
            OneOffPoints = entity.GetInt32("OneOffPoints") ?? 18,
            TwoOffPoints = entity.GetInt32("TwoOffPoints") ?? 15,
            CreatedDate = entity.GetDateTime("CreatedDate") ?? DateTime.UtcNow
        };
    }

    private static TableEntity MapToTableEntity(ScoringRules scoringRules)
    {
        return new TableEntity(scoringRules.SeasonId, RowKeyValue)
        {
            { "SeasonId", scoringRules.SeasonId },
            { "ExactMatchPoints", scoringRules.ExactMatchPoints },
            { "OneOffPoints", scoringRules.OneOffPoints },
            { "TwoOffPoints", scoringRules.TwoOffPoints },
            { "CreatedDate", scoringRules.CreatedDate }
        };
    }
}
