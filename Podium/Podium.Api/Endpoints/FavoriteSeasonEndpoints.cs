using Microsoft.AspNetCore.Mvc;
using Podium.Api.Middleware;
using Podium.Shared.Models;
using Podium.Shared.Services.Data;

namespace Podium.Api.Endpoints;

public static class FavoriteSeasonEndpoints
{
    public static void MapFavoriteSeasonEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/favorites")
            .WithTags("Favorites");

        // Get user's favorite seasons
        group.MapGet("/seasons", async (
            HttpContext context,
            [FromServices] IFavoriteSeasonRepository favoriteRepo) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var favorites = await favoriteRepo.GetUserFavoriteSeasonsAsync(userId);
            return Results.Ok(favorites);
        })
        .RequireAuth()
        .WithName("GetFavoriteSeasons");

        // Add a season to favorites
        group.MapPost("/seasons/{seasonId}", async (
            HttpContext context,
            string seasonId,
            [FromBody] AddFavoriteSeasonRequest request,
            [FromServices] IFavoriteSeasonRepository favoriteRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            // Check if user already has 5 favorites
            var count = await favoriteRepo.GetUserFavoriteCountAsync(userId);
            if (count >= 5)
                return Results.BadRequest(new { error = "You can only have up to 5 favorite seasons" });

            // Check if already a favorite
            var isFavorite = await favoriteRepo.IsFavoriteAsync(userId, seasonId);
            if (isFavorite)
                return Results.BadRequest(new { error = "Season is already in your favorites" });

            // Get season details to verify it exists
            var season = await seasonRepo.GetSeasonByIdOnlyAsync(seasonId);
            if (season == null)
                return Results.NotFound(new { error = "Season not found" });

            var success = await favoriteRepo.AddFavoriteSeasonAsync(
                userId, 
                seasonId, 
                request.SeasonName, 
                request.SeriesName, 
                request.Year);

            if (!success)
                return Results.BadRequest(new { error = "Failed to add favorite season" });

            return Results.Ok(new { message = "Season added to favorites" });
        })
        .RequireAuth()
        .WithName("AddFavoriteSeason");

        // Remove a season from favorites
        group.MapDelete("/seasons/{seasonId}", async (
            HttpContext context,
            string seasonId,
            [FromServices] IFavoriteSeasonRepository favoriteRepo) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var success = await favoriteRepo.RemoveFavoriteSeasonAsync(userId, seasonId);
            if (!success)
                return Results.BadRequest(new { error = "Failed to remove favorite season" });

            return Results.Ok(new { message = "Season removed from favorites" });
        })
        .RequireAuth()
        .WithName("RemoveFavoriteSeason");

        // Check if a season is favorited
        group.MapGet("/seasons/{seasonId}/check", async (
            HttpContext context,
            string seasonId,
            [FromServices] IFavoriteSeasonRepository favoriteRepo) =>
        {
            var userId = context.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var isFavorite = await favoriteRepo.IsFavoriteAsync(userId, seasonId);
            return Results.Ok(new { isFavorite });
        })
        .RequireAuth()
        .WithName("CheckFavoriteSeason");
    }
}

public record AddFavoriteSeasonRequest(
    string SeasonName,
    string SeriesName,
    int Year
);
