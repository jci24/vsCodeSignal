using MessagePack;

namespace VSCodeSignals.Api.Features.Importer.Command;

[MessagePackObject]
public sealed record ImportSignalCommand(
    [property: Key(0)] string FileName,
    [property: Key(1)] long FileSizeBytes);
