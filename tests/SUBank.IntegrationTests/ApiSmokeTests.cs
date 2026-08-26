using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SUBank.Contracts.Auth;
using SUBank.Contracts.Staff;
using SUBank.Contracts.Transfers;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SUBank.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<SUBankWebApplicationFactory>
{
    private const string AccountA = "1000000001";
    private const string AccountB = "1000000002";
    private readonly SUBankWebApplicationFactory factory;

    public ApiSmokeTests(SUBankWebApplicationFactory factory) => this.factory = factory;

    [Fact]
    public async Task HealthAndSwagger_AreAvailableInDevelopment()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public async Task Auth_LoginMeRefreshLogout_CompletesSessionLifecycle()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
        var login = await LoginAsync(client, "customer.a");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        var me = await client.GetFromJsonAsync<UserSummary>("/api/auth/me");
        Assert.Equal("customer.a", me!.UserName);
        Assert.Contains("Customer", me.Roles);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await PostCookieProtectedAsync(client, "/api/auth/refresh");
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.NotEqual(login.AccessToken, (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await PostCookieProtectedAsync(client, "/api/auth/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostCookieProtectedAsync(client, "/api/auth/refresh")).StatusCode);
    }

    [Fact]
    public async Task Auth_SecondLoginInvalidatesFirstAccessToken()
    {
        using var first = await CreateAuthorizedClientAsync("customer.a");
        using var second = await CreateAuthorizedClientAsync("customer.a");

        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Auth_ThirdInvalidPasswordLocksUser_AndAdminCanUnlock()
    {
        const string userName = "customer.b";
        await EnsureUnlockedAsync(userName);
        using var anonymous = factory.CreateClient();
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "Wrong@12345"))).StatusCode);

            Assert.Equal(HttpStatusCode.Unauthorized,
                (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "Demo@12345"))).StatusCode);
            using var admin = await CreateAuthorizedClientAsync("admin");
            var locked = await admin.GetFromJsonAsync<List<LockedUserSummary>>("/api/admin/locked-users");
            Assert.Contains(locked!, x => x.UserName == userName && x.FailedAttempts >= 3 && x.LockedAtUtc is not null);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/admin/users/{userName}/unlock", null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,
                (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "Demo@12345"))).StatusCode);
        }
        finally { await EnsureUnlockedAsync(userName); }
    }

    [Fact]
    public async Task Authorization_EnforcesAuthenticationRolesAndAccountOwnership()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/accounts")).StatusCode);
        using var customer = await CreateAuthorizedClientAsync("customer.a");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/admin/locked-users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await customer.PostAsJsonAsync("/api/teller/cash-deposits", new CashDepositRequest(AccountA, 1m, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/accounts/{AccountB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/accounts/{AccountB}/transactions")).StatusCode);
        using var admin = await CreateAuthorizedClientAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Transfer_IsAtomicAuditedAndIdempotent()
    {
        var key = $"test-transfer-{Guid.NewGuid():N}";
        var before = await GetBalancesAsync();
        try
        {
            using var customer = await CreateAuthorizedClientAsync("customer.a");
            var request = new TransferRequest(AccountA, AccountB, 12_345.67m, "Integration transfer", "123456");
            using var first = await PostWithIdempotencyAsync(customer, "/api/transfers", key, request);
            using var replay = await PostWithIdempotencyAsync(customer, "/api/transfers", key, request);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var firstBody = await first.Content.ReadFromJsonAsync<TransferResponse>();
            var replayBody = await replay.Content.ReadFromJsonAsync<TransferResponse>();
            Assert.False(firstBody!.Replayed);
            Assert.True(replayBody!.Replayed);
            Assert.Equal(firstBody.ReferenceNo, replayBody.ReferenceNo);
            var after = await GetBalancesAsync();
            Assert.Equal(before[AccountA] - request.Amount, after[AccountA]);
            Assert.Equal(before[AccountB] + request.Amount, after[AccountB]);
            await AssertTransactionAndAuditAsync(key, "TRANSFER");

            using var conflict = await PostWithIdempotencyAsync(customer, "/api/transfers", key, request with { Amount = request.Amount + 1m });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            AssertBalancesEqual(after, await GetBalancesAsync());
        }
        finally { await CleanupTransactionsAsync(key, before); }
    }

    [Fact]
    public async Task Transfer_InvalidRequestRollsBackWithoutTransactionRecord()
    {
        var key = $"test-rollback-{Guid.NewGuid():N}";
        var before = await GetBalancesAsync();
        try
        {
            using var customer = await CreateAuthorizedClientAsync("customer.a");
            using var response = await PostWithIdempotencyAsync(customer, "/api/transfers", key,
                new TransferRequest(AccountA, AccountB, before[AccountA] + 1m, "Insufficient", "123456"));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            AssertBalancesEqual(before, await GetBalancesAsync());
            Assert.False(await TransactionExistsAsync(key));
        }
        finally { await CleanupTransactionsAsync(key, before); }
    }

    [Fact]
    public async Task ConcurrentTransfers_CannotProduceNegativeBalance()
    {
        var prefix = $"test-concurrent-{Guid.NewGuid():N}";
        var before = await GetBalancesAsync();
        var amount = decimal.Round(before[AccountA] * 0.75m, 2);
        try
        {
            using var firstClient = await CreateAuthorizedClientAsync("customer.a");
            var request = new TransferRequest(AccountA, AccountB, amount, "Concurrent transfer", "123456");
            var responses = await Task.WhenAll(
                PostWithIdempotencyAsync(firstClient, "/api/transfers", $"{prefix}-1", request),
                PostWithIdempotencyAsync(firstClient, "/api/transfers", $"{prefix}-2", request));
            foreach (var response in responses) response.Dispose();

            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);
            var after = await GetBalancesAsync();
            Assert.Equal(before[AccountA] - amount, after[AccountA]);
            Assert.Equal(before[AccountB] + amount, after[AccountB]);
            Assert.True(after[AccountA] >= 0);
            Assert.Equal(1, await CountTransactionsAsync(prefix));
        }
        finally { await CleanupTransactionsAsync(prefix, before, true); }
    }

    [Fact]
    public async Task TellerDeposit_IsAtomicAuditedAndIdempotent()
    {
        var key = $"test-deposit-{Guid.NewGuid():N}";
        var before = await GetBalancesAsync();
        try
        {
            using var teller = await CreateAuthorizedClientAsync("teller");
            var request = new CashDepositRequest(AccountA, 54_321.25m, "Integration deposit");
            using var first = await PostWithIdempotencyAsync(teller, "/api/teller/cash-deposits", key, request);
            using var replay = await PostWithIdempotencyAsync(teller, "/api/teller/cash-deposits", key, request);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.False((await first.Content.ReadFromJsonAsync<CashDepositResponse>())!.Replayed);
            Assert.True((await replay.Content.ReadFromJsonAsync<CashDepositResponse>())!.Replayed);
            var after = await GetBalancesAsync();
            Assert.Equal(before[AccountA] + request.Amount, after[AccountA]);
            Assert.Equal(before[AccountB], after[AccountB]);
            await AssertTransactionAndAuditAsync(key, "CASH_DEPOSIT");
        }
        finally { await CleanupTransactionsAsync(key, before); }
    }

    [Fact]
    public void BankAccount_RowVersion_IsConfiguredForOptimisticConcurrency()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SUBankDbContext>();
        Assert.True(db.Model.FindEntityType(typeof(BankAccount))!.FindProperty(nameof(BankAccount.RowVersion))!.IsConcurrencyToken);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(string userName)
    {
        var client = factory.CreateClient();
        var session = await LoginAsync(client, userName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return client;
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string userName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, "Demo@12345"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static Task<HttpResponseMessage> PostWithIdempotencyAsync<T>(HttpClient client, string uri, string key, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostCookieProtectedAsync(HttpClient client, string uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Add("X-SUBank-CSRF", "1");
        return client.SendAsync(request);
    }

    private async Task<Dictionary<string, decimal>> GetBalancesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SUBankDbContext>();
        return await db.BankAccounts.AsNoTracking().Where(x => x.AccountNumber == AccountA || x.AccountNumber == AccountB)
            .ToDictionaryAsync(x => x.AccountNumber, x => x.Balance);
    }

    private async Task<bool> TransactionExistsAsync(string key)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SUBankDbContext>().FinancialTransactions.AnyAsync(x => x.IdempotencyKey == key);
    }

    private async Task<int> CountTransactionsAsync(string prefix)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SUBankDbContext>().FinancialTransactions.CountAsync(x => x.IdempotencyKey.StartsWith(prefix));
    }

    private async Task AssertTransactionAndAuditAsync(string key, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SUBankDbContext>();
        var transaction = await db.FinancialTransactions.AsNoTracking().SingleAsync(x => x.IdempotencyKey == key);
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(x => x.EntityId == transaction.ReferenceNo && x.Action == action));
    }

    private async Task CleanupTransactionsAsync(string key, IReadOnlyDictionary<string, decimal> balances, bool startsWith = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SUBankDbContext>();
        var transactions = await db.FinancialTransactions
            .Where(x => startsWith ? x.IdempotencyKey.StartsWith(key) : x.IdempotencyKey == key).ToListAsync();
        var references = transactions.Select(x => x.ReferenceNo).ToArray();
        if (references.Length > 0)
        {
            await db.AuditLogs.Where(x => x.EntityId != null && references.Contains(x.EntityId)).ExecuteDeleteAsync();
            db.FinancialTransactions.RemoveRange(transactions);
            await db.SaveChangesAsync();
        }
        foreach (var pair in balances)
            await db.BankAccounts.Where(x => x.AccountNumber == pair.Key)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Balance, pair.Value));
    }

    private async Task EnsureUnlockedAsync(string userName)
    {
        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByNameAsync(userName);
        if (user is null) return;
        await manager.SetLockoutEndDateAsync(user, null);
        await manager.ResetAccessFailedCountAsync(user);
        user.LockedAtUtc = null;
        await manager.UpdateAsync(user);
    }

    private static void AssertBalancesEqual(IReadOnlyDictionary<string, decimal> expected, IReadOnlyDictionary<string, decimal> actual)
    {
        Assert.Equal(expected[AccountA], actual[AccountA]);
        Assert.Equal(expected[AccountB], actual[AccountB]);
    }
}
