namespace VSCodeSignals.Api.Features.Import.Common;

public sealed class ImportFilesResult
{
    public string? BatchId { get; init; }

    public List<ImportFailure> FailedPaths { get; init; } = [];

    public List<ImportedSignalFile> ImportedFiles { get; init; } = [];

    public int WorkspaceBatchCount { get; init; }

    public int WorkspaceImportedFileCount { get; init; }

    public string WorkspaceId { get; init; } = "current";
}
