using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

internal sealed partial class AiActionPlanner(
    IAiPromptBuilder promptBuilder,
    IModelRoutingService modelRoutingService,
    LlmProviderRegistry llmProviderRegistry,
    IOptions<AiAssistantOptions> assistantOptions) : IAiActionPlanner
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiActionProposalDto> PlanAsync(
        AiPlanActionRequestDto request,
        WorkspaceContextDto context,
        CancellationToken ct)
    {
        var ruleBasedPlan = TryPlanFromRules(request.Prompt, context);

        if (ruleBasedPlan is not null)
            return ruleBasedPlan;

        if (!assistantOptions.Value.EnableLlm)
            return CreateClarificationProposal("This action is not clear enough yet. Try rephrasing it using a supported command.");

        try
        {
            var route = modelRoutingService.Resolve(AiOperationKind.ActionPlan, request.Prompt);
            var llmRequest = promptBuilder.BuildActionPlanPrompt(context, request.Prompt, request.History);
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
                    var proposal = JsonSerializer.Deserialize<AiActionProposalDto>(json, SerializerOptions);

                    if (proposal is not null)
                        return EnsureProposalIds(proposal);
                }
                catch (Exception)
                {
                }
            }
        }
        catch (Exception)
        {
        }

        return CreateClarificationProposal("I could not map that request to one of the supported workspace actions.");
    }

    private static AiActionProposalDto? TryPlanFromRules(string prompt, WorkspaceContextDto context)
    {
        var normalized = Normalize(prompt);

        if (string.IsNullOrWhiteSpace(normalized))
            return CreateClarificationProposal("Tell me what workspace action you want to run.");

        if (normalized.Contains("3d", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("three dimensional", StringComparison.OrdinalIgnoreCase))
        {
            return new AiActionProposalDto
            {
                ClosestSupportedAction = "Switch to spectrogram view",
                ProposalId = Guid.NewGuid().ToString("N"),
                RequiresConfirmation = false,
                Status = "unsupported",
                Summary = "3D graph creation is not supported in this MVP.",
                Title = "Unsupported action",
                UnsupportedReason = "The assistant can switch between waveform, FFT, and spectrogram views, but it cannot create 3D graphs yet.",
                Warnings = []
            };
        }

        if (normalized.Contains("reset transforms", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("clear transforms", StringComparison.OrdinalIgnoreCase))
        {
            return CreateProposal(
                "Reset transforms",
                "Reset all active transforms on the selected file.",
                [new AiActionStepDto
                {
                    Command = AiAssistantCommandCatalog.ResetTransforms,
                    DisplayText = "Reset transforms"
                }]);
        }

        if (TryPlanSwitchView(normalized, out var switchViewProposal))
            return switchViewProposal;

        if (TryPlanFilter(normalized, context, out var filterProposal))
            return filterProposal;

        if (TryPlanGain(normalized, out var gainProposal))
            return gainProposal;

        if (TryPlanToggle(normalized, "normalize", AiAssistantCommandCatalog.SetNormalize, out var normalizeProposal))
            return normalizeProposal;

        if (TryPlanToggle(normalized, "trim silence", AiAssistantCommandCatalog.SetTrimSilence, out var trimProposal))
            return trimProposal;

        if (normalized.Contains("compare", StringComparison.OrdinalIgnoreCase))
        {
            if (context.CompareFileIds.Count == 0)
                return CreateClarificationProposal("Select one or more comparison files first, then I can formalize the compare action.");

            return CreateProposal(
                "Use current compare set",
                "Refresh the current compare selection in the workspace.",
                [new AiActionStepDto
                {
                    Command = AiAssistantCommandCatalog.SetCompareTargets,
                    CompareSignalIds = context.CompareFileIds,
                    DisplayText = "Use the current comparison files",
                    PrimarySignalId = context.SelectedFileId
                }]);
        }

        return null;
    }

    private static AiActionProposalDto CreateClarificationProposal(string question) =>
        new()
        {
            ClarificationQuestion = question,
            ProposalId = Guid.NewGuid().ToString("N"),
            RequiresConfirmation = false,
            Status = "needs_clarification",
            Summary = question,
            Title = "Clarification needed"
        };

    private static AiActionProposalDto CreateProposal(
        string title,
        string summary,
        List<AiActionStepDto> steps,
        List<string>? warnings = null) =>
        new()
        {
            ProposalId = Guid.NewGuid().ToString("N"),
            RequiresConfirmation = true,
            Status = "needs_confirmation",
            Steps = steps,
            Summary = summary,
            Title = title,
            Warnings = warnings ?? []
        };

    private static AiActionProposalDto EnsureProposalIds(AiActionProposalDto proposal) =>
        new()
        {
            ClarificationQuestion = proposal.ClarificationQuestion,
            ClosestSupportedAction = proposal.ClosestSupportedAction,
            ProposalId = string.IsNullOrWhiteSpace(proposal.ProposalId)
                ? Guid.NewGuid().ToString("N")
                : proposal.ProposalId,
            RequiresConfirmation = proposal.RequiresConfirmation,
            Status = string.IsNullOrWhiteSpace(proposal.Status)
                ? "needs_confirmation"
                : proposal.Status,
            Steps = proposal.Steps,
            Summary = proposal.Summary,
            Title = proposal.Title,
            UnsupportedReason = proposal.UnsupportedReason,
            Warnings = proposal.Warnings
        };

    private static string Normalize(string prompt) =>
        string.IsNullOrWhiteSpace(prompt)
            ? string.Empty
            : prompt.Trim().ToLowerInvariant();

    private static bool TryPlanSwitchView(string prompt, out AiActionProposalDto proposal)
    {
        var view = prompt.Contains("fft", StringComparison.OrdinalIgnoreCase)
            ? "fft"
            : prompt.Contains("spectrogram", StringComparison.OrdinalIgnoreCase)
                ? "spectrogram"
                : prompt.Contains("waveform", StringComparison.OrdinalIgnoreCase)
                    ? "waveform"
                    : null;

        if (view is null ||
            !(prompt.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
              prompt.Contains("show", StringComparison.OrdinalIgnoreCase) ||
              prompt.Contains("open", StringComparison.OrdinalIgnoreCase)))
        {
            proposal = null!;
            return false;
        }

        proposal = CreateProposal(
            $"Switch to {view.ToUpperInvariant()}",
            $"Switch the active analysis view to {view}.",
            [new AiActionStepDto
            {
                Command = AiAssistantCommandCatalog.SwitchAnalysisView,
                DisplayText = $"Switch to {view}",
                View = view
            }]);
        return true;
    }

    private static bool TryPlanGain(string prompt, out AiActionProposalDto proposal)
    {
        var gainMatch = GainRegex().Match(prompt);

        if (!gainMatch.Success)
        {
            proposal = null!;
            return false;
        }

        var gainDb = double.Parse(gainMatch.Groups["value"].Value, CultureInfo.InvariantCulture);
        proposal = CreateProposal(
            "Set gain",
            $"Apply {gainDb.ToString("0.0", CultureInfo.InvariantCulture)} dB gain to the selected file preview.",
            [new AiActionStepDto
            {
                Command = AiAssistantCommandCatalog.SetGain,
                DisplayText = $"Set gain to {gainDb.ToString("0.0", CultureInfo.InvariantCulture)} dB",
                GainDb = gainDb
            }]);
        return true;
    }

    private static bool TryPlanToggle(
        string prompt,
        string phrase,
        string command,
        out AiActionProposalDto proposal)
    {
        if (!prompt.Contains(phrase, StringComparison.OrdinalIgnoreCase))
        {
            proposal = null!;
            return false;
        }

        var enabled = !prompt.Contains("disable", StringComparison.OrdinalIgnoreCase) &&
                      !prompt.Contains("turn off", StringComparison.OrdinalIgnoreCase) &&
                      !prompt.Contains("remove", StringComparison.OrdinalIgnoreCase);

        proposal = CreateProposal(
            $"{(enabled ? "Enable" : "Disable")} {phrase}",
            $"{(enabled ? "Enable" : "Disable")} {phrase} for the selected file preview.",
            [new AiActionStepDto
            {
                Command = command,
                DisplayText = $"{(enabled ? "Enable" : "Disable")} {phrase}",
                Enabled = enabled
            }]);
        return true;
    }

    private static bool TryPlanFilter(
        string prompt,
        WorkspaceContextDto context,
        out AiActionProposalDto proposal)
    {
        var mode = prompt.Contains("high-pass", StringComparison.OrdinalIgnoreCase) ||
                   prompt.Contains("highpass", StringComparison.OrdinalIgnoreCase) ||
                   prompt.Contains("high pass", StringComparison.OrdinalIgnoreCase)
            ? "highpass"
            : prompt.Contains("low-pass", StringComparison.OrdinalIgnoreCase) ||
              prompt.Contains("lowpass", StringComparison.OrdinalIgnoreCase) ||
              prompt.Contains("low pass", StringComparison.OrdinalIgnoreCase)
                ? "lowpass"
                : prompt.Contains("band-pass", StringComparison.OrdinalIgnoreCase) ||
                  prompt.Contains("bandpass", StringComparison.OrdinalIgnoreCase) ||
                  prompt.Contains("band pass", StringComparison.OrdinalIgnoreCase)
                    ? "bandpass"
                    : prompt.Contains("notch", StringComparison.OrdinalIgnoreCase)
                        ? "notch"
                        : null;

        if (mode is null)
        {
            proposal = null!;
            return false;
        }

        var frequencyMatches = FrequencyRegex().Matches(prompt);
        var warnings = new List<string>();
        var step = new AiActionStepDto
        {
            Command = AiAssistantCommandCatalog.ApplyFilter,
            DisplayText = $"Apply {mode} filter",
            FilterMode = mode,
            Q = 0.707d
        };

        switch (mode)
        {
            case "highpass":
                step = new AiActionStepDto
                {
                    Command = step.Command,
                    CutoffHz = frequencyMatches.Count > 0 ? ParseFrequency(frequencyMatches[0]) : 80d,
                    DisplayText = step.DisplayText,
                    FilterMode = step.FilterMode,
                    Q = step.Q
                };
                if (frequencyMatches.Count == 0)
                    warnings.Add("No cutoff was specified, so the assistant used a default high-pass cutoff of 80 Hz.");
                break;
            case "lowpass":
                step = new AiActionStepDto
                {
                    Command = step.Command,
                    CutoffHz = frequencyMatches.Count > 0 ? ParseFrequency(frequencyMatches[0]) : 1200d,
                    DisplayText = step.DisplayText,
                    FilterMode = step.FilterMode,
                    Q = step.Q
                };
                if (frequencyMatches.Count == 0)
                    warnings.Add("No cutoff was specified, so the assistant used a default low-pass cutoff of 1200 Hz.");
                break;
            case "bandpass":
                var low = frequencyMatches.Count > 0 ? ParseFrequency(frequencyMatches[0]) : 250d;
                var high = frequencyMatches.Count > 1 ? ParseFrequency(frequencyMatches[1]) : 3500d;
                step = new AiActionStepDto
                {
                    Command = step.Command,
                    DisplayText = step.DisplayText,
                    FilterMode = step.FilterMode,
                    HighCutoffHz = Math.Max(high, low + 50d),
                    LowCutoffHz = low,
                    Q = step.Q
                };
                if (frequencyMatches.Count < 2)
                    warnings.Add("Two cutoff values were not provided, so the assistant used the default 250 Hz to 3500 Hz band-pass range.");
                break;
            case "notch":
                step = new AiActionStepDto
                {
                    Command = step.Command,
                    CutoffHz = frequencyMatches.Count > 0 ? ParseFrequency(frequencyMatches[0]) : 60d,
                    DisplayText = step.DisplayText,
                    FilterMode = step.FilterMode,
                    Q = step.Q
                };
                if (frequencyMatches.Count == 0)
                    warnings.Add("No cutoff was specified, so the assistant used a default notch frequency of 60 Hz.");
                break;
        }

        if (prompt.Contains("compare", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(context.CompareFileIds.Count == 0
                ? "No comparison file is currently selected, so only the filter change will be applied."
                : "The comparison summary will reflect the selected file after the filter is applied.");
        }

        proposal = CreateProposal(
            $"Apply {mode} filter",
            $"Apply a {mode} filter to the selected file preview.",
            [step],
            warnings);
        return true;
    }

    private static double ParseFrequency(Match match)
    {
        var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value;
        return string.Equals(unit, "khz", StringComparison.OrdinalIgnoreCase)
            ? value * 1000d
            : value;
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

    [GeneratedRegex(@"(?<value>[+-]?\d+(?:\.\d+)?)\s*d\s?b", RegexOptions.IgnoreCase)]
    private static partial Regex GainRegex();

    [GeneratedRegex(@"(?<value>\d+(?:\.\d+)?)\s*(?<unit>hz|khz)", RegexOptions.IgnoreCase)]
    private static partial Regex FrequencyRegex();
}
