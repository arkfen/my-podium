using System.Net.Http.Json;
using Podium.Shared.Models;

namespace Podium.Shared.Services.Api;

public interface IPodiumApiClient
{
    // Authentication
    Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string username, string password, string preferredAuthMethod);
    Task<ApiResponse<MessageResponse>> SendOtpAsync(string email);
    Task<ApiResponse<AuthResponse>> VerifyOtpAsync(string email, string otpCode);
    Task<ApiResponse<AuthResponse>> SignInAsync(string email, string password);
    Task<ApiResponse<AuthResponse>> ValidateSessionAsync(string sessionId);
    Task<ApiResponse<MessageResponse>> SignOutAsync(string sessionId);

    // Sports & Competitions
    Task<ApiResponse<List<Sport>>> GetSportsAsync();
    Task<ApiResponse<List<Tier>>> GetTiersAsync(string sportId);
    Task<ApiResponse<List<Season>>> GetSeasonsAsync(string tierId);
    Task<ApiResponse<Season>> GetActiveSeasonAsync(string tierId);
    Task<ApiResponse<List<Event>>> GetEventsAsync(string seasonId);
    Task<ApiResponse<List<Event>>> GetUpcomingEventsAsync(string seasonId);
    Task<ApiResponse<List<SeasonCompetitor>>> GetCompetitorsAsync(string seasonId);
    Task<ApiResponse<Event>> GetEventDetailsAsync(string eventId, string seasonId);
    Task<ApiResponse<EventResult>> GetEventResultAsync(string eventId);

    // Predictions
    Task<ApiResponse<Prediction>> GetPredictionAsync(string eventId, string userId);
    Task<ApiResponse<List<Prediction>>> GetEventPredictionsAsync(string eventId);
    Task<ApiResponse<List<Prediction>>> GetUserSeasonPredictionsAsync(string userId, string seasonId);
    Task<ApiResponse<PredictionResponse>> SubmitPredictionAsync(SubmitPredictionRequest request);

    // Leaderboard
    Task<ApiResponse<List<UserStatistics>>> GetLeaderboardAsync(string seasonId);
    Task<ApiResponse<UserStatistics>> GetUserStatisticsAsync(string seasonId, string userId);
}

public class PodiumApiClient : IPodiumApiClient
{
    private readonly HttpClient _httpClient;

