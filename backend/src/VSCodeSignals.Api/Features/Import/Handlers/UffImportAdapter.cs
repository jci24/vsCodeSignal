using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Handlers;

public sealed class UffImportAdapter(
    IHostEnvironment environment,
    ILogger<UffImportAdapter> logger) : IImportAdapter
{
    private static readonly TimeSpan SidecarTimeout = TimeSpan.FromSeconds(20);
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".uff",
        ".unv"
    };

    public string Name => "uff";

    public bool CanImport(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    public async Task<ImportedSignalFile> ImportAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var fileInfo = new FileInfo(path);
        var summary = await RunSidecarAsync(path, ct);

        return new ImportedSignalFile
        {
            Adapter = Name,
            ChannelCount = summary.ChannelCount,
            DurationSeconds = summary.DurationSeconds,
            Format = summary.Format ?? "uff",
            Metadata = summary.Metadata,
            ResolvedPath = path,
            SampleRateHz = summary.SampleRateHz,
            SignalKind = summary.SignalKind ?? "engineering-signal",
            SizeBytes = fileInfo.Length,
            SourcePath = path
        };
    }

    private async Task<UffImportSummary> RunSidecarAsync(string path, CancellationToken ct)
    {
        var scriptPath = ResolveScriptPath();
        var pythonExecutable = ResolvePythonExecutable();

        if (!File.Exists(scriptPath))
            throw new InvalidOperationException($"UFF sidecar script was not found at '{scriptPath}'.");

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExecutable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = environment.ContentRootPath
        };

        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--file");
        startInfo.ArgumentList.Add(path);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to start Python sidecar for UFF import.");
            throw new InvalidOperationException(
                "UFF import requires Python and pyuff. Configure SIGNAL_STUDIO_PYTHON or install backend/.venv dependencies.",
                ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(SidecarTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new InvalidOperationException(
                $"UFF import timed out after {SidecarTimeout.TotalSeconds:0} seconds while parsing the file.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var reason = string.IsNullOrWhiteSpace(stderr) ? "Unknown pyuff sidecar failure." : stderr.Trim();
            throw new InvalidOperationException($"UFF import failed. {reason}");
        }

        var summary = JsonSerializer.Deserialize<UffImportSummary>(stdout, SerializerOptions);

        if (summary is null)
            throw new InvalidOperationException("UFF import failed. The pyuff sidecar returned an empty response.");

        return summary;
    }

    private string ResolveScriptPath() =>
        Path.Combine(environment.ContentRootPath, "Features", "Import", "Common", "pyuff_import.py");

    private string ResolvePythonExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("SIGNAL_STUDIO_PYTHON");

        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var backendRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        var localVenvPython = OperatingSystem.IsWindows()
            ? Path.Combine(backendRoot, ".venv", "Scripts", "python.exe")
            : Path.Combine(backendRoot, ".venv", "bin", "python");

        if (File.Exists(localVenvPython))
            return localVenvPython;

        return OperatingSystem.IsWindows() ? "python" : "python3";
    }

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup when the sidecar stops responding.
        }
    }

    private sealed record UffImportSummary(
        string? Format,
        string? SignalKind,
        double? DurationSeconds,
        int? SampleRateHz,
        int? ChannelCount,
        Dictionary<string, string> Metadata);
}
