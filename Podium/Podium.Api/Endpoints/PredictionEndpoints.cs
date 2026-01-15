using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Models;
using Podium.Shared.Services.Data;
using Podium.Api.Middleware;

namespace Podium.Api.Endpoints;

public static class PredictionEndpoints
{
    public static void MapPredictionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/predictions").WithTags("Predictions");

        // Get user's prediction for an event
        group.MapGet("/{eventId}/user/{userId}", async (
            string eventId,
            string userId,
            HttpContext httpContext,
            [FromServices] IPredictionRepository predictionRepo) =>
        {
            // Ensure user can only access their own predictions
            var authenticatedUserId = httpContext.GetUserId();
            if (authenticatedUserId != userId)
            {
                return Results.Forbid();
            }

            var prediction = await predictionRepo.GetPredictionAsync(eventId, userId);
            // Return 200 with null data if no prediction found (not an error, just no data)
            return Results.Ok(prediction);
        })
        .RequireAuth()
        .WithName("GetPrediction");

        // Get all predictions for an event (public after event starts)
        group.MapGet("/{eventId}", async (
            string eventId,
            [FromServices] IPredictionRepository predictionRepo) =>
        {
            var predictions = await predictionRepo.GetPredictionsByEventAsync(eventId);
            return Results.Ok(predictions);
        })
        .RequireAuth()
        .WithName("GetEventPredictions");

        // Get user's predictions for a season
        group.MapGet("/user/{userId}/season/{seasonId}", async (
            string userId,
            string seasonId,
            HttpContext httpContext,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IEventRepository eventRepo) =>
        {
            // Ensure user can only access their own predictions
            var authenticatedUserId = httpContext.GetUserId();
            if (authenticatedUserId != userId)
            {
                return Results.Forbid();
            }

            // Get all events in the season
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            var eventIds = events.Select(e => e.Id).ToList();

            // Get predictions for those events
            var predictions = await predictionRepo.GetPredictionsByUserAndSeasonAsync(userId, seasonId, eventIds);
            return Results.Ok(predictions);
        })
        .RequireAuth()
        .WithName("GetUserSeasonPredictions");

        // Get user's predictions for all active seasons (optimized bulk endpoint)
        group.MapGet("/user/{userId}/active-seasons", async (
            string userId,
            HttpContext httpContext,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] ISeasonRepository seasonRepo,
            [FromServices] ISeriesRepository seriesRepo,
            [FromServices] IDisciplineRepository disciplineRepo,
            [FromServices] IEventRepository eventRepo) =>
        {
            // Ensure user can only access their own predictions
            var authenticatedUserId = httpContext.GetUserId();
            if (authenticatedUserId != userId)
            {
                return Results.Forbid();
            }

            var allPredictions = new List<Prediction>();

            // Get all active disciplines
            var disciplines = await disciplineRepo.GetActiveDisciplinesAsync();
            
            foreach (var discipline in disciplines)
            {
                // Get active series for this discipline
                var seriesList = await seriesRepo.GetActiveSeriesByDisciplineAsync(discipline.Id);
                
                foreach (var series in seriesList)
                {
                    // Get active season for this series
                    var season = await seasonRepo.GetActiveSeasonBySeriesAsync(series.Id);
                    
                    if (season != null)
                    {
                        // Get all events in the season
                        var events = await eventRepo.GetEventsBySeasonAsync(season.Id);
                        var eventIds = events.Select(e => e.Id).ToList();

                        // Get predictions for those events
                        var predictions = await predictionRepo.GetPredictionsByUserAndSeasonAsync(userId, season.Id, eventIds);
                        allPredictions.AddRange(predictions);
                    }
                }
            }

            return Results.Ok(allPredictions);
        })
        .RequireAuth()
        .WithName("GetUserActiveSeasonsPredictions");

        // Submit/update a prediction
        group.MapPost("/", async (
            [FromBody] SubmitPredictionRequest request,
            HttpContext httpContext,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IEventRepository eventRepo) =>
        {
            // Ensure user can only submit their own predictions
            var authenticatedUserId = httpContext.GetUserId();
            if (authenticatedUserId != request.UserId)
            {
                return Results.Forbid();
            }

            // Validate event exists and accepts predictions
            var eventDetails = await eventRepo.GetEventByIdAsync(request.SeasonId, request.EventId);
            if (eventDetails == null)
                return Results.BadRequest(new { error = "Event not found" });

            if (!eventDetails.CanAcceptPredictions)
                return Results.BadRequest(new { error = "Event no longer accepts predictions" });

            // Validate all three competitors are different
            if (request.FirstPlaceId == request.SecondPlaceId || 
                request.FirstPlaceId == request.ThirdPlaceId || 
                request.SecondPlaceId == request.ThirdPlaceId)
            {
                return Results.BadRequest(new { error = "All three competitors must be different" });
            }

            var prediction = new Prediction
            {
                EventId = request.EventId,
                UserId = request.UserId,
                FirstPlaceId = request.FirstPlaceId,
                FirstPlaceName = request.FirstPlaceName,
                SecondPlaceId = request.SecondPlaceId,
                SecondPlaceName = request.SecondPlaceName,
                ThirdPlaceId = request.ThirdPlaceId,
                ThirdPlaceName = request.ThirdPlaceName,
                PointsEarned = null,
                SubmittedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            var success = await predictionRepo.SavePredictionAsync(prediction);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "Prediction saved successfully", prediction });
        })
        .RequireAuth()
        .WithName("SubmitPrediction");
    }
}

// Request DTOs
public record SubmitPredictionRequest(
    string EventId,
    string SeasonId,
    string UserId,
    string FirstPlaceId,
    string FirstPlaceName,
    string SecondPlaceId,
    string SecondPlaceName,
    string ThirdPlaceId,
    string ThirdPlaceName
);
