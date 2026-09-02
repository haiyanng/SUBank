using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Staff;
using SUBank.Contracts.Realtime;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;
using SUBank.Infrastructure.Profiles;

namespace SUBank.Infrastructure.Banking;

public sealed class StaffService(
    SUBankDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions,
    IActiveSessionStore activeSessionStore,
    IRealtimeNotifier realtimeNotifier,
    ILogger<StaffService> logger) : IStaffService
{
    public async Task<CashDepositResponse> CashDepositAsync(string tellerUserId, string idempotencyKey, CashDepositRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await CashDepositCoreAsync(tellerUserId, idempotencyKey, request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is BusinessRuleException or NotFoundException or AuthenticationException or ConflictException)
        {
            await TryAuditFailureAsync(tellerUserId);
            throw;
        }
    }

    private async Task<CashDepositResponse> CashDepositCoreAsync(
        string tellerUserId,
        string idempotencyKey,
        CashDepositRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) throw new BusinessRuleException("Dữ liệu nộp tiền không hợp lệ.");

        var teller = await userManager.FindByIdAsync(tellerUserId)
            ?? throw new AuthenticationException("Người dùng không tồn tại.");
        if (!teller.IsActive || teller.IsAdminSuspended || await userManager.IsLockedOutAsync(teller))
            throw new AuthenticationException("Tài khoản không thể thực hiện giao dịch.");

        BankingRules.ValidateIdempotencyKey(idempotencyKey);
        BankingRules.ValidateAccountNumber(request.DestinationAccountNumber, "Tài khoản nhận");
        BankingRules.ValidateAmount(request.Amount);
        var description = BankingRules.NormalizeDescription(request.Description);
        var hash = BankingService.Hash(FormattableString.Invariant(
            $"{request.DestinationAccountNumber}|{request.Amount:0.00}|{description}"));
        var replay = await IdempotencyReplay.FindMatchingAsync(
            dbContext, tellerUserId, idempotencyKey, hash, TransactionType.CashDeposit, cancellationToken);
        if (replay is not null)
        {
            return ToCashDepositResponse(replay, replayed: true);
        }

        BankAccount account;
        FinancialTransaction item;
        DateTimeOffset now;
        DbUpdateException? persistenceFailure = null;

        await using (var sqlTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            account = await dbContext.BankAccounts.Include(x => x.CustomerProfile)
                .SingleOrDefaultAsync(x => x.AccountNumber == request.DestinationAccountNumber, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản nhận.");
            if (account.Status != AccountStatus.Active) throw new BusinessRuleException("Tài khoản nhận không hoạt động.");
            BankingRules.ValidateCreditedBalance(account.Balance, request.Amount);
            account.Balance += request.Amount;
            now = DateTimeOffset.UtcNow;
            item = new FinancialTransaction
            {
                ReferenceNo = BankingService.NewReference("DEP"),
                DestinationAccountId = account.Id,
                CreatedByUserId = tellerUserId,
                Type = TransactionType.CashDeposit,
                Amount = request.Amount,
                Description = description,
                IdempotencyKey = idempotencyKey,
                RequestHash = hash,
                CreatedAtUtc = now
            };
            dbContext.FinancialTransactions.Add(item);
            dbContext.AuditLogs.Add(new AuditLog { UserId = tellerUserId, Action = "CASH_DEPOSIT", EntityType = "FinancialTransaction", EntityId = item.ReferenceNo, Result = AuditResult.Success, CreatedAtUtc = now });
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await sqlTransaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
            {
                persistenceFailure = exception;
                await sqlTransaction.RollbackAsync(CancellationToken.None);
            }
        }

        if (persistenceFailure is not null)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentReplay = await IdempotencyReplay.FindMatchingAsync(
                dbContext, tellerUserId, idempotencyKey, hash, TransactionType.CashDeposit, cancellationToken);
            if (concurrentReplay is not null)
            {
                return ToCashDepositResponse(concurrentReplay, replayed: true);
            }

            if (persistenceFailure is DbUpdateConcurrencyException)
                throw new ConflictException("Số dư vừa thay đổi. Vui lòng kiểm tra và thử lại.");
            if (IdempotencyReplay.IsUniqueConstraintViolation(persistenceFailure))
                throw new ConflictException("Yêu cầu bị trùng hoặc dữ liệu vừa thay đổi.");

            throw new DependencyUnavailableException("Dịch vụ dữ liệu tạm thời không khả dụng.", persistenceFailure);
        }

        await realtimeNotifier.BalanceChangedAsync(account.CustomerProfile.UserId, account.AccountNumber, CancellationToken.None);
        await realtimeNotifier.TransactionReceivedAsync(
            account.CustomerProfile.UserId, item.ReferenceNo, account.AccountNumber, CancellationToken.None);
        return new CashDepositResponse(item.ReferenceNo, item.Amount, account.AccountNumber, now, false);
    }

    private async Task TryAuditFailureAsync(string tellerUserId)
    {
        try
        {
            dbContext.ChangeTracker.Clear();
            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = tellerUserId,
                Action = "CASH_DEPOSIT_FAILED",
                Result = AuditResult.Failure,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể ghi audit cho nghiệp vụ nộp tiền thất bại");
        }
    }

    public async Task<IReadOnlyList<CustomerManagementSummary>> GetCustomersAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = CustomerAdministrationRules.NormalizeSearch(search);
        var now = DateTimeOffset.UtcNow;
        var lockoutThreshold = identityOptions.Value.Lockout.MaxFailedAccessAttempts;
        var query =
            from profile in dbContext.CustomerProfiles.AsNoTracking()
            join user in userManager.Users.AsNoTracking() on profile.UserId equals user.Id
            join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.NormalizedName == "CUSTOMER" && user.IsActive
            select new
            {
                user.UserName,
                profile.FullName,
                profile.Phone,
                user.LockoutEnabled,
                user.LockoutEnd,
                user.AccessFailedCount,
                user.LockedAtUtc,
                user.IsAdminSuspended,
                user.AdminSuspendedAtUtc,
                user.AdminSuspensionReason
            };

        if (normalizedSearch is not null)
            query = query.Where(x =>
                x.FullName.Contains(normalizedSearch) || x.Phone.Contains(normalizedSearch));

        var customers = await query
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Phone)
            .Take(200)
            .ToListAsync(cancellationToken);

        return customers.Select(customer =>
        {
            var isIdentityLocked = customer.LockoutEnabled &&
                customer.LockoutEnd is { } lockoutEnd && lockoutEnd > now;
            var failedAttempts = isIdentityLocked && customer.AccessFailedCount == 0
                ? lockoutThreshold
                : customer.AccessFailedCount;
            return new CustomerManagementSummary(
                customer.UserName ?? string.Empty,
                customer.FullName,
                customer.Phone,
                isIdentityLocked,
                failedAttempts,
                isIdentityLocked ? customer.LockedAtUtc : null,
                isIdentityLocked ? customer.LockoutEnd : null,
                customer.IsAdminSuspended,
                customer.AdminSuspendedAtUtc,
                customer.AdminSuspensionReason);
        }).ToList();
    }

    public async Task<CustomerManagementDetail> GetCustomerAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        var (user, profile) = await FindCustomerAsync(userName, cancellationToken);
        var isIdentityLocked = await userManager.IsLockedOutAsync(user);
        var failedAttempts = isIdentityLocked && user.AccessFailedCount == 0
            ? identityOptions.Value.Lockout.MaxFailedAccessAttempts
            : user.AccessFailedCount;
        string? suspendedByUserName = null;
        if (!string.IsNullOrWhiteSpace(user.AdminSuspendedByUserId))
        {
            suspendedByUserName = await dbContext.Users.AsNoTracking()
                .Where(x => x.Id == user.AdminSuspendedByUserId)
                .Select(x => x.UserName)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new CustomerManagementDetail(
            user.UserName ?? string.Empty,
            profile.FullName,
            profile.DateOfBirth,
            PersonalDataMasking.MaskIdentityCardNumber(profile.IdentityCardNumber),
            profile.Phone,
            profile.Email,
            profile.PermanentAddress,
            profile.TemporaryAddress,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc,
            isIdentityLocked,
            failedAttempts,
            isIdentityLocked ? user.LockedAtUtc : null,
            isIdentityLocked ? user.LockoutEnd : null,
            user.IsAdminSuspended,
            user.AdminSuspendedAtUtc,
            user.AdminSuspensionReason,
            suspendedByUserName);
    }

    public async Task<IReadOnlyList<LockedUserSummary>> GetLockedUsersAsync(CancellationToken cancellationToken) =>
        (await GetCustomersAsync(null, cancellationToken))
            .Where(x => x.IsIdentityLocked)
            .Select(x => new LockedUserSummary(x.UserName, x.FailedAttempts, x.IdentityLockedAtUtc))
            .ToList();

    public async Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .Select(x => new AuditLogSummary(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId,
                x.Result.ToString(), x.CreatedAtUtc, x.CorrelationId, x.Details))
            .ToListAsync(cancellationToken);

    public async Task SuspendCustomerAsync(
        string adminUserId,
        string userName,
        SuspendCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) throw new BusinessRuleException("Dữ liệu khóa tài khoản không hợp lệ.");
        var reason = CustomerAdministrationRules.NormalizeSuspensionReason(request.Reason);
        var (user, _) = await FindCustomerAsync(userName, cancellationToken);
        if (user.IsAdminSuspended)
            throw new ConflictException("Khách hàng đã bị khóa bởi quản trị viên.");

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sessionIds = await dbContext.UserSessions.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .Select(x => x.SessionId)
            .ToListAsync(cancellationToken);

        user.IsAdminSuspended = true;
        user.AdminSuspendedAtUtc = now;
        user.AdminSuspensionReason = reason;
        user.AdminSuspendedByUserId = adminUserId;

        await dbContext.UserSessions
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.RevokedAtUtc, now)
                .SetProperty(x => x.RevocationReason, "ADMIN_SUSPENSION"), cancellationToken);
        await dbContext.RefreshTokens
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        EnsureIdentityUpdateSucceeded(await userManager.UpdateAsync(user));

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "CUSTOMER_SUSPENDED_BY_ADMIN",
            EntityType = "ApplicationUser",
            EntityId = user.Id,
            Result = AuditResult.Success,
            Details = $"Lý do: {reason}",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await RevokeDistributedSessionsBestEffortAsync(user.Id, sessionIds);
    }

    public async Task ResumeCustomerAsync(
        string adminUserId,
        string userName,
        CancellationToken cancellationToken)
    {
        var (user, _) = await FindCustomerAsync(userName, cancellationToken);
        if (!user.IsAdminSuspended)
            throw new ConflictException("Khách hàng không bị khóa bởi quản trị viên.");

        var now = DateTimeOffset.UtcNow;
        var previousReason = user.AdminSuspensionReason;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        user.IsAdminSuspended = false;
        user.AdminSuspendedAtUtc = null;
        user.AdminSuspensionReason = null;
        user.AdminSuspendedByUserId = null;
        EnsureIdentityUpdateSucceeded(await userManager.UpdateAsync(user));
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "CUSTOMER_SUSPENSION_LIFTED_BY_ADMIN",
            EntityType = "ApplicationUser",
            EntityId = user.Id,
            Result = AuditResult.Success,
            Details = string.IsNullOrWhiteSpace(previousReason) ? null : $"Lý do khóa trước đó: {previousReason}",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ClearCustomerIdentityLockoutAsync(
        string adminUserId,
        string userName,
        CancellationToken cancellationToken)
    {
        var (user, _) = await FindCustomerAsync(userName, cancellationToken);
        if (!await userManager.IsLockedOutAsync(user))
            throw new ConflictException("Khách hàng không bị tạm khóa do đăng nhập sai.");

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        EnsureIdentityUpdateSucceeded(await userManager.SetLockoutEndDateAsync(user, null));
        EnsureIdentityUpdateSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        user.LockedAtUtc = null;
        EnsureIdentityUpdateSucceeded(await userManager.UpdateAsync(user));
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "IDENTITY_LOCKOUT_CLEARED_BY_ADMIN",
            EntityType = "ApplicationUser",
            EntityId = user.Id,
            Result = AuditResult.Success,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<(ApplicationUser User, CustomerProfile Profile)> FindCustomerAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > AuthenticationRules.MaximumLoginNameLength)
            throw new NotFoundException("Không tìm thấy khách hàng.");

        var user = await userManager.FindByNameAsync(userName.Trim());
        if (user is null || !user.IsActive || !await userManager.IsInRoleAsync(user, "Customer"))
            throw new NotFoundException("Không tìm thấy khách hàng.");

        var profile = await dbContext.CustomerProfiles
            .SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        return profile is null
            ? throw new NotFoundException("Không tìm thấy khách hàng.")
            : (user, profile);
    }

    private async Task RevokeDistributedSessionsBestEffortAsync(
        string userId,
        IReadOnlyCollection<string> persistedSessionIds)
    {
        var sessionIds = persistedSessionIds.ToHashSet(StringComparer.Ordinal);
        string? activeSessionId = null;
        try
        {
            activeSessionId = await activeSessionStore.GetActiveSessionIdAsync(userId, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(activeSessionId))
            {
                sessionIds.Add(activeSessionId);
                await activeSessionStore.RevokeAsync(userId, activeSessionId, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không thể thu hồi Redis session sau khi Admin khóa Customer {UserId}", userId);
        }

        foreach (var sessionId in sessionIds)
        {
            try
            {
                await realtimeNotifier.ForceLogoutAsync(
                    sessionId, ForceLogoutReasons.AdminSuspension, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Không thể gửi ForceLogout sau khi Admin khóa Customer {UserId}, session {SessionId}",
                    userId,
                    sessionId);
            }
        }
    }

    private static CashDepositResponse ToCashDepositResponse(FinancialTransaction transaction, bool replayed) =>
        new(
            transaction.ReferenceNo,
            transaction.Amount,
            transaction.DestinationAccount.AccountNumber,
            transaction.CreatedAtUtc,
            replayed);

    private static void EnsureIdentityUpdateSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new DependencyUnavailableException("Không thể cập nhật trạng thái tài khoản.");
    }
}
