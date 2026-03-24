using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

internal sealed class ModelRoutingService(
    IOptions<AiAssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions) : IModelRoutingService
{
    public ModelRouteDecision Resolve(AiOperationKind operation, string prompt)
    {
        var normalizedPrompt = string.IsNullOrWhiteSpace(prompt)
            ? string.Empty
            : prompt.Trim().ToLowerInvariant();
        var usePremium =
            operation == AiOperationKind.Summary &&
            (normalizedPrompt.Contains("report", StringComparison.OrdinalIgnoreCase) ||
             normalizedPrompt.Contains("draft", StringComparison.OrdinalIgnoreCase));

        return new ModelRouteDecision
        {
            AllowLocalFallback = assistantOptions.Value.EnableLocalFallback,
            Model = usePremium
                ? openAiOptions.Value.PremiumModel
                : openAiOptions.Value.StandardModel,
            ProviderKey = assistantOptions.Value.DefaultProvider
        };
    }
}
