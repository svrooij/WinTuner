using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Runtime.InteropServices;

namespace WingetIntune.Internal.Msal;

public sealed class InteractiveAuthenticationProvider : IAuthenticationProvider, IDisposable
{
    private readonly InteractiveAuthenticationProviderOptions _options;
    private readonly IPublicClientApplication publicClientApplication;
    private bool CacheLoaded = false;
    private const string DefaultClientId = "d5a8a406-3b1d-4069-91cc-d76acdd812fe";
    private AuthenticationResult? authenticationResult;

    private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

    public InteractiveAuthenticationProvider(InteractiveAuthenticationProviderOptions options)
    {
        if (options.Scopes is null || options.Scopes.Length == 0)
        {
            throw new ArgumentException("Scopes are required", nameof(options.Scopes));
        }


        _options = options;
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            _options.ClientId = DefaultClientId;
        }

        var builder = PublicClientApplicationBuilder
            .Create(_options.ClientId)
            .WithDefaultRedirectUri();

        if (!string.IsNullOrWhiteSpace(_options.TenantId))
        {
            builder.WithTenantId(_options.TenantId);
        }
        else
        {
            builder.WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs);
        }

        if (_options.UseBroker)
        {
            builder.WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "WinTuner" });
        }

        publicClientApplication = builder.Build();
    }

    private async Task LoadCache()
    {
        await semaphoreSlim.WaitAsync();
        if (CacheLoaded)
        {
            semaphoreSlim.Release();
            return;
        }
        var storageProperties = new StorageCreationPropertiesBuilder(".accounts", Path.Combine(Path.GetTempPath(), "wintuner"))
            .Build();
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(publicClientApplication.UserTokenCache);
        CacheLoaded = true;
        semaphoreSlim.Release();
    }

    public async Task<AuthenticationResult> AccuireTokenAsync(IEnumerable<string> scopes, string? tenantId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        if (!CacheLoaded)
            await LoadCache();

        if (authenticationResult is not null && authenticationResult.ExpiresOn > DateTimeOffset.UtcNow)
        {
            return authenticationResult;
        }

        var accounts = await publicClientApplication.GetAccountsAsync();
        bool tenantIsGuid = Guid.TryParse(tenantId, out _);
        var account = accounts.FirstOrDefault(a => (string.IsNullOrWhiteSpace(tenantId) || tenantIsGuid == false || a.HomeAccountId.TenantId == tenantId)
        && (string.IsNullOrEmpty(userId) || a.Username.Equals(userId, StringComparison.InvariantCultureIgnoreCase)));

        try
        {
            authenticationResult = account is null
                ? await publicClientApplication.AcquireTokenSilent(scopes, userId).ExecuteAsync(cancellationToken)
                : await publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
            return authenticationResult;
        }
        catch (MsalUiRequiredException)
        {
            return await AcquireTokenInteractiveAsync(scopes, tenantId, account?.Username ?? userId, cancellationToken);
        }
    }

    public async Task<AuthenticationResult> AcquireTokenInteractiveAsync(IEnumerable<string> scopes, string? tenantId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = new CancellationTokenSource(120000);

        // Create a "LinkedTokenSource" combining two CancellationTokens into one.
        using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);

        if (!CacheLoaded)
            await LoadCache();
        //logger.LogInformation("Acquiring token interactively {@scopes} {tenantId} {userId}", scopes, tenantId, userId);
        var builder = publicClientApplication.AcquireTokenInteractive(scopes);
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            builder = builder.WithTenantId(tenantId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            builder = builder.WithLoginHint(userId);
        }

        if (_options.UseBroker)
        {
            builder = builder.WithParentActivityOrWindow(BrokerHandle.GetConsoleOrTerminalWindow());
        }

        return authenticationResult = await builder.ExecuteAsync(combinedCancellation.Token);
    }

    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (request.URI.Host == "graph.microsoft.com")
        {
            var token = await AccuireTokenAsync(_options.Scopes!, _options.TenantId, _options.Username, cancellationToken);
            var headers = new RequestHeaders
            {
                { "Authorization", $"Bearer {token.AccessToken}" }
            };
            request.AddHeaders(headers);
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}

public class InteractiveAuthenticationProviderOptions
{
    public string[] Scopes { get; set; }
    public bool UseBroker { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public string? Username { get; set; }
    public string? ClientId { get; set; }
    public string? TenantId { get; set; }
}
