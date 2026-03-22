using System.Text.Json.Serialization;

namespace VSCodeSignals.Api.Features.Workspaces.Response;

public sealed class WorkspaceImportedFile
{
    public required string Id { get; init; }

    public required string BatchId { get; init; }

    public required string Adapter { get; init; }

    public int? ChannelCount { get; init; }

    public double? DurationSeconds { get; init; }

    public required string Format { get; init; }

    public DateTimeOffset ImportedAtUtc { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = [];

    public required string PreviewUrl { get; init; }

    [JsonIgnore]
    public string ResolvedPath { get; init; } = string.Empty;

    public int? SampleRateHz { get; init; }

    public long SizeBytes { get; init; }

    public required string SignalKind { get; init; }

    public required string SourcePath { get; init; }

    public bool StoredInWorkspace { get; init; }
}
