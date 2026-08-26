using Microsoft.AspNetCore.SignalR;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Realtime;

namespace SUBank.Api.Realtime;

public sealed class SignalRRealtimeNotifier(IHubContext<BankingHub> hub, ILogger<SignalRRealtimeNotifier> logger)
    : IRealtimeNotifier
{
    public Task ForceLogoutAsync(string sessionId, CancellationToken cancellationToken) =>
        SendBestEffortAsync(RealtimeGroups.Session(sessionId), "ForceLogout", null, cancellationToken);

    public Task BalanceChangedAsync(string userId, string accountNumber, CancellationToken cancellationToken) =>
        SendBestEffortAsync(RealtimeGroups.User(userId), "BalanceChanged",
            new BalanceChangedNotification(accountNumber), cancellationToken);

    public Task TransactionReceivedAsync(string userId, string referenceNo, string accountNumber, CancellationToken cancellationToken) =>
        SendBestEffortAsync(RealtimeGroups.User(userId), "TransactionReceived",
            new TransactionReceivedNotification(referenceNo, accountNumber), cancellationToken);

    private async Task SendBestEffortAsync(string group, string method, object? payload, CancellationToken cancellationToken)
    {
        try
        {
            if (payload is null)
                await hub.Clients.Group(group).SendAsync(method, cancellationToken);
            else
                await hub.Clients.Group(group).SendAsync(method, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Không thể gửi SignalR event {EventName}; client sẽ tải lại qua REST.", method);
        }
    }
}
