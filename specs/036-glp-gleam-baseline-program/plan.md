# Implementation Plan: GLP → Gleam/AtomVM Baseline — Research, Verification & Reconfiguration Program

**Branch**: `036-glp-gleam-baseline-program` | **Date**: 2026-06-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/036-glp-gleam-baseline-program/spec.md`

## Summary

This is a **research / verification / roadmap-reconfiguration program**, run under
**`/bk-marathon`** in two macro-phases, that compresses the three open epics
(REPL/engine-separation, marathon, Gleam-AtomVM) into the **shortest verified roadmap** to a
combined Gleam/AtomVM GLP instance (M1 single-instance parity + M2 linked parity vs Dart/C#). It
produces decision-ready, evidence-cited artifacts and a reorganised backlog — **not product code**.
The seam architecture is already owner-ratified and spike-verified (spec.md §Established Decisions
ED-1…ED-6): `ANTLR grammar → AST → lightweight 4-primitive IL (+verifiers) → frozen v2.16.3
bytecode → engine`, bytecode-on-wire as the front/back seam. The remaining work is the rest of the
machinery (P2–P8) and its disciplined execution, ending at the owner-gated two-epic migration.

## Technical Context

**Language/Version**: Orchestration in Claude (Agent/Workflow multi-agent seams). Verification
spikes in Dart 3.10.1 (reuse `glp_runtime/`, read-only), Gleam 1.17.0 / OTP 25 / AtomVM 0.6.6,
ANTLR4 4.13.2 (Java 17; Dart/C# targets). Proof harness: Lean 4, SPIN, MLIR (the in-repo armoury at
`docs/research/repl-engine-separation/spikes/`).
**Primary Dependencies**: the GLP corpus (`GLP_IMPLEMENTATION.pdf`, `Art-of-GLP-2025/`, the
authoritative Dart `glp_runtime/`), the in-repo research corpus (`docs/research/...`), sibling repos
(`qhstate`, `qhstate-Yngenios`, `MSTACK`, `olamnit` — read-only), the bk-marathon harness.
**Storage**: all artifacts under `docs/research/glp-gleam-baseline/`. Durable run state in the
marathon per-run isolated store (off-repo, Constitution VI-b exemption). **No** writes to the repo
working-data cluster, the target roadmap, or production code until owner approval.
**Testing**: per-pipeline verification gates — direct-source citation, adversarial verification of
findings, and **constructed proofs** (Lean/SPIN/MLIR) for load-bearing invariants. The P5 spike
(`spike/p5-il-merge/`) is the executed precedent (execution-equivalence + verifier-firing).
**Target Platform**: research deliverables now; the delivery target the roadmap aims at = Gleam on
AtomVM/BEAM.
**Project Type**: research + verification + roadmap-reconfiguration program (not normal software).
**Performance Goals**: N/A — quality goals instead: every claim cited; every Full-Gleam feature
scored + ordered; load-bearing invariants proved/refuted/open (never silently skipped).
**Constraints**: read-only on the target roadmap/specs/code AND all sibling repos until owner
approval (FR-010); epic migration only at the marathon discharge gate (FR-011); corpus inspected
directly, never summarised-as-substitute (FR-003); Claude-only LM, no external API (Constitution V);
anti-bias rules forced after the P1 failure (no "fastest-path" rubric; no self-cited synthesized
rubric; cite or do not assert).
**Scale/Scope**: ~22 not-completed features to disposition; 8 pipelines (P2–P8 + the ANTLR-integration
deep-dive; P5 + the verification spike DONE); 2 target epics.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Basis |
|---|---|---|
| I. Spec-First | **PASS** | This plan cites spec.md; the program is read-only and produces specs/epics for owner approval before any build. The P1 analysis failure was STOPPED + reported (II), not worked around. |
| II. Bug-Protocol / No-Workarounds | **PASS** | The P1 failure was surfaced and is being re-run, not patched over; anti-bias rules added at root cause. |
| III. SRSW inviolable | **PASS** (machine-checkable) | No `skipSRSW` token in any artifact; the program *reinforces* SRSW (the IL verifiers check it). |
| IV-a. Language Authority | **PASS** | No GLP language change. The 4-primitive IL is a front-end-internal codegen/verification aid that lowers to the **existing** v2.16.3 ISA (ED-2/ED-3); verifiers check **existing** invariants. |
| IV-b. Preserve Working Internals | **PASS** | Read-only on `glp_runtime/`; the spike modified zero production files (`git status` clean); `_ClauseVar`/`_TentativeStruct` untouched. |
| V. Claude-Only LM / No External API | **PASS** (machine-checkable) | All multi-agent work runs in Claude (Agent/Workflow); no `OPENAI_API_KEY`/`litellm`/`openai` on any LM path; proofs use deterministic oracles + Claude. |
| VI-a. Additive/idempotent migrations | **PASS (N/A)** | This program performs no DB migration; it is read-only on code/DB. |
| VI-b. Single PGLite cluster | **PASS** | Uses the marathon per-run isolated off-repo store (explicit VI-b exemption); creates no second working-data cluster. |
| VII. Test-gated, commit-scoped shipping | **PASS** | Commits are name-scoped (no `git add -A`); the marathon's scoped-commit discipline applies; GitFlow respected. |
| VIII. Single source of truth & traceability | **PASS** | roadmap → 036 pipeline → tasks; artifacts unified under `docs/research/glp-gleam-baseline/`; DECISIONS.md is the authoritative seam-architecture record. |

**No violations. Complexity Tracking: empty.** (Re-checked post-Phase-1: still PASS — the design adds
documents + isolated spike packages only.)

## Project Structure

### Documentation (this feature)

```text
specs/036-glp-gleam-baseline-program/
├── plan.md              # this file
├── research.md          # Phase 0 — machinery design + resolved decisions
├── data-model.md        # Phase 1 — the program's entities
├── quickstart.md        # Phase 1 — how to run/resume the machinery under the marathon
├── contracts/           # Phase 1 — the pipeline contract + the discharge-gate contract
└── tasks.md             # Phase 2 — /bk-tasks output (NOT created here)
```

### The machinery + artifacts (repository)

```text
docs/research/glp-gleam-baseline/
├── feature-definition.md                 # program source of truth (corpus/repo paths)
└── pipelines/
    ├── P1/                               # realignment (FLAWED first pass — to re-run in Phase B)
    └── P5-il-machine-language/           # IL/ML research → DOSSIER.md + DECISIONS.md (DONE, verified)
