using SUBank.Domain.Enums;

namespace SUBank.Domain.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public required string Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public AuditResult Result { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
