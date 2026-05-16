# Feature Specification: codeconv-planagents — orchestrated per-tombstone Dart→C#/.NET conversion-plan generation

**Feature Branch**: `017-conversion-plan-agents`
**Created**: 2026-05-16
**Status**: Draft
**Input**: User description (verbatim, lightly normalised):

> Create up to 7 sub-agents that each read one tombstone for which all prerequisites (if any) have already been fulfilled. Each such agent must then analyse the underlying code closely by actual code inspection and create a detailed, well-researched plan for converting the source code (here Dart) into target-language code with the same or equivalent interface, semantics, results, and behaviour. If web research is needed this should, by some appropriate best-practice mechanism, be done in a separate agent and passed back to the sub-agent. When the plan has been created it must then analyse the plan in detail to split the work into small enough units of work that can be reliably implemented. After that it should check across plan and tasks and research and spec to identify issues with consistency, gaps, or ambiguity not visible before and fix them where the solution is pre-specified and incremental, or otherwise raise an issue to be passed to me (the engineer) later for resolution before implementation. Then the agents can stop and the main agent process will create a new agent with the next tombstone.

## Context

Feature 012 (codeconv-runner) delivered the `codeconv` schema, the auto-discovered Python-tool registration mechanism, the unified PGLite bridge at `.pgdb/`, and the tombstone inventory under `.codeconv/tombstones/`. Feature 015 (codeconv-depgraph) added the topologically sorted dependency graph, SCC condensation, and a two-phase conversion-readiness oracle (`codeconv.dart_depgraph` + `codeconv.dart_conversions`), plus the `mark-started` / `mark-completed` / `stamp-tombstones` / `rebuild-conversions-from-tombstones` subcommands. The conversion target is **Dart → C# / .NET** (feature 012 clarification, 2026-05-09).

What is still missing is the layer **between** "what order do I convert in?" (015) and "actually convert this file" (a future downstream tool): a mechanism that, for each Dart file that is *ready to be planned*, produces a **detailed, researched conversion plan** — grounded in actual source inspection — decomposed into reliably-implementable units of work, internally cross-checked for consistency/gaps/ambiguity, with pre-specified incremental gaps fixed automatically and everything else escalated to the engineer before any conversion is attempted.

The established reference conventions are reused without change:

- **Skill-as-thin-wrapper-around-CLI** (`/codeconv-discover`, `/codeconv-depgraph`) — the Python CLI is the source of truth; the skill forwards arguments verbatim and resolves the venv / repo-root / pre-execution checks.
- **Tool registration under `codeconv/src/codeconv/tools/<name>/`** — auto-discovered by the `codeconv` console script (feature 012, FR-006).
- **Unified PGLite at `.pgdb/`** — all reads and writes go through the bridge daemon via the protocol in `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`; concurrent invocations serialise through the single PGLite session (feature 012 US1).
- **Tombstones at `.codeconv/tombstones/<rel>.dart.md`** — checked in; YAML frontmatter carries `dependencies`, `callers`, and (post-015) `topo_level` / `cycle_group_id` / `status` / conversion keys.
- **Depgraph oracle** — `codeconv.dart_depgraph` (`topo_level`, `cycle_group_id`, `status`) is the canonical ordering and SCC source; this feature consumes it and MUST NOT recompute or redefine it.

This spec covers the *generation and bookkeeping of conversion plans* only. It does NOT cover the act of converting a `.dart` file to C# / .NET (a separate future downstream tool), nor the resolution of escalated engineering questions (the engineer does that).

## Clarifications

### Session 2026-05-16

