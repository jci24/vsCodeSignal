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
            return Task.FromResult(new WorkspaceContextDto
            {
                ActiveView = NormalizeView(request.ActiveView),
                AvailableFiles = files,
                Selection = request.Selection,
                SelectionScope = request.Selection is null
                    ? "full-file"
                    : "selection requested but not yet applied in AI analysis",
                SupportedCommands = AiAssistantCommandCatalog.All.ToList(),
                Transforms = request.Transforms,
                Warnings = request.Selection is null
                    ? []
                    : ["Selected-range grounding is designed into the contract but not yet active in the MVP."],
                WorkspaceId = snapshot.WorkspaceId
            });
        }

        var selectedFile = importedAudioFileResolver.Resolve(request.FileId);
        var warnings = new List<string>();
        var compareFiles = new List<WorkspaceImportedFile>();

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

        if (request.Selection is not null)
            warnings.Add("Selected-range grounding is designed into the contract but not yet active in the MVP.");

        return Task.FromResult(new WorkspaceContextDto
        {
            ActiveView = NormalizeView(request.ActiveView),
            AvailableFiles = files,
            CompareFileIds = compareFiles.Select(file => file.Id).ToList(),
            CompareFiles = compareFiles.Select(ToFileReference).ToList(),
            IsSelectionApplied = false,
            SelectedFile = ToFileReference(selectedFile),
            SelectedFileId = selectedFile.Id,
            Selection = request.Selection,
            SelectionScope = request.Selection is null
                ? "full-file"
                : "selection requested but not yet applied in AI analysis",
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
}
