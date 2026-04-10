using System.Text.Json;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public class ClassifierService : IClassifierService
{
    private readonly IClaudeProcessRunner _runner;
    private readonly ClaudeSettings _settings;
    private readonly ILogger<ClassifierService> _logger;

    private const string ClassificationPrompt = """
        You are evaluating whether an Asana task can be implemented by an AI coding agent (Claude Code) working on a large travel marketplace monorepo.

        Task: {0}
        Description:
        {1}
        Link: {2}

        Assess whether this task:
        1. Is a code change that can be implemented by reading the task description alone (no ambiguous requirements, no need for human design decisions)
        2. Is scoped enough to be done in a single PR
        3. Does not require access to external systems, credentials, or manual testing steps that cannot be automated
        4. Has enough detail to implement without further clarification

        Respond ONLY with valid JSON matching this exact schema:
        {{"can_work": bool, "reason": "string", "summary": "string", "estimated_complexity": "low|medium|high"}}

        Be conservative — if in doubt, set can_work to false.
        """;

    public ClassifierService(
        IClaudeProcessRunner runner,
        IOptions<ClaudeSettings> settings,
        ILogger<ClassifierService> logger)
    {
        _runner = runner;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(AsanaTask task, CancellationToken ct)
    {
        var prompt = string.Format(ClassificationPrompt, task.Name, task.Notes, task.PermalinkUrl);

        var result = await _runner.RunAsync(new ClaudeProcessOptions
        {
            Prompt = prompt,
            WorkingDirectory = _settings.MonorepoPath,
            OutputFormat = "json",
            Model = _settings.ClassificationModel,
            DangerouslySkipPermissions = _settings.DangerouslySkipPermissions,
            Timeout = TimeSpan.FromSeconds(_settings.ClassificationTimeoutSeconds)
        }, ct);

        if (result.ExitCode != 0 || result.TimedOut)
        {
            _logger.LogWarning("Classification failed for task {Gid}: exit={ExitCode}, timeout={TimedOut}, stderr={Stderr}",
                task.Gid, result.ExitCode, result.TimedOut, result.Stderr);
            return new ClassificationResult
            {
                CanWork = false,
                Reason = result.TimedOut ? "Classification timed out" : $"Classification process failed: {result.Stderr}",
                Summary = task.Name
            };
        }

        try
        {
            var classification = JsonSerializer.Deserialize<ClassificationResult>(result.Stdout);
            if (classification is null)
                throw new JsonException("Deserialized to null");

            _logger.LogInformation("Task {Gid} ({Name}): canWork={CanWork}, complexity={Complexity}, reason={Reason}",
                task.Gid, task.Name, classification.CanWork, classification.EstimatedComplexity, classification.Reason);

            return classification;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse classification JSON for task {Gid}. Raw output: {Output}",
                task.Gid, result.Stdout[..Math.Min(result.Stdout.Length, 500)]);
            return new ClassificationResult
            {
                CanWork = false,
                Reason = "Failed to parse classification output",
                Summary = task.Name
            };
        }
    }
}
