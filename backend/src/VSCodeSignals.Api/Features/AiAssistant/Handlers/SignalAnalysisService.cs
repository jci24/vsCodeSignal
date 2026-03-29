using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class SignalAnalysisService(
    ImportedAudioFileResolver importedAudioFileResolver,
    AudioAnalysisService audioAnalysisService,
    IMemoryCache memoryCache) : ISignalAnalysisService
{
    private const double LowBandUpperHz = 250d;
    private const double MidBandUpperHz = 2000d;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);
    private static readonly JsonSerializerOptions CacheSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<SignalSummaryDto> GetSignalSummaryAsync(WorkspaceContextDto context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.SelectedFileId))
            throw new InvalidOperationException("A selected file is required for AI analysis.");

        var cacheKey = BuildSignalSummaryCacheKey(context);

        if (memoryCache.TryGetValue<SignalSummaryDto>(cacheKey, out var cachedSummary) && cachedSummary is not null)
            return cachedSummary;

        var file = importedAudioFileResolver.Resolve(context.SelectedFileId);
        var signal = await audioAnalysisService.DecodeMonoAsync(file.ResolvedPath, ct);
        signal = audioAnalysisService.ApplyTransforms(signal, context.Transforms);
        signal = ApplySelection(signal, context.Selection);
        var metrics = audioAnalysisService.BuildMetrics(signal);
        var spectrum = audioAnalysisService.BuildSpectrum(signal);
        var spectrogram = audioAnalysisService.BuildSpectrogram(signal);
        var spectralSummary = SummarizeSpectrum(spectrum);
        var spectrogramSummary = SummarizeSpectrogram(spectrogram);

        var summary = new SignalSummaryDto
        {
            CrestFactor = metrics.CrestFactor,
            CrestFactorDb = metrics.CrestFactorDb,
            DominantEnergyBand = spectralSummary.DominantBand,
            DominantFrequencyHz = metrics.DominantFrequencyHz,
            DominantMagnitudeDb = metrics.DominantMagnitudeDb,
            DurationSeconds = metrics.DurationSeconds,
            FileId = file.Id,
            Facts = BuildSignalFacts(metrics, spectralSummary, spectrogramSummary, file.SourcePath),
            HighBandEnergyRatio = spectralSummary.HighBandEnergyRatio,
            LowBandEnergyRatio = spectralSummary.LowBandEnergyRatio,
            MidBandEnergyRatio = spectralSummary.MidBandEnergyRatio,
            NearFullScaleCount = metrics.SamplesNearFullScaleCount,
            Peak = metrics.Peak,
            Rms = metrics.Rms,
            SampleRateHz = metrics.SampleRate,
            SamplesOverFullScaleCount = metrics.SamplesOverFullScaleCount,
            SpectralCentroidHz = spectralSummary.SpectralCentroidHz,
            SpectrogramDominantBand = spectrogramSummary.DominantBand,
            SpectrogramTemporalVariation = spectrogramSummary.TemporalVariation,
            SourcePath = file.SourcePath
        };

        memoryCache.Set(cacheKey, summary, CacheDuration);
        return summary;
    }

    public async Task<ComparisonSummaryDto?> GetComparisonSummaryAsync(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        CancellationToken ct)
    {
        var cacheKey = BuildComparisonSummaryCacheKey(context);

        if (memoryCache.TryGetValue<ComparisonSummaryCacheEntry>(cacheKey, out var cachedEntry) && cachedEntry is not null)
            return cachedEntry.Value;

        if (context.CompareFileIds.Count == 0)
        {
            if (!HasActiveTransforms(context.Transforms))
            {
                memoryCache.Set(cacheKey, new ComparisonSummaryCacheEntry(null), CacheDuration);
                return null;
            }

            var selectedFile = importedAudioFileResolver.Resolve(context.SelectedFileId);
            var originalSignal = await audioAnalysisService.DecodeMonoAsync(selectedFile.ResolvedPath, ct);
            originalSignal = ApplySelection(originalSignal, context.Selection);
            var originalMetrics = audioAnalysisService.BuildMetrics(originalSignal);
            var originalSpectralSummary = SummarizeSpectrum(audioAnalysisService.BuildSpectrum(originalSignal));

            var summary = new ComparisonSummaryDto
            {
                Comparisons =
                [
                    new ComparisonDeltaDto
                    {
                        ComparisonKind = "transform_baseline",
                        DominantFrequencyDeltaHz = signalSummary.DominantFrequencyHz - originalMetrics.DominantFrequencyHz,
                        DurationDeltaSeconds = signalSummary.DurationSeconds - originalMetrics.DurationSeconds,
                        Facts =
                        [
                            CreateFact(
                                code: "TRANSFORM_BASELINE_RMS",
                                label: "RMS change after current transforms",
                                valueText: $"{FormatSigned(ToDbfs(signalSummary.Rms) - ToDbfs(originalMetrics.Rms))} dB",
                                basis: "derived",
                                confidence: "high",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_DOM_FREQ",
                                label: "Dominant frequency change after current transforms",
                                valueText: $"{FormatSigned(signalSummary.DominantFrequencyHz - originalMetrics.DominantFrequencyHz)} Hz",
                                basis: "derived",
                                confidence: "medium",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_PEAK",
                                label: "Peak change after current transforms",
                                valueText: $"{FormatSigned(ToDbfs(signalSummary.Peak) - ToDbfs(originalMetrics.Peak))} dB",
                                basis: "derived",
                                confidence: "high",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_SPECTRAL_CENTROID",
                                label: "Spectral centroid change after current transforms",
                                valueText: $"{FormatSigned(signalSummary.SpectralCentroidHz - originalSpectralSummary.SpectralCentroidHz)} Hz",
                                basis: "derived",
                                confidence: "medium",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_LOW_BAND",
                                label: "Low-band energy share change after current transforms",
                                valueText: FormatPercentagePointDelta(signalSummary.LowBandEnergyRatio - originalSpectralSummary.LowBandEnergyRatio),
                                basis: "derived",
                                confidence: "medium",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_HIGH_BAND",
                                label: "High-band energy share change after current transforms",
                                valueText: FormatPercentagePointDelta(signalSummary.HighBandEnergyRatio - originalSpectralSummary.HighBandEnergyRatio),
                                basis: "derived",
                                confidence: "medium",
                                source: selectedFile.SourcePath),
                            CreateFact(
                                code: "TRANSFORM_BASELINE_DURATION",
                                label: "Duration change after current transforms",
                                valueText: $"{FormatSigned(signalSummary.DurationSeconds - originalMetrics.DurationSeconds)} s",
                                basis: "measured",
                                confidence: "high",
                                source: selectedFile.SourcePath)
                        ],
                        FileId = selectedFile.Id,
                        HighBandEnergyDelta = signalSummary.HighBandEnergyRatio - originalSpectralSummary.HighBandEnergyRatio,
                        PeakDeltaDbFs = ToDbfs(signalSummary.Peak) - ToDbfs(originalMetrics.Peak),
                        LowBandEnergyDelta = signalSummary.LowBandEnergyRatio - originalSpectralSummary.LowBandEnergyRatio,
                        MidBandEnergyDelta = signalSummary.MidBandEnergyRatio - originalSpectralSummary.MidBandEnergyRatio,
                        RmsDeltaDb = ToDbfs(signalSummary.Rms) - ToDbfs(originalMetrics.Rms),
                        SpectralCentroidDeltaHz = signalSummary.SpectralCentroidHz - originalSpectralSummary.SpectralCentroidHz,
                        SourcePath = selectedFile.SourcePath
                    }
                ],
                CompareFileIds = [],
                PrimaryFileId = signalSummary.FileId
            };

            memoryCache.Set(cacheKey, new ComparisonSummaryCacheEntry(summary), CacheDuration);
            return summary;
        }

        var comparisons = new List<ComparisonDeltaDto>();

        foreach (var compareFileId in context.CompareFileIds)
        {
            var compareFile = importedAudioFileResolver.Resolve(compareFileId);
            var signal = await audioAnalysisService.DecodeMonoAsync(compareFile.ResolvedPath, ct);
            signal = ApplySelection(signal, context.Selection);
            var metrics = audioAnalysisService.BuildMetrics(signal);
            var spectralSummary = SummarizeSpectrum(audioAnalysisService.BuildSpectrum(signal));
            var rmsDeltaDb = ToDbfs(metrics.Rms) - ToDbfs(signalSummary.Rms);
            var peakDeltaDbFs = ToDbfs(metrics.Peak) - ToDbfs(signalSummary.Peak);
            var frequencyDeltaHz = metrics.DominantFrequencyHz - signalSummary.DominantFrequencyHz;
            var spectralCentroidDeltaHz = spectralSummary.SpectralCentroidHz - signalSummary.SpectralCentroidHz;
            var durationDeltaSeconds = metrics.DurationSeconds - signalSummary.DurationSeconds;
            var lowBandDelta = spectralSummary.LowBandEnergyRatio - signalSummary.LowBandEnergyRatio;
            var highBandDelta = spectralSummary.HighBandEnergyRatio - signalSummary.HighBandEnergyRatio;
            var midBandDelta = spectralSummary.MidBandEnergyRatio - signalSummary.MidBandEnergyRatio;

            comparisons.Add(new ComparisonDeltaDto
            {
                ComparisonKind = "signal",
                DominantFrequencyDeltaHz = frequencyDeltaHz,
                DurationDeltaSeconds = durationDeltaSeconds,
                Facts =
                [
                    CreateFact(
                        code: $"COMPARE_RMS_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} RMS delta",
                        valueText: $"{FormatSigned(rmsDeltaDb)} dB",
                        basis: "derived",
                        confidence: "high",
                        source: compareFile.SourcePath),
                    CreateFact(
                        code: $"COMPARE_DOM_FREQ_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} dominant frequency delta",
                        valueText: $"{FormatSigned(frequencyDeltaHz)} Hz",
                        basis: "derived",
                        confidence: "medium",
                        source: compareFile.SourcePath),
                    CreateFact(
                        code: $"COMPARE_SPECTRAL_CENTROID_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} spectral centroid delta",
                        valueText: $"{FormatSigned(spectralCentroidDeltaHz)} Hz",
                        basis: "derived",
                        confidence: "medium",
                        source: compareFile.SourcePath),
                    CreateFact(
                        code: $"COMPARE_LOW_BAND_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} low-band energy share delta",
                        valueText: FormatPercentagePointDelta(lowBandDelta),
                        basis: "derived",
                        confidence: "medium",
                        source: compareFile.SourcePath),
                    CreateFact(
                        code: $"COMPARE_HIGH_BAND_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} high-band energy share delta",
                        valueText: FormatPercentagePointDelta(highBandDelta),
                        basis: "derived",
                        confidence: "medium",
                        source: compareFile.SourcePath),
                    CreateFact(
                        code: $"COMPARE_DURATION_{compareFile.Id}",
                        label: $"{compareFile.SourcePath} duration delta",
                        valueText: $"{FormatSigned(durationDeltaSeconds)} s",
                        basis: "measured",
                        confidence: "high",
                        source: compareFile.SourcePath)
                ],
                FileId = compareFile.Id,
                HighBandEnergyDelta = highBandDelta,
                PeakDeltaDbFs = peakDeltaDbFs,
                LowBandEnergyDelta = lowBandDelta,
                MidBandEnergyDelta = midBandDelta,
                RmsDeltaDb = rmsDeltaDb,
                SpectralCentroidDeltaHz = spectralCentroidDeltaHz,
                SourcePath = compareFile.SourcePath
            });
        }

        var comparisonSummary = new ComparisonSummaryDto
        {
            Comparisons = comparisons,
            CompareFileIds = comparisons.Select(item => item.FileId).ToList(),
            PrimaryFileId = signalSummary.FileId
        };

        memoryCache.Set(cacheKey, new ComparisonSummaryCacheEntry(comparisonSummary), CacheDuration);
        return comparisonSummary;
    }

    private static string BuildSignalSummaryCacheKey(WorkspaceContextDto context) =>
        string.Join('|',
            "signal-summary",
            context.WorkspaceId,
            context.SelectedFileId,
            SerializeSelection(context.Selection),
            SerializeTransforms(context.Transforms));

    private static string BuildComparisonSummaryCacheKey(WorkspaceContextDto context) =>
        string.Join('|',
            "comparison-summary",
            context.WorkspaceId,
            context.SelectedFileId,
            string.Join(',', context.CompareFileIds.OrderBy(item => item, StringComparer.Ordinal)),
            SerializeSelection(context.Selection),
            SerializeTransforms(context.Transforms));

    private static string SerializeSelection(SelectionRangeDto? selection) =>
        selection is null
            ? "full-file"
            : JsonSerializer.Serialize(selection, CacheSerializerOptions);

    private static string SerializeTransforms(SignalTransformRecipe transforms) =>
        JsonSerializer.Serialize(transforms, CacheSerializerOptions);

    private static List<EvidenceItemDto> BuildSignalFacts(
        SignalMetrics metrics,
        SpectralSummary spectralSummary,
        SpectrogramSummary spectrogramSummary,
        string sourcePath)
    {
        var overFullScaleShare = ComputeOverFullScaleShare(metrics);

        return
    [
        CreateFact(
            code: "SAMPLE_PEAK_DBFS",
            label: "Decoded sample peak",
            valueText: FormatDbfs(metrics.Peak),
            basis: "measured",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "SAMPLES_OVER_FULL_SCALE",
            label: "Samples over 0 dBFS",
            valueText: metrics.SamplesOverFullScaleCount.ToString("N0", CultureInfo.InvariantCulture),
            basis: "measured",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "OVER_FULL_SCALE_SHARE",
            label: "Over-full-scale coverage",
            valueText: FormatCoverage(overFullScaleShare),
            basis: "derived",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "RMS_DBFS",
            label: "RMS level",
            valueText: FormatDbfs(metrics.Rms),
            basis: "measured",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "CREST_FACTOR",
            label: "Crest factor",
            valueText: $"{metrics.CrestFactor.ToString("0.00", CultureInfo.InvariantCulture)}x ({FormatSigned(metrics.CrestFactorDb)} dB)",
            basis: "derived",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "DURATION_SECONDS",
            label: "Duration",
            valueText: $"{metrics.DurationSeconds.ToString("0.00", CultureInfo.InvariantCulture)} s",
            basis: "measured",
            confidence: "high",
            source: sourcePath),
        CreateFact(
            code: "DOMINANT_FREQUENCY",
            label: "Dominant FFT bin",
            valueText: metrics.DominantFrequencyHz > 0
                ? $"{metrics.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz at {metrics.DominantMagnitudeDb.ToString("0.0", CultureInfo.InvariantCulture)} dB"
                : "Unavailable",
            basis: "derived",
            confidence: metrics.DominantFrequencyHz > 0 ? "medium" : "low",
            source: sourcePath),
        CreateFact(
            code: "SPECTRAL_CENTROID",
            label: "Spectral centroid",
            valueText: spectralSummary.SpectralCentroidHz > 0
                ? $"{spectralSummary.SpectralCentroidHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz"
                : "Unavailable",
            basis: "derived",
            confidence: spectralSummary.SpectralCentroidHz > 0 ? "medium" : "low",
            source: sourcePath),
        CreateFact(
            code: "LOW_BAND_ENERGY_SHARE",
            label: "Low-band energy share",
            valueText: FormatShare(spectralSummary.LowBandEnergyRatio),
            basis: "derived",
            confidence: "medium",
            source: sourcePath),
        CreateFact(
            code: "MID_BAND_ENERGY_SHARE",
            label: "Mid-band energy share",
            valueText: FormatShare(spectralSummary.MidBandEnergyRatio),
            basis: "derived",
            confidence: "medium",
            source: sourcePath),
        CreateFact(
            code: "HIGH_BAND_ENERGY_SHARE",
            label: "High-band energy share",
            valueText: FormatShare(spectralSummary.HighBandEnergyRatio),
            basis: "derived",
            confidence: "medium",
            source: sourcePath),
        CreateFact(
            code: "DOMINANT_ENERGY_BAND",
            label: "Dominant energy band",
            valueText: ToTitleCase(spectralSummary.DominantBand),
            basis: "derived",
            confidence: "medium",
            source: sourcePath),
        CreateFact(
            code: "SPECTROGRAM_DOMINANT_BAND",
            label: "Dominant spectrogram band",
            valueText: ToTitleCase(spectrogramSummary.DominantBand),
            basis: "derived",
            confidence: "medium",
            source: sourcePath),
        CreateFact(
            code: "SPECTROGRAM_TEMPORAL_VARIATION",
            label: "Temporal energy variation",
            valueText: spectrogramSummary.HasData
                ? spectrogramSummary.TemporalVariation.ToString("0.00", CultureInfo.InvariantCulture)
                : "Unavailable",
            basis: "derived",
            confidence: spectrogramSummary.HasData ? "medium" : "low",
            source: sourcePath)
    ];
    }

    private static EvidenceItemDto CreateFact(
        string code,
        string label,
        string valueText,
        string basis,
        string confidence,
        string source) =>
        new()
        {
            Basis = basis,
            Code = code,
            Confidence = confidence,
            Label = label,
            Source = source,
            ValueText = valueText
        };

    private static string FormatDbfs(double value)
    {
        if (value <= 1e-9d)
            return "-∞ dBFS";

        return $"{FormatSigned(ToDbfs(value))} dBFS";
    }

    private static string FormatSigned(double value) =>
        value >= 0
            ? $"+{value.ToString("0.0", CultureInfo.InvariantCulture)}"
            : value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string FormatShare(double value) =>
        $"{(value * 100d).ToString("0.0", CultureInfo.InvariantCulture)}%";

    private static string FormatCoverage(double value) =>
        $"{(value * 100d).ToString(value >= 0.1d ? "0.0" : "0.00", CultureInfo.InvariantCulture)}% of analyzed points";

    private static string FormatPercentagePointDelta(double value) =>
        $"{FormatSigned(value * 100d)} pts";

    private static double ComputeOverFullScaleShare(SignalMetrics metrics)
    {
        var totalPoints = Math.Max(1d, Math.Round(metrics.DurationSeconds * metrics.SampleRate));
        return Math.Clamp(metrics.SamplesOverFullScaleCount / totalPoints, 0d, 1d);
    }

    private static DecodedAudioSignal ApplySelection(
        DecodedAudioSignal signal,
        SelectionRangeDto? selection)
    {
        if (selection is null || signal.Samples.Length == 0 || signal.SampleRate <= 0)
            return signal;

        var startSeconds = Math.Max(0d, selection.StartSeconds ?? 0d);
        var endSeconds = Math.Max(startSeconds, selection.EndSeconds ?? startSeconds);
        var startIndex = Math.Clamp((int)Math.Floor(startSeconds * signal.SampleRate), 0, signal.Samples.Length - 1);
        var endIndex = Math.Clamp((int)Math.Ceiling(endSeconds * signal.SampleRate), startIndex + 1, signal.Samples.Length);
        var length = Math.Max(1, endIndex - startIndex);
        var slice = signal.Samples.Skip(startIndex).Take(length).ToArray();

        return slice.Length == signal.Samples.Length
            ? signal
            : new DecodedAudioSignal(slice, signal.SampleRate);
    }

    private static SpectralSummary SummarizeSpectrum(IReadOnlyList<SpectrumBin> spectrum)
    {
        if (spectrum.Count == 0)
            return new SpectralSummary();

        double totalMagnitude = 0d;
        double weightedFrequency = 0d;
        double lowBand = 0d;
        double midBand = 0d;
        double highBand = 0d;

        foreach (var bin in spectrum)
        {
            var magnitude = Math.Max(0d, bin.Magnitude);

            if (magnitude <= 0d)
                continue;

            totalMagnitude += magnitude;
            weightedFrequency += bin.FrequencyHz * magnitude;

            if (bin.FrequencyHz < LowBandUpperHz)
                lowBand += magnitude;
            else if (bin.FrequencyHz < MidBandUpperHz)
                midBand += magnitude;
            else
                highBand += magnitude;
        }

        if (totalMagnitude <= 1e-9d)
            return new SpectralSummary();

        var lowRatio = lowBand / totalMagnitude;
        var midRatio = midBand / totalMagnitude;
        var highRatio = highBand / totalMagnitude;

        return new SpectralSummary
        {
            DominantBand = DescribeDominantBand(lowRatio, midRatio, highRatio),
            HighBandEnergyRatio = highRatio,
            LowBandEnergyRatio = lowRatio,
            MidBandEnergyRatio = midRatio,
            SpectralCentroidHz = weightedFrequency / totalMagnitude
        };
    }

    private static SpectrogramSummary SummarizeSpectrogram(SpectrogramAnalysis spectrogram)
    {
        if (spectrogram.Cells.Count == 0 || spectrogram.Times.Count == 0 || spectrogram.Frequencies.Count == 0)
            return new SpectrogramSummary();

        var frameTotals = new double[spectrogram.Times.Count];
        var frameCounts = new int[spectrogram.Times.Count];
        double totalIntensity = 0d;
        double lowBand = 0d;
        double midBand = 0d;
        double highBand = 0d;

        foreach (var cell in spectrogram.Cells)
        {
            if (cell.TimeIndex < 0 || cell.TimeIndex >= frameTotals.Length)
                continue;

            if (cell.FrequencyIndex < 0 || cell.FrequencyIndex >= spectrogram.Frequencies.Count)
                continue;

            var intensity = Math.Max(0d, cell.Intensity);
            var frequency = spectrogram.Frequencies[cell.FrequencyIndex];
            frameTotals[cell.TimeIndex] += intensity;
            frameCounts[cell.TimeIndex]++;
            totalIntensity += intensity;

            if (frequency < LowBandUpperHz)
                lowBand += intensity;
            else if (frequency < MidBandUpperHz)
                midBand += intensity;
            else
                highBand += intensity;
        }

        var frameAverages = frameTotals
            .Select((total, index) => frameCounts[index] > 0 ? total / frameCounts[index] : 0d)
            .ToList();
        var mean = frameAverages.Count > 0 ? frameAverages.Average() : 0d;

        if (totalIntensity <= 1e-9d || mean <= 1e-9d)
            return new SpectrogramSummary();

        var variance = frameAverages.Sum(value => Math.Pow(value - mean, 2d)) / frameAverages.Count;
        var temporalVariation = Math.Sqrt(variance) / mean;
        var lowRatio = lowBand / totalIntensity;
        var midRatio = midBand / totalIntensity;
        var highRatio = highBand / totalIntensity;

        return new SpectrogramSummary
        {
            DominantBand = DescribeDominantBand(lowRatio, midRatio, highRatio),
            HasData = true,
            TemporalVariation = temporalVariation
        };
    }

    private static string DescribeDominantBand(double lowRatio, double midRatio, double highRatio)
    {
        var ranked = new[]
        {
            ("low", lowRatio),
            ("mid", midRatio),
            ("high", highRatio)
        }
        .OrderByDescending(item => item.Item2)
        .ToArray();

        if (ranked[0].Item2 <= 1e-9d)
            return "unavailable";

        if (ranked[0].Item2 - ranked[1].Item2 < 0.08d)
            return "mixed";

        return ranked[0].Item1;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unavailable";

        return value.ToLowerInvariant() switch
        {
            "low" => "Low",
            "mid" => "Mid",
            "high" => "High",
            "mixed" => "Mixed",
            _ => "Unavailable"
        };
    }

    private static bool HasActiveTransforms(SignalTransformRecipe transforms) =>
        transforms.Normalize ||
        transforms.TrimSilence ||
        Math.Abs(transforms.GainDb) > 0.01d ||
        !string.Equals(transforms.Filter.Mode, "none", StringComparison.OrdinalIgnoreCase);

    private static double ToDbfs(double value) => 20d * Math.Log10(Math.Max(value, 1e-9d));

    private sealed class SpectralSummary
    {
        public string DominantBand { get; init; } = "unavailable";

        public double HighBandEnergyRatio { get; init; }

        public double LowBandEnergyRatio { get; init; }

        public double MidBandEnergyRatio { get; init; }

        public double SpectralCentroidHz { get; init; }
    }

    private sealed class SpectrogramSummary
    {
        public string DominantBand { get; init; } = "unavailable";

        public bool HasData { get; init; }

        public double TemporalVariation { get; init; }
    }

    private sealed record ComparisonSummaryCacheEntry(ComparisonSummaryDto? Value);
}
