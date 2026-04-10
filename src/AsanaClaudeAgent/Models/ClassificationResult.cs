using System.Text.Json.Serialization;

namespace AsanaClaudeAgent.Models;

public class ClassificationResult
{
    [JsonPropertyName("can_work")]
    public bool CanWork { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("estimated_complexity")]
    public string EstimatedComplexity { get; set; } = "";
}
