---
name: codeconv-planagents
description: Orchestrate per-tombstone Dart→C#/.NET conversion-plan generation. Use when the user types `/codeconv-planagents` or asks to generate conversion plans, advance the planning frontier, plan a circular-import group, aggregate plan escalations, or stamp/rebuild plan state through tombstones.
argument-hint: "[status|next|plan-started|plan-completed|aggregate-escalations|stamp-tombstones|rebuild-plans-from-tombstones] [flags]"
compatibility: "Claude Code (Agent tool required for the orchestration loop)"
---

# /codeconv-planagents

Wrapper over `codeconv planagents` for **all deterministic state**
(plan-readiness, selection, lifecycle, stamping, escalation
aggregation) — the Python CLI is the single source of truth for state
and the skill forwards arguments verbatim for every state operation.

The skill **additionally** carries the sub-agent orchestration loop
(spawning the ≤7 planning sub-agents and the separate research
sub-agent via the Claude Code **Agent tool**), because spawning Claude
sub-agents is a Claude Code harness capability the Python CLI
structurally cannot perform. This orchestration loop is the only
"skill machinery" beyond the `/codeconv-discover` / `/codeconv-depgraph`
pattern; it is required by FR-005 / FR-009 and is the planning-phase
transport decision recorded in plan Complexity Tracking + research R1.
The skill contains **no deterministic-state logic**.

## What this skill does

1. Resolve the codeconv venv: `codeconv/.venv/Scripts/python.exe` on
   Windows, `codeconv/.venv/bin/python` on POSIX. If absent, instruct
   Gabi to run `python -m venv codeconv/.venv &&
   codeconv/.venv/Scripts/python.exe -m pip install -e codeconv[dev]`
   first.
2. Run from the repo root. On this exFAT checkout **every** invocation
   MUST pass `--data-dir D:/bstdev/research/glp/glpnet/.pgdb` (CLI guard exits 64
   on a non-NTFS data-dir; `docs/known-issues.md` Issue 8).
3. For a bare `/codeconv-planagents` (or an explicit subcommand): run
   `codeconv planagents <args verbatim>` and show stdout/stderr.
4. For the **orchestration** flow (bare `/codeconv-planagents` with no
   subcommand, i.e. "plan everything ready"): run the loop in
   § "Orchestration loop" below.

## Subcommands and flags

`/codeconv-planagents [subcommand] [flags]`

| Subcommand | Purpose |
|---|---|
| `status` (default — bare `/codeconv-planagents status`) | Readiness view: counts of `plan_pending`/`plan_ready`/`plan_in_progress`/`planned`, `open_escalations_total`, stale list. No agents, no writes. |
| `next [--limit 7]` | Emit the next plan-ready batch as JSON (the loop consumes this). Read-only. |
| `plan-started <path>` | Record that a planning sub-agent was dispatched for `<path>`. |
| `plan-completed <path>` | Record that the conversion plan for `<path>` is complete. |
| `aggregate-escalations` | Walk all artefacts; write the engineer-facing `_escalations-report.md`. |
| `stamp-tombstones` | Embed the four plan-state keys into every tombstone's YAML. |
| `rebuild-plans-from-tombstones` | Inverse of stamp — repopulate `codeconv.dart_plans` from tombstone YAML. |

| Flag | Applies to | Default | Effect |
|---|---|---|---|
| `--limit <n>` | next | 7 | Soft cap on tombstones returned (SCC units are NEVER split — coordinated-batch integrity wins; the loop still throttles to ≤7 concurrent agents). |
| `--replan <selection>` | next / plan-started | off | Force re-planning of stale (`--replan stale`) or named (comma-list) files even if `planned`; UPDATEs the `dart_plans` row in place (FR-015 / R9 — never deletes). |
| `--plan-path <p>` | plan-completed | NULL | Relative path of the produced artefact. |
| `--escalations <n>` | plan-completed | 0 | Open escalation count; `> 0` ⇒ conversion-blocked (FR-017). |
| `--report-out <p>` | aggregate-escalations | `.codeconv/conversion-plans/_escalations-report.md` | Override the report path. |
| `--json-out <p>` | next | stdout | Override the JSON destination. |
| `--dry-run` | next / plan-* / aggregate / stamp / rebuild | off | Compute everything; write NOTHING (no DB, no tombstones, no artefacts) and spawn NO agents (SC-008). |
| `--no-tombstone-update` | plan-started / plan-completed | off | Skip the tombstone YAML write (testing only). |
| `--quiet` | all | off | Suppress per-step logging. |
| `--json` | all | off | Emit a JSON summary on stdout. |
| `--data-dir <path>` | all (top-level) | `<repo>/.pgdb` | Override the PGLite cluster — **the canonical repo-local cluster (checkout is NTFS)**: `--data-dir D:/bstdev/research/glp/glpnet/.pgdb`. |

