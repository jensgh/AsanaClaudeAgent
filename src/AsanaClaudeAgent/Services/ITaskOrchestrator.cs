namespace AsanaClaudeAgent.Services;

public interface ITaskOrchestrator
{
    Task RunCycleAsync(CancellationToken ct);
}
