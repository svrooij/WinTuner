using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Http.HttpClientLibrary.Middleware;

namespace Svrooij.WinTuner.Proxy.Client;
public static class WinTunerProxyServiceExtensions
{
    public static IServiceCollection AddWinTunerProxyClient(this IServiceCollection services, Action<WinTunerProxyClientOptions> configure)
    {
        services.Configure(configure);
        // Only add the Kiota handlers if they are not already present
        if (!services.Any(s => s.ServiceType == typeof(RetryHandler)))
        {
            services.AddKiotaHandlers();
        }

        services.AddHttpClient<WinTunerProxyClientFactory>().AttachKiotaHandlers();
        services.AddTransient<WinTunerProxyClient>((sp) => sp.GetRequiredService<WinTunerProxyClientFactory>().GetClient());
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
