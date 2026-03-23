namespace VSCodeSignals.Api.Shared.SignalAnalysis;

public sealed class SignalTransformRecipe
{
    public SignalFilterRecipe Filter { get; init; } = new();

    public double GainDb { get; init; }

    public bool Normalize { get; init; }

    public bool TrimSilence { get; init; }
}
