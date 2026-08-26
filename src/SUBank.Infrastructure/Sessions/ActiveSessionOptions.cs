namespace SUBank.Infrastructure.Sessions;

public sealed class ActiveSessionOptions
{
    public const string SectionName = "ActiveSession";
    public string RedisConnection { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = "subank:active-session:";
}
