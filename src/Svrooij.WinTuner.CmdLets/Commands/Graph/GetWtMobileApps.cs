using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System;
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
/// <para type="synopsis">Get all apps from Intune</para>
/// <para type="description">Load apps from Tenant. This CmdLet will return all mobile apps in Intune. The raw Graph models are returned.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>100</psOrder>
/// <example>
/// <para type="name">Display all mobile apps</para>
/// <para type="description">Get a list of all apps in this tenant, and format the result as a table.</para>
/// <code>Get-WtMobileApps | Format-table -Property Id, DisplayName, IsAssigned, OdataType</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "WtMobileApps", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Get-WtMobileApps")]
[OutputType(typeof(Microsoft.Graph.Beta.Models.MobileApp))]
[GenerateBindings]
public partial class GetWtMobileApps : BaseIntuneCmdlet
{
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

    /// <summary>
    /// <para type="description">Server-side filter on filter. This is a raw OData filter expression.</para>
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Server-side filter on filter. This is a raw OData filter expression.")]
    public string? Filter { get; set; }

    [ServiceDependency(Required = true)]
    private ILogger<GetWtWin32Apps> logger;

    [ServiceDependency(Required = true)]
    private WingetIntune.Graph.GraphClientFactory gcf;

    [ServiceDependency]
    private Svrooij.WinTuner.Proxy.Client.WinTunerProxyClient? proxyClient;



    /// <inheritdoc/>
    protected override async Task ProcessAuthenticatedAsync(Microsoft.Kiota.Abstractions.Authentication.IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting MobileApps with name: {NameFiler}, isAssigned: {IsAssigned}", NameContains, IsAssigned);

        var graphServiceClient = gcf.CreateClient(provider);
        proxyClient?.TriggerEvent(
            sessionId: ConnectWtWinTuner.SessionId,
            command: nameof(GetWtMobileApps),
            appVersion: ConnectWtWinTuner.AppVersion);
        var apps = await graphServiceClient.DeviceAppManagement.MobileApps.GetAsync(req =>
        {
            List<string> filters = new();
            if (!string.IsNullOrWhiteSpace(NameContains))
            {
                filters.Add($"contains(displayName, '{NameContains}')");
            }
            if (IsAssigned is not null)
            {
                filters.Add($"isAssigned eq {(IsAssigned == true ? "true" : "false")}");
            }
            if (!string.IsNullOrWhiteSpace(Filter))
            {
                filters.Add(Filter);
            }

            if (filters.Count > 0)
            {
                req.QueryParameters.Filter = string.Join(" and ", filters);
            }
            req.QueryParameters.Orderby = new[] { "displayName" };
            req.QueryParameters.Top = 999; // Limit to 999 results, this is the maximum we can get in one request
        }, cancellationToken);

        if (apps is null || apps.Value?.Any() != true)
        {
            return;
        }

        var vc = new WingetIntune.Models.StringVersionComparer();
        var result = apps.Value!.ToArray();
        this.WriteCollection(result);
    }
}
