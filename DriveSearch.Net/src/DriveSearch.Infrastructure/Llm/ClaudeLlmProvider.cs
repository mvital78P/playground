using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Llm;

/// <summary>
/// Anthropic Claude LLM provider using the Messages API.
/// </summary>
public class ClaudeLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private const string BaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";

    public string ProviderName => "Claude";

    public ClaudeLlmProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = configuration["LLM:AnthropicApiKey"]
            ?? configuration["ANTHROPIC_API_KEY"]
            ?? throw new InvalidOperationException("Anthropic API key not configured");

        _model = configuration["LLM:ClaudeModel"] ?? "claude-sonnet-4-6";
    }

    public async Task<string> GenerateAnswerAsync(string query, string context, CancellationToken ct = default)
    {
        Console.WriteLine($"[ClaudeLlmProvider] Using model: {_model}");
        Console.WriteLine($"[ClaudeLlmProvider] API endpoint: {BaseUrl}");

        var systemPrompt = "Du bist ein hilfreicher Assistent, der Fragen auf Basis von bereitgestellten Dokumenten beantwortet.";
        var userPrompt = BuildUserPrompt(query, context);

        var request = new ClaudeRequest
        {
            Model = _model,
            MaxTokens = 2048,
            Temperature = 0.2,
            System = systemPrompt,
            Messages = new[]
            {
                new ClaudeMessage
                {
                    Role = "user",
                    Content = userPrompt
                }
            }
        };

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", ApiVersion);
            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>(cancellationToken: ct);

            if (result?.Content == null || result.Content.Length == 0)
            {
                throw new InvalidOperationException("Empty response from Claude");
            }

            // Combine all text content blocks
            var answer = new StringBuilder();
            foreach (var content in result.Content)
            {
                if (content.Type == "text" && !string.IsNullOrEmpty(content.Text))
                {
                    answer.Append(content.Text);
                }
            }

            return answer.ToString();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Claude API request failed: {ex.Message}", ex);
        }
    }

    private static string BuildUserPrompt(string query, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DOKUMENTE:");
        sb.AppendLine(context);
        sb.AppendLine();
        sb.AppendLine("FRAGE:");
        sb.AppendLine(query);
        sb.AppendLine();
        sb.AppendLine("Beantworte die Frage ausschließlich auf Basis der Dokumente. Wenn die Antwort nicht in den Dokumenten zu finden ist, sage das.");

        return sb.ToString();
    }

    #region JSON Models

    private class ClaudeRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("system")]
        public required string System { get; set; }

        [JsonPropertyName("messages")]
        public required ClaudeMessage[] Messages { get; set; }
    }

    private class ClaudeMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public required ContentBlock[] Content { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("type")]
        public required string Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    #endregion
}
