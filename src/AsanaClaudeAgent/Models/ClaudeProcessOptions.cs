namespace AsanaClaudeAgent.Models;

public class ClaudeProcessOptions
{
    public required string Prompt { get; set; }
    public required string WorkingDirectory { get; set; }
    public string? OutputFormat { get; set; }
    public string? Model { get; set; }
    public string? AllowedTools { get; set; }
    public decimal? MaxCostUsd { get; set; }
    public bool DangerouslySkipPermissions { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public string? AppendSystemPrompt { get; set; }
}
