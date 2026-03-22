using FastEndpoints;
using VSCodeSignals.Api.Features.Workspaces.Common;
using VSCodeSignals.Api.Features.Workspaces.Handler;

namespace VSCodeSignals.Api.Features.Workspaces.Endpoint;

public sealed class GetCurrentWorkspaceImports(WorkspaceImportStore store) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/current/imports");
        Group<WorkspaceGroup>();
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        await HttpContext.Response.WriteAsJsonAsync(store.GetSnapshot(), cancellationToken: ct);
    }
}
