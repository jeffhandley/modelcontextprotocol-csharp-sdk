---
if: (!github.event.repository.fork) || github.event_name == 'workflow_dispatch'

permissions:
  contents: read
  issues: read
  pull-requests: read

# Allow only one audit run at a time. Queue the next run instead of canceling the current one.
concurrency:
  group: "conformance-tier-audit"
  cancel-in-progress: false

runtimes:
  dotnet:
    version: "10.0"
    action-version: "v5"
  node:
    version: "24"

network:
  allowed:
    - defaults
    - node
    - dotnet

tools:
  bash: true
  github:
    toolsets: [default]
    min-integrity: none

safe-outputs:
  noop:
    max: 60
    report-as-issue: false
  create-issue:
    title-prefix: "C# SDK Conformance Audit: "
    labels: [automation]
    max: 1
    close-older-issues: true

post-steps:
  - name: Publish conformance report to workflow summary
    if: always()
    shell: bash
    run: |
      report_path="${GITHUB_WORKSPACE}/artifacts/skill-output/step-summary.md"
      if [ -f "$report_path" ]; then
        printf '\n' >> "$GITHUB_STEP_SUMMARY"
        cat "$report_path" >> "$GITHUB_STEP_SUMMARY"
      fi

timeout-minutes: 240

on:
  schedule:
    - cron: "weekly on thursday around 9am utc-7"
  workflow_dispatch:
    inputs:
      audit-scope:
        description: 'Audit preset to run'
        required: false
        type: choice
        default: 'full'
        options:
          - full
          - tests
          - server
          - client
          - triage
          - repo
      repo:
        description: 'Repository to evaluate for repo-health checks (owner/repo)'
        required: false
        type: string
        default: 'modelcontextprotocol/csharp-sdk'
      conformance-repo:
        description: 'Conformance repo to clone (org/repo)'
        required: false
        type: string
        default: 'modelcontextprotocol/conformance'
      conformance-branch:
        description: 'Conformance repo branch to clone'
        required: false
        type: string
        default: 'main'

  # ###############################################################
  # Override the COPILOT_GITHUB_TOKEN secret usage for the workflow
  # with a randomly-selected token from a pool of secrets.
  #
  # As soon as organization-level billing is offered for Agentic
  # Workflows, this stop-gap approach will be removed.
  #
  # See: /.github/actions/select-copilot-pat/README.md
  # ###############################################################

  # Add the pre-activation step of selecting a random PAT from the supplied secrets
  steps:
    - uses: actions/checkout@de0fac2e4500dabe0009e67214ff5f5447ce83dd # v6.0.2
      name: Checkout the select-copilot-pat action folder
      with:
        persist-credentials: false
        sparse-checkout: .github/actions/select-copilot-pat
        sparse-checkout-cone-mode: true
        fetch-depth: 1

    - id: select-copilot-pat
      name: Select Copilot token from pool
      uses: ./.github/actions/select-copilot-pat
      env:
        # If the secret names are changed here, they must also be changed
        # in the `engine: env` case expression
        SECRET_0: ${{ secrets.COPILOT_PAT_0 }}
        SECRET_1: ${{ secrets.COPILOT_PAT_1 }}
        SECRET_2: ${{ secrets.COPILOT_PAT_2 }}
        SECRET_3: ${{ secrets.COPILOT_PAT_3 }}
        SECRET_4: ${{ secrets.COPILOT_PAT_4 }}
        SECRET_5: ${{ secrets.COPILOT_PAT_5 }}
        SECRET_6: ${{ secrets.COPILOT_PAT_6 }}
        SECRET_7: ${{ secrets.COPILOT_PAT_7 }}
        SECRET_8: ${{ secrets.COPILOT_PAT_8 }}
        SECRET_9: ${{ secrets.COPILOT_PAT_9 }}

# Add the pre-activation output of the randomly selected PAT
jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

