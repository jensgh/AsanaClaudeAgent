namespace AsanaClaudeAgent.Models;

public class ClaudeProcessResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public bool TimedOut { get; set; }
}

public class ClaudeProcessOptions
{
    public required string Prompt { get; set; }
    public required string WorkingDirectory { get; set; }
    public string? OutputFormat { get; set; }
    public string? Model { get; set; }
    public string? AllowedTools { get; set; }
    public decimal? MaxBudgetUsd { get; set; }
    public bool DangerouslySkipPermissions { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public string? AppendSystemPrompt { get; set; }
}

public class WorkResult
{
    public bool Success { get; set; }
    public string? PrUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
