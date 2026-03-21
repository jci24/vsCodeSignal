using MessagePack;

namespace VSCodeSignals.Api.Shared.Contracts;

public static class MessagePackDefaults
{
    public const string ContentType = "application/x-msgpack";

    public static readonly MessagePackSerializerOptions Options =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    public static bool WantsMessagePack(HttpRequest request)
    {
        if (!request.Headers.Accept.Any())
        {
            return false;
        }

        return request.Headers.Accept.Any(static value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(ContentType, StringComparison.OrdinalIgnoreCase));
    }
}
