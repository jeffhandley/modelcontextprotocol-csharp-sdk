---
name: conformance-tier-audit
description: >-
  Run an MCP SDK conformance tier audit for the C# MCP SDK. Starts the conformance server,
  pre-builds the conformance client, clones the conformance repo, and delegates
  to its mcp-sdk-tier-audit skill for all evaluation and reporting.
argument-hint: '[--repo <owner/repo>] [--port <port>] [--framework <tfm>] [--scope <scope>]'
compatibility: >-
  Requires: Node.js >= 20, .NET SDK (net9.0+),
  and internet access to clone the github.com/modelcontextprotocol/conformance repo.
---

# Conformance Tier Audit — C# MCP SDK

This skill orchestrates a tier audit by preparing the C# SDK's conformance server and client, then delegating to the `mcp-sdk-tier-audit` skill from the `modelcontextprotocol/conformance` repo for all tier evaluation, scoring, and report generation.

## Step 0: Pre-flight Checks

### 0a. Parse arguments

Extract optional overrides from the user's input (all have defaults):

- **repo** (default: current repository): Repository to use for issue triage, labels, and repo-health checks
- **port** (default: `3001`): Port for the conformance server
- **framework** (default: `net9.0`): Target framework for `dotnet run`. Available: `net8.0`, `net9.0`, `net10.0`
- **scope** (default: `full`): Preset subset to run. Supported values: `full`, `tests`, `server`, `client`, `triage`, `repo`

### 0b. Resolve the effective component set

Expand `scope` as follows:

| Scope | Components |
|-------|------------|
| `full` | `server`, `client`, `issue-triage`, `labels`, `docs`, `policies`, `releases` |
| `tests` | `server`, `client` |
| `server` | `server` |
| `client` | `client` |
| `triage` | `issue-triage` |
| `repo` | `issue-triage`, `labels`, `docs`, `policies`, `releases` |

Normalize the selection into booleans such as `runServer`, `runClient`, `runIssueTriage`, `runLabels`, `runDocs`, `runPolicies`, and `runReleases`.

If the effective component set does **not** cover the full tier rubric, treat the run as a **partial audit**:

- run only the selected checks
- skip unrelated setup and evaluation work
- keep the report focused on the chosen sections
- do **not** claim a final Tier 1/2/3 result unless all tier inputs were actually evaluated
- explicitly list which sections were intentionally skipped

### 0c. Keep the safe-output session alive during long runs

When this skill is running inside a GitHub Agentic Workflow and the `safeoutputs` tools are available, send a brief `noop` progress update immediately after pre-flight, then again after each major milestone:

- server process started and verified
- client pre-build finished
- server conformance finished
- client conformance finished
- repository-health / triage evaluation finished

This audit often runs for more than an hour. These periodic `noop` calls act as keepalive heartbeats so the streamable safe-output session does not expire before the final issue is created. In this workflow, `noop.report-as-issue: false` is configured, so these heartbeat updates will not open issues.

### 0d. Prepare issue-event reads for triage timing

When issue-triage or repository-health checks are in scope, use the resolved `repo` value from `--repo` and compute Tier 1 first-label timing from that repo's public GitHub issue-events API. Use `bash` plus the public REST API to fetch issue events for each relevant issue, for example:

```bash
curl -fsSL \
  -H "Accept: application/vnd.github+json" \
  "https://api.github.com/repos/<owner/repo>/issues/<issue-number>/events?per_page=100"
```

From that response, identify the earliest `labeled` event timestamp for the issue and compare it to the issue's `created_at` timestamp to measure the Tier 1 two-business-day triage SLA.

If the public API is rate-limited:

1. inspect `Retry-After` and `X-RateLimit-Reset`
2. pause until the reset window
3. if `safeoutputs.noop` is available, send a brief keepalive every 30-60 seconds while waiting, such as `Waiting for GitHub issue-events rate limit reset`
4. retry the request after the wait

