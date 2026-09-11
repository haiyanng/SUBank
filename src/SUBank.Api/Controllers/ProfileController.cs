using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Profiles;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/profile")]
public sealed class ProfileController(ICustomerProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CustomerProfileDetail>> Get(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profile = await service.GetAsync(userId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }
}
