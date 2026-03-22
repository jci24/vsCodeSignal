using FastEndpoints;

namespace VSCodeSignals.Api.Features.Fft.Common;

internal sealed class FftGroup : Group
{
    public FftGroup()
    {
        Configure("fft", ep =>
        {
            ep.Description(d => d.WithTags("FFT Analysis"));
        });
    }
}
