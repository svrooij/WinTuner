using Microsoft.Extensions.Logging;
using SvRooij.ContentPrep;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WingetIntune.Models;

namespace WingetIntune.Testing;

/// <summary>
/// Helper to test packages in the Windows Sandbox
/// </summary>
public class WindowsSandbox
{
    private readonly ILogger<WindowsSandbox> logger;
    private readonly Packager packager;
    private readonly IProcessManager processManager;
    private readonly IFileManager fileManager;

    // See https://github.com/microsoft/winget-cli/releases
    private const string WingetInstallerDownloadFormat = "https://github.com/microsoft/winget-cli/releases/download/{0}/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";
    private const string WingetPackageName = "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";
    private const string WingetVersion = "v1.10.340";
    private const string WingetInstallerDependenciesFormat = "https://github.com/microsoft/winget-cli/releases/download/{0}/DesktopAppInstaller_Dependencies.zip";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="loggerFactory"></param>
    /// <param name="processManager"></param>
    public WindowsSandbox(ILoggerFactory loggerFactory, IProcessManager processManager, IFileManager fileManager)
    {
        this.logger = loggerFactory.CreateLogger<WindowsSandbox>();
        this.packager = new Packager(loggerFactory.CreateLogger<Packager>());
        this.processManager = processManager;
        this.fileManager = fileManager;
    }

    /// <summary>
    /// Prepares a sandbox file for a package
    /// </summary>
    /// <param name="intuneWinFile">The absolute path to the .intunewin file</param>
    /// <param name="installerFilename">Name of the setup file inside the intune win, if not provided the name from the intunewin metadata will be used</param>
    /// <param name="installerArguments">Silent arguments for this setup</param>
    /// <param name="timeout">When a number above -1 is provided a shutdown command will be added to the script</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The location of the sandbox file, which may be started with <see cref="RunSandbox(string, bool, CancellationToken)"/> method.</returns>
    /// <remarks>Will decrypt the intunewin file to a temp folder, create install scripts and creates a Windows Sandbox file.</remarks>
    public async Task<string> PrepareSandboxFileForPackage(string intuneWinFile, string? installerFilename, string? installerArguments, int? timeout = null, CancellationToken cancellationToken = default)
    {
        var outputFolder = Path.Combine(Path.GetTempPath(), "wintuner-sandbox", Guid.NewGuid().ToString());
        logger.LogInformation("Preparing sandbox file for {IntuneWinFile}", intuneWinFile);

        var installerFolder = Path.Combine(outputFolder, "installer");
        var logsFolder = Path.Combine(outputFolder, "logs");

        Directory.CreateDirectory(installerFolder);
        Directory.CreateDirectory(logsFolder);
        var info = await packager.Unpack(intuneWinFile, installerFolder, cancellationToken);
        logger.LogDebug("Unpackaged intunewin file {IntuneWinFile} to {InstallerFolder} contained installer {InstallerFilename}", intuneWinFile, installerFolder, info?.SetupFile);
        installerFilename ??= info!.SetupFile!;
        if (!File.Exists(Path.Combine(installerFolder, installerFilename)))
        {
            throw new FileNotFoundException("Installer in the unpacked folder", installerFilename!);
        }
        await PrepareSandboxDependencies(cancellationToken: cancellationToken);
        var sandboxFile = Path.Combine(outputFolder, "sandbox.wsb");
        await WriteSandboxConfig(sandboxFile, installerFolder, logsFolder);
        var scriptFolder = Path.Combine(installerFolder, "wt_scripts");
        await WriteTestScript(scriptFolder, installerFilename, installerArguments, timeout);

        return sandboxFile;
    }

