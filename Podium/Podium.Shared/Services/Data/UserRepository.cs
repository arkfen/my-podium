using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Data;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User?> GetUserByIdAsync(string userId);
    Task<bool> CreateUserAsync(User user);
    Task<bool> UpdateLastLoginAsync(string userId);
}

public class UserRepository : IUserRepository
{
    private readonly ITableClientFactory _tableClientFactory;
    private const string TableName = "PodiumUsers";

    public UserRepository(ITableClientFactory tableClientFactory)
    {
        _tableClientFactory = tableClientFactory;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = email.ToLowerInvariant();
            var filter = $"Email eq '{normalizedEmail}'";
            await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: filter))
            {
                return MapToUser(entity);
            }
        }
        catch (RequestFailedException)
        {
            return null;
        }

        return null;
    }

    public async Task<User?> GetUserByIdAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // UserId is split: first 6 chars = PartitionKey, rest = RowKey
            if (userId.Length < 6)
                return null;

            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);

            var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            return MapToUser(response.Value);
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            // Split userId for partition/row key
            if (user.UserId.Length < 6)
                return false;

            var partitionKey = user.UserId.Substring(0, 6);
            var rowKey = user.UserId.Substring(6);

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["UserId"] = user.UserId,
                ["Email"] = user.Email.ToLowerInvariant(), // Store email in lowercase
                ["Username"] = user.Username,
                ["PasswordHash"] = user.PasswordHash,
                ["PasswordSalt"] = user.PasswordSalt,
                ["PreferredAuthMethod"] = user.PreferredAuthMethod,
                ["IsActive"] = user.IsActive,
                ["CreatedDate"] = user.CreatedDate,
                ["LastLoginDate"] = user.LastLoginDate
            };

            await tableClient.AddEntityAsync(entity);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> UpdateLastLoginAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            if (userId.Length < 6)
                return false;

            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);

            var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            var entity = response.Value;
            entity["LastLoginDate"] = DateTime.UtcNow;

            await tableClient.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    private static User MapToUser(TableEntity entity)
    {
        return new User
        {
            UserId = entity.GetString("UserId") ?? string.Empty,
            Email = entity.GetString("Email") ?? string.Empty,
            Username = entity.GetString("Username") ?? string.Empty,
            PasswordHash = entity.GetString("PasswordHash") ?? string.Empty,
            PasswordSalt = entity.GetString("PasswordSalt") ?? string.Empty,
            PreferredAuthMethod = entity.GetString("PreferredAuthMethod") ?? "Both",
            IsActive = entity.GetBoolean("IsActive") ?? false,
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.DateTime ?? DateTime.MinValue,
            LastLoginDate = entity.GetDateTimeOffset("LastLoginDate")?.DateTime
        };
    }
}
