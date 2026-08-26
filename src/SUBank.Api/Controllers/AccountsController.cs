using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.Transactions;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/accounts")]
public sealed class AccountsController(IBankingService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public Task<IReadOnlyList<AccountSummary>> GetAll(CancellationToken ct) => service.GetAccountsAsync(UserId, ct);

    [HttpGet("{accountNumber}")]
    public async Task<ActionResult<AccountDetail>> Get(string accountNumber, CancellationToken ct)
    {
        var result = await service.GetAccountAsync(UserId, accountNumber, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("resolve/{accountNumber}")]
    [EnableRateLimiting("AccountResolution")]
    public async Task<ActionResult<ResolvedAccount>> Resolve(string accountNumber, CancellationToken ct)
    {
        var result = await service.ResolveAccountAsync(accountNumber, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{accountNumber}/transactions")]
    public Task<IReadOnlyList<TransactionSummary>> Transactions(string accountNumber, CancellationToken ct) => service.GetTransactionsAsync(UserId, accountNumber, ct);
}
