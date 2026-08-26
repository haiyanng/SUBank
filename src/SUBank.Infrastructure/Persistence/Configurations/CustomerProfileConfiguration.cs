using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("CustomerProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.IdentityNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsRequired();
        builder.Property(x => x.PermanentAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TemporaryAddress).HasMaxLength(500);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.IdentityNumber).IsUnique();
        builder.HasIndex(x => x.Phone).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CustomerProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
