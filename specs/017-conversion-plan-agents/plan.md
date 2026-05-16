# Implementation Plan: codeconv-planagents — orchestrated per-tombstone Dart→C#/.NET conversion-plan generation

**Branch**: `017-conversion-plan-agents` | **Date**: 2026-05-16 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/017-conversion-plan-agents/spec.md` (fully clarified, Session 2026-05-16 — 8 Q&A entries)

## Summary

Add a Python tool `codeconv planagents` (auto-discovered by feature-012's runner registry) plus a `/codeconv-planagents` slash skill that — unlike the pure thin wrappers `/codeconv-discover` and `/codeconv-depgraph` — also drives Claude Code sub-agents. The Python tool is the **deterministic state engine**: it consumes feature-015's canonical `codeconv.dart_depgraph` (ordering + SCC), computes a new **plan-readiness** classification against a new two-phase table `codeconv.dart_plans` (parallel to feature-015's `codeconv.dart_conversions`), selects the next plan-ready batch in topological order with SCC-batch grouping, records `plan_started` / `plan_completed`, stamps plan-state YAML keys into tombstones, aggregates open escalations into a single engineer report, and supports `--dry-run` / `--json` / `--quiet` / `--replan`. The **skill is the agent orchestrator**: it loops `next → spawn ≤7 planning sub-agents (one tombstone each) → spawn a separate research sub-agent on demand → mark plan-completed`, until no plan-ready tombstones remain. Each planning sub-agent inspects the **real `.dart` source**, produces one checked-in conversion-plan artefact at `.codeconv/conversion-plans/<rel>.dart.md` (source analysis + Dart→C#/.NET plan preserving interface/semantics/behaviour + decomposed task units + research findings + consistency-pass results + escalations), and escalates any non-verbatim-derivable gap rather than guessing.

**Technical approach** (validated against `codeconv/src/codeconv/db/migrations/versions/0002_dart_depgraph.py`, `codeconv/src/codeconv/runner.py::tool_registry`, `codeconv/src/codeconv/tools/depgraph/{__init__.py,workflow.py,algorithm.py,tombstone_writer.py}`, `codeconv/src/codeconv/tools/discover/tombstone.py`, `.claude/skills/codeconv-depgraph/SKILL.md`, and the four feature-015 contracts):

1. **New Alembic revision** `0003_dart_plans.py` adds, under the existing `codeconv` schema only, `dart_plans` (per-file two-phase plan state, `path` PK FK → `dart_files`) and an optional `planagents_runs` traceability table (mirrors `depgraph_runs`). All DDL is `CREATE TABLE IF NOT EXISTS`; downgrade is a single `DROP TABLE IF EXISTS … CASCADE`. Schema isolation (FR-007/SC-007 carry-forward) preserved — no `public`/`dbos` objects.
2. **New tool subpackage** `codeconv/src/codeconv/tools/planagents/` registered automatically by the runner's `pkgutil.iter_modules` scan (feature 012 FR-006). Modules: `__init__.py` (Typer `app`), `readiness.py` (pure plan-readiness predicate + topo/SCC batch selection over a depgraph snapshot — unit-testable with no bridge), `workflow.py` (bridge acquire; `next`/`plan-started`/`plan-completed`/`aggregate-escalations`/`stamp-tombstones`/`rebuild-plans-from-tombstones`/`status`), `tombstone_writer.py` (append-only plan-state YAML keys), `artefact.py` (artefact + escalations-report path resolution and structural validation — content authoring is the agent's job, not Python's).
3. **CLI surface**: `codeconv planagents [status|next|plan-started|plan-completed|aggregate-escalations|stamp-tombstones|rebuild-plans-from-tombstones]`. `status` is the default (bare `codeconv planagents` shows the readiness view; spawns nothing). Read-only/observability flags (`--json`, `--quiet`, `--dry-run`, `--replan`, `--json-out`) mirror feature-015's shape; global `--repo-root` / `--data-dir` inherited from the console script (FR-019).
4. **Slash skill** `.claude/skills/codeconv-planagents/SKILL.md` — structurally derived from `.claude/skills/codeconv-depgraph/SKILL.md` for venv/repo-root/pre-execution-check resolution, **plus an orchestration loop** that uses the Claude Code Agent tool to spawn the ≤7 planning sub-agents and the separate research sub-agent. This is a **deliberate, justified deviation** from the pure thin-wrapper convention (see Complexity Tracking) because spawning Claude sub-agents is a Claude Code harness capability, not a Python-CLI capability; the Python tool remains pure, deterministic, and testable.
5. **Tombstone round-trip** appends four plan-state YAML keys (`plan_started_at`, `plan_completed_at`, `plan_path`, `open_escalation_count`) to the existing `_FIELD_ORDER` tuple in `tombstone.py`, AFTER feature-015's six keys — preserving the feature-012/-014/-015 idempotence guarantee (canonical YAML, sorted lists, pinned key order). Artefact *content* is NOT mirrored into YAML (it is durable in the checked-in artefacts, FR-010/FR-013).

Net code touched: ~150–220 lines of Python in `codeconv/src/codeconv/tools/planagents/` (new), ~6 lines in `tombstone.py` (extend `_FIELD_ORDER`), 1 new Alembic revision (~70 lines), 1 new `SKILL.md` (~140 lines — larger than the depgraph skill because it carries the agent-orchestration loop and the planning/research sub-agent prompt contracts). No change to feature-012/-014/-015 surfaces beyond the append-only `_FIELD_ORDER` extension.

## Technical Context

**Language/Version**: Python 3.11+ (matches `codeconv/pyproject.toml` from feature 012). Sub-agent layer: Claude Code Agent tool (harness capability — no SDK/API key added to the Python package).
**Primary Dependencies**: stdlib only for the readiness predicate (no graph library — the graph is already condensed by feature 015); `sqlalchemy>=2.0` + `psycopg[binary]` (vendored, feature 012); `PyYAML` (vendored). No new Python dependency.
**Storage**: PGLite via the unified bridge — `codeconv.dart_depgraph` (read, feature 015 — canonical ordering/SCC/status; MUST NOT recompute), `codeconv.dart_files` (read — node set + `sha256` for drift), `codeconv.dart_files_orphaned` (read — exclusion), `codeconv.dart_plans` (NEW — read+write), `codeconv.planagents_runs` (NEW, optional — write). All under the `codeconv` schema (SC-007).
**Testing**: `pytest codeconv/tests/`. Pure `readiness.py` tests need no bridge. Bridge-needing tests gated by `@needs_bridge` (feature 012 contract), serialised via `--test-concurrency=1` (PGLite cold-init ~7 s on Windows; memory `project_pglite_cold_init_windows.md`). Agent-orchestration is validated by a fixture-driven dry-run + a mocked-agent harness (the Python tool's contract surface is deterministic and fully testable without spawning real LLM agents).
**Target Platform**: Windows 11 primary (this checkout); cross-platform-portable Python; no Windows-only APIs.
**Project Type**: Python library + CLI inside the `codeconv/` subtree of a polyglot monorepo, with a Claude Code skill orchestration layer.
**Performance Goals**: The Python engine is trivially sub-second (one PGLite read of ~571 depgraph rows + an in-memory readiness pass O(V+E) + ≤128 UPSERTs). End-to-end wall time is dominated by the LLM planning sub-agents (out of the Python tool's control and out of scope for a hard SLA); the Python contract guarantees `next`/`plan-*`/`status` each return in ≤5 s on a warm bridge.
**Constraints**: `--data-dir C:/pglite/research/glpnet` mandatory on this exFAT checkout (memory `project_codeconv_data_dir_exfat.md`; `docs/known-issues.md` Issue 8; CLI guard exits 64 on non-NTFS). Carry-forward feature-012 FR-026 (no `COPY … FROM STDIN`) / FR-027 (no client-side prepared-statement caching): this feature's SQL is `SELECT … FROM codeconv.dart_depgraph/dart_files`, plain `INSERT … ON CONFLICT DO UPDATE` against `dart_plans`, and no DELETE-all (rows are append-then-update per FR-012 lifecycle).
**Scale/Scope**: 128 inventoried files, 443 in-subtree edges, ≥6 isolated nodes (post-feature-014). First wave = depgraph leaves/isolated files. Concurrency cap = 7 planning sub-agents. 0 new schemas (reuses `codeconv`), 1 normative new table (`dart_plans`) + 1 optional (`planagents_runs`), 0 new Python dependencies.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` contains only unfilled template placeholders (`[PRINCIPLE_1_NAME]`, `[GOVERNANCE_RULES]`, …) — no concrete project principles ratified. Per the spec-first discipline in `CLAUDE.md` and `docs/DISCIPLINE.md` (the operative authority for this repo), the relevant gates are:

| Gate (CLAUDE.md / DISCIPLINE.md) | Pass? | Note |
|---|---|---|
| §"Spec-First Development — No Implementation Without Spec" | PASS | spec.md present, fully clarified (8 Q&A 2026-05-16), checklist green |
| DISCIPLINE.md §1.1 "Specification-First" | PASS | this plan derives entirely from spec FRs; no new behaviour invented |
| DISCIPLINE.md §1.4 "Traceability" | PASS | every artefact below cites its spec FR and the feature-012/-015 mechanism it extends |
| DISCIPLINE.md §1.7 "Errors, not 'limitations'" | PASS | the planning/conversion gap is named a feature gap, not a "limitation" |
| DISCIPLINE.md §1.2 "No Workarounds" / §1.10 "spec authority" | PASS by design | the auto-fix-vs-escalate boundary (FR-008, verbatim-derivable only) is exactly the no-silent-guessing discipline encoded as a tool requirement |
| DISCIPLINE.md §2.2 "Test baseline before/after" | PASS by design | tasks.md sequences baseline-pytest BEFORE code change and re-run AFTER each step |
| Feature 012 contract preserved (FR-006 auto-discovery; FR-015 schema isolation; FR-022 tombstone round-trip) | PASS | new table stays in `codeconv`; tool registers by FS convention; plan-state round-trips through tombstones |
| Feature 015 contract preserved | PASS | reads `dart_depgraph` read-only; MUST NOT recompute the graph/SCC/status; appends YAML keys after 015's six |
| §"Skill-as-thin-wrapper-around-CLI" convention | **DEVIATION — justified** | the skill carries an agent-orchestration loop (it must, to spawn Claude sub-agents — a harness capability the Python CLI does not have). The Python tool itself stays pure/deterministic. See Complexity Tracking + research R1. |