## Pre-execution checks

- The unified bridge daemon must be reachable. `codeconv planagents`
  calls `acquire_or_discover` which auto-spawns it; the first call in a
  fresh repo pays a ~7 s PGLite cold-init penalty (memory
  `project_pglite_cold_init_windows.md`).
- Schema migrations must have run at least once (`/codeconv-runner
  migrate` — applies Alembic `0003_dart_plans`).
- A populated `codeconv.dart_depgraph` is required (run
  `/codeconv-discover` then `/codeconv-depgraph` first). Empty/absent
  depgraph ⇒ exit 2 with `"No depgraph. Run /codeconv-depgraph first."`
  (FR-018) — unconditionally, including under `--json`.

## Orchestration loop (R1 / FR-005 / FR-009)

Bare `/codeconv-planagents` (no subcommand chosen for state, i.e. the
user wants the frontier planned) resolves the venv/repo-root exactly as
`/codeconv-depgraph`, then runs:

```
loop:
  r := codeconv planagents next --limit 7 --json --data-dir D:/bstdev/research/glp/glpnet/.pgdb
  if r is exit 2 (depgraph empty): surface the error verbatim; STOP
  if r.batch is empty: report r.message ("nothing to plan"); break loop
  for each tombstone t in r.batch, keeping AT MOST 7 planning Agent calls in flight:
      codeconv planagents plan-started t.path --data-dir D:/bstdev/research/glp/glpnet/.pgdb
      spawn ONE planning sub-agent for t   (Agent tool; prompt = § "Planning
        sub-agent prompt contract" below; pass t.path, t.tombstone, t.artefact,
        t.cycle_group_id, t.scc_siblings)
        – if that planning agent returns a RESEARCH REQUEST: spawn a SEPARATE
          research sub-agent (§ "Research sub-agent prompt contract"); return
          its findings (+ every verbatim external request it issued) to the
          requesting planning agent. The research agent does NOT count against
          the 7 planning slots; keep concurrent research agents to a few.
      on planning-agent completion (artefact written to t.artefact):
          n := codeconv planagents plan-completed -> the skill counts
               `Status: open` `### E` entries in t.artefact (or pass
               --escalations <n> after counting them)
          codeconv planagents plan-completed t.path \
            --plan-path t.artefact --escalations <n> --data-dir D:/bstdev/research/glp/glpnet/.pgdb
  # SCC batch: all members of one cycle_group_id arrive in the SAME r.batch;
  # spawn one planning agent per member (each within the 7-cap), pass each its
  # scc_siblings. Do NOT call `next` expecting downstream files until EVERY
  # member is plan-completed (readiness.py enforces the gate; the loop must
  # keep the batch coherent — partial-batch resume re-selects only un-started
  # members on a re-invocation).
