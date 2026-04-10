using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Services;

public interface IClassifierService
{
    Task<ClassificationResult> ClassifyAsync(AsanaTask task, CancellationToken ct);
}
