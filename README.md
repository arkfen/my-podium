# Podium - Multi-Sport Prediction Platform

A modern, flexible prediction platform that supports multiple sports and competitions. Built with .NET 10, Blazor, and Azure Table Storage.

## 🏗️ Architecture

The platform consists of three main projects:

### 1. **Podium.Shared** - Core Business Logic
- Blazor Razor Class Library (.NET 10)
- Contains all business logic, services, models, and reusable components
- Platform-agnostic, can be used by web, desktop, and mobile apps

**Key Features:**
- Multi-sport support (Motorsport, Football, Basketball, etc.)
- Hierarchical competition structure (Sport → Tier → Season → Event)
- Dual authentication (Email OTP + Password)
- Real-time prediction management with time-based cutoffs
- Points calculation and leaderboards
- Azure Table Storage integration

### 2. **Podium.Web** - Blazor WebAssembly
- Blazor WebAssembly application (.NET 10)
- Progressive Web App (PWA) capable
- Responsive design for mobile and desktop browsers
- References Podium.Shared for all functionality

### 3. **Podium.Native** - .NET MAUI (Cross-Platform)
- .NET MAUI application (.NET 10)
- Targets iOS, Android, Windows, and macOS
- References Podium.Shared for all functionality
- **Note:** Requires Windows or macOS for development and testing

### 4. **Podium.DatabaseSetup** - Database Initialization
- Console application for one-time database setup
- Creates all required Azure Table Storage tables
- Seeds test data (sports, competitors, events, users)
- See [Podium.DatabaseSetup/README.md](Podium.DatabaseSetup/README.md) for usage

## 📊 Database Structure

The platform uses Azure Table Storage with 12 tables:

- **PodiumSports** - Sports categories
- **PodiumTiers** - Competition tiers within sports (e.g., F1, F2, F3)
- **PodiumSeasons** - Seasons per tier/competition
- **PodiumEvents** - Individual events/races
- **PodiumCompetitors** - Athletes/drivers/teams
- **PodiumSeasonParticipants** - Links competitors to seasons
- **PodiumEventResults** - Top 3 finishers per event
- **PodiumUsers** - User accounts with secure password hashing
- **PodiumUserPredictions** - User predictions for events
- **PodiumPointsConfig** - Points configuration per season
- **PodiumAuthSessions** - Active authentication sessions
- **PodiumOTPCodes** - One-time password codes for email auth

See [docs/DATABASE_STRUCTURE.md](docs/DATABASE_STRUCTURE.md) for detailed schema documentation.

## 🚀 Getting Started

### Prerequisites

- .NET 10.0 SDK
- Azure Storage Account or Azurite (Azure Storage Emulator)
- For MAUI: Visual Studio 2022 with MAUI workload (Windows or macOS)

### Setup Steps

#### 1. Clone the Repository

```bash
git clone https://github.com/arkfen/my-podium.git
cd my-podium
```

#### 2. Set Up Azure Storage

**Option A: Use Azure Storage Emulator (Azurite) - Recommended for Development**

Install Azurite:
```bash
npm install -g azurite
```

Start Azurite:
```bash
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

Use the default connection string: `UseDevelopmentStorage=true`

**Option B: Use Azure Storage Account**

1. Create an Azure Storage Account in the Azure Portal
2. Get the connection string from "Access keys"

#### 3. Create and Seed the Database

```bash
cd Podium.DatabaseSetup

# Using environment variable (recommended)
export PODIUM_STORAGE_CONNECTION="UseDevelopmentStorage=true"
# or for Azure Storage:
# export PODIUM_STORAGE_CONNECTION="DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"

dotnet run
```

This creates all tables and seeds test data including:
- Motorsport with F1, F2, F3 tiers
- 2025 and 2026 seasons
- 10 sample F1 drivers
- 5 upcoming races
- 3 test users (test@example.com, demo@example.com, admin@example.com - all with password: password123, demo123, admin123)

#### 4. Run the Blazor WebAssembly App

```bash
cd ../Podium.Web

# Update appsettings.json with your connection string if not using default
# Edit wwwroot/appsettings.json

dotnet run
```

Navigate to `https://localhost:5001` (or the URL shown in the console)

#### 5. (Optional) Run the MAUI App

**Note:** Requires Windows or macOS with Visual Studio 2022

```bash
cd ../Podium.Native

# For Android
dotnet build -t:Run -f net10.0-android

# For Windows
dotnet build -t:Run -f net10.0-windows10.0.19041.0

# For iOS/macOS - requires macOS
dotnet build -t:Run -f net10.0-ios
dotnet build -t:Run -f net10.0-maccatalyst
```

