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

    public async Task InitializeAsync(DatabaseInitializationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.SeedDemoData) EnsureDemoSeedTarget(options);
        if (options.ApplyMigrationsOnStartup) await dbContext.Database.MigrateAsync(cancellationToken);
        if (!options.SeedDemoData) return;
        foreach (var role in new[] { "Customer", "Teller", "Admin" })
            if (!await roleManager.RoleExistsAsync(role)) EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)));

        var customerA = await EnsureUserAsync("0900000001", "Customer", true, "customer.a", cancellationToken);
        var customerB = await EnsureUserAsync("0900000002", "Customer", true, "customer.b", cancellationToken);
        var customerC = await EnsureUserAsync("0900000003", "Customer", true, cancellationToken: cancellationToken);
        var customerD = await EnsureUserAsync("0900000004", "Customer", true, cancellationToken: cancellationToken);
        var customerE = await EnsureUserAsync("0900000005", "Customer", true, cancellationToken: cancellationToken);
        await EnsureUserAsync("teller", "Teller", false);
        await EnsureUserAsync("admin", "Admin", false);
        var customerAProfile = await EnsureCustomerAsync(customerA, "Nguyễn An", new DateOnly(1998, 5, 10), "001098000001",
            "0900000001", "annguyen@subank.demo", "Hà Nội", "0900000001", "1000000001", 100_000_000m, cancellationToken);
        var customerBProfile = await EnsureCustomerAsync(customerB, "Trần Bình", new DateOnly(1999, 8, 15), "001099000002",
            "0900000002", "binhtran@subank.demo", "TP. Hồ Chí Minh", "0900000002", "1000000002", 50_000_000m, cancellationToken);
        var customerCProfile = await EnsureCustomerAsync(customerC, "Lê Minh Châu", new DateOnly(2000, 3, 20), "001200000003",
            "0900000003", "chaule@subank.demo", "Đà Nẵng", "3000000001", "0900000003", 80_000_000m, cancellationToken);
        var customerDProfile = await EnsureCustomerAsync(customerD, "Phạm Gia Huy", new DateOnly(1997, 11, 8), "001097000004",
            "0900000004", "huypham@subank.demo", "Hải Phòng", "4000000001", "0900000004", 60_000_000m, cancellationToken);
        var customerEProfile = await EnsureCustomerAsync(customerE, "Đỗ Khánh Linh", new DateOnly(2001, 6, 25), "001201000005",
            "0900000005", "linhdo@subank.demo", "Cần Thơ", "5000000001", "0900000005", 40_000_000m, cancellationToken);
        await EnsureAccountAsync(customerAProfile, "1000000003", 20_000_000m, cancellationToken);
        await EnsureAccountAsync(customerAProfile, "1234567890", 15_000_000m, cancellationToken);
        await EnsureAccountAsync(customerAProfile, "1234567891", 5_000_000m, cancellationToken);
        await EnsureAccountAsync(customerBProfile, "1000000004", 10_000_000m, cancellationToken);
        await EnsureAccountAsync(customerBProfile, "2234567890", 15_000_000m, cancellationToken);
        await EnsureAccountAsync(customerBProfile, "2234567891", 5_000_000m, cancellationToken);
        await EnsureAccountAsync(customerCProfile, "3000000002", 20_000_000m, cancellationToken);
        await EnsureAccountAsync(customerCProfile, "3000000003", 10_000_000m, cancellationToken);
        await EnsureAccountAsync(customerDProfile, "4000000002", 15_000_000m, cancellationToken);
        await EnsureAccountAsync(customerDProfile, "4000000003", 5_000_000m, cancellationToken);
        await EnsureAccountAsync(customerEProfile, "5000000002", 10_000_000m, cancellationToken);
        await EnsureAccountAsync(customerEProfile, "5000000003", 5_000_000m, cancellationToken);
    }

    private async Task<ApplicationUser> EnsureUserAsync(string userName, string role, bool transactionPassword,
        string? legacyUserName = null, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(userName);
        var legacyUser = legacyUserName is null ? null : await userManager.FindByNameAsync(legacyUserName);

        if (user is not null && legacyUser is not null && user.Id != legacyUser.Id)
        {
            var userHasProfile = await dbContext.CustomerProfiles
                .AnyAsync(x => x.UserId == user.Id, cancellationToken);
            var legacyUserHasProfile = await dbContext.CustomerProfiles
                .AnyAsync(x => x.UserId == legacyUser.Id, cancellationToken);

            if (legacyUserHasProfile && !userHasProfile)
            {
                await QuarantineUserAsync(user);
                EnsureSucceeded(await userManager.SetUserNameAsync(legacyUser, userName));
                user = legacyUser;
            }
            else if (legacyUserHasProfile && userHasProfile)
            {
                throw new InvalidOperationException(
                    $"Không thể hợp nhất hai Customer '{userName}' và '{legacyUserName}' vì cả hai đều có hồ sơ.");
            }
            else if (!legacyUserHasProfile)
            {
                await RemoveOrDeactivateLegacyUserAsync(legacyUser, cancellationToken);
            }
        }

        if (user is null && legacyUser is not null)
        {
            EnsureSucceeded(await userManager.SetUserNameAsync(legacyUser, userName));
            user = legacyUser;
        }

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

    private async Task<CustomerProfile> EnsureCustomerAsync(
        ApplicationUser user, string fullName, DateOnly dateOfBirth, string identityCardNumber,
        string phone, string email, string address, string accountNumber, string legacyAccountNumber,
        decimal openingBalance,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            profile = new CustomerProfile
            {
                UserId = user.Id,
                FullName = fullName,
                DateOfBirth = dateOfBirth,
                IdentityCardNumber = identityCardNumber,
                Phone = phone,
                Email = email,
                PermanentAddress = address,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            dbContext.CustomerProfiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!string.Equals(profile.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            profile.Email = email;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureAccountAsync(profile, accountNumber, openingBalance, cancellationToken, legacyAccountNumber);
        return profile;
    }

    private async Task EnsureAccountAsync(CustomerProfile profile, string accountNumber, decimal openingBalance,
        CancellationToken cancellationToken, string? legacyAccountNumber = null)
    {
        var account = await dbContext.BankAccounts
            .SingleOrDefaultAsync(x => x.AccountNumber == accountNumber, cancellationToken);
        if (account is not null)
        {
            if (account.CustomerProfileId != profile.Id)
                throw new InvalidOperationException($"Số tài khoản demo '{accountNumber}' đã thuộc Customer khác.");
            return;
        }

        if (legacyAccountNumber is not null)
        {
            account = await dbContext.BankAccounts
                .SingleOrDefaultAsync(x => x.AccountNumber == legacyAccountNumber, cancellationToken);
            if (account is not null)
            {
                if (account.CustomerProfileId != profile.Id)
                    throw new InvalidOperationException($"Số tài khoản cũ '{legacyAccountNumber}' đã thuộc Customer khác.");
                account.AccountNumber = accountNumber;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        dbContext.BankAccounts.Add(new BankAccount
        {
            CustomerProfileId = profile.Id,
            AccountNumber = accountNumber,
            Balance = openingBalance,
            Currency = "VND",
            Status = AccountStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task QuarantineUserAsync(ApplicationUser user)
    {
        EnsureSucceeded(await userManager.SetUserNameAsync(user, $"disabled-{user.Id}"));
        user.IsActive = false;
        EnsureSucceeded(await userManager.UpdateAsync(user));
    }

    private async Task RemoveOrDeactivateLegacyUserAsync(
        ApplicationUser legacyUser,
        CancellationToken cancellationToken)
    {
        var hasBusinessHistory = await dbContext.FinancialTransactions
            .AnyAsync(x => x.CreatedByUserId == legacyUser.Id, cancellationToken);
        var hasAuditHistory = await dbContext.AuditLogs
            .AnyAsync(x => x.UserId == legacyUser.Id, cancellationToken);

        if (!hasBusinessHistory && !hasAuditHistory)
        {
            EnsureSucceeded(await userManager.DeleteAsync(legacyUser));
            return;
        }

        if (!legacyUser.IsActive) return;
        legacyUser.IsActive = false;
        EnsureSucceeded(await userManager.UpdateAsync(legacyUser));
    }

    private void EnsureDemoSeedTarget(DatabaseInitializationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AllowedSeedDataSource) ||
            string.IsNullOrWhiteSpace(options.AllowedSeedDatabase))
            throw new InvalidOperationException(
                "Demo seed requires an explicit allowed data source and database.");

        var connection = dbContext.Database.GetDbConnection();
        if (!string.Equals(connection.DataSource, options.AllowedSeedDataSource, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(connection.Database, options.AllowedSeedDatabase, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Demo seed is not allowed for data source '{connection.DataSource}' and database '{connection.Database}'.");
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
