using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta;
using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.PowerShell.DI;
using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using WingetIntune.Extensions;
using WingetIntune.Graph;
using Svrooij.WinTuner.Proxy.Client;
using GraphModels = Microsoft.Graph.Beta.Models;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;
/// <summary>
/// <para type="synopsis">Create a Win32Lob app in Intune</para>
/// <para type="description">Use this command to upload an intunewin package to Microsoft Intune as a new Win32LobApp.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>11</psOrder>
/// <parameterSet>
/// <para type="name">WinGet</para>
/// <para type="description">Deploy an app packaged by WinTuner. If you used the [New-WtWingetPackage](./New-WtWingetPackage) commandlet to create the package, there will be some metadata available to us that is needed to create the Win32App in Intune.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">Win32LobApp</para>
/// <para type="description">Deploy an application, by specifying all the needed properties of the `Win32LobApp` and an IntuneWinFile.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">PackageFolder</para>
/// <para type="description">Deploy a pre-packaged application, from just it's folder</para>
/// </parameterSet>
/// <example>
/// <para type="name">Deploy OhMyPosh</para>
/// <para type="description">OhMyPosh v19.5.0 is packaged to this folder, now deploy it to Azure</para>
/// <code>Deploy-WtWin32App -PackageFolder &quot;C:\Tools\packages\JanDeDobbeleer.OhMyPosh\19.5.2&quot;</code>
/// </example>
/// <example>
/// <para type="name">Package and deploy OhMyPosh</para>
/// <para type="description">Combining both the `New-WtWinGetPackage` and the `Deploy-WtWin32App` command, and making it available to All Users</para>
/// <code>New-WtWingetPackage -PackageId JanDeDobbeleer.OhMyPosh -PackageFolder C:\Tools\Packages | Deploy-WtWin32App -Available AllUsers</code>
/// </example>
[Cmdlet(VerbsLifecycle.Deploy, "WtWin32App", DefaultParameterSetName = ParameterSetWinGet, HelpUri = "https://wintuner.app/docs/wintuner-powershell/Deploy-WtWin32App")]
[OutputType(typeof(GraphModels.Win32LobApp))]
[GenerateBindings]
public partial class DeployWtWin32App : BaseIntuneCmdlet
{
    private const string ParameterSetApp = "Win32LobApp";
    private const string ParameterSetWinGet = "WinGet";
    /// <summary>
    /// <para type="description">The Win32LobApp configuration you want to create</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = ParameterSetApp,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "The App configuration you want to create")]
    public GraphModels.Win32LobApp? App { get; set; }

