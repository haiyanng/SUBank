using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace SUBank.Api.Infrastructure;

public sealed class ApplicationRequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<ApplicationRequestLoggingMiddleware> logger)
{
    private static readonly EventId RequestCompleted = new(1001, nameof(RequestCompleted));

    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var routeTemplate = context.GetEndpoint() is RouteEndpoint routeEndpoint
            ? routeEndpoint.RoutePattern.RawText ?? "<route>"
            : "<unmapped>";
        Exception? unhandledException = null;

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            unhandledException = exception;
            throw;
        }
        finally
        {
            var elapsedMilliseconds = Math.Round(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                2,
                MidpointRounding.AwayFromZero);
            var statusCode = unhandledException is null
                ? context.Response.StatusCode
                : StatusCodes.Status500InternalServerError;

            LogCompletion(context.Request.Method, routeTemplate, statusCode, elapsedMilliseconds);
        }
    }

    private void LogCompletion(
        string method,
        string routeTemplate,
        int statusCode,
        double elapsedMilliseconds)
    {
        if (IsSuccessfulHealthCheck(routeTemplate, statusCode))
        {
            logger.LogDebug(
                RequestCompleted,
                "HTTP {Method} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                routeTemplate,
                statusCode,
                elapsedMilliseconds);
        }
        else if (statusCode == 499)
        {
            logger.LogInformation(
                RequestCompleted,
                "HTTP {Method} {RouteTemplate} was canceled by the client in {ElapsedMilliseconds} ms",
                method,
                routeTemplate,
                elapsedMilliseconds);
        }
        else if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                RequestCompleted,
                "HTTP {Method} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                routeTemplate,
                statusCode,
                elapsedMilliseconds);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest)
        {
            logger.LogWarning(
                RequestCompleted,
                "HTTP {Method} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                routeTemplate,
                statusCode,
                elapsedMilliseconds);
        }
        else
        {
            logger.LogInformation(
                RequestCompleted,
                "HTTP {Method} {RouteTemplate} responded {StatusCode} in {ElapsedMilliseconds} ms",
                method,
                routeTemplate,
                statusCode,
                elapsedMilliseconds);
        }
    }

    private static bool IsSuccessfulHealthCheck(string routeTemplate, int statusCode) =>
        statusCode < StatusCodes.Status400BadRequest &&
        routeTemplate is "/health" or "/health/live" or "/health/ready";
}
