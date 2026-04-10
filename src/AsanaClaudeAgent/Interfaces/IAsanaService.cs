using AsanaClaudeAgent.Models;

namespace AsanaClaudeAgent.Interfaces;

public interface IAsanaService
{
    Task<List<AsanaTask>> GetAssignedIncompleteTasksAsync(DateTime? modifiedSince, CancellationToken ct);
    Task<AsanaTask> GetTaskAsync(string gid, CancellationToken ct);
    Task PostCommentAsync(string gid, string text, CancellationToken ct);
}