If the needed event history is still unavailable after this backoff/retry flow, assume Tier 1 triage is achieved but show it with a warning signal such as `⚠` and report the exact technical limitation that prevented precise timing.

## Step 1: Start the Conformance Server

Only do this step when `runServer` is true.

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

Only do this step when `runClient` is true.

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

Do this step whenever you need the conformance CLI or the `mcp-sdk-tier-audit` reference skill — typically for `server`, `client`, `issue-triage`, `labels`, or `releases`.

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

## Step 4: Run the Requested Audit Components

Read the `mcp-sdk-tier-audit` skill from the cloned conformance repo:

```
$conformanceDir/.claude/skills/mcp-sdk-tier-audit/SKILL.md
```

### 4a. Full audit

If the effective component set is the full default set, follow that skill's instructions end-to-end, providing these inputs:

| Input | Value |
|-------|-------|
| `--repo` | `<owner/repo>` |
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

### 4b. Partial audit

If the run selects only a subset of components, do **not** execute the full end-to-end tier workflow. Instead, run just the relevant pieces below and produce a targeted report.

#### Server conformance only

When `runServer` is true, run the server suite directly:

```bash
cd "$conformanceDir"
node dist/index.js server --url http://localhost:<port> --suite all -o <output-dir>/server-results
```

#### Client conformance only

When `runClient` is true, run the client suite directly:

```bash
cd "$conformanceDir"
node dist/index.js client --command "<client-cmd>" --suite all -o <output-dir>/client-results
```

Use the same `<client-cmd>` shown above, including `--no-build` and the scenario environment variable expansion.

#### Issue triage, labels, and repository-health signals

When any of `runIssueTriage`, `runLabels`, `runPolicies`, or `runReleases` is true, use the deterministic `tier-check` CLI without conformance execution where it is sufficient, and supplement it with direct public GitHub issue-events reads when you need exact first-label timing:

```bash
cd "$conformanceDir"
npm run --silent tier-check -- --repo <owner/repo> --skip-conformance --output json
```

Use these narrower commands when only a single section is needed:

```bash
# Issue triage only
npm run --silent tier-check -- triage --repo <owner/repo> --days 30

# Labels only
npm run --silent tier-check -- labels --repo <owner/repo>
```

Do **not** mark Tier 1 triage as uncertain merely because the workflow is running from a fork or because `--repo` points at a different repository than the current checkout.

If `tier-check` does not emit enough timing detail for the Tier 1 SLA, use public GitHub REST reads against the selected repo's issue-events endpoint to determine when the first triage label was applied, and use those timestamps in the report. If those reads hit rate limits, wait and retry while sending `noop` keepalives as described above. If the technical limitation persists after retry, report Tier 1 triage as achieved with a warning indicator rather than leaving it uncertain.

#### Documentation and policy evaluation

When `runDocs` or `runPolicies` is true, evaluate only those sections from the local checkout. Reuse the documentation and policy evaluation guidance from `mcp-sdk-tier-audit`, but skip them entirely when they are not selected.

### 4c. Reporting rules for partial runs

For a partial audit:

- summarize only the requested components
- omit or mark as intentionally skipped any sections that were not selected
- use a scope-specific title such as `Client Conformance`, `Server Conformance`, `Issue Triage`, or `Repository Health`
- do not state a final tier unless the run covered all inputs needed for a tier decision
- if safe-output tools are available, send a short `noop` milestone update before and after the partial audit so the session remains active

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

Use `artifacts/skill-output/step-summary.md` as the canonical final report file. Populate that file first, then:

- call `create_issue` with the same markdown body when the workflow should publish an issue
- optionally append the file to `$GITHUB_STEP_SUMMARY` if it is writable


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

# Only run client conformance
/conformance-tier-audit --scope client

# Only perform issue triage
/conformance-tier-audit --scope triage

```
