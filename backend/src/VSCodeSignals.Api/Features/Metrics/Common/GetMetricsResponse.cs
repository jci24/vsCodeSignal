namespace VSCodeSignals.Api.Features.Metrics.Common;

public sealed class GetMetricsResponse
{
    public double CrestFactor { get; init; }

    public double DominantFrequencyHz { get; init; }

    public double DominantMagnitudeDb { get; init; }

    public double DurationSeconds { get; init; }

    public string FileId { get; init; } = string.Empty;

    public double Peak { get; init; }

    public double Rms { get; init; }

    public int SampleRateHz { get; init; }

    public string SourcePath { get; init; } = string.Empty;
}
