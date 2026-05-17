# Contract: `codeconv planagents` CLI surface + skill orchestration loop

Implements spec FR-001, FR-002, FR-005, FR-014, FR-018, FR-019, FR-021. Implemented in `codeconv/src/codeconv/tools/planagents/__init__.py` + `workflow.py`; skill in `.claude/skills/codeconv-planagents/SKILL.md`.

## Source of truth references

- Feature 012 `contracts/codeconv_tool_contract.md` — subpackage MUST export `app: typer.Typer`; companion skill required.
- Feature 015 `contracts/depgraph_cli.md` — flag/exit-code/JSON conventions mirrored.
- Research R1 (skill = orchestrator), R3 (≤7 cap), R10 (escalations).

## CLI command tree

```text
codeconv planagents                                       # alias for `status` (FR-018 default; spawns nothing)
codeconv planagents status   [flags]                      # readiness view; no agents, no writes
codeconv planagents next     [--limit 7] [flags]          # emit the next plan-ready batch (JSON for the skill)
codeconv planagents plan-started   <path> [--sha256 <hex>] [flags]
codeconv planagents plan-completed <path> [--plan-path <p>] [--escalations <n>] [flags]
codeconv planagents aggregate-escalations [--report-out <p>] [flags]
codeconv planagents stamp-tombstones [flags]
codeconv planagents rebuild-plans-from-tombstones [flags]
```

The subcommand list is fixed by FR-001/FR-005/FR-016/FR-013. No others in v1.

## Top-level flags inherited from `codeconv` (feature 012 FR-019)

| Flag | Default | Effect |
|---|---|---|
| `--repo-root <path>` | `Path.cwd()` | Override repo root. |
| `--data-dir <path>` | `<repo-root>/.pgdb` | PGLite cluster location. **Mandatory on this exFAT checkout: `--data-dir C:/pglite/research/glpnet`** (CLI guard exits 64 on non-NTFS). |
| `--quiet` | off | Suppress per-step logging. |
| `--json` | off | Emit JSON summary on stdout. |

## Per-subcommand behaviour

### `status` (default)

1. Acquire-or-discover the bridge (feature-012 `bridge_client.acquire_or_discover`).
2. If `codeconv.dart_depgraph` is empty/absent → **exit 2**, unconditionally (incl. under `--json`): human → stderr `"No depgraph. Run /codeconv-depgraph first."`; `--json` → stdout `{"ok":false,"exit_code":2,"error":"No depgraph. Run /codeconv-depgraph first."}` AND process exit 2 (the JSON field does NOT replace process exit — feature-015 contract carry-forward) (FR-018).
3. Read depgraph + `dart_plans` + `dart_files.sha256`; classify all non-orphaned nodes (`readiness.py`).
4. **Stale detection (FR-015 / quickstart §7)**: any `planned` row whose current `dart_files.sha256 ≠ sha256_of_dart_at_plan_start` is reported **stale** — a distinct flag overlaid on the `planned` class (the lifecycle state stays `planned`; stale is an advisory the source drifted since the plan), with the file paths listed so the user knows exactly what to pass to `--replan` (human → a `stale: <n>` line + the path list; `--json` → a `"stale": ["<path>", …]` array).
5. Emit counts (`plan_pending`/`plan_ready`/`plan_in_progress`/`planned`, `stale`, `open_escalations_total`). **Writes nothing.** Exit 0.

### `next [--limit 7]`

1. Bridge; depgraph-empty guard (exit 2, as `status`).
2. `readiness.select_next(limit)` (see `plan_readiness_algorithm.md`).
3. Emit the batch as JSON on stdout (the skill consumes this — see Orchestration loop). **Writes nothing** to DB/tombstones/artefacts (selection is read-only; `plan-started` records dispatch).
4. Exit 0 with a non-empty batch, **exit 0 with an empty batch + `"nothing to plan"`** when every non-orphaned file is `planned` (FR-018 — not an error).

`next` JSON shape:

