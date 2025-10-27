using Azure;
using Azure.Data.Tables;

namespace MyPodium.Services;

public class HealthCheckStatus
{
    public string Status { get; set; } = "Unknown"; // "Healthy", "Degraded", "Unhealthy"
    public string StatusText { get; set; } = "Checking...";
    public string StatusColor { get; set; } = "text-secondary";
}

public class StatisticsService
{
    private readonly IConfiguration _configuration;
    private const string USERS_TABLE = "MyPodiumUsers";
    private const string ADMIN_USERS_TABLE = "MyPodiumAdminUsers";
    private const string USER_SESSIONS_TABLE = "MyPodiumUserSessions";
    private const string PREDICTIONS_TABLE = "MyPodiumDreams";
    private const string DRIVERS_TABLE = "MyPodiumDrivers";
    private const string RACES_TABLE = "MyPodiumRaces";
    
    private static bool _userSessionsTableCreated = false;
    private static readonly object _tableLock = new object();

    public StatisticsService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private TableClient CreateTableClient(string tableName)
    {
        var storageUri = _configuration.GetConnectionString("DefaultStorageUri");
        var accountName = _configuration.GetConnectionString("DefaultAccountName");
        var storageAccountKey = _configuration.GetConnectionString("DefaultStorageAccountKey");

        if (string.IsNullOrEmpty(storageUri) || string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(storageAccountKey))
            throw new InvalidOperationException("Storage connection information is missing");

        return new TableClient(
            new Uri(storageUri),
            tableName,
            new TableSharedKeyCredential(accountName, storageAccountKey));
    }

    public async Task<int> GetTotalUsersCountAsync()
    {
        try
        {
            var tableClient = CreateTableClient(USERS_TABLE);
            var userCount = 0;

            // Use a projection query to only retrieve PartitionKey and RowKey, and use continuation tokens
            string[] selectColumns = new[] { "PartitionKey", "RowKey" };
            string? continuationToken = null;
            do
            {
                var page = tableClient.QueryAsync<TableEntity>(select: selectColumns).AsPages(continuationToken, 1000);
                await foreach (var pageResult in page)
                {
                    userCount += pageResult.Values.Count;
                    continuationToken = pageResult.ContinuationToken;
                }
            } while (!string.IsNullOrEmpty(continuationToken));

            return userCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.GetTotalUsersCountAsync error: {ex}");
            return 0;
        }
    }

    public async Task<int> GetActiveSessionsCountAsync()
    {
        try
        {
            var tableClient = CreateTableClient(USER_SESSIONS_TABLE);
            var twoHoursAgo = DateTimeOffset.UtcNow.AddHours(-2);
            var activeCount = 0;

            // Count unique users with sessions less than 2 hours ago
            var seenUsers = new HashSet<string>();
            var query = tableClient.QueryAsync<TableEntity>();
            
            await foreach (var entity in query)
            {
                var lastAccess = entity.GetDateTimeOffset("LastAccessTime");
                var userId = entity.GetString("UserId");

                if (lastAccess.HasValue && lastAccess.Value > twoHoursAgo && !string.IsNullOrEmpty(userId))
                {
                    if (seenUsers.Add(userId)) // Only count unique users
                    {
                        activeCount++;
                    }
                }
            }

            return activeCount;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table doesn't exist yet
            Console.WriteLine($"StatisticsService.GetActiveSessionsCountAsync: Table '{USER_SESSIONS_TABLE}' not found. This is expected if no sessions have been created yet.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.GetActiveSessionsCountAsync error: {ex}");
            return 0;
        }
    }

    public async Task<int> GetTotalPredictionsCountAsync()
    {
        try
        {
            var tableClient = CreateTableClient(PREDICTIONS_TABLE);
            var predictionCount = 0;

            // Count predictions where Points field is NULL (not yet scored)
            var query = tableClient.QueryAsync<TableEntity>();
            
            await foreach (var entity in query)
            {
                // Check if Points field doesn't exist or is null
                if (!entity.ContainsKey("Points") || !entity.GetInt32("Points").HasValue)
                {
                    predictionCount++;
                }
            }

            return predictionCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.GetTotalPredictionsCountAsync error: {ex}");
            return 0;
        }
    }

