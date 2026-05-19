# Implementation Plan: codeconv-builder — Unified, DBOS-durable Conversion Workbench

**Branch**: `018-codeconv-builder` | **Date**: 2026-05-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/018-codeconv-builder/spec.md` (clarified Session 2026-05-17 — 3 Q&A; operator decisions FR-022=refactor, FR-023=spec-only, FR-024=official-docs-authoritative; planning decision D1=(a) full DBOS activation, D2=thin durable orchestrator over existing entrypoints)

## Summary

Consolidate features 015 (depgraph + readiness oracle), 016 (init/scaffold/mirror + langpair registry) and 017 (planagents) — plus a **new** `codeconv-convspec` capability — into one coherent, **DBOS-durable** conversion workbench driven by a single top-level `codeconv builder` tool and a `/codeconv-builder` skill.

**The two load-bearing planning decisions (resolved with Gabi 2026-05-17, surfaced again for the analyze/clarify gate):**

- **D1 = (a) — full, clearly-specified DBOS activation.** Today the codeconv package has the DBOS *plumbing* (the `dbos` dependency, the `set_dbos`/`get_dbos` process-singleton in `runner.py`, the vendored `_vendor/dbos_pglite_patch.py` uuid-ossp shim, a per-tool `register_workflows(dbos_app)` surface, and a working `setup_dbos(endpoint)` launch in `codeconv migrate`) but **every tool's `register()` is a dormant no-op** and durability is currently only the two-phase DB-state + tombstone round-trip idempotence model. Feature 018 **activates** that reserved integration: the builder pipeline runs as real `@DBOS.workflow` / `@DBOS.step` units so DBOS provides **persistence, resumability, completability, recoverability, and queryable workflow-trace analysis** (debugging / planning / observability) — the spec's word "DBOS" made literal, not nominal.
- **D2 = thin durable orchestrator over existing tool entrypoints (refactor, no rewrite) — integration MUST be robust and maintainable and MUST NOT change existing flows unnecessarily.** The builder does **not** reimplement discover/depgraph/init/scaffold/mirror/planagents. Each existing tool's already-tested pure entrypoint is called **verbatim** as the body of a `@DBOS.step`; the builder is the `@DBOS.workflow` that calls them in feature-015 topological / SCC order with the per-(file, stage) durable unit. The proven two-phase (`*_started_at`/`*_completed_at`) + tombstone-round-trip model is **kept** as the business projection; DBOS durability/recovery/trace is layered *around* it, never *instead of* it. Only the genuinely *overlapping* concepts are unified into shared definitions: the workspace model, the status/escalation vocabulary, and the single linear migration chain (FR-015/FR-022). This is the parsimonious reading of "behaviour preserved, no capability regression".

  **D2 governing principle (Gabi, 2026-05-17, emphatic).** Where DBOS offers more than one integration option, the option that **disturbs an existing, working flow the least** wins. No proven flow may be obliterated and replaced by an unproven, confabulated "more idiomatic" DBOS alternative. Any DBOS-option choice that would change an existing flow MUST (a) be justified against the working baseline it changes, (b) preserve that flow's observable behaviour, and (c) be rejected in favour of the wrapping option if the wrapping option meets the requirement. "Robust and maintainable" means: existing entrypoints unchanged, the durable layer additive and isolated to `durable/`, and every deviation traceable to a spec FR — not to DBOS-idiom aesthetics. This principle is a Constitution-Check gate (below) and the controlling rule for research R1/R8/R12.

**`codeconv-convspec`** (new, P1, co-critical with the durable pipeline) performs, per Dart file, an **agent/LLM-driven deep source-code analysis** + **thorough official-docs-authoritative web research** on the Dart→C#/.NET conversion nuances of each non-trivial construct, and emits a **structured, machine-consumable per-file conversion spec carrying embedded human-readable rationale + research provenance** (FR-011), recording reusable decisions into a persistent **conversion-idiom knowledge base** (FR-012) so research is never redundantly repeated and cross-file decisions stay consistent (FR-024 caching). convspec is **spec-only** (FR-023): no compilable C# is emitted by it. Each per-file analysis/research/spec/idiom-write is a DBOS step (feature-017 planagents-style agent orchestration carried by the skill; Python tool stays deterministic).

**Migration linearization (FR-015)** is unambiguous and confirmed real: `0003_d2net_into_codeconv.py` and `0003_dart_plans.py` *both* declare `revision="0003"`, `down_revision="0002"`, so `alembic upgrade head` (called by `codeconv migrate`) is **currently broken**. Fix: keep `0003_d2net_into_codeconv` as `0003`; re-chain `0003_dart_plans` → `0004` (`down_revision="0003"`); 018's new convspec/idiom/builder tables → `0005` (`down_revision="0004"`). All DDL is `CREATE TABLE IF NOT EXISTS`, no data migration (FR-021, fresh PG17 cluster), single head.

**Technical approach** (validated against `codeconv/src/codeconv/runner.py` {`set_dbos`/`get_dbos`/`tool_registry`/re-exported `workflow`}, `codeconv/src/codeconv/cli.py` {`_run_alembic_upgrade`→`command.upgrade(cfg,"head")`, `setup_dbos(endpoint)`}, `codeconv/src/codeconv/db/engine.py` {`build_url`/`setup_dbos`}, `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py`, every `tools/*/workflow.py::register()` (no-op) + `tools/*/__init__.py::register_workflows`, the four migration files, and the feature-015/-016/-017 contracts):

1. **Activate DBOS (D1=a).** A new `codeconv/src/codeconv/durable/` package centralises the DBOS workflow/step model so the six existing tools' `register()` no-ops are replaced by registering their pure entrypoints as steps *through the builder*, not by editing each tool's behaviour. Builder run = an outer `@DBOS.workflow` that, in feature-015 topo/SCC order, launches one child `@DBOS.workflow` per file (or per SCC group — one indivisible unit, FR-002/edge cases) whose steps are the pipeline stages. Workflow IDs are **deterministic** (`builder:{workspace}:{run-epoch}` outer; `file:{sha-stable-rel-path}` / `scc:{sorted-member-hash}` child) so re-running recovers in-flight workflows instead of starting new ones (FR-004/SC-002). DBOS startup recovery + a `builder resume` path recover pending workflows. A DBOS `Queue` with a configurable concurrency cap bounds parallel per-file child workflows (parallels 017's ≤7).
2. **Per-(file, stage) durable unit (FR-003).** Each stage (`discover`→`depgraph`→`scaffold`→`convspec`→`plan`→…) of each file is one `@DBOS.step`; DBOS checkpoints each step's output, so on resume completed steps are **skipped, not re-run**, and a crash mid-file resumes *at the interrupted stage* (not the file's first stage). The existing two-phase `*_started_at`/`*_completed_at` columns + tombstone YAML become the **business-visible projection** of DBOS step state (the durable system-of-record is DBOS's own `dbos`-schema tables; tombstones remain the checked-in round-trip truth — FR-019 reconciles the two).
3. **convspec as deterministic step boundary around agent work (FR-009/010/011/023).** LLM/web nondeterminism must not live in a DBOS step body (replay must not re-call the model). Resolution (R1/R3): the `convspec` step body is **deterministic ingest+persist** of an agent-produced, checked-in artifact; if the artifact is absent the step **returns a deterministic typed `needs_agent_work` result** (never a raised exception — raising in an `@DBOS.step` is recorded by DBOS as a *failed* step); the workflow ends in a durable **awaiting-agent** status surfaced via `builder run`'s exit code, which the **skill** detects (not a caught Python exception), spawns the convspec analysis sub-agent + (on demand) a separate research sub-agent (feature-017 transport, justified deviation), then re-drives the builder; on re-drive the step finds the artifact and completes durably. Idiom-KB lookups happen *before* spawning research so an already-decided construct is never re-researched (FR-012/FR-024).
4. **Unified shared model (FR-022).** New `codeconv/src/codeconv/workspace.py` (single workspace accessor over `codeconv.workspace_settings`/`excluded_directories`/`phase_*`, replacing per-tool ad-hoc reads) and `codeconv/src/codeconv/status.py` (one per-file state enum + escalation vocabulary: `not_started｜blocked_on_deps｜analysed｜specced｜scaffolded｜converted｜escalated｜complete`) are imported by the builder and re-used by tools without changing their behaviour. Original 015/016/017 specs remain as historical lineage; this spec governs (FR-022).
5. **Schema delta (FR-012, single linear chain).** Re-chain `0003_dart_plans`→`0004`; new `0005_codeconv_builder.py` adds, under the `codeconv` schema only: `dart_convspecs` (per-file conversion-spec state + artifact path, parallel to `dart_plans`), `conversion_idioms` (the persistent idiom KB), `research_findings` (provenance cache, FR-024), `builder_runs` (run traceability + the workflow-trace view join key). `CREATE TABLE IF NOT EXISTS`; downgrade `DROP … CASCADE`; schema isolation preserved (no `public`/`dbos` objects authored by Alembic — DBOS owns its own tables).
6. **Status & trace (FR-017/SC-009 + Gabi's D1 trace requirement).** `codeconv builder status` joins the unified per-file state with DBOS workflow state in <5 s; `codeconv builder trace` exposes the DBOS workflow/step history (`dbos.workflow_status` / `operation_outputs`) per file/run for debugging & planning — the explicit "workflow trace analysis" half of D1=a.

Net code: ~700–950 lines new Python in `codeconv/src/codeconv/{durable,workspace,status}.py` + `tools/builder/` + `tools/convspec/`; ~6-line `_FIELD_ORDER` extension in `tools/discover/tombstone.py`; 1 re-id'd migration + 1 new migration; 2 new `SKILL.md` (`/codeconv-builder` carries orchestration, `/codeconv-convspec` carries agent prompts); the six existing tools' `register()` bodies replaced (behaviour unchanged) by builder-side step registration. No Dart/.NET/Node/`glp_runtime/` change.

## Technical Context

**Language/Version**: Python 3.11+ (`codeconv/pyproject.toml`, feature 012). Agent layer: Claude Code Agent tool (harness capability — no SDK/API key added to the package).
**Primary Dependencies**: `dbos` (already a declared dependency — now *activated*), `sqlalchemy>=2.0` + `psycopg[binary]` (vendored, 012), `PyYAML` (vendored), `typer`. **No new Python dependency** (DBOS is pre-existing; the vendored uuid-ossp patch already makes it PGLite-safe).
**Storage**: PGLite via the unified bridge. Reads: `codeconv.dart_depgraph` (015 — canonical order/SCC/status; MUST NOT recompute), `codeconv.dart_files`/`dart_files_orphaned` (node set + `sha256` drift), `codeconv.dart_plans` (017), `codeconv.workspace_settings`/`excluded_directories`/`phase_*` (016). New (`codeconv` schema): `dart_convspecs`, `conversion_idioms`, `research_findings`, `builder_runs`. DBOS owns its `dbos`-schema workflow/step tables (created by `dbos.launch()`, not Alembic).
**Testing**: `pytest codeconv/tests/`. Pure logic (readiness reuse, idiom-lookup, status projection, workflow-id derivation) unit-tested with no bridge. DBOS-needing tests gated `@needs_bridge`; serial (no xdist); cross-process bridge access serialised by the 012 OS lock; PGLite cold-init ~7 s on Windows (memory `project_pglite_cold_init_windows.md`). Agent orchestration validated by fixture-driven dry-run + mocked-agent harness (no real LLM in tests). DBOS resumability tested by killing a workflow mid-step and asserting recovery skips completed steps.
**Target Platform**: Windows 11 primary (this checkout); cross-platform-portable Python.
**Project Type**: Python library + CLI inside `codeconv/` of a polyglot monorepo, plus two Claude Code skill orchestration layers.
**Performance Goals**: Deterministic Python is sub-second per stage call; `status`/`trace` ≤5 s on a warm bridge (SC-009). End-to-end wall time is dominated by LLM convspec/plan sub-agents (out of scope for a hard SLA, as in 017). DBOS step overhead (one PGLite checkpoint write per step) is bounded by the 012 single-writer bridge.
**Constraints**: `--data-dir C:/pglite/research/glpnet` (memory `project_codeconv_data_dir_exfat.md`; D: now NTFS so the guard passes but pass it proactively per CLAUDE.md). Carry-forward 012 FR-026 (no `COPY … FROM STDIN`) / FR-027 (no client-side prepared-statement cache). DBOS+PGLite: single-writer cluster — DBOS recovery/queue workers must serialise through the bridge (R12); the vendored patch strips the unsupported `uuid-ossp` extension at DBOS migration time.
**Scale/Scope**: 128 inventoried files, 443 in-subtree edges, ≥6 isolated nodes (post-014). 0 new schemas (reuse `codeconv`; DBOS reuses `dbos`), 4 new tables + 1 re-id'd migration + 1 new migration, 2 new skills, 2 new tool subpackages, 1 new `durable/` + 2 shared modules, 0 new Python dependencies.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` contains only unfilled template placeholders (`[PRINCIPLE_1_NAME]`, `[GOVERNANCE_RULES]`, …) — no concrete project principles ratified. Per the spec-first discipline in `CLAUDE.md` and `docs/DISCIPLINE.md` (the operative authority for this repo), the relevant gates are:

