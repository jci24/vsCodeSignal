using FastEndpoints;
using FluentValidation;
using VSCodeSignals.Api.Features.Fft.Commands;
using VSCodeSignals.Api.Features.Fft.Common;
using VSCodeSignals.Api.Features.Fft.Handlers;

namespace VSCodeSignals.Api.Features.Fft.Endpoints;

public sealed class GetFft(GetFftHandler handler) : Endpoint<GetFftCommand, GetFftResponse>
{
    public override void Configure()
    {
        Post("/");
        Group<FftGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetFftCommand req, CancellationToken ct)
    {
        var result = await handler.ExecuteAsync(req, ct);
        await Send.OkAsync(result, ct);
    }

    internal sealed class Validator : AbstractValidator<GetFftCommand>
    {
        public Validator()
        {
            RuleFor(command => command.FileId)
                .NotEmpty()
                .WithMessage("A fileId is required.");
        }
    }
}
