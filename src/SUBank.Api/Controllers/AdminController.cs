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
    [HttpGet("locked-users")]
    public Task<IReadOnlyList<LockedUserSummary>> LockedUsers(CancellationToken ct) => service.GetLockedUsersAsync(ct);

    [HttpGet("audit-logs")]
    public Task<IReadOnlyList<AuditLogSummary>> AuditLogs(CancellationToken ct) => service.GetAuditLogsAsync(ct);

    [HttpPost("users/{userName}/unlock")]
    public async Task<IActionResult> Unlock(string userName, CancellationToken ct)
    {
        await service.UnlockUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, userName, ct);
        return NoContent();
    }
}