    public PodiumApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Authentication
    public async Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string username, string password, string preferredAuthMethod)
    {
        return await PostAsync<RegisterResponse>("/api/auth/register", 
            new { email, username, password, preferredAuthMethod });
    }

    public async Task<ApiResponse<MessageResponse>> SendOtpAsync(string email)
    {
        return await PostAsync<MessageResponse>("/api/auth/send-otp", new { email });
    }

    public async Task<ApiResponse<AuthResponse>> VerifyOtpAsync(string email, string otpCode)
    {
        return await PostAsync<AuthResponse>("/api/auth/verify-otp", new { email, otpCode });
    }

    public async Task<ApiResponse<AuthResponse>> SignInAsync(string email, string password)
    {
        return await PostAsync<AuthResponse>("/api/auth/signin", new { email, password });
    }

    public async Task<ApiResponse<AuthResponse>> ValidateSessionAsync(string sessionId)
    {
        return await PostAsync<AuthResponse>("/api/auth/validate-session", new { sessionId });
    }

    public async Task<ApiResponse<MessageResponse>> SignOutAsync(string sessionId)
    {
        return await PostAsync<MessageResponse>("/api/auth/signout", new { sessionId });
    }

    // Sports & Competitions
    public async Task<ApiResponse<List<Sport>>> GetSportsAsync()
    {
        return await GetAsync<List<Sport>>("/api/sports");
    }

    public async Task<ApiResponse<List<Tier>>> GetTiersAsync(string sportId)
    {
        return await GetAsync<List<Tier>>($"/api/sports/{sportId}/tiers");
    }

    public async Task<ApiResponse<List<Season>>> GetSeasonsAsync(string tierId)
    {
        return await GetAsync<List<Season>>($"/api/tiers/{tierId}/seasons");
    }

    public async Task<ApiResponse<Season>> GetActiveSeasonAsync(string tierId)
    {
        return await GetAsync<Season>($"/api/tiers/{tierId}/seasons/active");
    }

    public async Task<ApiResponse<List<Event>>> GetEventsAsync(string seasonId)
    {
        return await GetAsync<List<Event>>($"/api/seasons/{seasonId}/events");
    }

    public async Task<ApiResponse<List<Event>>> GetUpcomingEventsAsync(string seasonId)
    {
        return await GetAsync<List<Event>>($"/api/seasons/{seasonId}/events/upcoming");
    }

    public async Task<ApiResponse<List<SeasonCompetitor>>> GetCompetitorsAsync(string seasonId)
    {
        return await GetAsync<List<SeasonCompetitor>>($"/api/seasons/{seasonId}/competitors");
    }

    public async Task<ApiResponse<Event>> GetEventDetailsAsync(string eventId, string seasonId)
    {
        return await GetAsync<Event>($"/api/events/{eventId}?seasonId={seasonId}");
    }

    public async Task<ApiResponse<EventResult>> GetEventResultAsync(string eventId)
    {
        return await GetAsync<EventResult>($"/api/events/{eventId}/result");
    }

    // Predictions
    public async Task<ApiResponse<Prediction>> GetPredictionAsync(string eventId, string userId)
    {
        return await GetAsync<Prediction>($"/api/predictions/{eventId}/user/{userId}");
    }

    public async Task<ApiResponse<List<Prediction>>> GetEventPredictionsAsync(string eventId)
    {
        return await GetAsync<List<Prediction>>($"/api/predictions/{eventId}");
    }

    public async Task<ApiResponse<List<Prediction>>> GetUserSeasonPredictionsAsync(string userId, string seasonId)
    {
        return await GetAsync<List<Prediction>>($"/api/predictions/user/{userId}/season/{seasonId}");
    }

    public async Task<ApiResponse<PredictionResponse>> SubmitPredictionAsync(SubmitPredictionRequest request)
    {
        return await PostAsync<PredictionResponse>("/api/predictions", request);
    }

    // Leaderboard
    public async Task<ApiResponse<List<UserStatistics>>> GetLeaderboardAsync(string seasonId)
    {
        return await GetAsync<List<UserStatistics>>($"/api/leaderboard/season/{seasonId}");
    }

    public async Task<ApiResponse<UserStatistics>> GetUserStatisticsAsync(string seasonId, string userId)
    {
        return await GetAsync<UserStatistics>($"/api/leaderboard/season/{seasonId}/user/{userId}");
    }

    // Helper methods
    private async Task<ApiResponse<T>> GetAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<T>();
                return new ApiResponse<T> { Success = true, Data = data };
            }

            var error = await response.Content.ReadAsStringAsync();
            return new ApiResponse<T> { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiResponse<T>> PostAsync<T>(string url, object data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, data);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>();
                return new ApiResponse<T> { Success = true, Data = result };
            }

            var error = await response.Content.ReadAsStringAsync();
            return new ApiResponse<T> { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T> { Success = false, Error = ex.Message };
        }
    }
}

// Response DTOs
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}

public record RegisterResponse(string UserId, string Message);
public record MessageResponse(string Message);
public record AuthResponse(string UserId, string Username, string Message);
public record PredictionResponse(string Message, Prediction Prediction);

public record SubmitPredictionRequest(
    string EventId,
    string SeasonId,
    string UserId,
    string FirstPlaceId,
    string FirstPlaceName,
    string SecondPlaceId,
    string SecondPlaceName,
    string ThirdPlaceId,
    string ThirdPlaceName
);
