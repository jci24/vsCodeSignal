using FastEndpoints;
using Microsoft.AspNetCore.Http;
using VSCodeSignals.Api.Features.AiAssistant.Common;
using VSCodeSignals.Api.Features.Workspaces.Handler;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class GetAiContext(
    IAiAssistantService assistantService,
    ILogger<GetAiContext> logger) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/context/{workspaceId}");
        Group<AiAssistantGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var request = new AiWorkspaceRequestContextDto
            {
                ActiveView = Query<string>("activeView", false) ?? "waveform",
                CompareFileIds = ParseCompareFileIds(HttpContext.Request.Query["compareFileIds"]),
                FileId = Query<string>("fileId", false) ?? string.Empty,
                WorkspaceId = Route<string>("workspaceId") ?? WorkspaceImportStore.CurrentWorkspaceId
            };

            var result = await assistantService.GetContextAsync(request, ct);
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AI context request failed.");
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { message = ex.Message }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI context request failed on the server.");
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "The AI context request failed on the server." }, cancellationToken: ct);
        }
    }

    private static List<string> ParseCompareFileIds(Microsoft.Extensions.Primitives.StringValues values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
