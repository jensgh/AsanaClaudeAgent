using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Hosting;

public class OrchestratorHostedService : BackgroundService
{
    private readonly ITaskOrchestrator _orchestrator;
    private readonly AppSettings _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<OrchestratorHostedService> _logger;

    public OrchestratorHostedService(
        ITaskOrchestrator orchestrator,
        IOptions<AppSettings> settings,
        IHostApplicationLifetime lifetime,
        ILogger<OrchestratorHostedService> logger)
    {
        _orchestrator = orchestrator;
        _settings = settings.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let host fully start
        await Task.Delay(500, stoppingToken);

        if (_settings.RunOnce)
        {
            _logger.LogInformation("Running single cycle (--once mode)");
            await RunSafe(stoppingToken);
            _lifetime.StopApplication();
            return;
        }

        _logger.LogInformation("Starting polling loop (interval: {Interval}m)", _settings.PollingIntervalMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.PollingIntervalMinutes));

        // Run immediately on start
        await RunSafe(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafe(stoppingToken);
        }
    }

    private async Task RunSafe(CancellationToken ct)
    {
        try
        {
            await _orchestrator.RunCycleAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Cycle cancelled due to shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration cycle failed unexpectedly");
        }
    }
}
