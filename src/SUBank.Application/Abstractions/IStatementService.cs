using SUBank.Contracts.Statements;

namespace SUBank.Application.Abstractions;

public interface IStatementService
{
    Task<AccountStatement> GetAsync(string userId, string accountNumber, int year, int? month, CancellationToken cancellationToken);
    Task<byte[]> GetPdfAsync(string userId, string accountNumber, int year, int? month, CancellationToken cancellationToken);
}
