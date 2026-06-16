using System.Reflection;

namespace WingetIntune.Intune;

internal class IntuneManagerConstants
{
    internal const string AllUsers = "AllUsers";
    internal const string AllDevices = "AllDevices";

    internal const string PowerShellPath = "%windir%\\sysnative\\windowspowershell\\v1.0\\powershell.exe";

    internal static string GetPsDetectionCommand(string packageId, string version)
    {
        var script = getResourceScript("WingetDetection.ps1");
        return script.Replace("{packageId}", packageId).Replace("{version}", version);
    }

    internal static string GetPsWingetCmd(string action, string command, string successString, string message, string? packageId = null)
    {
        var script = getResourceScript("WingetCommand.ps1")
            .Replace("{action}", action)
            .Replace("{command}", command)
            .Replace("{success}", successString)
            .Replace("{message}", message)
            .Replace("{packageId}", packageId ?? Guid.NewGuid().ToString());

        return script;
    }

    internal static string GetPsGetWingetCmd()
    {
        return getResourceScript("FunctionGetWingetCmd.ps1");
    }

    private static string getResourceScript(string filename)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"WingetIntune.Scripts.{filename}";
        using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
        using (StreamReader reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
