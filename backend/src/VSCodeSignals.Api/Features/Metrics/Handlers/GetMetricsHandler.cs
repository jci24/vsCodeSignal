using VSCodeSignals.Api.Features.Metrics.Commands;
using VSCodeSignals.Api.Features.Metrics.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Metrics.Handlers;

public sealed class GetMetricsHandler(
    ImportedAudioFileResolver importedAudioFileResolver,
    AudioAnalysisService audioAnalysisService)
{
    public async Task<GetMetricsResponse> ExecuteAsync(GetMetricsCommand command, CancellationToken ct)
    {
        var file = importedAudioFileResolver.Resolve(command.FileId);
        var signal = await audioAnalysisService.DecodeMonoAsync(file.ResolvedPath, ct);
        signal = audioAnalysisService.ApplyTransforms(signal, command.Transforms);
        var metrics = audioAnalysisService.BuildMetrics(signal);

        return new GetMetricsResponse
        {
            CrestFactor = metrics.CrestFactor,
            DominantFrequencyHz = metrics.DominantFrequencyHz,
            DominantMagnitudeDb = metrics.DominantMagnitudeDb,
            DurationSeconds = metrics.DurationSeconds,
            FileId = file.Id,
            Peak = metrics.Peak,
            Rms = metrics.Rms,
            SampleRateHz = metrics.SampleRate,
            SourcePath = file.SourcePath
        };
    }
}
