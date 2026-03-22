using VSCodeSignals.Api.Features.Import.Commands;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Handlers;

public sealed class ImportFilesHandler(
    IEnumerable<IImportAdapter> adapters,
    ILogger<ImportFilesHandler> logger)
{
    public async Task<ImportFilesResult> ExecuteAsync(
        IReadOnlyList<string> filePaths,
        Dictionary<string, string>? sourceLabels,
        CancellationToken ct = default)
    {
        var expandedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var failedPaths = new List<ImportFailure>();
        var importedFiles = new List<ImportedSignalFile>();
        var adapterList = adapters.ToArray();
        var displayLabels = sourceLabels ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            ct.ThrowIfCancellationRequested();
            ProcessPath(path, expandedFilePaths, failedPaths);
        }

        if (expandedFilePaths.Count == 0)
            throw new InvalidOperationException("No valid files found to import. All provided paths failed.");

        foreach (var filePath in expandedFilePaths.Order(StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();
            var displayPath = ResolveDisplayPath(displayLabels, filePath);

            var adapter = adapterList.FirstOrDefault(current => current.CanImport(filePath));

            if (adapter is null)
            {
                failedPaths.Add(new ImportFailure
                {
                    Path = displayPath,
                    Reason = $"No registered import adapter for '{Path.GetExtension(filePath)}' files."
                });
                continue;
            }

            try
            {
                var importedFile = await adapter.ImportAsync(filePath, ct);
                importedFiles.Add(new ImportedSignalFile
                {
                    BatchId = importedFile.BatchId,
                    Adapter = importedFile.Adapter,
                    ChannelCount = importedFile.ChannelCount,
                    DurationSeconds = importedFile.DurationSeconds,
                    Format = importedFile.Format,
                    Id = importedFile.Id,
                    Metadata = importedFile.Metadata,
                    PreviewUrl = importedFile.PreviewUrl,
                    ResolvedPath = filePath,
                    SampleRateHz = importedFile.SampleRateHz,
                    SignalKind = importedFile.SignalKind,
                    SizeBytes = importedFile.SizeBytes,
                    SourcePath = displayPath
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Import failed for {Path} using adapter {Adapter}.", filePath, adapter.Name);
                failedPaths.Add(new ImportFailure
                {
                    Path = displayPath,
                    Reason = ex.Message
                });
            }
        }

        if (importedFiles.Count == 0)
            throw new InvalidOperationException("No supported files were imported successfully.");

        logger.LogInformation(
            "Prepared import request with {ImportedCount} imports and {FailedCount} failed paths.",
            importedFiles.Count,
            failedPaths.Count);

        return new ImportFilesResult
        {
            FailedPaths = failedPaths,
            ImportedFiles = importedFiles
        };
    }

    private static string ResolveDisplayPath(
        Dictionary<string, string> sourceLabels,
        string filePath)
    {
        return sourceLabels.TryGetValue(filePath, out var label)
            ? label
            : filePath;
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
            failedPaths.Add(new ImportFailure
            {
                Path = path,
                Reason = $"Failed to inspect path. {ex.Message}"
            });
            return;
        }

        failedPaths.Add(new ImportFailure
        {
            Path = path,
            Reason = "Path does not exist or could not be resolved."
        });
    }
}
