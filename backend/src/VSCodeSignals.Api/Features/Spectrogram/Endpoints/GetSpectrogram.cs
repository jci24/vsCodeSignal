using FastEndpoints;
using FluentValidation;
using VSCodeSignals.Api.Features.Spectrogram.Commands;
using VSCodeSignals.Api.Features.Spectrogram.Common;
using VSCodeSignals.Api.Features.Spectrogram.Handlers;

namespace VSCodeSignals.Api.Features.Spectrogram.Endpoints;

public sealed class GetSpectrogram(GetSpectrogramHandler handler)
    : Endpoint<GetSpectrogramCommand, GetSpectrogramResponse>
{
    public override void Configure()
    {
        Post("/");
        Group<SpectrogramGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetSpectrogramCommand req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);
        await Send.OkAsync(result, ct);
    }

    internal sealed class Validator : AbstractValidator<GetSpectrogramCommand>
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
