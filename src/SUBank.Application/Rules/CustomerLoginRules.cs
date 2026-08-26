namespace SUBank.Application.Rules;

public static class CustomerLoginRules
{
    public const int PhoneNumberLength = 10;

    public static bool IsCanonicalPhoneNumber(string? value) =>
        value is { Length: PhoneNumberLength } &&
        value[0] == '0' &&
        value.All(character => character is >= '0' and <= '9');
}
