# Database Structure Documentation

## Overview
This document describes the Azure Table Storage database structure for the multi-sport Podium prediction platform.

## Design Principles
- Azure Table Storage is used for all data persistence
- All tables use PartitionKey and RowKey for optimal performance
- UTC timestamps are used throughout for consistency
- No runtime table existence checks - tables must be created via setup scripts

## Table Schemas

### 1. Sports Table (`PodiumSports`)

Stores information about different sports categories.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Fixed value: "Sport" |
| RowKey | string | Unique sport ID (GUID) |
| Name | string | Sport name (e.g., "Motorsport", "Football", "Basketball") |
| Description | string | Brief description of the sport |
| IsActive | bool | Whether sport is currently active |
| CreatedDate | DateTimeOffset | When the sport was added (UTC) |

**Example:**
```
PartitionKey: "Sport"
RowKey: "550e8400-e29b-41d4-a716-446655440000"
Name: "Motorsport"
Description: "Motor racing competitions"
IsActive: true
CreatedDate: 2025-01-01T00:00:00Z
```

---

### 2. Tiers Table (`PodiumTiers`)

Stores competition tiers/levels within sports (e.g., F1, F2, F3 within Motorsport).

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Sport ID (foreign key to Sports.RowKey) |
| RowKey | string | Unique tier ID (GUID) |
| Name | string | Tier name (e.g., "Formula 1", "Formula 2") |
| ShortName | string | Abbreviated name (e.g., "F1", "F2") |
| Description | string | Tier description |
| DisplayOrder | int | Order for display purposes |
| IsActive | bool | Whether tier is currently active |
| CreatedDate | DateTimeOffset | When the tier was added (UTC) |

**Example:**
```
PartitionKey: "550e8400-e29b-41d4-a716-446655440000" (Motorsport ID)
RowKey: "660e8400-e29b-41d4-a716-446655440001"
Name: "Formula 1"
ShortName: "F1"
Description: "Premier single-seater racing"
DisplayOrder: 1
IsActive: true
CreatedDate: 2025-01-01T00:00:00Z
```

---

### 3. Seasons Table (`PodiumSeasons`)

Stores season information for each sport/tier combination.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Tier ID (foreign key to Tiers.RowKey) |
| RowKey | string | Season year as string (e.g., "2025", "2026") |
| Name | string | Season display name (e.g., "2025 Season") |
| StartDate | DateTimeOffset | Season start date (UTC) |
| EndDate | DateTimeOffset | Season end date (UTC) |
| IsActive | bool | Whether season is currently active |
| CreatedDate | DateTimeOffset | When the season was added (UTC) |

**Example:**
```
PartitionKey: "660e8400-e29b-41d4-a716-446655440001" (F1 Tier ID)
RowKey: "2025"
Name: "2025 Season"
StartDate: 2025-03-01T00:00:00Z
EndDate: 2025-12-01T00:00:00Z
IsActive: true
CreatedDate: 2024-12-01T00:00:00Z
```

---

### 4. Events Table (`PodiumEvents`)

Stores individual events/races within a season.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Format: "{TierID}_{SeasonYear}" |
| RowKey | string | Unique event ID (GUID) |
| Name | string | Event name (e.g., "Monaco Grand Prix") |
| Location | string | Event location |
| EventDate | DateTimeOffset | When the event takes place (UTC) |
| PredictionCutoffDate | DateTimeOffset | Deadline for predictions (UTC) |
| Round | int | Event number in season sequence |
| IsCompleted | bool | Whether event has finished |
| IsActive | bool | Whether event is currently active |
| CreatedDate | DateTimeOffset | When the event was added (UTC) |

**Example:**
```
PartitionKey: "660e8400-e29b-41d4-a716-446655440001_2025"
RowKey: "770e8400-e29b-41d4-a716-446655440002"
Name: "Monaco Grand Prix"
Location: "Monaco"
EventDate: 2025-05-25T13:00:00Z
PredictionCutoffDate: 2025-05-25T12:45:00Z
Round: 6
IsCompleted: false
IsActive: true
CreatedDate: 2025-01-15T00:00:00Z
```

---

### 5. Competitors Table (`PodiumCompetitors`)

