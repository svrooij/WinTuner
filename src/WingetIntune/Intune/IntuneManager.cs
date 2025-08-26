using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Abstractions.Serialization;
using System.Text;
using System.Text.Json;
using WingetIntune.Commands;
using WingetIntune.Graph;
using WingetIntune.Interfaces;
using WingetIntune.Internal.Msal;
using WingetIntune.Msi;
using WingetIntune.Intune;
using WingetIntune.Models;

namespace WingetIntune;

public partial class IntuneManager
{
    public static string[] RequiredScopes = new[] { "DeviceManagementConfiguration.ReadWrite.All", "DeviceManagementApps.ReadWrite.All" };
    private readonly ILogger<IntuneManager> logger;
    private readonly ILoggerFactory loggerFactory;
    private readonly IFileManager fileManager;
    private readonly IProcessManager processManager;
    private readonly IWingetRepository wingetRepository;
    private readonly HttpClient httpClient;
    private readonly Mapper mapper = new Mapper();
    private readonly IAzureFileUploader azureFileUploader;
    private readonly IIntunePackager intunePackager;
    private readonly PublicClientAuth publicClient;
    private readonly GraphAppUploader graphAppUploader;
    private readonly GraphStoreAppUploader graphStoreAppUploader;
    private readonly ComputeBestInstallerForPackageCommand computeBestInstallerForPackageCommand;

    public IntuneManager(ILoggerFactory? loggerFactory, IFileManager fileManager, IProcessManager processManager, HttpClient httpClient, IAzureFileUploader azureFileUploader, PublicClientAuth publicClient, IIntunePackager intunePackager, IWingetRepository wingetRepository, GraphAppUploader graphAppUploader, GraphStoreAppUploader graphStoreAppUploader, ComputeBestInstallerForPackageCommand computeBestInstallerForPackageCommand)
    {
        this.loggerFactory = loggerFactory ?? new NullLoggerFactory();
        this.logger = this.loggerFactory.CreateLogger<IntuneManager>();
        this.fileManager = fileManager;
        this.processManager = processManager;
        this.httpClient = httpClient;
        this.azureFileUploader = azureFileUploader;
        this.publicClient = publicClient;
        this.intunePackager = intunePackager;
        this.wingetRepository = wingetRepository;
        this.graphAppUploader = graphAppUploader;
        this.graphStoreAppUploader = graphStoreAppUploader;
        this.computeBestInstallerForPackageCommand = computeBestInstallerForPackageCommand;
    }

    public async Task<Models.WingetPackage> GenerateMsiPackage(string tempFolder, string outputFolder, Models.PackageInfo packageInfo, PackageOptions packageOptions, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(tempFolder);
        ArgumentException.ThrowIfNullOrEmpty(outputFolder);
        ArgumentNullException.ThrowIfNull(packageInfo);
        ArgumentNullException.ThrowIfNull(packageOptions);
#endif
        if (!packageInfo.InstallerType.IsMsi())
        {
            throw new ArgumentException("Package is not an MSI package", nameof(packageInfo));
        }
        if (packageInfo.Architecture == Architecture.Unknown)
        {
            computeBestInstallerForPackageCommand.Execute(ref packageInfo, packageOptions);
        }
        LogGeneratePackage(packageInfo.PackageIdentifier!, packageInfo.Version!, packageInfo.Architecture, packageInfo.InstallerContext, outputFolder);
        var packageTempFolder = fileManager.CreateFolderForPackage(tempFolder, packageInfo.PackageIdentifier!, packageInfo.Version!);
        var packageFolder = fileManager.CreateFolderForPackage(outputFolder, packageInfo.PackageIdentifier!, packageInfo.Version!, packageInfo.Architecture == Architecture.Arm64);
        var installerPath = await DownloadInstallerAsync(packageTempFolder, packageInfo, cancellationToken);
        LoadMsiDetails(installerPath, ref packageInfo, packageOptions.OverrideArguments, packageOptions.MsiProductCode, packageOptions.MsiVersion);
        var intunePackage = await intunePackager.CreatePackage(packageTempFolder, packageFolder, packageInfo.InstallerFilename!, packageInfo, packageOptions.PartialPackage, cancellationToken: cancellationToken);
        await DownloadLogoAsync(packageFolder, packageInfo.PackageIdentifier!, cancellationToken);
        await WriteReadmeAsync(packageFolder, packageInfo, cancellationToken);
        await WritePackageInfo(packageFolder, packageInfo, cancellationToken);

        return new Models.WingetPackage(packageInfo, packageFolder, intunePackage!) { InstallerArguments = packageInfo.InstallCommandLine?.Substring(packageInfo.InstallerFilename?.Length + 3 ?? 0), InstallerFile = packageInfo.InstallerFilename };
    }

