using Podium.Shared.Services.Data;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Auth;

public interface IRegistrationService
{
    Task<(bool Success, string UserId, string ErrorMessage)> RegisterUserAsync(string email, string username, string password, string preferredAuthMethod);
    Task<bool> IsEmailAvailableAsync(string email);
}

public class RegistrationService : IRegistrationService
{
    private readonly IUserRepository _userRepository;

    public RegistrationService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(bool Success, string UserId, string ErrorMessage)> RegisterUserAsync(
        string email, string username, string password, string preferredAuthMethod)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
        {
            return (false, string.Empty, "Invalid email address.");
        }

        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            return (false, string.Empty, "Username must be at least 3 characters.");
        }

        if (preferredAuthMethod != "Email" && (string.IsNullOrWhiteSpace(password) || password.Length < 6))
        {
            return (false, string.Empty, "Password must be at least 6 characters.");
        }

        // Check if email already exists
        var existingUser = await _userRepository.GetUserByEmailAsync(email);
        if (existingUser != null)
        {
            return (false, string.Empty, "Email already registered.");
        }

        // Create new user
        var userId = Guid.NewGuid().ToString();
        var (hash, salt) = preferredAuthMethod == "Email" 
            ? (string.Empty, string.Empty) 
            : AuthenticationService.HashPassword(password);

        var user = new User
        {
            UserId = userId,
            Email = email,
            Username = username,
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
        var user = await _userRepository.GetUserByEmailAsync(email);
        return user == null;
    }
}
