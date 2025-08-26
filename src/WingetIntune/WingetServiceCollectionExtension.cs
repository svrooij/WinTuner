using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Http.HttpClientLibrary;
using WingetIntune.Interfaces;
using WingetIntune.Intune;
using WingetIntune.Testing;
using Svrooij.WinTuner.Proxy.Client;
[assembly: InternalsVisibleTo("WingetIntune.Tests")]
namespace WingetIntune;

public static class WingetServiceCollectionExtension
{
    public const string DEFAULT_PROXY_CODE = "*REPLACED_AT_BUILD*";
    public static IServiceCollection AddWingetServices(this IServiceCollection services, string? proxyCode = null)
    {
        services.ConfigureHttpClientDefaults(config =>
        {
            config.ConfigureHttpClient(client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "WinTuner");
                client.Timeout = TimeSpan.FromSeconds(180);
                // Set buffer size to 500MB
                // TODO: fix this in some other way, by not loading the whole file into memory.
                client.MaxResponseContentBufferSize = 1024L * 1024 * 50;
            });
        });

        services.AddKiotaHandlers();
        services.AddHttpClient<Graph.GraphClientFactory>((sp, client) =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/beta");
        }).AttachKiotaHandlers();

        services.AddTransient<IFileManager, Os.DefaultFileManager>();
        services.AddTransient<IProcessManager, Os.ProcessManager>();
        services.AddTransient<IWingetRepository, Implementations.WingetManager>();
        if (Environment.GetEnvironmentVariable("WINGETINTUNE_USE_PROCESS_PACKAGER") == "true")
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new NotSupportedException("Process packager is only supported on Windows");
            }
            services.AddTransient<IIntunePackager, ProcessIntunePackager>();
        }
        else
        {
            services.AddTransient<IIntunePackager, LibraryIntunePackager>();
        }

        // Old IAzureFileUploader implementation is not used anymore, it does not work correctly.
        //services.AddTransient<IAzureFileUploader, Implementations.AzCopyAzureUploader>();
        services.AddTransient<IAzureFileUploader, Implementations.ChunkedAzureFileUploader>();
        services.AddTransient<Intune.MetadataManager>();
        services.AddTransient<Graph.GraphAppUploader>();
        services.AddTransient<Graph.GraphStoreAppUploader>();
        services.AddSingleton<Internal.Msal.PublicClientAuth>();
        services.AddSingleton<IntuneManager>();
        services.AddTransient<WindowsSandbox>();
        services.AddTransient<Commands.ComputeBestInstallerForPackageCommand>();

        services.AddWinTunerProxyClient(config =>
        {
            var codeFromEnv = Environment.GetEnvironmentVariable("WINTUNER_PROXY_CODE");
            if (!string.IsNullOrEmpty(codeFromEnv))
            {
                config.Code = codeFromEnv;
            }
            else if (!string.IsNullOrEmpty(proxyCode) && proxyCode != DEFAULT_PROXY_CODE)
            {
                config.Code = proxyCode;
            }
            var proxyUrl = Environment.GetEnvironmentVariable("WINTUNER_PROXY_URL");
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                config.BaseAddress = new Uri(proxyUrl);
            }
        });

        return services;
    }

    private static IServiceCollection AddKiotaHandlers(this IServiceCollection services)
    {
        // Dynamically load the Kiota handlers from the Client Factory
        var kiotaHandlers = KiotaClientFactory.GetDefaultHandlerActivatableTypes();
        // And register them in the DI container
        foreach (var handler in kiotaHandlers)
        {
            services.AddTransient(handler);
        }

        return services;
    }

    private static IHttpClientBuilder AttachKiotaHandlers(this IHttpClientBuilder builder)
    {
        // Dynamically load the Kiota handlers from the Client Factory
        var kiotaHandlers = KiotaClientFactory.GetDefaultHandlerActivatableTypes();
        // And attach them to the http client builder
        foreach (var handler in kiotaHandlers)
        {
            builder.AddHttpMessageHandler((sp) => (DelegatingHandler)sp.GetRequiredService(handler));
        }

        return builder;
    }
}
