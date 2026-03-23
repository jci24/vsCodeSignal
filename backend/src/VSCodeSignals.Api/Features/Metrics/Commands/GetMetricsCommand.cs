using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.Metrics.Commands;

public sealed class GetMetricsCommand
{
    public string FileId { get; init; } = string.Empty;

    public SignalTransformRecipe Transforms { get; init; } = new();
}
