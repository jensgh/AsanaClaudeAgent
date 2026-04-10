namespace AsanaClaudeAgent.Interfaces;

public interface ITaskOrchestrator
{
    Task RunCycleAsync(CancellationToken ct);
}
