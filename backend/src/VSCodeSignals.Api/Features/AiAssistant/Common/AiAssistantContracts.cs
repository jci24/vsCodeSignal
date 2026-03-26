using VSCodeSignals.Api.Shared.SignalAnalysis;

namespace VSCodeSignals.Api.Features.AiAssistant.Common;

public class AiWorkspaceRequestContextDto
{
    public string ActiveView { get; init; } = "waveform";

    public List<string> CompareFileIds { get; init; } = [];

    public string FileId { get; init; } = string.Empty;

    public SelectionRangeDto? Selection { get; init; }

    public SignalTransformRecipe Transforms { get; init; } = new();

    public string WorkspaceId { get; init; } = "current";
}

public sealed class SelectionRangeDto
{
    public double? EndSeconds { get; init; }

    public double? StartSeconds { get; init; }
}

public sealed class WorkspaceFileReferenceDto
{
    public string FileId { get; init; } = string.Empty;

    public string SignalKind { get; init; } = string.Empty;

    public string SourcePath { get; init; } = string.Empty;
}

public sealed class WorkspaceContextDto
{
    public string ActiveView { get; init; } = "waveform";

    public string AnalysisBasis { get; init; } =
        "Mono decode metrics and backend observations grounded in the current workspace state.";

    public List<WorkspaceFileReferenceDto> AvailableFiles { get; init; } = [];

    public List<string> CompareFileIds { get; init; } = [];

    public List<WorkspaceFileReferenceDto> CompareFiles { get; init; } = [];

    public bool IsSelectionApplied { get; init; }

    public string SelectedFileId { get; init; } = string.Empty;

    public SelectionRangeDto? Selection { get; init; }

    public string SelectionScope { get; init; } = "full-file";

    public WorkspaceFileReferenceDto? SelectedFile { get; init; }

    public List<string> SupportedCommands { get; init; } = [];

    public SignalTransformRecipe Transforms { get; init; } = new();

    public List<string> Warnings { get; init; } = [];

    public string WorkspaceId { get; init; } = string.Empty;
}

public sealed class EvidenceItemDto
{
    public string Basis { get; init; } = "measured";

    public string Code { get; init; } = string.Empty;

    public string Confidence { get; init; } = "high";

    public string Kind { get; init; } = "fact";

    public string Label { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string ValueText { get; init; } = string.Empty;
}

public sealed class ObservationDto
{
    public string Basis { get; init; } = "rule_based";

    public string Code { get; init; } = string.Empty;

    public string Confidence { get; init; } = "medium";

    public List<string> EvidenceCodes { get; init; } = [];

    public string Message { get; init; } = string.Empty;

    public string Severity { get; init; } = "info";
}

public sealed class SignalSummaryDto
{
    public double CrestFactor { get; init; }

    public double CrestFactorDb { get; init; }

    public string DominantEnergyBand { get; init; } = "unavailable";

    public double DominantFrequencyHz { get; init; }

    public double DominantMagnitudeDb { get; init; }

    public double DurationSeconds { get; init; }

    public string FileId { get; init; } = string.Empty;

    public List<EvidenceItemDto> Facts { get; init; } = [];

    public double HighBandEnergyRatio { get; init; }

    public double LowBandEnergyRatio { get; init; }

    public double MidBandEnergyRatio { get; init; }

    public int NearFullScaleCount { get; init; }

    public double Peak { get; init; }

    public double Rms { get; init; }

    public int SampleRateHz { get; init; }

    public int SamplesOverFullScaleCount { get; init; }

    public double SpectralCentroidHz { get; init; }

    public string SpectrogramDominantBand { get; init; } = "unavailable";

    public double SpectrogramTemporalVariation { get; init; }

    public string SourcePath { get; init; } = string.Empty;
}

public sealed class ComparisonDeltaDto
{
    public string ComparisonKind { get; init; } = "signal";

    public double DominantFrequencyDeltaHz { get; init; }

    public string FileId { get; init; } = string.Empty;

    public List<EvidenceItemDto> Facts { get; init; } = [];

    public double HighBandEnergyDelta { get; init; }

    public double DurationDeltaSeconds { get; init; }

    public double LowBandEnergyDelta { get; init; }

    public double MidBandEnergyDelta { get; init; }

    public double PeakDeltaDbFs { get; init; }

    public double RmsDeltaDb { get; init; }

    public double SpectralCentroidDeltaHz { get; init; }

    public string SourcePath { get; init; } = string.Empty;
}

public sealed class ComparisonSummaryDto
{
    public List<ComparisonDeltaDto> Comparisons { get; init; } = [];

    public List<string> CompareFileIds { get; init; } = [];

    public string PrimaryFileId { get; init; } = string.Empty;
}

public sealed class AiConversationTurnDto
{
    public string Content { get; init; } = string.Empty;

    public string Role { get; init; } = "user";
}

public sealed class AiRequestDto : AiWorkspaceRequestContextDto
{
    public List<AiConversationTurnDto> History { get; init; } = [];

