using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.PowerShell.DI;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using WingetIntune.Graph;
using Svrooij.WinTuner.Proxy.Client;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Get all apps from Intune packaged by WinTuner</para>
/// <para type="description">Load apps from Tenant and filter based on Update available.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>12</psOrder>
/// <example>
/// <para type="name">Get all apps with updates</para>
/// <para type="description">Get all apps that have updates available</para>
/// <code>Get-WtWin32Apps -Update $true</code>
/// </example>
/// <example>
/// <para type="name">Update apps</para>
/// <para type="description">Get all apps that have be an update available and are not superseeded. This executes the [New-WtWingetPackage](./New-WtWingetPackage) command.\r\nYou could run this on a weekly bases.</para>
/// <code>$updatedApps = Get-WtWin32Apps -Update $true -Superseded $false\r\nforeach($app in $updatedApps) { New-WtWingetPackage -PackageId $($app.PackageId) -PackageFolder $rootPackageFolder -Version $($app.LatestVersion) | Deploy-WtWin32App -GraphId $($app.GraphId) -KeepAssignments }</code>
/// </example>
/// <example>
/// <para type="name">Remove superseeded apps</para>
/// <para type="description">Get all apps that have been superseeded and remove them. This executes the [Remove-WtWin32App](./Remove-WtWin32App) command.\r\nYou could run this on a weekly bases.</para>
/// <code>$oldApps = Get-WtWin32Apps -Superseded $true\r\nforeach($app in $oldApps) { Remove-WtWin32App -AppId $app.GraphId }</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "WtWin32Apps", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Get-WtWin32Apps")]
[OutputType(typeof(Models.WtWin32App))]
[GenerateBindings]
public partial class GetWtWin32Apps : BaseIntuneCmdlet
{
    /// <summary>
    /// <para type="description">Filter based on UpdateAvailable</para>
    /// </summary>
    [Parameter(Mandatory = false,
        HelpMessage = "Filter based on UpdateAvailable")]
    public bool? Update { get; set; }

    /// <summary>
    /// <para type="description">Filter on if this app is superseded. `true` means only show apps that are superseded. `false` means only show apps that are not superseded</para>
    /// </summary>
    [Parameter(Mandatory = false,
               HelpMessage = "Filter on if this app is superseded. `true` means only show apps that are superseded. `false` means only show apps that are not superseded")]
    public bool? Superseded { get; set; }

    /// <summary>
    /// <para type="description">Filter on if this app is superseding other apps. `true` means only show apps that are superseding at least 1 app. `false` means only show apps that are not superseding any app</para>
    /// </summary>
    [Parameter(Mandatory = false,
                      HelpMessage = "Filter on if this app is superseding other apps. `true` means only show apps that are superseding at least 1 app. `false` means only show apps that are not superseding any app")]
    public bool? Superseding { get; set; }

    /// <summary>
    /// <para type="description">Server-side filter on displayName contains.</para>
    /// </summary>
    [Parameter(Mandatory = false,
                      HelpMessage = "Server-side filter on displayName contains.")]
    public string? NameContains { get; set; }

    /// <summary>
    /// <para type="description">Server-side filter on isAssigned.</para>
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Server-side filter on isAssigned.")]
    public bool? IsAssigned { get; set; }

    [ServiceDependency(Required = true)]
    private ILogger<GetWtWin32Apps> logger;

    [ServiceDependency(Required = true)]
    private WingetIntune.Graph.GraphClientFactory gcf;

    [ServiceDependency]
    private Winget.CommunityRepository.WingetRepository? repo;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? proxyClient;

    /// <inheritdoc/>
    protected override async Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        logger?.LogInformation("Getting list of published apps");


        var graphServiceClient = gcf!.CreateClient(provider);
        proxyClient?.TriggerEvent(
            sessionId: ConnectWtWinTuner.SessionId,
            command: nameof(GetWtWin32Apps),
            appVersion: ConnectWtWinTuner.AppVersion);
        var apps = await graphServiceClient.DeviceAppManagement.MobileApps.GetWinTunerAppsAsync(NameContains, isAssigned: IsAssigned, cancellationToken: cancellationToken);

        List<Models.WtWin32App> result = new();

        foreach (var app in apps)
        {
            var version = await repo!.GetLatestVersion(app.PackageId, cancellationToken);
            result.Add(new Models.WtWin32App
            {
                GraphId = app.GraphId,
                PackageId = app.PackageId,
                Name = app.Name,
                CurrentVersion = app.CurrentVersion ?? "",
                SupersededAppCount = app.SupersededAppCount,
                SupersedingAppCount = app.SupersedingAppCount,
                InstallerContext = app.InstallerContext,
                Architecture = app.Architecture,
                LatestVersion = version ?? "",
                IsAssigned = app.IsAssigned,
            });
        }

        if (Update.HasValue)
        {
            result = result.Where(x => x.IsUpdateAvailable == Update.Value).ToList();
        }

        if (Superseded.HasValue)
        {
            result = result.Where(x => x.SupersedingAppCount > 0 == Superseded.Value).ToList();
        }

        if (Superseding.HasValue)
        {
            result = Superseding.Value
                ? result.Where(x => x.SupersededAppCount > 0).ToList()
                : result.Where(x => x.SupersededAppCount == 0).ToList();
        }

        // Sort the results by Name and CurrentVersion
        // Server side the results are alread sorted by Name, but sorting on CurrentVersion is not possible server side, so we do it here.
        var vc = new WingetIntune.Models.StringVersionComparer();
        result = result.OrderBy(x => x.Name).ThenBy(x => x.CurrentVersion, vc).ToList();

        this.WriteCollection(result);
    }
}
