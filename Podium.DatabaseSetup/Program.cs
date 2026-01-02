using Azure.Data.Tables;
using System.Diagnostics;

namespace Podium.DatabaseSetup;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("Podium Database Setup Tool");
        Console.WriteLine("========================================");
        Console.WriteLine();

        // Get connection string from environment variable or command line
        string? connectionString = Environment.GetEnvironmentVariable("PODIUM_STORAGE_CONNECTION");
        
        if (string.IsNullOrEmpty(connectionString) && args.Length > 0)
        {
            connectionString = args[0];
        }

        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("ERROR: No connection string provided.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Set environment variable: PODIUM_STORAGE_CONNECTION");
            Console.WriteLine("  Or pass as argument: dotnet run \"<connection-string>\"");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run \"DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net\"");
            return;
        }

        Console.WriteLine($"Connection string configured: {MaskConnectionString(connectionString)}");
        Console.WriteLine();

        var setupManager = new DatabaseSetupManager(connectionString);

        try
        {
            Console.WriteLine("Step 1: Creating tables...");
            await setupManager.CreateAllTablesAsync();
            Console.WriteLine("✓ All tables created successfully");
            Console.WriteLine();

            Console.WriteLine("Step 2: Seeding data...");
            await setupManager.SeedDataAsync();
            Console.WriteLine("✓ Seed data inserted successfully");
            Console.WriteLine();

            Console.WriteLine("========================================");
            Console.WriteLine("Database setup completed successfully!");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("ERROR: Database setup failed!");
            Console.WriteLine($"Message: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }

    static string MaskConnectionString(string connectionString)
    {
        // Mask the AccountKey for security
        var parts = connectionString.Split(';');
        var masked = parts.Select(part =>
        {
            if (part.StartsWith("AccountKey=", StringComparison.OrdinalIgnoreCase))
            {
                return "AccountKey=***MASKED***";
            }
            return part;
        });
        return string.Join(";", masked);
    }
}
