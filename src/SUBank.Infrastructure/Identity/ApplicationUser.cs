using Microsoft.AspNetCore.Identity;

namespace SUBank.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string? TransactionPasswordHash { get; set; }
    public DateTimeOffset? LockedAtUtc { get; set; }
    public bool IsAdminSuspended { get; set; }
    public DateTimeOffset? AdminSuspendedAtUtc { get; set; }
    public string? AdminSuspensionReason { get; set; }
    public string? AdminSuspendedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
