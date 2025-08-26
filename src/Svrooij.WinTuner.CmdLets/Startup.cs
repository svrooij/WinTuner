using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Svrooij.PowerShell.DI;
using Svrooij.PowerShell.DI.Logging;
using System;
using WingetIntune;

namespace Svrooij.WinTuner.CmdLets;

/// <inheritdoc/>
public class Startup : PsStartup
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<SvRooij.ContentPrep.Packager>();
        services.AddTransient<Winget.CommunityRepository.WingetRepository>();
        const string code = "*REPLACED_AT_BUILD*";
        services.AddWingetServices(code);
    }

    /// <inheritdoc/>
    public override Action<PowerShellLoggerConfiguration> ConfigurePowerShellLogging()
    {
        return builder =>
        {
            builder.DefaultLevel = LogLevel.Debug;
            builder.LogLevel.Add("System.Net.Http.HttpClient", LogLevel.Warning);
            builder.LogLevel.Add("System.Net.Http.HttpClient.GraphClientFactory.LogicalHandler", LogLevel.Warning);
            builder.IncludeCategory = true;
            builder.StripNamespace = true;
        };
    }
}
