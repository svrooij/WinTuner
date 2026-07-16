namespace WingetIntune.Graph;

public enum GraphCloudEnvironment
{
    Global,
    USGov,
    USGovDoD,
    China,
}

public sealed record GraphCloudEndpoint(
    GraphCloudEnvironment Environment,
    string DisplayName,
    Uri AuthorityHost,
    string GraphHost,
    string GraphRoot)
{
    public string GraphBetaEndpoint => $"{GraphRoot}/beta";

    public string DefaultClientCredentialScope => $"{GraphRoot}/.default";

    public string TestEndpoint => $"{GraphRoot}/test";

    public string[] AllowedHosts => new[] { GraphHost };

    public string GetAuthority(string? tenantId = null)
    {
        var tenant = string.IsNullOrWhiteSpace(tenantId) ? "organizations" : tenantId;
        return $"{AuthorityHost.ToString().TrimEnd('/')}/{tenant}";
    }
}

public static class GraphCloudEnvironments
{
    public static GraphCloudEndpoint Global { get; } = new(
        GraphCloudEnvironment.Global,
        "Global",
        new Uri("https://login.microsoftonline.com"),
        "graph.microsoft.com",
        "https://graph.microsoft.com");

    public static GraphCloudEndpoint USGov { get; } = new(
        GraphCloudEnvironment.USGov,
        "US Government L4 / GCC High",
        new Uri("https://login.microsoftonline.us"),
        "graph.microsoft.us",
        "https://graph.microsoft.us");

    public static GraphCloudEndpoint USGovDoD { get; } = new(
        GraphCloudEnvironment.USGovDoD,
        "US Government L5 / DoD",
        new Uri("https://login.microsoftonline.us"),
        "dod-graph.microsoft.us",
        "https://dod-graph.microsoft.us");

    public static GraphCloudEndpoint China { get; } = new(
        GraphCloudEnvironment.China,
        "China operated by 21Vianet",
        new Uri("https://login.chinacloudapi.cn"),
        "microsoftgraph.chinacloudapi.cn",
        "https://microsoftgraph.chinacloudapi.cn");

    public static GraphCloudEndpoint GetEndpoint(GraphCloudEnvironment environment) => environment switch
    {
        GraphCloudEnvironment.Global => Global,
        GraphCloudEnvironment.USGov => USGov,
        GraphCloudEnvironment.USGovDoD => USGovDoD,
        GraphCloudEnvironment.China => China,
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null),
    };

    public static GraphCloudEndpoint GetEndpoint(string? environment) => GetEndpoint(Parse(environment));

    public static GraphCloudEnvironment Parse(string? environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            return GraphCloudEnvironment.Global;
        }

        return Normalize(environment) switch
        {
            "GLOBAL" or "PUBLIC" or "AZUREPUBLIC" or "AZUREPUBLICCLOUD" => GraphCloudEnvironment.Global,
            "USGOV" or "USGOVERNMENT" or "GCC HIGH" or "GCCHIGH" or "GCCH" => GraphCloudEnvironment.USGov,
            "USGOVDOD" or "DOD" or "USGOVERNMENTDOD" => GraphCloudEnvironment.USGovDoD,
            "CHINA" or "AZURECHINA" or "MOONCAKE" => GraphCloudEnvironment.China,
            _ => throw new ArgumentException($"Unknown Graph cloud environment '{environment}'.", nameof(environment)),
        };
    }

    private static string Normalize(string value) =>
        value.Trim().Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .ToUpperInvariant();
}
