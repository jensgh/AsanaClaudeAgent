using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class AsanaTaskListResponse
{
    [JsonPropertyName("data")]
    public List<AsanaTask> Data { get; set; } = [];

    [JsonPropertyName("next_page")]
    public AsanaNextPage? NextPage { get; set; }
}
