# Implementation Plan: Gleam Port — Source & Toolchain / AtomVM Feasibility Spike

**Branch**: `031-gleam-port-spike` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/031-gleam-port-spike/spec.md`

## Summary

This is a **research and decision spike** (epic `gleam-atomvm`, feature F1). It produces three durable artifacts — a **decision dossier**, a **hello-GLP-term smoke** (a self-contained Gleam project), and a **toolchain inventory** — all under `docs/research/gleam-atomvm/`. It writes **no** production code: it does not touch the GLP runtime, programs, or roadmap, and it does **not** create the `glp_gleam/` subtree (that is F2/F3).

Technical approach: stand up a Gleam + Erlang/OTP toolchain on a documented environment (Windows-first, WSL/Linux or sibling-Mac fallback); author a minimal Gleam module that constructs a representative GLP term (one compound/structure + one unbound-variable analogue) and demonstrates **one** unbound→bound transition observed by a reader; compile it to BEAM and run it on Erlang with observed output; attempt the same on an **effort-bounded** AtomVM host build; and assemble the evidence into a build-target matrix (Erlang/BEAM · AtomVM · JavaScript) and a source-basis recommendation (Dart vs C# vs file-by-file replication) with an architectural-fit assessment, downstream re-scope notes, and a single go / no-go / go-with-revisions verdict. Every "it works" claim is backed by a command + observed output or an authoritative citation.

## Technical Context

**Language/Version**: **Gleam** (latest stable at execution time — pinned during the spike) compiling to **Erlang/OTP** (latest stable, BEAM target) and evaluated against the **JavaScript** backend. Subjects under study (not modified): **Dart** source `glp_runtime/` and the **C#** source (`glp_runtime_net/`, `csharp/`, scaffold mirror `out/csharp/`). Spike deliverables themselves are **Markdown** documents + one **Gleam** project.
**Primary Dependencies**: `gleam` compiler + build tool; Erlang/OTP runtime (`erl`, `escript`, `rebar3`); an **AtomVM host/generic build** (prebuilt preferred); standard Gleam stdlib + `gleam_erlang`/`gleam_otp` for the process/state-holder bind demo. No glpnet runtime dependency is added or changed.
**Storage**: N/A — the spike's only durable outputs are files under `docs/research/gleam-atomvm/`. No PGLite, no database, no migrations.
**Testing**: Reproducible **command + observed-output** evidence (FR-009), not an automated suite. Acceptance is the spec's Success Criteria — chiefly SC-002 reproducibility ("a second person on a clean checkout reproduces the same result"). A short verification note (exact commands, versions, observed stdout) is recorded with the smoke and in the toolchain inventory.
**Target Platform**: **Erlang/BEAM** is the test runtime; **AtomVM** host build is the feasibility target (epic's ultimate target, P2 here); **JavaScript** backend is evaluated as a fallback (P3). Development environment: **Windows** primary; documented **WSL/Linux** or **sibling Mac** fallback if the toolchain (esp. AtomVM bring-up) proves infeasible on Windows.
**Project Type**: Research/decision spike — documentation + one throwaway-grade smoke artifact. Not a library/service/runtime.
**Performance Goals**: N/A — performance is explicitly **out of scope** for this spike (Assumptions).
**Constraints**: AtomVM bring-up is **effort-bounded** (prebuilt preferred → time-boxed source build → else record the bring-up blocker as evidence). No production code; no `glp_gleam/` subtree; no modification of GLP runtime / programs / roadmap definitions (FR-011). Claude-only LM, no external API. Recommends only — the engineer ratifies the source decision and any roadmap edits.
**Scale/Scope**: 3 durable artifacts (dossier · smoke · toolchain inventory); a 3-row build-target matrix ({Erlang/BEAM, AtomVM, JavaScript} × {verdict, evidence, constraints, host-vs-hardware caveat}); 3 source candidates ranked; exactly **one** unbound→bound bind demonstrated; bounded representative term (one compound/structure + one unbound-variable analogue).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.* Constitution v1.1.0.

| Principle | Bearing on this spike | Status |
|---|---|---|
| **I. Spec-First** (judgement) | An identified, clarified, consistency-checked spec exists (`spec.md`, Clarifications 2026-06-22); this plan derives from it and quotes its FR/SC. No code precedes spec. | **PASS** |
| **II. Bug-Protocol / No-Workarounds** (judgement) | The spike *records* toolchain/AtomVM failures as first-class findings (edge cases) rather than working around them — exactly the no-workaround posture. | **PASS** |
| **III. SRSW invariant** (machine-checkable) | The spike writes **no GLP clauses** (the smoke is Gleam, not GLP), so the forbidden single-reader/single-writer bypass token never appears in spec/plan/tasks. | **PASS** |
| **IV-a. Language Authority** (judgement) | The spike proposes **no** change to the GLP language. Dossier recommendations about a *future* port are recommendations for the engineer to ratify, not language edits. | **PASS** |
| **IV-b. Preserve Working Internals** (judgement) | FR-011: the spike modifies no runtime internals (`_ClauseVar`, `_TentativeStruct`, etc. untouched). | **PASS** |
| **V. Claude-Only LM** (machine-checkable) | No LM in the spike's deliverables; no external-LLM API key or third-party LM-client library appears on any path. | **PASS** |
| **VI-a. Additive/Idempotent migrations** (machine-checkable) | No DB migrations created or altered. | **N/A — PASS** |
| **VI-b. Single PGLite cluster** (judgement) | The spike touches no PGLite cluster. | **N/A — PASS** |
| **VII. Test-Gated, Commit-Scoped Shipping** (advisory) | Commit only spike files (docs + smoke); ship via buildkit GitFlow; "tests" = reproducible command evidence. | **PASS** |
| **VIII. Single Source of Truth & Traceability** (judgement) | The dossier is the single authoritative source-decision document; downstream features reference it, not duplicate it. Traceable roadmap (`gleam-atomvm` F1) → pipeline (031) → tasks. | **PASS** |

**Result**: No violations. **Complexity Tracking is empty** (simplest design that satisfies the spec — consistent with the planning guidance "prefer the simplest design").

## Project Structure

### Documentation (this feature)

```text
specs/031-gleam-port-spike/
├── spec.md              # Feature spec (input)
├── plan.md              # This file (/bk-plan output)
├── research.md          # Phase 0 output — how the spike is executed (decisions)
├── data-model.md        # Phase 1 output — the dossier/matrix/smoke entities
├── quickstart.md        # Phase 1 output — reproducible setup→build→run→read path
├── contracts/           # Phase 1 output — required shape of each deliverable
│   ├── dossier-outline.md
│   ├── build-target-matrix.schema.md
│   ├── hello-glp-term.contract.md
│   └── toolchain-inventory.schema.md
├── checklists/
│   └── requirements.md  # spec-quality checklist (already passing)
└── tasks.md             # /bk-tasks output (NOT created by /bk-plan)
```

### Deliverables (repository root) — created during `/bk-implement`, NOT by this plan

```text
docs/research/gleam-atomvm/          # all durable spike outputs live here (FR-011)
├── dossier.md                       # Decision Dossier — source decision + criteria table,
│                                    #   build-target matrix, architectural-fit assessment,
│                                    #   downstream re-scope notes, single go/no-go verdict
│                                    #   (FR-001,002,006,007,010; SC-001,003,005,006)
├── toolchain-inventory.md           # exact versions, install/build/run commands, env verified
│                                    #   (FR-003; SC-002)
└── hello-glp-term/                  # self-contained Gleam project (FR-004; SC-002,006)
    ├── gleam.toml
    ├── manifest.toml
    ├── src/hello_glp_term.gleam      # constructs representative GLP term; one unbound→bound bind
    ├── test/hello_glp_term_test.gleam
    └── README.md                     # recorded compile+run commands & observed output
                                      #   (BEAM result; AtomVM attempt result/blocker)
```

**Structure Decision**: This is a **documentation + spike** feature, not application source. The default `src/ + tests/` layout does **not** apply. The spike's "source code" is the single Gleam project `docs/research/gleam-atomvm/hello-glp-term/`; everything else is Markdown evidence under the same research directory, per the 2026-06-22 clarification. The `glp_gleam/` subtree is **explicitly not** created here.

## Complexity Tracking

> No Constitution Check violations — nothing to justify. (Section intentionally empty.)
