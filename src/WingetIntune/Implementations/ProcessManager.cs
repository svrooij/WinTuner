using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace WingetIntune.Os;

public partial class ProcessManager : IProcessManager
{
    private readonly ILogger<ProcessManager> logger;

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        this.logger = logger;
    }

    public async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default, bool admin = false)//, string workingDirectory, bool waitForExit)
    {
        LogProcessStarting(fileName, arguments);
        CheckWindows(fileName);
        using var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        //process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.UseShellExecute = admin;
        process.StartInfo.Verb = admin ? "runas" : "";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var exitCode = process.ExitCode;
        if (exitCode == 0)
            LogProcessSuccess(exitCode);
        else
            LogProcessError(exitCode, error.ToString());

        return new ProcessResult(exitCode, output.ToString(), error.ToString());
    }

    private bool requiresWindows(string filename)
    {
        return filename.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || filename.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
            || filename.Equals("winget", StringComparison.OrdinalIgnoreCase);
    }

    private void CheckWindows(string filename)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && requiresWindows(filename))
            throw new PlatformNotSupportedException("This feature is only supported on Windows.");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Running {FileName} {Arguments}")]
    private partial void LogProcessStarting(string fileName, string arguments);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Process exited with code {ExitCode}.")]
    private partial void LogProcessSuccess(int ExitCode);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Process exited with code {ExitCode}. {Error}")]
    private partial void LogProcessError(int ExitCode, string? Error);
}
