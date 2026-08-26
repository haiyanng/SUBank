using SUBank.Domain.Enums;

namespace SUBank.Domain.Entities;

public sealed class AddressChangeRequest
{
    public long Id { get; set; }
    public required string RequestNo { get; set; }
    public long CustomerProfileId { get; set; }
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public required string PermanentAddress { get; set; }
    public string? TemporaryAddress { get; set; }
    public AddressChangeRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? DecidedAtUtc { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? RejectionReason { get; set; }
}
