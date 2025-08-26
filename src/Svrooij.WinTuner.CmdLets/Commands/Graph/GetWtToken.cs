using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.PowerShell.DI;
using Svrooij.WinTuner.CmdLets.Commands.Graph;

namespace Svrooij.WinTuner.CmdLets.Commands;

/// <summary>
/// <para type="synopsis">Get a token for graph</para>
/// <para type="description">This command will get a token for the graph api. The token is cached, so you can call this as often as you want.\r\n\r\nThis is an [**authenticated command**](./authentication), so call [Connect-WtWinTuner](./Connect-WtWinTuner) before calling this command.</para>
/// </summary>
/// <psOrder>100</psOrder>
/// <example>
/// <para type="name">Get a token</para>
/// <para type="description">Get token, show details and copy to clipboard</para>
/// <code>Get-WtToken -DecodeToken | Set-Clipboard</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "WtToken", HelpUri = "https://wintuner.app/docs/wintuner-powershell/Get-WtToken")]
[OutputType(typeof(string))]
[GenerateBindings]
public partial class GetWtToken : BaseIntuneCmdlet
{
    /// <summary>
    /// Decode the token
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Decode the token")]
    public SwitchParameter DecodeToken { get; set; }

    /// <summary>
    /// Output the token to the logs?
    /// </summary>
    [Parameter(Mandatory = false, HelpMessage = "Output the token to the logs?")]
    public SwitchParameter ShowToken { get; set; }

    [ServiceDependency]
    private ILogger<GetWtToken>? _logger;

    /// <summary>
    /// Execute the command
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="cancellationToken"></param>
    protected override async Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        var token = await ConnectWtWinTuner.GetTokenAsync(cancellationToken);
        if (ShowToken)
        {
            _logger?.LogInformation("Got token {Token}", token);
        }

        if (DecodeToken)
        {
            // Decode the jwt token and output the claims to the logs
            var jwt = new JwtSecurityToken(token);
            _logger?.LogInformation("Token claims: {Claims}", string.Join("\r\n", jwt.Claims.Select(c => $"{c.Type}\t{c.Value}")));
        }

        WriteObject(token);
    }
}
