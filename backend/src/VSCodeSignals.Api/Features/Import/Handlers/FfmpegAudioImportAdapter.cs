using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Handlers;

public sealed class FfmpegAudioImportAdapter(ILogger<FfmpegAudioImportAdapter> logger)
    : IImportAdapter
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

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
            var ffprobePath = ResolveFfprobeExecutable();
            var probeResult = await ProbeAsync(ffprobePath, path, ct);
            var fileInfo = new FileInfo(path);

            return new ImportedSignalFile
            {
                Adapter = Name,
                ChannelCount = probeResult.ChannelCount,
                DurationSeconds = probeResult.DurationSeconds,
                Format = Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
                Metadata = probeResult.Metadata,
                SampleRateHz = probeResult.SampleRateHz,
                SignalKind = "audio",
                SizeBytes = fileInfo.Length,
                SourcePath = path
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Audio import failed for {Path}.", path);
            throw new InvalidOperationException(
                "Audio import failed. Ensure ffprobe/ffmpeg is installed and reachable on PATH.",
                ex);
        }
    }

    private static string ResolveFfprobeExecutable()
    {
        var resolvedPath = TryResolveExecutable("ffprobe");

        if (resolvedPath is not null)
            return resolvedPath;

        throw new InvalidOperationException(
            "Audio import requires ffprobe/ffmpeg. Install ffmpeg and ensure ffprobe is available on PATH.");
    }

    private static string? TryResolveExecutable(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        var executableNames = OperatingSystem.IsWindows()
            ? new[] { $"{executableName}.exe", executableName }
            : new[] { executableName };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidateName in executableNames)
            {
                var candidatePath = Path.Combine(directory, candidateName);

                if (File.Exists(candidatePath))
                    return candidatePath;
            }
        }

        return null;
    }

    private static async Task<AudioProbeResult> ProbeAsync(
        string ffprobePath,
        string audioPath,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffprobePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(audioPath);

        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(ProbeTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new InvalidOperationException(
                $"Audio import timed out after {ProbeTimeout.TotalSeconds:0} seconds while probing the file.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var reason = string.IsNullOrWhiteSpace(stderr) ? "Unknown ffprobe failure." : stderr.Trim();
            throw new InvalidOperationException($"Audio import failed. {reason}");
        }

        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;
        var audioStream = FindPrimaryAudioStream(root);

        if (audioStream is null)
            throw new InvalidOperationException("FFProbe did not find an audio stream in the provided file.");

        return BuildProbeResult(root, audioStream.Value);
    }

    private static JsonElement? FindPrimaryAudioStream(JsonElement root)
    {
        if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var stream in streams.EnumerateArray())
        {
            if (stream.TryGetProperty("codec_type", out var codecType)
                && string.Equals(codecType.GetString(), "audio", StringComparison.OrdinalIgnoreCase))
                return stream;
        }

        return null;
    }

    private static AudioProbeResult BuildProbeResult(JsonElement root, JsonElement audioStream)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["codec"] = ReadString(audioStream, "codec_name") ?? "unknown",
            ["codecLongName"] = ReadString(audioStream, "codec_long_name") ?? "unknown",
            ["container"] = ReadFormatName(root) ?? "unknown"
        };

        return new AudioProbeResult(
            DurationSeconds: ReadDouble(audioStream, "duration") ?? ReadFormatDuration(root),
            SampleRateHz: ReadInt(audioStream, "sample_rate"),
            ChannelCount: ReadInt(audioStream, "channels"),
            Metadata: metadata);
    }

    private static string? ReadFormatName(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var format))
            return null;

        return ReadString(format, "format_name");
    }

    private static double? ReadFormatDuration(JsonElement root)
    {
        if (!root.TryGetProperty("format", out var format))
            return null;

        return ReadDouble(format, "duration");
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var numberValue))
            return numberValue;

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out numberValue))
            return numberValue;

        return null;
    }

    private static double? ReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numberValue))
            return numberValue;

        if (property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out numberValue))
            return numberValue;

        return null;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup when ffprobe stops responding.
        }
    }

    private sealed record AudioProbeResult(
        double? DurationSeconds,
        int? SampleRateHz,
        int? ChannelCount,
        Dictionary<string, string> Metadata);
}
