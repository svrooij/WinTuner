using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Text;
using System.Text.Json;

namespace WingetIntune.Graph;

public static class GraphServiceClientExtensions
{
    // These extensions are on the service client, not the request builder.
    // until this issue is resolved: https://github.com/microsoft/kiota-abstractions-dotnet/issues/113

    public static Task<Entity?> Intune_CreateWin32LobAppContentVersionAsync(this GraphServiceClient graphServiceClient, string win32LobAppId, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(win32LobAppId);
#endif
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.POST,
            URI = new Uri($"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileApps/{win32LobAppId}/microsoft.graph.win32LobApp/contentVersions"),
        };
        requestInfo.Headers.Add("Content-Type", "application/json");
        requestInfo.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        return graphServiceClient.RequestAdapter.SendAsync<Entity>(requestInfo, Entity.CreateFromDiscriminatorValue, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
    }

    public static Task<MobileAppContentFile?> Intune_CreateWin32LobAppContentVersionFileAsync(this GraphServiceClient graphServiceClient, string win32LobAppId, string contentVersionId, MobileAppContentFile mobileAppContentFile, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(win32LobAppId);
        ArgumentException.ThrowIfNullOrEmpty(contentVersionId);
        ArgumentNullException.ThrowIfNull(mobileAppContentFile);
#endif
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.POST,
            URI = new Uri($"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileApps/{win32LobAppId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files"),
        };
        requestInfo.SetContentFromParsable(graphServiceClient.RequestAdapter, "application/json", mobileAppContentFile);
        return graphServiceClient.RequestAdapter.SendAsync<MobileAppContentFile>(requestInfo, MobileAppContentFile.CreateFromDiscriminatorValue, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
    }

    public static Task<MobileAppContentFile?> Intune_GetWin32LobAppContentVersionFileAsync(this GraphServiceClient graphServiceClient, string win32LobAppId, string contentVersionId, string mobileAppContentFileId, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(win32LobAppId);
        ArgumentException.ThrowIfNullOrEmpty(contentVersionId);
        ArgumentException.ThrowIfNullOrEmpty(mobileAppContentFileId);
#endif
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            URI = new Uri($"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileApps/{win32LobAppId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files/{mobileAppContentFileId}"),
        };
        return graphServiceClient.RequestAdapter.SendAsync<MobileAppContentFile>(requestInfo, MobileAppContentFile.CreateFromDiscriminatorValue, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
    }

    public static async Task<MobileAppContentFile?> Intune_WaitForFinalCommitStateAsync(this GraphServiceClient graphServiceClient, string win32LobAppId, string contentVersionId, string mobileAppContentFileId, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(win32LobAppId);
        ArgumentException.ThrowIfNullOrEmpty(contentVersionId);
        ArgumentException.ThrowIfNullOrEmpty(mobileAppContentFileId);
#endif
        while (!cancellationToken.IsCancellationRequested)
        {
            MobileAppContentFile? result = await graphServiceClient.DeviceAppManagement.MobileApps[win32LobAppId].GraphWin32LobApp.ContentVersions[contentVersionId].Files[mobileAppContentFileId].GetAsync(cancellationToken: cancellationToken);
            //MobileAppContentFile? result = await graphServiceClient.Intune_GetWin32LobAppContentVersionFileAsync(win32LobAppId, contentVersionId, mobileAppContentFileId, cancellationToken)!;
            switch (result!.UploadState)
            {
                case MobileAppContentFileUploadState.CommitFileSuccess:
                    return result;

                case MobileAppContentFileUploadState.CommitFilePending:
                    await Task.Delay(1000, cancellationToken);
                    break;

                case MobileAppContentFileUploadState.CommitFileFailed:
                    throw new Exception("Commit failed");
                case MobileAppContentFileUploadState.CommitFileTimedOut:
                    throw new Exception("Commit timed out");

                default:
                    throw new Exception("Unexpected state");
            }
        }

        throw new TaskCanceledException();
    }

    public static Task Intune_CommitWin32LobAppContentVersionFileAsync(this GraphServiceClient graphServiceClient, string win32LobAppId, string contentVersionId, string mobileAppContentFileId, FileEncryptionInfo fileEncryptionInfo, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(win32LobAppId);
        ArgumentException.ThrowIfNullOrEmpty(contentVersionId);
        ArgumentException.ThrowIfNullOrEmpty(mobileAppContentFileId);
        ArgumentNullException.ThrowIfNull(fileEncryptionInfo);
#endif
        var body = new MobileAppContentFileCommitBody
        {
            FileEncryptionInfo = fileEncryptionInfo,
        };
        var data = JsonSerializer.SerializeToUtf8Bytes(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.POST,
            Content = new MemoryStream(data),
            URI = new Uri($"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileApps/{win32LobAppId}/microsoft.graph.win32LobApp/contentVersions/{contentVersionId}/files/{mobileAppContentFileId}/commit"),
        };
        requestInfo.Headers.Add("Content-Type", "application/json");
        return graphServiceClient.RequestAdapter.SendNoContentAsync(requestInfo, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
    }

    public static Task Intune_AddCategoryToApp(this GraphServiceClient graphServiceClient, string appId, string categoryId, CancellationToken cancellationToken)
    {
        var requestInfo = Intune_AddCategoryToApp_RequestInfo(graphServiceClient, appId, categoryId);

        return graphServiceClient.RequestAdapter.SendNoContentAsync(requestInfo, errorMapping: ErrorMapping, cancellationToken: cancellationToken);
    }

    public static RequestInformation Intune_AddCategoryToApp_RequestInfo(this GraphServiceClient graphServiceClient, string appId, string categoryId)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentException.ThrowIfNullOrEmpty(categoryId);
#endif
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.POST,
            URI = new Uri($"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileApps/{appId}/categories/$ref"),
        };

        var categoryReference = new Entity();
        categoryReference.AdditionalData.Add("@odata.id", $"{graphServiceClient.RequestAdapter.BaseUrl}/deviceAppManagement/mobileAppCategories/{categoryId}");
        // Body should look like '{"@odata.id":"https://graph.microsoft.com/beta/deviceAppManagement/mobileAppCategories/category-id-here"}'
        requestInfo.SetContentFromParsable(graphServiceClient.RequestAdapter, "application/json", categoryReference);
        return requestInfo;
    }

    public static Dictionary<string, ParsableFactory<IParsable>> ErrorMapping => new Dictionary<string, ParsableFactory<IParsable>> {
                {"4XX", ODataError.CreateFromDiscriminatorValue},
                {"5XX", ODataError.CreateFromDiscriminatorValue},
            };
}
