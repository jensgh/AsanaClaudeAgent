namespace AsanaClaudeAgent.Models;

public class ClaudeProcessResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
    public bool TimedOut { get; set; }
}
