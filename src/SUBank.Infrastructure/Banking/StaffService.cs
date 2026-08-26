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
    IOptions<IdentityOptions> identityOptions) : IStaffService
{
    public async Task<CashDepositResponse> CashDepositAsync(string tellerUserId, string idempotencyKey, CashDepositRequest request, CancellationToken cancellationToken)
    {
        BankingRules.ValidateIdempotencyKey(idempotencyKey);
        BankingRules.ValidateAmount(request.Amount);
        var description = BankingRules.NormalizeDescription(request.Description);
        var hash = BankingService.Hash($"{request.DestinationAccountNumber}|{request.Amount:0.00}|{description}");
        var replay = await dbContext.FinancialTransactions.AsNoTracking().Include(x => x.DestinationAccount)
            .SingleOrDefaultAsync(x => x.CreatedByUserId == tellerUserId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.RequestHash != hash) throw new ConflictException("Idempotency-Key đã được dùng cho một yêu cầu khác.");
            return new CashDepositResponse(replay.ReferenceNo, replay.Amount, replay.DestinationAccount.AccountNumber, replay.CreatedAtUtc, true);
        }

        await using var sqlTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var account = await dbContext.BankAccounts.SingleOrDefaultAsync(x => x.AccountNumber == request.DestinationAccountNumber, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhận.");
        if (account.Status != AccountStatus.Active) throw new BusinessRuleException("Tài khoản nhận không hoạt động.");
        account.Balance += request.Amount;
        var now = DateTimeOffset.UtcNow;
        var item = new FinancialTransaction
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
        catch (DbUpdateException)
        {
            await sqlTransaction.RollbackAsync(cancellationToken);
            throw new ConflictException("Yêu cầu bị trùng hoặc dữ liệu vừa thay đổi.");
        }
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
}
