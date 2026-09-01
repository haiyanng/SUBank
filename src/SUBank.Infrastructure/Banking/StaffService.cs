using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Staff;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Banking;

public sealed class StaffService(
    SUBankDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<IdentityOptions> identityOptions,
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
        if (!teller.IsActive || await userManager.IsLockedOutAsync(teller))
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

    public async Task<IReadOnlyList<UserManagementSummary>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lockoutThreshold = identityOptions.Value.Lockout.MaxFailedAccessAttempts;
        var users = await userManager.Users.AsNoTracking()
            .OrderBy(x => x.UserName)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.IsActive,
                x.LockoutEnabled,
                x.LockoutEnd,
                x.AccessFailedCount,
                x.LockedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (users.Count == 0) return [];

        var userIds = users.Select(x => x.Id).ToList();
        var roleAssignments = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name })
            .ToListAsync(cancellationToken);
        var rolesByUser = roleAssignments
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => x.Name!).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToArray(),
                StringComparer.Ordinal);

        return users.Select(user =>
        {
            var isLocked = user.LockoutEnabled && user.LockoutEnd is { } lockoutEnd && lockoutEnd > now;
            var failedAttempts = isLocked && user.AccessFailedCount == 0
                ? lockoutThreshold
                : user.AccessFailedCount;
            var roles = rolesByUser.TryGetValue(user.Id, out var assignedRoles)
                ? assignedRoles
                : [];
            return new UserManagementSummary(
                user.UserName ?? string.Empty,
                roles,
                user.IsActive,
                isLocked,
                failedAttempts,
                user.LockedAtUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<LockedUserSummary>> GetLockedUsersAsync(CancellationToken cancellationToken) =>
        (await GetUsersAsync(cancellationToken))
            .Where(x => x.IsLocked)
            .Select(x => new LockedUserSummary(x.UserName, x.FailedAttempts, x.LockedAtUtc))
            .ToList();

    public async Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .Select(x => new AuditLogSummary(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId, x.Result.ToString(), x.CreatedAtUtc, x.CorrelationId))
            .ToListAsync(cancellationToken);

    public async Task UnlockUserAsync(string adminUserId, string userName, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(userName) ?? throw new NotFoundException("Không tìm thấy người dùng.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        EnsureIdentityUpdateSucceeded(await userManager.SetLockoutEndDateAsync(user, null));
        EnsureIdentityUpdateSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        user.LockedAtUtc = null;
        EnsureIdentityUpdateSucceeded(await userManager.UpdateAsync(user));
        dbContext.AuditLogs.Add(new AuditLog { UserId = adminUserId, Action = "UNLOCK_USER", EntityType = "ApplicationUser", EntityId = user.Id, Result = AuditResult.Success, CreatedAtUtc = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
