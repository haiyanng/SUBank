using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Exceptions;

namespace SUBank.Api.Infrastructure;

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        var status = exception switch
        {
            AuthenticationException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            BusinessRuleException => StatusCodes.Status422UnprocessableEntity,
            DependencyUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status == StatusCodes.Status503ServiceUnavailable)
            logger.LogWarning(exception, "API dependency is temporarily unavailable");
        else if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled API exception");

        context.Response.StatusCode = status;
        var details = new ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status500InternalServerError
                ? "Đã xảy ra lỗi hệ thống."
                : exception.Message,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
        };
        details.Extensions["correlationId"] = context.TraceIdentifier;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details
        });
    }
}
