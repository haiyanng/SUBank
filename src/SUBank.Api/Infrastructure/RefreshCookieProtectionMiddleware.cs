namespace SUBank.Api.Infrastructure;

public sealed class RefreshCookieProtectionMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string CsrfHeader = "X-SUBank-CSRF";

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            (context.Request.Path.Equals("/api/auth/refresh") || context.Request.Path.Equals("/api/auth/logout")))
        {
            if (context.Request.Headers[CsrfHeader] != "1" || !HasAllowedOrigin(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    status = StatusCodes.Status403Forbidden,
                    title = "Yêu cầu bảo vệ cookie không hợp lệ."
                });
                return;
            }
        }
        await next(context);
    }

    private bool HasAllowedOrigin(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Origin", out var origin)) return true;
        var actual = origin.ToString().TrimEnd('/');
        var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
        var developmentClient = configuration["Cors:ClientOrigin"]?.TrimEnd('/');
        return string.Equals(actual, sameOrigin, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actual, developmentClient, StringComparison.OrdinalIgnoreCase);
    }
}
