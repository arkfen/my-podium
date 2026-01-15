# Scoring Rules System - Implementation Summary

## Overview
Successfully removed hardcoded points calculations from the main Podium application and implemented a database-driven scoring rules system per season.

## What Was Changed

### 1. New Repository: `ScoringRulesRepository`
**File:** `Podium\Podium.Shared\Services\Data\ScoringRulesRepository.cs`

- **Interface:** `IScoringRulesRepository`
- **Methods:**
  - `GetScoringRulesBySeasonAsync(string seasonId)` - Retrieves scoring rules for a season
  - `CreateOrUpdateScoringRulesAsync(ScoringRules scoringRules)` - Creates or updates scoring rules
  - `DeleteScoringRulesAsync(string seasonId)` - Deletes scoring rules (reverts to defaults)

- **Database Table:** `PodiumScoringRules`
  - PartitionKey: SeasonId
  - RowKey: "Scoring" (fixed value)
  - Columns: ExactMatchPoints, OneOffPoints, TwoOffPoints, CreatedDate

### 2. New Service: `ScoringService`
**File:** `Podium\Podium.Shared\Services\Business\ScoringService.cs`

- **Interface:** `IScoringService`
- **Methods:**
  - `CalculatePointsAsync(seasonId, predicted, actual)` - Calculates points using DB rules
  - `RecalculateEventPredictionsAsync(eventId, seasonId)` - Recalculates all predictions for an event

- **Features:**
  - Retrieves scoring rules from database per season
  - Falls back to default values (25/18/15) if no custom rules exist
  - Implements the same logic as before: exact match, all drivers correct (one-off), 2 drivers correct (two-off)

### 3. Admin API Endpoints
**File:** `Podium\Podium.Api\Endpoints\AdminEndpoints.cs`

New endpoints for managing scoring rules:

#### GET `/api/admin/seasons/{seasonId}/scoring-rules`
- Returns scoring rules for a season
- Shows default values if not configured

#### POST `/api/admin/seasons/{seasonId}/scoring-rules`
- Creates or updates scoring rules
- **Request Body:**
  ```json
  {
    "exactMatchPoints": 25,
    "oneOffPoints": 18,
    "twoOffPoints": 15
  }
  ```
- **Validation:**
  - Points must be non-negative
  - ExactMatchPoints should be highest value
  - Season must exist

#### DELETE `/api/admin/seasons/{seasonId}/scoring-rules`
- Removes custom scoring rules
- Season reverts to default values

### 4. Automatic Points Calculation
**File:** `Podium\Podium.Api\Endpoints\AdminEndpoints.cs` (line 983)

- **Updated Endpoint:** `POST /api/admin/events/{eventId}/result`
- **New Behavior:** When event results are saved, all predictions for that event are automatically recalculated using the season's scoring rules
- **Response includes:**
  ```json
  {
    "result": { ... },
    "pointsRecalculated": true,
    "message": "Event result saved and all prediction points recalculated successfully"
  }
  ```

### 5. Dependency Injection Registration
**File:** `Podium\Podium.Api\Program.cs`

Added to DI container:
```csharp
builder.Services.AddScoped<IScoringRulesRepository, ScoringRulesRepository>();
builder.Services.AddScoped<IScoringService, ScoringService>();
```

### 6. Legacy Code Documentation
**File:** `MyPodiumLegacy\Components\Pages\Counter.razor`

- Added clear comments indicating it uses hardcoded values for backward compatibility
- Main Podium app no longer uses this approach

## How It Works

### Flow for Points Calculation

1. **Admin sets up a season:**
   - Season is created via admin dashboard
   - Optionally, admin configures custom scoring rules for the season
   - If no custom rules, defaults are used (25/18/15)

2. **Users make predictions:**
   - Users submit predictions for upcoming events
   - No points assigned yet (PointsEarned = null)

3. **Event completes and admin enters results:**
   - Admin marks event as "Completed"
   - Admin submits top 3 results via `POST /api/admin/events/{eventId}/result`

4. **Automatic calculation triggers:**
   - System retrieves event's seasonId
   - `ScoringService.RecalculateEventPredictionsAsync()` is called
   - For each prediction:
     - Retrieves season's scoring rules from `PodiumScoringRules` table
     - Compares prediction with actual results
     - Calculates points based on DB rules
     - Updates prediction with earned points

