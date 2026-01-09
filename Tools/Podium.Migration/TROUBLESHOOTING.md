# Migration Tool Fixes - Diagnostic Guide

## Changes Made

I've enhanced the migration tool with better diagnostics and fixed potential issues:

### 1. **Added Diagnostic Mode**
Run with `--diagnose` flag to inspect your database before migration:

```bash
dotnet run --diagnose
```

This will show:
- How many races exist for 2024 and 2025
- What field names are used (Number vs NumberRace)
- Sample data from each table
- How many predictions exist per season

### 2. **Fixed 2024 vs 2025 Field Names**
- **2024**: Uses `Number` field for race ordering
- **2025**: Uses `NumberRace` field (with `NumberGP` for GP numbering)

The code now correctly handles both:
```csharp
if (year == 2024)
{
    raceNumber = entity.GetInt32("Number") ?? 0;
}
else // 2025
{
    raceNumber = entity.GetInt32("NumberRace") ?? entity.GetInt32("Number") ?? 0;
}
```

### 3. **Enhanced Logging**
The tool now provides much more detailed output:
- Shows each race being processed
- Displays predictions found per race
- Shows race results being created
- Reports statistics as they're calculated

Example output:
```
Processing Race #1: Australian Grand Prix
  Found 5 predictions for this race
  Result: 1st=Max Verstappen, 2nd=Sergio Perez, 3rd=Charles Leclerc
  Migrated 5 predictions
```

### 4. **Better Error Handling**
- More descriptive warnings when data is missing
- Detailed exception messages
- Continues processing even if some records fail

## How to Use

### Step 1: Diagnose First
Before running the actual migration, run diagnostic mode to see what data exists:

```bash
cd Tools\Podium.Migration
dotnet run --diagnose
```

**Look for:**
- ? Number of races for 2024/2025 (should match what you expect)
- ? Number of predictions (should be substantial)
- ? Field names being used correctly
- ? Any "table not found" errors

### Step 2: Run Migration
After confirming data looks good:

```bash
dotnet run
```

**Watch for:**
- "Extracted X races" messages
- "Found X predictions" messages
- "Processing Race #N" messages for each event
- "Migrated X predictions" per race
- Final summary showing non-zero counts

### Step 3: Verify Results
Check the new Podium tables for:
- `PodiumEvents` - Should have all races from both seasons
- `PodiumEventResults` - Should have results for completed races
- `PodiumPredictions` - Should have all user predictions
- `PodiumUserStatistics` - Should have stats per user per season

## Common Issues & Solutions

### Issue: "No races found for year X"
**Cause:** Race data doesn't exist or wrong field names
**Solution:** 
1. Run `--diagnose` to see what's in MyPodiumRaces
2. Check the field names displayed
3. Verify Year filter is working

### Issue: "No predictions found for year X"
**Cause:** Prediction data doesn't exist or Year field mismatch
**Solution:**
1. Run `--diagnose` to see what's in MyPodiumDreams
2. Check if Year field matches expected value (2024 or 2025)
3. Verify PartitionKey is "F1"

### Issue: "Found 0 predictions for this race"
**Cause:** Race number mismatch between MyPodiumRaces and MyPodiumDreams
**Solution:**
1. Check the `Race` field in MyPodiumDreams matches `Number`/`NumberRace` in MyPodiumRaces
2. May need to map race numbers if they're different

### Issue: Races created but no results
**Cause:** No predictions have Points=25 (exact matches)
**Solution:** This is normal if race hasn't happened yet or no one got exact match

### Issue: Predictions not created
**Cause:** Could be driver name mismatch or user ID issues
**Solution:** 
1. Check warnings in output
2. Look for specific error messages
3. Verify UserId in predictions matches Id/UsrId in MyPodiumUsers

## Debugging Checklist

Run through this checklist if you see issues:

- [ ] Azurite is running
- [ ] Legacy MyPodium tables exist and have data
- [ ] Ran `--diagnose` and saw expected counts
- [ ] Field names match year (2024=Number, 2025=NumberRace)
- [ ] Race numbers in MyPodiumDreams match MyPodiumRaces
- [ ] UserIds in predictions match users table
- [ ] Driver names are consistent (no typos)

## Expected Counts (Approximate)

Based on typical F1 seasons:
- **2024 Races**: ~24 races
- **2025 Races**: ~20-24 races (may have more if sprints counted separately)
- **Predictions per race**: Number of active users
- **Total predictions**: (races × users) per season
- **User statistics**: One record per user per season

## Next Steps After Successful Migration

1. Verify data in Azure Storage Explorer
2. Test the new Podium app with migrated data
3. Check user login (OTP only)
4. Verify predictions display correctly
5. Check leaderboards/statistics

## Contact Points

If migration fails:
1. Copy the full console output
2. Note which step failed (users/drivers/races/predictions/stats)
3. Check the Errors section in migration summary
4. Review warnings for hints

## Code Files Modified

- `Program.cs` - Added diagnostic mode
- `DiagnosticHelper.cs` - New diagnostic tool
- `LegacyDataExtractor.cs` - Fixed 2024/2025 field handling, better logging
- `MigrationOrchestrator.cs` - Improved logging, better error handling
