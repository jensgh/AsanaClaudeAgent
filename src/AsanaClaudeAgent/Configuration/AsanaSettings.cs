namespace AsanaClaudeAgent.Configuration;

public class AsanaSettings
{
    public string Token { get; set; } = "";
    public string WorkspaceGid { get; set; } = "";
    public string AssigneeGid { get; set; } = "me";
    public string BaseUrl { get; set; } = "https://app.asana.com/api/1.0/";
}
