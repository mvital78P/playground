using DriveSearch.Application.Services;
using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Data;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Llm;
using DriveSearch.Infrastructure.Repositories;
using DriveSearch.Infrastructure.Search;
using DriveSearch.TelegramBot.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

// Load .env file from project root (3 levels up from bin/Debug/net10.0 when running, 2 levels up from project dir)
var projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../.."));
var envPath = Path.Combine(projectRoot, ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Create host builder
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.AddSingleton<IConfiguration>(configuration);

        // HttpClient for providers
        services.AddHttpClient();

        // Database
        var dbPath = configuration["Database:Path"] ?? "data/documents.db";
        services.AddDbContext<DriveSearchContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Repositories
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // Search Services
        services.AddScoped<VectorSearchService>();
        services.AddScoped<FullTextSearchService>();
        services.AddScoped<HybridSearchService>();

        // Embedding Providers
        services.AddScoped<GeminiEmbeddingProvider>();
        services.AddScoped<OllamaEmbeddingProvider>();
        services.AddScoped<LmStudioEmbeddingProvider>();
        services.AddScoped<EmbeddingProviderFactory>();

        // LLM Providers
        services.AddScoped<GeminiLlmProvider>();
        services.AddScoped<ClaudeLlmProvider>();
        services.AddScoped<OllamaLlmProvider>();
        services.AddScoped<LmStudioLlmProvider>();
        services.AddScoped<LlmProviderFactory>();

        // Application Services
        services.AddScoped<SnippetExtractor>();
        services.AddScoped<RagService>();

        // Telegram Bot Services
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var botToken = configuration["Telegram:BotToken"]
                ?? throw new InvalidOperationException("Telegram bot token not configured");
            return new TelegramBotClient(botToken);
        });

        services.AddSingleton<BotService>();
        services.AddHostedService<BotService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

// Run the bot
await host.RunAsync();
