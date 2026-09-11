namespace SUBank.Api.Infrastructure;

public sealed class DeploymentSecurityOptions
{
    public const string SectionName = "DeploymentSecurity";

    public bool UseForwardedHeaders { get; init; }
    public string[] KnownProxies { get; init; } = [];
}
