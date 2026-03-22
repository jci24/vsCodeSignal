using System.Text.Json.Serialization;

namespace VSCodeSignals.Api.Features.Import.Common;

public sealed class ImportedSignalFile
{
    public string? BatchId { get; init; }

    public required string Adapter { get; init; }

    public int? ChannelCount { get; init; }

    public double? DurationSeconds { get; init; }

    public required string Format { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = [];

    public string? Id { get; init; }

    public string? PreviewUrl { get; init; }

    [JsonIgnore]
    public string? ResolvedPath { get; init; }

    public int? SampleRateHz { get; init; }

    public long SizeBytes { get; init; }

    public required string SignalKind { get; init; }

    public required string SourcePath { get; init; }
}
