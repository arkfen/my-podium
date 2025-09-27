using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using System.Security.Cryptography;
using System.Text;

namespace MyPodium.Services;

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

    public async Task<(bool Success, string ErrorMessage)> CreateAdminAsync(string username, string password)
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
            
            await adminTableClient.AddEntityAsync(adminEntity);
            
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"AdminAuthService.CreateAdminAsync error: {ex}");
            return (false, "An error occurred while creating the admin account.");
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