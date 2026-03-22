namespace VSCodeSignals.Api.Features.Import.Common;

public sealed record ImportFilesResult(
    IReadOnlyList<ImportedSignalFile> ImportedFiles,
    IReadOnlyList<ImportFailure> FailedPaths);
