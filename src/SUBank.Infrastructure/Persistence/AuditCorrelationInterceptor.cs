using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SUBank.Application.Abstractions;
using SUBank.Domain.Entities;

namespace SUBank.Infrastructure.Persistence;

public sealed class AuditCorrelationInterceptor(ICorrelationContext correlationContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyCorrelationId(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyCorrelationId(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyCorrelationId(DbContext? dbContext)
    {
        var correlationId = correlationContext.CorrelationId;
        if (dbContext is null || string.IsNullOrWhiteSpace(correlationId)) return;

        foreach (var entry in dbContext.ChangeTracker.Entries<AuditLog>()
                     .Where(entry => entry.State == EntityState.Added &&
                                     string.IsNullOrWhiteSpace(entry.Entity.CorrelationId)))
        {
            entry.Entity.CorrelationId = correlationId;
        }
    }
}
