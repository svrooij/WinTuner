using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace WingetIntune.Internal.Msal;

public partial class PublicClientAuth : IAuthenticationProvider
{
    private readonly PublicClientOptions _options;
    private readonly IPublicClientApplication publicClientApplication;
    private readonly ILogger<PublicClientAuth> logger;
    private bool CacheLoaded = false;
    private const string DefaultClientId = "d5a8a406-3b1d-4069-91cc-d76acdd812fe";
    private AccountSuggestion? accountSuggestion;
    private string[] Scopes = IntuneManager.RequiredScopes;
    private AuthenticationResult? authenticationResult;

    private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

    public PublicClientAuth(ILogger<PublicClientAuth> logger, IOptions<PublicClientOptions>? options)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.Value.ClientId))
        {
            _options = new PublicClientOptions { ClientId = DefaultClientId };
        }
        else
        {
            _options = options.Value;
        }

        if (_options.UseBroker)
        {
            publicClientApplication = PublicClientApplicationBuilder
                .Create(_options.ClientId)
                .WithDefaultRedirectUri()
                .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "Winget Intune uploader" })
                .Build();
        }
        else
        {
            publicClientApplication = PublicClientApplicationBuilder
                .Create(_options.ClientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
                .WithDefaultRedirectUri()
                .Build();
        }

        this.logger = logger;
    }

    private async Task LoadCache()
    {
        await semaphoreSlim.WaitAsync();

        if (CacheLoaded)
        {
            semaphoreSlim.Release();
            return;
        }

        var storageProperties = new StorageCreationPropertiesBuilder(".accounts", Path.Combine(Path.GetTempPath(), "wingetintune"))
            .Build();
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(publicClientApplication.UserTokenCache);
        CacheLoaded = true;
        semaphoreSlim.Release();

    }

    public void SetAccountSuggestion(AccountSuggestion accountSuggestion)
    {
        this.accountSuggestion = accountSuggestion;
    }

    public void SetScopes(string[] scopes)
    {
        Scopes = scopes;
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
            authenticationResult = await publicClientApplication.AcquireTokenSilent(scopes, account).ExecuteAsync(cancellationToken);
            LogAcquiredTokenSilently(scopes, tenantId ?? authenticationResult.TenantId, authenticationResult.Account.Username);
            return authenticationResult;
        }
        catch (MsalUiRequiredException)
        {
            return await AcquireTokenInteractiveAsync(scopes, tenantId, userId, cancellationToken);
        }
    }

    public async Task<AuthenticationResult> AcquireTokenInteractiveAsync(IEnumerable<string> scopes, string? tenantId = null, string? userId = null, CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = new CancellationTokenSource(30000);

        // Create a "LinkedTokenSource" combining two CancellationTokens into one.
        using var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token);

        if (!CacheLoaded)
            await LoadCache();
        LogAcquiringTokenInteractive(scopes, tenantId, userId);
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
            var token = await AccuireTokenAsync(Scopes, accountSuggestion?.TenantId, accountSuggestion?.Username, cancellationToken);
            var headers = new RequestHeaders
            {
                { "Authorization", $"Bearer {token.AccessToken}" }
            };
            request.AddHeaders(headers);
        }
    }

    [LoggerMessage(EventId = 100, Level = Microsoft.Extensions.Logging.LogLevel.Information, Message = "Acquiring token interactively {Scopes} {TenantId} {UserId}")]
    private partial void LogAcquiringTokenInteractive(IEnumerable<string> Scopes, string? TenantId, string? UserId);

    // logger.LogTrace("Acquired token silently {@scopes} {tenantId} {username}", scopes, tenantId ?? authenticationResult.TenantId, authenticationResult.Account.Username);
    [LoggerMessage(EventId = 101, Level = Microsoft.Extensions.Logging.LogLevel.Trace, Message = "Acquired token silently {Scopes} {TenantId} {UserId}")]
    private partial void LogAcquiredTokenSilently(IEnumerable<string> Scopes, string? TenantId, string? UserId);
}

public class PublicClientOptions
{
    public string? ClientId { get; set; }
    public bool UseBroker { get; set; } = true;
}

public record AccountSuggestion(string? TenantId, string? Username);
