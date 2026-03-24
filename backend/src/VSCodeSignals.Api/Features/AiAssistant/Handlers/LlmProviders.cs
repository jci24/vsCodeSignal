using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VSCodeSignals.Api.Features.AiAssistant.Common;

namespace VSCodeSignals.Api.Features.AiAssistant.Handlers;

public sealed class LlmProviderRegistry(IEnumerable<ILlmProvider> providers)
{
    private readonly Dictionary<string, ILlmProvider> providersByKey = providers.ToDictionary(
        provider => provider.ProviderKey,
        provider => provider,
        StringComparer.OrdinalIgnoreCase);

    public ILlmProvider Resolve(string providerKey)
    {
        if (!providersByKey.TryGetValue(providerKey, out var provider))
            throw new InvalidOperationException($"No LLM provider is registered for '{providerKey}'.");

        return provider;
    }
}

public sealed class AiExecutionReceiptStore
{
    private readonly object sync = new();
    private readonly Queue<AiExecutionReceipt> receipts = new();

    internal void Record(AiExecutionReceipt receipt)
    {
        lock (sync)
        {
            receipts.Enqueue(receipt);

            while (receipts.Count > 100)
                receipts.Dequeue();
        }
    }

    internal List<AiExecutionReceipt> Snapshot()
    {
        lock (sync)
            return receipts.ToList();
    }
}

internal sealed class AiExecutionReceipt
{
    public DateTimeOffset CreatedAtUtc { get; init; }

    public string FailureReason { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string ProviderKey { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public bool UsedFallback { get; init; }
}

internal sealed class LlmProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class OpenAiLlmProvider(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options) : ILlmProvider
{
    public string ProviderKey => "openai";

    public async Task<string> GenerateStructuredJsonAsync(LlmStructuredRequest request, CancellationToken ct)
    {
        var apiKey = ResolveApiKey();

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new LlmProviderUnavailableException("OpenAI is not configured.");

        var endpoint = $"{options.Value.BaseUrl.TrimEnd('/')}/chat/completions";
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "system",
                    ["content"] = request.SystemPrompt
                },
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["content"] = request.UserPrompt
                }
            },
            ["response_format"] = new Dictionary<string, object?>
            {
                ["type"] = "json_schema",
                ["json_schema"] = new Dictionary<string, object?>
                {
                    ["name"] = request.SchemaName,
                    ["strict"] = true,
                    ["schema"] = request.JsonSchema
                }
            }
        };

        if (ShouldSendTemperature(request.Model, request.Temperature))
            requestBody["temperature"] = request.Temperature;

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(requestBody)
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmProviderUnavailableException("OpenAI could not be reached.", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new LlmProviderUnavailableException($"OpenAI returned {(int)response.StatusCode}: {SummarizeFailurePayload(payload)}");

        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new LlmProviderUnavailableException($"OpenAI returned no choices. Payload: {SummarizeFailurePayload(payload)}");

        var content = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new LlmProviderUnavailableException($"OpenAI returned an empty response. Payload: {SummarizeFailurePayload(payload)}");

        return NormalizeJsonText(content);
    }

    private string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.ApiKey))
            return options.Value.ApiKey;

        return Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
    }

    private static string NormalizeJsonText(string text)
    {
        var trimmed = text.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');

        return firstBrace >= 0 && lastBrace > firstBrace
            ? trimmed[firstBrace..(lastBrace + 1)]
            : trimmed;
    }

    private static bool ShouldSendTemperature(string model, double temperature) =>
        !model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(temperature - 1d) > double.Epsilon;

    private static string SummarizeFailurePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return "Empty response body.";

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                var message = errorElement.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;
                var type = errorElement.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                return string.Join(" | ", new[] { type, message }.Where(item => !string.IsNullOrWhiteSpace(item)));
            }
        }
        catch
        {
        }

        var trimmed = payload.Trim();
        return trimmed.Length <= 240 ? trimmed : $"{trimmed[..240]}...";
    }
}

public sealed class OllamaLlmProvider(
    HttpClient httpClient,
    IOptions<OllamaOptions> options) : ILlmProvider
{
    public string ProviderKey => "ollama";

    public async Task<string> GenerateStructuredJsonAsync(LlmStructuredRequest request, CancellationToken ct)
    {
        var endpoint = $"{options.Value.BaseUrl.TrimEnd('/')}/api/chat";

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new Dictionary<string, object?>
            {
                ["model"] = options.Value.Model,
                ["stream"] = false,
                ["format"] = request.JsonSchema,
                ["messages"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["role"] = "system",
                        ["content"] = request.SystemPrompt
                    },
                    new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = request.UserPrompt
                    }
                }
            })
        };
        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmProviderUnavailableException("Ollama could not be reached.", ex);
        }

        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new LlmProviderUnavailableException($"Ollama returned {(int)response.StatusCode}: {payload.Trim()}");

        using var document = JsonDocument.Parse(payload);

        if (!document.RootElement.TryGetProperty("message", out var messageElement) ||
            !messageElement.TryGetProperty("content", out var contentElement))
            throw new LlmProviderUnavailableException($"Ollama returned an unexpected payload: {payload.Trim()}");

        var content = contentElement.GetString();

        if (string.IsNullOrWhiteSpace(content))
            throw new LlmProviderUnavailableException($"Ollama returned an empty response: {payload.Trim()}");

        return content.Trim();
    }
}