codeconv planagents aggregate-escalations --data-dir D:/bstdev/research/glp/glpnet/.pgdb
```

Concurrency-cap (SC-001 / R3) is **dual**: (1) Python `next --limit 7`
never returns an already-`plan_in_progress` tombstone (so a
resumed/interrupted loop cannot double-spawn an in-flight file); (2) the
skill runs at most 7 planning Agent calls concurrently and only issues
the next `next` when a slot frees. An SCC unit taken whole may
transiently exceed the soft `--limit` count in the returned list — the
skill still throttles actual concurrent Agent calls to ≤7, draining the
batch across iterations. Together these guarantee SC-001 crash-safely.

## Planning sub-agent prompt contract (FR-006/FR-007/FR-008/FR-009/FR-011)

Each planning sub-agent is spawned with **exactly one tombstone**. The
prompt MUST supply, and the agent MUST honour, all of:

1. **Inputs**: the tombstone path `.codeconv/tombstones/<rel>.dart.md`
   AND the **real source path** `<rel>.dart`. The agent MUST inspect
   the **actual `.dart` file** by real code reading (FR-006) — not rely
   solely on the tombstone's scraped metadata.
2. **Target artefact**: write exactly one Markdown artefact at
   `.codeconv/conversion-plans/<rel>.dart.md` with the mandated
   structure (front-matter + sections 1–6, +§7 iff SCC) defined in
   `specs/017-conversion-plan-agents/contracts/conversion_plan_artefact_format.md`.
   The agent does NOT write `dart_plans` or tombstones (the Python CLI
   does, via `plan-started`/`plan-completed`).
3. **Mandated sections** (exact order; SC-004): front-matter (`path`,
   `cycle_group_id`, `scc_siblings`, `generated_at`, `source_sha256`,
   `schema_version: 1`); `## 1. Source Analysis` (grounded in actual
   `.dart` inspection); `## 2. Dart → C#/.NET Conversion Plan`
   (interface/semantics/results/observable-behaviour-preserving — each
   Dart construct → its C#/.NET equivalent with rationale);
   `## 3. Decomposed Task Units` (small, individually & reliably
   implementable units T1, T2, …, each with a one-line definition of
   done — FR-007); `## 4. Research Findings` (`none required` OR the
   separate research sub-agent's findings + provenance + the VERBATIM
   external request(s) — FR-009); `## 5. Consistency Pass` (cross-check
   §2 vs §3 vs §4 vs spec/referenced contracts — each gap either
   "fixed (pre-specified, incremental) — derived from <cite>" OR
   "ESCALATED → see §6"); `## 6. Escalations` (zero or more `### E<n>`
   entries with the five bullet fields, or the literal `None.`);
   `## 7. Cycle Siblings` **iff** `scc_siblings` non-empty (forbidden
   otherwise) — cross-reference every sibling and flag co-dependent
   decisions (FR-011).
4. **Conversion target**: **Dart → C#/.NET** (spec Assumptions;
   feature-012 clarification 2026-05-09).
