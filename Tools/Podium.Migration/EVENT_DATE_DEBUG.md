# Fix for Event Dates Issue

## Problem
Event dates are showing as today's date instead of the actual race dates from the database.

## Diagnostic Enhancements Added

### 1. Enhanced Race Extraction Logging
Added detailed logging in `LegacyDataExtractor.cs` to show:

```
Race properties: PartitionKey, RowKey, Year, Day, Month, Number, Name, Location, etc.
Raw date fields - Day: 16, Month: 3, Year: 2024
? Constructed date from components: 2024-03-16 00:00:00 UTC
```

Or if there's an issue:
```
? Day or Month is missing or zero - cannot construct date from components
? Date field also not available or invalid
```

### 2. Enhanced Event Migration Logging
Added detailed logging in `MigrationOrchestrator.cs` to show:

```
Processing Race #1: Australian Grand Prix
  Using event date: 2024-03-16 00:00:00
```

Or if date is missing:
```
  ? WARNING: No date available, using current date: 2025-01-09 15:30:00
```

And adds a warning to the migration result.

## How to Debug

After closing the current migration process and rebuilding:

```bash
cd Tools\Podium.Migration
dotnet build
dotnet run
```

Look for the following in the output:

### For Each Race:
```
Extracting F1 races for year 2024...
  Race properties: PartitionKey, RowKey, Year, Day, Month, Number, Name, Location
  2024 Race: Australian Grand Prix, Number=1
    Raw date fields - Day: 16, Month: 3, Year: 2024
    ? Constructed date from components: 2024-03-16 00:00:00 UTC
```

### During Event Migration:
```
Processing Race #1: Australian Grand Prix
  Using event date: 2024-03-16 00:00:00
```

## Possible Issues

### Issue 1: Day/Month fields are NULL or 0
If you see:
```
Raw date fields - Day: , Month: , Year: 2024
? Day or Month is missing or zero
```

**Problem:** The MyPodiumRaces table doesn't have Day/Month fields, or they're null.

**Solution:** Check the old database structure. The fields might be named differently (e.g., `RaceDay`, `RaceMonth`, or stored in a different format).

### Issue 2: Date field is in wrong format
If you see:
```
? Date field also not available or invalid
```

**Problem:** The Date field exists but can't be parsed.

**Solution:** We may need to adjust the `SafeGetDateTime` method to handle different date formats.

### Issue 3: Field names don't match
If you see limited properties:
```
Race properties: PartitionKey, RowKey, Name, Number
```

**Problem:** Day/Month/Date fields might have different names in your database.

**Common alternatives:**
- `RaceDay` / `RaceMonth` instead of `Day` / `Month`
- `EventDate` instead of `Date`
- `Timestamp` for the date
- Date stored as string in format "2024-03-16" or "03/16/2024"

## Next Steps

1. **Close** the running migration tool (process 16584)
2. **Rebuild** the project
3. **Run** the migration with enhanced logging
4. **Review** the console output for the detailed date information
5. **Report back** what you see in the logs for a few sample races

The enhanced logging will tell us exactly:
- What fields exist in the database
- What values they contain
- Whether dates are being constructed successfully
- Where the fallback to `DateTime.UtcNow` is happening

## Expected Output (if working correctly)

```
Extracting F1 races for year 2024...
  Race properties: PartitionKey, RowKey, Timestamp, etag, odata.etag, Year, Day, Month, Number, Name, Location
  2024 Race: Australian Grand Prix, Number=1
    Raw date fields - Day: 16, Month: 3, Year: 2024
    ? Constructed date from components: 2024-03-16 00:00:00 UTC
  2024 Race: Bahrain Grand Prix, Number=2
    Raw date fields - Day: 2, Month: 3, Year: 2024
    ? Constructed date from components: 2024-03-02 00:00:00 UTC
...

Migrating 24 events for 2024...
  Processing Race #1: Australian Grand Prix
    Using event date: 2024-03-16 00:00:00
  Processing Race #2: Bahrain Grand Prix
    Using event date: 2024-03-02 00:00:00
```

## Code Changes

1. **LegacyDataExtractor.cs** - `ExtractRacesAsync` method
   - Added logging of all entity properties
   - Added logging of raw Day/Month/Year values
   - Added success/failure indicators for date construction

2. **MigrationOrchestrator.cs** - `MigrateEventsAsync` method
   - Added explicit check for null date with warning
   - Added logging of which date is being used
   - Added warning to migration result when date is missing
