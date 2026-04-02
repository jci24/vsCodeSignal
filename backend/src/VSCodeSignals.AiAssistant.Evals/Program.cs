using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.AiAssistant.Handlers;
using VSCodeSignals.Api.Shared.SignalAnalysis;

var failures = new List<string>();

Run("Intent explain routes correctly", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var result = classifier.Classify(
        new AiRequestDto { Prompt = "What stands out in this signal?" },
        CreateContext(activeView: "waveform"));

    return result.Intent == AiIntentType.Explain;
});

Run("Simple compare ask stays on local grounded path", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var request = new AiRequestDto
    {
        Prompt = "What changed between these two signals?"
    };
    var context = CreateContext(activeView: "waveform");
    var intent = classifier.Classify(request, context);

    return AiOrchestrator.ShouldHandleAskLocally(request, intent);
});

Run("Rewrite-style ask stays on LLM path", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var request = new AiRequestDto
    {
        Prompt = "Rewrite this explanation for a client update."
    };
    var context = CreateContext(activeView: "waveform");
    var intent = classifier.Classify(request, context);

    return !AiOrchestrator.ShouldHandleAskLocally(request, intent);
});

Run("Multi-turn ask stays on LLM path", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var request = new AiRequestDto
    {
        Prompt = "Rewrite this for a client update.",
        History =
        [
            new AiConversationTurnDto
            {
                Content = "What changed between these two signals?",
                Role = "user"
            }
        ]
    };
    var context = CreateContext(activeView: "waveform");
    var intent = classifier.Classify(request, context);

    return !AiOrchestrator.ShouldHandleAskLocally(request, intent);
});

Run("Simple follow-up ask with history stays on local grounded path", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var request = new AiRequestDto
    {
        Prompt = "What changed?",
        History =
        [
            new AiConversationTurnDto
            {
                Content = "Explain this comparison.",
                Role = "user"
            },
            new AiConversationTurnDto
            {
                Content = "The candidate is quieter than the baseline.",
                Role = "assistant"
            }
        ]
    };
    var context = CreateContext(activeView: "waveform");
    var intent = classifier.Classify(request, context);

    return AiOrchestrator.ShouldHandleAskLocally(request, intent);
});

Run("Intent compare routes transform question to compare", () =>
{
    var classifier = new RuleBasedAiIntentClassifier();
    var result = classifier.Classify(
        new AiRequestDto
        {
            Prompt = "What changed after the filter?",
            Transforms = CreateTransforms(mode: "highpass", cutoffHz: 80d)
        },
        CreateContext(activeView: "fft", transforms: CreateTransforms(mode: "highpass", cutoffHz: 80d)));

    return result.Intent == AiIntentType.Compare;
});

await RunAsync("Action planner accepts spaced high pass prompt", async () =>
{
    var planner = CreateActionPlanner(enableLlm: false);
    var proposal = await planner.PlanAsync(
        new AiPlanActionRequestDto
        {
            Prompt = "apply high pass filter",
            FileId = "file-a",
            WorkspaceId = "current"
        },
        CreateContext(activeView: "fft"),
        CancellationToken.None);

    return proposal.Status == "needs_confirmation" &&
           proposal.Steps.Count == 1 &&
           proposal.Steps[0].Command == AiAssistantCommandCatalog.ApplyFilter &&
           proposal.Steps[0].FilterMode == "highpass" &&
           Math.Abs((proposal.Steps[0].CutoffHz ?? 0d) - 80d) < 0.01d;
});

await RunAsync("Action planner marks 3D graph as unsupported", async () =>
{
    var planner = CreateActionPlanner(enableLlm: false);
    var proposal = await planner.PlanAsync(
        new AiPlanActionRequestDto
        {
            Prompt = "create a 3D graph",
            FileId = "file-a",
            WorkspaceId = "current"
        },
        CreateContext(activeView: "spectrogram"),
        CancellationToken.None);

    return proposal.Status == "unsupported" &&
           proposal.UnsupportedReason.Contains("cannot create 3D graphs", StringComparison.OrdinalIgnoreCase);
});

Run("Observation service emits FFT view observations", () =>
{
    var service = new ObservationService();
    var bundle = service.Build(
        CreateContext(activeView: "fft"),
        CreateSignalSummary(
            dominantBand: "low",
            dominantFrequencyHz: 107.7d,
            spectralCentroidHz: 420.0d,
            crestFactorDb: 15.0d,
            highBandRatio: 0.14d,
            lowBandRatio: 0.58d,
            midBandRatio: 0.28d,
            samplesOverFullScaleCount: 12),
        null);

    return bundle.Observations.Any(item => item.Code == "FFT_DOMINANT_PEAK_FOCUS") &&
           bundle.Observations.Any(item => item.Code == "FFT_BAND_BALANCE");
});

