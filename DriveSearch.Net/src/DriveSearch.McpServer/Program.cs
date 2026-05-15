using DriveSearch.Application.Services;
using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Data;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Llm;
using DriveSearch.Infrastructure.Repositories;
using DriveSearch.Infrastructure.Search;
using DriveSearch.McpServer;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// .env from project root (2 levels up from src/DriveSearch.McpServer/)
var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
var envPath = Path.Combine(projectRoot, ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConfiguration>(configuration);
builder.Services.AddHttpClient();

// Database — path relative to project root so it matches the other services
var dbPath = configuration["Database:Path"] ?? "data/documents.db";
if (!Path.IsPathRooted(dbPath))
    dbPath = Path.Combine(projectRoot, dbPath);

builder.Services.AddDbContext<DriveSearchContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Repositories
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Search Services
builder.Services.AddScoped<VectorSearchService>();
builder.Services.AddScoped<FullTextSearchService>();
builder.Services.AddScoped<HybridSearchService>();

// Embedding Providers
builder.Services.AddScoped<GeminiEmbeddingProvider>();
builder.Services.AddScoped<OllamaEmbeddingProvider>();
builder.Services.AddScoped<LmStudioEmbeddingProvider>();
builder.Services.AddScoped<EmbeddingProviderFactory>();

// LLM Providers
builder.Services.AddScoped<GeminiLlmProvider>();
builder.Services.AddScoped<ClaudeLlmProvider>();
builder.Services.AddScoped<OllamaLlmProvider>();
builder.Services.AddScoped<LmStudioLlmProvider>();
builder.Services.AddScoped<LlmProviderFactory>();

// Application Services
builder.Services.AddScoped<SnippetExtractor>();
builder.Services.AddScoped<RagService>();

// MCP Server — stdio transport, tools from this assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(DriveSearchTools).Assembly);

// Suppress all logging so stdout stays clean for the MCP stdio protocol
builder.Logging.ClearProviders();

await builder.Build().RunAsync();
