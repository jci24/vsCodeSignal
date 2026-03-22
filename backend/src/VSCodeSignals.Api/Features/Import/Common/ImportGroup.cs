using FastEndpoints;

namespace VSCodeSignals.Api.Features.Import.Common;

internal sealed class ImportGroup : Group
{
    public ImportGroup()
    {
        Configure("import", ep =>
        {
            ep.Description(d => d.WithTags("Import Operations"));
        });
    }
}
