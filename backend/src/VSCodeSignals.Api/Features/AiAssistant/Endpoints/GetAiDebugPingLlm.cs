using FastEndpoints;
using Microsoft.AspNetCore.Http;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.AiAssistant.Handlers;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class GetAiDebugPingLlm(
    IWebHostEnvironment environment,
    IModelRoutingService modelRoutingService,
    LlmProviderRegistry providerRegistry,
    AiExecutionReceiptStore receiptStore) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/debug/ping-llm");
        Group<AiAssistantGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!environment.IsDevelopment())
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "Not available outside development." }, cancellationToken: ct);
            return;
        }

        var route = modelRoutingService.Resolve(AiOperationKind.Explain, "debug ping");
        var provider = providerRegistry.Resolve(route.ProviderKey);
        var request = new LlmStructuredRequest
        {
            Model = route.Model,
            SchemaName = "debug_ping",
            SystemPrompt = "Return valid JSON only. Set ok to true and note to a brief confirmation.",
            UserPrompt = "Ping the configured model.",
            JsonSchema = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new Dictionary<string, object?>
                {
                    ["ok"] = new Dictionary<string, object?>
                    {
                        ["type"] = "boolean"
                    },
                    ["note"] = new Dictionary<string, object?>
                    {
                        ["type"] = "string"
                    }
                },
                ["required"] = new[] { "ok", "note" }
            }
        };

        try
        {
            var response = await provider.GenerateStructuredJsonAsync(request, ct);

            receiptStore.Record(new AiExecutionReceipt
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                FailureReason = string.Empty,
                Model = route.Model,
                Operation = "debug_ping",
                ProviderKey = provider.ProviderKey,
                Succeeded = true,
                UsedFallback = false
            });

            await HttpContext.Response.WriteAsJsonAsync(new
            {
                success = true,
                provider = provider.ProviderKey,
                model = route.Model,
                response
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            receiptStore.Record(new AiExecutionReceipt
            {
                CreatedAtUtc = DateTimeOffset.UtcNow,
                FailureReason = ex.Message,
                Model = route.Model,
                Operation = "debug_ping",
                ProviderKey = provider.ProviderKey,
                Succeeded = false,
                UsedFallback = false
            });

            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new
            {
                success = false,
                provider = provider.ProviderKey,
                model = route.Model,
                error = ex.Message
            }, cancellationToken: ct);
        }
    }
}
