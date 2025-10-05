using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;
using System.Text;

namespace MyPodium.Services;

public class AdminInfo
{
    public string AdminId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
    public string? CreatedByAdminId { get; set; }
}

public class AdminAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ProtectedLocalStorage _localStorage;
    private const string ADMIN_AUTH_STORAGE_KEY = "podium_admin_auth";
    private const string ADMIN_USERS_TABLE = "MyPodiumAdminUsers";
    private const string ADMIN_SESSIONS_TABLE = "MyPodiumAdminSessions";

    public AdminAuthService(
        IConfiguration configuration,
        ProtectedLocalStorage localStorage)
    {
        _configuration = configuration;
        _localStorage = localStorage;
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

    public async Task<bool> CheckAdminAuthenticationAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(ADMIN_AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                // Verify the admin session exists and is valid
                var tableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
                try
                {
                    var sessionEntity = await tableClient.GetEntityAsync<TableEntity>("AdminSessions", sessionId);
                    
                    // Check if session expired
                    if (sessionEntity != null)
                    {
                        var expiryDate = sessionEntity.Value.GetDateTimeOffset("ExpiryDate");
                        if (expiryDate > DateTimeOffset.UtcNow)
                        {
                            return true;
                        }
                        else
                        {
                            // Session expired, remove it
                            await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
                            return false;
                        }
                    }
                }
                catch (RequestFailedException)
                {
                    // Session doesn't exist
                    await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
                    return false;
                }
            }
        }
        catch
        {
            // If there's an error, assume not authenticated
        }
        
        return false;
    }

    public async Task<(bool Success, string ErrorMessage)> SignInAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Username and password are required.");
        }

        try
        {
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            // Find admin user by username (case-insensitive)
            var adminQuery = adminTableClient.Query<TableEntity>();
            
            TableEntity? adminEntity = null;
            foreach (var entity in adminQuery)
            {
                var entityUsername = entity.GetString("Username");
                if (string.Equals(entityUsername, username, StringComparison.OrdinalIgnoreCase))
                {
                    adminEntity = entity;
                    break;
                }
            }

            if (adminEntity == null)
            {
                return (false, "Invalid username or password.");
            }

            // Verify password hash
            var storedPasswordHash = adminEntity.GetString("PasswordHash");
            var storedSalt = adminEntity.GetString("Salt");
            
            if (string.IsNullOrEmpty(storedPasswordHash) || string.IsNullOrEmpty(storedSalt))
            {
                return (false, "Invalid account configuration. Please contact support.");
            }

            var providedPasswordHash = HashPassword(password, storedSalt);
            
            if (storedPasswordHash != providedPasswordHash)
            {
                return (false, "Invalid username or password.");
            }

            // Create admin session
            var sessionId = Guid.NewGuid().ToString();
            var sessionTableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
            
            var sessionEntity = new TableEntity("AdminSessions", sessionId)
            {
                ["Username"] = username,
                ["AdminId"] = adminEntity.RowKey,
                ["CreatedDate"] = DateTimeOffset.UtcNow,
                ["ExpiryDate"] = DateTimeOffset.UtcNow.AddHours(8), // 8-hour admin sessions
                ["IsActive"] = true
            };
            
            await sessionTableClient.AddEntityAsync(sessionEntity);
            
            // Store session in local storage
            await _localStorage.SetAsync(ADMIN_AUTH_STORAGE_KEY, sessionId);
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.SignInAsync error: {ex}");
            return (false, "An error occurred during sign in. Please try again.");
        }
    }

    public async Task<string> GetAdminUsernameAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(ADMIN_AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                var tableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
                try
                {
                    var sessionEntityResponse = await tableClient.GetEntityAsync<TableEntity>("AdminSessions", sessionId);
                    if (sessionEntityResponse != null && sessionEntityResponse.Value != null)
                    {
                        var sessionEntity = sessionEntityResponse.Value;
                        var isActive = sessionEntity.GetBoolean("IsActive");
                        var expiryDate = sessionEntity.GetDateTimeOffset("ExpiryDate");

                        if (isActive == true && expiryDate > DateTimeOffset.UtcNow)
                        {
                            return sessionEntity.GetString("Username") ?? string.Empty;
                        }
                        else
                        {
                            // Session expired
                            await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
                        }
                    }
                }
                catch (RequestFailedException)
                {
                    // Session doesn't exist
                    await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return string.Empty;
    }

    public async Task SignOutAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(ADMIN_AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                // Mark the session as inactive
                var tableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
                try
                {
                    var sessionEntity = await tableClient.GetEntityAsync<TableEntity>("AdminSessions", sessionId);
                    if (sessionEntity != null)
                    {
                        sessionEntity.Value["IsActive"] = false;
                        await tableClient.UpdateEntityAsync(sessionEntity.Value, ETag.All);
                    }
                }
                catch
                {
                    // Session doesn't exist, that's fine
                }
            }
            
            // Remove from local storage
            await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
        }
        catch
        {
            // Ignore any errors during sign out
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(string username, string currentPassword, string newPassword)
    {
        try
        {
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            // Find the admin user (case-insensitive)
            var adminQuery = adminTableClient.Query<TableEntity>();
            
            TableEntity? adminEntity = null;
            foreach (var entity in adminQuery)
            {
                var entityUsername = entity.GetString("Username");
                if (string.Equals(entityUsername, username, StringComparison.OrdinalIgnoreCase))
                {
                    adminEntity = entity;
                    break;
                }
            }

            if (adminEntity == null)
            {
                return (false, "Admin user not found.");
            }

            // Verify current password
            var storedPasswordHash = adminEntity.GetString("PasswordHash");
            var storedSalt = adminEntity.GetString("Salt");
            
            if (string.IsNullOrEmpty(storedPasswordHash) || string.IsNullOrEmpty(storedSalt))
            {
                return (false, "Invalid account configuration.");
            }

            var currentPasswordHash = HashPassword(currentPassword, storedSalt);
            
            if (storedPasswordHash != currentPasswordHash)
            {
                return (false, "Current password is incorrect.");
            }

            // Generate new salt and hash for the new password
            var newSalt = GenerateSalt();
            var newPasswordHash = HashPassword(newPassword, newSalt);

            // Update the admin entity
            adminEntity["PasswordHash"] = newPasswordHash;
            adminEntity["Salt"] = newSalt;
            
            await adminTableClient.UpdateEntityAsync(adminEntity, ETag.All, TableUpdateMode.Replace);
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.ChangePasswordAsync error: {ex}");
            return (false, "An error occurred while changing the password.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> CreateAdminAsync(string username, string password, string? createdByAdminId = null)
    {
        try
        {
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            // Check if username already exists (case-insensitive)
            var existingAdminQuery = adminTableClient.Query<TableEntity>();
            
            foreach (var entity in existingAdminQuery)
            {
                var entityUsername = entity.GetString("Username");
                if (string.Equals(entityUsername, username, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "An admin with this username already exists.");
                }
            }

            // Generate salt and hash for the new admin
            var salt = GenerateSalt();
            var passwordHash = HashPassword(password, salt);
            
            // Create new admin entity
            var adminId = Guid.NewGuid().ToString();
            var adminEntity = new TableEntity("Admin", adminId)
            {
                ["Username"] = username,
                ["PasswordHash"] = passwordHash,
                ["Salt"] = salt,
                ["CreatedDate"] = DateTimeOffset.UtcNow
            };

            // Add CreatedByAdminId if provided (null means super admin)
            if (!string.IsNullOrEmpty(createdByAdminId))
            {
                adminEntity["CreatedByAdminId"] = createdByAdminId;
            }
            
            await adminTableClient.AddEntityAsync(adminEntity);
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.CreateAdminAsync error: {ex}");
            return (false, "An error occurred while creating the admin account.");
        }
    }

    public async Task<string?> GetCurrentAdminIdAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(ADMIN_AUTH_STORAGE_KEY);
            
            if (result.Success && !string.IsNullOrEmpty(result.Value))
            {
                var sessionId = result.Value;
                
                var tableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
                try
                {
                    var sessionEntityResponse = await tableClient.GetEntityAsync<TableEntity>("AdminSessions", sessionId);
                    if (sessionEntityResponse != null && sessionEntityResponse.Value != null)
                    {
                        var sessionEntity = sessionEntityResponse.Value;
                        var isActive = sessionEntity.GetBoolean("IsActive");
                        var expiryDate = sessionEntity.GetDateTimeOffset("ExpiryDate");

                        if (isActive == true && expiryDate > DateTimeOffset.UtcNow)
                        {
                            return sessionEntity.GetString("AdminId");
                        }
                    }
                }
                catch (RequestFailedException)
                {
                    // Session doesn't exist
                    await _localStorage.DeleteAsync(ADMIN_AUTH_STORAGE_KEY);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return null;
    }

    public Task<List<AdminInfo>> GetDeletableAdminsAsync(string currentAdminId)
    {
        var deletableAdmins = new List<AdminInfo>();
        
        try
        {
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            var adminQuery = adminTableClient.Query<TableEntity>();
            
            foreach (var entity in adminQuery)
            {
                // Check if this admin was created by the current admin
                var createdByAdminId = entity.ContainsKey("CreatedByAdminId") 
                    ? entity.GetString("CreatedByAdminId") 
                    : null;
                
                if (!string.IsNullOrEmpty(createdByAdminId) && createdByAdminId == currentAdminId)
                {
                    deletableAdmins.Add(new AdminInfo
                    {
                        AdminId = entity.RowKey,
                        Username = entity.GetString("Username") ?? string.Empty,
                        CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.UtcNow,
                        CreatedByAdminId = createdByAdminId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.GetDeletableAdminsAsync error: {ex}");
        }
        
        return Task.FromResult(deletableAdmins);
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteAdminAsync(string adminIdToDelete, string currentAdminId)
    {
        try
        {
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            // Get the admin to delete
            TableEntity? adminToDelete = null;
            try
            {
                var response = await adminTableClient.GetEntityAsync<TableEntity>("Admin", adminIdToDelete);
                adminToDelete = response.Value;
            }
            catch (RequestFailedException)
            {
                return (false, "Admin not found.");
            }
            
            if (adminToDelete == null)
            {
                return (false, "Admin not found.");
            }
            
            // Check if this admin was created by the current admin
            var createdByAdminId = adminToDelete.ContainsKey("CreatedByAdminId") 
                ? adminToDelete.GetString("CreatedByAdminId") 
                : null;
            
            if (string.IsNullOrEmpty(createdByAdminId))
            {
                return (false, "Cannot delete super admin.");
            }
            
            if (createdByAdminId != currentAdminId)
            {
                return (false, "You can only delete admins that you created.");
            }
            
            // Delete the admin
            await adminTableClient.DeleteEntityAsync("Admin", adminIdToDelete);
            
            // Also invalidate any active sessions for this admin
            var sessionTableClient = CreateTableClient(ADMIN_SESSIONS_TABLE);
            var sessionQuery = sessionTableClient.QueryAsync<TableEntity>($"AdminId eq '{adminIdToDelete}'");
            
            await foreach (var session in sessionQuery)
            {
                try
                {
                    session["IsActive"] = false;
                    await sessionTableClient.UpdateEntityAsync(session, ETag.All);
                }
                catch
                {
                    // Ignore session update errors
                }
            }
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.DeleteAdminAsync error: {ex}");
            return (false, "An error occurred while deleting the admin.");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> DeleteSelfAsync()
    {
        try
        {
            var currentAdminId = await GetCurrentAdminIdAsync();
            
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return (false, "Could not determine current admin.");
            }
            
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            // Get the current admin
            TableEntity? currentAdmin = null;
            try
            {
                var response = await adminTableClient.GetEntityAsync<TableEntity>("Admin", currentAdminId);
                currentAdmin = response.Value;
            }
            catch (RequestFailedException)
            {
                return (false, "Admin not found.");
            }
            
            if (currentAdmin == null)
            {
                return (false, "Admin not found.");
            }
            
            // Check if this is a super admin (cannot delete self if super admin)
            var createdByAdminId = currentAdmin.ContainsKey("CreatedByAdminId") 
                ? currentAdmin.GetString("CreatedByAdminId") 
                : null;
            
            if (string.IsNullOrEmpty(createdByAdminId))
            {
                return (false, "Super admins cannot delete themselves.");
            }
            
            // Delete the admin
            await adminTableClient.DeleteEntityAsync("Admin", currentAdminId);
            
            // Sign out
            await SignOutAsync();
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.DeleteSelfAsync error: {ex}");
            return (false, "An error occurred while deleting your account.");
        }
    }

    public async Task<bool> IsCurrentAdminSuperAdminAsync()
    {
        try
        {
            var currentAdminId = await GetCurrentAdminIdAsync();
            
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return false;
            }
            
            var adminTableClient = CreateTableClient(ADMIN_USERS_TABLE);
            
            try
            {
                var response = await adminTableClient.GetEntityAsync<TableEntity>("Admin", currentAdminId);
                var currentAdmin = response.Value;
                
                // Check if CreatedByAdminId is null or empty (super admin)
                var createdByAdminId = currentAdmin.ContainsKey("CreatedByAdminId") 
                    ? currentAdmin.GetString("CreatedByAdminId") 
                    : null;
                
                return string.IsNullOrEmpty(createdByAdminId);
            }
            catch (RequestFailedException)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private string HashPassword(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 10000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(hash);
    }

    public static string GenerateSalt()
    {
        using var rng = RandomNumberGenerator.Create();
        var saltBytes = new byte[32];
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    public static string HashPasswordWithSalt(string password, string salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 10000, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(hash);
    }
}