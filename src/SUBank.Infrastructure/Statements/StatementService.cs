using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
using SUBank.Contracts.Statements;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Statements;

public sealed class StatementService(SUBankDbContext dbContext, IStatementPdfGenerator pdfGenerator) : IStatementService
{
    public async Task<AccountStatement> GetAsync(string userId, string accountNumber, int year, int? month,
        CancellationToken cancellationToken)
    {
        BankingRules.ValidateAccountNumber(accountNumber, "Số tài khoản");
        ValidatePeriod(year, month);
        var vietnamOffset = TimeSpan.FromHours(7);
        var localFrom = month is null
            ? new DateTimeOffset(year, 1, 1, 0, 0, 0, vietnamOffset)
            : new DateTimeOffset(year, month.Value, 1, 0, 0, 0, vietnamOffset);
        var localTo = month is null ? localFrom.AddYears(1) : localFrom.AddMonths(1);
        var from = localFrom.ToUniversalTime();
        var to = localTo.ToUniversalTime();

        try
        {
            return await GetConsistentAsync(userId, accountNumber, from, to, cancellationToken);
        }
        catch (Exception exception) when (IsSqlDeadlock(exception))
        {
            dbContext.ChangeTracker.Clear();
            try
            {
                return await GetConsistentAsync(userId, accountNumber, from, to, cancellationToken);
            }
            catch (Exception retryException) when (IsSqlDeadlock(retryException))
            {
                throw new ConflictException("Sao kê đang được cập nhật đồng thời. Vui lòng thử lại.");
            }
        }
    }

    private async Task<AccountStatement> GetConsistentAsync(
        string userId,
        string accountNumber,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        var account = await dbContext.BankAccounts.AsNoTracking()
            .Where(x => x.AccountNumber == accountNumber && x.CustomerProfile.UserId == userId)
            .Select(x => new { x.Id, x.AccountNumber, x.Currency, x.Balance })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        var asOfUtc = DateTimeOffset.UtcNow;

        var sinceStart = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc <= asOfUtc &&
                        (x.SourceAccountId == account.Id || x.DestinationAccountId == account.Id))
            .Select(x => new { x.Amount, IsDebit = x.SourceAccountId == account.Id }).ToListAsync(cancellationToken);
        var period = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc < to && x.CreatedAtUtc <= asOfUtc &&
                        (x.SourceAccountId == account.Id || x.DestinationAccountId == account.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.ReferenceNo,
                Type = x.Type.ToString(),
                x.Amount,
                x.Description,
                x.CreatedAtUtc,
                IsDebit = x.SourceAccountId == account.Id
            }).ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var opening = account.Balance - sinceStart.Sum(x => x.IsDebit ? -x.Amount : x.Amount);
        var totalDebit = period.Where(x => x.IsDebit).Sum(x => x.Amount);
        var totalCredit = period.Where(x => !x.IsDebit).Sum(x => x.Amount);
        return new AccountStatement(account.AccountNumber, account.Currency, from, to, opening,
            opening + totalCredit - totalDebit, totalCredit, totalDebit,
            period.Select(x => new StatementTransaction(x.ReferenceNo, x.Type, x.Amount,
                x.IsDebit ? "Debit" : "Credit", x.Description, x.CreatedAtUtc)).ToList());
    }

    public async Task<byte[]> GetPdfAsync(string userId, string accountNumber, int year, int? month,
        CancellationToken cancellationToken) =>
        pdfGenerator.Generate(await GetAsync(userId, accountNumber, year, month, cancellationToken));

    private static void ValidatePeriod(int year, int? month)
    {
        if (year < 2000 || year > DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Year + 1)
            throw new BusinessRuleException("Năm sao kê không hợp lệ.");
        if (month is < 1 or > 12) throw new BusinessRuleException("Tháng sao kê phải từ 1 đến 12.");
    }

    private static bool IsSqlDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 }) return true;
        }

        return false;
    }
}
