using System.Globalization;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class ObservationService : IObservationService
{
    public ObservationBundle Build(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary)
    {
        var observations = new List<ObservationDto>();
        var limitations = new List<string>
        {
            context.IsSelectionApplied
                ? $"Analysis is based on a mono decode at 22.05 kHz for {context.SelectionScope}."
                : "Analysis is based on a mono decode at 22.05 kHz for the full file.",
            "The MVP reports decoded sample metrics and FFT-derived features only. It does not currently estimate true peak or run a dedicated clipping detector."
        };
        var recommendedActions = new List<string>();

        if (signalSummary.SamplesOverFullScaleCount > 0)
        {
            observations.Add(CreateObservation(
                code: "PCM_OVER_FULL_SCALE",
                message: $"The decode is running over full scale, so headroom is the main concern. Sample peak reaches {FormatDbfs(signalSummary.Peak)}, and about {FormatPercentage(ComputeOverFullScaleShare(signalSummary))} of analyzed points are above 0 dBFS. That makes this {DescribeOverFullScaleExtent(signalSummary)} rather than a few isolated spikes. This does not by itself confirm clipping in the original source asset.",
                severity: "medium",
                basis: "measured",
                confidence: "high",
                evidenceCodes: ["SAMPLES_OVER_FULL_SCALE", "SAMPLE_PEAK_DBFS"]));

            recommendedActions.Add($"Reduce gain by at least {ComputeSuggestedGainReduction(signalSummary.Peak).ToString("0.0", CultureInfo.InvariantCulture)} dB and re-check sample peak.");
        }
        else if (signalSummary.Peak >= 0.98d)
        {
            observations.Add(CreateObservation(
                code: "LOW_HEADROOM",
                message: $"Decoded sample peak is {FormatDbfs(signalSummary.Peak)}, which leaves little headroom.",
                severity: "info",
                basis: "measured",
                confidence: "high",
                evidenceCodes: ["SAMPLE_PEAK_DBFS"]));

            recommendedActions.Add("Inspect the highest peaks before describing this as clipping.");
        }

        if (signalSummary.CrestFactorDb <= 8d)
        {
            observations.Add(CreateObservation(
                code: "DENSE_DYNAMICS",
                message: $"Crest factor is {FormatSigned(signalSummary.CrestFactorDb)} dB, which is consistent with relatively dense or tightly controlled dynamics.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["CREST_FACTOR"]));
        }

        if (signalSummary.DominantFrequencyHz > 0)
        {
            observations.Add(CreateObservation(
                code: "DOMINANT_FREQUENCY_PRESENT",
                message: $"The strongest FFT bin is centered near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["DOMINANT_FREQUENCY"]));

            if (!string.Equals(context.ActiveView, "fft", StringComparison.OrdinalIgnoreCase))
                recommendedActions.Add($"Switch to FFT view and inspect energy around {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz.");
        }

        AddViewSpecificObservations(context, signalSummary, observations, recommendedActions);

        if (comparisonSummary is not null)
        {
            foreach (var comparison in comparisonSummary.Comparisons.Take(2))
            {
                if (string.Equals(comparison.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase))
                {
                    observations.Add(CreateObservation(
                        code: $"TRANSFORM_RMS_DELTA_{comparison.FileId}",
                        message: $"After the current transforms, RMS changed by {FormatSigned(comparison.RmsDeltaDb)} dB relative to the original signal.",
                        severity: "info",
                        basis: "derived",
                        confidence: "high",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));

                    if (Math.Abs(comparison.DominantFrequencyDeltaHz) >= 10d)
                    {
                        observations.Add(CreateObservation(
                            code: $"TRANSFORM_DOMINANT_FREQUENCY_DELTA_{comparison.FileId}",
                            message: $"After the current transforms, the dominant FFT bin shifted by {FormatSigned(comparison.DominantFrequencyDeltaHz)} Hz relative to the original signal.",
                            severity: "info",
                            basis: "derived",
                            confidence: "medium",
                            evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                    }

                    if (Math.Abs(comparison.PeakDeltaDbFs) >= 0.5d)
                    {
                        observations.Add(CreateObservation(
                            code: $"TRANSFORM_PEAK_DELTA_{comparison.FileId}",
                            message: $"After the current transforms, sample peak changed by {FormatSigned(comparison.PeakDeltaDbFs)} dB relative to the original signal.",
                            severity: "info",
                            basis: "derived",
                            confidence: "high",
                            evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                    }

                    if (Math.Abs(comparison.SpectralCentroidDeltaHz) >= 120d)
                    {
                        observations.Add(CreateObservation(
                            code: $"TRANSFORM_SPECTRAL_CENTROID_DELTA_{comparison.FileId}",
                            message: $"After the current transforms, spectral centroid moved by {FormatSigned(comparison.SpectralCentroidDeltaHz)} Hz relative to the original signal.",
                            severity: "info",
                            basis: "derived",
                            confidence: "medium",
                            evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                    }

                    AddBandShiftObservation("TRANSFORM", comparison, observations);

                    if (Math.Abs(comparison.DurationDeltaSeconds) >= 0.05d)
                    {
                        observations.Add(CreateObservation(
                            code: $"TRANSFORM_DURATION_DELTA_{comparison.FileId}",
                            message: $"After the current transforms, duration changed by {FormatSigned(comparison.DurationDeltaSeconds)} seconds relative to the original signal.",
                            severity: "info",
                            basis: "measured",
                            confidence: "high",
                            evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                    }

                    recommendedActions.Add("Reset transforms to return to the original signal if you want to verify the before/after difference directly.");
                    continue;
                }

                if (Math.Abs(comparison.RmsDeltaDb) >= 1.5d)
                {
                    observations.Add(CreateObservation(
                        code: $"COMPARE_LEVEL_DELTA_{comparison.FileId}",
                        message: $"{comparison.SourcePath} differs by {FormatSigned(comparison.RmsDeltaDb)} dB RMS versus the selected file.",
                        severity: "info",
                        basis: "derived",
                        confidence: "high",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                }

                if (Math.Abs(comparison.PeakDeltaDbFs) >= 0.75d)
                {
                    observations.Add(CreateObservation(
                        code: $"COMPARE_PEAK_DELTA_{comparison.FileId}",
                        message: $"{comparison.SourcePath} differs by {FormatSigned(comparison.PeakDeltaDbFs)} dB in sample peak versus the selected file.",
                        severity: "info",
                        basis: "derived",
                        confidence: "high",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                }

                if (Math.Abs(comparison.DominantFrequencyDeltaHz) >= 150d)
                {
                    observations.Add(CreateObservation(
                        code: $"COMPARE_DOMINANT_FREQUENCY_DELTA_{comparison.FileId}",
                        message: $"{comparison.SourcePath} shifts the dominant FFT bin by {FormatSigned(comparison.DominantFrequencyDeltaHz)} Hz versus the selected file.",
                        severity: "info",
                        basis: "derived",
                        confidence: "medium",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                }

                if (Math.Abs(comparison.SpectralCentroidDeltaHz) >= 180d)
                {
                    observations.Add(CreateObservation(
                        code: $"COMPARE_SPECTRAL_CENTROID_DELTA_{comparison.FileId}",
                        message: $"{comparison.SourcePath} shifts spectral centroid by {FormatSigned(comparison.SpectralCentroidDeltaHz)} Hz versus the selected file.",
                        severity: "info",
                        basis: "derived",
                        confidence: "medium",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                }

                AddBandShiftObservation("COMPARE", comparison, observations);

                if (Math.Abs(comparison.DurationDeltaSeconds) >= 0.2d)
                {
                    observations.Add(CreateObservation(
                        code: $"COMPARE_DURATION_DELTA_{comparison.FileId}",
                        message: $"{comparison.SourcePath} differs in duration by {FormatSigned(comparison.DurationDeltaSeconds)} seconds versus the selected file.",
                        severity: "info",
                        basis: "measured",
                        confidence: "high",
                        evidenceCodes: comparison.Facts.Select(item => item.Code).ToList()));
                }
            }

            if (comparisonSummary.CompareFileIds.Count > 0)
            {
                limitations.Add("Compared files are analyzed without automatically applying the selected-file transform recipe.");
                recommendedActions.Add("Level-match the comparison files if you want a cleaner qualitative comparison.");
            }
        }

        if (HasActiveTransforms(context.Transforms))
        {
            observations.Add(CreateObservation(
                code: "TRANSFORMS_APPLY_TO_SELECTED_FILE_ONLY",
                message: "Active transforms apply to the selected file only, so the current facts are post-transform for that file.",
                severity: "info",
                basis: "rule_based",
                confidence: "high",
                evidenceCodes: []));

            limitations.Add("Active transforms can change level and duration metrics, so interpret them as post-transform values for the selected file.");
        }

        if (context.IsSelectionApplied)
            recommendedActions.Add("Reset AI scope to the full file if you want to compare this selected region against the overall signal.");

        if (recommendedActions.Count == 0)
            recommendedActions.Add("Ask a narrower question about level, dominant frequency, or transform impact to get a more targeted explanation.");

        return new ObservationBundle
        {
            Limitations = limitations,
            Observations = observations,
            RecommendedActions = recommendedActions.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList()
        };
    }

    private static ObservationDto CreateObservation(
        string code,
        string message,
        string severity,
        string basis,
        string confidence,
        List<string> evidenceCodes) =>
        new()
        {
            Basis = basis,
            Code = code,
            Confidence = confidence,
            EvidenceCodes = evidenceCodes,
            Message = message,
            Severity = severity
        };

    private static void AddViewSpecificObservations(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        List<ObservationDto> observations,
        List<string> recommendedActions)
    {
        switch (context.ActiveView.ToLowerInvariant())
        {
            case "waveform":
                AddWaveformObservations(signalSummary, observations, recommendedActions);
                break;
            case "fft":
                AddFftObservations(signalSummary, observations, recommendedActions);
                break;
            case "spectrogram":
                AddSpectrogramObservations(signalSummary, observations, recommendedActions);
                break;
        }
    }

    private static void AddWaveformObservations(
        SignalSummaryDto signalSummary,
        List<ObservationDto> observations,
        List<string> recommendedActions)
    {
        if (signalSummary.SamplesOverFullScaleCount > 0 || signalSummary.Peak >= 0.98d)
        {
            observations.Add(CreateObservation(
                code: "WAVEFORM_HEADROOM_FOCUS",
                message: $"In waveform view, headroom is the main standout because decoded sample peak reaches {FormatDbfs(signalSummary.Peak)}.",
                severity: "info",
                basis: "measured",
                confidence: "high",
                evidenceCodes: ["SAMPLE_PEAK_DBFS", "SAMPLES_OVER_FULL_SCALE"]));
        }

        if (signalSummary.CrestFactorDb <= 8d)
        {
            observations.Add(CreateObservation(
                code: "WAVEFORM_DENSE_ENVELOPE",
                message: $"In waveform view, the signal is likely to look relatively dense rather than sharply spiky because crest factor is {FormatSigned(signalSummary.CrestFactorDb)} dB.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["CREST_FACTOR"]));
        }
        else if (signalSummary.CrestFactorDb >= 14d)
        {
            observations.Add(CreateObservation(
                code: "WAVEFORM_PRONOUNCED_PEAKS",
                message: $"In waveform view, pronounced peaks should stand out against the average level because crest factor is {FormatSigned(signalSummary.CrestFactorDb)} dB.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["CREST_FACTOR"]));
        }

        recommendedActions.Add("Zoom into the loudest section if you want to confirm whether the highest peaks are isolated or repeated.");
    }

    private static void AddFftObservations(
        SignalSummaryDto signalSummary,
        List<ObservationDto> observations,
        List<string> recommendedActions)
    {
        if (signalSummary.DominantFrequencyHz > 0)
        {
            observations.Add(CreateObservation(
                code: "FFT_DOMINANT_PEAK_FOCUS",
                message: $"In FFT view, the strongest concentration sits near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["DOMINANT_FREQUENCY"]));
        }

        if (!string.Equals(signalSummary.DominantEnergyBand, "mixed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(signalSummary.DominantEnergyBand, "unavailable", StringComparison.OrdinalIgnoreCase))
        {
            var dominantShare = signalSummary.DominantEnergyBand switch
            {
                "low" => signalSummary.LowBandEnergyRatio,
                "mid" => signalSummary.MidBandEnergyRatio,
                "high" => signalSummary.HighBandEnergyRatio,
                _ => 0d
            };

            observations.Add(CreateObservation(
                code: "FFT_BAND_BALANCE",
                message: $"FFT energy is weighted toward the {signalSummary.DominantEnergyBand} band, which carries about {(dominantShare * 100d).ToString("0.0", CultureInfo.InvariantCulture)}% of the visible spectrum energy.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["LOW_BAND_ENERGY_SHARE", "MID_BAND_ENERGY_SHARE", "HIGH_BAND_ENERGY_SHARE", "DOMINANT_ENERGY_BAND"]));
        }

        if (signalSummary.SpectralCentroidHz > 0)
        {
            observations.Add(CreateObservation(
                code: "FFT_SPECTRAL_CENTROID",
                message: $"Spectral centroid is {signalSummary.SpectralCentroidHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz, which is a compact read on where the spectrum balances overall.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["SPECTRAL_CENTROID"]));
        }

        recommendedActions.Add("Inspect the dominant peak and the low-versus-high band balance before drawing broader conclusions.");
    }

    private static void AddSpectrogramObservations(
        SignalSummaryDto signalSummary,
        List<ObservationDto> observations,
        List<string> recommendedActions)
    {
        if (!string.Equals(signalSummary.SpectrogramDominantBand, "unavailable", StringComparison.OrdinalIgnoreCase))
        {
            observations.Add(CreateObservation(
                code: "SPECTROGRAM_BAND_FOCUS",
                message: $"In spectrogram view, the most sustained energy sits in the {signalSummary.SpectrogramDominantBand} band.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["SPECTROGRAM_DOMINANT_BAND"]));
        }

        if (signalSummary.SpectrogramTemporalVariation > 0.32d)
        {
            observations.Add(CreateObservation(
                code: "SPECTROGRAM_TEMPORAL_VARIATION_HIGH",
                message: $"Energy changes noticeably over time in the spectrogram, so time-local bursts matter more than the average spectrum.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["SPECTROGRAM_TEMPORAL_VARIATION"]));
        }
        else if (signalSummary.SpectrogramTemporalVariation > 0d)
        {
            observations.Add(CreateObservation(
                code: "SPECTROGRAM_TEMPORAL_VARIATION_STABLE",
                message: "Energy stays relatively consistent over time in the spectrogram rather than appearing only as isolated bursts.",
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: ["SPECTROGRAM_TEMPORAL_VARIATION"]));
        }

        recommendedActions.Add("Compare the early and late sections if you want to confirm whether the visible band energy stays stable over time.");
    }

    private static void AddBandShiftObservation(
        string prefix,
        ComparisonDeltaDto comparison,
        List<ObservationDto> observations)
    {
        var lowDelta = comparison.LowBandEnergyDelta;
        var highDelta = comparison.HighBandEnergyDelta;
        var evidenceCodes = comparison.Facts.Select(item => item.Code).ToList();

        if (Math.Abs(lowDelta) >= 0.08d)
        {
            observations.Add(CreateObservation(
                code: $"{prefix}_LOW_BAND_DELTA_{comparison.FileId}",
                message: BuildBandShiftMessage(prefix, comparison, "low", lowDelta),
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: evidenceCodes));
        }

        if (Math.Abs(highDelta) >= 0.08d)
        {
            observations.Add(CreateObservation(
                code: $"{prefix}_HIGH_BAND_DELTA_{comparison.FileId}",
                message: BuildBandShiftMessage(prefix, comparison, "high", highDelta),
                severity: "info",
                basis: "derived",
                confidence: "medium",
                evidenceCodes: evidenceCodes));
        }
    }

    private static string BuildBandShiftMessage(
        string prefix,
        ComparisonDeltaDto comparison,
        string bandName,
        double delta)
    {
        var amount = $"{FormatSigned(delta * 100d)} points";

        return string.Equals(prefix, "TRANSFORM", StringComparison.OrdinalIgnoreCase)
            ? $"After the current transforms, {bandName}-band energy share changed by {amount} relative to the original signal."
            : $"{comparison.SourcePath} changes {bandName}-band energy share by {amount} versus the selected file.";
    }

    private static double ComputeSuggestedGainReduction(double peak)
    {
        if (peak <= 1e-9d)
            return 0d;

        return Math.Max(0d, ToDbfs(peak) + 1d);
    }

    private static double ComputeOverFullScaleShare(SignalSummaryDto signalSummary)
    {
        var totalPoints = Math.Max(1d, Math.Round(signalSummary.DurationSeconds * signalSummary.SampleRateHz));
        return Math.Clamp(signalSummary.SamplesOverFullScaleCount / totalPoints, 0d, 1d);
    }

    private static string DescribeOverFullScaleExtent(SignalSummaryDto signalSummary)
    {
        var share = ComputeOverFullScaleShare(signalSummary);

        if (share >= 0.01d)
            return "a persistent over-full-scale condition in the decode path";

        if (share >= 0.001d)
            return "a recurring over-full-scale condition in the decode path";

        return "an isolated over-full-scale condition in the decode path";
    }

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

    private static string FormatPercentage(double value) =>
        $"{(value * 100d).ToString(value >= 0.1d ? "0.0" : "0.00", CultureInfo.InvariantCulture)}%";

    private static bool HasActiveTransforms(Shared.SignalAnalysis.SignalTransformRecipe transforms) =>
        transforms.Normalize ||
        transforms.TrimSilence ||
        Math.Abs(transforms.GainDb) > 0.01d ||
        !string.Equals(transforms.Filter.Mode, "none", StringComparison.OrdinalIgnoreCase);

    private static double ToDbfs(double value) => 20d * Math.Log10(Math.Max(value, 1e-9d));
}