# Override the COPILOT_GITHUB_TOKEN expression used in the activation job
# Consume the PAT number from the pre-activation step and select the corresponding secret
engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `COPILOT_PAT_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.COPILOT_PAT_0, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.COPILOT_PAT_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.COPILOT_PAT_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.COPILOT_PAT_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.COPILOT_PAT_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.COPILOT_PAT_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.COPILOT_PAT_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.COPILOT_PAT_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.COPILOT_PAT_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.COPILOT_PAT_9, secrets.COPILOT_GITHUB_TOKEN) }}
---

# Conformance Tier Audit

Run the MCP SDK conformance tier audit by invoking the upstream conformance CLI directly and publishing results to the workflow summary and (conditionally) a GitHub issue.

## Inputs

These values are configurable via `workflow_dispatch` inputs. On scheduled runs, the defaults are used.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `audit-scope` | `full` | Preset subset to run: `full`, `tests`, `server`, `client`, `triage`, or `repo` |
| `repo` | `modelcontextprotocol/csharp-sdk` | Repository to use for issue triage, labels, and repo-health checks |
| `conformance-repo` | `modelcontextprotocol/conformance` | The conformance repo to clone |
| `conformance-branch` | `main` | The conformance repo branch to clone |

### Scope presets

| Preset | Components |
|--------|------------|
| `full` | Server conformance, client conformance, issue triage, labels, policies, release signals, spec tracking |
| `tests` | Server and client conformance only |
| `server` | Server conformance only |
| `client` | Client conformance only |
| `triage` | Issue triage/SLA checks only |
| `repo` | Issue triage, labels, policies, release signals, spec tracking — no conformance |

## Instructions

Resolve these values from workflow inputs (falling back to defaults):

- **SCOPE**: `${{ github.event.inputs.audit-scope || 'full' }}`
- **REPO**: `${{ github.event.inputs.repo || 'modelcontextprotocol/csharp-sdk' }}`
- **CONFORMANCE_REPO**: `${{ github.event.inputs.conformance-repo || 'modelcontextprotocol/conformance' }}`
- **CONFORMANCE_BRANCH**: `${{ github.event.inputs.conformance-branch || 'main' }}`
- **FRAMEWORK**: `net9.0`
- **PORT**: `3001`

### Step 1 — Keepalive

Send a `noop` safe-output immediately: `Audit started — scope: {SCOPE}`. This audit can run for over an hour; send additional `noop` heartbeats after each major milestone so the safe-output session stays alive.

### Step 2 — Clone and build the conformance CLI

```bash
conformance_dir=$(mktemp -d)
git clone --depth 1 --branch "$CONFORMANCE_BRANCH" \
  "https://github.com/${CONFORMANCE_REPO}.git" "$conformance_dir"
cd "$conformance_dir"
npm install --silent
npm run build
```

Send a `noop`: `Conformance CLI built`.

### Step 3 — Start the conformance server (if needed)

Only when SCOPE is `full`, `tests`, or `server`.

Start the C# SDK conformance server as a detached background process from the SDK repo root:

```bash
dotnet run --project tests/ModelContextProtocol.ConformanceServer \
  --framework $FRAMEWORK -p:NuGetAudit=false -- --urls http://localhost:$PORT
```

Use `mode: async, detach: true`. Wait a few seconds, then verify:

```bash
curl -sf http://localhost:$PORT -o /dev/null -w '%{http_code}'
```

A `400` response means the server is running. If it fails, check stderr for build errors.

Send a `noop`: `Conformance server started`.

### Step 4 — Pre-build the conformance client (if needed)

Only when SCOPE is `full`, `tests`, or `client`.

```bash
dotnet build tests/ModelContextProtocol.ConformanceClient \
  --framework $FRAMEWORK -p:NuGetAudit=false --nologo -v q
