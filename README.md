# RepoSum

Windows desktop tool for summarizing recent changes across multiple Azure DevOps repositories (commits, pull requests, and releases).

## Tech
- .NET 8
- WPF (MVVM)
- Clean architecture projects:
  - `src/RepoSum.Domain`
  - `src/RepoSum.Application`
  - `src/RepoSum.Infrastructure`
  - `src/RepoSum.UI`
- DI via `Microsoft.Extensions.Hosting`
- HTTP via `HttpClient` / `IHttpClientFactory`
- Retry via Polly
- Logging via Serilog
- Caching: in-memory + local file cache

## Run
From the repo root:
- Build: `dotnet build RepoSum.sln -c Release`
- Run: `dotnet run --project src/RepoSum.UI/RepoSum.UI.csproj`

## Azure DevOps setup
In the app, fill in:
- **Organization URL**: e.g. `https://dev.azure.com/<org>`
- **Project**: your Azure DevOps project name
- **PAT**: Personal Access Token

Typical PAT scopes for read-only usage:
- Code: Read
- Pull request: Read
- Release: Read (if you want release entries; Azure DevOps releases are project-level)

## Local data
RepoSum stores data under `%APPDATA%\RepoSum\`:
- `settings.json` (PAT is stored encrypted via Windows DPAPI)
- `readstate.json` (read/unread state per item)
- `cache\` (local cache entries)
- `logs\` (Serilog rolling logs)

## AI summary
The current implementation uses a lightweight heuristic summarizer (keywords like `feat`, `fix`, `breaking`) to:
- summarize commit messages / PR descriptions
- group items into **Feature**, **BugFix**, **BreakingChange**, **Other**

To plug in a real LLM later, implement `RepoSum.Application.Abstractions.IAiSummarizer` and register it instead of `HeuristicAiSummarizer`.
