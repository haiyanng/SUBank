using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.JSInterop;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.Auth;
using SUBank.Contracts.Profiles;
using SUBank.Contracts.Qr;
using SUBank.Contracts.Staff;
using SUBank.Contracts.Statements;
using SUBank.Contracts.Transactions;
using SUBank.Contracts.Transfers;

namespace SUBank.Client.Services;

public sealed class ApiSession(
    HttpClient httpClient,
    NavigationManager navigation,
    IJSRuntime jsRuntime,
    ILogger<ApiSession> logger) : IAsyncDisposable
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);
    private const string BlockedTabSession = "__blocked__";
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly object bankingDataChangeSync = new();
    private CancellationTokenSource? bankingDataChangeCancellation;
    private CancellationTokenSource? customerExpiryCancellation;
    private DotNetObjectReference<ApiSession>? browserCallbackReference;
    private TimeSpan customerSessionLifetime;
    private DateTimeOffset customerSessionStartedUtc;
    private long customerSessionStartedTimestamp;
    private long sessionGeneration;

    public AuthResponse? Current { get; private set; }
    internal long Generation => Interlocked.Read(ref sessionGeneration);
    public event Action? Changed;
    public event Action? BankingDataChanged;

    public async Task<SessionRestoreResult> TryRestoreAsync()
    {
        if (Current is not null) return SessionRestoreResult.Restored;

        await refreshGate.WaitAsync();
        try
        {
            if (Current is not null) return SessionRestoreResult.Restored;
            await EnsureBrowserCallbackAsync();
            var expectedTabSessionId = await GetTabSessionIdAsync();
            if (string.Equals(expectedTabSessionId, BlockedTabSession, StringComparison.Ordinal))
                return SessionRestoreResult.NoSession;
            if (await IsLogoutPendingAsync(expectedTabSessionId)) return SessionRestoreResult.NoSession;

            return await RefreshCoreAsync(
                    bootstrap: true,
                    expectedBootstrapSessionId: expectedTabSessionId)
                ? SessionRestoreResult.Restored
                : SessionRestoreResult.NoSession;
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
            await EnsureBrowserCallbackAsync();
            var loginGeneration = Generation;
            using var response = await SendCookieAuthRequestAsync(
                "login",
                "api/auth/login",
                HttpMethod.Post,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Content-Type"] = "application/json"
                },
                JsonSerializer.Serialize(new LoginRequest(userName, password), WebJsonOptions));
            await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
            var session = await ReadLoginAuthResponseAsync(response);
            if (Generation != loginGeneration)
                throw await RejectLoginSessionAsync(
                    session,
                    "Yêu cầu đăng nhập đã bị thay thế bởi một thay đổi phiên mới hơn.");
            if (!await SetTabSessionIdAsync(session.SessionId))
                throw await RejectLoginSessionAsync(
                    session,
                    "Trình duyệt không thể cô lập phiên đăng nhập cho tab này.");
            if (await IsLogoutPendingAsync(session.SessionId))
                throw await RejectLoginSessionAsync(
                    session,
                    "Yêu cầu đăng nhập bị hủy vì phiên này vừa được đánh dấu đăng xuất ở tab khác.");

            Interlocked.Increment(ref sessionGeneration);
            SetCurrent(session);
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
            await EnsureBrowserCallbackAsync();
            var current = Current;
            var expectedSessionId = current?.SessionId;
            var expectedAccessToken = current?.AccessToken;
            if (string.IsNullOrWhiteSpace(expectedSessionId) || string.IsNullOrWhiteSpace(expectedAccessToken))
                throw new LogoutNotConfirmedException("Không có phiên cục bộ hợp lệ để xác nhận đăng xuất.");

            ClearLogicalSession();
            await BlockTabRestoreAsync(expectedSessionId);

            var sessionRevoked = false;
            try
            {
                using var response = await SendCookieAuthRequestAsync(
                    "logout",
                    "api/auth/logout",
                    HttpMethod.Post,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [AuthProtocol.CsrfHeader] = "1",
                        [AuthProtocol.SessionIdHeader] = expectedSessionId
                    },
                    logoutSessionId: expectedSessionId);
                sessionRevoked = response.IsSuccessStatusCode && HasConfirmationHeader(
                    response,
                    AuthProtocol.SessionRevokedHeader);

                if (!sessionRevoked)
                    logger.LogWarning(
                        "Logout bằng refresh cookie chưa xác nhận thu hồi phiên; HTTP {StatusCode}",
                        (int)response.StatusCode);
            }
            catch (Exception exception) when (exception is
                HttpRequestException or TaskCanceledException or InvalidOperationException or JSException)
            {
                logger.LogWarning(
                    exception,
                    "Logout bằng refresh cookie thất bại; chuyển sang thu hồi bằng bearer");
            }

            if (!sessionRevoked)
                sessionRevoked = await TryRevokeSessionByBearerAsync(expectedAccessToken);

            if (!sessionRevoked)
                throw new LogoutNotConfirmedException(
                    "Server chưa xác nhận thu hồi phiên. Thiết bị đã khóa giao diện; vui lòng đăng nhập lại để tiếp tục.");
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public bool IsInRole(string role) => Current?.User.Roles.Contains(role) == true;
    internal void NotifyBankingDataChanged()
    {
        CancellationTokenSource cancellation;
        lock (bankingDataChangeSync)
        {
            bankingDataChangeCancellation?.Cancel();
            cancellation = new CancellationTokenSource();
            bankingDataChangeCancellation = cancellation;
        }

        _ = NotifyBankingDataChangedAsync(cancellation);
    }
    internal async Task<string?> GetFreshAccessTokenAsync()
    {
        if (Current is null) return null;

        var expectedGeneration = Generation;
        await EnsureFreshAccessTokenAsync(expectedGeneration);
        return Generation == expectedGeneration ? Current?.AccessToken : null;
    }

    internal async Task<bool> EndFromServerAsync(long expectedGeneration)
    {
        await refreshGate.WaitAsync();
        try
        {
            if (Generation != expectedGeneration || Current is null) return false;

            var sessionId = Current.SessionId;
            ClearLogicalSession();
            await BlockTabRestoreAsync(sessionId);
            return true;
        }
        finally
        {
            refreshGate.Release();
        }
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
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
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
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
        var result = await response.Content.ReadFromJsonAsync<TransferResponse>();
        NotifyBankingDataChanged();
        return result;
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
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
        var result = await response.Content.ReadFromJsonAsync<CashDepositResponse>();
        NotifyBankingDataChanged();
        return result;
    }

    public Task<List<CustomerManagementSummary>?> GetCustomersAsync(string? search = null)
    {
        var uri = "api/admin/customers";
        if (!string.IsNullOrWhiteSpace(search))
            uri += $"?search={Uri.EscapeDataString(search.Trim())}";

        return GetAsync<List<CustomerManagementSummary>>(uri);
    }

    public Task<CustomerManagementDetail?> GetCustomerAsync(string userName) =>
        GetAsync<CustomerManagementDetail>($"api/admin/customers/{Uri.EscapeDataString(userName)}");

    public Task<List<AuditLogSummary>?> GetAuditLogsAsync() => GetAsync<List<AuditLogSummary>>("api/admin/audit-logs");

    public async Task SuspendCustomerAsync(string userName, SuspendCustomerRequest request)
    {
        using var response = await SendAuthorizedAsync(() =>
            Authorized(
                HttpMethod.Post,
                $"api/admin/customers/{Uri.EscapeDataString(userName)}/suspend",
                JsonContent.Create(request)));
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
    }

    public async Task ResumeCustomerAsync(string userName)
    {
        using var response = await SendAuthorizedAsync(() =>
            Authorized(HttpMethod.Post, $"api/admin/customers/{Uri.EscapeDataString(userName)}/resume"));
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
    }

    public async Task UnlockCustomerIdentityAsync(string userName)
    {
        using var response = await SendAuthorizedAsync(() =>
            Authorized(
                HttpMethod.Post,
                $"api/admin/customers/{Uri.EscapeDataString(userName)}/identity-lockout/unlock"));
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
    }

    private async Task<T?> GetAsync<T>(string uri)
    {
        using var response = await SendAuthorizedAsync(() => Authorized(HttpMethod.Get, uri));
        await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> requestFactory)
    {
        var sentGeneration = Generation;
        await EnsureFreshAccessTokenAsync(sentGeneration);
        if (Generation != sentGeneration)
            throw SessionChangedDuringRequest();

        var sentWithAccessToken = Current?.AccessToken;
        var sentByCustomer = IsCustomer(Current);
        var response = await SendOnceAsync(requestFactory, sentWithAccessToken);
        if (Generation != sentGeneration)
        {
            response.Dispose();
            throw SessionChangedDuringRequest();
        }

        if (response.StatusCode != HttpStatusCode.Unauthorized || !HasBearerChallenge(response))
            return response;

        response.Dispose();
        if (sentByCustomer)
            throw await ExpireCurrentSessionAsync(sentGeneration);

        var refreshed = await RefreshSingleFlightAsync(sentWithAccessToken, force: true);
        if (Generation != sentGeneration)
            throw SessionChangedDuringRequest();
        if (!refreshed)
            throw await ExpireCurrentSessionAsync(sentGeneration);

        var retryAccessToken = Current?.AccessToken;
        var retryResponse = await SendOnceAsync(requestFactory, retryAccessToken);
        if (Generation != sentGeneration)
        {
            retryResponse.Dispose();
            throw SessionChangedDuringRequest();
        }
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized && HasBearerChallenge(retryResponse))
        {
            retryResponse.Dispose();
            throw await ExpireCurrentSessionAsync(sentGeneration);
        }

        return retryResponse;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> requestFactory,
        string? accessToken)
    {
        using var request = requestFactory();
        request.Headers.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
        return await httpClient.SendAsync(request);
    }

    private async Task EnsureFreshAccessTokenAsync(long expectedGeneration)
    {
        var current = Current;
        if (current is null) return;

        if (IsCustomer(current)) return;

        if (current.ExpiresAtUtc > DateTimeOffset.UtcNow + RefreshSkew) return;

        var refreshed = await RefreshSingleFlightAsync(current.AccessToken, force: false);
        if (Generation != expectedGeneration)
            throw SessionChangedDuringRequest();
        if (!refreshed)
            throw await ExpireCurrentSessionAsync(expectedGeneration);
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

    private async Task<bool> RefreshCoreAsync(
        bool bootstrap,
        string? expectedBootstrapSessionId = null)
    {
        var expectedGeneration = Generation;
        var expectedSessionId = Current?.SessionId;
        var sessionIdForPendingLogout = expectedSessionId ?? expectedBootstrapSessionId;
        if (await IsLogoutPendingAsync(sessionIdForPendingLogout)) return false;

        if (Generation != expectedGeneration ||
            await IsLogoutPendingAsync(sessionIdForPendingLogout)) return false;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await SendCookieAuthRequestAsync(
                "refresh",
                "api/auth/refresh",
                HttpMethod.Post,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AuthProtocol.CsrfHeader] = "1"
                });
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    if (bootstrap) await BlockTabRestoreAsync(expectedBootstrapSessionId);
                    return false;
                }
                if (response.StatusCode == HttpStatusCode.Conflict && attempt == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                    if (Generation != expectedGeneration) return Current is not null;
                    continue;
                }
                await EnsureSuccessAsync(response, redirectOnUnauthorized: false);
                return false;
            }

            var refreshed = await ReadAuthResponseAsync(response);
            if (Generation != expectedGeneration) return false;
            if (await IsLogoutPendingAsync(refreshed.SessionId))
            {
                await BlockTabRestoreAsync(refreshed.SessionId);
                await TryRevokeSessionByBearerAsync(refreshed.AccessToken);
                return false;
            }
            if (bootstrap && !string.IsNullOrWhiteSpace(expectedBootstrapSessionId) &&
                !string.Equals(expectedBootstrapSessionId, refreshed.SessionId, StringComparison.Ordinal))
            {
                await BlockTabRestoreAsync(expectedBootstrapSessionId);
                return false;
            }
            if (!bootstrap && !string.Equals(expectedSessionId, refreshed.SessionId, StringComparison.Ordinal))
                return false;

            if (bootstrap)
            {
                if (!await SetTabSessionIdAsync(refreshed.SessionId))
                {
                    await BlockTabRestoreAsync();
                    return false;
                }
                Interlocked.Increment(ref sessionGeneration);
            }
            SetCurrent(refreshed);
            return true;
        }

        return false;
    }

    private static async Task<AuthResponse> ReadAuthResponseAsync(HttpResponseMessage response)
    {
        var session = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (session is null || string.IsNullOrWhiteSpace(session.AccessToken) ||
            !Guid.TryParseExact(session.SessionId, "N", out var parsedSessionId) ||
            !string.Equals(session.SessionId, parsedSessionId.ToString("N"), StringComparison.Ordinal) ||
            session.ExpiresInMilliseconds is < 0 or > 3_600_000 ||
            session.User is null || string.IsNullOrWhiteSpace(session.User.UserName) || session.User.Roles is null)
            throw new InvalidOperationException("API không trả về thông tin phiên đăng nhập hợp lệ.");

        return session;
    }

    private async Task<AuthResponse> ReadLoginAuthResponseAsync(HttpResponseMessage response)
    {
        AuthResponse session;
        try
        {
            session = await ReadAuthResponseAsync(response);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            var serverRevoked = false;
            if (TryGetCanonicalSessionHeader(response, out var issuedSessionId))
            {
                await BlockTabRestoreAsync(issuedSessionId);
                serverRevoked = await TryRevokeSessionByCookieAsync(issuedSessionId);
            }
            else
            {
                await BlockTabRestoreAsync();
            }

            throw new InvalidOperationException(serverRevoked
                ? "API trả về dữ liệu đăng nhập không hợp lệ. Phiên vừa tạo đã được server thu hồi."
                : "API trả về dữ liệu đăng nhập không hợp lệ và server chưa xác nhận thu hồi phiên vừa tạo.",
                exception);
        }

        if (TryGetCanonicalSessionHeader(response, out var responseSessionId) &&
            !string.Equals(responseSessionId, session.SessionId, StringComparison.Ordinal))
        {
            var headerSessionBlocked = await BlockTabRestoreAsync(responseSessionId);
            var bodySessionBlocked = await BlockTabRestoreAsync(session.SessionId);
            var headerSessionRevoked = await TryRevokeSessionByCookieAsync(responseSessionId);
            var bodySessionRevoked = await TryRevokeSessionByBearerAsync(session.AccessToken);
            throw new InvalidOperationException(
                headerSessionRevoked && bodySessionRevoked
                    ? "API trả về định danh phiên không nhất quán; cả hai phiên liên quan đã được server thu hồi."
                    : headerSessionBlocked && bodySessionBlocked
                        ? "API trả về định danh phiên không nhất quán. Trình duyệt đã chặn cả hai phiên, nhưng server chưa xác nhận thu hồi đầy đủ."
                        : "API trả về định danh phiên không nhất quán và chưa thể xác nhận chặn hoặc thu hồi đầy đủ các phiên liên quan.");
        }

        return session;
    }

    private static bool TryGetCanonicalSessionHeader(
        HttpResponseMessage response,
        out string canonicalSessionId)
    {
        if (response.Headers.TryGetValues(AuthProtocol.SessionIdHeader, out var values))
        {
            var sessionId = values.SingleOrDefault();
            if (TryNormalizeSessionId(sessionId, out canonicalSessionId)) return true;
        }

        canonicalSessionId = string.Empty;
        return false;
    }

    private async Task<InvalidOperationException> ExpireCurrentSessionAsync(long expectedGeneration)
    {
        await refreshGate.WaitAsync();
        try
        {
            if (Generation != expectedGeneration) return SessionChangedDuringRequest();

            var sessionId = Current?.SessionId;
            ClearLogicalSession();
            navigation.NavigateTo("/login?reason=session-expired", replace: true);
            await BlockTabRestoreAsync(sessionId);
            return new InvalidOperationException("Phiên đăng nhập đã hết hạn hoặc không còn hiệu lực.");
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private static InvalidOperationException SessionChangedDuringRequest() =>
        new("Phiên đăng nhập đã thay đổi trong khi xử lý yêu cầu. Kết quả cũ đã được bỏ qua an toàn.");

    private void ClearLogicalSession()
    {
        CancelBankingDataChangeNotification();
        CancelCustomerExpiryTimer();
        Interlocked.Increment(ref sessionGeneration);
        Current = null;
        Changed?.Invoke();
    }

    private void SetCurrent(AuthResponse current)
    {
        CancelCustomerExpiryTimer();
        Current = current;
        if (IsCustomer(current))
        {
            customerSessionLifetime = TimeSpan.FromMilliseconds(Math.Max(0, current.ExpiresInMilliseconds));
            customerSessionStartedUtc = DateTimeOffset.UtcNow;
            customerSessionStartedTimestamp = Stopwatch.GetTimestamp();
            customerExpiryCancellation = new CancellationTokenSource();
            _ = ExpireCustomerSessionAsync(
                customerSessionLifetime,
                Generation,
                customerExpiryCancellation.Token);
        }

        Changed?.Invoke();
    }

    private async Task ExpireCustomerSessionAsync(
        TimeSpan remainingLifetime,
        long expectedGeneration,
        CancellationToken cancellationToken)
    {
        try
        {
            if (remainingLifetime > TimeSpan.Zero)
                await Task.Delay(remainingLifetime, cancellationToken);

            await refreshGate.WaitAsync(cancellationToken);
            try
            {
                if (Generation != expectedGeneration || !IsCustomer(Current)) return;

                var sessionId = Current?.SessionId;
                ClearLogicalSession();
                navigation.NavigateTo("/login?reason=session-expired", replace: true);
                await BlockTabRestoreAsync(sessionId);
            }
            finally
            {
                refreshGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Login, logout hoặc một phiên mới đã thay thế timer này.
        }
    }

    private void CancelCustomerExpiryTimer()
    {
        var cancellation = customerExpiryCancellation;
        customerExpiryCancellation = null;
        customerSessionLifetime = TimeSpan.Zero;
        customerSessionStartedUtc = default;
        customerSessionStartedTimestamp = 0;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    [JSInvokable]
    public async Task OnBrowserSessionSignalAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            if (await IsLogoutPendingAsync(Current?.SessionId))
            {
                var sessionId = Current?.SessionId;
                if (Current is not null)
                {
                    ClearLogicalSession();
                    navigation.NavigateTo("/login?reason=logout-unconfirmed", replace: true);
                }

                await BlockTabRestoreAsync(sessionId);

                return;
            }

            if (!IsCustomer(Current) || !HasCustomerSessionExpired()) return;

            var expiredSessionId = Current?.SessionId;
            ClearLogicalSession();
            navigation.NavigateTo("/login?reason=session-expired", replace: true);
            await BlockTabRestoreAsync(expiredSessionId);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private bool HasCustomerSessionExpired()
    {
        if (customerSessionLifetime <= TimeSpan.Zero || customerSessionStartedTimestamp == 0) return true;

        var monotonicElapsed = Stopwatch.GetElapsedTime(customerSessionStartedTimestamp);
        var wallClockElapsed = DateTimeOffset.UtcNow - customerSessionStartedUtc;
        return monotonicElapsed >= customerSessionLifetime || wallClockElapsed >= customerSessionLifetime;
    }

    private async Task EnsureBrowserCallbackAsync()
    {
        if (browserCallbackReference is not null) return;

        var callbackReference = DotNetObjectReference.Create(this);
        try
        {
            await jsRuntime.InvokeVoidAsync("subankSession.subscribe", callbackReference);
            browserCallbackReference = callbackReference;
        }
        catch (JSException exception)
        {
            callbackReference.Dispose();
            logger.LogWarning(exception, "Không thể đăng ký browser session callback");
        }
    }

    private async Task<bool> IsLogoutPendingAsync(string? sessionId)
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>("subankSession.isLogoutPending", sessionId);
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "Không thể đọc trạng thái logout của trình duyệt");
            return true;
        }
    }

    private async Task<string?> GetTabSessionIdAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("subankSession.getTabSessionId");
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "Không thể đọc định danh phiên của tab");
            return BlockedTabSession;
        }
    }

    private async Task<bool> SetTabSessionIdAsync(string sessionId)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("subankSession.setTabSessionId", sessionId);
            return true;
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "Không thể lưu định danh phiên của tab");
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendCookieAuthRequestAsync(
        string type,
        string relativeUri,
        HttpMethod method,
        IReadOnlyDictionary<string, string>? headers = null,
        string? body = null,
        string? logoutSessionId = null)
    {
        var baseAddress = httpClient.BaseAddress ??
            throw new InvalidOperationException("Client chưa được cấu hình địa chỉ API.");
        BrowserAuthResponse? browserResponse;
        try
        {
            browserResponse = await jsRuntime.InvokeAsync<BrowserAuthResponse?>(
                "subankSession.sendCookieRequest",
                type,
                new Uri(baseAddress, relativeUri).ToString(),
                method.Method,
                headers,
                body,
                logoutSessionId);
        }
        catch (JSException exception)
        {
            throw new InvalidOperationException(
                "Trình duyệt không thể điều phối yêu cầu xác thực an toàn.",
                exception);
        }

        if (browserResponse is null)
            throw new InvalidOperationException("Trình duyệt không trả về kết quả xác thực hợp lệ.");

        if (browserResponse.Status == 0)
        {
            throw browserResponse.Error switch
            {
                "web-locks-unavailable" => new InvalidOperationException(
                    "Trình duyệt không hỗ trợ Web Locks để bảo vệ cookie đăng nhập. Hãy dùng Chrome hoặc Edge phiên bản mới."),
                "coordination-timeout" => new InvalidOperationException(
                    "Một tab khác đang xử lý đăng nhập hoặc đăng xuất quá lâu. Vui lòng thử lại."),
                "coordination-storage-unavailable" => new InvalidOperationException(
                    "Trình duyệt không thể lưu trạng thái đăng xuất an toàn. Vui lòng kiểm tra quyền lưu trữ của trang."),
                "login-response-unreadable-revoked" => new InvalidOperationException(
                    "Trình duyệt không đọc được phản hồi đăng nhập. Phiên vừa tạo đã được server thu hồi; vui lòng thử lại."),
                "login-response-unreadable-unconfirmed" => new InvalidOperationException(
                    "Trình duyệt không đọc được phản hồi đăng nhập và server chưa xác nhận thu hồi phiên vừa tạo. Vui lòng kiểm tra kết nối rồi thử lại."),
                "auth-response-invalid-revoked" => new InvalidOperationException(
                    "Dịch vụ trả về dữ liệu phiên không hợp lệ. Phiên liên quan đã được server thu hồi; vui lòng thử lại."),
                "auth-response-invalid-unconfirmed" => new InvalidOperationException(
                    "Dịch vụ trả về dữ liệu phiên không hợp lệ và server chưa xác nhận thu hồi phiên liên quan."),
                "auth-session-mismatch-revoked" => new InvalidOperationException(
                    "Dịch vụ trả về định danh phiên không nhất quán. Phiên liên quan đã được server thu hồi; vui lòng thử lại."),
                "auth-session-mismatch-unconfirmed" => new InvalidOperationException(
                    "Dịch vụ trả về định danh phiên không nhất quán và server chưa xác nhận thu hồi đầy đủ."),
                "request-timeout" => new TaskCanceledException(
                    "Dịch vụ xác thực phản hồi quá chậm."),
                "network-error" or "response-body-error" => new HttpRequestException(
                    "Không thể kết nối đến dịch vụ xác thực."),
                _ => new InvalidOperationException("Yêu cầu xác thực trên trình duyệt không hoàn tất an toàn.")
            };
        }

        if (browserResponse.Status is < 100 or > 599)
            throw new InvalidOperationException("Trình duyệt trả về mã HTTP không hợp lệ.");

        var content = new StringContent(browserResponse.Body ?? string.Empty, Encoding.UTF8);
        content.Headers.ContentType = null;
        if (!string.IsNullOrWhiteSpace(browserResponse.ContentType))
            content.Headers.TryAddWithoutValidation("Content-Type", browserResponse.ContentType);

        var response = new HttpResponseMessage((HttpStatusCode)browserResponse.Status)
        {
            Content = content
        };
        CopyResponseHeader(response, "WWW-Authenticate", browserResponse.WwwAuthenticate);
        CopyResponseHeader(response, "X-Correlation-ID", browserResponse.CorrelationId);
        CopyResponseHeader(response, AuthProtocol.SessionIdHeader, browserResponse.SessionId);
        CopyResponseHeader(
            response,
            AuthProtocol.RefreshCookieClearedHeader,
            browserResponse.RefreshCookieCleared);
        CopyResponseHeader(response, AuthProtocol.SessionRevokedHeader, browserResponse.SessionRevoked);
        return response;
    }

    private static void CopyResponseHeader(
        HttpResponseMessage response,
        string headerName,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            response.Headers.TryAddWithoutValidation(headerName, value);
    }

    private static bool HasConfirmationHeader(HttpResponseMessage response, string headerName) =>
        response.Headers.TryGetValues(headerName, out var values) &&
        values.Contains("1", StringComparer.Ordinal);

    private static bool TryNormalizeSessionId(string? sessionId, out string canonicalSessionId)
    {
        if (Guid.TryParseExact(sessionId, "N", out var parsedSessionId))
        {
            canonicalSessionId = parsedSessionId.ToString("N");
            return true;
        }

        canonicalSessionId = string.Empty;
        return false;
    }

    private async Task<bool> TryRevokeSessionByCookieAsync(string sessionId)
    {
        try
        {
            using var response = await SendCookieAuthRequestAsync(
                "logout",
                "api/auth/logout",
                HttpMethod.Post,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [AuthProtocol.CsrfHeader] = "1",
                    [AuthProtocol.SessionIdHeader] = sessionId
                },
                logoutSessionId: sessionId);
            if (response.IsSuccessStatusCode && HasConfirmationHeader(
                response,
                AuthProtocol.SessionRevokedHeader)) return true;

            logger.LogWarning(
                "Server chưa xác nhận thu hồi phiên lỗi bằng cookie; HTTP {StatusCode}",
                (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is
            HttpRequestException or TaskCanceledException or InvalidOperationException or JSException)
        {
            logger.LogWarning(exception, "Không thể thu hồi phiên lỗi bằng cookie");
            return false;
        }
    }

    private async Task<bool> TryRevokeSessionByBearerAsync(string accessToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/reject-session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
            using var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode) return true;

            logger.LogWarning(
                "Server chưa xác nhận thu hồi phiên bằng bearer; HTTP {StatusCode}",
                (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JSException)
        {
            logger.LogWarning(exception, "Không thể thu hồi phiên bằng bearer");
            return false;
        }
    }

    private async Task<InvalidOperationException> RejectLoginSessionAsync(
        AuthResponse session,
        string reason)
    {
        var browserBlocked = await BlockTabRestoreAsync(session.SessionId);
        var serverRevoked = await TryRevokeSessionByBearerAsync(session.AccessToken);
        if (serverRevoked)
            return new InvalidOperationException($"{reason} Phiên mới đã được server thu hồi.");

        return new InvalidOperationException(browserBlocked
            ? $"{reason} Trình duyệt đã chặn khôi phục phiên này, nhưng server chưa xác nhận thu hồi."
            : $"{reason} Không thể xác nhận chặn khôi phục hoặc thu hồi phiên; vui lòng đóng tab và thử lại khi kết nối ổn định.");
    }

    private async Task<bool> BlockTabRestoreAsync(string? sessionId = null)
    {
        var tabBlocked = false;
        try
        {
            await jsRuntime.InvokeVoidAsync("subankSession.setTabSessionId", BlockedTabSession);
            tabBlocked = true;
        }
        catch (JSException exception)
        {
            logger.LogWarning(exception, "Không thể chặn khôi phục phiên cũ của tab");
        }

        if (!TryNormalizeSessionId(sessionId, out var canonicalSessionId)) return tabBlocked;

        try
        {
            var intentStored = await jsRuntime.InvokeAsync<bool>(
                "subankSession.blockSession",
                canonicalSessionId);
            if (!intentStored)
                logger.LogWarning("Không thể lưu dấu chặn khôi phục dùng chung cho phiên");
            return tabBlocked && intentStored;
        }
        catch (JSException exception)
        {
            logger.LogWarning(
                exception,
                "Không thể lưu dấu chặn khôi phục dùng chung cho phiên");
            return false;
        }
    }

    private async Task NotifyBankingDataChangedAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellation.Token);
            if (BankingDataChanged is { } handlers)
            {
                foreach (Action handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler();
                    }
                    catch
                    {
                        // Một UI subscriber lỗi không được làm hỏng notification debounce.
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Nhiều SignalR event của cùng giao dịch được gộp thành một lần tải lại.
        }
        finally
        {
            lock (bankingDataChangeSync)
            {
                if (ReferenceEquals(bankingDataChangeCancellation, cancellation))
                    bankingDataChangeCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelBankingDataChangeNotification()
    {
        lock (bankingDataChangeSync)
        {
            bankingDataChangeCancellation?.Cancel();
            bankingDataChangeCancellation = null;
        }
    }

    private static bool IsCustomer(AuthResponse? session) =>
        session?.User.Roles.Contains("Customer", StringComparer.Ordinal) == true;

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
            throw await ExpireCurrentSessionAsync(Generation);
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

        if (response.StatusCode == HttpStatusCode.Forbidden)
            navigation.NavigateTo("/403", replace: true);

        var message = problem?.Detail ?? problem?.Title ?? response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.ServiceUnavailable => "Dịch vụ đang tạm thời không khả dụng. Vui lòng thử lại.",
            _ => $"API trả về lỗi {(int)response.StatusCode}."
        };
        throw new ApiRequestException(response.StatusCode, message, problem?.CorrelationId);
    }

    private sealed record ApiProblem(string? Title, string? Detail, string? CorrelationId);
    private sealed record BrowserAuthResponse(
        int Status,
        string? Body,
        string? ContentType,
        string? WwwAuthenticate,
        string? CorrelationId,
        string? SessionId,
        string? RefreshCookieCleared,
        string? SessionRevoked,
        string? Error);

    public async ValueTask DisposeAsync()
    {
        CancelBankingDataChangeNotification();
        CancelCustomerExpiryTimer();
        if (browserCallbackReference is not null)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("subankSession.unsubscribe");
            }
            catch (JSException)
            {
                // Ứng dụng đang đóng nên không còn hành động khôi phục hữu ích nào.
            }

            browserCallbackReference.Dispose();
            browserCallbackReference = null;
        }

        refreshGate.Dispose();
    }
}

public enum SessionRestoreResult
{
    NoSession,
    Restored
}

public sealed class ApiRequestException(
    HttpStatusCode statusCode,
    string message,
    string? correlationId = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? CorrelationId { get; } = correlationId;
    public bool IsTransient =>
        StatusCode == HttpStatusCode.RequestTimeout ||
        StatusCode == HttpStatusCode.TooManyRequests ||
        (int)StatusCode >= 500;
}

public sealed class LogoutNotConfirmedException(string message) : Exception(message);
