using VSCodeSignals.Api.Features.Import.Handlers;
using VSCodeSignals.Api.Features.Import.Common;
using VSCodeSignals.Api.Features.Fft.Handlers;
using VSCodeSignals.Api.Features.Spectrogram.Handlers;
using VSCodeSignals.Api.Features.Waveform.Handlers;
using VSCodeSignals.Api.Features.Workspaces.Handler;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.App.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatureHandlers(this IServiceCollection services)
    {
        services.AddSingleton<AudioAnalysisService>();
        services.AddSingleton<ImportedAudioFileResolver>();
        services.AddSingleton<WorkspaceImportStore>();
        services.AddScoped<IImportAdapter, FfmpegAudioImportAdapter>();
        services.AddScoped<IImportAdapter, UffImportAdapter>();
        services.AddScoped<GetFftHandler>();
        services.AddScoped<GetSpectrogramHandler>();
        services.AddScoped<GetWaveformHandler>();
        services.AddScoped<ImportFilesHandler>();

        return services;
    }
}
