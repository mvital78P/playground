using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Llm;

/// <summary>
/// LM Studio LLM provider using OpenAI-compatible API.
/// </summary>
public class LmStudioLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public string ProviderName => "LM Studio";

    public LmStudioLlmProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["LLM:LmStudioUrl"] ?? "http://localhost:1234/v1";
        _model = configuration["LLM:LmStudioModel"] ?? "local-model";
    }

    public async Task<string> GenerateAnswerAsync(string query, string context, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/chat/completions";

        var systemPrompt = "Du bist ein hilfreicher Assistent, der Fragen auf Basis von bereitgestellten Dokumenten beantwortet.";
        var userPrompt = BuildUserPrompt(query, context);

        var request = new ChatRequest
        {
            Model = _model,
            Messages = new[]
            {
                new Message { Role = "system", Content = systemPrompt },
                new Message { Role = "user", Content = userPrompt }
            },
            Temperature = 0.2,
            MaxTokens = 2048
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Empty response from LM Studio");
            }

            return result.Choices[0].Message?.Content ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"LM Studio API request failed: {ex.Message}", ex);
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

    private class ChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required Message[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class ChatResponse
    {
        [JsonPropertyName("choices")]
        public required Choice[] Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    #endregion
}
