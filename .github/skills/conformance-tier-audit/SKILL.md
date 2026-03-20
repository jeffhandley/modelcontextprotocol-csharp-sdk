---
name: conformance-tier-audit
description: >-
  Run an MCP SDK conformance tier audit for the C# MCP SDK. Starts the conformance server,
  pre-builds the conformance client, clones the conformance repo, and delegates
  to its mcp-sdk-tier-audit skill for all evaluation and reporting.
argument-hint: '[--port <port>] [--framework <tfm>] [--branch <branch>]'
compatibility: >-
  Requires: Node.js >= 20, .NET SDK (net9.0+),
  and internet access to clone the github.com/modelcontextprotocol/conformance repo.
---

# Conformance Tier Audit — C# MCP SDK

This skill orchestrates a tier audit by preparing the C# SDK's conformance server and client, then delegating to the `mcp-sdk-tier-audit` skill from the `modelcontextprotocol/conformance` repo for all tier evaluation, scoring, and report generation.

## Step 0: Pre-flight Checks

### 0a. Parse arguments

Extract optional overrides from the user's input (all have defaults):

- **port** (default: `3001`): Port for the conformance server
- **framework** (default: `net9.0`): Target framework for `dotnet run`. Available: `net8.0`, `net9.0`, `net10.0`
- **branch** (default: current branch): Git branch for GitHub API checks. Derive from: `git rev-parse --abbrev-ref HEAD`

## Step 1: Start the Conformance Server

Start the C# SDK's conformance server as a detached background process from the SDK root (the cwd):

```
dotnet run --project tests/ModelContextProtocol.ConformanceServer --framework <framework> -p:NuGetAudit=false -- --urls http://localhost:<port>
```

Use `mode: async, detach: true` so the server persists.

Wait a few seconds, then verify it's reachable:

**PowerShell** (Windows):
```powershell
curl -sf http://localhost:<port> -o NUL -w '%{http_code}'
```

**Bash** (Linux/macOS):
```bash
curl -sf http://localhost:<port> -o /dev/null -w '%{http_code}'
```

A `400` response is expected and means the server is running (it rejects plain GET requests).

If the server fails to start, check stderr for build errors. Common issues:
- **NU1903 (NuGet vulnerability)**: The `-p:NuGetAudit=false` flag should suppress this.
- **Multiple TFMs**: The `--framework` flag is required because the project multi-targets.

## Step 2: Pre-build the Conformance Client

**CRITICAL**: Pre-build the conformance client before the audit runs tests. The conformance runner executes 26 scenarios in parallel — without pre-building, each `dotnet run` invocation triggers a full compilation, causing massive CPU contention and 30-second timeouts.

**PowerShell** (Windows):
```powershell
dotnet build tests\ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --nologo -v q
```

**Bash** (Linux/macOS):
```bash
dotnet build tests/ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --nologo -v q
```

## Step 3: Clone the Conformance Repo

Clone the conformance repo to a temporary directory and build it:

**PowerShell** (Windows):
```powershell
$conformanceDir = Join-Path $env:TEMP "mcp-conformance-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
git clone --depth 1 https://github.com/modelcontextprotocol/conformance.git $conformanceDir
cd $conformanceDir
npm install --silent
npm run build
```

**Bash** (Linux/macOS):
```bash
conformanceDir=$(mktemp -d)
git clone --depth 1 https://github.com/modelcontextprotocol/conformance.git "$conformanceDir"
cd "$conformanceDir"
npm install --silent
npm run build
```

Store the conformance directory path for cleanup later.

## Step 4: Delegate to the Conformance Repo's Audit Skill

Read the `mcp-sdk-tier-audit` skill from the cloned conformance repo:

```
$conformanceDir/.claude/skills/mcp-sdk-tier-audit/SKILL.md
```

Follow that skill's instructions end-to-end, providing these inputs:

| Input | Value |
|-------|-------|
| `--repo` | `modelcontextprotocol/csharp-sdk` |
| `--branch` | `<branch>` |
| `--conformance-server-url` | `http://localhost:<port>` |
| `--client-cmd` | See platform-specific commands below |

Where `<sdk-path>` is the absolute path to the SDK checkout (the original cwd, not the conformance temp dir).

**PowerShell** (Windows) — use backslashes and `%MCP_CONFORMANCE_SCENARIO%`:
```
dotnet run --project <sdk-path>\tests\ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --no-build -- %MCP_CONFORMANCE_SCENARIO%
```

**Bash** (Linux/macOS) — use forward slashes and `$MCP_CONFORMANCE_SCENARIO`:
```
dotnet run --project <sdk-path>/tests/ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --no-build -- $MCP_CONFORMANCE_SCENARIO
```

### Windows Quoting Note

The Windows `--client-cmd` uses `%MCP_CONFORMANCE_SCENARIO%` — the conformance runner sets this as an environment variable and spawns the client with `shell: true`, so the Windows shell expands it. If the tier-check CLI reports 0/N client scenarios with 0 checks passed AND 0 checks failed, the command is being parsed incorrectly due to the CLI wrapping it in single quotes (which don't work on Windows cmd.exe). In that case, run the client suite directly:

**PowerShell** (Windows):
```powershell
cd $conformanceDir
node dist/index.js client `
  --command "dotnet run --project <sdk-path>\tests\ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --no-build -- %MCP_CONFORMANCE_SCENARIO%" `
  --suite all `
  -o <output-dir>
```

**Bash** (Linux/macOS):
```bash
cd "$conformanceDir"
node dist/index.js client \
  --command "dotnet run --project <sdk-path>/tests/ModelContextProtocol.ConformanceClient --framework <framework> -p:NuGetAudit=false --no-build -- $MCP_CONFORMANCE_SCENARIO" \
  --suite all \
  -o <output-dir>
```

### Output Location Override

The conformance skill may specify its own output location. Override it: write all output files to `artifacts/skill-output/` at the SDK repo root. Create the directory if it doesn't exist. The `artifacts/` directory is already gitignored.

## Step 5: Cleanup

Stop the conformance server process. Remove the temporary conformance repo directory:

**PowerShell** (Windows):
```powershell
Remove-Item -Recurse -Force $conformanceDir
```

**Bash** (Linux/macOS):
```bash
rm -rf "$conformanceDir"
```

## Usage Examples

```
# Default settings (port 3001, net9.0, current branch)
/conformance-tier-audit

# Custom port and framework
/conformance-tier-audit --port 3003 --framework net10.0

# Specific branch for GitHub API checks
/conformance-tier-audit --branch main
```
