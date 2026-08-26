using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.AddressChanges;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Admin")]
[Route("api/admin/address-change-requests")]
public sealed class AdminAddressChangeRequestsController(IAddressChangeService service) : ControllerBase
{
    [HttpGet("pending")]
    public Task<IReadOnlyList<AddressChangeRequestSummary>> GetPending(CancellationToken cancellationToken) =>
        service.GetPendingAsync(cancellationToken);

    [HttpPost("{requestNo}/approve")]
    public async Task<IActionResult> Approve(string requestNo, CancellationToken cancellationToken)
    {
        await service.ApproveAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, requestNo, cancellationToken);
        return NoContent();
    }

    [HttpPost("{requestNo}/reject")]
    public async Task<IActionResult> Reject(
        string requestNo, RejectAddressChangeRequest request, CancellationToken cancellationToken)
    {
        await service.RejectAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, requestNo, request, cancellationToken);
        return NoContent();
    }
}
