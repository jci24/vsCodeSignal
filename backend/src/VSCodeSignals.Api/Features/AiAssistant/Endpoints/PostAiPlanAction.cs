using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class PostAiPlanAction(
    IAiAssistantService assistantService,
    ILogger<PostAiPlanAction> logger) : Endpoint<AiPlanActionRequestDto, AiActionProposalDto>
{
    public override void Configure()
    {
        Post("/plan-action");
        Group<AiAssistantGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(AiPlanActionRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = await assistantService.PlanActionAsync(req, ct);
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AI action planning request failed.");
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { message = ex.Message }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI action planning request failed on the server.");
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "The AI action planning request failed on the server." }, cancellationToken: ct);
        }
    }

    internal sealed class Validator : AbstractValidator<AiPlanActionRequestDto>
    {
        public Validator()
        {
            RuleFor(item => item.FileId).NotEmpty();
            RuleFor(item => item.Prompt).NotEmpty().MaximumLength(800);
        }
    }
}
