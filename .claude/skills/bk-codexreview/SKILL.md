---
name: "bk-codexreview"
description: "Iterate-to-convergence code review/fix loop. By default it reviews the branch diff with codex, applies fixes via a Claude sub-agent, commits a per-cycle checkpoint, and re-reviews until convergence or a cycle cap (default 10). --review-only reproduces today's read-only single-shot. Scope spans diff | path/subtree | repo with an optional named aspect; diff mode also verifies spec delivery. Per-cycle/per-reviewer token usage is recorded via the spec-020 ledger. Never pushes, never mutates pipeline state, refuses on a dirty tree or protected branch (without --confirm-protected). Advisory: it never auto-invokes another buildkit-* command."
argument-hint: "[--review-only] [--scope diff|<path>|repo] [--aspect <descriptor>] [--max-cycles <n>] [--reviewers <n>] [--confirm-protected] [--base <ref>] [--reasoning-effort low|medium|high] [--allow-secrets-in-diff] [--max-seconds <n>]"
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "github-spec-kit"
  source: "templates/commands/buildkit-codexreview.md"
user-invocable: true
disable-model-invocation: false
---



## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (it carries the flags below).

## What this does

`/bk-codexreview` is the **conductor** for an iterate-to-convergence review/fix loop. Each
cycle it runs the **codex** reviewer over the selected scope, (in fix mode) spawns a Claude Code
**fixer** sub-agent to apply the findings, commits a per-cycle checkpoint, and re-reviews — until a
cycle surfaces **no new finding** (convergence) or the **cycle cap** (default 10) is reached. The
deterministic work (scope resolution, secret pre-scan, finding identity/merge/convergence, the
checkpoint commit, token recording, the verdict) lives in the `buildkit-codexreview` console
subcommands you call below; **you** (the agent) orchestrate the loop and spawn the sub-agents.

It is **advisory**: it edits/commits **locally only**, never pushes, never mutates pipeline/DBOS
state, and never auto-invokes another `buildkit-*` command.

## The two layers (do not violate)

- **Python primitives** (`buildkit-codexreview <subcommand>`): everything deterministic.
- **Sub-agents (you spawn via the Agent tool)**: the **fixer** (fix mode), and — added in later
  modes — the parallel Claude **reviewers** and the **spec-delivery reviewer**. A plain Python
  process cannot spawn a Claude sub-agent, so the loop is yours to drive.

## Core loop (fix mode — the default)

Run from the repo root. Parse the user flags into: `SCOPE` (default `diff`), `ASPECT` (default
`general`), `MAX_CYCLES` (default 10), `REVIEWERS` (default 1), `BASE`, `CONFIRM_PROTECTED`,
`REASONING`, `MAX_SECONDS`, `ALLOW_SECRETS`.

### 1. Preflight (once, before cycle 1)

```bash
buildkit-codexreview preflight --scope "$SCOPE" [--base "$BASE"] [--aspect "$ASPECT"] \
    [--confirm-protected] [--allow-secrets-in-diff] --json
```

- If it refuses (exit 2 — `empty_scope` / `secrets_in_diff` / `dirty_tree` / `protected_branch` /
  `codex_not_found` / `codex_not_usable`), **surface the message verbatim and STOP**. No artifacts
  or commits have been made.
- Otherwise record `run_id`, `base`, `branch`, the unit list, `MAX_CYCLES`, `REVIEWERS` from the
  JSON. Use this `run_id` for **every** cycle and the final verdict.

### 2. For each cycle `K` in `1..MAX_CYCLES`

Compute the cycle dir `reviews/<branch>/<run_id>/cycle0K` (the subcommands create it).

**a. Review — codex pass.** (Claude reviewers run in parallel here in `--reviewers` mode — US5.)

