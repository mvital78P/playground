using DriveSearch.Application.Services;
using DriveSearch.Core.Interfaces;
using DriveSearch.Infrastructure.Data;
using DriveSearch.Infrastructure.Drive;
using DriveSearch.Infrastructure.Embeddings;
using DriveSearch.Infrastructure.Repositories;
using DriveSearch.Infrastructure.TextExtraction;
using DriveSearch.SyncService.Services;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

// Load .env file from project root (2 levels up from project dir)
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

// Create web application builder
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddSingleton<IConfiguration>(configuration);

// HttpClient for providers
builder.Services.AddHttpClient();

// Database
var dbPath = configuration["Database:Path"] ?? "data/documents.db";
builder.Services.AddDbContext<DriveSearchContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Repositories
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// Google Drive Services
builder.Services.AddScoped<GoogleAuthService>(sp =>
{
    var credentialsFile = configuration["Google:CredentialsFile"] ?? "credentials.json";
    var tokenFile = configuration["Google:TokenFile"] ?? "token.json";

    // Make paths absolute relative to project root
    var credentialsPath = Path.IsPathRooted(credentialsFile)
        ? credentialsFile
        : Path.Combine(projectRoot, credentialsFile);

    var tokenPath = Path.IsPathRooted(tokenFile)
        ? tokenFile
        : Path.Combine(projectRoot, tokenFile);

    return new GoogleAuthService(credentialsPath, tokenPath);
});
builder.Services.AddScoped<GoogleDriveClient>();

// Text Extraction
builder.Services.AddScoped<PdfExtractor>();
builder.Services.AddScoped<DocxExtractor>();
builder.Services.AddScoped<XlsxExtractor>();
builder.Services.AddScoped<PptxExtractor>();
builder.Services.AddScoped<TextExtractionFactory>();

// Embedding Providers
builder.Services.AddScoped<GeminiEmbeddingProvider>();
builder.Services.AddScoped<OllamaEmbeddingProvider>();
builder.Services.AddScoped<LmStudioEmbeddingProvider>();
builder.Services.AddScoped<EmbeddingProviderFactory>();

// Application Services
builder.Services.AddScoped<SyncService>();

// Hosted Service for periodic syncing
builder.Services.AddHostedService<SyncSchedulerService>();

// Build the app
var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<DriveSearchContext>();
    await context.Database.EnsureCreatedAsync();
    await context.InitializeFtsTableAsync();
}

// API endpoint to trigger manual sync
app.MapPost("/api/sync/trigger", async (IServiceScopeFactory scopeFactory) =>
{
    try
    {
        using var scope = scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<SyncService>();

        var result = await syncService.SynchronizeAsync();

        return Results.Ok(new
        {
            message = "Sync completed successfully",
            filesAdded = result.FilesAdded,
            filesUpdated = result.FilesUpdated,
            filesDeleted = result.FilesDeleted,
            filesErrored = result.FilesErrored
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Sync failed: {ex.Message}");
    }
});

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

Console.WriteLine("DriveSearch Sync Service starting on http://localhost:5013...");

// Run the service
await app.RunAsync("http://localhost:5013");
