using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.Transactions;
using SUBank.Contracts.Transfers;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Banking;

public sealed class BankingService(
    SUBankDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IRealtimeNotifier realtimeNotifier) : IBankingService
{
    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(string userId, CancellationToken cancellationToken) =>
        await dbContext.BankAccounts.AsNoTracking()
            .Where(x => x.CustomerProfile.UserId == userId)
            .OrderBy(x => x.AccountNumber)
            .Select(x => new AccountSummary(x.AccountNumber, x.Balance, x.Currency, x.Status.ToString()))
            .ToListAsync(cancellationToken);

    public async Task<AccountDetail?> GetAccountAsync(string userId, string accountNumber, CancellationToken cancellationToken) =>
        await dbContext.BankAccounts.AsNoTracking()
            .Where(x => x.CustomerProfile.UserId == userId && x.AccountNumber == accountNumber)
            .Select(x => new AccountDetail(x.AccountNumber, x.Balance, x.Currency, x.Status.ToString(), x.CustomerProfile.FullName))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ResolvedAccount?> ResolveAccountAsync(string accountNumber, CancellationToken cancellationToken) =>
        await dbContext.BankAccounts.AsNoTracking()
            .Where(x => x.AccountNumber == accountNumber)
            .Select(x => new ResolvedAccount(x.AccountNumber, MaskName(x.CustomerProfile.FullName), x.Status.ToString()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TransactionSummary>> GetTransactionsAsync(string userId, string accountNumber, CancellationToken cancellationToken)
    {
        await EnsureOwnedAccountAsync(userId, accountNumber, cancellationToken);
        return await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.SourceAccount!.AccountNumber == accountNumber || x.DestinationAccount.AccountNumber == accountNumber)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100)
            .Select(x => new TransactionSummary(x.ReferenceNo, x.Type.ToString(), x.Amount,
                x.SourceAccount == null ? null : x.SourceAccount.AccountNumber, x.DestinationAccount.AccountNumber,
                x.Description, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionDetail?> GetTransactionAsync(string userId, string referenceNo, CancellationToken cancellationToken) =>
        await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.ReferenceNo == referenceNo &&
                (x.SourceAccount!.CustomerProfile.UserId == userId || x.DestinationAccount.CustomerProfile.UserId == userId))
            .Select(x => new TransactionDetail(x.ReferenceNo, x.Type.ToString(), x.Amount,
                x.SourceAccount == null ? null : x.SourceAccount.AccountNumber, x.DestinationAccount.AccountNumber,
                x.Description, x.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<TransferResponse> TransferAsync(string userId, string idempotencyKey, TransferRequest request, CancellationToken cancellationToken)
    {
        BankingRules.ValidateIdempotencyKey(idempotencyKey);
        BankingRules.ValidateAmount(request.Amount);
        var description = BankingRules.NormalizeDescription(request.Description);
        var requestHash = Hash($"{request.SourceAccountNumber}|{request.DestinationAccountNumber}|{request.Amount:0.00}|{description}");
        var replay = await dbContext.FinancialTransactions.AsNoTracking()
            .Include(x => x.SourceAccount).Include(x => x.DestinationAccount)
            .SingleOrDefaultAsync(x => x.CreatedByUserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.RequestHash != requestHash) throw new ConflictException("Idempotency-Key đã được dùng cho một yêu cầu khác.");
            return new TransferResponse(replay.ReferenceNo, replay.Amount, replay.SourceAccount!.AccountNumber,
                replay.DestinationAccount.AccountNumber, replay.CreatedAtUtc, true);
        }

        if (request.SourceAccountNumber == request.DestinationAccountNumber)
            throw new BusinessRuleException("Tài khoản nguồn và tài khoản nhận phải khác nhau.");
        var user = await userManager.FindByIdAsync(userId) ?? throw new AuthenticationException("Người dùng không tồn tại.");
        if (!user.IsActive || await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản không thể thực hiện giao dịch.");
        if (user.TransactionPasswordHash is null ||
            passwordHasher.VerifyHashedPassword(user, user.TransactionPasswordHash, request.TransactionPassword) == PasswordVerificationResult.Failed)
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                user.LockedAtUtc = DateTimeOffset.UtcNow;
                await userManager.UpdateAsync(user);
                dbContext.AuditLogs.Add(new AuditLog { UserId = userId, Action = "USER_LOCKED", Result = AuditResult.Success, CreatedAtUtc = DateTimeOffset.UtcNow });
            }
            dbContext.AuditLogs.Add(new AuditLog { UserId = userId, Action = "TRANSACTION_PASSWORD_FAILED", Result = AuditResult.Failure, CreatedAtUtc = DateTimeOffset.UtcNow });
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new AuthenticationException("Mật khẩu giao dịch không đúng.");
        }
        await userManager.ResetAccessFailedCountAsync(user);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var source = await dbContext.BankAccounts.Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.AccountNumber == request.SourceAccountNumber, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nguồn.");
        var destination = await dbContext.BankAccounts.Include(x => x.CustomerProfile)
            .SingleOrDefaultAsync(x => x.AccountNumber == request.DestinationAccountNumber, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản nhận.");
        if (source.CustomerProfile.UserId != userId) throw new NotFoundException("Không tìm thấy tài khoản nguồn.");
        if (source.Status != AccountStatus.Active || destination.Status != AccountStatus.Active)
            throw new BusinessRuleException("Tài khoản nguồn hoặc tài khoản nhận không hoạt động.");
        if (source.Balance < request.Amount) throw new BusinessRuleException("Số dư không đủ.");

        source.Balance -= request.Amount;
        destination.Balance += request.Amount;
        var now = DateTimeOffset.UtcNow;
        var item = new FinancialTransaction
        {
            ReferenceNo = NewReference("TRF"), SourceAccountId = source.Id, DestinationAccountId = destination.Id,
            CreatedByUserId = userId, Type = TransactionType.Transfer, Amount = request.Amount,
            Description = description, IdempotencyKey = idempotencyKey, RequestHash = requestHash, CreatedAtUtc = now
        };
        dbContext.FinancialTransactions.Add(item);
        dbContext.AuditLogs.Add(new AuditLog { UserId = userId, Action = "TRANSFER", EntityType = "FinancialTransaction", EntityId = item.ReferenceNo, Result = AuditResult.Success, CreatedAtUtc = now });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("Số dư vừa thay đổi. Vui lòng kiểm tra và thử lại.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("Yêu cầu bị trùng hoặc dữ liệu vừa thay đổi.");
        }
        await NotifyTransactionAsync(source.CustomerProfile.UserId, source.AccountNumber,
            destination.CustomerProfile.UserId, destination.AccountNumber, item.ReferenceNo);
        return new TransferResponse(item.ReferenceNo, item.Amount, source.AccountNumber, destination.AccountNumber, now, false);
    }

    private async Task NotifyTransactionAsync(string sourceUserId, string sourceAccountNumber,
        string destinationUserId, string destinationAccountNumber, string referenceNo)
    {
        await realtimeNotifier.BalanceChangedAsync(sourceUserId, sourceAccountNumber, CancellationToken.None);
        await realtimeNotifier.TransactionReceivedAsync(sourceUserId, referenceNo, sourceAccountNumber, CancellationToken.None);
        await realtimeNotifier.BalanceChangedAsync(destinationUserId, destinationAccountNumber, CancellationToken.None);
        await realtimeNotifier.TransactionReceivedAsync(destinationUserId, referenceNo, destinationAccountNumber, CancellationToken.None);
    }

    private async Task EnsureOwnedAccountAsync(string userId, string accountNumber, CancellationToken cancellationToken)
    {
        if (!await dbContext.BankAccounts.AnyAsync(x => x.AccountNumber == accountNumber && x.CustomerProfile.UserId == userId, cancellationToken))
            throw new NotFoundException("Không tìm thấy tài khoản.");
    }

    internal static string NewReference(string prefix) => $"{prefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(1000, 9999)}";
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string MaskName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(x => x.Length < 2 ? x : $"{x[0]}{new string('*', x.Length - 1)}"));
    }
}
