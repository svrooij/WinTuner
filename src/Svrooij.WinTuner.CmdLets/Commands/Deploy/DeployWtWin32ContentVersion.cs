using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.WinTuner.Proxy.Client;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;
/// <summary>
/// <para type="synopsis">Upload IntuneWin to existing application</para>
/// <para type="description">Use this command to upload an IntuneWin file to an existing app.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>20</psOrder>
/// <parameterSet>
/// <para type="name">IntuneWinFile</para>
/// <para type="description">Deploy an app packaged by WinTuner. If you used the [New-WtWingetPackage](./New-WtWingetPackage) commandlet to create the package, there will be some metadata available to us that is needed to create the Win32App in Intune.</para>
/// </parameterSet>
/// <example>
/// <para type="name">Upload intunewin</para>
/// <para type="description">Upload an intunewin to existing app.</para>
/// <code>Deploy-WtWin32ContentVersion -IntuneWinFile some-folder\\AppPackage.intunewin -AppId '187649c1-29b3-4f01-b32d-b4769334ec9c'</code>
/// </example>
[Cmdlet(VerbsLifecycle.Deploy, "WtWin32ContentVersion", DefaultParameterSetName = nameof(IntuneWinFile), HelpUri = "https://wintuner.app/docs/wintuner-powershell/Deploy-WtWin32ContentVersion")]
[OutputType(typeof(string))]
[GenerateBindings]
public partial class DeployWtWin32ContentVersion : BaseIntuneCmdlet
{

    /// <summary>
    /// <para type="description">The .intunewin file that should be added to this app</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = nameof(IntuneWinFile),
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "The .intunewin file that should be added to this app")]
    public string? IntuneWinFile { get; set; }

    /// <summary>
    /// <para type="description">To which app does this new file have to be uploaded?</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 2, ParameterSetName = nameof(IntuneWinFile), ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The Id of the app you want to have this new content added to.")]
    [Alias("GraphId")]
    public string? AppId { get; set; }

    [ServiceDependency]
    private ILogger<DeployWtWin32App>? logger;

    [ServiceDependency]
    private WingetIntune.Graph.GraphAppUploader? graphAppUploader;

    [ServiceDependency]
    private WingetIntune.Graph.GraphClientFactory? gcf;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? winTunerProxyClient;

    private bool isPartialPackage;
    private string? metadataFilename;

    /// <inheritdoc/>
    protected override async Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {

        logger?.LogInformation("Uploading file {IntuneWinFile} to {AppId}", IntuneWinFile, AppId);
        var graphServiceClient = gcf!.CreateClient(provider);

        if (IntuneWinFile is null)
        {
            var ex = new ArgumentException("No IntuneWinFile was provided");
            logger?.LogError(ex, "No IntuneWinFile was provided");
            throw ex;
        }

        // Check if the file exists
        if (!File.Exists(IntuneWinFile))
        {
            var ex = new FileNotFoundException("IntuneWin file not found", IntuneWinFile);
            logger?.LogError(ex, "IntuneWin file not found");
            throw ex;
        }

        var metadataFile = Path.Combine(Path.GetDirectoryName(IntuneWinFile)!, "metadata.xml");
        if (File.Exists(metadataFile))
        {
            metadataFilename = metadataFile;
            isPartialPackage = true;
            logger?.LogInformation("Assuming this is a partial intunewin, using {MetadataFile}", metadataFile);
        }

        var newApp = isPartialPackage
            ? await graphAppUploader!.CreateNewContentVersionAsync(graphServiceClient, AppId!, IntuneWinFile, metadataFilename!, cancellationToken)
            : await graphAppUploader!.CreateNewContentVersionAsync(graphServiceClient, AppId!, IntuneWinFile, cancellationToken);
        logger?.LogInformation("Uploaded new content version {ContentVersion} for {AppId}", newApp?.CommittedContentVersion, AppId);

        winTunerProxyClient?.TriggerEvent(ConnectWtWinTuner.SessionId, nameof(DeployWtWin32ContentVersion), appVersion: ConnectWtWinTuner.AppVersion, packageId: newApp?.DisplayName, cancellationToken: CancellationToken.None);


        WriteObject(newApp?.CommittedContentVersion);
    }
}
