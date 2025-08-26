using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Svrooij.WinTuner.Proxy.Client;
internal class WinTunerProxyClientAuthenticationProvider : IAuthenticationProvider
{
    private readonly string apiKey;
    private readonly string headerName;
    public WinTunerProxyClientAuthenticationProvider(string apiKey, string headerName = "x-functions-key")
    {
        this.apiKey = apiKey;
        this.headerName = headerName;
    }
    public Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        request.Headers.Add(headerName, apiKey);
        return Task.CompletedTask;
    }
}