```json
{
  "schema_version": 1,
  "ok": true,
  "limit": 7,
  "batch": [
    { "path": "lib/runtime/terms.dart", "topo_level": 0,
      "cycle_group_id": 12, "scc_siblings": [], "tombstone": ".codeconv/tombstones/lib/runtime/terms.dart.md",
      "artefact": ".codeconv/conversion-plans/lib/runtime/terms.dart.md" },
    { "path": "lib/a.dart", "topo_level": 3, "cycle_group_id": 40,
      "scc_siblings": ["lib/b.dart","lib/c.dart"], "tombstone": "…", "artefact": "…" }
  ],
  "remaining_ready": 18
}
```

Keys inside each `batch[]` row are alphabetical; `batch` is ordered by `(topo_level ASC, path ASC)` with SCC members contiguous and lexicographic (FR-021).

### `plan-started <path> [--sha256 <hex>] [--replan]`

1. Bridge. Validate `path ∈ codeconv.dart_files` (else stderr + exit 2). Reject orphaned `path` (exit 2).
2. **Default** (no `--replan`): `INSERT … ON CONFLICT (path) DO NOTHING` with `plan_started_at=NOW()`, `sha256_of_dart_at_plan_start = <arg or dart_files.sha256>`, `plan_run_id=<this run>`. If row already existed: warn `"already started"` (in-progress) or `"already completed"` (idempotent) → exit 0.
   **With `--replan`** (the mutation path for FR-015 stale replan / FR-019; pairs with `next --replan`): `INSERT … ON CONFLICT (path) DO UPDATE` — reset `plan_started_at=NOW()`, `sha256_of_dart_at_plan_start = <arg or current dart_files.sha256>`, `plan_completed_at = NULL`, `open_escalation_count = 0`, `plan_run_id=<this run>` (in-place row reset per data-model.md §lifecycle; prior open escalations are carried forward in the regenerated artefact with a "carried from <prior generated_at>" note — R9 / artefact-format §idempotence, never silently dropped).
3. Optional `planagents_runs` insert. Stamp tombstone `plan_started_at` (unless `--no-tombstone-update`, testing only). Exit 0.

### `plan-completed <path> [--plan-path <p>] [--escalations <n>]`

1. Bridge. Validate `path`. SELECT row:
   - absent → stderr `"must call plan-started first"` → **exit 2**;
   - `plan_completed_at IS NOT NULL` → warn `"already completed"` → exit 0 (idempotent).
2. `UPDATE … SET plan_completed_at=NOW(), plan_path=:p, open_escalation_count=:n, plan_run_id=:r WHERE path=:path AND plan_completed_at IS NULL`.
3. Stamp tombstone (`plan_completed_at`, `plan_path`, `open_escalation_count`) unless `--no-tombstone-update`. Exit 0. (`--escalations` default 0; `> 0` ⇒ conversion-blocked per FR-017.)

### `aggregate-escalations [--report-out <p>]`

1. Bridge (read `dart_plans` for `open_escalation_count`).
2. Walk `.codeconv/conversion-plans/**.dart.md`; parse each artefact's `## 6. Escalations` section (the exact heading mandated by `conversion_plan_artefact_format.md`); collect open entries.
3. Write the aggregated report (default `.codeconv/conversion-plans/_escalations-report.md`, overridable) — atomic temp-file rename. Each entry: file(s), observed situation, why not pre-specified/incremental, decision required (FR-016).
4. Exit 0. `--dry-run` ⇒ compute, write nothing.

### `stamp-tombstones` / `rebuild-plans-from-tombstones`

- `stamp-tombstones`: read `dart_plans`; for each file update the four plan-state YAML keys via the canonical writer (idempotent; byte-identical re-stamp — data-model §2). `--dry-run` writes nothing.
- `rebuild-plans-from-tombstones`: walk tombstones (skip `.orphaned/`); for each with `plan_started_at` present+non-null, `INSERT … ON CONFLICT (path) DO UPDATE` into `dart_plans` (`sha256_of_dart_at_plan_start` ← current `dart_files.sha256`, same caveat as feature-015 `rebuild-conversions-from-tombstones`). `--dry-run` writes nothing.

