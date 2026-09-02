using Microsoft.EntityFrameworkCore;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Sessions;

public sealed class ActiveSessionValidator(
    IActiveSessionStore activeSessionStore,
    SUBankDbContext dbContext) : IActiveSessionValidator
{
    public async Task<bool> IsValidAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await activeSessionStore.IsActiveAsync(userId, sessionId, cancellationToken)) return false;

            var now = DateTimeOffset.UtcNow;
            var isDurablyValid = await (
                from session in dbContext.UserSessions.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
                where session.UserId == userId &&
                      session.SessionId == sessionId &&
                      session.RevokedAtUtc == null &&
                      session.ExpiresAtUtc > now &&
                      user.IsActive &&
                      !user.IsAdminSuspended &&
                      (user.LockoutEnd == null || user.LockoutEnd <= now)
                select session.Id)
                .AnyAsync(cancellationToken);

            if (!isDurablyValid)
            {
                try
                {
                    await activeSessionStore.RevokeAsync(userId, sessionId, CancellationToken.None);
                }
                catch (DependencyUnavailableException)
                {
                    // Durable state đã đủ để từ chối request; Redis sẽ được dọn ở lần kiểm tra sau.
                }
            }

            return isDurablyValid;
        }
        catch (DependencyUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DependencyUnavailableException(
                "Dịch vụ kiểm soát phiên tạm thời không khả dụng.",
                exception);
        }
    }
}
