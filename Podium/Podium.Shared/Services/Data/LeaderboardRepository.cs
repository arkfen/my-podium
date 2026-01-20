using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface ILeaderboardRepository
{
    Task<List<UserStatistics>> GetLeaderboardBySeasonAsync(string seasonId);
    Task<UserStatistics?> GetUserStatisticsAsync(string seasonId, string userId);
    Task<bool> UpsertUserStatisticsAsync(UserStatistics userStatistics);
    Task<List<string>> GetDistinctUserIdsForSeasonAsync(string seasonId);
}

public class LeaderboardRepository : ILeaderboardRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumUserStatistics";

    public LeaderboardRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<UserStatistics>> GetLeaderboardBySeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var leaderboard = new List<UserStatistics>();

        try
        {
            var filter = $"PartitionKey eq '{seasonId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                leaderboard.Add(MapToUserStatistics(entity));
            }
        }
        catch (RequestFailedException)
        {
            return leaderboard;
        }

        return leaderboard.OrderByDescending(us => us.TotalPoints).ToList();
    }

    public async Task<UserStatistics?> GetUserStatisticsAsync(string seasonId, string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(seasonId, userId);
            return MapToUserStatistics(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> UpsertUserStatisticsAsync(UserStatistics userStatistics)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = new TableEntity(userStatistics.SeasonId, userStatistics.UserId)
            {
                ["SeasonId"] = userStatistics.SeasonId,
                ["UserId"] = userStatistics.UserId,
                ["Username"] = userStatistics.Username,
                ["TotalPoints"] = userStatistics.TotalPoints,
                ["PredictionsCount"] = userStatistics.PredictionsCount,
                ["ExactMatches"] = userStatistics.ExactMatches,
                ["OneOffMatches"] = userStatistics.OneOffMatches,
                ["TwoOffMatches"] = userStatistics.TwoOffMatches,
                ["LastUpdated"] = DateTime.SpecifyKind(userStatistics.LastUpdated, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<List<string>> GetDistinctUserIdsForSeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var userIds = new HashSet<string>();

        try
        {
            var filter = $"PartitionKey eq '{seasonId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter, select: new[] { "RowKey" }))
            {
                userIds.Add(entity.RowKey);
            }
        }
        catch (RequestFailedException)
        {
            return userIds.ToList();
        }

        return userIds.ToList();
    }

    private static UserStatistics MapToUserStatistics(TableEntity entity)
    {
        return new UserStatistics
        {
            SeasonId = entity.PartitionKey,
            UserId = entity.RowKey,
            Username = entity.GetString("Username") ?? string.Empty,
            TotalPoints = entity.GetInt32("TotalPoints") ?? 0,
            PredictionsCount = entity.GetInt32("PredictionsCount") ?? 0,
            ExactMatches = entity.GetInt32("ExactMatches") ?? 0,
            OneOffMatches = entity.GetInt32("OneOffMatches") ?? 0,
            TwoOffMatches = entity.GetInt32("TwoOffMatches") ?? 0,
            LastUpdated = entity.GetDateTimeOffset("LastUpdated")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }
}
