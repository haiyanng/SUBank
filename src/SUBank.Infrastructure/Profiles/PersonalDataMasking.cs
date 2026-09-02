namespace SUBank.Infrastructure.Profiles;

internal static class PersonalDataMasking
{
    public static string MaskIdentityCardNumber(string value)
    {
        if (value.Length <= 6)
            return new string('*', value.Length);

        return $"{value[..3]}{new string('*', value.Length - 6)}{value[^3..]}";
    }
}