5. **Escalate-don't-guess boundary (FR-008 / R6 — verbatim)**: the
   agent MAY auto-fix a consistency gap in-artefact **ONLY** when its
   resolution is **verbatim-derivable** from this feature's spec, a
   referenced feature-012/015 contract, or an explicit written project
   convention, **AND** applying it introduces **no new design decision
   and no scope change**. Any **language-semantics judgement**, any
   **mapping not already written down**, or any **scope growth** is
   NOT pre-specified/incremental: the agent MUST record a structured
   `### E<n>` escalation and MUST NOT guess or silently work around it
   (DISCIPLINE.md §1.2 "no workarounds" / §1.10 "spec authority, never
   guess"). A verbatim-derivable fix MUST cite exactly what it was
   derived from.
6. **Research-delegation rule (FR-009)**: the planning agent MUST NOT
   perform open-ended inline web research. When it needs information
   beyond the source + referenced project docs (e.g. an idiomatic
   C#/.NET equivalent for an external Dart library behaviour), it
   emits a **research request** (a scoped question, optionally with the
   relevant Dart snippet/identifiers) back to the skill and waits for
   the research sub-agent's findings, which it embeds in §4 with
   provenance.
7. **SCC awareness (FR-011)**: if `scc_siblings` is non-empty, the
   agent MUST author §7 and write its plan aware that **no sibling can
   be converted in isolation** — co-dependent type/interface decisions
   must be called out and kept consistent across siblings.

Output: a single structurally-valid artefact at the given path. Run
`codeconv planagents` will NOT validate plan quality (only structure,
via `artefact.validate`) — quality is the agent's responsibility.

## Research sub-agent prompt contract (FR-009 / R5 / Clarification Q4·Q6)

A **separate** agent (Claude Code Agent, general-purpose, with
WebSearch/WebFetch), spawned by the skill **only** on a planning
agent's research request:

1. **Input**: the scoped research question + (optionally) raw Dart
   snippets/identifiers. The research agent **MAY transmit raw Dart
   source snippets and identifiers** to external/web services when it
   judges them necessary for accurate research — the engineer has
   accepted the associated IP-exposure risk (Clarification Q4;
   third-party services may cache/index transmitted content).
2. **Output**: findings + **provenance** (source URLs/titles) + the
   **verbatim text of every external request it issued** (FR-009 audit
   requirement). Returned to the skill, which hands it back to the
   requesting planning agent for embedding in artefact §4.
3. The research agent does **NOT** write the artefact and does **NOT**
   make conversion decisions — it only supplies researched facts.
4. **Failure / timeout / empty (Clarification Q6 / R10)**: if the
   research sub-agent fails, times out, or returns nothing usable, the
   planning agent records a `### E<n>` escalation with
   `Observed: research unavailable for <topic>`, completes the rest of
   the plan **best-effort**, and the artefact is marked
   completed-with-escalation. The skill still calls `plan-completed …
   --escalations <n>` (n ≥ 1). Result: the file is `planned` for the
   planning frontier (downstream planning proceeds — FR-017) but
   conversion-blocked. The agent MUST NOT stall the file
   `plan_in_progress` on a flaky external dependency and MUST NOT
   silently substitute its own guess for the missing research.

## What planagents writes

| Target | Content |
|---|---|
| `codeconv.dart_plans` | Two-phase per-file plan state (`plan_started_at`, `plan_completed_at`, `sha256_of_dart_at_plan_start`, `plan_path`, `open_escalation_count`, `plan_run_id`). INSERT-ON-CONFLICT / UPDATE — never a bulk DELETE. |
| `codeconv.planagents_runs` | One row per orchestrator-affecting invocation (mode, metrics) — optional traceability. |
| `.codeconv/conversion-plans/<rel>.dart.md` | One conversion-plan artefact per tombstone (authored by the planning sub-agent; **checked into git**, FR-010). |
| `.codeconv/conversion-plans/_escalations-report.md` | Aggregated open escalations (checked in, FR-016; path overridable). |
| `.codeconv/tombstones/<rel>.dart.md` | `plan-started`/`plan-completed`/`stamp-tombstones` embed the four plan-state keys (appended after feature-015's six). |

## Plan-readiness lifecycle (FR-004)

`plan_pending` → `plan_ready` (derived: every SCC-external dependency is
`planned`) ; `plan-started` → `plan_in_progress` ; `plan-completed` →
`planned`. SCC members advance as a coordinated batch. An in-progress
plan does NOT unblock downstream. A plan completed WITH open escalations
still counts as `planned` for the **planning** frontier but is flagged
**conversion-blocking** (`open_escalation_count > 0`, queryable —
FR-017). Source drift (`dart_files.sha256` ≠
`sha256_of_dart_at_plan_start`) ⇒ the plan is reported **stale** and is
re-planned only under explicit `--replan` (FR-015).

## Idempotence (SC-003 / SC-008)

- A re-run on unchanged source + plan state re-plans zero files,
  creates zero duplicate `dart_plans` rows / artefacts, and yields zero
  artefact diff except each artefact's `generated_at` front-matter
  field.
- A re-`stamp-tombstones` on unchanged DB state produces zero tombstone
  diff (append-only `_FIELD_ORDER`, canonical YAML emitter).
- `--dry-run` spawns no agents and writes nothing.

## Examples

- `/codeconv-planagents status --data-dir D:/bstdev/research/glp/glpnet/.pgdb` →
  show the readiness view.
- `/codeconv-planagents --data-dir D:/bstdev/research/glp/glpnet/.pgdb` → run the
  orchestration loop: plan every plan-ready tombstone (≤7 agents
  concurrent), then aggregate escalations.
- `/codeconv-planagents next --limit 7 --json --data-dir
  D:/bstdev/research/glp/glpnet/.pgdb` → emit the next batch (no agents, no
  writes).
- `/codeconv-planagents aggregate-escalations --data-dir
  D:/bstdev/research/glp/glpnet/.pgdb` → regenerate the engineer escalations
  report.
- `/codeconv-planagents --replan stale --data-dir
  D:/bstdev/research/glp/glpnet/.pgdb` → re-plan files whose source drifted.
- `/codeconv-planagents stamp-tombstones --data-dir
  D:/bstdev/research/glp/glpnet/.pgdb` → embed plan state into every tombstone.
- `/codeconv-planagents rebuild-plans-from-tombstones --data-dir
  D:/bstdev/research/glp/glpnet/.pgdb` → restore `dart_plans` from tombstones
  after a DB wipe.

## What this skill does NOT do

- Does NOT translate `.dart` to C#/.NET (out of scope — a separate
  future downstream tool).
- Does NOT resolve escalations (the engineer does this before
  conversion).
- Does NOT recompute the depgraph / SCC / conversion `status` (feature
  015 owns it — consumed read-only).
- Does NOT modify `dart_files`, `dart_imports`, `dart_callers`,
  `dart_files_orphaned`, `discover_runs`, `dart_depgraph`, or
  `dart_conversions` (FR-020).
- Does NOT put any deterministic-state logic in the skill — the Python
  CLI is the single source of truth for state; the skill adds only
  venv/repo-root resolution + the Agent-spawn orchestration loop the
  CLI structurally cannot perform.

## Contract

`specs/017-conversion-plan-agents/contracts/planagents_cli.md` and
`contracts/agent_orchestration.md` are the source of truth. This skill
MUST stay in sync with those contracts; if you change behaviour here,
update the contract first.