    /// <summary>
    /// Prepares a sandbox file for an installer
    /// </summary>
    /// <param name="setupFile">Absolute path to the installer</param>
    /// <param name="installerArguments">Arguments that should be used</param>
    /// <param name="timeout">Want to auto shutdown the Sandbox?</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    public async Task<string> PrepareSandboxForInstaller(string setupFile, string? installerArguments, int? timeout = null, CancellationToken cancellationToken = default)
    {
        var outputFolder = Path.Combine(Path.GetTempPath(), "wintuner-sandbox", Guid.NewGuid().ToString());
        var installerFolder = Directory.GetParent(setupFile)!.FullName;
        logger.LogInformation("Preparing sandbox file for {SetupFile}", setupFile);

        var scriptFolder = Path.Combine(outputFolder, "wt_scripts");
        var logsFolder = Path.Combine(outputFolder, "logs");

        Directory.CreateDirectory(scriptFolder);
        Directory.CreateDirectory(logsFolder);

        if (!File.Exists(setupFile))
        {
            throw new FileNotFoundException("Installer file not found", setupFile);
        }
        await PrepareSandboxDependencies(cancellationToken: cancellationToken);
        var sandboxFile = Path.Combine(outputFolder, "sandbox.wsb");
        await WriteSandboxConfig(sandboxFile, installerFolder, logsFolder, scriptFolder);
        await WriteTestScript(scriptFolder, Path.GetFileName(setupFile), installerArguments, timeout);

        return sandboxFile;
    }

    /// <summary>
    /// Creates a Windows Sandbox configuration file
    /// </summary>
    /// <param name="sandboxFilename">Absolute path the the sandbox file</param>
    /// <param name="installerFolder">Where is the installer located, this folder will be mapped to the Sandbox as readonly</param>
    /// <param name="logFolder">Where should the logs be placed? This folder will be mapped to the Sandbox as writable</param>
    /// <param name="scriptFolder">Additional script folder if not in the installer folder</param>
    /// <returns></returns>
    private static async Task WriteSandboxConfig(string sandboxFilename, string installerFolder, string logFolder, string? scriptFolder = null)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("<Configuration>");
        // Mapped folders (installer and logs)
        // some logs are parsed to show the actual result.
        stringBuilder.AppendLine("  <MappedFolders>");
        stringBuilder.AppendLine("    <MappedFolder>");
        stringBuilder.AppendLine($"      <HostFolder>{installerFolder}</HostFolder>");
        stringBuilder.AppendLine("      <SandboxFolder>c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner</SandboxFolder>");
        stringBuilder.AppendLine("      <ReadOnly>true</ReadOnly>");
        stringBuilder.AppendLine("    </MappedFolder>");
        stringBuilder.AppendLine("    <MappedFolder>");
        stringBuilder.AppendLine($"      <HostFolder>{logFolder}</HostFolder>");
        stringBuilder.AppendLine("      <SandboxFolder>c:\\Users\\WDAGUtilityAccount\\Desktop\\logs</SandboxFolder>");
        stringBuilder.AppendLine("      <ReadOnly>false</ReadOnly>");
        stringBuilder.AppendLine("    </MappedFolder>");
        if (scriptFolder is not null)
        {
            stringBuilder.AppendLine("    <MappedFolder>");
            stringBuilder.AppendLine($"      <HostFolder>{scriptFolder}</HostFolder>");
            stringBuilder.AppendLine("      <SandboxFolder>c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\wt_scripts</SandboxFolder>");
            stringBuilder.AppendLine("      <ReadOnly>true</ReadOnly>");
            stringBuilder.AppendLine("    </MappedFolder>");
        }
        var depsFolder = Path.Combine(Path.GetTempPath(), "wintuner-sandbox", "deps");
        stringBuilder.AppendLine("    <MappedFolder>");
        stringBuilder.AppendLine($"      <HostFolder>{depsFolder}</HostFolder>");
        stringBuilder.AppendLine("      <SandboxFolder>c:\\Users\\WDAGUtilityAccount\\Downloads\\dependencies</SandboxFolder>");
        stringBuilder.AppendLine("      <ReadOnly>true</ReadOnly>");
        stringBuilder.AppendLine("    </MappedFolder>");
        stringBuilder.AppendLine("  </MappedFolders>");

        // Startup command
        stringBuilder.AppendLine("  <LogonCommand>");
        stringBuilder.AppendLine("    <Command>c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\wt_scripts\\startup.cmd</Command>");
        stringBuilder.AppendLine("  </LogonCommand>");

