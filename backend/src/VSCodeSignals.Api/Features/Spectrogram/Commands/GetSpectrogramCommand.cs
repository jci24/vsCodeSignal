using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Spectrogram.Commands;

public sealed class GetSpectrogramCommand
{
    public string FileId { get; init; } = string.Empty;

    public SignalTransformRecipe Transforms { get; init; } = new();
}
