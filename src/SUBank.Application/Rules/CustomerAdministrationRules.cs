using SUBank.Application.Exceptions;

namespace SUBank.Application.Rules;

public static class CustomerAdministrationRules
{
    public const int MaximumSearchLength = 100;
    public const int MinimumSuspensionReasonLength = 3;
    public const int MaximumSuspensionReasonLength = 500;

    public static string? NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var normalized = value.Trim();
        if (normalized.Length > MaximumSearchLength)
            throw new BusinessRuleException(
                $"Từ khóa tìm kiếm không vượt quá {MaximumSearchLength} ký tự.");

        return normalized;
    }

    public static string NormalizeSuspensionReason(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Length < MinimumSuspensionReasonLength ||
            normalized.Length > MaximumSuspensionReasonLength)
            throw new BusinessRuleException(
                $"Lý do khóa phải có từ {MinimumSuspensionReasonLength} đến {MaximumSuspensionReasonLength} ký tự.");

        return normalized;
    }
}
