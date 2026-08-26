using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Profiles;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Profiles;

public sealed class CustomerProfileService(SUBankDbContext dbContext) : ICustomerProfileService
{
    public async Task<CustomerProfileDetail?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.CustomerProfiles.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                x.FullName,
                x.DateOfBirth,
                x.IdentityNumber,
                x.Phone,
                x.Email,
                x.PermanentAddress,
                x.TemporaryAddress,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        return profile is null
            ? null
            : new CustomerProfileDetail(
                profile.FullName,
                profile.DateOfBirth,
                MaskIdentityNumber(profile.IdentityNumber),
                profile.Phone,
                profile.Email,
                profile.PermanentAddress,
                profile.TemporaryAddress,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc);
    }

    private static string MaskIdentityNumber(string value)
    {
        if (value.Length <= 6)
            return new string('*', value.Length);

        return $"{value[..3]}{new string('*', value.Length - 6)}{value[^3..]}";
    }
}
