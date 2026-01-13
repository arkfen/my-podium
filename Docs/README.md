# Podium - Multi-Sport Prediction Platform

A modern, flexible prediction platform for sports competitions built with .NET 10, Blazor, .NET MAUI, and Azure Table Storage.

## 🏗️ Architecture

### Projects

1. **Podium.Shared** - Shared Razor components, models, and services
   - Used by both Web and MAUI apps
   - Pages (Razor components for UI)
   - Models (Entity models)
   - Services (Business logic, API client, state management, configuration)
   - Styles (CSS in wwwroot)

2. **Podium.Web** - Blazor WebAssembly app for web browsers
   - Runs entirely in the browser
   - Consumes the API
   - Shares all UI with MAUI via Podium.Shared

3. **Podium.Native** - .NET MAUI app for iOS, Android, Windows, macOS
   - Cross-platform mobile and desktop
   - Shares UI with Podium.Web via Podium.Shared
   - Uses BlazorWebView for hybrid apps

4. **Podium.Api** - ASP.NET Core Minimal API
   - Secure backend for data access
   - Protects Azure Storage credentials
   - RESTful endpoints

5. **Tools/Podium.DbInit** - Database initialization tool
   - Creates Azure Table Storage schema
   - Generates sample data for testing

## 🔒 Security Model

**Client apps NEVER directly access Azure Storage**. All data flows through the secure API:

```
[Web/MAUI Apps] → HTTPS → [Podium.Api] → [Azure Table Storage]
                            (Secure)         (Hidden credentials)
```

## ⚙️ Environment Configuration

Both Web and MAUI apps use the shared `IAppConfiguration` service for environment-aware settings.

### Web App (Blazor WASM)
- Development: Reads from `wwwroot/appsettings.json`
- Production: Reads from `wwwroot/appsettings.Production.json`
- Auto-detects environment via `IWebAssemblyHostEnvironment.IsDevelopment()`

### MAUI App
- Development: `#if DEBUG` - hardcoded in `MauiProgram.cs`
- Production: `#else` - hardcoded in `MauiProgram.cs`
- Android emulator: Use `http://10.0.2.2:50242` (not localhost)
- iOS simulator: Use `https://localhost:50242`

## 💾 Session Persistence

The app now includes automatic session persistence:

### How It Works
- **Sign In Once**: Your session is saved to browser localStorage (Web) or device storage (MAUI)
- **Stay Signed In**: Sessions persist for 14 days automatically
- **Survives Refreshes**: Page refreshes don't sign you out
- **Cross-Tab Support**: Sign in once, stay signed in across all tabs
- **Secure**: Session data stored locally, never sent to other sites

### What Gets Stored
- User ID
- Username
- Session ID
- Expiry date (14 days)

### Manual Sign Out
- Use the "Sign Out" button to clear your session
- Session is removed from storage immediately
- All tabs/windows will update

### Privacy & Security
- Data stored locally in your browser/device only
- Not accessible by other websites
- Automatically expires after 14 days
- No sensitive data (passwords) stored

## 🚀 Getting Started

### 1. Initialize the Database

```bash
cd Tools/Podium.DbInit

# Configure Azure Storage (choose one):

# Option A: User Secrets (recommended for dev)
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotting user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-account-key"

# Option B: appsettings.json
# Edit appsettings.json with your credentials

# Run initialization
dotnet run
```

Creates all tables and adds sample data (F1 2025 season with drivers, races, test users).

### 2. Configure & Run the API

```bash
cd ../../Podium/Podium.Api

# Set Azure Storage credentials
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-account-key"

# Run the API
dotnet run
```

API starts at `https://localhost:50242` (check console output for actual port).

### 3. Run the Web App

```bash
cd ../Podium.Web

# Edit wwwroot/appsettings.json if API runs on different port
# {
#   "ApiBaseUrl": "https://localhost:50242"
# }

dotnet run
```

Or run from Visual Studio - set Podium.Web as startup project.

### 4. Run the MAUI App

**From Visual Studio:**
1. Set Podium.Native as startup project
2. Select target platform (Windows, Android, iOS)
3. Update `MauiProgram.cs` if needed:
   - Android emulator: `http://10.0.2.2:50242`
   - iOS simulator: `https://localhost:50242`
   - Physical device: Your computer's IP address
4. Run (F5)

### 5. Test with Sample Accounts

