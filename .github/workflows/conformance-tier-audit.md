---
if: (!github.event.repository.fork) || github.event_name == 'workflow_dispatch'

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

timeout-minutes: 240

on:
  schedule:
    - cron: "weekly on thursday around 9am utc-7"
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
