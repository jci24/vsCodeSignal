using VSCodeSignals.Api.Features.Waveform.Commands;
using VSCodeSignals.Api.Features.Waveform.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Waveform.Handlers;

public sealed class GetWaveformHandler(
    ImportedAudioFileResolver importedAudioFileResolver,
    AudioAnalysisService audioAnalysisService)
{
    public async Task<GetWaveformResponse> ExecuteAsync(
        GetWaveformCommand command,
        CancellationToken ct)
    {
        var file = importedAudioFileResolver.Resolve(command.FileId);
        var signal = await audioAnalysisService.DecodeMonoAsync(file.ResolvedPath, ct);
        var frames = audioAnalysisService.BuildWaveform(signal);

        return new GetWaveformResponse
        {
            FileId = file.Id,
            Points = frames
                .Select(point => new WaveformPoint
                {
                    Amplitude = point.Amplitude,
                    TimeSeconds = point.TimeSeconds
                })
                .ToList(),
            SampleRateHz = signal.SampleRate,
            SourcePath = file.SourcePath
        };
    }
}