    public string Prompt { get; init; } = string.Empty;
}

public sealed class AiSummaryRequestDto : AiWorkspaceRequestContextDto
{
}

public sealed class AiPlanActionRequestDto : AiWorkspaceRequestContextDto
{
    public List<AiConversationTurnDto> History { get; init; } = [];

    public string Prompt { get; init; } = string.Empty;
}

public sealed class AiExecuteActionRequestDto : AiWorkspaceRequestContextDto
{
    public bool Confirmed { get; init; }

    public AiActionProposalDto Proposal { get; init; } = new();
}

public sealed class AiActionStepDto
{
    public string Command { get; init; } = string.Empty;

    public List<string> CompareSignalIds { get; init; } = [];

    public double? CutoffHz { get; init; }

    public string DisplayText { get; init; } = string.Empty;

    public bool? Enabled { get; init; }

    public string? FilterMode { get; init; }

    public double? GainDb { get; init; }

    public double? HighCutoffHz { get; init; }

    public double? LowCutoffHz { get; init; }

    public string? PrimarySignalId { get; init; }

    public double? Q { get; init; }

    public string? View { get; init; }
}

public sealed class AiActionProposalDto
{
    public string ClarificationQuestion { get; init; } = string.Empty;

    public string ClosestSupportedAction { get; init; } = string.Empty;

    public string ProposalId { get; init; } = string.Empty;

    public bool RequiresConfirmation { get; init; } = true;

    public string Status { get; init; } = "needs_confirmation";

    public List<AiActionStepDto> Steps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string UnsupportedReason { get; init; } = string.Empty;

    public List<string> Warnings { get; init; } = [];
}

public sealed class WorkspaceStatePatchDto
{
    public string? ActiveView { get; init; }

    public List<string>? CompareFileIds { get; init; }

    public string? TargetFileId { get; init; }

    public SignalTransformRecipe? Transforms { get; init; }
}

public sealed class WorkspaceCommandExecutionResultDto
{
    public List<string> ExecutedSteps { get; init; } = [];

    public string Message { get; init; } = string.Empty;

    public WorkspaceStatePatchDto Patch { get; init; } = new();

    public bool Succeeded { get; init; }
}

public sealed class AiFollowUpPromptDto
{
    public string Id { get; init; } = string.Empty;

    public string Intent { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;
}

public sealed class AiSummaryCardDto
{
    public List<EvidenceItemDto> KeyFacts { get; init; } = [];

    public List<string> Limitations { get; init; } = [];

    public string Mode { get; init; } = "single_signal";

    public List<string> NextSteps { get; init; } = [];

    public string PrimaryFinding { get; init; } = string.Empty;

    public string ImpactSummary { get; init; } = string.Empty;

    public string RecommendedNextStep { get; init; } = string.Empty;

    public string? RecommendedView { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public List<ObservationDto> TopObservations { get; init; } = [];
}

public sealed class AiResponseDto
{
    public AiActionProposalDto? ActionProposal { get; init; }

    public WorkspaceContextDto Context { get; init; } = new();

    public WorkspaceCommandExecutionResultDto? ExecutionResult { get; init; }

    public List<AiFollowUpPromptDto> FollowUpPrompts { get; init; } = [];

    public string Intent { get; init; } = "explain";

    public List<string> Limitations { get; init; } = [];

    public string Message { get; init; } = string.Empty;

    public List<ObservationDto> Observations { get; init; } = [];

    public string Status { get; init; } = "ready";

    public AiSummaryCardDto SummaryCard { get; init; } = new();

    public bool UsedFallback { get; init; }

    public WorkspaceStatePatchDto? WorkspacePatch { get; init; }
}

internal static class AiAssistantCommandCatalog
{
    public const string ApplyFilter = "ApplyFilter";
    public const string ResetTransforms = "ResetTransforms";
    public const string SetCompareTargets = "SetCompareTargets";
    public const string SetGain = "SetGain";
    public const string SetNormalize = "SetNormalize";
    public const string SetTrimSilence = "SetTrimSilence";
    public const string SwitchAnalysisView = "SwitchAnalysisView";

    public static IReadOnlyList<string> All { get; } =
    [
        SwitchAnalysisView,
        ApplyFilter,
        SetGain,
        SetNormalize,
        SetTrimSilence,
        SetCompareTargets,
        ResetTransforms
    ];
}

public enum AiIntentType
{
    Explain,
    Compare,
    Recommend,
    Action,
    Clarify,
    Unsupported
}

public enum AiOperationKind
{
    Summary,
    Explain,
    Compare,
    Recommend,
    ActionPlan
}

internal enum AiResponseStatus
{
    Ready,
    NeedsConfirmation,
    NeedsClarification,
    Unsupported,
    Degraded,
    Error
}

public sealed class AiIntentResult
{
    public double Confidence { get; init; }

    public AiIntentType Intent { get; init; }

    public string NormalizedPrompt { get; init; } = string.Empty;
}
