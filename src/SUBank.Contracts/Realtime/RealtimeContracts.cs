namespace SUBank.Contracts.Realtime;

public sealed record ForceLogoutNotification(string Reason);
public sealed record BalanceChangedNotification(string AccountNumber);
public sealed record TransactionReceivedNotification(string ReferenceNo, string AccountNumber);

public static class ForceLogoutReasons
{
    public const string SessionReplaced = "SESSION_REPLACED";
    public const string IdentityLockout = "IDENTITY_LOCKOUT";
    public const string AdminSuspension = "ADMIN_SUSPENSION";
    public const string SessionRevoked = "SESSION_REVOKED";
    public const string SecurityEvent = "SECURITY_EVENT";
}
