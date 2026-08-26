using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(80).IsUnicode(false).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.EntityId).HasMaxLength(100);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        builder.Property(x => x.CorrelationId).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Details).HasMaxLength(1000);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
