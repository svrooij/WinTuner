using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using WingetIntune.Intune;
using WingetIntune.Models;

namespace WingetIntune.Graph;
public partial class GraphAppUploader
{
    private readonly ILogger<GraphAppUploader> logger;
    private readonly IFileManager fileManager;
    private readonly IAzureFileUploader azureFileUploader;
    private readonly Mapper mapper = new();

    public GraphAppUploader(ILogger<GraphAppUploader> logger, IFileManager fileManager, IAzureFileUploader azureFileUploader)
    {
        this.logger = logger;
        this.fileManager = fileManager;
        this.azureFileUploader = azureFileUploader;
    }

    public async Task<Win32LobApp?> CreateNewAppAsync(GraphServiceClient graphServiceClient, Win32LobApp win32LobApp, string intunePackageFile, string? logoPath = null, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentNullException.ThrowIfNull(win32LobApp);
        ArgumentException.ThrowIfNullOrEmpty(intunePackageFile);
#endif
        if (!fileManager.FileExists(intunePackageFile))
        {
            throw new FileNotFoundException("IntuneWin file not found", intunePackageFile);
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            // Extract intunewin file to get the metadata file

            await fileManager.ExtractFileToFolderAsync(intunePackageFile, tempFolder, cancellationToken);
            var metadataFile = IntuneMetadata.GetMetadataPath(tempFolder);
            var intuneWinFile = IntuneMetadata.GetContentsPath(tempFolder);

            if (!fileManager.FileExists(metadataFile))
            {
                throw new FileNotFoundException("Metadata file not found", metadataFile);
            }
            if (!fileManager.FileExists(intuneWinFile))
            {
                throw new FileNotFoundException("IntuneWin file not found", intuneWinFile);
            }

            return await CreateNewAppAsync(graphServiceClient, win32LobApp, intuneWinFile, metadataFile, logoPath, cancellationToken);

        }
        finally
        {
            fileManager.DeleteFileOrFolder(tempFolder);
        }

    }

    public async Task<Win32LobApp?> CreateNewAppAsync(GraphServiceClient graphServiceClient, Win32LobApp win32LobApp, string partialIntuneWinFile, string metadataFile, string? logoPath = null, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentNullException.ThrowIfNull(win32LobApp);
        ArgumentException.ThrowIfNullOrEmpty(partialIntuneWinFile);
#endif
        if (!fileManager.FileExists(partialIntuneWinFile))
        {
            throw new FileNotFoundException("IntuneWin file not found", partialIntuneWinFile);
        }
        if (win32LobApp.LargeIcon is null && !string.IsNullOrEmpty(logoPath) && fileManager.FileExists(logoPath))
        {
            win32LobApp.LargeIcon = new MimeContent
            {
                Type = "image/png",
                Value = await fileManager.ReadAllBytesAsync(logoPath, cancellationToken)
            };
        }
        LogCreatingNewWin32LobApp();
        string? appId = null;
        try
        {
            Win32LobApp? app = await graphServiceClient.DeviceAppManagement.MobileApps.PostAsync(win32LobApp, cancellationToken);
            appId = app?.Id;

            // TODO: Maybe this delay is not needed? Have to test this.
            await Task.Delay(1000, cancellationToken);

            // Upload the content and update the app with the latest commited file id.
            await CreateNewContentVersionAsync(graphServiceClient, app!.Id!, partialIntuneWinFile, metadataFile, cancellationToken);

            // Load the app again to get the final state
            Win32LobApp? updatedApp = await graphServiceClient.DeviceAppManagement.MobileApps[app.Id].GetAsync(cancellationToken: cancellationToken) as Win32LobApp;

            return updatedApp;
        }
        catch (Microsoft.Identity.Client.MsalServiceException ex)
        {
            LogErrorPublishingAppAuthFailed(ex, ex.Message);
            throw;
        }
        catch (ODataError ex)
        {
            LogErrorPublishingAppWithCleanup(ex, ex.Error?.Message ?? "Unknown OData error");
            if (appId != null)
            {
                try
                {
                    await graphServiceClient.DeviceAppManagement.MobileApps[appId].DeleteAsync(cancellationToken: cancellationToken);
                }
                catch (Exception ex2)
                {
                    LogErrorDeletingApp(ex2);
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            LogErrorPublishingAppWithCleanup(ex, ex.Message);
            if (appId != null)
            {
                try
                {
                    // Do not use the cancellationToken here, we want to delete the app no matter what.
                    await graphServiceClient.DeviceAppManagement.MobileApps[appId].DeleteAsync(cancellationToken: CancellationToken.None);
                }
                catch (Exception ex2)
                {
                    LogErrorDeletingApp(ex2);
                }
            }
            throw;
        }
    }

    public async Task<Win32LobApp?> CreateNewContentVersionAsync(GraphServiceClient graphServiceClient, string appId, string intuneWinFile, CancellationToken cancellationToken = default)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            await fileManager.ExtractFileToFolderAsync(intuneWinFile, tempFolder, cancellationToken);
            var metadataFile = IntuneMetadata.GetMetadataPath(tempFolder);
            var partialIntuneWinFile = IntuneMetadata.GetContentsPath(tempFolder);

            if (!fileManager.FileExists(metadataFile))
            {
                throw new FileNotFoundException("Metadata file not found", metadataFile);
            }
            if (!fileManager.FileExists(intuneWinFile))
            {
                throw new FileNotFoundException("IntuneWin file not found", partialIntuneWinFile);
            }

            return await CreateNewContentVersionAsync(graphServiceClient, appId, partialIntuneWinFile, metadataFile, cancellationToken);
        }
        finally
        {
            fileManager.DeleteFileOrFolder(tempFolder);
        }
    }

