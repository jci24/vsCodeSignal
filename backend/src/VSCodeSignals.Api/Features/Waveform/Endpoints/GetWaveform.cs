using FastEndpoints;
using FluentValidation;
using VSCodeSignals.Api.Features.Waveform.Commands;
using VSCodeSignals.Api.Features.Waveform.Common;
using VSCodeSignals.Api.Features.Waveform.Handlers;

namespace VSCodeSignals.Api.Features.Waveform.Endpoints;

public sealed class GetWaveform(GetWaveformHandler handler)
    : Endpoint<GetWaveformCommand, GetWaveformResponse>
{
    public override void Configure()
    {
        Post("/");
        Group<WaveformGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetWaveformCommand req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);
        await Send.OkAsync(result, ct);
    }

    internal sealed class Validator : AbstractValidator<GetWaveformCommand>
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
