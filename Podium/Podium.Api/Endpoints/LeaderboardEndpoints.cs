using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;
using Podium.Api.Middleware;
using Podium.Shared.Models;

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
        .RequireAuth()
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
        .RequireAuth()
        .WithName("GetUserStatistics");

        // Get last completed event result with top predictions
        group.MapGet("/season/{seasonId}/last-event-result", async (
            string seasonId,
            [FromServices] IEventRepository eventRepo,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IUserRepository userRepo) =>
        {
            // Get all events for the season
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            
            // Find the last completed event (has result and date is past)
            var completedEvents = new List<Event>();
            var now = DateTime.UtcNow;
            
            foreach (var evt in events.Where(e => e.EventDate <= now).OrderByDescending(e => e.EventDate))
            {
                var result = await eventRepo.GetEventResultAsync(evt.Id);
                if (result != null)
                {
                    completedEvents.Add(evt);
                }
            }
            
            if (!completedEvents.Any())
            {
                return Results.Ok(null); // No completed events yet
            }
            
            var lastEvent = completedEvents.First();
            var eventResult = await eventRepo.GetEventResultAsync(lastEvent.Id);
            
            if (eventResult == null)
            {
                return Results.Ok(null);
            }
            
            // Get all predictions for this event
            var predictions = await predictionRepo.GetPredictionsByEventAsync(lastEvent.Id);
            
            // Filter only predictions with points (scored predictions)
            var scoredPredictions = predictions.Where(p => p.PointsEarned.HasValue && p.PointsEarned.Value > 0).ToList();
            
            // Get user info for all predictions
            var userPredictions = new List<UserEventPrediction>();
            
            foreach (var prediction in scoredPredictions)
            {
                var user = await userRepo.GetUserByIdAsync(prediction.UserId);
                if (user != null)
                {
                    userPredictions.Add(new UserEventPrediction
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        Email = user.Email,
                        FirstPlaceId = prediction.FirstPlaceId,
                        FirstPlaceName = prediction.FirstPlaceName,
                        SecondPlaceId = prediction.SecondPlaceId,
                        SecondPlaceName = prediction.SecondPlaceName,
                        ThirdPlaceId = prediction.ThirdPlaceId,
                        ThirdPlaceName = prediction.ThirdPlaceName,
                        PointsEarned = prediction.PointsEarned ?? 0,
                        SubmittedDate = prediction.SubmittedDate
                    });
                }
            }
            
            // Sort by points (descending) then by submission date (ascending - older is better)
            userPredictions = userPredictions
                .OrderByDescending(p => p.PointsEarned)
                .ThenBy(p => p.SubmittedDate)
                .ToList();
            
            var resultDetails = new EventResultDetails
            {
                EventId = lastEvent.Id,
                EventName = lastEvent.Name,
                SeasonId = seasonId,
                EventNumber = lastEvent.EventNumber,
                EventDate = lastEvent.EventDate,
                FirstPlaceId = eventResult.FirstPlaceId,
                FirstPlaceName = eventResult.FirstPlaceName,
                SecondPlaceId = eventResult.SecondPlaceId,
                SecondPlaceName = eventResult.SecondPlaceName,
                ThirdPlaceId = eventResult.ThirdPlaceId,
                ThirdPlaceName = eventResult.ThirdPlaceName,
                ResultUpdatedDate = eventResult.UpdatedDate,
                TopPredictions = userPredictions
            };
            
            return Results.Ok(resultDetails);
        })
        .RequireAuth()
        .WithName("GetLastEventResult");

        // Search users for last event result
        group.MapGet("/season/{seasonId}/last-event-result/search", async (
            string seasonId,
            [FromQuery] string query,
            [FromServices] IEventRepository eventRepo,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IUserRepository userRepo) =>
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Results.Ok(new List<UserEventPrediction>());
            }
            
            // Get all events for the season
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            
            // Find the last completed event
            var completedEvents = new List<Event>();
            var now = DateTime.UtcNow;
            
            foreach (var evt in events.Where(e => e.EventDate <= now).OrderByDescending(e => e.EventDate))
            {
                var result = await eventRepo.GetEventResultAsync(evt.Id);
                if (result != null)
                {
                    completedEvents.Add(evt);
                }
            }
            
            if (!completedEvents.Any())
            {
                return Results.Ok(new List<UserEventPrediction>());
            }
            
            var lastEvent = completedEvents.First();
            
            // Get all predictions for this event
            var predictions = await predictionRepo.GetPredictionsByEventAsync(lastEvent.Id);
            
            // Search users by query
            var searchResults = await userRepo.SearchUsersAsync(query);
            var userIds = searchResults.Select(u => u.UserId).ToHashSet();
            
            // Filter predictions for matching users
            var matchingPredictions = predictions.Where(p => userIds.Contains(p.UserId)).ToList();
            
            var userPredictions = new List<UserEventPrediction>();
            
            foreach (var prediction in matchingPredictions)
            {
                var user = searchResults.FirstOrDefault(u => u.UserId == prediction.UserId);
                if (user != null)
                {
                    userPredictions.Add(new UserEventPrediction
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        Email = user.Email,
                        FirstPlaceId = prediction.FirstPlaceId,
                        FirstPlaceName = prediction.FirstPlaceName,
                        SecondPlaceId = prediction.SecondPlaceId,
                        SecondPlaceName = prediction.SecondPlaceName,
                        ThirdPlaceId = prediction.ThirdPlaceId,
                        ThirdPlaceName = prediction.ThirdPlaceName,
                        PointsEarned = prediction.PointsEarned ?? 0,
                        SubmittedDate = prediction.SubmittedDate
                    });
                }
            }
            
            // Sort by points (descending) then by submission date (ascending)
            userPredictions = userPredictions
                .OrderByDescending(p => p.PointsEarned)
                .ThenBy(p => p.SubmittedDate)
                .ToList();
            
            return Results.Ok(userPredictions);
        })
        .RequireAuth()
        .WithName("SearchLastEventResult");
    }
}
