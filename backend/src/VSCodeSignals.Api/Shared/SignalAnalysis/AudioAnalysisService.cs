using System.Diagnostics;
using System.Numerics;
using VSCodeSignals.Api.Features.Workspaces.Response;

namespace VSCodeSignals.Api.Shared.SignalAnalysis;

public sealed class AudioAnalysisService(ILogger<AudioAnalysisService> logger)
{
    private const int TargetSampleRate = 22050;
    private const int WaveformPointCount = 1200;
    private const int FftSize = 4096;
    private const int SpectrogramWindowSize = 512;
    private const int SpectrogramHopSize = 256;
    private const int MaxSpectrogramFrames = 120;
    private const int MaxSpectrogramBins = 96;
    private const double MinimumFilterCutoffHz = 20d;
    private const float TrimSilenceThresholdDb = -42f;
    private static readonly TimeSpan DecodeTimeout = TimeSpan.FromSeconds(20);

    public async Task<DecodedAudioSignal> DecodeMonoAsync(string filePath, CancellationToken ct)
    {
        var ffmpegPath = ResolveFfmpegExecutable();
        using var stdout = new MemoryStream();
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(filePath);
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("f32le");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add(TargetSampleRate.ToString());
        startInfo.ArgumentList.Add("pipe:1");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(stdout, ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DecodeTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            await copyTask;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TryKill(process);
            throw new InvalidOperationException(
                $"Audio analysis timed out after {DecodeTimeout.TotalSeconds:0} seconds.");
        }

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var reason = string.IsNullOrWhiteSpace(stderr) ? "Unknown ffmpeg failure." : stderr.Trim();
            logger.LogWarning("Audio analysis decode failed for {Path}: {Reason}", filePath, reason);
            throw new InvalidOperationException($"Audio analysis failed. {reason}");
        }

        var bytes = stdout.ToArray();

        if (bytes.Length == 0)
            throw new InvalidOperationException("Audio analysis could not decode any PCM samples.");

        var sampleCount = bytes.Length / sizeof(float);
        var samples = new float[sampleCount];

        for (var index = 0; index < sampleCount; index++)
            samples[index] = BitConverter.ToSingle(bytes, index * sizeof(float));

