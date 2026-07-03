namespace Winget.CommunityRepository.Models;

public partial class WingetInstallerManifest
{
    public string? PackageIdentifier { get; set; }
    public string? PackageVersion { get; set; }
    public string[]? Platform { get; set; }
    public string? MinimumOSVersion { get; set; }
    public WingetInstallerSwitches? InstallerSwitches { get; set; }
    public string? Scope { get; set; }
    public string? InstallerType { get; set; }
    public string? NestedInstallerType { get; set; }
    public List<WingetNestedInstallerFile>? NestedInstallerFiles { get; set; }
    public List<string>? InstallModes { get; set; }
    public string? UpgradeBehavior { get; set; }
    public List<string>? Commands { get; set; }
    public List<string>? FileExtensions { get; set; }
    public List<WingetInstaller>? Installers { get; set; }
    public string? ManifestType { get; set; }
    public string? ManifestVersion { get; set; }
    public string? ReleaseDate { get; set; }
}

public partial class WingetInstaller
{
    public string? InstallerLocale { get; set; }
    public string? Architecture { get; set; }
    public string? Scope { get; set; }
    public string? InstallerType { get; set; }
    public string? NestedInstallerType { get; set; }
    public List<WingetNestedInstallerFile>? NestedInstallerFiles { get; set; }
    public string? InstallerUrl { get; set; }
    public string? InstallerSha256 { get; set; }
    public WingetInstallerSwitches? InstallerSwitches { get; set; }
    public string? ProductCode { get; set; }
    public List<WingetAppsAndFeatures>? AppsAndFeaturesEntries { get; set; }
    public string? ElevationRequirement { get; set; }
    public string? InstallerFilename
    {
        get
        {
            if (InstallerUrl is null) { return null; }
            var uri = new Uri(InstallerUrl);
            return Path.GetFileName(uri.LocalPath.Replace(" ", ""));
        }
    }

    public override string ToString()
    {
        return $"{Architecture} {Scope} {InstallerType}";
    }
}

public partial class WingetInstallerSwitches
{
    public string? Silent { get; set; }
    public string? SilentWithProgress { get; set; }
    public string? Interactive { get; set; }
    public string? InstallLocation { get; set; }
    public string? Log { get; set; }
    public string? Upgrade { get; set; }
    public string? Custom { get; set; }

    public override string ToString()
    {
        return Custom ?? Silent ?? SilentWithProgress ?? nameof(WingetInstallerSwitches);
    }

    public string? GetPreferred()
    {
        var result = string.Join(' ', Custom, (Silent ?? SilentWithProgress)).Trim();
        return string.IsNullOrEmpty(result) ? null : result; // ?? Interactive
    }
}

public partial class WingetAppsAndFeatures
{
    public string? DisplayName { get; set; }
    public string? Publisher { get; set; }
    public string? DisplayVersion { get; set; }
    public string? ProductCode { get; set; }
}

public partial class WingetNestedInstallerFile
{
    public string? RelativeFilePath { get; set; }
    public string? PortableCommandAlias { get; set; }
}

