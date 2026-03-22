using FastEndpoints;
using FluentValidation;
using VSCodeSignals.Api.Features.Import.Commands;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Endpoints;

public sealed class ImportFiles : Endpoint<ImportFilesCommand, ImportFilesResult>
{
    public override void Configure()
    {
        Post("/");
        Group<ImportGroup>();
        AllowAnonymous();
    }

    internal sealed class ImportFilesCommandValidator : Validator<ImportFilesCommand>
    {
        private const string ValidPathPattern = @"^[a-zA-Z0-9_\/\\.\s(),:-]+$";

        public ImportFilesCommandValidator()
        {
            RuleFor(v => v)
                .Must(cmd => (cmd.FilePaths?.Count ?? 0) > 0)
                .WithMessage("At least one file or directory path must be provided in FilePaths.");

            RuleForEach(v => v.FilePaths)
                .Matches(ValidPathPattern)
                .WithMessage("Path contains invalid/special characters.");
        }
    }

    public override async Task HandleAsync(ImportFilesCommand req, CancellationToken ct)
    {
        var result = await req.ExecuteAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
