using System.IdentityModel.Tokens.Jwt;
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

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SUBankDbContext dbContext,
    IOptions<JwtOptions> options) : IAuthService
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
        return await CreateSessionAsync(user, Guid.NewGuid().ToString("N"), cancellationToken);
    }

    public async Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var current = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            ?? throw new AuthenticationException("Phiên đăng nhập không hợp lệ.");
        if (current.RevokedAtUtc is not null || current.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");

        var user = await userManager.FindByIdAsync(current.UserId);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản không thể tiếp tục phiên đăng nhập.");

        current.RevokedAtUtc = DateTimeOffset.UtcNow;
        var session = await CreateSessionAsync(user, current.SessionId, cancellationToken, false);
        var replacement = await dbContext.RefreshTokens.SingleAsync(x => x.TokenHash == Hash(session.RefreshToken), cancellationToken);
        current.ReplacedByTokenId = replacement.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (token is null || token.RevokedAtUtc is not null) return;
        token.RevokedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserSummary?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return null;
        return new UserSummary(user.UserName!, (await userManager.GetRolesAsync(user)).ToArray());
    }

    private async Task<AuthSession> CreateSessionAsync(ApplicationUser user, string sessionId, CancellationToken cancellationToken, bool auditLogin = true)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(jwt.AccessTokenMinutes);
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new("sid", sessionId)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshExpiry = now.AddDays(jwt.RefreshTokenDays);
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, SessionId = sessionId, TokenHash = Hash(rawRefreshToken),
            CreatedAtUtc = now, ExpiresAtUtc = refreshExpiry
        });
        if (auditLogin) dbContext.AuditLogs.Add(NewAudit(user.Id, "LOGIN_SUCCESS", AuditResult.Success));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSession(
            new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), accessExpiry, new UserSummary(user.UserName!, roles.ToArray())),
            rawRefreshToken, refreshExpiry);
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
