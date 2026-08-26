using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Identity;

namespace SUBank.Infrastructure.Persistence;

public sealed class DatabaseInitializer(SUBankDbContext dbContext, RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, IPasswordHasher<ApplicationUser> passwordHasher)
{
    public const string DemoPassword = "Demo@12345";
    public const string DemoTransactionPassword = "123456";

    public async Task InitializeAsync(bool seedDemoData, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        if (!seedDemoData) return;
        foreach (var role in new[] { "Customer", "Teller", "Admin" })
            if (!await roleManager.RoleExistsAsync(role)) EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)));

        var customerA = await EnsureUserAsync("customer.a", "Customer", true);
        var customerB = await EnsureUserAsync("customer.b", "Customer", true);
        await EnsureUserAsync("teller", "Teller", false);
        await EnsureUserAsync("admin", "Admin", false);
        await EnsureCustomerAsync(customerA, "Nguyễn An", new DateOnly(1998, 5, 10), "001098000001",
            "0900000001", "customer.a@subank.demo", "Hà Nội", "1000000001", 100_000_000m, cancellationToken);
        await EnsureCustomerAsync(customerB, "Trần Bình", new DateOnly(1999, 8, 15), "001099000002",
            "0900000002", "customer.b@subank.demo", "TP. Hồ Chí Minh", "1000000002", 50_000_000m, cancellationToken);
    }

    private async Task<ApplicationUser> EnsureUserAsync(string userName, string role, bool transactionPassword)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new ApplicationUser { UserName = userName, IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };
            if (transactionPassword) user.TransactionPasswordHash = passwordHasher.HashPassword(user, DemoTransactionPassword);
            EnsureSucceeded(await userManager.CreateAsync(user, DemoPassword));
        }
        else if (transactionPassword && user.TransactionPasswordHash is null)
        {
            user.TransactionPasswordHash = passwordHasher.HashPassword(user, DemoTransactionPassword);
            EnsureSucceeded(await userManager.UpdateAsync(user));
        }
        if (!await userManager.IsInRoleAsync(user, role)) EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
        return user;
    }

    private async Task EnsureCustomerAsync(
        ApplicationUser user, string fullName, DateOnly dateOfBirth, string identityNumber,
        string phone, string email, string address, string accountNumber, decimal openingBalance,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            profile = new CustomerProfile
            {
                UserId = user.Id, FullName = fullName, DateOfBirth = dateOfBirth, IdentityNumber = identityNumber,
                Phone = phone, Email = email, PermanentAddress = address, CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.CustomerProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.BankAccounts.AnyAsync(x => x.AccountNumber == accountNumber, cancellationToken))
        {
            dbContext.BankAccounts.Add(new BankAccount
            {
                CustomerProfileId = profile.Id, AccountNumber = accountNumber, Balance = openingBalance,
                Currency = "VND", Status = AccountStatus.Active, CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