```bash
buildkit-codexreview codex-pass --cycle K --scope "$SCOPE" [--aspect "$ASPECT"] [--base "$BASE"] \
    --run "<run_id>" --out "reviews/<branch>/<run_id>/cycle0K" \
    [--cross-feed <prev_cycle_crossfeed.json>] --reasoning-effort "$REASONING" \
    --max-seconds "$MAX_SECONDS" [--allow-secrets-in-diff] --json
```

This **secret-scans the exact bytes about to be sent FIRST** (FR-014, every cycle — the fixer
mutates content between cycles) and refuses (exit 2 `secrets_in_diff`) on a hit. It streams
`cycle0K/codex.md`, parses `cycle0K/codex.json`, and records the codex token. A timeout leaves a
partial artifact and exits 3 — surface it and stop the loop.

> **Fallback (verified live, codex 0.130.0):** `codex review "<PROMPT>"` sometimes returns prose
> only and omits the requested `buildkit-findings` block, so `codex.json` may be empty that cycle.
> This is expected — the parallel Claude reviewer sub-agents (default ≥1) always return strict
> finding JSON, so the **combined** set still drives convergence (R7). codex's prose review is
> preserved in `codex.md` for the human regardless.

**b. Merge — combine + convergence (deterministic).**

```bash
buildkit-codexreview merge --cycle K --out "reviews/<branch>/<run_id>/cycle0K" \
    --codex "reviews/<branch>/<run_id>/cycle0K/codex.json" \
    [--claude <claude_findings_i.json> ...] [--prev "reviews/<branch>/<run_id>/cycle0(K-1)"] \
    --scope "$SCOPE" [--aspect "$ASPECT"] --json
```

Read `combined_count`, `new_count`, `converged` from the JSON.

**c. Convergence / zero.** If `converged` is `true` (no new identity vs the prior cycle, or the
combined set is empty), the loop is done: go to step 3 with verdict `converged@K` (or `clean` when
`K==1` and empty).

**d. Fix (fix mode only).** If the combined set is non-empty, spawn **one Claude Code fixer
sub-agent** (Agent tool) with the combined findings from `cycle0K/result.json` (the `combined`
array). The fixer is a **different** role from any reviewer (FR-002). Use this prompt:

> You are a code-fixer. Apply minimal, correct fixes to the working tree for EACH finding below.
> Edit only what is needed to resolve them; do not reformat unrelated code; do not commit. For each
> finding, address the `path`/`line_start`/`line_end` location guided by `description` and
> `suggested_fix`. Return a short summary of what you changed.
> Findings (JSON):
> ```json
> { "findings": [ { "path": "...", "line_start": 0, "line_end": 0, "issue_class": "...",
>   "severity": "...", "description": "...", "suggested_fix": "..." } ] }
> ```

After the fixer returns, commit the cycle checkpoint:

```bash
buildkit-codexreview commit-cycle --cycle K --max-cycles "$MAX_CYCLES" --scope "$SCOPE" \
    [--aspect "$ASPECT"] --out "reviews/<branch>/<run_id>/cycle0K" [--confirm-protected] --json
```

This refuses on a protected branch without `--confirm-protected` and never pushes. If the fixer
changed nothing, `committed` is `false` (note it and continue).

**e. Next cycle.** Carry `cycle0K/crossfeed.json` as the next cycle's `--cross-feed` (US5; empty in
the core loop). Set `--prev` to `cycle0K`. Continue.

### 3. Verdict (always)

```bash
buildkit-codexreview verdict --run "<run_id>" --out "reviews/<branch>/<run_id>" \
    --mode fix --max-cycles "$MAX_CYCLES" --scope "$SCOPE" [--aspect "$ASPECT"] [--base "$BASE"] --json
```

Writes `run.json` + `verdict.md` (cycles run vs cap, `converged@K` / `capped@N` / `clean`, residual
findings, per-cycle checkpoints, unreviewed units). If the loop hit the cap without converging, the
verdict is `capped@MAX_CYCLES` — commits stay intact; report the residual findings.

## Rollback

