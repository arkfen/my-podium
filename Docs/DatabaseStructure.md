# to start Azurite on my local PC

cd C:\Users\arkfe\AppData\Roaming\npm
azurite --silent --location c:\azurite --debug c:\azurite\debug.log


# to start additional Azurite instances on different ports use:
azurite --silent   --location c:\azurite2   --debug c:\azurite2\debug.log   --blobPort 11000   --queuePort 11001   --tablePort 11002

---

# Podium Prediction Platform - Database Structure

This document describes the Azure Table Storage structure for the Podium prediction platform.

## Overview

The platform uses Azure Table Storage for data persistence. All tables follow Azure Table Storage conventions with PartitionKey and RowKey as primary identifiers.

## Tables

### 1. Disciplines
**Table Name:** `PodiumDisciplines`

Stores information about different motorsport disciplines (racing categories).

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Fixed value: "Discipline" |
| RowKey | string | Unique discipline ID (GUID) |
| Name | string | Discipline name (e.g., "Single-Seater Racing", "Touring / Stock Cars") |
| DisplayName | string | Display name for UI |
| IsActive | bool | Whether discipline is active |
| CreatedDate | DateTime | When the discipline was created |

**Example:**
- PartitionKey: "Discipline"
- RowKey: "f1a2b3c4-d5e6-7f8g-9h0i-1j2k3l4m5n6o"
- Name: "Single-Seater Racing"

---

### 2. Series
**Table Name:** `PodiumSeries`

Stores racing series within disciplines (e.g., Formula 1, MotoGP, NASCAR).

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Discipline ID (links to Disciplines.RowKey) |
| RowKey | string | Unique series ID (GUID) |
| DisciplineId | string | Discipline ID (redundant for easier queries) |
| Name | string | Series name (e.g., "Formula 1", "MotoGP") |
| DisplayName | string | Display name for UI |
| GoverningBody | string | Governing organization (e.g., "FIA", "FIM", "NASCAR") |
| Region | string | Primary region (e.g., "Global", "USA", "Europe") |
| VehicleType | string | Type of vehicle (e.g., "Open-wheel", "Motorcycle", "Stock Car") |
| IsActive | bool | Whether series is active |
| CreatedDate | DateTime | When the series was created |

**Example:**
- PartitionKey: "f1a2b3c4-d5e6-7f8g-9h0i-1j2k3l4m5n6o"
- RowKey: "a1b2c3d4-e5f6-7g8h-9i0j-1k2l3m4n5o6p"
- Name: "Formula 1"
- GoverningBody: "FIA"
- Region: "Global"
- VehicleType: "Open-wheel"

---

### 3. Seasons
**Table Name:** `PodiumSeasons`

Stores seasons for each series/competition.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Series ID (links to Series.RowKey) |
| RowKey | string | Unique season ID (GUID) |
| SeriesId | string | Series ID (redundant for easier queries) |
| Year | int | Season year (e.g., 2025) |
| Name | string | Season name (e.g., "2025 Season") |
| IsActive | bool | Whether season is currently active |
| StartDate | DateTime | Season start date |
| EndDate | DateTime? | Season end date (nullable for ongoing) |
| BestResultsNumber | int? | Number of best results to count for leaderboard ranking (nullable) |
| CreatedDate | DateTime | When the season was created |

**Example:**
- PartitionKey: "a1b2c3d4-e5f6-7g8h-9i0j-1k2l3m4n5o6p"
- RowKey: "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q"
- Year: 2025
- BestResultsNumber: 15

---

### 4. Competitors
**Table Name:** `PodiumCompetitors`

Stores competitors (drivers, riders, teams) across all disciplines.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Discipline ID (links to Disciplines.RowKey) |
| RowKey | string | Unique competitor ID (GUID) |
| DisciplineId | string | Discipline ID (redundant for easier queries) |
| Name | string | Competitor full name |
| ShortName | string | Short name for display |
| Type | string | "Individual" or "Team" |
| IsActive | bool | Whether competitor is active |
| CreatedDate | DateTime | When the competitor was created |

**Example:**
- PartitionKey: "f1a2b3c4-d5e6-7f8g-9h0i-1j2k3l4m5n6o"
- RowKey: "c3d4e5f6-g7h8-9i0j-1k2l-3m4n5o6p7q8r"
- Name: "Max Verstappen"
- Type: "Individual"

---

### 5. SeasonCompetitors
**Table Name:** `PodiumSeasonCompetitors`

