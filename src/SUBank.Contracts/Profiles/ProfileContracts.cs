namespace SUBank.Contracts.Profiles;

public sealed record CustomerProfileDetail(
    string FullName,
    DateOnly DateOfBirth,
    string MaskedIdentityCardNumber,
    string Phone,
    string Email,
    string PermanentAddress,
    string? TemporaryAddress,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