        return new DecodedAudioSignal(samples, TargetSampleRate);
    }

    public DecodedAudioSignal ApplyTransforms(
        DecodedAudioSignal signal,
        SignalTransformRecipe? transforms)
    {
        if (transforms is null || signal.Samples.Length == 0)
            return signal;

        var samples = signal.Samples;

        if (transforms.TrimSilence)
            samples = TrimSilence(samples);

        if (HasActiveFilter(transforms.Filter))
            samples = ApplyFilter(samples, signal.SampleRate, transforms.Filter);

        if (transforms.Normalize)
            samples = Normalize(samples);

        if (Math.Abs(transforms.GainDb) > 0.01)
            samples = ApplyGain(samples, transforms.GainDb);

        return ReferenceEquals(samples, signal.Samples)
            ? signal
            : new DecodedAudioSignal(samples, signal.SampleRate);
    }

    public IReadOnlyList<WaveformFrame> BuildWaveform(DecodedAudioSignal signal)
    {
        if (signal.Samples.Length == 0)
            return [];

        var bucketSize = Math.Max(1, signal.Samples.Length / WaveformPointCount);
        var frames = new List<WaveformFrame>((signal.Samples.Length / bucketSize) + 1);

        for (var start = 0; start < signal.Samples.Length; start += bucketSize)
        {
            var end = Math.Min(start + bucketSize, signal.Samples.Length);
            double sum = 0;

            for (var index = start; index < end; index++)
                sum += signal.Samples[index];

            var average = sum / (end - start);
            var timeSeconds = (start + ((end - start) / 2.0)) / signal.SampleRate;
            frames.Add(new WaveformFrame(timeSeconds, average));
        }

        return frames;
    }

    public IReadOnlyList<SpectrumBin> BuildSpectrum(DecodedAudioSignal signal)
    {
        if (signal.Samples.Length == 0)
            return [];

        var window = TakeCenteredWindow(signal.Samples, FftSize);
        var spectrum = ComputeMagnitudeSpectrum(window, signal.SampleRate);
        return spectrum
            .Take(Math.Min(1024, spectrum.Count))
            .ToList();
    }

    public SpectrogramAnalysis BuildSpectrogram(DecodedAudioSignal signal)
    {
        if (signal.Samples.Length < SpectrogramWindowSize)
            return new SpectrogramAnalysis([], [], []);

        var totalFrames = 1 + ((signal.Samples.Length - SpectrogramWindowSize) / SpectrogramHopSize);
        var frameStep = Math.Max(1, totalFrames / MaxSpectrogramFrames);
        var rawCells = new List<(int TimeIndex, int FrequencyIndex, double Value)>();
        var timeValues = new List<double>();
        var frequencyValues = BuildFrequencyAxis(signal.SampleRate, SpectrogramWindowSize, MaxSpectrogramBins);
        var minValue = double.MaxValue;
        var maxValue = double.MinValue;
        var outputFrameIndex = 0;

        for (var frame = 0; frame < totalFrames; frame += frameStep)
        {
            var start = frame * SpectrogramHopSize;
            var window = new float[SpectrogramWindowSize];
            Array.Copy(signal.Samples, start, window, 0, SpectrogramWindowSize);

            var spectrum = ComputeMagnitudeSpectrum(window, signal.SampleRate);
            var binStep = Math.Max(1, spectrum.Count / MaxSpectrogramBins);
            timeValues.Add(start / (double)signal.SampleRate);

            for (var bin = 0; bin < MaxSpectrogramBins; bin++)
            {
                var spectrumIndex = Math.Min(bin * binStep, spectrum.Count - 1);
                var value = spectrum[spectrumIndex].Magnitude;
                minValue = Math.Min(minValue, value);
                maxValue = Math.Max(maxValue, value);
                rawCells.Add((outputFrameIndex, bin, value));
            }

            outputFrameIndex++;
        }

        var range = Math.Max(maxValue - minValue, 1e-9);
        var cells = rawCells
            .Select(cell => new SpectrogramCell(
                cell.TimeIndex,
                cell.FrequencyIndex,
                (cell.Value - minValue) / range))
            .ToList();

        return new SpectrogramAnalysis(timeValues, frequencyValues, cells);
    }

    public SignalMetrics BuildMetrics(DecodedAudioSignal signal)
    {
        if (signal.Samples.Length == 0)
        {
            return new SignalMetrics(0, 0, 0, 0, 0, signal.SampleRate, 0);
        }

        double squaredSum = 0;
        var peak = 0d;

        for (var index = 0; index < signal.Samples.Length; index++)
        {
            var sample = signal.Samples[index];
            var absolute = Math.Abs(sample);
            peak = Math.Max(peak, absolute);
            squaredSum += sample * sample;
        }

        var rms = Math.Sqrt(squaredSum / signal.Samples.Length);
        var crestFactor = rms > 1e-9 ? peak / rms : 0;
        var durationSeconds = signal.Samples.Length / (double)signal.SampleRate;
        var dominantBin = BuildSpectrum(signal)
            .Where(bin => bin.FrequencyHz > 0)
            .OrderByDescending(bin => bin.Magnitude)
            .FirstOrDefault();

        return new SignalMetrics(
            rms,
            peak,
            crestFactor,
            dominantBin?.FrequencyHz ?? 0,
            dominantBin?.Magnitude ?? 0,
            signal.SampleRate,
            durationSeconds);
    }

    private static List<double> BuildFrequencyAxis(int sampleRate, int fftSize, int binCount)
    {
        var axis = new List<double>(binCount);
        var maxFrequency = sampleRate / 2.0;

        for (var bin = 0; bin < binCount; bin++)
            axis.Add((maxFrequency / Math.Max(1, binCount - 1)) * bin);

        return axis;
    }

    private static float[] TakeCenteredWindow(float[] samples, int size)
    {
        var window = new float[size];

        if (samples.Length <= size)
        {
            Array.Copy(samples, window, samples.Length);
            return window;
        }

        var start = Math.Max(0, (samples.Length - size) / 2);
        Array.Copy(samples, start, window, 0, size);
        return window;
    }

    private static float[] ApplyGain(float[] samples, double gainDb)
    {
        var multiplier = Math.Pow(10, gainDb / 20d);
        var transformed = new float[samples.Length];

        for (var index = 0; index < samples.Length; index++)
            transformed[index] = ClampSample(samples[index] * multiplier);

        return transformed;
    }

    private static float ClampSample(double sample)
    {
        if (sample > 1d)
            return 1f;

        if (sample < -1d)
            return -1f;

        return (float)sample;
    }

    private static float[] Normalize(float[] samples)
    {
        var peak = 0f;

        for (var index = 0; index < samples.Length; index++)
            peak = Math.Max(peak, Math.Abs(samples[index]));

        if (peak < 1e-6f)
            return samples;

        var scale = 1f / peak;
        var transformed = new float[samples.Length];

        for (var index = 0; index < samples.Length; index++)
            transformed[index] = ClampSample(samples[index] * scale);

        return transformed;
    }

    private static float[] TrimSilence(float[] samples)
    {
        var threshold = (float)Math.Pow(10, TrimSilenceThresholdDb / 20d);
        var start = 0;

        while (start < samples.Length && Math.Abs(samples[start]) < threshold)
            start++;

        var end = samples.Length - 1;

        while (end > start && Math.Abs(samples[end]) < threshold)
            end--;

        var trimmedLength = end - start + 1;

        if (trimmedLength <= 0 || trimmedLength == samples.Length)
            return samples;

        var trimmed = new float[trimmedLength];
        Array.Copy(samples, start, trimmed, 0, trimmedLength);
        return trimmed;
    }

    private static bool HasActiveFilter(SignalFilterRecipe? filter)
    {
        if (filter is null)
            return false;

        return NormalizeFilterMode(filter.Mode) is not "none";
    }

    private static float[] ApplyFilter(float[] samples, int sampleRate, SignalFilterRecipe filter)
    {
        if (samples.Length == 0)
            return samples;

        var mode = NormalizeFilterMode(filter.Mode);
        BiquadCoefficients coefficients;

        switch (mode)
        {
            case "lowpass":
                coefficients = CreateLowPassCoefficients(sampleRate, filter.CutoffHz, filter.Q);
                break;
            case "highpass":
                coefficients = CreateHighPassCoefficients(sampleRate, filter.CutoffHz, filter.Q);
                break;
            case "bandpass":
                coefficients = CreateBandPassCoefficients(
                    sampleRate,
                    filter.LowCutoffHz,
                    filter.HighCutoffHz);
                break;
            case "notch":
                coefficients = CreateNotchCoefficients(sampleRate, filter.CutoffHz, filter.Q);
                break;
            default:
                return samples;
        }

        return ApplyBiquad(samples, coefficients);
    }

    private static string NormalizeFilterMode(string? mode) =>
        string.IsNullOrWhiteSpace(mode)
            ? "none"
            : mode.Trim().ToLowerInvariant();

    private static BiquadCoefficients CreateLowPassCoefficients(
        int sampleRate,
        double cutoffHz,
        double q)
    {
        var frequency = ClampCutoff(cutoffHz, sampleRate);
        var quality = ClampQ(q);
        var omega = 2d * Math.PI * frequency / sampleRate;
        var cosOmega = Math.Cos(omega);
        var alpha = Math.Sin(omega) / (2d * quality);
        var b0 = (1d - cosOmega) / 2d;
        var b1 = 1d - cosOmega;
        var b2 = (1d - cosOmega) / 2d;
        var a0 = 1d + alpha;
        var a1 = -2d * cosOmega;
        var a2 = 1d - alpha;

        return NormalizeCoefficients(b0, b1, b2, a0, a1, a2);
    }

    private static BiquadCoefficients CreateHighPassCoefficients(
        int sampleRate,
        double cutoffHz,
        double q)
    {
        var frequency = ClampCutoff(cutoffHz, sampleRate);
        var quality = ClampQ(q);
        var omega = 2d * Math.PI * frequency / sampleRate;
        var cosOmega = Math.Cos(omega);
        var alpha = Math.Sin(omega) / (2d * quality);
        var b0 = (1d + cosOmega) / 2d;
        var b1 = -(1d + cosOmega);
        var b2 = (1d + cosOmega) / 2d;
        var a0 = 1d + alpha;
        var a1 = -2d * cosOmega;
        var a2 = 1d - alpha;

        return NormalizeCoefficients(b0, b1, b2, a0, a1, a2);
    }

    private static BiquadCoefficients CreateBandPassCoefficients(
        int sampleRate,
        double lowCutoffHz,
        double highCutoffHz)
    {
        var (low, high) = ClampBand(lowCutoffHz, highCutoffHz, sampleRate);
        var centerFrequency = Math.Sqrt(low * high);
        var quality = ClampQ(centerFrequency / Math.Max(high - low, 1d));
        var omega = 2d * Math.PI * centerFrequency / sampleRate;
        var cosOmega = Math.Cos(omega);
        var alpha = Math.Sin(omega) / (2d * quality);
        var b0 = alpha;
        var b1 = 0d;
        var b2 = -alpha;
        var a0 = 1d + alpha;
        var a1 = -2d * cosOmega;
        var a2 = 1d - alpha;

        return NormalizeCoefficients(b0, b1, b2, a0, a1, a2);
    }

    private static BiquadCoefficients CreateNotchCoefficients(
        int sampleRate,
        double cutoffHz,
        double q)
    {
        var frequency = ClampCutoff(cutoffHz, sampleRate);
        var quality = ClampQ(q);
        var omega = 2d * Math.PI * frequency / sampleRate;
        var cosOmega = Math.Cos(omega);
        var alpha = Math.Sin(omega) / (2d * quality);
        var b0 = 1d;
        var b1 = -2d * cosOmega;
        var b2 = 1d;
        var a0 = 1d + alpha;
        var a1 = -2d * cosOmega;
        var a2 = 1d - alpha;

        return NormalizeCoefficients(b0, b1, b2, a0, a1, a2);
    }

    private static double ClampCutoff(double cutoffHz, int sampleRate)
    {
        var maximum = Math.Max(MinimumFilterCutoffHz * 2d, (sampleRate / 2d) * 0.95d);
        return Math.Clamp(cutoffHz, MinimumFilterCutoffHz, maximum);
    }

    private static (double Low, double High) ClampBand(
        double lowCutoffHz,
        double highCutoffHz,
        int sampleRate)
    {
        var low = ClampCutoff(lowCutoffHz, sampleRate);
        var high = ClampCutoff(highCutoffHz, sampleRate);

        if (high <= low)
            high = Math.Min((sampleRate / 2d) * 0.95d, low + 50d);

        if (high <= low)
            low = Math.Max(MinimumFilterCutoffHz, high - 50d);

        return (low, high);
    }

    private static double ClampQ(double q) => Math.Clamp(q, 0.35d, 18d);

    private static BiquadCoefficients NormalizeCoefficients(
        double b0,
        double b1,
        double b2,
        double a0,
        double a1,
        double a2) =>
        new(
            b0 / a0,
            b1 / a0,
            b2 / a0,
            a1 / a0,
            a2 / a0);

    private static float[] ApplyBiquad(float[] samples, BiquadCoefficients coefficients)
    {
        var transformed = new float[samples.Length];
        double x1 = 0d;
        double x2 = 0d;
        double y1 = 0d;
        double y2 = 0d;

        for (var index = 0; index < samples.Length; index++)
        {
            var input = samples[index];
            var output =
                (coefficients.B0 * input) +
                (coefficients.B1 * x1) +
                (coefficients.B2 * x2) -
                (coefficients.A1 * y1) -
                (coefficients.A2 * y2);

            transformed[index] = (float)output;
            x2 = x1;
            x1 = input;
            y2 = y1;
            y1 = output;
        }

        return transformed;
    }

    private static List<SpectrumBin> ComputeMagnitudeSpectrum(float[] samples, int sampleRate)
    {
        var size = NextPowerOfTwo(samples.Length);
        var buffer = new Complex[size];

        for (var index = 0; index < size; index++)
        {
            var sample = index < samples.Length ? samples[index] : 0f;
            var window = size > 1
                ? 0.5 - (0.5 * Math.Cos((2 * Math.PI * index) / (size - 1)))
                : 1.0;
            buffer[index] = new Complex(sample * window, 0);
        }

        PerformFft(buffer);

        var halfSize = size / 2;
        var result = new List<SpectrumBin>(halfSize);

        for (var index = 0; index < halfSize; index++)
        {
            var magnitude = buffer[index].Magnitude / halfSize;
            var decibel = 20 * Math.Log10(magnitude + 1e-9);
            var frequency = index * sampleRate / (double)size;
            result.Add(new SpectrumBin(frequency, decibel));
        }

        return result;
    }

    private static int NextPowerOfTwo(int value)
    {
        var size = 1;

        while (size < value)
            size <<= 1;

        return size;
    }

    private static void PerformFft(Complex[] buffer)
    {
        var count = buffer.Length;
        var j = 0;

        for (var i = 1; i < count; i++)
        {
            var bit = count >> 1;

            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }

            j ^= bit;

            if (i < j)
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }

        for (var length = 2; length <= count; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wLength = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (var offset = 0; offset < count; offset += length)
            {
                var w = Complex.One;

                for (var index = 0; index < length / 2; index++)
                {
                    var even = buffer[offset + index];
                    var odd = w * buffer[offset + index + (length / 2)];
                    buffer[offset + index] = even + odd;
                    buffer[offset + index + (length / 2)] = even - odd;
                    w *= wLength;
                }
            }
        }
    }

    private static string ResolveFfmpegExecutable()
    {
        var resolvedPath = TryResolveExecutable("ffmpeg");

        if (resolvedPath is not null)
            return resolvedPath;

        throw new InvalidOperationException(
            "Signal analysis requires ffmpeg. Install ffmpeg and ensure it is available on PATH.");
    }

    private static string? TryResolveExecutable(string executableName)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        var executableNames = OperatingSystem.IsWindows()
            ? new[] { $"{executableName}.exe", executableName }
            : [executableName];

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

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort cleanup when ffmpeg stops responding.
        }
    }
}

public sealed record DecodedAudioSignal(float[] Samples, int SampleRate);

public sealed record WaveformFrame(double TimeSeconds, double Amplitude);

public sealed record SpectrumBin(double FrequencyHz, double Magnitude);

public sealed record SpectrogramAnalysis(
    IReadOnlyList<double> Times,
    IReadOnlyList<double> Frequencies,
    IReadOnlyList<SpectrogramCell> Cells);

public sealed record SpectrogramCell(int TimeIndex, int FrequencyIndex, double Intensity);

internal sealed record BiquadCoefficients(double B0, double B1, double B2, double A1, double A2);

public sealed record SignalMetrics(
    double Rms,
    double Peak,
    double CrestFactor,
    double DominantFrequencyHz,
    double DominantMagnitudeDb,
    int SampleRate,
    double DurationSeconds);
