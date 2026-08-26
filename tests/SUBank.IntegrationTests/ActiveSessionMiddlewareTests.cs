using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SUBank.Api.Infrastructure;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;

namespace SUBank.IntegrationTests;

public sealed class ActiveSessionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowsTheActiveSession()
    {
        var nextCalled = false;
        var middleware = new ActiveSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = AuthenticatedContext("user-1", "session-1");

        await middleware.InvokeAsync(context, new FixedSessionStore(true));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RejectsAReplacedSession()
    {
        var middleware = new ActiveSessionMiddleware(_ => Task.CompletedTask);
        var context = AuthenticatedContext("user-1", "old-session");

        await Assert.ThrowsAsync<AuthenticationException>(
            () => middleware.InvokeAsync(context, new FixedSessionStore(false)));
    }

    [Fact]
    public async Task InvokeAsync_FailsClosedWhenSessionStoreIsUnavailable()
    {
        var middleware = new ActiveSessionMiddleware(_ => Task.CompletedTask);
        var context = AuthenticatedContext("user-1", "session-1");

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => middleware.InvokeAsync(context, new UnavailableSessionStore()));
    }

    private static DefaultHttpContext AuthenticatedContext(string userId, string sessionId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim("sid", sessionId)], "Test"));
        return context;
    }

    private sealed class FixedSessionStore(bool active) : IActiveSessionStore
    {
        public Task<string?> ReplaceAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
        public Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(active);
        public Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnavailableSessionStore : IActiveSessionStore
    {
        public Task<string?> ReplaceAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken) =>
            throw new DependencyUnavailableException("Unavailable");
        public Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
            throw new DependencyUnavailableException("Unavailable");
        public Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
            throw new DependencyUnavailableException("Unavailable");
    }
}
