# Quick Start Guide - Podium Platform

## ? 5-Minute Setup

### Step 1: Initialize Database (1 min)
```bash
cd Tools/Podium.DbInit
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-key"
dotnet run
```

### Step 2: Start API (30 sec)
```bash
cd ../../Podium/Podium.Api
dotnet user-secrets set "AzureStorage:StorageUri" "https://youraccount.table.core.windows.net/"
dotnet user-secrets set "AzureStorage:AccountName" "youraccount"
dotnet user-secrets set "AzureStorage:AccountKey" "your-key"
dotnet run
```
? Note the port (e.g., https://localhost:50242)

### Step 3: Update Web Config (30 sec)
Edit `Podium/Podium.Web/wwwroot/appsettings.json`:
```json
{
  "ApiBaseUrl": "https://localhost:50242"
}
```

### Step 4: Start Web App (30 sec)
```bash
cd ../Podium.Web
dotnet run
```

### Step 5: Test! (2 min)
1. Open browser to web app URL (shown in console)
2. Click "Sign In"
3. Use test account:
   - Email: `john@example.com`
   - Password: `John's password`
4. Browse Sports ? Formula 1 ? Events
5. Make a prediction!

## ?? You're Done!

**What you have:**
- ? Full database with F1 2025 season
- ? Secure API running
- ? Web app connected
- ? 3 test accounts ready
- ? Sample data to play with

## ?? Want to test MAUI too?

### For Windows:
1. Open `Podium.sln` in Visual Studio
2. Right-click `Podium.Native` ? Set as Startup Project
3. Select "Windows Machine" from debug target
4. Press F5

### For Android Emulator:
1. Open `Podium.Native/MauiProgram.cs`
2. Change API URL to: `http://10.0.2.2:50242`
3. Select Android Emulator from debug target
4. Press F5

## ?? Something not working?

### API won't start?
- Check you set all 3 secrets (StorageUri, AccountName, AccountKey)
- Run: `dotnet user-secrets list` to verify

### Web app shows errors?
- Make sure API is running first
- Check API URL in `appsettings.json` matches API console output
- Look for CORS errors in browser console

### Can't sign in?
- Make sure you ran DbInit first
- Check API console logs for errors
- Try creating a new account (Sign Up)

### OTP code not working?
- OTP is logged to API console (not email yet)
- Check API terminal for the 6-digit code
- Code expires in 10 minutes

## ?? Need more help?

See full `README.md` for:
- Complete architecture explanation
- All API endpoints
- Troubleshooting guide
- Development tips
- MAUI deployment guide

## ?? Next Steps

Now that it's running:
1. **Explore the UI** - All pages are implemented
2. **Make predictions** - Try the full flow
3. **Check leaderboard** - See how you rank
4. **Customize** - Add your own sports/events via DbInit
5. **Deploy** - Ready for production!

---

**Need immediate help?** Check the API console and browser console for error messages!
