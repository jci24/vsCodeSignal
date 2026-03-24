using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Endpoints;

public sealed class PostAiSummary(
    IAiAssistantService assistantService,
    ILogger<PostAiSummary> logger) : Endpoint<AiSummaryRequestDto, AiSummaryCardDto>
{
    public override void Configure()
    {
        Post("/summary");
        Group<AiAssistantGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(AiSummaryRequestDto req, CancellationToken ct)
    {
        try
        {
            var result = await assistantService.SummaryAsync(req, ct);
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "AI summary request failed.");
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { message = ex.Message }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI summary request failed on the server.");
            HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await HttpContext.Response.WriteAsJsonAsync(new { message = "The AI summary request failed on the server." }, cancellationToken: ct);
        }
    }

    internal sealed class Validator : AbstractValidator<AiSummaryRequestDto>
    {
        public Validator()
        {
            RuleFor(item => item.FileId).NotEmpty();
            RuleFor(item => item.ActiveView).Must(view =>
            {
                var normalized = string.IsNullOrWhiteSpace(view) ? "waveform" : view.Trim().ToLowerInvariant();
                return normalized is "waveform" or "fft" or "spectrogram";
            });
        }
    }
}
