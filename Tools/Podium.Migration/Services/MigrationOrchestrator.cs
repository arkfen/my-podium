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

            // Step 4: Extract drivers from MyPodiumDrivers table
            var legacyDrivers = await _extractor.ExtractDriversAsync();
            var driverNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Add drivers from MyPodiumDrivers
            foreach (var driver in legacyDrivers)
            {
                if (!string.IsNullOrWhiteSpace(driver.Name))
                    driverNames.Add(driver.Name.Trim());
            }
            Console.WriteLine($"Found {driverNames.Count} drivers from MyPodiumDrivers");
            
            // Also add drivers from predictions to catch any missing ones
            var predictionDrivers = await _extractor.ExtractDriverNamesAsync(yearsToMigrate);
            foreach (var driver in predictionDrivers)
            {
                driverNames.Add(driver);
            }
            Console.WriteLine($"Total unique drivers: {driverNames.Count}");
            
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

            // Link all drivers to this season (from predictions AND results)
            var driversInSeason = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var pred in predictions)
            {
                if (!string.IsNullOrWhiteSpace(pred.P1)) driversInSeason.Add(pred.P1.Trim());
                if (!string.IsNullOrWhiteSpace(pred.P2)) driversInSeason.Add(pred.P2.Trim());
                if (!string.IsNullOrWhiteSpace(pred.P3)) driversInSeason.Add(pred.P3.Trim());
            }
            
            foreach (var result in raceResults)
            {
                if (!string.IsNullOrWhiteSpace(result.P1)) driversInSeason.Add(result.P1.Trim());
                if (!string.IsNullOrWhiteSpace(result.P2)) driversInSeason.Add(result.P2.Trim());
                if (!string.IsNullOrWhiteSpace(result.P3)) driversInSeason.Add(result.P3.Trim());
            }

            Console.WriteLine($"  Linking {driversInSeason.Count} drivers to season");
            foreach (var driverName in driversInSeason)
            {
                var driverId = _transformer.GetDriverId(driverName);
                if (!string.IsNullOrEmpty(driverId))
                {
                    await _inserter.InsertSeasonCompetitorAsync(seasonId, driverId, driverName);
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
                var eventDate = race.Date ?? DateTime.UtcNow;
                
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
                    
                    // Ensure driver IDs exist for result drivers
                    var p1Id = _transformer.GetDriverId(actualResult.P1);
                    var p2Id = _transformer.GetDriverId(actualResult.P2);
                    var p3Id = _transformer.GetDriverId(actualResult.P3);
                    
                    // If driver IDs don't exist, create them on-the-fly
                    if (string.IsNullOrEmpty(p1Id))
                    {
                        p1Id = _transformer.GetOrCreateDriverId(actualResult.P1);
                        var shortName = _transformer.GenerateShortName(actualResult.P1);
                        await _inserter.InsertCompetitorAsync(_transformer.DisciplineId, p1Id, actualResult.P1, shortName);
                        await _inserter.InsertSeasonCompetitorAsync(seasonId, p1Id, actualResult.P1);
                    }
                    if (string.IsNullOrEmpty(p2Id))
                    {
                        p2Id = _transformer.GetOrCreateDriverId(actualResult.P2);
                        var shortName = _transformer.GenerateShortName(actualResult.P2);
                        await _inserter.InsertCompetitorAsync(_transformer.DisciplineId, p2Id, actualResult.P2, shortName);
                        await _inserter.InsertSeasonCompetitorAsync(seasonId, p2Id, actualResult.P2);
                    }
                    if (string.IsNullOrEmpty(p3Id))
                    {
                        p3Id = _transformer.GetOrCreateDriverId(actualResult.P3);
                        var shortName = _transformer.GenerateShortName(actualResult.P3);
                        await _inserter.InsertCompetitorAsync(_transformer.DisciplineId, p3Id, actualResult.P3, shortName);
                        await _inserter.InsertSeasonCompetitorAsync(seasonId, p3Id, actualResult.P3);
                    }
                    
                    await _inserter.InsertEventResultAsync(
                        eventId,
                        p1Id, actualResult.P1,
                        p2Id, actualResult.P2,
                        p3Id, actualResult.P3
                    );
                    _result.EventResultsCreated++;
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
                // Count how many drivers are correctly predicted (regardless of position)
                var predictedDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    pred.P1?.Trim() ?? "", 
                    pred.P2?.Trim() ?? "",
                    pred.P3?.Trim() ?? "" 
                };
                predictedDrivers.Remove(""); // Remove empty
                
                var actualDrivers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    actualResult.P1.Trim(), 
                    actualResult.P2.Trim(), 
                    actualResult.P3.Trim() 
                };
                
                // Count correct drivers
                int correctDrivers = predictedDrivers.Intersect(actualDrivers).Count();
                
                // Now count exact position matches
                int exactPositions = 0;
                if (string.Equals(pred.P1?.Trim(), actualResult.P1.Trim(), StringComparison.OrdinalIgnoreCase))
                    exactPositions++;
                if (string.Equals(pred.P2?.Trim(), actualResult.P2.Trim(), StringComparison.OrdinalIgnoreCase))
                    exactPositions++;
                if (string.Equals(pred.P3?.Trim(), actualResult.P3.Trim(), StringComparison.OrdinalIgnoreCase))
                    exactPositions++;
                
                // Classification based on both correct drivers and exact positions
                if (exactPositions == 3)
                {
                    // All 3 in exact positions = exact match
                    exact++;
                }
                else if (correctDrivers == 3)
                {
                    // All 3 drivers correct but not all in right positions = one off
                    oneOff++;
                }
                else if (correctDrivers == 2)
                {
                    // 2 drivers correct = two off
                    twoOff++;
                }
                // Less than 2 correct = no match category
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
