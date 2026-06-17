using Microsoft.Graph.Beta;
using Microsoft.Kiota.Abstractions.Authentication;

namespace WingetIntune.Graph;

public class GraphClientFactory
{
    private readonly System.Net.Http.HttpClient httpClient;
    public GraphClientFactory(System.Net.Http.HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public Microsoft.Graph.Beta.GraphServiceClient CreateClient(IAuthenticationProvider authenticationProvider)
    {
        return new GraphServiceClient(httpClient: httpClient, authenticationProvider: authenticationProvider);
    }
}
