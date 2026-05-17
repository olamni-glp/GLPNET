# Phase 0 Research: 017-conversion-plan-agents

All decisions below resolve the plan template's NEEDS CLARIFICATION items. The spec is fully clarified (8 Q&A, Session 2026-05-16); the only genuine planning-phase open question is the sub-agent spawn transport (R1), explicitly deferred by spec Assumptions line 185.

---

## R1 — Sub-agent spawn transport (the central deferred decision)

**Decision**: The **skill is the agent orchestrator**; the **Python tool is the deterministic state engine**. `/codeconv-planagents` (Claude Code skill) runs the orchestration loop and uses the Claude Code **Agent tool** to spawn the ≤7 planning sub-agents and the separate research sub-agent. `codeconv planagents` (Python CLI) owns plan-readiness computation, batch selection, `dart_plans` lifecycle writes, tombstone stamping, and escalation aggregation — and spawns nothing.

**Rationale**:
- Spawning Claude sub-agents is a Claude Code **harness** capability (the Agent tool). A pure Python CLI cannot do it without adding the Anthropic SDK + an API key secret + network + per-token cost to a previously offline, deterministic, fully-`pytest`-able tool.
- The established skills (`/codeconv-discover`, `/codeconv-depgraph`) already resolve venv / repo-root / pre-execution checks; extending one of them with an orchestration loop is the minimal delta that satisfies FR-005 (≤7 concurrent, one-tombstone-each) and FR-009 (separate research agent).
- The Clarification line ("the Python tool itself is the orchestrator: it spawns the per-tombstone sub-agents and the separate research agent") is reconciled with Assumptions line 185 ("the precise mechanism … is a planning-phase decision; this spec fixes the *capability*, *concurrency cap (7)*, *one-tombstone-per-agent*, and *separate research agent* requirements, not the transport") by reading "orchestrator" as **owns readiness/selection/bookkeeping/concurrency-accounting**, while the **spawn transport** is the skill + Agent tool. The capability, the 7-cap, one-tombstone-per-agent, and the separate research agent are all delivered; only the transport is the skill rather than in-Python.

**Alternatives considered**:
- **(A) Python spawns agents via Claude Agent SDK / Anthropic API.** Rejected: introduces a network dependency, an API-key secret, and non-determinism into `codeconv`; breaks `@needs_bridge`-only test isolation; no API key is configured for this repo; per-token cost on a 128-file batch.
- **(B) Python shells out to `claude -p` headless per agent.** Rejected: fragile nested-harness, no clean ≤7 concurrency primitive, hard to test, no provenance, brittle across platforms.
- **(C) Single in-process LLM call, no sub-agents.** Rejected: violates FR-005 (≤7 concurrent, one-tombstone-each) and FR-009 (separate research agent); context bloat; no isolation.

**Surfaced for `/speckit-analyze`**: this is a deliberate deviation from the pure thin-wrapper convention (feature-012 `codeconv_tool_contract.md`, spec FR-002). Recorded in plan Complexity Tracking; analyze should confirm the deviation is correctly scoped (skill carries orchestration only; no deterministic state logic leaks into the skill).

---

## R2 — Plan-readiness predicate

