using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Models;
using Podium.Shared.Services.Data;

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
            [FromServices] IPredictionRepository predictionRepo) =>
        {
            var prediction = await predictionRepo.GetPredictionAsync(eventId, userId);
            if (prediction == null)
                return Results.NotFound(new { error = "No prediction found" });
            
            return Results.Ok(prediction);
        })
        .WithName("GetPrediction")
        .WithOpenApi();

        // Get all predictions for an event
        group.MapGet("/{eventId}", async (
            string eventId,
            [FromServices] IPredictionRepository predictionRepo) =>
        {
            var predictions = await predictionRepo.GetPredictionsByEventAsync(eventId);
            return Results.Ok(predictions);
        })
        .WithName("GetEventPredictions")
        .WithOpenApi();

        // Get user's predictions for a season
        group.MapGet("/user/{userId}/season/{seasonId}", async (
            string userId,
            string seasonId,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IEventRepository eventRepo) =>
        {
            // Get all events in the season
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            var eventIds = events.Select(e => e.Id).ToList();

            // Get predictions for those events
            var predictions = await predictionRepo.GetPredictionsByUserAndSeasonAsync(userId, seasonId, eventIds);
            return Results.Ok(predictions);
        })
        .WithName("GetUserSeasonPredictions")
        .WithOpenApi();

        // Submit/update a prediction
        group.MapPost("/", async (
            [FromBody] SubmitPredictionRequest request,
            [FromServices] IPredictionRepository predictionRepo,
            [FromServices] IEventRepository eventRepo) =>
        {
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
        .WithName("SubmitPrediction")
        .WithOpenApi();
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
