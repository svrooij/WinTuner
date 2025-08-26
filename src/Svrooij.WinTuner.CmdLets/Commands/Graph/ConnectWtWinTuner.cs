using Microsoft.Kiota.Abstractions.Authentication;
using Svrooij.PowerShell.DI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using System.Security.Cryptography.X509Certificates;

namespace Svrooij.WinTuner.CmdLets.Commands.Graph;
/// <summary>
/// <para type="synopsis">Connect to Intune</para>
/// <para type="description">A separate command to select the correct authentication provider, you no longer have to provide the auth parameters with each command.</para>
/// </summary>
/// <psOrder>3</psOrder>
/// <parameterSet>
/// <para type="name">Interactive</para>
/// <para type="description">If you're running WinTuner on your local machine, you can use the interactive browser login. This will integrate with the native browser based login screen on Windows and with the default browser on other platforms.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">UseManagedIdentity</para>
/// <para type="description">WinTuner supports Managed Identity authentication, this is the recommended way if you run WinTuner in the Azure Environment.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">UseDefaultCredentials</para>
/// <para type="description">A more extended version of the Managed Identity is the Default Credentials, this will use the [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet&amp;wt.mc_id=SEC-MVP-5004985), from the `Azure.Identity` package. This will try several methods to authenticate, Environment Variables, Managed Identity, Azure CLI and more.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">Token</para>
/// <para type="description">Let's say you have a token from another source, just hand us to token and we'll use it to connect to Intune. This token has a limited lifetime, so you'll be responsible for refreshing it.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">ClientCertificateCredentials</para>
/// <para type="description">Client credentials flow using a certificate in the user or local computer store.\r\n\r\nMake sure to mark the certificate as not exportable, this helps in keeping the certificate secure.</para>
/// </parameterSet>
/// <parameterSet>
/// <para type="name">ClientCredentials</para>
/// <para type="description">:::warning Last resort\r\nUsing client credentials is not recommended because you'll have to keep the secret, **secret**!\r\n\r\nPlease let us know if you have to use this method, we might be able to help you with a better solution.\r\n:::</para>
/// </parameterSet>
/// <example>
/// <para type="name">Connect using interactive authentication</para>
/// <para type="description">This will trigger a login broker popup (Windows Hello) on Windows and the default browser on other platforms</para>
/// <code>Connect-WtWinTuner -Username "youruser@contoso.com"</code>
/// </example>
/// <example>
/// <para type="name">Connect using managed identity</para>
/// <para type="description">Try to connect using a managed identity on the current platform, obviously only works in Azure.</para>
/// <code>Connect-WtWinTuner -UseManagedIdentity</code>
/// </example>
/// <example>
/// <para type="name">Connect using default credentials</para>
/// <para type="description">A chain of credentials is tried until one succeeds. Including Environment Variables, Managed Identity, Visual Studio (code) and Azure CLI</para>
/// <code>az login\r\nConnect-WtWinTuner -UseDefaultCredentials</code>
/// </example>
[Cmdlet(VerbsCommunications.Connect, "WtWinTuner", DefaultParameterSetName = ParamSetInteractive, HelpUri = "https://wintuner.app/docs/wintuner-powershell/Connect-WtWinTuner")]
[Alias("Connect-WinTuner")]
public class ConnectWtWinTuner : DependencyCmdlet<Startup>
{
    private const string DefaultClientId = "d5a8a406-3b1d-4069-91cc-d76acdd812fe";
    private const string DefaultClientCredentialScope = "https://graph.microsoft.com/.default";
    private const string ParamSetInteractive = "Interactive";
    private const string ParamSetClientCredentials = "ClientCredentials";
    private const string ParamSetClientCertificateCredentials = "ClientCertificateCredentials";

    /// <summary>
    /// Used default scopes
    /// </summary>
    private static readonly string[] DefaultScopes = { "DeviceManagementConfiguration.ReadWrite.All", "DeviceManagementApps.ReadWrite.All" };

