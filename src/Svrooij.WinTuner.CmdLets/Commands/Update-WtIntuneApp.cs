﻿using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta.Models;
using Svrooij.PowerShell.DependencyInjection;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WingetIntune.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Update an app in Intune</para>
/// <para type="description">Update the assignments and/or categories for an app in Intune.</para>
/// <para type="link" uri="https://wintuner.app/docs/wintuner-powershell/Update-WtIntuneApp">Documentation</para> 
/// </summary>
/// <example>
/// <para type="description">Update the categories of an app and make it available for all users</para>
/// <code>Update-WtIntuneApp -AppId "1450c17d-aee5-4bef-acf9-9e0107d340f2" -UseDefaultCredentials -Categories "Productivity","Business" -AvailableFor "AllUsers" -EnableAutoUpdate $true</code>
/// </example>
[Cmdlet(VerbsData.Update, "WtIntuneApp")]
[OutputType(typeof(MobileApp))]
public class UpdateWtIntuneApp : BaseIntuneCmdlet
{
    /// <summary>
    /// <para type="description">Id of the app in Intune</para>
    /// </summary>
    [Parameter(Mandatory = true,
        HelpMessage = "Id of the app in Intune")]
    public string? AppId { get; set; }

    /// <summary>
    /// <para type="description">Categories to add to the app</para>
    /// </summary>
    [Parameter(Mandatory = false,
        HelpMessage = "Categories to add to the app")]
    public string[]? Categories { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should available for, Group Object ID or 'AllUsers'/'AllDevices'</para>
    /// </summary>
    [Parameter(Mandatory = false,
               HelpMessage = "Groups that the app should available for, Group Object ID or 'AllUsers'/'AllDevices'")]
    public string[]? AvailableFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app is required for, Group Object ID or 'AllUsers'/'AllDevices'</para>
    /// </summary>
    [Parameter(Mandatory = false,
                      HelpMessage = "Groups that the app is required for, Group Object ID or 'AllUsers'/'AllDevices'")]
    public string[]? RequiredFor { get; set; }

    /// <summary>
    /// <para type="description">Groups that the app should be uninstalled for, Group Object ID or 'AllUsers'/'AllDevices'</para>
    /// </summary>
    [Parameter(Mandatory = false,
                             HelpMessage = "Groups that the app should be uninstalled for, Group Object ID or 'AllUsers'/'AllDevices'")]
    public string[]? UninstallFor { get; set; }

    /// <summary>
    /// <para type="description">Enable auto update for the app</para>
    /// </summary>
    [Parameter(Mandatory = false,
                      HelpMessage = "Enable auto update for the app")]
    public bool EnableAutoUpdate { get; set; } = false;

    [ServiceDependency]
    private ILogger<UpdateWtIntuneApp>? logger;

    [ServiceDependency]
    private HttpClient? httpClient;

    /// <inheritdoc/>
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        ValidateAuthenticationParameters();
        logger?.LogInformation("Updating app {appId} in Intune", AppId);

        var graphServiceClient = CreateGraphServiceClient(httpClient!);

        if (Categories is not null && Categories.Any())
        {
            logger?.LogInformation("Adding categories to app {appId}", AppId);
            await graphServiceClient.AddIntuneCategoriesToAppAsync(AppId!, Categories, cancellationToken);
        }

        if ((AvailableFor is not null && AvailableFor.Any()) ||
            (RequiredFor is not null && RequiredFor.Any()) ||
            (UninstallFor is not null && UninstallFor.Any()))
        {
            logger?.LogInformation("Assigning app {appId} to groups", AppId);
            await graphServiceClient.AssignAppAsync(AppId!, RequiredFor, AvailableFor, UninstallFor, EnableAutoUpdate, cancellationToken);
        }

        // Load the app to get the relationships
        var app = await graphServiceClient.DeviceAppManagement.MobileApps[AppId].GetAsync(req =>
        {
            req.QueryParameters.Expand = new string[] { "categories", "assignments" };
        }, cancellationToken: cancellationToken);

        logger?.LogInformation("App {appId} updated from Intune", AppId);

        WriteObject(app);
    }
}