Run("Observation service emits spectrogram view observations", () =>
{
    var service = new ObservationService();
    var bundle = service.Build(
        CreateContext(activeView: "spectrogram"),
        CreateSignalSummary(
            dominantBand: "mid",
            dominantFrequencyHz: 640.0d,
            spectrogramBand: "mid",
            spectrogramVariation: 0.44d),
        null);

    return bundle.Observations.Any(item => item.Code == "SPECTROGRAM_BAND_FOCUS") &&
           bundle.Observations.Any(item => item.Code == "SPECTROGRAM_TEMPORAL_VARIATION_HIGH");
});

Run("Selection-scoped observations update the limitation text", () =>
{
    var service = new ObservationService();
    var bundle = service.Build(
        CreateContext(
            activeView: "waveform",
            selection: new SelectionRangeDto
            {
                StartSeconds = 12.5d,
                EndSeconds = 18.0d
            }),
        CreateSignalSummary(samplesOverFullScaleCount: 128),
        null);

    return bundle.Limitations.Any(item => item.Contains("selected region 12.50s to 18.00s", StringComparison.OrdinalIgnoreCase)) &&
           bundle.Limitations.All(item => !item.Contains("not yet applied to DSP analysis", StringComparison.OrdinalIgnoreCase));
});

Run("Prompt builder includes concise guidance and supported next steps", () =>
{
    var promptBuilder = new AiPromptBuilder();
    var context = CreateContext(activeView: "fft");
    var signalSummary = CreateSignalSummary(
        dominantBand: "low",
        dominantFrequencyHz: 107.7d,
        spectralCentroidHz: 420.0d,
        highBandRatio: 0.14d,
        lowBandRatio: 0.58d,
        midBandRatio: 0.28d);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var prompt = promptBuilder.BuildExplanationPrompt(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "What stands out in this FFT?",
        []);

    return prompt.UserPrompt.Contains("Do not repeat the same issue in multiple phrasings.", StringComparison.OrdinalIgnoreCase) &&
           prompt.UserPrompt.Contains("Prefer percentages or coverage wording when explaining over-full-scale behavior.", StringComparison.OrdinalIgnoreCase) &&
           prompt.UserPrompt.Contains("Keep the answer to 2-4 sentences total.", StringComparison.OrdinalIgnoreCase) &&
           prompt.UserPrompt.Contains("Supported next steps:", StringComparison.OrdinalIgnoreCase);
});

await RunAsync("Fallback narrative for FFT uses view-aware language", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "fft");
    var signalSummary = CreateSignalSummary(
        dominantBand: "low",
        dominantFrequencyHz: 107.7d,
        spectralCentroidHz: 420.0d,
        highBandRatio: 0.14d,
        lowBandRatio: 0.58d,
        midBandRatio: 0.28d,
        samplesOverFullScaleCount: 12);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "What stands out in this FFT?",
        [],
        CancellationToken.None);

    return narrative.UsedFallback &&
           narrative.Answer.Contains("FFT view", StringComparison.OrdinalIgnoreCase) &&
           narrative.Answer.Contains("dominant FFT bin", StringComparison.OrdinalIgnoreCase);
});

await RunAsync("Fallback waveform narrative uses coverage instead of raw sample count", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "waveform");
    var signalSummary = CreateSignalSummary(
        dominantBand: "mid",
        dominantFrequencyHz: 209.9d,
        crestFactorDb: 12.6d,
        samplesOverFullScaleCount: 101_644);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "Explain this signal",
        [],
        CancellationToken.None);

    return narrative.Answer.StartsWith("In waveform view, headroom is the main issue", StringComparison.OrdinalIgnoreCase) &&
           narrative.Answer.Contains("decode is above full scale", StringComparison.OrdinalIgnoreCase) &&
           narrative.Answer.Contains("% of analyzed points", StringComparison.OrdinalIgnoreCase) &&
           !narrative.Answer.Contains("samples above 0 dBFS", StringComparison.OrdinalIgnoreCase) &&
           !narrative.Answer.StartsWith("101,644", StringComparison.OrdinalIgnoreCase);
});

await RunAsync("Selected-region fallback narrative makes scope explicit", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(
        activeView: "waveform",
        selection: new SelectionRangeDto
        {
            StartSeconds = 4.0d,
            EndSeconds = 9.5d
        });
    var signalSummary = CreateSignalSummary(
        dominantBand: "mid",
        dominantFrequencyHz: 209.9d,
        crestFactorDb: 12.6d,
        samplesOverFullScaleCount: 320);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "What stands out in this section?",
        [],
        CancellationToken.None);

    return narrative.Answer.Contains("selected region 4.00s to 9.50s", StringComparison.OrdinalIgnoreCase);
});

