---
description: "SDK Issue Triage"

permissions:
  contents: read
  issues: read
  pull-requests: read

network:
  allowed:
    - defaults
    - github

safe-outputs:
  mentions: false
  allowed-github-references: []
  create-issue:
    title-prefix: "[C# SDK Issue Triage] "
    labels: [automation]
    max: 1
  update-issue:
    title-prefix: "[C# SDK Issue Triage] "
    title: # enable title updates
    body:  # enable body updates
    target: "*"
    max: 1
  noop:
    report-as-issue: false

tools:
  github:
    toolsets: [default]
    min-integrity: none

if: github.event_name == 'workflow_dispatch' || !github.event.repository.fork

concurrency:
  group: csharp-sdk-issue-triage
  cancel-in-progress: true

timeout-minutes: 90

steps:
  - name: Write issue triage parameters
    env:
      OUTPUT_MODE: ${{ github.event.inputs.output || 'Create Issue' }}
    run: |
      mkdir -p /tmp/issue-triage-params
      echo "$OUTPUT_MODE" > /tmp/issue-triage-params/output

post-steps:
  - name: Write issue triage report to action summary
    if: always()
    env:
      JOB_STATUS: ${{ job.status }}
    run: |
      OUTPUT_MODE=$(cat /tmp/issue-triage-params/output 2>/dev/null || echo "Create Issue")
      if [ "$OUTPUT_MODE" = "Action Summary" ] || [ "$JOB_STATUS" != "success" ]; then
        if [ -f /tmp/issue-triage-report.md ]; then
          cat /tmp/issue-triage-report.md >> "$GITHUB_STEP_SUMMARY"
        else
          echo "## Issue Triage: No Report" >> "$GITHUB_STEP_SUMMARY"
          echo "The agent did not produce /tmp/issue-triage-report.md." >> "$GITHUB_STEP_SUMMARY"
        fi
      fi

on:
  schedule: "daily between 6:00 and 7:00 utc-5"
  workflow_dispatch:
    inputs:
      output:
        description: "Where to publish results"
        required: true
        type: choice
        options:
          - Create Issue
          - Action Summary
        default: "Create Issue"

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
        SECRET_0: ${{ secrets.AUDIT_PAT_0 }}
        SECRET_1: ${{ secrets.AUDIT_PAT_1 }}
        SECRET_2: ${{ secrets.AUDIT_PAT_2 }}
        SECRET_3: ${{ secrets.AUDIT_PAT_3 }}
        SECRET_4: ${{ secrets.AUDIT_PAT_4 }}
        SECRET_5: ${{ secrets.AUDIT_PAT_5 }}
        SECRET_6: ${{ secrets.AUDIT_PAT_6 }}
        SECRET_7: ${{ secrets.AUDIT_PAT_7 }}
        SECRET_8: ${{ secrets.AUDIT_PAT_8 }}
        SECRET_9: ${{ secrets.AUDIT_PAT_9 }}

jobs:
  pre-activation:
    outputs:
      copilot_pat_number: ${{ steps.select-copilot-pat.outputs.copilot_pat_number }}

engine:
  id: copilot
  env:
    # We cannot use line breaks in this expression as it leads to a syntax error in the compiled workflow
    # If none of the `AUDIT_PAT_#` secrets were selected, then the default COPILOT_GITHUB_TOKEN is used
    COPILOT_GITHUB_TOKEN: ${{ case(needs.pre_activation.outputs.copilot_pat_number == '0', secrets.AUDIT_PAT_0, needs.pre_activation.outputs.copilot_pat_number == '1', secrets.AUDIT_PAT_1, needs.pre_activation.outputs.copilot_pat_number == '2', secrets.AUDIT_PAT_2, needs.pre_activation.outputs.copilot_pat_number == '3', secrets.AUDIT_PAT_3, needs.pre_activation.outputs.copilot_pat_number == '4', secrets.AUDIT_PAT_4, needs.pre_activation.outputs.copilot_pat_number == '5', secrets.AUDIT_PAT_5, needs.pre_activation.outputs.copilot_pat_number == '6', secrets.AUDIT_PAT_6, needs.pre_activation.outputs.copilot_pat_number == '7', secrets.AUDIT_PAT_7, needs.pre_activation.outputs.copilot_pat_number == '8', secrets.AUDIT_PAT_8, needs.pre_activation.outputs.copilot_pat_number == '9', secrets.AUDIT_PAT_9, secrets.COPILOT_GITHUB_TOKEN) }}
