using Microsoft.Kiota.Abstractions;

namespace Svrooij.WinTuner.Proxy.Client;
public static class WinTunerProxyClientExtensions
{
    public const string WINTUNER_TELEMETRY_OPT_OUT = nameof(WINTUNER_TELEMETRY_OPT_OUT);

    /// <summary>
    /// Send usage event to the WinTuner Proxy API.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="sessionId">A random string to indicate a session</param>
    /// <param name="command">Which command is triggered?</param>
    /// <param name="packageId">What is the package ID</param>
    /// <param name="appVersion">What is the version of the app?</param>
    /// <param name="cancellationToken">(optional) cancellation token</param>
    /// <remarks>This is a fire-and-forget method, that triggers a task in the backend.</remarks>
    public static void TriggerEvent(this WinTunerProxyClient? client, string? sessionId, string command, string? packageId = null, string? appVersion = null, CancellationToken cancellationToken = default)
    {
        if (client is null) // || System.Environment.GetEnvironmentVariable(WINTUNER_TELEMETRY_OPT_OUT)?.Equals("1") == true)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await client.Event.PostAsync(new Models.UsageEventRequest
                {
                    SessionId = sessionId ?? Guid.NewGuid().ToString(),
                    Command = command,
                    PackageId = packageId,
                    AppVersion = appVersion,
                }, cancellationToken: cancellationToken);
            }
            catch (ApiException)
            {
                // ignore all exceptions, we just want to trigger the event
            }
            catch (Exception)
            {
                // Log the exception if needed, but do not throw it
                // This is a fire-and-forget method, so we don't want to disrupt the main flow
            }
        }, cancellationToken);
    }
}
