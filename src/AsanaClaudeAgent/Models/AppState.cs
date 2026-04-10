using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AppState
{
    [JsonPropertyName("last_run_utc")]
    public DateTime? LastRunUtc { get; set; }

    [JsonPropertyName("tasks")]
    public Dictionary<string, TaskState> Tasks { get; set; } = new();
}

public class TaskState
{
    [JsonPropertyName("gid")]
    public string Gid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("branch_name")]
    public string? BranchName { get; set; }

    [JsonPropertyName("pr_url")]
    public string? PrUrl { get; set; }

    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; }

    [JsonPropertyName("last_updated_utc")]
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
