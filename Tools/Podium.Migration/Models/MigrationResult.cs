namespace Podium.Migration.Models;

/// <summary>
/// Represents migration results and statistics
/// </summary>
public class MigrationResult
{
    public int UsersCreated { get; set; }
    public int DriversCreated { get; set; }
    public int SeasonsCreated { get; set; }
    public int EventsCreated { get; set; }
    public int PredictionsMigrated { get; set; }
    public int EventResultsCreated { get; set; }
    public int UserStatisticsCreated { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public void AddError(string error)
    {
        Errors.Add($"[{DateTime.UtcNow:HH:mm:ss}] {error}");
    }

    public void AddWarning(string warning)
    {
        Warnings.Add($"[{DateTime.UtcNow:HH:mm:ss}] {warning}");
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n========== MIGRATION SUMMARY ==========");
        Console.WriteLine($"Users Created: {UsersCreated}");
        Console.WriteLine($"Drivers Created: {DriversCreated}");
        Console.WriteLine($"Seasons Created: {SeasonsCreated}");
        Console.WriteLine($"Events Created: {EventsCreated}");
        Console.WriteLine($"Predictions Migrated: {PredictionsMigrated}");
        Console.WriteLine($"Event Results Created: {EventResultsCreated}");
        Console.WriteLine($"User Statistics Created: {UserStatisticsCreated}");
        Console.WriteLine($"Errors: {Errors.Count}");
        Console.WriteLine($"Warnings: {Warnings.Count}");
        
        if (Warnings.Any())
        {
            Console.WriteLine("\n--- Warnings ---");
            foreach (var warning in Warnings)
            {
                Console.WriteLine($"? {warning}");
            }
        }
        
        if (Errors.Any())
        {
            Console.WriteLine("\n--- Errors ---");
            foreach (var error in Errors)
            {
                Console.WriteLine($"? {error}");
            }
        }
        
        Console.WriteLine("=======================================\n");
    }
}
