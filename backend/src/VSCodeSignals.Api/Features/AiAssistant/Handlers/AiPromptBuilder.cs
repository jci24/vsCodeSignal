using System.Text;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

internal sealed class AiPromptBuilder : IAiPromptBuilder
{
    public LlmStructuredRequest BuildExplanationPrompt(
        AiOperationKind operation,
        WorkspaceContextDto context,
        SignalSummaryDto signalSummary,
        ComparisonSummaryDto? comparisonSummary,
        ObservationBundle observationBundle,
        string prompt,
        IReadOnlyList<AiConversationTurnDto> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are the AI assistant inside a signal analysis workspace.");
        builder.AppendLine("Use only the supplied facts, observations, and limitations.");
        builder.AppendLine("Do not invent measurements, diagnoses, unsupported actions, or stronger claims than the evidence supports.");
        builder.AppendLine("Keep the answer concise, practical, and engineering-focused.");
        builder.AppendLine("Lead with the main standout for the current view, then add at most two supporting findings.");
        builder.AppendLine("Do not repeat the same issue in multiple phrasings.");
        builder.AppendLine("If the active view is waveform, focus on level, headroom, and dynamics.");
        builder.AppendLine("If the active view is FFT, focus on dominant peaks, spectral balance, and centroid.");
        builder.AppendLine("If the active view is spectrogram, focus on sustained bands and whether energy looks stable or time-varying.");
        builder.AppendLine("If this is a comparison request, state the most meaningful before/after or file-to-file differences first.");
        builder.AppendLine("If this is a recommendation request, pick the single best next supported step and explain why it is useful.");
        builder.AppendLine("If a value is unavailable, omit it instead of calling it out.");
        builder.AppendLine("Treat raw sample counts as supporting evidence, not the main explanation. Prefer percentages or coverage wording when explaining over-full-scale behavior.");
        builder.AppendLine("Keep the answer to 2-4 sentences total.");
        builder.AppendLine();
        builder.AppendLine($"Operation: {operation}");
        builder.AppendLine($"User request: {prompt}");
        builder.AppendLine($"Active view: {context.ActiveView}");
        builder.AppendLine($"Selection scope: {context.SelectionScope}");
        builder.AppendLine($"Selected file: {signalSummary.SourcePath}");
        builder.AppendLine("Facts:");

        foreach (var fact in signalSummary.Facts)
            builder.AppendLine($"- [{fact.Basis}/{fact.Confidence}] {fact.Label}: {fact.ValueText}");

        if (comparisonSummary is not null)
        {
            builder.AppendLine("Comparison facts:");

            foreach (var comparison in comparisonSummary.Comparisons)
            foreach (var fact in comparison.Facts)
                builder.AppendLine($"- [{fact.Basis}/{fact.Confidence}] {fact.Label}: {fact.ValueText}");
        }

        if (observationBundle.Observations.Count > 0)
        {
            builder.AppendLine("Observations:");

            foreach (var observation in observationBundle.Observations)
                builder.AppendLine($"- [{observation.Severity}/{observation.Basis}/{observation.Confidence}] {observation.Message}");
        }

        if (observationBundle.Limitations.Count > 0)
        {
            builder.AppendLine("Limitations:");

            foreach (var limitation in observationBundle.Limitations)
                builder.AppendLine($"- {limitation}");
        }

        if (observationBundle.RecommendedActions.Count > 0)
        {
            builder.AppendLine("Supported next steps:");

            foreach (var action in observationBundle.RecommendedActions)
                builder.AppendLine($"- {action}");
        }

        if (history.Count > 0)
        {
            builder.AppendLine("Recent conversation:");

            foreach (var turn in history.TakeLast(6))
                builder.AppendLine($"- {turn.Role}: {turn.Content}");
        }

        return new LlmStructuredRequest
        {
            JsonSchema = BuildNarrativeSchema(),
            SchemaName = "signal_assistant_narrative",
            SystemPrompt = "Return JSON only.",
            UserPrompt = builder.ToString()
        };
    }

    public LlmStructuredRequest BuildActionPlanPrompt(
        WorkspaceContextDto context,
        string prompt,
        IReadOnlyList<AiConversationTurnDto> history)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Convert the user request into a plan using only the supported commands.");
        builder.AppendLine("Do not invent new commands. Ask for clarification when parameters are missing.");
        builder.AppendLine("If the request is unsupported, return unsupported.");
        builder.AppendLine($"User request: {prompt}");
        builder.AppendLine($"Active view: {context.ActiveView}");
        builder.AppendLine($"Selected file id: {context.SelectedFileId}");
        builder.AppendLine($"Compare file ids: {(context.CompareFileIds.Count == 0 ? "none" : string.Join(", ", context.CompareFileIds))}");
        builder.AppendLine("Supported commands:");

        foreach (var command in context.SupportedCommands)
            builder.AppendLine($"- {command}");

        if (history.Count > 0)
        {
            builder.AppendLine("Recent conversation:");

            foreach (var turn in history.TakeLast(4))
                builder.AppendLine($"- {turn.Role}: {turn.Content}");
        }

        return new LlmStructuredRequest
        {
            JsonSchema = BuildActionProposalSchema(),
            SchemaName = "signal_assistant_action_plan",
            SystemPrompt = "Return JSON only.",
            UserPrompt = builder.ToString()
        };
    }

    private static Dictionary<string, object?> BuildNarrativeSchema() =>
        new()
        {
            ["type"] = "object",
            ["required"] = new[] { "headline", "answer", "followUpPrompts" },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["headline"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["answer"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["followUpPrompts"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string"
                    }
                }
            }
        };

    private static Dictionary<string, object?> BuildActionProposalSchema() =>
        new()
        {
            ["type"] = "object",
            ["required"] = new[] { "status", "title", "summary", "warnings", "steps", "clarificationQuestion", "unsupportedReason", "closestSupportedAction" },
            ["additionalProperties"] = false,
            ["properties"] = new Dictionary<string, object?>
            {
                ["status"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["title"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["summary"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["clarificationQuestion"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["unsupportedReason"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["closestSupportedAction"] = new Dictionary<string, object?>
                {
                    ["type"] = "string"
                },
                ["warnings"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string"
                    }
                },
                ["steps"] = new Dictionary<string, object?>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object?>
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = false,
                        ["properties"] = new Dictionary<string, object?>
                        {
                            ["command"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["displayText"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["view"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["filterMode"] = new Dictionary<string, object?> { ["type"] = "string" },
                            ["cutoffHz"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["lowCutoffHz"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["highCutoffHz"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["q"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["gainDb"] = new Dictionary<string, object?> { ["type"] = "number" },
                            ["enabled"] = new Dictionary<string, object?> { ["type"] = "boolean" }
                        }
                    }
                }
            }
        };
}
