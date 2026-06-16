using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Decrypt an IntuneWin package</para>
/// <para type="description">Decrypt IntuneWin files, based on [this post](https://svrooij.io/2023/10/09/decrypting-intunewin-files/)</para>
/// </summary>
/// <psOrder>101</psOrder>
/// <example>
/// <code>Unprotect-IntuneWinPackage -SourceFile C:\Temp\Source\MyApp.intunewin -DestinationPath C:\Temp\Destination</code>
/// </example>
[Cmdlet(VerbsSecurity.Unprotect, "IntuneWinPackage", HelpUri = "https://wintuner.app/docs/wintuner-powershell/contentprep/Unprotect-IntuneWinPackage")]
[GenerateBindings]
public partial class UnprotectIntuneWinPackage : DependencyCmdlet<Startup>
{
    /// <summary>
    /// <para type="description">The location of the .intunewin file</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The location of the .intunewin file")]
    public string SourceFile { get; set; }

    /// <summary>
    /// <para type="description">Destination folder</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "Destination folder")]
    public string DestinationPath { get; set; }

    [ServiceDependency(Required = true)]
    private SvRooij.ContentPrep.Packager packager;

    [ServiceDependency]
    private ILogger<UnprotectIntuneWinPackage>? logger;

    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(SourceFile))
            {
                WriteError(new ErrorRecord(new FileNotFoundException("File not found", SourceFile), "1", ErrorCategory.InvalidOperation, null));
                return;
            }
            if (!Directory.Exists(DestinationPath))
            {
                WriteVerbose($"Creating destination folder {DestinationPath}");
                Directory.CreateDirectory(DestinationPath);
            }

            await packager.Unpack(SourceFile, DestinationPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error unlocking package");
        }
    }
}
