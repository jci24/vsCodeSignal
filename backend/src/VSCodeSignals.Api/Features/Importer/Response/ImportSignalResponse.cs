using MessagePack;

namespace VSCodeSignals.Api.Features.Importer.Response;

[MessagePackObject]
public sealed record ImportSignalResponse(
    [property: Key(0)] Guid ImportId,
    [property: Key(1)] string Status,
    [property: Key(2)] string Message);
