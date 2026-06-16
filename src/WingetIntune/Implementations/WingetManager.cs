using Microsoft.Extensions.Logging;
using WingetIntune.Models;

namespace WingetIntune.Implementations;

public partial class WingetManager : IWingetRepository
{
    private readonly ILogger<WingetManager> logger;
    private readonly IProcessManager processManager;
    private readonly IFileManager fileManager;

    public WingetManager(ILogger<WingetManager> logger, IProcessManager processManager, IFileManager fileManager)
    {
        this.logger = logger;
        this.processManager = processManager;
        this.fileManager = fileManager;
    }

    public async Task<Models.IsInstalledResult> CheckInstalled(string id, string? version, CancellationToken cancellationToken = default)
    {
        LogCheckInstalled(id, version);
        var result = await processManager.RunProcessAsync("winget", $"list --id {id} --exact --disable-interactivity --accept-source-agreements", cancellationToken);

        if (result.ExitCode != 0)
        {
            var exception = CreateExceptionForFailedProcess(result);
            LogErrorCheckInstalled(exception, id, version, result.Error);
            return IsInstalledResult.Error;
        }

        if (result.Output.Contains($"{id} "))
        {
            if (string.IsNullOrWhiteSpace(version) || result.Output.Contains($"{id} {version} "))
            {
                return IsInstalledResult.Installed;
            }
            else
            {
                return IsInstalledResult.UpgradeAvailable;
            }
        }
        return IsInstalledResult.NotInstalled;
    }

    public async Task<PackageInfo> GetPackageInfoAsync(string id, string? version, string? source, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNullOrEmpty(id);
#endif
        if (string.IsNullOrEmpty(version) || source != "winget")
        {
            return await GetPackageInfoFromWingetAsync(id, version, source, cancellationToken);
        }
        else
        {
            return await GetPackageInfoFromWingetManifestAsync(id, version, cancellationToken);
        }
    }

    private async Task<PackageInfo> GetPackageInfoFromWingetAsync(string id, string? version, string? source, CancellationToken cancellationToken = default)
    {
        // Show package info from winget like the Install command
        LogGetPackageInfo(id, version);
        var showArgs = WingetHelper.GetShowArgumentsForPackage(id, version, source);
        var result = await processManager.RunProcessAsync("winget", showArgs, cancellationToken);
        if (result?.ExitCode != 0)
        {
            var exception = CreateExceptionForFailedProcess(result);
            LogErrorGetPackageInfo(exception, id, version, result?.Error);
            throw exception;
        }

        var info = Models.PackageInfo.Parse(result.Output);
        if (info is null || string.IsNullOrWhiteSpace(info.PackageIdentifier))
        {
            var exception = new Exception($"Winget output not parsed, locale might be unsupported {id} {version}");
            LogErrorGetPackageInfo(exception, id, version, result?.Error + result?.Output);
            throw exception;
        }

        return info;
    }

