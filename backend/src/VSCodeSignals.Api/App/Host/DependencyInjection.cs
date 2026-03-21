using VSCodeSignals.Api.Features.Importer.Handler;

namespace VSCodeSignals.Api.App.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        services.AddScoped<ImportSignalHandler>();

        return services;
    }
}
