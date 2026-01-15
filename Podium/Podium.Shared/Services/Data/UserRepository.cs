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
    Task<List<User>> GetAllUsersAsync();
    Task<List<User>> SearchUsersAsync(string searchTerm);
    Task<bool> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(string userId);
    Task<UserDependencies> GetUserDependenciesAsync(string userId);
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
                ["CreatedDate"] = DateTime.SpecifyKind(user.CreatedDate, DateTimeKind.Utc),
                ["LastLoginDate"] = user.LastLoginDate.HasValue 
                    ? DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null
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

    public async Task<List<User>> GetAllUsersAsync()
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var users = new List<User>();

        try
        {
            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                users.Add(MapToUser(entity));
            }
        }
        catch (RequestFailedException)
        {
            return users;
        }

        return users.OrderBy(u => u.Username).ToList();
    }

    public async Task<List<User>> SearchUsersAsync(string searchTerm)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);
        var users = new List<User>();

        if (string.IsNullOrWhiteSpace(searchTerm))
            return users;

        try
        {
            var normalizedSearch = searchTerm.ToLowerInvariant();
            
            // Get all users and filter in memory (Azure Tables has limited query capabilities)
            await foreach (var entity in tableClient.QueryAsync<TableEntity>())
            {
                var user = MapToUser(entity);
                if (user.Username.ToLowerInvariant().Contains(normalizedSearch) ||
                    user.Email.ToLowerInvariant().Contains(normalizedSearch))
                {
                    users.Add(user);
                }
            }
        }
        catch (RequestFailedException)
        {
            return users;
        }

        return users.OrderBy(u => u.Username).ToList();
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            if (user.UserId.Length < 6)
                return false;

            var partitionKey = user.UserId.Substring(0, 6);
            var rowKey = user.UserId.Substring(6);

            var entity = new TableEntity(partitionKey, rowKey)
            {
                ["UserId"] = user.UserId,
                ["Email"] = user.Email.ToLowerInvariant(),
                ["Username"] = user.Username,
                ["PasswordHash"] = user.PasswordHash,
                ["PasswordSalt"] = user.PasswordSalt,
                ["PreferredAuthMethod"] = user.PreferredAuthMethod,
                ["IsActive"] = user.IsActive,
                ["CreatedDate"] = DateTime.SpecifyKind(user.CreatedDate, DateTimeKind.Utc),
                ["LastLoginDate"] = user.LastLoginDate.HasValue 
                    ? DateTime.SpecifyKind(user.LastLoginDate.Value, DateTimeKind.Utc) 
                    : (DateTime?)null
            };

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var tableClient = _tableClientFactory.GetTableClient(TableName);

        try
        {
            if (userId.Length < 6)
                return false;

            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);

            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public async Task<UserDependencies> GetUserDependenciesAsync(string userId)
    {
        // Check if user has any data that would prevent deletion
        var dependencies = new UserDependencies { UserId = userId };

        try
        {
            // Check predictions
            var predictionsClient = _tableClientFactory.GetTableClient("PodiumPredictions");
            var predictionFilter = $"UserId eq '{userId}'";
            await foreach (var _ in predictionsClient.QueryAsync<TableEntity>(filter: predictionFilter))
            {
                dependencies.PredictionCount++;
            }

            // Check if user is an admin
            var adminsClient = _tableClientFactory.GetTableClient("PodiumAdmins");
            try
            {
                var adminResponse = await adminsClient.GetEntityAsync<TableEntity>("Admin", userId);
                dependencies.IsAdmin = true;
            }
            catch (RequestFailedException)
            {
                dependencies.IsAdmin = false;
            }
        }
        catch (RequestFailedException)
        {
            // Tables might not exist, that's okay
        }

        return dependencies;
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
            CreatedDate = entity.GetDateTimeOffset("CreatedDate")?.UtcDateTime ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
            LastLoginDate = entity.GetDateTimeOffset("LastLoginDate")?.UtcDateTime
        };
    }
}