    private async Task<PackageInfo> GetPackageInfoFromWingetManifestAsync(string id, string version, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNullOrEmpty(version);
#endif
        LogGetPackageInfoFromManifest(id, version);
        try
        {
            var mainUri = CreateManifestUri(id, version, null);
            var installerUri = CreateManifestUri(id, version, ".installer");
            var mainManifest = await fileManager.DownloadStringAsync(mainUri, cancellationToken: cancellationToken);
            var installerManifest = await fileManager.DownloadStringAsync(installerUri, cancellationToken: cancellationToken);

            var parser = new Winget.CommunityRepository.ManifestParser();

            var mainManifestObject = parser.ParseMainManifest(mainManifest!);
            var installerManifestObject = parser.ParseInstallerManifest(installerManifest!);

            var localizedManifestUri = CreateManifestUri(id, version, $".locale.{mainManifestObject.DefaultLocale}");
            var localizedManifest = await fileManager.DownloadStringAsync(localizedManifestUri, cancellationToken: cancellationToken);
            var localizedManifestObject = parser.ParseLocalizedManifest(localizedManifest!);

            installerManifestObject.Installers?.ForEach(i =>
            {
                if (i.Scope is null)
                {
                    i.Scope = installerManifestObject.Scope ?? "system";
                }
                if (i.InstallerSwitches is null && installerManifestObject.InstallerSwitches is not null)
                {
                    i.InstallerSwitches = installerManifestObject.InstallerSwitches;
                }
                if (i.InstallerType is null && installerManifestObject.InstallerType is not null)
                {
                    i.InstallerType = installerManifestObject.InstallerType;
                }
            });

            var installer = installerManifestObject.InstallerType
                ?? installerManifestObject.Installers.SingleOrDefault(InstallerType.Msi, Architecture.X64, InstallerContext.Unknown)?.InstallerType
                ?? installerManifestObject.Installers.SingleOrDefault(InstallerType.Wix, Architecture.X64, InstallerContext.Unknown)?.InstallerType
                ?? installerManifestObject.Installers.SingleOrDefault(InstallerType.Unknown, Architecture.X64, InstallerContext.Unknown)?.InstallerType
                ?? installerManifestObject.Installers?.FirstOrDefault()?.InstallerType;

            return new PackageInfo
            {
                Version = mainManifestObject.PackageVersion,
                DisplayName = localizedManifestObject.PackageName,
                Source = PackageSource.Winget,
                Publisher = localizedManifestObject.Publisher,
                PublisherUrl = localizedManifestObject.PublisherUrl,
                InformationUrl = localizedManifestObject.PackageUrl,
                SupportUrl = localizedManifestObject.PublisherSupportUrl,
                InstallerType = EnumParsers.ParseInstallerType(installer ?? "unknown"),
                InstallerContext = EnumParsers.ParseInstallerContext(installerManifestObject.Scope ?? "system"),
                Description = localizedManifestObject.Description ?? localizedManifestObject.ShortDescription,
                Installers = installerManifestObject.Installers,
                PackageIdentifier = mainManifestObject.PackageIdentifier,
            };
        }
        catch (Exception ex)
        {
            LogErrorGetPackageInfo(ex, id, version, ex.Message);
            throw;
        }
    }

    internal static string CreateManifestUri(string id, string version, string? addition)
    {
        var sanitizedId = id.Replace("+", "%2B");
        var idParts = sanitizedId.Split('.');
        var encodedVersion = Uri.EscapeDataString(version).Replace("+", "%2B");
        return $"https://github.com/microsoft/winget-pkgs/raw/master/manifests/{id[0].ToString().ToLower()}/{string.Join("/", idParts)}/{encodedVersion}/{sanitizedId}{addition}.yaml";
    }

    private static Exception CreateExceptionForFailedProcess(ProcessResult? processResult)
    {
        if (processResult?.ExitCode == 0)
        {
            throw new ArgumentException("Process exited with exitcode 0");
        }

        var exception = new Exception("Winget exited with non-zero exitcode");
        exception.Data.Add("ExitCode", processResult?.ExitCode);
        exception.Data.Add("Error", processResult?.Error);
        return exception;
    }

    public async Task<ProcessResult> Install(string id, string? version, string? source, bool force, CancellationToken cancellationToken = default)
    {
        LogInstall(id, version);
        var installArguments = WingetHelper.GetInstallArgumentsForPackage(id, version, source, force);
        return await processManager.RunProcessAsync("winget", installArguments, cancellationToken, true);
    }

    public async Task<ProcessResult> Upgrade(string id, string? version, string? source, bool force, CancellationToken cancellationToken = default)
    {
        LogUpgrade(id, version);
        var upgradeArgs = WingetHelper.GetUpgradeArgumentsForPackage(id, version, source, force);
        return await processManager.RunProcessAsync("winget", upgradeArgs, cancellationToken, true);
    }

    // Console.WriteLine($"Checking if package {id} {version} is installed");
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Checking if package {id} {version} is installed")]
    private partial void LogCheckInstalled(string id, string? version);

    // Console.WriteLine($"Upgrading package {id} {version}");
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Upgrading package {id} {version}")]
    private partial void LogUpgrade(string id, string? version);

    // Console.WriteLine($"Installing package {id} {version}");
    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Installing package {id} {version}")]
    private partial void LogInstall(string id, string? version);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "Getting package info for {id} {version}")]
    private partial void LogGetPackageInfo(string id, string? version);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Getting package info for {id} {version} from github")]
    private partial void LogGetPackageInfoFromManifest(string id, string? version);

    [LoggerMessage(EventId = 100, Level = LogLevel.Warning, Message = "Error getting package info for {id} {version}:\r\n{error}")]
    private partial void LogErrorGetPackageInfo(Exception exception, string id, string? version, string? error);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning, Message = "Error checking installed {id} {version}:\r\n{error}")]
    private partial void LogErrorCheckInstalled(Exception exception, string id, string? version, string error);
}
