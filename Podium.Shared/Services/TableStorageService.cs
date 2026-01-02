using Azure.Data.Tables;

namespace Podium.Shared.Services;

public interface ITableStorageService
{
    TableClient GetTableClient(string tableName);
}

public class TableStorageService : ITableStorageService
{
    private readonly string _connectionString;
    private readonly Dictionary<string, TableClient> _tableClients = new();

    public TableStorageService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public TableClient GetTableClient(string tableName)
    {
        if (!_tableClients.ContainsKey(tableName))
        {
            _tableClients[tableName] = new TableClient(_connectionString, tableName);
        }
        return _tableClients[tableName];
    }
}
