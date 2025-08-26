using Microsoft.Extensions.Options;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Svrooij.WinTuner.Proxy.Client;
internal class WinTunerProxyClientFactory
{
    private readonly WinTunerProxyClientOptions options;
    private readonly HttpClient httpClient;
    public WinTunerProxyClientFactory(HttpClient httpClient, IOptions<WinTunerProxyClientOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        if (this.options.BaseAddress is not null)
        {
            this.httpClient.BaseAddress = this.options.BaseAddress;
            this.httpClient.DefaultRequestHeaders.Add("User-Agent", "WinTuner.Proxy.Client/1.0");
        }
    }

    public WinTunerProxyClient GetClient(string? code = null) => new WinTunerProxyClient(new HttpClientRequestAdapter(new WinTunerProxyClientAuthenticationProvider(code ?? options.Code), httpClient: httpClient));
}
