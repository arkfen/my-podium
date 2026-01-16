using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Auth;
using Podium.Shared.Services.Data;

namespace Podium.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        // Register new user
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            [FromServices] IRegistrationService registrationService) =>
        {
            var (success, userId, errorMessage) = await registrationService.RegisterUserAsync(
                request.Email, 
                request.Username, 
                request.Password, 
                request.PreferredAuthMethod);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { userId, message = "Registration successful" });
        })
        .WithName("Register");

        // Send OTP to email
        group.MapPost("/send-otp", async (
            [FromBody] SendOtpRequest request,
            [FromServices] IAuthenticationService authService,
            [FromServices] IUserRepository userRepo) =>
        {
            // Check user's preferred auth method
            var user = await userRepo.GetUserByEmailAsync(request.Email);
            if (user != null && user.PreferredAuthMethod == "Password")
            {
                return Results.BadRequest(new { error = "Email authentication is not enabled for this account. Please sign in with password." });
            }

            var (success, errorMessage) = await authService.SendOTPAsync(request.Email);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { message = "OTP sent to email" });
        })
        .WithName("SendOTP");

        // Verify OTP
        group.MapPost("/verify-otp", async (
            [FromBody] VerifyOtpRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, sessionId, errorMessage) = await authService.VerifyOTPAsync(
                request.Email, 
                request.OtpCode);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { userId, username, sessionId, message = "Authentication successful" });
        })
        .WithName("VerifyOTP");

        // Sign in with password
        group.MapPost("/signin", async (
            [FromBody] SignInRequest request,
            [FromServices] IAuthenticationService authService,
            [FromServices] IUserRepository userRepo) =>
        {
            // Check user's preferred auth method
            var user = await userRepo.GetUserByEmailAsync(request.Email);
            if (user != null && user.PreferredAuthMethod == "Email")
            {
                return Results.BadRequest(new { error = "Password authentication is not enabled for this account. Please sign in with email OTP." });
            }

            var (success, userId, username, sessionId, errorMessage) = await authService.SignInWithPasswordAsync(
                request.Email, 
                request.Password);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { userId, username, sessionId, message = "Sign in successful" });
        })
        .WithName("SignIn");

        // Validate session
        group.MapPost("/validate-session", async (
            [FromBody] ValidateSessionRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, sessionId, errorMessage) = 
                await authService.ValidateSessionAsync(request.SessionId);

            if (!success)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { userId, username, sessionId });
        })
        .WithName("ValidateSession");

        // Sign out
        group.MapPost("/signout", async (
            [FromBody] SignOutRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            await authService.SignOutAsync(request.SessionId);
            return Results.Ok(new { message = "Signed out successfully" });
        })
        .WithName("SignOut");
    }
}

// Request DTOs
public record RegisterRequest(string Email, string Username, string Password, string PreferredAuthMethod);
public record SendOtpRequest(string Email);
public record VerifyOtpRequest(string Email, string OtpCode);
public record SignInRequest(string Email, string Password);
public record ValidateSessionRequest(string SessionId);
public record SignOutRequest(string SessionId);
