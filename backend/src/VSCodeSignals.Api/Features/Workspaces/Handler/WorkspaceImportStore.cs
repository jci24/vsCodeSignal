using VSCodeSignals.Api.Features.Import.Common;
using VSCodeSignals.Api.Features.Workspaces.Response;

namespace VSCodeSignals.Api.Features.Workspaces.Handler;

public sealed class WorkspaceImportStore
{
    public const string CurrentWorkspaceId = "current";

    private readonly object sync = new();
    private readonly string storageRoot = Path.Combine(
        Path.GetTempPath(),
        "signal-studio-workspaces",
        CurrentWorkspaceId);
    private readonly List<WorkspaceImportBatch> batches = [];
    private readonly Dictionary<string, WorkspaceImportedFile> filesById = new(StringComparer.OrdinalIgnoreCase);

    public WorkspaceImportsResponse AppendImportBatch(
        ImportFilesResult result,
        IReadOnlySet<string> uploadedPaths)
    {
        lock (sync)
        {
            Directory.CreateDirectory(storageRoot);

            var importedAtUtc = DateTimeOffset.UtcNow;
            var batchId = Guid.NewGuid().ToString("N");
            var importedFiles = result.ImportedFiles
                .Select(file => CreateWorkspaceFile(file, batchId, importedAtUtc, uploadedPaths))
                .ToList();

            var batch = new WorkspaceImportBatch
            {
                FailedPaths = result.FailedPaths
                    .Select(failure => new ImportFailure
                    {
                        Path = failure.Path,
                        Reason = failure.Reason
                    })
                    .ToList(),
                FailedFileCount = result.FailedPaths.Count,
                Id = batchId,
                ImportedFiles = importedFiles,
                ImportedFileCount = importedFiles.Count,
                ImportedAtUtc = importedAtUtc
            };

            batches.Insert(0, batch);

            foreach (var file in importedFiles)
                filesById[file.Id] = file;

            return BuildSnapshot();
        }
    }

    public WorkspaceImportedFile? GetImportedFile(string fileId)
    {
        lock (sync)
        {
            if (!filesById.TryGetValue(fileId, out var file))
                return null;

            return CloneWorkspaceFile(file);
        }
    }

    public WorkspaceImportsResponse GetSnapshot()
    {
        lock (sync)
        {
            return BuildSnapshot();
        }
    }

    private WorkspaceImportsResponse BuildSnapshot()
    {
        return new WorkspaceImportsResponse
        {
            BatchCount = batches.Count,
            Batches = batches.Select(CloneBatch).ToList(),
            FailedFileCount = batches.Sum(batch => batch.FailedFileCount),
            ImportedFileCount = batches.Sum(batch => batch.ImportedFileCount),
            WorkspaceId = CurrentWorkspaceId
        };
    }

    private WorkspaceImportBatch CloneBatch(WorkspaceImportBatch batch)
    {
        return new WorkspaceImportBatch
        {
            FailedPaths = batch.FailedPaths
                .Select(failure => new ImportFailure
                {
                    Path = failure.Path,
                    Reason = failure.Reason
                })
                .ToList(),
            FailedFileCount = batch.FailedFileCount,
            Id = batch.Id,
            ImportedFiles = batch.ImportedFiles.Select(CloneWorkspaceFile).ToList(),
            ImportedFileCount = batch.ImportedFileCount,
            ImportedAtUtc = batch.ImportedAtUtc
        };
    }

    private WorkspaceImportedFile CloneWorkspaceFile(WorkspaceImportedFile file)
    {
        return new WorkspaceImportedFile
        {
            Adapter = file.Adapter,
            BatchId = file.BatchId,
            ChannelCount = file.ChannelCount,
            DurationSeconds = file.DurationSeconds,
            Format = file.Format,
            Id = file.Id,
            ImportedAtUtc = file.ImportedAtUtc,
            Metadata = new Dictionary<string, string>(file.Metadata, StringComparer.OrdinalIgnoreCase),
            PreviewUrl = file.PreviewUrl,
            ResolvedPath = file.ResolvedPath,
            SampleRateHz = file.SampleRateHz,
            SignalKind = file.SignalKind,
            SizeBytes = file.SizeBytes,
            SourcePath = file.SourcePath,
            StoredInWorkspace = file.StoredInWorkspace
        };
    }

    private WorkspaceImportedFile CreateWorkspaceFile(
        ImportedSignalFile file,
        string batchId,
        DateTimeOffset importedAtUtc,
        IReadOnlySet<string> uploadedPaths)
    {
        var fileId = Guid.NewGuid().ToString("N");
        var resolvedPath = file.ResolvedPath ?? file.SourcePath;
        var storedInWorkspace = uploadedPaths.Contains(resolvedPath);
        var workspacePath = storedInWorkspace
            ? CopyUploadedFileIntoWorkspace(fileId, resolvedPath, file.Format)
            : resolvedPath;

        return new WorkspaceImportedFile
        {
            Adapter = file.Adapter,
            BatchId = batchId,
            ChannelCount = file.ChannelCount,
            DurationSeconds = file.DurationSeconds,
            Format = file.Format,
            Id = fileId,
            ImportedAtUtc = importedAtUtc,
            Metadata = new Dictionary<string, string>(file.Metadata, StringComparer.OrdinalIgnoreCase),
            PreviewUrl = $"/workspaces/current/imports/files/{fileId}/content",
            ResolvedPath = workspacePath,
            SampleRateHz = file.SampleRateHz,
            SignalKind = file.SignalKind,
            SizeBytes = file.SizeBytes,
            SourcePath = file.SourcePath,
            StoredInWorkspace = storedInWorkspace
        };
    }

    private string CopyUploadedFileIntoWorkspace(string fileId, string sourcePath, string format)
    {
        var extension = Path.GetExtension(sourcePath);

        if (string.IsNullOrWhiteSpace(extension) && !string.IsNullOrWhiteSpace(format))
            extension = $".{format.TrimStart('.')}";

        var destinationPath = Path.Combine(storageRoot, $"{fileId}{extension}");
        File.Copy(sourcePath, destinationPath, overwrite: true);
        return destinationPath;
    }
}
