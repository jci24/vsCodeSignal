using System.Diagnostics;
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

    public string ImpactSummary { get; init; } = string.Empty;

    public string PrimaryFinding { get; init; } = string.Empty;

    public string RecommendedNextStep { get; init; } = string.Empty;

    public string? RecommendedView { get; init; }

    public bool UsedFallback { get; init; }
}

internal sealed class AiNarrativePayload
{
    public string Answer { get; init; } = string.Empty;

    public List<string> FollowUpPrompts { get; init; } = [];

    public string Headline { get; init; } = string.Empty;

    public string ImpactSummary { get; init; } = string.Empty;

    public string PrimaryFinding { get; init; } = string.Empty;

    public string RecommendedNextStep { get; init; } = string.Empty;

    public string? RecommendedView { get; init; }
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
            var providerStopwatch = Stopwatch.StartNew();

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

                logger.LogInformation(
                    "AI narrative generated via {ProviderKey} for {Operation} in {ElapsedMs} ms.",
                    providerKey,
                    operation,
                    providerStopwatch.ElapsedMilliseconds);

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
                    ImpactSummary = string.IsNullOrWhiteSpace(payload.ImpactSummary)
                        ? fallback.ImpactSummary
                        : payload.ImpactSummary.Trim(),
                    PrimaryFinding = string.IsNullOrWhiteSpace(payload.PrimaryFinding)
                        ? fallback.PrimaryFinding
                        : payload.PrimaryFinding.Trim(),
                    RecommendedNextStep = string.IsNullOrWhiteSpace(payload.RecommendedNextStep)
                        ? fallback.RecommendedNextStep
                        : payload.RecommendedNextStep.Trim(),
                    RecommendedView = NormalizeRecommendedView(payload.RecommendedView) ?? fallback.RecommendedView,
                    UsedFallback = false
                };
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "AI assistant provider {ProviderKey} failed for {Operation} after {ElapsedMs} ms: {Reason}",
                    providerKey,
                    operation,
                    providerStopwatch.ElapsedMilliseconds,
                    ex.Message);
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

    public AiNarrativeResult BuildGroundedNarrative(
        AiOperationKind operation,
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle) =>
        BuildFallbackNarrative(
            operation,
            context,
            signalSummary,
            comparisonSummary,
            observationBundle,
            usedFallback: false);

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
            ImpactSummary = narrative.ImpactSummary,
            KeyFacts = SelectKeyFacts(operation, context.ActiveView, signalSummary, comparisonSummary),
            Limitations = observationBundle.Limitations,
            Mode = comparisonSummary is not null && comparisonSummary.Comparisons.Count > 0 ? "comparison" : "single_signal",
            NextSteps = observationBundle.RecommendedActions,
            PrimaryFinding = narrative.PrimaryFinding,
            RecommendedNextStep = narrative.RecommendedNextStep,
            RecommendedView = narrative.RecommendedView,
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
        ObservationBundle observationBundle,
        bool usedFallback = true)
    {
        var headline = GetDefaultHeadline(operation, context.ActiveView);
        string answer;
        string primaryFinding;
        string impactSummary;

        switch (operation)
        {
            case AiOperationKind.Compare:
                answer = comparisonSummary is null || comparisonSummary.Comparisons.Count == 0
                    ? "No comparison files are currently selected, so I can only describe the active signal."
                    : BuildComparisonAnswer(context, comparisonSummary, observationBundle);
                primaryFinding = BuildComparisonPrimaryFinding(context, comparisonSummary, observationBundle);
                impactSummary = BuildComparisonImpactSummary(context, comparisonSummary, observationBundle);
                break;
            case AiOperationKind.Recommend:
                answer = BuildRecommendationAnswer(context, observationBundle);
                primaryFinding = BuildRecommendationPrimaryFinding(context, observationBundle);
                impactSummary = BuildRecommendationImpactSummary(context);
                break;
            default:
                answer = BuildExplainAnswer(context, signalSummary, observationBundle);
                primaryFinding = BuildExplainPrimaryFinding(context, signalSummary, observationBundle);
                impactSummary = BuildExplainImpactSummary(context, signalSummary, observationBundle);
                break;
        }

        return new AiNarrativeResult
        {
            Answer = answer,
            FollowUpPrompts = observationBundle.RecommendedActions,
            Headline = headline,
            ImpactSummary = impactSummary,
            PrimaryFinding = primaryFinding,
            RecommendedNextStep = observationBundle.RecommendedActions.FirstOrDefault() ?? "Inspect the current evidence and ask a narrower follow-up question.",
            RecommendedView = PickRecommendedView(context, observationBundle.Observations, comparisonSummary),
            UsedFallback = usedFallback
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

        if (context.IsSelectionApplied)
            sentences.Insert(0, $"This explanation is grounded in {context.SelectionScope}, not the full file.");

        if (sentences.Count == 0)
            return "The selected signal is ready for analysis, but there are not enough grounded findings yet to highlight a strong standout.";

        return string.Join(" ", sentences.Take(4));
    }

    private static string BuildComparisonAnswer(
        WorkspaceContextDto context,
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

            if (context.IsSelectionApplied)
                answer = $"Within {context.SelectionScope}, {LowercaseSentenceStart(answer)}";

            return answer;
        }

        var first = comparisonSummary.Comparisons[0];

        if (HasNoMaterialDifference(first))
        {
            var answer = $"{first.SourcePath} is closely matched to the selected file, with no strong measured difference standing out.";
            return context.IsSelectionApplied
                ? $"Within {context.SelectionScope}, {LowercaseSentenceStart(answer)}"
                : answer;
        }

        if (IsDurationLedDifference(first))
        {
            var answer = $"{first.SourcePath} differs in duration by {first.DurationDeltaSeconds:+0.0;-0.0} seconds versus the selected file.";
            return context.IsSelectionApplied
                ? $"Within {context.SelectionScope}, {LowercaseSentenceStart(answer)}"
                : answer;
        }

        var baseAnswer = string.Equals(first.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase)
            ? $"After the current transforms, RMS changed by {first.RmsDeltaDb:+0.0;-0.0} dB, sample peak changed by {first.PeakDeltaDbFs:+0.0;-0.0} dB, and spectral centroid shifted by {first.SpectralCentroidDeltaHz:+0.0;-0.0} Hz relative to the original signal."
            : $"{first.SourcePath} differs by {first.RmsDeltaDb:+0.0;-0.0} dB RMS, {first.PeakDeltaDbFs:+0.0;-0.0} dB in sample peak, and {first.SpectralCentroidDeltaHz:+0.0;-0.0} Hz in spectral centroid versus the selected file.";

        return context.IsSelectionApplied
            ? $"Within {context.SelectionScope}, {LowercaseSentenceStart(baseAnswer)}"
            : baseAnswer;
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

        return context.IsSelectionApplied
            ? $"Next best step for {context.SelectionScope}: {firstStep} This is useful because {reason}."
            : $"Next best step: {firstStep} This is useful because {reason}.";
    }

    private static string BuildExplainPrimaryFinding(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ObservationBundle observationBundle)
    {
        var standout = SelectExplainObservations(context.ActiveView, observationBundle.Observations).FirstOrDefault()?.Message;

        if (!string.IsNullOrWhiteSpace(standout))
            return standout;

        return context.ActiveView.ToLowerInvariant() switch
        {
            "fft" when signalSummary.DominantFrequencyHz > 0 =>
                $"The strongest spectral feature sits near {signalSummary.DominantFrequencyHz.ToString("0.0", CultureInfo.InvariantCulture)} Hz.",
            "spectrogram" =>
                "The current spectrogram view is ready, but it does not yet expose a strong standout.",
            _ => "The current signal is ready, but it does not yet expose a strong standout."
        };
    }

    private static string BuildExplainImpactSummary(
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ObservationBundle observationBundle)
    {
        if (signalSummary.SamplesOverFullScaleCount > 0)
            return "This matters because level/headroom problems can make other differences harder to trust until they are checked first.";

        if (context.ActiveView.Equals("fft", StringComparison.OrdinalIgnoreCase))
            return "This matters because the main spectral balance often tells you more than isolated peaks when you are comparing runs.";

        if (context.ActiveView.Equals("spectrogram", StringComparison.OrdinalIgnoreCase))
            return "This matters because stable versus time-varying energy changes can point you to the right section to inspect next.";

        var dynamics = observationBundle.Observations.FirstOrDefault(item =>
            item.Code is "WAVEFORM_DENSE_ENVELOPE" or "WAVEFORM_PRONOUNCED_PEAKS" or "DENSE_DYNAMICS");

        return dynamics is not null
            ? "This matters because the overall shape of the signal is more important here than a single isolated peak."
            : "This matters because the current signal has one dominant pattern worth checking before going deeper.";
    }

    private static string BuildComparisonPrimaryFinding(
        WorkspaceContextDto context,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle)
    {
        var comparisonObservation = observationBundle.Observations
            .Where(item => item.Code.StartsWith("TRANSFORM_", StringComparison.OrdinalIgnoreCase) ||
                           item.Code.StartsWith("COMPARE_", StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.Equals(item.Code, "TRANSFORMS_APPLY_TO_SELECTED_FILE_ONLY", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetComparisonPriority)
            .FirstOrDefault();

        if (comparisonObservation is not null)
            return comparisonObservation.Message;

        if (comparisonSummary is null || comparisonSummary.Comparisons.Count == 0)
            return "No comparison is active yet.";

        var first = comparisonSummary.Comparisons[0];
        var prefix = context.IsSelectionApplied ? $"Within {context.SelectionScope}, " : string.Empty;

        if (HasNoMaterialDifference(first))
            return $"{prefix}{first.SourcePath} is closely matched to the selected file, with no strong measured difference standing out.";

        if (IsDurationLedDifference(first))
            return $"{prefix}{first.SourcePath} differs in duration by {first.DurationDeltaSeconds:+0.0;-0.0} seconds versus the selected file.";

        return string.Equals(first.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase)
            ? $"{prefix}RMS changed by {first.RmsDeltaDb:+0.0;-0.0} dB relative to the original signal."
            : $"{prefix}{first.SourcePath} differs by {first.RmsDeltaDb:+0.0;-0.0} dB RMS versus the selected file.";
    }

    private static string BuildComparisonImpactSummary(
        WorkspaceContextDto context,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle)
    {
        if (observationBundle.Observations.Any(item => item.Code.Contains("NO_MATERIAL_DIFFERENCE", StringComparison.OrdinalIgnoreCase)))
            return "This matters because the runs are closely matched, so you likely only need deeper inspection if another part of the workflow still looks suspicious.";

        var persistentIssue = observationBundle.Observations.FirstOrDefault(item => item.Code == "PCM_OVER_FULL_SCALE");

        if (persistentIssue is not null)
            return "This matters because the decode is still over full scale, so level issues may be as important as the comparison itself.";

        if (comparisonSummary is null || comparisonSummary.Comparisons.Count == 0)
            return "This matters because there is no active comparison yet.";

        var first = comparisonSummary.Comparisons[0];

        if (HasNoMaterialDifference(first))
            return "This matters because the runs are closely matched, so you likely only need deeper inspection if another part of the workflow still looks suspicious.";

        if (Math.Abs(first.SpectralCentroidDeltaHz) >= 180d ||
            Math.Abs(first.DominantFrequencyDeltaHz) >= 150d ||
            Math.Abs(first.HighBandEnergyDelta) >= 0.08d ||
            Math.Abs(first.LowBandEnergyDelta) >= 0.08d)
        {
            return "This matters because the tonal balance shifted, so FFT is the best place to confirm what changed.";
        }

        if (Math.Abs(first.RmsDeltaDb) >= 1d || Math.Abs(first.PeakDeltaDbFs) >= 0.75d)
            return "This matters because the biggest change is level or headroom, which can dominate how the difference is perceived.";

        if (Math.Abs(first.DurationDeltaSeconds) >= 0.2d)
            return "This matters because timing or trimming changed, so waveform is the best place to confirm whether the content moved or was cut.";

        return context.IsSelectionApplied
            ? "This matters because the selected region changed in a measurable way, even if the full-file difference may be smaller."
            : "This matters because the current run differs enough from the baseline to justify a focused follow-up check.";
    }

    private static string BuildRecommendationPrimaryFinding(
        WorkspaceContextDto context,
        ObservationBundle observationBundle)
    {
        var next = observationBundle.RecommendedActions.FirstOrDefault();

        return string.IsNullOrWhiteSpace(next)
            ? "There is not a strong guided next step yet."
            : context.IsSelectionApplied
                ? $"The next best step for {context.SelectionScope} is clear."
                : "The next best step is clear from the current evidence.";
    }

    private static string BuildRecommendationImpactSummary(WorkspaceContextDto context) =>
        context.ActiveView.ToLowerInvariant() switch
        {
            "fft" => "This matters because FFT makes the main frequency difference easier to confirm than waveform alone.",
            "spectrogram" => "This matters because spectrogram view helps verify whether the change is steady or time-local.",
            _ => "This matters because one focused next check is more useful here than another broad summary."
        };

    private static string? PickRecommendedView(
        WorkspaceContextDto context,
        IReadOnlyList<ObservationDto> observations,
        ComparisonSummaryDto? comparisonSummary)
    {
        if (observations.Any(item => item.Code.Contains("NO_MATERIAL_DIFFERENCE", StringComparison.OrdinalIgnoreCase)) ||
            observations.Any(item => item.Code.Contains("DURATION_DELTA", StringComparison.OrdinalIgnoreCase)))
        {
            return "waveform";
        }

        if (observations.Any(item => item.Code.Contains("SPECTROGRAM", StringComparison.OrdinalIgnoreCase)))
            return "spectrogram";

        if (comparisonSummary is not null && comparisonSummary.Comparisons.Count > 0)
        {
            var first = comparisonSummary.Comparisons[0];

            if (HasNoMaterialDifference(first) || IsDurationLedDifference(first))
                return "waveform";

            if (Math.Abs(first.SpectralCentroidDeltaHz) >= 120d ||
                Math.Abs(first.DominantFrequencyDeltaHz) >= 100d ||
                Math.Abs(first.HighBandEnergyDelta) >= 0.08d ||
                Math.Abs(first.LowBandEnergyDelta) >= 0.08d)
            {
                return "fft";
            }
        }

        if (observations.Any(item => item.Code.StartsWith("FFT_", StringComparison.OrdinalIgnoreCase)) ||
            observations.Any(item => item.Code == "DOMINANT_FREQUENCY_PRESENT"))
        {
            return "fft";
        }

        return NormalizeRecommendedView(context.ActiveView) ?? "waveform";
    }

    private static bool HasNoMaterialDifference(ComparisonDeltaDto comparison) =>
        Math.Abs(comparison.RmsDeltaDb) < 0.25d &&
        Math.Abs(comparison.PeakDeltaDbFs) < 0.25d &&
        Math.Abs(comparison.DominantFrequencyDeltaHz) < 20d &&
        Math.Abs(comparison.SpectralCentroidDeltaHz) < 40d &&
        Math.Abs(comparison.LowBandEnergyDelta) < 0.03d &&
        Math.Abs(comparison.HighBandEnergyDelta) < 0.03d &&
        Math.Abs(comparison.DurationDeltaSeconds) < 0.1d;

    private static bool IsDurationLedDifference(ComparisonDeltaDto comparison) =>
        Math.Abs(comparison.DurationDeltaSeconds) >= 0.2d &&
        Math.Abs(comparison.RmsDeltaDb) < 1d &&
        Math.Abs(comparison.PeakDeltaDbFs) < 0.75d &&
        Math.Abs(comparison.DominantFrequencyDeltaHz) < 100d &&
        Math.Abs(comparison.SpectralCentroidDeltaHz) < 120d &&
        Math.Abs(comparison.LowBandEnergyDelta) < 0.08d &&
        Math.Abs(comparison.HighBandEnergyDelta) < 0.08d;

    private static string? NormalizeRecommendedView(string? view)
    {
        if (string.IsNullOrWhiteSpace(view))
            return null;

        var normalized = view.Trim().ToLowerInvariant();

        return normalized is "waveform" or "fft" or "spectrogram"
            ? normalized
            : null;
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

    private static string LowercaseSentenceStart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
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

        if (code.Contains("NO_MATERIAL_DIFFERENCE"))
            return "compare_match";

        if (code.Contains("HEADROOM") || code.Contains("OVER_FULL_SCALE") || code.Contains("CLIPPING"))
            return "headroom";

        if (code.Contains("NOISE_FLOOR") || code.Contains("HISS"))
            return "noise_floor";

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

        if (code.Contains("NO_MATERIAL_DIFFERENCE", StringComparison.OrdinalIgnoreCase))
            return 65;

        if (code.Contains("NOISE_FLOOR", StringComparison.OrdinalIgnoreCase))
            return 58;

        if (code.Contains("RMS_DELTA", StringComparison.OrdinalIgnoreCase))
            return 60;

        if (code.Contains("LEVEL_DELTA", StringComparison.OrdinalIgnoreCase))
            return 60;

        if (code.Contains("PEAK_DELTA", StringComparison.OrdinalIgnoreCase))
            return 55;

        if (code.Contains("SPECTRAL_CENTROID", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("DOMINANT_FREQUENCY", StringComparison.OrdinalIgnoreCase))
            return 50;

        if (code.Contains("LOW_BAND", StringComparison.OrdinalIgnoreCase) ||
            code.Contains("HIGH_BAND", StringComparison.OrdinalIgnoreCase))
            return 45;

        if (code.Contains("DURATION_DELTA", StringComparison.OrdinalIgnoreCase))
            return 42;

        return 20;
    }

    private static List<EvidenceItemDto> SelectComparisonFacts(ComparisonSummaryDto comparisonSummary)
    {
        var first = comparisonSummary.Comparisons[0];
        string[] preferredPatterns;

        if (string.Equals(first.ComparisonKind, "transform_baseline", StringComparison.OrdinalIgnoreCase))
        {
            preferredPatterns = Math.Abs(first.DurationDeltaSeconds) >= 0.2d
                ? ["TRANSFORM_BASELINE_RMS", "TRANSFORM_BASELINE_DURATION", "TRANSFORM_BASELINE_PEAK", "TRANSFORM_BASELINE_SPECTRAL_CENTROID", "TRANSFORM_BASELINE_LOW_BAND", "TRANSFORM_BASELINE_HIGH_BAND"]
                : ["TRANSFORM_BASELINE_RMS", "TRANSFORM_BASELINE_PEAK", "TRANSFORM_BASELINE_SPECTRAL_CENTROID", "TRANSFORM_BASELINE_LOW_BAND", "TRANSFORM_BASELINE_HIGH_BAND"];
        }
        else
        {
            preferredPatterns = Math.Abs(first.DurationDeltaSeconds) >= 0.2d
                ? ["COMPARE_DURATION_", "COMPARE_RMS_", "COMPARE_SPECTRAL_CENTROID_", "COMPARE_LOW_BAND_", "COMPARE_HIGH_BAND_"]
                : ["COMPARE_RMS_", "COMPARE_SPECTRAL_CENTROID_", "COMPARE_LOW_BAND_", "COMPARE_HIGH_BAND_", "COMPARE_DURATION_"];
        }

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
    AiResponseComposer responseComposer,
    ILogger<AiAssistantService> logger) : IAiAssistantService
{
    public async Task<WorkspaceContextDto> GetContextAsync(AiWorkspaceRequestContextDto request, CancellationToken ct) =>
        await workspaceContextService.BuildAsync(request, ct);

    public async Task<AiSummaryCardDto> SummaryAsync(AiSummaryRequestDto request, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var phaseStopwatch = Stopwatch.StartNew();
        var context = await workspaceContextService.BuildAsync(request, ct);
        var contextMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(context, ct);
        var signalSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(context, signalSummary, ct);
        var comparisonSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var observationBundle = observationService.Build(context, signalSummary, comparisonSummary);
        var observationMs = phaseStopwatch.ElapsedMilliseconds;
        var operation = comparisonSummary is not null && comparisonSummary.Comparisons.Count > 0
            ? AiOperationKind.Compare
            : AiOperationKind.Summary;
        var narrative = responseComposer.BuildGroundedNarrative(
            operation,
            context,
            signalSummary,
            comparisonSummary,
            observationBundle);

        var summaryCard = responseComposer.BuildSummaryCard(operation, context, narrative, signalSummary, comparisonSummary, observationBundle);

        logger.LogInformation(
            "AI summary completed in {TotalMs} ms (context={ContextMs} ms, signal={SignalMs} ms, comparison={ComparisonMs} ms, observations={ObservationMs} ms, operation={Operation}).",
            totalStopwatch.ElapsedMilliseconds,
            contextMs,
            signalSummaryMs,
            comparisonSummaryMs,
            observationMs,
            operation);

        return summaryCard;
    }

    public async Task<AiResponseDto> AskAsync(AiRequestDto request, CancellationToken ct)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var phaseStopwatch = Stopwatch.StartNew();
        var context = await workspaceContextService.BuildAsync(request, ct);
        var contextMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(context, ct);
        var signalSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(context, signalSummary, ct);
        var comparisonSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var observationBundle = observationService.Build(context, signalSummary, comparisonSummary);
        var observationMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var response = await aiOrchestrator.ProcessAsync(request, context, signalSummary, comparisonSummary, observationBundle, ct);
        var narrativeMs = phaseStopwatch.ElapsedMilliseconds;

        logger.LogInformation(
            "AI ask completed in {TotalMs} ms (context={ContextMs} ms, signal={SignalMs} ms, comparison={ComparisonMs} ms, observations={ObservationMs} ms, narrative={NarrativeMs} ms).",
            totalStopwatch.ElapsedMilliseconds,
            contextMs,
            signalSummaryMs,
            comparisonSummaryMs,
            observationMs,
            narrativeMs);

        return response;
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

        var totalStopwatch = Stopwatch.StartNew();
        var phaseStopwatch = Stopwatch.StartNew();
        var context = await workspaceContextService.BuildAsync(request, ct);
        var contextMs = phaseStopwatch.ElapsedMilliseconds;
        var validation = actionValidator.Validate(request.Proposal, context);

        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(" ", validation.Errors));

        phaseStopwatch.Restart();
        var execution = await workspaceCommandExecutor.ExecuteAsync(request.Proposal, context, ct);
        var executionMs = phaseStopwatch.ElapsedMilliseconds;
        var nextRequest = ApplyPatch(request, execution.Patch);
        phaseStopwatch.Restart();
        var nextContext = await workspaceContextService.BuildAsync(nextRequest, ct);
        var nextContextMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var signalSummary = await signalAnalysisService.GetSignalSummaryAsync(nextContext, ct);
        var signalSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var comparisonSummary = await signalAnalysisService.GetComparisonSummaryAsync(nextContext, signalSummary, ct);
        var comparisonSummaryMs = phaseStopwatch.ElapsedMilliseconds;
        phaseStopwatch.Restart();
        var observationBundle = observationService.Build(nextContext, signalSummary, comparisonSummary);
        var observationMs = phaseStopwatch.ElapsedMilliseconds;
        var narrative = responseComposer.BuildGroundedNarrative(
            AiOperationKind.Explain,
            nextContext,
            signalSummary,
            comparisonSummary,
            observationBundle);

        logger.LogInformation(
            "AI execute-action completed in {TotalMs} ms (context={ContextMs} ms, execute={ExecuteMs} ms, nextContext={NextContextMs} ms, signal={SignalMs} ms, comparison={ComparisonMs} ms, observations={ObservationMs} ms).",
            totalStopwatch.ElapsedMilliseconds,
            contextMs,
            executionMs,
            nextContextMs,
            signalSummaryMs,
            comparisonSummaryMs,
            observationMs);

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
            UsedFallback = false,
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
