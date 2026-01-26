using System.Text.RegularExpressions;

namespace Podium.Shared.Utilities;

public static class InputValidator
{
    // Username validation: Latin letters, numbers, spaces - max 50 characters
    private static readonly Regex UsernameRegex = new Regex(@"^[a-zA-Z0-9 ]+$", RegexOptions.Compiled);
    
    // Password validation: Latin letters, numbers, common special chars - no spaces - max 100 characters
    private static readonly Regex PasswordRegex = new Regex(@"^[a-zA-Z0-9!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]+$", RegexOptions.Compiled);
    
    // Email validation: basic email format
    private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    public static (bool IsValid, string? ErrorMessage) ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return (false, "Username is required.");
        }

        var trimmed = username.Trim();
        
        if (trimmed.Length < 3)
        {
            return (false, "Username must be at least 3 characters.");
        }

        if (trimmed.Length > 50)
        {
            return (false, "Username cannot exceed 50 characters.");
        }

        if (!UsernameRegex.IsMatch(trimmed))
        {
            return (false, "Username can only contain Latin letters, numbers, and spaces.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return (false, "Password is required.");
        }

        if (password.Length < 6)
        {
            return (false, "Password must be at least 6 characters.");
        }

        if (password.Length > 100)
        {
            return (false, "Password cannot exceed 100 characters.");
        }

        if (password.Contains(' '))
        {
            return (false, "Password cannot contain spaces.");
        }

        if (!PasswordRegex.IsMatch(password))
        {
            return (false, "Password can only contain Latin letters, numbers, and common special characters.");
        }

        return (true, null);
    }

    public static (bool IsValid, string? ErrorMessage) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return (false, "Email is required.");
        }

        var trimmed = email.Trim();

        if (!EmailRegex.IsMatch(trimmed))
        {
            return (false, "Please enter a valid email address.");
        }

        return (true, null);
    }

    public static string NormalizeUsername(string username)
    {
        // Normalize username for lookup: lowercase and trim
        return username.Trim().ToLowerInvariant();
    }
}
