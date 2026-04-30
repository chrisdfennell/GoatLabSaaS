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
    // Cache window. Long enough to skip a /me call on every render burst,
    // short enough that a soft-deleted user (or a user whose admin disabled
    // them, or someone whose cookie expired silently) doesn't keep seeing
    // their own face in the nav for the rest of the WASM session.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    private readonly AuthService _auth;
    private AuthenticationState? _cached;
    private CurrentUserDto? _cachedUser;
    private DateTime _cachedAt;

    public CookieAuthStateProvider(AuthService auth) => _auth = auth;

    /// <summary>
    /// The last known CurrentUserDto (includes plan features, memberships, etc.)
    /// Null while loading or if unauthenticated. Components wanting DTO-level
    /// state should subscribe to <see cref="AuthenticationStateProvider.AuthenticationStateChanged"/>
    /// and re-read this on each notification.
    /// </summary>
    public CurrentUserDto? CurrentUser => _cachedUser;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Serve a fresh cache without a round-trip; otherwise re-fetch so that
        // server-side state changes (admin soft-delete bumping SecurityStamp,
        // forced sign-out, etc.) propagate to the UI within TTL seconds.
        if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
            return _cached;

        CurrentUserDto? user = null;
        try { user = await _auth.GetCurrentUserAsync(); }
        catch { /* network blips / server cold start — treat as unauthenticated */ }

        var stateChanged = !ReferenceEquals(user, _cachedUser)
            && (user?.Id != _cachedUser?.Id);
        _cachedUser = user;
        _cached = BuildState(user);
        _cachedAt = DateTime.UtcNow;

        // If the identity actually flipped (e.g. cookie was rejected, user
        // soft-deleted), broadcast so AuthorizeView re-renders nav, hides
        // logged-in chrome, etc.
        if (stateChanged)
            NotifyAuthenticationStateChanged(Task.FromResult(_cached));

        return _cached;
    }

    public void NotifyUserChanged(CurrentUserDto? user)
    {
        _cachedUser = user;
        _cached = BuildState(user);
        _cachedAt = DateTime.UtcNow;
        NotifyAuthenticationStateChanged(Task.FromResult(_cached));
    }

    /// <summary>
    /// Force the next <see cref="GetAuthenticationStateAsync"/> to bypass the
    /// cache and re-fetch. ApiService calls this when any HTTP request returns
    /// 401, so a deleted/disabled user's stale identity disappears from the
    /// nav immediately instead of waiting out the TTL.
    /// </summary>
    public void Invalidate()
    {
        _cachedAt = DateTime.MinValue;
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
