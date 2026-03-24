using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class AiActionValidator : IAiActionValidator
{
    public ValidationResult Validate(AiActionProposalDto proposal, WorkspaceContextDto context)
    {
        var errors = new List<string>();

        if (!string.Equals(proposal.Status, "needs_confirmation", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(proposal.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Only actionable proposals can be executed.");
            return new ValidationResult { Errors = errors };
        }

        if (proposal.Steps.Count == 0)
        {
            errors.Add("The proposal does not contain any executable steps.");
            return new ValidationResult { Errors = errors };
        }

        foreach (var step in proposal.Steps)
        {
            switch (step.Command)
            {
                case AiAssistantCommandCatalog.SwitchAnalysisView:
                    if (step.View is not ("waveform" or "fft" or "spectrogram"))
                        errors.Add("SwitchAnalysisView requires waveform, fft, or spectrogram.");
                    break;

                case AiAssistantCommandCatalog.ApplyFilter:
                    ValidateFilter(step, errors);
                    break;

                case AiAssistantCommandCatalog.SetGain:
                    if (step.GainDb is null or < -24d or > 24d)
                        errors.Add("SetGain requires gainDb between -24 and 24.");
                    break;

                case AiAssistantCommandCatalog.SetNormalize:
                case AiAssistantCommandCatalog.SetTrimSilence:
                    if (step.Enabled is null)
                        errors.Add($"{step.Command} requires an enabled flag.");
                    break;

                case AiAssistantCommandCatalog.SetCompareTargets:
                    if (!string.Equals(step.PrimarySignalId, context.SelectedFileId, StringComparison.OrdinalIgnoreCase))
                        errors.Add("SetCompareTargets requires the current selected file as the primary signal.");

                    if (step.CompareSignalIds.Count == 0)
                        errors.Add("SetCompareTargets requires at least one comparison signal.");
                    break;

                case AiAssistantCommandCatalog.ResetTransforms:
                    break;

                default:
                    errors.Add($"Unsupported command '{step.Command}'.");
                    break;
            }
        }

        return new ValidationResult { Errors = errors };
    }

    private static void ValidateFilter(AiActionStepDto step, List<string> errors)
    {
        var mode = step.FilterMode?.Trim().ToLowerInvariant();

        if (mode is not ("highpass" or "lowpass" or "bandpass" or "notch"))
        {
            errors.Add("ApplyFilter requires a supported filter mode.");
            return;
        }

        if (mode is "highpass" or "lowpass" or "notch")
        {
            if (step.CutoffHz is null or < 20d or > 10_000d)
                errors.Add($"ApplyFilter {mode} requires cutoffHz between 20 and 10000.");
        }

        if (mode == "bandpass")
        {
            if (step.LowCutoffHz is null or < 20d)
                errors.Add("ApplyFilter bandpass requires lowCutoffHz of at least 20.");

            if (step.HighCutoffHz is null or > 10_000d)
                errors.Add("ApplyFilter bandpass requires highCutoffHz of at most 10000.");

            if (step.LowCutoffHz is double low && step.HighCutoffHz is double high && high <= low)
                errors.Add("ApplyFilter bandpass requires highCutoffHz greater than lowCutoffHz.");
        }

        if (step.Q is double q && (q < 0.1d || q > 10d))
            errors.Add("ApplyFilter Q must be between 0.1 and 10.");
    }
}
