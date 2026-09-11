using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.TransactionPasswordHash).HasMaxLength(500);
        builder.Property(x => x.IsAdminSuspended).HasDefaultValue(false);
        builder.Property(x => x.AdminSuspensionReason).HasMaxLength(500);
        builder.Property(x => x.AdminSuspendedByUserId).HasMaxLength(450);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
    }
}
