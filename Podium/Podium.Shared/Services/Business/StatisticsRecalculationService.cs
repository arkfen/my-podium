using Podium.Shared.Models;
using Podium.Shared.Services.Data;

namespace Podium.Shared.Services.Business;

public interface IStatisticsRecalculationService
{
    Task<string> StartRecalculationAsync(string seasonId);
    Task<StatisticsRecalculationJob?> GetJobStatusAsync(string jobId);
}

public class StatisticsRecalculationService : IStatisticsRecalculationService
{
    private readonly IStatisticsJobRepository _jobRepository;
    private readonly IPredictionRepository _predictionRepository;
    private readonly ILeaderboardRepository _leaderboardRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IScoringRulesRepository _scoringRulesRepository;
    private readonly ISeasonRepository _seasonRepository;

    public StatisticsRecalculationService(
        IStatisticsJobRepository jobRepository,
        IPredictionRepository predictionRepository,
        ILeaderboardRepository leaderboardRepository,
        IEventRepository eventRepository,
        IUserRepository userRepository,
        IScoringRulesRepository scoringRulesRepository,
        ISeasonRepository seasonRepository)
    {
        _jobRepository = jobRepository;
        _predictionRepository = predictionRepository;
        _leaderboardRepository = leaderboardRepository;
        _eventRepository = eventRepository;
        _userRepository = userRepository;
        _scoringRulesRepository = scoringRulesRepository;
        _seasonRepository = seasonRepository;
    }

    public async Task<string> StartRecalculationAsync(string seasonId)
    {
        // Create a new job
        var jobId = Guid.NewGuid().ToString();
        var job = new StatisticsRecalculationJob
        {
            JobId = jobId,
            SeasonId = seasonId,
            Status = "Running",
            TotalUsers = 0,
            ProcessedUsers = 0,
            StartedAt = DateTime.UtcNow
        };

        await _jobRepository.SaveJobAsync(job);

        // Start background processing (fire and forget)
        _ = Task.Run(async () => await ProcessRecalculationAsync(jobId, seasonId));

        return jobId;
    }

    public async Task<StatisticsRecalculationJob?> GetJobStatusAsync(string jobId)
    {
        return await _jobRepository.GetJobAsync(jobId);
    }

