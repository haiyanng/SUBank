namespace SUBank.Domain.Entities;

public sealed class UserSession
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string SessionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevocationReason { get; set; }
}
