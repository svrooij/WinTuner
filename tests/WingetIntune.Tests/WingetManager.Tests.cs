using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WingetIntune.Implementations;
using WingetIntune.Models;

namespace WingetIntune.Tests
{
    public class WingetManagerTests
    {
        private readonly ILogger<WingetManager> logger;

        public WingetManagerTests()
        {
            logger = new NullLogger<WingetManager>();
        }

        [Theory]
        [InlineData(WingetManagerTestConstants.wingetListNoInstalledPackage, IsInstalledResult.NotInstalled)]
        [InlineData(WingetManagerTestConstants.wingetListInstalledPackage, IsInstalledResult.Installed)]
        public async Task CheckInstalled_NoVersion_ReturnsCorrectValue(string mockedResponse, IsInstalledResult expectedResult)
        {
            var packageId = "Notepad++.Notepad++";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                $"list --id {packageId} --exact --disable-interactivity --accept-source-agreements",
                Arg.Any<CancellationToken>(), false)
                .Returns(Task.FromResult(new ProcessResult(0, mockedResponse, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);
            var result = await wingetManager.CheckInstalled(packageId, null);

            Assert.Equal(expectedResult, result);

            await processManager.Received().RunProcessAsync("winget",
                $"list --id {packageId} --exact --disable-interactivity --accept-source-agreements",
                Arg.Any<CancellationToken>(), false);

        }

        [Theory]
        [InlineData("8.5.4", WingetManagerTestConstants.wingetListNoInstalledPackage, IsInstalledResult.NotInstalled)]
        [InlineData("8.5.4", WingetManagerTestConstants.wingetListInstalledPackage, IsInstalledResult.Installed)]
        [InlineData("8.5.9", WingetManagerTestConstants.wingetListInstalledPackage, IsInstalledResult.UpgradeAvailable)]
        public async Task CheckInstalled_WithVersion_ReturnsCorrectValue(string version, string mockedResponse, IsInstalledResult expectedResult)
        {
            var packageId = "Notepad++.Notepad++";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                $"list --id {packageId} --exact --disable-interactivity --accept-source-agreements",
                Arg.Any<CancellationToken>(), false)
                .Returns(Task.FromResult(new ProcessResult(0, mockedResponse, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);
            var result = await wingetManager.CheckInstalled(packageId, version);

            Assert.Equal(expectedResult, result);

            await processManager.Received().RunProcessAsync("winget",
                $"list --id {packageId} --exact --disable-interactivity --accept-source-agreements",
                Arg.Any<CancellationToken>(), false);

        }

        [Fact]
        public async Task GetPackageInfoAsync_ParsesResponse_WingetResult()
        {
            var packageId = "JanDeDobbeleer.OhMyPosh";
            var version = "18.7.0";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                $"show --id {packageId} --version {version} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false)
                .Returns(Task.FromResult(new ProcessResult(0, WingetManagerTestConstants.ohMyPoshOutput, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);
            var info = await wingetManager.GetPackageInfoAsync(packageId, version, null, CancellationToken.None);

            await processManager.Received().RunProcessAsync("winget",
                $"show --id {packageId} --version {version} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false);


            // Check PackageId, Name, Version, Publisher, PublisherUrl, HomePageUrl, InstallerType, InstallerUrl, Hash
            Assert.Equal(packageId, info.PackageIdentifier);
            Assert.Equal("Oh My Posh", info.DisplayName);
            Assert.Equal(version, info.Version);
            Assert.Equal("Jan De Dobbeleer", info.Publisher);
            Assert.Equal("Prompt theme engine for any shell", info.Description);
            Assert.Equal("https://github.com/JanDeDobbeleer/oh-my-posh/", info.PublisherUrl!.ToString());
            Assert.Equal("https://github.com/JanDeDobbeleer/oh-my-posh/issues", info.SupportUrl!.ToString());
            Assert.Equal("https://ohmyposh.dev/", info.InformationUrl!.ToString());
            Assert.Equal(InstallerType.Inno, info.InstallerType);
            Assert.Equal("https://github.com/JanDeDobbeleer/oh-my-posh/releases/download/v18.7.0/install-amd64.exe", info.InstallerUrl!.ToString());
            Assert.Equal("071ceebaafbfbce77352ab2752301aa51938f2601d112574bedbf58773dbda25", info.Hash);
        }

        [Fact]
        public async Task GetPackageInfoAsync_DownloadsData_WingetResult()
        {
            var packageId = "JanDeDobbeleer.OhMyPosh";
            var version = "18.3.3";
            var source = "winget";
            var processManager = Substitute.For<IProcessManager>();
            var filemanagerMock = Substitute.For<IFileManager>();
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, null), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.ohMyPoshYaml);
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".installer"), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.ohMyPoshInstallYaml);
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".locale.en-US"), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.ohMyPoshLocaleYaml);

