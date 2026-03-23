using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Waveform.Commands;

public sealed class GetWaveformCommand
{
    public string FileId { get; init; } = string.Empty;

    public SignalTransformRecipe Transforms { get; init; } = new();
}
