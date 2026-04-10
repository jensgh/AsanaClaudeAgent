using System.Text.RegularExpressions;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public partial class WorkerService : IWorkerService
{
    private readonly IClaudeProcessRunner _runner;
    private readonly ClaudeSettings _settings;
    private readonly ILogger<WorkerService> _logger;

    private const string WorkerPromptTemplate = """
        You are working on an Asana task in the Travelshift monorepo. You are already on branch '{0}'.

        Task: {1}
        Description:
        {2}
        Asana link: {3}

        Classifier summary: {4}

        Instructions:
        1. Read the CLAUDE.md and relevant AGENTS.md files for coding conventions
        2. Implement the changes described in the task
        3. Run any relevant linters/tests (dotnet build, dotnet test, npm run lint, npm run type-check, etc.)
        4. Commit your changes with a descriptive commit message
        5. Push the branch: git push -u origin {0}
        6. Create a PR using: gh pr create --title "<short title>" --body "<body with Asana link: {3}>" --base master
        7. Print the PR URL as the very last line of your output

        Do NOT merge the PR. Just create it for review.
        """;

    public WorkerService(
        IClaudeProcessRunner runner,
        IOptions<ClaudeSettings> settings,
        ILogger<WorkerService> logger)
    {
        _runner = runner;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<WorkResult> ExecuteAsync(
        AsanaTask task,
        ClassificationResult classification,
        string branchName,
        CancellationToken ct)
    {
        var prompt = string.Format(
            WorkerPromptTemplate,
            branchName,
            task.Name,
            task.Notes,
            task.PermalinkUrl,
            classification.Summary);

        var result = await _runner.RunAsync(new ClaudeProcessOptions
        {
            Prompt = prompt,
            WorkingDirectory = _settings.MonorepoPath,
            OutputFormat = "text",
            Model = _settings.WorkerModel,
            MaxBudgetUsd = _settings.MaxBudgetPerTask,
            DangerouslySkipPermissions = _settings.DangerouslySkipPermissions,
            Timeout = TimeSpan.FromMinutes(_settings.WorkerTimeoutMinutes)
        }, ct);

        if (result.TimedOut)
        {
            return new WorkResult
            {
                Success = false,
                ErrorMessage = "Worker timed out"
            };
        }

        if (result.ExitCode != 0)
        {
            return new WorkResult
            {
                Success = false,
                ErrorMessage = $"Worker exited with code {result.ExitCode}: {result.Stderr}"
            };
        }

        var prUrl = ExtractPrUrl(result.Stdout);
        if (prUrl is null)
        {
            _logger.LogWarning("Worker completed but no PR URL found in output for task {Gid}", task.Gid);
            return new WorkResult
            {
                Success = false,
                ErrorMessage = "Worker completed but no PR URL was found in output"
            };
        }

        _logger.LogInformation("Worker created PR for task {Gid}: {PrUrl}", task.Gid, prUrl);
        return new WorkResult
        {
            Success = true,
            PrUrl = prUrl
        };
    }

    private static string? ExtractPrUrl(string output)
    {
        var match = PrUrlRegex().Match(output);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"https://github\.com/[^\s""']+/pull/\d+")]
    private static partial Regex PrUrlRegex();
}