            var wingetManager = new WingetManager(logger, processManager, filemanagerMock);
            var info = await wingetManager.GetPackageInfoAsync(packageId, version, source);

            await filemanagerMock.Received().DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, null), true, Arg.Any<CancellationToken>());
            await filemanagerMock.Received().DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".installer"), true, Arg.Any<CancellationToken>());
            await filemanagerMock.Received().DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".locale.en-US"), true, Arg.Any<CancellationToken>());


            // Check PackageId, Name, Version, Publisher, PublisherUrl, HomePageUrl, InstallerType, InstallerUrl, Hash
            Assert.Equal("JanDeDobbeleer.OhMyPosh", info.PackageIdentifier);
            Assert.Equal("Oh My Posh", info.DisplayName);
            Assert.Equal("18.3.3", info.Version);
            Assert.Equal("Jan De Dobbeleer", info.Publisher);
            Assert.Equal("Prompt theme engine for any shell", info.Description);
            Assert.Equal("https://github.com/JanDeDobbeleer/oh-my-posh/", info.PublisherUrl!.ToString());
            Assert.Equal("https://github.com/JanDeDobbeleer/oh-my-posh/issues", info.SupportUrl!.ToString());
            Assert.Equal("https://ohmyposh.dev/", info.InformationUrl!.ToString());
            Assert.Equal(InstallerType.Inno, info.InstallerType);
            Assert.NotEmpty(info.Installers!);
        }

        [Fact]
        public async Task GetPackageInfoAsync_ZipWithNestedInstaller_ReturnsZipInstallerType()
        {
            var packageId = "Adobe.Acrobat.Pro";
            var version = "26.001.21662";
            var source = "winget";
            var processManager = Substitute.For<IProcessManager>();
            var filemanagerMock = Substitute.For<IFileManager>();
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, null), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.adobeAcrobatYaml);
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".installer"), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.adobeAcrobatInstallYaml);
            filemanagerMock.DownloadStringAsync(WingetManager.CreateManifestUri(packageId, version, ".locale.en-US"), true, Arg.Any<CancellationToken>())
                .Returns(WingetManagerTestConstants.adobeAcrobatLocaleYaml);

            var wingetManager = new WingetManager(logger, processManager, filemanagerMock);
            var info = await wingetManager.GetPackageInfoAsync(packageId, version, source);

            Assert.Equal(packageId, info.PackageIdentifier);
            Assert.Equal(version, info.Version);
            Assert.Equal(InstallerType.Zip, info.InstallerType);
            Assert.NotEmpty(info.Installers!);

            // Verify NestedInstallerType was propagated to the installer
            var installer = info.Installers!.First();
            Assert.Equal("zip", installer.InstallerType);
            Assert.Equal("exe", installer.NestedInstallerType);
            Assert.NotNull(installer.NestedInstallerFiles);
            Assert.Single(installer.NestedInstallerFiles!);
            Assert.Equal(@"Adobe Acrobat\setup.exe", installer.NestedInstallerFiles!.First().RelativeFilePath);
        }

        [Fact]
        public async Task GetPackageInfoAsync_ParsesResponse_StoreResult()
        {
            var packageId = "9MZ1SNWT0N5D";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                $"show --id {packageId} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false)
                .Returns(Task.FromResult(new ProcessResult(0, WingetManagerTestConstants.powershellOutput, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);
            var info = await wingetManager.GetPackageInfoAsync(packageId, null, null);

            await processManager.Received().RunProcessAsync("winget",
                $"show --id {packageId} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false);


            // Check PackageId, Name, Version, Publisher, PublisherUrl, HomePageUrl, InstallerType, InstallerUrl, Hash
            Assert.Equal("9MZ1SNWT0N5D", info.PackageIdentifier);
            Assert.Equal("PowerShell", info.DisplayName);
            Assert.Equal("Unknown", info.Version);
            Assert.Equal("Microsoft Corporation", info.Publisher);
            Assert.Equal("https://github.com/powershell/powershell", info.PublisherUrl!.ToString());
            Assert.Equal("https://github.com/PowerShell/PowerShell/issues", info.SupportUrl!.ToString());
            var containsSecondLine = info.Description!.Contains("PowerShell commands let you manage computers from the command line.");
            Assert.True(containsSecondLine, "Contains second line");
        }

        [Fact]
        public async Task GetPackageInfoAsync_ThrowsException()
        {
            var packageId = "JanDeDobbeleer.OhMyPosh";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                $"show --id {packageId} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false)
                .Returns(Task.FromResult(new ProcessResult(10, string.Empty, "Something went terribly wrong")));

            var wingetManager = new WingetManager(logger, processManager, null);
            await Assert.ThrowsAsync<Exception>(() => wingetManager.GetPackageInfoAsync(packageId, null, null));

            await processManager.Received().RunProcessAsync("winget",
                $"show --id {packageId} --exact --accept-source-agreements",
                Arg.Any<CancellationToken>(), false);

        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, null, true)]
        [InlineData("0.1.1", null, false)]
        [InlineData("0.1.1", null, true)]
        [InlineData("0.1.1", "winget", true)]
        [InlineData("0.1.1", "store", false)]
        [InlineData(null, "winget", true)]
        public async Task Install_CallsCorrectProcess(string? version, string? source, bool force)
        {
            var packageId = "Notepad++.Notepad++";
            var arguments = $"install --id {packageId}";
            if (!string.IsNullOrWhiteSpace(version))
            {
                arguments += $" --version {version}";
            }
            if (!string.IsNullOrWhiteSpace(source))
            {
                arguments += $" --source {source}";
            }
            if (force)
            {
                arguments += " --force";
            }
            arguments += " --silent --accept-package-agreements --accept-source-agreements";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                arguments,
                Arg.Any<CancellationToken>(),
                true)
                .Returns(Task.FromResult(new ProcessResult(0, WingetManagerTestConstants.powershellOutput, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);

            await wingetManager.Install(packageId, version, source, force);

            await processManager.Received().RunProcessAsync("winget",
                arguments,
                Arg.Any<CancellationToken>(), true);

        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, null, true)]
        [InlineData("0.1.1", null, false)]
        [InlineData("0.1.1", null, true)]
        [InlineData("0.1.1", "winget", true)]
        [InlineData("0.1.1", "store", false)]
        [InlineData(null, "winget", true)]
        public async Task Upgrade_CallsCorrectProcess(string? version, string? source, bool force)
        {
            var packageId = "Notepad++.Notepad++";
            var arguments = $"upgrade --id {packageId}";
            if (!string.IsNullOrWhiteSpace(version))
            {
                arguments += $" --version {version}";
            }
            if (!string.IsNullOrWhiteSpace(source))
            {
                arguments += $" --source {source}";
            }
            if (force)
            {
                arguments += " --force";
            }
            arguments += " --silent --accept-package-agreements --accept-source-agreements";
            var processManager = Substitute.For<IProcessManager>();
            processManager.RunProcessAsync("winget",
                arguments,
                Arg.Any<CancellationToken>(),
                true)
                .Returns(Task.FromResult(new ProcessResult(0, WingetManagerTestConstants.powershellOutput, string.Empty)));

            var wingetManager = new WingetManager(logger, processManager, null);

            await wingetManager.Upgrade(packageId, version, source, force);

            await processManager.Received().RunProcessAsync("winget",
                arguments,
                Arg.Any<CancellationToken>(), true);

        }
    }
}