5. **Points are displayed:**
   - Leaderboard shows updated scores
   - User statistics reflect new points

## Database Schema

### PodiumScoringRules Table Structure
```
PartitionKey: SeasonId (e.g., "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q")
RowKey: "Scoring" (fixed value)
---
SeasonId: string
ExactMatchPoints: int (default: 25)
OneOffPoints: int (default: 18)
TwoOffPoints: int (default: 15)
CreatedDate: DateTime
```

## Scoring Rules Logic

The system supports three levels of accuracy:

### 1. Exact Match (ExactMatchPoints)
- All 3 drivers predicted in correct positions
- Example: Predicted [P1: Verstappen, P2: Hamilton, P3: Leclerc]
- Actual: [P1: Verstappen, P2: Hamilton, P3: Leclerc]
- **Points: ExactMatchPoints (default: 25)**

### 2. All Drivers Correct, Positions Off (OneOffPoints)
- All 3 drivers are in top 3, but positions are wrong
- Example: Predicted [P1: Verstappen, P2: Hamilton, P3: Leclerc]
- Actual: [P1: Hamilton, P2: Verstappen, P3: Leclerc]
- **Points: OneOffPoints (default: 18)**

### 3. 2 Drivers Correct (TwoOffPoints)
- Exactly 2 of the predicted drivers finished in top 3
- Example: Predicted [P1: Verstappen, P2: Hamilton, P3: Leclerc]
- Actual: [P1: Verstappen, P2: Norris, P3: Leclerc]
- **Points: TwoOffPoints (default: 15)**

### 4. Less Than 2 Correct
- 0 or 1 drivers correct
- **Points: 0**

## Testing the System

### 1. Create Scoring Rules for a Season
```bash
POST /api/admin/seasons/{seasonId}/scoring-rules
Authorization: X-Session-Id: {admin-session-id}
Content-Type: application/json

{
  "exactMatchPoints": 30,
  "oneOffPoints": 20,
  "twoOffPoints": 10
}
```

### 2. View Scoring Rules
```bash
GET /api/admin/seasons/{seasonId}/scoring-rules
Authorization: X-Session-Id: {admin-session-id}
```

### 3. Submit Event Results (Triggers Auto-Calculation)
```bash
POST /api/admin/events/{eventId}/result
Authorization: X-Session-Id: {admin-session-id}
Content-Type: application/json

{
  "firstPlaceId": "{competitorId1}",
  "firstPlaceName": "Max Verstappen",
  "secondPlaceId": "{competitorId2}",
  "secondPlaceName": "Lewis Hamilton",
  "thirdPlaceId": "{competitorId3}",
  "thirdPlaceName": "Charles Leclerc"
}
```

Response will include:
```json
{
  "result": { ... },
  "pointsRecalculated": true,
  "message": "Event result saved and all prediction points recalculated successfully"
}
```

## Benefits

? **No Hardcoded Values** - Points are stored in database, not in code  
? **Per-Season Flexibility** - Different seasons can have different scoring rules  
? **Automatic Calculation** - Points recalculate when results are entered  
? **Backward Compatible** - Falls back to defaults if rules not configured  
? **Admin Controlled** - Admins can change rules via API endpoints  
? **Audit Trail** - CreatedDate tracks when rules were set  

## Migration Notes

### For Existing Seasons
- If a season doesn't have custom scoring rules, the system uses defaults (25/18/15)
- No data migration needed
- Old predictions will be recalculated next time event results are updated

### For New Seasons
- Optionally configure scoring rules when creating a season
- Rules can be updated at any time before or during the season
- Changes only affect future calculations (existing points remain until results are re-entered)

## Future Enhancements

Potential additions:
- Historical scoring rules tracking (version history)
- Bulk recalculation endpoint for all events in a season
- Different scoring methods (position-based, bonus points, etc.)
- UI in admin dashboard for managing scoring rules
- Preview scoring impact before saving rules

---

**Status:** ? Successfully Implemented  
**Build Status:** ? Passing  
**Main App:** No hardcoded values  
**Migration Tool:** Keeps hardcoded defaults (as intended)  
**Legacy Code:** Documented as legacy with hardcoded values