---

# SDK Issue Triage

Perform issue triage for the C# SDK. Always use the workflow, skill, and reference files from the checked-out repository workspace. The issue data target is cross-repository: always triage `modelcontextprotocol/csharp-sdk`. Any created or updated report issue belongs in the repository where this workflow is running.

## Inputs

Read the output mode from the file written by a deterministic pre-agent step:

```bash
OUTPUT_MODE=$(cat /tmp/issue-triage-params/output)
```

The workflow repository is already checked out in the workspace. Use the local skill and reference files from that checkout.

## Skill source

Read and follow these files:

- `.github/skills/issue-triage/SKILL.md`
- Any files the skill references under `.github/skills/issue-triage/references/`

Treat those local files as the source of truth for the triage procedure, safety rules, prioritization logic, and report structure. Do not change the target repository. Always triage `modelcontextprotocol/csharp-sdk`.

## Canonical report file

The skill writes its report to `/tmp/issue-triage-report.md`. After trend analysis (below), this file becomes the final published artifact.

Use the current New York date when computing the report title:

```bash
REPORT_DATE=$(TZ=America/New_York date +%F)
```

Set `TO_TRIAGE_COUNT` to the number of open issues that still need triage under the skill's classification rules.

When you call `create-issue`, pass only the date and count as the `title`:

```text
yyyy-MM-dd (N to triage)
```

The workflow's `title-prefix` setting for `create-issue` will automatically prepend `[C# SDK Issue Triage] ` to produce the final issue title. Do **not** include the prefix yourself when calling `create-issue`.

When you call `update-issue`, the `title-prefix` setting is used only for **matching** the target issue — it does **not** auto-prepend. You must pass the **full** title including the prefix:

```text
[C# SDK Issue Triage] yyyy-MM-dd (N to triage)
```

> ⚠️ **Critical: `create-issue` and `update-issue` handle the prefix differently.**
> - `create-issue` **auto-prepends** the prefix → pass only the date/count portion.
> - `update-issue` does **NOT** auto-prepend → you **must** include `[C# SDK Issue Triage] ` at the start of the title yourself.
> Failing to include the prefix when calling `update-issue` will strip it from the issue title.

Do **not** include `Issue Triage Report`, `Report`, an em dash, or any extra words in either case.

## Trend analysis

After the skill generates `/tmp/issue-triage-report.md`, augment the report with historical context by comparing against prior triage reports.

### Gathering prior reports

Search the workflow repository (where this workflow is running) for triage report issues whose title starts with `[C# SDK Issue Triage] `. Search both open **and** closed issues across three sliding windows:

| Window | Anchor | Use |
|--------|--------|-----|
| **7 days** | Issue `created_at` or `updated_at` (whichever is more recent) | Most recent comparison point |
| **14 days** | Same | Short-term trend |
| **28 days** | Same | Medium-term trend |

For each matching issue found:

1. Read the issue **body** (the triage report from that run).
2. Read **all comments** on the issue (these may contain maintainer feedback or guidance).
3. Note whether the issue is open or closed and its date.

If there is currently an open triage report issue, record its issue number for use in the publishing step below.

### Generating the Trends section

Compare the current report's metrics against prior reports to produce a `## 📈 Trends` section. Include:

