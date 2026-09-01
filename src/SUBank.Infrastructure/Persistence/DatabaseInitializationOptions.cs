namespace SUBank.Infrastructure.Persistence;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; init; }
    public bool SeedDemoData { get; init; }
    public string AllowedSeedDataSource { get; init; } = string.Empty;
    public string AllowedSeedDatabase { get; init; } = string.Empty;
}
