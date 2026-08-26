namespace SUBank.Application.Abstractions;

public interface IRealtimeNotifier
{
    Task ForceLogoutAsync(string sessionId, CancellationToken cancellationToken);
    Task BalanceChangedAsync(string userId, string accountNumber, CancellationToken cancellationToken);
    Task TransactionReceivedAsync(string userId, string referenceNo, string accountNumber, CancellationToken cancellationToken);
}
