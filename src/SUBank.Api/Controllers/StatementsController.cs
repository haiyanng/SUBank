using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Statements;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Customer")]
[Route("api/accounts/{accountNumber}/statements")]
public sealed class StatementsController(IStatementService service) : ControllerBase
{
    [HttpGet]
    public Task<AccountStatement> Get(string accountNumber, [FromQuery] int year, [FromQuery] int? month,
        CancellationToken cancellationToken) =>
        service.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, accountNumber, year, month, cancellationToken);

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(string accountNumber, [FromQuery] int year, [FromQuery] int? month,
        CancellationToken cancellationToken)
    {
        var bytes = await service.GetPdfAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!, accountNumber, year, month, cancellationToken);
        var period = month is null ? year.ToString() : $"{year}-{month:00}";
        return File(bytes, "application/pdf", $"SUBank-{accountNumber}-{period}.pdf");
    }
}
