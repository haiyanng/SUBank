namespace SUBank.Contracts.Transfers;

public sealed record TransferRequest(string SourceAccountNumber, string DestinationAccountNumber, decimal Amount, string? Description, string TransactionPassword);
public sealed record TransferResponse(string ReferenceNo, decimal Amount, string SourceAccountNumber, string DestinationAccountNumber, DateTimeOffset CreatedAtUtc, bool Replayed);