```bash
buildkit-codexreview rollback --cycle K [--repo .] --json      # revert one cycle's checkpoint
buildkit-codexreview rollback --run <run_id> --json            # revert the whole run's checkpoints
```

Default uses `git revert --no-edit` (safe). `--hard` (a reset) requires `--confirm`. Never pushes.

## Scope & aspect (`--scope` / `--aspect`, US2 / FR-011/FR-012/FR-015/FR-016)

- `--scope diff` (default) reviews the branch diff vs `--base`.
- `--scope <path>` sweeps the tracked files under a file/subtree; `--scope repo` sweeps the whole
  repo (minus vendored excludes). For these, `preflight`/`codex-pass` **partition** the content
  into per-pass units (default budget ~200 KB / ~4,000 lines per unit); `codex-pass` runs **one
  codex pass per unit** and aggregates the findings with per-file attribution. A file too large
  for one pass is reported in `unreviewed_units` (never silently dropped — SC-006).
- `--aspect <descriptor>` (e.g. `code-smells`, `error-handling`, `SOLID compliance`) is injected
  into the enriched codex prompt (and the Claude reviewers) to focus the review; default `general`.
- The secret pre-scan covers **every** scope type before any send (FR-014): the diff text for
  diff scope, each unit's file content for path/repo scope.
- The `verdict` surfaces `unreviewed_units` so whole-repo coverage is honest (no silent
  truncation — SC-006). Pass `--scope`/`--aspect` through to `codex-pass`, `merge`, `commit-cycle`
  and `verdict` so they are recorded in each artifact (FR-018).

## Review-only mode (`--review-only`, US4 / FR-008/FR-019)

When the user passes `--review-only`, **report findings only — make NO edits and NO commits**:

- Run `preflight --review-only` (dirty-tree / protected-branch guards do not apply — nothing is
  committed), then a single `codex-pass --review-only` (no fixer, no `merge`-driven loop, no
  `commit-cycle`), then `verdict --mode review_only`. The working tree and git history are
  unchanged (SC-004).
- **Back-compat single-shot (SC-010).** The exact invocation
  `/bk-codexreview --review-only --max-cycles 1 --scope diff` (no `--aspect`) reproduces
  today's read-only review: `codex-pass` selects the **vanilla `codex review --base <ref>`** form
  (no prompt, no Claude cross-feed), and the artifact is written at today's location
  `reviews/<branch>/<UTC>/codex.md` (run-dir root, not under `cycleNN/`). The
  `buildkit-codexreview` console entry with no subcommand does exactly this single pass (a direct
  Python call cannot spawn the fixer); the default auto-fix loop is this skill's job.

## Parallel Claude reviewers + cross-feed (`--reviewers N`, US5 / FR-020–FR-024)

Each cycle, run `--reviewers N` (default 1, ≥1) Claude Code reviewer sub-agents **in parallel
with** the codex pass (step 2a/2b above):

- Spawn the N reviewer sub-agents (Agent tool) over the **same** scope/aspect and the **same
  secret-scanned content** codex sees. Each MUST return its final message as the strict reviewer
  JSON `{schema_version, reviewer, scope, aspect, findings:[…], spec_promises:[…],
  unreviewed_units:[…]}`. Write each to `cycle0K/claude-i.json`. (The H1/FR-014 secret pre-scan
  that `codex-pass` runs covers the bytes the reviewers receive too — never hand a reviewer
  unscanned post-fix content.)
- `merge` combines codex + all `--claude` files, **de-duplicates by identity** (codex+Claude at
  the same identity collapse to one with the union of reviewers; conflicting findings at the same
  location but a different class are both retained), and writes `cycle0K/crossfeed.json` = the
  **Claude-only delta** (`Claude identities − codex identities`, FR-022). Convergence is measured
  over the **combined, de-duplicated** set (FR-021).
- Pass `cycle0K/crossfeed.json` as the **next** cycle's `--cross-feed` so codex sees the findings
  only the Claude reviewers raised (never the ones codex already raised).
