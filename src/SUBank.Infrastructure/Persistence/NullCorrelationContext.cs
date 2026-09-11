using SUBank.Application.Abstractions;

namespace SUBank.Infrastructure.Persistence;

internal sealed class NullCorrelationContext : ICorrelationContext
{
    public string? CorrelationId => null;
}
