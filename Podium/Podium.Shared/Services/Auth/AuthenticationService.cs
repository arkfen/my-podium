using Azure;
using Azure.Data.Tables;
using Podium.Shared.Models;
using Podium.Shared.Services.Data;
using System.Security.Cryptography;

namespace Podium.Shared.Services.Auth;

public interface IAuthenticationService
{
    Task<(bool Success, string ErrorMessage)> SendOTPAsync(string email);
    Task<(bool Success, string ErrorMessage)> SendOTPForNewEmailAsync(string email, string userId);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> SignInWithPasswordAsync(string email, string password);
    Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> ValidateSessionAsync(string sessionId);
    Task SignOutAsync(string sessionId);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ITableClientFactory _tableClientFactory;
    private readonly Action<string, string>? _sendEmailCallback;
    private const string UsersTable = "PodiumUsers";
    private const string SessionsTable = "PodiumAuthSessions";
    private const string OTPTable = "PodiumOTPCodes";

    public AuthenticationService(ITableClientFactory tableClientFactory, Action<string, string>? sendEmailCallback = null)
    {
        _tableClientFactory = tableClientFactory;
        _sendEmailCallback = sendEmailCallback;
    }

    public async Task<(bool Success, string ErrorMessage)> SendOTPAsync(string email)
    {
        // Check if user exists
        var userClient = _tableClientFactory.GetTableClient(UsersTable);
        User? user = null;

        try
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = email.ToLowerInvariant();
            var filter = $"Email eq '{normalizedEmail}'";
            await foreach (var entity in userClient.QueryAsync<TableEntity>(filter: filter))
            {
                user = MapToUser(entity);
                break;
            }
        }
        catch (RequestFailedException ex)
        {
            return (false, $"Database error: {ex.Message}");
        }

        if (user == null)
        {
            return (false, "Email not found. Please sign up first.");
        }

        // Generate 6-digit OTP
        var otpCode = GenerateOTP();

