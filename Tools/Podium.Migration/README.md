# F1 Historical Data Migration Tool

This tool migrates historical Formula 1 data from the legacy MyPodium database structure to the new Podium database structure.

## Features

- **User Migration**: Migrates all non-admin users from MyPodiumUsers to PodiumUsers (OTP-only authentication)
- **Driver Migration**: Extracts all unique driver names from predictions and creates competitor records
- **Season Migration**: Creates F1 seasons for 2024 and/or 2025
- **Event Migration**: Migrates all races/events with proper ordering (uses NumberRace for sorting)
- **Prediction Migration**: Transfers all user predictions with calculated points
- **Statistics Calculation**: Generates user statistics per season
- **Result Detection**: Automatically determines race results from predictions with exact match points

## Database Structure Mapping

### Old Schema (MyPodium)
- `MyPodiumUsers` ? User table
- `MyPodiumRaces` ? Race table (F1 partition, Year filter)
- `MyPodiumDreams` ? Predictions table (F1 partition, Year + Race filters)

### New Schema (Podium)
- `PodiumDisciplines` ? Single-Seater Racing
- `PodiumSeries` ? Formula 1
- `PodiumSeasons` ? 2024 Season, 2025 Season
- `PodiumCompetitors` ? F1 Drivers
- `PodiumSeasonCompetitors` ? Driver-Season links
- `PodiumEvents` ? Races/Events
- `PodiumEventResults` ? Actual race results
- `PodiumUsers` ? Migrated users (OTP-only)
- `PodiumPredictions` ? User predictions
- `PodiumScoringRules` ? 25/18/15 points system
- `PodiumUserStatistics` ? Per-season user stats

## Configuration

Edit `appsettings.json` or use user secrets:

```json
{
  "AzureStorage": {
    "StorageUri": "http://127.0.0.1:10002/devstoreaccount1",
    "AccountName": "devstoreaccount1",
    "AccountKey": "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
  },
  "Migration": {
    "Season2024": true,
    "Season2025": true,
    "DryRun": false
  }
}
```

### Configuration Options

- **Season2024**: Migrate 2024 F1 season data (default: true)
- **Season2025**: Migrate 2025 F1 season data (default: true)
- **DryRun**: Test mode - no data written (default: false)

## Usage

### Prerequisites
1. Ensure Azurite is running with your legacy MyPodium data
2. Verify the old tables exist: MyPodiumUsers, MyPodiumRaces, MyPodiumDreams
3. Backup your data if needed

### Running the Migration

```bash
cd Tools\Podium.Migration
dotnet run
```

The tool will:
1. Ask for confirmation (unless in dry-run mode)
2. Create the base structure (Discipline, Series)
3. Migrate users (excluding admins)
4. Extract and migrate all unique drivers
5. Process each season:
   - Create season record
   - Set up scoring rules
   - Link drivers to season
   - Migrate all events/races
   - Determine actual results from predictions
   - Migrate all predictions with points
   - Calculate and store user statistics

### Dry Run Mode

To preview the migration without writing data:

```json
"Migration": {
  "DryRun": true
}
```

## Race Number Handling

The old system used two numbers:
- `NumberRace`: Sequential race number (used for ordering)
- `NumberGP`: Grand Prix number (differs when sprint races exist)

The new system uses a single `EventNumber` field. The migration uses `NumberRace` for ordering since it provides the correct chronological sequence.

## Points Calculation

The scoring system:
- **25 points**: Exact match (all 3 positions correct)
- **18 points**: One position off (all 3 drivers correct, positions slightly wrong)
- **15 points**: Two positions off (2 drivers correct)
- **0 points**: Less than 2 drivers correct

## Result Detection Logic

The tool determines actual race results by:
1. Looking for predictions with 25 points (exact matches)
2. If no exact matches, using the most common podium from scored predictions
3. Creating EventResult records for races with identifiable results

## User Migration Notes

- All migrated users use **OTP (email) authentication only**
- No passwords are migrated (they didn't exist in old system)
- PreferredAuthMethod is set to "Email"
- Admin users are excluded from migration
- UserIds are remapped with new GUIDs

## Output

The tool provides:
- Real-time progress updates
- Detailed migration summary
- Error and warning logs
- ID mapping summary
- Timing statistics

## Troubleshooting

### Table Not Found
Ensure Azurite is running and the legacy tables exist.

### Connection Errors
Verify the connection string in appsettings.json matches your Azurite configuration.

### Missing Predictions
Some users may not have predictions for all races - this is normal and handled gracefully.

### Driver Name Variations
The tool uses exact string matching for driver names. Ensure consistency in the source data.

## Development

Project structure:
- `Models/` - Data models and result tracking
- `Services/` - Migration services (extract, transform, insert, orchestrate)
- `Program.cs` - Main entry point
- `appsettings.json` - Configuration

## Next Steps After Migration

1. Verify the migrated data in the new tables
2. Test user authentication (OTP via email)
3. Review predictions and points calculations
4. Check user statistics accuracy
5. Consider running the new app's tests

## Support

For issues or questions, review the migration summary output and error logs.