    public async Task<Win32LobApp?> CreateNewContentVersionAsync(GraphServiceClient graphServiceClient, string appId, string partialIntuneWinFile, string metadataFile, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentException.ThrowIfNullOrEmpty(partialIntuneWinFile);
#endif
        if (!fileManager.FileExists(partialIntuneWinFile))
        {
            throw new FileNotFoundException("IntuneWin file not found", partialIntuneWinFile);
        }
        LogCreatingNewContentVersion(appId);

        // Load the metadata file
        var info = IntuneMetadata.GetApplicationInfo(await fileManager.ReadAllBytesAsync(metadataFile, cancellationToken))!;

        // Create the content version
        var contentVersion = await graphServiceClient.DeviceAppManagement.MobileApps[appId].GraphWin32LobApp.ContentVersions.PostAsync(new MobileAppContent(), cancellationToken: cancellationToken);
        //var contentVersion = await graphServiceClient.Intune_CreateWin32LobAppContentVersionAsync(appId, cancellationToken);
        LogCreatedContentVersion(contentVersion!.Id!);

        var mobileAppContentFileRequest = new MobileAppContentFile
        {
            Name = info.FileName,
            IsDependency = false,
            Size = info.UnencryptedContentSize,
            SizeEncrypted = fileManager.GetFileSize(partialIntuneWinFile),
            Manifest = null,
        };

        LogCreatingContentFile(mobileAppContentFileRequest.Name!, mobileAppContentFileRequest.Size!.Value, mobileAppContentFileRequest.SizeEncrypted!.Value);

        MobileAppContentFile? mobileAppContentFile = await graphServiceClient.DeviceAppManagement.MobileApps[appId].GraphWin32LobApp.ContentVersions[contentVersion.Id!].Files.PostAsync(mobileAppContentFileRequest, cancellationToken: cancellationToken);
        LogCreatedContentFile(mobileAppContentFile!.Id!);
        // Wait for a bit (it's generating the azure storage uri)
        await Task.Delay(3000, cancellationToken);

        MobileAppContentFile? updatedMobileAppContentFile = await graphServiceClient.DeviceAppManagement.MobileApps[appId].GraphWin32LobApp.ContentVersions[contentVersion.Id!].Files[mobileAppContentFile!.Id!].GetAsync(cancellationToken: cancellationToken);

        LogLoadedContentFile(updatedMobileAppContentFile!.Id!, updatedMobileAppContentFile.AzureStorageUri!);

        await azureFileUploader.UploadFileToAzureAsync(
            partialIntuneWinFile,
            new Uri(updatedMobileAppContentFile!.AzureStorageUri!),
            cancellationToken);

        LogUploadedContentFile(updatedMobileAppContentFile.Id!, updatedMobileAppContentFile.AzureStorageUri!);

        var encryptionInfo = mapper.ToFileEncryptionInfo(info.EncryptionInfo);
        await graphServiceClient.Intune_CommitWin32LobAppContentVersionFileAsync(appId,
            contentVersion!.Id!,
            mobileAppContentFile!.Id!,
            encryptionInfo,
            cancellationToken);

        MobileAppContentFile? commitedFile = await graphServiceClient.Intune_WaitForFinalCommitStateAsync(appId, contentVersion!.Id!, mobileAppContentFile!.Id!, cancellationToken);

        LogAddedContentVersion(contentVersion.Id!, appId);

        var app = await graphServiceClient.DeviceAppManagement.MobileApps[appId].PatchAsync(new Win32LobApp
        {
            CommittedContentVersion = contentVersion.Id,
        }, cancellationToken);

        return app;
    }

    [LoggerMessage(EventId = 300, Level = LogLevel.Debug, Message = "Creating new Win32LobApp")]
    private partial void LogCreatingNewWin32LobApp();

    [LoggerMessage(EventId = 301, Level = LogLevel.Error, Message = "Error publishing app, auth failed {Message}")]
    private partial void LogErrorPublishingAppAuthFailed(Exception ex, string Message);

    [LoggerMessage(EventId = 302, Level = LogLevel.Error, Message = "Error publishing app, deleting the remains {Message}")]
    private partial void LogErrorPublishingAppWithCleanup(Exception ex, string? Message = null);

    [LoggerMessage(EventId = 303, Level = LogLevel.Error, Message = "Error deleting app")]
    private partial void LogErrorDeletingApp(Exception ex);

    [LoggerMessage(EventId = 305, Level = LogLevel.Debug, Message = "Creating new content version for app {AppId}")]
    private partial void LogCreatingNewContentVersion(string AppId);

    [LoggerMessage(EventId = 306, Level = LogLevel.Debug, Message = "Created content version {Id}")]
    private partial void LogCreatedContentVersion(string Id);

    [LoggerMessage(EventId = 307, Level = LogLevel.Debug, Message = "Creating content file {Name} {Size} {SizeEncrypted}")]
    private partial void LogCreatingContentFile(string Name, long Size, long SizeEncrypted);

    [LoggerMessage(EventId = 308, Level = LogLevel.Debug, Message = "Created content file {Id}")]
    private partial void LogCreatedContentFile(string Id);

    [LoggerMessage(EventId = 309, Level = LogLevel.Debug, Message = "Loaded content file {Id} {BlobUri}")]
    private partial void LogLoadedContentFile(string Id, string BlobUri);

    [LoggerMessage(EventId = 310, Level = LogLevel.Debug, Message = "Uploaded content file {Id} {BlobUri}")]
    private partial void LogUploadedContentFile(string Id, string BlobUri);

    [LoggerMessage(EventId = 311, Level = LogLevel.Information, Message = "Added content version {ContentVersionId} to app {AppId}")]
    private partial void LogAddedContentVersion(string ContentVersionId, string AppId);
}
