namespace SUBank.Domain.Entities;

public sealed class RefreshToken
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string SessionId { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long? ReplacedByTokenId { get; set; }
    public RefreshToken? ReplacedByToken { get; set; }
}
