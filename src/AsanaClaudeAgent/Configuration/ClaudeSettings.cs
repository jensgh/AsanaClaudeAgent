namespace AsanaClaudeAgent.Configuration;

public class ClaudeSettings
{
    public string MonorepoPath { get; set; } = "/home/jens/repos/monorepo";
    public string ClaudeBinaryPath { get; set; } = "claude";
    public string? ClassificationModel { get; set; } = "sonnet";
    public string? WorkerModel { get; set; }
    public int MaxConcurrentTasks { get; set; } = 1;
    public decimal? MaxBudgetPerTask { get; set; }
    public int ClassificationTimeoutSeconds { get; set; } = 120;
    public int WorkerTimeoutMinutes { get; set; } = 30;
    public bool DangerouslySkipPermissions { get; set; }
}
