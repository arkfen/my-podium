using Microsoft.AspNetCore.Mvc;
using Podium.Api.Middleware;
using Podium.Shared.Models;
using Podium.Shared.Services.Auth;
using Podium.Shared.Services.Data;
using Podium.Shared.Utilities;

namespace Podium.Api.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/profile")
            .WithTags("Profile");

        // Get current user's profile
        group.MapGet("/", async (
            HttpContext context,
            [FromServices] IUserRepository userRepository) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            return Results.Ok(new UserProfileResponse(
                user.UserId,
                user.Email,
                user.Username,
                user.PreferredAuthMethod,
                HasPassword: !string.IsNullOrEmpty(user.PasswordHash),
                HasEmail: !string.IsNullOrEmpty(user.Email)
            ));
        })
        .RequireAuth()
        .WithName("GetProfile");

        // Update username
        group.MapPost("/username", async (
            HttpContext context,
            [FromBody] UpdateUsernameRequest request,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            // Verify identity with password or OTP
            var verified = await VerifyIdentityAsync(user, request.Password, request.OtpCode, authService);
            if (!verified.Success)
                return Results.BadRequest(new { error = verified.Error });

            // Validate username
            var (usernameValid, usernameError) = InputValidator.ValidateUsername(request.NewUsername);
            if (!usernameValid)
                return Results.BadRequest(new { error = usernameError });

            // Check if username is already taken
            var existingUser = await userRepository.GetUserByUsernameAsync(request.NewUsername);
            if (existingUser != null && existingUser.UserId != userId)
                return Results.BadRequest(new { error = "Username already taken" });

            user.Username = request.NewUsername;
            user.NormalizedUsername = InputValidator.NormalizeUsername(request.NewUsername);
            var success = await userRepository.UpdateUserAsync(user);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update username" });

            return Results.Ok(new { message = "Username updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdateUsername");

        // Update auth method
        group.MapPost("/auth-method", async (
            HttpContext context,
            [FromBody] UpdateAuthMethodRequest request,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            // Validate new auth method
            var validMethods = new[] { "Email", "Password", "Both" };
            if (!validMethods.Contains(request.NewAuthMethod))
                return Results.BadRequest(new { error = "Invalid auth method. Must be 'Email', 'Password', or 'Both'" });

            // Check if user can switch to the new method
            var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
            var hasEmail = !string.IsNullOrEmpty(user.Email);

            if ((request.NewAuthMethod == "Password" || request.NewAuthMethod == "Both") && !hasPassword)
            {
                return Results.BadRequest(new { 
                    error = "You must set a password first before enabling password sign-in",
                    requiresPasswordSetup = true
                });
            }

            if ((request.NewAuthMethod == "Email" || request.NewAuthMethod == "Both") && !hasEmail)
            {
                return Results.BadRequest(new { 
                    error = "You must set an email first before enabling email sign-in",
                    requiresEmailSetup = true
                });
            }

            // Verify identity before changing auth method
            var verified = await VerifyIdentityAsync(user, request.Password, request.OtpCode, authService);
            if (!verified.Success)
                return Results.BadRequest(new { error = verified.Error });

            user.PreferredAuthMethod = request.NewAuthMethod;
            var success = await userRepository.UpdateUserAsync(user);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update auth method" });

            return Results.Ok(new { message = "Sign-in method updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdateAuthMethod");

        // Update password
        group.MapPost("/password", async (
            HttpContext context,
            [FromBody] UpdatePasswordRequest request,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            // Validate new password
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return Results.BadRequest(new { error = "Password must be at least 6 characters" });

            var hasExistingPassword = !string.IsNullOrEmpty(user.PasswordHash);

            if (hasExistingPassword)
            {
                // User has password, verify with old password
                if (string.IsNullOrEmpty(request.OldPassword))
                    return Results.BadRequest(new { error = "Current password is required" });

                var verified = await VerifyIdentityAsync(user, request.OldPassword, null, authService);
                if (!verified.Success)
                    return Results.BadRequest(new { error = "Current password is incorrect" });
            }
            else
            {
                // User doesn't have password, verify with OTP
                if (string.IsNullOrEmpty(request.OtpCode))
                    return Results.BadRequest(new { error = "Verification code is required to set up password" });

                var verified = await VerifyIdentityAsync(user, null, request.OtpCode, authService);
                if (!verified.Success)
                    return Results.BadRequest(new { error = verified.Error });
            }

            // Hash and set new password
            var (hash, salt) = AuthenticationService.HashPassword(request.NewPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;

            var success = await userRepository.UpdateUserAsync(user);
            if (!success)
                return Results.BadRequest(new { error = "Failed to update password" });

            return Results.Ok(new { message = "Password updated successfully" });
        })
        .RequireAuth()
        .WithName("UpdatePassword");

        // Send OTP for password setup (when user doesn't have password)
        group.MapPost("/password/send-otp", async (
            HttpContext context,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            if (string.IsNullOrEmpty(user.Email))
                return Results.BadRequest(new { error = "No email address set. Please add an email first." });

            var (success, error) = await authService.SendOTPAsync(user.Email);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.Ok(new { message = "Verification code sent to your email" });
        })
        .RequireAuth()
        .WithName("SendPasswordSetupOtp");

        // Send OTP for email update
        group.MapPost("/email/send-otp", async (
            HttpContext context,
            [FromBody] SendEmailUpdateOtpRequest request,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.NewEmail) || !request.NewEmail.Contains("@"))
                return Results.BadRequest(new { error = "Please enter a valid email address" });

            // Check if email is already in use by another user
            var existingUser = await userRepository.GetUserByEmailAsync(request.NewEmail);
            if (existingUser != null && existingUser.UserId != userId)
                return Results.BadRequest(new { error = "This email is already in use" });

            // Send OTP to the new email using special method that doesn't require user to exist
            var (success, error) = await authService.SendOTPForNewEmailAsync(request.NewEmail, userId);
            if (!success)
                return Results.BadRequest(new { error });

            return Results.Ok(new { message = "Verification code sent to new email address" });
        })
        .RequireAuth()
        .WithName("SendEmailUpdateOtp");

        // Confirm email update with OTP
        group.MapPost("/email/confirm", async (
            HttpContext context,
            [FromBody] ConfirmEmailUpdateRequest request,
            [FromServices] IUserRepository userRepository,
            [FromServices] IAuthenticationService authService) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var user = await userRepository.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            if (string.IsNullOrWhiteSpace(request.NewEmail) || !request.NewEmail.Contains("@"))
                return Results.BadRequest(new { error = "Please enter a valid email address" });

            if (string.IsNullOrEmpty(request.OtpCode))
                return Results.BadRequest(new { error = "Verification code is required" });

            // Verify OTP for the new email
            var (success, _, _, _, error) = await authService.VerifyOTPAsync(request.NewEmail, request.OtpCode);
            if (!success)
                return Results.BadRequest(new { error = "Invalid or expired verification code" });

            // Update email
            user.Email = request.NewEmail.ToLowerInvariant();
            var updateSuccess = await userRepository.UpdateUserAsync(user);
            if (!updateSuccess)
                return Results.BadRequest(new { error = "Failed to update email" });

            return Results.Ok(new { message = "Email updated successfully" });
        })
        .RequireAuth()
        .WithName("ConfirmEmailUpdate");
    }

    private static async Task<(bool Success, string Error)> VerifyIdentityAsync(
        User user, 
        string? password, 
        string? otpCode,
        IAuthenticationService authService)
    {
        var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
        var hasEmail = !string.IsNullOrEmpty(user.Email);

        // If password provided and user has password
        if (!string.IsNullOrEmpty(password) && hasPassword)
        {
            var (success, _, _, _, error) = await authService.SignInWithPasswordAsync(user.Email, password);
            if (success)
                return (true, string.Empty);
            return (false, "Invalid password");
        }

        // If OTP provided and user has email
        if (!string.IsNullOrEmpty(otpCode) && hasEmail)
        {
            var (success, _, _, _, error) = await authService.VerifyOTPAsync(user.Email, otpCode);
            if (success)
                return (true, string.Empty);
            return (false, "Invalid or expired verification code");
        }

        // No valid verification provided
        if (hasPassword && hasEmail)
            return (false, "Please provide your password or verification code");
        else if (hasPassword)
            return (false, "Please provide your password");
        else if (hasEmail)
            return (false, "Please provide the verification code");
        else
            return (false, "Unable to verify identity");
    }
}

// Request DTOs
public record UpdateUsernameRequest(string NewUsername, string? Password, string? OtpCode);
public record UpdateAuthMethodRequest(string NewAuthMethod, string? Password, string? OtpCode);
public record UpdatePasswordRequest(string? OldPassword, string? OtpCode, string NewPassword);
public record SendEmailUpdateOtpRequest(string NewEmail);
public record ConfirmEmailUpdateRequest(string NewEmail, string OtpCode);
public record UserProfileResponse(
    string UserId, 
    string Email, 
    string Username, 
    string PreferredAuthMethod,
    bool HasPassword,
    bool HasEmail);
