using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class WorkspaceCommandExecutor : IWorkspaceCommandExecutor
{
    public Task<WorkspaceCommandExecutionResultDto> ExecuteAsync(
        AiActionProposalDto proposal,
        WorkspaceContextDto context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var activeView = context.ActiveView;
        var compareFileIds = context.CompareFileIds.ToList();
        var recipe = CloneRecipe(context.Transforms);
        var transformsChanged = false;
        var executedSteps = new List<string>();

        foreach (var step in proposal.Steps)
        {
            executedSteps.Add(string.IsNullOrWhiteSpace(step.DisplayText) ? step.Command : step.DisplayText);

            switch (step.Command)
            {
                case AiAssistantCommandCatalog.SwitchAnalysisView:
                    activeView = step.View ?? activeView;
                    break;

                case AiAssistantCommandCatalog.ApplyFilter:
                    recipe = new SignalTransformRecipe
                    {
                        Filter = new SignalFilterRecipe
                        {
                            CutoffHz = step.CutoffHz ?? recipe.Filter.CutoffHz,
                            HighCutoffHz = step.HighCutoffHz ?? recipe.Filter.HighCutoffHz,
                            LowCutoffHz = step.LowCutoffHz ?? recipe.Filter.LowCutoffHz,
                            Mode = step.FilterMode ?? recipe.Filter.Mode,
                            Q = step.Q ?? recipe.Filter.Q
                        },
                        GainDb = recipe.GainDb,
                        Normalize = recipe.Normalize,
                        TrimSilence = recipe.TrimSilence
                    };
                    transformsChanged = true;
                    break;

                case AiAssistantCommandCatalog.SetGain:
                    recipe = new SignalTransformRecipe
                    {
                        Filter = CloneFilter(recipe.Filter),
                        GainDb = step.GainDb ?? recipe.GainDb,
                        Normalize = recipe.Normalize,
                        TrimSilence = recipe.TrimSilence
                    };
                    transformsChanged = true;
                    break;

                case AiAssistantCommandCatalog.SetNormalize:
                    recipe = new SignalTransformRecipe
                    {
                        Filter = CloneFilter(recipe.Filter),
                        GainDb = recipe.GainDb,
                        Normalize = step.Enabled ?? recipe.Normalize,
                        TrimSilence = recipe.TrimSilence
                    };
                    transformsChanged = true;
                    break;

                case AiAssistantCommandCatalog.SetTrimSilence:
                    recipe = new SignalTransformRecipe
                    {
                        Filter = CloneFilter(recipe.Filter),
                        GainDb = recipe.GainDb,
                        Normalize = recipe.Normalize,
                        TrimSilence = step.Enabled ?? recipe.TrimSilence
                    };
                    transformsChanged = true;
                    break;

                case AiAssistantCommandCatalog.SetCompareTargets:
                    compareFileIds = step.CompareSignalIds.ToList();
                    break;

                case AiAssistantCommandCatalog.ResetTransforms:
                    recipe = new SignalTransformRecipe();
                    transformsChanged = true;
                    break;
            }
        }

        return Task.FromResult(new WorkspaceCommandExecutionResultDto
        {
            ExecutedSteps = executedSteps,
            Message = executedSteps.Count == 1
                ? $"{executedSteps[0]} completed."
                : $"{executedSteps.Count} assistant actions completed.",
            Patch = new WorkspaceStatePatchDto
            {
                ActiveView = string.Equals(activeView, context.ActiveView, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : activeView,
                CompareFileIds = compareFileIds.SequenceEqual(context.CompareFileIds)
                    ? null
                    : compareFileIds,
                TargetFileId = transformsChanged ? context.SelectedFileId : null,
                Transforms = transformsChanged ? recipe : null
            },
            Succeeded = true
        });
    }

    private static SignalFilterRecipe CloneFilter(SignalFilterRecipe filter) =>
        new()
        {
            CutoffHz = filter.CutoffHz,
            HighCutoffHz = filter.HighCutoffHz,
            LowCutoffHz = filter.LowCutoffHz,
            Mode = filter.Mode,
            Q = filter.Q
        };

    private static SignalTransformRecipe CloneRecipe(SignalTransformRecipe recipe) =>
        new()
        {
            Filter = CloneFilter(recipe.Filter),
            GainDb = recipe.GainDb,
            Normalize = recipe.Normalize,
            TrimSilence = recipe.TrimSilence
        };
}
