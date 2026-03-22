using VSCodeSignals.Api.Features.Fft.Commands;
using VSCodeSignals.Api.Features.Fft.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Fft.Handlers;

public sealed class GetFftHandler(
    ImportedAudioFileResolver importedAudioFileResolver,
    AudioAnalysisService audioAnalysisService)
{
    public async Task<GetFftResponse> ExecuteAsync(GetFftCommand command, CancellationToken ct)
    {
        var file = importedAudioFileResolver.Resolve(command.FileId);
        var signal = await audioAnalysisService.DecodeMonoAsync(file.ResolvedPath, ct);
        var bins = audioAnalysisService.BuildSpectrum(signal);

        return new GetFftResponse
        {
            Bins = bins
                .Select(bin => new FftBinResponse
                {
                    FrequencyHz = bin.FrequencyHz,
                    Magnitude = bin.Magnitude
                })
                .ToList(),
            FileId = file.Id,
            SourcePath = file.SourcePath
        };
    }
}
