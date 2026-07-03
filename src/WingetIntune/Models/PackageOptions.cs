namespace WingetIntune.Models;

public class PackageOptions
{
    public InstallerType InstallerType { get; init; } = InstallerType.Msi;
    public InstallerContext InstallerContext { get; init; }
    public Architecture Architecture { get; init; }
    public bool PackageScript { get; init; }
    public string? Locale { get; init; }
    public string? OverrideArguments { get; init; }
    public bool PartialPackage { get; init; }
    public bool PrepareOnly { get; init; }
    public string? MsiProductCode { get; init; }
    public string? MsiVersion { get; init; }
    public bool Versionless { get; set; } = false;
    public static PackageOptions Create() => new PackageOptions { Architecture = Architecture.X64, InstallerContext = InstallerContext.System, PackageScript = false };
}