    public async Task<HealthCheckStatus> GetSystemHealthAsync()
    {
        var healthStatus = new HealthCheckStatus();
        var checks = new List<(bool Success, long ResponseTime)>();

        try
        {
            // Check 1: MyPodiumDrivers table
            var driversCheck = await CheckTableHealthAsync(DRIVERS_TABLE);
            checks.Add(driversCheck);

            // Check 2: MyPodiumRaces table
            var racesCheck = await CheckTableHealthAsync(RACES_TABLE);
            checks.Add(racesCheck);

            // Check 3: MyPodiumUsers table
            var usersCheck = await CheckTableHealthAsync(USERS_TABLE);
            checks.Add(usersCheck);

            // Determine overall health
            var failedChecks = checks.Count(c => !c.Success);
            var avgResponseTime = checks.Where(c => c.Success).Select(c => c.ResponseTime).DefaultIfEmpty(0).Average();

            if (failedChecks == 0)
            {
                if (avgResponseTime < 1000) // Less than 1 second
                {
                    healthStatus.Status = "Healthy";
                    healthStatus.StatusText = "All Good";
                    healthStatus.StatusColor = "text-success";
                }
                else if (avgResponseTime < 3000) // 1-3 seconds
                {
                    healthStatus.Status = "Degraded";
                    healthStatus.StatusText = "Slow Response";
                    healthStatus.StatusColor = "text-warning";
                }
                else // More than 3 seconds
                {
                    healthStatus.Status = "Degraded";
                    healthStatus.StatusText = "High Latency";
                    healthStatus.StatusColor = "text-warning";
                }
            }
            else if (failedChecks < checks.Count)
            {
                healthStatus.Status = "Degraded";
                healthStatus.StatusText = "Partial Failure";
                healthStatus.StatusColor = "text-warning";
            }
            else
            {
                healthStatus.Status = "Unhealthy";
                healthStatus.StatusText = "System Error";
                healthStatus.StatusColor = "text-danger";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.GetSystemHealthAsync error: {ex}");
            healthStatus.Status = "Unhealthy";
            healthStatus.StatusText = "Check Failed";
            healthStatus.StatusColor = "text-danger";
        }

        return healthStatus;
    }

    private async Task<(bool Success, long ResponseTime)> CheckTableHealthAsync(string tableName)
    {
        var startTime = DateTimeOffset.UtcNow;
        
        try
        {
            var tableClient = CreateTableClient(tableName);
            
            // Try to query one entity
            var query = tableClient.QueryAsync<TableEntity>(maxPerPage: 1);
            await foreach (var entity in query)
            {
                // Successfully retrieved at least one entity
                break;
            }

            var responseTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
            return (true, (long)responseTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.CheckTableHealthAsync({tableName}) error: {ex}");
            return (false, 0);
        }
    }

    public async Task TrackUserSessionAsync(string userId, string userName)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        try
        {
            var tableClient = CreateTableClient(USER_SESSIONS_TABLE);
            
            // Only check/create table once per application lifetime
            if (!_userSessionsTableCreated)
            {
                lock (_tableLock)
                {
                    if (!_userSessionsTableCreated)
                    {
                        // Create table if it doesn't exist (only runs once)
                        tableClient.CreateIfNotExistsAsync().GetAwaiter().GetResult();
                        _userSessionsTableCreated = true;
                        Console.WriteLine($"StatisticsService: Table '{USER_SESSIONS_TABLE}' created or verified to exist.");
                    }
                }
            }

            var sessionId = Guid.NewGuid().ToString();
            var sessionEntity = new TableEntity("UserSession", sessionId)
            {
                ["UserId"] = userId,
                ["UserName"] = userName,
                ["LastAccessTime"] = DateTimeOffset.UtcNow,
                ["CreatedTime"] = DateTimeOffset.UtcNow
            };

            await tableClient.UpsertEntityAsync(sessionEntity, TableUpdateMode.Merge);
            Console.WriteLine($"StatisticsService: Session tracked for user '{userName}' (ID: {userId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StatisticsService.TrackUserSessionAsync error: {ex}");
            // Don't throw - session tracking is not critical
        }
    }
}
