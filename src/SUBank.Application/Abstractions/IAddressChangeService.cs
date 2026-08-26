using SUBank.Contracts.AddressChanges;

namespace SUBank.Application.Abstractions;

public interface IAddressChangeService
{
    Task<AddressChangeRequestSummary> CreateAsync(string userId, CreateAddressChangeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AddressChangeRequestSummary>> GetMineAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AddressChangeRequestSummary>> GetPendingAsync(CancellationToken cancellationToken);
    Task ApproveAsync(string adminUserId, string requestNo, CancellationToken cancellationToken);
    Task RejectAsync(string adminUserId, string requestNo, RejectAddressChangeRequest request, CancellationToken cancellationToken);
}
