using FastEndpoints;
using VSCodeSignals.Api.Features.Import.Commands;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Handlers;

public sealed class ImportFilesHandler(
    IEnumerable<IImportAdapter> adapters,
    ILogger<ImportFilesHandler> logger)
    : CommandHandler<ImportFilesCommand, ImportFilesResult>
{
    public override async Task<ImportFilesResult> ExecuteAsync(
        ImportFilesCommand command,
        CancellationToken ct = default)
    {
        var expandedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failedPaths = new List<ImportFailure>();
        var importedFiles = new List<ImportedSignalFile>();
        var adapterList = adapters.ToArray();

        foreach (var path in command.FilePaths)
        {
            ct.ThrowIfCancellationRequested();
            ProcessPath(path, expandedFilePaths, failedPaths);
        }

        if (expandedFilePaths.Count == 0)
            ThrowError("No valid files found to import. All provided paths failed.");

        foreach (var filePath in expandedFilePaths.Order(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            var adapter = adapterList.FirstOrDefault(current => current.CanImport(filePath));

            if (adapter is null)
            {
                failedPaths.Add(new ImportFailure(
                    filePath,
                    $"No registered import adapter for '{Path.GetExtension(filePath)}' files."));
                continue;
            }

            try
            {
                var importedFile = await adapter.ImportAsync(filePath, ct);
                importedFiles.Add(importedFile);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Import failed for {Path} using adapter {Adapter}.", filePath, adapter.Name);
                failedPaths.Add(new ImportFailure(filePath, ex.Message));
            }
        }

        if (importedFiles.Count == 0)
            ThrowError("No supported files were imported successfully.");

        logger.LogInformation(
            "Prepared import request with {ImportedCount} imports and {FailedCount} failed paths.",
            importedFiles.Count,
            failedPaths.Count);

        return new ImportFilesResult(importedFiles, failedPaths);
    }

    private void ProcessPath(
        string path,
        HashSet<string> expandedFiles,
        List<ImportFailure> failedPaths)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                    expandedFiles.Add(file);

                return;
            }

            if (File.Exists(path))
            {
                expandedFiles.Add(path);
                return;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to inspect import path {Path}.", path);
            failedPaths.Add(new ImportFailure(path, $"Failed to inspect path. {ex.Message}"));
            return;
        }

        failedPaths.Add(new ImportFailure(path, "Path does not exist or could not be resolved."));
    }
}
