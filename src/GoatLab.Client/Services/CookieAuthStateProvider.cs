using System.Security.Claims;
using GoatLab.Shared.DTOs;
using Microsoft.AspNetCore.Components.Authorization;

namespace GoatLab.Client.Services;

/// <summary>
/// Custom AuthenticationStateProvider backed by the /api/account/me endpoint.
/// Cookie auth means we don't manage tokens in JS — we just ask the server
/// who the user is and cache the claims. Call <see cref="NotifyUserChanged"/>
/// after login/register/logout to refresh Blazor's auth state.
/// </summary>
public class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly AuthService _auth;
    private AuthenticationState? _cached;

    public CookieAuthStateProvider(AuthService auth) => _auth = auth;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cached is not null) return _cached;

        CurrentUserDto? user = null;
        try { user = await _auth.GetCurrentUserAsync(); }
        catch { /* network blips / server cold start — treat as unauthenticated */ }

        _cached = BuildState(user);
        return _cached;
    }

    public void NotifyUserChanged(CurrentUserDto? user)
    {
        _cached = BuildState(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_cached));
    }

    private static AuthenticationState BuildState(CurrentUserDto? user)
    {
        if (user is null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
        };
        if (user.CurrentTenantId is int tid)
            claims.Add(new Claim("tenant_id", tid.ToString()));
        if (user.IsSuperAdmin)
            claims.Add(new Claim("super_admin", "true"));

        var identity = new ClaimsIdentity(claims, authenticationType: "cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }
}
