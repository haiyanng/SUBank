using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SUBank.Application.Abstractions;
using SUBank.Application.Exceptions;

namespace SUBank.Infrastructure.Sessions;

public sealed class RedisActiveSessionStore(IConnectionMultiplexer redis, IOptions<ActiveSessionOptions> options)
    : IActiveSessionStore
{
    private readonly string keyPrefix = options.Value.KeyPrefix;

    public async Task<string?> ReplaceAsync(string userId, string sessionId, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        const string script = "local old = redis.call('GET', KEYS[1]); redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2]); return old";
        try
        {
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                script,
                [Key(userId)],
                [sessionId, checked((long)lifetime.TotalMilliseconds)]).WaitAsync(cancellationToken);
            return result.IsNull ? null : result.ToString();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    public async Task<string?> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(Key(userId)).WaitAsync(cancellationToken);
            return value.HasValue ? value.ToString() : null;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    public async Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken cancellationToken) =>
        string.Equals(
            await GetActiveSessionIdAsync(userId, cancellationToken),
            sessionId,
            StringComparison.Ordinal);

    public async Task<bool> RenewAsync(
        string userId,
        string sessionId,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        if (lifetime <= TimeSpan.Zero) return false;

        const string script = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";
        try
        {
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                script,
                [Key(userId)],
                [sessionId, checked((long)lifetime.TotalMilliseconds)]).WaitAsync(cancellationToken);
            return (long)result == 1;
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    public async Task RevokeAsync(string userId, string sessionId, CancellationToken cancellationToken)
    {
        const string script = "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(script, [Key(userId)], [sessionId]).WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            throw Unavailable(exception);
        }
    }

    private RedisKey Key(string userId) => $"{keyPrefix}{userId}";

    private static DependencyUnavailableException Unavailable(Exception exception) =>
        new("Dịch vụ kiểm soát phiên tạm thời không khả dụng.", exception);
}
