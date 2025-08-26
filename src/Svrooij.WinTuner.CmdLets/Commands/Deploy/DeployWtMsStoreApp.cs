using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Kiota.Abstractions.Authentication;
using WingetIntune.Graph;
using GraphModels = Microsoft.Graph.Beta.Models;
using System.Linq;
using Svrooij.WinTuner.Proxy.Client;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;
/// <summary>
/// <para type="synopsis">Create a MsStore app in Intune</para>
/// <para type="description">Use this command to create an Microsoft Store app in Microsoft Intune.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>20</psOrder>
/// <parameterSet>
/// <para type="name">PackageId</para>
/// <para type="description">Deploy an app to Intune by specifying the package ID of the app in the Microsoft Store.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">SearchQuery</para>
/// <para type="description">Deploy an app to Intune by searching for packages and pick the first one, use carefully!</para>
/// </parameterSet>
/// <example>
/// <para type="name">Add Firefox to Intune</para>
/// <para type="description">Add Firefox to Intune and make available for `AllUsers`</para>
/// <code>Deploy-WtMsStoreApp -PackageId &quot;9NZVDKPMR9RD&quot; -AvailableFor AllUsers</code>
/// </example>
[Cmdlet(VerbsLifecycle.Deploy, "WtMsStoreApp", DefaultParameterSetName = nameof(PackageId), HelpUri = "https://wintuner.app/docs/wintuner-powershell/Deploy-WtMsStoreApp")]
[OutputType(typeof(GraphModels.WinGetApp))]
[GenerateBindings]
public partial class DeployWtMsStoreApp : BaseIntuneCmdlet
{
    /// <summary>
    /// <para type="description">The package id to upload to Intune.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(PackageId),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "The package id to upload to Intune.")]
    public string? PackageId { get; set; }

    /// <summary>
    /// <para type="description">Name of the app to look for, first match will be created.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(SearchQuery),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Name of the app to look for, first match will be created.")]
    public string? SearchQuery { get; set; }

    /// <summary>
    /// <para type="description">Categories to add to the app</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 1,
        HelpMessage = "Categories to add to the app")]
    public string[]? Categories { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should available for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 2,
        HelpMessage = "Groups that the app should available for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? AvailableFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app is required for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 3,
                      HelpMessage = "Groups that the app is required for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? RequiredFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should be uninstalled for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 4,
        HelpMessage = "Groups that the app should be uninstalled for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? UninstallFor { get; set; }

    [ServiceDependency]
    private ILogger<DeployWtMsStoreApp>? logger;

    [ServiceDependency]
    private WingetIntune.Graph.GraphStoreAppUploader? graphStoreAppUploader;

    [ServiceDependency]
    private WingetIntune.Graph.GraphClientFactory? gcf;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? proxyClient;

    /// <inheritdoc />
    protected override async Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        if (ParameterSetName == nameof(SearchQuery))
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(SearchQuery);
#endif
            logger!.LogInformation("Searching package id for {searchQuery}", SearchQuery);
            PackageId = await graphStoreAppUploader!.GetStoreIdForNameAsync(SearchQuery!, cancellationToken);
            if (string.IsNullOrEmpty(PackageId))
            {
                logger!.LogError("No package found for {searchQuery}", SearchQuery);
                return;
            }
        }

        // At this moment the package ID should always be filled.

#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(PackageId);
#endif

        if (PackageId!.StartsWith("https://apps.microsoft.com/detail/", StringComparison.OrdinalIgnoreCase))
        {
            var match = System.Text.RegularExpressions.Regex.Match(PackageId, @"detail/([^?]+)", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
            if (match.Success)
            {
                PackageId = match.Groups[1].Value;
                logger!.LogInformation("Extracted package id {PackageId} from URL", PackageId);
            }
            else
            {
                var ex = new ArgumentException("PackageId is not a valid Microsoft Store URL", nameof(PackageId));
                logger!.LogError(ex, "Only urls that start with 'https://apps.microsoft.com/' can be parsed");
                return;
            }
        }

        logger!.LogInformation("Uploading MSStore app {PackageId} to Intune", PackageId);
        proxyClient?.TriggerEvent(ConnectWtWinTuner.SessionId, nameof(DeployWtMsStoreApp), appVersion: ConnectWtWinTuner.AppVersion, packageId: PackageId, cancellationToken: CancellationToken.None);
        var graphServiceClient = gcf!.CreateClient(provider);
        try
        {
            var app = await graphStoreAppUploader!.CreateStoreAppAsync(graphServiceClient, PackageId!, cancellationToken);

            logger!.LogInformation("Created MSStore app {PackageId} with id {appId}", PackageId, app!.Id);

            if (Categories is not null && Categories.Any())
            {
                logger?.LogInformation("Adding categories to app {appId}", app!.Id);
                await graphServiceClient.AddIntuneCategoriesToAppAsync(app!.Id!, Categories, cancellationToken);
            }

            if ((AvailableFor is not null && AvailableFor.Any()) ||
                (RequiredFor is not null && RequiredFor.Any()) ||
                (UninstallFor is not null && UninstallFor.Any()))
            {
                logger?.LogInformation("Assigning app {appId} to groups", app!.Id);
                await graphServiceClient.AssignAppAsync(app!.Id!, RequiredFor, AvailableFor, UninstallFor, false, cancellationToken);
            }
            WriteObject(app);
        }
        catch (Exception ex)
        {
            logger!.LogError(ex, "Error creating MSStore app {PackageId}", PackageId);
        }
    }
}
