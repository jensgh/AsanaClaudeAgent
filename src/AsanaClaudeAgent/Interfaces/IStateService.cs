using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Interfaces;

public interface IStateService
{
    Task<AppState> LoadAsync(CancellationToken ct);
    Task SaveAsync(AppState state, CancellationToken ct);
    Task<bool> IsTaskLockedAsync(string gid, CancellationToken ct);
    Task LockTaskAsync(string gid, CancellationToken ct);
    Task UnlockTaskAsync(string gid, CancellationToken ct);
}
