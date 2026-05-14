using DriveSearch.Dashboard.Models;
using DriveSearch.Dashboard.Services;
using DriveSearch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<ProcessManagementService>();
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddHttpClient();

// Add database for sync stats
var dbPath = builder.Configuration["Database:Path"] ?? "data/documents.db";
// Make path absolute relative to project root
if (!Path.IsPathRooted(dbPath))
{
    dbPath = Path.Combine(projectRoot, dbPath);
}
builder.Services.AddDbContext<DriveSearchContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Scoped);

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable static files
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();

// API Endpoints

// Get all service statuses
app.MapGet("/api/status", (ProcessManagementService pm) =>
{
    var statuses = pm.GetAllStatuses();
    return Results.Ok(new
    {
        services = statuses,
        timestamp = DateTime.UtcNow
    });
});

// Get current configuration
app.MapGet("/api/config", (IConfiguration config, ConfigurationService cs) =>
{
    var appConfig = cs.GetConfiguration(config);
    return Results.Ok(appConfig);
});

// Update configuration
app.MapPost("/api/config", async (AppConfiguration config, ConfigurationService cs) =>
{
    var success = await cs.UpdateConfigurationAsync(config);

    if (success)
    {
        return Results.Ok(new { message = "Configuration updated successfully" });
    }

    return Results.Problem("Failed to update configuration");
});

// Start a service
app.MapPost("/api/services/{serviceName}/start", async (string serviceName, ProcessManagementService pm) =>
{
    var success = await pm.StartServiceAsync(serviceName);

    if (success)
    {
        return Results.Ok(new { message = $"Service {serviceName} started" });
    }

    return Results.NotFound(new { message = $"Service {serviceName} not found" });
});

// Stop a service
app.MapPost("/api/services/{serviceName}/stop", async (string serviceName, ProcessManagementService pm) =>
{
    var success = await pm.StopServiceAsync(serviceName);

    if (success)
    {
        return Results.Ok(new { message = $"Service {serviceName} stopped" });
    }

    return Results.NotFound(new { message = $"Service {serviceName} not found" });
});

// Restart a service
app.MapPost("/api/services/{serviceName}/restart", async (string serviceName, ProcessManagementService pm) =>
{
    var success = await pm.RestartServiceAsync(serviceName);

    if (success)
    {
        return Results.Ok(new { message = $"Service {serviceName} restarted" });
    }

    return Results.NotFound(new { message = $"Service {serviceName} not found" });
});

// Start all services
app.MapPost("/api/start-all", async (ProcessManagementService pm) =>
{
    var results = await pm.StartAllAsync();
    return Results.Ok(new
    {
        message = "All services started",
        results
    });
});

// Stop all services
app.MapPost("/api/stop-all", async (ProcessManagementService pm) =>
{
    var results = await pm.StopAllAsync();
    return Results.Ok(new
    {
        message = "All services stopped",
        results
    });
});

// Restart all services
app.MapPost("/api/restart-all", async (ProcessManagementService pm) =>
{
    var results = await pm.RestartAllAsync();
    return Results.Ok(new
    {
        message = "All services restarted",
        results
    });
});

// Health check
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

// Get sync statistics
app.MapGet("/api/sync/stats", async (DriveSearchContext db) =>
{
    try
    {
        var documentCount = await db.Documents.CountAsync();
        var embeddingCount = await db.Embeddings.CountAsync();

        var lastDocument = await db.Documents
            .OrderByDescending(d => d.IndexedAt)
            .FirstOrDefaultAsync();

        return Results.Ok(new
        {
            documentCount,
            embeddingCount,
            lastSyncTime = lastDocument?.IndexedAt,
            hasData = documentCount > 0
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            documentCount = 0,
            embeddingCount = 0,
            lastSyncTime = (DateTime?)null,
            hasData = false,
            error = ex.Message
        });
    }
});

// Trigger manual sync by calling SyncService HTTP endpoint
app.MapPost("/api/sync/trigger", async (IHttpClientFactory httpClientFactory) =>
{
    try
    {
        var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.PostAsync("http://localhost:5013/api/sync/trigger", null);

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return Results.Ok(new { message = "Sync triggered successfully", details = content });
        }
        else
        {
            return Results.Problem($"Failed to trigger sync: {response.StatusCode}");
        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to trigger sync: {ex.Message}");
    }
});

app.Run();
