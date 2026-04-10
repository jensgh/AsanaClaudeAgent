using System.Diagnostics;
using AsanaClaudeAgent.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public class GitService : IGitService
{
    private readonly string _repoPath;
    private readonly ILogger<GitService> _logger;

    public GitService(IOptions<ClaudeSettings> settings, ILogger<GitService> logger)
    {
        _repoPath = settings.Value.MonorepoPath;
        _logger = logger;
    }

    public async Task EnsureCleanWorkingTreeAsync(CancellationToken ct)
    {
        var result = await RunGitAsync("status --porcelain", ct);
        if (!string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                $"Monorepo working tree is not clean. Please commit or stash changes first.\n{result}");
        }
    }

    public async Task<string> CreateBranchAsync(string branchName, CancellationToken ct)
    {
        await RunGitAsync("fetch origin master", ct);
        await RunGitAsync($"checkout -b {branchName} origin/master", ct);
        _logger.LogInformation("Created and checked out branch {Branch}", branchName);
        return branchName;
    }

    public async Task CheckoutAsync(string branchName, CancellationToken ct)
    {
        await RunGitAsync($"checkout {branchName}", ct);
    }

    public async Task DeleteLocalBranchAsync(string branchName, CancellationToken ct)
    {
        try
        {
            await RunGitAsync("checkout master", ct);
            await RunGitAsync($"branch -D {branchName}", ct);
            _logger.LogInformation("Deleted local branch {Branch}", branchName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete branch {Branch}", branchName);
        }
    }

    private async Task<string> RunGitAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start git process");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments} failed (exit {process.ExitCode}): {stderr}");
        }

        return stdout;
    }
}
