# Podium Database Setup Tool

This standalone console application creates the required Azure Table Storage tables and seeds them with test data for the Podium platform.

## Prerequisites

- .NET 10.0 SDK
- Azure Storage Account (or Azure Storage Emulator/Azurite for local development)

## Usage

### Option 1: Environment Variable (Recommended)

Set the `PODIUM_STORAGE_CONNECTION` environment variable with your Azure Storage connection string:

```bash
# Windows (PowerShell)
$env:PODIUM_STORAGE_CONNECTION="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
dotnet run

# Linux/Mac
export PODIUM_STORAGE_CONNECTION="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
dotnet run
```

### Option 2: Command Line Argument

Pass the connection string as a command line argument:

```bash
dotnet run "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
```

### For Local Development (Azurite)

If using Azurite (Azure Storage Emulator), use the default emulator connection string:

```bash
dotnet run "UseDevelopmentStorage=true"
```

## What It Does

The tool performs the following operations:

### Step 1: Create Tables

Creates the following Azure Table Storage tables:
- `PodiumSports` - Sports categories
- `PodiumTiers` - Competition tiers/levels within sports
- `PodiumSeasons` - Seasons for each tier
- `PodiumEvents` - Individual events/races
- `PodiumCompetitors` - Athletes/drivers/teams
- `PodiumSeasonParticipants` - Links competitors to seasons
- `PodiumEventResults` - Top 3 finishers for events
- `PodiumUsers` - User accounts
- `PodiumUserPredictions` - User predictions
- `PodiumPointsConfig` - Points configuration per season
- `PodiumAuthSessions` - Active authentication sessions
- `PodiumOTPCodes` - One-time password codes

### Step 2: Seed Data

Inserts test data including:
- **Motorsport** sport category
- **Formula 1, F2, F3** tiers
- **2025 and 2026** seasons
- **10 F1 drivers** (2025 grid)
- **5 sample races** (Bahrain, Saudi Arabia, Australia, Japan, China)
- **Points configuration** (10/5/3/1 point system)
- **3 test users** with passwords:
  - `test@example.com` / `password123`
  - `demo@example.com` / `demo123`
  - `admin@example.com` / `admin123`

## Important Notes

- **One-time execution**: This tool is designed to be run once to set up the database
- **Idempotent operations**: Running it multiple times will update existing entities (upsert)
- **No runtime checks**: The main application assumes tables exist and doesn't check at runtime
- **Test data only**: The seeded data is for testing purposes only
- **Security**: Test user passwords are hashed with BCrypt (work factor 10)

## After Setup

Once the database is set up:
1. Update your application's `appsettings.json` with the same connection string
2. Test authentication with one of the seeded test users
3. Make predictions for the seeded events
4. Add production data via an admin panel (future feature)

## Troubleshooting

### Connection Errors
- Verify your connection string is correct
- Check that your Azure Storage account is accessible
- For Azurite, ensure the emulator is running

### Permission Errors
- Ensure your Azure Storage account key has the necessary permissions
- For production, consider using Azure Active Directory authentication

### Table Already Exists
- This is expected behavior - the tool uses `UpsertEntity` to update existing data
- If you need a clean slate, delete the tables manually first

## Database Schema

For detailed information about the database schema, see [DATABASE_STRUCTURE.md](../docs/DATABASE_STRUCTURE.md).
