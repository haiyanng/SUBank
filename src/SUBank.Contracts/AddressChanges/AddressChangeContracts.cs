namespace SUBank.Contracts.AddressChanges;

public sealed record CreateAddressChangeRequest(string PermanentAddress, string? TemporaryAddress);
public sealed record RejectAddressChangeRequest(string Reason);
public sealed record AddressChangeRequestSummary(
    string RequestNo,
    string CustomerName,
    string PermanentAddress,
    string? TemporaryAddress,
    string Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string? RejectionReason);
