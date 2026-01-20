using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IStatisticsJobRepository
{
    Task<StatisticsRecalculationJob?> GetJobAsync(string jobId);
    Task<bool> SaveJobAsync(StatisticsRecalculationJob job);
    Task<bool> UpdateJobAsync(StatisticsRecalculationJob job);
}

public class StatisticsJobRepository : IStatisticsJobRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumStatisticsJobs";

    public StatisticsJobRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<StatisticsRecalculationJob?> GetJobAsync(string jobId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>("Job", jobId);
            return MapToJob(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> SaveJobAsync(StatisticsRecalculationJob job)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = CreateEntity(job);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> UpdateJobAsync(StatisticsRecalculationJob job)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = CreateEntity(job);
            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static TableEntity CreateEntity(StatisticsRecalculationJob job)
    {
        var entity = new TableEntity("Job", job.JobId)
        {
            ["JobId"] = job.JobId,
            ["SeasonId"] = job.SeasonId,
            ["Status"] = job.Status,
            ["TotalUsers"] = job.TotalUsers,
            ["ProcessedUsers"] = job.ProcessedUsers,
            ["StartedAt"] = DateTime.SpecifyKind(job.StartedAt, DateTimeKind.Utc)
        };

        if (job.CompletedAt.HasValue)
        {
            entity["CompletedAt"] = DateTime.SpecifyKind(job.CompletedAt.Value, DateTimeKind.Utc);
        }

        if (!string.IsNullOrEmpty(job.ErrorMessage))
        {
            entity["ErrorMessage"] = job.ErrorMessage;
        }

        return entity;
    }

    private static StatisticsRecalculationJob MapToJob(TableEntity entity)
    {
        return new StatisticsRecalculationJob
        {
            JobId = entity.RowKey,
            SeasonId = entity.GetString("SeasonId") ?? string.Empty,
            Status = entity.GetString("Status") ?? "Pending",
            TotalUsers = entity.GetInt32("TotalUsers") ?? 0,
            ProcessedUsers = entity.GetInt32("ProcessedUsers") ?? 0,
            StartedAt = entity.GetDateTimeOffset("StartedAt")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            CompletedAt = entity.GetDateTimeOffset("CompletedAt")?.UtcDateTime,
            ErrorMessage = entity.GetString("ErrorMessage")
        };
    }
}
