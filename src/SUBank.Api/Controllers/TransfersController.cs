using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Transfers;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/transfers")]
public sealed class TransfersController(IBankingService service) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("TransactionPassword")]
    public async Task<ActionResult<TransferResponse>> Create([FromHeader(Name = "Idempotency-Key")] string? key, TransferRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BusinessRuleException("Thiếu header Idempotency-Key.");
        return Ok(await service.TransferAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, key, request, ct));
    }
}
