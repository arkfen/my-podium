using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IEventRepository
{
    Task<List<Event>> GetEventsBySeasonAsync(string seasonId);
    Task<Event?> GetEventByIdAsync(string seasonId, string eventId);
    Task<List<Event>> GetUpcomingEventsBySeasonAsync(string seasonId);
    Task<EventResult?> GetEventResultAsync(string eventId);
}

public class EventRepository : IEventRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string EventsTableName = "PodiumEvents";
    private const string ResultsTableName = "PodiumEventResults";

    public EventRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Event>> GetEventsBySeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);
        var events = new List<Event>();

        try
        {
            var filter = $"PartitionKey eq '{seasonId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                events.Add(MapToEvent(entity));
            }
        }
        catch (RequestFailedException)
        {
            return events;
        }

        return events.OrderBy(e => e.EventNumber).ToList();
    }

    public async Task<Event?> GetEventByIdAsync(string seasonId, string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(seasonId, eventId);
            return MapToEvent(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<List<Event>> GetUpcomingEventsBySeasonAsync(string seasonId)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);
        var events = new List<Event>();
        var now = DateTime.UtcNow;

        try
        {
            var filter = $"PartitionKey eq '{seasonId}' and Status eq 'Upcoming' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                var evt = MapToEvent(entity);
                if (evt.EventDate > now)
                {
                    events.Add(evt);
                }
            }
        }
        catch (RequestFailedException)
        {
            return events;
        }

        return events.OrderBy(e => e.EventDate).ToList();
    }

    public async Task<EventResult?> GetEventResultAsync(string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(ResultsTableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(eventId, "Result");
            return MapToEventResult(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    private static Event MapToEvent(TableEntity entity)
    {
        return new Event
        {
            Id = entity.RowKey,
            SeasonId = entity.PartitionKey,
            Name = entity.GetString("Name") ?? string.Empty,
            DisplayName = entity.GetString("DisplayName") ?? string.Empty,
            EventNumber = entity.GetInt32("EventNumber") ?? 0,
            EventDate = entity.GetDateTimeOffset("EventDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            Location = entity.GetString("Location") ?? string.Empty,
            Status = entity.GetString("Status") ?? "Upcoming",
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    private static EventResult MapToEventResult(TableEntity entity)
    {
        return new EventResult
        {
            EventId = entity.PartitionKey,
            FirstPlaceId = entity.GetString("FirstPlaceId") ?? string.Empty,
            FirstPlaceName = entity.GetString("FirstPlaceName") ?? string.Empty,
            SecondPlaceId = entity.GetString("SecondPlaceId") ?? string.Empty,
            SecondPlaceName = entity.GetString("SecondPlaceName") ?? string.Empty,
            ThirdPlaceId = entity.GetString("ThirdPlaceId") ?? string.Empty,
            ThirdPlaceName = entity.GetString("ThirdPlaceName") ?? string.Empty,
            UpdatedDate = entity.GetDateTimeOffset("UpdatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }
}