| Gate (CLAUDE.md / DISCIPLINE.md) | Pass? | Note |
|---|---|---|
| §"Spec-First Development — No Implementation Without Spec" | PASS | spec.md present, clarified (3 Q&A 2026-05-17); checklist `requirements.md` green |
| §"Never program based on ignorance" | PASS | the load-bearing DBOS-reality premise was read-only-verified in code *before* planning; spec-vs-code tension surfaced to Gabi; D1/D2 decided, not assumed |
| DISCIPLINE.md §1.1 Specification-First | PASS | plan derives entirely from spec FRs; no behaviour invented |
| DISCIPLINE.md §1.4 Traceability | PASS | every artefact below cites its FR + the 012/015/016/017 mechanism it extends |
| DISCIPLINE.md §1.7 Errors not "limitations" | PASS | undecidable conversion → escalation (FR-013), not a silent fallback |
| DISCIPLINE.md §1.2 No Workarounds / §1.10 spec authority | PASS by design | escalate-don't-guess (FR-013/014) is the no-silent-workaround discipline encoded as a tool requirement |
| DISCIPLINE.md §2.2 Test baseline before/after | PASS by design | tasks.md sequences baseline-pytest before each change, re-run after |
| Feature 012 contract (FR-006 auto-discovery; schema isolation; tombstone round-trip) | PASS | new tables stay in `codeconv`; new tools register by FS convention; state round-trips through tombstones |
| Feature 015 contract (read depgraph; MUST NOT recompute order/SCC/status) | PASS | builder consumes `dart_depgraph` read-only as the backbone |
| Feature 016/017 capability preservation (FR-016/SC-005) | PASS by design | existing entrypoints wrapped as DBOS steps, behaviour unchanged; no capability deleted |
| §"Skill-as-thin-wrapper-around-CLI" convention | **DEVIATION — justified (×2)** | `/codeconv-builder` carries a durable-orchestration loop and `/codeconv-convspec` carries agent/research sub-agent prompts — same justified deviation class as feature-017 planagents (spawning Claude sub-agents is a harness capability the Python CLI structurally lacks). Python tools stay pure/deterministic. See Complexity Tracking. |
| DBOS activation widens runtime surface (dormant → live) | **GATED — see Complexity Tracking** | D1=(a) is an explicit operator decision; risk (DBOS+PGLite single-writer, vendored patch at scale) is tracked, mitigated by R12, and is the designated top item for `/speckit-analyze` + the human/clarify gate |
| **D2 governing principle** — least-invasive DBOS option; no proven flow obliterated by an unproven idiomatic alternative; existing entrypoints called verbatim; durable layer additive & isolated to `durable/` | PASS by design (HARD GATE) | Gabi, emphatic 2026-05-17. Every DBOS-option decision in R1/R8/R12 + contracts MUST cite the working baseline it preserves; any flow change is rejected unless spec-FR-forced and behaviour-preserving. Re-verified post-Phase-1: no contract reimplements a 015/016/017 entrypoint. |

