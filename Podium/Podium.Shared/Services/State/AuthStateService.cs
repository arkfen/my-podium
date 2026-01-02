namespace Podium.Shared.Services.State;

public class AuthStateService
{
    private string? _userId;
    private string? _username;
    private string? _sessionId;

    public event Action? OnAuthStateChanged;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_userId);
    public string? UserId => _userId;
    public string? Username => _username;
    public string? SessionId => _sessionId;

    public void SetAuthState(string userId, string username, string sessionId)
    {
        _userId = userId;
        _username = username;
        _sessionId = sessionId;
        NotifyStateChanged();
    }

    public void ClearAuthState()
    {
        _userId = null;
        _username = null;
        _sessionId = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnAuthStateChanged?.Invoke();
}
