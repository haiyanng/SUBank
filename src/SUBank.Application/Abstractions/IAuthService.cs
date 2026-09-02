using SUBank.Contracts.Auth;

namespace SUBank.Application.Abstractions;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<RefreshCookieLogoutResult> LogoutAsync(
        string refreshToken,
        string expectedSessionId,
        CancellationToken cancellationToken);
    Task LogoutCurrentSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
    Task RejectCurrentSessionAsync(string userId, string sessionId, CancellationToken cancellationToken);
    Task<UserSummary?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}

public enum RefreshCookieLogoutResult
{
    Revoked,
    TokenUnknown,
    SessionMismatch
}

public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc, string SessionId);
