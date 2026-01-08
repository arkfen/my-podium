using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;

namespace Podium.Api.Endpoints;

public static class SportEndpoints
{
    public static void MapSportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Disciplines & Competitions");

        // Get all active disciplines
        group.MapGet("/disciplines", async ([FromServices] IDisciplineRepository disciplineRepo) =>
        {
            var disciplines = await disciplineRepo.GetActiveDisciplinesAsync();
            return Results.Ok(disciplines);
        })
        .WithName("GetDisciplines");

        // Get series for a discipline
        group.MapGet("/disciplines/{disciplineId}/series", async (
            string disciplineId,
            [FromServices] ISeriesRepository seriesRepo) =>
        {
            var series = await seriesRepo.GetActiveSeriesByDisciplineAsync(disciplineId);
            return Results.Ok(series);
        })
        .WithName("GetSeries");

        // Get seasons for a series
        group.MapGet("/series/{seriesId}/seasons", async (
            string seriesId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var seasons = await seasonRepo.GetSeasonsBySeriesAsync(seriesId);
            return Results.Ok(seasons);
        })
        .WithName("GetSeasons");

        // Get active season for a series
        group.MapGet("/series/{seriesId}/seasons/active", async (
            string seriesId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var season = await seasonRepo.GetActiveSeasonBySeriesAsync(seriesId);
            if (season == null)
                return Results.NotFound(new { error = "No active season found" });
            
            return Results.Ok(season);
        })
        .WithName("GetActiveSeason");

        // Get events for a season
        group.MapGet("/seasons/{seasonId}/events", async (
            string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            return Results.Ok(events);
        })
        .WithName("GetEvents");

        // Get upcoming events for a season
        group.MapGet("/seasons/{seasonId}/events/upcoming", async (
            string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var events = await eventRepo.GetUpcomingEventsBySeasonAsync(seasonId);
            return Results.Ok(events);
        })
        .WithName("GetUpcomingEvents");

        // Get competitors for a season
        group.MapGet("/seasons/{seasonId}/competitors", async (
            string seasonId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var competitors = await competitorRepo.GetCompetitorsBySeasonAsync(seasonId);
            return Results.Ok(competitors);
        })
        .WithName("GetCompetitors");

        // Get event details
        group.MapGet("/events/{eventId}", async (
            string eventId,
            string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var eventDetails = await eventRepo.GetEventByIdAsync(seasonId, eventId);
            if (eventDetails == null)
                return Results.NotFound(new { error = "Event not found" });
            
            return Results.Ok(eventDetails);
        })
        .WithName("GetEventDetails");

        // Get event result
        group.MapGet("/events/{eventId}/result", async (
            string eventId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var result = await eventRepo.GetEventResultAsync(eventId);
            if (result == null)
                return Results.NotFound(new { error = "Result not available yet" });
            
            return Results.Ok(result);
        })
        .WithName("GetEventResult");
    }
}
