using Podium.Shared.Services.Data;
using Podium.Shared.Models;
using Podium.Shared.Utilities;
using Azure.Data.Tables;
using System.Security.Cryptography;

namespace Podium.Shared.Services.Auth;

public interface IRegistrationService
{
    Task<(bool Success, string TempUserId, string ErrorMessage)> SendRegistrationVerificationAsync(string email, string username, string password, string preferredAuthMethod);
    Task<(bool Success, string UserId, string ErrorMessage)> VerifyAndCompleteRegistrationAsync(string tempUserId, string otpCode);
    Task<(bool Success, string UserId, string ErrorMessage)> RegisterUserAsync(string email, string username, string password, string preferredAuthMethod);
    Task<bool> IsEmailAvailableAsync(string email);
    Task<bool> IsUsernameAvailableAsync(string username);
}

public class RegistrationService : IRegistrationService
{
    private readonly IUserRepository _userRepository;
    private readonly ITableClientFactory _tableClientFactory;
    private readonly Action<string, string>? _sendEmailCallback;
    private const string PendingRegistrationsTable = "PodiumPendingRegistrations";
    private const string OTPTable = "PodiumOTPCodes";

    public RegistrationService(IUserRepository userRepository, ITableClientFactory tableClientFactory, Action<string, string>? sendEmailCallback = null)
    {
        _userRepository = userRepository;
        _tableClientFactory = tableClientFactory;
        _sendEmailCallback = sendEmailCallback;
    }

