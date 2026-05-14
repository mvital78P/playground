using DriveSearch.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace DriveSearch.SyncService.Services;

public class SyncSchedulerService : BackgroundService
{
    private readonly ILogger<SyncSchedulerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public SyncSchedulerService(
        ILogger<SyncSchedulerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int?>("Sync:IntervalMinutes") ?? 5;
        _logger.LogInformation("SyncScheduler started. Sync interval: {Interval} minutes", intervalMinutes);

        // Run initial sync immediately
        await RunSyncAsync();

        // Schedule periodic syncs
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSyncAsync();
        }

        _logger.LogInformation("SyncScheduler stopped");
    }

    private async Task RunSyncAsync()
    {
        _logger.LogInformation("Starting Drive synchronization...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<Application.Services.SyncService>();

            var result = await syncService.SynchronizeAsync();

            _logger.LogInformation(
                "Sync completed: {NewDocs} new, {UpdatedDocs} updated, {DeletedDocs} deleted, {ErrorDocs} errors",
                result.FilesAdded,
                result.FilesUpdated,
                result.FilesDeleted,
                result.FilesErrored);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed with error");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SyncScheduler is stopping...");
        await base.StopAsync(cancellationToken);
    }
}
