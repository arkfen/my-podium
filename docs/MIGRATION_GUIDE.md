# Migration Guide: Legacy MyPodium → New Multi-Sport Platform

This guide explains the migration from the legacy F1-focused MyPodium application to the new flexible, multi-sport Podium platform.

## Architecture Changes

### Legacy Architecture (MyPodium)
- **Single Project**: MyPodium (Blazor Server, .NET 9)
- **Sport Focus**: F1 only
- **Database**: Azure Table Storage with F1-specific schema
- **Authentication**: Email OTP only
- **Deployment**: Server-side rendering only

### New Architecture (Podium)
- **Multi-Project Solution**: Shared library + Web + Native
  - **Podium.Shared**: Core business logic (Razor Class Library, .NET 10)
  - **Podium.Web**: Blazor WebAssembly application (PWA-capable)
  - **Podium.Native**: .NET MAUI for iOS, Android, Windows, macOS
- **Sport Focus**: Multi-sport with hierarchical structure (Sport → Tier → Season)
- **Database**: Redesigned Azure Table Storage schema supporting multiple sports
- **Authentication**: Dual mode (Email OTP + Password)
- **Deployment**: Client-side WASM + native mobile/desktop apps

## Key Differences

### Database Schema

#### Legacy Tables (MyPodium)
- `MyPodiumUsers` - Basic user info
- `MyPodiumAuthSessions` - Auth sessions
- `MyPodiumOTPCodes` - OTP codes
- `MyPodiumDrivers` - F1 drivers only
- `MyPodiumRaces` - F1 races only
- `MyPodiumDreams` - Predictions
- (Limited to F1 context)

#### New Tables (Podium)
- `PodiumSports` - Multiple sports support
- `PodiumTiers` - Competition levels (F1, F2, F3, etc.)
- `PodiumSeasons` - Seasons per tier
- `PodiumEvents` - Generic events (races, matches, games)
- `PodiumCompetitors` - Athletes/drivers/teams across sports
- `PodiumSeasonParticipants` - Competitors per season
- `PodiumEventResults` - Top 3 finishers
- `PodiumUsers` - Enhanced user accounts with password hashing
- `PodiumUserPredictions` - Structured predictions
- `PodiumPointsConfig` - Configurable points system per season
- `PodiumAuthSessions` - Enhanced session management
- `PodiumOTPCodes` - OTP authentication

### Code Structure

#### Legacy (MyPodium)
```
MyPodium/
├── Components/Pages/
│   ├── Podium.razor (Prediction entry)
│   ├── Season2024.razor
│   ├── Season2025.razor
│   └── SignIn.razor
├── Services/
│   ├── AuthService.cs
│   └── StatisticsService.cs
└── Program.cs
```

#### New (Podium)
```
Podium.Shared/           # Reusable library
├── Models/
│   └── CoreModels.cs    # All data models
├── Services/
│   ├── TableStorageService.cs
│   ├── AuthenticationService.cs
│   ├── SportService.cs
│   ├── EventService.cs
│   ├── PredictionService.cs
│   ├── ResultsService.cs
│   └── LeaderboardService.cs
└── Components/
    ├── Auth/SignInComponent.razor
    ├── Predictions/PredictionForm.razor
    └── Leaderboard/LeaderboardComponent.razor

Podium.Web/              # WASM app
├── Pages/
│   ├── Home.razor
│   ├── Predictions.razor
│   └── Leaderboard.razor
└── Program.cs

Podium.Native/           # MAUI app
└── (Native platform implementations)
```

### Feature Comparison

| Feature | Legacy MyPodium | New Podium | Notes |
|---------|----------------|------------|-------|
| **Sports Supported** | F1 only | Multiple | Extensible architecture |
| **Tiers/Competitions** | N/A | Multi-tier | F1, F2, F3, etc. |
| **Authentication** | Email OTP | Email OTP + Password | Dual authentication |
| **Password Security** | N/A | BCrypt hashing | Industry standard |
| **Predictions** | F1 races | Generic events | Sport-agnostic |
| **Points System** | Hardcoded | Configurable | Per season/competition |
| **Leaderboards** | Limited | Full support | Season & event leaderboards |
| **Client Type** | Server-side | Client-side WASM | Better performance |
| **Mobile Apps** | No | Yes (MAUI) | iOS, Android native |
| **Desktop Apps** | No | Yes (MAUI) | Windows, macOS |
| **Admin Panel** | Basic | Future | Planned enhancement |

