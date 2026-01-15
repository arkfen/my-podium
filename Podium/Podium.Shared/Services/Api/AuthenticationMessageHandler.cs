using Podium.Shared.Services.State;

namespace Podium.Shared.Services.Api;

/// <summary>
/// HTTP message handler that automatically injects the X-Session-Id header
/// from the AuthStateService into all API requests (except auth endpoints)
/// </summary>
public class AuthenticationMessageHandler : DelegatingHandler
{
    private readonly AuthStateService _authStateService;

    public AuthenticationMessageHandler(AuthStateService authStateService)
    {
        _authStateService = authStateService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Don't add session header to auth endpoints (they don't need it)
        var isAuthEndpoint = request.RequestUri?.PathAndQuery.Contains("/api/auth/") ?? false;
        
        if (!isAuthEndpoint && !string.IsNullOrEmpty(_authStateService.SessionId))
        {
            request.Headers.Add("X-Session-Id", _authStateService.SessionId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
