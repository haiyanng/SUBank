using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SUBank.Application.Exceptions;
using SUBank.Domain.Entities;
using SUBank.Domain.Enums;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Infrastructure.Banking;

internal static class IdempotencyReplay
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 }) return true;
        }

        return false;
    }

    public static async Task<FinancialTransaction?> FindMatchingAsync(
        SUBankDbContext dbContext,
        string actorUserId,
        string idempotencyKey,
        string requestHash,
        TransactionType expectedType,
        CancellationToken cancellationToken)
    {
        var replay = await dbContext.FinancialTransactions
            .AsNoTracking()
            .Include(x => x.SourceAccount)
            .Include(x => x.DestinationAccount)
            .SingleOrDefaultAsync(
                x => x.CreatedByUserId == actorUserId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (replay is not null &&
            (replay.Type != expectedType ||
             !string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal)))
        {
            throw new ConflictException("Idempotency-Key đã được dùng cho một yêu cầu khác.");
        }

        return replay;
    }
}
