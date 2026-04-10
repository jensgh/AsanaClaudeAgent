using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Interfaces;

public interface IWorkerService
{
    Task<WorkResult> ExecuteAsync(AsanaTask task, ClassificationResult classification, string branchName, CancellationToken ct);
}
