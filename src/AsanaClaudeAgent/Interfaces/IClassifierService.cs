using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Interfaces;

public interface IClassifierService
{
    Task<ClassificationResult> ClassifyAsync(AsanaTask task, CancellationToken ct);
}
