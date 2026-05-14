namespace DriveSearch.Dashboard.Models;

/// <summary>
/// Application configuration model for dashboard editing.
/// </summary>
public class AppConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public GoogleConfig Google { get; set; } = new();
    public EmbeddingsConfig Embeddings { get; set; } = new();
    public LlmConfig LLM { get; set; } = new();
    public SyncConfig Sync { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
}

public class DatabaseConfig
{
    public string Path { get; set; } = "data/documents.db";
}

public class GoogleConfig
{
    public string CredentialsFile { get; set; } = "credentials.json";
    public string TokenFile { get; set; } = "token.json";
}

public class EmbeddingsConfig
{
    public string Provider { get; set; } = "gemini";
    public string? GeminiApiKey { get; set; }
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "nomic-embed-text";
    public int OllamaDimensions { get; set; } = 768;
    public string LmStudioUrl { get; set; } = "http://localhost:1234/v1";
    public string LmStudioModel { get; set; } = "text-embedding-nomic-embed-text-v1.5";
    public int LmStudioDimensions { get; set; } = 768;
}

public class LlmConfig
{
    public string Provider { get; set; } = "gemini";
    public string? GeminiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string ClaudeModel { get; set; } = "claude-3-5-sonnet-20241022";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public string LmStudioUrl { get; set; } = "http://localhost:1234/v1";
    public string LmStudioModel { get; set; } = "local-model";
}

public class SyncConfig
{
    public int IntervalMinutes { get; set; } = 5;
}

public class TelegramConfig
{
    public string? BotToken { get; set; }
    public long AllowedUserId { get; set; }
}
