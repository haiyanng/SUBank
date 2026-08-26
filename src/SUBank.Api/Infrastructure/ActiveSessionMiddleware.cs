using System.Security.Claims;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;

namespace SUBank.Api.Infrastructure;

public sealed class ActiveSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IActiveSessionStore sessions)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessionId = context.User.FindFirstValue("sid");
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId) ||
                !await sessions.IsActiveAsync(userId, sessionId, context.RequestAborted))
                throw new AuthenticationException("Phiên đăng nhập không còn hiệu lực.");
        }

        await next(context);
    }
}
