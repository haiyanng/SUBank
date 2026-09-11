using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Staff;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Teller")]
[Route("api/teller")]
public sealed class TellerController(IStaffService service) : ControllerBase
{
    [HttpPost("cash-deposits")]
    [EnableRateLimiting("CashDeposit")]
    public async Task<ActionResult<CashDepositResponse>> Deposit([FromHeader(Name = "Idempotency-Key")] string? key, CashDepositRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BusinessRuleException("Thiếu header Idempotency-Key.");
        return Ok(await service.CashDepositAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, key, request, ct));
    }
}
