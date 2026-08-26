using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence.Configurations;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SessionId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.RevocationReason).HasMaxLength(50).IsUnicode(false);
        builder.HasIndex(x => new { x.UserId, x.SessionId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.RevokedAtUtc });
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
