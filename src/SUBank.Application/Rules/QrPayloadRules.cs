using System.Globalization;
using SUBank.Application.Exceptions;
using SUBank.Contracts.Qr;

namespace SUBank.Application.Rules;

public static class QrPayloadRules
{
    public const int MaxImageBytes = 5 * 1024 * 1024;

    public static string Create(string accountNumber, decimal? amount, string? message)
    {
        ValidateAccount(accountNumber);
        if (amount is not null) BankingRules.ValidateAmount(amount.Value);
        var normalizedMessage = BankingRules.NormalizeDescription(message);
        return $"subank://transfer?v=1&account={accountNumber}" +
               (amount is null ? "" : $"&amount={amount.Value.ToString("0", CultureInfo.InvariantCulture)}") +
               (normalizedMessage is null ? "" : $"&message={Uri.EscapeDataString(normalizedMessage)}");
    }

    public static QrTransferData Parse(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 1_000)
            throw new BusinessRuleException("QR payload không hợp lệ.");
        if (!Uri.TryCreate(payload, UriKind.Absolute, out var uri) || uri.Scheme != "subank" || uri.Host != "transfer")
            throw new BusinessRuleException("Đây không phải SUBank QR.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2 || !values.TryAdd(Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1])))
                throw new BusinessRuleException("QR payload không hợp lệ.");
        }
        if (!values.TryGetValue("v", out var version) || version != "1" || !values.TryGetValue("account", out var account))
            throw new BusinessRuleException("Phiên bản hoặc tài khoản trong QR không hợp lệ.");
        ValidateAccount(account);
        decimal? amount = null;
        if (values.TryGetValue("amount", out var amountText))
        {
            if (!decimal.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                throw new BusinessRuleException("Số tiền trong QR không hợp lệ.");
            BankingRules.ValidateAmount(parsed);
            amount = parsed;
        }
        var message = values.TryGetValue("message", out var text) ? BankingRules.NormalizeDescription(text) : null;
        return new QrTransferData(account, amount, message);
    }

    private static void ValidateAccount(string accountNumber)
    {
        if (accountNumber.Length != 10 || accountNumber.Any(x => !char.IsAsciiDigit(x)))
            throw new BusinessRuleException("Số tài khoản trong QR không hợp lệ.");
    }
}
