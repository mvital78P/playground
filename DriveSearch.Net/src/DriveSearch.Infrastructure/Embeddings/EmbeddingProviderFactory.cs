using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriveSearch.Infrastructure.Embeddings;

/// <summary>
/// Factory for creating embedding providers based on configuration.
/// Uses strategy pattern to select the appropriate provider.
/// </summary>
public class EmbeddingProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public EmbeddingProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Creates an embedding provider based on the configured provider name.
    /// Supported providers: gemini, ollama, lmstudio
    /// </summary>
    public IEmbeddingProvider Create()
    {
        var providerName = _configuration["Embeddings:Provider"]?.ToLowerInvariant() ?? "gemini";

        return providerName switch
        {
            "gemini" => _serviceProvider.GetRequiredService<GeminiEmbeddingProvider>(),
            "ollama" => _serviceProvider.GetRequiredService<OllamaEmbeddingProvider>(),
            "lmstudio" => _serviceProvider.GetRequiredService<LmStudioEmbeddingProvider>(),
            _ => throw new NotSupportedException($"Embedding provider '{providerName}' is not supported. Supported providers: gemini, ollama, lmstudio")
        };
    }

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static IEnumerable<string> GetSupportedProviders()
    {
        return new[] { "gemini", "ollama", "lmstudio" };
    }
}
