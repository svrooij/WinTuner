using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Create a new IntuneWin package</para>
/// <para type="description">This is a re-implementation of the IntuneWinAppUtil.exe tool, it's not feature complete use at your own risk.</para>
/// </summary>
/// <psOrder>100</psOrder>
/// <example>
/// <para type="description">Package all files in `C:\Temp\Source`, with setup file `..\setup.exe` to the specified folder</para>
/// <code>New-IntuneWinPackage -SourcePath C:\Temp\Source -SetupFile C:\Temp\Source\setup.exe -DestinationPath C:\Temp\Destination</code>
/// </example>
[Cmdlet(VerbsCommon.New, "IntuneWinPackage", HelpUri = "https://wintuner.app/docs/wintuner-powershell/contentprep/New-IntuneWinPackage")]
[OutputType(typeof(string))]
[GenerateBindings]
public partial class NewIntuneWinPackage : DependencyCmdlet<Startup>
{
    /// <summary>
    /// <para type="description">Directory containing all the installation files</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The directory containing all the installation files")]
    public string? SourcePath { get; set; }

    /// <summary>
    /// <para type="description">The main setupfile in the source directory</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The main setupfile in the source directory")]
    public string? SetupFile { get; set; }

    /// <summary>
    /// <para type="description">Destination folder</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 2,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Destination folder, should be outside the source folder")]
    public string? DestinationPath { get; set; }

    /// <summary>
    /// <para type="description">Create partial package</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 3,
        DontShow = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Create partial package, this will not zip the final IntuneWin. So it does not need extracting before uploading.")]
    public SwitchParameter PartialPackage { get; set; }

    [ServiceDependency]
    private ILogger<NewIntuneWinPackage>? _logger;

    [ServiceDependency]
    private WingetIntune.Interfaces.IIntunePackager? _packager;
    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var setupFile = Path.Combine(SourcePath!, SetupFile!);
            if (!Directory.Exists(DestinationPath!))
            {
                _logger?.LogInformation("Creating destination folder {DestinationPath}", DestinationPath);
                Directory.CreateDirectory(DestinationPath!);
            }
            _logger?.LogInformation("Creating package for {setupFile}", setupFile);

            var location = await _packager!.CreatePackage(SourcePath!, DestinationPath!, setupFile, partialPackage: PartialPackage, cancellationToken: cancellationToken);
            _logger?.LogInformation("Package created at {location}", location);
            WriteObject(location);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error creating a package");
        }
    }
}
