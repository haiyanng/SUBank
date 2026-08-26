namespace SUBank.Contracts.Qr;

public sealed record GenerateQrRequest(string AccountNumber, decimal? Amount, string? Message);
public sealed record GeneratedQr(string Payload, string PngBase64);
public sealed record QrTransferData(string AccountNumber, decimal? Amount, string? Message);
