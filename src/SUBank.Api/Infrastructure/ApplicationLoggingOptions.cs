namespace SUBank.Api.Infrastructure;

public sealed class ApplicationLoggingOptions
{
    public const string SectionName = "ApplicationLogging";

    public bool FileEnabled { get; init; }
    public string Directory { get; init; } = "logs";
    public long FileSizeLimitBytes { get; init; } = 10 * 1024 * 1024;
    public int RetainedFileCountLimit { get; init; } = 31;
    public int RetainedDays { get; init; } = 14;

    public string ValidateAndResolveDirectory(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(Directory) || Path.IsPathRooted(Directory))
            throw new InvalidOperationException(
                $"{SectionName}:Directory must be a relative directory inside the API content root.");
        if (FileSizeLimitBytes is < 1_048_576 or > 104_857_600)
            throw new InvalidOperationException(
                $"{SectionName}:FileSizeLimitBytes must be between 1 MB and 100 MB.");
        if (RetainedFileCountLimit is < 1 or > 200)
            throw new InvalidOperationException(
                $"{SectionName}:RetainedFileCountLimit must be between 1 and 200.");
        if (RetainedDays is < 1 or > 90)
            throw new InvalidOperationException(
                $"{SectionName}:RetainedDays must be between 1 and 90.");

        var root = Path.GetFullPath(contentRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, Directory));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!resolved.StartsWith(root, comparison))
            throw new InvalidOperationException(
                $"{SectionName}:Directory must stay inside the API content root.");

        return resolved;
    }
}
