using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.AiAssistant.Handlers;
using VSCodeSignals.Api.Features.Import.Handlers;
using VSCodeSignals.Api.Features.Import.Common;
using VSCodeSignals.Api.Features.Fft.Handlers;
using VSCodeSignals.Api.Features.Metrics.Handlers;
using VSCodeSignals.Api.Features.Spectrogram.Handlers;
using VSCodeSignals.Api.Features.Waveform.Handlers;
using VSCodeSignals.Api.Features.Workspaces.Handler;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.App.Host;

public static class DependencyInjection
{
    public static IServiceCollection AddFeatureHandlers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AiAssistantOptions>(configuration.GetSection("AiAssistant"));
        services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.AddHttpClient<OpenAiLlmProvider>();
        services.AddHttpClient<OllamaLlmProvider>();
        services.AddSingleton<AiExecutionReceiptStore>();
        services.AddSingleton<AudioAnalysisService>();
        services.AddSingleton<IAiActionValidator, AiActionValidator>();
        services.AddSingleton<IAiIntentClassifier, RuleBasedAiIntentClassifier>();
        services.AddSingleton<IModelRoutingService, ModelRoutingService>();
        services.AddSingleton<IObservationService, ObservationService>();
        services.AddSingleton<IAiPromptBuilder, AiPromptBuilder>();
        services.AddSingleton<ISignalAnalysisService, SignalAnalysisService>();
        services.AddSingleton<IWorkspaceCommandExecutor, WorkspaceCommandExecutor>();
        services.AddSingleton<IWorkspaceContextService, WorkspaceContextService>();
        services.AddSingleton<LlmProviderRegistry>();
        services.AddSingleton<ImportedAudioFileResolver>();
        services.AddSingleton<ILlmProvider, OpenAiLlmProvider>();
        services.AddSingleton<ILlmProvider, OllamaLlmProvider>();
        services.AddSingleton<WorkspaceImportStore>();
        services.AddScoped<AiResponseComposer>();
        services.AddScoped<IAiActionPlanner, AiActionPlanner>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();
        services.AddScoped<IImportAdapter, FfmpegAudioImportAdapter>();
        services.AddScoped<IImportAdapter, UffImportAdapter>();
        services.AddScoped<GetFftHandler>();
        services.AddScoped<GetMetricsHandler>();
        services.AddScoped<GetSpectrogramHandler>();
        services.AddScoped<GetWaveformHandler>();
        services.AddScoped<ImportFilesHandler>();

        return services;
    }
}
