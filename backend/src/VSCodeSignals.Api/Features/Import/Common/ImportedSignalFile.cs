namespace VSCodeSignals.Api.Features.Import.Common;

public sealed record ImportedSignalFile(
    string SourcePath,
    string Adapter,
    string Format,
    string SignalKind,
    long SizeBytes,
    double? DurationSeconds,
    int? SampleRateHz,
    int? ChannelCount,
    IReadOnlyDictionary<string, string> Metadata);