    private async Task ProcessRecalculationAsync(string jobId, string seasonId)
    {
        try
        {
            // Get the season to access BestResultsNumber
            var season = await _seasonRepository.GetSeasonByIdOnlyAsync(seasonId);
            int? bestResultsNumber = season?.BestResultsNumber;

            // Get all events for the season
            var events = await _eventRepository.GetEventsBySeasonAsync(seasonId);
            var eventIds = events.Select(e => e.Id).ToList();

            // Get all predictions for the season (only those with calculated points)
            var allPredictions = await _predictionRepository.GetAllPredictionsForSeasonAsync(seasonId, eventIds);
            var predictionsWithPoints = allPredictions.Where(p => p.PointsEarned.HasValue).ToList();

            // Group by user
            var predictionsByUser = predictionsWithPoints.GroupBy(p => p.UserId).ToList();

            // Get scoring rules for calculating match types
            var scoringRules = await _scoringRulesRepository.GetScoringRulesBySeasonAsync(seasonId);
            int exactMatchPoints = scoringRules?.ExactMatchPoints ?? 25;
            int oneOffPoints = scoringRules?.OneOffPoints ?? 18;
            int twoOffPoints = scoringRules?.TwoOffPoints ?? 15;

            // Update job with total users count
            var job = await _jobRepository.GetJobAsync(jobId);
            if (job != null)
            {
                job.TotalUsers = predictionsByUser.Count;
                await _jobRepository.UpdateJobAsync(job);
            }

            // Process each user
            int processedCount = 0;
            foreach (var userGroup in predictionsByUser)
            {
                var userId = userGroup.Key;
                var userPredictions = userGroup.ToList();

                // Get user info
                var user = await _userRepository.GetUserByIdAsync(userId);
                if (user == null) continue;

                // Calculate statistics
                int totalPoints = 0;
                int exactMatches = 0;
                int oneOffMatches = 0;
                int twoOffMatches = 0;
                var pointsPerPrediction = new List<int>();

                foreach (var prediction in userPredictions)
                {
                    if (!prediction.PointsEarned.HasValue) continue;

                    int pts = prediction.PointsEarned.Value;
                    totalPoints += pts;
                    pointsPerPrediction.Add(pts);

                    // Get the event result to calculate match types per driver
                    var eventId = prediction.EventId;
                    var eventResult = await _eventRepository.GetEventResultAsync(eventId);
                    
                    if (eventResult != null)
                    {
                        // Count matches per driver position
                        var matches = CalculateMatchesPerDriver(
                            prediction.FirstPlaceName, prediction.SecondPlaceName, prediction.ThirdPlaceName,
                            eventResult.FirstPlaceName, eventResult.SecondPlaceName, eventResult.ThirdPlaceName);
                        
                        exactMatches += matches.ExactMatches;
                        oneOffMatches += matches.OneOffMatches;
                        twoOffMatches += matches.TwoOffMatches;
                    }
                }

                // Calculate BestResultsPoints: sum of top N results if configured, otherwise leave as null
                int? bestResultsPoints = null;
                if (bestResultsNumber.HasValue && bestResultsNumber.Value > 0)
                {
                    bestResultsPoints = pointsPerPrediction
                        .OrderByDescending(p => p)
                        .Take(bestResultsNumber.Value)
                        .Sum();
                }
                // If no BestResultsNumber set, leave bestResultsPoints as null
                // UI and sorting will fall back to TotalPoints

                // Create/update user statistics
                var userStats = new UserStatistics
                {
                    SeasonId = seasonId,
                    UserId = userId,
                    Username = user.Username,
                    TotalPoints = totalPoints,
                    BestResultsPoints = bestResultsPoints,
                    PredictionsCount = userPredictions.Count,
                    ExactMatches = exactMatches,
                    OneOffMatches = oneOffMatches,
                    TwoOffMatches = twoOffMatches,
                    LastUpdated = DateTime.UtcNow
                };

                await _leaderboardRepository.UpsertUserStatisticsAsync(userStats);

                // Update progress
                processedCount++;
                job = await _jobRepository.GetJobAsync(jobId);
                if (job != null)
                {
                    job.ProcessedUsers = processedCount;
                    await _jobRepository.UpdateJobAsync(job);
                }
            }

            // Mark job as completed
            job = await _jobRepository.GetJobAsync(jobId);
            if (job != null)
            {
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
                await _jobRepository.UpdateJobAsync(job);
            }
        }
        catch (Exception ex)
        {
            // Mark job as failed
            var job = await _jobRepository.GetJobAsync(jobId);
            if (job != null)
            {
                job.Status = "Failed";
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                await _jobRepository.UpdateJobAsync(job);
            }
        }
    }

    private (int ExactMatches, int OneOffMatches, int TwoOffMatches) CalculateMatchesPerDriver(
        string predictedP1, string predictedP2, string predictedP3,
        string actualP1, string actualP2, string actualP3)
    {
        int exactMatches = 0;
        int oneOffMatches = 0;
        int twoOffMatches = 0;

        // Normalize names for comparison
        var predicted = new[] { 
            predictedP1?.Trim() ?? "", 
            predictedP2?.Trim() ?? "", 
            predictedP3?.Trim() ?? "" 
        };
        var actual = new[] { 
            actualP1?.Trim() ?? "", 
            actualP2?.Trim() ?? "", 
            actualP3?.Trim() ?? "" 
        };

        // Check each predicted driver
        for (int predPos = 0; predPos < 3; predPos++)
        {
            if (string.IsNullOrWhiteSpace(predicted[predPos]))
                continue;

            // Find where this driver actually finished
            int actualPos = -1;
            for (int i = 0; i < 3; i++)
            {
                if (string.Equals(predicted[predPos], actual[i], StringComparison.OrdinalIgnoreCase))
                {
                    actualPos = i;
                    break;
                }
            }

            // If driver not in podium, no match
            if (actualPos == -1)
                continue;

            // Calculate position difference
            int positionDiff = Math.Abs(predPos - actualPos);

            // Count match type based on position accuracy
            if (positionDiff == 0)
            {
                exactMatches++;
            }
            else if (positionDiff == 1)
            {
                oneOffMatches++;
            }
            else if (positionDiff == 2)
            {
                twoOffMatches++;
            }
        }

        return (exactMatches, oneOffMatches, twoOffMatches);
    }
}