```

This avoids 26 parallel `dotnet run` invocations each triggering a full compilation.

### Step 5 — Run the audit

The conformance CLI is at `$conformance_dir/dist/index.js`. The SDK repo root is `$GITHUB_WORKSPACE` (or the original cwd). Store the SDK root path in `$SDK_ROOT` before changing directories.

**GitHub token**: The `tier-check` command and its subcommands (`triage`, `labels`) require a GitHub token for API access. The CLI reads `GITHUB_TOKEN` from the environment, then falls back to `gh auth token`. In the agentic workflow, `GITHUB_TOKEN` should already be set. If it is not, export it before running the CLI:

```bash
export GITHUB_TOKEN=$(gh auth token)
```

**Client command**: Define the client command template for the CLI. The conformance runner sets `MCP_CONFORMANCE_SCENARIO` as an environment variable and spawns the client with `shell: true`, so `$MCP_CONFORMANCE_SCENARIO` is expanded at invocation time. Use a literal `$` (escaped as `\$` inside double-quotes):

```bash
CLIENT_CMD="dotnet run --project $SDK_ROOT/tests/ModelContextProtocol.ConformanceClient --framework $FRAMEWORK -p:NuGetAudit=false --no-build -- \$MCP_CONFORMANCE_SCENARIO"
```

**Output directory**: Create it before running any CLI commands:

```bash
mkdir -p "$SDK_ROOT/artifacts/skill-output"
```

#### 5a. Full audit (`full`)

Run `tier-check` **once** with `--output json`. This single invocation runs all checks — server conformance, client conformance, labels, triage, P0 resolution, stable release, policy signals, and spec tracking. Do **not** run it twice (once for JSON, once for markdown) as that would re-execute all conformance tests.

```bash
cd "$conformance_dir"
node dist/index.js tier-check \
  --repo "$REPO" \
  --conformance-server-url "http://localhost:$PORT" \
  --client-cmd "$CLIENT_CMD" \
  --output json > "$SDK_ROOT/artifacts/skill-output/tier-check.json"
```

The CLI prints progress to stderr (e.g., `✓ Server Conformance`, `✓ Client Conformance`, `✓ Triage`). Send a `noop` keepalive after you see each conformance milestone complete.

The JSON output contains the full `TierScorecard` with these fields:
- `repo`, `branch`, `timestamp`, `version`
- `implied_tier.tier` (integer: 1, 2, or 3), `implied_tier.tier1_blockers[]`, `implied_tier.note`
- `checks.conformance` — server conformance results with `details[]` (each has `scenario`, `passed`, `specVersions[]`)
- `checks.client_conformance` — client conformance results with same structure
- `checks.labels` — `status`, `present`, `required`, `missing[]`
- `checks.triage` — `status`, `compliance_rate`, `median_hours`, `p95_hours`, `total_issues`
- `checks.p0_resolution` — `status`, `open_p0s`, `closed_within_7d`, `closed_total`, `open_p0_details[]`
- `checks.stable_release` — `status`, `version`, `is_stable`
- `checks.policy_signals` — `status`, `files` (map of filename → boolean)
- `checks.spec_tracking` — `status`, `days_gap`

#### 5b. Tests only (`tests`)

Run server and client conformance suites separately. Server suite default is `active` (excludes pending/draft scenarios); use `--suite all` to include everything.

```bash
cd "$conformance_dir"
node dist/index.js server --url "http://localhost:$PORT" --suite active \
  -o "$SDK_ROOT/artifacts/skill-output/server-results"
node dist/index.js client --command "$CLIENT_CMD" --suite all \
  -o "$SDK_ROOT/artifacts/skill-output/client-results"
```

Results are saved to the output directories as `checks.json` per scenario.

#### 5c. Server only (`server`)

```bash
cd "$conformance_dir"
node dist/index.js server --url "http://localhost:$PORT" --suite active \
  -o "$SDK_ROOT/artifacts/skill-output/server-results"
```

#### 5d. Client only (`client`)

```bash
cd "$conformance_dir"
node dist/index.js client --command "$CLIENT_CMD" --suite all \
  -o "$SDK_ROOT/artifacts/skill-output/client-results"
```

Available client suites: `all`, `core`, `extensions`, `backcompat`, `auth`, `metadata`, `draft`, `sep-835`.

#### 5e. Triage only (`triage`)

```bash
cd "$conformance_dir"
node dist/index.js tier-check triage --repo "$REPO" --days 30
```

This outputs JSON to stdout with triage check results.

#### 5f. Repo health (`repo`)

Run `tier-check` once with `--skip-conformance` to skip server/client tests and evaluate only labels, triage, P0 resolution, stable release, policy signals, and spec tracking:

```bash
cd "$conformance_dir"
node dist/index.js tier-check --repo "$REPO" --skip-conformance \
  --output json > "$SDK_ROOT/artifacts/skill-output/tier-check.json"
