using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Auth;

namespace SUBank.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookie = "subank_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await authService.LoginAsync(request, cancellationToken);
        WriteRefreshCookie(session);
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var token)) throw new AuthenticationException("Không có refresh token.");
        var session = await authService.RefreshAsync(token, cancellationToken);
        WriteRefreshCookie(session);
        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(RefreshCookie, out var token)) await authService.LogoutAsync(token, cancellationToken);
        Response.Cookies.Delete(RefreshCookie);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserSummary>> Me(CancellationToken cancellationToken)
    {
        var user = await authService.GetCurrentUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    private void WriteRefreshCookie(AuthSession session) => Response.Cookies.Append(RefreshCookie, session.RefreshToken,
        new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = session.RefreshExpiresAtUtc, Path = "/api/auth" });
}
