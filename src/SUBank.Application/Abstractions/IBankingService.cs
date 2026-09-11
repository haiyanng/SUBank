using SUBank.Contracts.Accounts;
using SUBank.Contracts.Transactions;
using SUBank.Contracts.Transfers;

namespace SUBank.Application.Abstractions;

public interface IBankingService
{
    Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(string userId, CancellationToken cancellationToken);
    Task<AccountDetail?> GetAccountAsync(string userId, string accountNumber, CancellationToken cancellationToken);
    Task<ResolvedAccount?> ResolveAccountAsync(string accountNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<TransactionSummary>> GetTransactionsAsync(string userId, string accountNumber, CancellationToken cancellationToken);
    Task<TransactionDetail?> GetTransactionAsync(string userId, string referenceNo, CancellationToken cancellationToken);
    Task<TransferResponse> TransferAsync(string userId, string idempotencyKey, TransferRequest request, CancellationToken cancellationToken);
}
