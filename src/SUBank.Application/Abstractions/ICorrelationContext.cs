namespace SUBank.Application.Abstractions;

public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
