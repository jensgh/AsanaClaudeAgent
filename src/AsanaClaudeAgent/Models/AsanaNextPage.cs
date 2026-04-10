using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AsanaNextPage
{
    [JsonPropertyName("offset")]
    public string? Offset { get; set; }

    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}
