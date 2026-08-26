using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SUBank.Application.Abstractions;

namespace SUBank.IntegrationTests;

public sealed class SUBankWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Server=(localdb)\\MSSQLLocalDB;Database=SUBankV2_Integration;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IActiveSessionStore>();
            services.AddSingleton<IActiveSessionStore, TestActiveSessionStore>();
        });
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

    public Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        if (sessions.TryGetValue(userId, out var active) && active == sessionId)
            sessions.TryRemove(new KeyValuePair<string, string>(userId, sessionId));
        return Task.CompletedTask;
    }
}
