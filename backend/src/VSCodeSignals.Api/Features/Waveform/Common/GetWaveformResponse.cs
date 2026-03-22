namespace VSCodeSignals.Api.Features.Waveform.Common;

public sealed class GetWaveformResponse
{
    public string FileId { get; init; } = string.Empty;

    public List<WaveformPoint> Points { get; init; } = [];

    public int SampleRateHz { get; init; }

    public string SourcePath { get; init; } = string.Empty;
}

public sealed class WaveformPoint
{
    public double Amplitude { get; init; }

    public double TimeSeconds { get; init; }
}
