using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Interfaces;

public interface IClaudeProcessRunner
{
    Task<ClaudeProcessResult> RunAsync(ClaudeProcessOptions options, CancellationToken ct);
}
