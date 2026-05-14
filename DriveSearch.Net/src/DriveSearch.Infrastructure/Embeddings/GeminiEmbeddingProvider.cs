using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Embeddings;

/// <summary>
/// Google Gemini embedding provider using text-embedding-004 model.
/// </summary>
public class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string Model = "text-embedding-004";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public int Dimensions => 768;

    public GeminiEmbeddingProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Embeddings:GeminiApiKey"]
            ?? configuration["GEMINI_API_KEY"]
            ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string taskType = "RETRIEVAL_DOCUMENT")
    {
        var url = $"{BaseUrl}/{Model}:embedContent?key={_apiKey}";

        var request = new EmbedRequest
        {
            Content = new Content
            {
                Parts = new[] { new Part { Text = text } }
            },
            TaskType = taskType
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();

            if (result?.Embedding?.Values == null || result.Embedding.Values.Length == 0)
            {
                throw new InvalidOperationException("Empty embedding returned from Gemini API");
            }

            return result.Embedding.Values;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Gemini API request failed: {ex.Message}", ex);
        }
    }

    #region JSON Models

    private class EmbedRequest
    {
        [JsonPropertyName("content")]
        public required Content Content { get; set; }

        [JsonPropertyName("taskType")]
        public string TaskType { get; set; } = "RETRIEVAL_DOCUMENT";
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

    private class EmbedResponse
    {
        [JsonPropertyName("embedding")]
        public required EmbeddingData Embedding { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("values")]
        public required float[] Values { get; set; }
    }

    #endregion
}
