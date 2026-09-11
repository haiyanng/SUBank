using SUBank.Application.Abstractions;

namespace SUBank.Api.Infrastructure;

public sealed class HttpCorrelationContext(IHttpContextAccessor httpContextAccessor) : ICorrelationContext
{
    public string? CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier;
}
