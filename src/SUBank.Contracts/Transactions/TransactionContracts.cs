namespace SUBank.Contracts.Transactions;

public sealed record TransactionSummary(string ReferenceNo, string Type, decimal Amount, string? SourceAccountNumber, string DestinationAccountNumber, string? Description, DateTimeOffset CreatedAtUtc);
public sealed record TransactionDetail(string ReferenceNo, string Type, decimal Amount, string? SourceAccountNumber, string DestinationAccountNumber, string? Description, DateTimeOffset CreatedAtUtc);
