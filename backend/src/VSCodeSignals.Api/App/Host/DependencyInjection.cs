using VSCodeSignals.Api.Features.Import.Handlers;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.App.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        services.AddScoped<IImportAdapter, FfmpegAudioImportAdapter>();
        services.AddScoped<IImportAdapter, UffImportAdapter>();
        services.AddScoped<ImportFilesHandler>();

        return services;
    }
}
