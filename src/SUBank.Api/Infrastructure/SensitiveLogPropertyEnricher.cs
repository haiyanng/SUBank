using Serilog.Core;
using Serilog.Events;

namespace SUBank.Api.Infrastructure;

public sealed class SensitiveLogPropertyEnricher : ILogEventEnricher
{
    private static readonly HashSet<string> PropertiesToRemove = new(StringComparer.OrdinalIgnoreCase)
    {
        "RequestPath",
        "QueryString",
        "RequestBody",
        "ResponseBody",
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "Password",
        "TransactionPassword",
        "AccessToken",
        "RefreshToken",
        "SigningKey",
        "ConnectionString",
        "ApiKey",
        "RawToken",
        "TokenHash",
        "SessionId",
        "RedisKey",
        "IdentityCardNumber",
        "AccountNumber",
        "Phone",
        "Email"
    };

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var propertyName in logEvent.Properties.Keys
                     .Where(PropertiesToRemove.Contains)
                     .ToArray())
        {
            logEvent.RemovePropertyIfPresent(propertyName);
        }
    }
}
