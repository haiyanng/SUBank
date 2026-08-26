using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("FinancialTransactions", table =>
        {
            table.HasCheckConstraint("CK_FinancialTransactions_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_FinancialTransactions_Accounts", "([Type] = 'Transfer' AND [SourceAccountId] IS NOT NULL AND [SourceAccountId] <> [DestinationAccountId]) OR ([Type] = 'CashDeposit' AND [SourceAccountId] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceNo).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(280);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.HasIndex(x => x.ReferenceNo).IsUnique();
        builder.HasIndex(x => new { x.CreatedByUserId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.SourceAccountId, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.DestinationAccountId, x.CreatedAtUtc });
        builder.HasOne(x => x.SourceAccount).WithMany(x => x.OutgoingTransactions)
            .HasForeignKey(x => x.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationAccount).WithMany(x => x.IncomingTransactions)
            .HasForeignKey(x => x.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