- Q: What does "all prerequisites already fulfilled" mean — dependencies *converted* (015's `ready`) or dependencies *planned*? → A: **A new plan-readiness predicate.** A tombstone is plan-ready when every SCC-external in-subtree dependency has a **completed conversion plan** (this feature's output), tracked two-phase in a new table `codeconv.dart_plans` parallel to feature-015's `codeconv.dart_conversions`. This keeps planning ahead of conversion, lets the planning frontier advance without any conversion having occurred, and mirrors 015's two-phase design. Reusing 015's conversion-`ready` was rejected (chicken/egg: conversion needs the plan).
- Q: What is this feature, concretely? → A: **The established codeconv shape** — a robust Python tool registered under `codeconv/src/codeconv/tools/` plus a thin `/codeconv-planagents` skill wrapper (mirroring `/codeconv-discover` / `/codeconv-depgraph`). The Python tool itself is the orchestrator: it spawns the per-tombstone conversion-planning sub-agents and the separate research agent (the precise spawn mechanism — via the skill wrapper or another best-practice mechanism — is a planning-phase decision; the *capability* is in scope here).
- Q: Where do the per-file artefacts live, and is the planning unit one file or an SCC batch? → A: **One planning artefact per tombstone**, named after the tombstone and stored local to it (mirroring the `.codeconv/tombstones/<rel>.dart.md` layout). Multi-file SCCs are planned as a coordinated batch (one artefact per member, cross-referencing siblings) because feature 015 mandates that SCC members convert together. The speckit `specs/NNN-*/` layout is NOT used per file (it would create ~128 pseudo-features and collide with human feature numbering).
- Q: What may the research sub-agent transmit to external/web services? → A: **Arbitrary source snippets permitted.** The research sub-agent MAY send Dart source snippets and identifiers to external/web services when it judges them necessary for accurate research. The engineer has accepted the associated IP-exposure risk (third-party services may cache/index transmitted content). Every external request the research agent issues MUST be recorded verbatim in the artefact's research section for audit. (Rejected: abstracted-only and no-external-research — judged too weak for accurate Dart→C#/.NET mapping.)
- Q: What is the precise auto-fix vs escalate boundary for FR-008's "pre-specified and incremental"? → A: **Verbatim-derivable only.** The agent MAY auto-fix a consistency gap only when its resolution is verbatim-derivable from this spec, a referenced feature-012/015 contract, or an explicit written project convention, AND applying it introduces no new design decision and no scope change. Any language-semantics judgement, any mapping not already written down, or any scope growth MUST escalate. (Rejected: "obvious/mechanical" judgement-based auto-fix — reintroduces forbidden silent guessing; never-auto-fix — floods the engineer with trivial escalations.)
- Q: Are conversion-plan artefacts checked into git or gitignored? → A: **Checked in.** The artefacts under `.codeconv/conversion-plans/` are committed (like tombstones), making plans + escalation history durable, diffable, PR-reviewable, and DB-wipe-survivable directly. Tombstone YAML therefore carries plan *state* only (timestamps, counts, artefact path) — NOT artefact content. (Rejected: gitignored/recomputable — loses resolved-escalation audit trail and forces heavy content round-trip into YAML.)
- Q: When the research sub-agent fails, times out, or returns nothing usable, what does the planning agent do? → A: **Escalate and still complete the plan.** The planning agent records a "research unavailable for X" escalation, completes the rest of the plan on best-effort, and marks the plan completed-with-escalation: downstream *planning* proceeds (FR-017), but *conversion* is blocked until the engineer resolves it. The agent MUST NOT silently substitute its own guess for the missing research. (Rejected: stall as `plan_in_progress` — a flaky external dependency would stall the whole frontier; best-effort-no-escalation — reintroduces silent guessing exactly where research was needed.)
- Q: What does "local to the tombstone" mean for artefact placement? → A: **Parallel mirrored tree.** Artefacts live at `.codeconv/conversion-plans/<rel>.dart.md`, mirroring `.codeconv/tombstones/<rel>.dart.md` — matching the established `.codeconv/<artefact-kind>/` convention. (Rejected: sibling-in-tombstone-tree — intermixes two artefact kinds and complicates tombstone-tree tooling/round-trip.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Generate the first wave of conversion plans (plan-ready leaves) (Priority: P1)

A developer has run `/codeconv-discover` and `/codeconv-depgraph` on a checkout. They invoke `/codeconv-planagents`. The tool reads `codeconv.dart_depgraph` and `codeconv.dart_plans`, identifies every tombstone that is **plan-ready** (every SCC-external dependency already has a completed plan — initially this is exactly the depgraph leaves / isolated files), and orchestrates up to 7 concurrent planning sub-agents. Each sub-agent reads exactly one tombstone, inspects the actual Dart source, produces a single conversion-plan artefact (source analysis + Dart→C#/.NET plan preserving interface/semantics/behaviour + decomposed task units + research findings + consistency results + any escalations), and the tool records plan-started then plan-completed for that file. When an agent finishes, the orchestrator hands the next plan-ready tombstone to a new agent until none remain.

**Why this priority**: This is the headline goal — turning the readiness oracle into actual, researched, per-file conversion plans. Without it the feature does not exist.

**Independent Test**: On a checkout where `/codeconv-discover` and `/codeconv-depgraph` have run, invoke `/codeconv-planagents`. For every depgraph leaf, a conversion-plan artefact MUST exist at the tombstone-mirrored path, `codeconv.dart_plans` MUST have a row with `plan_completed_at IS NOT NULL` for that file, and each artefact MUST contain all mandated sections (analysis, plan, tasks, research, consistency, escalations).

**Acceptance Scenarios**:

1. **Given** a discovered + depgraph-computed baseline with N leaves, **When** `/codeconv-planagents` runs to completion, **Then** exactly N plan artefacts exist for those leaves, each with a completed `dart_plans` row, and no more than 7 planning agents were ever active concurrently.
2. **Given** a checkout where `/codeconv-depgraph` has never been run (`codeconv.dart_depgraph` empty), **When** `/codeconv-planagents` runs, **Then** it exits non-zero with an actionable error pointing the user at `/codeconv-depgraph` (no silent no-op, no plans written).
3. **Given** `/codeconv-planagents` has produced a plan for a file and the source `.dart` is unchanged, **When** it runs again, **Then** that file is skipped as already-planned (idempotent: no duplicate artefact, no second `dart_plans` row, plan content unchanged).

---

### User Story 2 — Advance the planning frontier (Priority: P2)

After the first wave of plans is complete, the developer re-invokes `/codeconv-planagents`. The tool recomputes plan-readiness from `codeconv.dart_plans`: files at the next `topo_level` whose every SCC-external dependency now has a completed plan become plan-ready, and the orchestrator plans them next.

**Why this priority**: This is the incremental, dependency-ordered half of the request. P2 because US1 already delivers value (leaves can be planned immediately); the frontier-advance only matters once the first wave is done.

**Independent Test**: For a chain A→B→C (A no deps, B→A, C→B) with no rows in `dart_plans`: a first run plans only A. After A's plan completes, a second run plans B (not C). After B completes, a third run plans C.

**Acceptance Scenarios**:

1. **Given** chain A→B→C and an empty `dart_plans`, **When** the tool runs, **Then** only A is selected for planning; B and C are not plan-ready.
2. **Given** the same chain with A plan-started but not plan-completed, **When** the tool runs, **Then** B remains NOT plan-ready (an in-progress plan does not unblock downstream — only `plan_completed_at IS NOT NULL` counts).
3. **Given** the same chain with A plan-completed, **When** the tool runs, **Then** B becomes plan-ready and is planned; C is still not plan-ready.

---

### User Story 3 — Plan a circular-import group as a coordinated batch (Priority: P2)

The depgraph reports a multi-file SCC (cycle_group_id shared by ≥2 files). The developer runs `/codeconv-planagents`. All members of the SCC become plan-ready together (once their SCC-external dependencies have completed plans) and are planned as one coordinated batch: each member gets its own artefact, but each artefact is written with knowledge of its sibling members (since none can be converted in isolation). Downstream files behind the SCC only become plan-ready once **every** member's plan is completed.

**Why this priority**: The 128-file baseline almost certainly contains at least one cycle (feature 015 SC-006); without coordinated-batch planning, downstream files behind a cycle are permanently blocked or get mutually-inconsistent plans.

**Independent Test**: With a 3-file SCC (A↔B↔C) and a downstream D depending on A: a run plans A, B, C as a batch (three artefacts, each referencing the other two as same-cycle siblings); D is not plan-ready until all of A, B, C have completed plans.

**Acceptance Scenarios**:

1. **Given** a 3-file SCC with no external deps, **When** the tool runs, **Then** all three members are planned in one batch and each artefact records the same `cycle_group_id` and lists its sibling members.
2. **Given** the same SCC with only A and B plan-completed (C still in progress), **When** the tool runs, **Then** a downstream file depending on any SCC member is NOT plan-ready.
3. **Given** the same SCC fully plan-completed, **When** the tool runs, **Then** downstream files whose remaining deps are also completed become plan-ready.

---

### User Story 4 — Escalate non-incremental gaps to the engineer (Priority: P2)

During the consistency pass, a planning agent finds an inconsistency, gap, or ambiguity (across the plan, the decomposed tasks, the research, and this spec / referenced contracts) for which the resolution is **not** both pre-specified and incremental. Instead of guessing, the agent records a structured escalation in the artefact's escalations section and contributes it to an aggregated engineer-facing report. Gaps whose resolution **is** pre-specified and incremental are fixed in place and noted.

**Why this priority**: Honouring the bug/ambiguity discipline (no silent workarounds or guessed behaviour) is a hard project constraint; an unescalated wrong guess corrupts every downstream plan.

**Independent Test**: Feed a tombstone whose source uses a Dart construct with no pre-specified C#/.NET mapping. The resulting artefact MUST contain an escalation entry (clear "expected vs. observed / decision needed" framing), the aggregated escalations report MUST list it, and the agent MUST NOT have silently chosen a mapping.

**Acceptance Scenarios**:

1. **Given** a pre-specified, incremental gap (e.g. a 1:1 type rename already covered by referenced conventions), **When** the consistency pass runs, **Then** the agent fixes it in the artefact and records a "fixed (pre-specified, incremental)" note — no escalation.
2. **Given** a non-incremental / unspecified gap, **When** the consistency pass runs, **Then** the agent records an escalation, leaves the affected decision unresolved (no guess), and the aggregated report includes it.
3. **Given** a file whose plan completed with one or more open escalations, **When** plan-readiness is recomputed, **Then** the plan still counts as completed for *planning-frontier* purposes (downstream planning may proceed) but the open escalations are surfaced as blocking *conversion* (resolved by the engineer before the future conversion tool runs).

---

### User Story 5 — Delegate web/external research to a separate agent (Priority: P3)

A planning agent determines it needs information it cannot get from the source or referenced project docs (e.g. an idiomatic C#/.NET equivalent for an external Dart library behaviour). It does not research inline; it issues a research request that is handled by a **separate** research sub-agent, whose findings are returned to the planning agent and embedded (with provenance) into the conversion-plan artefact.

**Why this priority**: Most files will not need external research; the separation keeps planning-agent context focused and research auditable. P3 because US1–US4 deliver the core value without it.

**Independent Test**: A tombstone requiring an external-API mapping triggers a separate research agent; the artefact's research section contains the findings with a clear provenance marker, and the planning agent's own output references those findings rather than re-deriving them.

**Acceptance Scenarios**:

1. **Given** a file needing no external research, **When** it is planned, **Then** no research agent is spawned and the research section records "none required".
2. **Given** a file needing external research, **When** it is planned, **Then** exactly the research sub-agent performs it, results are passed back, and the artefact cites them.

---

### Edge Cases

- **Depgraph never computed**: `codeconv.dart_depgraph` empty → exit non-zero with an actionable error pointing at `/codeconv-depgraph`. No plans, no `dart_plans` rows.
- **Source drift after planning**: a file's `.dart` SHA differs from `sha256_of_dart_at_plan_start` → the tool MUST flag the plan as stale (re-plan candidate) and MUST NOT silently treat the stale plan as authoritative. Re-planning is opt-in (a `--replan` selection), never destructive of the prior artefact's escalation history without record.
- **Orphaned files**: files in `codeconv.dart_files_orphaned` are not conversion targets and MUST NOT be planned.
- **Plan-started but never completed (crashed agent)**: the file is `plan_in_progress`; it does NOT unblock downstream; a re-run MUST be able to resume/replan it (idempotent recovery, no orphaned half-written artefact treated as complete).
- **All files already planned**: the tool reports "nothing to plan" and exits zero (no error).
- **Concurrent `/codeconv-planagents` invocations**: DB plan-state writes serialise through the bridge (feature 012 US1); the tool MUST NOT corrupt `dart_plans` under concurrency, though running two orchestrators simultaneously is not a supported workflow and MAY be rejected.
- **SCC member subset already planned**: if some but not all SCC members have completed plans (e.g. from an interrupted batch), the batch MUST be completable/resumable so the whole SCC ends consistent; downstream stays blocked until all members complete.
- **Agent pool starvation**: when fewer than 7 tombstones are plan-ready, the tool runs only as many agents as there are ready tombstones (no idle/blocked agents waiting on unmet prerequisites).
- **Research sub-agent failure / timeout / empty result**: the planning agent records a "research unavailable" escalation, completes the rest of the plan on best-effort, and marks it completed-with-escalation (downstream planning proceeds; conversion blocked until resolved). It MUST NOT stall the file `plan_in_progress` indefinitely on a flaky external dependency, and MUST NOT silently substitute its own guess for the missing research.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST register a new Python tool `codeconv-planagents` under `codeconv/src/codeconv/tools/planagents/` so that `codeconv list` shows it and `codeconv planagents ...` invokes it (feature-012 FR-006 auto-discovery).
- **FR-002**: System MUST provide a slash command `/codeconv-planagents` at `.claude/skills/codeconv-planagents/SKILL.md` that mirrors the `/codeconv-discover` / `/codeconv-depgraph` skill conventions for all deterministic-state interaction (venv resolution, repo-root cwd, pre-execution checks, stdout/stderr passthrough) and forwards arguments verbatim to `codeconv planagents` for every state operation (readiness, selection, lifecycle, stamping, aggregation). The skill MUST NOT contain any deterministic-state logic — the Python CLI remains the single source of truth for state. The skill DOES, however, additionally carry the sub-agent orchestration loop (spawning the ≤7 planning sub-agents and the separate research sub-agent via the Claude Code Agent tool), because spawning Claude sub-agents is a Claude Code harness capability the Python CLI structurally cannot perform. This orchestration loop is the only "skill machinery" introduced beyond the `/codeconv-discover` / `/codeconv-depgraph` pattern; its presence is required by FR-005 / FR-009 and is the planning-phase transport decision explicitly deferred to this phase by Clarification Q2 (Session 2026-05-16) and the Assumptions section ("the precise mechanism by which the Python tool spawns planning / research sub-agents … is a planning-phase decision").
- **FR-003**: System MUST read ordering, SCC membership, and conversion `status` exclusively from `codeconv.dart_depgraph` (canonical, feature 015) and the node set from `codeconv.dart_files`; it MUST NOT recompute or redefine the depgraph, and MUST NOT read `.dart` source to derive dependencies.
- **FR-004**: System MUST compute a **plan-readiness** classification for every inventoried, non-orphaned file with exactly one state: `plan_pending` (no row in `codeconv.dart_plans`), `plan_ready` (`plan_pending` AND every SCC-external in-subtree dependency has `dart_plans.plan_completed_at IS NOT NULL`, or the file's SCC has no external deps), `plan_in_progress` (row with `plan_started_at IS NOT NULL` AND `plan_completed_at IS NULL`), or `planned` (row with `plan_completed_at IS NOT NULL`). Intra-SCC edges are ignored for eligibility (mirroring feature-015 FR-006). In-progress plans DO NOT unblock downstream files.
- **FR-005**: System MUST orchestrate planning by spawning **at most 7 concurrent** per-tombstone planning sub-agents. Each sub-agent handles exactly one tombstone (one Dart file). When an agent completes, the orchestrator MUST select the next plan-ready tombstone (in feature-015 topological order; lexicographic by `path` to break ties) and spawn a new agent, until no plan-ready tombstones remain.
- **FR-006**: Each planning sub-agent MUST analyse the underlying code by **actual source inspection** of the real `.dart` file (not solely the tombstone's scraped metadata) and produce a conversion plan for translating it to C# / .NET that preserves the **same or equivalent interface, semantics, results, and observable behaviour**.
- **FR-007**: Each planning sub-agent MUST decompose its conversion plan into small, individually and reliably implementable units of work, recorded within the same artefact.
- **FR-008**: Each planning sub-agent MUST run a consistency pass that cross-checks the plan, the decomposed task units, the research findings, and this spec / referenced contracts for inconsistencies, gaps, and ambiguities not visible earlier. A resolution is **pre-specified and incremental** ONLY when it is verbatim-derivable from this spec, a referenced feature-012/015 contract, or an explicit written project convention, AND it introduces no new design decision and no scope change; in that case the agent MUST apply it in-artefact and record what it derived from. Any language-semantics judgement, any mapping not already written down, or any scope growth is NOT pre-specified/incremental: the agent MUST record a structured **escalation** and MUST NOT guess or silently work around it.
- **FR-009**: When a planning sub-agent needs information beyond the source and referenced project docs (e.g. web/external research), that research MUST be performed by a **separate** research sub-agent; its findings MUST be returned to the planning sub-agent and embedded in the artefact with provenance. The planning sub-agent MUST NOT itself perform open-ended web research inline. The research sub-agent MAY transmit raw Dart source snippets and identifiers to external/web services when it judges them necessary for accurate research (the engineer has accepted the associated IP-exposure risk). Every external request the research sub-agent issues MUST be recorded verbatim in the artefact's research section for audit.
- **FR-010**: System MUST produce exactly **one conversion-plan artefact per tombstone**, named after the tombstone and stored local to it (mirroring the `.codeconv/tombstones/<rel>.dart.md` layout — default `.codeconv/conversion-plans/<rel>.dart.md`, overridable via a flag). The artefact MUST contain, at minimum: source-analysis summary, the Dart→C#/.NET conversion plan, the decomposed task units, research findings (or "none required"), consistency-pass results, and the escalations list (possibly empty). Artefacts are **checked into git** (committed, like tombstones) and are the durable, reviewable record of plans and escalation history.
- **FR-011**: Multi-file SCCs MUST be planned as one coordinated batch: every member is planned together, each member gets its own artefact, and each artefact MUST identify its `cycle_group_id` and list its sibling members. Downstream files become plan-ready only when **every** SCC member has a completed plan.
- **FR-012**: System MUST track plan state two-phase in a new table `codeconv.dart_plans` (path PK / FK to `codeconv.dart_files.path`, `plan_started_at timestamptz NOT NULL`, `plan_completed_at timestamptz NULL`, `sha256_of_dart_at_plan_start text NOT NULL`, `plan_path text NULL`, `open_escalation_count int NOT NULL DEFAULT 0`, `plan_run_id` for traceability). A row's lifecycle: absent → started (`plan_in_progress`) → completed (`planned`). Mirrors feature-015 `codeconv.dart_conversions` shape and is created in the `codeconv` schema only (no `public`/`dbos` objects).
- **FR-013**: System MUST update the corresponding tombstone YAML frontmatter with plan-state keys ONLY (e.g. `plan_started_at`, `plan_completed_at`, `plan_path`, `open_escalation_count`) so plan *state* is rebuildable from tombstones — artefact *content* is NOT mirrored into YAML (it is durable in the checked-in artefacts per FR-010). This mirrors feature-015's stamp/round-trip discipline (DB COMMIT before the filesystem tombstone write; a post-COMMIT FS failure warns + exits non-zero and is reconciled on the next stamp).
- **FR-014**: System MUST be idempotent: a re-run on unchanged source + plan state MUST NOT re-plan already-`planned` files, MUST NOT create duplicate artefacts or duplicate `dart_plans` rows, and MUST produce unchanged artefact content for unchanged inputs (modulo a timestamp metadata field).
- **FR-015**: System MUST detect source drift — if a file's current `.dart` SHA differs from `sha256_of_dart_at_plan_start`, the file MUST be reported as having a **stale** plan and excluded from "already planned" skip logic only under an explicit `--replan` selection; the tool MUST NOT silently treat a stale plan as current.
- **FR-016**: System MUST aggregate all open escalations across all artefacts into a single engineer-facing report artefact (path overridable) so the engineer can resolve them before any conversion. An escalation MUST clearly state the file(s), the observed situation, why it is not pre-specified/incremental, and the decision required.
- **FR-017**: A plan that completes **with** open escalations still counts as `planned` for plan-frontier purposes (downstream planning may proceed), but its open escalations MUST be surfaced as **blocking conversion** (the future conversion tool / engineer must resolve them first). This MUST be queryable (e.g. `open_escalation_count > 0`).
- **FR-018**: System MUST exit non-zero with a clear, actionable error when `codeconv.dart_depgraph` is empty or absent (instruct the user to run `/codeconv-depgraph`), and MUST exit zero with a "nothing to plan" message when every file is already `planned`.
- **FR-019**: System MUST support `--dry-run` (compute plan-readiness and what would be planned; spawn no agents; write nothing to DB, tombstones, or artefacts), `--json` (machine-readable run summary on stdout), `--quiet` (suppress per-step logging), and `--replan <selection>` (force re-planning of specified / stale files). It MUST honour the global `--repo-root` and `--data-dir` flags inherited from the `codeconv` console script (feature 012); `--data-dir C:/pglite/research/glpnet` is mandatory on this exFAT checkout.
- **FR-020**: System MUST confine its writes to: (a) `codeconv.dart_plans` (new), (b) the conversion-plan artefacts under the configured artefact root, (c) the aggregated escalations report, and (d) the plan-state YAML keys in tombstones. It MUST NOT modify `codeconv.dart_files`, `codeconv.dart_imports`, `codeconv.dart_callers`, `codeconv.dart_files_orphaned`, `codeconv.discover_runs`, `codeconv.dart_depgraph`, or `codeconv.dart_conversions`.
- **FR-021**: System MUST emit deterministic ordering — plan-ready tombstones are selected by ascending `topo_level` then lexicographic `path`; artefact section order and JSON keys are stable; SCC members are ordered lexicographically within the batch.

### Key Entities *(include if feature involves data)*

- **dart_plan** (`codeconv.dart_plans`): one row per file once planning begins. `path` (PK, FK → `codeconv.dart_files.path`), `plan_started_at`, `plan_completed_at` (NULL until done), `sha256_of_dart_at_plan_start` (drift detection), `plan_path` (artefact location), `open_escalation_count`, `plan_run_id`. Lifecycle: absent → in-progress → completed. No DELETE workflow in v1. Parallel to feature-015 `codeconv.dart_conversions`.
- **conversion-plan artefact**: one Markdown document per tombstone, mirrored at `.codeconv/conversion-plans/<rel>.dart.md`. Sections: source analysis, Dart→C#/.NET conversion plan (interface/semantics/behaviour-preserving), decomposed task units, research findings (with provenance), consistency-pass results, escalations list, and (for SCC members) cycle siblings.
- **escalation**: a structured engineer-facing open question raised when a consistency gap is not pre-specified+incremental. Attributes: file(s), observed situation, why not auto-resolvable, decision required. Aggregated into the escalations report; counted in `dart_plans.open_escalation_count`; blocks conversion, not planning.
- **research request/response**: a scoped question the planning agent hands to the separate research agent, and the returned findings (with provenance) embedded in the artefact.
- **orchestrator run**: one `/codeconv-planagents` invocation; selects plan-ready tombstones in topo order, maintains ≤7 concurrent planning agents, spawns the research agent on demand, records metrics for traceability.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a discovered + depgraph-computed baseline, a first `/codeconv-planagents` run produces exactly one conversion-plan artefact for every depgraph leaf, each with a completed `codeconv.dart_plans` row, and at no point are more than 7 planning sub-agents active concurrently.
- **SC-002**: For every dependency edge `(A → B)` that crosses SCCs, A is never selected for planning before B has `plan_completed_at IS NOT NULL` — verifiable by an SQL self-join over `codeconv.dart_imports` × `codeconv.dart_plans` × `codeconv.dart_depgraph`.
- **SC-003**: A re-run on unchanged source + plan state re-plans zero files, creates zero duplicate artefacts and zero duplicate `dart_plans` rows, and yields zero artefact-content diff (modulo a timestamp field) — idempotence mirroring feature-015 SC-002.
- **SC-004**: Every produced artefact contains all mandated sections (analysis, plan, decomposed tasks, research, consistency, escalations), verifiable by a structural check over the artefact root.
- **SC-005**: No artefact contains a silently-guessed resolution for a non-pre-specified gap: every such gap appears as an escalation in both the artefact and the aggregated report (sampling/audit verifiable; zero un-escalated unresolved gaps).
- **SC-006**: For a synthetic multi-file SCC fixture, all members are planned in one batch with cross-references to each other, and no downstream file is planned until every member's plan is completed.
- **SC-007**: Schema isolation is preserved — `codeconv.dart_plans` exists only in the `codeconv` schema; no objects are created in `public` or `dbos` (mirroring feature-012 FR-015 / feature-015 SC-007).
- **SC-008**: `/codeconv-planagents --dry-run` spawns no agents and writes nothing (verifiable by `git status` showing no artefact/tombstone changes and `SELECT count(*) FROM codeconv.dart_plans` unchanged).
- **SC-009**: After a full pass over the current baseline, every non-orphaned inventoried file is either `planned` (possibly with recorded escalations) or explicitly blocked behind a recorded escalation/stale-source flag — there is no file left in an undiagnosed state.

## Assumptions

- `/codeconv-discover` and `/codeconv-depgraph` have both been run before `/codeconv-planagents`; the tool detects an empty/absent `codeconv.dart_depgraph` and fails loudly (FR-018) but does not auto-run them.
- The conversion target is Dart → C# / .NET (feature 012 clarification, 2026-05-09). If feature 016's langpair registry lands, the target becomes langpair-driven; this spec assumes the single Dart→C#/.NET pair as the v1 default and does not depend on 016 being merged.
- The unified PGLite bridge at `.pgdb/` is the only DB target (feature 012); no new bridge/sidecar is introduced; concurrent DB writes serialise through the single PGLite session.
- The precise mechanism by which the Python tool spawns planning / research sub-agents (via the skill wrapper or another best-practice mechanism) is a planning-phase decision; this spec fixes the *capability*, *concurrency cap (7)*, *one-tombstone-per-agent*, and *separate research agent* requirements, not the transport.
- A plan completing with open escalations is acceptable for advancing the *planning* frontier; escalations gate the future *conversion* step, not downstream planning (FR-017) — derived from the user's "raise an issue ... for resolution before implementation".
- The artefact root defaults to `.codeconv/conversion-plans/` mirroring tombstone layout and is **checked into git** (resolved in Clarifications 2026-05-16); tombstone YAML carries plan state only, not artefact content.
- `codeconv` schema changes (the `dart_plans` table) are added via a new idempotent Alembic revision under `codeconv/src/codeconv/db/migrations/versions/`; no data migration of feature-012/015 tables is needed.
- The "pre-specified and incremental" auto-fix boundary is fixed by FR-008 (verbatim-derivable only; resolved in Clarifications 2026-05-16) — no longer an open question.

## Out of Scope

- Actually converting `.dart` files to C# / .NET — a separate future downstream codeconv tool.
- Resolving escalated engineering questions — the engineer does that before conversion.
- Recomputing or redefining the depgraph / SCC / conversion `status` — owned by feature 015.
- Semantic enrichment of tombstone `purpose` / `key_idea` — mechanical-only per feature-012 FR-020.
- Cross-process bridge coordination — feature 012 owns this; this tool is just another consumer.
- A graphical view of plan progress — JSON + SQL + artefacts suffice for v1.
- Choosing an optimal multi-file conversion batching beyond honouring feature-015 SCC groups.
