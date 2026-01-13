using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;
using Podium.Shared.Models;
using Podium.Api.Middleware;

namespace Podium.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        // Set active season (automatically deactivates other seasons in the same series)
        group.MapPost("/series/{seriesId}/seasons/{seasonId}/set-active", async (
            string seriesId,
            string seasonId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var success = await seasonRepo.SetActiveSeasonAsync(seriesId, seasonId);
            
            if (!success)
                return Results.BadRequest(new { error = "Failed to set active season. Season may not exist." });
            
            return Results.Ok(new { message = "Season activated successfully. Other seasons in this series have been deactivated." });
        })
        .RequireAdmin()
        .WithName("SetActiveSeason");

        // Diagnostic: Find series with multiple active seasons
        group.MapGet("/diagnostics/duplicate-active-seasons", async (
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var duplicates = await seasonRepo.FindSeriesWithMultipleActiveSeasonsAsync();
            
            if (duplicates.Count == 0)
                return Results.Ok(new { message = "No duplicate active seasons found.", series = new { } });
            
            return Results.Ok(new 
            { 
                message = $"Found {duplicates.Count} series with multiple active seasons.",
                series = duplicates 
            });
        })
        .RequireAdmin()
        .WithName("FindDuplicateActiveSeasons");

        // Get all admins
        group.MapGet("/admins", async (
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admins = await adminRepo.GetAllAdminsAsync();
            return Results.Ok(admins);
        })
        .RequireAdmin()
        .WithName("GetAllAdmins");

        // Get specific admin
        group.MapGet("/admins/{userId}", async (
            string userId,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admin = await adminRepo.GetAdminAsync(userId);
            if (admin == null)
                return Results.NotFound(new { error = "Admin not found" });
            
            return Results.Ok(admin);
        })
        .RequireAdmin()
        .WithName("GetAdmin");

        // Create new admin (requires admin management permission)
        group.MapPost("/admins", async (
            [FromBody] CreateAdminRequest request,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo,
            [FromServices] IUserRepository userRepo) =>
        {
            // Verify the user exists
            var user = await userRepo.GetUserByIdAsync(request.UserId);
            if (user == null)
                return Results.BadRequest(new { error = "User not found" });

            // Check if already an admin
            var existingAdmin = await adminRepo.GetAdminAsync(request.UserId);
            if (existingAdmin != null)
                return Results.BadRequest(new { error = "User is already an admin" });

            var createdBy = httpContext.GetUserId() ?? "System";
            var admin = new Admin
            {
                UserId = request.UserId,
                IsActive = request.IsActive,
                CanManageAdmins = request.CanManageAdmins,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            var success = await adminRepo.CreateAdminAsync(admin);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "Admin created successfully", admin });
        })
        .RequireAdminManagement()
        .WithName("CreateAdmin");

        // Update admin (requires admin management permission)
        group.MapPut("/admins/{userId}", async (
            string userId,
            [FromBody] UpdateAdminRequest request,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var admin = await adminRepo.GetAdminAsync(userId);
            if (admin == null)
                return Results.NotFound(new { error = "Admin not found" });

            var modifiedBy = httpContext.GetUserId() ?? "System";
            admin.IsActive = request.IsActive;
            admin.CanManageAdmins = request.CanManageAdmins;
            admin.LastModifiedDate = DateTime.UtcNow;
            admin.LastModifiedBy = modifiedBy;

            var success = await adminRepo.UpdateAdminAsync(admin);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "Admin updated successfully", admin });
        })
        .RequireAdminManagement()
        .WithName("UpdateAdmin");

        // Remove admin (requires admin management permission)
        group.MapDelete("/admins/{userId}", async (
            string userId,
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var requestingUserId = httpContext.GetUserId();
            if (requestingUserId == userId)
                return Results.BadRequest(new { error = "Cannot remove yourself as admin" });

            var success = await adminRepo.DeleteAdminAsync(userId);
            if (!success)
                return Results.NotFound(new { error = "Admin not found" });

            return Results.Ok(new { message = "Admin removed successfully" });
        })
        .RequireAdminManagement()
        .WithName("RemoveAdmin");
    }
}

// Request DTOs
public record CreateAdminRequest(string UserId, bool IsActive, bool CanManageAdmins);
public record UpdateAdminRequest(bool IsActive, bool CanManageAdmins);
