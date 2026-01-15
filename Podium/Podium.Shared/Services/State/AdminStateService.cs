using Podium.Shared.Services.Api;

namespace Podium.Shared.Services.State;

public class AdminStateService
{
    private readonly IPodiumApiClient _apiClient;
    private readonly AuthStateService _authState;
    
    private bool _isAdmin;
    private bool _canManageAdmins;
    private bool _isInitialized;

    public event Action? OnAdminStateChanged;

    public bool IsAdmin => _isAdmin;
    public bool CanManageAdmins => _canManageAdmins;
    public bool IsInitialized => _isInitialized;

    public AdminStateService(IPodiumApiClient apiClient, AuthStateService authState)
    {
        _apiClient = apiClient;
        _authState = authState;
        
        // Subscribe to auth state changes to refresh admin status
        _authState.OnAuthStateChanged += OnAuthChanged;
    }

    private async void OnAuthChanged()
    {
        if (!_authState.IsAuthenticated)
        {
            ClearAdminState();
        }
        else
        {
            await RefreshAdminStatusAsync();
        }
    }

    public async Task InitializeAsync()
    {
        if (_authState.IsAuthenticated && _authState.UserId != null)
        {
            await RefreshAdminStatusAsync();
        }
        _isInitialized = true;
    }

    public async Task RefreshAdminStatusAsync()
    {
        if (!_authState.IsAuthenticated || string.IsNullOrEmpty(_authState.UserId))
        {
            ClearAdminState();
            return;
        }

        try
        {
            var response = await _apiClient.GetMyAdminStatusAsync();
            if (response.Success && response.Data != null)
            {
                _isAdmin = response.Data.IsAdmin;
                _canManageAdmins = response.Data.CanManageAdmins;
            }
            else
            {
                _isAdmin = false;
                _canManageAdmins = false;
            }
        }
        catch
        {
            _isAdmin = false;
            _canManageAdmins = false;
        }

        NotifyStateChanged();
    }

    private void ClearAdminState()
    {
        _isAdmin = false;
        _canManageAdmins = false;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnAdminStateChanged?.Invoke();

    public void Dispose()
    {
        _authState.OnAuthStateChanged -= OnAuthChanged;
    }
}
