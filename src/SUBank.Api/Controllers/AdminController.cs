using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SUBank.Application.Abstractions;
using SUBank.Contracts.Staff;

namespace SUBank.Api.Controllers;

[ApiController, Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(IStaffService service) : ControllerBase
{
    [HttpGet("customers")]
    [HttpGet("users")]
    public Task<IReadOnlyList<CustomerManagementSummary>> Customers([FromQuery] string? search, CancellationToken ct) =>
        service.GetCustomersAsync(search, ct);

    [HttpGet("customers/{userName}")]
    public Task<CustomerManagementDetail> Customer(string userName, CancellationToken ct) =>
        service.GetCustomerAsync(userName, ct);

    [HttpGet("locked-users")]
    public Task<IReadOnlyList<LockedUserSummary>> LockedUsers(CancellationToken ct) => service.GetLockedUsersAsync(ct);

    [HttpGet("audit-logs")]
    public Task<IReadOnlyList<AuditLogSummary>> AuditLogs(CancellationToken ct) => service.GetAuditLogsAsync(ct);

    [HttpPost("customers/{userName}/suspend")]
    public async Task<IActionResult> Suspend(
        string userName,
        [FromBody] SuspendCustomerRequest request,
        CancellationToken ct)
    {
        await service.SuspendCustomerAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!, userName, request, ct);
        return NoContent();
    }

    [HttpPost("customers/{userName}/resume")]
    public async Task<IActionResult> Resume(string userName, CancellationToken ct)
    {
        await service.ResumeCustomerAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, userName, ct);
        return NoContent();
    }

    [HttpPost("customers/{userName}/identity-lockout/unlock")]
    [HttpPost("users/{userName}/unlock")]
    public async Task<IActionResult> ClearIdentityLockout(string userName, CancellationToken ct)
    {
        await service.ClearCustomerIdentityLockoutAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!, userName, ct);
        return NoContent();
    }
}
