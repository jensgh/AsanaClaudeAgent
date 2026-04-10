using System.Diagnostics;

namespace AsanaClaudeAgent.Configuration;

public static class StartupValidator
{
    public static void Validate(AsanaSettings asana, ClaudeSettings claude, AppSettings app)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(asana.Token))
        {
            errors.Add("Asana token is not configured. Set 'Asana:Token' in appsettings.json or the ASANA_TOKEN environment variable.");
        }

        if (string.IsNullOrWhiteSpace(asana.WorkspaceGid))
        {
            errors.Add("Asana workspace GID is not configured. Set 'Asana:WorkspaceGid' in appsettings.json or the Asana__WorkspaceGid environment variable. "
                        + "You can find it in the Asana URL: https://app.asana.com/0/<workspace_gid>/...");
        }

        if (string.IsNullOrWhiteSpace(claude.MonorepoPath))
        {
            errors.Add("Monorepo path is not configured. Set 'Claude:MonorepoPath' in appsettings.json.");
        }
        else if (!Directory.Exists(claude.MonorepoPath))
        {
            errors.Add($"Monorepo path does not exist: '{claude.MonorepoPath}'. Set 'Claude:MonorepoPath' to the correct path.");
        }
        else if (!Directory.Exists(Path.Combine(claude.MonorepoPath, ".git")))
        {
            errors.Add($"Monorepo path is not a git repository: '{claude.MonorepoPath}'. It must be the root of a git repo.");
        }

        if (!IsToolAvailable(claude.ClaudeBinaryPath))
        {
            errors.Add($"'{claude.ClaudeBinaryPath}' CLI is not installed or not in PATH. Install it with: npm install -g @anthropic-ai/claude-code");
        }

        if (!IsToolAvailable("git"))
        {
            errors.Add("'git' is not installed or not in PATH.");
        }

        if (!IsToolAvailable("gh"))
        {
            errors.Add("'gh' (GitHub CLI) is not installed or not in PATH. Install it from https://cli.github.com. "
                        + "It is needed to create PRs. Also ensure you are authenticated: gh auth login");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
        {
            errors.Add("ANTHROPIC_API_KEY environment variable is not set. Claude Code CLI requires this to authenticate with the Anthropic API.");
        }

        if (app.PollingIntervalMinutes < 1)
        {
            errors.Add("'App:PollingIntervalMinutes' must be at least 1.");
        }

        if (claude.ClassificationTimeoutSeconds < 10)
        {
            errors.Add("'Claude:ClassificationTimeoutSeconds' must be at least 10.");
        }

        if (claude.WorkerTimeoutMinutes < 1)
        {
            errors.Add("'Claude:WorkerTimeoutMinutes' must be at least 1.");
        }

        if (errors.Count == 0)
        {
            return;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("\n==========================================");
        Console.Error.WriteLine(" CONFIGURATION ERRORS — Cannot start");
        Console.Error.WriteLine("==========================================\n");
        Console.ResetColor();

        for (var i = 0; i < errors.Count; i++)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.Write($"  {i + 1}. ");
            Console.ResetColor();
            Console.Error.WriteLine(errors[i]);
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("Fix the above issues and try again.");
        Console.Error.WriteLine("Configuration can be set in appsettings.json or via environment variables (e.g., Asana__Token).\n");

        Environment.Exit(1);
    }

    private static bool IsToolAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
