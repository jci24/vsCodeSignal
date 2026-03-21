using VSCodeSignals.Api.Features.Importer.Command;
using VSCodeSignals.Api.Features.Importer.Response;
using VSCodeSignals.Api.Shared.Kernel;

namespace VSCodeSignals.Api.Features.Importer.Handler;

public sealed class ImportSignalHandler : ICommandHandler<ImportSignalCommand, ImportSignalResponse>
{
    public Task<ImportSignalResponse> HandleAsync(
        ImportSignalCommand command,
        CancellationToken cancellationToken)
    {
        var message = $"Queued signal import for '{command.FileName}' ({command.FileSizeBytes} bytes).";
        var response = new ImportSignalResponse(Guid.NewGuid(), "accepted", message);

        return Task.FromResult(response);
    }
}
