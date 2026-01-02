# Podium - Multi-Sport Prediction Platform

A modern, flexible prediction platform for sports competitions built with .NET 10, Blazor, and Azure Table Storage.

## ??? Architecture

### Projects

1. **Podium.Shared** - Shared Razor components, models, and services
   - Pages (Razor components for UI)
   - Models (Entity models)
   - Services (Business logic, API client, state management)
   - Styles (CSS)

2. **Podium.Web** - Blazor WebAssembly app for web browsers
   - Runs entirely in the browser
   - Consumes the API

3. **Podium.Native** - .NET MAUI app for iOS, Android, Windows, macOS
   - Cross-platform mobile and desktop
   - Shares UI with Podium.Web via Podium.Shared

4. **Podium.Api** - ASP.NET Core Minimal API
   - Secure backend for data access
   - Protects Azure Storage credentials
   - RESTful endpoints with Swagger documentation

5. **Tools/Podium.DbInit** - Database initialization tool
   - Creates Azure Table Storage schema
   - Generates sample data for testing

## ?? Security Model

**Client apps NEVER directly access Azure Storage**. All data flows through the secure API:

```
[Client Apps] ? HTTPS ? [Podium.Api] ? [Azure Table Storage]
 (Web/Mobile)                (Secure)         (Hidden credentials)
```

### Why This Matters
- Azure Storage credentials stay server-side only
- API enforces business rules and validation
- Easier to add authentication/authorization later

## ??? Database Structure

13 Azure Tables:
- **Sports & Competitions**: Sports, Tiers, Seasons, Competitors, SeasonCompetitors
- **Events**: Events, EventResults
- **Users & Auth**: Users, AuthSessions, OTPCodes
- **Predictions**: Predictions, ScoringRules, UserStatistics

See `Docs/DatabaseStructure.md` for complete schema.

## ?? Getting Started

### 1. Initialize the Database

```bash
cd Tools/Podium.DbInit

# Configure Azure Storage (choose one):

# Option A: User Secrets (recommended for dev)
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-account-key"

# Option B: appsettings.json
# Edit appsettings.json with your credentials

# Run initialization
dotnet run
```

This creates all tables and adds sample data (F1 2025 season, drivers, races, test users).

### 2. Configure the API

```bash
cd ../../Podium/Podium.Api

# Set Azure Storage credentials
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-account-key"
```

Or edit `appsettings.Development.json` with your credentials.

### 3. Run the API

```bash
dotnet run
```

API will start at `https://localhost:7002` (or similar). Check console output.

### 4. Run the Web App

```bash
cd ../Podium.Web
dotnet run
```

Or run from Visual Studio - set Podium.Web as startup project.

###5. Test with Sample Accounts

After running DbInit, you can sign in with:
- Email: `john@example.com` | Password: `John's password`
- Email: `jane@example.com` | Password: `Jane's password`
- Email: `alex@example.com` | Password: `Alex's password`

## ?? Project Status

### ? Completed
- Database schema & initialization tool
- Core entity models
- Data access layer (repositories)
- Authentication services (password & OTP)
- API project with RESTful endpoints
- API client for Blazor apps
- State management
- Basic UI pages:
  - Home/Landing page
  - Sign In (password & email OTP)
  - Sign Up
  - Sport selection
  - Tier/Competition selection

### ?? In Progress / TODO

**High Priority**:
- Event selection page
- Prediction submission page with competitor selection
- My Predictions page (view user's predictions)
- Leaderboard page
- Navigation menu/layout improvements
- Email service integration (OTP currently logs to console)

**Medium Priority**:
- Results viewing
- User profile page
- Session persistence (browser storage)
- Better error handling
- Loading states and spinners

**Future Enhancements**:
- Admin panel (manage sports, events, results)
- Points calculation engine
- Real-time leaderboard updates
- Push notifications for events
- Social features (comments, sharing)
- Multiple scoring systems
- Season archives

## ?? Tech Stack

- **.NET 10** - Latest framework
- **Blazor WebAssembly** - Web UI
- **.NET MAUI** - Cross-platform mobile/desktop
- **Azure Table Storage** - NoSQL database
- **ASP.NET Core Minimal API** - Backend
- **CSS** - Responsive styling (no framework dependencies)

## ?? API Documentation

When the API is running, visit:
- Swagger UI: `https://localhost:7002/swagger`

### Key Endpoints

**Authentication:**
- `POST /api/auth/register` - Create account
- `POST /api/auth/signin` - Sign in with password
- `POST /api/auth/send-otp` - Send email verification code
- `POST /api/auth/verify-otp` - Verify OTP and sign in

**Sports & Competitions:**
- `GET /api/sports` - List all sports
- `GET /api/sports/{sportId}/tiers` - Get competitions for a sport
- `GET /api/tiers/{tierId}/seasons` - Get seasons for a competition
- `GET /api/seasons/{seasonId}/events` - Get events in a season
- `GET /api/seasons/{seasonId}/competitors` - Get competitors

**Predictions:**
- `POST /api/predictions` - Submit a prediction
- `GET /api/predictions/{eventId}/user/{userId}` - Get user's prediction
- `GET /api/predictions/user/{userId}/season/{seasonId}` - Get all user predictions for season

**Leaderboard:**
- `GET /api/leaderboard/season/{seasonId}` - Get season leaderboard
- `GET /api/leaderboard/season/{seasonId}/user/{userId}` - Get user stats

## ?? Development Notes

### Running Multiple Projects
For full development, run both API and Web projects:

**Terminal 1:**
```bash
cd Podium/Podium.Api
dotnet watch run
```

**Terminal 2:**
```bash
cd Podium/Podium.Web
dotnet watch run
```

### CORS Configuration
API is configured to allow requests from `https://localhost:7001` and `http://localhost:5000`.
Update `Program.cs` in Podium.Api if your Web app runs on different ports.

### Mobile Development
To run the MAUI app:
1. Open solution in Visual Studio 2022+
2. Set Podium.Native as startup project
3. Select target platform (Android, iOS, Windows)
4. Run

## ?? Contributing

This is a learning/portfolio project. Feel free to:
- Add features
- Improve UI/UX
- Fix bugs
- Suggest enhancements

## ?? License

[Your license here]

## ?? Next Steps

To continue development, focus on:
1. **Event selection page** - Show upcoming events with dates
2. **Prediction form** - Let users pick 3 competitors
3. **My Predictions view** - Show submitted predictions
4. **Leaderboard** - Display rankings

See the TODO comments in code for specific implementation notes.

---

**Built with ?? using .NET 10 and Blazor**
