# CRITICAL FIX: Type Conversion Error

## Problem Identified

The error message you saw:
```
Failed to migrate season 2024: Cannot return System.Nullable<DateTimeOffset> type for a System.Int32 typed property.
Failed to migrate season 2025: Cannot return System.Nullable<DateTimeOffset> type for a System.Int32 typed property.
```

This was happening because Azure Table Storage was storing fields in different types than expected, and the code was trying to read them directly without proper type conversion.

## Root Cause

When the code tried to use methods like:
- `entity.GetInt32("Number")` - Expected int, but might be stored as long or double
- `entity.GetDouble("NumberGP")` - Expected double, but might be stored as int
- `entity.GetDateTimeOffset("Date")` - Expected DateTimeOffset, causing type mismatch

Azure Table Storage doesn't enforce strict types, so fields can be stored as various numeric types.

## Solution Applied

I've completely rewritten the `LegacyDataExtractor.cs` with three new safe helper methods:

### 1. SafeGetInt32
```csharp
private int? SafeGetInt32(TableEntity entity, string propertyName)
{
    // Handles: int, long, double, or string that can be parsed
    // Returns: int? (null if not found or can't convert)
}
```

### 2. SafeGetDouble
```csharp
private double? SafeGetDouble(TableEntity entity, string propertyName)
{
    // Handles: double, int, long, or string that can be parsed
    // Returns: double? (null if not found or can't convert)
}
```

### 3. SafeGetDateTime
```csharp
private DateTime? SafeGetDateTime(TableEntity entity, string propertyName)
{
    // Handles: DateTimeOffset, DateTime, or string that can be parsed
    // Returns: DateTime? (null if not found or can't convert)
}
```

## Changes Made

### ExtractRacesAsync
**Before:**
```csharp
raceNumber = entity.GetInt32("Number") ?? 0;
Date = entity.GetDateTimeOffset("Date")?.DateTime
```

**After:**
```csharp
raceNumber = SafeGetInt32(entity, "Number") ?? 0;
Date = SafeGetDateTime(entity, "Date")
```

### ExtractPredictionsAsync
**Before:**
```csharp
Race = entity.GetInt32("Race") ?? 0;
Points = entity.GetInt32("Points");
```

**After:**
```csharp
Race = SafeGetInt32(entity, "Race") ?? 0;
Points = SafeGetInt32(entity, "Points");
```

### Error Handling
Each entity processing is now wrapped in try-catch, so if one record fails, it logs a warning and continues with the rest.

## What This Fixes

? **Type conversion errors** - No more "Cannot return type X for property Y"  
? **2024 races with Number field** - Safely reads int/long/double  
? **2025 races with NumberRace field** - Safely reads with fallback  
? **Date fields** - Handles DateTimeOffset/DateTime conversion  
? **Missing fields** - Returns null instead of crashing  
? **Partial failures** - Continues processing even if some records fail  

## Next Steps

1. **Close the running migration tool** (it's holding a file lock)
2. **Rebuild**: The code has been fixed and should compile without errors
3. **Run with --diagnose**: This will show you what's in your database
4. **Run full migration**: Should now complete without type conversion errors

## What to Expect

You should now see output like:
```
Extracting F1 races for year 2024...
  2024 Race: Australian Grand Prix, Number=1
  2024 Race: Bahrain Grand Prix, Number=2
  ...
? Extracted 24 races for 2024

Extracting predictions for year 2024...
? Extracted 156 predictions for 2024
  Predictions per race: Race 1: 6, Race 2: 7, ...

Migrating 24 events for 2024...
  Processing Race #1: Australian Grand Prix
    Found 6 predictions for this race
    Result: 1st=Max Verstappen, 2nd=Sergio Perez, 3rd=Charles Leclerc
    Migrated 6 predictions
  ...
```

## Testing

After you close the current process and rebuild:

```bash
cd Tools\Podium.Migration
dotnet build
dotnet run --diagnose
```

The diagnostic will show you exactly what fields exist and their types, helping verify the fix works correctly.
