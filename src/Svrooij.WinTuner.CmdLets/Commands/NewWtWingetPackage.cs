using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Svrooij.WinTuner.Proxy.Client;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;
/// <summary>
/// <para type="synopsis">Create intunewin file from Winget installer</para>
/// <para type="description">Downloads the installer for the package and creates an `.intunewin` file for uploading in Intune.</para>
/// </summary>
/// <psOrder>10</psOrder>
/// <example>
/// <para type="name">Package winget installer</para>
/// <para type="description">Package the latest version of `JanDeDobbeleer.OhMyPosh` to `C:\tools\packages`. The package will be in `C:\tools\packages\{packageId}\{version}`</para>
/// <code>New-WtWingetPackage -PackageId JanDeDobbeleer.OhMyPosh -PackageFolder C:\Tools\Packages</code>
/// </example>
[Cmdlet(VerbsCommon.New, "WtWingetPackage", HelpUri = "https://wintuner.app/docs/wintuner-powershell/New-WtWingetPackage")]
[OutputType(typeof(WingetIntune.Models.WingetPackage))]
[GenerateBindings]
public partial class NewWtWingetPackage : DependencyCmdlet<Startup>
{
    /// <summary>
    /// Package id to download
    /// </summary>
    [Parameter(
               Mandatory = true,
               Position = 0,
               ValueFromPipeline = true,
               ValueFromPipelineByPropertyName = true,
               HelpMessage = "The package id to download")]
    public string? PackageId { get; set; }

    /// <summary>
    /// The folder to store the package in
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The folder to store the package in")]
    public string PackageFolder { get; set; }

    /// <summary>
    /// The version to download (optional)
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 2,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The version to download (optional)")]
    public string? Version { get; set; }

    /// <summary>
    /// The folder to store temporary files in
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 3,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The folder to store temporary files in")]
    public string? TempFolder { get; set; } = Path.Combine(Path.GetTempPath(), "wintuner");

    /// <summary>
    /// Pick this architecture
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 4,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Pick this architecture")]
    public WingetIntune.Models.Architecture Architecture { get; set; } = WingetIntune.Models.Architecture.Unknown;

    /// <summary>
    /// The installer context
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 5,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The installer context")]
    public WingetIntune.Models.InstallerContext InstallerContext { get; set; } = WingetIntune.Models.InstallerContext.System;

    /// <summary>
    /// Package as script
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 6,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Package WinGet script, instead of the actual installer. Helpful for installers that don't really work with WinTuner.")]
    public SwitchParameter PackageScript { get; set; }

    /// <summary>
    /// Desired locale
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 7,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The desired locale, if available (eg. 'en-US')")]
    public string? Locale { get; set; }

    /// <summary>
    /// Override the installer arguments
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 8,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Override the installer arguments")]
    public string? InstallerArguments { get; set; }

    /// <summary>
    /// Prefered installer type, (default: Msi)
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 9,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Prefered installer type")]
    public WingetIntune.Models.InstallerType PreferedInstaller { get; set; } = WingetIntune.Models.InstallerType.Msi;

    /// <summary>
    /// MSI Product code (optional), in case the installer is an MSI with the wrong product code in winget.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 10,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "MSI Product code")]
    public string? MsiProductCode { get; set; }

    /// <summary>
    /// MSI Product version (optional), in case the installer is an MSI with the wrong msi version in winget.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 11,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "MSI Version")]
    public string? MsiVersion { get; set; }

    /// <summary>
    /// Creating a partial package means that the files are not zipped into the intunewin file, but are left as is.
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 15,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        DontShow = true, // this is still experimental
        HelpMessage = "Creating a partial package means that the files are not zipped into the intunewin file, but are left as is.")]
    public SwitchParameter PartialPackage { get; set; }

    [ServiceDependency]
    private ILogger<NewWtWingetPackage>? logger;

    [ServiceDependency(Required = true)]
    private Winget.CommunityRepository.WingetRepository wingetRepository;

    [ServiceDependency(Required = true)]
    private WingetIntune.IWingetRepository repository;

    [ServiceDependency(Required = true)]
    private WingetIntune.IntuneManager intuneManager;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? proxyClient;

    private bool versionless = false;

    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        // Fix the package id casing.
        PackageId = (await wingetRepository!.GetPackageId(PackageId!, cancellationToken)) ?? PackageId;
        if (string.IsNullOrEmpty(PackageId))
        {
            logger?.LogWarning("Package {PackageId} not found", PackageId);
            return;
        }

        if (string.IsNullOrEmpty(Version))
        {
            Version = await wingetRepository.GetLatestVersion(PackageId!, cancellationToken);
        }
        else if (Version.Equals("latest", System.StringComparison.OrdinalIgnoreCase) == true)
        {
            Version = await wingetRepository.GetLatestVersion(PackageId!, cancellationToken);
            versionless = PackageScript;
        }

        logger?.LogInformation("Packaging package {PackageId} {Version}", PackageId, Version);
        var command = nameof(NewWtWingetPackage);
        if (versionless)
        {
            command = $"{command}vl";
        }
        proxyClient?.TriggerEvent(ConnectWtWinTuner.SessionId, command, appVersion: ConnectWtWinTuner.AppVersion, packageId: PackageId, cancellationToken: CancellationToken.None);
        var packageInfo = await repository.GetPackageInfoAsync(PackageId!, Version, source: "winget", cancellationToken: cancellationToken);

        if (packageInfo != null)
        {
            logger?.LogDebug("Package {PackageId} {Version} from {Source}", packageInfo.PackageIdentifier, packageInfo.Version, packageInfo.Source);

            var package = await intuneManager.GenerateInstallerPackage(
                TempFolder!,
                PackageFolder,
                packageInfo,
                new WingetIntune.Models.PackageOptions
                {
                    Architecture = Architecture,
                    InstallerContext = InstallerContext,
                    PackageScript = PackageScript,
                    Locale = Locale,
                    OverrideArguments = InstallerArguments,
                    InstallerType = PreferedInstaller,
                    PartialPackage = PartialPackage,
                    MsiProductCode = MsiProductCode,
                    MsiVersion = MsiVersion,
                    Versionless = versionless
                },
                cancellationToken: cancellationToken);

            WriteObject(package);
        }
        else
        {
            logger?.LogWarning("Package {PackageId} not found", PackageId);
        }
    }
}
