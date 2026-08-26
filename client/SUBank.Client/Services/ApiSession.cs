using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.AddressChanges;
using SUBank.Contracts.Auth;
using SUBank.Contracts.Staff;
using SUBank.Contracts.Transactions;
using SUBank.Contracts.Transfers;

namespace SUBank.Client.Services;

public sealed class ApiSession(HttpClient httpClient)
{
    public AuthResponse? Current { get; private set; }
    public event Action? Changed;
    public event Action? BankingDataChanged;

    public async Task TryRestoreAsync()
    {
        if (Current is not null) return;
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh");
        request.Headers.Add("X-SUBank-CSRF", "1");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return;
        Current = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Changed?.Invoke();
    }

    public async Task LoginAsync(string userName, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
        { Content = JsonContent.Create(new LoginRequest(userName, password)) };
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        Current = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Changed?.Invoke();
    }

    public async Task LogoutAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
        request.Headers.Add("X-SUBank-CSRF", "1");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        await httpClient.SendAsync(request);
        Current = null;
        Changed?.Invoke();
    }

    public bool IsInRole(string role) => Current?.User.Roles.Contains(role) == true;
    internal void NotifyBankingDataChanged() => BankingDataChanged?.Invoke();
    internal void EndFromServer()
    {
        Current = null;
        Changed?.Invoke();
    }
    public Task<List<AccountSummary>?> GetAccountsAsync() => GetAsync<List<AccountSummary>>("api/accounts");
    public Task<List<TransactionSummary>?> GetTransactionsAsync(string account) => GetAsync<List<TransactionSummary>>($"api/accounts/{account}/transactions");
    public Task<TransactionDetail?> GetTransactionAsync(string referenceNo) => GetAsync<TransactionDetail>($"api/transactions/{referenceNo}");

    public async Task<TransferResponse?> TransferAsync(TransferRequest model)
    {
        using var request = Authorized(HttpMethod.Post, "api/transfers", JsonContent.Create(model));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TransferResponse>();
    }

    public async Task<CashDepositResponse?> DepositAsync(CashDepositRequest model)
    {
        using var request = Authorized(HttpMethod.Post, "api/teller/cash-deposits", JsonContent.Create(model));
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CashDepositResponse>();
    }

    public Task<List<LockedUserSummary>?> GetLockedUsersAsync() => GetAsync<List<LockedUserSummary>>("api/admin/locked-users");
    public Task<List<AuditLogSummary>?> GetAuditLogsAsync() => GetAsync<List<AuditLogSummary>>("api/admin/audit-logs");
    public Task<List<AddressChangeRequestSummary>?> GetAddressChangesAsync() =>
        GetAsync<List<AddressChangeRequestSummary>>("api/address-change-requests");
    public Task<List<AddressChangeRequestSummary>?> GetPendingAddressChangesAsync() =>
        GetAsync<List<AddressChangeRequestSummary>>("api/admin/address-change-requests/pending");

    public async Task CreateAddressChangeAsync(CreateAddressChangeRequest model)
    {
        using var request = Authorized(HttpMethod.Post, "api/address-change-requests", JsonContent.Create(model));
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task ApproveAddressChangeAsync(string requestNo)
    {
        using var request = Authorized(HttpMethod.Post,
            $"api/admin/address-change-requests/{Uri.EscapeDataString(requestNo)}/approve");
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task RejectAddressChangeAsync(string requestNo, string reason)
    {
        using var request = Authorized(HttpMethod.Post,
            $"api/admin/address-change-requests/{Uri.EscapeDataString(requestNo)}/reject",
            JsonContent.Create(new RejectAddressChangeRequest(reason)));
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task UnlockAsync(string userName)
    {
        using var request = Authorized(HttpMethod.Post, $"api/admin/users/{Uri.EscapeDataString(userName)}/unlock");
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    private async Task<T?> GetAsync<T>(string uri)
    {
        using var request = Authorized(HttpMethod.Get, uri);
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private HttpRequestMessage Authorized(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        if (Current is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Current.AccessToken);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var problem = await response.Content.ReadFromJsonAsync<ApiProblem>();
        throw new InvalidOperationException(problem?.Detail ?? problem?.Title ?? $"API trả về lỗi {(int)response.StatusCode}.");
    }

    private sealed record ApiProblem(string? Title, string? Detail);
}
