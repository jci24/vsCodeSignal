using FastEndpoints;

namespace VSCodeSignals.Api.Features.Waveform.Common;

internal sealed class WaveformGroup : Group
{
    public WaveformGroup()
    {
        Configure("waveform", ep =>
        {
            ep.Description(d => d.WithTags("Waveform Analysis"));
        });
    }
}
