using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SUBank.Api.Realtime;
using SUBank.Application.Abstractions;

namespace SUBank.IntegrationTests;

public sealed class SUBankWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\MSSQLLocalDB;Database=SUBankV2_Integration;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False");
        builder.UseSetting("DatabaseInitialization:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("DatabaseInitialization:SeedDemoData", "true");
        builder.UseSetting("DatabaseInitialization:AllowedSeedDataSource", "(localdb)\\MSSQLLocalDB");
        builder.UseSetting("DatabaseInitialization:AllowedSeedDatabase", "SUBankV2_Integration");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IActiveSessionStore>();
            services.AddSingleton<IActiveSessionStore, TestActiveSessionStore>();
            services.RemoveAll<IRealtimeNotifier>();
            services.AddSingleton<TestRealtimeNotifier>();
            services.AddSingleton<IRealtimeNotifier>(provider => provider.GetRequiredService<TestRealtimeNotifier>());
        });
    }
}

public sealed class TestRealtimeNotifier(IServiceScopeFactory scopeFactory) : IRealtimeNotifier
{
    public ConcurrentQueue<string> ForcedSessions { get; } = new();
    public ConcurrentQueue<(string UserId, string AccountNumber)> BalanceChanges { get; } = new();
    public ConcurrentQueue<(string UserId, string ReferenceNo, string AccountNumber)> Transactions { get; } = new();

    public async Task ForceLogoutAsync(string sessionId, string reason, CancellationToken cancellationToken)
    {
        ForcedSessions.Enqueue(sessionId);
        using var scope = scopeFactory.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<SignalRRealtimeNotifier>();
        await notifier.ForceLogoutAsync(sessionId, reason, cancellationToken);
    }

    public async Task BalanceChangedAsync(string userId, string accountNumber, CancellationToken cancellationToken)
    {
        BalanceChanges.Enqueue((userId, accountNumber));
        using var scope = scopeFactory.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<SignalRRealtimeNotifier>();
        await notifier.BalanceChangedAsync(userId, accountNumber, cancellationToken);
    }

    public async Task TransactionReceivedAsync(string userId, string referenceNo, string accountNumber, CancellationToken cancellationToken)
    {
        Transactions.Enqueue((userId, referenceNo, accountNumber));
        using var scope = scopeFactory.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<SignalRRealtimeNotifier>();
        await notifier.TransactionReceivedAsync(userId, referenceNo, accountNumber, cancellationToken);
    }

    public void Clear()
    {
        ForcedSessions.Clear();
        BalanceChanges.Clear();
        Transactions.Clear();
    }
}

internal sealed class TestActiveSessionStore : IActiveSessionStore
{
    private readonly ConcurrentDictionary<string, string> sessions = new();

    public Task<string?> ReplaceAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        string? previous = null;
        sessions.AddOrUpdate(userId, sessionId, (_, current) =>
        {
            previous = current;
            return sessionId;
        });
        return Task.FromResult(previous);
    }

    public Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(sessions.TryGetValue(userId, out var active) && active == sessionId);

    public Task<string?> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken) =>
        Task.FromResult(sessions.TryGetValue(userId, out var active) ? active : null);

    public Task<bool> RenewAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken) =>
        IsActiveAsync(userId, sessionId, cancellationToken);

    public Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        if (sessions.TryGetValue(userId, out var active) && active == sessionId)
            sessions.TryRemove(new KeyValuePair<string, string>(userId, sessionId));
        return Task.CompletedTask;
    }
}
