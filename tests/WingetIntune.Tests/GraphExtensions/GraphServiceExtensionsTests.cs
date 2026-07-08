using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using System.Net;
using WingetIntune.Graph;
using WingetIntune.Internal.Msal;

namespace WingetIntune.Tests.GraphExtensions;

public class GraphServiceExtensionsTests
{
    [Fact]
    public async Task Intune_CreateWin32LobAppContentVersionAsync_MakesCorrectRequest()
    {
        var appId = Guid.NewGuid().ToString();
        var token = Guid.NewGuid().ToString();

        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContent"",
  ""id"": ""1""
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var call = handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/microsoft.graph.win32LobApp/contentVersions",
            HttpMethod.Post,
            "{}",
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));

        var result = await graphServiceClient.Intune_CreateWin32LobAppContentVersionAsync(appId, CancellationToken.None)!;
        Assert.Equal("1", result!.Id);
    }

    [Fact]
    public async Task Intune_CreateWin32LobAppContentVersionAsync_UsesConfiguredGraphBaseUrl()
    {
        var appId = Guid.NewGuid().ToString();
        var token = Guid.NewGuid().ToString();

        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContent"",
  ""id"": ""1""
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.us/beta/deviceAppManagement/mobileApps/{appId}/microsoft.graph.win32LobApp/contentVersions",
            HttpMethod.Post,
            "{}",
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(
            httpClient,
            new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token),
            GraphCloudEnvironments.USGov.GraphBetaEndpoint);

        var result = await graphServiceClient.Intune_CreateWin32LobAppContentVersionAsync(appId, CancellationToken.None)!;
        Assert.Equal("1", result!.Id);
    }

    [Fact]
    public async Task Intune_CreateWin32LobAppContentVersionFileAsync_MakesCorrectRequest()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContentFile"",
  ""azureStorageUri"": ""Azure Storage Uri value"",
  ""isCommitted"": true,
  ""id"": ""eab2e29b-e29b-eab2-9be2-b2ea9be2b2ea"",
  ""createdDateTime"": ""2017-01-01T00:02:43.5775965-08:00"",
  ""name"": ""Name value"",
  ""size"": 4,
  ""sizeEncrypted"": 13,
  ""azureStorageUriExpirationDateTime"": ""2017-01-01T00:00:08.4940464-08:00"",
  ""manifest"": ""bWFuaWZlc3Q="",
  ""uploadState"": ""transientError"",
  ""isFrameworkFile"": true,
  ""isDependency"": true
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files",
            HttpMethod.Post,
            "{\"name\":\"test\",\"size\":1}",
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));

        var mobileAppFileContent = new MobileAppContentFile
        {
            Name = "test",
            Size = 1,
        };

        var result = await graphServiceClient.Intune_CreateWin32LobAppContentVersionFileAsync(appId, contentVersionId.ToString(), mobileAppFileContent, CancellationToken.None)!;

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Intune_CommitWin32LobAppContentVersionFileAsync_MakesCorrectRequest()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();

        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContent"",
  ""id"": ""1""
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files/{fileId}/commit",
            HttpMethod.Post,
            @"{""fileEncryptionInfo"":{""encryptionKey"":""test"",""initializationVector"":null,""mac"":null,""macKey"":null,""profileIdentifier"":null,""fileDigest"":""test"",""fileDigestAlgorithm"":""test""}}",
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));

        var body = new WingetIntune.Graph.FileEncryptionInfo
        {
            EncryptionKey = "test",
            FileDigest = "test",
            FileDigestAlgorithm = "test",
        };

        await graphServiceClient.Intune_CommitWin32LobAppContentVersionFileAsync(appId, contentVersionId.ToString(), fileId, body, CancellationToken.None)!;
    }

    [Fact]
    public async Task Intune_GetWin32LobAppContentVersionFileAsync_MakesCorrectRequest()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();
        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContentFile"",
  ""azureStorageUri"": ""Azure Storage Uri value"",
  ""isCommitted"": true,
  ""id"": ""eab2e29b-e29b-eab2-9be2-b2ea9be2b2ea"",
  ""createdDateTime"": ""2017-01-01T00:02:43.5775965-08:00"",
  ""name"": ""Name value"",
  ""size"": 4,
  ""sizeEncrypted"": 13,
  ""azureStorageUriExpirationDateTime"": ""2017-01-01T00:00:08.4940464-08:00"",
  ""manifest"": ""bWFuaWZlc3Q="",
  ""uploadState"": ""transientError"",
  ""isFrameworkFile"": true,
  ""isDependency"": true
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files/{fileId}",
            HttpMethod.Get,
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));
        var result = await graphServiceClient.Intune_GetWin32LobAppContentVersionFileAsync(appId, contentVersionId.ToString(), fileId, CancellationToken.None)!;

        Assert.NotNull(result);

        Assert.Equal("Name value", result.Name);
        Assert.Equal(4, result.Size);
        Assert.Equal(13, result.SizeEncrypted);
    }

    [Fact()]
    public async Task Intune_WaitForFinalCommitStateAsync_ReturnsOnSuccess()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();
        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContentFile"",
  ""azureStorageUri"": ""Azure Storage Uri value"",
  ""isCommitted"": true,
  ""id"": ""eab2e29b-e29b-eab2-9be2-b2ea9be2b2ea"",
  ""createdDateTime"": ""2017-01-01T00:02:43.5775965-08:00"",
  ""name"": ""Name value"",
  ""size"": 4,
  ""sizeEncrypted"": 13,
  ""azureStorageUriExpirationDateTime"": ""2017-01-01T00:00:08.4940464-08:00"",
  ""manifest"": ""bWFuaWZlc3Q="",
  ""uploadState"": ""commitFileSuccess"",
  ""isFrameworkFile"": true,
  ""isDependency"": true
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/graph.win32LobApp/contentVersions/{contentVersionId}/files/{fileId}",
            HttpMethod.Get,
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));
        var result = await graphServiceClient.Intune_WaitForFinalCommitStateAsync(appId, contentVersionId.ToString(), fileId, default)!;

        Assert.NotNull(result);

        Assert.Equal("Name value", result.Name);
        Assert.Equal(4, result.Size);
        Assert.Equal(13, result.SizeEncrypted);
    }

    [Fact()]
    public async Task Intune_WaitForFinalCommitStateAsync_ThrowsOnFailure()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();
        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContentFile"",
  ""azureStorageUri"": ""Azure Storage Uri value"",
  ""isCommitted"": true,
  ""id"": ""eab2e29b-e29b-eab2-9be2-b2ea9be2b2ea"",
  ""createdDateTime"": ""2017-01-01T00:02:43.5775965-08:00"",
  ""name"": ""Name value"",
  ""size"": 4,
  ""sizeEncrypted"": 13,
  ""azureStorageUriExpirationDateTime"": ""2017-01-01T00:00:08.4940464-08:00"",
  ""manifest"": ""bWFuaWZlc3Q="",
  ""uploadState"": ""commitFileFailed"",
  ""isFrameworkFile"": true,
  ""isDependency"": true
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/graph.win32LobApp/contentVersions/{contentVersionId}/files/{fileId}",
            HttpMethod.Get,
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));
        await Assert.ThrowsAsync<Exception>(async () => await graphServiceClient.Intune_WaitForFinalCommitStateAsync(appId, contentVersionId.ToString(), fileId, CancellationToken.None)!);
    }

    [Fact()]
    public async Task Intune_WaitForFinalCommitStateAsync_ThrowsOnTimedOut()
    {
        var appId = Guid.NewGuid().ToString();
        int contentVersionId = 1;
        var token = Guid.NewGuid().ToString();
        var fileId = Guid.NewGuid().ToString();
        var handler = Substitute.For<HttpMessageHandlerWrapper>();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Content = new StringContent(@"{
  ""@odata.type"": ""#microsoft.graph.mobileAppContentFile"",
  ""azureStorageUri"": ""Azure Storage Uri value"",
  ""isCommitted"": true,
  ""id"": ""eab2e29b-e29b-eab2-9be2-b2ea9be2b2ea"",
  ""createdDateTime"": ""2017-01-01T00:02:43.5775965-08:00"",
  ""name"": ""Name value"",
  ""size"": 4,
  ""sizeEncrypted"": 13,
  ""azureStorageUriExpirationDateTime"": ""2017-01-01T00:00:08.4940464-08:00"",
  ""manifest"": ""bWFuaWZlc3Q="",
  ""uploadState"": ""commitFileTimedOut"",
  ""isFrameworkFile"": true,
  ""isDependency"": true
}");
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        handler.AddFakeResponse(
            $"https://graph.microsoft.com/beta/deviceAppManagement/mobileApps/{appId}/graph.win32LobApp/contentVersions/{contentVersionId}/files/{fileId}",
            HttpMethod.Get,
            response);

        var httpClient = new HttpClient(handler);
        var graphServiceClient = new GraphServiceClient(httpClient, new WingetIntune.Internal.Msal.StaticAuthenticationProvider(token));
        await Assert.ThrowsAsync<Exception>(async () => await graphServiceClient.Intune_WaitForFinalCommitStateAsync(appId, contentVersionId.ToString(), fileId, CancellationToken.None)!);
    }
}
