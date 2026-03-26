using System.Globalization;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.Workspaces.Handler;
using VSCodeSignals.Api.Features.Workspaces.Response;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class WorkspaceContextService(
    WorkspaceImportStore workspaceImportStore,
    ImportedAudioFileResolver importedAudioFileResolver) : IWorkspaceContextService
{
    public Task<WorkspaceContextDto> BuildAsync(AiWorkspaceRequestContextDto request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var snapshot = workspaceImportStore.GetSnapshot();
        var files = snapshot.Batches
            .SelectMany(batch => batch.ImportedFiles)
            .Select(ToFileReference)
            .ToList();

        if (!string.Equals(request.WorkspaceId, WorkspaceImportStore.CurrentWorkspaceId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the current workspace is supported in this MVP.");

        if (string.IsNullOrWhiteSpace(request.FileId))
        {
            var emptySelection = NormalizeSelection(request.Selection, durationSeconds: null, out var emptySelectionWarning);
            var emptyWarnings = string.IsNullOrWhiteSpace(emptySelectionWarning)
                ? new List<string>()
                : new List<string> { emptySelectionWarning };

            return Task.FromResult(new WorkspaceContextDto
            {
                ActiveView = NormalizeView(request.ActiveView),
                AvailableFiles = files,
                IsSelectionApplied = emptySelection is not null,
                Selection = emptySelection,
                SelectionScope = DescribeSelectionScope(emptySelection),
                SupportedCommands = AiAssistantCommandCatalog.All.ToList(),
                Transforms = request.Transforms,
                Warnings = emptyWarnings,
                WorkspaceId = snapshot.WorkspaceId
            });
        }

        var selectedFile = importedAudioFileResolver.Resolve(request.FileId);
        var warnings = new List<string>();
        var compareFiles = new List<WorkspaceImportedFile>();
        var normalizedSelection = NormalizeSelection(request.Selection, selectedFile.DurationSeconds, out var selectionWarning);

        if (!string.IsNullOrWhiteSpace(selectionWarning))
            warnings.Add(selectionWarning);

        foreach (var compareFileId in request.CompareFileIds
                     .Where(id => !string.Equals(id, selectedFile.Id, StringComparison.OrdinalIgnoreCase))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(4))
        {
            try
            {
                compareFiles.Add(importedAudioFileResolver.Resolve(compareFileId));
            }
            catch (InvalidOperationException)
            {
                warnings.Add($"Comparison file {compareFileId} is unavailable and was ignored.");
            }
        }

        return Task.FromResult(new WorkspaceContextDto
        {
            ActiveView = NormalizeView(request.ActiveView),
            AvailableFiles = files,
            CompareFileIds = compareFiles.Select(file => file.Id).ToList(),
            CompareFiles = compareFiles.Select(ToFileReference).ToList(),
            IsSelectionApplied = normalizedSelection is not null,
            SelectedFile = ToFileReference(selectedFile),
            SelectedFileId = selectedFile.Id,
            Selection = normalizedSelection,
            SelectionScope = DescribeSelectionScope(normalizedSelection),
            SupportedCommands = AiAssistantCommandCatalog.All.ToList(),
            Transforms = request.Transforms,
            Warnings = warnings,
            WorkspaceId = snapshot.WorkspaceId
        });
    }

    private static WorkspaceFileReferenceDto ToFileReference(WorkspaceImportedFile file) =>
        new()
        {
            FileId = file.Id,
            SignalKind = file.SignalKind,
            SourcePath = file.SourcePath
        };

    private static string NormalizeView(string view)
    {
        var normalized = string.IsNullOrWhiteSpace(view)
            ? "waveform"
            : view.Trim().ToLowerInvariant();

        return normalized is "waveform" or "fft" or "spectrogram"
            ? normalized
            : "waveform";
    }

    private static SelectionRangeDto? NormalizeSelection(
        SelectionRangeDto? selection,
        double? durationSeconds,
        out string? warning)
    {
        warning = null;

        if (selection is null)
            return null;

        var start = Math.Max(0d, selection.StartSeconds ?? 0d);
        var end = selection.EndSeconds ?? durationSeconds ?? start;

        if (durationSeconds is > 0d)
        {
            var clampedStart = Math.Min(start, durationSeconds.Value);
            var clampedEnd = Math.Min(Math.Max(end, 0d), durationSeconds.Value);

            if (Math.Abs(clampedStart - start) > 0.0001d || Math.Abs(clampedEnd - end) > 0.0001d)
                warning = "Selection was clamped to the available signal duration.";

            start = clampedStart;
            end = clampedEnd;
        }
        else
        {
            end = Math.Max(end, 0d);
        }

        if (end <= start)
        {
            warning = "Selection was ignored because the end time must be greater than the start time.";
            return null;
        }

        return new SelectionRangeDto
        {
            StartSeconds = Math.Round(start, 4),
            EndSeconds = Math.Round(end, 4)
        };
    }

    private static string DescribeSelectionScope(SelectionRangeDto? selection) =>
        selection is null
            ? "full-file"
            : $"selected region {selection.StartSeconds?.ToString("0.00", CultureInfo.InvariantCulture)}s to {selection.EndSeconds?.ToString("0.00", CultureInfo.InvariantCulture)}s";
}
