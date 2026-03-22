using VSCodeSignals.Api.Features.Workspaces.Handler;
using VSCodeSignals.Api.Features.Workspaces.Response;

namespace VSCodeSignals.Api.Shared.SignalAnalysis;

public sealed class ImportedAudioFileResolver(WorkspaceImportStore workspaceImportStore)
{
    public WorkspaceImportedFile Resolve(string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new InvalidOperationException("A fileId must be provided.");

        var file = workspaceImportStore.GetImportedFile(fileId);

        if (file is null)
            throw new InvalidOperationException("The selected imported file could not be found.");

        if (!string.Equals(file.SignalKind, "audio", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Waveform, FFT, and spectrogram analysis are currently available for audio files only.");

        if (string.IsNullOrWhiteSpace(file.ResolvedPath) || !File.Exists(file.ResolvedPath))
            throw new InvalidOperationException("The selected imported file is no longer available on disk.");

        return file;
    }
}
