using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DriveSearch.Dashboard.Services;

/// <summary>
/// Service for managing background processes (Telegram Bot, Sync Service, etc.).
/// </summary>
public class ProcessManagementService
{
    private readonly Dictionary<string, ManagedService> _services = new();
    private readonly ILogger<ProcessManagementService> _logger;
    private readonly string _projectRoot;

    public ProcessManagementService(ILogger<ProcessManagementService> logger)
    {
        _logger = logger;

        // Determine project root (3 levels up from Dashboard bin directory when running)
        _projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));

        _logger.LogInformation("Project root: {Root}", _projectRoot);

        // Initialize services
        _services["telegram_bot"] = new ManagedService
        {
            Name = "Telegram Bot",
            ProjectPath = Path.Combine(_projectRoot, "DriveSearch.TelegramBot"),
            Status = new ServiceStatus
            {
                Name = "Telegram Bot",
                Status = "stopped",
                LastUpdated = DateTime.UtcNow
            }
        };

        _services["sync_service"] = new ManagedService
        {
            Name = "Sync Service",
            ProjectPath = Path.Combine(_projectRoot, "DriveSearch.SyncService"),
            Status = new ServiceStatus
            {
                Name = "Sync Service",
                Status = "stopped",
                LastUpdated = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// Gets the status of a service.
    /// </summary>
    public ServiceStatus GetStatus(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var service))
        {
            // Update status based on process state
            if (service.Process != null && !service.Process.HasExited)
            {
                service.Status.Status = "running";
                service.Status.Pid = service.Process.Id;
            }
            else if (service.Status.Status == "running")
            {
                service.Status.Status = "stopped";
                service.Status.Pid = null;
            }

            return service.Status;
        }

        return new ServiceStatus
        {
            Name = serviceName,
            Status = "unknown",
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets the status of all services.
    /// </summary>
    public Dictionary<string, ServiceStatus> GetAllStatuses()
    {
        var statuses = new Dictionary<string, ServiceStatus>();

        foreach (var kvp in _services)
        {
            statuses[kvp.Key] = GetStatus(kvp.Key);
        }

        return statuses;
    }

    /// <summary>
    /// Starts a service by launching its dotnet run process.
    /// First kills any existing instances to prevent duplicates.
    /// </summary>
    public async Task<bool> StartServiceAsync(string serviceName)
    {
        _logger.LogInformation("Starting service: {ServiceName}", serviceName);

        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogError("Service not found: {ServiceName}", serviceName);
            return false;
        }

        // IMPORTANT: Always stop existing instances first to prevent duplicates
        _logger.LogInformation("Ensuring no existing instances are running...");
        await StopServiceAsync(serviceName);
        await Task.Delay(1000); // Wait for processes to fully terminate

        try
        {
            _logger.LogInformation("Project path: {Path}", service.ProjectPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = service.ProjectPath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            service.Process = Process.Start(startInfo);

            if (service.Process != null)
            {
                service.Status.Status = "running";
                service.Status.Pid = service.Process.Id;
                service.Status.LastUpdated = DateTime.UtcNow;
                service.Status.ErrorMessage = null;

                _logger.LogInformation("Service started: {ServiceName} (PID: {Pid})", serviceName, service.Process.Id);

                // Capture and log output
                _ = Task.Run(async () =>
                {
                    while (!service.Process.StandardOutput.EndOfStream)
                    {
                        var line = await service.Process.StandardOutput.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _logger.LogInformation("[{Service}] {Line}", serviceName, line);
                        }
                    }
                });

                _ = Task.Run(async () =>
                {
                    while (!service.Process.StandardError.EndOfStream)
                    {
                        var line = await service.Process.StandardError.ReadLineAsync();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            _logger.LogError("[{Service}] {Line}", serviceName, line);
                            service.Status.ErrorMessage = line;
                        }
                    }
                });

                // Monitor process exit
                _ = Task.Run(async () =>
                {
                    await service.Process.WaitForExitAsync();
                    var exitCode = service.Process.ExitCode;

                    service.Status.Status = exitCode == 0 ? "stopped" : "error";
                    service.Status.Pid = null;
                    service.Status.LastUpdated = DateTime.UtcNow;

                    if (exitCode != 0)
                    {
                        service.Status.ErrorMessage = $"Process exited with code {exitCode}";
                        _logger.LogError("Service crashed: {ServiceName} (Exit code: {ExitCode})", serviceName, exitCode);
                    }
                    else
                    {
                        _logger.LogInformation("Service stopped: {ServiceName}", serviceName);
                    }
                });

                return true;
            }

            _logger.LogError("Failed to start service: {ServiceName}", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting service: {ServiceName}", serviceName);
            service.Status.Status = "error";
            service.Status.ErrorMessage = ex.Message;
            service.Status.LastUpdated = DateTime.UtcNow;
            return false;
        }
    }

    /// <summary>
    /// Stops a service by killing ALL instances (not just the one we started).
    /// </summary>
    public Task<bool> StopServiceAsync(string serviceName)
    {
        _logger.LogInformation("Stopping service: {ServiceName}", serviceName);

        if (!_services.TryGetValue(serviceName, out var service))
        {
            _logger.LogError("Service not found: {ServiceName}", serviceName);
            return Task.FromResult(false);
        }

        try
        {
            // Determine process name based on service
            string processName = serviceName switch
            {
                "telegram_bot" => "DriveSearch.TelegramBot",
                "sync_service" => "DriveSearch.SyncService",
                _ => null
            };

            int killedCount = 0;

            // Kill ALL instances of this process (not just the one we started)
            if (processName != null)
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    try
                    {
                        _logger.LogInformation("Killing process: {ProcessName} (PID: {Pid})", processName, proc.Id);
                        proc.Kill(entireProcessTree: true);
                        killedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not kill process {Pid}", proc.Id);
                    }
                }
            }

            // Also kill the tracked process if it exists
            if (service.Process != null && !service.Process.HasExited)
            {
                try
                {
                    _logger.LogInformation("Killing tracked process (PID: {Pid})", service.Process.Id);
                    service.Process.Kill(entireProcessTree: true);
                    killedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not kill tracked process");
                }
            }

            service.Status.Status = "stopped";
            service.Status.Pid = null;
            service.Status.LastUpdated = DateTime.UtcNow;
            service.Status.ErrorMessage = null;

            _logger.LogInformation("Service stopped: {ServiceName} ({Count} instances killed)", serviceName, killedCount);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping service: {ServiceName}", serviceName);
            service.Status.Status = "error";
            service.Status.ErrorMessage = ex.Message;
            service.Status.LastUpdated = DateTime.UtcNow;
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Restarts a service.
    /// </summary>
    public async Task<bool> RestartServiceAsync(string serviceName)
    {
        _logger.LogInformation("Restarting service: {ServiceName}", serviceName);

        await StopServiceAsync(serviceName);
        await Task.Delay(2000); // Wait 2 seconds
        return await StartServiceAsync(serviceName);
    }

    /// <summary>
    /// Restarts all services.
    /// </summary>
    public async Task<Dictionary<string, bool>> RestartAllAsync()
    {
        var results = new Dictionary<string, bool>();

        foreach (var serviceName in _services.Keys)
        {
            results[serviceName] = await RestartServiceAsync(serviceName);
        }

        return results;
    }

    /// <summary>
    /// Starts all services.
    /// </summary>
    public async Task<Dictionary<string, bool>> StartAllAsync()
    {
        var results = new Dictionary<string, bool>();

        foreach (var serviceName in _services.Keys)
        {
            results[serviceName] = await StartServiceAsync(serviceName);
            await Task.Delay(1000); // Stagger starts
        }

        return results;
    }

    /// <summary>
    /// Stops all services.
    /// </summary>
    public async Task<Dictionary<string, bool>> StopAllAsync()
    {
        var results = new Dictionary<string, bool>();

        foreach (var serviceName in _services.Keys)
        {
            results[serviceName] = await StopServiceAsync(serviceName);
        }

        return results;
    }
}

internal class ManagedService
{
    public string Name { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public Process? Process { get; set; }
    public ServiceStatus Status { get; set; } = new();
}

public class ServiceStatus
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "stopped"; // running, stopped, error
    public DateTime LastUpdated { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Pid { get; set; } // Process ID when running
}
