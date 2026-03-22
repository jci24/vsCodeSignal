using FastEndpoints;

namespace VSCodeSignals.Api.Features.Workspaces.Common;

internal sealed class WorkspaceGroup : Group
{
    public WorkspaceGroup()
    {
        Configure("workspaces", ep =>
        {
            ep.Description(d => d.WithTags("Workspace Operations"));
        });
    }
}