await RunAsync("Comparison summary card prioritizes comparison facts", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "fft", transforms: CreateTransforms(mode: "highpass", cutoffHz: 80d));
    var signalSummary = CreateSignalSummary(
        dominantBand: "mid",
        dominantFrequencyHz: 320.0d,
        spectralCentroidHz: 920.0d,
        highBandRatio: 0.26d,
        lowBandRatio: 0.22d,
        midBandRatio: 0.52d);
    var comparison = new ComparisonSummaryDto
    {
        PrimaryFileId = signalSummary.FileId,
        Comparisons =
        [
            new ComparisonDeltaDto
            {
                ComparisonKind = "transform_baseline",
                FileId = signalSummary.FileId,
                SourcePath = signalSummary.SourcePath,
                RmsDeltaDb = -1.4d,
                PeakDeltaDbFs = -0.8d,
                SpectralCentroidDeltaHz = 240.0d,
                LowBandEnergyDelta = -0.11d,
                HighBandEnergyDelta = 0.12d,
                Facts =
                [
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_RMS", Label = "RMS change", ValueText = "-1.4 dB" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_PEAK", Label = "Peak change", ValueText = "-0.8 dB" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_SPECTRAL_CENTROID", Label = "Spectral centroid change", ValueText = "+240.0 Hz" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_HIGH_BAND", Label = "High-band change", ValueText = "+12.0 pts" }
                ]
            }
        ]
    };
    var bundle = new ObservationService().Build(context, signalSummary, comparison);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Compare,
        context,
        signalSummary,
        comparison,
        bundle,
        "What changed after the filter?",
        [],
        CancellationToken.None);
    var card = composer.BuildSummaryCard(
        AiOperationKind.Compare,
        context,
        narrative,
        signalSummary,
        comparison,
        bundle);

    return card.Title == "Comparison summary" &&
           card.Mode == "comparison" &&
           card.KeyFacts.Any(item => item.Code == "TRANSFORM_BASELINE_RMS") &&
           !string.IsNullOrWhiteSpace(card.PrimaryFinding) &&
           !string.IsNullOrWhiteSpace(card.ImpactSummary) &&
           !string.IsNullOrWhiteSpace(card.RecommendedNextStep) &&
           card.RecommendedView == "fft" &&
           card.TopObservations.All(item => item.Code.StartsWith("TRANSFORM_", StringComparison.OrdinalIgnoreCase));
});

await RunAsync("Transform comparison fallback keeps the before-after change and remaining issue", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "waveform", transforms: CreateTransforms(mode: "highpass", cutoffHz: 80d));
    var signalSummary = CreateSignalSummary(
        dominantBand: "mid",
        dominantFrequencyHz: 320.0d,
        spectralCentroidHz: 920.0d,
        highBandRatio: 0.26d,
        lowBandRatio: 0.22d,
        midBandRatio: 0.52d,
        samplesOverFullScaleCount: 500);
    var comparison = new ComparisonSummaryDto
    {
        PrimaryFileId = signalSummary.FileId,
        Comparisons =
        [
            new ComparisonDeltaDto
            {
                ComparisonKind = "transform_baseline",
                FileId = signalSummary.FileId,
                SourcePath = signalSummary.SourcePath,
                RmsDeltaDb = -1.4d,
                PeakDeltaDbFs = -0.8d,
                SpectralCentroidDeltaHz = 240.0d,
                LowBandEnergyDelta = -0.11d,
                HighBandEnergyDelta = 0.12d,
                Facts =
                [
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_RMS", Label = "RMS change", ValueText = "-1.4 dB" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_PEAK", Label = "Peak change", ValueText = "-0.8 dB" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_SPECTRAL_CENTROID", Label = "Spectral centroid change", ValueText = "+240.0 Hz" },
                    new EvidenceItemDto { Code = "TRANSFORM_BASELINE_HIGH_BAND", Label = "High-band change", ValueText = "+12.0 pts" }
                ]
            }
        ]
    };
    var bundle = new ObservationService().Build(context, signalSummary, comparison);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Compare,
        context,
        signalSummary,
        comparison,
        bundle,
        "What changed after the filter?",
        [],
        CancellationToken.None);

    return narrative.Answer.Contains("RMS changed by -1.4 dB", StringComparison.OrdinalIgnoreCase) &&
           narrative.Answer.Contains("% of analyzed points", StringComparison.OrdinalIgnoreCase);
});

