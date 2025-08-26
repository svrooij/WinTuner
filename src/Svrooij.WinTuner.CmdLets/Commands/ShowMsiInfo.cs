using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using WingetIntune;
using WingetIntune.Msi;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Show information about an MSI file</para>
/// <para type="description">Show information about an MSI file, this includes the MSI code and version.</para>
/// </summary>
/// <psOrder>100</psOrder>
/// <example>
/// <para type="name">Show information about an MSI file</para>
/// <para type="description">Show information about an MSI file, this includes the MSI code and version.</para>
/// <code>Show-MsiInfo -MsiPath "C:\path\to\file.msi"</code>
/// </example>
/// <example>
/// <para type="name">Show information about an MSI file from URL</para>
/// <para type="description">Download an MSI file and show the details</para>
/// <code>Show-MsiInfo -MsiUrl "https://example.com/file.msi" -OutputPath "C:\path\to"</code>
/// </example>
[Cmdlet(VerbsCommon.Show, "MsiInfo", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Show-MsiInfo", DefaultParameterSetName = nameof(MsiPath))]
[OutputType(typeof(Models.MsiInfo))]
[GenerateBindings]
public class ShowMsiInfo : DependencyCmdlet<Startup>
{
    /// <summary>
    /// <para type="description">Path to the MSI file</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = nameof(MsiPath))]
    public string? MsiPath { get; set; }

    /// <summary>
    /// <para type="description">URL to the MSI file</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = nameof(MsiUrl))]
    public Uri? MsiUrl { get; set; }

    /// <summary>
    /// <para type="description">Path to save the MSI file</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = nameof(MsiUrl))]
    public string? OutputPath { get; set; }

    /// <summary>
    /// <para type="description">Filename to save the MSI file, if cannot be discovered from url</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 2, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = nameof(MsiUrl))]
    public string? OutputFilename { get; set; }

    [ServiceDependency]
    private ILogger<ShowMsiInfo>? logger;

    [ServiceDependency]
    private WingetIntune.IFileManager? fileManager;

    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        if (MsiPath is not null)
        {
            logger?.LogInformation("Reading MSI from path: {MsiPath}", MsiPath);
        }
        else if (MsiUrl is not null)
        {
            OutputPath ??= Path.GetTempPath();
            logger?.LogInformation("Downloading MSI from URL {MsiUrl} to {OutputPath}", MsiUrl, OutputPath);
            var outputFile = Path.Combine(OutputPath, OutputFilename ?? Path.GetFileName(MsiUrl.LocalPath));
            // The file managed does automatic chunking of the download, so it will also work for very large files.
            await fileManager!.DownloadFileAsync(MsiUrl!.ToString(), outputFile, cancellationToken: cancellationToken);
            MsiPath = outputFile;
        }
        else
        {
            throw new InvalidOperationException("Either MsiPath or MsiUrl must be set");
        }

        using var msiStream = new FileStream(MsiPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous);
        var decoder = new MsiDecoder(msiStream);
        var codeFromMsi = decoder.GetCode();
        var versionFromMsi = decoder.GetVersion();

        WriteObject(new Models.MsiInfo
        {
            Path = MsiPath,
            ProductCode = codeFromMsi,
            ProductVersion = versionFromMsi
        });
    }
}
