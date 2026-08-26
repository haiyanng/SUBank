using SUBank.Application.Exceptions;

namespace SUBank.Application.Rules;

public static class BankingRules
{
    public const int MaximumIdempotencyKeyLength = 64;
    public const int MaximumDescriptionLength = 280;

    public static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
            throw new BusinessRuleException("Số tiền phải lớn hơn 0 và có tối đa 2 chữ số thập phân.");
    }

    public static void ValidateIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdempotencyKeyLength)
            throw new BusinessRuleException($"Idempotency-Key là bắt buộc và không vượt quá {MaximumIdempotencyKeyLength} ký tự.");
    }

    public static string? NormalizeDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > MaximumDescriptionLength)
            throw new BusinessRuleException($"Nội dung giao dịch không vượt quá {MaximumDescriptionLength} ký tự.");
        return normalized;
    }
}
