using VSCodeSignals.Api.Features.Spectrogram.Commands;
using VSCodeSignals.Api.Features.Spectrogram.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Spectrogram.Handlers;

public sealed class GetSpectrogramHandler(
    ImportedAudioFileResolver importedAudioFileResolver,
    AudioAnalysisService audioAnalysisService)
{
    public async Task<GetSpectrogramResponse> ExecuteAsync(
        GetSpectrogramCommand command,
        CancellationToken ct)
    {
        var file = importedAudioFileResolver.Resolve(command.FileId);
        var signal = await audioAnalysisService.DecodeMonoAsync(file.ResolvedPath, ct);
        signal = audioAnalysisService.ApplyTransforms(signal, command.Transforms);
        var spectrogram = audioAnalysisService.BuildSpectrogram(signal);

        return new GetSpectrogramResponse
        {
            Cells = spectrogram.Cells
                .Select(cell => new SpectrogramCellResponse
                {
                    FrequencyIndex = cell.FrequencyIndex,
                    Intensity = cell.Intensity,
                    TimeIndex = cell.TimeIndex
                })
                .ToList(),
            FileId = file.Id,
            Frequencies = spectrogram.Frequencies.ToList(),
            SourcePath = file.SourcePath,
            Times = spectrogram.Times.ToList()
        };
    }
}
