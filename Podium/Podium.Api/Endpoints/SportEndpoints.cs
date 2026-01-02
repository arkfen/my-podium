using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;

namespace Podium.Api.Endpoints;

public static class SportEndpoints
{
    public static void MapSportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Sports & Competitions");

        // Get all active sports
        group.MapGet("/sports", async ([FromServices] ISportRepository sportRepo) =>
        {
            var sports = await sportRepo.GetActiveSportsAsync();
            return Results.Ok(sports);
        })
        .WithName("GetSports");

        // Get tiers for a sport
        group.MapGet("/sports/{sportId}/tiers", async (
            string sportId,
            [FromServices] ITierRepository tierRepo) =>
        {
            var tiers = await tierRepo.GetActiveTiersBySportAsync(sportId);
            return Results.Ok(tiers);
        })
        .WithName("GetTiers");

        // Get seasons for a tier
        group.MapGet("/tiers/{tierId}/seasons", async (
            string tierId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var seasons = await seasonRepo.GetSeasonsByTierAsync(tierId);
            return Results.Ok(seasons);
        })
        .WithName("GetSeasons");

        // Get active season for a tier
        group.MapGet("/tiers/{tierId}/seasons/active", async (
            string tierId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var season = await seasonRepo.GetActiveSeasonByTierAsync(tierId);
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
