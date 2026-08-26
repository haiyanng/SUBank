using SUBank.Domain.Enums;

namespace SUBank.Domain.Entities;

public sealed class BankAccount
{
    public long Id { get; set; }
    public long CustomerProfileId { get; set; }
    public required string AccountNumber { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "VND";
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public byte[] RowVersion { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }

    public CustomerProfile CustomerProfile { get; set; } = null!;
    public ICollection<FinancialTransaction> OutgoingTransactions { get; set; } = [];
    public ICollection<FinancialTransaction> IncomingTransactions { get; set; } = [];
}
