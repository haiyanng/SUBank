using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SUBank.Contracts.Realtime;

namespace SUBank.Client.Services;

public sealed class RealtimeService(ApiSession session, HttpClient httpClient, NavigationManager navigation) : IAsyncDisposable
{
    private readonly SemaphoreSlim syncGate = new(1, 1);
    private HubConnection? connection;
    private string? connectionAccessToken;
    public string? LastMessage { get; private set; }
    public event Action? MessageChanged;

    public async Task SyncAsync()
    {
        await syncGate.WaitAsync();
        try
        {
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

        if ((connection?.State is HubConnectionState.Connected or HubConnectionState.Connecting) &&
            string.Equals(connectionAccessToken, currentAccessToken, StringComparison.Ordinal)) return;

        await StopCoreAsync();
        var boundAccessToken = currentAccessToken;
        var nextConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "hubs/banking"), options =>
                options.AccessTokenProvider = () => Task.FromResult<string?>(boundAccessToken))
            .WithAutomaticReconnect()
            .Build();
        nextConnection.On("ForceLogout", () =>
        {
            if (!string.Equals(session.Current?.AccessToken, boundAccessToken, StringComparison.Ordinal)) return;

            LastMessage = "Tài khoản đã đăng nhập ở nơi khác. Phiên này đã kết thúc.";
            session.EndFromServer();
            MessageChanged?.Invoke();
            navigation.NavigateTo("/login?reason=session-replaced");
        });
        nextConnection.On<BalanceChangedNotification>("BalanceChanged", notification =>
        {
            LastMessage = $"Số dư tài khoản {notification.AccountNumber} vừa thay đổi.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
        });
        nextConnection.On<TransactionReceivedNotification>("TransactionReceived", notification =>
        {
            LastMessage = $"Có cập nhật giao dịch {notification.ReferenceNo}.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
        });
        nextConnection.Reconnecting += _ =>
        {
            LastMessage = "Kết nối realtime đang được khôi phục…";
            MessageChanged?.Invoke();
            return Task.CompletedTask;
        };
        nextConnection.Reconnected += _ =>
        {
            LastMessage = "Đã khôi phục kết nối realtime.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
            return Task.CompletedTask;
        };

        connection = nextConnection;
        connectionAccessToken = boundAccessToken;
        try
        {
            await nextConnection.StartAsync();
        }
        catch
        {
            LastMessage = "Realtime tạm thời không khả dụng; dữ liệu REST vẫn hoạt động.";
            MessageChanged?.Invoke();
        }
    }

    public void ClearMessage()
    {
        LastMessage = null;
        MessageChanged?.Invoke();
    }

    private async Task StopCoreAsync()
    {
        var previousConnection = connection;
        connection = null;
        connectionAccessToken = null;
        if (previousConnection is not null) await previousConnection.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await syncGate.WaitAsync();
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            syncGate.Release();
            syncGate.Dispose();
        }
    }
}