        // Security settings
        stringBuilder.AppendLine("  <AudioInput>Disable</AudioInput>");
        stringBuilder.AppendLine("  <VideoInput>Disable</VideoInput>");
        // Not sure about this next one https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file#protected-client
        stringBuilder.AppendLine("  <ProtectedClient>Enabled</ProtectedClient>");
        stringBuilder.AppendLine("  <PrinterRedirection>Disable</PrinterRedirection>");
        stringBuilder.AppendLine("  <ClipboardRedirection>Disable</ClipboardRedirection>");
        stringBuilder.AppendLine("</Configuration>");

        await File.WriteAllTextAsync(sandboxFilename, stringBuilder.ToString());
    }

    /// <summary>
    /// Creates the test script that will be executed in the sandbox
    /// </summary>
    /// <param name="scriptFolder">Script folder location, it will create the scripts here.</param>
    /// <param name="installerFilename">Filename of the installer</param>
    /// <param name="installerArguments">Arguments of the installer, will be added to the install script</param>
    /// <param name="timeout">If a value above -1 is provided, 'shutdown /s /t {timeout}' is added to the install script</param>
    /// <returns></returns>
    private static async Task WriteTestScript(string scriptFolder, string installerFilename, string? installerArguments, int? timeout)
    {

        // Create a batch script that will run a powershell script (Execution policy stuff...)
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("start /wait /low powershell.exe -ExecutionPolicy Bypass -File \"C:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\wt_scripts\\install.ps1\"");
        sb.AppendLine();
        sb.AppendLine("c:\\Windows\\System32\\notepad.exe c:\\Users\\WDAGUtilityAccount\\Desktop\\logs\\wintuner.log");
        Directory.CreateDirectory(scriptFolder);
        await File.WriteAllTextAsync(Path.Combine(scriptFolder, "startup.cmd"), sb.ToString());

        sb.Clear();

        // Create the powershell script that will install the app
        // and collect the installed apps
        // This script will also shutdown the sandbox after the installation (if a timeout above -1 is provided)
        sb.AppendLine("Start-Transcript -Path c:\\Users\\WDAGUtilityAccount\\Desktop\\logs\\wintuner.log -Append -Force");

        sb.AppendLine("Write-Host \"Installing winget\"");
        sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");
        sb.AppendLine("Import-Module -Name Appx");
        sb.AppendLine("# Maybe set the x64 part correctly?");
        sb.AppendLine("$dependencies = Get-ChildItem -Path c:\\Users\\WDAGUtilityAccount\\Downloads\\dependencies\\x64\\ -Filter \"*.appx\" -Name");
        sb.AppendLine("foreach ($dependency in $dependencies) {");
        sb.AppendLine("    Write-Host \"Installing $dependency\"");
        sb.AppendLine("    Add-AppxPackage -Path c:\\Users\\WDAGUtilityAccount\\Downloads\\dependencies\\x64\\$dependency");
        sb.AppendLine("}");

        string[] appx = new string[] { WingetPackageName };
        foreach (var filename in appx)
        {

            sb.AppendLine($"Write-Host \"Excuting Add-AppxPackage downloads\\dependencies\\{filename}\"");
            sb.AppendLine($"Add-AppxPackage -Path c:\\Users\\WDAGUtilityAccount\\Downloads\\dependencies\\{filename}");
            sb.AppendLine("Start-Sleep -Seconds 2");
        }
        sb.AppendLine();
        sb.AppendLine(Intune.IntuneManagerConstants.GetPsGetWingetCmd());
        sb.AppendLine();
        sb.AppendLine("Write-Host \"Winget installed\"");
        sb.AppendLine("Write-Host \"Installing notepad\"");
        sb.AppendLine("Copy-Item -Path c:\\Users\\WDAGUtilityAccount\\Downloads\\dependencies\\notepad.exe -Destination c:\\Windows\\System32\\notepad.exe -Force");
        sb.AppendLine("cmd /c assoc .txt=txtfile");
        sb.AppendLine("cmd /c assoc .log=txtfile");
        sb.AppendLine("cmd /c ftype txtfile=c:\\Windows\\System32\\notepad.exe %1");
        sb.AppendLine("$ProgressPreference = 'Continue'");

        sb.AppendLine("Write-Host \"Notepad reinstalled\"");
        sb.AppendLine("Write-Host \"Starting installation\"");
        sb.AppendLine($"Write-Host \"Installer: {installerFilename}\"");
        sb.AppendLine($"Write-Host \"Arguments: {installerArguments?.Replace("\"", "`\"")}\"");

        // execute the installer and capture the exit code in powershell

        if (installerFilename.EndsWith(".msi"))
        {
            sb.AppendLine($"$setupProcess = Start-Process -FilePath \"c:\\windows\\system32\\msiexec.exe\" -ArgumentList \"/i c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\{installerFilename} /qn /quiet /norestart \" -Wait -PassThru");
        }
        else if (string.IsNullOrWhiteSpace(installerArguments))
        {
            sb.AppendLine($"$setupProcess = Start-Process -FilePath \"c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\{installerFilename}\" -Wait -PassThru");
        }
        else
        {
            sb.AppendLine($"$setupProcess = Start-Process -FilePath \"c:\\Users\\WDAGUtilityAccount\\Downloads\\Wintuner\\{installerFilename}\" -ArgumentList \"{installerArguments?.Replace("\"", "'")}\" -Wait -PassThru");
        }

        sb.AppendLine("$exitCode = $setupProcess.ExitCode");
        sb.AppendLine("Write-Host \"Installer finished with exitcode $exitCode\"");

        // write the exit code to a file
        sb.AppendLine("$exitCode | Out-File -FilePath c:\\Users\\WDAGUtilityAccount\\Desktop\\logs\\exitcode.txt");
        sb.AppendLine("Write-Host \"App installed, collecting installed apps\"");
        // Cannot collect installed apps, it throws unauthorized exception
        //sb.AppendLine("$apps = $(Get-WmiObject -Class Win32_InstalledWin32Program | Select-Object -Property Version,Vendor,Name)");
        //sb.AppendLine("$apps | Format-Table -AutoSize");
        //sb.AppendLine("$apps | Export-Csv -Path c:\\Users\\WDAGUtilityAccount\\Desktop\\logs\\installed.csv -NoTypeInformation");
        sb.AppendLine("Write-Host \"Installed apps collected\"");
        sb.AppendLine("Stop-Transcript");
        if (timeout is not null && timeout >= 0)
        {
            if (timeout > 0) // Cancelable shutdown
            {
                sb.AppendLine($"shutdown /s /t {timeout}");
                sb.AppendLine($"Write-Host \"Closing sandbox in {timeout} seconds unless you press a button\"");
                sb.AppendLine("Read-Host");
                sb.AppendLine("shutdown /a");
            }
            else // Immediate shutdown
            {
                sb.AppendLine($"shutdown /s /t {timeout}");
            }
        }
        // Exit with the exit code of the installer (not sure if that does anything)
        sb.AppendLine("exit $exitCode");
        await File.WriteAllTextAsync(Path.Combine(scriptFolder, "install.ps1"), sb.ToString());
    }

    private async Task PrepareSandboxDependencies(bool fetchWinget = true, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(Path.GetTempPath(), "wintuner-sandbox", "deps");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        if (fetchWinget)
        {
            var wingetFile = Path.Combine(folder, WingetPackageName);
            if (!fileManager.FileExists(wingetFile))
            {
                await fileManager.DownloadFileAsync(string.Format(WingetInstallerDownloadFormat, WingetVersion), wingetFile);
            }

            var wingetDependencies = Path.Combine(folder, "DesktopAppInstaller_Dependencies.zip");
            if (!fileManager.FileExists(wingetDependencies))
            {
                await fileManager.DownloadFileAsync(string.Format(WingetInstallerDependenciesFormat, WingetVersion), wingetDependencies);
                await fileManager.ExtractFileToFolderAsync(wingetDependencies, folder, cancellationToken);
            }
        }

        var notepadFile = Path.Combine(folder, "notepad.exe");
        if (!fileManager.FileExists(notepadFile))
        {
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe"), notepadFile);
        }
    }

    /// <summary>
    /// Runs a sandbox file
    /// </summary>
    /// <param name="sandboxFile">Absolute path of the .wsb file</param>
    /// <param name="cleanup">Should we try to cleanup the folder containing the sandbox file?</param>
    /// <param name="cancellationToken">In case you want to cancel the process.</param>
    /// <returns></returns>
    public async Task<SandboxResult?> RunSandbox(string sandboxFile, bool cleanup, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running sandbox {SsandboxFile}", sandboxFile);
        var processResult = await processManager.RunProcessAsync("WindowsSandbox.exe", sandboxFile, cancellationToken);
        logger.LogInformation("Sandbox exited with exitcode {ExitCode}", processResult.ExitCode);
        bool shouldProcess = true;
        SandboxResult? result = null;
        if (processResult.ExitCode == -2147024713)
        {
            logger.LogWarning("Sandbox failed to start, this is likely because the host does not support virtualization or already started");
            shouldProcess = false;
        }

        logger.LogInformation("Press enter when you closed the sandbox");
        Console.ReadLine();
        //try
        //{
        //    await Task.Delay(600_000, cancellationToken);
        //} catch
        //{

        //}


        logger.LogInformation("Processing results and closing");


        if (shouldProcess)
        {
            var cancelToken = cancellationToken.IsCancellationRequested ? CancellationToken.None : cancellationToken;
            var logDirectory = Path.Combine(Path.GetDirectoryName(sandboxFile)!, "logs");
            var logFile = Path.Combine(logDirectory, "wintuner.log");
            var exitCodeFile = Path.Combine(logDirectory, "exitcode.txt");
            result = new SandboxResult
            {
                ExitCode = File.Exists(exitCodeFile) && int.TryParse(await File.ReadAllTextAsync(exitCodeFile, cancelToken), out int exitCode) ? exitCode : 0,
                Log = File.Exists(logFile) ? await File.ReadAllTextAsync(logFile, cancelToken) : null,
                InstalledApps = await ParseInstalledApps(Path.Combine(logDirectory, "installed.csv"), cancelToken)
            };
        }

        if (cleanup)
        {
            logger.LogDebug("Cleaning up sandbox files");
            Directory.Delete(Path.GetDirectoryName(sandboxFile)!, true);
        }
        return result;

    }

    private Task<IEnumerable<SandboxInstalledApps>?> ParseInstalledApps(string filename, CancellationToken cancellationToken = default)
    {
        // No longer working because of Unauthorized exception in Sandbox
        return Task.FromResult<IEnumerable<SandboxInstalledApps>?>(null);
        //// the file is a csv with headers Version,Vendor,Name and uses , as separator
        //// Parse the file if it exists
        //if (!File.Exists(filename))
        //{
        //    logger.LogWarning("Installed apps file not found {filename}", filename);
        //    return null;
        //}

        //var lines = await File.ReadAllLinesAsync(filename, cancellationToken);
        //if (lines.Length < 2)
        //{
        //    logger.LogWarning("Installed apps file is empty {filename}", filename);
        //    return null;
        //}

        //return lines.Skip(1).Select(l =>
        //{
        //    var parts = l.Split(',');
        //    return new SandboxInstalledApps
        //    {
        //        Version = parts[0].Trim('"'),
        //        Vendor = parts[1].Trim('"'),
        //        Name = parts[2].Trim('"')
        //    };
        //});
    }

    public class SandboxResult
    {
        public int ExitCode { get; set; } = 0;
        public string? Log { get; set; }
        public IEnumerable<SandboxInstalledApps>? InstalledApps { get; set; }

        public override string ToString()
        {
            return InstalledApps?.Any() == true ? $"ExitCode: {ExitCode}, Installed apps {string.Join(", ", InstalledApps.Select(i => i.Name))}" : $"Exit code: {ExitCode}";
        }

    }

    public class SandboxInstalledApps
    {
        public string? Version { get; set; }
        public string? Vendor { get; set; }
        public string? Name { get; set; }

        public override string ToString()
        {
            return $"{Name} by {Vendor}";
        }
    }
}
