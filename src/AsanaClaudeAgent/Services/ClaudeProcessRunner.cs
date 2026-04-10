using System.Diagnostics;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;

namespace AsanaClaudeAgent.Services;

public class ClaudeProcessRunner : IClaudeProcessRunner
{
    private readonly ILogger<ClaudeProcessRunner> _logger;

    public ClaudeProcessRunner(ILogger<ClaudeProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ClaudeProcessResult> RunAsync(ClaudeProcessOptions options, CancellationToken ct)
    {
        var args = BuildArguments(options);
        _logger.LogInformation("Running: claude {Args} (timeout: {Timeout})", args, options.Timeout);

        var psi = new ProcessStartInfo
        {
            FileName = options.WorkingDirectory.Contains("claude") ? "claude" : options.WorkingDirectory,
            Arguments = args,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Fix: FileName should always be the claude binary
        psi.FileName = "claude";

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start claude process");

        // Pipe prompt via stdin to avoid shell escaping issues
        await process.StandardInput.WriteAsync(options.Prompt);
        process.StandardInput.Close();

        // Read stdout and stderr concurrently to prevent deadlocks
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.Timeout);

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _logger.LogInformation("Claude exited with code {ExitCode}", process.ExitCode);
            if (!string.IsNullOrWhiteSpace(stderr))
                _logger.LogDebug("Claude stderr: {Stderr}", stderr[..Math.Min(stderr.Length, 500)]);

            return new ClaudeProcessResult
            {
                ExitCode = process.ExitCode,
                Stdout = stdout,
                Stderr = stderr
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Claude process timed out after {Timeout}", options.Timeout);
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new ClaudeProcessResult
            {
                ExitCode = -1,
                Stdout = "",
                Stderr = "Process timed out",
                TimedOut = true
            };
        }
    }

    private static string BuildArguments(ClaudeProcessOptions options)
    {
        var args = new List<string> { "--print", "--output-format", options.OutputFormat ?? "text" };

        if (options.Model is not null)
        {
            args.Add("--model");
            args.Add(options.Model);
        }

        if (options.AllowedTools is not null)
        {
            args.Add("--allowedTools");
            args.Add(options.AllowedTools);
        }

        if (options.MaxBudgetUsd.HasValue)
        {
            args.Add("--max-turns");
            args.Add("100");
        }

        if (options.DangerouslySkipPermissions)
            args.Add("--dangerously-skip-permissions");

        if (options.AppendSystemPrompt is not null)
        {
            args.Add("--append-system-prompt");
            args.Add($"\"{options.AppendSystemPrompt}\"");
        }

        return string.Join(' ', args);
    }
}