Links competitors to specific seasons (which competitors are competing in which season).

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Season ID (links to Seasons.RowKey) |
| RowKey | string | Competitor ID (links to Competitors.RowKey) |
| SeasonId | string | Season ID (redundant for easier queries) |
| CompetitorId | string | Competitor ID (redundant) |
| CompetitorName | string | Competitor name (denormalized) |
| JoinDate | DateTime | When competitor joined the season |

**Example:**
- PartitionKey: "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q"
- RowKey: "c3d4e5f6-g7h8-9i0j-1k2l-3m4n5o6p7q8r"

---

### 6. Events
**Table Name:** `PodiumEvents`

Stores individual events/races within a season.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Season ID (links to Seasons.RowKey) |
| RowKey | string | Unique event ID (GUID) |
| SeasonId | string | Season ID (redundant for easier queries) |
| Name | string | Event name (e.g., "Australian Grand Prix") |
| DisplayName | string | Display name for UI |
| EventNumber | int | Sequential number in season (1, 2, 3...) |
| EventDate | DateTime | Date and time of event (UTC) |
| Location | string | Event location |
| Status | string | "Upcoming", "InProgress", "Completed" |
| IsActive | bool | Whether event accepts predictions |
| CreatedDate | DateTime | When the event was created |

**Example:**
- PartitionKey: "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q"
- RowKey: "d4e5f6g7-h8i9-0j1k-2l3m-4n5o6p7q8r9s"
- Name: "Australian Grand Prix"
- EventDate: 2025-03-16T05:00:00Z

---

### 7. EventResults
**Table Name:** `PodiumEventResults`

Stores the actual results (top 3) for completed events.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Event ID (links to Events.RowKey) |
| RowKey | string | Fixed value: "Result" |
| EventId | string | Event ID (redundant for easier queries) |
| FirstPlaceId | string | Competitor ID for 1st place |
| FirstPlaceName | string | Competitor name (denormalized) |
| SecondPlaceId | string | Competitor ID for 2nd place |
| SecondPlaceName | string | Competitor name (denormalized) |
| ThirdPlaceId | string | Competitor ID for 3rd place |
| ThirdPlaceName | string | Competitor name (denormalized) |
| UpdatedDate | DateTime | When results were entered/updated |

**Example:**
- PartitionKey: "d4e5f6g7-h8i9-0j1k-2l3m-4n5o6p7q8r9s"
- RowKey: "Result"

---

### 8. Users
**Table Name:** `PodiumUsers`

Stores user accounts.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | First 6 chars of user ID |
| RowKey | string | Remaining chars of user ID |
| UserId | string | Full unique user ID (GUID) |
| Email | string | User email (unique) |
| Username | string | Display username |
| PasswordHash | string | Hashed password (if using password auth) |
| PasswordSalt | string | Salt for password hash |
| PreferredAuthMethod | string | "Email" or "Password" or "Both" |
| IsActive | bool | Whether account is active |
| CreatedDate | DateTime | Account creation date |
| LastLoginDate | DateTime? | Last login date |

**Example:**
- PartitionKey: "e5f6g7"
- RowKey: "h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"
- UserId: "e5f6g7h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"

---

### 9. AuthSessions
**Table Name:** `PodiumAuthSessions`

Stores active authentication sessions.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Fixed value: "Session" |
| RowKey | string | Session ID (GUID) |
| UserId | string | User ID (links to Users.UserId) |
| Email | string | User email (denormalized) |
| Username | string | Username (denormalized) |
| CreatedDate | DateTime | Session creation date |
| ExpiryDate | DateTime | Session expiry date |
| IsActive | bool | Whether session is active |

**Example:**
- PartitionKey: "Session"
- RowKey: "f6g7h8i9-j0k1-2l3m-4n5o-6p7q8r9s0t1u"

---

### 10. OTPCodes
**Table Name:** `PodiumOTPCodes`

Stores one-time passcodes for email authentication.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Fixed value: "OTP" |
| RowKey | string | Unique OTP ID (GUID) |
| Email | string | Email address |
| Code | string | 6-digit OTP code |
| UserId | string | User ID (links to Users.UserId) |
| ExpiryTime | DateTime | Code expiry time (10 minutes) |
| IsUsed | bool | Whether code has been used |
| CreatedDate | DateTime | When code was generated |

**Example:**
- PartitionKey: "OTP"
- RowKey: "g7h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"
- Code: "123456"

---

### 11. Predictions
**Table Name:** `PodiumPredictions`

