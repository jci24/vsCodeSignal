namespace VSCodeSignals.Api.Features.Workspaces.Response;

public sealed class WorkspaceImportsResponse
{
    public int BatchCount { get; init; }

    public List<WorkspaceImportBatch> Batches { get; init; } = [];

    public int FailedFileCount { get; init; }

    public int ImportedFileCount { get; init; }

    public string WorkspaceId { get; init; } = "current";
}
