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

        await middleware.InvokeAsync(context, new FixedSessionValidator(true));

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_RejectsAReplacedSession()
    {
        var middleware = new ActiveSessionMiddleware(_ => Task.CompletedTask);
        var context = AuthenticatedContext("user-1", "old-session");

        await Assert.ThrowsAsync<AuthenticationException>(
            () => middleware.InvokeAsync(context, new FixedSessionValidator(false)));
    }

    [Fact]
    public async Task InvokeAsync_FailsClosedWhenSessionStoreIsUnavailable()
    {
        var middleware = new ActiveSessionMiddleware(_ => Task.CompletedTask);
        var context = AuthenticatedContext("user-1", "session-1");

        await Assert.ThrowsAsync<DependencyUnavailableException>(
            () => middleware.InvokeAsync(context, new UnavailableSessionValidator()));
    }

    private static DefaultHttpContext AuthenticatedContext(string userId, string sessionId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim("sid", sessionId)], "Test"));
        return context;
    }

    private sealed class FixedSessionValidator(bool active) : IActiveSessionValidator
    {
        public Task<bool> IsValidAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(active);
    }

    private sealed class UnavailableSessionValidator : IActiveSessionValidator
    {
        public Task<bool> IsValidAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
            throw new DependencyUnavailableException("Unavailable");
    }
}
