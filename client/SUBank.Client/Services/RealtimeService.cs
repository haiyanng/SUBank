using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SUBank.Contracts.Realtime;

namespace SUBank.Client.Services;

public sealed class RealtimeService(ApiSession session, HttpClient httpClient, NavigationManager navigation) : IAsyncDisposable
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly SemaphoreSlim syncGate = new(1, 1);
    private HubConnection? connection;
    private CancellationTokenSource? retryCancellation;
    private Task? retryTask;
    private long activeSessionGeneration = -1;
    private int retryAttempt;
    private bool disposed;
    public string? LastMessage { get; private set; }
    public event Action? MessageChanged;

    public async Task SyncAsync()
    {
        await syncGate.WaitAsync();
        try
        {
            if (disposed) return;
            await SyncCoreAsync();
        }
        finally
        {
            syncGate.Release();
        }
    }

    public async Task PrepareForLoginAsync()
    {
        await syncGate.WaitAsync();
        try
        {
            if (disposed) return;
            await StopCoreAsync();
            ClearMessage();
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task SyncCoreAsync()
    {
        var currentAccessToken = session.Current?.AccessToken;
        if (string.IsNullOrWhiteSpace(currentAccessToken))
        {
            await StopCoreAsync();
            return;
        }

        var currentGeneration = session.Generation;
        if (activeSessionGeneration == currentGeneration &&
            connection?.State is HubConnectionState.Connected or
                HubConnectionState.Connecting or HubConnectionState.Reconnecting)
            return;

        if (activeSessionGeneration == currentGeneration && retryTask is { IsCompleted: false }) return;

        await StopCoreAsync();
        activeSessionGeneration = currentGeneration;
        retryCancellation = new CancellationTokenSource();
        await StartConnectionAttemptCoreAsync(currentGeneration, retryCancellation.Token);
    }

    private async Task StartConnectionAttemptCoreAsync(long generation, CancellationToken cancellationToken)
    {
        await DisposeConnectionCoreAsync();
        if (!CanRetry(generation, cancellationToken)) return;

        var nextConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "hubs/banking"), options =>
                options.AccessTokenProvider = async () =>
                {
                    if (session.Generation != generation) return null;

                    var accessToken = await session.GetFreshAccessTokenAsync();
                    return session.Generation == generation ? accessToken : null;
                })
            .WithAutomaticReconnect()
            .Build();
        nextConnection.On("ForceLogout", () =>
        {
            if (!IsCurrentConnection(nextConnection, generation)) return;

            LastMessage = "Tài khoản đã đăng nhập ở nơi khác. Phiên này đã kết thúc.";
            session.EndFromServer();
            NotifyMessageChanged();
            navigation.NavigateTo("/login?reason=session-replaced");
        });
        nextConnection.On<BalanceChangedNotification>("BalanceChanged", notification =>
        {
            if (!IsCurrentConnection(nextConnection, generation)) return;

            LastMessage = $"Số dư tài khoản {notification.AccountNumber} vừa thay đổi.";
            session.NotifyBankingDataChanged();
            NotifyMessageChanged();
        });
        nextConnection.On<TransactionReceivedNotification>("TransactionReceived", notification =>
        {
            if (!IsCurrentConnection(nextConnection, generation)) return;

            LastMessage = $"Có cập nhật giao dịch {notification.ReferenceNo}.";
            session.NotifyBankingDataChanged();
            NotifyMessageChanged();
        });
        nextConnection.Reconnecting += _ =>
        {
            if (!IsCurrentConnection(nextConnection, generation)) return Task.CompletedTask;

            LastMessage = "Kết nối realtime đang được khôi phục…";
            NotifyMessageChanged();
            return Task.CompletedTask;
        };
        nextConnection.Reconnected += _ =>
        {
            if (!IsCurrentConnection(nextConnection, generation)) return Task.CompletedTask;

            retryAttempt = 0;
            LastMessage = "Đã khôi phục kết nối realtime.";
            session.NotifyBankingDataChanged();
            NotifyMessageChanged();
            return Task.CompletedTask;
        };
        nextConnection.Closed += _ =>
        {
            if (IsCurrentConnection(nextConnection, generation))
            {
                LastMessage = "Kết nối realtime bị gián đoạn; hệ thống đang tự thử lại.";
                NotifyMessageChanged();
                RequestRetry(generation, cancellationToken);
            }

            return Task.CompletedTask;
        };

        connection = nextConnection;
        _ = StartConnectionAsync(nextConnection, generation, cancellationToken);
    }

    private async Task StartConnectionAsync(
        HubConnection candidate,
        long generation,
        CancellationToken cancellationToken)
    {
        try
        {
            await candidate.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Logout, thay phiên hoặc dispose chủ động hủy lần kết nối này.
            return;
        }
        catch (Exception)
        {
            await HandleStartFailureAsync(candidate, generation, cancellationToken);
            return;
        }

        await syncGate.WaitAsync();
        try
        {
            if (!IsCurrentConnection(candidate, generation) ||
                candidate.State != HubConnectionState.Connected)
                return;

            var recovered = retryAttempt > 0;
            retryAttempt = 0;
            if (recovered)
            {
                LastMessage = "Đã khôi phục kết nối realtime.";
                session.NotifyBankingDataChanged();
                NotifyMessageChanged();
            }
        }
        finally
        {
            syncGate.Release();
        }
    }

    private async Task HandleStartFailureAsync(
        HubConnection candidate,
        long generation,
        CancellationToken cancellationToken)
    {
        var shouldRetry = false;
        var shouldDispose = false;
        await syncGate.WaitAsync();
        try
        {
            if (ReferenceEquals(connection, candidate))
            {
                connection = null;
                shouldRetry = CanRetry(generation, cancellationToken);
                shouldDispose = true;
                if (shouldRetry)
                {
                    LastMessage = "Realtime tạm thời không khả dụng; dữ liệu REST vẫn hoạt động và hệ thống sẽ tự thử lại.";
                    NotifyMessageChanged();
                }
            }
        }
        finally
        {
            syncGate.Release();
        }

        if (shouldDispose) await DisposeSafelyAsync(candidate);
        if (shouldRetry) RequestRetry(generation, cancellationToken);
    }

    private void RequestRetry(long generation, CancellationToken cancellationToken) =>
        _ = EnsureRetryScheduledAsync(generation, cancellationToken);

    private async Task EnsureRetryScheduledAsync(long generation, CancellationToken cancellationToken)
    {
        try
        {
            await syncGate.WaitAsync(cancellationToken);
            try
            {
                if (!CanRetry(generation, cancellationToken) ||
                    connection?.State is HubConnectionState.Connected or
                        HubConnectionState.Connecting or HubConnectionState.Reconnecting ||
                    retryTask is { IsCompleted: false })
                    return;

                var delay = RetryDelays[Math.Min(retryAttempt, RetryDelays.Length - 1)];
                retryAttempt++;
                retryTask = RetryAfterDelayAsync(generation, delay, cancellationToken);
            }
            finally
            {
                syncGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Phiên đã kết thúc nên không tạo retry mới.
        }
        catch
        {
            // Scheduler là best-effort; lỗi nền không được làm hỏng phiên REST hiện tại.
        }
    }

    private async Task RetryAfterDelayAsync(
        long generation,
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
            await syncGate.WaitAsync(cancellationToken);
            try
            {
                if (!CanRetry(generation, cancellationToken)) return;

                retryTask = null;
                await StartConnectionAttemptCoreAsync(generation, cancellationToken);
            }
            finally
            {
                syncGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Logout, thay phiên hoặc dispose dừng vòng retry đang chờ.
        }
        catch (Exception)
        {
            if (CanRetry(generation, cancellationToken)) RequestRetry(generation, cancellationToken);
        }
    }

    private bool IsCurrentConnection(HubConnection candidate, long generation) =>
        !disposed &&
        ReferenceEquals(connection, candidate) &&
        CanRetry(generation, CancellationToken.None);

    private bool CanRetry(long generation, CancellationToken cancellationToken) =>
        !disposed &&
        !cancellationToken.IsCancellationRequested &&
        session.Current is not null &&
        session.Generation == generation &&
        activeSessionGeneration == generation;

    public void ClearMessage()
    {
        LastMessage = null;
        NotifyMessageChanged();
    }

    private void NotifyMessageChanged()
    {
        if (MessageChanged is not { } handlers) return;

        foreach (Action handler in handlers.GetInvocationList())
        {
            try
            {
                handler();
            }
            catch
            {
                // Một UI subscriber lỗi không được làm hỏng connection/retry state machine.
            }
        }
    }

    private async Task StopCoreAsync()
    {
        var cancellation = retryCancellation;
        retryCancellation = null;
        retryTask = null;
        retryAttempt = 0;
        activeSessionGeneration = -1;
        cancellation?.Cancel();

        await DisposeConnectionCoreAsync();
        cancellation?.Dispose();
    }

    private async Task DisposeConnectionCoreAsync()
    {
        var previousConnection = connection;
        connection = null;
        if (previousConnection is not null) await DisposeSafelyAsync(previousConnection);
    }

    private static async Task DisposeSafelyAsync(HubConnection candidate)
    {
        try
        {
            await candidate.DisposeAsync();
        }
        catch
        {
            // Connection đã hỏng không được phép chặn logout hoặc vòng retry kế tiếp.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await syncGate.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            await StopCoreAsync();
        }
        finally
        {
            syncGate.Release();
        }
    }
}
