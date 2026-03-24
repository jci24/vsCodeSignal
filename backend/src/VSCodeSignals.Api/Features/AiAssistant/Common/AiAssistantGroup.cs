using FastEndpoints;

namespace VSCodeSignals.Api.Features.AiAssistant.Common;

internal sealed class AiAssistantGroup : Group
{
    public AiAssistantGroup()
    {
        Configure("api/ai", ep =>
        {
            ep.Description(d => d.WithTags("AI Assistant"));
        });
    }
}
