using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SUBank.Contracts.Realtime;

namespace SUBank.Client.Services;

public sealed class RealtimeService(ApiSession session, HttpClient httpClient, NavigationManager navigation) : IAsyncDisposable
{
    private HubConnection? connection;
    public string? LastMessage { get; private set; }
    public event Action? MessageChanged;

    public async Task SyncAsync()
    {
        if (session.Current is null)
        {
            await StopAsync();
            return;
        }
        if (connection?.State is HubConnectionState.Connected or HubConnectionState.Connecting) return;

        await StopAsync();
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(httpClient.BaseAddress!, "hubs/banking"), options =>
                options.AccessTokenProvider = () => Task.FromResult(session.Current?.AccessToken))
            .WithAutomaticReconnect()
            .Build();
        connection.On("ForceLogout", () =>
        {
            LastMessage = "Tài khoản đã đăng nhập ở nơi khác. Phiên này đã kết thúc.";
            session.EndFromServer();
            MessageChanged?.Invoke();
            navigation.NavigateTo("/login?reason=session-replaced");
        });
        connection.On<BalanceChangedNotification>("BalanceChanged", notification =>
        {
            LastMessage = $"Số dư tài khoản {notification.AccountNumber} vừa thay đổi.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
        });
        connection.On<TransactionReceivedNotification>("TransactionReceived", notification =>
        {
            LastMessage = $"Có cập nhật giao dịch {notification.ReferenceNo}.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
        });
        connection.Reconnecting += _ =>
        {
            LastMessage = "Kết nối realtime đang được khôi phục…";
            MessageChanged?.Invoke();
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            LastMessage = "Đã khôi phục kết nối realtime.";
            session.NotifyBankingDataChanged();
            MessageChanged?.Invoke();
            return Task.CompletedTask;
        };
        try
        {
            await connection.StartAsync();
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

    private async Task StopAsync()
    {
        if (connection is null) return;
        await connection.DisposeAsync();
        connection = null;
    }

    public ValueTask DisposeAsync() => connection is null ? ValueTask.CompletedTask : connection.DisposeAsync();
}
