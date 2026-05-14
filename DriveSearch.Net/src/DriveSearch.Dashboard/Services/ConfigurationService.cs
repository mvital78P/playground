using DriveSearch.Dashboard.Models;
using System.Text.Json;

namespace DriveSearch.Dashboard.Services;

/// <summary>
/// Service for reading and writing application configuration.
/// </summary>
public class ConfigurationService
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(IConfiguration configuration, ILogger<ConfigurationService> logger)
    {
        _logger = logger;

        // Determine config file path (appsettings.json in project root)
        var contentRoot = configuration["ContentRootPath"] ?? Directory.GetCurrentDirectory();
        _configFilePath = Path.Combine(contentRoot, "appsettings.json");
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public AppConfiguration GetConfiguration(IConfiguration config)
    {
        return new AppConfiguration
        {
            Database = new DatabaseConfig
            {
                Path = config["Database:Path"] ?? "data/documents.db"
            },
            Google = new GoogleConfig
            {
                CredentialsFile = config["Google:CredentialsFile"] ?? "credentials.json",
                TokenFile = config["Google:TokenFile"] ?? "token.json"
            },
            Embeddings = new EmbeddingsConfig
            {
                Provider = config["Embeddings:Provider"] ?? "gemini",
                GeminiApiKey = config["Embeddings:GeminiApiKey"] ?? config["GEMINI_API_KEY"],
                OllamaUrl = config["Embeddings:OllamaUrl"] ?? "http://localhost:11434",
                OllamaModel = config["Embeddings:OllamaModel"] ?? "nomic-embed-text",
                OllamaDimensions = int.TryParse(config["Embeddings:OllamaDimensions"], out var od) ? od : 768,
                LmStudioUrl = config["Embeddings:LmStudioUrl"] ?? "http://localhost:1234/v1",
                LmStudioModel = config["Embeddings:LmStudioModel"] ?? "text-embedding-nomic-embed-text-v1.5",
                LmStudioDimensions = int.TryParse(config["Embeddings:LmStudioDimensions"], out var ld) ? ld : 768
            },
            LLM = new LlmConfig
            {
                Provider = config["LLM:Provider"] ?? "gemini",
                GeminiApiKey = config["LLM:GeminiApiKey"] ?? config["GEMINI_API_KEY"],
                AnthropicApiKey = config["LLM:AnthropicApiKey"] ?? config["ANTHROPIC_API_KEY"],
                ClaudeModel = config["LLM:ClaudeModel"] ?? "claude-3-5-sonnet-20241022",
                OllamaUrl = config["LLM:OllamaUrl"] ?? "http://localhost:11434",
                OllamaModel = config["LLM:OllamaModel"] ?? "llama3.2",
                LmStudioUrl = config["LLM:LmStudioUrl"] ?? "http://localhost:1234/v1",
                LmStudioModel = config["LLM:LmStudioModel"] ?? "local-model"
            },
            Sync = new SyncConfig
            {
                IntervalMinutes = int.TryParse(config["Sync:IntervalMinutes"], out var interval) ? interval : 5
            },
            Telegram = new TelegramConfig
            {
                BotToken = config["Telegram:BotToken"],
                AllowedUserId = long.TryParse(config["Telegram:AllowedUserId"], out var userId) ? userId : 0
            }
        };
    }

    /// <summary>
    /// Updates the configuration and saves to appsettings.json.
    /// </summary>
    public async Task<bool> UpdateConfigurationAsync(AppConfiguration newConfig)
    {
        try
        {
            // Read existing appsettings.json
            var json = await File.ReadAllTextAsync(_configFilePath);
            var existingConfig = JsonSerializer.Deserialize<JsonElement>(json);

            // Build new configuration object
            var updatedConfig = new Dictionary<string, object>
            {
                ["Database"] = new Dictionary<string, object>
                {
                    ["Path"] = newConfig.Database.Path
                },
                ["Google"] = new Dictionary<string, object>
                {
                    ["CredentialsFile"] = newConfig.Google.CredentialsFile,
                    ["TokenFile"] = newConfig.Google.TokenFile
                },
                ["Embeddings"] = new Dictionary<string, object>
                {
                    ["Provider"] = newConfig.Embeddings.Provider,
                    ["GeminiApiKey"] = newConfig.Embeddings.GeminiApiKey ?? "",
                    ["OllamaUrl"] = newConfig.Embeddings.OllamaUrl,
                    ["OllamaModel"] = newConfig.Embeddings.OllamaModel,
                    ["OllamaDimensions"] = newConfig.Embeddings.OllamaDimensions.ToString(),
                    ["LmStudioUrl"] = newConfig.Embeddings.LmStudioUrl,
                    ["LmStudioModel"] = newConfig.Embeddings.LmStudioModel,
                    ["LmStudioDimensions"] = newConfig.Embeddings.LmStudioDimensions.ToString()
                },
                ["LLM"] = new Dictionary<string, object>
                {
                    ["Provider"] = newConfig.LLM.Provider,
                    ["GeminiApiKey"] = newConfig.LLM.GeminiApiKey ?? "",
                    ["AnthropicApiKey"] = newConfig.LLM.AnthropicApiKey ?? "",
                    ["ClaudeModel"] = newConfig.LLM.ClaudeModel,
                    ["OllamaUrl"] = newConfig.LLM.OllamaUrl,
                    ["OllamaModel"] = newConfig.LLM.OllamaModel,
                    ["LmStudioUrl"] = newConfig.LLM.LmStudioUrl,
                    ["LmStudioModel"] = newConfig.LLM.LmStudioModel
                },
                ["Sync"] = new Dictionary<string, object>
                {
                    ["IntervalMinutes"] = newConfig.Sync.IntervalMinutes
                },
                ["Telegram"] = new Dictionary<string, object>
                {
                    ["BotToken"] = newConfig.Telegram.BotToken ?? "",
                    ["AllowedUserId"] = newConfig.Telegram.AllowedUserId
                }
            };

            // Preserve existing properties (like Logging, AllowedHosts, etc.)
            if (existingConfig.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in existingConfig.EnumerateObject())
                {
                    if (!updatedConfig.ContainsKey(property.Name))
                    {
                        updatedConfig[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText()) ?? new object();
                    }
                }
            }

            // Write to file with formatting
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var newJson = JsonSerializer.Serialize(updatedConfig, options);
            await File.WriteAllTextAsync(_configFilePath, newJson);

            _logger.LogInformation("Configuration updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration");
            return false;
        }
    }
}
