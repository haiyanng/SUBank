using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SUBank.Infrastructure.Persistence;

namespace SUBank.Api.HealthChecks;

public sealed class SqlServerHealthCheck(SUBankDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("SQL Server is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check timed out.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server is unavailable.", exception);
        }
    }
}
