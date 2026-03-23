namespace VSCodeSignals.Api.Shared.SignalAnalysis;

public sealed class SignalFilterRecipe
{
    public double CutoffHz { get; init; } = 1200d;

    public double HighCutoffHz { get; init; } = 3500d;

    public double LowCutoffHz { get; init; } = 250d;

    public string Mode { get; init; } = "none";

    public double Q { get; init; } = 0.707d;
}
