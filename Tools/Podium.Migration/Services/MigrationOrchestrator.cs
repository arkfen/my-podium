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

            // Step 4: Extract drivers from MyPodiumDrivers table ONLY
            Console.WriteLine("\n--- Extracting Drivers from MyPodiumDrivers ---");
            var legacyDrivers = await _extractor.ExtractDriversAsync();
            
            // Create drivers ONLY from MyPodiumDrivers - no other sources!
            await MigrateDriversAsync(legacyDrivers.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
            
            Console.WriteLine($"? Migrated {_result.DriversCreated} drivers from MyPodiumDrivers");

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

            // Extract races FIRST
            var races = await _extractor.ExtractRacesAsync(year);
            Console.WriteLine($"  Found {races.Count} races to process");
            
            if (races.Count == 0)
            {
                _result.AddWarning($"No races found for {year}. Skipping predictions and stats.");
                return;
            }

            // Extract race results from MyPodiumResults table
            var raceResults = await _extractor.ExtractRaceResultsAsync(year);
            Console.WriteLine($"  Found {raceResults.Count} race results");

            // Extract predictions for this season
            var predictions = await _extractor.ExtractPredictionsAsync(year);
            Console.WriteLine($"  Found {predictions.Count} predictions to process");

            // Link all official F1 drivers who raced in this season
            Console.WriteLine($"  Linking F1 drivers to {year} season...");
            var seasonDrivers = Data.F1DriverLineups.GetDriversForSeason(year).ToList();
            int linkedCount = 0;

            foreach (var (driverName, joinDate) in seasonDrivers)
            {
                // Try to find driver by case-insensitive name match
                var driverId = _transformer.GetDriverId(driverName);
                
                // If driver doesn't exist, skip with warning (should exist from MyPodiumDrivers)
                if (string.IsNullOrEmpty(driverId))
                {
                    Console.WriteLine($"    ? Warning: Driver '{driverName}' not found in MyPodiumDrivers, skipping season link");
                    continue;
                }
                
                await _inserter.InsertSeasonCompetitorAsync(seasonId, driverId, driverName, joinDate);
                linkedCount++;
            }

            Console.WriteLine($"  ? Linked {linkedCount} official F1 drivers to {year} season");

            // Also link any additional drivers found in results (substitutes, etc.)
            // But DON'T create them - they must exist in MyPodiumDrivers
            var additionalDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in raceResults)
            {
                // Trim names before checking to avoid issues with leading/trailing spaces
                var p1Trimmed = result.P1?.Trim() ?? "";
                var p2Trimmed = result.P2?.Trim() ?? "";
                var p3Trimmed = result.P3?.Trim() ?? "";
                
                if (!string.IsNullOrWhiteSpace(p1Trimmed) && !Data.F1DriverLineups.DriverRacedInSeason(year, p1Trimmed))
                    additionalDrivers.Add(p1Trimmed);
                if (!string.IsNullOrWhiteSpace(p2Trimmed) && !Data.F1DriverLineups.DriverRacedInSeason(year, p2Trimmed))
                    additionalDrivers.Add(p2Trimmed);
                if (!string.IsNullOrWhiteSpace(p3Trimmed) && !Data.F1DriverLineups.DriverRacedInSeason(year, p3Trimmed))
                    additionalDrivers.Add(p3Trimmed);
            }

            if (additionalDrivers.Any())
            {
                Console.WriteLine($"  Found {additionalDrivers.Count} additional drivers in results (substitutes/reserves)");
                foreach (var driverName in additionalDrivers)
                {
                    var driverId = _transformer.GetDriverId(driverName);
                    if (!string.IsNullOrEmpty(driverId))
                    {
                        // Check if already linked to avoid duplicate linking
                        // (This shouldn't happen now with proper trimming, but add safety check)
                        Console.WriteLine($"    Linking substitute: {driverName}");
                        await _inserter.InsertSeasonCompetitorAsync(seasonId, driverId, driverName);
                    }
                    else
                    {
                        Console.WriteLine($"    ? Warning: Substitute driver '{driverName}' not in MyPodiumDrivers, skipping");
                    }
                }
            }
            
            // Migrate events and predictions with race results
            await MigrateEventsAsync(seasonId, year, races, predictions, raceResults);
            
            // Calculate and insert user statistics with proper match calculations
            await CalculateUserStatisticsAsync(seasonId, year, predictions, raceResults, users);
        }
        catch (Exception ex)
        {
            _result.AddError($"Failed to migrate season {year}: {ex.Message}");
            Console.WriteLine($"  Exception: {ex}");
        }
    }

    private async Task MigrateEventsAsync(string seasonId, int year, 
        List<LegacyRace> races, List<LegacyPrediction> predictions, List<LegacyRaceResult> raceResults)
    {
        Console.WriteLine($"Migrating {races.Count} events for {year}...");
        
        if (races.Count == 0)
        {
            Console.WriteLine($"  ? No races to migrate for {year}");
            return;
        }
        
        foreach (var race in races)
        {
            try
            {
                var eventId = _transformer.GetOrCreateEventId(year, race.NumberRace);
                var status = _transformer.DetermineEventStatus(race.Date);
                
                Console.WriteLine($"  Processing Race #{race.NumberRace}: {race.Name}");
                
                // Use the date from race.Date (already constructed from Day/Month/Year or Date field)
                DateTime eventDate;
                if (race.Date.HasValue)
                {
                    eventDate = race.Date.Value;
                    Console.WriteLine($"    Using event date: {eventDate:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    eventDate = DateTime.UtcNow;
                    _result.AddWarning($"Race {race.NumberRace} ({race.Name}) has no date - using current date as fallback");
                    Console.WriteLine($"    ? WARNING: No date available, using current date: {eventDate:yyyy-MM-dd HH:mm:ss}");
                }
                
                await _inserter.InsertEventAsync(
                    seasonId,
                    eventId,
                    race.Name,
                    race.NumberRace,
                    eventDate,
                    race.Location ?? "Unknown",
                    status
                );
                _result.EventsCreated++;

                // Find predictions for this race
                var racePredictions = predictions
                    .Where(p => p.Race == race.NumberRace)
                    .ToList();

                Console.WriteLine($"    Found {racePredictions.Count} predictions for this race");

                // Get actual result from MyPodiumResults table
                var actualResult = raceResults.FirstOrDefault(r => r.Race == race.NumberRace);
                
                if (actualResult != null && 
                    !string.IsNullOrWhiteSpace(actualResult.P1) && 
                    !string.IsNullOrWhiteSpace(actualResult.P2) && 
                    !string.IsNullOrWhiteSpace(actualResult.P3))
                {
                    Console.WriteLine($"    Result: 1st={actualResult.P1}, 2nd={actualResult.P2}, 3rd={actualResult.P3}");
                    
                    // Get driver IDs with trimmed names for better matching
                    var p1Id = _transformer.GetDriverId(actualResult.P1.Trim());
                    var p2Id = _transformer.GetDriverId(actualResult.P2.Trim());
                    var p3Id = _transformer.GetDriverId(actualResult.P3.Trim());
                    
                    // Track which drivers are missing
                    bool anyMissing = false;
                    if (string.IsNullOrEmpty(p1Id))
                    {
                        Console.WriteLine($"    ? Warning: P1 driver '{actualResult.P1}' not found in MyPodiumDrivers");
                        anyMissing = true;
                    }
                    if (string.IsNullOrEmpty(p2Id))
                    {
                        Console.WriteLine($"    ? Warning: P2 driver '{actualResult.P2}' not found in MyPodiumDrivers");
                        anyMissing = true;
                    }
                    if (string.IsNullOrEmpty(p3Id))
                    {
                        Console.WriteLine($"    ? Warning: P3 driver '{actualResult.P3}' not found in MyPodiumDrivers");
                        anyMissing = true;
                    }
                    
                    // If any drivers missing, try to fix common issues and log details
                    if (anyMissing)
                    {
                        Console.WriteLine($"    ? MISSING DRIVER DETAILS:");
                        Console.WriteLine($"       P1: '{actualResult.P1}' (Length: {actualResult.P1.Length}, Has leading space: {actualResult.P1.StartsWith(" ")})");
                        Console.WriteLine($"       P2: '{actualResult.P2}' (Length: {actualResult.P2.Length}, Has leading space: {actualResult.P2.StartsWith(" ")})");
                        Console.WriteLine($"       P3: '{actualResult.P3}' (Length: {actualResult.P3.Length}, Has leading space: {actualResult.P3.StartsWith(" ")})");
                        
                        // Try to find these drivers in the known list for debugging
                        var allDriverIds = _transformer.GetAllDriverNames();
                        Console.WriteLine($"    Known drivers containing similar names:");
                        foreach (var knownDriver in allDriverIds.Where(d => 
                            d.Contains(actualResult.P1.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            d.Contains(actualResult.P2.Trim(), StringComparison.OrdinalIgnoreCase) ||
                            d.Contains(actualResult.P3.Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"       - '{knownDriver}'");
                        }
                    }
                    
                    // Insert result even if some drivers are missing (use empty ID for missing drivers)
                    await _inserter.InsertEventResultAsync(
                        eventId,
                        p1Id ?? string.Empty, actualResult.P1.Trim(),
                        p2Id ?? string.Empty, actualResult.P2.Trim(),
                        p3Id ?? string.Empty, actualResult.P3.Trim()
                    );
                    _result.EventResultsCreated++;
                    
                    if (anyMissing)
                    {
                        Console.WriteLine($"    ? Result inserted with missing driver IDs (will need manual correction)");
                    }
                }
                else
                {
                    Console.WriteLine($"    No result available yet");
                }

                // Migrate predictions for this race
                int predsMigrated = 0;
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
                        predsMigrated++;
                    }
                    catch (Exception ex)
                    {
                        _result.AddWarning($"Failed to migrate prediction for race {race.NumberRace}, user {pred.UserId}: {ex.Message}");
                    }
                }
                Console.WriteLine($"    Migrated {predsMigrated} predictions");
            }
            catch (Exception ex)
            {
                _result.AddError($"Failed to migrate event {race.Name}: {ex.Message}");
                Console.WriteLine($"    Exception: {ex}");
            }
        }
        
        Console.WriteLine($"? Completed migration of {_result.EventsCreated} events");
    }

    private async Task CalculateUserStatisticsAsync(string seasonId, int year, 
        List<LegacyPrediction> predictions, List<LegacyRaceResult> raceResults, List<LegacyUser> users)
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
            total += pred.Points!.Value;
            count++;
            
            // Find the actual result for this race
            var actualResult = raceResults.FirstOrDefault(r => r.Race == pred.Race);
            
            if (actualResult != null &&
                !string.IsNullOrWhiteSpace(actualResult.P1) &&
                !string.IsNullOrWhiteSpace(actualResult.P2) &&
                !string.IsNullOrWhiteSpace(actualResult.P3))
            {
                // Count INDIVIDUAL driver matches (not whole predictions)
                // For each predicted driver position, check if they're in the actual podium
                
                // Check P1 prediction
                if (!string.IsNullOrWhiteSpace(pred.P1))
                {
                    var p1Trim = pred.P1.Trim();
                    if (string.Equals(p1Trim, actualResult.P1.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        exact++; // P1 exact match (25 points)
                    }
                    else if (string.Equals(p1Trim, actualResult.P2.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oneOff++; // P1 predicted was actually P2 (18 points, 1 position off)
                    }
                    else if (string.Equals(p1Trim, actualResult.P3.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        twoOff++; // P1 predicted was actually P3 (15 points, 2 positions off)
                    }
                    // else: P1 not in podium (0 points)
                }
                
                // Check P2 prediction
                if (!string.IsNullOrWhiteSpace(pred.P2))
                {
                    var p2Trim = pred.P2.Trim();
                    if (string.Equals(p2Trim, actualResult.P2.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        exact++; // P2 exact match (25 points)
                    }
                    else if (string.Equals(p2Trim, actualResult.P1.Trim(), StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(p2Trim, actualResult.P3.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oneOff++; // P2 predicted was P1 or P3 (18 points, 1 position off)
                    }
                    // else: P2 not in podium (0 points)
                }
                
                // Check P3 prediction
                if (!string.IsNullOrWhiteSpace(pred.P3))
                {
                    var p3Trim = pred.P3.Trim();
                    if (string.Equals(p3Trim, actualResult.P3.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        exact++; // P3 exact match (25 points)
                    }
                    else if (string.Equals(p3Trim, actualResult.P2.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        oneOff++; // P3 predicted was actually P2 (18 points, 1 position off)
                    }
                    else if (string.Equals(p3Trim, actualResult.P1.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        twoOff++; // P3 predicted was actually P1 (15 points, 2 positions off)
                    }
                    // else: P3 not in podium (0 points)
                }
            }
            
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
        
        Console.WriteLine($"? Calculated statistics for {userStats.Count} users");
    }
}
