using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;

namespace Podium.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leaderboard").WithTags("Leaderboard");

        // Get leaderboard for a season
        group.MapGet("/season/{seasonId}", async (
            string seasonId,
            [FromServices] ILeaderboardRepository leaderboardRepo) =>
        {
            var leaderboard = await leaderboardRepo.GetLeaderboardBySeasonAsync(seasonId);
            return Results.Ok(leaderboard);
        })
        .WithName("GetLeaderboard");

        // Get user statistics for a season
        group.MapGet("/season/{seasonId}/user/{userId}", async (
            string seasonId,
            string userId,
            [FromServices] ILeaderboardRepository leaderboardRepo) =>
        {
            var stats = await leaderboardRepo.GetUserStatisticsAsync(seasonId, userId);
            if (stats == null)
            {
                // Return empty stats if user hasn't made predictions yet
                return Results.Ok(new
                {
                    seasonId,
                    userId,
                    username = "",
                    totalPoints = 0,
                    predictionsCount = 0,
                    exactMatches = 0,
                    oneOffMatches = 0,
                    twoOffMatches = 0
                });
            }
            
            return Results.Ok(stats);
        })
        .WithName("GetUserStatistics");
    }
}
