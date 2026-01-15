using Microsoft.AspNetCore.Mvc;
using Podium.Shared.Services.Data;
using Podium.Shared.Services.Business;
using Podium.Shared.Models;
using Podium.Shared.Services.Api;
using Podium.Api.Middleware;

namespace Podium.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin");

        // Get current user's admin status (no admin permission required - just authentication)
        group.MapGet("/me", async (
            HttpContext httpContext,
            [FromServices] IAdminRepository adminRepo) =>
        {
            var userId = httpContext.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var admin = await adminRepo.GetAdminAsync(userId);
            if (admin == null)
                return Results.Ok(new { isAdmin = false, canManageAdmins = false });
            
            return Results.Ok(new { isAdmin = admin.IsActive, canManageAdmins = admin.CanManageAdmins });
        })
        .RequireAuth() // Only requires authentication, not admin status
        .WithName("GetMyAdminStatus");

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

        // ===== DISCIPLINE MANAGEMENT =====
        
        // Get all disciplines (admin)
        group.MapGet("/disciplines", async (
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            var disciplines = await disciplineRepo.GetAllDisciplinesAsync();
            return Results.Ok(disciplines);
        })
        .RequireAdmin()
        .WithName("GetAllDisciplinesAdmin");

        // Get specific discipline (admin)
        group.MapGet("/disciplines/{disciplineId}", async (
            string disciplineId,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            var discipline = await disciplineRepo.GetDisciplineByIdAsync(disciplineId);
            if (discipline == null)
                return Results.NotFound(new { error = "Discipline not found" });
            
            return Results.Ok(discipline);
        })
        .RequireAdmin()
        .WithName("GetDisciplineAdmin");

        // Create discipline
        group.MapPost("/disciplines", async (
            [FromBody] CreateDisciplineRequest request,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            var discipline = new Discipline
            {
                Name = request.Name,
                DisplayName = request.DisplayName,
                IsActive = request.IsActive
            };

            var created = await disciplineRepo.CreateDisciplineAsync(discipline);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(created);
        })
        .RequireAdmin()
        .WithName("CreateDiscipline");

        // Update discipline
        group.MapPut("/disciplines/{disciplineId}", async (
            string disciplineId,
            [FromBody] UpdateDisciplineRequest request,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            var existing = await disciplineRepo.GetDisciplineByIdAsync(disciplineId);
            if (existing == null)
                return Results.NotFound(new { error = "Discipline not found" });

            existing.Name = request.Name;
            existing.DisplayName = request.DisplayName;
            existing.IsActive = request.IsActive;

            var updated = await disciplineRepo.UpdateDisciplineAsync(existing);
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(updated);
        })
        .RequireAdmin()
        .WithName("UpdateDiscipline");

        // Delete discipline
        group.MapDelete("/disciplines/{disciplineId}", async (
            string disciplineId,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            // Check for dependencies first
            var seriesCount = await disciplineRepo.GetSeriesCountByDisciplineAsync(disciplineId);
            if (seriesCount > 0)
            {
                return Results.BadRequest(new 
                { 
                    error = "Cannot delete discipline with existing series",
                    message = $"This discipline has {seriesCount} series associated with it. Please delete or reassign those series first.",
                    seriesCount = seriesCount,
                    canDelete = false
                });
            }

            var success = await disciplineRepo.DeleteDisciplineAsync(disciplineId);
            if (!success)
                return Results.NotFound(new { error = "Discipline not found" });

            return Results.Ok(new { message = "Discipline deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteDiscipline");

        // ===== SERIES MANAGEMENT =====

        // Get all series for a discipline (admin)
        group.MapGet("/disciplines/{disciplineId}/series", async (
            string disciplineId,
            [FromServices] ISeriesRepository seriesRepo) =>
        {
            var series = await seriesRepo.GetSeriesByDisciplineAsync(disciplineId);
            return Results.Ok(series);
        })
        .RequireAdmin()
        .WithName("GetSeriesByDisciplineAdmin");

        // Get specific series (admin)
        group.MapGet("/series/{seriesId}", async (
            string seriesId,
            string disciplineId,
            [FromServices] ISeriesRepository seriesRepo) =>
        {
            var series = await seriesRepo.GetSeriesByIdAsync(disciplineId, seriesId);
            if (series == null)
                return Results.NotFound(new { error = "Series not found" });
            
            return Results.Ok(series);
        })
        .RequireAdmin()
        .WithName("GetSeriesAdmin");

        // Create series
        group.MapPost("/series", async (
            [FromBody] CreateSeriesRequest request,
            [FromServices] ISeriesRepository seriesRepo,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            // Verify discipline exists
            var discipline = await disciplineRepo.GetDisciplineByIdAsync(request.DisciplineId);
            if (discipline == null)
                return Results.BadRequest(new { error = "Discipline not found" });

            var series = new Series
            {
                DisciplineId = request.DisciplineId,
                Name = request.Name,
                DisplayName = request.DisplayName,
                GoverningBody = request.GoverningBody ?? string.Empty,
                Region = request.Region ?? string.Empty,
                VehicleType = request.VehicleType ?? string.Empty,
                IsActive = request.IsActive
            };

            var created = await seriesRepo.CreateSeriesAsync(series);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(created);
        })
        .RequireAdmin()
        .WithName("CreateSeries");

        // Update series
        group.MapPut("/series/{seriesId}", async (
            string seriesId,
            [FromQuery] string currentDisciplineId,
            [FromBody] UpdateSeriesRequest request,
            [FromServices] ISeriesRepository seriesRepo,
            [FromServices] IDisciplineRepository disciplineRepo) =>
        {
            // Get existing series using CURRENT disciplineId (before any changes)
            var existing = await seriesRepo.GetSeriesByIdAsync(currentDisciplineId, seriesId);
            if (existing == null)
                return Results.NotFound(new { error = "Series not found" });

            // Verify NEW discipline exists (in case it's being changed)
            var discipline = await disciplineRepo.GetDisciplineByIdAsync(request.DisciplineId);
            if (discipline == null)
                return Results.BadRequest(new { error = "Target discipline not found" });

            existing.DisciplineId = request.DisciplineId;
            existing.Name = request.Name;
            existing.DisplayName = request.DisplayName;
            existing.GoverningBody = request.GoverningBody ?? string.Empty;
            existing.Region = request.Region ?? string.Empty;
            existing.VehicleType = request.VehicleType ?? string.Empty;
            existing.IsActive = request.IsActive;

            // Pass the old discipline ID if it changed
            var updated = await seriesRepo.UpdateSeriesAsync(existing, currentDisciplineId);
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(updated);
        })
        .RequireAdmin()
        .WithName("UpdateSeries");

        // Delete series
        group.MapDelete("/series/{seriesId}", async (
            string seriesId,
            [FromQuery] string disciplineId,
            [FromServices] ISeriesRepository seriesRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Check for dependencies first
            var seasonCount = await seasonRepo.GetSeasonCountBySeriesAsync(seriesId);
            if (seasonCount > 0)
            {
                return Results.BadRequest(new 
                { 
                    error = "Cannot delete series with existing seasons",
                    message = $"This series has {seasonCount} season(s) associated with it. Please delete those seasons first.",
                    seasonCount = seasonCount,
                    canDelete = false
                });
            }

            var success = await seriesRepo.DeleteSeriesAsync(disciplineId, seriesId);
            if (!success)
                return Results.NotFound(new { error = "Series not found" });

            return Results.Ok(new { message = "Series deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteSeries");

        // ===== SEASON MANAGEMENT =====

        // Get all seasons for a series (admin)
        group.MapGet("/series/{seriesId}/seasons", async (
            string seriesId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var seasons = await seasonRepo.GetSeasonsBySeriesAsync(seriesId);
            return Results.Ok(seasons);
        })
        .RequireAdmin()
        .WithName("GetSeasonsBySeriesAdmin");

        // Get specific season (admin)
        group.MapGet("/seasons/{seasonId}", async (
            string seasonId,
            [FromQuery] string seriesId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var season = await seasonRepo.GetSeasonByIdAsync(seriesId, seasonId);
            if (season == null)
                return Results.NotFound(new { error = "Season not found" });
            
            return Results.Ok(season);
        })
        .RequireAdmin()
        .WithName("GetSeasonAdmin");

        // Get season dependencies
        group.MapGet("/seasons/{seasonId}/dependencies", async (
            string seasonId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            var dependencies = await seasonRepo.GetSeasonDependenciesAsync(seasonId);
            return Results.Ok(dependencies);
        })
        .RequireAdmin()
        .WithName("GetSeasonDependencies");

        // Create season
        group.MapPost("/seasons", async (
            [FromBody] CreateSeasonRequest request,
            [FromServices] ISeasonRepository seasonRepo,
            [FromServices] ISeriesRepository seriesRepo) =>
        {
            // Verify series exists
            var series = await seriesRepo.GetSeriesByIdOnlyAsync(request.SeriesId);
            if (series == null)
                return Results.BadRequest(new { error = "Series not found" });

            // Validate dates
            if (request.EndDate.HasValue && request.StartDate >= request.EndDate.Value)
            {
                return Results.BadRequest(new { error = "Start date must be before end date" });
            }

            // Validate year matches date range
            if (request.StartDate.Year != request.Year)
            {
                return Results.BadRequest(new { error = "Year must match the start date year" });
            }

            var season = new Season
            {
                SeriesId = request.SeriesId,
                Year = request.Year,
                Name = request.Name,
                IsActive = request.IsActive,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var created = await seasonRepo.CreateSeasonAsync(season);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(created);
        })
        .RequireAdmin()
        .WithName("CreateSeason");

        // Update season
        group.MapPut("/seasons/{seasonId}", async (
            string seasonId,
            [FromQuery] string currentSeriesId,
            [FromBody] UpdateSeasonRequest request,
            [FromServices] ISeasonRepository seasonRepo,
            [FromServices] ISeriesRepository seriesRepo) =>
        {
            // Get existing season using CURRENT seriesId
            var existing = await seasonRepo.GetSeasonByIdAsync(currentSeriesId, seasonId);
            if (existing == null)
                return Results.NotFound(new { error = "Season not found" });

            // Verify NEW series exists (in case it's being changed)
            var series = await seriesRepo.GetSeriesByIdOnlyAsync(request.SeriesId);
            if (series == null)
                return Results.BadRequest(new { error = "Target series not found" });

            // Validate dates
            if (request.EndDate.HasValue && request.StartDate >= request.EndDate.Value)
            {
                return Results.BadRequest(new { error = "Start date must be before end date" });
            }

            // Validate year matches date range
            if (request.StartDate.Year != request.Year)
            {
                return Results.BadRequest(new { error = "Year must match the start date year" });
            }

            // IMPORTANT: If moving to a different series AND the season is active,
            // deactivate it to prevent duplicate active seasons in the new series
            bool seriesChanged = currentSeriesId != request.SeriesId;
            bool wasActive = existing.IsActive;
            
            if (seriesChanged && wasActive)
            {
                // Deactivate the season when moving to prevent duplicate active seasons
                existing.IsActive = false;
            }
            else
            {
                existing.IsActive = request.IsActive;
            }

            existing.SeriesId = request.SeriesId;
            existing.Year = request.Year;
            existing.Name = request.Name;
            existing.StartDate = request.StartDate;
            existing.EndDate = request.EndDate;

            var updated = await seasonRepo.UpdateSeasonAsync(existing, currentSeriesId);
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(updated);
        })
        .RequireAdmin()
        .WithName("UpdateSeason");

        // Delete season
        group.MapDelete("/seasons/{seasonId}", async (
            string seasonId,
            [FromQuery] string seriesId,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Check for dependencies first
            var dependencies = await seasonRepo.GetSeasonDependenciesAsync(seasonId);
            if (dependencies.HasDependencies)
            {
                var reasons = new List<string>();
                if (dependencies.EventCount > 0)
                    reasons.Add($"{dependencies.EventCount} event(s)");
                if (dependencies.CompetitorCount > 0)
                    reasons.Add($"{dependencies.CompetitorCount} competitor(s)");

                return Results.BadRequest(new 
                { 
                    error = "Cannot delete season with existing data",
                    message = $"This season has {string.Join(" and ", reasons)}. Please delete those first.",
                    dependencies = dependencies,
                    canDelete = false
                });
            }

            var success = await seasonRepo.DeleteSeasonAsync(seriesId, seasonId);
            if (!success)
                return Results.NotFound(new { error = "Season not found" });

            return Results.Ok(new { message = "Season deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteSeason");

        // ===== COMPETITOR MANAGEMENT =====

        // Get all competitors (admin)
        group.MapGet("/competitors", async (
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var competitors = await competitorRepo.GetAllCompetitorsAsync();
            return Results.Ok(competitors);
        })
        .RequireAdmin()
        .WithName("GetAllCompetitorsAdmin");

        // Get competitors by type (admin)
        group.MapGet("/competitors/type/{type}", async (
            string type,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var competitors = await competitorRepo.GetCompetitorsByTypeAsync(type);
            return Results.Ok(competitors);
        })
        .RequireAdmin()
        .WithName("GetCompetitorsByType");

        // Get specific competitor (admin)
        group.MapGet("/competitors/{competitorId}", async (
            string competitorId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var competitor = await competitorRepo.GetCompetitorByIdOnlyAsync(competitorId);
            if (competitor == null)
                return Results.NotFound(new { error = "Competitor not found" });
            
            return Results.Ok(competitor);
        })
        .RequireAdmin()
        .WithName("GetCompetitorAdmin");

        // Get competitor dependencies
        group.MapGet("/competitors/{competitorId}/dependencies", async (
            string competitorId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var dependencies = await competitorRepo.GetCompetitorDependenciesAsync(competitorId);
            return Results.Ok(dependencies);
        })
        .RequireAdmin()
        .WithName("GetCompetitorDependencies");

        // Get competitor's assigned seasons
        group.MapGet("/competitors/{competitorId}/seasons", async (
            string competitorId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var seasonIds = await competitorRepo.GetCompetitorSeasonIdsAsync(competitorId);
            return Results.Ok(seasonIds);
        })
        .RequireAdmin()
        .WithName("GetCompetitorSeasons");

        // Create competitor
        group.MapPost("/competitors", async (
            [FromBody] CreateCompetitorRequest request,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            // Validate type
            if (request.Type != "Individual" && request.Type != "Team")
            {
                return Results.BadRequest(new { error = "Type must be 'Individual' or 'Team'" });
            }

            var competitor = new Competitor
            {
                Name = request.Name,
                ShortName = request.ShortName,
                Type = request.Type,
                IsActive = request.IsActive
            };

            var created = await competitorRepo.CreateCompetitorAsync(competitor);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(created);
        })
        .RequireAdmin()
        .WithName("CreateCompetitor");

        // Update competitor
        group.MapPut("/competitors/{competitorId}", async (
            string competitorId,
            [FromBody] UpdateCompetitorRequest request,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            // Get existing competitor
            var existing = await competitorRepo.GetCompetitorByIdOnlyAsync(competitorId);
            if (existing == null)
                return Results.NotFound(new { error = "Competitor not found" });

            // Validate type
            if (request.Type != "Individual" && request.Type != "Team")
            {
                return Results.BadRequest(new { error = "Type must be 'Individual' or 'Team'" });
            }

            existing.Name = request.Name;
            existing.ShortName = request.ShortName;
            existing.Type = request.Type;
            existing.IsActive = request.IsActive;

            var updated = await competitorRepo.UpdateCompetitorAsync(existing);
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(updated);
        })
        .RequireAdmin()
        .WithName("UpdateCompetitor");

        // Delete competitor
        group.MapDelete("/competitors/{competitorId}", async (
            string competitorId,
            [FromQuery] string type,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            // Check for dependencies first
            var dependencies = await competitorRepo.GetCompetitorDependenciesAsync(competitorId);
            if (dependencies.HasDependencies)
            {
                var reasons = new List<string>();
                if (dependencies.SeasonCount > 0)
                    reasons.Add($"{dependencies.SeasonCount} season(s)");
                if (dependencies.ResultCount > 0)
                    reasons.Add($"{dependencies.ResultCount} event result(s)");

                return Results.BadRequest(new 
                { 
                    error = "Cannot delete competitor with existing data",
                    message = $"This competitor is assigned to {string.Join(" and ", reasons)}. Please remove those assignments first.",
                    dependencies = dependencies,
                    canDelete = false
                });
            }

            var success = await competitorRepo.DeleteCompetitorAsync(type, competitorId);
            if (!success)
                return Results.NotFound(new { error = "Competitor not found" });

            return Results.Ok(new { message = "Competitor deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteCompetitor");

        // Add competitor to season
        group.MapPost("/seasons/{seasonId}/competitors/{competitorId}", async (
            string seasonId,
            string competitorId,
            [FromServices] ICompetitorRepository competitorRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Verify season exists (use cross-partition query)
            var season = await seasonRepo.GetSeasonByIdOnlyAsync(seasonId);
            if (season == null)
                return Results.BadRequest(new { error = "Season not found" });

            // Verify competitor exists
            var competitor = await competitorRepo.GetCompetitorByIdOnlyAsync(competitorId);
            if (competitor == null)
                return Results.BadRequest(new { error = "Competitor not found" });

            var success = await competitorRepo.AddCompetitorToSeasonAsync(seasonId, competitorId, competitor.Name);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "Competitor added to season successfully" });
        })
        .RequireAdmin()
        .WithName("AddCompetitorToSeason");

        // Remove competitor from season
        group.MapDelete("/seasons/{seasonId}/competitors/{competitorId}", async (
            string seasonId,
            string competitorId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var success = await competitorRepo.RemoveCompetitorFromSeasonAsync(seasonId, competitorId);
            if (!success)
                return Results.NotFound(new { error = "Assignment not found" });

            return Results.Ok(new { message = "Competitor removed from season successfully" });
        })
        .RequireAdmin()
        .WithName("RemoveCompetitorFromSeason");

        // Get competitors for a season
        group.MapGet("/seasons/{seasonId}/competitors", async (
            string seasonId,
            [FromServices] ICompetitorRepository competitorRepo) =>
        {
            var competitors = await competitorRepo.GetCompetitorsBySeasonAsync(seasonId);
            return Results.Ok(competitors);
        })
        .RequireAdmin()
        .WithName("GetSeasonCompetitorsAdmin");

        // ===== EVENT MANAGEMENT =====

        // Get all events (admin)
        group.MapGet("/events", async (
            [FromServices] IEventRepository eventRepo) =>
        {
            var events = await eventRepo.GetAllEventsAsync();
            return Results.Ok(events);
        })
        .RequireAdmin()
        .WithName("GetAllEventsAdmin");

        // Get events by season
        group.MapGet("/seasons/{seasonId}/events", async (
            string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var events = await eventRepo.GetEventsBySeasonAsync(seasonId);
            return Results.Ok(events);
        })
        .RequireAdmin()
        .WithName("GetEventsBySeasonAdmin");

        // Get specific event
        group.MapGet("/events/{eventId}", async (
            string eventId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var evt = await eventRepo.GetEventByIdOnlyAsync(eventId);
            if (evt == null)
                return Results.NotFound(new { error = "Event not found" });
            
            return Results.Ok(evt);
        })
        .RequireAdmin()
        .WithName("GetEventAdmin");

        // Get event dependencies
        group.MapGet("/events/{eventId}/dependencies", async (
            string eventId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var dependencies = await eventRepo.GetEventDependenciesAsync(eventId);
            return Results.Ok(dependencies);
        })
        .RequireAdmin()
        .WithName("GetEventDependencies");

        // Get next event number for season
        group.MapGet("/seasons/{seasonId}/events/next-number", async (
            string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var nextNumber = await eventRepo.GetNextEventNumberAsync(seasonId);
            return Results.Ok(new { nextNumber });
        })
        .RequireAdmin()
        .WithName("GetNextEventNumber");

        // Create event
        group.MapPost("/events", async (
            [FromBody] CreateEventRequest request,
            [FromServices] IEventRepository eventRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Verify season exists
            var season = await seasonRepo.GetSeasonByIdOnlyAsync(request.SeasonId);
            if (season == null)
                return Results.BadRequest(new { error = "Season not found" });

            // Validate status
            var validStatuses = new[] { "Upcoming", "InProgress", "Completed" };
            if (!validStatuses.Contains(request.Status))
            {
                return Results.BadRequest(new { error = "Status must be 'Upcoming', 'InProgress', or 'Completed'" });
            }

            // Validate event date within season range
            if (request.EventDate < season.StartDate)
            {
                return Results.BadRequest(new { error = "Event date must be within season date range" });
            }

            if (season.EndDate.HasValue && request.EventDate > season.EndDate.Value)
            {
                return Results.BadRequest(new { error = "Event date must be within season date range" });
            }

            var evt = new Event
            {
                SeasonId = request.SeasonId,
                Name = request.Name,
                DisplayName = request.DisplayName,
                EventNumber = request.EventNumber,
                EventDate = request.EventDate,
                Location = request.Location,
                Status = request.Status,
                IsActive = request.IsActive
            };

            var created = await eventRepo.CreateEventAsync(evt);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(created);
        })
        .RequireAdmin()
        .WithName("CreateEvent");

        // Update event
        group.MapPut("/events/{eventId}", async (
            string eventId,
            [FromBody] UpdateEventRequest request,
            [FromServices] IEventRepository eventRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Get existing event
            var existing = await eventRepo.GetEventByIdOnlyAsync(eventId);
            if (existing == null)
                return Results.NotFound(new { error = "Event not found" });

            // Verify season exists (in case it's being changed)
            var season = await seasonRepo.GetSeasonByIdOnlyAsync(existing.SeasonId);
            if (season == null)
                return Results.BadRequest(new { error = "Season not found" });

            // Validate status
            var validStatuses = new[] { "Upcoming", "InProgress", "Completed" };
            if (!validStatuses.Contains(request.Status))
            {
                return Results.BadRequest(new { error = "Status must be 'Upcoming', 'InProgress', or 'Completed'" });
            }

            // Validate event date within season range
            if (request.EventDate < season.StartDate)
            {
                return Results.BadRequest(new { error = "Event date must be within season date range" });
            }

            if (season.EndDate.HasValue && request.EventDate > season.EndDate.Value)
            {
                return Results.BadRequest(new { error = "Event date must be within season date range" });
            }

            existing.Name = request.Name;
            existing.DisplayName = request.DisplayName;
            existing.EventNumber = request.EventNumber;
            existing.EventDate = request.EventDate;
            existing.Location = request.Location;
            existing.Status = request.Status;
            existing.IsActive = request.IsActive;

            var updated = await eventRepo.UpdateEventAsync(existing);
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(updated);
        })
        .RequireAdmin()
        .WithName("UpdateEvent");

        // Delete event
        group.MapDelete("/events/{eventId}", async (
            string eventId,
            [FromQuery] string seasonId,
            [FromServices] IEventRepository eventRepo) =>
        {
            // Check for dependencies first
            var dependencies = await eventRepo.GetEventDependenciesAsync(eventId);
            if (dependencies.HasDependencies)
            {
                var reasons = new List<string>();
                if (dependencies.PredictionCount > 0)
                    reasons.Add($"{dependencies.PredictionCount} prediction(s)");
                if (dependencies.HasResult)
                    reasons.Add("event result");

                return Results.BadRequest(new 
                { 
                    error = "Cannot delete event with existing data",
                    message = $"This event has {string.Join(" and ", reasons)}. Please delete those first.",
                    dependencies = dependencies,
                    canDelete = false
                });
            }

            var success = await eventRepo.DeleteEventAsync(seasonId, eventId);
            if (!success)
                return Results.NotFound(new { error = "Event not found" });

            return Results.Ok(new { message = "Event deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteEvent");

        // ===== EVENT RESULT MANAGEMENT =====

        // Get event result
        group.MapGet("/events/{eventId}/result", async (
            string eventId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var result = await eventRepo.GetEventResultAsync(eventId);
            // Return 200 with null if no result exists (event hasn't happened yet)
            // This is NOT an error - it's a normal state for future events
            return Results.Ok(result);
        })
        .RequireAdmin()
        .WithName("GetEventResultAdmin");

        // Create or update event result
        group.MapPost("/events/{eventId}/result", async (
            string eventId,
            [FromBody] CreateEventResultRequest request,
            [FromServices] IEventRepository eventRepo,
            [FromServices] ICompetitorRepository competitorRepo,
            [FromServices] IScoringService scoringService) =>
        {
            // Verify event exists
            var evt = await eventRepo.GetEventByIdOnlyAsync(eventId);
            if (evt == null)
                return Results.BadRequest(new { error = "Event not found" });

            // Validate event status - should be Completed
            if (evt.Status != "Completed")
            {
                return Results.BadRequest(new { error = "Results can only be entered for completed events. Change event status to 'Completed' first." });
            }

            // Verify all three competitors exist
            var first = await competitorRepo.GetCompetitorByIdOnlyAsync(request.FirstPlaceId);
            if (first == null)
                return Results.BadRequest(new { error = "First place competitor not found" });

            var second = await competitorRepo.GetCompetitorByIdOnlyAsync(request.SecondPlaceId);
            if (second == null)
                return Results.BadRequest(new { error = "Second place competitor not found" });

            var third = await competitorRepo.GetCompetitorByIdOnlyAsync(request.ThirdPlaceId);
            if (third == null)
                return Results.BadRequest(new { error = "Third place competitor not found" });

            // Ensure all three competitors are different
            if (request.FirstPlaceId == request.SecondPlaceId || 
                request.FirstPlaceId == request.ThirdPlaceId || 
                request.SecondPlaceId == request.ThirdPlaceId)
            {
                return Results.BadRequest(new { error = "All three podium positions must be different competitors" });
            }

            var result = new EventResult
            {
                EventId = eventId,
                FirstPlaceId = request.FirstPlaceId,
                FirstPlaceName = request.FirstPlaceName,
                SecondPlaceId = request.SecondPlaceId,
                SecondPlaceName = request.SecondPlaceName,
                ThirdPlaceId = request.ThirdPlaceId,
                ThirdPlaceName = request.ThirdPlaceName
            };

            var created = await eventRepo.CreateOrUpdateEventResultAsync(eventId, result);
            if (created == null)
                return Results.StatusCode(500);

            // Automatically recalculate points for all predictions for this event
            // Get the event's seasonId from the evt object we already have
            var recalculationSuccess = await scoringService.RecalculateEventPredictionsAsync(eventId, evt.SeasonId);

            return Results.Ok(new 
            { 
                result = created,
                pointsRecalculated = recalculationSuccess,
                message = recalculationSuccess 
                    ? "Event result saved and all prediction points recalculated successfully" 
                    : "Event result saved, but some predictions may not have been recalculated"
            });
        })
        .RequireAdmin()
        .WithName("CreateOrUpdateEventResult");

        // Delete event result
        group.MapDelete("/events/{eventId}/result", async (
            string eventId,
            [FromServices] IEventRepository eventRepo) =>
        {
            var success = await eventRepo.DeleteEventResultAsync(eventId);
            if (!success)
                return Results.NotFound(new { error = "Result not found" });

            return Results.Ok(new { message = "Result deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteEventResult");

        // ===== PREDICTION MANAGEMENT =====

        // Update prediction points (for recalculation after result changes)
        group.MapPut("/predictions/{eventId}/{userId}/points", async (
            string eventId,
            string userId,
            [FromBody] UpdatePointsRequest request,
            [FromServices] IPredictionRepository predictionRepo) =>
        {
            var prediction = await predictionRepo.GetPredictionAsync(eventId, userId);
            if (prediction == null)
                return Results.NotFound(new { error = "Prediction not found" });

            prediction.PointsEarned = request.Points;
            var updated = await predictionRepo.UpdatePredictionAsync(prediction);
            
            if (updated == null)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "Points updated successfully" });
        })
        .RequireAdmin()
        .WithName("UpdatePredictionPoints");

        // ===== USER MANAGEMENT =====

        // Get all users (admin)
        group.MapGet("/users", async (
            [FromServices] IUserRepository userRepo) =>
        {
            var users = await userRepo.GetAllUsersAsync();
            // Don't send password hash/salt to frontend
            var safeUsers = users.Select(u => new 
            {
                u.UserId,
                u.Email,
                u.Username,
                u.PreferredAuthMethod,
                u.IsActive,
                u.CreatedDate,
                u.LastLoginDate
            }).ToList();
            return Results.Ok(safeUsers);
        })
        .RequireAdmin()
        .WithName("GetAllUsers");

        // Search users by username or email
        group.MapGet("/users/search", async (
            [FromQuery] string q,
            [FromServices] IUserRepository userRepo) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Ok(new List<object>());

            var users = await userRepo.SearchUsersAsync(q);
            // Don't send password hash/salt to frontend
            var safeUsers = users.Select(u => new 
            {
                u.UserId,
                u.Email,
                u.Username,
                u.IsActive
            }).ToList();
            return Results.Ok(safeUsers);
        })
        .RequireAdmin()
        .WithName("SearchUsers");

        // Get specific user (admin)
        group.MapGet("/users/{userId}", async (
            string userId,
            [FromServices] IUserRepository userRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });
            
            // Don't send password hash/salt to frontend
            var safeUser = new 
            {
                user.UserId,
                user.Email,
                user.Username,
                user.PreferredAuthMethod,
                user.IsActive,
                user.CreatedDate,
                user.LastLoginDate
            };
            return Results.Ok(safeUser);
        })
        .RequireAdmin()
        .WithName("GetUserAdmin");

        // Check user dependencies before deletion
        group.MapGet("/users/{userId}/dependencies", async (
            string userId,
            [FromServices] IUserRepository userRepo) =>
        {
            var dependencies = await userRepo.GetUserDependenciesAsync(userId);
            return Results.Ok(dependencies);
        })
        .RequireAdmin()
        .WithName("GetUserDependencies");

        // Update user
        group.MapPut("/users/{userId}", async (
            string userId,
            [FromBody] UpdateUserRequest request,
            [FromServices] IUserRepository userRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            // Update allowed fields
            user.Username = request.Username;
            user.Email = request.Email;
            user.IsActive = request.IsActive;

            var success = await userRepo.UpdateUserAsync(user);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "User updated successfully" });
        })
        .RequireAdmin()
        .WithName("UpdateUserAdmin");

        // Delete user (with dependency check)
        group.MapDelete("/users/{userId}", async (
            string userId,
            [FromServices] IUserRepository userRepo) =>
        {
            var user = await userRepo.GetUserByIdAsync(userId);
            if (user == null)
                return Results.NotFound(new { error = "User not found" });

            // Check for dependencies
            var dependencies = await userRepo.GetUserDependenciesAsync(userId);
            if (dependencies.HasDependencies)
            {
                var reasons = new List<string>();
                if (dependencies.PredictionCount > 0)
                    reasons.Add($"{dependencies.PredictionCount} prediction(s)");
                if (dependencies.IsAdmin)
                    reasons.Add("admin privileges");

                return Results.BadRequest(new 
                { 
                    error = "Cannot delete user with existing data",
                    message = $"This user has {string.Join(" and ", reasons)}. Please deactivate the user instead of deleting.",
                    dependencies = dependencies,
                    canDelete = false
                });
            }

            var success = await userRepo.DeleteUserAsync(userId);
            if (!success)
                return Results.StatusCode(500);

            return Results.Ok(new { message = "User deleted successfully" });
        })
        .RequireAdmin()
        .WithName("DeleteUserAdmin");

        // ===== SCORING RULES MANAGEMENT =====

        // Get scoring rules for a season
        group.MapGet("/seasons/{seasonId}/scoring-rules", async (
            string seasonId,
            [FromServices] IScoringRulesRepository scoringRulesRepo) =>
        {
            var scoringRules = await scoringRulesRepo.GetScoringRulesBySeasonAsync(seasonId);
            if (scoringRules == null)
            {
                // Return default values if not configured
                return Results.Ok(new 
                { 
                    seasonId = seasonId,
                    exactMatchPoints = 25,
                    oneOffPoints = 18,
                    twoOffPoints = 15,
                    isDefault = true,
                    message = "No custom scoring rules configured. Showing default values."
                });
            }
            
            return Results.Ok(scoringRules);
        })
        .RequireAdmin()
        .WithName("GetScoringRules");

        // Create or update scoring rules for a season
        group.MapPost("/seasons/{seasonId}/scoring-rules", async (
            string seasonId,
            [FromBody] CreateOrUpdateScoringRulesRequest request,
            [FromServices] IScoringRulesRepository scoringRulesRepo,
            [FromServices] ISeasonRepository seasonRepo) =>
        {
            // Verify season exists
            var season = await seasonRepo.GetSeasonByIdOnlyAsync(seasonId);
            if (season == null)
                return Results.BadRequest(new { error = "Season not found" });

            // Validate points are positive
            if (request.ExactMatchPoints < 0 || request.OneOffPoints < 0 || request.TwoOffPoints < 0)
            {
                return Results.BadRequest(new { error = "Points values must be non-negative" });
            }

            // Validate logical order (exact match should be highest)
            if (request.ExactMatchPoints < request.OneOffPoints || request.ExactMatchPoints < request.TwoOffPoints)
            {
                return Results.BadRequest(new { error = "ExactMatchPoints should be the highest value" });
            }

            var scoringRules = new ScoringRules
            {
                SeasonId = seasonId,
                ExactMatchPoints = request.ExactMatchPoints,
                OneOffPoints = request.OneOffPoints,
                TwoOffPoints = request.TwoOffPoints
            };

            var created = await scoringRulesRepo.CreateOrUpdateScoringRulesAsync(scoringRules);
            if (created == null)
                return Results.StatusCode(500);

            return Results.Ok(new 
            { 
                message = "Scoring rules saved successfully",
                scoringRules = created 
            });
        })
        .RequireAdmin()
        .WithName("CreateOrUpdateScoringRules");

        // Delete scoring rules for a season (will revert to defaults)
        group.MapDelete("/seasons/{seasonId}/scoring-rules", async (
            string seasonId,
            [FromServices] IScoringRulesRepository scoringRulesRepo) =>
        {
            var success = await scoringRulesRepo.DeleteScoringRulesAsync(seasonId);
            if (!success)
                return Results.NotFound(new { error = "Scoring rules not found" });

            return Results.Ok(new { message = "Scoring rules deleted. Season will use default scoring rules." });
        })
        .RequireAdmin()
        .WithName("DeleteScoringRules");
    }
}

// Request DTOs
public record CreateAdminRequest(string UserId, bool IsActive, bool CanManageAdmins);
public record UpdateAdminRequest(bool IsActive, bool CanManageAdmins);
public record CreateDisciplineRequest(string Name, string DisplayName, bool IsActive);
public record UpdateDisciplineRequest(string Name, string DisplayName, bool IsActive);
public record UpdateUserRequest(string Username, string Email, bool IsActive);
public record CreateOrUpdateScoringRulesRequest(int ExactMatchPoints, int OneOffPoints, int TwoOffPoints);
