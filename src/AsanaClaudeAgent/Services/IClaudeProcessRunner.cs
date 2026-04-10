using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Services;

public interface IClaudeProcessRunner
{
    Task<ClaudeProcessResult> RunAsync(ClaudeProcessOptions options, CancellationToken ct);
}
