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

        // Get user's predictions with full details (event, season, series, discipline, results)
        // Supports filtering by seasonId (optional query param) and includeInactive flag
        group.MapGet("/user/{userId}/details", async (
            string userId,
            HttpContext httpContext,
            [FromQuery] string? seasonId,
            [FromQuery] string? seriesId,
            [FromQuery] string? disciplineId,
            [FromQuery] bool? includeInactive,
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

            var allPredictionsWithDetails = new List<PredictionWithDetails>();
            var includeInactiveSeasons = includeInactive ?? false;

            // Get disciplines based on filter
            var disciplines = string.IsNullOrEmpty(disciplineId)
                ? (includeInactiveSeasons ? await disciplineRepo.GetAllDisciplinesAsync() : await disciplineRepo.GetActiveDisciplinesAsync())
                : new List<Discipline> { await disciplineRepo.GetDisciplineByIdAsync(disciplineId) }.Where(d => d != null).ToList()!;

            foreach (var discipline in disciplines)
            {
                // Get series based on filter
                var seriesList = string.IsNullOrEmpty(seriesId)
                    ? (includeInactiveSeasons ? await seriesRepo.GetSeriesByDisciplineAsync(discipline.Id) : await seriesRepo.GetActiveSeriesByDisciplineAsync(discipline.Id))
                    : new List<Series> { await seriesRepo.GetSeriesByIdOnlyAsync(seriesId) }.Where(s => s != null).ToList()!;

                foreach (var series in seriesList)
                {
                    // Get seasons based on filter
                    var seasons = new List<Season>();
                    if (!string.IsNullOrEmpty(seasonId))
                    {
                        var season = await seasonRepo.GetSeasonByIdOnlyAsync(seasonId);
                        if (season != null) seasons.Add(season);
                    }
                    else if (includeInactiveSeasons)
                    {
                        seasons = await seasonRepo.GetSeasonsBySeriesAsync(series.Id);
                    }
                    else
                    {
                        var activeSeason = await seasonRepo.GetActiveSeasonBySeriesAsync(series.Id);
                        if (activeSeason != null) seasons.Add(activeSeason);
                    }

                    foreach (var season in seasons)
                    {
                        // Get all events in the season
                        var events = await eventRepo.GetEventsBySeasonAsync(season.Id);
                        var eventIds = events.Select(e => e.Id).ToList();

                        // Get predictions for those events
                        var predictions = await predictionRepo.GetPredictionsByUserAndSeasonAsync(userId, season.Id, eventIds);

                        // Build PredictionWithDetails for each prediction
                        foreach (var prediction in predictions)
                        {
                            var evt = events.FirstOrDefault(e => e.Id == prediction.EventId);
                            if (evt == null) continue;

                            // Get event result if event is completed
                            EventResult? result = null;
                            if (evt.Status == "Completed")
                            {
                                result = await eventRepo.GetEventResultAsync(evt.Id);
                            }

                            var predictionWithDetails = new PredictionWithDetails
                            {
                                // Prediction data
                                EventId = prediction.EventId,
                                UserId = prediction.UserId,
                                FirstPlaceId = prediction.FirstPlaceId,
                                FirstPlaceName = prediction.FirstPlaceName,
                                SecondPlaceId = prediction.SecondPlaceId,
                                SecondPlaceName = prediction.SecondPlaceName,
                                ThirdPlaceId = prediction.ThirdPlaceId,
                                ThirdPlaceName = prediction.ThirdPlaceName,
                                PointsEarned = prediction.PointsEarned,
                                SubmittedDate = prediction.SubmittedDate,
                                UpdatedDate = prediction.UpdatedDate,

                                // Event information
                                EventName = evt.Name,
                                EventDisplayName = evt.DisplayName,
                                EventNumber = evt.EventNumber,
                                EventDate = evt.EventDate,
                                EventLocation = evt.Location,
                                EventStatus = evt.Status,

                                // Season information
                                SeasonId = season.Id,
                                SeasonName = season.Name,
                                SeasonYear = season.Year,
                                SeasonIsActive = season.IsActive,

                                // Series information
                                SeriesId = series.Id,
                                SeriesName = series.Name,
                                SeriesDisplayName = series.DisplayName,

                                // Discipline information
                                DisciplineId = discipline.Id,
                                DisciplineName = discipline.Name,
                                DisciplineDisplayName = discipline.DisplayName,

                                // Actual results (if available)
                                ActualFirstPlaceId = result?.FirstPlaceId,
                                ActualFirstPlaceName = result?.FirstPlaceName,
                                ActualSecondPlaceId = result?.SecondPlaceId,
                                ActualSecondPlaceName = result?.SecondPlaceName,
                                ActualThirdPlaceId = result?.ThirdPlaceId,
                                ActualThirdPlaceName = result?.ThirdPlaceName
                            };

                            allPredictionsWithDetails.Add(predictionWithDetails);
                        }
                    }
                }
            }

            return Results.Ok(allPredictionsWithDetails);
        })
        .RequireAuth()
        .WithName("GetUserPredictionsWithDetails");

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