```

### Step 6 — Build the report

Write the canonical report to `artifacts/skill-output/step-summary.md`.

For **full** and **repo** scopes, you have the JSON scorecard from `tier-check`. Parse it to extract:
- `implied_tier.tier` — the final tier number
- `implied_tier.tier1_blockers` — what's blocking Tier 1
- `checks.conformance.details` and `checks.client_conformance.details` — per-scenario pass/fail with `specVersions`
- All repository health checks (labels, triage, P0, release, policy, spec tracking)

Build a conformance matrix table grouping scenarios by spec version (the JSON `specVersions` array maps each scenario to one or more spec versions like `2025-03-26`, `2025-06-18`, `2025-11-25`, or informational versions like `draft`, `extension`).

For **partial** scopes (`tests`, `server`, `client`), the results are in the output directories as `checks.json` files per scenario. Summarize pass/fail counts from those files.

For **triage** scope, the output is the triage check JSON with `compliance_rate`, `median_hours`, `p95_hours`, and `total_issues`.

1. **Executive summary** at the top — final tier result (e.g., "**Tier 3**"), conformance matrix table, repository health table
2. **Full assessment report** in a collapsible `<details><summary>📋 Full Assessment Report</summary>` block
3. **Full remediation report** in a collapsible `<details><summary>🔧 Remediation Guide</summary>` block — only for items that are not yet passing

For **partial** scopes (`tests`, `server`, `client`, `triage`), produce a focused summary covering only the selected components. Explicitly note which sections were intentionally skipped. Do **not** claim a final tier unless all tier inputs were evaluated.

When writing remediation:

- If client conformance is below the tier threshold, inspect the detailed JSON/logs before prescribing work. Distinguish confirmed SDK behavior failures (e.g., `"Tool was not called by client"`) from conformance-harness gaps (e.g., `Expected Check Missing` or `0 passed, 0 failed`). Do not prescribe SDK fixes for the latter.
- In any **Path to Tier 1** section, list only **currently open issues** that still need action. Closed issues may be mentioned as context but must not appear as outstanding items.

Build the file using bash:

```bash
mkdir -p artifacts/skill-output
report_path="artifacts/skill-output/step-summary.md"
echo "# Conformance Tier Audit — C# MCP SDK" > "$report_path"
# ... append sections from CLI output and your analysis ...
```

If `$GITHUB_STEP_SUMMARY` is writable from inside the agent, append the same content there, but `artifacts/skill-output/step-summary.md` is the primary path (the workflow post-step copies it to the summary automatically).

### Step 7 — Publish issue (conditional)

Create a GitHub issue using `create-issue` **only** when:

- the workflow ran on its **scheduled** trigger, or
- the workflow was manually triggered and SCOPE is `full`

If `${{ github.event_name }}` is `workflow_dispatch` and SCOPE is **not** `full`, do **not** create an issue — keep results in the workflow summary only. Send a final `noop` completion message instead.

For runs that create an issue, call `create-issue` immediately after assembling the report — before cleanup.

Issue title (the `title-prefix` "C# SDK Conformance Audit: " is added automatically):

- **Full audit**: `{yyyy-MM-dd} - Tier {N}` (e.g., `2026-04-03 - Tier 3`)
- **Non-full** (if an issue is ever needed): `{yyyy-MM-dd} - {Scope Label}` (e.g., `2026-04-03 - Client Conformance`)

The issue body is the same content written to `step-summary.md`. The `create-issue` metadata in the frontmatter ensures the **automation** label is applied automatically.

### Step 8 — Cleanup

Stop the conformance server process (if started). Remove the temporary conformance repo directory:

```bash
rm -rf "$conformance_dir"
```

### Resilience notes

- **Rate limits**: If the `tier-check` CLI or any public GitHub API call returns `403`/`429`, check `Retry-After` and `X-RateLimit-Reset`, pause until the reset window, and send `noop` keepalives every 30–60 seconds while waiting. Retry after the wait.
- **Windows quoting**: The `CLIENT_CMD` uses `$MCP_CONFORMANCE_SCENARIO` — the conformance runner sets this as an env var and spawns with `shell: true`. If the runner wraps the command in single quotes (which don't work on Windows cmd.exe), you may see 0 scenarios with 0 checks. In that case, run the client suite directly using `node dist/index.js client --command "..." --suite all`.
- **Noop cadence**: Send a `noop` at least every 20 minutes during long-running steps to keep the safe-output session alive.
