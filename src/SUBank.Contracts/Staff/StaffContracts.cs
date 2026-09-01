namespace SUBank.Contracts.Staff;

public sealed record CashDepositRequest(string DestinationAccountNumber, decimal Amount, string? Description);
public sealed record CashDepositResponse(string ReferenceNo, decimal Amount, string DestinationAccountNumber, DateTimeOffset CreatedAtUtc, bool Replayed);
public sealed record UserManagementSummary(
    string UserName,
    string[] Roles,
    bool IsActive,
    bool IsLocked,
    int FailedAttempts,
    DateTimeOffset? LockedAtUtc);
public sealed record LockedUserSummary(string UserName, int FailedAttempts, DateTimeOffset? LockedAtUtc);
public sealed record AuditLogSummary(long Id, string? UserId, string Action, string? EntityType, string? EntityId, string Result, DateTimeOffset CreatedAtUtc, string? CorrelationId);
