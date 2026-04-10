namespace AsanaClaudeAgent.Configuration;

public class AppSettings
{
    public bool RunOnce { get; set; }
    public int PollingIntervalMinutes { get; set; } = 30;
    public string StateFilePath { get; set; } = "state.json";
    public string LocksDirectory { get; set; } = "locks";
    public bool CommentOnAsana { get; set; } = true;
    public bool FetchAllTasks { get; set; }
    public int MaxRetries { get; set; } = 1;
    public bool DryRun { get; set; }
}
