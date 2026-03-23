using FastEndpoints;
using FluentValidation;
using VSCodeSignals.Api.Features.Metrics.Commands;
using VSCodeSignals.Api.Features.Metrics.Common;
using VSCodeSignals.Api.Features.Metrics.Handlers;

namespace VSCodeSignals.Api.Features.Metrics.Endpoints;

public sealed class GetMetrics(GetMetricsHandler handler)
    : Endpoint<GetMetricsCommand, GetMetricsResponse>
{
    public override void Configure()
    {
        Post("/");
        Group<MetricsGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetMetricsCommand req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);
        await Send.OkAsync(result, ct);
    }

    internal sealed class Validator : AbstractValidator<GetMetricsCommand>
    {
        public Validator()
        {
            RuleFor(command => command.FileId)
                .NotEmpty()
                .WithMessage("A fileId is required.");

            RuleFor(command => command.Transforms.GainDb)
                .InclusiveBetween(-24d, 24d)
                .WithMessage("gainDb must be between -24 and 24.");
        }
    }
}
