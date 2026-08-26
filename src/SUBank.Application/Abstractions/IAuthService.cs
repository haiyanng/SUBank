using SUBank.Contracts.Auth;

namespace SUBank.Application.Abstractions;

public interface IAuthService
{
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<UserSummary?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken);
}

public sealed record AuthSession(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshExpiresAtUtc);
