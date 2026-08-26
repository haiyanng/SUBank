namespace SUBank.Contracts.Statements;

public sealed record StatementTransaction(
    string ReferenceNo, string Type, decimal Amount, string Direction,
    string? Description, DateTimeOffset CreatedAtUtc);

public sealed record AccountStatement(
    string AccountNumber, string Currency, DateTimeOffset FromUtc, DateTimeOffset ToUtc,
    decimal OpeningBalance, decimal ClosingBalance, decimal TotalCredit, decimal TotalDebit,
    IReadOnlyList<StatementTransaction> Transactions);
