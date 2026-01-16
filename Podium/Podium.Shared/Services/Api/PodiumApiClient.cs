using System.Net.Http.Json;
using System.Text.Json;
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

    // Disciplines & Competitions
    Task<ApiResponse<List<Discipline>>> GetDisciplinesAsync();
    Task<ApiResponse<List<Series>>> GetSeriesAsync(string disciplineId);
    Task<ApiResponse<List<Season>>> GetSeasonsAsync(string seriesId);
    Task<ApiResponse<Season>> GetActiveSeasonAsync(string seriesId);
    Task<ApiResponse<List<Event>>> GetEventsAsync(string seasonId);
    Task<ApiResponse<List<Event>>> GetUpcomingEventsAsync(string seasonId);
    Task<ApiResponse<List<SeasonCompetitor>>> GetCompetitorsAsync(string seasonId);
    Task<ApiResponse<Event>> GetEventDetailsAsync(string eventId, string seasonId);
    Task<ApiResponse<EventResult>> GetEventResultAsync(string eventId);

    // Predictions
    Task<ApiResponse<Prediction>> GetPredictionAsync(string eventId, string userId);
    Task<ApiResponse<List<Prediction>>> GetEventPredictionsAsync(string eventId);
    Task<ApiResponse<List<Prediction>>> GetUserSeasonPredictionsAsync(string userId, string seasonId);
    Task<ApiResponse<List<Prediction>>> GetUserActiveSeasonsPredictionsAsync(string userId);
    Task<ApiResponse<PredictionResponse>> SubmitPredictionAsync(SubmitPredictionRequest request);

    // Leaderboard
    Task<ApiResponse<List<UserStatistics>>> GetLeaderboardAsync(string seasonId);
    Task<ApiResponse<UserStatistics>> GetUserStatisticsAsync(string seasonId, string userId);

    // Admin - Season Management
    Task<ApiResponse<MessageResponse>> SetActiveSeasonAsync(string seriesId, string seasonId);
    Task<ApiResponse<MessageResponse>> FindDuplicateActiveSeasonsAsync();

    // Admin - Admin Management
    Task<ApiResponse<AdminStatusResponse>> GetMyAdminStatusAsync();
    Task<ApiResponse<List<Admin>>> GetAllAdminsAsync();
    Task<ApiResponse<Admin>> GetAdminAsync(string userId);
    Task<ApiResponse<MessageResponse>> CreateAdminAsync(string userId, bool isActive, bool canManageAdmins);
    Task<ApiResponse<MessageResponse>> UpdateAdminAsync(string userId, bool isActive, bool canManageAdmins);
    Task<ApiResponse<MessageResponse>> RemoveAdminAsync(string userId);

    // Admin - Discipline Management
    Task<ApiResponse<List<Discipline>>> GetAllDisciplinesAdminAsync();
    Task<ApiResponse<Discipline>> GetDisciplineAdminAsync(string disciplineId);
    Task<ApiResponse<Discipline>> CreateDisciplineAsync(CreateDisciplineRequest request);
    Task<ApiResponse<Discipline>> UpdateDisciplineAsync(string disciplineId, UpdateDisciplineRequest request);
    Task<ApiResponse<MessageResponse>> DeleteDisciplineAsync(string disciplineId);

    // Admin - Series Management
    Task<ApiResponse<List<Series>>> GetAllSeriesAdminAsync(string disciplineId);
    Task<ApiResponse<Series>> GetSeriesAdminAsync(string seriesId);
    Task<ApiResponse<Series>> CreateSeriesAsync(CreateSeriesRequest request);
    Task<ApiResponse<Series>> UpdateSeriesAsync(string seriesId, string currentDisciplineId, UpdateSeriesRequest request);
    Task<ApiResponse<MessageResponse>> DeleteSeriesAsync(string seriesId, string disciplineId);

    // Admin - Season Management
    Task<ApiResponse<List<Season>>> GetAllSeasonsAdminAsync(string seriesId);
    Task<ApiResponse<Season>> GetSeasonAdminAsync(string seasonId, string seriesId);
    Task<ApiResponse<SeasonDependencies>> GetSeasonDependenciesAsync(string seasonId);
    Task<ApiResponse<Season>> CreateSeasonAsync(CreateSeasonRequest request);
    Task<ApiResponse<Season>> UpdateSeasonAsync(string seasonId, string currentSeriesId, UpdateSeasonRequest request);
    Task<ApiResponse<MessageResponse>> DeleteSeasonAsync(string seasonId, string seriesId);

    // Admin - Competitor Management
    Task<ApiResponse<List<Competitor>>> GetAllCompetitorsAdminAsync();
    Task<ApiResponse<List<Competitor>>> GetCompetitorsByTypeAsync(string type);
    Task<ApiResponse<Competitor>> GetCompetitorAdminAsync(string competitorId);
    Task<ApiResponse<CompetitorDependencies>> GetCompetitorDependenciesAsync(string competitorId);
    Task<ApiResponse<List<string>>> GetCompetitorSeasonsAsync(string competitorId);
    Task<ApiResponse<Competitor>> CreateCompetitorAsync(CreateCompetitorRequest request);
    Task<ApiResponse<Competitor>> UpdateCompetitorAsync(string competitorId, UpdateCompetitorRequest request);
    Task<ApiResponse<MessageResponse>> DeleteCompetitorAsync(string competitorId, string type);

    // Admin - Season Competitor Management
    Task<ApiResponse<List<SeasonCompetitor>>> GetSeasonCompetitorsAdminAsync(string seasonId);
    Task<ApiResponse<MessageResponse>> AddCompetitorToSeasonAsync(string seasonId, string competitorId);
    Task<ApiResponse<MessageResponse>> RemoveCompetitorFromSeasonAsync(string seasonId, string competitorId);

    // Admin - Event Management
    Task<ApiResponse<List<Event>>> GetAllEventsAsync();
    Task<ApiResponse<List<Event>>> GetEventsBySeasonAdminAsync(string seasonId);
    Task<ApiResponse<Event>> GetEventAdminAsync(string eventId);
    Task<ApiResponse<EventDependencies>> GetEventDependenciesAsync(string eventId);
    Task<ApiResponse<object>> GetNextEventNumberAsync(string seasonId);
    Task<ApiResponse<Event>> CreateEventAsync(CreateEventRequest request);
    Task<ApiResponse<Event>> UpdateEventAsync(string eventId, UpdateEventRequest request);
    Task<ApiResponse<MessageResponse>> DeleteEventAsync(string eventId, string seasonId);

    // Admin - Event Result Management
    Task<ApiResponse<EventResult>> GetEventResultAdminAsync(string eventId);
    Task<ApiResponse<EventResult>> CreateOrUpdateEventResultAsync(string eventId, CreateEventResultRequest request);
    Task<ApiResponse<MessageResponse>> DeleteEventResultAsync(string eventId);

    // Admin - Scoring & Predictions
    Task<ApiResponse<ScoringRules>> GetScoringRulesAsync(string seasonId);
    Task<ApiResponse<MessageResponse>> UpdatePredictionPointsAsync(string eventId, string userId, int points);

    // Admin - User Management
    Task<ApiResponse<List<User>>> GetAllUsersAsync();
    Task<ApiResponse<List<UserSearchResult>>> SearchUsersAsync(string searchTerm);
    Task<ApiResponse<User>> GetUserAdminAsync(string userId);
    Task<ApiResponse<UserDependencies>> GetUserDependenciesAsync(string userId);
    Task<ApiResponse<MessageResponse>> UpdateUserAsync(string userId, UpdateUserRequest request);
    Task<ApiResponse<MessageResponse>> UpdateUserAuthMethodAsync(string userId, string preferredAuthMethod);
    Task<ApiResponse<PasswordSetupInitResponse>> InitiatePasswordSetupAsync(string userId);
    Task<ApiResponse<PasswordSetupConfirmResponse>> ConfirmPasswordSetupAsync(string userId);
    Task<ApiResponse<MessageResponse>> DeleteUserAsync(string userId);
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

    // Disciplines & Competitions
    public async Task<ApiResponse<List<Discipline>>> GetDisciplinesAsync()
    {
        return await GetAsync<List<Discipline>>("/api/disciplines");
    }

    public async Task<ApiResponse<List<Series>>> GetSeriesAsync(string disciplineId)
    {
        return await GetAsync<List<Series>>($"/api/disciplines/{disciplineId}/series");
    }

    public async Task<ApiResponse<List<Season>>> GetSeasonsAsync(string seriesId)
    {
        return await GetAsync<List<Season>>($"/api/series/{seriesId}/seasons");
    }

    public async Task<ApiResponse<Season>> GetActiveSeasonAsync(string seriesId)
    {
        return await GetAsync<Season>($"/api/series/{seriesId}/seasons/active");
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

    public async Task<ApiResponse<List<Prediction>>> GetUserActiveSeasonsPredictionsAsync(string userId)
    {
        return await GetAsync<List<Prediction>>($"/api/predictions/user/{userId}/active-seasons");
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

    // Admin - Season Management
    public async Task<ApiResponse<MessageResponse>> SetActiveSeasonAsync(string seriesId, string seasonId)
    {
        return await PostAsync<MessageResponse>($"/api/admin/series/{seriesId}/seasons/{seasonId}/set-active", new { });
    }

    public async Task<ApiResponse<MessageResponse>> FindDuplicateActiveSeasonsAsync()
    {
        return await GetAsync<MessageResponse>("/api/admin/diagnostics/duplicate-active-seasons");
    }

    // Admin - Admin Management
    public async Task<ApiResponse<AdminStatusResponse>> GetMyAdminStatusAsync()
    {
        return await GetAsync<AdminStatusResponse>("/api/admin/me");
    }

    public async Task<ApiResponse<List<Admin>>> GetAllAdminsAsync()
    {
        return await GetAsync<List<Admin>>("/api/admin/admins");
    }

    public async Task<ApiResponse<Admin>> GetAdminAsync(string userId)
    {
        return await GetAsync<Admin>($"/api/admin/admins/{userId}");
    }

    public async Task<ApiResponse<MessageResponse>> CreateAdminAsync(string userId, bool isActive, bool canManageAdmins)
    {
        return await PostAsync<MessageResponse>("/api/admin/admins", 
            new { userId, isActive, canManageAdmins });
    }

    public async Task<ApiResponse<MessageResponse>> UpdateAdminAsync(string userId, bool isActive, bool canManageAdmins)
    {
        return await PutAsync<MessageResponse>($"/api/admin/admins/{userId}", 
            new { isActive, canManageAdmins });
    }

    public async Task<ApiResponse<MessageResponse>> RemoveAdminAsync(string userId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/admins/{userId}");
    }

    // Admin - Discipline Management
    public async Task<ApiResponse<List<Discipline>>> GetAllDisciplinesAdminAsync()
    {
        return await GetAsync<List<Discipline>>("/api/admin/disciplines");
    }

    public async Task<ApiResponse<Discipline>> GetDisciplineAdminAsync(string disciplineId)
    {
        return await GetAsync<Discipline>($"/api/admin/disciplines/{disciplineId}");
    }

    public async Task<ApiResponse<Discipline>> CreateDisciplineAsync(CreateDisciplineRequest request)
    {
        return await PostAsync<Discipline>("/api/admin/disciplines", request);
    }

    public async Task<ApiResponse<Discipline>> UpdateDisciplineAsync(string disciplineId, UpdateDisciplineRequest request)
    {
        return await PutAsync<Discipline>($"/api/admin/disciplines/{disciplineId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteDisciplineAsync(string disciplineId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/disciplines/{disciplineId}");
    }

    // Admin - Series Management
    public async Task<ApiResponse<List<Series>>> GetAllSeriesAdminAsync(string disciplineId)
    {
        return await GetAsync<List<Series>>($"/api/admin/disciplines/{disciplineId}/series");
    }

    public async Task<ApiResponse<Series>> GetSeriesAdminAsync(string seriesId)
    {
        return await GetAsync<Series>($"/api/admin/series/{seriesId}");
    }

    public async Task<ApiResponse<Series>> CreateSeriesAsync(CreateSeriesRequest request)
    {
        return await PostAsync<Series>("/api/admin/series", request);
    }

    public async Task<ApiResponse<Series>> UpdateSeriesAsync(string seriesId, string currentDisciplineId, UpdateSeriesRequest request)
    {
        return await PutAsync<Series>($"/api/admin/series/{seriesId}?currentDisciplineId={currentDisciplineId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteSeriesAsync(string seriesId, string disciplineId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/series/{seriesId}?disciplineId={disciplineId}");
    }

    // Admin - Season Management
    public async Task<ApiResponse<List<Season>>> GetAllSeasonsAdminAsync(string seriesId)
    {
        return await GetAsync<List<Season>>($"/api/admin/series/{seriesId}/seasons");
    }

    public async Task<ApiResponse<Season>> GetSeasonAdminAsync(string seasonId, string seriesId)
    {
        return await GetAsync<Season>($"/api/admin/seasons/{seasonId}?seriesId={seriesId}");
    }

    public async Task<ApiResponse<SeasonDependencies>> GetSeasonDependenciesAsync(string seasonId)
    {
        return await GetAsync<SeasonDependencies>($"/api/admin/seasons/{seasonId}/dependencies");
    }

    public async Task<ApiResponse<Season>> CreateSeasonAsync(CreateSeasonRequest request)
    {
        return await PostAsync<Season>("/api/admin/seasons", request);
    }

    public async Task<ApiResponse<Season>> UpdateSeasonAsync(string seasonId, string currentSeriesId, UpdateSeasonRequest request)
    {
        return await PutAsync<Season>($"/api/admin/seasons/{seasonId}?currentSeriesId={currentSeriesId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteSeasonAsync(string seasonId, string seriesId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/seasons/{seasonId}?seriesId={seriesId}");
    }

    // Admin - Competitor Management
    public async Task<ApiResponse<List<Competitor>>> GetAllCompetitorsAdminAsync()
    {
        return await GetAsync<List<Competitor>>($"/api/admin/competitors");
    }

    public async Task<ApiResponse<List<Competitor>>> GetCompetitorsByTypeAsync(string type)
    {
        return await GetAsync<List<Competitor>>($"/api/admin/competitors/type/{type}");
    }

    public async Task<ApiResponse<Competitor>> GetCompetitorAdminAsync(string competitorId)
    {
        return await GetAsync<Competitor>($"/api/admin/competitors/{competitorId}");
    }

    public async Task<ApiResponse<CompetitorDependencies>> GetCompetitorDependenciesAsync(string competitorId)
    {
        return await GetAsync<CompetitorDependencies>($"/api/admin/competitors/{competitorId}/dependencies");
    }

    public async Task<ApiResponse<List<string>>> GetCompetitorSeasonsAsync(string competitorId)
    {
        return await GetAsync<List<string>>($"/api/admin/competitors/{competitorId}/seasons");
    }

    public async Task<ApiResponse<Competitor>> CreateCompetitorAsync(CreateCompetitorRequest request)
    {
        return await PostAsync<Competitor>("/api/admin/competitors", request);
    }

    public async Task<ApiResponse<Competitor>> UpdateCompetitorAsync(string competitorId, UpdateCompetitorRequest request)
    {
        return await PutAsync<Competitor>($"/api/admin/competitors/{competitorId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteCompetitorAsync(string competitorId, string type)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/competitors/{competitorId}?type={type}");
    }

    // Admin - Season Competitor Management
    public async Task<ApiResponse<List<SeasonCompetitor>>> GetSeasonCompetitorsAdminAsync(string seasonId)
    {
        return await GetAsync<List<SeasonCompetitor>>($"/api/admin/seasons/{seasonId}/competitors");
    }

    public async Task<ApiResponse<MessageResponse>> AddCompetitorToSeasonAsync(string seasonId, string competitorId)
    {
        return await PostAsync<MessageResponse>($"/api/admin/seasons/{seasonId}/competitors/{competitorId}", new { });
    }

    public async Task<ApiResponse<MessageResponse>> RemoveCompetitorFromSeasonAsync(string seasonId, string competitorId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/seasons/{seasonId}/competitors/{competitorId}");
    }

    // Admin - Event Management
    public async Task<ApiResponse<List<Event>>> GetAllEventsAsync()
    {
        return await GetAsync<List<Event>>("/api/admin/events");
    }

    public async Task<ApiResponse<List<Event>>> GetEventsBySeasonAdminAsync(string seasonId)
    {
        return await GetAsync<List<Event>>($"/api/admin/seasons/{seasonId}/events");
    }

    public async Task<ApiResponse<Event>> GetEventAdminAsync(string eventId)
    {
        return await GetAsync<Event>($"/api/admin/events/{eventId}");
    }

    public async Task<ApiResponse<EventDependencies>> GetEventDependenciesAsync(string eventId)
    {
        return await GetAsync<EventDependencies>($"/api/admin/events/{eventId}/dependencies");
    }

    public async Task<ApiResponse<object>> GetNextEventNumberAsync(string seasonId)
    {
        return await GetAsync<object>($"/api/admin/seasons/{seasonId}/events/next-number");
    }

    public async Task<ApiResponse<Event>> CreateEventAsync(CreateEventRequest request)
    {
        return await PostAsync<Event>("/api/admin/events", request);
    }

    public async Task<ApiResponse<Event>> UpdateEventAsync(string eventId, UpdateEventRequest request)
    {
        return await PutAsync<Event>($"/api/admin/events/{eventId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteEventAsync(string eventId, string seasonId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/events/{eventId}?seasonId={seasonId}");
    }

    // Admin - Event Result Management
    public async Task<ApiResponse<EventResult>> GetEventResultAdminAsync(string eventId)
    {
        return await GetAsync<EventResult>($"/api/admin/events/{eventId}/result");
    }

    public async Task<ApiResponse<EventResult>> CreateOrUpdateEventResultAsync(string eventId, CreateEventResultRequest request)
    {
        return await PostAsync<EventResult>($"/api/admin/events/{eventId}/result", request);
    }

    public async Task<ApiResponse<MessageResponse>> DeleteEventResultAsync(string eventId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/events/{eventId}/result");
    }

    // Admin - Scoring & Predictions
    public async Task<ApiResponse<ScoringRules>> GetScoringRulesAsync(string seasonId)
    {
        return await GetAsync<ScoringRules>($"/api/scoring/{seasonId}");
    }

    public async Task<ApiResponse<MessageResponse>> UpdatePredictionPointsAsync(string eventId, string userId, int points)
    {
        return await PutAsync<MessageResponse>($"/api/admin/predictions/{eventId}/{userId}/points", new { points });
    }

    // Admin - User Management
    public async Task<ApiResponse<List<User>>> GetAllUsersAsync()
    {
        return await GetAsync<List<User>>("/api/admin/users");
    }

    public async Task<ApiResponse<List<UserSearchResult>>> SearchUsersAsync(string searchTerm)
    {
        return await GetAsync<List<UserSearchResult>>($"/api/admin/users/search?q={Uri.EscapeDataString(searchTerm)}");
    }

    public async Task<ApiResponse<User>> GetUserAdminAsync(string userId)
    {
        return await GetAsync<User>($"/api/admin/users/{userId}");
    }

    public async Task<ApiResponse<UserDependencies>> GetUserDependenciesAsync(string userId)
    {
        return await GetAsync<UserDependencies>($"/api/admin/users/{userId}/dependencies");
    }

    public async Task<ApiResponse<MessageResponse>> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        return await PutAsync<MessageResponse>($"/api/admin/users/{userId}", request);
    }

    public async Task<ApiResponse<MessageResponse>> UpdateUserAuthMethodAsync(string userId, string preferredAuthMethod)
    {
        return await PutAsync<MessageResponse>($"/api/admin/users/{userId}/auth-method", 
            new { preferredAuthMethod });
    }

    public async Task<ApiResponse<PasswordSetupInitResponse>> InitiatePasswordSetupAsync(string userId)
    {
        return await PostAsync<PasswordSetupInitResponse>($"/api/admin/users/{userId}/setup-password", new { });
    }

    public async Task<ApiResponse<PasswordSetupConfirmResponse>> ConfirmPasswordSetupAsync(string userId)
    {
        return await PostAsync<PasswordSetupConfirmResponse>($"/api/admin/users/{userId}/setup-password/confirm", new { });
    }

    public async Task<ApiResponse<MessageResponse>> DeleteUserAsync(string userId)
    {
        return await DeleteAsync<MessageResponse>($"/api/admin/users/{userId}");
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

            var error = await ParseErrorResponseAsync(response);
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

            var error = await ParseErrorResponseAsync(response);
            return new ApiResponse<T> { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiResponse<T>> PutAsync<T>(string url, object data)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, data);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>();
                return new ApiResponse<T> { Success = true, Data = result };
            }

            var error = await ParseErrorResponseAsync(response);
            return new ApiResponse<T> { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<ApiResponse<T>> DeleteAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<T>();
                return new ApiResponse<T> { Success = true, Data = result };
            }

            var error = await ParseErrorResponseAsync(response);
            return new ApiResponse<T> { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new ApiResponse<T> { Success = false, Error = ex.Message };
        }
    }

    private async Task<string> ParseErrorResponseAsync(HttpResponseMessage response)
    {
        try
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            
            // Try to parse as JSON and extract the error message
            using var doc = JsonDocument.Parse(errorContent);
            if (doc.RootElement.TryGetProperty("error", out var errorProperty))
            {
                return errorProperty.GetString() ?? errorContent;
            }
            
            // If no "error" property, return the raw content
            return errorContent;
        }
        catch
        {
            // If parsing fails, return a generic error message
            return "An error occurred. Please try again.";
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
public record AuthResponse(string UserId, string Username, string SessionId, string Message);
public record PredictionResponse(string Message, Prediction Prediction);
public record AdminStatusResponse(bool IsAdmin, bool CanManageAdmins);
public record PasswordSetupInitResponse(string UserId, string Username, string Email, string CurrentAuthMethod, string Message);
public record PasswordSetupConfirmResponse(string Message, string Email, bool Success, bool? EmailFailed);

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

// Admin Request DTOs
public record CreateDisciplineRequest(string Name, string DisplayName, bool IsActive);
public record UpdateDisciplineRequest(string Name, string DisplayName, bool IsActive);

public record CreateSeasonRequest(string SeriesId, int Year, string Name, bool IsActive, DateTime StartDate, DateTime? EndDate);
public record UpdateSeasonRequest(string SeriesId, int Year, string Name, bool IsActive, DateTime StartDate, DateTime? EndDate);

public record CreateCompetitorRequest(string Name, string ShortName, string Type, bool IsActive);
public record UpdateCompetitorRequest(string Name, string ShortName, string Type, bool IsActive);

public record CreateSeriesRequest(string DisciplineId, string Name, string DisplayName, bool IsActive, string? GoverningBody, string? Region, string? VehicleType);
public record UpdateSeriesRequest(string DisciplineId, string Name, string DisplayName, bool IsActive, string? GoverningBody, string? Region, string? VehicleType);

public record CreateEventRequest(string SeasonId, string Name, string DisplayName, int EventNumber, DateTime EventDate, string Location, string Status, bool IsActive);
public record UpdateEventRequest(string Name, string DisplayName, int EventNumber, DateTime EventDate, string Location, string Status, bool IsActive);

public record CreateEventResultRequest(string FirstPlaceId, string FirstPlaceName, string SecondPlaceId, string SecondPlaceName, string ThirdPlaceId, string ThirdPlaceName);

public record UpdatePointsRequest(int Points);

public record UpdateUserRequest(string Username, string Email, bool IsActive);

public record UserSearchResult(string UserId, string Email, string Username, bool IsActive);
