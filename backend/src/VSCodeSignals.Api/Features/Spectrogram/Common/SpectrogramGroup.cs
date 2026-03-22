using FastEndpoints;

namespace VSCodeSignals.Api.Features.Spectrogram.Common;

internal sealed class SpectrogramGroup : Group
{
    public SpectrogramGroup()
    {
        Configure("spectrogram", ep =>
        {
            ep.Description(d => d.WithTags("Spectrogram Analysis"));
        });
    }
}
