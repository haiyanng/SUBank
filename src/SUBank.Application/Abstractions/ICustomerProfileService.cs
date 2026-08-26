using SUBank.Contracts.Profiles;

namespace SUBank.Application.Abstractions;

public interface ICustomerProfileService
{
    Task<CustomerProfileDetail?> GetAsync(string userId, CancellationToken cancellationToken);
}
