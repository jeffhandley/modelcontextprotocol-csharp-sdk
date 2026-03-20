---
on:
  schedule:
    - cron: "0 14 * * 4"
  workflow_dispatch:
    inputs:
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

if: github.repository == 'modelcontextprotocol/csharp-sdk' || github.event_name == 'workflow_dispatch'

permissions:
  contents: read
  issues: read
  pull-requests: read

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
  github: true
---

# Weekly Conformance Tier Audit

Run the MCP SDK conformance tier audit every Thursday and publish the assessment and remediation reports to the workflow summary.

## Inputs

These values are configurable via `workflow_dispatch` inputs. On scheduled runs, the defaults are used.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `conformance-repo` | `modelcontextprotocol/conformance` | The conformance repo to clone |
| `conformance-branch` | `main` | The conformance repo branch to clone |

The workflow always uses `modelcontextprotocol/csharp-sdk` with branch `main` for issue triage, labels, and policy checks — regardless of which fork or branch the workflow runs on. The SDK source code is taken from the current repository and branch (selectable via workflow_dispatch's native branch picker).

## Instructions

Read and follow the conformance-tier-audit skill at `.github/skills/conformance-tier-audit/SKILL.md`. Use these parameter overrides:

- **`--repo`**: `modelcontextprotocol/csharp-sdk` (always — for issue triage, labels, policy signals)
- **`--branch`**: `main` (always — for GitHub API checks against the upstream repo)
- **`--framework net9.0`** for the conformance server and client
- When cloning the conformance repo, use `https://github.com/${{ github.event.inputs.conformance-repo || 'modelcontextprotocol/conformance' }}.git` and checkout branch `${{ github.event.inputs.conformance-branch || 'main' }}`

**Important**: The `--repo` and `--branch` values above are for GitHub API checks (issue triage, labels, policy signals) and must always target the upstream `modelcontextprotocol/csharp-sdk` repo on `main`. The SDK source code being audited (conformance server/client) comes from the current repository checkout.

### Output to Workflow Summary

Instead of writing files to `artifacts/skill-output/`, write **all** reports to the GitHub Actions step summary (`$GITHUB_STEP_SUMMARY`). This makes the reports visible directly in the workflow run summary page.

Format the step summary as:

1. **Executive summary** at the top — final tier result, conformance matrix table, repository health table
2. **Full assessment report** in a collapsible `<details>` block with a 📋 prefix
3. **Full remediation report** in a separate collapsible `<details>` block with a 🔧 prefix

Write the content to `$GITHUB_STEP_SUMMARY` using bash, for example:

    echo "# Conformance Tier Audit — C# MCP SDK" >> "$GITHUB_STEP_SUMMARY"
    echo "" >> "$GITHUB_STEP_SUMMARY"
    echo "## Executive Summary" >> "$GITHUB_STEP_SUMMARY"
    echo "...tables and tier result..." >> "$GITHUB_STEP_SUMMARY"
    echo "<details><summary>📋 Full Assessment Report</summary>" >> "$GITHUB_STEP_SUMMARY"
    echo "...assessment content..." >> "$GITHUB_STEP_SUMMARY"
    echo "</details>" >> "$GITHUB_STEP_SUMMARY"
