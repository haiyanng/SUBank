using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Accounts;
using SUBank.Contracts.Transactions;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer,Teller")]
[Route("api/accounts")]
public sealed class AccountsController(IBankingService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    [Authorize(Roles = "Customer")]
    public Task<IReadOnlyList<AccountSummary>> GetAll(CancellationToken ct) => service.GetAccountsAsync(UserId, ct);

    [HttpGet("{accountNumber}")]
    [Authorize(Roles = "Customer")]
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
    [Authorize(Roles = "Customer")]
    public Task<IReadOnlyList<TransactionSummary>> Transactions(string accountNumber, CancellationToken ct) => service.GetTransactionsAsync(UserId, accountNumber, ct);
}
