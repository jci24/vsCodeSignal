namespace VSCodeSignals.Api.Features.Fft.Common;

public sealed class GetFftResponse
{
    public List<FftBinResponse> Bins { get; init; } = [];

    public string FileId { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;
}

public sealed class FftBinResponse
{
    public double FrequencyHz { get; init; }

    public double Magnitude { get; init; }
}
