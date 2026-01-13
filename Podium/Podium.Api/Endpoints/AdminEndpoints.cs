using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;

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
        .WithName("FindDuplicateActiveSeasons");
    }
}
