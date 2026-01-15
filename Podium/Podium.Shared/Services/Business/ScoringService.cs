using Podium.Shared.Models;
using Podium.Shared.Services.Data;

namespace Podium.Shared.Services.Business;

public interface IScoringService
{
    Task<int> CalculatePointsAsync(
        string seasonId,
        string predictedP1, string predictedP2, string predictedP3,
        string actualP1, string actualP2, string actualP3);
    
    Task<bool> RecalculateEventPredictionsAsync(string eventId, string seasonId);
}

public class ScoringService : IScoringService
{
    private readonly IScoringRulesRepository _scoringRulesRepo;
    private readonly IPredictionRepository _predictionRepo;
    private readonly IEventRepository _eventRepo;

    public ScoringService(
        IScoringRulesRepository scoringRulesRepo,
        IPredictionRepository predictionRepo,
        IEventRepository eventRepo)
    {
        _scoringRulesRepo = scoringRulesRepo;
        _predictionRepo = predictionRepo;
        _eventRepo = eventRepo;
    }

    public async Task<int> CalculatePointsAsync(
        string seasonId,
        string predictedP1, string predictedP2, string predictedP3,
        string actualP1, string actualP2, string actualP3)
    {
        // Get scoring rules for the season
        var scoringRules = await _scoringRulesRepo.GetScoringRulesBySeasonAsync(seasonId);
        
        // Use default values if scoring rules don't exist (backward compatibility)
        int exactMatchPoints = scoringRules?.ExactMatchPoints ?? 25;
        int oneOffPoints = scoringRules?.OneOffPoints ?? 18;
        int twoOffPoints = scoringRules?.TwoOffPoints ?? 15;

        // Normalize names for comparison
        var pred = new[] { 
            predictedP1?.Trim() ?? "", 
            predictedP2?.Trim() ?? "", 
            predictedP3?.Trim() ?? "" 
        };
        var actual = new[] { 
            actualP1?.Trim() ?? "", 
            actualP2?.Trim() ?? "", 
            actualP3?.Trim() ?? "" 
        };

        // Check for exact match (all 3 in correct positions)
        if (string.Equals(pred[0], actual[0], StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pred[1], actual[1], StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pred[2], actual[2], StringComparison.OrdinalIgnoreCase))
        {
            return exactMatchPoints;
        }

        // Check if all predicted drivers are in actual top 3 (positions off logic)
        var predSet = new HashSet<string>(pred.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
        var actualSet = new HashSet<string>(actual.Where(a => !string.IsNullOrWhiteSpace(a)), StringComparer.OrdinalIgnoreCase);
        
        // Count how many predicted drivers are in actual results
        int correctDrivers = predSet.Intersect(actualSet).Count();
        
        if (correctDrivers == 3)
        {
            // All 3 drivers correct, just positions off
            return oneOffPoints;
        }
        else if (correctDrivers == 2)
        {
            // 2 drivers correct
            return twoOffPoints;
        }

        // Less than 2 correct drivers = 0 points
        return 0;
    }

    public async Task<bool> RecalculateEventPredictionsAsync(string eventId, string seasonId)
    {
        try
        {
            // Get event result
            var result = await _eventRepo.GetEventResultAsync(eventId);
            if (result == null)
            {
                // No result yet, nothing to calculate
                return true;
            }

            // Get all predictions for this event
            var predictions = await _predictionRepo.GetPredictionsByEventAsync(eventId);
            
            // Recalculate points for each prediction
            foreach (var prediction in predictions)
            {
                var points = await CalculatePointsAsync(
                    seasonId,
                    prediction.FirstPlaceName,
                    prediction.SecondPlaceName,
                    prediction.ThirdPlaceName,
                    result.FirstPlaceName,
                    result.SecondPlaceName,
                    result.ThirdPlaceName
                );

                prediction.PointsEarned = points;
                await _predictionRepo.UpdatePredictionAsync(prediction);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
