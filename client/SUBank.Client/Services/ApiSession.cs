using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.Auth;
using SUBank.Contracts.Staff;
using SUBank.Contracts.Statements;
using SUBank.Contracts.Qr;
using SUBank.Contracts.Profiles;
using SUBank.Contracts.Transactions;
using SUBank.Contracts.Transfers;

namespace SUBank.Client.Services;

public sealed class ApiSession(HttpClient httpClient, NavigationManager navigation)
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private long sessionGeneration;

    public AuthResponse? Current { get; private set; }
    internal long Generation => Interlocked.Read(ref sessionGeneration);
    public event Action? Changed;
    public event Action? BankingDataChanged;

    public async Task TryRestoreAsync()
    {
        if (Current is not null) return;

        await refreshGate.WaitAsync();
        try
        {
            if (Current is not null) return;
            await RefreshCoreAsync(bootstrap: true);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public async Task LoginAsync(string userName, string password)
    {
        await refreshGate.WaitAsync();
        try
        {
            var loginGeneration = Interlocked.Increment(ref sessionGeneration);
            Current = null;
            Changed?.Invoke();

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login")
            { Content = JsonContent.Create(new LoginRequest(userName, password)) };
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await httpClient.SendAsync(request);
            await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
            var session = await ReadAuthResponseAsync(response);
            if (Generation != loginGeneration)
                throw new InvalidOperationException("Yêu cầu đăng nhập đã bị thay thế bởi một thay đổi phiên mới hơn.");

            Current = session;
            Changed?.Invoke();
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public async Task LogoutAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout");
                request.Headers.Add("X-SUBank-CSRF", "1");
                request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                using var response = await httpClient.SendAsync(request);
            }
            finally
            {
                ClearLogicalSession();
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public bool IsInRole(string role) => Current?.User.Roles.Contains(role) == true;
    internal void NotifyBankingDataChanged() => BankingDataChanged?.Invoke();
    internal async Task<string?> GetFreshAccessTokenAsync()
    {
        if (Current is null) return null;

        await EnsureFreshAccessTokenAsync();
        return Current?.AccessToken;
    }

    internal void EndFromServer()
    {
        ClearLogicalSession();
    }
    public Task<List<AccountSummary>?> GetAccountsAsync() => GetAsync<List<AccountSummary>>("api/accounts");
    public Task<CustomerProfileDetail?> GetProfileAsync() => GetAsync<CustomerProfileDetail>("api/profile");
    public Task<List<TransactionSummary>?> GetTransactionsAsync(string account) => GetAsync<List<TransactionSummary>>($"api/accounts/{account}/transactions");
    public Task<TransactionDetail?> GetTransactionAsync(string referenceNo) => GetAsync<TransactionDetail>($"api/transactions/{referenceNo}");
    public Task<AccountStatement?> GetStatementAsync(string accountNumber, int year, int? month) =>
        GetAsync<AccountStatement>($"api/accounts/{Uri.EscapeDataString(accountNumber)}/statements?year={year}&month={month}");

    public async Task<byte[]> GetStatementPdfAsync(string accountNumber, int year, int? month)
    {
        using var response = await SendAuthorizedAsync(() => Authorized(HttpMethod.Get,
            $"api/accounts/{Uri.EscapeDataString(accountNumber)}/statements/pdf?year={year}&month={month}"));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<GeneratedQr?> GenerateQrAsync(GenerateQrRequest model)
    {
        using var request = Authorized(HttpMethod.Post, "api/qr/generate", JsonContent.Create(model));
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<GeneratedQr>();
    }

    public async Task<QrTransferData?> DecodeQrAsync(byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(image, "image", fileName);
        using var request = Authorized(HttpMethod.Post, "api/qr/decode", form);
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<QrTransferData>();
    }

    public async Task<TransferResponse?> TransferAsync(TransferRequest model, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        using var response = await SendAuthorizedAsync(() =>
        {
            var request = Authorized(HttpMethod.Post, "api/transfers", JsonContent.Create(model));
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return request;
        });
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TransferResponse>();
    }

    public async Task<CashDepositResponse?> DepositAsync(CashDepositRequest model, string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        using var response = await SendAuthorizedAsync(() =>
        {
            var request = Authorized(HttpMethod.Post, "api/teller/cash-deposits", JsonContent.Create(model));
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return request;
        });
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CashDepositResponse>();
    }

    public Task<List<LockedUserSummary>?> GetLockedUsersAsync() => GetAsync<List<LockedUserSummary>>("api/admin/locked-users");
    public Task<List<AuditLogSummary>?> GetAuditLogsAsync() => GetAsync<List<AuditLogSummary>>("api/admin/audit-logs");

    public async Task UnlockAsync(string userName)
    {
        using var response = await SendAuthorizedAsync(() =>
            Authorized(HttpMethod.Post, $"api/admin/users/{Uri.EscapeDataString(userName)}/unlock"));
        await EnsureSuccessAsync(response);
    }

    private async Task<T?> GetAsync<T>(string uri)
    {
        using var response = await SendAuthorizedAsync(() => Authorized(HttpMethod.Get, uri));
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> requestFactory)
    {
        await EnsureFreshAccessTokenAsync();
        var sentWithAccessToken = Current?.AccessToken;
        var response = await SendOnceAsync(requestFactory);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !HasBearerChallenge(response))
            return response;

        response.Dispose();
        if (!await RefreshSingleFlightAsync(sentWithAccessToken, force: true))
            throw ExpireCurrentSession();

        return await SendOnceAsync(requestFactory);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> requestFactory)
    {
        using var request = requestFactory();
        return await httpClient.SendAsync(request);
    }

    private async Task EnsureFreshAccessTokenAsync()
    {
        var current = Current;
        if (current is null || current.ExpiresAtUtc > DateTimeOffset.UtcNow + RefreshSkew) return;

        if (!await RefreshSingleFlightAsync(current.AccessToken, force: false))
            throw ExpireCurrentSession();
    }

    private async Task<bool> RefreshSingleFlightAsync(string? expectedAccessToken, bool force)
    {
        await refreshGate.WaitAsync();
        try
        {
            var current = Current;
            if (current is null) return false;
            if (expectedAccessToken is not null &&
                !string.Equals(current.AccessToken, expectedAccessToken, StringComparison.Ordinal))
                return true;
            if (!force && current.ExpiresAtUtc > DateTimeOffset.UtcNow + RefreshSkew)
                return true;

            return await RefreshCoreAsync(bootstrap: false);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<bool> RefreshCoreAsync(bool bootstrap)
    {
        var expectedGeneration = Generation;
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh");
        request.Headers.Add("X-SUBank-CSRF", "1");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (bootstrap || response.StatusCode == HttpStatusCode.Unauthorized) return false;
            await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
            return false;
        }

        var refreshed = await ReadAuthResponseAsync(response);
        if (Generation != expectedGeneration) return false;

        if (bootstrap) Interlocked.Increment(ref sessionGeneration);
        Current = refreshed;
        Changed?.Invoke();
        return true;
    }

    private static async Task<AuthResponse> ReadAuthResponseAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<AuthResponse>()
        ?? throw new InvalidOperationException("API không trả về thông tin phiên đăng nhập hợp lệ.");

    private InvalidOperationException ExpireCurrentSession()
    {
        ClearLogicalSession();
        navigation.NavigateTo("/login?reason=session-expired", replace: true);
        return new InvalidOperationException("Phiên đăng nhập đã hết hạn hoặc không còn hiệu lực.");
    }

    private void ClearLogicalSession()
    {
        Interlocked.Increment(ref sessionGeneration);
        Current = null;
        Changed?.Invoke();
    }

    private static bool HasBearerChallenge(HttpResponseMessage response) =>
        response.Headers.WwwAuthenticate.Any(x =>
            string.Equals(x.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase));

    private HttpRequestMessage Authorized(HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        if (Current is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Current.AccessToken);
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, bool redirectOnUnauthorized = true)
    {
        if (response.IsSuccessStatusCode) return;

        if (redirectOnUnauthorized && response.StatusCode == HttpStatusCode.Unauthorized &&
            HasBearerChallenge(response))
        {
            throw ExpireCurrentSession();
        }

        ApiProblem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>();
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Một số phản hồi 4xx không có JSON body. Dùng thông báo dự phòng an toàn bên dưới.
        }

        throw new InvalidOperationException(problem?.Detail ?? problem?.Title ?? $"API trả về lỗi {(int)response.StatusCode}.");
    }

    private sealed record ApiProblem(string? Title, string? Detail);
}
