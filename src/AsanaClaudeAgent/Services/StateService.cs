using System.Text.Json;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public class StateService : IStateService
{
    private readonly AppSettings _settings;
    private readonly ILogger<StateService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public StateService(IOptions<AppSettings> settings, ILogger<StateService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<AppState> LoadAsync(CancellationToken ct)
    {
        var path = _settings.StateFilePath;
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            return JsonSerializer.Deserialize<AppState>(json, JsonOptions) ?? new AppState();
        }
        catch (FileNotFoundException)
        {
            _logger.LogInformation("No state file found at {Path}, starting fresh", path);
            return new AppState();
        }
    }

    public async Task SaveAsync(AppState state, CancellationToken ct)
    {
        var path = _settings.StateFilePath;
        var tmpPath = path + ".tmp";
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await File.WriteAllTextAsync(tmpPath, json, ct);
        File.Move(tmpPath, path, overwrite: true);
    }

    public Task<bool> IsTaskLockedAsync(string gid, CancellationToken ct)
    {
        return Task.FromResult(File.Exists(GetLockPath(gid)));
    }

    public async Task LockTaskAsync(string gid, CancellationToken ct)
    {
        Directory.CreateDirectory(_settings.LocksDirectory);
        var content = JsonSerializer.Serialize(new
        {
            pid = Environment.ProcessId,
            startedUtc = DateTime.UtcNow
        });
        await File.WriteAllTextAsync(GetLockPath(gid), content, ct);
    }

    public Task UnlockTaskAsync(string gid, CancellationToken ct)
    {
        File.Delete(GetLockPath(gid));
        return Task.CompletedTask;
    }

    private string GetLockPath(string gid) =>
        Path.Combine(_settings.LocksDirectory, $"{gid}.lock");
}
