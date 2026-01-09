using Microsoft.Extensions.Configuration;
using Podium.Migration.Services;

namespace Podium.Migration;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    F1 Historical Data Migration Tool                  ║");
        Console.WriteLine("║    MyPodium → Podium Database Migration               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<Program>(optional: true)
                .Build();

            var storageUri = configuration["AzureStorage:StorageUri"];
            var accountName = configuration["AzureStorage:AccountName"];
            var accountKey = configuration["AzureStorage:AccountKey"];

            if (string.IsNullOrEmpty(storageUri) || string.IsNullOrEmpty(accountName) || string.IsNullOrEmpty(accountKey))
            {
                Console.WriteLine("❌ ERROR: Azure Storage connection information is missing!");
                Console.WriteLine("Please configure AzureStorage settings in appsettings.json or user secrets.");
                return;
            }

            var season2024 = bool.TryParse(configuration["Migration:Season2024"], out var s2024) ? s2024 : true;
            var season2025 = bool.TryParse(configuration["Migration:Season2025"], out var s2025) ? s2025 : true;
            var dryRun = bool.TryParse(configuration["Migration:DryRun"], out var dr) ? dr : false;

            Console.WriteLine($"📊 Configuration:");
            Console.WriteLine($"   Storage Account: {accountName}");
            Console.WriteLine($"   Migrate 2024 Season: {season2024}");
            Console.WriteLine($"   Migrate 2025 Season: {season2025}");
            Console.WriteLine($"   Dry Run: {dryRun}");
            Console.WriteLine();

            if (dryRun)
            {
                Console.WriteLine("⚠️  DRY RUN MODE - No data will be written");
                Console.WriteLine();
            }
            else
            {
                Console.Write("⚠️  This will migrate data to the Podium tables. Continue? (yes/no): ");
                var confirm = Console.ReadLine();
                if (confirm?.ToLower() != "yes")
                {
                    Console.WriteLine("Migration cancelled.");
                    return;
                }
                Console.WriteLine();
            }

            // Initialize services
            var extractor = new LegacyDataExtractor(storageUri, accountName, accountKey);
            var transformer = new DataTransformer();
            var inserter = new PodiumDataInserter(storageUri, accountName, accountKey, dryRun);
            var orchestrator = new MigrationOrchestrator(extractor, transformer, inserter, season2024, season2025);

            // Execute migration
            var startTime = DateTime.UtcNow;
            var result = await orchestrator.ExecuteMigrationAsync();
            var duration = DateTime.UtcNow - startTime;

            // Print results
            result.PrintSummary();
            Console.WriteLine($"⏱️  Migration took: {duration.TotalSeconds:F2} seconds");
            
            if (result.Errors.Count == 0)
            {
                Console.WriteLine("\n✅ Migration completed successfully!");
            }
            else
            {
                Console.WriteLine("\n⚠️  Migration completed with errors. Please review the log above.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ CRITICAL ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
