namespace VSCodeSignals.Api.Features.Spectrogram.Common;

public sealed class GetSpectrogramResponse
{
    public List<SpectrogramCellResponse> Cells { get; init; } = [];

    public string FileId { get; init; } = string.Empty;

    public List<double> Frequencies { get; init; } = [];

    public string SourcePath { get; init; } = string.Empty;

    public List<double> Times { get; init; } = [];
}

public sealed class SpectrogramCellResponse
{
    public int FrequencyIndex { get; init; }

    public double Intensity { get; init; }

    public int TimeIndex { get; init; }
}
