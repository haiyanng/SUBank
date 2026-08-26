using SUBank.Domain.Enums;

namespace SUBank.Domain.Entities;

public sealed class FinancialTransaction
{
    public long Id { get; set; }
    public required string ReferenceNo { get; set; }
    public long? SourceAccountId { get; set; }
    public long DestinationAccountId { get; set; }
    public required string CreatedByUserId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public BankAccount? SourceAccount { get; set; }
    public BankAccount DestinationAccount { get; set; } = null!;
}
