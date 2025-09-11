using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WingetIntune.Implementations;
using WingetIntune.Models;

namespace WingetIntune.Commands;
public partial class ComputeBestInstallerForPackageCommand
{
    private readonly ILogger<ComputeBestInstallerForPackageCommand> _logger;

    public ComputeBestInstallerForPackageCommand(ILogger<ComputeBestInstallerForPackageCommand>? logger = null)
    {
        _logger = logger ?? new NullLogger<ComputeBestInstallerForPackageCommand>();
    }

    public void Execute(ref PackageInfo package, PackageOptions? packageOptions = null)
    {
        packageOptions ??= PackageOptions.Create();
        LogComputingBestInstaller(package.PackageIdentifier!, package.Version!, packageOptions.Architecture);
        var installer = package.GetBestInstaller(packageOptions);
        if (installer is null)
        {
            throw new ArgumentException($"No installer found for {package.PackageIdentifier} {package.Version} {packageOptions.Architecture}");
        }

        package.InstallerUrl = new Uri(installer.InstallerUrl!);
        package.InstallerFilename = Path.GetFileName(package.InstallerUrl.LocalPath.Replace(" ", ""));

        if (string.IsNullOrEmpty(package.InstallerFilename))
        {
            package.InstallerFilename = $"{package.PackageIdentifier}_{package.Version}.{GuessInstallerExtension(installer.ParseInstallerType())}";
        }

        // Maybe this should be done for other installers as well?
        if ((installer.InstallerType!.Equals("exe", StringComparison.OrdinalIgnoreCase) || installer.InstallerType!.Equals("burn", StringComparison.OrdinalIgnoreCase)) && package.InstallerFilename!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == false)
        {
            package.InstallerFilename += ".exe";
        }
        package.Hash = installer.InstallerSha256;
        package.Architecture = installer.InstallerArchitecture();
        package.InstallerContext = installer.ParseInstallerContext() == InstallerContext.Unknown ? (package.InstallerContext ?? packageOptions.InstallerContext) : installer.ParseInstallerContext();
        package.InstallerType = installer.ParseInstallerType();
        package.Installer = installer;
        package.MsiVersion ??= installer.AppsAndFeaturesEntries?.FirstOrDefault()?.DisplayVersion;
        package.MsiProductCode ??= installer.ProductCode ?? installer.AppsAndFeaturesEntries?.FirstOrDefault()?.ProductCode;
        if (!package.InstallerType.IsMsi() || packageOptions.PackageScript)
        {
            ComputeInstallerCommands(ref package, packageOptions);
        }
    }

    private static string GuessInstallerExtension(InstallerType installerType) => installerType switch
    {
        InstallerType.Inno => "exe",
        InstallerType.Msi => "msi",
        InstallerType.Msix => "msix",
        InstallerType.Appx => "appx",
        InstallerType.Burn => "exe",
        InstallerType.Wix => "msi",
        InstallerType.Nullsoft => "exe",
        InstallerType.Exe => "exe",
        InstallerType.Zip => "zip",
        _ => throw new ArgumentException("Unknown installer type", nameof(installerType))
    };

    private static readonly Dictionary<InstallerType, string> DefaultInstallerSwitches = new()
    {
        { InstallerType.Inno, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-" },
        { InstallerType.Burn, "/quiet /norestart /install" },
        { InstallerType.Nullsoft, "/S" },
    };

    /// <summary>
    /// Compute the installer commands for the package
    /// </summary>
    /// <param name="package">Package info</param>
    /// <param name="packageOptions">User-defined options</param>
    private void ComputeInstallerCommands(ref PackageInfo package, PackageOptions packageOptions)
    {
        // If package script is enabled, we will just use the winget install & uninstall commands
        // This way your packages in Intune will not contain the installer files
        // And it also helps with installers that otherwise would just not install silently or install at all
        if (packageOptions.PackageScript != true)
        {
            string? installerSwitches = packageOptions.OverrideArguments ?? package.Installer?.InstallerSwitches?.GetPreferred();
            switch (package.InstallerType)
            {
                case InstallerType.Inno:
                    if (installerSwitches?.Contains("/VERYSILENT") != true)
                    {
                        installerSwitches += " " + DefaultInstallerSwitches[InstallerType.Inno];
                        installerSwitches = installerSwitches.Trim();
                    }
                    package.InstallCommandLine = $"\"{package.InstallerFilename}\" {installerSwitches}";
                    // Don't know the uninstall command
                    // Configure the uninstall command for Inno Setup
                    //package.UninstallCommandLine = $"\"{package.InstallerFilename}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /D={{0}}";
                    break;

                case InstallerType.Burn:
                    if (installerSwitches?.Contains("/quiet") != true)
                    {
                        installerSwitches += " " + DefaultInstallerSwitches[InstallerType.Burn];
                        installerSwitches = string.Join(" ", installerSwitches.Split(' ').Distinct()).Trim();
                    }
                    package.InstallCommandLine = $"\"{package.InstallerFilename}\" {installerSwitches}";
                    // Have to check the uninstall command
                    package.UninstallCommandLine = $"\"{package.InstallerFilename}\" /quiet /norestart /uninstall /passive"; // /burn.ignoredependencies=\"{package.PackageIdentifier}\"
                    break;

                case InstallerType.Nullsoft:
                    package.InstallCommandLine = $"\"{package.InstallerFilename}\" {installerSwitches ?? DefaultInstallerSwitches[InstallerType.Nullsoft]}";
                    break;

                case InstallerType.Exe:
                    package.InstallCommandLine = $"\"{package.InstallerFilename}\" {installerSwitches}";
                    // Have to check the uninstall command
                    //package.UninstallCommandLine = $"\"{package.InstallerFilename}\" /quiet /norestart /uninstall /passive"; // /burn.ignoredependencies=\"{package.PackageIdentifier}\"
                    break;

                case InstallerType.Msix:
                    package.InstallCommandLine = $"Add-AppxPackage -Path \"{package.InstallerFilename}\" -Confirm -StubPackageOption InstallFull";
                    if (package.MsiProductCode is not null)
                        package.UninstallCommandLine = $"Remove-AppxPackage -Package \"{package.MsiProductCode}\" -Confirm";
                    break;
            }
        }

        // If the installer type is unsupported or the package script is enabled, we will generate a script to install the package
        if (string.IsNullOrWhiteSpace(package.InstallCommandLine))
        {
            var installArguments = WingetHelper.GetInstallArgumentsForPackage(package.PackageIdentifier!, package.Version, installerContext: package.InstallerContext ?? InstallerContext.Unknown);
            // This seems like a hack I know, but it's the only way to get the install command for now.
            package.InstallCommandLine = $"winget {installArguments}";
        }

        // Uninstall command is almost always empty, so we just use winget to uninstall the package
        if (string.IsNullOrWhiteSpace(package.UninstallCommandLine))
        {
            var uninstallArguments = WingetHelper.GetUninstallArgumentsForPackage(package.PackageIdentifier!, installerContext: package.InstallerContext ?? InstallerContext.Unknown);
            package.UninstallCommandLine = $"winget {uninstallArguments}";
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Computing best installer for {PackageIdentifier} {Version} {Architecture}")]
    private partial void LogComputingBestInstaller(string PackageIdentifier, string Version, Architecture Architecture);
}
