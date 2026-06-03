# Implementation Plan: Trace-Equivalence-Driven Codegen Fidelity

**Branch**: `020-trace-equivalence-fidelity` | **Date**: 2026-05-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification at `specs/020-trace-equivalence-fidelity/spec.md` (clarified Session 2026-05-27 — 10 Q&A; zero open clarifications; decisions: real `dspy.GEPA`; behavioural + execution-trace equivalence objective; optimizer-first co-evolution; tiered fidelity metric `0.0 / 0.25 / 0.5+0.5·frac / 1.0`; causal/partial-order trace relation; per-subsystem prompts on a shared base; all-suites corpus; strict→dynamic curriculum; versioned train/held-out manifest).

> Authored under the buildkit toolchain. Pipeline-tracked feature `020-trace-equivalence-fidelity`; this is the `plan` stage.

## Summary

Feature 020 raises the codeconv fidelity bar from 019's *compile + ported-test + sampled-review* signal to **behavioural + execution-trace equivalence** of the converted C# GLP runtime against the Dart original, across all GLP source suites, and wires a **real `dspy.GEPA`** optimizer (per-subsystem, easy-to-hard curriculum) to drive codegen toward it.

The deliverable is a **fidelity layer over 019's deterministic engine**, not a new codegen engine. It adds:

- **A deterministic, LM-free differential equivalence oracle** (`tools/equiv/`) that runs a GLP source through the Dart (golden) and converted C# (candidate) REPLs, captures a **normalized trace** (heap addresses → logical variable identities; events carry causal/data-dependence edges), and emits a verdict (equivalent | divergent) with the first divergent event pinpointed. A cheap **bytecode-emission diff** is the early checkpoint. Bonds plays are outcome-only.
- **Causal/partial-order trace comparison** (FR-003): REQUIRE identical sequences of unification outcomes, suspension/reactivation events, writer-binding order, and bytecode ops; ABSTRACT over heap addresses and the interleaving of causally-independent goals. The bytecode-op sequence is the spine.
- **A tiered fidelity metric** (`tools/equiv/fidelity.py`, FR-013) computed *identically* by the production gate and the GEPA optimizer: `0.0` non-compile floor; flat `0.25` compiling-unreviewed; high band `0.5 + 0.5·frac` (frac = fraction of in-scope sources trace-equivalent), clamped strictly `< 1.0` until frac = 100%, which snaps to exactly `1.0`. `1.0` reserved for total trace-equivalence.
- **Real `dspy.GEPA`** in `tools/codegen_opt/` replacing 019's hand-rolled `dspy.Predict` reflective loop. GEPA's metric returns a scalar score **and textual feedback** (the actual divergence: compiler error / failing back-test / specific trace-divergence event). **Per-subsystem prompts** descending from a shared optimized base that transfers forward across the curriculum.
- **A new migration `0008`** chained after `0007` for equivalence/trace/per-subsystem state (additive; no recompute of upstream).
- **C# REPL trace instrumentation** so the candidate emits a structured trace comparable to Dart's `:trace`.

**019's (C)-hybrid invariant is preserved verbatim.** The oracle and the metric are **deterministic and LM-free**, so they compose with the durable path; GEPA (LM-bearing) stays offline in `codegen_opt` and consumes the oracle's divergence reports as reflective feedback. The production/durable path imports no dspy/litellm/openai.

