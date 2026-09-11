using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Transactions;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/transactions")]
public sealed class TransactionsController(IBankingService service) : ControllerBase
{
    [HttpGet("{referenceNo}")]
    public async Task<ActionResult<TransactionDetail>> Get(string referenceNo, CancellationToken ct)
    {
        var result = await service.GetTransactionAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, referenceNo, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