## Common flags

| Flag | Applies to | Effect |
|---|---|---|
| `--dry-run` | next/plan-*/aggregate/stamp/rebuild | Compute everything; write nothing to DB, tombstones, artefacts (SC-008). |
| `--replan <selection>` | next, plan-started | `next --replan <selection>`: force re-selection of the named/stale files even if `planned` (FR-015), read-only. `plan-started --replan`: the actual mutation — `ON CONFLICT (path) DO UPDATE` resets the row (`plan_completed_at→NULL`, new sha/timestamp), so a stale/planned file can genuinely be replanned (without `--replan`, `plan-started` no-ops "already completed"). |
| `--limit <n>` | next | Soft cap on tombstones returned (default 7; SCC units never split — `plan_readiness_algorithm.md` step 5). |
| `--json-out <path>` | next | Override the JSON destination (else stdout). |
| `--report-out <path>` | aggregate-escalations | Override the report path (default `.codeconv/conversion-plans/_escalations-report.md`). |
| `--no-tombstone-update` | plan-started/plan-completed | Skip tombstone YAML write (testing only). |
| `--quiet` / `--json` | all | As feature-015. |

## Exit codes

- `0` — success (incl. empty batch / "nothing to plan", FR-018).
- `2` — empty/absent `dart_depgraph` (FR-018); unknown/orphaned `path`; `plan-completed` before `plan-started`.
- `64` — non-NTFS `--data-dir` (feature-012 CLI guard carry-forward).
- `1` — any other unexpected error.

## Skill orchestration loop (R1 / FR-005 / FR-009)

`.claude/skills/codeconv-planagents/SKILL.md` resolves the venv/repo-root exactly as `/codeconv-depgraph`, then runs:

```
loop:
  r := codeconv planagents next --limit 7 --json --data-dir C:/pglite/research/glpnet
  if r.batch is empty: report "nothing to plan"; STOP
  if --dry-run:                       # FR-019 / SC-008 — MUST short-circuit here
      report r.batch as "would plan" (paths, SCC units); spawn NO agents;
      issue NO plan-started / plan-completed / aggregate; STOP
      # `next` is read-only; everything past this point mutates state, so the
      # dry-run branch precedes any dispatch — not merely relying on per-CLI --dry-run
  for each tombstone t in r.batch, with at most 7 Agent calls in flight:
      codeconv planagents plan-started t.path --data-dir …
      spawn planning sub-agent for t   (Agent tool; prompt per agent_orchestration.md)
        – if the agent requests research: spawn a SEPARATE research sub-agent;
          return findings (+ verbatim external requests) to the planning agent
      on agent completion (artefact written to t.artefact):
          codeconv planagents plan-completed t.path \
            --plan-path t.artefact --escalations <open-count parsed from artefact> --data-dir …
  # SCC batch: all members of one cycle_group_id are spawned together,
  # each agent told its scc_siblings; loop does not advance past the SCC
  # until every member is plan-completed.
codeconv planagents aggregate-escalations --data-dir …
```

The skill contains **no deterministic state logic** — readiness, selection, lifecycle, stamping, and aggregation are all the Python CLI's. The skill adds only: venv/repo-root resolution (same as `/codeconv-depgraph`), the Agent-spawn loop, and the ≤7-concurrent enforcement (R3 second half). This is the justified deviation from the pure thin-wrapper convention recorded in plan Complexity Tracking.

## What this CLI does NOT do

- Does NOT convert `.dart` to C#/.NET (spec Out of Scope).
- Does NOT spawn LLM agents itself (R1 — the skill does, via the Agent tool).
- Does NOT modify `dart_files`/`dart_imports`/`dart_callers`/`dart_files_orphaned`/`discover_runs`/`dart_depgraph`/`dart_conversions` (FR-020).
- Does NOT recompute the depgraph/SCC/status (FR-003 — feature 015 owns it).
