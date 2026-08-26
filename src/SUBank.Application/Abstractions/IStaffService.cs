using SUBank.Contracts.Staff;

namespace SUBank.Application.Abstractions;

public interface IStaffService
{
    Task<CashDepositResponse> CashDepositAsync(string tellerUserId, string idempotencyKey, CashDepositRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LockedUserSummary>> GetLockedUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(CancellationToken cancellationToken);
    Task UnlockUserAsync(string adminUserId, string userName, CancellationToken cancellationToken);
}
