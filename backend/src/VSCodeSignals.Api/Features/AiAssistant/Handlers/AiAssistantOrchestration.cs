using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

internal sealed class AiNarrativeResult
{
    public string Answer { get; init; } = string.Empty;

    public List<string> FollowUpPrompts { get; init; } = [];

    public string Headline { get; init; } = string.Empty;

    public bool UsedFallback { get; init; }
}

internal sealed class AiNarrativePayload
{
    public string Answer { get; init; } = string.Empty;

    public List<string> FollowUpPrompts { get; init; } = [];

    public string Headline { get; init; } = string.Empty;
}

internal sealed class AiResponseComposer(
    IAiPromptBuilder promptBuilder,
    IModelRoutingService modelRoutingService,
    LlmProviderRegistry llmProviderRegistry,
    AiExecutionReceiptStore receiptStore,
    IOptions<AiAssistantOptions> assistantOptions,
    ILogger<AiResponseComposer> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiNarrativeResult> GenerateNarrativeAsync(
        AiOperationKind operation,
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle,
        string prompt,
        IReadOnlyList<AiConversationTurnDto> history,
        CancellationToken ct)
    {
        var fallback = BuildFallbackNarrative(operation, context, signalSummary, comparisonSummary, observationBundle);

        if (!assistantOptions.Value.EnableLlm)
            return fallback;

        var route = modelRoutingService.Resolve(operation, prompt);
        var llmRequest = promptBuilder.BuildExplanationPrompt(
            operation,
            context,
            signalSummary,
            comparisonSummary,
            observationBundle,
            prompt,
            history);

        foreach (var providerKey in ResolveProviderAttemptOrder(route))
        {
            try
            {
                var provider = llmProviderRegistry.Resolve(providerKey);
                var json = await provider.GenerateStructuredJsonAsync(new LlmStructuredRequest
                {
                    JsonSchema = llmRequest.JsonSchema,
                    Model = providerKey.Equals("openai", StringComparison.OrdinalIgnoreCase)
                        ? route.Model
                        : string.Empty,
                    SchemaName = llmRequest.SchemaName,
                    SystemPrompt = llmRequest.SystemPrompt,
                    Temperature = llmRequest.Temperature,
                    UserPrompt = llmRequest.UserPrompt
                }, ct);
                var payload = JsonSerializer.Deserialize<AiNarrativePayload>(json, SerializerOptions);

                if (payload is null || string.IsNullOrWhiteSpace(payload.Answer))
                    continue;

                receiptStore.Record(new AiExecutionReceipt
                {
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    FailureReason = string.Empty,
                    Model = providerKey.Equals("openai", StringComparison.OrdinalIgnoreCase)
                        ? route.Model
                        : "configured-ollama-model",
                    Operation = operation.ToString(),
                    ProviderKey = providerKey,
                    Succeeded = true
                });

                return new AiNarrativeResult
                {
                    Answer = payload.Answer.Trim(),
                    FollowUpPrompts = payload.FollowUpPrompts
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Select(item => item.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(4)
                        .ToList(),
                    Headline = string.IsNullOrWhiteSpace(payload.Headline)
                        ? fallback.Headline
                        : payload.Headline.Trim(),
                    UsedFallback = false
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AI assistant provider {ProviderKey} failed: {Reason}", providerKey, ex.Message);
            }
        }

        receiptStore.Record(new AiExecutionReceipt
        {
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FailureReason = "All provider attempts failed; backend fallback used.",
            Model = "fallback",
            Operation = operation.ToString(),
            ProviderKey = "backend",
            Succeeded = false,
            UsedFallback = true
        });

        return fallback;
    }

    private static IReadOnlyList<string> ResolveProviderAttemptOrder(ModelRouteDecision route)
    {
        if (route.AllowLocalFallback &&
            !string.Equals(route.ProviderKey, "ollama", StringComparison.OrdinalIgnoreCase))
        {
            return [route.ProviderKey, "ollama"];
        }

        return [route.ProviderKey];
    }

    public AiSummaryCardDto BuildSummaryCard(
        AiOperationKind operation,
        WorkspaceContextDto context,
        AiNarrativeResult narrative,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle) =>
        new()
        {
            KeyFacts = SelectKeyFacts(operation, context.ActiveView, signalSummary, comparisonSummary),
            Limitations = observationBundle.Limitations,
            NextSteps = observationBundle.RecommendedActions,
            Summary = narrative.Answer,
            Title = string.IsNullOrWhiteSpace(narrative.Headline)
                ? GetDefaultHeadline(operation, context.ActiveView)
                : narrative.Headline,
            TopObservations = SelectTopObservations(operation, context.ActiveView, observationBundle.Observations)
        };

    public List<AiFollowUpPromptDto> BuildFollowUps(
        AiOperationKind operation,
        AiNarrativeResult narrative,
        ObservationBundle observationBundle)
    {
        var prompts = narrative.FollowUpPrompts.Count > 0
            ? narrative.FollowUpPrompts
            : observationBundle.RecommendedActions;

        return prompts
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select((item, index) => new AiFollowUpPromptDto
            {
                Id = $"{operation.ToString().ToLowerInvariant()}-{index}",
                Intent = operation.ToString().ToLowerInvariant(),
                Label = item,
                Prompt = item
            })
            .ToList();
    }

    private static AiNarrativeResult BuildFallbackNarrative(
        AiOperationKind operation,
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle)
    {
        var headline = GetDefaultHeadline(operation, context.ActiveView);
        string answer;

        switch (operation)
        {
            case AiOperationKind.Compare:
                answer = comparisonSummary is null || comparisonSummary.Comparisons.Count == 0
                    ? "No comparison files are currently selected, so I can only describe the active signal."
                    : BuildComparisonAnswer(comparisonSummary, observationBundle);
                break;
            case AiOperationKind.Recommend:
                answer = BuildRecommendationAnswer(context, observationBundle);
                break;
            default:
                answer = BuildExplainAnswer(context, signalSummary, observationBundle);
                break;
        }

        return new AiNarrativeResult
        {
            Answer = answer,
            FollowUpPrompts = observationBundle.RecommendedActions,
            Headline = headline,
            UsedFallback = true
        };
    }

    private static string BuildExplainAnswer(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ObservationBundle observationBundle)
    {
        var sentences = context.ActiveView.ToLowerInvariant() switch
        {
            "fft" => BuildFftExplainSentences(signalSummary, observationBundle.Observations),
            "spectrogram" => BuildSpectrogramExplainSentences(signalSummary, observationBundle.Observations),
            _ => BuildWaveformExplainSentences(signalSummary, observationBundle.Observations)
        };
        var factSummary = BuildViewFactSummary(context.ActiveView, signalSummary);

        if (!string.IsNullOrWhiteSpace(factSummary))
            sentences.Add($"Key facts: {factSummary}.");

        if (sentences.Count == 0)
            return "The selected signal is ready for analysis, but there are not enough grounded findings yet to highlight a strong standout.";

        return string.Join(" ", sentences.Take(4));
    }

    private static string BuildComparisonAnswer(
        ComparisonSummaryDto comparisonSummary,
        ObservationBundle observationBundle)
    {
        var comparisonObservations = observationBundle.Observations
            .Where(item => item.Code.StartsWith("TRANSFORM_", StringComparison.OrdinalIgnoreCase) ||
                           item.Code.StartsWith("COMPARE_", StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.Equals(item.Code, "TRANSFORMS_APPLY_TO_SELECTED_FILE_ONLY", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => GetComparisonPriority(item))
            .ThenByDescending(item => GetSeverityWeight(item.Severity))
            .DistinctBy(GetObservationTopicKey)
            .Select(item => item.Message)
            .Take(3)
            .ToList();

        if (comparisonObservations.Count > 0)
        {
            var answer = string.Join(" ", comparisonObservations);
            var persistentIssue = observationBundle.Observations.FirstOrDefault(item => item.Code == "PCM_OVER_FULL_SCALE");

            if (persistentIssue is not null)
                answer = $"{answer} {persistentIssue.Message}";

            return answer;
        }

        var first = comparisonSummary.Comparisons[0];

        return string.Equals(first.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase)
            ? $"After the current transforms, RMS changed by {first.RmsDeltaDb:+0.0;-0.0} dB, sample peak changed by {first.PeakDeltaDbFs:+0.0;-0.0} dB, and spectral centroid shifted by {first.SpectralCentroidDeltaHz:+0.0;-0.0} Hz relative to the original signal."
            : $"{first.SourcePath} differs by {first.RmsDeltaDb:+0.0;-0.0} dB RMS, {first.PeakDeltaDbFs:+0.0;-0.0} dB in sample peak, and {first.SpectralCentroidDeltaHz:+0.0;-0.0} Hz in spectral centroid versus the selected file.";
    }

    private static string BuildRecommendationAnswer(
        WorkspaceContextDto context,
        ObservationBundle observationBundle)
    {
        if (observationBundle.RecommendedActions.Count == 0)
            return "Inspect the current metrics and narrow the question to level, dominant frequency, or transform impact.";

        var firstStep = observationBundle.RecommendedActions[0];
        var reason = context.ActiveView.ToLowerInvariant() switch
        {
            "fft" => "the current FFT view is best read by checking the main peak against the overall band balance",
            "spectrogram" => "the current spectrogram view benefits from checking whether the visible energy pattern stays stable over time",
            _ => "the current waveform view needs one concrete next check rather than a broader summary"
        };

        return $"Next best step: {firstStep} This is useful because {reason}.";
    }

    private static string GetDefaultHeadline(AiOperationKind operation, string activeView) =>
        operation switch
        {
            AiOperationKind.Compare => "Comparison summary",
            AiOperationKind.Recommend => "Recommended next step",
            AiOperationKind.Summary when string.Equals(activeView, "fft", StringComparison.OrdinalIgnoreCase) => "FFT summary",
            AiOperationKind.Summary when string.Equals(activeView, "spectrogram", StringComparison.OrdinalIgnoreCase) => "Spectrogram summary",
            AiOperationKind.Summary when string.Equals(activeView, "waveform", StringComparison.OrdinalIgnoreCase) => "Waveform summary",
            _ when string.Equals(activeView, "fft", StringComparison.OrdinalIgnoreCase) => "FFT summary",
            _ when string.Equals(activeView, "spectrogram", StringComparison.OrdinalIgnoreCase) => "Spectrogram summary",
            _ when string.Equals(activeView, "waveform", StringComparison.OrdinalIgnoreCase) => "Waveform summary",
            _ => "Signal summary"
        };

    private static List<ObservationDto> SelectExplainObservations(
        string activeView,
        IReadOnlyList<ObservationDto> observations) =>
        observations
            .OrderByDescending(item => GetExplainPriority(activeView, item))
            .ThenByDescending(item => GetSeverityWeight(item.Severity))
            .DistinctBy(GetObservationTopicKey)
            .Take(3)
            .ToList();

    private static int GetExplainPriority(string activeView, ObservationDto observation)
    {
        var normalizedView = activeView.ToLowerInvariant();
        var code = observation.Code;

        if (normalizedView == "waveform" && code.StartsWith("WAVEFORM_", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (normalizedView == "fft" && code.StartsWith("FFT_", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (normalizedView == "spectrogram" && code.StartsWith("SPECTROGRAM_", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (code is "PCM_OVER_FULL_SCALE" or "LOW_HEADROOM" or "DENSE_DYNAMICS" or "DOMINANT_FREQUENCY_PRESENT")
            return 35;

        if (code.StartsWith("TRANSFORM_", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("COMPARE_", StringComparison.OrdinalIgnoreCase))
            return 20;

        if (string.Equals(code, "TRANSFORMS_APPLY_TO_SELECTED_FILE_ONLY", StringComparison.OrdinalIgnoreCase))
            return 5;

        return 10;
    }

    private static int GetSeverityWeight(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "high" => 4,
            "medium" => 3,
            "warning" => 3,
            "info" => 2,
            _ => 1
        };

    private static string BuildViewFactSummary(string activeView, SignalSummaryDto signalSummary)
    {
        var descriptors = activeView.ToLowerInvariant() switch
        {
            "fft" => new[]
            {
                BuildFactDescriptor(signalSummary.Facts, "DOMINANT_FREQUENCY", "dominant FFT bin"),
                BuildFactDescriptor(signalSummary.Facts, "SPECTRAL_CENTROID", "spectral centroid"),
                BuildFactDescriptor(signalSummary.Facts, "DOMINANT_ENERGY_BAND", "dominant energy band")
            },
            "spectrogram" => new[]
            {
                BuildFactDescriptor(signalSummary.Facts, "SPECTROGRAM_DOMINANT_BAND", "dominant spectrogram band"),
                BuildFactDescriptor(signalSummary.Facts, "SPECTROGRAM_TEMPORAL_VARIATION", "temporal energy variation"),
                BuildFactDescriptor(signalSummary.Facts, "DOMINANT_FREQUENCY", "dominant FFT bin")
            },
            _ => new[]
            {
                BuildFactDescriptor(signalSummary.Facts, "SAMPLE_PEAK_DBFS", "sample peak"),
                BuildFactDescriptor(signalSummary.Facts, "RMS_DBFS", "RMS level"),
                BuildFactDescriptor(signalSummary.Facts, "CREST_FACTOR", "crest factor")
            }
        };

        return JoinNaturalLanguage(descriptors.Where(item => !string.IsNullOrWhiteSpace(item)).ToList());
    }

    private static List<EvidenceItemDto> SelectKeyFacts(
        AiOperationKind operation,
        string activeView,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary)
    {
        if (operation == AiOperationKind.Compare && comparisonSummary is not null && comparisonSummary.Comparisons.Count > 0)
            return SelectComparisonFacts(comparisonSummary);

        var preferredCodes = activeView.ToLowerInvariant() switch
        {
            "fft" => new[] { "DOMINANT_FREQUENCY", "SPECTRAL_CENTROID", "LOW_BAND_ENERGY_SHARE", "HIGH_BAND_ENERGY_SHARE" },
            "spectrogram" => new[] { "SPECTROGRAM_DOMINANT_BAND", "SPECTROGRAM_TEMPORAL_VARIATION", "DOMINANT_FREQUENCY", "DURATION_SECONDS" },
            _ => new[] { "SAMPLE_PEAK_DBFS", "OVER_FULL_SCALE_SHARE", "RMS_DBFS", "CREST_FACTOR" }
        };

        var selected = preferredCodes
            .Select(code => signalSummary.Facts.FirstOrDefault(item => item.Code == code))
            .Where(IsAvailableFact)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        if (selected.Count >= 4)
            return selected.Take(4).ToList();

        foreach (var fact in signalSummary.Facts)
        {
            if (selected.Any(item => item.Code == fact.Code) || !IsAvailableFact(fact))
                continue;

            selected.Add(fact);

            if (selected.Count == 4)
                break;
        }

        return selected;
    }

    private static List<ObservationDto> SelectTopObservations(
        AiOperationKind operation,
        string activeView,
        IReadOnlyList<ObservationDto> observations)
    {
        if (operation == AiOperationKind.Compare)
        {
            var comparisonObservations = observations
                .Where(item => item.Code.StartsWith("TRANSFORM_", StringComparison.OrdinalIgnoreCase) ||
                               item.Code.StartsWith("COMPARE_", StringComparison.OrdinalIgnoreCase))
                .Where(item => !string.Equals(item.Code, "TRANSFORMS_APPLY_TO_SELECTED_FILE_ONLY", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            if (comparisonObservations.Count > 0)
                return comparisonObservations;
        }

        return SelectExplainObservations(activeView, observations)
            .Take(3)
            .ToList();
    }

    private static string FormatValue(IReadOnlyList<EvidenceItemDto> facts, string code) =>
        facts.FirstOrDefault(item => item.Code == code)?.ValueText ?? "unavailable";

    private static bool IsAvailableFact(EvidenceItemDto? fact) =>
        fact is not null &&
        !string.IsNullOrWhiteSpace(fact.ValueText) &&
        !string.Equals(fact.ValueText, "unavailable", StringComparison.OrdinalIgnoreCase);

    private static string BuildFactDescriptor(
        IReadOnlyList<EvidenceItemDto> facts,
        string code,
        string label)
    {
        var value = FormatValue(facts, code);
        return string.Equals(value, "unavailable", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $"{label} {value}";
    }

    private static string JoinNaturalLanguage(IReadOnlyList<string> items)
    {
        if (items.Count == 0)
            return string.Empty;

        if (items.Count == 1)
            return items[0];

        if (items.Count == 2)
            return $"{items[0]} and {items[1]}";

        return $"{string.Join(", ", items.Take(items.Count - 1))}, and {items[^1]}";
    }

    private static List<string> BuildWaveformExplainSentences(
        SignalSummaryDto signalSummary,
        IReadOnlyList<ObservationDto> observations)
    {
        var sentences = new List<string>();
        var headroom = SelectPreferredObservation(
            observations,
            "WAVEFORM_HEADROOM_FOCUS",
            "PCM_OVER_FULL_SCALE",
            "LOW_HEADROOM");
        var dynamics = SelectPreferredObservation(
            observations,
            "WAVEFORM_DENSE_ENVELOPE",
            "WAVEFORM_PRONOUNCED_PEAKS",
            "DENSE_DYNAMICS");

        if (signalSummary.SamplesOverFullScaleCount > 0)
        {
            sentences.Add(
                $"In waveform view, headroom is the main issue because the decode is above full scale, with sample peak at {FormatDbfsFromFact(signalSummary.Facts, "SAMPLE_PEAK_DBFS")}.");
            sentences.Add(
                $"About {FormatOverFullScaleCoverage(signalSummary)} of analyzed points are above 0 dBFS, so this looks like a persistent headroom problem rather than a few isolated spikes.");
        }
        else if (headroom is not null)
        {
            sentences.Add(headroom.Message);
        }

        if (dynamics is not null)
            sentences.Add(dynamics.Message);

        if (signalSummary.DominantFrequencyHz > 0)
        {
            sentences.Add(
                $"The strongest recurring spectral component sits near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz, so the waveform read should be paired with FFT inspection if you need frequency-specific explanation.");
        }

        return sentences;
    }

    private static List<string> BuildFftExplainSentences(
        SignalSummaryDto signalSummary,
        IReadOnlyList<ObservationDto> observations)
    {
        var sentences = new List<string>();
        var balance = SelectPreferredObservation(observations, "FFT_BAND_BALANCE");
        var centroid = SelectPreferredObservation(observations, "FFT_SPECTRAL_CENTROID");
        var headroom = SelectPreferredObservation(observations, "PCM_OVER_FULL_SCALE");

        if (signalSummary.DominantFrequencyHz > 0)
        {
            sentences.Add(
                $"In FFT view, the dominant FFT bin sits near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz.");
        }

        if (balance is not null)
            sentences.Add(balance.Message);

        if (centroid is not null)
            sentences.Add(centroid.Message);

        if (headroom is not null)
            sentences.Add($"Level still needs attention alongside the spectrum because the current decode remains above full scale across about {FormatOverFullScaleCoverage(signalSummary)} of analyzed points.");

        return sentences;
    }

    private static List<string> BuildSpectrogramExplainSentences(
        SignalSummaryDto signalSummary,
        IReadOnlyList<ObservationDto> observations)
    {
        var sentences = new List<string>();
        var band = SelectPreferredObservation(observations, "SPECTROGRAM_BAND_FOCUS");
        var variation = SelectPreferredObservation(
            observations,
            "SPECTROGRAM_TEMPORAL_VARIATION_HIGH",
            "SPECTROGRAM_TEMPORAL_VARIATION_STABLE");
        var headroom = SelectPreferredObservation(observations, "PCM_OVER_FULL_SCALE");

        if (band is not null)
            sentences.Add(band.Message);

        if (variation is not null)
            sentences.Add(variation.Message);

        if (signalSummary.DominantFrequencyHz > 0)
        {
            sentences.Add(
                $"The strongest average spectral component still sits near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz, which helps anchor the spectrogram read.");
        }

        if (headroom is not null)
            sentences.Add("Any level conclusions should still account for the current over-full-scale decode path.");

        return sentences;
    }

    private static string FormatDbfsFromFact(IReadOnlyList<EvidenceItemDto> facts, string code)
    {
        var value = FormatValue(facts, code);
        return string.Equals(value, "unavailable", StringComparison.OrdinalIgnoreCase) ? "an unavailable value" : value;
    }

    private static string FormatOverFullScaleCoverage(SignalSummaryDto signalSummary)
    {
        var totalPoints = Math.Max(1d, Math.Round(signalSummary.DurationSeconds * signalSummary.SampleRateHz));
        var share = Math.Clamp(signalSummary.SamplesOverFullScaleCount / totalPoints, 0d, 1d);
        return $"{(share * 100d).ToString(share >= 0.1d ? "0.0" : "0.00", CultureInfo.InvariantCulture)}%";
    }

    private static string GetObservationTopicKey(ObservationDto observation)
    {
        var code = observation.Code.ToUpperInvariant();

        if (code.Contains("HEADROOM") || code.Contains("OVER_FULL_SCALE") || code.Contains("CLIPPING"))
            return "headroom";

        if (code.Contains("DOMINANT_FREQUENCY") || code.Contains("DOMINANT_PEAK"))
            return "dominant_peak";

        if (code.Contains("SPECTRAL_CENTROID"))
            return "spectral_centroid";

        if (code.Contains("LOW_BAND") || code.Contains("HIGH_BAND") || code.Contains("BAND_BALANCE"))
            return "band_balance";

        if (code.Contains("TEMPORAL_VARIATION"))
            return "temporal_variation";

        if (code.Contains("DENSE") || code.Contains("PEAKS"))
            return "dynamics";

        if (code.Contains("RMS_DELTA"))
            return "compare_rms";

        if (code.Contains("PEAK_DELTA"))
            return "compare_peak";

        if (code.Contains("DURATION_DELTA"))
            return "compare_duration";

        return code;
    }

    private static ObservationDto? SelectPreferredObservation(
        IReadOnlyList<ObservationDto> observations,
        params string[] codes)
    {
        foreach (var code in codes)
        {
            var match = observations.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;
        }

        return null;
    }

    private static int GetComparisonPriority(ObservationDto observation)
    {
        var code = observation.Code;

        if (code.Contains("RMS_DELTA", StringComparison.OrdinalIgnoreCase))
            return 60;

        if (code.Contains("PEAK_DELTA", StringComparison.OrdinalIgnoreCase))
            return 55;

        if (code.Contains("SPECTRAL_CENTROID", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("DOMINANT_FREQUENCY", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (code.Contains("LOW_BAND", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("HIGH_BAND", StringComparison.OrdinalIgnoreCase))
            return 45;

        return 20;
    }

    private static List<EvidenceItemDto> SelectComparisonFacts(ComparisonSummaryDto comparisonSummary)
    {
        var first = comparisonSummary.Comparisons[0];
        var preferredPatterns = string.Equals(first.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase)
            ? new[] { "TRANSFORM_BASELINE_RMS", "TRANSFORM_BASELINE_PEAK", "TRANSFORM_BASELINE_SPECTRAL_CENTROID", "TRANSFORM_BASELINE_LOW_BAND", "TRANSFORM_BASELINE_HIGH_BAND" }
            : new[] { "COMPARE_RMS_", "COMPARE_SPECTRAL_CENTROID_", "COMPARE_LOW_BAND_", "COMPARE_HIGH_BAND_", "COMPARE_DURATION_" };
        var selected = new List<EvidenceItemDto>();

        foreach (var pattern in preferredPatterns)
        {
            var fact = first.Facts.FirstOrDefault(item =>
                item.Code.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                item.Code.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));

            if (fact is null || selected.Any(item => item.Code == fact.Code))
                continue;

            selected.Add(fact);

            if (selected.Count == 4)
                return selected;
        }

        foreach (var fact in first.Facts)
        {
            if (selected.Any(item => item.Code == fact.Code))
                continue;

            selected.Add(fact);

            if (selected.Count == 4)
                break;
        }

        return selected;
    }
}

internal sealed class AiOrchestrator(
    IAiIntentClassifier intentClassifier,
    IAiActionPlanner actionPlanner,
    AiResponseComposer responseComposer) : IAiOrchestrator
{
    public async Task<AiResponseDto> ProcessAsync(
        AiRequestDto request,
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle,
        CancellationToken ct)
    {
        var intentResult = intentClassifier.Classify(request, context);

        if (intentResult.Intent == AiIntentType.Action)
        {
            var proposal = await actionPlanner.PlanAsync(new AiPlanActionRequestDto
            {
                ActiveView = request.ActiveView,
                CompareFileIds = request.CompareFileIds,
                FileId = request.FileId,
                History = request.History,
                Prompt = request.Prompt,
                Selection = request.Selection,
                Transforms = request.Transforms,
                WorkspaceId = request.WorkspaceId
            }, context, ct);
            var summaryNarrative = await responseComposer.GenerateNarrativeAsync(
                AiOperationKind.Explain,
                context,
                signalSummary,
                comparisonSummary,
                observationBundle,
                request.Prompt,
                request.History,
                ct);

            return new AiResponseDto
            {
                ActionProposal = proposal,
                Context = context,
                FollowUpPrompts = responseComposer.BuildFollowUps(AiOperationKind.Explain, summaryNarrative, observationBundle),
                Intent = "action",
                Limitations = observationBundle.Limitations,
                Message = proposal.Status switch
                {
                    "unsupported" => proposal.UnsupportedReason,
                    "needs_clarification" => proposal.ClarificationQuestion,
                    _ => proposal.Summary
                },
                Observations = observationBundle.Observations,
                Status = proposal.Status,
                SummaryCard = responseComposer.BuildSummaryCard(AiOperationKind.Explain, context, summaryNarrative, signalSummary, comparisonSummary, observationBundle),
                UsedFallback = summaryNarrative.UsedFallback
            };
        }

        var operation = intentResult.Intent switch
        {
            AiIntentType.Compare => AiOperationKind.Compare,
            AiIntentType.Recommend => AiOperationKind.Recommend,
            _ => AiOperationKind.Explain
        };
        var narrative = await responseComposer.GenerateNarrativeAsync(
            operation,
            context,
            signalSummary,
            comparisonSummary,
            observationBundle,
            request.Prompt,
            request.History,
            ct);

        return new AiResponseDto
        {
            Context = context,
            FollowUpPrompts = responseComposer.BuildFollowUps(operation, narrative, observationBundle),
            Intent = intentResult.Intent.ToString().ToLowerInvariant(),
            Limitations = observationBundle.Limitations,
            Message = narrative.Answer,
            Observations = observationBundle.Observations,
            Status = narrative.UsedFallback ? "degraded" : "ready",
            SummaryCard = responseComposer.BuildSummaryCard(operation, context, narrative, signalSummary, comparisonSummary, observationBundle),
            UsedFallback = narrative.UsedFallback
        };
    }
}

internal sealed class AiAssistantService(
    IWorkspaceContextService workspaceContextService,
    ISignalAnalysisService signalAnalysisService,
    IObservationService observationService,
    IAiOrchestrator aiOrchestrator,
    IAiActionPlanner actionPlanner,
    IAiActionValidator actionValidator,
    IWorkspaceCommandExecutor workspaceCommandExecutor,
    AiResponseComposer responseComposer) : IAiAssistantService
{
    public async Task<WorkspaceContextDto> GetContextAsync(AiWorkspaceRequestContextDto request, CancellationToken ct) =>
        await workspaceContextService.BuildAsync(request, ct);

    public async Task<AiSummaryCardDto> SummaryAsync(AiSummaryRequestDto request, CancellationToken ct)
    {
        var context = await workspaceContextService.BuildAsync(request, ct);
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(context, ct);
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(context, signalSummary, ct);
        var observationBundle = observationService.Build(context, signalSummary, comparisonSummary);
        var narrative = await responseComposer.GenerateNarrativeAsync(
            AiOperationKind.Summary,
            context,
            signalSummary,
            comparisonSummary,
            observationBundle,
            "Provide a first-pass summary of the current signal workspace state.",
            [],
            ct);

        return responseComposer.BuildSummaryCard(AiOperationKind.Summary, context, narrative, signalSummary, comparisonSummary, observationBundle);
    }

    public async Task<AiResponseDto> AskAsync(AiRequestDto request, CancellationToken ct)
    {
        var context = await workspaceContextService.BuildAsync(request, ct);
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(context, ct);
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(context, signalSummary, ct);
        var observationBundle = observationService.Build(context, signalSummary, comparisonSummary);

        return await aiOrchestrator.ProcessAsync(request, context, signalSummary, comparisonSummary, observationBundle, ct);
    }

    public async Task<AiActionProposalDto> PlanActionAsync(AiPlanActionRequestDto request, CancellationToken ct)
    {
        var context = await workspaceContextService.BuildAsync(request, ct);
        var proposal = await actionPlanner.PlanAsync(request, context, ct);

        if (string.Equals(proposal.Status, "needs_confirmation", StringComparison.OrdinalIgnoreCase))
        {
            var validation = actionValidator.Validate(proposal, context);

            if (!validation.IsValid)
            {
                return new AiActionProposalDto
                {
                    ClarificationQuestion = string.Join(" ", validation.Errors),
                    ProposalId = proposal.ProposalId,
                    RequiresConfirmation = false,
                    Status = "needs_clarification",
                    Summary = string.Join(" ", validation.Errors),
                    Title = "Clarification needed"
                };
            }
        }

        return proposal;
    }

    public async Task<AiResponseDto> ExecuteActionAsync(AiExecuteActionRequestDto request, CancellationToken ct)
    {
        if (!request.Confirmed)
            throw new InvalidOperationException("Assistant actions require explicit confirmation before execution.");

        var context = await workspaceContextService.BuildAsync(request, ct);
        var validation = actionValidator.Validate(request.Proposal, context);

        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(" ", validation.Errors));

        var execution = await workspaceCommandExecutor.ExecuteAsync(request.Proposal, context, ct);
        var nextRequest = ApplyPatch(request, execution.Patch);
        var nextContext = await workspaceContextService.BuildAsync(nextRequest, ct);
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(nextContext, ct);
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(nextContext, signalSummary, ct);
        var observationBundle = observationService.Build(nextContext, signalSummary, comparisonSummary);
        var narrative = await responseComposer.GenerateNarrativeAsync(
            AiOperationKind.Explain,
            nextContext,
            signalSummary,
            comparisonSummary,
            observationBundle,
            "Summarize the result of the just-executed assistant action.",
            [],
            ct);

        return new AiResponseDto
        {
            Context = nextContext,
            ExecutionResult = execution,
            FollowUpPrompts = responseComposer.BuildFollowUps(AiOperationKind.Explain, narrative, observationBundle),
            Intent = "action",
            Limitations = observationBundle.Limitations,
            Message = narrative.Answer,
            Observations = observationBundle.Observations,
            Status = "ready",
            SummaryCard = responseComposer.BuildSummaryCard(AiOperationKind.Explain, nextContext, narrative, signalSummary, comparisonSummary, observationBundle),
            UsedFallback = narrative.UsedFallback,
            WorkspacePatch = execution.Patch
        };
    }

    private static AiWorkspaceRequestContextDto ApplyPatch(AiWorkspaceRequestContextDto request, WorkspaceStatePatchDto patch) =>
        new()
        {
            ActiveView = patch.ActiveView ?? request.ActiveView,
            CompareFileIds = patch.CompareFileIds ?? request.CompareFileIds,
            FileId = request.FileId,
            Selection = request.Selection,
            Transforms = patch.Transforms ?? request.Transforms,
            WorkspaceId = request.WorkspaceId
        };
}

internal sealed class RuleBasedAiIntentClassifier : IAiIntentClassifier
{
    public AiIntentResult Classify(AiRequestDto request, WorkspaceContextDto context)
    {
        var prompt = string.IsNullOrWhiteSpace(request.Prompt)
            ? "What stands out in this signal?"
            : request.Prompt.Trim();
        var normalized = prompt.ToLowerInvariant();
        var mentionsComparison =
            normalized.Contains("changed", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("difference", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("compare", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("versus", StringComparison.OrdinalIgnoreCase);
        var isImperativeAction =
            normalized.Contains("apply ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("switch ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("show ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("reset ", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("normalize", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("trim silence", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("gain", StringComparison.OrdinalIgnoreCase);

        if (mentionsComparison &&
            (!isImperativeAction || normalized.Contains("after ", StringComparison.OrdinalIgnoreCase)))
        {
            return new AiIntentResult
            {
                Confidence = context.CompareFileIds.Count > 0 ||
                             !string.Equals(context.Transforms.Filter.Mode, "none", StringComparison.OrdinalIgnoreCase)
                    ? 0.95d
                    : 0.75d,
                Intent = AiIntentType.Compare,
                NormalizedPrompt = prompt
            };
        }

        if (isImperativeAction ||
            normalized.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            return new AiIntentResult
            {
                Confidence = 0.95d,
                Intent = AiIntentType.Action,
                NormalizedPrompt = prompt
            };
        }

        if (normalized.Contains("next", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("inspect", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("recommend", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("should i", StringComparison.OrdinalIgnoreCase))
        {
            return new AiIntentResult
            {
                Confidence = 0.9d,
                Intent = AiIntentType.Recommend,
                NormalizedPrompt = prompt
            };
        }

        if (normalized.Contains("clarify", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("why ", StringComparison.OrdinalIgnoreCase))
        {
            return new AiIntentResult
            {
                Confidence = 0.8d,
                Intent = AiIntentType.Clarify,
                NormalizedPrompt = prompt
            };
        }

        return new AiIntentResult
        {
            Confidence = 0.9d,
            Intent = AiIntentType.Explain,
            NormalizedPrompt = prompt
        };
    }
}
