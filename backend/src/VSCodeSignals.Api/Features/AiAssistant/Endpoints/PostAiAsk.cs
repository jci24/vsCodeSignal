using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class PostAiAsk(
    IAiAssistantService assistantService,
    ILogger<PostAiAsk> logger) : Endpoint<AiRequestDto, AiResponseDto>
{
    public override void Configure()
    {
        Post("/ask");
        Group<AiAssistantGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(AiRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = await assistantService.AskAsync(req, ct);
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AI ask request failed.");
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { message = ex.Message }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI ask request failed on the server.");
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "The AI assistant request failed on the server." }, cancellationToken: ct);
        }
    }

    internal sealed class Validator : AbstractValidator<AiRequestDto>
    {
        public Validator()
        {
            RuleFor(item => item.FileId).NotEmpty();
            RuleFor(item => item.Prompt).NotEmpty().MaximumLength(800);
            RuleFor(item => item.History).Must(history => history.Count <= 8);
        }
    }
}