## 🎮 Using the Application

### Sign In

1. Navigate to the home page
2. Choose authentication method:
   - **Email Code**: Enter email, receive 4-digit code, verify
   - **Password**: Enter email and password directly

Test accounts:
- test@example.com / password123
- demo@example.com / demo123
- admin@example.com / admin123

### Make Predictions

1. Select a sport (e.g., Motorsport)
2. Select a tier/competition (e.g., Formula 1)
3. Select a season (e.g., 2025)
4. Select an event (only open events allow predictions)
5. Choose your top 3 finishers
6. Submit prediction

**Note:** Predictions automatically close 15 minutes before the event starts.

### View Results

1. Navigate to Results page
2. Select sport → tier → season
3. View your predictions vs. actual results
4. See points earned per event

### Check Leaderboards

1. Navigate to Leaderboards page
2. Select sport → tier → season
3. View rankings with points and events predicted
4. Your position is highlighted

## 🔐 Security Features

- **Password Hashing**: BCrypt with work factor 10
- **OTP Codes**: 4-digit codes, 10-minute expiry, single-use
- **Session Management**: 14-day expiry, server-side validation
- **Input Validation**: All user inputs sanitized
- **Secure Communication**: HTTPS enforced in production

## 📁 Project Structure

```
my-podium/
├── docs/
│   └── DATABASE_STRUCTURE.md          # Comprehensive database documentation
├── Podium.Shared/                      # Core business logic library
│   ├── Models/                         # Data models
│   ├── Services/                       # Business services
│   │   ├── TableStorageService.cs
│   │   ├── AuthenticationService.cs
│   │   ├── SportService.cs
│   │   ├── EventService.cs
│   │   ├── PredictionService.cs
│   │   ├── ResultsService.cs
│   │   └── LeaderboardService.cs
│   └── Components/                     # Reusable Blazor components
│       ├── Auth/
│       ├── Predictions/
│       └── Leaderboard/
├── Podium.Web/                         # Blazor WebAssembly app
│   ├── Pages/                          # Application pages
│   │   ├── Home.razor
│   │   ├── Predictions.razor
│   │   └── Leaderboard.razor
│   ├── wwwroot/
│   │   └── appsettings.json           # Configuration
│   └── Program.cs                      # DI and service registration
├── Podium.Native/                      # .NET MAUI app
│   └── Podium.Native.csproj           # Multi-platform configuration
├── Podium.DatabaseSetup/               # Database initialization tool
│   ├── Program.cs
│   ├── DatabaseSetupManager.cs
│   └── README.md
└── MyPodium/                           # Legacy F1-focused app (reference only)
```

## 🎯 Key Features

### Implemented ✅
- Multi-sport architecture with hierarchical organization
- Dual authentication (Email OTP + Password)
- Sport/Tier/Season/Event selection flow
- Prediction entry with time-based cutoffs
- Points calculation (10/5/3/1 system)
- Season and event leaderboards
- Responsive UI with Bootstrap 5
- Comprehensive database with seed data

### Future Enhancements 🚧
- Admin panel for managing sports, events, and results
- Automated points calculation engine
- Email notifications for upcoming events
- Social features (friends, private leagues)
- Advanced statistics and analytics
- Mobile app push notifications

## 🧪 Development

### Building the Solution

```bash
# Build all projects
dotnet build

# Build specific project
dotnet build Podium.Shared/Podium.Shared.csproj
dotnet build Podium.Web/Podium.Web.csproj
```

### Running Tests

```bash
# Run all tests (when added)
dotnet test
```

### Code Style

- Follow standard .NET conventions
- Use nullable reference types (`<Nullable>enable</Nullable>`)
- Document public APIs with XML comments
- Use async/await for all I/O operations

## 📝 Configuration

### Podium.Web (appsettings.json)

```json
{
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  }
}
```

### Environment Variables

- `PODIUM_STORAGE_CONNECTION`: Azure Storage connection string (for DatabaseSetup)

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is private. All rights reserved.

## 🙋 Support

For questions or issues, please open a GitHub issue.

## 🏆 Credits

Built with ❤️ using:
- .NET 10
- Blazor WebAssembly
- .NET MAUI
- Azure Table Storage
- Bootstrap 5
- BCrypt.Net

---

**Note:** The legacy MyPodium application in the `MyPodium/` directory is kept for reference only. The new architecture (Podium.*) is the active development target.
