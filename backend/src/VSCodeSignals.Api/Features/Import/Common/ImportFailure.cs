namespace VSCodeSignals.Api.Features.Import.Common;

public sealed class ImportFailure
{
    public required string Path { get; init; }

    public required string Reason { get; init; }
}