spike/
└── p5-il-merge/                          # the executed verification spike (DONE, PASS)
# Phase A creates: P2..P8 pipeline scripts (workflow scripts), a shared corpus index,
# and the proof-harness wiring (reusing docs/research/repl-engine-separation/spikes/{lean,spin,mlir}).
# Phase B writes each pipeline's verified artifact under docs/research/glp-gleam-baseline/pipelines/.
```

**Structure Decision**: documentation + research artifacts under `docs/research/glp-gleam-baseline/`;
verification spikes as **isolated packages** (e.g. `spike/p5-il-merge/`) that depend on `glp_runtime/`
read-only and never modify it. No production source tree changes in this program — those belong to
the future *Full Gleam implementation* epic, post-approval.

## Phases (this program's execution model — see research.md for the machinery, quickstart.md to run it)

- **Phase A — build the machinery**: author P2–P8 as resumable multi-agent pipelines (workflow
  scripts), a shared corpus index, and the proof-harness wiring. (P5 + the verification spike are the
  built+run precedent.)
- **Phase B — run the machinery**: execute the pipelines (order starts with **P4 faithfulness-proofs**,
  then re-run the corrected P1 realignment, then the remaining threads), accumulate verified artifacts,
  then **P8 synthesis** → the two-epic reconfiguration proposal.
- **Discharge gate** (FR-011): owner approves the reconfiguration → only then migrate the recombined
  features into the two epics (*Optional features* / *Full Gleam implementation*).

## Complexity Tracking

*No constitution violations — section intentionally empty.*
