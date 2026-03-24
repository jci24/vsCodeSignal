using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.AiAssistant.Handlers;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class GetAiDebugStatus(
    IOptions<AiAssistantOptions> assistantOptions,
    IOptions<OpenAiOptions> openAiOptions,
    IOptions<OllamaOptions> ollamaOptions,
    AiExecutionReceiptStore receiptStore,
    IWebHostEnvironment environment) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/debug/status");
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

        var openAiConfigured =
            !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var receipts = receiptStore.Snapshot();
        var latestReceipt = receipts.LastOrDefault();

        await HttpContext.Response.WriteAsJsonAsync(new
        {
            enableLlm = assistantOptions.Value.EnableLlm,
            defaultProvider = assistantOptions.Value.DefaultProvider,
            enableLocalFallback = assistantOptions.Value.EnableLocalFallback,
            openAiConfigured,
            openAiBaseUrl = openAiOptions.Value.BaseUrl,
            openAiStandardModel = openAiOptions.Value.StandardModel,
            openAiPremiumModel = openAiOptions.Value.PremiumModel,
            ollamaBaseUrl = ollamaOptions.Value.BaseUrl,
            ollamaModel = ollamaOptions.Value.Model,
            lastReceipt = latestReceipt is null
                ? null
                : new
                {
                    latestReceipt.CreatedAtUtc,
                    latestReceipt.FailureReason,
                    latestReceipt.Model,
                    latestReceipt.Operation,
                    latestReceipt.ProviderKey,
                    latestReceipt.Succeeded,
                    latestReceipt.UsedFallback
                }
        }, cancellationToken: ct);
    }
}