Stores user predictions for events.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Event ID (links to Events.RowKey) |
| RowKey | string | User ID (links to Users.UserId) |
| EventId | string | Event ID (redundant for easier queries) |
| UserId | string | User ID (redundant) |
| FirstPlaceId | string | Predicted competitor ID for 1st |
| FirstPlaceName | string | Competitor name (denormalized) |
| SecondPlaceId | string | Predicted competitor ID for 2nd |
| SecondPlaceName | string | Competitor name (denormalized) |
| ThirdPlaceId | string | Predicted competitor ID for 3rd |
| ThirdPlaceName | string | Competitor name (denormalized) |
| PointsEarned | int? | Points earned (null until calculated) |
| SubmittedDate | DateTime | When prediction was submitted |
| UpdatedDate | DateTime | Last update date |

**Example:**
- PartitionKey: "d4e5f6g7-h8i9-0j1k-2l3m-4n5o6p7q8r9s"
- RowKey: "e5f6g7h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"

---

### 12. ScoringRules
**Table Name:** `PodiumScoringRules`

Stores scoring rules for each series/season combination.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Season ID (links to Seasons.RowKey) |
| RowKey | string | Fixed value: "Scoring" |
| SeasonId | string | Season ID (redundant) |
| ExactMatchPoints | int | Points for exact prediction (all 3 correct positions) |
| OneOffPoints | int | Points when 1 position is off |
| TwoOffPoints | int | Points when 2 positions are off |
| CreatedDate | DateTime | When rules were created |

**Example:**
- PartitionKey: "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q"
- RowKey: "Scoring"
- ExactMatchPoints: 25
- OneOffPoints: 18
- TwoOffPoints: 15

---

### 13. UserStatistics
**Table Name:** `PodiumUserStatistics`

Stores aggregated user statistics per season.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Season ID (links to Seasons.RowKey) |
| RowKey | string | User ID (links to Users.UserId) |
| SeasonId | string | Season ID (redundant) |
| UserId | string | User ID (redundant) |
| Username | string | Username (denormalized) |
| BestResultsPoints | int? | Points from best N results for leaderboard ranking (nullable, null if feature not configured) |
| TotalPoints | int | Total points earned in season |
| PredictionsCount | int | Number of predictions made |
| ExactMatches | int | Count of exact predictions |
| OneOffMatches | int | Count of one-off predictions |
| TwoOffMatches | int | Count of two-off predictions |
| LastUpdated | DateTime | Last statistics update |

**Example:**
- PartitionKey: "b2c3d4e5-f6g7-8h9i-0j1k-2l3m4n5o6p7q"
- RowKey: "e5f6g7h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"

---

### 14. Admins
**Table Name:** `PodiumAdmins`

Stores administrator privileges for users. Admins are regular users with elevated permissions.

| Column | Type | Description |
|--------|------|-------------|
| PartitionKey | string | Fixed value: "Admin" |
| RowKey | string | User ID (links to Users.UserId) |
| UserId | string | User ID (redundant for easier queries) |
| IsActive | bool | Whether admin account is active |
| CanManageAdmins | bool | Whether admin can create/modify other admins |
| CreatedDate | DateTime | When admin privileges were granted |
| CreatedBy | string | User ID who created this admin |
| LastModifiedDate | DateTime? | Last modification date (nullable) |
| LastModifiedBy | string? | User ID who last modified this admin |

**Example:**
- PartitionKey: "Admin"
- RowKey: "e5f6g7h8i9j0-k1l2-3m4n-5o6p-7q8r9s0t1u2v"
- IsActive: true
- CanManageAdmins: true

**Admin Permissions:**
- **IsActive = true**: User can access admin endpoints (manage seasons, view diagnostics, etc.)
- **CanManageAdmins = true**: User can create, modify, and remove other admin accounts

**Security Notes:**
- Admins must first be regular users (must exist in PodiumUsers table)
- Admins still sign in normally using their user credentials
- Admin status is checked after session validation
- Admins cannot remove themselves
- Only admins with CanManageAdmins=true can modify the admin list

---

## Future Enhancements

Potential future additions:
1. ~~UserRoles table for admin/moderator roles~~ **IMPLEMENTED** (see PodiumAdmins above)
2. Notifications table for user notifications
3. AuditLog table for tracking changes
4. UserPreferences table for UI preferences
5. Comments/Social features

---

## Security Considerations

1. **Password Storage**: Passwords are hashed using PBKDF2 with salt
2. **OTP Codes**: Expire after 10 minutes and can only be used once
3. **Sessions**: Expire after 14 days and can be invalidated
4. **Connection Strings**: Stored securely in app configuration
5. **Input Validation**: All user inputs validated before storage
6. **API Authorization**: All endpoints (except auth endpoints) require valid session via X-Session-Id header
7. **Admin Authorization**: Admin endpoints require active admin status; admin management endpoints require CanManageAdmins permission