    /// <summary>
    /// Generates an Intune package for a winget package
    /// </summary>
    /// <param name="tempFolder">Folder to temporary store files</param>
    /// <param name="outputFolder">Folder where the package should be</param>
    /// <param name="packageInfo">(Partial) information about the package</param>
    /// <param name="packageOptions">User-defined options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<Models.WingetPackage> GenerateInstallerPackage(string tempFolder, string outputFolder, Models.PackageInfo packageInfo, PackageOptions? packageOptions = null, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(tempFolder);
        ArgumentException.ThrowIfNullOrEmpty(outputFolder);
#endif
        if (packageOptions is null)
        {
            packageOptions = PackageOptions.Create();
        }

        if (packageInfo.Source != PackageSource.Winget)
        {
            throw new ArgumentException("Package is not a winget package", nameof(packageInfo));
        }

        if (!packageInfo.InstallersLoaded)
        {
            packageInfo = await wingetRepository.GetPackageInfoAsync(packageInfo.PackageIdentifier!, packageInfo.Version, "winget", cancellationToken);
        }

        computeBestInstallerForPackageCommand.Execute(ref packageInfo, packageOptions);

        if (packageInfo.InstallerType.IsMsi() && !packageOptions.PackageScript)
        {
            return await GenerateMsiPackage(tempFolder, outputFolder, packageInfo, packageOptions, cancellationToken);
        }
        LogGeneratePackage(packageInfo.PackageIdentifier!, packageInfo.Version!, packageInfo.Architecture, packageInfo.InstallerContext, outputFolder);
        return await GenerateNoneMsiInstaller(tempFolder, outputFolder, packageInfo, packageOptions, cancellationToken);
    }

    private async Task<WingetPackage> GenerateNoneMsiInstaller(string tempFolder, string outputFolder, PackageInfo packageInfo, PackageOptions packageOptions, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(tempFolder);
        ArgumentException.ThrowIfNullOrEmpty(outputFolder);
        ArgumentNullException.ThrowIfNull(packageInfo);
#endif
        var packageTempFolder = fileManager.CreateFolderForPackage(tempFolder, packageInfo.PackageIdentifier!, packageInfo.Version!);
        var packageFolder = fileManager.CreateFolderForPackage(outputFolder, packageInfo.PackageIdentifier!, packageOptions.Versionless ? "latest" : packageInfo.Version!, packageInfo.Architecture == Architecture.Arm64);

        if (packageOptions.Versionless)
        {
            var installScript = IntuneManagerConstants.GetWingetInstallCmd(packageInfo.PackageIdentifier!);
            await fileManager.WriteAllTextAsync(
                Path.Combine(packageTempFolder, "install.ps1"),
                installScript,
                cancellationToken);
            await fileManager.WriteAllTextAsync(
                Path.Combine(packageFolder, "install.ps1"),
                installScript,
                cancellationToken);
            packageInfo.InstallCommandLine = $"%windir%\\sysnative\\windowspowershell\\v1.0\\powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File install.ps1";
            packageInfo.InstallerFilename = "install.ps1";

            var uninstallScript = IntuneManagerConstants.GetWingetUninstallCmd(packageInfo.PackageIdentifier!);
            await fileManager.WriteAllTextAsync(
                Path.Combine(packageTempFolder, "uninstall.ps1"),
                uninstallScript,
                cancellationToken);
            await fileManager.WriteAllTextAsync(
                Path.Combine(packageFolder, "uninstall.ps1"),
                uninstallScript,
                cancellationToken);
            packageInfo.UninstallCommandLine = $"%windir%\\sysnative\\windowspowershell\\v1.0\\powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File uninstall.ps1";
        }
        else if (SupportedInstallers.Contains(packageInfo.InstallerType) && !packageOptions.PackageScript)
        {
            _ = await DownloadInstallerAsync(packageTempFolder, packageInfo, cancellationToken);
        }
        else
        {
            // Generate scripts
            if (packageInfo.InstallCommandLine!.StartsWith("winget"))
            {
                // WinGet is not always available in the context of the user and you need to make sure to run to correct version.
                // This is way there is a helper script that always discovers the correct winget location to use.
                var installScript = GetPsCommandContent(packageInfo.InstallCommandLine, "installed", $"Package {packageInfo.PackageIdentifier} v{packageInfo.Version} installed successfully", packageId: packageInfo.PackageIdentifier, action: "install");
                await fileManager.WriteAllTextAsync(
                    Path.Combine(packageTempFolder, "install.ps1"),
                    installScript,
                    cancellationToken);
                packageInfo.InstallCommandLine = $"%windir%\\sysnative\\windowspowershell\\v1.0\\powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File install.ps1";
                packageInfo.InstallerFilename = "install.ps1";

                // Also output the script to the output folder
                await fileManager.WriteAllTextAsync(
                    Path.Combine(packageFolder, "install.ps1"),
                    installScript,
                    cancellationToken);
            }
        }

        if (packageInfo.UninstallCommandLine!.StartsWith("winget"))
        {
            // Helper script to discover the correct winget location
            var uninstallScript = GetPsCommandContent(packageInfo.UninstallCommandLine, "uninstalled", $"Package {packageInfo.PackageIdentifier} uninstalled successfully", packageId: packageInfo.PackageIdentifier, action: "uninstall");
            await fileManager.WriteAllTextAsync(
                    Path.Combine(packageTempFolder, "uninstall.ps1"),
                    uninstallScript,
                    cancellationToken);
            packageInfo.UninstallCommandLine = $"%windir%\\sysnative\\windowspowershell\\v1.0\\powershell.exe -WindowStyle Hidden -ExecutionPolicy Bypass -File uninstall.ps1";

            // Also output the script to the output folder
            await fileManager.WriteAllTextAsync(
                Path.Combine(packageFolder, "uninstall.ps1"),
                uninstallScript,
                cancellationToken);
        }

        var intuneFile = await intunePackager.CreatePackage(packageTempFolder, packageFolder, packageInfo.InstallerFilename!, packageInfo, packageOptions.PartialPackage, cancellationToken: cancellationToken);
        await DownloadLogoAsync(packageFolder, packageInfo.PackageIdentifier!, cancellationToken);

        var detectionScript = IntuneManagerConstants.GetPsDetectionCommand(packageInfo.PackageIdentifier!, packageOptions.Versionless ? "" : packageInfo.Version!);
        await fileManager.WriteAllTextAsync(
            Path.Combine(packageFolder, "detection.ps1"),
            detectionScript,
            cancellationToken);
        packageInfo.DetectionScript = detectionScript;
        if (packageOptions.Versionless)
        {
            // For a versionless package you don't want the version in the package info.
            packageInfo.Version = "latest";
        }
        await WritePackageInfo(packageFolder, packageInfo, cancellationToken);
        await WriteReadmeAsync(packageFolder, packageInfo, cancellationToken);

        var result = new WingetPackage(packageInfo, packageFolder, intuneFile)
        {
            InstallerFile = packageInfo.InstallerFilename,
            InstallerArguments = packageInfo.InstallCommandLine!.StartsWith(packageInfo.InstallerFilename!)
                ? packageInfo.InstallCommandLine!.Substring(packageInfo.InstallerFilename!.Length)
                : packageInfo.InstallCommandLine,

        };
        if (result.InstallerArguments!.StartsWith(IntuneManagerConstants.PowerShellPath))
        {
            result.InstallerArguments = result.InstallerArguments.Substring(IntuneManagerConstants.PowerShellPath.Length + 1);
            result.InstallCommand = IntuneManagerConstants.PowerShellPath;
        }
        return result;
    }

    private static string GetPsCommandContent(string command, string successSearch, string message, string? packageId = null, string? action = null)
    {
        var commandSplitted = command.Split(" ");
        string commandWithQuotes = "";
        if (commandSplitted.Length > 1 && commandSplitted[0].Equals("winget", StringComparison.OrdinalIgnoreCase))
        {
            commandWithQuotes = "$(Get-WingetCmd) ";
            commandSplitted = commandSplitted.Skip(1).ToArray();
        }
        commandWithQuotes += string.Join(" ", commandSplitted.Select(x => $"\"{x}\""));
        return IntuneManagerConstants.GetPsWingetCmd(action ?? "install", commandWithQuotes, successSearch, message, packageId);
    }

    public Task<PackageInfo> LoadPackageInfoFromFolder(string packageFolder, string packageId, string version, CancellationToken cancellationToken = default)
    {
        var packageFile = Path.Combine(packageFolder, packageId, version, "app.json");
        return LoadPackageInfoFromFile(packageFile, cancellationToken);
    }

    public async Task<PackageInfo> LoadPackageInfoFromFile(string packageFile, CancellationToken cancellationToken = default)
    {
        if (!fileManager.FileExists(packageFile))
        {
            throw new FileNotFoundException("Package file not found", packageFile);
        }

        var data = await fileManager.ReadAllBytesAsync(packageFile, cancellationToken);
        return JsonSerializer.Deserialize<PackageInfo>(data, MyJsonContext.Default.PackageInfo)!;
    }

    public async Task<MobileApp> PublishAppAsync(string packagesFolder, PackageInfo packageInfo, IntunePublishOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(packagesFolder);
        ArgumentNullException.ThrowIfNull(packageInfo);
        ArgumentNullException.ThrowIfNull(options);
#endif
        if (packageInfo.Source == PackageSource.Store)
        {
            return await PublishStoreAppAsync(options, packageId: packageInfo.PackageIdentifier, cancellationToken: cancellationToken);
        }

        GraphServiceClient graphServiceClient = CreateGraphClientFromOptions(options);

        Win32LobApp? app = mapper.ToWin32LobApp(packageInfo);

        var packageFolder = Path.Join(packagesFolder, packageInfo.PackageIdentifier!, packageInfo.Version!);
        var logoFile = Path.Combine(packageFolder, "..", "logo.png");
        var intuneFilePath = Path.Combine(packageFolder, Path.GetFileNameWithoutExtension(packageInfo.InstallerFilename!) + ".intunewin");

        app = await graphAppUploader.CreateNewAppAsync(graphServiceClient, app, intuneFilePath, logoFile, cancellationToken);

        if (options.Categories != null && options.Categories.Any())
        {
            await AddCategoriesToApp(graphServiceClient, app!.Id!, options.Categories, cancellationToken);
        }

        if (options.AvailableFor.Any() || options.RequiredFor.Any() || options.UninstallFor.Any())
        {
            await AssignAppAsync(graphServiceClient, app!.Id!, options.RequiredFor, options.AvailableFor, options.UninstallFor, options.AddAutoUpdateSetting, cancellationToken);
        }
        return app!;
    }

    public async Task<string> AddContentVersionToApp(IntunePublishOptions publishOptions, string appId, string intuneFilePath, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(publishOptions);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentException.ThrowIfNullOrEmpty(intuneFilePath);
#endif
        var graphServiceClient = CreateGraphClientFromOptions(publishOptions);

        return await AddContentVersionToApp(graphServiceClient, appId, intuneFilePath, cancellationToken);
    }

    internal async Task<string> AddContentVersionToApp(GraphServiceClient graphServiceClient, string appId, string intuneFilePath, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentException.ThrowIfNullOrEmpty(intuneFilePath);
#endif
        if (!fileManager.FileExists(intuneFilePath))
        {
            throw new FileNotFoundException("IntuneWin file not found", intuneFilePath);
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        LogExtractingIntuneWinFile(intuneFilePath, tempFolder);
        await fileManager.ExtractFileToFolderAsync(intuneFilePath, tempFolder, cancellationToken);

        var info = IntuneMetadata.GetApplicationInfo(await fileManager.ReadAllBytesAsync(IntuneMetadata.GetMetadataPath(tempFolder), cancellationToken))!;
        var intuneFileData = await fileManager.ReadAllBytesAsync(IntuneMetadata.GetContentsPath(tempFolder), cancellationToken);

        var contentVersion = await graphServiceClient.Intune_CreateWin32LobAppContentVersionAsync(appId, cancellationToken);

        LogCreatedContentVersion(contentVersion!.Id!);

        var mobileAppContentFileRequest = new MobileAppContentFile
        {
            Name = info.FileName,
            IsDependency = false,
            Size = info.UnencryptedContentSize,
            SizeEncrypted = intuneFileData.LongLength,
            Manifest = null,
        };

        LogCreatingContentFile(mobileAppContentFileRequest.Name!, mobileAppContentFileRequest.Size!.Value, mobileAppContentFileRequest.SizeEncrypted!.Value);

        var mobileAppContentFile = await graphServiceClient.Intune_CreateWin32LobAppContentVersionFileAsync(appId, contentVersion.Id!, mobileAppContentFileRequest, cancellationToken);

        LogCreatedContentFile(mobileAppContentFile!.Id!);
        // Wait for a bit (it's generating the azure storage uri)
        await Task.Delay(3000, cancellationToken);

        MobileAppContentFile? updatedMobileAppContentFile = await graphServiceClient.Intune_GetWin32LobAppContentVersionFileAsync(appId,
            contentVersion!.Id!,
            mobileAppContentFile!.Id!,
            cancellationToken);

        LogLoadedContentFile(updatedMobileAppContentFile!.Id!, updatedMobileAppContentFile.AzureStorageUri!);

        await azureFileUploader.UploadFileToAzureAsync(
            IntuneMetadata.GetContentsPath(tempFolder),
            new Uri(updatedMobileAppContentFile!.AzureStorageUri!),
            cancellationToken);

        LogUploadedContentFile(updatedMobileAppContentFile.Id!, updatedMobileAppContentFile.AzureStorageUri!);
        fileManager.DeleteFileOrFolder(tempFolder);

        await Task.Delay(5000, cancellationToken);

        var encryptionInfo = mapper.ToFileEncryptionInfo(info.EncryptionInfo);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            LogMappedEncryptionInfo(JsonSerializer.Serialize(encryptionInfo));
        }

        // Commit the file
        await graphServiceClient.Intune_CommitWin32LobAppContentVersionFileAsync(appId,
            contentVersion!.Id!,
            mobileAppContentFile!.Id!,
            encryptionInfo,
            cancellationToken);

        LogCommittedContentFile(mobileAppContentFile.Id!);

        MobileAppContentFile? commitedFile = await graphServiceClient.Intune_WaitForFinalCommitStateAsync(appId, contentVersion!.Id!, mobileAppContentFile!.Id!, cancellationToken);

        LogAddedContentVersion(contentVersion.Id!, appId);
        return contentVersion.Id!;
    }

    public async Task<WinGetApp> PublishStoreAppAsync(IntunePublishOptions options, string? packageId = null, string? searchString = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(packageId) && string.IsNullOrEmpty(searchString))
        {
            throw new ArgumentException("Either id or searchString must be specified");
        }

        if (!string.IsNullOrEmpty(searchString) && !string.IsNullOrEmpty(packageId))
        {
            throw new ArgumentException("Only one of id or searchString must be specified");
        }

        if (!string.IsNullOrEmpty(searchString))
        {
            var id = await graphStoreAppUploader.GetStoreIdForNameAsync(searchString, cancellationToken);
            return await PublishStoreAppAsync(options, id, null, cancellationToken);
        }

        GraphServiceClient graphServiceClient = CreateGraphClientFromOptions(options);

        try
        {
            var appCreated = await graphStoreAppUploader.CreateStoreAppAsync(graphServiceClient, packageId!, cancellationToken);
            if (appCreated == null)
            {
                throw new Exception("App was not created");
            }
            if (options.Categories != null && options.Categories.Any())
            {
                await graphServiceClient.AddIntuneCategoriesToAppAsync(appCreated!.Id!, options.Categories, cancellationToken);
            }
            if (options.AvailableFor.Any() || options.RequiredFor.Any() || options.UninstallFor.Any())
            {
                await graphServiceClient.AssignAppAsync(appCreated!.Id!, options.RequiredFor, options.AvailableFor, options.UninstallFor, options.AddAutoUpdateSetting, cancellationToken);
            }
            return appCreated;
        }
        catch (ODataError ex)
        {
            LogErrorPublishingApp(ex, ex.Error?.Message ?? "Unknown OData error");
            throw;
        }
        catch (Exception ex)
        {
            LogErrorPublishingAppGeneral(ex);
            throw;
        }
    }

    private async Task AddCategoriesToApp(GraphServiceClient graphServiceClient, string appId, string[] categories, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentNullException.ThrowIfNull(categories);
#endif
        LogAddingCategories(string.Join(",", categories), appId);

        try
        {
            await GraphWorkflows.AddIntuneCategoriesToAppAsync(graphServiceClient, appId, categories, cancellationToken);
        }
        catch (Exception ex)
        {
            LogErrorAddingCategories(ex);
            // Don't throw, just continue.
            //throw;
        }
    }

    private async Task AssignAppAsync(GraphServiceClient graphServiceClient, string appId, string[]? requiredFor, string[]? availableFor, string[]? uninstallFor, bool addAutoUpdateSetting, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(graphServiceClient);
        ArgumentException.ThrowIfNullOrEmpty(appId);
        ArgumentNullException.ThrowIfNull(cancellationToken);
#endif
        try
        {
            var assignments = await GraphWorkflows.AssignAppAsync(graphServiceClient, appId, requiredFor, availableFor, uninstallFor, addAutoUpdateSetting, cancellationToken);
            LogAssignedApp(appId, assignments);
        }
        catch (Exception ex)
        {
            LogErrorAssigningApp(ex);
            // Don't throw, just continue.
            //throw;
        }
    }

    internal async Task DownloadLogoAsync(string packageFolder, string packageId, CancellationToken cancellationToken)
    {
        var logoPath = Path.GetFullPath(Path.Combine(packageFolder, "..", "logo.png"));
        try
        {
            var logoUri = $"https://api.winstall.app/icons/{packageId}.png";//new Uri($"https://winget.azureedge.net/cache/icons/48x48/{packageId}.png");
            LogDownloadLogo(logoUri);
            await fileManager.DownloadFileAsync(logoUri, logoPath, throwOnFailure: false, overrideFile: false, cancellationToken: cancellationToken);
        }
        catch (Exception e)
        {
            LogErrorDownloadingLogo(e, packageId, logoPath);
        }

    }

    internal async Task<string> DownloadInstallerAsync(string tempPackageFolder, PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        var installerPath = Path.Combine(tempPackageFolder, packageInfo.InstallerFilename!);
        LogDownloadInstaller(packageInfo.InstallerUrl!, installerPath);
        await fileManager.DownloadFileAsync(packageInfo.InstallerUrl!.ToString(), installerPath, expectedHash: packageInfo.Installer!.InstallerSha256!, throwOnFailure: true, overrideFile: false, cancellationToken: cancellationToken);
        return installerPath;
    }

    public static (string?, string?) GetMsiInfo(string setupFile, ILogger? logger)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(setupFile);
