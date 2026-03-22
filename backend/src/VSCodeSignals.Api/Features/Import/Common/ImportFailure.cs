namespace VSCodeSignals.Api.Features.Import.Common;

public sealed record ImportFailure(
    string Path,
    string Reason);
