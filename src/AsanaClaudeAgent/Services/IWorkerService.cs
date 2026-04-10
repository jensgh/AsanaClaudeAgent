using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Services;

public interface IWorkerService
{
    Task<WorkResult> ExecuteAsync(AsanaTask task, ClassificationResult classification, string branchName, CancellationToken ct);
}
