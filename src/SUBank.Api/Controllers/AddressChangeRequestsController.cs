using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.AddressChanges;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/address-change-requests")]
public sealed class AddressChangeRequestsController(IAddressChangeService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<AddressChangeRequestSummary>> GetMine(CancellationToken cancellationToken) =>
        service.GetMineAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<AddressChangeRequestSummary>> Create(
        CreateAddressChangeRequest request, CancellationToken cancellationToken) =>
        Ok(await service.CreateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, request, cancellationToken));
}