    public async Task<(bool Success, string TempUserId, string ErrorMessage)> SendRegistrationVerificationAsync(
        string email, string username, string password, string preferredAuthMethod)
    {
        // Validate username - ALWAYS required
        var (usernameValid, usernameError) = InputValidator.ValidateUsername(username);
        if (!usernameValid)
        {
            return (false, string.Empty, usernameError!);
        }

        // Check if username already exists
        var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(username);
        if (existingUserByUsername != null)
        {
            return (false, string.Empty, "Username already taken. Please choose a different username.");
        }

        // Validate based on preferred auth method
        bool emailRequired = (preferredAuthMethod == "Email" || preferredAuthMethod == "Both");
        
        if (emailRequired || !string.IsNullOrWhiteSpace(email))
        {
            var (emailValid, emailError) = InputValidator.ValidateEmail(email);
            if (!emailValid)
            {
                return (false, string.Empty, emailError!);
            }

            // Check if email already exists
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(email);
            if (existingUserByEmail != null)
            {
                return (false, string.Empty, "Email already registered.");
            }
        }

        if (preferredAuthMethod == "Password" || preferredAuthMethod == "Both")
        {
            var (passwordValid, passwordError) = InputValidator.ValidatePassword(password);
            if (!passwordValid)
            {
                return (false, string.Empty, passwordError!);
            }
        }

        // If email is provided, send verification code
        if (!string.IsNullOrWhiteSpace(email))
        {
            var tempUserId = Guid.NewGuid().ToString();
            
            // Store pending registration
            var pendingRegClient = _tableClientFactory.GetTableClient(PendingRegistrationsTable);
            var (hash, salt) = (preferredAuthMethod == "Password" || preferredAuthMethod == "Both")
                ? AuthenticationService.HashPassword(password)
                : (string.Empty, string.Empty);

            var pendingEntity = new TableEntity("PendingReg", tempUserId)
            {
                ["Email"] = email.Trim().ToLowerInvariant(),
                ["Username"] = username.Trim(),
                ["NormalizedUsername"] = InputValidator.NormalizeUsername(username),
                ["PasswordHash"] = hash,
                ["PasswordSalt"] = salt,
                ["PreferredAuthMethod"] = preferredAuthMethod,
                ["CreatedDate"] = DateTime.UtcNow,
                ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(30) // Pending reg expires in 30 min
            };

            try
            {
                await pendingRegClient.AddEntityAsync(pendingEntity);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Failed to create pending registration: {ex.Message}");
            }

            // Generate and send OTP
            var otpCode = GenerateOTP();
            var otpClient = _tableClientFactory.GetTableClient(OTPTable);
            var otpEntity = new TableEntity("OTP", Guid.NewGuid().ToString())
            {
                ["Email"] = email.Trim().ToLowerInvariant(),
                ["Code"] = otpCode,
                ["UserId"] = tempUserId, // Store temp ID for pending registration
                ["ExpiryTime"] = DateTime.UtcNow.AddMinutes(10),
                ["IsUsed"] = false,
                ["CreatedDate"] = DateTime.UtcNow,
                ["IsRegistration"] = true // Mark as registration OTP
            };

            try
            {
                await otpClient.AddEntityAsync(otpEntity);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, $"Failed to generate verification code: {ex.Message}");
            }

            // Send email
            if (_sendEmailCallback != null)
            {
                try
                {
                    _sendEmailCallback(email, otpCode);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send email: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"Registration OTP for {email}: {otpCode}");
            }

            return (true, tempUserId, string.Empty);
        }
        else
        {
            // No email provided (password-only without email) - register directly
            return await RegisterUserAsync(email, username, password, preferredAuthMethod);
        }
    }

    public async Task<(bool Success, string UserId, string ErrorMessage)> VerifyAndCompleteRegistrationAsync(string tempUserId, string otpCode)
    {
        var otpClient = _tableClientFactory.GetTableClient(OTPTable);
        var pendingRegClient = _tableClientFactory.GetTableClient(PendingRegistrationsTable);

        try
        {
            // Find valid OTP for this temp user ID
            var cutoffTime = DateTime.UtcNow.AddMinutes(-10);
            var filter = $"PartitionKey eq 'OTP' and UserId eq '{tempUserId}' and IsUsed eq false and ExpiryTime gt datetime'{cutoffTime:yyyy-MM-ddTHH:mm:ss.fffffffZ}' and IsRegistration eq true";
            
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
                return (false, string.Empty, "Invalid or expired verification code.");
            }

            // Mark OTP as used
            validOtp["IsUsed"] = true;
            await otpClient.UpdateEntityAsync(validOtp, Azure.ETag.All, TableUpdateMode.Merge);

            // Get pending registration
            var pendingResponse = await pendingRegClient.GetEntityAsync<TableEntity>("PendingReg", tempUserId);
            var pending = pendingResponse.Value;

            // Check if not expired
            var expiryTime = pending.GetDateTimeOffset("ExpiryTime")?.DateTime ?? DateTime.MinValue;
            if (expiryTime < DateTime.UtcNow)
            {
                return (false, string.Empty, "Registration session expired. Please start registration again.");
            }

            // Create the actual user
            var userId = Guid.NewGuid().ToString();
            var user = new User
            {
                UserId = userId,
                Email = pending.GetString("Email") ?? string.Empty,
                Username = pending.GetString("Username") ?? string.Empty,
                NormalizedUsername = pending.GetString("NormalizedUsername") ?? string.Empty,
                PasswordHash = pending.GetString("PasswordHash") ?? string.Empty,
                PasswordSalt = pending.GetString("PasswordSalt") ?? string.Empty,
                PreferredAuthMethod = pending.GetString("PreferredAuthMethod") ?? "Both",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var success = await _userRepository.CreateUserAsync(user);
            if (!success)
            {
                return (false, string.Empty, "Failed to create account. Please try again.");
            }

            // Delete pending registration
            await pendingRegClient.DeleteEntityAsync("PendingReg", tempUserId);

            return (true, userId, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"Verification failed: {ex.Message}");
        }
    }

    private static string GenerateOTP()
    {
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[4];
        rng.GetBytes(randomNumber);
        var code = Math.Abs(BitConverter.ToInt32(randomNumber, 0) % 1000000);
        return code.ToString("D6");
    }

    public async Task<(bool Success, string UserId, string ErrorMessage)> RegisterUserAsync(
        string email, string username, string password, string preferredAuthMethod)
    {
        // Validate username - ALWAYS required
        var (usernameValid, usernameError) = InputValidator.ValidateUsername(username);
        if (!usernameValid)
        {
            return (false, string.Empty, usernameError!);
        }

        // Check if username already exists
        var existingUserByUsername = await _userRepository.GetUserByUsernameAsync(username);
        if (existingUserByUsername != null)
        {
            return (false, string.Empty, "Username already taken. Please choose a different username.");
        }

        // Validate based on preferred auth method
        if (preferredAuthMethod == "Email" || preferredAuthMethod == "Both")
        {
            // Email is required for Email or Both methods
            var (emailValid, emailError) = InputValidator.ValidateEmail(email);
            if (!emailValid)
            {
                return (false, string.Empty, emailError!);
            }

            // Check if email already exists
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(email);
            if (existingUserByEmail != null)
            {
                return (false, string.Empty, "Email already registered.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            // Email is optional for Password-only method, but if provided, validate it
            var (emailValid, emailError) = InputValidator.ValidateEmail(email);
            if (!emailValid)
            {
                return (false, string.Empty, emailError!);
            }

            // Check if email already exists
            var existingUserByEmail = await _userRepository.GetUserByEmailAsync(email);
            if (existingUserByEmail != null)
            {
                return (false, string.Empty, "Email already registered.");
            }
        }

        if (preferredAuthMethod == "Password" || preferredAuthMethod == "Both")
        {
            // Password is required for Password or Both methods
            var (passwordValid, passwordError) = InputValidator.ValidatePassword(password);
            if (!passwordValid)
            {
                return (false, string.Empty, passwordError!);
            }
        }

        // Create new user
        var userId = Guid.NewGuid().ToString();
        var (hash, salt) = (preferredAuthMethod == "Password" || preferredAuthMethod == "Both")
            ? AuthenticationService.HashPassword(password)
            : (string.Empty, string.Empty);

        var user = new User
        {
            UserId = userId,
            Email = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant(),
            Username = username.Trim(),
            NormalizedUsername = InputValidator.NormalizeUsername(username),
            PasswordHash = hash,
            PasswordSalt = salt,
            PreferredAuthMethod = preferredAuthMethod,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var success = await _userRepository.CreateUserAsync(user);
        if (!success)
        {
            return (false, string.Empty, "Failed to create account. Please try again.");
        }

        return (true, userId, string.Empty);
    }

    public async Task<bool> IsEmailAvailableAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return true;
            
        var user = await _userRepository.GetUserByEmailAsync(email);
        return user == null;
    }

    public async Task<bool> IsUsernameAvailableAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;
            
        var user = await _userRepository.GetUserByUsernameAsync(username);
        return user == null;
    }
}
