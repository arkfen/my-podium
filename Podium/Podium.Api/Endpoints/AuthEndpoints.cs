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
        .WithName("Register")
        .WithOpenApi();

        // Send OTP to email
        group.MapPost("/send-otp", async (
            [FromBody] SendOtpRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, errorMessage) = await authService.SendOTPAsync(request.Email);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { message = "OTP sent to email" });
        })
        .WithName("SendOTP")
        .WithOpenApi();

        // Verify OTP
        group.MapPost("/verify-otp", async (
            [FromBody] VerifyOtpRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, errorMessage) = await authService.VerifyOTPAsync(
                request.Email, 
                request.OtpCode);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { userId, username, message = "Authentication successful" });
        })
        .WithName("VerifyOTP")
        .WithOpenApi();

        // Sign in with password
        group.MapPost("/signin", async (
            [FromBody] SignInRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            var (success, userId, username, errorMessage) = await authService.SignInWithPasswordAsync(
                request.Email, 
                request.Password);

            if (!success)
            {
                return Results.BadRequest(new { error = errorMessage });
            }

            return Results.Ok(new { userId, username, message = "Sign in successful" });
        })
        .WithName("SignIn")
        .WithOpenApi();

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
        .WithName("ValidateSession")
        .WithOpenApi();

        // Sign out
        group.MapPost("/signout", async (
            [FromBody] SignOutRequest request,
            [FromServices] IAuthenticationService authService) =>
        {
            await authService.SignOutAsync(request.SessionId);
            return Results.Ok(new { message = "Signed out successfully" });
        })
        .WithName("SignOut")
        .WithOpenApi();
    }
}

// Request DTOs
public record RegisterRequest(string Email, string Username, string Password, string PreferredAuthMethod);
public record SendOtpRequest(string Email);
public record VerifyOtpRequest(string Email, string OtpCode);
public record SignInRequest(string Email, string Password);
public record ValidateSessionRequest(string SessionId);
public record SignOutRequest(string SessionId);