After running DbInit:
- Email: `john@example.com` | Password: `password123`
- Email: `jane@example.com` | Password: `password123`
- Email: `alex@example.com` | Password: `password123`

## ✅ Completed Features

### Core Infrastructure
- ✅ Database schema (13 Azure Tables)
- ✅ Database initialization tool with sample data
- ✅ Complete entity models
- ✅ Repository pattern data access layer
- ✅ RESTful API with authentication
- ✅ Environment-aware configuration (Dev/Prod)
- ✅ Shared service architecture (Web + MAUI)

### Authentication
- ✅ User registration
- ✅ Sign in with password
- ✅ Sign in with email OTP (code logs to console for now)
- ✅ Session management
- ✅ Auth state service
- ✅ **Session persistence** (survives page refreshes and app restarts)

### User Features
- ✅ Home/Landing page
- ✅ Sign In page (dual auth)
- ✅ Sign Up page
- ✅ Sport selection
- ✅ Tier/Competition selection
- ✅ Event selection with dates
- ✅ Prediction submission (select 3 competitors)
- ✅ My Predictions view
- ✅ Leaderboard

### UI/UX
- ✅ Responsive design (mobile & desktop)
- ✅ Modern, clean CSS styling
- ✅ Navigation header with auth state
- ✅ Footer
- ✅ Loading states
- ✅ Error handling
- ✅ Success messages

## 🚦 Future Enhancements

### High Priority
- 📧 Email service integration (real OTP sending)
- 💾 Session persistence (browser/device storage)
- ⚙️ Points calculation engine
- 🛠️ Admin panel (manage sports, events, results)
- 📊 Results display after events

### Nice to Have
- 👤 User profile page
- 📅 Multiple seasons support
- 🔔 Push notifications for events
- 💬 Social features (comments, sharing)
- 📥 Export predictions
- 📚 Historical data/archives
- 🏅 More sports and competitions

## 🛠️ Tech Stack

- **.NET 10** - Latest framework
- **Blazor WebAssembly** - Web UI (runs in browser)
- **.NET MAUI** - Cross-platform mobile/desktop
- **Azure Table Storage** - NoSQL database
- **ASP.NET Core Minimal API** - Secure backend
- **Shared Razor Components** - Write once, run everywhere

## 📡 API Endpoints

### Authentication
- `POST /api/auth/register` - Create account
- `POST /api/auth/signin` - Sign in with password
- `POST /api/auth/send-otp` - Send email verification code
- `POST /api/auth/verify-otp` - Verify OTP and sign in
- `POST /api/auth/validate-session` - Validate session
- `POST /api/auth/signout` - Sign out

### Sports & Competitions
- `GET /api/disciplines` - List all active disciplines
- `GET /api/disciplines/{disciplineId}/series` - Get series for a discipline
- `GET /api/series/{seriesId}/seasons` - Get seasons
- `GET /api/series/{seriesId}/seasons/active` - Get active season
- `GET /api/seasons/{seasonId}/events` - Get all events
- `GET /api/seasons/{seasonId}/events/upcoming` - Get upcoming events
- `GET /api/seasons/{seasonId}/competitors` - Get competitors
- `GET /api/events/{eventId}` - Get event details
- `GET /api/events/{eventId}/result` - Get event result

### Predictions
- `POST /api/predictions` - Submit prediction
- `GET /api/predictions/{eventId}/user/{userId}` - Get user's prediction
- `GET /api/predictions/{eventId}` - Get all predictions for event
- `GET /api/predictions/user/{userId}/season/{seasonId}` - Get user's season predictions

### Leaderboard
- `GET /api/leaderboard/season/{seasonId}` - Get season leaderboard
- `GET /api/leaderboard/season/{seasonId}/user/{userId}` - Get user stats

### Health
- `GET /api/health` - Health check

## 🛠️ Development

### Running Multiple Projects

**Terminal 1 (API):**
```bash
cd Podium/Podium.Api
dotnet watch run
```

**Terminal 2 (Web):**
```bash
cd Podium/Podium.Web
dotnet watch run
```

### CORS Configuration

API allows requests from:
- `https://localhost:7001`
- `https://localhost:5001`
- `http://localhost:5000`
- `https://localhost:7002`
- `http://localhost:5002`

Update `Podium.Api/Program.cs` if your app runs on different ports.

### Android Emulator Setup