await RunAsync("FFT summary card omits unavailable facts", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "fft");
    var signalSummary = CreateSignalSummary(
        dominantBand: "unavailable",
        dominantFrequencyHz: 107.7d,
        spectralCentroidHz: 0d,
        highBandRatio: 0.14d,
        lowBandRatio: 0.58d,
        midBandRatio: 0.28d);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "What stands out in this FFT?",
        [],
        CancellationToken.None);
    var card = composer.BuildSummaryCard(
        AiOperationKind.Explain,
        context,
        narrative,
        signalSummary,
        null,
        bundle);

    return card.KeyFacts.All(item => !item.ValueText.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
});

await RunAsync("Waveform summary card prefers over-full-scale coverage fact", async () =>
{
    var composer = CreateResponseComposer(enableLlm: false);
    var context = CreateContext(activeView: "waveform");
    var signalSummary = CreateSignalSummary(
        dominantBand: "mid",
        dominantFrequencyHz: 209.9d,
        crestFactorDb: 12.6d,
        samplesOverFullScaleCount: 101_644);
    var bundle = new ObservationService().Build(context, signalSummary, null);
    var narrative = await composer.GenerateNarrativeAsync(
        AiOperationKind.Explain,
        context,
        signalSummary,
        null,
        bundle,
        "Explain this signal",
        [],
        CancellationToken.None);
    var card = composer.BuildSummaryCard(
        AiOperationKind.Explain,
        context,
        narrative,
        signalSummary,
        null,
        bundle);

    return card.KeyFacts.Any(item => item.Code == "OVER_FULL_SCALE_SHARE") &&
           card.Mode == "single_signal" &&
           !string.IsNullOrWhiteSpace(card.PrimaryFinding) &&
           !string.IsNullOrWhiteSpace(card.ImpactSummary) &&
           !string.IsNullOrWhiteSpace(card.RecommendedNextStep) &&
           card.KeyFacts.All(item => item.Code != "SAMPLES_OVER_FULL_SCALE");
});

Run("Model routing can prefer Ollama directly", () =>
{
    var routing = new ModelRoutingService(
        Options.Create(new AiAssistantOptions
        {
            DefaultProvider = "ollama",
            EnableLocalFallback = true,
            EnableLlm = true
        }),
        Options.Create(new OpenAiOptions()));

    var route = routing.Resolve(AiOperationKind.Explain, "Explain this FFT");
    return route.ProviderKey == "ollama";
});

await RunGroundTruthScenariosAsync();

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("AI assistant regression suite failed:");

    foreach (var failure in failures)
        Console.Error.WriteLine($"- {failure}");

    return 1;
}

Console.WriteLine();
Console.WriteLine("AI assistant regression suite passed.");
return 0;

void Run(string name, Func<bool> assertion)
{
    try
    {
        var passed = assertion();
        WriteResult(name, passed);
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL  {name}");
    }
}

async Task RunAsync(string name, Func<Task<bool>> assertion)
{
    try
    {
        var passed = await assertion();
        WriteResult(name, passed);
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL  {name}");
    }
}

void WriteResult(string name, bool passed)
{
    if (passed)
    {
        Console.WriteLine($"PASS  {name}");
        return;
    }

    failures.Add($"{name}: assertion returned false");
    Console.WriteLine($"FAIL  {name}");
}

async Task RunGroundTruthScenariosAsync()
{
    foreach (var scenario in LoadGroundTruthScenarios())
    {
        var displayName = string.IsNullOrWhiteSpace(scenario.Label)
            ? scenario.Id
            : $"{scenario.Id} ({scenario.Label})";

        await RunAsync($"Ground truth {displayName}", async () =>
        {
            var composer = CreateResponseComposer(enableLlm: false);
            var operation = ParseOperation(scenario.Operation);
            var context = CreateGroundTruthContext(scenario);
            var signalSummary = CreateGroundTruthSignalSummary(scenario.Signal);
            var comparisonSummary = CreateComparisonSummary(scenario.Comparison, signalSummary);
            var bundle = new ObservationService().Build(context, signalSummary, comparisonSummary);
            var narrative = await composer.GenerateNarrativeAsync(
                operation,
                context,
                signalSummary,
                comparisonSummary,
                bundle,
                scenario.Prompt,
                [],
                CancellationToken.None);
            var card = composer.BuildSummaryCard(
                operation,
                context,
                narrative,
                signalSummary,
                comparisonSummary,
                bundle);

            AssertGroundTruthScenario(scenario, narrative, card);
            return true;
        });
    }
}

static WorkspaceContextDto CreateContext(
    string activeView,
    SignalTransformRecipe? transforms = null,
    SelectionRangeDto? selection = null) =>
    new()
    {
        ActiveView = activeView,
        IsSelectionApplied = selection is not null,
        Selection = selection,
        SelectionScope = selection is null
            ? "full-file"
            : $"selected region {selection.StartSeconds?.ToString("0.00", CultureInfo.InvariantCulture)}s to {selection.EndSeconds?.ToString("0.00", CultureInfo.InvariantCulture)}s",
        SelectedFileId = "file-a",
        SelectedFile = new WorkspaceFileReferenceDto
        {
            FileId = "file-a",
            SignalKind = "audio",
            SourcePath = "sample.wav"
        },
        SupportedCommands = AiAssistantCommandCatalog.All.ToList(),
        Transforms = transforms ?? CreateTransforms()
    };

