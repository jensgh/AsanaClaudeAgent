using AsanaClaudeAgent.Interfaces;
using System.Net.Http.Json;
using System.Text.Json;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public class AsanaService : IAsanaService
{
    private readonly HttpClient _httpClient;
    private readonly AsanaSettings _settings;
    private readonly ILogger<AsanaService> _logger;

    private const string TaskOptFields = "gid,name,notes,completed,permalink_url,modified_at,assignee,projects.name";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AsanaService(
        HttpClient httpClient,
        IOptions<AsanaSettings> settings,
        ILogger<AsanaService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<AsanaTask>> GetAssignedIncompleteTasksAsync(DateTime? modifiedSince, CancellationToken ct)
    {
        var allTasks = new List<AsanaTask>();
        var url = BuildTaskListUrl(modifiedSince);

        while (url is not null)
        {
            _logger.LogDebug("Fetching tasks from {Url}", url);
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AsanaTaskListResponse>(JsonOptions, ct);
            if (result?.Data is not null)
            {
                allTasks.AddRange(result.Data);
            }

            url = result?.NextPage?.Uri;
        }

        _logger.LogInformation("Fetched {Count} incomplete tasks from Asana", allTasks.Count);
        return allTasks;
    }

    public async Task<AsanaTask> GetTaskAsync(string gid, CancellationToken ct)
    {
        var url = $"tasks/{gid}?opt_fields={TaskOptFields}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        return JsonSerializer.Deserialize<AsanaTask>(data.GetRawText(), JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize task {gid}");
    }

    public async Task PostCommentAsync(string gid, string text, CancellationToken ct)
    {
        var url = $"tasks/{gid}/stories";
        var payload = new { data = new { text } };

        var response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Failed to post comment on task {Gid}: {StatusCode} {Error}", gid, response.StatusCode, error);
            return;
        }

        _logger.LogInformation("Posted comment on Asana task {Gid}", gid);
    }

    private string BuildTaskListUrl(DateTime? modifiedSince)
    {
        var url = $"tasks?assignee={_settings.AssigneeGid}"
                  + $"&workspace={_settings.WorkspaceGid}"
                  + "&completed_since=now"
                  + $"&opt_fields={TaskOptFields}";

        if (modifiedSince.HasValue)
        {
            url += $"&modified_since={modifiedSince.Value:yyyy-MM-ddTHH:mm:ss.fffZ}";
        }

        return url;
    }
}
