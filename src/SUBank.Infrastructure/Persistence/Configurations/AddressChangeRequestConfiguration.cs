using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class AddressChangeRequestConfiguration : IEntityTypeConfiguration<AddressChangeRequest>
{
    public void Configure(EntityTypeBuilder<AddressChangeRequest> builder)
    {
        builder.ToTable("AddressChangeRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestNo).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.PermanentAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TemporaryAddress).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(280);
        builder.HasIndex(x => x.RequestNo).IsUnique();
        builder.HasIndex(x => new { x.CustomerProfileId, x.Status });
        builder.HasOne(x => x.CustomerProfile).WithMany().HasForeignKey(x => x.CustomerProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
