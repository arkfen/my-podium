using Azure.Data.Tables;

namespace Podium.Shared.Services.Data;

public interface ITableClientFactory
{
    TableClient GetTableClient(string tableName);
}

public class TableClientFactory : ITableClientFactory
{
    private readonly string _storageUri;
    private readonly string _accountName;
    private readonly string _accountKey;

    public TableClientFactory(string storageUri, string accountName, string accountKey)
    {
        _storageUri = storageUri ?? throw new ArgumentNullException(nameof(storageUri));
        _accountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
        _accountKey = accountKey ?? throw new ArgumentNullException(nameof(accountKey));
    }

    public TableClient GetTableClient(string tableName)
    {
        return new TableClient(
            new Uri(_storageUri),
            tableName,
            new TableSharedKeyCredential(_accountName, _accountKey));
    }
}
