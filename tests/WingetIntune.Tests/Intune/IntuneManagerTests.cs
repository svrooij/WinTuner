using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;
using Winget.CommunityRepository.Models;
using WingetIntune.Commands;
using WingetIntune.Interfaces;
using WingetIntune.Models;

namespace WingetIntune.Tests.Intune;

public class IntuneManagerTests
{
    [Fact]
    public async Task GenerateMsiPackage_OtherPackage_ThrowsError()
    {
        var intuneManager = new IntuneManager(null, null, null, null, null, null, null, null, null, null, new ComputeBestInstallerForPackageCommand());
        var tempFolder = Path.Combine(Path.GetTempPath(), "intunewin");
        var outputFolder = Path.Combine(Path.GetTempPath(), "packages");

        await Assert.ThrowsAsync<ArgumentException>(() => intuneManager.GenerateMsiPackage(tempFolder, outputFolder, new PackageInfo(), PackageOptions.Create(), CancellationToken.None));
    }

    [Fact]
    public async Task GenerateInstallerPackage_MsiPackage_Returns()
    {
        var packageId = "Microsoft.AzureCLI";
        var version = "2.51.0";
        var tempFolder = Path.Combine(Path.GetTempPath(), "intunewin");
        var tempPackageFolder = Path.Combine(tempFolder, packageId, version);

        var outputFolder = Path.Combine(Path.GetTempPath(), "packages");
        var outputPackageFolder = Path.Combine(outputFolder, packageId, version);
        var installer = IntuneTestConstants.azureCliPackageInfo.Installers!.First();
        var installerPath = Path.Combine(tempPackageFolder, installer.InstallerFilename!);

        var logoPath = Path.GetFullPath(Path.Combine(outputPackageFolder, "..", "logo.png"));

        var fileManager = Substitute.For<IFileManager>();
        fileManager.CreateFolderForPackage(tempFolder, packageId, version, false).Returns(Path.Combine(tempFolder, packageId, version));
        fileManager.CreateFolderForPackage(outputFolder, packageId, version, false).Returns(Path.Combine(outputFolder, packageId, version));
        fileManager.DownloadFileAsync(installer.InstallerUrl!.ToString(), installerPath, null, true, false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        fileManager.DownloadFileAsync($"https://api.winstall.app/icons/{packageId}.png", logoPath, null, false, false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var detectionContent = @"Package Microsoft.AzureCLI 2.51.0 from Winget

MsiProductCode={89E4C65D-96DD-435B-9BBB-EF1EAEF5B738}
MsiVersion=2.51.0
";

        var readmeContent = @"Package Microsoft.AzureCLI 2.51.0 from Winget

Display name: Microsoft Azure CLI
Publisher: Microsoft Corporation
Homepage: 

Install script:
msiexec /i azure-cli-2.51.0-x64.msi /quiet /qn

Uninstall script:
msiexec /x {89E4C65D-96DD-435B-9BBB-EF1EAEF5B738} /quiet /qn

Description:
The Azure command-line interface (Azure CLI) is a set of commands used to create and manage Azure resources. The Azure CLI is available across Azure services and is designed to get you working quickly with Azure, with an emphasis on automation.
";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            fileManager.WriteAllTextAsync(Path.Combine(outputPackageFolder, "detection.txt"), detectionContent, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            fileManager.WriteAllTextAsync(Path.Combine(outputPackageFolder, "readme.txt"), readmeContent, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }
        else
        {
            fileManager.WriteAllTextAsync(Path.Combine(outputPackageFolder, "detection.txt"), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            fileManager.WriteAllTextAsync(Path.Combine(outputPackageFolder, "readme.txt"), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }

        fileManager.WriteAllBytesAsync(Path.Combine(outputPackageFolder, "app.json"), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var processManager = Substitute.For<IProcessManager>();

        var intunePackager = Substitute.For<IIntunePackager>();

        var intuneManager = new IntuneManager(new NullLoggerFactory(), fileManager, processManager, null, null, null, intunePackager, null, null, null, new ComputeBestInstallerForPackageCommand());

        await intuneManager.GenerateInstallerPackage(tempFolder, outputFolder, IntuneTestConstants.azureCliPackageInfo, new PackageOptions { Architecture = Models.Architecture.X64, InstallerContext = InstallerContext.User }, CancellationToken.None);

        fileManager.Received().CreateFolderForPackage(tempFolder, packageId, version, false);
        fileManager.Received().CreateFolderForPackage(outputFolder, packageId, version, false);
        await fileManager.Received().DownloadFileAsync(installer.InstallerUrl!.ToString(), installerPath, null, true, false, Arg.Any<CancellationToken>());
        await fileManager.Received().DownloadFileAsync($"https://api.winstall.app/icons/{packageId}.png", logoPath, null, false, false, Arg.Any<CancellationToken>());
        await fileManager.Received().WriteAllBytesAsync(Path.Combine(outputPackageFolder, "app.json"), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task DownloadLogoAsync_CallsFilemanager()
    {
        var packageId = "Microsoft.AzureCLI";
        var version = "2.26.1";
        var folder = Path.Combine(Path.GetTempPath(), "intunewin", packageId, version);

        var logoPath = Path.GetFullPath(Path.Combine(folder, "..", "logo.png"));

        var fileManager = Substitute.For<IFileManager>();
        fileManager.DownloadFileAsync($"https://api.winstall.app/icons/{packageId}.png", logoPath, null, false, false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var intuneManager = new IntuneManager(new NullLoggerFactory(), fileManager, null, null, null, null, null, null, null, null, new ComputeBestInstallerForPackageCommand());
        await intuneManager.DownloadLogoAsync(folder, packageId, CancellationToken.None);

        //call.Received(1);

    }

    [Theory]
    [InlineData(InstallerContext.User, true, "--scope", "user")]
    [InlineData(InstallerContext.System, true, "--scope", "machine")]
    [InlineData(InstallerContext.Unknown, false, "--scope", null)]
    public async Task GenerateInstallerPackage_Versionless_ScriptsContainFullWingetArgs(InstallerContext installerContext, bool expectScope, string scopeArg, string? scopeValue)
    {
        var packageId = "Test.Package";
        var version = "1.0.0";
        var tempFolder = Path.Combine(Path.GetTempPath(), "intunewin-versionless");
        var outputFolder = Path.Combine(Path.GetTempPath(), "packages-versionless");
        var tempPackageFolder = Path.Combine(tempFolder, packageId, version);
        var outputPackageFolder = Path.Combine(outputFolder, packageId, "latest");

        var packageInfo = new PackageInfo
        {
            PackageIdentifier = packageId,
            DisplayName = "Test Package",
            Version = version,
            Source = PackageSource.Winget,
            InstallerContext = installerContext,
            InstallerType = InstallerType.Exe,
            InstallCommandLine = $"winget install --id {packageId} --version {version} --source winget --silent --accept-package-agreements --accept-source-agreements",
            UninstallCommandLine = $"winget uninstall --id {packageId} --source winget --silent --accept-source-agreements",
            InstallerFilename = "setup.exe",
            Installers =
            [
                new WingetInstaller
                {
                    Architecture = "x64",
                    Scope = installerContext == InstallerContext.User ? "user"
                          : installerContext == InstallerContext.System ? "machine"
                          : null,
                    InstallerType = "exe",
                    InstallerUrl = "https://localhost/setup.exe",
                }
            ],
        };

        var capturedScripts = new Dictionary<string, string>();

        var fileManager = Substitute.For<IFileManager>();
        fileManager.CreateFolderForPackage(tempFolder, packageId, version, false).Returns(tempPackageFolder);
        fileManager.CreateFolderForPackage(outputFolder, packageId, "latest", false).Returns(outputPackageFolder);
        fileManager.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedScripts[Path.GetFileName(ci.ArgAt<string>(0))] = ci.ArgAt<string>(1);
                return Task.CompletedTask;
            });
        fileManager.WriteAllBytesAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        fileManager.DownloadFileAsync(Arg.Any<string>(), Arg.Any<string>(), null, false, false, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var intunePackager = Substitute.For<IIntunePackager>();
        intunePackager.CreatePackage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PackageInfo>(), Arg.Any<bool>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns("test.intunewin");

        var intuneManager = new IntuneManager(new NullLoggerFactory(), fileManager, null, null, null, null, intunePackager, null, null, null, new ComputeBestInstallerForPackageCommand());

        await intuneManager.GenerateInstallerPackage(tempFolder, outputFolder, packageInfo, new PackageOptions { Versionless = true, PackageScript = true, InstallerContext = installerContext }, CancellationToken.None);

        Assert.True(capturedScripts.ContainsKey("install.ps1"), "install.ps1 was not written");
        Assert.True(capturedScripts.ContainsKey("uninstall.ps1"), "uninstall.ps1 was not written");

        var installScript = capturedScripts["install.ps1"];
        Assert.Contains("--source", installScript);
        Assert.Contains("winget", installScript);
        Assert.Contains("--silent", installScript);
        Assert.Contains("--accept-package-agreements", installScript);
        Assert.Contains("--accept-source-agreements", installScript);
        Assert.DoesNotContain("--version", installScript);

        var uninstallScript = capturedScripts["uninstall.ps1"];
        Assert.Contains("--source", uninstallScript);
        Assert.Contains("--silent", uninstallScript);
        Assert.Contains("--accept-source-agreements", uninstallScript);
        Assert.DoesNotContain("--version", uninstallScript);

        if (expectScope)
        {
            Assert.Contains(scopeArg, installScript);
            Assert.Contains(scopeValue!, installScript);
            Assert.Contains(scopeArg, uninstallScript);
            Assert.Contains(scopeValue!, uninstallScript);
        }
        else
        {
            Assert.DoesNotContain(scopeArg, installScript);
            Assert.DoesNotContain(scopeArg, uninstallScript);
        }
    }

    [Fact]
    public async Task DownloadInstallerAsync_CallsFilemanager()
    {
        var packageId = "Microsoft.AzureCLI";
        var version = "2.26.1";
        var hash = "1234567890";
        var folder = Path.Combine(Path.GetTempPath(), "intunewin", packageId, version);

        var packageInfo = new PackageInfo
        {
            InstallerFilename = "testpackage.exe",
            InstallerUrl = new Uri("https://localhost/testpackage.exe"),
            Installer = new WingetInstaller
            {
                InstallerType = "exe",
                InstallerSha256 = hash
            }
        };

        var installerPath = Path.GetFullPath(Path.Combine(folder, packageInfo.InstallerFilename));

        var fileManager = Substitute.For<IFileManager>();
        fileManager.DownloadFileAsync(packageInfo.InstallerUrl.ToString(), installerPath, hash, true, false, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var intuneManager = new IntuneManager(new NullLoggerFactory(), fileManager, null, null, null, null, null, null, null, null, new ComputeBestInstallerForPackageCommand());
        await intuneManager.DownloadInstallerAsync(folder, packageInfo, CancellationToken.None);

        //call.Received(1);
    }
}
