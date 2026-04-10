using System.Text.RegularExpressions;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public partial class TaskOrchestrator : ITaskOrchestrator
{
    private readonly IAsanaService _asana;
    private readonly IClassifierService _classifier;
    private readonly IWorkerService _worker;
    private readonly IGitService _git;
    private readonly IStateService _state;
    private readonly AppSettings _appSettings;
    private readonly ILogger<TaskOrchestrator> _logger;

    public TaskOrchestrator(
        IAsanaService asana,
        IClassifierService classifier,
        IWorkerService worker,
        IGitService git,
        IStateService state,
        IOptions<AppSettings> appSettings,
        ILogger<TaskOrchestrator> logger)
    {
        _asana = asana;
        _classifier = classifier;
        _worker = worker;
        _git = git;
        _state = state;
        _appSettings = appSettings.Value;
        _logger = logger;
    }

    public async Task RunCycleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting orchestration cycle");
        var appState = await _state.LoadAsync(ct);

        DateTime? modifiedSince = _appSettings.FetchAllTasks
            ? null
            : appState.LastRunUtc ?? DateTime.UtcNow.AddDays(-7);

        var tasks = await _asana.GetAssignedIncompleteTasksAsync(modifiedSince, ct);

        foreach (var task in tasks)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (ShouldSkipTask(appState, task.Gid))
            {
                _logger.LogDebug("Skipping task {Gid} ({Name}) — already processed", task.Gid, task.Name);
                continue;
            }

            if (await _state.IsTaskLockedAsync(task.Gid, ct))
            {
                _logger.LogDebug("Skipping task {Gid} — locked by another process", task.Gid);
                continue;
            }

            await ProcessTaskAsync(appState, task, ct);
        }

        appState.LastRunUtc = DateTime.UtcNow;
        await _state.SaveAsync(appState, ct);
        _logger.LogInformation("Orchestration cycle complete");
    }

    private async Task ProcessTaskAsync(AppState appState, AsanaTask task, CancellationToken ct)
    {
        _logger.LogInformation("Processing task {Gid}: {Name}", task.Gid, task.Name);
        await _state.LockTaskAsync(task.Gid, ct);

        try
        {
            // List endpoint already includes notes via opt_fields, but fetch full details
            // to guarantee we have the complete description
            var fullTask = await _asana.GetTaskAsync(task.Gid, ct);

            UpdateTaskState(appState, fullTask, AgentTaskStatus.Classifying);

            var classification = await _classifier.ClassifyAsync(fullTask, ct);

            if (!classification.CanWork)
            {
                _logger.LogInformation("Task {Gid} classified as not workable: {Reason}", task.Gid, classification.Reason);
                UpdateTaskState(appState, fullTask, AgentTaskStatus.Skipped, failureReason: classification.Reason);
                await _state.SaveAsync(appState, ct);
                await CommentOnTaskAsync(task.Gid,
                    $"Evaluated this task and determined it cannot be automated at this time.\n\nReason: {classification.Reason}", ct);
                return;
            }

            if (_appSettings.DryRun)
            {
                _logger.LogInformation("DRY RUN: Would work on task {Gid} — {Summary} (complexity: {Complexity})",
                    task.Gid, classification.Summary, classification.EstimatedComplexity);
                UpdateTaskState(appState, fullTask, AgentTaskStatus.Skipped, failureReason: "Dry run");
                await _state.SaveAsync(appState, ct);
                return;
            }

            var branchName = GenerateBranchName(fullTask);
            await _git.EnsureCleanWorkingTreeAsync(ct);
            await _git.CreateBranchAsync(branchName, ct);

            UpdateTaskState(appState, fullTask, AgentTaskStatus.Working, branchName: branchName);
            await _state.SaveAsync(appState, ct);

            var workResult = await _worker.ExecuteAsync(fullTask, classification, branchName, ct);

            if (workResult.Success)
            {
                _logger.LogInformation("Task {Gid} completed. PR: {PrUrl}", task.Gid, workResult.PrUrl);
                UpdateTaskState(appState, fullTask, AgentTaskStatus.Completed, branchName: branchName, prUrl: workResult.PrUrl);
                await CommentOnTaskAsync(task.Gid, $"Created a PR for this task: {workResult.PrUrl}", ct);
                await _git.CheckoutAsync("master", ct);
                await _state.SaveAsync(appState, ct);
                return;
            }

            _logger.LogWarning("Task {Gid} failed: {Error}", task.Gid, workResult.ErrorMessage);
            var taskState = appState.Tasks.GetValueOrDefault(task.Gid);
            var retryCount = (taskState?.RetryCount ?? 0) + 1;

            if (retryCount <= _appSettings.MaxRetries)
            {
                UpdateTaskState(appState, fullTask, AgentTaskStatus.Pending, failureReason: workResult.ErrorMessage);
                appState.Tasks[task.Gid].RetryCount = retryCount;
            }
            else
            {
                UpdateTaskState(appState, fullTask, AgentTaskStatus.Failed, branchName: branchName, failureReason: workResult.ErrorMessage);
                await CommentOnTaskAsync(task.Gid,
                    $"Attempted to work on this task but failed.\n\nError: {workResult.ErrorMessage}", ct);
            }

            await _git.DeleteLocalBranchAsync(branchName, ct);
            await _state.SaveAsync(appState, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing task {Gid}", task.Gid);
            UpdateTaskState(appState, task, AgentTaskStatus.Failed, failureReason: ex.Message);
            await _state.SaveAsync(appState, ct);
        }
        finally
        {
            await _state.UnlockTaskAsync(task.Gid, ct);
        }
    }

    private async Task CommentOnTaskAsync(string gid, string message, CancellationToken ct)
    {
        if (!_appSettings.CommentOnAsana)
        {
            return;
        }

        await _asana.PostCommentAsync(gid, $"[Claude Code Agent] {message}", ct);
    }

    private static bool ShouldSkipTask(AppState appState, string gid)
    {
        if (!appState.Tasks.TryGetValue(gid, out var taskState))
        {
            return false;
        }

        return taskState.Status is AgentTaskStatus.Completed or AgentTaskStatus.Skipped or AgentTaskStatus.Working;
    }

    private static void UpdateTaskState(
        AppState appState,
        AsanaTask task,
        string status,
        string? branchName = null,
        string? prUrl = null,
        string? failureReason = null)
    {
        if (!appState.Tasks.TryGetValue(task.Gid, out var state))
        {
            state = new TaskState { Gid = task.Gid };
            appState.Tasks[task.Gid] = state;
        }

        state.Name = task.Name;
        state.Status = status;
        state.LastUpdatedUtc = DateTime.UtcNow;

        if (branchName is not null)
        {
            state.BranchName = branchName;
        }

        if (prUrl is not null)
        {
            state.PrUrl = prUrl;
        }

        if (failureReason is not null)
        {
            state.FailureReason = failureReason;
        }
    }

    private static string GenerateBranchName(AsanaTask task)
    {
        var slug = SlugRegex().Replace(task.Name.ToLowerInvariant(), "-");
        slug = MultiDashRegex().Replace(slug, "-").Trim('-');
        if (slug.Length > 40)
        {
            slug = slug[..40].TrimEnd('-');
        }

        return $"claude/asana-{task.Gid}-{slug}";
    }

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDashRegex();
}