The Android emulator can't access `localhost` from the host machine. Use:
- `http://10.0.2.2:50242` - Maps to host's localhost

Update in `Podium.Native/MauiProgram.cs`:
```csharp
ApiBaseUrl = "http://10.0.2.2:50242"
```

### iOS Simulator Setup

iOS simulator can access localhost directly:
```csharp
ApiBaseUrl = "https://localhost:50242"
```

### Build & Deploy

**Build all projects:**
```bash
dotnet build Podium.sln
```

**Publish Web app:**
```bash
cd Podium/Podium.Web
dotnet publish -c Release
```

**Publish MAUI app:**
```bash
cd Podium/Podium.Native
# For Android
dotnet publish -f net10.0-android -c Release
# For iOS
dotnet publish -f net10.0-ios -c Release
# For Windows
dotnet publish -f net10.0-windows10.0.19041.0 -c Release
```

## 📁 Project Structure

```
MyDreamPodium/
├── Docs/
│   └── DatabaseStructure.md        # Complete DB schema
├── Tools/
│   └── Podium.DbInit/              # Database initialization
├── Podium/
│   ├── Podium.Shared/              # Shared UI & logic ⭐
│   │   ├── Models/                 # Entity classes
│   │   ├── Services/
│   │   │   ├── Configuration/     # Environment config
│   │   │   ├── Data/              # Repositories
│   │   │   ├── Auth/              # Authentication
│   │   │   ├── Api/               # API client
│   │   │   └── State/             # State management
│   │   ├── Pages/                  # Razor pages (shared!)
│   │   ├── Layout/                 # App layout
│   │   └── wwwroot/                # CSS & static files
│   ├── Podium.Web/                 # Blazor WebAssembly
│   │   ├── wwwroot/
│   │   │   └── appsettings.json   # API URL config
│   │   └── Program.cs              # App setup
│   ├── Podium.Native/              # .NET MAUI
│   │   └── MauiProgram.cs          # App setup + config
│   └── Podium.Api/                 # Secure backend
│       ├── Endpoints/              # API routes
│       └── Program.cs              # API setup
├── MyPodiumLegacy/                 # Old app (reference)
└── README.md
```

## 💡 Key Design Decisions

### Why Shared Project?
- **Write once, run everywhere** - Same UI for Web and Mobile
- **Consistent UX** - Users get identical experience
- **Less maintenance** - Fix bugs once, deploy everywhere
- **Faster development** - No need to sync features

### Why Minimal API?
- **Lightweight** - No unnecessary overhead
- **Modern** - Uses latest .NET features
- **Fast** - Optimized for performance
- **Simple** - Easy to understand and extend

### Why Azure Table Storage?
- **Cost-effective** - Pay for what you use
- **Scalable** - Handles millions of records
- **No SQL required** - NoSQL flexibility
- **Fast** - Low latency queries
- **Serverless** - No server management

### Why Environment-Aware Config?
- **Security** - Different credentials for dev/prod
- **Flexibility** - Easy to test locally
- **Best practice** - Industry standard approach
- **Safe deployment** - Production credentials never in code

## 🔧 Troubleshooting

### API won't start
- Check Azure Storage credentials are set
- Verify port 50242 is available
- Check console for specific error
- Ensure `dotnet run` is from Podium.Api folder

### Web app can't reach API
- Ensure API is running first
- Check `wwwroot/appsettings.json` has correct API URL
- Verify CORS settings in API `Program.cs`
- Check browser console for errors

### MAUI app can't connect
- Android emulator: Use `http://10.0.2.2:50242`
- iOS simulator: Use `https://localhost:50242`
- Physical device: Use your computer's IP address
- Ensure API allows CORS from all origins (dev only)

### Database empty
- Run `Tools/Podium.DbInit` first
- Check sample data was created
- Verify Azure Storage credentials

### CSS not loading
- CSS is in `Podium.Shared/wwwroot/app.css`
- Blazor serves it as `_content/Podium.Shared/app.css`
- Check browser dev tools Network tab
- Clear browser cache

### OTP not received
- OTP currently logs to API console (development mode)
- Check API terminal output for the code
- Real email service not yet implemented

## 📄 License

[Your license here]

## 🤝 Contributing

This is a learning/portfolio project. Contributions welcome!

---

**Built with ❤️ using .NET 10, Blazor, and MAUI**

**Status: ✅ MVP Complete - Ready for testing!**

