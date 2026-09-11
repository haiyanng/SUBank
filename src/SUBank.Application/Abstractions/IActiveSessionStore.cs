namespace SUBank.Application.Abstractions;

public interface IActiveSessionStore
{
    Task<string?> ReplaceAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken);
    Task<string?> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken);
    Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken cancellationToken);
    Task<bool> RenewAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken);
    Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken);
}
