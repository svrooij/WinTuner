using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands.Graph;
/// <summary>
/// <para type="synopsis">Clear authentication data from module</para>
/// <para type="description">You can call Connect-WtWinTuner which will keep security things in memory, to be able to clear things we made this command</para>
/// </summary>
/// <psOrder>4</psOrder>
/// <example>
/// <para type="name">Logout</para>
/// <para type="description">This will remove all credentials from the memory</para>
/// <code>Disconnect-WtWinTuner</code>
/// </example>
[Cmdlet(VerbsCommunications.Disconnect, "WtWinTuner", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Disconnect-WtWinTuner")]
[Alias("Disconnect-WinTuner")]
public class DisconnectWtWinTuner : DependencyCmdlet<Startup>
{
    [ServiceDependency]
    private ILogger<DisconnectWtWinTuner>? _logger;

    /// <inheritdoc />
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Disconnecting from WinTuner");
        await ConnectWtWinTuner.ClearAuthentication(cancellationToken);
    }
}
