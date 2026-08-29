using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Application.Rules;
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
        var loginName = request.UserName.Trim();
        var user = await userManager.FindByNameAsync(loginName);
        if (user is null || !user.IsActive)
            throw new AuthenticationException("Số điện thoại/tên đăng nhập hoặc mật khẩu không đúng.");
        if (await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.");
        if (!await LoginNameMatchesCustomerProfileAsync(user, loginName, cancellationToken))
            throw new AuthenticationException("Số điện thoại/tên đăng nhập hoặc mật khẩu không đúng.");

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
            throw new AuthenticationException("Số điện thoại/tên đăng nhập hoặc mật khẩu không đúng.");
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
        try
        {
            return await RefreshCoreAsync(refreshToken, cancellationToken);
        }
        catch (Exception exception) when (IsSqlDeadlock(exception))
        {
            throw new ConflictException("Phiên đang được làm mới bởi một yêu cầu khác. Vui lòng thử lại.");
        }
    }

    private async Task<AuthSession> RefreshCoreAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var tokenHash = Hash(refreshToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var snapshot = await dbContext.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new AuthenticationException("Phiên đăng nhập không hợp lệ.");

        var history = await LockSessionAsync(snapshot.UserId, snapshot.SessionId, cancellationToken);
        if (history is null || history.RevokedAtUtc is not null || history.ExpiresAtUtc <= now)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        if (snapshot.RevokedAtUtc is not null)
            return await RejectRevokedRefreshTokenAsync(snapshot, now, transaction, cancellationToken);
        if (snapshot.ExpiresAtUtc <= now)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");

        if (!await activeSessionStore.IsActiveAsync(snapshot.UserId, snapshot.SessionId, cancellationToken))
            throw new AuthenticationException("Phiên đăng nhập không còn hiệu lực.");

        var user = await userManager.FindByIdAsync(snapshot.UserId);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            throw new AuthenticationException("Tài khoản không thể tiếp tục phiên đăng nhập.");

        var claimed = await dbContext.RefreshTokens
            .Where(x => x.Id == snapshot.Id && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.RevokedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            dbContext.ChangeTracker.Clear();
            var latest = await dbContext.RefreshTokens.AsNoTracking()
                .SingleAsync(x => x.Id == snapshot.Id, cancellationToken);
            if (latest.RevokedAtUtc is not null)
                return await RejectRevokedRefreshTokenAsync(latest, DateTimeOffset.UtcNow, transaction, cancellationToken);
            if (latest.ExpiresAtUtc <= DateTimeOffset.UtcNow)
                throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
            throw new ConflictException("Phiên đang được làm mới bởi một yêu cầu khác. Vui lòng thử lại.");
        }

        var current = await dbContext.RefreshTokens.SingleAsync(x => x.Id == snapshot.Id, cancellationToken);
        var session = await CreateSessionAsync(
            user,
            current.SessionId,
            cancellationToken,
            auditLogin: false,
            refreshExpiresAtUtc: history.ExpiresAtUtc);
        var replacement = await dbContext.RefreshTokens.SingleAsync(
            x => x.TokenHash == Hash(session.RefreshToken), cancellationToken);
        current.ReplacedByTokenId = replacement.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var remainingLifetime = history.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (remainingLifetime <= TimeSpan.Zero ||
            !await activeSessionStore.RenewAsync(
                current.UserId, current.SessionId, remainingLifetime, cancellationToken))
            throw new AuthenticationException("Phiên đăng nhập không còn hiệu lực.");

        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            await LogoutCoreAsync(refreshToken, cancellationToken);
        }
        catch (Exception exception) when (IsSqlDeadlock(exception))
        {
            dbContext.ChangeTracker.Clear();
            try
            {
                await LogoutCoreAsync(refreshToken, cancellationToken);
            }
            catch (Exception retryException) when (IsSqlDeadlock(retryException))
            {
                throw new ConflictException("Phiên đang được cập nhật đồng thời. Vui lòng thử đăng xuất lại.");
            }
        }
    }

    private async Task LogoutCoreAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var token = await dbContext.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (token is null) return;

        var history = await LockSessionAsync(token.UserId, token.SessionId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        if (history is not null && history.RevokedAtUtc is null)
        {
            history.RevokedAtUtc = now;
            history.RevocationReason = "LOGOUT";
        }

        await dbContext.RefreshTokens
            .Where(x => x.UserId == token.UserId &&
                        x.SessionId == token.SessionId &&
                        x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activeSessionStore.RevokeAsync(token.UserId, token.SessionId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<UserSummary?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : new UserSummary(user.UserName!, (await userManager.GetRolesAsync(user)).ToArray());
    }

    private async Task<AuthSession> CreateSessionAsync(
        ApplicationUser user,
        string sessionId,
        CancellationToken cancellationToken,
        bool auditLogin = true,
        bool createHistory = false,
        DateTimeOffset? refreshExpiresAtUtc = null)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshExpiry = refreshExpiresAtUtc ?? now.AddDays(jwt.RefreshTokenDays);
        if (refreshExpiry <= now)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        var requestedAccessExpiry = now.AddMinutes(jwt.AccessTokenMinutes);
        var accessExpiry = requestedAccessExpiry < refreshExpiry ? requestedAccessExpiry : refreshExpiry;
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

    private async Task<AuthSession> RejectRevokedRefreshTokenAsync(
        RefreshToken token,
        DateTimeOffset now,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        if (token.ReplacedByTokenId is not null)
        {
            var concurrencyGrace = TimeSpan.FromSeconds(jwt.RefreshConcurrencyGraceSeconds);
            if (token.RevokedAtUtc >= now - concurrencyGrace)
                throw new ConflictException("Phiên đang được làm mới bởi một yêu cầu khác. Vui lòng thử lại.");

            await activeSessionStore.RevokeAsync(token.UserId, token.SessionId, cancellationToken);
            await RevokePersistedSessionAsync(token.UserId, token.SessionId, "REFRESH_REUSE", cancellationToken);
            await AuditAsync(token.UserId, "REFRESH_TOKEN_REUSE", AuditResult.Failure, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
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

    private Task<UserSession?> LockSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions
            .FromSqlInterpolated(
                $"SELECT * FROM [UserSessions] WITH (UPDLOCK, ROWLOCK) WHERE [UserId] = {userId} AND [SessionId] = {sessionId}")
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> LoginNameMatchesCustomerProfileAsync(ApplicationUser user, string loginName,
        CancellationToken cancellationToken)
    {
        if (!await userManager.IsInRoleAsync(user, "Customer")) return true;
        if (!CustomerLoginRules.IsCanonicalPhoneNumber(loginName) ||
            !string.Equals(user.UserName, loginName, StringComparison.Ordinal)) return false;

        return await dbContext.CustomerProfiles.AsNoTracking()
            .AnyAsync(x => x.UserId == user.Id && x.Phone == loginName, cancellationToken);
    }

    private async Task AuditAsync(string userId, string action, AuditResult result, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(NewAudit(userId, action, result));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditLog NewAudit(string userId, string action, AuditResult result) =>
        new() { UserId = userId, Action = action, Result = result, CreatedAtUtc = DateTimeOffset.UtcNow };
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool IsSqlDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 }) return true;
        }

        return false;
    }
}
