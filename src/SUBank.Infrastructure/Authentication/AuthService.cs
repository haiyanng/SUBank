using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Auth;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Identity;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Authentication;

public sealed class AuthService(UserManager<ApplicationUser> userManager, SUBankDbContext dbContext,
    IActiveSessionStore activeSessionStore, IRealtimeNotifier realtimeNotifier, IOptions<JwtOptions> options) : IAuthService
{
    private readonly JwtOptions jwt = options.Value;

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByNameAsync(request.UserName.Trim());
        if (user is null || !user.IsActive)
            throw new AuthenticationException("Tên đăng nhập hoặc mật khẩu không đúng.");
        if (await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                user.LockedAtUtc = DateTimeOffset.UtcNow;
                await userManager.UpdateAsync(user);
                dbContext.AuditLogs.Add(NewAudit(user.Id, "USER_LOCKED", AuditResult.Success));
            }
            await AuditAsync(user.Id, "LOGIN_FAILED", AuditResult.Failure, cancellationToken);
            throw new AuthenticationException("Tên đăng nhập hoặc mật khẩu không đúng.");
        }

        await userManager.ResetAccessFailedCountAsync(user);
        var session = await CreateSessionAsync(
            user, Guid.NewGuid().ToString("N"), cancellationToken, auditLogin: false, createHistory: true);
        try
        {
            var oldSessionId = await activeSessionStore.ReplaceAsync(
                user.Id, session.SessionId, session.RefreshExpiresAtUtc - DateTimeOffset.UtcNow, cancellationToken);
            if (!string.IsNullOrEmpty(oldSessionId) && oldSessionId != session.SessionId)
            {
                await RevokePersistedSessionAsync(user.Id, oldSessionId, "REPLACED", cancellationToken);
                await realtimeNotifier.ForceLogoutAsync(oldSessionId, CancellationToken.None);
            }
            await AuditAsync(user.Id, "LOGIN_SUCCESS", AuditResult.Success, cancellationToken);
            return session;
        }
        catch (DependencyUnavailableException)
        {
            await RevokePersistedSessionAsync(user.Id, session.SessionId, "ACTIVATION_FAILED", cancellationToken);
            await AuditAsync(user.Id, "SESSION_ACTIVATION_FAILED", AuditResult.Failure, cancellationToken);
            throw;
        }
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var current = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken)
            ?? throw new AuthenticationException("Phiên đăng nhập không hợp lệ.");
        if (current.RevokedAtUtc is not null)
        {
            if (current.ReplacedByTokenId is not null)
            {
                await activeSessionStore.RevokeAsync(current.UserId, current.SessionId, cancellationToken);
                await RevokePersistedSessionAsync(current.UserId, current.SessionId, "REFRESH_REUSE", cancellationToken);
                await AuditAsync(current.UserId, "REFRESH_TOKEN_REUSE", AuditResult.Failure, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        }
        if (current.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        if (!await activeSessionStore.IsActiveAsync(current.UserId, current.SessionId, cancellationToken))
            throw new AuthenticationException("Phiên đăng nhập không còn hiệu lực.");

        var user = await userManager.FindByIdAsync(current.UserId);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản không thể tiếp tục phiên đăng nhập.");

        current.RevokedAtUtc = DateTimeOffset.UtcNow;
        var session = await CreateSessionAsync(user, current.SessionId, cancellationToken, auditLogin: false);
        var replacement = await dbContext.RefreshTokens.SingleAsync(x => x.TokenHash == Hash(session.RefreshToken), cancellationToken);
        current.ReplacedByTokenId = replacement.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (token is null || token.RevokedAtUtc is not null) return;

        await activeSessionStore.RevokeAsync(token.UserId, token.SessionId, cancellationToken);
        token.RevokedAtUtc = DateTimeOffset.UtcNow;
        var history = await dbContext.UserSessions.SingleOrDefaultAsync(
            x => x.UserId == token.UserId && x.SessionId == token.SessionId, cancellationToken);
        if (history is not null && history.RevokedAtUtc is null)
        {
            history.RevokedAtUtc = DateTimeOffset.UtcNow;
            history.RevocationReason = "LOGOUT";
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSummary?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : new UserSummary(user.UserName!, (await userManager.GetRolesAsync(user)).ToArray());
    }

    private async Task<AuthSession> CreateSessionAsync(ApplicationUser user, string sessionId,
        CancellationToken cancellationToken, bool auditLogin = true, bool createHistory = false)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(jwt.AccessTokenMinutes);
        var refreshExpiry = now.AddDays(jwt.RefreshTokenDays);
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Name, user.UserName!), new("sid", sessionId)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, SessionId = sessionId, TokenHash = Hash(rawRefreshToken),
            CreatedAtUtc = now, ExpiresAtUtc = refreshExpiry
        });
        if (createHistory)
            dbContext.UserSessions.Add(new UserSession
            {
                UserId = user.Id, SessionId = sessionId, CreatedAtUtc = now, ExpiresAtUtc = refreshExpiry
            });
        if (auditLogin) dbContext.AuditLogs.Add(NewAudit(user.Id, "LOGIN_SUCCESS", AuditResult.Success));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSession(
            new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), accessExpiry, new UserSummary(user.UserName!, roles.ToArray())),
            rawRefreshToken, refreshExpiry, sessionId);
    }

    private async Task RevokePersistedSessionAsync(string userId, string sessionId, string reason,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var history = await dbContext.UserSessions.SingleOrDefaultAsync(
            x => x.UserId == userId && x.SessionId == sessionId, cancellationToken);
        if (history is not null && history.RevokedAtUtc is null)
        {
            history.RevokedAtUtc = now;
            history.RevocationReason = reason;
        }
        var tokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.RevokedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AuditAsync(string userId, string action, AuditResult result, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(NewAudit(userId, action, result));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditLog NewAudit(string userId, string action, AuditResult result) =>
        new() { UserId = userId, Action = action, Result = result, CreatedAtUtc = DateTimeOffset.UtcNow };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