Stores athletes/drivers/teams participating across sports.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Sport ID (foreign key to Sports.RowKey) |
| RowKey | string | Unique competitor ID (GUID) |
| Name | string | Competitor full name |
| ShortName | string | Abbreviated name or nickname |
| Number | string | Competitor number (if applicable) |
| Team | string | Team name (if applicable) |
| Country | string | Country code (e.g., "GB", "USA") |
| IsActive | bool | Whether competitor is currently active |
| CreatedDate | DateTimeOffset | When the competitor was added (UTC) |

**Example:**
```
PartitionKey: "550e8400-e29b-41d4-a716-446655440000" (Motorsport ID)
RowKey: "880e8400-e29b-41d4-a716-446655440003"
Name: "Max Verstappen"
ShortName: "VER"
Number: "1"
Team: "Red Bull Racing"
Country: "NL"
IsActive: true
CreatedDate: 2025-01-01T00:00:00Z
```

---

### 6. Season Participants Table (`PodiumSeasonParticipants`)

Links competitors to specific seasons (who competes in which sport/tier/season).

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Format: "{TierID}_{SeasonYear}" |
| RowKey | string | Competitor ID (foreign key to Competitors.RowKey) |
| CompetitorName | string | Cached competitor name for performance |
| Team | string | Team name for this season |
| Number | string | Number for this season |
| IsActive | bool | Whether participation is active |
| JoinedDate | DateTimeOffset | When competitor joined season (UTC) |

**Example:**
```
PartitionKey: "660e8400-e29b-41d4-a716-446655440001_2025"
RowKey: "880e8400-e29b-41d4-a716-446655440003"
CompetitorName: "Max Verstappen"
Team: "Red Bull Racing"
Number: "1"
IsActive: true
JoinedDate: 2025-01-15T00:00:00Z
```

---

### 7. Event Results Table (`PodiumEventResults`)

Stores the top 3 finishers for each event.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Event ID (foreign key to Events.RowKey) |
| RowKey | string | Position ("P1", "P2", "P3") |
| CompetitorId | string | Competitor ID (foreign key to Competitors.RowKey) |
| CompetitorName | string | Cached competitor name |
| RecordedDate | DateTimeOffset | When result was recorded (UTC) |

**Example:**
```
PartitionKey: "770e8400-e29b-41d4-a716-446655440002" (Monaco GP)
RowKey: "P1"
CompetitorId: "880e8400-e29b-41d4-a716-446655440003"
CompetitorName: "Max Verstappen"
RecordedDate: 2025-05-25T15:30:00Z
```

---

### 8. Users Table (`PodiumUsers`)

Stores user account information with secure password hashing.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Fixed value: "User" |
| RowKey | string | Unique user ID (GUID) |
| Email | string | User email (unique, indexed) |
| Name | string | User display name |
| PasswordHash | string | Bcrypt password hash |
| IsActive | bool | Whether account is active |
| IsEmailVerified | bool | Whether email is verified |
| CreatedDate | DateTimeOffset | When account was created (UTC) |
| LastLoginDate | DateTimeOffset | Last successful login (UTC) |

**Example:**
```
PartitionKey: "User"
RowKey: "990e8400-e29b-41d4-a716-446655440004"
Email: "user@example.com"
Name: "John Doe"
PasswordHash: "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy"
IsActive: true
IsEmailVerified: true
CreatedDate: 2025-01-10T00:00:00Z
LastLoginDate: 2025-01-15T08:30:00Z
```

---

### 9. User Predictions Table (`PodiumUserPredictions`)

Stores user predictions for events.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Format: "{UserId}_{EventId}" |
| RowKey | string | Position ("P1", "P2", "P3") |
| CompetitorId | string | Predicted competitor ID |
| CompetitorName | string | Cached competitor name |
| PredictedDate | DateTimeOffset | When prediction was made (UTC) |
| PointsAwarded | int | Points earned (calculated post-event) |

**Example:**
```
PartitionKey: "990e8400-e29b-41d4-a716-446655440004_770e8400-e29b-41d4-a716-446655440002"
RowKey: "P1"
CompetitorId: "880e8400-e29b-41d4-a716-446655440003"
CompetitorName: "Max Verstappen"
PredictedDate: 2025-05-24T20:15:00Z
PointsAwarded: 10
```

---

