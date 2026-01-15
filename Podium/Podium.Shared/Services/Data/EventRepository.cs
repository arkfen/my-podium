using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IEventRepository
{
    Task<List<Event>> GetEventsBySeasonAsync(string seasonId);
    Task<List<Event>> GetAllEventsAsync();
    Task<Event?> GetEventByIdAsync(string seasonId, string eventId);
    Task<Event?> GetEventByIdOnlyAsync(string eventId);
    Task<List<Event>> GetUpcomingEventsBySeasonAsync(string seasonId);
    Task<EventResult?> GetEventResultAsync(string eventId);
    Task<EventResult?> CreateOrUpdateEventResultAsync(string eventId, EventResult result);
    Task<bool> DeleteEventResultAsync(string eventId);
    Task<Event?> CreateEventAsync(Event evt);
    Task<Event?> UpdateEventAsync(Event evt);
    Task<bool> DeleteEventAsync(string seasonId, string eventId);
    Task<EventDependencies> GetEventDependenciesAsync(string eventId);
    Task<int> GetNextEventNumberAsync(string seasonId);
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

    public async Task<EventResult?> CreateOrUpdateEventResultAsync(string eventId, EventResult result)
    {
        var tableClient = _tableClientFactory.GetTableClient(ResultsTableName);

        try
        {
            result.EventId = eventId;
            result.UpdatedDate = DateTime.UtcNow;

            var entity = new TableEntity(eventId, "Result")
            {
                ["EventId"] = eventId,
                ["FirstPlaceId"] = result.FirstPlaceId,
                ["FirstPlaceName"] = result.FirstPlaceName,
                ["SecondPlaceId"] = result.SecondPlaceId,
                ["SecondPlaceName"] = result.SecondPlaceName,
                ["ThirdPlaceId"] = result.ThirdPlaceId,
                ["ThirdPlaceName"] = result.ThirdPlaceName,
                ["UpdatedDate"] = DateTime.SpecifyKind(result.UpdatedDate, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return result;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteEventResultAsync(string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(ResultsTableName);

        try
        {
            await tableClient.DeleteEntityAsync(eventId, "Result");
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
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

    public async Task<List<Event>> GetAllEventsAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);
        var events = new List<Event>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                events.Add(MapToEvent(entity));
            }
        }
        catch (RequestFailedException)
        {
            return events;
        }

        return events.OrderByDescending(e => e.EventDate).ToList();
    }

    public async Task<Event?> GetEventByIdOnlyAsync(string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);

        try
        {
            var filter = $"RowKey eq '{eventId}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                return MapToEvent(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<Event?> CreateEventAsync(Event evt)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);

        try
        {
            evt.Id = Guid.NewGuid().ToString();
            evt.CreatedDate = DateTime.UtcNow;

            var entity = new TableEntity(evt.SeasonId, evt.Id)
            {
                ["Name"] = evt.Name,
                ["DisplayName"] = evt.DisplayName,
                ["EventNumber"] = evt.EventNumber,
                ["EventDate"] = DateTime.SpecifyKind(evt.EventDate, DateTimeKind.Utc),
                ["Location"] = evt.Location,
                ["Status"] = evt.Status,
                ["IsActive"] = evt.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(evt.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.AddEntityAsync(entity);
            return evt;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Event?> UpdateEventAsync(Event evt)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);

        try
        {
            var entity = new TableEntity(evt.SeasonId, evt.Id)
            {
                ["Name"] = evt.Name,
                ["DisplayName"] = evt.DisplayName,
                ["EventNumber"] = evt.EventNumber,
                ["EventDate"] = DateTime.SpecifyKind(evt.EventDate, DateTimeKind.Utc),
                ["Location"] = evt.Location,
                ["Status"] = evt.Status,
                ["IsActive"] = evt.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(evt.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return evt;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteEventAsync(string seasonId, string eventId)
    {
        var tableClient = _tableClientFactory.GetTableClient(EventsTableName);

        try
        {
            await tableClient.DeleteEntityAsync(seasonId, eventId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<EventDependencies> GetEventDependenciesAsync(string eventId)
    {
        var dependencies = new EventDependencies();

        try
        {
            // Check for predictions
            var predictionsTableClient = _tableClientFactory.GetTableClient("PodiumPredictions");
            var predictionsFilter = $"PartitionKey eq '{eventId}'";
            await foreach (var _ in predictionsTableClient.QueryAsync<TableEntity>(filter: predictionsFilter))
            {
                dependencies.PredictionCount++;
            }

            // Check for results
            var resultsTableClient = _tableClientFactory.GetTableClient(ResultsTableName);
            try
            {
                var response = await resultsTableClient.GetEntityAsync<TableEntity>(eventId, "Result");
                dependencies.HasResult = response.Value != null;
            }
            catch (RequestFailedException)
            {
                dependencies.HasResult = false;
            }
        }
        catch (RequestFailedException)
        {
            // Tables don't exist or error
        }

        return dependencies;
    }

    public async Task<int> GetNextEventNumberAsync(string seasonId)
    {
        var events = await GetEventsBySeasonAsync(seasonId);
        if (!events.Any())
        {
            return 1;
        }
        return events.Max(e => e.EventNumber) + 1;
    }
}


