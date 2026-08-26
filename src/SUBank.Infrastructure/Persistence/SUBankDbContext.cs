using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SUBank.Domain.Entities;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence;

public sealed class SUBankDbContext(DbContextOptions<SUBankDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AddressChangeRequest> AddressChangeRequests => Set<AddressChangeRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SUBankDbContext).Assembly);
    }
}
