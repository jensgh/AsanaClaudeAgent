# AsanaClaudeAgent

Standalone .NET console app that polls Asana for tasks, classifies them via Claude Code, and spawns Claude Code to implement + create PRs.

## Build & Run

```bash
dotnet build
dotnet run --project src/AsanaClaudeAgent          # polling mode
dotnet run --project src/AsanaClaudeAgent -- --once # single run
```

## Configuration

Settings via `appsettings.json` or environment variables (e.g., `Asana__Token`, `Claude__MonorepoPath`).

Three secrets required as env vars: `ASANA_TOKEN`, `ANTHROPIC_API_KEY`, `GH_TOKEN`.

## Structure

- `Configuration/` — Settings POCOs bound from appsettings
- `Services/` — All business logic (Asana API, Claude CLI runner, Git, Classifier, Worker, Orchestrator)
- `Hosting/` — BackgroundService that drives the orchestrator
- `Models/` — DTOs for Asana API, classification results, app state