**Result**: GATE PASSED with one justified deviation recorded in Complexity Tracking. Re-checked post-Phase-1: the Phase-1 contracts confine all LLM-judgement to the agent layer and keep every Python surface deterministic and unit-testable — the deviation does not widen.

## Project Structure

### Documentation (this feature)

```text
specs/017-conversion-plan-agents/
├── plan.md                                   # This file (/speckit-plan output)
├── spec.md                                   # Feature spec (written, fully clarified)
├── checklists/requirements.md                # Spec quality checklist (already passing)
├── research.md                               # Phase 0 — R1-R11 (this run)
├── data-model.md                             # Phase 1 — one new table (+optional) + four new tombstone keys
├── quickstart.md                             # Phase 1 — Flow I (planagents end-to-end)
├── contracts/
│   ├── plan_readiness_algorithm.md           # Phase 1 — plan-readiness predicate + topo/SCC batch selection
│   ├── planagents_cli.md                     # Phase 1 — CLI subcommand surface + skill orchestration loop
│   ├── planagents_schema.md                  # Phase 1 — DDL contract for dart_plans (+ planagents_runs)
│   ├── conversion_plan_artefact_format.md    # Phase 1 — artefact + escalation structure; tombstone YAML delta; idempotence proof
│   └── agent_orchestration.md                # Phase 1 — planning/research sub-agent prompt contracts; ≤7 cap; SCC batch
└── tasks.md                                  # Phase 2 output — /speckit-tasks (next chained command)
```

### Source Code (repository root)

This feature touches only `codeconv/` and `.claude/skills/`. No Dart, .NET, Node, or `glp_runtime/` change.