    internal static IAuthenticationProvider? AuthenticationProvider { get; private set; }
    internal static string? SessionId { get; private set; }
    internal static string AppVersion { get; } = typeof(ConnectWtWinTuner).Assembly.GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Use a managed identity to connect to Intune
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(UseManagedIdentity),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Use a managed identity to connect to Intune")]
    public SwitchParameter UseManagedIdentity { get; set; } = Environment.GetEnvironmentVariable("AZURE_USE_MANAGED_IDENTITY")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Use default Azure Credentials from Azure.Identity to connect to Intune
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(UseDefaultCredentials),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Use default Azure Credentials from Azure.Identity to connect to Intune")]
    public SwitchParameter UseDefaultCredentials { get; set; } = Environment.GetEnvironmentVariable("AZURE_USE_DEFAULT_CREDENTIALS")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Use a token from another source to connect to Intune
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = nameof(Token),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Use a token from another source to connect to Intune, this is the least preferred way to use")]
    public string? Token { get; set; } = Environment.GetEnvironmentVariable("AZURE_TOKEN");

    /// <summary>
    /// 
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 2,
        ParameterSetName = ParamSetInteractive,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Disable Windows authentication broker")]
    public SwitchParameter NoBroker { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Parameter(
        Mandatory = true,
        Position = 0,
        ParameterSetName = ParamSetInteractive,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Use a username to trigger interactive login or SSO")]
    public string? Username { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 2,
        ParameterSetName = ParamSetInteractive,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the tenant ID, optional. Loaded from `AZURE_TENANT_ID`")]
    [Parameter(
        Mandatory = true,
        Position = 2,
        ParameterSetName = ParamSetClientCredentials,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the tenant ID. Loaded from `AZURE_TENANT_ID`")]
    [Parameter(
        Mandatory = true,
        Position = 2,
        ParameterSetName = ParamSetClientCertificateCredentials,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the tenant ID. Loaded from `AZURE_TENANT_ID`")]
    public string? TenantId { get; set; } = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

    /// <summary>
    /// 
    /// </summary>
    [Parameter(
               Mandatory = true,
               Position = 0,
               ParameterSetName = ParamSetClientCredentials,
               ValueFromPipeline = false,
               ValueFromPipelineByPropertyName = false,
               HelpMessage = "Specify the client ID, mandatory for Client Credentials flow. Loaded from `AZURE_CLIENT_ID`")]
    [Parameter(
               Mandatory = true,
               Position = 0,
               ParameterSetName = ParamSetClientCertificateCredentials,
               ValueFromPipeline = false,
               ValueFromPipelineByPropertyName = false,
               HelpMessage = "Specify the client ID, mandatory for Client Certificate flow. Loaded from `AZURE_CLIENT_ID`")]
    [Parameter(
        Mandatory = false,
        Position = 3,
        ParameterSetName = ParamSetInteractive,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the alternative client ID, optional. Loaded from `AZURE_CLIENT_ID`")]
    public string? ClientId { get; set; } = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");


    /// <summary>
    /// Client secret for client credentials flow
    /// </summary>
    [Parameter(
               Mandatory = true,
               Position = 1,
               ParameterSetName = ParamSetClientCredentials,
               ValueFromPipeline = false,
               ValueFromPipelineByPropertyName = false,
               HelpMessage = "Specify the client secret. Loaded from `AZURE_CLIENT_SECRET`")]
    public string? ClientSecret { get; set; } = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");

    /// <summary>
    /// Certificate Thumbprint for client authentication
    /// </summary>
    [Parameter(
                Mandatory = true,
                Position = 1,
                ParameterSetName = ParamSetClientCertificateCredentials,
                ValueFromPipeline = false,
                HelpMessage = "Specify the thumbprint of the certificate. Loaded from `AZURE_CLIENT_CERT_THUMBPRINT`")]
    public string? ClientCertificateThumbprint { get; set; } = Environment.GetEnvironmentVariable("AZURE_CLIENT_CERT_THUMBPRINT");

    /// <summary>
    /// Specify scopes to use
    /// </summary>
    [Parameter(
        Mandatory = false,
        Position = 10,
        ParameterSetName = ParamSetClientCredentials,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the scopes to request, default is `https://graph.microsoft.com/.default`")]
    [Parameter(
        Mandatory = false,
        Position = 10,
        ParameterSetName = nameof(UseDefaultCredentials),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the scopes to request, default is `https://graph.microsoft.com/.default`")]
    [Parameter(
        Mandatory = false,
        Position = 10,
        ParameterSetName = nameof(UseManagedIdentity),
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the scopes to request, default is `https://graph.microsoft.com/.default`")]
    [Parameter(
        Mandatory = false,
        Position = 10,
        ParameterSetName = ParamSetClientCertificateCredentials,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the scopes to request, default is `https://graph.microsoft.com/.default`")]
    [Parameter(
        Mandatory = false,
        Position = 10,
        ParameterSetName = ParamSetInteractive,
        ValueFromPipeline = false,
        ValueFromPipelineByPropertyName = false,
        HelpMessage = "Specify the scopes to request, default is `DeviceManagementConfiguration.ReadWrite.All`, `DeviceManagementApps.ReadWrite.All`")]
    public string[]? Scopes { get; set; } = Environment.GetEnvironmentVariable("AZURE_SCOPES")?.Split(' ');

    /// <summary>
    /// Immediately try to get a token.
    /// </summary>
    [Parameter(Mandatory = false, Position = 11, ParameterSetName = nameof(UseManagedIdentity), HelpMessage = "Immediately try to get a token.")]
    [Parameter(Mandatory = false, Position = 11, ParameterSetName = ParamSetInteractive, HelpMessage = "Immediately try to get a token.")]
    [Parameter(Mandatory = false, Position = 11, ParameterSetName = nameof(UseDefaultCredentials), HelpMessage = "Immediately try to get a token.")]
    [Parameter(Mandatory = false, Position = 11, ParameterSetName = ParamSetClientCredentials, HelpMessage = "Immediately try to get a token.")]
    public SwitchParameter Test { get; set; }

    [ServiceDependency]
    private ILogger<ConnectWtWinTuner>? _logger;

    /// <inheritdoc />
    public override async Task ProcessRecordAsync(CancellationToken cancellationToken)
    {
        SessionId ??= Guid.NewGuid().ToString();
        _logger?.LogInformation("Connecting to Intune using {ParameterSetName}", ParameterSetName);
        AuthenticationProvider = CreateAuthenticationProvider(cancellationToken);

        // Try getting a token at this point helps solve a lot of issues.
        var token = await GetTokenAsync(cancellationToken);
        if (Test)
        {
            _logger?.LogInformation("Got token {Token}", token);
            WriteObject(token);
        }
    }

    private IAuthenticationProvider CreateAuthenticationProvider(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(Token))
        {
            return new WingetIntune.Internal.Msal.StaticAuthenticationProvider(Token);
        }

        var scope = (Scopes ?? DefaultScopes)[0];

        if (UseManagedIdentity || UseDefaultCredentials)
        {
            // Maybe make which credentials to use configurable
            Azure.Core.TokenCredential credentials = UseManagedIdentity
                ? new Azure.Identity.ManagedIdentityCredential(ClientId)
                : new Azure.Identity.DefaultAzureCredential();
            return new Microsoft.Graph.Authentication.AzureIdentityAuthenticationProvider(credentials, null, null, isCaeEnabled: false, DefaultClientCredentialScope);
        }

        if (ParameterSetName == ParamSetClientCredentials)
        {
            if (!string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(TenantId) &&
                !string.IsNullOrEmpty(ClientSecret))
            {
                return new Microsoft.Graph.Authentication.AzureIdentityAuthenticationProvider(
                    new Azure.Identity.ClientSecretCredential(TenantId, ClientId, ClientSecret,
                        new Azure.Identity.ClientSecretCredentialOptions
                        {
                            TokenCachePersistenceOptions = new Azure.Identity.TokenCachePersistenceOptions
                            {
                                Name = "WinTuner-PowerShell-CC",
                                UnsafeAllowUnencryptedStorage = true,
                            }
                        }), isCaeEnabled: false, scopes: DefaultClientCredentialScope);
            }
            else
            {
                throw new ArgumentException("Not all parameters for client credentials are specified",
                    nameof(ClientId));
            }
        }

        if (ParameterSetName == ParamSetClientCertificateCredentials)
        {
            if (!string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(TenantId) &&
                !string.IsNullOrEmpty(ClientCertificateThumbprint))
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var certificate = store.Certificates.Cast<X509Certificate2>().FirstOrDefault(cert => cert.Thumbprint == ClientCertificateThumbprint);
                store.Close();
                if (certificate == null)
                {
                    using var storeLocal = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    storeLocal.Open(OpenFlags.ReadOnly);
                    certificate = storeLocal.Certificates.Cast<X509Certificate2>().FirstOrDefault(cert => cert.Thumbprint == ClientCertificateThumbprint);
                    storeLocal.Close();
                }
                if (certificate == null)
                {
                    throw new ArgumentException("Cannot find cert thumbprint in User or Machine store");
                }

                return new Microsoft.Graph.Authentication.AzureIdentityAuthenticationProvider(
                    new Azure.Identity.ClientCertificateCredential(TenantId, ClientId, certificate,
                        new Azure.Identity.ClientCertificateCredentialOptions
                        {
                            TokenCachePersistenceOptions = new Azure.Identity.TokenCachePersistenceOptions
                            {
                                Name = "WinTuner-PowerShell-CC",
                                UnsafeAllowUnencryptedStorage = true,
                            }
                        }
                    ), isCaeEnabled: false, scopes: DefaultClientCredentialScope);

            }
            else
            {
                throw new ArgumentException("Not all parameters for client certificate are specified",
                    nameof(ClientId));
            }

        }
        if (ParameterSetName == ParamSetInteractive)
        {
            if (NoBroker || RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
            {
                var interactiveOptions = new Azure.Identity.InteractiveBrowserCredentialOptions
                {
                    TenantId = TenantId,
                    ClientId = ClientId ?? DefaultClientId,
                    LoginHint = Username,
                    RedirectUri = new Uri("http://localhost:12228/"),
                    TokenCachePersistenceOptions = new Azure.Identity.TokenCachePersistenceOptions
                    {
                        Name = "WinTuner-PowerShell",
                        UnsafeAllowUnencryptedStorage = true,
                    },
                    DisableAutomaticAuthentication = false,

                };
                interactiveOptions.AdditionallyAllowedTenants.Add("*");

                var credential = new Azure.Identity.InteractiveBrowserCredential(interactiveOptions);

                // This is to make sure it will get a token before we start using it.
                // This will trigger the login screen early in the process.
                //var result = credential.Authenticate(new Azure.Core.TokenRequestContext(scopes!, tenantId: TenantId), cancellationToken);

                return new Microsoft.Graph.Authentication.AzureIdentityAuthenticationProvider(credential, isCaeEnabled: false, scopes: Scopes ?? DefaultScopes);
            }

            return new WingetIntune.Internal.Msal.InteractiveAuthenticationProvider(new WingetIntune.Internal.Msal.InteractiveAuthenticationProviderOptions
            {
                ClientId = ClientId,
                TenantId = TenantId,
                Username = Username,
                Scopes = Scopes ?? DefaultScopes,
            });
        }

        // This should never happen, but just in case.
        throw new NotImplementedException();
    }

    /// <summary>
    /// Asynchronously retrieves a token from the authentication provider.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the token string if successful; otherwise, null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the authentication provider is not set.</exception>
    internal static async ValueTask<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (AuthenticationProvider == null)
        {
            throw new InvalidOperationException("AuthenticationProvider is not set, please run Connect-WtWinTuner first.");
        }
        // This is a "hack" to get a token from the authentication provider.
        var ri = new RequestInformation(Method.GET, "https://graph.microsoft.com/test", new Dictionary<string, object>());
        await AuthenticationProvider.AuthenticateRequestAsync(ri, cancellationToken: cancellationToken);
        string? headerValue = ri.Headers.TryGetValue("Authorization", out var values) ? values.FirstOrDefault() : null;

        // Header should be in the format "Bearer <token>"
        // So we need to remove the "Bearer " part.
        int AuthenticationSchemeLength = AuthenticationScheme.Length + 1;
        return headerValue?.Length > AuthenticationSchemeLength && headerValue.StartsWith(AuthenticationScheme, StringComparison.InvariantCultureIgnoreCase) ? headerValue.Substring(AuthenticationSchemeLength) : null;
    }

    internal static Task ClearAuthentication(CancellationToken cancellationToken = default)
    {
        if (AuthenticationProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        AuthenticationProvider = null;
        return Task.CompletedTask;
    }

    private const string AuthenticationScheme = "Bearer";
}
