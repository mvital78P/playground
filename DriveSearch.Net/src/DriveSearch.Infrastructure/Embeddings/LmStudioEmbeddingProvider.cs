using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DriveSearch.Infrastructure.Embeddings;

/// <summary>
/// LM Studio embedding provider using OpenAI-compatible API.
/// </summary>
public class LmStudioEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public int Dimensions { get; }

    public LmStudioEmbeddingProvider(IConfiguration configuration, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Embeddings:LmStudioUrl"] ?? "http://localhost:1234/v1";
        _model = configuration["Embeddings:LmStudioModel"] ?? "text-embedding-nomic-embed-text-v1.5";

        // Default to 768, but can be overridden in config
        Dimensions = int.TryParse(configuration["Embeddings:LmStudioDimensions"], out var dims) ? dims : 768;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, string taskType = "RETRIEVAL_DOCUMENT")
    {
        var url = $"{_baseUrl}/embeddings";

        var request = new EmbedRequest
        {
            Model = _model,
            Input = text
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>();

            if (result?.Data == null || result.Data.Length == 0 || result.Data[0].Embedding == null)
            {
                throw new InvalidOperationException("Empty embedding returned from LM Studio");
            }

            return result.Data[0].Embedding;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"LM Studio API request failed: {ex.Message}", ex);
        }
    }

    #region JSON Models

    private class EmbedRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("input")]
        public required string Input { get; set; }
    }

    private class EmbedResponse
    {
        [JsonPropertyName("data")]
        public required EmbeddingData[] Data { get; set; }
    }

    private class EmbeddingData
    {
        [JsonPropertyName("embedding")]
        public required float[] Embedding { get; set; }
    }

    #endregion
}
