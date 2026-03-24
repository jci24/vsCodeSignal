namespace VSCodeSignals.Api.Features.AiAssistant.Common;

public sealed class AiAssistantOptions
{
    public string DefaultProvider { get; init; } = "openai";

    public bool EnableLlm { get; init; } = true;

    public bool EnableLocalFallback { get; init; }
}

public sealed class OpenAiOptions
{
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.openai.com/v1";

    public string PremiumModel { get; init; } = "gpt-5.4";

    public string StandardModel { get; init; } = "gpt-5-mini";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "qwen2.5:7b";
}
