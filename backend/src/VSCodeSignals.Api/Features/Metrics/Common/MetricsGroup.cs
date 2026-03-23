using FastEndpoints;

namespace VSCodeSignals.Api.Features.Metrics.Common;

internal sealed class MetricsGroup : Group
{
    public MetricsGroup()
    {
        Configure("metrics", ep =>
        {
            ep.Description(d => d.WithTags("Signal Metrics"));
        });
    }
}
