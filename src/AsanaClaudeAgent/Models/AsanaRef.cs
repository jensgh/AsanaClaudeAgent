using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AsanaRef
{
    [JsonPropertyName("gid")]
    public string Gid { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
