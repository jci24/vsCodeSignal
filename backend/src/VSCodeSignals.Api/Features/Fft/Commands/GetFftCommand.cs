using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Fft.Commands;

public sealed class GetFftCommand
{
    public string FileId { get; init; } = string.Empty;

    public SignalTransformRecipe Transforms { get; init; } = new();
}
