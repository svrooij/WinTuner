using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.PowerShell.DI;
using Svrooij.WinTuner.CmdLets.Commands.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Svrooij.WinTuner.CmdLets.Commands;
/// <summary>
/// Base class for all cmdlets that need to connect to Graph.
/// </summary>
public abstract class BaseIntuneCmdlet : DependencyCmdlet<Startup>
{
    /// <summary>
    /// Executes the cmdlet asynchronously.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <remarks>This is sealed, override ProcessAuthenticatedAsync</remarks>
    public sealed override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        ValidateAuthenticationParameters();
        await ProcessAuthenticatedAsync(CreateAuthenticationProvider(cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Execute a cmdlet with required authentication provider.
    /// </summary>
    /// <param name="provider">Authentication provider, use <see cref="ConnectWtWinTuner"/> to connect to Intune</param>
    /// <param name="cancellationToken">CancallationToken that will cancel if the user pressed ctrl + c, during execution</param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    protected virtual Task ProcessAuthenticatedAsync(IAuthenticationProvider provider, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    private void ValidateAuthenticationParameters()
    {
        if (ConnectWtWinTuner.AuthenticationProvider is null)
        {
            throw new InvalidDataException("Not logged in");
        }
    }

    private IAuthenticationProvider CreateAuthenticationProvider(CancellationToken cancellationToken = default)
    {
        return ConnectWtWinTuner.AuthenticationProvider!;
    }
}
