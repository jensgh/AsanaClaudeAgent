using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AppState
{
    [JsonPropertyName("last_run_utc")]
    public DateTime? LastRunUtc { get; set; }

    [JsonPropertyName("tasks")]
    public Dictionary<string, TaskState> Tasks { get; set; } = new();
}
