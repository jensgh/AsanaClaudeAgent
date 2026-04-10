using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AsanaTask
{
    [JsonPropertyName("gid")]
    public string Gid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }

    [JsonPropertyName("permalink_url")]
    public string PermalinkUrl { get; set; } = "";

    [JsonPropertyName("modified_at")]
    public DateTime? ModifiedAt { get; set; }

    [JsonPropertyName("assignee")]
    public AsanaRef? Assignee { get; set; }

    [JsonPropertyName("projects")]
    public List<AsanaRef>? Projects { get; set; }
}
