using FastEndpoints;
using MessagePack;
using VSCodeSignals.Api.Features.Importer.Command;
using VSCodeSignals.Api.Features.Importer.Handler;
using VSCodeSignals.Api.Features.Importer.Response;
using VSCodeSignals.Api.Shared.Contracts;

namespace VSCodeSignals.Api.Features.Importer.Endpoint;

public sealed class ImportSignalEndpoint(ImportSignalHandler handler)
    : Endpoint<ImportSignalCommand, ImportSignalResponse>
{
    public override void Configure()
    {
        Post("/api/importer/signals");
        AllowAnonymous();
        Description(b => b.Accepts<ImportSignalCommand>("application/json", MessagePackDefaults.ContentType));
    }

    public override async Task HandleAsync(ImportSignalCommand req, CancellationToken ct)
    {
        var response = await handler.HandleAsync(req, ct);

        if (MessagePackDefaults.WantsMessagePack(HttpContext.Request))
        {
            var payload = MessagePackSerializer.Serialize(response, MessagePackDefaults.Options);

            HttpContext.Response.StatusCode = StatusCodes.Status202Accepted;
            HttpContext.Response.ContentType = MessagePackDefaults.ContentType;
            await HttpContext.Response.Body.WriteAsync(payload, ct);
            return;
        }

        await HttpContext.Response.SendAsync(response, StatusCodes.Status202Accepted, cancellation: ct);
    }
}
