namespace SUBank.Contracts.Auth;

public static class AuthProtocol
{
    public const string CsrfHeader = "X-SUBank-CSRF";
    public const string SessionIdHeader = "X-SUBank-Session-ID";
    public const string RefreshCookieClearedHeader = "X-SUBank-Refresh-Cookie-Cleared";
    public const string SessionRevokedHeader = "X-SUBank-Session-Revoked";
}
