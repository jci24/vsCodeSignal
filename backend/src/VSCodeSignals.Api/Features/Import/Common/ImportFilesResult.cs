namespace VSCodeSignals.Api.Features.Import.Common;

public sealed class ImportFilesResult
{
    public List<ImportFailure> FailedPaths { get; init; } = [];

    public List<ImportedSignalFile> ImportedFiles { get; init; } = [];
}
