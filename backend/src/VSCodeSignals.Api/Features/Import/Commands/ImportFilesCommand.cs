using FastEndpoints;
using VSCodeSignals.Api.Features.Import.Common;

namespace VSCodeSignals.Api.Features.Import.Commands;

public sealed record ImportFilesCommand(IReadOnlyList<string> FilePaths)
    : ICommand<ImportFilesResult>;
