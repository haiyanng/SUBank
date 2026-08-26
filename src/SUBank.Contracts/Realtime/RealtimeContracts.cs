namespace SUBank.Contracts.Realtime;

public sealed record BalanceChangedNotification(string AccountNumber);
public sealed record TransactionReceivedNotification(string ReferenceNo, string AccountNumber);