- **Record a per-reviewer token row** for each Claude reviewer the harness reports usage for:
  `buildkit-codexreview tokens --cycle K --reviewer claude-i --total <N> --method self-reported
  --run <run_id> …` (omit counts → `unavailable`; never fabricate — FR-025/SC-011/SC-012).
- **Degrade gracefully (FR-024):** if **no** Claude reviewer can run, continue **codex-only** (no
  `--claude`, empty cross-feed) and pass `--reduced-coverage` to `verdict` so the run notes the
  reduced coverage. Not a failure.

## Spec-delivery verification (diff scope, US3 / FR-013)

In **diff** scope, before cycle 1 resolve the linked spec's promises and write them to the run
root, then have the reviewers assess delivery:

```bash
python -m buildkit_cli.codexreview ...   # (promises are resolved via the specdelivery module)
```

- Resolve promises from `.specify/feature.json` → `<feature_directory>/spec.md` (the `FR-###`
  requirements + acceptance scenarios). Write `reviews/<branch>/<run_id>/promises.json`. If **no
  spec is linked** (no `feature.json`/`spec.md`), **skip with a notice** — the review still
  completes (FR-013 / SC-008); do not write `promises.json`.
- Pass `--spec-promises reviews/<branch>/<run_id>/promises.json` to each cycle's `codex-pass`
  (diff scope only). The enriched prompt asks codex to mark each promise met/partial/unmet.
- **Spawn a Claude spec-delivery reviewer sub-agent** (diff scope) that maps each promise to
  met/partial/unmet against the diff and returns a doc with a `spec_promises` array
  (`[{requirement_id, status, evidence}]`); pass its file as one of the `--claude` inputs to
  `merge`. `merge` aggregates promise statuses (worst wins) into `cycleNN/spec_promises.json`.
- `verdict` surfaces the partial/unmet promises in its `spec_delivery` section (`checked: true`
  with `gaps`, or `checked: false` + skip notice when no spec was linked).

## Constraints (hard)

- **Never push, never mutate pipeline/DBOS state** (FR-007). The only writes are working-tree
  edits, local commits, on-disk artifacts under `reviews/` (gitignored), and the advisory token
  ledger.
- **Fix mode needs a clean tree on a non-protected branch.** Dirty → refuse (commit/stash first).
  Protected (`main`/`master`/`develop`) → refuse unless `--confirm-protected`.
- **Reviewer ≠ fixer** (FR-002): codex (and the Claude reviewers) only review; a separate Claude
  sub-agent applies fixes.
- **Secret pre-scan runs every cycle** over the exact bytes about to be sent (FR-014), because the
  fixer mutates content between cycles. Override only with `--allow-secrets-in-diff`.
- **Convergence is deterministic** — computed by `merge` over the `(path, range, class)` identity
  set, never by your judgement.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | loop completed (converged / capped / clean / review-only) |
| 1 | internal error |
| 2 | refusal (codex unavailable, empty scope, secrets, dirty tree, protected branch) |
| 3 | codex exited non-zero or timed out (partial artifact persisted) — stop the loop |

## Per-cycle / per-reviewer token record (spec-020 ledger, FR-025 — advisory)

`codex-pass` records the codex token each cycle automatically. For each Claude reviewer/fixer the
harness exposes usage for, record it too (never fabricate counts — omit for an `unavailable` 0):

```bash
buildkit-codexreview tokens --cycle K --reviewer claude-1 --total <N> --method self-reported \
    --run "<run_id>" [--feature <id>] --scope "$SCOPE" [--aspect "$ASPECT"] --json
```

Advisory and non-blocking — a catalog failure prints a notice and the loop continues.

**Registry upkeep (spec-028 FR-004)**: run
`python -m buildkit_cli.registry touch --tool buildkit-codexreview` from the project root. It marks the
capability registry possibly-stale and **always exits 0** (fail-safe; never blocks this stage).
Ignore its output.
