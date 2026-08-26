using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Statements;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Statements;

public sealed class StatementService(SUBankDbContext dbContext, IStatementPdfGenerator pdfGenerator) : IStatementService
{
    public async Task<AccountStatement> GetAsync(string userId, string accountNumber, int year, int? month,
        CancellationToken cancellationToken)
    {
        ValidatePeriod(year, month);
        var account = await dbContext.BankAccounts.AsNoTracking()
            .Where(x => x.AccountNumber == accountNumber && x.CustomerProfile.UserId == userId)
            .Select(x => new { x.Id, x.AccountNumber, x.Currency, x.Balance })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        var from = month is null
            ? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero);
        var to = month is null ? from.AddYears(1) : from.AddMonths(1);

        var sinceStart = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from && (x.SourceAccountId == account.Id || x.DestinationAccountId == account.Id))
            .Select(x => new { x.Amount, IsDebit = x.SourceAccountId == account.Id }).ToListAsync(cancellationToken);
        var opening = account.Balance - sinceStart.Sum(x => x.IsDebit ? -x.Amount : x.Amount);
        var period = await dbContext.FinancialTransactions.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc < to &&
                        (x.SourceAccountId == account.Id || x.DestinationAccountId == account.Id))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.ReferenceNo, Type = x.Type.ToString(), x.Amount, x.Description, x.CreatedAtUtc,
                IsDebit = x.SourceAccountId == account.Id }).ToListAsync(cancellationToken);
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
        if (year < 2000 || year > DateTime.UtcNow.Year + 1)
            throw new BusinessRuleException("Năm sao kê không hợp lệ.");
        if (month is < 1 or > 12) throw new BusinessRuleException("Tháng sao kê phải từ 1 đến 12.");
    }
}
