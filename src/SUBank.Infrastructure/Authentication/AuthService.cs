using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
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
    IActiveSessionStore activeSessionStore, IRealtimeNotifier realtimeNotifier, IOptions<JwtOptions> options,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions jwt = options.Value;

    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !AuthenticationRules.HasValidLoginShape(request.UserName, request.Password))
            throw new AuthenticationException("Số điện thoại/tên đăng nhập hoặc mật khẩu không đúng.");

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
            EnsureIdentityUpdateSucceeded(await userManager.AccessFailedAsync(user));
            var wasLocked = await userManager.IsLockedOutAsync(user);
            if (wasLocked)
            {
                user.LockedAtUtc = DateTimeOffset.UtcNow;
                EnsureIdentityUpdateSucceeded(await userManager.UpdateAsync(user));
                dbContext.AuditLogs.Add(NewAudit(user.Id, "USER_LOCKED", AuditResult.Success));
            }
            if (wasLocked) await RevokeActiveSessionAfterLockAsync(user.Id, CancellationToken.None);
            await AuditAsync(user.Id, "LOGIN_FAILED", AuditResult.Failure, cancellationToken);
            throw new AuthenticationException("Số điện thoại/tên đăng nhập hoặc mật khẩu không đúng.");
        }

        EnsureIdentityUpdateSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        var session = await CreateSessionAsync(
            user, Guid.NewGuid().ToString("N"), cancellationToken, auditLogin: false, createHistory: true);
        try
        {
            var oldSessionId = await activeSessionStore.ReplaceAsync(
                user.Id, session.SessionId, session.RefreshExpiresAtUtc - DateTimeOffset.UtcNow, cancellationToken);
            if (!string.IsNullOrEmpty(oldSessionId) && oldSessionId != session.SessionId)
            {
                await RevokePersistedSessionAsync(user.Id, oldSessionId, "REPLACED", cancellationToken);
                await TryForceLogoutAsync(oldSessionId);
            }
            await AuditAsync(user.Id, "LOGIN_SUCCESS", AuditResult.Success, cancellationToken);
            return session;
        }
        catch
        {
            await CompensateFailedSessionActivationAsync(user.Id, session.SessionId);
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
            return await RejectRevokedRefreshTokenAsync(snapshot, now, transaction);
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
                return await RejectRevokedRefreshTokenAsync(latest, DateTimeOffset.UtcNow, transaction);
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

    public async Task<RefreshCookieLogoutResult> LogoutAsync(
        string refreshToken,
        string expectedSessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await LogoutCoreAsync(refreshToken, expectedSessionId, cancellationToken);
        }
        catch (Exception exception) when (IsSqlDeadlock(exception))
        {
            dbContext.ChangeTracker.Clear();
            try
            {
                return await LogoutCoreAsync(refreshToken, expectedSessionId, cancellationToken);
            }
            catch (Exception retryException) when (IsSqlDeadlock(retryException))
            {
                throw new ConflictException("Phiên đang được cập nhật đồng thời. Vui lòng thử đăng xuất lại.");
            }
        }
    }

    private async Task<RefreshCookieLogoutResult> LogoutCoreAsync(
        string refreshToken,
        string expectedSessionId,
        CancellationToken cancellationToken)
    {
        RefreshToken? token = null;
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);
            token = await dbContext.RefreshTokens.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
            if (token is null) return RefreshCookieLogoutResult.TokenUnknown;
            if (!string.Equals(token.SessionId, expectedSessionId, StringComparison.Ordinal))
                return RefreshCookieLogoutResult.SessionMismatch;

            await RevokePersistedSessionAsync(token.UserId, token.SessionId, "LOGOUT", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (ConflictException)
        {
            throw;
        }
        catch
        {
            if (token is not null)
                await TryRevokeActiveSessionAsync(token.UserId, token.SessionId);
            throw;
        }

        await TryRevokeActiveSessionAsync(token.UserId, token.SessionId);
        await TryForceLogoutAsync(token.SessionId);
        return RefreshCookieLogoutResult.Revoked;
    }

    public Task LogoutCurrentSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken) =>
        RevokeCurrentSessionAsync(userId, sessionId, "LOGOUT", cancellationToken);

    public Task RejectCurrentSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken) =>
        RevokeCurrentSessionAsync(userId, sessionId, "CLIENT_REJECTED", cancellationToken);

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
        var roles = await userManager.GetRolesAsync(user);
        var isCustomer = roles.Contains("Customer", StringComparer.Ordinal);
        var refreshExpiry = refreshExpiresAtUtc ?? (isCustomer
            ? now.AddMinutes(jwt.CustomerSessionMinutes)
            : now.AddDays(jwt.RefreshTokenDays));
        if (refreshExpiry <= now)
            throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
        var requestedAccessExpiry = isCustomer
            ? refreshExpiry
            : now.AddMinutes(jwt.AccessTokenMinutes);
        var accessExpiry = requestedAccessExpiry < refreshExpiry ? requestedAccessExpiry : refreshExpiry;
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
            UserId = user.Id,
            SessionId = sessionId,
            TokenHash = Hash(rawRefreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiry
        });
        if (createHistory)
            dbContext.UserSessions.Add(new UserSession
            {
                UserId = user.Id,
                SessionId = sessionId,
                CreatedAtUtc = now,
                ExpiresAtUtc = refreshExpiry
            });
        if (auditLogin) dbContext.AuditLogs.Add(NewAudit(user.Id, "LOGIN_SUCCESS", AuditResult.Success));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AuthSession(
            new AuthResponse(
                new JwtSecurityTokenHandler().WriteToken(token),
                accessExpiry,
                Math.Max(0, (long)(accessExpiry - DateTimeOffset.UtcNow).TotalMilliseconds),
                sessionId,
                new UserSummary(user.UserName!, roles.ToArray())),
            rawRefreshToken, refreshExpiry, sessionId);
    }

    private async Task<AuthSession> RejectRevokedRefreshTokenAsync(
        RefreshToken token,
        DateTimeOffset now,
        IDbContextTransaction transaction)
    {
        if (token.ReplacedByTokenId is not null)
        {
            var concurrencyGrace = TimeSpan.FromSeconds(jwt.RefreshConcurrencyGraceSeconds);
            if (token.RevokedAtUtc >= now - concurrencyGrace)
                throw new ConflictException("Phiên đang được làm mới bởi một yêu cầu khác. Vui lòng thử lại.");

            await RevokePersistedSessionAsync(
                token.UserId,
                token.SessionId,
                "REFRESH_REUSE",
                CancellationToken.None);
            await AuditAsync(
                token.UserId,
                "REFRESH_TOKEN_REUSE",
                AuditResult.Failure,
                CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
            await TryRevokeActiveSessionAsync(token.UserId, token.SessionId);
            await TryForceLogoutAsync(token.SessionId);
        }

        throw new AuthenticationException("Phiên đăng nhập đã hết hạn hoặc bị thu hồi.");
    }

    private async Task RevokePersistedSessionAsync(string userId, string sessionId, string reason,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null;
        var now = DateTimeOffset.UtcNow;
        var history = await LockSessionAsync(userId, sessionId, cancellationToken);
        if (history is not null)
        {
            history.RevokedAtUtc ??= now;
            if (ShouldReplaceRevocationReason(history.RevocationReason, reason))
                history.RevocationReason = reason;
        }

        await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.SessionId == sessionId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAtUtc, now), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private static bool ShouldReplaceRevocationReason(string? currentReason, string candidateReason) =>
        RevocationReasonPriority(candidateReason) > RevocationReasonPriority(currentReason);

    private static int RevocationReasonPriority(string? reason) => reason switch
    {
        "REFRESH_REUSE" => 400,
        "USER_LOCKED" => 350,
        "ACTIVATION_FAILED" => 300,
        "CLIENT_REJECTED" => 250,
        "REPLACED" => 200,
        "LOGOUT" => 100,
        null or "" => 0,
        _ => 50
    };

    private async Task RevokeActiveSessionAfterLockAsync(string userId, CancellationToken cancellationToken)
    {
        var persistedSessionIds = await dbContext.UserSessions.AsNoTracking()
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .OrderBy(x => x.Id)
            .Select(x => x.SessionId)
            .ToListAsync(cancellationToken);

        if (persistedSessionIds.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            foreach (var persistedSessionId in persistedSessionIds)
                await RevokePersistedSessionAsync(userId, persistedSessionId, "USER_LOCKED", cancellationToken);
        }

        var activeSessionId = await activeSessionStore.GetActiveSessionIdAsync(userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(activeSessionId)) return;

        if (!persistedSessionIds.Contains(activeSessionId, StringComparer.Ordinal))
            await RevokePersistedSessionAsync(userId, activeSessionId, "USER_LOCKED", cancellationToken);

        await activeSessionStore.RevokeAsync(userId, activeSessionId, cancellationToken);
        await TryForceLogoutAsync(activeSessionId);
    }

    private async Task CompensateFailedSessionActivationAsync(string userId, string sessionId)
    {
        try
        {
            await activeSessionStore.RevokeAsync(userId, sessionId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể dọn active session sau khi kích hoạt đăng nhập thất bại");
        }

        try
        {
            dbContext.ChangeTracker.Clear();
            await RevokePersistedSessionAsync(
                userId,
                sessionId,
                "ACTIVATION_FAILED",
                CancellationToken.None);
            await AuditAsync(
                userId,
                "SESSION_ACTIVATION_FAILED",
                AuditResult.Failure,
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể hoàn tất compensation SQL cho phiên đăng nhập thất bại");
        }
    }

    private async Task TryRevokeActiveSessionAsync(string userId, string sessionId)
    {
        try
        {
            await activeSessionStore.RevokeAsync(userId, sessionId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không thể thực hiện Redis revoke dự phòng sau lỗi SQL");
        }
    }

    private async Task RevokeCurrentSessionAsync(
        string userId,
        string sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await RevokePersistedSessionAsync(userId, sessionId, reason, cancellationToken);
        await TryRevokeActiveSessionAsync(userId, sessionId);
        await TryForceLogoutAsync(sessionId);
    }

    private async Task TryForceLogoutAsync(string sessionId)
    {
        try
        {
            await realtimeNotifier.ForceLogoutAsync(sessionId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Không thể gửi thông báo ForceLogout best-effort");
        }
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
    private static void EnsureIdentityUpdateSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new DependencyUnavailableException("Không thể cập nhật trạng thái xác thực của tài khoản.");
    }

    private static bool IsSqlDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 }) return true;
        }

        return false;
    }
}
