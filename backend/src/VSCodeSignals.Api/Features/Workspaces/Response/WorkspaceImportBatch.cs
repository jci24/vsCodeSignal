using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Workspaces.Response;

public sealed class WorkspaceImportBatch
{
    public List<ImportFailure> FailedPaths { get; init; } = [];

    public int FailedFileCount { get; init; }

    public required string Id { get; init; }

    public List<WorkspaceImportedFile> ImportedFiles { get; init; } = [];

    public int ImportedFileCount { get; init; }

    public DateTimeOffset ImportedAtUtc { get; init; }
}
