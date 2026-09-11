namespace SUBank.Application.Rules;

public static class AuthenticationRules
{
    public const int MaximumLoginNameLength = 256;
    public const int MaximumLoginPasswordLength = 256;

    public static bool HasValidLoginShape(string? loginName, string? password) =>
        !string.IsNullOrWhiteSpace(loginName) &&
        loginName.Length <= MaximumLoginNameLength &&
        !string.IsNullOrEmpty(password) &&
        password.Length <= MaximumLoginPasswordLength;
}
