using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using WingetIntune.Intune;
using WingetIntune.Testing;

namespace Svrooij.WinTuner.CmdLets.Commands.Testing;
/// <summary>
/// <para type="synopsis">Test if a package will install</para>
/// <para type="description">Use the Windows Sandbox to test if the provided installer arguments will make it install silently and if it exists with the expected exit code.</para>
/// </summary>
/// <psOrder>30</psOrder>
/// <example>
/// <para type="name">Test OhMyPosh version 22.0.3</para>
/// <para type="description">Test a packaged installer in sandbox</para>
/// <code>Test-WtIntuneWin -PackageFolder &quot;D:\packages\JanDeDobbeleer.OhMyPosh\22.0.3&quot;</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "WtIntuneWin", DefaultParameterSetName = nameof(PackageFolder), HelpUri = "https://wintuner.app/docs/wintuner-powershell/Test-WtIntuneWin/")]
public class TestWtIntuneWin : DependencyCmdlet<Startup>
{
    private const string ParameterSetWinGet = "WinGet";
    private const string ParameterSetIntuneWin = "IntuneWin";

    /// <summary>
    /// <para type="description">The package id to upload to Intune.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = ParameterSetWinGet,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "The package id to upload to Intune.")]
    public string? PackageId { get; set; }

    /// <summary>
    /// <para type="description">The version to upload to Intune</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        ParameterSetName = ParameterSetWinGet,
        ValueFromPipeline = false,
        HelpMessage = "The version to upload to Intune"
        )]
    public string? Version { get; set; }

    /// <summary>
    /// <para type="description">The Root folder where all the package live in.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 3,
        ParameterSetName = ParameterSetWinGet,
        ValueFromPipeline = false,
        HelpMessage = "The Root folder where all the package live in.")]
    public string? RootPackageFolder { get; set; }

    /// <summary>
    /// <para type="description">The folder where the package is</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = nameof(PackageFolder),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The folder where the package is")]
    public string? PackageFolder { get; set; }

    /// <summary>
    /// <para type="description">The IntuneWin file to test</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = ParameterSetIntuneWin,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The IntuneWin file to test")]
    public string? IntuneWinFile { get; set; }

    /// <summary>
    /// <para type="description">The installer filename (if not set correctly inside the intunewin)</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 2,
        ParameterSetName = ParameterSetIntuneWin,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The installer filename (if not set correctly inside the intunewin)")]
    public string? InstallerFilename { get; set; }

    /// <summary>
    /// <para type="description">The installer arguments (if you want it to execute silently)</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 3,
        ParameterSetName = ParameterSetIntuneWin,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The installer arguments (if you want it to execute silently)")]
    [Parameter(
        Mandatory = false,
        Position = 2,
        ParameterSetName = nameof(PackageFolder),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        DontShow = true,
        HelpMessage = "Override the installer arguments")]
    public string? InstallerArguments { get; set; }

    /// <summary>
    /// <para type="description">Clean the test files after run</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        ParameterSetName = nameof(PackageFolder),
        Position = 3,
        HelpMessage = "Clean the test files after run")]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ParameterSetWinGet,
        Position = 4,
        HelpMessage = "Clean the test files after run")]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ParameterSetIntuneWin,
        Position = 4,
        HelpMessage = "Clean the test files after run")]
    public SwitchParameter Clean { get; set; }

    /// <summary>
    /// <para type="description">Sleep for x seconds before closing</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        ParameterSetName = nameof(PackageFolder),
        Position = 4,
        HelpMessage = "Sleep for x seconds before auto shutdown")]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ParameterSetWinGet,
        Position = 5,
        HelpMessage = "Sleep for x seconds before auto shutdown")]
    [Parameter(
        Mandatory = false,
        ParameterSetName = ParameterSetIntuneWin,
        Position = 5,
        HelpMessage = "Sleep for x seconds before auto shutdown")]
    public int? Sleep { get; set; }

    [ServiceDependency]
    private ILogger<TestWtIntuneWin>? logger;

    [ServiceDependency]
    private WindowsSandbox? sandbox;

    [ServiceDependency]
    private MetadataManager? metadataManager;

    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (ParameterSetName == ParameterSetWinGet)
        {
            logger?.LogDebug("Loading package details from RootPackageFolder {RootPackageFolder}, PackageId {PackageId}, Version {Version}", RootPackageFolder, PackageId, Version);
            PackageFolder = Path.Combine(RootPackageFolder!, PackageId!, Version!);
            logger?.LogDebug("Loading package details from folder {PackageFolder}", PackageFolder);
        }

        if (PackageFolder is not null)
        {
            logger?.LogInformation("Loading package details from folder {PackageFolder}", PackageFolder);
            var packageInfo = await metadataManager!.LoadPackageInfoFromFolderAsync(PackageFolder, cancellationToken);
            InstallerFilename = packageInfo.InstallerFilename;
            // If the installer arguments are not set, use the ones from the package info.
            InstallerArguments ??= packageInfo.InstallCommandLine?.Replace($"\"{packageInfo.InstallerFilename!}\" ", "");
            IntuneWinFile = metadataManager.GetIntuneWinFileName(PackageFolder, packageInfo);
        }

        if (IntuneWinFile is null)
        {
            var ex = new ArgumentException("PackageFolder was provided");
            logger?.LogError(ex, "PackageFolder was provided");
            throw ex;
        }

        var sandboxFile = await sandbox!.PrepareSandboxFileForPackage(IntuneWinFile!, InstallerFilename, InstallerArguments, timeout: Sleep, cancellationToken: cancellationToken);
        logger?.LogDebug("Sandbox file created at {SandboxFile}", sandboxFile);
        var result = await sandbox.RunSandbox(sandboxFile, Clean, cancellationToken);
        if (result is null)
        {
            logger?.LogError("Sandbox exited with null result");
            return;
        }

        logger?.LogInformation("Installed {InstallerFilename} in sandbox, reported exitcode {ExitCode}, number of apps installed {AppsInstalled}", InstallerFilename, result.ExitCode, result?.InstalledApps?.Count());
        logger?.LogInformation("Sandbox result: {Result}", result);
    }
}