static WorkspaceContextDto CreateGroundTruthContext(GroundTruthScenario scenario)
{
    SelectionRangeDto? selection = null;

    if (scenario.SelectionStartSeconds is not null || scenario.SelectionEndSeconds is not null)
    {
        selection = new SelectionRangeDto
        {
            StartSeconds = scenario.SelectionStartSeconds,
            EndSeconds = scenario.SelectionEndSeconds
        };
    }

    return CreateContext(
        activeView: scenario.ActiveView,
        transforms: CreateTransforms(
            mode: scenario.TransformMode ?? "none",
            cutoffHz: scenario.TransformCutoffHz ?? 1200d),
        selection: selection);
}

static SignalSummaryDto CreateSignalSummary(
    string dominantBand = "mixed",
    double dominantFrequencyHz = 220.0d,
    double spectralCentroidHz = 0d,
    double crestFactorDb = 10d,
    double highBandRatio = 0.2d,
    double lowBandRatio = 0.2d,
    double midBandRatio = 0.6d,
    int samplesOverFullScaleCount = 0,
    string spectrogramBand = "mixed",
    double spectrogramVariation = 0.18d) =>
    new()
    {
        CrestFactorDb = crestFactorDb,
        DominantEnergyBand = dominantBand,
        DominantFrequencyHz = dominantFrequencyHz,
        DominantMagnitudeDb = -16.0d,
        DurationSeconds = 180.0d,
        Facts =
        [
            new EvidenceItemDto { Code = "SAMPLE_PEAK_DBFS", Label = "Decoded sample peak", ValueText = "+1.0 dBFS" },
            new EvidenceItemDto { Code = "SAMPLES_OVER_FULL_SCALE", Label = "Samples over 0 dBFS", ValueText = samplesOverFullScaleCount.ToString() },
            new EvidenceItemDto { Code = "OVER_FULL_SCALE_SHARE", Label = "Over-full-scale coverage", ValueText = $"{(samplesOverFullScaleCount / Math.Max(1d, Math.Round(180d * 22050d)) * 100d):0.0}% of analyzed points" },
            new EvidenceItemDto { Code = "RMS_DBFS", Label = "RMS level", ValueText = "-12.0 dBFS" },
            new EvidenceItemDto { Code = "CREST_FACTOR", Label = "Crest factor", ValueText = $"6.00x ({crestFactorDb:+0.0;-0.0} dB)" },
            new EvidenceItemDto { Code = "DOMINANT_FREQUENCY", Label = "Dominant FFT bin", ValueText = $"{dominantFrequencyHz:0.0} Hz at -16.0 dB" },
            new EvidenceItemDto { Code = "SPECTRAL_CENTROID", Label = "Spectral centroid", ValueText = spectralCentroidHz > 0 ? $"{spectralCentroidHz:0.0} Hz" : "Unavailable" },
            new EvidenceItemDto { Code = "LOW_BAND_ENERGY_SHARE", Label = "Low-band energy share", ValueText = $"{lowBandRatio * 100d:0.0}%" },
            new EvidenceItemDto { Code = "MID_BAND_ENERGY_SHARE", Label = "Mid-band energy share", ValueText = $"{midBandRatio * 100d:0.0}%" },
            new EvidenceItemDto { Code = "HIGH_BAND_ENERGY_SHARE", Label = "High-band energy share", ValueText = $"{highBandRatio * 100d:0.0}%" },
            new EvidenceItemDto { Code = "DOMINANT_ENERGY_BAND", Label = "Dominant energy band", ValueText = dominantBand },
            new EvidenceItemDto { Code = "SPECTROGRAM_DOMINANT_BAND", Label = "Dominant spectrogram band", ValueText = spectrogramBand },
            new EvidenceItemDto { Code = "SPECTROGRAM_TEMPORAL_VARIATION", Label = "Temporal energy variation", ValueText = spectrogramVariation.ToString("0.00") }
        ],
        FileId = "file-a",
        HighBandEnergyRatio = highBandRatio,
        LowBandEnergyRatio = lowBandRatio,
        MidBandEnergyRatio = midBandRatio,
        Peak = 1.12d,
        Rms = 0.25d,
        SampleRateHz = 22050,
        SamplesOverFullScaleCount = samplesOverFullScaleCount,
        SourcePath = "sample.wav",
        SpectralCentroidHz = spectralCentroidHz,
        SpectrogramDominantBand = spectrogramBand,
        SpectrogramTemporalVariation = spectrogramVariation
    };

