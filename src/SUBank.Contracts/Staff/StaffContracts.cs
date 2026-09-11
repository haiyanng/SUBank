namespace SUBank.Contracts.Staff;

public sealed record CashDepositRequest(string DestinationAccountNumber, decimal Amount, string? Description);
public sealed record CashDepositResponse(string ReferenceNo, decimal Amount, string DestinationAccountNumber, DateTimeOffset CreatedAtUtc, bool Replayed);
public sealed record CustomerManagementSummary(
    string UserName,
    string FullName,
    string Phone,
    bool IsIdentityLocked,
    int FailedAttempts,
    DateTimeOffset? IdentityLockedAtUtc,
    DateTimeOffset? IdentityLockoutEndUtc,
    bool IsAdminSuspended,
    DateTimeOffset? AdminSuspendedAtUtc,
    string? AdminSuspensionReason);
public sealed record CustomerManagementDetail(
    string UserName,
    string FullName,
    DateOnly DateOfBirth,
    string MaskedIdentityCardNumber,
    string Phone,
    string Email,
    string PermanentAddress,
    string? TemporaryAddress,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    bool IsIdentityLocked,
    int FailedAttempts,
    DateTimeOffset? IdentityLockedAtUtc,
    DateTimeOffset? IdentityLockoutEndUtc,
    bool IsAdminSuspended,
    DateTimeOffset? AdminSuspendedAtUtc,
    string? AdminSuspensionReason,
    string? AdminSuspendedByUserName);
public sealed record SuspendCustomerRequest(string Reason);
public sealed record LockedUserSummary(string UserName, int FailedAttempts, DateTimeOffset? LockedAtUtc);
public sealed record AuditLogSummary(long Id, string? UserId, string Action, string? EntityType, string? EntityId, string Result, DateTimeOffset CreatedAtUtc, string? CorrelationId, string? Details);
