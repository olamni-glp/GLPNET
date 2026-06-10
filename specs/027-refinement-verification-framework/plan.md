# Implementation Plan: Iterative Refinement & Verification Framework

**Branch**: `027-refinement-verification-framework` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/027-refinement-verification-framework/spec.md`

## Summary

This is a **PREP / methodology** feature for the `engine-separation` epic. It produces no shippable
runtime. It finalizes a shared **specification + framework skeleton** that every successor seed (#2–#16)
instantiates, **plus three minimal, runnable, real-tool validation spikes** that empirically prove the
methodology's three highest-risk claims before any successor depends on them.

Concretely, Option D + the owner's 2026-06-09 experimentation directive (R13/R14/R15) require:

1. **Specification artifacts** (mostly authored in the 026 reconciliation pass; this feature *finalizes*
   them as the framework's authoritative deliverables): the GEPA/DSPy refinement-loop method
   (`REFINEMENT-METHOD.md`, FR-001), the owner-decisions record (`DECISIONS-FOR-OWNER.md`, FR-002), the
   reusable **metric-combination template** (FR-003), the Lean 4 tactic-loop architecture sketch
   (FR-030–034), the MLIR/GLP-dialect primitive spec (FR-040–042), the Shapiro/embedded-switch mapping
   (FR-050–051), the interactive spec-step protocol (FR-060–061), and a **protocol/concurrency
   verification armoury** (SPIN default + 6 alternatives, FR-078–079).
2. **Three runnable validation spikes** against *real installed tools* (the load-bearing new work):
   - **Lean** (FR-035): a Python harness drives a bounded Claude-over-MCP tactic loop against a **real
     Lean 4 install** (WSL2/container per R10) on one concrete GLP property; records proved/`sorry`-isolated
     + tactic-attempt count.
   - **MLIR** (FR-043): a Python harness (MLIR Python bindings) realizes the four GLP/FCP dialect primitives
     on a minimal GLP IL fragment and demonstrates `decode(encode(p)) ≡ p`; records pass/fail.
   - **SPIN** (FR-080): a minimal Promela model of the front↔back request/response protocol checked with
     **real SPIN** for deadlock-freedom + progress; records the verdict (or counterexample).

**Technical approach**: docs are Markdown under the existing `docs/research/repl-engine-separation/`
tree; spikes are self-contained, reproducible Python/Promela harnesses under a new `spikes/` subtree, each
committing its reproduction command, tool versions, and recorded result (FR-071). The refinement/verification
LM steps run **in Claude via Agent-tool seams / MCP** — never OpenAI/litellm/`OPENAI_API_KEY` (FR-012/073);
deterministic tooling (Lean/MLIR kernels, the round-trip oracle, SPIN, Z3/CVC5) is ordinary local tooling.

## Technical Context

**Language/Version**: Python 3.11 (spike harnesses, reusing `codeconv/.venv`); Markdown (framework artifacts);
Lean 4 (toolchain via `elan`/`lake`, latest stable); Promela (SPIN 6.5.2+); MLIR (LLVM Python bindings).
**Primary Dependencies**: real Lean 4 toolchain + **Lean-LSP-MCP** (Claude-native, model-agnostic; APOLLO-style
`sorry`-isolation); **MLIR Python bindings** (`mlir` from an LLVM build / `llvmlite`-adjacent / pip `mlir` wheel);
**SPIN** model checker (`spin` + a C compiler for the verifier); Claude Agent-tool/MCP seams for all LM steps;
the in-repo `codeconv.tools.codegen_opt` precedent (`optimize.py:257–335`) as the loop reference.
**Storage**: filesystem only — Markdown docs + committed spike artifacts (harness source, reproduction script,
`tool-versions.txt`, recorded `RESULT.md`). No database; no PGLite involvement.
**Testing**: each spike is its own acceptance test (it must run against the real tool and record a result);
`grep` invariants for the no-API rule (FR-012/SC-003); doc-completeness checks (template well-formedness,
armoury ≥7 tools, six tooling slots, five Shapiro criteria mapped). The GLP REPL suite is **not** touched
by this feature (no runtime change).
**Target Platform**: Windows 11 host (`D:\`) for authoring + the SPIN/MLIR spikes where wheels exist; **WSL2 /
Linux container** for the Lean 4 toolchain and Lean-LSP-MCP (R10/FR-033 — Lean tooling is Linux/Mac-first).
**Project Type**: methodology + research-validation (docs + three feasibility spikes). No application code,
no library, no service.
**Performance Goals**: N/A — feasibility, not performance. The only "performance" notion is the Lean
tactic-attempt budget (start **20**, empirically tuned during the spike, FR-031).
**Constraints**: (1) **No external LM API** on any refinement/verification path (FR-012/073, SC-003) —
hard, grep-enforced. (2) Spikes are **minimal feasibility spikes** (one GLP property; one IL fragment; one
handshake), NOT full implementations (FR-074) — full proofs/MLIR-infra/protocol-model stay at #4/#11/#12 and
#5/#6. (3) Every spike must be **reproducible** from a committed command + pinned tool versions (FR-071).
(4) Real tools required — desk research does NOT satisfy FR-035/043/080 (R13/R14).
**Scale/Scope**: 1 epic, 15 successor seeds (#2–#16) consume the framework; this feature delivers ~7
framework artifacts/sections + 3 spikes. No user-facing surface.

### NEEDS CLARIFICATION — resolved in Phase 0 research

- **[ENV-1]** Lean 4 install path on this Windows host (WSL2 vs Docker container) and Lean-LSP-MCP wiring to
  Claude → resolved in research.md §1.
- **[ENV-2]** MLIR Python-bindings acquisition on Windows (prebuilt wheel vs WSL2 LLVM build) → research.md §2.
- **[ENV-3]** SPIN install on Windows (native `spin.exe` + `gcc`/`cl` vs WSL2) → research.md §3.
- **[PROP-1]** The single concrete GLP property for the Lean spike (candidate: SRSW preservation, or
  unification soundness/`decode∘encode=id` on a toy clause) → research.md §1.
- **[ILFRAG-1]** The minimal GLP IL fragment for the MLIR round-trip (which opcodes/primitives) → research.md §2.
- **[HANDSHAKE-1]** The minimal front↔back handshake to model in Promela (request → ack → response → close) →
  research.md §3.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution (`.specify/memory/constitution.md`) is the **unfilled template** (placeholders only)
— it ratifies no concrete gates. The effective governance for this feature is therefore the repo's
authoritative discipline in `CLAUDE.md` + `docs/DISCIPLINE.md`. Checked against those:

| Principle (CLAUDE.md / project) | Status | Notes |
|---|---|---|
| **Spec-first; single source of truth** | ✅ PASS | Spec (#1a) ratified 2026-06-09; this plan adds no requirement absent from spec.md. Each artifact has ONE authoritative home under `docs/research/repl-engine-separation/`. |
| **No external LM API (GEPA runs in Claude)** | ✅ PASS (load-bearing) | FR-012/073 + SC-003 make this a grep gate, not a preference. Mirrors `codeconv-codegen-opt` precedent. |
| **Language authority (no new GLP primitives without Gabi)** | ✅ PASS | This feature defines no GLP language surface. The one place a new system predicate would arise (self-prove liveness) is explicitly deferred (DEF-F1), not touched here. |
| **GLP code location policy** | ✅ N/A | No `.glp` source is authored; the Lean spike's GLP property is *modeled in Lean*, referencing existing `programs/` semantics, not adding GLP code. |
| **Owner-confirmation gates** | ✅ PASS | The interactive spec-step protocol (FR-060) and the spike-result review are owner gates by design. No hard-to-reverse action (commit/merge/migration) is taken without surfacing it. |
| **Real-tool validation over desk research** | ⚠ RISK-TRACKED | R13/R14 require *real installed tools*. The environment lift (Lean/MLIR/SPIN on a Windows host) is the dominant feasibility risk — front-loaded as Phase 0 research + explicit setup tasks, with WSL2/container fallbacks. Not a constitution violation; a sequencing risk. |

**Gate result: PASS.** No violations to justify in Complexity Tracking. The single tracked risk
(real-tool environment setup) is a research/sequencing item, addressed in Phase 0, not a principle breach.

## Project Structure

### Documentation (this feature)

```text
specs/027-refinement-verification-framework/
├── plan.md              # This file (/buildkit-plan)
├── research.md          # Phase 0 — env setup paths + spike-target choices (/buildkit-plan)
├── data-model.md        # Phase 1 — framework entities (/buildkit-plan)
├── quickstart.md        # Phase 1 — how to instantiate the template + re-run each spike (/buildkit-plan)
├── contracts/           # Phase 1 — artifact/spike acceptance contracts (/buildkit-plan)
│   ├── metric-combination-template.contract.md
│   ├── refinement-loop.contract.md
│   ├── lean-spike.contract.md
│   ├── mlir-spike.contract.md
│   └── spin-spike.contract.md
├── checklists/          # (pre-existing dir)
└── tasks.md             # Phase 2 — (/buildkit-tasks, NOT created here)
```

### Source / deliverable layout (repository root)

The framework artifacts live in the existing reconciliation tree (single source of truth); the three
spikes get a new self-contained `spikes/` subtree alongside the methodology they validate.

```text
docs/research/repl-engine-separation/
├── reconciliation/
│   ├── REFINEMENT-METHOD.md            # FR-001 — finalize (exists): loop + metric model + no-API + budget
│   ├── DECISIONS-FOR-OWNER.md          # FR-002 — finalize (exists): prover choice, MLIR primitives, Shapiro map
│   ├── METRIC-COMBINATION-TEMPLATE.md  # FR-003 — NEW: reusable `name|kind|tool|threshold` table + filled example
│   ├── LEAN-TACTIC-LOOP.md             # FR-030–034 — NEW: bounded tactic-loop architecture sketch
│   ├── MLIR-GLP-DIALECT.md             # FR-040–042 — NEW: four primitives + round-trip criterion + citation note
│   ├── PROTOCOL-VERIFICATION-ARMOURY.md# FR-078–079 — NEW: SPIN default + 6-tool matrix + seed-type selection
│   ├── INTERACTIVE-SPEC-STEP.md        # FR-060–061 — NEW: owner-confirmation protocol + PRE-SPECIFY pointer rule
│   ├── DECISIONS-LOG.md                # (exists) R1–R15 — referenced, R13–R15 already added
│   └── DEFERRALS.md                    # (exists) DEF-A3/B1/B2/H1 — referenced, updated statuses
└── spikes/                             # NEW — reproducible real-tool validation spikes (FR-071)
    ├── lean/
    │   ├── harness.py                  # drives Claude-over-MCP bounded tactic loop (no API)
    │   ├── <GlpProperty>.lean          # the one concrete GLP property under proof
    │   ├── run.sh / run.ps1            # reproduction command
    │   ├── tool-versions.txt           # Lean/elan/Lean-LSP-MCP versions
    │   └── RESULT.md                   # recorded outcome (proved|sorry) + tactic-attempt count
    ├── mlir/
    │   ├── harness.py                  # builds the 4 primitives; asserts decode(encode(p))≡p
    │   ├── run.sh / run.ps1
    │   ├── tool-versions.txt           # MLIR/LLVM + python-bindings versions
    │   └── RESULT.md                   # recorded pass/fail on the minimal IL fragment
    └── spin/
        ├── front_back.pml              # minimal front↔back handshake model
        ├── run.sh / run.ps1           # `spin -a` + `gcc pan.c` + `./pan` (or `spin -run`)
        ├── tool-versions.txt           # SPIN version
        └── RESULT.md                   # deadlock-freedom + progress verdict (or counterexample)
```

**Structure Decision**: framework docs extend the **existing** `docs/research/repl-engine-separation/reconciliation/`
authoritative tree (no duplication — single source of truth per CLAUDE.md). The three spikes are
**self-contained and reproducible** under a sibling `docs/research/repl-engine-separation/spikes/{lean,mlir,spin}/`,
each carrying its harness, reproduction command, pinned tool versions, and recorded `RESULT.md` so #4/#11/#12
and #5/#6 inherit a working starting point (FR-071/072). Python harnesses reuse `codeconv/.venv`; no new
package is created. No application/runtime code is added.

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.

The single tracked risk (real-tool environment setup on a Windows host) is a Phase-0 research item with
documented WSL2/container fallbacks (R10), not an architectural complexity that needs justification.
