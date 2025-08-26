using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using WingetIntune.Testing;

namespace Svrooij.WinTuner.CmdLets.Commands.Testing;
/// <summary>
/// <para type="synopsis">Test your silent install switches</para>
/// <para type="description">Test if a setup will install on the Windows Sandbox</para>
/// <para type="link" uri="https://wintuner.app/docs/wintuner-powershell/Test-WtSetupFile">Documentation</para>
/// </summary>
/// <psOrder>31</psOrder>
/// <example>
/// <para type="description">Test any installer in sandbox</para>
/// <code>Test-WtSetupFile -SetupFile D:\packages\xyz.exe -Installer "all your arguments"</code>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "WtSetupFile", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Test-WtSetupFile/")]
[GenerateBindings]
public class TestWtSetupFile : DependencyCmdlet<Startup>
{
    [ServiceDependency]
    private ILogger<TestWtSetupFile>? logger;

    [ServiceDependency]
    private WindowsSandbox? sandbox;

    /// <summary>
    /// <para type="description">Override the installer arguments</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 1,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Override the installer arguments")]
    public string? InstallerArguments { get; set; }

    /// <summary>
    /// <para type="description">The absolute path to your setup file</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Absolute path to your setup file")]
    public string? SetupFile { get; set; }
    /// <summary>
    /// <para type="description">Sleep for x seconds before closing</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        HelpMessage = "Sleep for x seconds before auto shutdown")]
    public int? Sleep { get; set; }
    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        var sandboxFile = await sandbox!.PrepareSandboxForInstaller(SetupFile!, InstallerArguments, Sleep, cancellationToken);
        logger?.LogDebug("Sandbox file created at {SandboxFile}", sandboxFile);
        var result = await sandbox.RunSandbox(sandboxFile, true, cancellationToken);
        if (result is null)
        {
            logger?.LogError("Sandbox exited with null result");
            return;
        }

        logger?.LogInformation("Installed {InstallerFilename} in sandbox, reported exitcode {ExitCode}, number of apps installed {AppsInstalled}", Path.GetFileName(SetupFile), result.ExitCode, result?.InstalledApps?.Count());
        logger?.LogInformation("Sandbox result: {Result}", result);
    }
}
