using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Llm;

/// <summary>
/// Google Gemini LLM provider using gemini-1.5-flash model.
/// </summary>
public class GeminiLlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string Model = "gemini-1.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public string ProviderName => "Gemini";

    public GeminiLlmProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = configuration["LLM:GeminiApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task<string> GenerateAnswerAsync(string query, string context, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";

        Console.WriteLine($"[GeminiLlmProvider] Using model: {Model}");
        Console.WriteLine($"[GeminiLlmProvider] API endpoint: {BaseUrl}");
        Console.WriteLine($"[GeminiLlmProvider] API key length: {_apiKey?.Length ?? 0}");
        Console.WriteLine($"[GeminiLlmProvider] Full URL (key masked): {BaseUrl}/{Model}:generateContent?key=***");

        var prompt = BuildPrompt(query, context);

        var request = new GenerateRequest
        {
            Contents = new[]
            {
                new Content
                {
                    Parts = new[] { new Part { Text = prompt } }
                }
            },
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2,
                TopP = 0.8,
                TopK = 40,
                MaxOutputTokens = 2048
            }
        };

        try
        {
            Console.WriteLine($"[GeminiLlmProvider] Sending request to Gemini API...");
            var response = await _httpClient.PostAsJsonAsync(url, request, ct);

            Console.WriteLine($"[GeminiLlmProvider] Response status: {(int)response.StatusCode} {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                Console.WriteLine($"[GeminiLlmProvider] Error response body: {errorBody}");
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(cancellationToken: ct);

            if (result?.Candidates == null || result.Candidates.Length == 0)
            {
                throw new InvalidOperationException("No response from Gemini API");
            }

            var candidate = result.Candidates[0];
            if (candidate.Content?.Parts == null || candidate.Content.Parts.Length == 0)
            {
                throw new InvalidOperationException("Empty response from Gemini API");
            }

            return candidate.Content.Parts[0].Text ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Gemini API request failed: {ex.Message}", ex);
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
        [JsonPropertyName("contents")]
        public required Content[] Contents { get; set; }

        [JsonPropertyName("generationConfig")]
        public GenerationConfig? GenerationConfig { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public required Part[] Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public required string Text { get; set; }
    }

    private class GenerationConfig
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("topP")]
        public double TopP { get; set; }

        [JsonPropertyName("topK")]
        public int TopK { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }
    }

    private class GenerateResponse
    {
        [JsonPropertyName("candidates")]
        public required Candidate[] Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    #endregion
}
