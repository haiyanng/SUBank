namespace SUBank.Contracts.Auth;

public sealed record LoginRequest(string UserName, string Password);
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, UserSummary User);
public sealed record UserSummary(string UserName, IReadOnlyList<string> Roles);
