using FastEndpoints;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using VSCodeSignals.Api.Features.Import.Commands;
using VSCodeSignals.Api.Features.Import.Common;
using VSCodeSignals.Api.Features.Import.Handlers;

namespace VSCodeSignals.Api.Features.Import.Endpoints;

public sealed class ImportFiles(
    ImportFilesHandler handler,
    ILogger<ImportFiles> logger) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/");
        Group<ImportGroup>();
        AllowAnonymous();
        AllowFileUploads();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await ReadRequestAsync(ct);

        var requestedPaths = (payload.FilePaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var stagedUpload = await StageUploadedFilesAsync(ct);

        if (requestedPaths.Length == 0 && stagedUpload.Paths.Count == 0)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = "Provide file paths or upload one or more files." },
                cancellationToken: ct);
            return;
        }

        var sourceLabels = new Dictionary<string, string>(stagedUpload.SourceLabels, StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = await handler.ExecuteAsync(
                [.. requestedPaths, .. stagedUpload.Paths],
                sourceLabels,
                ct);
            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = ex.Message },
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled import failure.");
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(
                new { message = "The import request failed on the server." },
                cancellationToken: ct);
        }
        finally
        {
            TryDeleteDirectory(stagedUpload.TempDirectory);
        }
    }

    private async Task<ImportFilesCommand> ReadRequestAsync(CancellationToken ct)
    {
        if (HttpContext.Request.HasFormContentType)
        {
            var form = await HttpContext.Request.ReadFormAsync(ct);
            var filePaths = form["filePaths"]
                .ToArray()
                .SelectMany(value => (value ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return new ImportFilesCommand
            {
                FilePaths = filePaths
            };
        }

        var payload = await HttpContext.Request.ReadFromJsonAsync<ImportFilesCommand>(cancellationToken: ct);

        return payload ?? new ImportFilesCommand();
    }

    private async Task<StagedUpload> StageUploadedFilesAsync(CancellationToken ct)
    {
        if (Files.Count == 0)
            return StagedUpload.Empty;

        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "signal-studio-imports",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDirectory);

        var paths = new List<string>(Files.Count);
        var sourceLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Files)
        {
            ct.ThrowIfCancellationRequested();

            if (file.Length == 0 || string.IsNullOrWhiteSpace(file.FileName))
                continue;

            var safeName = Path.GetFileName(file.FileName);
            var destinationPath = GetUniqueDestinationPath(tempDirectory, safeName);

            await using var destinationStream = File.Create(destinationPath);
            await using var uploadStream = file.OpenReadStream();
            await uploadStream.CopyToAsync(destinationStream, ct);

            paths.Add(destinationPath);
            sourceLabels[destinationPath] = safeName;
        }

        return new StagedUpload(tempDirectory, paths, sourceLabels);
    }

    private static string GetUniqueDestinationPath(string tempDirectory, string fileName)
    {
        var sanitizedBaseName = string.Concat(
            fileName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        var destinationPath = Path.Combine(tempDirectory, sanitizedBaseName);

        if (!File.Exists(destinationPath))
            return destinationPath;

        var extension = Path.GetExtension(sanitizedBaseName);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sanitizedBaseName);
        var suffix = 1;

        while (true)
        {
            var candidatePath = Path.Combine(
                tempDirectory,
                $"{fileNameWithoutExtension}-{suffix}{extension}");

            if (!File.Exists(candidatePath))
                return candidatePath;

            suffix++;
        }
    }

    private static void TryDeleteDirectory(string? tempDirectory)
    {
        if (string.IsNullOrWhiteSpace(tempDirectory) || !Directory.Exists(tempDirectory))
            return;

        try
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for staged browser uploads.
        }
    }

    private sealed record StagedUpload(
        string? TempDirectory,
        IReadOnlyList<string> Paths,
        IReadOnlyDictionary<string, string> SourceLabels)
    {
        public static StagedUpload Empty { get; } = new(null, [], new Dictionary<string, string>());
    }
}