**Decision**: A file is `plan_ready` iff it has **no row in `codeconv.dart_plans`** AND every **SCC-external in-subtree dependency** has `dart_plans.plan_completed_at IS NOT NULL` (or the file's SCC has no external dependencies). Intra-SCC edges are ignored for eligibility. A `plan_in_progress` row (started, not completed) does **not** unblock downstream files. This parallels feature-015's conversion-readiness predicate but is keyed on `dart_plans` (this feature's output) instead of `dart_conversions`.

**Rationale**: spec Clarification (2026-05-16, Q1) — reusing 015's conversion-`ready` was rejected (chicken/egg: conversion needs the plan). A new plan-readiness predicate keeps the *planning* frontier independent of and ahead of the *conversion* frontier (FR-004, US2). Mirroring feature-015 FR-006's "intra-SCC edges ignored; only completed unblocks" keeps the two oracles structurally consistent.

**Alternatives considered**: reuse `dart_conversions.completed_at` (rejected by spec — chicken/egg); count `plan_started_at` as unblocking (rejected — FR-004/US2-AC2: an in-progress plan must not unblock downstream, or a crashed agent would corrupt the frontier).

---

## R3 — Concurrency-cap (≤7) enforcement

**Decision**: Dual enforcement. (1) Python `codeconv planagents next` accepts `--limit N` (**default 7**) and returns at most `N` plan-ready tombstones, **never** including one already `plan_in_progress`. (2) The skill orchestration loop runs at most 7 Agent calls concurrently and only calls `next` again when a slot frees. The combination guarantees SC-001 ("at no point are more than 7 planning sub-agents active concurrently") even if the skill is interrupted/resumed.

**Rationale**: a single enforcement point is insufficient — the Python tool cannot observe live agent count, and the skill loop alone is not crash-safe. The `plan_in_progress` exclusion in `next` makes resumption idempotent (a re-invoked loop will not double-spawn an in-flight tombstone). `--limit` keeps the cap a tool contract, not skill prose.

**Alternatives considered**: skill-only cap (rejected — not crash-safe; SC-001 would depend on skill prose); Python semaphore tracking live agents (rejected — Python has no visibility into Agent-tool processes).

---

## R4 — SCC coordinated-batch planning

**Decision**: `next` groups multi-file SCC members into one **batch unit**: when an SCC becomes plan-ready (all SCC-external deps completed), `next` emits **all** its members together with `cycle_group_id` and the sibling member list. The skill spawns one planning sub-agent per member (counts toward the 7-cap individually) and passes each agent its siblings so each artefact cross-references the others. A downstream file behind the SCC becomes plan-ready only when **every** member has `plan_completed_at IS NOT NULL`.

**Rationale**: FR-011 / US3 / SC-006 — feature 015 mandates SCC members convert together; planning them in isolation would yield mutually-inconsistent plans. One artefact per member (FR-010 "exactly one per tombstone") with sibling cross-references is the minimal structure that honours both "one artefact per tombstone" and "coordinated batch".

**Alternatives considered**: one merged artefact for the whole SCC (rejected — violates FR-010's one-per-tombstone); plan SCC members independently (rejected — FR-011 / SC-006: downstream gets inconsistent plans, permanent block).

---

## R5 — Separate research sub-agent transport

**Decision**: When a planning sub-agent determines it needs information beyond the source + referenced project docs, it returns a structured **research request** to the skill loop; the skill spawns a **distinct** research sub-agent (Claude Code Agent, general-purpose, with WebSearch/WebFetch). The research agent's findings — and **every external request it issued, verbatim** — are returned to the planning agent and embedded in the artefact's research section with provenance. The planning sub-agent MUST NOT perform open-ended inline web research. The research agent MAY transmit raw Dart snippets/identifiers (engineer accepted the IP-exposure risk, Clarification Q4).

**Rationale**: FR-009 + Clarification Q4/Q6. Separation keeps planning-agent context focused and research auditable. Routing the request through the skill (not agent-to-agent directly) keeps the spawn transport uniform with R1 and lets the skill enforce the failure/timeout escalation (R10 / Clarification Q6).

**Alternatives considered**: planning agent researches inline (rejected — FR-009 explicitly forbids; context bloat, no audit); abstracted-only snippets (rejected by Clarification Q4 — too weak for accurate Dart→C#/.NET mapping).

---

## R6 — Auto-fix-vs-escalate boundary

**Decision**: **Verbatim-derivable only.** A consistency-pass gap is auto-fixed in-artefact ONLY when its resolution is verbatim-derivable from this spec, a referenced feature-012/015 contract, or an explicit written project convention, AND it introduces no new design decision and no scope change. Any language-semantics judgement, any unwritten mapping, or any scope growth ⇒ structured escalation, no guess.

**Rationale**: fixed by spec FR-008 + Clarification Q5 — not an open research question; restated here so the contract authors and the planning sub-agent prompt encode exactly this boundary. Directly implements DISCIPLINE.md §1.2 (no workarounds) / §1.10 (spec authority, never guess) as a tool requirement.

**Alternatives considered**: (both already rejected by spec Clarification Q5) "obvious/mechanical" judgement-based auto-fix (reintroduces forbidden silent guessing); never-auto-fix (floods the engineer with trivial escalations).

---

## R7 — Artefact path + git status

**Decision**: One artefact per tombstone at `.codeconv/conversion-plans/<rel>.dart.md` — a **parallel mirrored tree** of `.codeconv/tombstones/<rel>.dart.md`. Artefacts are **checked into git** (committed, like tombstones). Tombstone YAML carries plan **state** only (timestamps, count, artefact path) — NOT artefact content. The aggregated escalations report defaults to `.codeconv/conversion-plans/_escalations-report.md` (path overridable, FR-016).

**Rationale**: fixed by spec Clarification Q3/Q7 + FR-010/FR-013 — checked-in makes plans + escalation history durable, diffable, PR-reviewable, DB-wipe-survivable; the parallel tree matches the established `.codeconv/<artefact-kind>/` convention and avoids intermixing two artefact kinds in the tombstone tree. The `_`-prefixed report name sorts first and cannot collide with a `<rel>.dart.md` path.

**Alternatives considered**: (rejected by spec) gitignored/recomputable (loses resolved-escalation audit; heavy YAML round-trip); sibling-in-tombstone-tree (intermixes kinds, complicates tombstone tooling).

---

## R8 — Schema delta

**Decision**: One normative new table `codeconv.dart_plans` with exactly FR-012's columns (`path` PK FK → `dart_files.path`, `plan_started_at timestamptz NOT NULL`, `plan_completed_at timestamptz NULL`, `sha256_of_dart_at_plan_start text NOT NULL`, `plan_path text NULL`, `open_escalation_count int NOT NULL DEFAULT 0`, `plan_run_id uuid NULL`). One optional traceability table `codeconv.planagents_runs` (mirrors `codeconv.depgraph_runs` from feature 015). New Alembic revision `0003_dart_plans.py` (down-revision `0002_dart_depgraph`); all DDL `CREATE TABLE IF NOT EXISTS`; downgrade single `DROP TABLE IF EXISTS … CASCADE`. No `public`/`dbos` objects (SC-007).

**Rationale**: FR-012 fixes the column set verbatim; mirroring feature-015's `dart_conversions`/`depgraph_runs` shape keeps the two two-phase tables structurally identical and the migration trivially reviewable. `planagents_runs` is optional but planned for parity with feature-015 R5 (per-invocation provenance for `plan_run_id`).

**Alternatives considered**: fold plan state into `dart_conversions` (rejected — FR-012/Clarification Q1 require a *parallel* table; conflating planning and conversion frontiers reintroduces the chicken/egg); no runs table (acceptable but loses `plan_run_id` provenance — kept optional).

---

## R9 — Idempotence + source-drift

**Decision**: `dart_plans.sha256_of_dart_at_plan_start` snapshots `dart_files.sha256` at `plan-started`. On any run, if a file's current `dart_files.sha256` differs, the plan is reported **stale**; stale plans are excluded from "already-planned" skip logic ONLY under `--replan <selection>`. Re-planning never destroys the prior artefact's escalation history without recording it (the new artefact's escalations section retains a "superseded prior escalations" note). A re-run on unchanged source + plan state re-plans zero files, creates zero duplicate rows/artefacts, and yields zero artefact diff modulo a single timestamp metadata field (SC-003).

**Rationale**: FR-014/FR-015 + edge cases ("source drift after planning", "plan-started but never completed"). Mirrors feature-015's `sha256_of_dart_at_start` drift mechanism for cross-feature consistency. `--replan` opt-in prevents a flapping SHA from silently churning the whole frontier.

**Alternatives considered**: auto-replan on drift (rejected — FR-015 "MUST NOT silently treat a stale plan as current" but also must not silently re-plan/destroy escalation history); ignore drift (rejected — FR-015 explicit).

---

## R10 — Escalations aggregation + conversion-gating

**Decision**: `codeconv planagents aggregate-escalations` scans all artefacts and writes a single engineer-facing report (default `.codeconv/conversion-plans/_escalations-report.md`, overridable). Each escalation states file(s), observed situation, why it is not pre-specified/incremental, and the decision required. `dart_plans.open_escalation_count > 0` is the queryable conversion-blocking flag (FR-017): a plan that completes WITH open escalations still counts as `planned` for the *planning* frontier (downstream planning proceeds) but is flagged blocking for the future *conversion* step. Research-unavailable (sub-agent failure/timeout/empty) is recorded as a "research unavailable for X" escalation, the plan is completed best-effort and marked completed-with-escalation — never stalled `plan_in_progress` on a flaky external dependency, never silently guessed (Clarification Q6).

**Rationale**: FR-016/FR-017 + Clarification Q6 + US4-AC3. Decoupling planning-frontier advance from conversion-gating is exactly the user's "raise an issue … for resolution before implementation" — planning continues; conversion waits for the engineer.

**Alternatives considered**: stall `plan_in_progress` on research failure (rejected by Clarification Q6 — one flaky dependency stalls the whole frontier); block downstream planning on open escalations (rejected — FR-017: escalations gate conversion, not planning).

---

## R11 — Tombstone `_FIELD_ORDER` extension

**Decision**: Append four keys to `tombstone.py::_FIELD_ORDER`, AFTER feature-015's six (`topo_level`, `cycle_group_id`, `status`, `conversion_started_at`, `conversion_completed_at`, `target_path`): `plan_started_at`, `plan_completed_at`, `plan_path`, `open_escalation_count`. Null-vs-missing convention identical to feature 015: a key is **omitted** when there is no `dart_plans` row; **present with `null`** when the row exists but the field is NULL (e.g. `plan_completed_at` for an in-progress plan). YAML emitter settings unchanged from feature 012 (`default_flow_style=False, sort_keys=False, allow_unicode=True, width=10000`). Append-only ⇒ existing key positions unchanged ⇒ a re-stamp on unchanged data is byte-identical (SC-003).

**Rationale**: FR-013 + carry-forward of feature-012/-014/-015 idempotence. Append-only is the only extension that preserves byte-identical re-stamp for the pre-existing 14 keys.

**Alternatives considered**: interleave plan keys with conversion keys (rejected — would shift feature-015 key positions, breaking its idempotence proof); mirror artefact content into YAML (rejected by FR-010/FR-013 — content is durable in the checked-in artefact; YAML carries state only).

---

## Closed template clarifications

| Plan-template NEEDS CLARIFICATION | Resolution |
|---|---|
| Language/Version | Python 3.11+ (feature-012 `pyproject.toml`); agent layer = Claude Code Agent tool (no SDK added) — R1 |
| Primary Dependencies | stdlib + already-vendored `sqlalchemy`/`psycopg`/`PyYAML`; zero new deps — R8 |
| Storage | PGLite via unified bridge; `codeconv` schema only; new `dart_plans` (+optional `planagents_runs`) — R8 |
| Testing | `pytest` (serial by default); pure `readiness.py` no-bridge; rest `@needs_bridge`, bridge access serialised by the feature-012 OS lock (no `--test-concurrency` flag exists) — plan Technical Context |
| Project Type | Python library + CLI + Claude Code skill orchestration layer — plan Technical Context |
| Performance Goals | Python engine sub-second; end-to-end dominated by LLM agents (out of scope for hard SLA) — plan Technical Context |
| Constraints | `--data-dir C:/pglite/research/glpnet` mandatory (exFAT); FR-026/-027 carry-forward — plan Technical Context |
| Scale/Scope | 128 files / 443 edges / ≥6 isolated; 7-agent cap; 1 normative new table — plan Technical Context |