## Migration Steps

### 1. Data Migration (If Keeping Legacy Data)

If you want to migrate existing user data and predictions from legacy MyPodium:

```csharp
// Example migration script (create as needed)
// 1. Read from MyPodiumUsers → Write to PodiumUsers
// 2. Map MyPodiumDrivers → PodiumCompetitors (with SportId)
// 3. Map MyPodiumRaces → PodiumEvents (with TierId and SeasonYear)
// 4. Map MyPodiumDreams → PodiumUserPredictions
// 5. Create initial Sport/Tier/Season records for F1
```

**Note**: Migration scripts are not included. Create custom scripts based on your data.

### 2. Fresh Start (Recommended for Testing)

1. Run the Podium.DatabaseSetup tool to create new tables with seed data
2. Test with seed data (3 test users, 10 F1 drivers, 5 races)
3. Add production data via future admin panel or scripts

### 3. Configuration Changes

#### Legacy (MyPodium)
```json
// appsettings.json
{
  "ConnectionStrings": {
    "DefaultStorageUri": "...",
    "DefaultAccountName": "...",
    "DefaultStorageAccountKey": "..."
  }
}
```

#### New (Podium)
```json
// Podium.Web/wwwroot/appsettings.json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

### 4. Code Migration Guidelines

If migrating custom code from legacy to new:

#### Service Pattern
```csharp
// Legacy: Direct table access
var tableClient = new TableClient(uri, tableName, credentials);

// New: Use ITableStorageService
public class MyService
{
    private readonly ITableStorageService _storageService;
    
    public MyService(ITableStorageService storageService)
    {
        _storageService = storageService;
    }
    
    public void DoSomething()
    {
        var tableClient = _storageService.GetTableClient("MyTable");
        // Use tableClient
    }
}
```

#### Model Usage
```csharp
// Legacy: TableEntity directly
var entity = new TableEntity("User", userId);

// New: Use typed models
var user = new User
{
    Id = userId,
    Email = email,
    Name = name
};
```

## Deployment

### Legacy MyPodium
- Deploy to Azure App Service (Blazor Server)
- Requires active server connection

### New Podium

#### Podium.Web (Blazor WASM)
- Deploy to Azure Static Web Apps, GitHub Pages, or any static hosting
- No server required (except for API endpoints if added)
- Works offline (PWA)

#### Podium.Native (MAUI)
- Publish to App Stores (Apple App Store, Google Play)
- Distribute Windows/macOS apps via Microsoft Store or direct download

## Testing

### Test Users (Seeded Data)
- `test@example.com` / `password123`
- `demo@example.com` / `demo123`
- `admin@example.com` / `admin123`

### Test Scenarios
1. Sign in with email OTP
2. Sign in with password
3. Select Motorsport → Formula 1 → 2025 Season
4. Make prediction for Bahrain Grand Prix
5. View leaderboards
6. Check results

## Rollback Plan

If issues occur:

1. **Keep legacy MyPodium running** in parallel during transition
2. **Use separate Azure Storage accounts** for legacy and new
3. **Test thoroughly** before switching users to new platform
4. **Monitor usage** and gather feedback

## Support

For migration questions:
1. Check [README.md](README.md) for setup instructions
2. Review [docs/DATABASE_STRUCTURE.md](docs/DATABASE_STRUCTURE.md) for schema details
3. Open GitHub issues for specific problems

## Timeline

Recommended migration timeline:

- **Week 1**: Set up development environment, test new platform
- **Week 2**: Migrate/create production data
- **Week 3**: Internal testing with small user group
- **Week 4**: Full rollout to all users
- **Week 5+**: Monitor, gather feedback, iterate

## Future Enhancements

Post-migration roadmap:
1. Admin panel for data management
2. Automated points calculation
3. Email notifications
4. Social features (friends, private leagues)
5. Advanced analytics
6. Mobile app refinements

---

## Questions?

Contact the development team or open a GitHub issue.
