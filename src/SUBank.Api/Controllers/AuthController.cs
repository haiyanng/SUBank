using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SUBank.Api.Infrastructure;
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
        PreventAuthResponseCaching();
        var session = await authService.LoginAsync(request, cancellationToken);
        WriteRefreshCookie(session);
        Response.Headers[AuthProtocol.SessionIdHeader] = session.Response.SessionId;
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        PreventAuthResponseCaching();
        if (!Request.Cookies.TryGetValue(RefreshCookie, out var token)) throw new AuthenticationException("Không có refresh token.");
        var session = await authService.RefreshAsync(token, cancellationToken);
        WriteRefreshCookie(session);
        Response.Headers[AuthProtocol.SessionIdHeader] = session.Response.SessionId;
        return Ok(session.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [AllowInactiveSession]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        PreventAuthResponseCaching();
        var hasRefreshCookie = Request.Cookies.TryGetValue(RefreshCookie, out var token);
        var hasAuthenticatedBearer = User.Identity?.IsAuthenticated == true;
        if (!hasRefreshCookie && !hasAuthenticatedBearer)
        {
            ConfirmCookieCleared();
            return NoContent();
        }

        var expectedSessionId = Request.Headers[AuthProtocol.SessionIdHeader].ToString();
        if (!Guid.TryParseExact(expectedSessionId, "N", out var parsedSessionId))
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Thiếu định danh phiên đăng xuất hợp lệ.",
                "Yêu cầu đăng xuất phải xác định đúng phiên của tab hiện tại."));

        var canonicalSessionId = parsedSessionId.ToString("N");
        var cookieLogoutResult = hasRefreshCookie
            ? await authService.LogoutAsync(token!, canonicalSessionId, cancellationToken)
            : RefreshCookieLogoutResult.TokenUnknown;
        var bearerMatchesExpectedSession = TryGetBearerSession(canonicalSessionId, out var bearerUserId);

        if (cookieLogoutResult == RefreshCookieLogoutResult.SessionMismatch)
        {
            if (!bearerMatchesExpectedSession)
            {
                return Conflict(CreateProblem(
                    StatusCodes.Status409Conflict,
                    "Cookie đang thuộc phiên khác.",
                    "Không thể thu hồi phiên cũ nếu access token không xác nhận đúng phiên của tab."));
            }

            await authService.LogoutCurrentSessionAsync(bearerUserId!, canonicalSessionId, cancellationToken);
            Response.Headers[AuthProtocol.SessionRevokedHeader] = "1";
            return NoContent();
        }

        var sessionRevoked = cookieLogoutResult == RefreshCookieLogoutResult.Revoked;
        if (!sessionRevoked && bearerMatchesExpectedSession)
        {
            await authService.LogoutCurrentSessionAsync(bearerUserId!, canonicalSessionId, cancellationToken);
            sessionRevoked = true;
        }

        ConfirmCookieCleared();
        if (!sessionRevoked)
        {
            return Unauthorized(CreateProblem(
                StatusCodes.Status401Unauthorized,
                "Không thể xác nhận thu hồi phiên.",
                "Cookie đã được xóa nhưng server không thể xác định phiên cần thu hồi."));
        }

        Response.Headers[AuthProtocol.SessionRevokedHeader] = "1";
        return NoContent();
    }

    [HttpPost("reject-session")]
    [Authorize]
    [AllowInactiveSession]
    public async Task<IActionResult> RejectCurrentSession(CancellationToken cancellationToken)
    {
        PreventAuthResponseCaching();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = User.FindFirstValue("sid");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
            return Unauthorized();

        await authService.RejectCurrentSessionAsync(userId, sessionId, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserSummary>> Me(CancellationToken cancellationToken)
    {
        PreventAuthResponseCaching();
        var user = await authService.GetCurrentUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    private void WriteRefreshCookie(AuthSession session) => Response.Cookies.Append(RefreshCookie, session.RefreshToken,
        new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Expires = session.RefreshExpiresAtUtc, Path = "/api/auth" });

    private void ExpireRefreshCookie() => Response.Cookies.Delete(RefreshCookie,
        new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/auth" });

    private void ConfirmCookieCleared()
    {
        ExpireRefreshCookie();
        Response.Headers[AuthProtocol.RefreshCookieClearedHeader] = "1";
    }

    private bool TryGetBearerSession(string expectedSessionId, out string? userId)
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var claimedSessionId = User.FindFirstValue("sid");
        return User.Identity?.IsAuthenticated == true &&
            !string.IsNullOrWhiteSpace(userId) &&
            Guid.TryParseExact(claimedSessionId, "N", out var parsedClaimedSessionId) &&
            string.Equals(parsedClaimedSessionId.ToString("N"), expectedSessionId, StringComparison.Ordinal);
    }

    private ProblemDetails CreateProblem(int status, string title, string detail)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return problem;
    }

    private void PreventAuthResponseCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }
}
