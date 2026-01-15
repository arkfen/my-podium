# Podium Database Initialization Tool

This console application initializes the Azure Table Storage database structure for the Podium prediction platform.

## Configuration

Configure the Azure Storage connection in one of two ways:

### Option 1: appsettings.json
```json
{
  "AzureStorage": {
    "StorageUri": "https://youraccount.table.core.windows.net/",
    "AccountName": "youraccount",
    "AccountKey": "your-account-key"
  }
}
```

### Option 2: User Secrets (Recommended for development)
```bash
dotnet user-secrets init
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-account-key"
```

## Options

Control the initialization behavior in appsettings.json:

```json
{
  "Options": {
    "CreateTables": true,        // Create all database tables
    "AddSampleData": true,        // Add sample/test data
    "DropExistingTables": false   // WARNING: Delete existing tables first
  }
}
```

## Usage

### Basic Usage
```bash
cd Tools/Podium.DbInit
dotnet run
```

### Create Tables Only
Set `"AddSampleData": false` in appsettings.json, then run:
```bash
dotnet run
```

### Recreate Everything (Fresh Start)
?? **WARNING**: This will delete all existing data!

Set `"DropExistingTables": true` in appsettings.json, then run:
```bash
dotnet run
```

You'll be prompted to confirm before deletion.

## What Gets Created

### Tables
- PodiumSports
- PodiumTiers
- PodiumSeasons
- PodiumCompetitors
- PodiumSeasonCompetitors
- PodiumEvents
- PodiumEventResults
- PodiumUsers
- PodiumAuthSessions
- PodiumOTPCodes
- PodiumPredictions
- PodiumScoringRules
- PodiumUserStatistics

### Sample Data (when enabled)
- **Sport**: Motorsport
- **Tier**: Formula 1
- **Season**: 2025
- **Competitors**: 20 F1 drivers
- **Events**: 5 sample races
- **Scoring**: 25 (exact), 18 (one off), 15 (two off)
- **Users**: 3 test accounts
- **Predictions**: Sample predictions for upcoming events

### Test Accounts
When sample data is enabled, you can log in with:
- Email: john@example.com | Username: JohnDoe | Password: John's password
- Email: jane@example.com | Username: JaneSmith | Password: Jane's password
- Email: alex@example.com | Username: AlexRacer | Password: Alex's password

## Notes

- The tool is idempotent - safe to run multiple times
- Existing records will be updated (upsert operation)
- Sample data uses realistic F1 driver names and race schedules
- Passwords are properly hashed using PBKDF2 with salt

## Troubleshooting

### "Storage connection information is missing"
Ensure you've configured Azure Storage credentials in appsettings.json or user secrets.

### "Table already exists" errors
This is normal if tables already exist. The tool uses CreateIfNotExists.

### Access denied errors
Verify your storage account key and ensure the account has permission to create tables.
