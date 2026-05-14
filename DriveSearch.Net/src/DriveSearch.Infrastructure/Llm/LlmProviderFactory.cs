using DriveSearch.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DriveSearch.Infrastructure.Llm;

/// <summary>
/// Factory for creating LLM providers based on configuration.
/// Uses strategy pattern to select the appropriate provider.
/// </summary>
public class LlmProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public LlmProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    /// <summary>
    /// Creates an LLM provider based on the configured provider name.
    /// Supported providers: gemini, claude, ollama, lmstudio
    /// </summary>
    public ILlmProvider Create()
    {
        var providerName = _configuration["LLM:Provider"]?.ToLowerInvariant() ?? "gemini";

        return providerName switch
        {
            "gemini" => _serviceProvider.GetRequiredService<GeminiLlmProvider>(),
            "claude" => _serviceProvider.GetRequiredService<ClaudeLlmProvider>(),
            "ollama" => _serviceProvider.GetRequiredService<OllamaLlmProvider>(),
            "lmstudio" => _serviceProvider.GetRequiredService<LmStudioLlmProvider>(),
            _ => throw new NotSupportedException($"LLM provider '{providerName}' is not supported. Supported providers: gemini, claude, ollama, lmstudio")
        };
    }

    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    public static IEnumerable<string> GetSupportedProviders()
    {
        return new[] { "gemini", "claude", "ollama", "lmstudio" };
    }
}
