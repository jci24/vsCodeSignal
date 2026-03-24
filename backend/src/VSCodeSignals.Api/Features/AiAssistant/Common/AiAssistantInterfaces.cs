namespace VSCodeSignals.Api.Features.AiAssistant.Common;

public interface IWorkspaceContextService
{
    Task<WorkspaceContextDto> BuildAsync(AiWorkspaceRequestContextDto request, CancellationToken ct);
}

public interface ISignalAnalysisService
{
    Task<SignalSummaryDto> GetSignalSummaryAsync(WorkspaceContextDto context, CancellationToken ct);

    Task<ComparisonSummaryDto?> GetComparisonSummaryAsync(WorkspaceContextDto context, SignalSummaryDto signalSummary, CancellationToken ct);
}

public interface IObservationService
{
    ObservationBundle Build(WorkspaceContextDto context, SignalSummaryDto signalSummary, ComparisonSummaryDto? comparisonSummary);
}

public interface IAiIntentClassifier
{
    AiIntentResult Classify(AiRequestDto request, WorkspaceContextDto context);
}

public interface IAiPromptBuilder
{
    LlmStructuredRequest BuildActionPlanPrompt(WorkspaceContextDto context, string prompt, IReadOnlyList<AiConversationTurnDto> history);

    LlmStructuredRequest BuildExplanationPrompt(AiOperationKind operation, WorkspaceContextDto context, SignalSummaryDto signalSummary, ComparisonSummaryDto? comparisonSummary, ObservationBundle observationBundle, string prompt, IReadOnlyList<AiConversationTurnDto> history);
}

public interface IAiOrchestrator
{
    Task<AiResponseDto> ProcessAsync(AiRequestDto request, WorkspaceContextDto context, SignalSummaryDto signalSummary, ComparisonSummaryDto? comparisonSummary, ObservationBundle observationBundle, CancellationToken ct);
}

public interface IAiActionPlanner
{
    Task<AiActionProposalDto> PlanAsync(AiPlanActionRequestDto request, WorkspaceContextDto context, CancellationToken ct);
}

public interface IAiActionValidator
{
    ValidationResult Validate(AiActionProposalDto proposal, WorkspaceContextDto context);
}

public interface IWorkspaceCommandExecutor
{
    Task<WorkspaceCommandExecutionResultDto> ExecuteAsync(AiActionProposalDto proposal, WorkspaceContextDto context, CancellationToken ct);
}

public interface IAiAssistantService
{
    Task<AiResponseDto> AskAsync(AiRequestDto request, CancellationToken ct);

    Task<WorkspaceContextDto> GetContextAsync(AiWorkspaceRequestContextDto request, CancellationToken ct);

    Task<AiActionProposalDto> PlanActionAsync(AiPlanActionRequestDto request, CancellationToken ct);

    Task<AiSummaryCardDto> SummaryAsync(AiSummaryRequestDto request, CancellationToken ct);

    Task<AiResponseDto> ExecuteActionAsync(AiExecuteActionRequestDto request, CancellationToken ct);
}

public interface ILlmProvider
{
    string ProviderKey { get; }

    Task<string> GenerateStructuredJsonAsync(LlmStructuredRequest request, CancellationToken ct);
}

public interface IModelRoutingService
{
    ModelRouteDecision Resolve(AiOperationKind operation, string prompt);
}

public sealed class ValidationResult
{
    public List<string> Errors { get; init; } = [];

    public bool IsValid => Errors.Count == 0;
}

public sealed class ObservationBundle
{
    public List<string> Limitations { get; init; } = [];

    public List<ObservationDto> Observations { get; init; } = [];

    public List<string> RecommendedActions { get; init; } = [];
}

public sealed class LlmStructuredRequest
{
    public Dictionary<string, object?> JsonSchema { get; init; } = [];

    public string Model { get; init; } = string.Empty;

    public string SchemaName { get; init; } = string.Empty;

    public string SystemPrompt { get; init; } = string.Empty;

    public double Temperature { get; init; } = 0.2d;

    public string UserPrompt { get; init; } = string.Empty;
}

public sealed class ModelRouteDecision
{
    public bool AllowLocalFallback { get; init; }

    public string Model { get; init; } = string.Empty;

    public string ProviderKey { get; init; } = string.Empty;
}
