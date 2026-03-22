using FFMpegCore;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Handlers;

public sealed class FfmpegAudioImportAdapter(ILogger<FfmpegAudioImportAdapter> logger)
    : IImportAdapter
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aac",
        ".aif",
        ".aiff",
        ".flac",
        ".m4a",
        ".mp3",
        ".ogg",
        ".wav",
        ".wma"
    };

    public string Name => "ffmpeg-audio";

    public bool CanImport(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task<ImportedSignalFile> ImportAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var analysis = await FFProbe.AnalyseAsync(path);
            ct.ThrowIfCancellationRequested();

            var audioStream = analysis.PrimaryAudioStream ?? analysis.AudioStreams.FirstOrDefault();

            if (audioStream is null)
                throw new InvalidOperationException("FFProbe did not find an audio stream in the provided file.");

            var fileInfo = new FileInfo(path);
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["codec"] = audioStream.CodecName ?? "unknown",
                ["codecLongName"] = audioStream.CodecLongName ?? "unknown",
                ["container"] = analysis.Format.FormatName ?? "unknown"
            };

            return new ImportedSignalFile(
                SourcePath: path,
                Adapter: Name,
                Format: Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
                SignalKind: "audio",
                SizeBytes: fileInfo.Length,
                DurationSeconds: analysis.Duration.TotalSeconds > 0
                    ? analysis.Duration.TotalSeconds
                    : null,
                SampleRateHz: audioStream.SampleRateHz > 0 ? audioStream.SampleRateHz : null,
                ChannelCount: audioStream.Channels > 0 ? audioStream.Channels : null,
                Metadata: metadata);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audio import failed for {Path}.", path);
            throw new InvalidOperationException(
                "Audio import failed. Ensure ffprobe/ffmpeg is installed and reachable on PATH.",
                ex);
        }
    }
}
