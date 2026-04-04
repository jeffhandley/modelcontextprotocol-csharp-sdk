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
    assignees: [jeffhandley]
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

Run the MCP SDK conformance tier audit and publish the assessment and remediation reports to the workflow summary.

## Inputs

These values are configurable via `workflow_dispatch` inputs. On scheduled runs, the defaults are used.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `audit-scope` | `full` | Preset subset to run: `full`, `tests`, `server`, `client`, `triage`, or `repo` |
| `repo` | `modelcontextprotocol/csharp-sdk` | Repository to use for issue triage, labels, and repo-health checks |
| `conformance-repo` | `modelcontextprotocol/conformance` | The conformance repo to clone |
| `conformance-branch` | `main` | The conformance repo branch to clone |

### Scope presets

| Preset | Runs |
|--------|------|
| `full` | Complete tier audit with conformance, issue triage, labels, docs, policies, and release signals |
| `tests` | Server and client conformance only |
| `server` | Server conformance only |
| `client` | Client conformance only |
| `triage` | Issue triage/SLA checks only |
| `repo` | Issue triage, labels, docs, policies, and release/spec-tracking checks without conformance |

## Instructions

Read and follow the conformance-tier-audit skill at `.github/skills/conformance-tier-audit/SKILL.md`. Use these parameter overrides:

- **`--repo`**: `${{ github.event.inputs.repo || 'modelcontextprotocol/csharp-sdk' }}`
- **`--framework net9.0`** for the conformance server and client
- **`--scope`**: `${{ github.event.inputs.audit-scope || 'full' }}`
- Because this audit can run for well over an hour, call the `noop` safe-output tool near the start of the run and again after each major milestone to keep the safe-outputs session alive. Use brief progress messages such as `Audit started`, `Server conformance complete`, `Client conformance complete`, and `Repository health evaluation complete`. Do not wait until the final report for the first safe-output call.
- If public GitHub issue-event reads return a rate-limit response (`403`/`429`, `Retry-After`, or `X-RateLimit-Remaining: 0`), pause until the reset time, send a brief `noop` keepalive every 30-60 seconds while waiting, then resume the audit and retry the request.
- When cloning the conformance repo, use `https://github.com/${{ github.event.inputs.conformance-repo || 'modelcontextprotocol/conformance' }}.git` and checkout branch `${{ github.event.inputs.conformance-branch || 'main' }}`
- For partial runs (`server`, `client`, `triage`, or `repo`), execute only the requested checks and skip unrelated setup. Keep the summary focused on the selected components and explicitly note which sections were intentionally skipped.
- If client conformance is below the tier threshold, inspect the detailed client result JSON/logs before writing remediation. Distinguish confirmed behavior failures (for example `"Tool was not called by client"` or missing SSE reconnect) from conformance-client / audit-harness gaps (for example `Expected Check Missing` or `0 passed, 0 failed`, such as `initialize`). Do not prescribe SDK implementation work for the latter unless the logs show a concrete SDK exception or protocol defect.
- For issue triage, use the resolved `--repo` value above. To determine Tier 1 first-label timing, query that repo's public GitHub issue-events API directly from `bash` and compute the earliest `labeled` event timestamp for each issue. If technical issues still prevent exact scoring after retry/backoff, assume Tier 1 triage is achieved but mark it with a warning signal such as `⚠` and explain the data limitation briefly instead of labeling it uncertain or using the generic phrase `GitHub auth unavailable`.

### Output to Workflow Summary and Conditional Issue

Write the canonical report to `artifacts/skill-output/step-summary.md`. The workflow has a post-step that appends this file to the GitHub Actions step summary (`$GITHUB_STEP_SUMMARY`) outside the sandbox so the report is visible on the workflow run summary page.

Format the step summary as:

1. **Executive summary** at the top — final tier result, conformance matrix table, repository health table
2. **Full assessment report** in a collapsible `<details>` block with a 📋 prefix
3. **Full remediation report** in a separate collapsible `<details>` block with a 🔧 prefix

Build the content in `artifacts/skill-output/step-summary.md` using bash, for example:

    mkdir -p artifacts/skill-output
    report_path="artifacts/skill-output/step-summary.md"
    echo "# Conformance Tier Audit — C# MCP SDK" >> "$report_path"
    echo "" >> "$report_path"
    echo "## Executive Summary" >> "$report_path"
    echo "...tables and tier result..." >> "$report_path"
    echo "<details><summary>📋 Full Assessment Report</summary>" >> "$report_path"
    echo "...assessment content..." >> "$report_path"
    echo "</details>" >> "$report_path"

If `$GITHUB_STEP_SUMMARY` is writable from inside the agent, you may append the same file there as well, but do **not** treat that as the primary path.

Create a GitHub issue using the `create-issue` safe output with the same report content **only** when either of the following is true:

- the workflow ran on its scheduled trigger, or
- the workflow was manually triggered and `audit-scope` is `full`

If `${{ github.event_name }}` is `workflow_dispatch` and `${{ github.event.inputs.audit-scope || 'full' }}` is **not** `full`, do **not** create an issue. In that case, keep the results in the workflow summary only.

For runs that should create an issue, assemble the final report body first and then call `create-issue` immediately, before any optional cleanup or teardown. If no issue should be created for the selected scope, send a final `noop` completion message instead.

- For a **full** audit issue, the title must follow this structure (do **not** include the `title-prefix` — it is added automatically):

    {yyyy-MM-dd} - Tier {N}

- For example: `2026-04-03 - Tier 3`

- If a **non-full** audit ever creates an issue outside the manual-trigger exception above, use a scope-specific title instead, for example:

    {yyyy-MM-dd} - Client Conformance
    {yyyy-MM-dd} - Issue Triage
    {yyyy-MM-dd} - Repository Health

The issue body should contain the same content written to `$GITHUB_STEP_SUMMARY`.