#endif
        try
        {
            var decoder = new MsiDecoder(setupFile);
            return (decoder.GetCode(), decoder.GetVersion());
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error getting product code from {SetupFile} ignoring error, is this actually an MSI? Provide 'MsiProductCode'", setupFile);
        }

        return (null, null);
    }

    private void LoadMsiDetails(string installerPath, ref PackageInfo packageInfo, string? overrideInstallerArguments = null, string? msiProductCode = null, string? msiVersion = null)
    {
        if (!string.IsNullOrEmpty(msiProductCode))
        {
            packageInfo.MsiProductCode = msiProductCode;
        }
        if (!string.IsNullOrEmpty(msiVersion))
        {
            packageInfo.MsiVersion = msiVersion;
        }
        if (string.IsNullOrEmpty(packageInfo.MsiProductCode) || string.IsNullOrEmpty(packageInfo.MsiVersion))
        {
            var (loadedMsiProductCode, loadedMsiVersion) = GetMsiInfo(installerPath, logger);
            packageInfo.MsiProductCode ??= loadedMsiProductCode ?? packageInfo.MsiProductCode ?? throw new ArgumentException("MsiProductCode is required for MSI packages, one is not found in the installer or provided");
            packageInfo.MsiVersion ??= loadedMsiVersion ?? packageInfo.MsiVersion;
        }
        packageInfo.InstallCommandLine = $"msiexec /i {packageInfo.InstallerFilename!} " + (overrideInstallerArguments ?? "/qn /norestart");
        packageInfo.UninstallCommandLine = $"msiexec /x {packageInfo.MsiProductCode} /qn /norestart";
    }

    private static readonly InstallerType[] SupportedInstallers = new[] { InstallerType.Inno, InstallerType.Msi, InstallerType.Burn, InstallerType.Wix, InstallerType.Nullsoft, InstallerType.Exe };

    private async Task WriteReadmeAsync(string packageFolder, PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        if (packageInfo.InstallerType.IsMsi() || !string.IsNullOrEmpty(packageInfo.MsiProductCode))
        {
            LogWritingDetectionInfo(packageInfo.PackageIdentifier!, packageInfo.MsiProductCode!);

            sb.AppendFormat("Package {0} {1} from {2}\r\n", packageInfo.PackageIdentifier, packageInfo.Version, packageInfo.Source);
            sb.AppendLine();
            sb.AppendFormat("MsiProductCode={0}\r\n", packageInfo.MsiProductCode);
            sb.AppendFormat("MsiVersion={0}\r\n", packageInfo.MsiVersion);

            var detectionFile = Path.Combine(packageFolder, "detection.txt");
            await fileManager.WriteAllTextAsync(detectionFile, sb.ToString(), cancellationToken);
            sb.Clear();
        }

        LogWritingPackageReadme(packageInfo.PackageIdentifier!);
        sb.AppendFormat("Package {0} {1} from {2}\r\n", packageInfo.PackageIdentifier, packageInfo.Version, packageInfo.Source);
        sb.AppendLine();
        sb.AppendFormat("Display name: {0}\r\n", packageInfo.DisplayName);
        sb.AppendFormat("Publisher: {0}\r\n", packageInfo.Publisher);
        sb.AppendFormat("Homepage: {0}\r\n", packageInfo.InformationUrl);
        sb.AppendLine();
        sb.AppendLine("Install script:");
        if (packageInfo.InstallerType.IsMsi())
        {
            sb.AppendFormat("msiexec /i {0} /quiet /qn\r\n", packageInfo.InstallerFilename);
        }
        else
        {
            sb.AppendFormat("{0}\r\n", packageInfo.InstallCommandLine);
        }
        sb.AppendLine();
        sb.AppendLine("Uninstall script:");
        if (packageInfo.InstallerType.IsMsi() || !string.IsNullOrEmpty(packageInfo.MsiProductCode))
        {
            sb.AppendFormat("msiexec /x {0} /quiet /qn\r\nor\r\n", packageInfo.MsiProductCode);
        }

        if (!string.IsNullOrEmpty(packageInfo.UninstallCommandLine))
        {
            sb.AppendFormat("{0}\r\n", packageInfo.UninstallCommandLine);
        }

        sb.AppendLine();
        sb.AppendLine("Description:");
        sb.AppendLine(packageInfo.Description);

        var readme = Path.Combine(packageFolder, "readme.txt");
        await fileManager.WriteAllTextAsync(readme, sb.ToString(), cancellationToken);
    }

    private async Task WritePackageInfo(string packageFolder, PackageInfo packageInfo, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(packageInfo, MyJsonContext.Default.PackageInfo);
        var jsonFile = Path.Combine(packageFolder, "app.json");
        await fileManager.WriteAllBytesAsync(jsonFile, json, cancellationToken);

        var win32File = Path.Combine(packageFolder, "win32LobApp.json");

        var win32App = mapper.ToWin32LobApp(packageInfo);
        var logoPath = Path.Combine(packageFolder, "..", "logo.png");
        if (fileManager.FileExists(logoPath))
        {
            var logoBytes = await fileManager.ReadAllBytesAsync(logoPath, cancellationToken);
            if (logoBytes.Length > 0)
            {
                win32App.LargeIcon = new MimeContent
                {
                    Type = "image/png",
                    Value = logoBytes
                };
            }
        }
        else
        {
            LogNoLogoFound(packageInfo.PackageIdentifier!, Path.GetFullPath(logoPath));
        }

        using var stream = new MemoryStream();
        using var jsonStream = KiotaSerializer.SerializeAsStream("application/json", win32App, false);
        await jsonStream.CopyToAsync(stream, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        await fileManager.WriteAllBytesAsync(win32File, stream.ToArray(), cancellationToken);
    }

    private GraphServiceClient CreateGraphClientFromOptions(IntunePublishOptions options)
    {
        IAuthenticationProvider provider = publicClient;
        if (!string.IsNullOrEmpty(options.Username) || !string.IsNullOrEmpty(options.Tenant))
        {
            publicClient.SetAccountSuggestion(new AccountSuggestion(options.Tenant, options.Username));
        }
        if (options.Credential is not null)
        {
            provider = new Microsoft.Graph.Authentication.AzureIdentityAuthenticationProvider(options.Credential, null, null, isCaeEnabled: false, RequiredScopes);
        }
        else if (!string.IsNullOrEmpty(options.Token))
        {
            provider = new StaticAuthenticationProvider(options.Token);
        }
        return new GraphServiceClient(httpClient, provider, "https://graph.microsoft.com/beta");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Generating IntuneWin package for {PackageId} {Version} {Architecture} {Context} in {OutputFolder}")]
    private partial void LogGeneratePackage(string PackageId, string Version, Architecture? Architecture, InstallerContext? Context, string OutputFolder);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Downloading content prep tool from {ContentPrepUri}")]
    private partial void LogDownloadContentPrepTool(Uri ContentPrepUri);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Downloading installer from {InstallerUri} to {Path}")]
    private partial void LogDownloadInstaller(Uri InstallerUri, string Path);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Generating IntuneWin package from {TempPackageFolder} to {OutputFolder} with {InstallerFilename}")]
    private partial void LogGenerateIntuneWinFile(string TempPackageFolder, string OutputFolder, string InstallerFilename);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Downloading logo from {LogoUri}")]
    private partial void LogDownloadLogo(string LogoUri);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Extracting intunewin file {File} to {TempFolder}")]
    private partial void LogExtractingIntuneWinFile(string File, string TempFolder);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Created content version {Id}")]
    private partial void LogCreatedContentVersion(string Id);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Creating content file {Name} {Size} {SizeEncrypted}")]
    private partial void LogCreatingContentFile(string Name, long Size, long SizeEncrypted);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "Created content file {Id}")]
    private partial void LogCreatedContentFile(string Id);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Loaded content file {Id} {BlobUri}")]
    private partial void LogLoadedContentFile(string Id, string BlobUri);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Uploaded content file {Id} {BlobUri}")]
    private partial void LogUploadedContentFile(string Id, string BlobUri);

    // Using serialization here, so IsEnabled is already checked.
    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "Mapped encryption info {EncryptionInfo}", SkipEnabledCheck = true)]
    private partial void LogMappedEncryptionInfo(string EncryptionInfo);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "Committed content file {Id}")]
    private partial void LogCommittedContentFile(string Id);

    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Added content version {ContentVersionId} to app {AppId}")]
    private partial void LogAddedContentVersion(string ContentVersionId, string AppId);

    [LoggerMessage(EventId = 16, Level = LogLevel.Error, Message = "Error publishing app {Message}")]
    private partial void LogErrorPublishingApp(Exception ex, string Message);

    [LoggerMessage(EventId = 17, Level = LogLevel.Error, Message = "Error publishing app")]
    private partial void LogErrorPublishingAppGeneral(Exception ex);

    [LoggerMessage(EventId = 18, Level = LogLevel.Debug, Message = "Adding categories {Categories} to app {AppId}")]
    private partial void LogAddingCategories(string Categories, string AppId);

    [LoggerMessage(EventId = 19, Level = LogLevel.Error, Message = "Error adding categories to app")]
    private partial void LogErrorAddingCategories(Exception ex);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "Assigned app {AppId} to {AssignmentCount} assignments")]
    private partial void LogAssignedApp(string AppId, int AssignmentCount);

    [LoggerMessage(EventId = 21, Level = LogLevel.Error, Message = "Error assigning app to groups")]
    private partial void LogErrorAssigningApp(Exception ex);

    [LoggerMessage(EventId = 22, Level = LogLevel.Warning, Message = "Error downloading logo for {PackageId}, place your own logo here: {LogoPath}")]
    private partial void LogErrorDownloadingLogo(Exception ex, string PackageId, string LogoPath);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Writing detection info with msi details {PackageId} {ProductCode}")]
    private partial void LogWritingDetectionInfo(string PackageId, string ProductCode);

    [LoggerMessage(EventId = 24, Level = LogLevel.Debug, Message = "Writing package readme for package {PackageId}")]
    private partial void LogWritingPackageReadme(string PackageId);

    [LoggerMessage(EventId = 25, Level = LogLevel.Warning, Message = "No logo found for {PackageId}, place your image at {LogoPath}")]
    private partial void LogNoLogoFound(string PackageId, string LogoPath);
}