### 10. Points Configuration Table (`PodiumPointsConfig`)

Defines points awarded for prediction accuracy per sport/tier/season.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Format: "{TierID}_{SeasonYear}" |
| RowKey | string | Fixed value: "Config" |
| ExactPositionPoints | int | Points for exact position match |
| OneOffPoints | int | Points for 1 position off |
| TwoOffPoints | int | Points for 2 positions off |
| InPodiumPoints | int | Points for being in top 3 but wrong position |
| CreatedDate | DateTimeOffset | When config was created (UTC) |

**Example:**
```
PartitionKey: "660e8400-e29b-41d4-a716-446655440001_2025"
RowKey: "Config"
ExactPositionPoints: 10
OneOffPoints: 5
TwoOffPoints: 3
InPodiumPoints: 1
CreatedDate: 2025-01-01T00:00:00Z
```

---

### 11. Auth Sessions Table (`PodiumAuthSessions`)

Stores active authentication sessions.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Fixed value: "Sessions" |
| RowKey | string | Session ID (GUID) |
| UserId | string | User ID (foreign key to Users.RowKey) |
| Email | string | User email |
| UserName | string | User display name (cached) |
| CreatedDate | DateTimeOffset | When session was created (UTC) |
| ExpiryDate | DateTimeOffset | When session expires (UTC) |
| IsActive | bool | Whether session is still active |
| LastActivityDate | DateTimeOffset | Last activity timestamp (UTC) |

**Example:**
```
PartitionKey: "Sessions"
RowKey: "aa0e8400-e29b-41d4-a716-446655440005"
UserId: "990e8400-e29b-41d4-a716-446655440004"
Email: "user@example.com"
UserName: "John Doe"
CreatedDate: 2025-01-15T08:30:00Z
ExpiryDate: 2025-01-29T08:30:00Z
IsActive: true
LastActivityDate: 2025-01-15T10:15:00Z
```

---

### 12. OTP Codes Table (`PodiumOTPCodes`)

Stores one-time password codes for email authentication.

| Field | Type | Description |
|-------|------|-------------|
| PartitionKey | string | Fixed value: "OTP" |
| RowKey | string | Unique OTP record ID (GUID) |
| Email | string | Email address for this OTP |
| Code | string | 4-digit OTP code |
| UserId | string | User ID (foreign key to Users.RowKey) |
| ExpiryTime | DateTimeOffset | When OTP expires (UTC, typically 10 min) |
| IsUsed | bool | Whether OTP has been consumed |
| CreatedDate | DateTimeOffset | When OTP was generated (UTC) |

**Example:**
```
PartitionKey: "OTP"
RowKey: "bb0e8400-e29b-41d4-a716-446655440006"
Email: "user@example.com"
Code: "1234"
UserId: "990e8400-e29b-41d4-a716-446655440004"
ExpiryTime: 2025-01-15T08:40:00Z
IsUsed: false
CreatedDate: 2025-01-15T08:30:00Z
```

---

## Indexing Strategy

Azure Table Storage automatically indexes PartitionKey and RowKey. For additional lookups:

1. **Email Lookup (Users)**: Query by `Email` property with partition scan
2. **Season Lookup (Events)**: Partition by "{TierID}_{SeasonYear}" for efficient season queries
3. **User Predictions**: Partition by "{UserId}_{EventId}" for fast user-event lookups
4. **Event Results**: Partition by EventId for quick podium retrieval

## Security Considerations

1. **Password Storage**: Use BCrypt with minimum work factor of 10
2. **OTP Codes**: 4-digit codes, 10-minute expiry, single-use only
3. **Session Management**: 14-day expiry, server-side validation required
4. **Data Validation**: All inputs must be sanitized before storage
5. **Access Control**: Services should validate user permissions before data access

## Performance Optimization

1. **Denormalization**: Competitor names cached in predictions/results for read performance
2. **Partition Strategy**: Designed to avoid hot partitions
3. **Batch Operations**: Multiple predictions can be inserted in batches
4. **TTL Strategy**: OTP codes and expired sessions should be periodically cleaned

## Migration Notes

- Tables must be created in order due to dependencies
- Seed data should be inserted after all tables exist
- Legacy MyPodium data can be migrated using separate migration scripts
- Test thoroughly in development environment before production deployment