- **Open issue count trajectory** — increasing, stable, or decreasing across the windows
- **SLA compliance trajectory** — improving or declining
- **Resolution velocity comparison** — how the current closed-issue counts (from the skill's report) compare to the rates observed in prior reports
- **New issues since last report** — issues opened since the most recent prior report
- **Issues resolved since last report** — issues that appeared in a prior report but are now closed
- **Maintainer guidance** — if comments on any prior triage report issue contain instructions (e.g., "plan to close #42 next sprint", "this is intentionally kept open"), surface them as guidance notes

Insert the `## 📈 Trends` section into `/tmp/issue-triage-report.md` immediately after the BLUF section and its `---` separator, before the first content section (Safety Concerns or Urgent Attention).

If no prior triage reports exist, insert a brief note: `## 📈 Trends\n\n_No prior triage reports found. Trends will appear after the next run._`

## Publishing rules

Search the workflow repository (where this workflow is running — which may be a fork, a side repo, or `modelcontextprotocol/csharp-sdk` itself) for triage report issues whose title starts with `[C# SDK Issue Triage] `. If this search was already performed during trend analysis above, reuse those results. If there is a currently open matching issue, record its number for the publishing step below.

### If `OUTPUT_MODE` is `Create Issue`

1. **If a matching open issue exists, try `update-issue` first:**
   - Use the matching issue number.
   - Set `operation: replace`.
   - Set the title to the full value **including** the prefix: `[C# SDK Issue Triage] yyyy-MM-dd (N to triage)`. The `update-issue` tool does **not** auto-prepend the prefix.
   - Set the body to the exact contents of `/tmp/issue-triage-report.md`.
   - If `update-issue` **succeeds**, publishing is done.
   - If `update-issue` **fails** (e.g., the issue was closed between the search and the update), fall through to step 2.
2. **If no matching open issue exists, or if `update-issue` failed:**
   - Use `create-issue` with:
     - the title using only `yyyy-MM-dd (N to triage)` (the `create-issue` tool auto-prepends the prefix); and
     - the exact contents of `/tmp/issue-triage-report.md` as the body.

Do not use comments for the report. Keep the report in the issue body itself.

### If `OUTPUT_MODE` is `Action Summary`

Do not create an issue. Do not update any existing issue. Call `noop` with a short message such as `Issue triage complete - results in Action Summary.` The deterministic post-step will append `/tmp/issue-triage-report.md` to the workflow summary page verbatim.

## Failure handling

If triage fails at any point, or if you cannot produce `/tmp/issue-triage-report.md`:

1. Do not create or update any issue.
2. Write a failure report to `/tmp/issue-triage-report.md` explaining what happened.
3. Call `noop` with a short failure message.
4. Let the deterministic post-step handle surfacing the failure report in the action summary when appropriate.

## Constraints

- Never change the triage target away from `modelcontextprotocol/csharp-sdk`.
- Always create or update the rolling report issue in the workflow repository.
- Never rewrite the report in deterministic workflow steps.
- Never publish the report as a comment.
- When the output mode is `Action Summary`, never create a noop issue and never touch any existing report issue.
- Use the skill's output as the base report content. The only permitted modification is inserting the Trends section after BLUF during trend analysis.
- Always include the `[C# SDK Issue Triage] ` prefix when calling `update-issue`. The prefix is only auto-prepended by `create-issue`.
- Exclude issues labeled `automation` from the triage data set; they are not part of the SDK issue backlog.

## Usage

Compile this workflow with:

```bash
gh aw compile issue-triage --schedule-seed modelcontextprotocol/csharp-sdk
```

This workflow reuses the `AUDIT_PAT_#` pool for Copilot engine authentication.

GitHub reads inside the agent sandbox and safe outputs that create or update the rolling issue use the workflow's default GitHub token.

`tools.github.min-integrity` is intentionally set to `none` so triage can inspect public issue bodies and comments from non-member authors in `modelcontextprotocol/csharp-sdk`.

Those PATs therefore need:

- `Copilot Requests` read access

The workflow's default GitHub token therefore needs:

- Enough repository read access for the modelcontextprotocol repositories consulted by the issue-triage skill
- Permission to create and update issues in the repository where this workflow runs
