using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Embeddings;

/// <summary>
/// Ollama embedding provider for local embeddings.
/// </summary>
public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public int Dimensions { get; }

    public OllamaEmbeddingProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Embeddings:OllamaUrl"] ?? "http://localhost:11434";
        _model = configuration["Embeddings:OllamaModel"] ?? "nomic-embed-text";

        // nomic-embed-text uses 768 dimensions, but this could vary by model
        Dimensions = int.TryParse(configuration["Embeddings:OllamaDimensions"], out var dims) ? dims : 768;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string taskType = "RETRIEVAL_DOCUMENT")
    {
        var url = $"{_baseUrl}/api/embeddings";

        var request = new EmbedRequest
        {
            Model = _model,
            Prompt = text
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Empty embedding returned from Ollama");
            }

            return result.Embedding;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Ollama API request failed: {ex.Message}", ex);
        }
    }

    #region JSON Models

    private class EmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("prompt")]
        public required string Prompt { get; set; }
    }

    private class EmbedResponse
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; set; }
    }

    #endregion
}
