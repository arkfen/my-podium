using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services;

public interface IEventService
{
    Task<List<Event>> GetEventsBySeasonAsync(string tierId, string seasonYear);
    Task<Event?> GetEventByIdAsync(string tierId, string seasonYear, string eventId);
    Task<List<SeasonParticipant>> GetSeasonParticipantsAsync(string tierId, string seasonYear);
}

public class EventService : IEventService
{
    private readonly ITableStorageService _storageService;
    private const string EVENTS_TABLE = "PodiumEvents";
    private const string SEASON_PARTICIPANTS_TABLE = "PodiumSeasonParticipants";

    public EventService(ITableStorageService storageService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task<List<Event>> GetEventsBySeasonAsync(string tierId, string seasonYear)
    {
        var tableClient = _storageService.GetTableClient(EVENTS_TABLE);
        var partitionKey = $"{tierId}_{seasonYear}";
        var events = new List<Event>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{partitionKey}' and IsActive eq true"))
        {
            events.Add(new Event
            {
                Id = entity.RowKey,
                TierId = tierId,
                SeasonYear = seasonYear,
                Name = entity.GetString("Name") ?? string.Empty,
                Location = entity.GetString("Location") ?? string.Empty,
                EventDate = entity.GetDateTimeOffset("EventDate") ?? DateTimeOffset.MinValue,
                PredictionCutoffDate = entity.GetDateTimeOffset("PredictionCutoffDate") ?? DateTimeOffset.MinValue,
                Round = entity.GetInt32("Round") ?? 0,
                IsCompleted = entity.GetBoolean("IsCompleted") ?? false,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            });
        }

        return events.OrderBy(e => e.Round).ToList();
    }

    public async Task<Event?> GetEventByIdAsync(string tierId, string seasonYear, string eventId)
    {
        var tableClient = _storageService.GetTableClient(EVENTS_TABLE);
        var partitionKey = $"{tierId}_{seasonYear}";

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>(partitionKey, eventId);
            
            return new Event
            {
                Id = entity.Value.RowKey,
                TierId = tierId,
                SeasonYear = seasonYear,
                Name = entity.Value.GetString("Name") ?? string.Empty,
                Location = entity.Value.GetString("Location") ?? string.Empty,
                EventDate = entity.Value.GetDateTimeOffset("EventDate") ?? DateTimeOffset.MinValue,
                PredictionCutoffDate = entity.Value.GetDateTimeOffset("PredictionCutoffDate") ?? DateTimeOffset.MinValue,
                Round = entity.Value.GetInt32("Round") ?? 0,
                IsCompleted = entity.Value.GetBoolean("IsCompleted") ?? false,
                IsActive = entity.Value.GetBoolean("IsActive") ?? false,
                CreatedDate = entity.Value.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<SeasonParticipant>> GetSeasonParticipantsAsync(string tierId, string seasonYear)
    {
        var tableClient = _storageService.GetTableClient(SEASON_PARTICIPANTS_TABLE);
        var partitionKey = $"{tierId}_{seasonYear}";
        var participants = new List<SeasonParticipant>();

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq '{partitionKey}' and IsActive eq true"))
        {
            participants.Add(new SeasonParticipant
            {
                CompetitorId = entity.RowKey,
                TierId = tierId,
                SeasonYear = seasonYear,
                CompetitorName = entity.GetString("CompetitorName") ?? string.Empty,
                Team = entity.GetString("Team") ?? string.Empty,
                Number = entity.GetString("Number") ?? string.Empty,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                JoinedDate = entity.GetDateTimeOffset("JoinedDate") ?? DateTimeOffset.MinValue
            });
        }

        return participants.OrderBy(p => p.CompetitorName).ToList();
    }
}