```text
codeconv/
├── src/codeconv/
│   ├── tools/
│   │   └── planagents/                               # NEW — tool subpackage (auto-discovered by runner.py)
│   │       ├── __init__.py                           # NEW — Typer app (status/next/plan-*/aggregate/stamp/rebuild)
│   │       ├── readiness.py                          # NEW — pure plan-readiness predicate + topo/SCC batch selection
│   │       ├── workflow.py                           # NEW — bridge acquire; DB read/write; orchestration primitives
│   │       ├── tombstone_writer.py                   # NEW — append-only plan-state YAML key stamp/rebuild
│   │       └── artefact.py                           # NEW — artefact/escalations-report path + structural validation
│   ├── tools/discover/
│   │   └── tombstone.py                              # MODIFIED — extend _FIELD_ORDER with four plan-state keys (after feature-015's six)
│   └── db/migrations/versions/
│       └── 0003_dart_plans.py                        # NEW — Alembic revision: dart_plans (+ optional planagents_runs)
└── tests/
    ├── test_planagents_readiness.py                  # NEW — pure unit tests for the predicate + SCC batch (no bridge)
    ├── test_planagents_next.py                       # NEW — @needs_bridge: topo order, ≤7 cap, leaf-first selection
    ├── test_planagents_lifecycle.py                  # NEW — @needs_bridge: plan-started/plan-completed semantics + idempotence
    ├── test_planagents_scc_batch.py                  # NEW — @needs_bridge: synthetic SCC batch (US3 / SC-006)
    ├── test_planagents_escalations.py                # NEW — @needs_bridge: open_escalation_count + aggregated report (FR-016/17, SC-005)
    ├── test_planagents_stale.py                      # NEW — @needs_bridge: source-drift / --replan (FR-015)
    ├── test_planagents_stamp_rebuild.py              # NEW — @needs_bridge: tombstone round-trip idempotent (FR-013, SC-003)
    ├── test_planagents_dry_run.py                    # NEW — @needs_bridge: --dry-run writes nothing (SC-008)
    └── test_planagents_schema_isolation.py           # NEW — verifies SC-007 (codeconv schema only)

.claude/skills/
└── codeconv-planagents/                              # NEW — skill: venv/repo-root resolver + agent-orchestration loop
    └── SKILL.md                                      # NEW — derived from codeconv-depgraph/SKILL.md + orchestration loop + sub-agent prompt contracts

.codeconv/
├── conversion-plans/                                 # NEW (checked in, FR-010) — one <rel>.dart.md artefact per tombstone
│   └── _escalations-report.md                        # NEW (checked in, FR-016) — aggregated open escalations (path overridable)
└── tombstones/<rel>.dart.md                          # MODIFIED (checked in) — four appended plan-state YAML keys
```

**Structure Decision**: Single-project Python additions inside the existing `codeconv/` package, mirroring feature 015's structure decision (no new top-level directory, no new language). One Alembic revision; one slash skill; one new tool subpackage; the subpackage is the unit of registration (feature 012 FR-006) so no runner edits. The four plan-state YAML keys are appended at the END of `_FIELD_ORDER` (after feature-015's six) so the extension is append-only and idempotence is preserved. The single architectural difference from feature 015 — the skill carries an orchestration loop rather than being a pure pass-through — is isolated to `SKILL.md` and justified in Complexity Tracking; the Python tool surface remains as pure and deterministic as the depgraph tool's.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| `/codeconv-planagents` skill is NOT a pure thin wrapper — it carries an agent-orchestration loop and the planning/research sub-agent prompt contracts (deviates from the `/codeconv-discover` / `/codeconv-depgraph` convention codified in feature-012 `codeconv_tool_contract.md` and spec FR-002). | The spec (FR-005/FR-009 + Assumptions) requires spawning ≤7 concurrent Claude planning sub-agents and a separate research sub-agent. Spawning Claude sub-agents is a Claude Code **harness** capability (the Agent tool); a pure Python CLI cannot do it without adding the Anthropic SDK + an API key + network + per-token cost to a previously offline, deterministic, fully-unit-testable tool. The skill is the only layer that natively has the Agent capability. | (a) **Python spawns agents via Claude Agent SDK / Anthropic API** — adds a network dependency, an API-key secret, and non-determinism to `codeconv`; breaks `@needs_bridge`-only test isolation; no API key is configured for this repo. (b) **Python shells out to `claude -p` headless per agent** — fragile nested-harness, no clean ≤7 concurrency primitive, hard to test, no provenance. (c) **No sub-agents; single in-process LLM call** — violates FR-005 (≤7 concurrent, one-tombstone-each) and FR-009 (separate research agent). The chosen split keeps the Python tool pure and pushes only the irreducibly-LLM work (source analysis, plan authoring, research judgement, consistency reasoning) into the agent layer where it belongs; FR-002's "thin wrapper" intent (no business logic in the skill, CLI is source of truth for *state*) is still honoured for all deterministic state — the skill adds only orchestration the CLI structurally cannot perform. |

## Phase 0: Research outputs

See [research.md](./research.md) for:

- **R1**: Sub-agent spawn transport — skill-as-orchestrator + Claude Code Agent tool; Python tool is the deterministic state engine (resolves the spec's deferred "spawn mechanism" assumption; reconciles the Clarification's "the Python tool is the orchestrator" with Assumptions line 185).
- **R2**: Plan-readiness predicate — parallels feature-015 readiness but keyed on `dart_plans.plan_completed_at`, not `dart_conversions`; intra-SCC edges ignored; in-progress plans do NOT unblock downstream.
- **R3**: Concurrency-cap enforcement (≤7) — dual: Python `next` never emits an already-`plan_in_progress` tombstone and honours `--limit` (default 7); the skill runs at most 7 Agent calls concurrently.
- **R4**: SCC coordinated-batch planning — `next` groups SCC members; one artefact per member with sibling cross-reference; downstream blocked until ALL members `plan_completed` (FR-011 / US3 / SC-006).
- **R5**: Separate research sub-agent transport — the skill spawns a distinct research Agent on the planning agent's request; findings + every verbatim external request embedded in the artefact (FR-009).
- **R6**: Auto-fix-vs-escalate boundary — verbatim-derivable-only (fixed by spec FR-008/Clarification; restated, no open decision).
- **R7**: Artefact path + git status — `.codeconv/conversion-plans/<rel>.dart.md`, checked in; tombstone YAML carries plan *state* only (FR-010/FR-013).
- **R8**: Schema delta — new `codeconv.dart_plans` (FR-012 exact columns) + optional `codeconv.planagents_runs`; Alembic `0003_dart_plans.py`; schema isolation.
- **R9**: Idempotence + source-drift — `sha256_of_dart_at_plan_start`; drift ⇒ stale; `--replan` opt-in; never destructive of escalation history without record (FR-014/FR-015).
- **R10**: Escalations aggregation + conversion-gating — single `_escalations-report.md`; `open_escalation_count` queryable; gates *conversion*, not *planning* (FR-016/FR-017).
- **R11**: Tombstone `_FIELD_ORDER` extension — four keys appended after feature-015's six; null-vs-missing convention; append-only idempotence proof.

All NEEDS CLARIFICATION items raised by the plan template are closed in research.md (the spec is fully clarified; the only genuine planning-phase open question — the spawn transport — is resolved in R1 and surfaced for `/speckit-analyze`).

## Phase 1: Design artefacts

- **[data-model.md](./data-model.md)** — explicit delta against feature-012/-015 data models. **One normative new table** `codeconv.dart_plans` (+ one optional `codeconv.planagents_runs`); **four new tombstone YAML keys** appended after feature-015's six; no change to any existing column/row/constraint. Alembic revision `0003_dart_plans.py`; downgrade is a single `DROP TABLE IF EXISTS … CASCADE`.
- **[contracts/plan_readiness_algorithm.md](./contracts/plan_readiness_algorithm.md)** — the plan-readiness predicate over (`dart_depgraph`, `dart_imports` cross-SCC edges, `dart_plans`), the four-state classification (`plan_pending`/`plan_ready`/`plan_in_progress`/`planned`), the topo+lexicographic selection order, the SCC-batch grouping rule, and the FR-004/SC-002 correctness invariant.
- **[contracts/planagents_cli.md](./contracts/planagents_cli.md)** — `codeconv planagents [status|next|plan-started|plan-completed|aggregate-escalations|stamp-tombstones|rebuild-plans-from-tombstones]` signature, flag semantics, exit codes, JSON shapes, idempotence contracts, and the skill orchestration-loop pseudocode that consumes `next`/`plan-*`.
- **[contracts/planagents_schema.md](./contracts/planagents_schema.md)** — DDL contract for `codeconv.dart_plans` (FR-012 columns/PK/FK/constraints) and optional `codeconv.planagents_runs`; the append-then-UPDATE lifecycle write protocol; schema-isolation assertion.
- **[contracts/conversion_plan_artefact_format.md](./contracts/conversion_plan_artefact_format.md)** — the mandated artefact section structure, the structured escalation schema, the aggregated escalations-report schema, the four-key tombstone YAML delta with null-vs-missing semantics, and the append-only idempotence proof (SC-003/SC-004).
- **[contracts/agent_orchestration.md](./contracts/agent_orchestration.md)** — the planning sub-agent prompt contract (inputs, mandatory artefact sections, escalate-don't-guess discipline), the separate research sub-agent contract (provenance + verbatim external-request logging + failure/timeout escalation), the ≤7 concurrency protocol, and the SCC coordinated-batch protocol.

The agent context file (`CLAUDE.md`) was updated this run to reference this plan between the existing `<!-- SPECKIT START -->` / `<!-- SPECKIT END -->` markers, replacing the prior reference to feature 015's plan.
