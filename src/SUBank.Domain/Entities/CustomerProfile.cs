namespace SUBank.Domain.Entities;

public sealed class CustomerProfile
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required string FullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public required string IdentityNumber { get; set; }
    public required string Phone { get; set; }
    public required string Email { get; set; }
    public required string PermanentAddress { get; set; }
    public string? TemporaryAddress { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ICollection<BankAccount> Accounts { get; set; } = [];
}
