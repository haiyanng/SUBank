using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccounts", table =>
        {
            table.HasCheckConstraint("CK_BankAccounts_Balance", "[Balance] >= 0");
            table.HasCheckConstraint("CK_BankAccounts_Currency", "[Currency] = 'VND'");
            table.HasCheckConstraint("CK_BankAccounts_Number", "[AccountNumber] NOT LIKE '%[^0-9]%' AND LEN([AccountNumber]) = 10");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountNumber).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.Balance).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.AccountNumber).IsUnique();
        builder.HasIndex(x => x.CustomerProfileId);
        builder.HasOne(x => x.CustomerProfile).WithMany(x => x.Accounts)
            .HasForeignKey(x => x.CustomerProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
