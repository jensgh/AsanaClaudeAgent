using System.Diagnostics;
using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AsanaClaudeAgent.Services;

public class ClaudeProcessRunner : IClaudeProcessRunner
{
    private readonly ClaudeSettings _settings;
    private readonly ILogger<ClaudeProcessRunner> _logger;

    public ClaudeProcessRunner(IOptions<ClaudeSettings> settings, ILogger<ClaudeProcessRunner> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ClaudeProcessResult> RunAsync(ClaudeProcessOptions options, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _settings.ClaudeBinaryPath,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in BuildArguments(options))
        {
            psi.ArgumentList.Add(arg);
        }

        _logger.LogInformation("Running: {FileName} {Args} (timeout: {Timeout})",
            psi.FileName, string.Join(' ', psi.ArgumentList), options.Timeout);

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("Failed to start claude process");

        await process.StandardInput.WriteAsync(options.Prompt);
        process.StandardInput.Close();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(options.Timeout);

        // Read both streams concurrently to prevent pipe buffer deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            _logger.LogInformation("Claude exited with code {ExitCode}", process.ExitCode);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _logger.LogDebug("Claude stderr: {Stderr}", stderr[..Math.Min(stderr.Length, 500)]);
            }

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

    private static List<string> BuildArguments(ClaudeProcessOptions options)
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

        if (options.MaxCostUsd.HasValue)
        {
            args.Add("--max-cost");
            args.Add(options.MaxCostUsd.Value.ToString("F2"));
        }

        if (options.DangerouslySkipPermissions)
        {
            args.Add("--dangerously-skip-permissions");
        }

        if (options.AppendSystemPrompt is not null)
        {
            args.Add("--append-system-prompt");
            args.Add(options.AppendSystemPrompt);
        }

        return args;
    }
}