**Result**: GATE PASSED with justified deviations recorded in Complexity Tracking; the DBOS-activation risk is explicitly flagged for the analyze/clarify human gate (per the user's own chained "human in the loop → /speckit-clarify loop"). Re-checked post-Phase-1: contracts confine all LLM judgement to the agent/skill layer and keep every Python + DBOS-step surface deterministic and replay-safe — the deviations do not widen.

## Project Structure

### Documentation (this feature)

```text
specs/018-codeconv-builder/
├── plan.md                                  # This file (/speckit-plan output)
├── spec.md                                  # Feature spec (clarified 2026-05-17)
├── checklists/requirements.md               # Spec quality checklist (passing)
├── research.md                              # Phase 0 — R1–R13 (this run)
├── data-model.md                            # Phase 1 — 4 new tables + tombstone keys + migration linearization
├── quickstart.md                            # Phase 1 — Flow B (builder end-to-end, kill/resume, convspec, status/trace)
├── contracts/
│   ├── migration_linearization.md           # Phase 1 — re-id 0003_dart_plans→0004; 0005 new; single-head proof
│   ├── dbos_workflow_model.md               # Phase 1 — workflow/step taxonomy, deterministic IDs, queue cap, recovery, replay-safety
│   ├── builder_cli.md                       # Phase 1 — `codeconv builder [run|resume|status|trace|retry|...]` + skill loop
│   ├── builder_schema.md                    # Phase 1 — DDL for builder_runs/dart_convspecs/conversion_idioms/research_findings
│   ├── convspec_artifact_format.md          # Phase 1 — machine-consumable + embedded-rationale per-file spec; tombstone YAML delta
│   ├── convspec_idiom_schema.md             # Phase 1 — idiom KB schema, lookup-before-research, conflict-escalation (FR-012/013/014/024)
│   ├── agent_orchestration.md               # Phase 1 — convspec analysis + separate research sub-agent prompt contracts; concurrency cap
│   └── status_trace_contract.md             # Phase 1 — unified per-file state vocabulary; status<5s; DBOS trace surface
└── tasks.md                                 # Phase 2 — /speckit-tasks (next chained command)
```

### Source Code (repository root)

Touches only `codeconv/` and `.claude/skills/`. No Dart/.NET/Node/`glp_runtime/` change.

```text
codeconv/
├── src/codeconv/
│   ├── durable/                                  # NEW — centralised DBOS workflow/step activation (D1=a)
│   │   ├── __init__.py                           # NEW — workflow/step registry; deterministic workflow-id derivation
│   │   ├── workflows.py                          # NEW — outer builder workflow + per-file/per-SCC child workflow
│   │   ├── steps.py                              # NEW — stage steps wrapping existing tool entrypoints (no rewrite)
│   │   ├── queue.py                              # NEW — DBOS Queue concurrency cap; recovery/resume helpers
│   │   └── trace.py                              # NEW — read-only views over dbos.workflow_status/operation_outputs
│   ├── workspace.py                              # NEW — single shared workspace accessor (FR-006/FR-022)
│   ├── status.py                                 # NEW — unified per-file state enum + escalation vocabulary (FR-017/FR-022)
│   ├── tools/
│   │   ├── builder/                              # NEW — top-level orchestrator tool (auto-discovered)
│   │   │   ├── __init__.py                       # NEW — Typer app (run/resume/status/trace/retry/redrive)
│   │   │   ├── workflow.py                       # NEW — register(): activates DBOS workflows via durable/ (no longer a no-op)
│   │   │   └── orchestrate.py                    # NEW — deterministic frontier driver over feature-015 depgraph
│   │   ├── convspec/                             # NEW — per-file deep-analysis + research conversion-spec tool
│   │   │   ├── __init__.py                       # NEW — Typer app (status/next/ingest/record-idiom/aggregate-escalations)
│   │   │   ├── readiness.py                      # NEW — pure convspec-readiness predicate (parallels 017 readiness)
│   │   │   ├── workflow.py                       # NEW — register(): DBOS step boundary (deterministic ingest+persist)
│   │   │   ├── artefact.py                       # NEW — structured+rationale artifact path/validation (FR-011/023)
│   │   │   └── idioms.py                         # NEW — idiom-KB lookup/record + conflict detection (FR-012/014/024)
│   │   └── discover/tombstone.py                 # MODIFIED — extend _FIELD_ORDER with convspec/builder-state keys (append-only, after 017's)
│   └── db/migrations/versions/
│       ├── 0003_dart_plans.py                    # MODIFIED — re-id revision "0003"→"0004", down_revision "0002"→"0003"
│       └── 0005_codeconv_builder.py              # NEW — dart_convspecs + conversion_idioms + research_findings + builder_runs
└── tests/
    ├── test_migration_single_head.py             # NEW — alembic upgrade head reaches one head, 0 dup/multi-head (FR-015/SC-004)
    ├── test_workflow_id_determinism.py           # NEW — pure: deterministic outer/child/SCC workflow-id derivation (FR-004)
    ├── test_builder_frontier.py                  # NEW — @needs_bridge: topo/SCC order; dependency-before invariant (FR-002/SC-003)
    ├── test_builder_resume.py                    # NEW — @needs_bridge: kill mid-step → resume skips completed steps (FR-003/004/SC-002)
    ├── test_builder_idempotent_rerun.py          # NEW — @needs_bridge: resumed run == uninterrupted run (SC-002)
    ├── test_builder_nothing_to_do.py             # NEW — @needs_bridge: empty subtree exits "nothing to convert" (FR-020)
    ├── test_convspec_readiness.py                # NEW — pure: convspec-readiness predicate + SCC batch
    ├── test_convspec_ingest_step.py              # NEW — @needs_bridge: deterministic ingest; NeedsAgentWork signal; replay-safe (FR-009/023)
    ├── test_convspec_idiom_kb.py                 # NEW — @needs_bridge: lookup-before-research; reuse; consistency (FR-012/SC-007)
    ├── test_convspec_idiom_conflict.py           # NEW — @needs_bridge: idiom↔research / idiom↔idiom conflict → escalation (FR-013/014/SC-008)
    ├── test_convspec_research_provenance.py      # NEW — @needs_bridge: official-docs-authoritative provenance recorded+cached (FR-024)
    ├── test_status_projection.py                 # NEW — @needs_bridge: unified per-file state reconciles durable state <5s (FR-017/SC-009)
    ├── test_builder_trace.py                     # NEW — @needs_bridge: DBOS trace surface per file/run (D1=a trace requirement)
    ├── test_tombstone_divergence.py              # NEW — @needs_bridge: DB↔tombstone drift detected, refuses stale (FR-019)
    ├── test_tombstone_stamp_rebuild.py           # NEW — @needs_bridge: append-only _FIELD_ORDER round-trip idempotent (FR-021)
    ├── test_capability_preservation.py           # NEW — @needs_bridge: every 015/016/017 entrypoint still reachable (FR-016/SC-005)
    └── test_schema_isolation.py                  # NEW — codeconv schema only; Alembic authors no public/dbos object

.claude/skills/
├── codeconv-builder/SKILL.md                     # NEW — venv/repo-root resolver + durable-orchestration loop + NeedsAgentWork handler
└── codeconv-convspec/SKILL.md                    # NEW — analysis sub-agent + separate research sub-agent prompt contracts

.codeconv/
├── conversion-specs/<rel>.dart.md                # NEW (checked in, FR-011/023) — structured+rationale per-file conversion spec
├── conversion-idioms/                            # NEW (checked in, FR-012) — idiom KB export (DB is runtime store; this is round-trip truth)
│   └── _escalations-report.md                    # NEW (checked in, FR-013/014) — aggregated open escalations (path overridable)
└── tombstones/<rel>.dart.md                      # MODIFIED (checked in) — appended convspec/builder-state YAML keys
```

**Structure Decision**: Single-project Python additions inside the existing `codeconv/` package (no new top-level dir, no new language), mirroring the 015/016/017 structure decisions. The one architectural addition is the `durable/` package — the *single place* DBOS activation lives, so the six existing tools are wrapped (not rewritten) and the dormant `register()` no-ops are superseded by builder-side step registration. Two shared modules (`workspace.py`, `status.py`) absorb the previously-duplicated workspace/status concepts (FR-022 unification) without changing tool behaviour. Tool subpackages remain the unit of runner auto-registration (012 FR-006) so no `runner.py` edit is needed. Tombstone keys are appended at the END of `_FIELD_ORDER` (after 017's) so the extension is append-only and idempotence is preserved. The two skill-side orchestration loops are isolated to their `SKILL.md`s and justified in Complexity Tracking.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| **DBOS activation (D1=a): dormant `register()` no-ops → live `@DBOS.workflow`/`@DBOS.step`**, widening the runtime surface and adding DBOS-on-single-writer-PGLite to the critical path. | Explicit operator decision (D1=a, 2026-05-17): the spec's "DBOS" must mean real persistence / resumability / completability / recoverability **and** queryable workflow-trace analysis for debugging & planning — not the nominal two-phase-state model. FR-003/FR-004 resumability+idempotence and the new trace requirement cannot be met by the existing ad-hoc state alone (no step-replay skip, no recovery, no trace history). | (b) **Keep the two-phase state model, call it "DBOS"** — rejected by Gabi: nominal, no real recovery, no trace surface, fails the literal spec word. (c) **Hybrid b-now/a-later** — rejected by Gabi: defers the core value; the consolidation is the right time to activate. The vendored uuid-ossp patch + the working `setup_dbos` launch path mean activation reuses an already-proven launch; risk is scoped to R12 and is the designated top analyze/clarify item. |
| **`/codeconv-builder` + `/codeconv-convspec` skills are NOT pure thin wrappers** — they carry a durable-orchestration loop and agent/research sub-agent prompt contracts (deviates from the `/codeconv-discover` / `/codeconv-depgraph` thin-wrapper convention; same class as feature-017). | Spec FR-009/FR-010 + the agent-driven clarification require spawning a convspec analysis sub-agent and a *separate* research sub-agent, and the builder must catch `NeedsAgentWork` and re-drive. Spawning Claude sub-agents is a Claude Code **harness** capability (the Agent tool); a pure Python CLI cannot do it without adding the Anthropic SDK + an API key + network + per-token cost + nondeterminism to a previously offline, deterministic, replay-safe tool — which would also poison DBOS step replay-safety. | (a) **Python spawns agents via SDK/API** — adds a secret, network, nondeterminism; breaks `@needs_bridge`-only isolation **and** DBOS step determinism (replay would re-call the model). (b) **`claude -p` headless shell-out per agent** — fragile nested harness, no clean concurrency primitive, no provenance, untestable. (c) **No sub-agents; single in-process call** — violates FR-009/FR-010 (deep analysis + *separate* research) and the spec quality bar. The chosen split keeps Python + every DBOS step deterministic and replay-safe (step body = deterministic ingest of a checked-in artifact) and pushes only irreducibly-LLM work into the skill/agent layer — exactly the feature-017 precedent. |

### Amendment 2 (2026-05-19) — facet-3 remediation, re-opened defect

The genuine 128-file live pass proved feature 018 was validated against
a 3-faceted defect that made the convspec agent path structurally
non-functional (mocked tests fed synthetic results a real run never
produces): (1) `outer_builder_workflow` discarded per-unit results ⇒
`needs_agent_work` never surfaced; (2) `builder_runs.outcome` CHECK
lacked `needs_agent_work` (fixed: migration **0006**); (3) the per-unit
child returned on the sentinel ⇒ terminally SUCCESS ⇒ checkpointed
`convspec` never re-ran, and a plain re-drive reused the awaiting epoch
⇒ an agent spec could never be ingested. **Resolution (FR-044, Gabi
Option 4):** split the per-unit child into a deterministic PRE wf + an
out-of-DBOS agent gate + a **content-addressed POST wf**; a
`needs_agent_work` run mints a new epoch on plain re-drive. Constraint
trade-off: nothing weakened globally — R3 (no raise), FR-003/FR-004
(each sub-wf's steps skipped on resume; PRE recovered), SC-002
(bit-identical within a sub-wf), R9-in-spirit (PRE deterministic; POST
deterministic *given the artifact*) all preserved; the only new concept
is content-addressing POST by the artifact digest, which is precisely
the "re-drive finds the artifact and completes" the contract intended.
Isolated to `durable/workflows.py` + `durable/__init__.py` id helpers;
`durable/steps.py` reused verbatim (D2). Acceptance gate:
`test_agent_gate_traversal.py` (real-bridge plain-re-drive ingest).

## Phase 0: Research outputs

See [research.md](./research.md) for R1–R13:

- **R1** DBOS-activation model — outer builder workflow + per-file/per-SCC child workflows; stages = steps; replay skips completed steps; the convspec step is a deterministic ingest boundary around skill-spawned agent work (resolves the central D1=a "how").
- **R2** Builder/convspec skill-as-orchestrator — feature-017 justified-deviation precedent; Python stays deterministic.
- **R3** Migration linearization — re-id `0003_dart_plans`→`0004`, new `0005`; single-head proof; `alembic upgrade head` currently broken (confirmed).
- **R4** convspec schema — `dart_convspecs` two-phase state parallel to `dart_plans`.
- **R5** convspec artifact — structured machine-consumable block + embedded human rationale/provenance (FR-011); checked-in; spec-only (FR-023).
- **R6** Idiom KB — `conversion_idioms` persistent, codebase-scoped; lookup-before-research; reuse for consistency (FR-012/SC-007).
- **R7** Research provenance + caching — official-docs-authoritative (FR-024); `research_findings` cache → offline-reproducible, never re-researched.
- **R8** Refactor-scope boundary (D2) — wrap existing entrypoints as steps; unify only workspace/status/migration; behaviour preserved (FR-022/FR-016/SC-005).
- **R9** Builder durable state + deterministic workflow IDs — idempotent re-run == uninterrupted run (FR-004/SC-002).
- **R10** Tombstone↔DB divergence detection — refuse stale (FR-019); append-only `_FIELD_ORDER` extension; idempotence proof.
- **R11** Unified status + DBOS trace surface — one per-file state enum; `status` <5 s; `trace` over `dbos.*` (FR-017/SC-009 + D1 trace).
- **R12** DBOS + single-writer PGLite constraints — recovery/queue workers serialised through the 012 bridge lock; cold-init ~7 s; uuid-ossp patch; queue concurrency cap; conductor disabled.
- **R13** Crash/cycle-group semantics — SCC = one indivisible workflow unit; crash mid-file resumes at the interrupted stage; deterministic resume-vs-restart on code change mid-run (edge cases).

All template NEEDS CLARIFICATION are closed in research.md. The one genuine planning open question — DBOS-activation risk on single-writer PGLite (R12) — is resolved with a mitigation and **explicitly surfaced as the top item for `/speckit-analyze` and the human/clarify gate** (per the user's chained workflow).

## Phase 1: Design artefacts

- **[migration_linearization.md](./contracts/migration_linearization.md)** — exact revision-id rewrite, the new `0005` chain head, the single-head invariant test, downgrade path, no-data-migration justification (FR-015/FR-021/SC-004).
- **[dbos_workflow_model.md](./contracts/dbos_workflow_model.md)** — workflow/step taxonomy, deterministic workflow-id derivation, the `NeedsAgentWork` step protocol, DBOS Queue concurrency cap, startup recovery + `resume`, replay-safety invariants, SCC-as-one-unit (FR-002/003/004).
- **[builder_cli.md](./contracts/builder_cli.md)** — `codeconv builder [run|resume|status|trace|retry|redrive|aggregate-escalations]` signature, flags, exit codes, JSON shapes, idempotence contracts, the skill durable-orchestration-loop pseudocode + `NeedsAgentWork` handler.
- **[builder_schema.md](./contracts/builder_schema.md)** — DDL for `builder_runs`/`dart_convspecs`/`conversion_idioms`/`research_findings`; PK/FK/constraints; append-then-UPDATE lifecycle; schema-isolation assertion.
- **[convspec_artifact_format.md](./contracts/convspec_artifact_format.md)** — mandated structured (schema'd, codegen-parseable) section + embedded human rationale/provenance; per-construct nuance requirement; escalation schema; the appended tombstone YAML keys + null-vs-missing semantics + append-only idempotence proof (FR-011/023, SC-006).
- **[convspec_idiom_schema.md](./contracts/convspec_idiom_schema.md)** — idiom record schema, the lookup-before-research order, reuse-not-re-derive rule, idiom↔research and idiom↔idiom conflict → escalation (FR-012/013/014/024, SC-007/008).
- **[agent_orchestration.md](./contracts/agent_orchestration.md)** — convspec analysis sub-agent prompt contract (mandatory artifact sections, escalate-don't-guess), the *separate* research sub-agent contract (official-docs-authoritative, verbatim external-request + provenance logging, failure/timeout → escalation), concurrency cap, SCC coordinated batch.
- **[status_trace_contract.md](./contracts/status_trace_contract.md)** — the single per-file state vocabulary + aggregate counts reconciling durable state in <5 s, and the DBOS workflow-trace read model (per-file/per-run step history) for debugging/planning (D1=a).

The agent context file (`CLAUDE.md`) is updated this run to reference this plan between the existing `<!-- SPECKIT START -->` / `<!-- SPECKIT END -->` markers (replacing the prior feature-017 reference).