        // Store OTP
        var otpClient = _tableClientFactory.GetTableClient(OTPTable);
        var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = email.ToLowerInvariant(), // Store normalized email
            ["Code"] = otpCode,
            ["UserId"] = user.UserId,
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10),
            ["IsUsed"] = false,
            ["CreatedDate"] = DateTime.UtcNow
        };

        try
        {
            await otpClient.AddEntityAsync(otpEntity);
        }
        catch (RequestFailedException ex)
        {
            return (false, $"Failed to generate code: {ex.Message}");
        }

        // Send email via callback (API will provide this)
        if (_sendEmailCallback != null)
        {
            try
            {
                _sendEmailCallback(email, otpCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                // Don't fail the request - OTP is still valid
            }
        }
        else
        {
            // Fallback: Log to console (development)
            Console.WriteLine($"OTP Code for {email}: {otpCode}");
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> SendOTPForNewEmailAsync(string email, string userId)
    {
        // This method sends OTP to a NEW email address that doesn't exist in user table yet
        // Used when user is updating their email to a new address
        
        // Generate 6-digit OTP
        var otpCode = GenerateOTP();

        // Store OTP
        var otpClient = _tableClientFactory.GetTableClient(OTPTable);
        var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
        {
            ["Email"] = email.ToLowerInvariant(), // Store normalized email
            ["Code"] = otpCode,
            ["UserId"] = userId, // Use the current user's ID
            ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10),
            ["IsUsed"] = false,
            ["CreatedDate"] = DateTime.UtcNow
        };

        try
        {
            await otpClient.AddEntityAsync(otpEntity);
        }
        catch (RequestFailedException ex)
        {
            return (false, $"Failed to generate code: {ex.Message}");
        }

        // Send email via callback (API will provide this)
        if (_sendEmailCallback != null)
        {
            try
            {
                _sendEmailCallback(email, otpCode);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send email: {ex.Message}");
                // Don't fail the request - OTP is still valid
            }
        }
        else
        {
            // Fallback: Log to console (development)
            Console.WriteLine($"OTP Code for {email}: {otpCode}");
        }

        return (true, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> VerifyOTPAsync(string email, string otpCode)
    {
        var otpClient = _tableClientFactory.GetTableClient(OTPTable);
        var cutoffTime = DateTime.UtcNow.AddMinutes(-10);

        try
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = email.ToLowerInvariant();
            var filter = $"PartitionKey eq 'OTP' and Email eq '{normalizedEmail}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}'";
            
            TableEntity? validOtp = null;
            await foreach (var entity in otpClient.QueryAsync<TableEntity>(filter: filter))
            {
                if (entity.GetString("Code") == otpCode)
                {
                    validOtp = entity;
                    break;
                }
            }

            if (validOtp == null)
            {
                return (false, string.Empty, string.Empty, string.Empty, "Invalid or expired code.");
            }

            // Mark OTP as used
            validOtp["IsUsed"] = true;
            await otpClient.UpdateEntityAsync(validOtp, ETag.All, TableUpdateMode.Merge);

            var userId = validOtp.GetString("UserId") ?? string.Empty;
            
            // Get user details
            var userClient = _tableClientFactory.GetTableClient(UsersTable);
            var partitionKey = userId.Substring(0, 6);
            var rowKey = userId.Substring(6);
            var userResponse = await userClient.GetEntityAsync<TableEntity>(partitionKey, rowKey);
            var username = userResponse.Value.GetString("Username") ?? string.Empty;

            // Create session with normalized email
            var sessionId = await CreateSessionAsync(userId, normalizedEmail, username);

            return (true, userId, username, sessionId, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, string.Empty, string.Empty, $"Verification failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> SignInWithPasswordAsync(string email, string password)
    {
        var userClient = _tableClientFactory.GetTableClient(UsersTable);
        User? user = null;

        try
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = email.ToLowerInvariant();
            var filter = $"Email eq '{normalizedEmail}'";
            await foreach (var entity in userClient.QueryAsync<TableEntity>(filter: filter))
            {
                user = MapToUser(entity);
                break;
            }
        }
        catch (RequestFailedException ex)
        {
            return (false, string.Empty, string.Empty, string.Empty, $"Database error: {ex.Message}");
        }

        if (user == null)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid email or password.");
        }

        // Verify password
        if (!VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid email or password.");
        }

        // Create session
        var sessionId = await CreateSessionAsync(user.UserId, user.Email, user.Username);

        return (true, user.UserId, user.Username, sessionId, string.Empty);
    }

    public async Task<(bool Success, string UserId, string Username, string SessionId, string ErrorMessage)> ValidateSessionAsync(string sessionId)
    {
        var sessionClient = _tableClientFactory.GetTableClient(SessionsTable);

        try
        {
            var response = await sessionClient.GetEntityAsync<TableEntity>("Session", sessionId);
            var session = response.Value;

            var isActive = session.GetBoolean("IsActive") ?? false;
            var expiryDate = session.GetDateTimeOffset("ExpiryDate")?.DateTime ?? DateTime.MinValue;

            if (isActive && expiryDate > DateTime.UtcNow)
            {
                return (true, 
                    session.GetString("UserId") ?? string.Empty, 
                    session.GetString("Username") ?? string.Empty,
                    sessionId,
                    string.Empty);
            }

            return (false, string.Empty, string.Empty, string.Empty, "Session expired.");
        }
        catch (RequestFailedException)
        {
            return (false, string.Empty, string.Empty, string.Empty, "Invalid session.");
        }
    }

    public async Task SignOutAsync(string sessionId)
    {
        var sessionClient = _tableClientFactory.GetTableClient(SessionsTable);

        try
        {
            var response = await sessionClient.GetEntityAsync<TableEntity>("Session", sessionId);
            var session = response.Value;
            session["IsActive"] = false;
            await sessionClient.UpdateEntityAsync(session, ETag.All, TableUpdateMode.Merge);
        }
        catch (RequestFailedException)
        {
            // Session doesn't exist, that's fine
        }
    }

    private async Task<string> CreateSessionAsync(string userId, string email, string username)
    {
        var sessionId = Guid.NewGuid().ToString();
        var sessionClient = _tableClientFactory.GetTableClient(SessionsTable);

        var sessionEntity = new TableEntity("Session", sessionId)
        {
            ["UserId"] = userId,
            ["Email"] = email,
            ["Username"] = username,
            ["CreatedDate"] = DateTime.UtcNow,
            ["ExpiryDate"] = DateTime.UtcNow.AddDays(14),
            ["IsActive"] = true
        };

        await sessionClient.AddEntityAsync(sessionEntity);
        return sessionId;
    }

    private static string GenerateOTP()
    {
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[4];
        rng.GetBytes(randomNumber);
        var code = Math.Abs(BitConverter.ToInt32(randomNumber, 0) % 1000000);
        return code.ToString("D6");
    }

    private static bool VerifyPassword(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        var computedHash = Convert.ToBase64String(hashBytes);
        return computedHash == hash;
    }

    public static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        var salt = Convert.ToBase64String(saltBytes);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, 10000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        var hash = Convert.ToBase64String(hashBytes);

        return (hash, salt);
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
