using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IDisciplineRepository
{
    Task<List<Discipline>> GetAllDisciplinesAsync();
    Task<Discipline?> GetDisciplineByIdAsync(string disciplineId);
    Task<List<Discipline>> GetActiveDisciplinesAsync();
    Task<Discipline?> CreateDisciplineAsync(Discipline discipline);
    Task<Discipline?> UpdateDisciplineAsync(Discipline discipline);
    Task<bool> DeleteDisciplineAsync(string disciplineId);
}

public class DisciplineRepository : IDisciplineRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumDisciplines";

    public DisciplineRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<List<Discipline>> GetAllDisciplinesAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var disciplines = new List<Discipline>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: "PartitionKey eq 'Discipline'"))
            {
                disciplines.Add(MapToDiscipline(entity));
            }
        }
        catch (RequestFailedException)
        {
            // Table doesn't exist or other error
            return disciplines;
        }

        return disciplines;
    }

    public async Task<Discipline?> GetDisciplineByIdAsync(string disciplineId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>("Discipline", disciplineId);
            return MapToDiscipline(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<List<Discipline>> GetActiveDisciplinesAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var disciplines = new List<Discipline>();

        try
        {
            var filter = "PartitionKey eq 'Discipline' and IsActive eq true";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                disciplines.Add(MapToDiscipline(entity));
            }
        }
        catch (RequestFailedException)
        {
            return disciplines;
        }

        return disciplines;
    }

    private static Discipline MapToDiscipline(TableEntity entity)
    {
        return new Discipline
        {
            Id = entity.RowKey,
            Name = entity.GetString("Name") ?? string.Empty,
            DisplayName = entity.GetString("DisplayName") ?? string.Empty,
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)
        };
    }

    public async Task<Discipline?> CreateDisciplineAsync(Discipline discipline)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            discipline.Id = Guid.NewGuid().ToString();
            discipline.CreatedDate = DateTime.UtcNow;

            var entity = new TableEntity("Discipline", discipline.Id)
            {
                ["Name"] = discipline.Name,
                ["DisplayName"] = discipline.DisplayName,
                ["IsActive"] = discipline.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(discipline.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.AddEntityAsync(entity);
            return discipline;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<Discipline?> UpdateDisciplineAsync(Discipline discipline)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            var entity = new TableEntity("Discipline", discipline.Id)
            {
                ["Name"] = discipline.Name,
                ["DisplayName"] = discipline.DisplayName,
                ["IsActive"] = discipline.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(discipline.CreatedDate, DateTimeKind.Utc)
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return discipline;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteDisciplineAsync(string disciplineId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            await tableClient.DeleteEntityAsync("Discipline", disciplineId);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }
}
