namespace SUBank.Contracts.Accounts;

public sealed record AccountSummary(string AccountNumber, decimal Balance, string Currency, string Status);
public sealed record AccountDetail(string AccountNumber, decimal Balance, string Currency, string Status, string CustomerName);
public sealed record ResolvedAccount(string AccountNumber, string DisplayName, string Status);