**Optimizer-first co-evolution (FR-015):** per subsystem — optimize-before-generate on the currently-available signal (build + module back-tests before a runnable C# runtime exists; trace-equivalence once it does) → generate → run the available equivalence gate → reflect divergences into GEPA → regenerate weak files → freeze the subsystem prompt → carry the base forward. Curriculum is strict subsystems first, dynamic `multiagent` last.

**Net code:** ~1100–1500 lines new Python in `tools/equiv/` + GEPA rewiring of `tools/codegen_opt/` + a durable `equiv` step + migration `0008`; C# trace instrumentation in the converted REPL; 1 new skill (`/codeconv-equiv`) + extension of `/codeconv-codegen-opt`; the per-subsystem prompt artifacts + the checked-in train/held-out manifest. No change to the Dart `glp_runtime/` source (it is the golden oracle — read-only).

## Technical Context

**Language/Version**: Python 3.11+ (`codeconv/pyproject.toml`). Agent layer: Claude Code Agent tool (codegen sub-agents, 017/018/019 precedent). Golden runtime: Dart (existing `glp_runtime/`, read-only). Candidate runtime: C#/.NET 10 (`dotnet ≥ 10` on PATH), the `out/csharp/` tree filled by 019.
**Primary Dependencies**: existing — `dbos`, `sqlalchemy>=2.0` + `psycopg[binary]`, `PyYAML`, `typer`. Offline-optimizer-only — `dspy ≥ 3.2`, `gepa`, `litellm`, `openai` (already in `codeconv/.venv`; 019 used `dspy 3.2.1`/`gepa 0.0.27`). Feedback signal — the Dart REPL (`dart run bin/glp_repl.dart` / `glp_repl.exe`) and the converted C# REPL, plus the `dotnet` CLI for the build gate. **No new Python dependency.**
**Storage**: PGLite via the unified bridge at `C:/pglite/research/glpnet` (the codeconv cluster — distinct from buildkit's own `pgdb/`). Reads (read-only): `codeconv.dart_depgraph` (015), `dart_convspecs` (018), `dart_plans` (017/018), `dart_codegen` (019), `conversion_idioms`. New (`codeconv` schema): `dart_equivalence` (+ supporting trace/subsystem state). DBOS owns its `dbos`-schema tables.
**Testing**: `pytest codeconv/tests/`. Pure logic (trace normalization, the causal-equivalence relation, the tiered fidelity scorer, the manifest splitter) unit-tested without a bridge or any runtime. Bridge-needing tests `@needs_bridge`, serial, through the 012 OS lock; PGLite cold-init ~7 s (`--test-concurrency=1`). The oracle is exercised on tiny fixture GLP programs + recorded fixture traces; the real Dart/C# REPLs are invoked only in `@needs_runtime`-marked tests, skipped where absent. GEPA/LM never called in tests — the optimizer tested with a MOCKED LM + fixture metric.
**Target Platform**: Windows 11 primary; cross-platform Python.
**Performance Goals**: deterministic Python sub-second per oracle invocation excluding REPL spawn; trace normalization linear in trace length. End-to-end dominated by REPL execution (Dart cold ~seconds; C# build incremental against already-built dependency assemblies) + (offline) GEPA rollouts — no hard SLA. `equiv status` ≤ 5 s warm.
**Constraints**: `--data-dir C:/pglite/research/glpnet` (CLAUDE.md convention). 012 FR-026/FR-027 carry-forward. DBOS+PGLite single-writer (019 R12). The optimizer's API key is read from env, lives ONLY in `codegen_opt`, never imported by `tools/equiv/`, `tools/codegen/`, or any DBOS step (replay-safety). GEPA bounded by a hard budget/rollout cap (SC-006). GLP `.glp` sources executed in place under `programs/` — never copied (FR-006, single source of truth).
**Scale/Scope**: corpus = unified REPL suite (384) + book suite (141) + bonds plays (outcome-only) + 374 Dart unit tests ported to C# for back-test signal. 5 curriculum subsystems (`heap`, `bytecode`, `compiler`, `runtime-core`, `multiagent`). 1 new table + 1 migration (`0008`), 1 new tool subpackage (`tools/equiv/`), 1 durable step, 1 new skill + 1 extended skill, per-subsystem prompt artifacts + 1 checked-in manifest, 0 new Python deps.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is unfilled template placeholders. Per `CLAUDE.md` / `docs/DISCIPLINE.md` (the operative authority for this repo, as in 019), the gates:

| Gate | Pass? | Note |
|---|---|---|
| Spec-First | PASS | spec.md clarified (10 Q&A); zero open clarifications |
| Never program based on ignorance of GLP | PASS | trace relation derived from the GLP three-phase model (HEAD/GUARD/BODY), SRSW, writer-MGU, three-valued unification — all in `typed-glp-manual.md`/cheat-sheet; the oracle measures these exact events |
| DISCIPLINE §1.1 Specification-First | PASS | plan derives entirely from spec FRs |
| DISCIPLINE §1.4 Traceability | PASS | every artefact cites an FR + the 015/017/018/019 mechanism it extends |
| DISCIPLINE §1.7 Errors not "limitations" | PASS | non-compile / divergence → escalation or stale-mark, never silent tolerance (FR-008, FR-016) |
| DISCIPLINE §1.2/§1.10 no workarounds / spec authority | PASS by design | escalate-don't-guess as a gate requirement; a divergence that is a Dart-spec violation is a GLP bug → STOP & report (FR-017), never "fix C# to match a wrong oracle" |
| DISCIPLINE §2.2 baseline before/after | PASS by design | tasks.md sequences the 019 baseline (104 pure + 73 codegen suite, green 2026-05-27) before and after |
| Feature 012 (auto-discovery; schema isolation; tombstone round-trip) | PASS | new table in `codeconv`; FS-convention tool registration; equiv state round-trips through tombstones (append-only `_FIELD_ORDER`) |
| Feature 015 (read depgraph; MUST NOT recompute) | PASS | consumes `dart_depgraph` read-only for curriculum/subsystem topo order |
| Feature 019 reuse (engine, `dart_codegen`, durable step) | PASS | extends read-only; equiv state persists additively in a new table + migration `0008` |
| 015–019 capability preservation (FR-019) | PASS by design | upstream untouched; 019 baseline must stay green; equiv/GEPA additive |
| Skill-as-thin-wrapper convention | **DEVIATION — justified (×1, extends ×1)** | `/codeconv-equiv` carries the oracle-driver + escalation loop (017/018/019 class); `/codeconv-codegen-opt` extended for real-GEPA per-subsystem optimization. See Complexity Tracking. |
| In-package LM client + API key | **GATED — see Complexity Tracking** | confined to offline `codegen_opt`; `tools/equiv/` + `tools/codegen/` + every DBOS step LM-free, replay-safe (019 R3) |
| DBOS replay-safety (R3) | PASS (HARD GATE) | the durable `equiv` step = deterministic ingest of recorded/normalized traces + verdict; no model call and **no nondeterministic REPL spawn inside the step** — trace capture happens in the agent/CLI layer, the step ingests checked-in/recorded artifacts (the 019 `needs_agent_work` pattern). GEPA non-determinism offline only. |
| C# REPL instrumentation touches converted output, not Dart source | PASS | trace hooks added to the **converted** C# REPL (the candidate); Dart `glp_runtime/` is the read-only golden — not modified |

**Result**: GATE PASSED; two deviations recorded below (one new justified skill loop, one extension); the in-package-LM risk again contained by the (C) split and flagged as a top `/buildkit-analyze` item.

## Project Structure

### Documentation (this feature)

```text
specs/020-trace-equivalence-fidelity/
├── plan.md                              # This file (/buildkit-plan output)
├── spec.md                              # Clarified spec (10 Q&A 2026-05-27)
├── research.md                          # Phase 0 — R1–R12 (this command)
├── data-model.md                        # Phase 1 — dart_equivalence + tombstone keys + manifest + migration 0008 linearization
├── quickstart.md                        # Phase 1 — end-to-end oracle → metric → GEPA co-evolution flow
├── contracts/
│   ├── equiv_cli.md                     # `codeconv equiv [status|next|capture|compare|ingest|fidelity|promote|aggregate-escalations|retry|mark-stale]`
│   ├── trace_normalization.md           # normalized-trace schema; heap→logical relabeling; causal-edge derivation; bonds outcome-only mode
│   ├── equivalence_relation.md          # the causal/partial-order relation; strict-tier total-order specialization; divergence-record format
│   ├── fidelity_metric.md               # the exact tiered scorer (0.0 / 0.25 / 0.5+0.5·frac / 1.0); production-gate ≡ GEPA-metric proof
│   ├── gepa_optimizer.md                # real dspy.GEPA wiring; per-subsystem prompt + shared base; budget cap; manifest train/held-out split
│   ├── equiv_schema.md                  # dart_equivalence DDL; migration 0008 single-head proof; schema isolation
│   ├── dbos_equiv_stage.md              # durable equiv step (deterministic trace ingest + verdict; needs_agent_work; replay-safety)
│   └── subsystem_curriculum.md          # subsystem classification (heap/bytecode/compiler/runtime-core/multiagent) + tier + curriculum order
└── tasks.md                             # Phase 2 — /buildkit-tasks (next)
```

### Source Code (repository root)

Touches `codeconv/`, `.claude/skills/`, the converted `out/csharp/` REPL (trace instrumentation), the checked-in manifest/prompt artifacts in `.codeconv/`. **No Dart `glp_runtime/` change** (golden oracle, read-only).

```text
codeconv/src/codeconv/
├── tools/
│   ├── equiv/                                # NEW — deterministic, LM-free fidelity oracle (auto-discovered)
│   │   ├── __init__.py                       # Typer app (status/next/capture/compare/ingest/fidelity/promote/aggregate-escalations/retry/mark-stale); bare = status
│   │   ├── trace.py                          # normalized-trace model + heap→logical relabeling + causal-edge derivation (PURE)
│   │   ├── normalize.py                      # parse Dart `:trace` + C# trace into the normalized model (PURE)
│   │   ├── relation.py                       # causal/partial-order equivalence relation + total-order specialization + first-divergence (PURE)
│   │   ├── bytecode_diff.py                  # early cheap bytecode-emission diff checkpoint (PURE)
│   │   ├── fidelity.py                       # the tiered scorer — SINGLE source, imported by gate AND GEPA metric (PURE)
│   │   ├── corpus.py                         # the equivalence corpus + bonds-outcome-only designation; reads .glp in-place under programs/
│   │   ├── manifest.py                       # versioned train(~70%)/held-out(~30%) split loader (deterministic, checked-in)
│   │   ├── readiness.py                      # equiv-readiness predicate (file's deps converted+equivalent; subsystem/tier)
│   │   ├── workflow.py                       # register() durable equiv step + deterministic verdict ingest (two-phase dart_equivalence)
│   │   └── stale.py                          # Dart source-drift detection → mark affected equivalence results stale (FR-016)
│   └── codegen_opt/                          # MODIFIED — real dspy.GEPA + per-subsystem prompts (was hand-rolled dspy.Predict loop)
│       ├── program.py                        # MODIFIED — dspy.Module signature unchanged shape; consumes subsystem + divergence feedback
│       ├── metric.py                         # MODIFIED — GEPA metric returns (score, textual_feedback); score = tools/equiv/fidelity.py
│       ├── dataset.py                        # MODIFIED — per-subsystem dataset via the checked-in manifest (train vs held-out)
│       └── optimize.py                       # MODIFIED — real GEPA driver; per-subsystem prompt; shared base carry-forward; budget cap best-so-far
├── durable/{steps.py,workflows.py}           # MODIFIED — register equiv step; add `equiv` stage after `codegen`
├── tools/discover/tombstone.py               # MODIFIED — extend _FIELD_ORDER with equiv-state keys (append-only, after codegen keys)
└── db/migrations/versions/0008_equivalence.py # NEW — dart_equivalence (down_revision 0007)

out/csharp/ (converted REPL)                  # MODIFIED — structured trace instrumentation comparable to Dart `:trace` (candidate-side only)

.claude/skills/
├── codeconv-equiv/SKILL.md                   # NEW — oracle driver: capture both traces, run gate, record divergence, escalation loop
└── codeconv-codegen-opt/SKILL.md             # MODIFIED — real-GEPA per-subsystem optimization driver

.codeconv/
├── equiv-manifest/subsystems.yml             # NEW (checked in, versioned) — per-subsystem source assignment + train/held-out split
├── codegen-prompt/<subsystem>.md             # NEW (checked in) — per-subsystem GEPA-optimized prompts (descend from shared base)
├── codegen-prompt/_base.md                   # NEW (checked in) — the shared optimized base prompt
├── conversion-equiv/_escalations-report.md   # NEW (checked in, FR aggregate)
└── tombstones/<rel>.dart.md                  # MODIFIED — appended equiv-state keys
```

**Structure Decision**: Single-project Python additions inside `codeconv/`, mirroring 015–019. The architectural addition is **`tools/equiv/`** — a fully deterministic, LM-free oracle whose pure core (`trace.py`, `normalize.py`, `relation.py`, `bytecode_diff.py`, `fidelity.py`) is unit-testable without any runtime or bridge, and whose `workflow.py` wraps a deterministic verdict-ingest durable step (no nondeterministic REPL spawn inside the DBOS step — trace capture is an agent/CLI-layer concern, the step ingests recorded artifacts, exactly the 019 `needs_agent_work` discipline). `tools/codegen_opt/` is rewired to real `dspy.GEPA` with per-subsystem prompts — and remains the ONLY place DSPy/GEPA/litellm/OpenAI + the API key live. `tools/equiv/fidelity.py` is the **single** scorer imported by both the production gate and the GEPA metric, so SC-004 (production gate ≡ GEPA metric) holds by construction. Tombstone keys append at the END of `_FIELD_ORDER`. The one new skill loop and the one extended skill are isolated to their `SKILL.md`s and justified below.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `/codeconv-equiv` skill carries an orchestration loop (not a thin CLI wrapper) | The oracle must spawn/drive both REPLs, capture traces, run the gate, record divergences, and loop escalations across the frontier — the same agent-orchestration class as 017/018/019 | A thin wrapper cannot orchestrate dual-REPL trace capture + escalation; pushing it into the durable step would put nondeterministic REPL spawning inside a DBOS step, violating replay-safety (019 R3) |
| `/codeconv-codegen-opt` extended with real `dspy.GEPA` + LM client (in-package model client) | FR-010 mandates real GEPA with textual reflective feedback; GEPA needs an LM | Hand-rolled `dspy.Predict` loop (019) is explicitly replaced by FR-010; the LM stays OFFLINE-only, never imported by `tools/equiv/`, `tools/codegen/`, or any DBOS step — the 019 (C)-hybrid containment is preserved verbatim |
