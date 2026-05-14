using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Llm;

/// <summary>
/// Ollama LLM provider for local language models.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public string ProviderName => "Ollama";

    public OllamaLlmProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["LLM:OllamaUrl"] ?? "http://localhost:11434";
        _model = configuration["LLM:OllamaModel"] ?? "llama3.2";
    }

    public async Task<string> GenerateAnswerAsync(string query, string context, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/api/generate";

        var prompt = BuildPrompt(query, context);

        var request = new GenerateRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false,
            Options = new Options
            {
                Temperature = 0.2,
                TopP = 0.8,
                TopK = 40
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: ct);

            if (string.IsNullOrEmpty(result?.Response))
            {
                throw new InvalidOperationException("Empty response from Ollama");
            }

            return result.Response;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Ollama API request failed: {ex.Message}", ex);
        }
    }

    private static string BuildPrompt(string query, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Du bist ein hilfreicher Assistent, der Fragen auf Basis von bereitgestellten Dokumenten beantwortet.");
        sb.AppendLine();
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

    private class GenerateRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public Options? Options { get; set; }
    }

    private class Options
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("top_k")]
        public int TopK { get; set; }
    }

    private class GenerateResponse
    {
        [JsonPropertyName("response")]
        public required string Response { get; set; }
    }

    #endregion
}