static SignalSummaryDto CreateGroundTruthSignalSummary(GroundTruthSignalSpec signal) =>
    CreateSignalSummary(
        dominantBand: signal.DominantBand,
        dominantFrequencyHz: signal.DominantFrequencyHz,
        spectralCentroidHz: signal.SpectralCentroidHz,
        crestFactorDb: signal.CrestFactorDb,
        highBandRatio: signal.HighBandRatio,
        lowBandRatio: signal.LowBandRatio,
        midBandRatio: signal.MidBandRatio,
        samplesOverFullScaleCount: signal.SamplesOverFullScaleCount,
        spectrogramBand: signal.SpectrogramBand,
        spectrogramVariation: signal.SpectrogramVariation);

static ComparisonSummaryDto? CreateComparisonSummary(
    GroundTruthComparisonSpec? comparison,
    SignalSummaryDto signalSummary)
{
    if (comparison is null)
        return null;

    return new ComparisonSummaryDto
    {
        PrimaryFileId = signalSummary.FileId,
        CompareFileIds = [comparison.FileId],
        Comparisons =
        [
            new ComparisonDeltaDto
            {
                ComparisonKind = comparison.Kind,
                DurationDeltaSeconds = comparison.DurationDeltaSeconds,
                DominantFrequencyDeltaHz = comparison.DominantFrequencyDeltaHz,
                Facts = BuildComparisonFacts(comparison),
                FileId = comparison.FileId,
                HighBandEnergyDelta = comparison.HighBandEnergyDelta,
                LowBandEnergyDelta = comparison.LowBandEnergyDelta,
                PeakDeltaDbFs = comparison.PeakDeltaDbFs,
                RmsDeltaDb = comparison.RmsDeltaDb,
                SourcePath = comparison.SourcePath,
                SpectralCentroidDeltaHz = comparison.SpectralCentroidDeltaHz
            }
        ]
    };
}

static List<EvidenceItemDto> BuildComparisonFacts(GroundTruthComparisonSpec comparison)
{
    if (string.Equals(comparison.Kind, "transform_baseline", StringComparison.OrdinalIgnoreCase))
    {
        return
        [
            new EvidenceItemDto { Code = "TRANSFORM_BASELINE_RMS", Label = "RMS change", ValueText = $"{comparison.RmsDeltaDb:+0.0;-0.0} dB" },
            new EvidenceItemDto { Code = "TRANSFORM_BASELINE_PEAK", Label = "Peak change", ValueText = $"{comparison.PeakDeltaDbFs:+0.0;-0.0} dB" },
            new EvidenceItemDto { Code = "TRANSFORM_BASELINE_SPECTRAL_CENTROID", Label = "Spectral centroid change", ValueText = $"{comparison.SpectralCentroidDeltaHz:+0.0;-0.0} Hz" },
            new EvidenceItemDto { Code = "TRANSFORM_BASELINE_LOW_BAND", Label = "Low-band change", ValueText = $"{comparison.LowBandEnergyDelta * 100d:+0.0;-0.0} pts" },
            new EvidenceItemDto { Code = "TRANSFORM_BASELINE_HIGH_BAND", Label = "High-band change", ValueText = $"{comparison.HighBandEnergyDelta * 100d:+0.0;-0.0} pts" }
        ];
    }

    return
    [
        new EvidenceItemDto { Code = $"COMPARE_RMS_{comparison.FileId}", Label = "RMS change", ValueText = $"{comparison.RmsDeltaDb:+0.0;-0.0} dB" },
        new EvidenceItemDto { Code = $"COMPARE_PEAK_{comparison.FileId}", Label = "Peak change", ValueText = $"{comparison.PeakDeltaDbFs:+0.0;-0.0} dB" },
        new EvidenceItemDto { Code = $"COMPARE_SPECTRAL_CENTROID_{comparison.FileId}", Label = "Spectral centroid change", ValueText = $"{comparison.SpectralCentroidDeltaHz:+0.0;-0.0} Hz" },
        new EvidenceItemDto { Code = $"COMPARE_LOW_BAND_{comparison.FileId}", Label = "Low-band change", ValueText = $"{comparison.LowBandEnergyDelta * 100d:+0.0;-0.0} pts" },
        new EvidenceItemDto { Code = $"COMPARE_HIGH_BAND_{comparison.FileId}", Label = "High-band change", ValueText = $"{comparison.HighBandEnergyDelta * 100d:+0.0;-0.0} pts" },
        new EvidenceItemDto { Code = $"COMPARE_DURATION_{comparison.FileId}", Label = "Duration change", ValueText = $"{comparison.DurationDeltaSeconds:+0.0;-0.0} s" }
    ];
}

