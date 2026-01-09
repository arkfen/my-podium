using Podium.Migration.Models;

namespace Podium.Migration.Services;

/// <summary>
/// Main orchestrator for the migration process
/// </summary>
public class MigrationOrchestrator
{
    private readonly LegacyDataExtractor _extractor;
    private readonly DataTransformer _transformer;
    private readonly PodiumDataInserter _inserter;
    private readonly MigrationResult _result;
    private readonly bool _season2024;
    private readonly bool _season2025;

    public MigrationOrchestrator(
        LegacyDataExtractor extractor,
        DataTransformer transformer,
        PodiumDataInserter inserter,
        bool season2024,
        bool season2025)
    {
        _extractor = extractor;
        _transformer = transformer;
        _inserter = inserter;
        _result = new MigrationResult();
        _season2024 = season2024;
        _season2025 = season2025;
    }

    public async Task<MigrationResult> ExecuteMigrationAsync()
    {
        Console.WriteLine("\n========================================");
        Console.WriteLine("Starting F1 Historical Data Migration");
        Console.WriteLine("========================================\n");

        try
        {
            // Step 1: Create base structure (Discipline, Series)
            await CreateBaseStructureAsync();

            // Step 2: Extract and process each season
            var yearsToMigrate = new List<int>();
            if (_season2024) yearsToMigrate.Add(2024);
            if (_season2025) yearsToMigrate.Add(2025);

            if (!yearsToMigrate.Any())
            {
                _result.AddWarning("No seasons selected for migration");
                return _result;
            }

            // Step 3: Extract users
            var users = await _extractor.ExtractUsersAsync();
            await MigrateUsersAsync(users);

            // Step 4: Extract drivers from all predictions
            var driverNames = await _extractor.ExtractDriverNamesAsync(yearsToMigrate);
            await MigrateDriversAsync(driverNames);

            // Step 5: Process each season
            foreach (var year in yearsToMigrate)
            {
                await MigrateSeasonAsync(year, users);
            }

            Console.WriteLine("\n? Migration completed!");
            _transformer.PrintMappingSummary();
        }
        catch (Exception ex)
        {
            _result.AddError($"Critical error: {ex.Message}");
            Console.WriteLine($"\n? Migration failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return _result;
    }

    private async Task CreateBaseStructureAsync()
    {
        Console.WriteLine("\n--- Creating Base Structure ---");
        
        // Create discipline: Single-Seater Racing
        await _inserter.InsertDisciplineAsync(_transformer.DisciplineId, "Single-Seater Racing");
        
        // Create series: Formula 1
        await _inserter.InsertSeriesAsync(
            _transformer.DisciplineId,
            _transformer.SeriesId,
            "Formula 1",
            "FIA",
            "Global",
            "Open-wheel"
        );
    }

    private async Task MigrateUsersAsync(List<LegacyUser> users)
    {
        Console.WriteLine("\n--- Migrating Users ---");
        
        // Filter out admin users (we're not migrating admins)
        var regularUsers = users.Where(u => 
            !string.IsNullOrWhiteSpace(u.Email) && 
            !u.Email.Contains("admin", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        foreach (var user in regularUsers)
        {
            try
            {
                var userId = user.UsrId ?? user.Id;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    _result.AddWarning($"Skipping user with no ID: {user.Name}");
                    continue;
                }

                var newUserId = _transformer.GetOrCreateUserId(userId);
                await _inserter.InsertUserAsync(newUserId, user.Email, user.Name);
                _result.UsersCreated++;
            }
            catch (Exception ex)
            {
                _result.AddError($"Failed to migrate user {user.Name}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"Migrated {_result.UsersCreated} users");
    }

    private async Task MigrateDriversAsync(HashSet<string> driverNames)
    {
        Console.WriteLine("\n--- Migrating Drivers ---");
        
        foreach (var driverName in driverNames.OrderBy(n => n))
        {
            try
            {
                var driverId = _transformer.GetOrCreateDriverId(driverName);
                var shortName = _transformer.GenerateShortName(driverName);
                
                await _inserter.InsertCompetitorAsync(
                    _transformer.DisciplineId,
                    driverId,
                    driverName,
                    shortName
                );
                
                _result.DriversCreated++;
            }
            catch (Exception ex)
            {
                _result.AddError($"Failed to migrate driver {driverName}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"Migrated {_result.DriversCreated} drivers");
    }

    private async Task MigrateSeasonAsync(int year, List<LegacyUser> users)
    {
        Console.WriteLine($"\n--- Migrating Season {year} ---");
        
        try
        {
            // Create season
            var seasonId = _transformer.GetOrCreateSeasonId(year);
            await _inserter.InsertSeasonAsync(
                _transformer.SeriesId,
                seasonId,
                year,
                new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc)
            );
            _result.SeasonsCreated++;

            // Create scoring rules
            await _inserter.InsertScoringRulesAsync(seasonId);

            // Link all drivers to this season
            var predictions = await _extractor.ExtractPredictionsAsync(year);
            var driversInSeason = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var pred in predictions)
            {
                if (!string.IsNullOrWhiteSpace(pred.P1)) driversInSeason.Add(pred.P1.Trim());
                if (!string.IsNullOrWhiteSpace(pred.P2)) driversInSeason.Add(pred.P2.Trim());
                if (!string.IsNullOrWhiteSpace(pred.P3)) driversInSeason.Add(pred.P3.Trim());
            }

            foreach (var driverName in driversInSeason)
            {
                var driverId = _transformer.GetDriverId(driverName);
                if (!string.IsNullOrEmpty(driverId))
                {
                    await _inserter.InsertSeasonCompetitorAsync(seasonId, driverId, driverName);
                }
            }

            // Extract races
            var races = await _extractor.ExtractRacesAsync(year);
            
            // Migrate events
            await MigrateEventsAsync(seasonId, year, races, predictions);
            
            // Calculate and insert user statistics
            await CalculateUserStatisticsAsync(seasonId, year, predictions, users);
        }
        catch (Exception ex)
        {
            _result.AddError($"Failed to migrate season {year}: {ex.Message}");
        }
    }

    private async Task MigrateEventsAsync(string seasonId, int year, 
        List<LegacyRace> races, List<LegacyPrediction> predictions)
    {
        Console.WriteLine($"Migrating {races.Count} events for {year}...");
        
        foreach (var race in races)
        {
            try
            {
                var eventId = _transformer.GetOrCreateEventId(year, race.NumberRace);
                var status = _transformer.DetermineEventStatus(race.Date);
                
                await _inserter.InsertEventAsync(
                    seasonId,
                    eventId,
                    race.Name,
                    race.NumberRace,
                    race.Date ?? DateTime.UtcNow,
                    race.Location ?? "Unknown",
                    status
                );
                _result.EventsCreated++;

                // Find the actual results from predictions (the ones with points)
                var racePredictions = predictions
                    .Where(p => p.Race == race.NumberRace)
                    .ToList();

                // Determine actual results from the most common podium in scored predictions
                var actualResult = DetermineActualResults(racePredictions);
                
                if (actualResult != null)
                {
                    var (p1, p2, p3) = actualResult.Value;
                    await _inserter.InsertEventResultAsync(
                        eventId,
                        _transformer.GetDriverId(p1), p1,
                        _transformer.GetDriverId(p2), p2,
                        _transformer.GetDriverId(p3), p3
                    );
                    _result.EventResultsCreated++;
                }

                // Migrate predictions for this race
                foreach (var pred in racePredictions)
                {
                    try
                    {
                        var userId = _transformer.GetOrCreateUserId(pred.UserId);
                        
                        await _inserter.InsertPredictionAsync(
                            eventId,
                            userId,
                            _transformer.GetDriverId(pred.P1), pred.P1,
                            _transformer.GetDriverId(pred.P2), pred.P2,
                            _transformer.GetDriverId(pred.P3), pred.P3,
                            pred.Points,
                            pred.SubmittedDate ?? DateTime.UtcNow
                        );
                        _result.PredictionsMigrated++;
                    }
                    catch (Exception ex)
                    {
                        _result.AddWarning($"Failed to migrate prediction for race {race.NumberRace}, user {pred.UserId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _result.AddError($"Failed to migrate event {race.Name}: {ex.Message}");
            }
        }
    }

    private (string P1, string P2, string P3)? DetermineActualResults(List<LegacyPrediction> predictions)
    {
        // Find predictions that have exact match points (25) - these likely reflect actual results
        var exactMatches = predictions.Where(p => p.Points == 25).ToList();
        
        if (exactMatches.Any())
        {
            // Return the most common exact match
            var result = exactMatches.First();
            return (result.P1, result.P2, result.P3);
        }

        // If no exact matches, try to find the most common podium from scored predictions
        var scoredPredictions = predictions.Where(p => p.Points.HasValue && p.Points.Value > 0).ToList();
        
        if (scoredPredictions.Any())
        {
            // Use the first scored prediction as best guess
            var result = scoredPredictions.First();
            return (result.P1, result.P2, result.P3);
        }

        return null; // No results available yet
    }

    private async Task CalculateUserStatisticsAsync(string seasonId, int year, 
        List<LegacyPrediction> predictions, List<LegacyUser> users)
    {
        Console.WriteLine("Calculating user statistics...");
        
        var userStats = new Dictionary<string, (int total, int count, int exact, int oneOff, int twoOff)>();
        
        foreach (var pred in predictions.Where(p => p.Points.HasValue))
        {
            var userId = _transformer.GetOrCreateUserId(pred.UserId);
            
            if (!userStats.ContainsKey(userId))
            {
                userStats[userId] = (0, 0, 0, 0, 0);
            }

            var (total, count, exact, oneOff, twoOff) = userStats[userId];
            total += pred.Points!.Value; // Non-null assertion since we filtered by HasValue
            count++;
            
            if (pred.Points.Value == 25) exact++;
            else if (pred.Points.Value == 18) oneOff++;
            else if (pred.Points.Value == 15) twoOff++;
            
            userStats[userId] = (total, count, exact, oneOff, twoOff);
        }

        foreach (var kvp in userStats)
        {
            var userId = kvp.Key;
            var (total, count, exact, oneOff, twoOff) = kvp.Value;
            
            // Find username
            var legacyUser = users.FirstOrDefault(u => 
                _transformer.GetOrCreateUserId(u.UsrId ?? u.Id) == userId);
            var username = legacyUser?.Name ?? "Unknown";
            
            await _inserter.UpsertUserStatisticsAsync(
                seasonId, userId, username, total, count, exact, oneOff, twoOff);
            _result.UserStatisticsCreated++;
        }
    }
}
