using SUBank.Contracts.Staff;

namespace SUBank.Application.Abstractions;

public interface IStaffService
{
    Task<CashDepositResponse> CashDepositAsync(string tellerUserId, string idempotencyKey, CashDepositRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerManagementSummary>> GetCustomersAsync(string? search, CancellationToken cancellationToken);
    Task<CustomerManagementDetail> GetCustomerAsync(string userName, CancellationToken cancellationToken);
    Task<IReadOnlyList<LockedUserSummary>> GetLockedUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(CancellationToken cancellationToken);
    Task SuspendCustomerAsync(string adminUserId, string userName, SuspendCustomerRequest request, CancellationToken cancellationToken);
    Task ResumeCustomerAsync(string adminUserId, string userName, CancellationToken cancellationToken);
    Task ClearCustomerIdentityLockoutAsync(string adminUserId, string userName, CancellationToken cancellationToken);
}
