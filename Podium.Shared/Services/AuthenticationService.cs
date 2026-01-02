using Azure;
using Azure.Data.Tables;
using BCrypt.Net;
using Podium.Shared.Models;
using System.Security.Cryptography;

namespace Podium.Shared.Services;

public interface IAuthenticationService
{
    Task<(bool Success, string ErrorMessage)> RegisterUserAsync(string email, string name, string password);
    Task<(bool Success, string UserId, string UserName, string ErrorMessage)> SignInWithPasswordAsync(string email, string password);
    Task<(bool Success, string ErrorMessage)> SendOTPAsync(string email);
    Task<(bool Success, string UserId, string UserName, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode);
    Task<AuthSession?> GetSessionAsync(string sessionId);
    Task<bool> ValidateSessionAsync(string sessionId);
    Task SignOutAsync(string sessionId);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ITableStorageService _storageService;
    private readonly IEmailService? _emailService;
    
    private const string USERS_TABLE = "PodiumUsers";
    private const string AUTH_SESSIONS_TABLE = "PodiumAuthSessions";
    private const string OTP_CODES_TABLE = "PodiumOTPCodes";

    public AuthenticationService(ITableStorageService storageService, IEmailService? emailService = null)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _emailService = emailService;
    }

    public async Task<(bool Success, string ErrorMessage)> RegisterUserAsync(string email, string name, string password)
    {
        var tableClient = _storageService.GetTableClient(USERS_TABLE);

        // Check if user already exists
        var existingUser = await FindUserByEmailAsync(email);
        if (existingUser != null)
        {
            return (false, "A user with this email already exists.");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 10);

        // Create new user
        var userId = Guid.NewGuid().ToString();
        var entity = new TableEntity("User", userId)
        {
            ["Email"] = email,
            ["Name"] = name,
            ["PasswordHash"] = passwordHash,
            ["IsActive"] = true,
            ["IsEmailVerified"] = false, // Require email verification via OTP
            ["CreatedDate"] = DateTimeOffset.UtcNow,
            ["LastLoginDate"] = DateTimeOffset.UtcNow
        };

        try
        {
            await tableClient.AddEntityAsync(entity);
            return (true, string.Empty);
        }
        catch
        {
            return (false, "Failed to create user account. Please try again.");
        }
    }

    public async Task<(bool Success, string UserId, string UserName, string ErrorMessage)> SignInWithPasswordAsync(string email, string password)
    {
        var user = await FindUserByEmailAsync(email);
        
        if (user == null)
        {
            return (false, string.Empty, string.Empty, "Invalid email or password.");
        }

        if (!user.IsActive)
        {
            return (false, string.Empty, string.Empty, "This account has been deactivated.");
        }

        // Verify password
        var passwordHash = user.PasswordHash;
        if (!BCrypt.Net.BCrypt.Verify(password, passwordHash))
        {
            return (false, string.Empty, string.Empty, "Invalid email or password.");
        }

        // Create session
        var sessionId = await CreateSessionAsync(user.Id, user.Email, user.Name);
        
        // Update last login
        await UpdateLastLoginAsync(user.Id);

        return (true, user.Id, user.Name, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> SendOTPAsync(string email)
    {
        if (_emailService == null)
        {
            return (false, "Email service is not configured.");
        }

        var user = await FindUserByEmailAsync(email);
        
        if (user == null)
        {
            return (false, "Email not found. Please check your email address.");
        }

        // Generate OTP
        var otpCode = GenerateOTP();
        
        // Store OTP in table
        var tableClient = _storageService.GetTableClient(OTP_CODES_TABLE);
        var entity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = email,
            ["Code"] = otpCode,
            ["UserId"] = user.Id,
            ["ExpiryTime"] = DateTimeOffset.UtcNow.AddMinutes(10),
            ["IsUsed"] = false,
            ["CreatedDate"] = DateTimeOffset.UtcNow
        };

        await tableClient.AddEntityAsync(entity);

        // Send email
        await _emailService.SendOTPEmailAsync(email, otpCode);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string UserId, string UserName, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode)
    {
        var tableClient = _storageService.GetTableClient(OTP_CODES_TABLE);
        var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        // Find valid OTP
        TableEntity? validOtp = null;
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq 'OTP' and Email eq '{email}' and IsUsed eq false"))
        {
            var code = entity.GetString("Code");
            var expiryTime = entity.GetDateTimeOffset("ExpiryTime");

            if (code == otpCode && expiryTime > DateTimeOffset.UtcNow)
            {
                validOtp = entity;
                break;
            }
        }

        if (validOtp == null)
        {
            return (false, string.Empty, string.Empty, "Invalid or expired verification code.");
        }

        // Mark OTP as used
        validOtp["IsUsed"] = true;
        await tableClient.UpdateEntityAsync(validOtp, ETag.All);

        // Get user
        var userId = validOtp.GetString("UserId") ?? string.Empty;
        var user = await GetUserByIdAsync(userId);

        if (user == null)
        {
            return (false, string.Empty, string.Empty, "User not found.");
        }

        // Create session
        await CreateSessionAsync(user.Id, user.Email, user.Name);

        // Update last login
        await UpdateLastLoginAsync(user.Id);

        return (true, user.Id, user.Name, string.Empty);
    }

    public async Task<AuthSession?> GetSessionAsync(string sessionId)
    {
        var tableClient = _storageService.GetTableClient(AUTH_SESSIONS_TABLE);

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>("Sessions", sessionId);
            
            return new AuthSession
            {
                SessionId = entity.Value.RowKey,
                UserId = entity.Value.GetString("UserId") ?? string.Empty,
                Email = entity.Value.GetString("Email") ?? string.Empty,
                UserName = entity.Value.GetString("UserName") ?? string.Empty,
                CreatedDate = entity.Value.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue,
                ExpiryDate = entity.Value.GetDateTimeOffset("ExpiryDate") ?? DateTimeOffset.MinValue,
                IsActive = entity.Value.GetBoolean("IsActive") ?? false,
                LastActivityDate = entity.Value.GetDateTimeOffset("LastActivityDate") ?? DateTimeOffset.MinValue
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> ValidateSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        
        if (session == null)
        {
            return false;
        }

        return session.IsActive && session.ExpiryDate > DateTimeOffset.UtcNow;
    }

    public async Task SignOutAsync(string sessionId)
    {
        var tableClient = _storageService.GetTableClient(AUTH_SESSIONS_TABLE);

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>("Sessions", sessionId);
            entity.Value["IsActive"] = false;
            await tableClient.UpdateEntityAsync(entity.Value, ETag.All);
        }
        catch
        {
            // Session doesn't exist, that's fine
        }
    }

    private async Task<User?> FindUserByEmailAsync(string email)
    {
        var tableClient = _storageService.GetTableClient(USERS_TABLE);

        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter: $"PartitionKey eq 'User' and Email eq '{email}'"))
        {
            return new User
            {
                Id = entity.RowKey,
                Email = entity.GetString("Email") ?? string.Empty,
                Name = entity.GetString("Name") ?? string.Empty,
                PasswordHash = entity.GetString("PasswordHash") ?? string.Empty,
                IsActive = entity.GetBoolean("IsActive") ?? false,
                IsEmailVerified = entity.GetBoolean("IsEmailVerified") ?? false,
                CreatedDate = entity.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue,
                LastLoginDate = entity.GetDateTimeOffset("LastLoginDate") ?? DateTimeOffset.MinValue
            };
        }

        return null;
    }

    private async Task<User?> GetUserByIdAsync(string userId)
    {
        var tableClient = _storageService.GetTableClient(USERS_TABLE);

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>("User", userId);
            
            return new User
            {
                Id = entity.Value.RowKey,
                Email = entity.Value.GetString("Email") ?? string.Empty,
                Name = entity.Value.GetString("Name") ?? string.Empty,
                PasswordHash = entity.Value.GetString("PasswordHash") ?? string.Empty,
                IsActive = entity.Value.GetBoolean("IsActive") ?? false,
                IsEmailVerified = entity.Value.GetBoolean("IsEmailVerified") ?? false,
                CreatedDate = entity.Value.GetDateTimeOffset("CreatedDate") ?? DateTimeOffset.MinValue,
                LastLoginDate = entity.Value.GetDateTimeOffset("LastLoginDate") ?? DateTimeOffset.MinValue
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> CreateSessionAsync(string userId, string email, string userName)
    {
        var tableClient = _storageService.GetTableClient(AUTH_SESSIONS_TABLE);
        var sessionId = Guid.NewGuid().ToString();

        var entity = new TableEntity("Sessions", sessionId)
        {
            ["UserId"] = userId,
            ["Email"] = email,
            ["UserName"] = userName,
            ["CreatedDate"] = DateTimeOffset.UtcNow,
            ["ExpiryDate"] = DateTimeOffset.UtcNow.AddDays(14),
            ["IsActive"] = true,
            ["LastActivityDate"] = DateTimeOffset.UtcNow
        };

        await tableClient.AddEntityAsync(entity);
        return sessionId;
    }

    private async Task UpdateLastLoginAsync(string userId)
    {
        var tableClient = _storageService.GetTableClient(USERS_TABLE);

        try
        {
            var entity = await tableClient.GetEntityAsync<TableEntity>("User", userId);
            entity.Value["LastLoginDate"] = DateTimeOffset.UtcNow;
            await tableClient.UpdateEntityAsync(entity.Value, ETag.All);
        }
        catch
        {
            // Ignore errors
        }
    }

    private string GenerateOTP()
    {
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[4];
        rng.GetBytes(randomNumber);
        var code = Math.Abs(BitConverter.ToInt32(randomNumber, 0) % 10000);
        return code.ToString("D4");
    }
}

public interface IEmailService
{
    Task SendOTPEmailAsync(string email, string otpCode);
}
