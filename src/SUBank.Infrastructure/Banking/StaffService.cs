using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
    IRealtimeNotifier realtimeNotifier) : IStaffService
{
    public async Task<CashDepositResponse> CashDepositAsync(string tellerUserId, string idempotencyKey, CashDepositRequest request, CancellationToken cancellationToken)
    {
        BankingRules.ValidateIdempotencyKey(idempotencyKey);
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
                ReferenceNo = BankingService.NewReference("DEP"), DestinationAccountId = account.Id,
                CreatedByUserId = tellerUserId, Type = TransactionType.CashDeposit, Amount = request.Amount,
                Description = description, IdempotencyKey = idempotencyKey, RequestHash = hash, CreatedAtUtc = now
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
            throw new ConflictException("Yêu cầu bị trùng hoặc dữ liệu vừa thay đổi.");
        }

        await realtimeNotifier.BalanceChangedAsync(account.CustomerProfile.UserId, account.AccountNumber, CancellationToken.None);
        await realtimeNotifier.TransactionReceivedAsync(
            account.CustomerProfile.UserId, item.ReferenceNo, account.AccountNumber, CancellationToken.None);
        return new CashDepositResponse(item.ReferenceNo, item.Amount, account.AccountNumber, now, false);
    }

    public async Task<IReadOnlyList<LockedUserSummary>> GetLockedUsersAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var lockoutThreshold = identityOptions.Value.Lockout.MaxFailedAccessAttempts;
        return await userManager.Users.AsNoTracking()
            .Where(x => x.LockoutEnd != null && x.LockoutEnd > now)
            .OrderBy(x => x.UserName)
            .Select(x => new LockedUserSummary(x.UserName!,
                x.AccessFailedCount == 0 ? lockoutThreshold : x.AccessFailedCount,
                x.LockedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogSummary>> GetAuditLogsAsync(CancellationToken cancellationToken) =>
        await dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .Select(x => new AuditLogSummary(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId, x.Result.ToString(), x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task UnlockUserAsync(string adminUserId, string userName, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(userName) ?? throw new NotFoundException("Không tìm thấy người dùng.");
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        user.LockedAtUtc = null;
        await userManager.UpdateAsync(user);
        dbContext.AuditLogs.Add(new AuditLog { UserId = adminUserId, Action = "UNLOCK_USER", EntityType = "ApplicationUser", EntityId = user.Id, Result = AuditResult.Success, CreatedAtUtc = DateTimeOffset.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CashDepositResponse ToCashDepositResponse(FinancialTransaction transaction, bool replayed) =>
        new(
            transaction.ReferenceNo,
            transaction.Amount,
            transaction.DestinationAccount.AccountNumber,
            transaction.CreatedAtUtc,
            replayed);
}