static void AssertGroundTruthScenario(
    GroundTruthScenario scenario,
    AiNarrativeResult narrative,
    AiSummaryCardDto card)
{
    var issues = new List<string>();

    if (!string.Equals(card.Mode, scenario.Expected.Mode, StringComparison.OrdinalIgnoreCase))
        issues.Add($"Expected summary mode '{scenario.Expected.Mode}' but found '{card.Mode}'.");

    if (!string.Equals(card.RecommendedView ?? string.Empty, scenario.Expected.RecommendedView ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        issues.Add($"Expected recommended view '{scenario.Expected.RecommendedView}' but found '{card.RecommendedView ?? "null"}'.");

    foreach (var phrase in scenario.Expected.RequiredAnswerPhrases)
    {
        if (!narrative.Answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            issues.Add($"Answer did not include required phrase '{phrase}'.");
    }

    foreach (var phrase in scenario.Expected.RequiredPrimaryFindingPhrases)
    {
        if (!card.PrimaryFinding.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            issues.Add($"Primary finding did not include required phrase '{phrase}'.");
    }

    foreach (var phrase in scenario.Expected.RequiredImpactPhrases)
    {
        if (!card.ImpactSummary.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            issues.Add($"Impact summary did not include required phrase '{phrase}'.");
    }

    foreach (var phrase in scenario.Expected.ForbiddenAnswerPhrases)
    {
        if (narrative.Answer.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            issues.Add($"Answer contained forbidden phrase '{phrase}'.");
    }

    foreach (var code in scenario.Expected.RequiredFactCodes)
    {
        if (!card.KeyFacts.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
            issues.Add($"Summary card did not include required fact code '{code}'.");
    }

    if (issues.Count == 0)
    {
        return;
    }

    throw new InvalidOperationException(string.Join(
        Environment.NewLine,
        new[]
        {
            $"Scenario '{scenario.Id}' failed.",
            string.IsNullOrWhiteSpace(scenario.Source) ? string.Empty : $"Source: {scenario.Source}",
            scenario.Artifacts is null ? string.Empty : $"Artifacts: {scenario.Artifacts}",
            $"Prompt: {scenario.Prompt}",
            $"Expected recommended view: {scenario.Expected.RecommendedView ?? "null"}",
            $"Actual recommended view: {card.RecommendedView ?? "null"}",
            $"Primary finding: {card.PrimaryFinding}",
            $"Impact summary: {card.ImpactSummary}",
            $"Answer: {narrative.Answer}",
            $"Key facts: {string.Join(", ", card.KeyFacts.Select(item => item.Code))}",
            "Issues:",
            string.Join(Environment.NewLine, issues.Select(item => $"- {item}"))
        }.Where(item => !string.IsNullOrWhiteSpace(item))));
}

static IReadOnlyList<GroundTruthScenario> LoadGroundTruthScenarios()
{
    var fixturesRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    if (!Directory.Exists(fixturesRoot))
        throw new DirectoryNotFoundException($"Ground-truth fixtures directory was not found at '{fixturesRoot}'.");

    var fixturePaths = Directory
        .EnumerateFiles(fixturesRoot, "*.json", SearchOption.AllDirectories)
        .Where(path => !Path.GetFileName(path).StartsWith("template", StringComparison.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (fixturePaths.Count == 0)
        throw new InvalidOperationException($"No ground-truth fixture JSON files were found under '{fixturesRoot}'.");

    var scenarios = new List<GroundTruthScenario>();
    var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    foreach (var path in fixturePaths)
    {
        var json = File.ReadAllText(path);
        var suite = JsonSerializer.Deserialize<GroundTruthSuite>(json, serializerOptions);

        if (suite is null || suite.Scenarios.Count == 0)
            throw new InvalidOperationException($"Ground-truth fixture file '{path}' did not contain any scenarios.");

        foreach (var scenario in suite.Scenarios)
        {
            if (!scenario.Enabled)
                continue;

            scenario.Source ??= Path.GetRelativePath(fixturesRoot, path);
            scenarios.Add(scenario);
        }
    }

    return scenarios;
}

static AiOperationKind ParseOperation(string value) =>
    value.ToLowerInvariant() switch
    {
        "compare" => AiOperationKind.Compare,
        "recommend" => AiOperationKind.Recommend,
        "summary" => AiOperationKind.Summary,
        _ => AiOperationKind.Explain
    };

static SignalTransformRecipe CreateTransforms(string mode = "none", double cutoffHz = 1200d) =>
    new()
    {
        Filter = new SignalFilterRecipe
        {
            Mode = mode,
            CutoffHz = cutoffHz,
            LowCutoffHz = 250d,
            HighCutoffHz = 3500d,
            Q = 0.707d
        }
    };

static AiActionPlanner CreateActionPlanner(bool enableLlm) =>
    new(
        new AiPromptBuilder(),
        new ModelRoutingService(
            Options.Create(new AiAssistantOptions
            {
                DefaultProvider = "openai",
                EnableLlm = enableLlm,
                EnableLocalFallback = true
            }),
            Options.Create(new OpenAiOptions())),
        new LlmProviderRegistry([]),
        Options.Create(new AiAssistantOptions
        {
            DefaultProvider = "openai",
            EnableLlm = enableLlm,
            EnableLocalFallback = true
        }));

static AiResponseComposer CreateResponseComposer(bool enableLlm) =>
    new(
        new AiPromptBuilder(),
        new ModelRoutingService(
            Options.Create(new AiAssistantOptions
            {
                DefaultProvider = "openai",
                EnableLlm = enableLlm,
                EnableLocalFallback = true
            }),
            Options.Create(new OpenAiOptions())),
        new LlmProviderRegistry([]),
        new AiExecutionReceiptStore(),
        Options.Create(new AiAssistantOptions
        {
            DefaultProvider = "openai",
            EnableLlm = enableLlm,
            EnableLocalFallback = true
        }),
        NullLogger<AiResponseComposer>.Instance);

internal sealed class GroundTruthSuite
{
    public List<GroundTruthScenario> Scenarios { get; init; } = [];
}

internal sealed class GroundTruthScenario
{
    public string ActiveView { get; init; } = "waveform";

    public GroundTruthArtifactsSpec? Artifacts { get; init; }

    public GroundTruthComparisonSpec? Comparison { get; init; }

    public bool Enabled { get; init; } = true;

    public GroundTruthExpectedSpec Expected { get; init; } = new();

    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Operation { get; init; } = "explain";

    public string Prompt { get; init; } = string.Empty;

    public GroundTruthSignalSpec Signal { get; init; } = new();

    public string? Source { get; set; }

    public double? SelectionEndSeconds { get; init; }

    public double? SelectionStartSeconds { get; init; }

    public double? TransformCutoffHz { get; init; }

    public string? TransformMode { get; init; }
}

internal sealed class GroundTruthArtifactsSpec
{
    public string? BaselineAudio { get; init; }

    public string? CandidateAudio { get; init; }

    public string? Notes { get; init; }

    public string? Screenshot { get; init; }

    public override string ToString()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(BaselineAudio))
            parts.Add($"baseline={BaselineAudio}");

        if (!string.IsNullOrWhiteSpace(CandidateAudio))
            parts.Add($"candidate={CandidateAudio}");

        if (!string.IsNullOrWhiteSpace(Screenshot))
            parts.Add($"screenshot={Screenshot}");

        if (!string.IsNullOrWhiteSpace(Notes))
            parts.Add($"notes={Notes}");

        return string.Join(", ", parts);
    }
}

internal sealed class GroundTruthSignalSpec
{
    public double CrestFactorDb { get; init; } = 10d;

    public string DominantBand { get; init; } = "mixed";

    public double DominantFrequencyHz { get; init; } = 220d;

    public double HighBandRatio { get; init; } = 0.2d;

    public double LowBandRatio { get; init; } = 0.2d;

    public double MidBandRatio { get; init; } = 0.6d;

    public int SamplesOverFullScaleCount { get; init; }

    public double SpectralCentroidHz { get; init; }

    public string SpectrogramBand { get; init; } = "mixed";

    public double SpectrogramVariation { get; init; } = 0.18d;
}

internal sealed class GroundTruthComparisonSpec
{
    public double DominantFrequencyDeltaHz { get; init; }

    public double DurationDeltaSeconds { get; init; }

    public string FileId { get; init; } = "file-b";

    public double HighBandEnergyDelta { get; init; }

    public string Kind { get; init; } = "candidate_compare";

    public double LowBandEnergyDelta { get; init; }

    public double PeakDeltaDbFs { get; init; }

    public double RmsDeltaDb { get; init; }

    public string SourcePath { get; init; } = "candidate.wav";

    public double SpectralCentroidDeltaHz { get; init; }
}

internal sealed class GroundTruthExpectedSpec
{
    public List<string> ForbiddenAnswerPhrases { get; init; } = [];

    public string Mode { get; init; } = "single_signal";

    public string? RecommendedView { get; init; }

    public List<string> RequiredAnswerPhrases { get; init; } = [];

    public List<string> RequiredFactCodes { get; init; } = [];

    public List<string> RequiredImpactPhrases { get; init; } = [];

    public List<string> RequiredPrimaryFindingPhrases { get; init; } = [];
}
