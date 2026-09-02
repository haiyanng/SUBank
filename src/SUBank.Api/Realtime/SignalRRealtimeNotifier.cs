using Microsoft.AspNetCore.SignalR;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Realtime;

namespace SUBank.Api.Realtime;

public sealed class SignalRRealtimeNotifier(
    IHubContext<BankingHub> hub,
    IActiveSessionStore activeSessions,
    IActiveSessionValidator sessionValidator,
    ILogger<SignalRRealtimeNotifier> logger)
    : IRealtimeNotifier
{
    public Task ForceLogoutAsync(string sessionId, string reason, CancellationToken cancellationToken) =>
        SendBestEffortAsync(RealtimeGroups.Session(sessionId), "ForceLogout",
            new ForceLogoutNotification(reason), cancellationToken);

    public Task BalanceChangedAsync(string userId, string accountNumber, CancellationToken cancellationToken) =>
        SendToActiveSessionBestEffortAsync(userId, "BalanceChanged",
            new BalanceChangedNotification(accountNumber), cancellationToken);

    public Task TransactionReceivedAsync(string userId, string referenceNo, string accountNumber, CancellationToken cancellationToken) =>
        SendToActiveSessionBestEffortAsync(userId, "TransactionReceived",
            new TransactionReceivedNotification(referenceNo, accountNumber), cancellationToken);

    private async Task SendToActiveSessionBestEffortAsync(
        string userId,
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = await activeSessions.GetActiveSessionIdAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            if (!await sessionValidator.IsValidAsync(userId, sessionId, cancellationToken)) return;

            await SendBestEffortAsync(RealtimeGroups.Session(sessionId), method, payload, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Không thể xác định active session cho SignalR event {EventName}; client sẽ tải lại qua REST.",
                method);
        }
    }

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