    /// <summary>
    /// <para type="description">The .intunewin file that should be added to this app</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 1,
        ParameterSetName = ParameterSetApp,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "The .intunewin file that should be added to this app")]
    public string? IntuneWinFile { get; set; }

    /// <summary>
    /// <para type="description">Load the logo from file (optional)</para>
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 2,
        ParameterSetName = ParameterSetApp,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Load the logo from file")]
    public string? LogoPath { get; set; }

    /// <summary>
    /// <para type="description">The package id to upload to Intune.</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
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
        Position = 1,
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
        Position = 2,
        ParameterSetName = ParameterSetWinGet,
        ValueFromPipeline = false,
        HelpMessage = "The Root folder where all the package live in.")]
    public string? RootPackageFolder { get; set; }

    /// <summary>
    /// <para type="description">The folder where the package is</para>
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(PackageFolder),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = true,
        HelpMessage = "The folder where the package is")]
    public string? PackageFolder { get; set; }

    /// <summary>
    /// Override the name of the app in Intune
    /// </summary>
    [Parameter(HelpMessage = "Override the name of the app in Intune", Position = 100, Mandatory = false)]
    public string? OverrideAppName { get; set; }

    /// <summary>
    /// <para type="description">The graph id of the app to supersede</para>
    /// </summary>
    [Parameter(DontShow = true, HelpMessage = "Graph ID of the app to supersede", Position = 30, Mandatory = false)]
    public string? GraphId { get; set; }

    /// <summary>
    /// <para type="description">If set to true the old apps will stay assigned to the user.</para>
    /// </summary>
    [Parameter(DontShow = true, HelpMessage = "Keep assignments on app that is superseded", Position = 30, Mandatory = false)]
    [Alias("AppId")]
    public SwitchParameter KeepAssignments { get; set; }

    /// <summary>
    /// <para type="description">Categories to add to the app</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 11,
        HelpMessage = "Categories to add to the app")]
    public string[]? Categories { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should available for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 12,
               HelpMessage = "Groups that the app should available for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? AvailableFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app is required for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 13,
                      HelpMessage = "Groups that the app is required for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? RequiredFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should be uninstalled for, Group Object ID or `AllUsers` / `AllDevices`</para>
    /// </summary>
    [Parameter(Mandatory = false,
        Position = 14,
                             HelpMessage = "Groups that the app should be uninstalled for, Group Object ID or `AllUsers` / `AllDevices`")]
    public string[]? UninstallFor { get; set; }

    /// <summary>
    /// <para type="description">The role scope tags for this app</para>
    /// </summary>
    [Parameter(Mandatory = false, Position = 15, HelpMessage = "The role scope tags for this app")]
    public string[]? RoleScopeTags { get; set; }

    [ServiceDependency]
    private ILogger<DeployWtWin32App>? logger;

    [ServiceDependency]
    private WingetIntune.Graph.GraphAppUploader? graphAppUploader;

    [ServiceDependency]
    private WingetIntune.Intune.MetadataManager? metadataManager;

    [ServiceDependency]
    private WingetIntune.Graph.GraphClientFactory? gcf;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? proxyClient;

    private bool isPartialPackage;
    private string? metadataFilename;

    /// <inheritdoc/>
    protected override async Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        proxyClient?.TriggerEvent(ConnectWtWinTuner.SessionId, nameof(DeployWtWin32App), appVersion: ConnectWtWinTuner.AppVersion, packageId: PackageId, cancellationToken: CancellationToken.None);

        if (App is null)
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
                var win32LobAppFile = Path.Combine(PackageFolder, "win32LobApp.json");
                if (File.Exists(win32LobAppFile))
                {
                    logger?.LogDebug("Loading Win32LobApp from file {Win32LobAppFile}", win32LobAppFile);
                    var json = await File.ReadAllTextAsync(win32LobAppFile, cancellationToken);
                    App = await json!.ParseJson<GraphModels.Win32LobApp>(cancellationToken);
                    App!.BackingStore.InitializationCompleted = false;
                    App.BackingStore.ReturnOnlyChangedValues = false;
                    IntuneWinFile = Path.Combine(PackageFolder, App!.FileName!);
                }
                else
                {
                    logger?.LogDebug("Loading package info from folder {PackageFolder}", PackageFolder);
                    var packageInfo = await metadataManager!.LoadPackageInfoFromFolderAsync(PackageFolder, cancellationToken);
                    App = metadataManager.ConvertPackageInfoToWin32App(packageInfo);
                    LogoPath = Path.GetFullPath(Path.Combine(PackageFolder, "..", "logo.png"));
                    IntuneWinFile = metadataManager.GetIntuneWinFileName(PackageFolder, packageInfo);
                }
            }
            else
            {
                var ex = new ArgumentException("No App or PackageFolder was provided");
                logger?.LogError(ex, "No App or PackageFolder was provided");
                throw ex;
            }
        }

        if (RoleScopeTags is not null && RoleScopeTags.Any())
        {
            logger?.LogDebug("Adding role scope tags to app");
            if (App.RoleScopeTagIds is null)
            {
                App.RoleScopeTagIds = new();
            }
            foreach (var tag in RoleScopeTags)
            {
                App.RoleScopeTagIds.Add(tag);
            }
            logger?.LogInformation("Role scope tags added to app {@RoleScopeTags}", App?.RoleScopeTagIds);
        }

        if (!string.IsNullOrEmpty(OverrideAppName))
        {
            App!.DisplayName = OverrideAppName;
            App.BackingStore.InitializationCompleted = false;
            App.BackingStore.ReturnOnlyChangedValues = false;
        }

        logger?.LogInformation("Uploading Win32App {DisplayName} to Intune with file {IntuneWinFile}", App!.DisplayName, IntuneWinFile);
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
            var partialFileName = Path.Combine(Path.GetDirectoryName(IntuneWinFile)!, Path.GetFileNameWithoutExtension(IntuneWinFile) + ".partial.intunewin");
            metadataFilename = Path.Combine(Path.GetDirectoryName(IntuneWinFile)!, "metadata.xml");
            if (File.Exists(partialFileName) && File.Exists(metadataFilename))
            {
                logger?.LogDebug("Found partial file {PartialFileName} and metadata.xml, using that instead of {IntuneWinFile}", partialFileName, IntuneWinFile);
                IntuneWinFile = partialFileName;
                isPartialPackage = true;
            }
            else
            {
                var ex = new FileNotFoundException("IntuneWin file not found", IntuneWinFile);
                logger?.LogError(ex, "IntuneWin file not found");
                throw ex;
            }
        }

        var newApp = isPartialPackage
            ? await graphAppUploader!.CreateNewAppAsync(graphServiceClient, App!, IntuneWinFile!, metadataFilename!, logoPath: LogoPath, cancellationToken: cancellationToken)
            : await graphAppUploader!.CreateNewAppAsync(graphServiceClient, App!, IntuneWinFile!, LogoPath, cancellationToken);
        logger?.LogInformation("Created Win32App {DisplayName} with id {AppId}", newApp!.DisplayName, newApp.Id);

        // Check if we need to supersede an app
        if (GraphId is not null)
        {
            await SupersedeApp(logger!, graphServiceClient, newApp!.Id!, GraphId, KeepAssignments, cancellationToken);
        }
        else
        {
            if (Categories is not null && Categories.Any())
            {
                logger?.LogInformation("Adding categories to app {AppId}", newApp!.Id);
                await graphServiceClient.AddIntuneCategoriesToAppAsync(newApp!.Id!, Categories, cancellationToken);
            }

            if ((AvailableFor is not null && AvailableFor.Any()) ||
                (RequiredFor is not null && RequiredFor.Any()) ||
                (UninstallFor is not null && UninstallFor.Any()))
            {
                logger?.LogInformation("Assigning app {AppId} to groups", newApp!.Id);
                // By default the Auto Update is enabled for the new app, be sure to update this on old apps.
                await graphServiceClient.AssignAppAsync(newApp!.Id!, RequiredFor, AvailableFor, UninstallFor, true, cancellationToken);
            }
        }

        WriteObject(newApp!);
    }

    /// <summary>
    /// Supersede an app.
    /// </summary>
    /// <remarks>
    /// 1. Load the old app
    /// 2. Update relationships of the new app to supersede the old app
    /// 3. Copy categories from the old app to the new app
    /// 4. Copy assignments from the old app to the new app
    /// 5. Remove assignments from the old app
    /// </remarks>
    /// <param name="logger"></param>
    /// <param name="graphServiceClient"></param>
    /// <param name="newAppId"></param>
    /// <param name="oldAppId"></param>
    /// <param name="keepOldAssignments"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task SupersedeApp(ILogger logger, GraphServiceClient graphServiceClient, string newAppId, string oldAppId, bool keepOldAssignments, CancellationToken cancellationToken)
    {
        logger?.LogDebug("Loading old app {OldAppId} to superseed", oldAppId);
        var oldApp = await graphServiceClient.DeviceAppManagement.MobileApps[oldAppId].GetAsync(req =>
        {
            req.QueryParameters.Expand = new string[] { "categories", "assignments" };
        }, cancellationToken);

        if (oldApp is GraphModels.Win32LobApp oldWin32App)
        {
            logger?.LogInformation("Superseeding app {OldAppId} with {AppId}", oldAppId, newAppId);
            var batch = new Microsoft.Graph.BatchRequestContentCollection(graphServiceClient);
            // Add supersedence relationship to new app
            await batch.AddBatchRequestStepAsync(graphServiceClient.DeviceAppManagement.MobileApps[newAppId].UpdateRelationships.ToPostRequestInformation(new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.UpdateRelationships.UpdateRelationshipsPostRequestBody
            {
                Relationships = new()
                    {
                        new GraphModels.MobileAppSupersedence
                        {
                            // TODO Should the SupersedenceType be Update or Replace, maybe configureable?
                            SupersedenceType = GraphModels.MobileAppSupersedenceType.Update,
                            TargetId = oldAppId!
                        }
                    }
            }));

            // Copy categories from old app to new app
            if (oldWin32App.Categories is not null && oldWin32App.Categories.Count > 0)
            {
                foreach (var c in oldWin32App.Categories)
                {
                    await batch.AddBatchRequestStepAsync(graphServiceClient.Intune_AddCategoryToApp_RequestInfo(newAppId, c.Id!));
                }
            }

            // Copy assignments from old app to new app
            if (oldWin32App.Assignments is not null && oldWin32App.Assignments.Count > 0)
            {
                // This part is to enable auto update for the new app, if that was not set on the old app
                var assignments = oldWin32App.Assignments;
                foreach (var assignment in assignments)
                {
                    if (assignment.Intent == GraphModels.InstallIntent.Available && assignment.Settings is null)
                    {
                        assignment.Settings = new GraphModels.Win32LobAppAssignmentSettings
                        {
                            Notifications = GraphModels.Win32LobAppNotification.ShowReboot,
                            AutoUpdateSettings = new GraphModels.Win32LobAppAutoUpdateSettings { AutoUpdateSupersededAppsState = GraphModels.Win32LobAutoUpdateSupersededAppsState.Enabled }
                        };

                    }
                }

                await batch.AddBatchRequestStepAsync(graphServiceClient.DeviceAppManagement.MobileApps[newAppId].Assign.ToPostRequestInformation(new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
                {
                    MobileAppAssignments = assignments, //oldWin32App.Assignments
                }));

                // Remove assignments from old app
                if (!keepOldAssignments)
                {
                    await batch.AddBatchRequestStepAsync(graphServiceClient.DeviceAppManagement.MobileApps[oldAppId].Assign.ToPostRequestInformation(new Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item.Assign.AssignPostRequestBody
                    {
                        MobileAppAssignments = new System.Collections.Generic.List<GraphModels.MobileAppAssignment>()
                    }));
                }

            }

            // Execute batch
            await graphServiceClient.Batch.PostAsync(batch, cancellationToken);
        }
    }
}
