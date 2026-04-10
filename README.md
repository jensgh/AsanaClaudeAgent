# AsanaClaudeAgent

A .NET console app that polls Asana for tasks assigned to you, classifies whether Claude Code can handle them, and if so spawns Claude Code to implement the changes, push to a branch, and open a PR.

```
Asana task ──> Classifier (claude -p) ──> Worker (claude -p) ──> GitHub PR
                    │                                                │
                    ▼                                                ▼
              Can't automate?                                 Comments on
              Comment on Asana                                Asana with PR link
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) (`npm install -g @anthropic-ai/claude-code`)
- [GitHub CLI](https://cli.github.com) (`gh`) — authenticated via `gh auth login`
- Git

## Running from Rider / Visual Studio

1. Open `AsanaClaudeAgent.sln` in Rider or Visual Studio.

2. Set environment variables in your run configuration:

   **Rider:** Run > Edit Configurations > Environment variables:
   ```
   ASANA_TOKEN=your-asana-pat
   ANTHROPIC_API_KEY=your-anthropic-key
   ```

   **Visual Studio:** Right-click project > Properties > Debug > Environment variables, or edit `launchSettings.json`:
   ```json
   {
     "profiles": {
       "AsanaClaudeAgent": {
         "commandName": "Project",
         "commandLineArgs": "--once",
         "environmentVariables": {
           "ASANA_TOKEN": "your-asana-pat",
           "ANTHROPIC_API_KEY": "your-anthropic-key"
         }
       }
     }
   }
   ```

3. Edit `src/AsanaClaudeAgent/appsettings.json` — set at minimum:
   ```json
   {
     "Asana": {
       "WorkspaceGid": "your-workspace-gid"
     },
     "Claude": {
       "MonorepoPath": "/path/to/your/monorepo"
     }
   }
   ```
   You can find your workspace GID in any Asana URL: `https://app.asana.com/0/<workspace_gid>/...`

4. Run the project. Pass `--once` as a program argument for a single cycle, or omit it for continuous polling.

5. To test without actually running the worker, set `App:DryRun` to `true` in appsettings — this classifies tasks but skips implementation.

## Running from the command line

```bash
export ASANA_TOKEN="your-asana-pat"
export ANTHROPIC_API_KEY="your-anthropic-key"

# Single run
dotnet run --project src/AsanaClaudeAgent -- --once

# Continuous polling (every 5 minutes by default)
dotnet run --project src/AsanaClaudeAgent
```

## Running with Docker

The Docker image bundles the .NET runtime, Claude Code CLI, GitHub CLI, and git.

### Option 1: docker compose (recommended)

1. Create a `.env` file in the project root:
   ```env
   ASANA_TOKEN=your-asana-pat
   ANTHROPIC_API_KEY=your-anthropic-key
   GH_TOKEN=your-github-token
   ASANA_WORKSPACE_GID=your-workspace-gid
   ```

2. Update the monorepo volume path in `docker-compose.yml` if needed (defaults to `/home/jens/repos/monorepo`).

3. Run:
   ```bash
   docker compose up -d
   ```

   This runs continuously with `restart: unless-stopped`. State is persisted in `./state/`.

### Option 2: docker run (single execution)

```bash
docker build -t asana-claude-agent .

docker run --rm \
  -e ASANA_TOKEN="your-asana-pat" \
  -e ANTHROPIC_API_KEY="your-anthropic-key" \
  -e GH_TOKEN="your-github-token" \
  -e Asana__WorkspaceGid="your-workspace-gid" \
  -e Claude__MonorepoPath=/repo \
  -e Claude__DangerouslySkipPermissions=true \
  -e App__StateFilePath=/app/state/state.json \
  -v /path/to/your/monorepo:/repo \
  -v $(pwd)/state:/app/state \
  asana-claude-agent --once
```

### Option 3: Scheduled via cron

Add to your crontab to run every 30 minutes:

```cron
*/30 * * * * docker run --rm --env-file /path/to/.env -v /path/to/monorepo:/repo -v /path/to/state:/app/state asana-claude-agent --once
```

## Configuration

All settings can be set in `appsettings.json` or overridden via environment variables (e.g., `Asana__WorkspaceGid`).

### Asana

| Setting | Env var | Default | Description |
|---|---|---|---|
| `Asana:Token` | `ASANA_TOKEN` | — | Asana Personal Access Token (required) |
| `Asana:WorkspaceGid` | `Asana__WorkspaceGid` | — | Asana workspace GID (required) |
| `Asana:AssigneeGid` | `Asana__AssigneeGid` | `me` | Whose tasks to fetch |

### Claude

| Setting | Default | Description |
|---|---|---|
| `Claude:MonorepoPath` | `/home/jens/repos/monorepo` | Path to the git repo Claude Code works on |
| `Claude:ClaudeBinaryPath` | `claude` | Path to the Claude Code CLI binary |
| `Claude:ClassificationModel` | `sonnet` | Model for classification (lighter/faster) |
| `Claude:WorkerModel` | `null` (default) | Model for implementation work |
| `Claude:MaxBudgetPerTask` | `null` | Max cost in USD per task (`--max-cost`) |
| `Claude:ClassificationTimeoutSeconds` | `120` | Timeout for classification step |
| `Claude:WorkerTimeoutMinutes` | `30` | Timeout for worker step |
| `Claude:DangerouslySkipPermissions` | `false` | Skip permission prompts (required for Docker) |

### App

| Setting | Default | Description |
|---|---|---|
| `App:RunOnce` | `false` | Run a single cycle and exit (also set via `--once` flag) |
| `App:PollingIntervalMinutes` | `5` | Minutes between polling cycles |
| `App:CommentOnAsana` | `true` | Post comments on Asana tasks with results |
| `App:FetchAllTasks` | `false` | Fetch all incomplete tasks (ignores last run time) |
| `App:MaxRetries` | `1` | How many times to retry a failed task |
| `App:DryRun` | `false` | Classify tasks but don't run the worker |

## How it works

1. **Poll** — Fetches incomplete Asana tasks assigned to you, modified since the last run
2. **Classify** — For each task, runs `claude --print --output-format json` in the monorepo directory. Claude reads the repo's `CLAUDE.md` and project structure to assess whether the task is implementable
3. **Work** — For tasks classified as workable, creates a branch (`claude/asana-<gid>-<slug>`), runs Claude Code to implement the changes, commit, push, and open a PR via `gh pr create`
4. **Report** — Comments on the Asana task with the PR link (or a reason why it couldn't be automated)

State is persisted in `state.json` so the app knows which tasks have already been processed. Lock files prevent duplicate processing if multiple instances run.

## Project structure

```
src/AsanaClaudeAgent/
  Program.cs                          # Host builder, DI, startup validation
  Configuration/
    AsanaSettings.cs                  # Asana API config
    ClaudeSettings.cs                 # Claude CLI config
    AppSettings.cs                    # App behavior config
    StartupValidator.cs               # Validates all required config on startup
  Services/
    AsanaService.cs                   # Asana REST API (list tasks, get task, post comment)
    ClaudeProcessRunner.cs            # Spawns claude CLI via Process with stdin piping
    ClassifierService.cs              # Task classification via Claude
    WorkerService.cs                  # Task implementation via Claude
    GitService.cs                     # Branch creation and cleanup
    StateService.cs                   # Persists run state and task locks
    TaskOrchestrator.cs               # Main orchestration loop
  Hosting/
    OrchestratorHostedService.cs      # BackgroundService (single run or polling timer)
  Models/                             # DTOs, state models, status constants
```
