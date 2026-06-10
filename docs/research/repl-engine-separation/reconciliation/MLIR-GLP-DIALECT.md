# MLIR GLP/FCP IL-Dialect — Specification

**Feature**: 027 refinement-verification-framework · **Story**: US4 (P2) · **Task**: T016
**Status**: AUTHORITATIVE for the GLP/FCP MLIR-dialect *primitive set* + round-trip criterion.
**Requirements**: FR-040, FR-041, FR-042. Consistent with `REFINEMENT-METHOD.md` §4, tooling slot 2.
**Empirical backing**: the round-trip claim below is validated by a recorded **real-MLIR** spike —
`../spikes/mlir/RESULT.md` (✅ PASS, 2026-06-10) — not desk research (R13/R14, FR-070).

---

## 1. Purpose

This document fixes the **GLP/FCP MLIR dialect primitives** and the **deterministic acceptance
criterion** any IL-touching seed (#4 IL codec, #11 compiled-IL-on-the-wire, and the language seeds
that lower through them) must meet. It is a *specification*, not the production dialect: the full
dialect, its verifier, and the lowering passes are built at #4/#11 (deferred — DEF-B1/H1). What is
in scope here is the **four-primitive vocabulary**, the **round-trip oracle**, the **Claude-role
restriction**, and the **citation-to-pin**.

## 2. The four dialect primitives (FR-040)

The GLP/FCP execution model is three-phase (HEAD tentative unification → GUARD pure tests → BODY
mutations) with suspension/reactivation on readers. The dialect represents a clause through exactly
four primitives, each carrying a precise GLP semantics:

| Primitive | GLP-semantic meaning | FCP/runtime anchor |
|---|---|---|
| **HEAD-unify** | Tentative unification of a goal against a clause head — writer/reader binding under the writer-MGU (binds writers only, never readers; three-valued Success\|Suspend\|Fail). | HEAD phase; `_TentativeStruct` / `_ClauseVar` building (CLAUDE.md runtime arch). |
| **GUARD-test** | A pure, side-effect-free guard test gating commitment (`ground`, `=?=`, comparison guards) — three-valued (succeed\|suspend\|fail), no bindings escape. | GUARD phase; committed-choice commit point. |
| **BODY-spawn** | Spawn a concurrent body goal after commitment — the body's writers may bind, new goals enter the pool. | BODY phase; concurrent goal spawn. |
| **suspend-reactivate** | A goal blocked on an unbound **reader** suspends, and reactivates when the paired **writer** binds it. | `SuspendedGoals` / `BlockingReaders` (scheduler), suspension correctness (REFINEMENT-METHOD §4). |

These four are *sufficient* to express one well-typed GLP clause's control/data shape: a head match,
a guard, a spawned body goal, and the suspension point its readers carry. ILFRAG-1
(`p(X, Y?) :- ground(X?) | q(Y).`, `../spikes/mlir/ilfrag1.py`) touches each exactly once.

## 3. Progressive-lowering intent (FR-040)

The dialect is positioned as the **high level** of a progressive-lowering stack: `glp` dialect →
(lower) → imperative/SSA targets (e.g. a control-flow + heap-op dialect) → (lower) → LLVM IR / a
bytecode target. Each primitive has a lowering obligation deferred to #11:

- HEAD-unify → unify-loop + trail/bind ops; GUARD-test → branch on a pure predicate;
  BODY-spawn → goal-queue push; suspend-reactivate → reader-wait + wake-list wiring.

Verification of the dialect itself is intended via **first-class verification dialects** (PLDI'25
direction): op-level verifiers asserting phase ordering (HEAD before GUARD before BODY) and
single-writer discipline. Out of scope for #1a; recorded as the #11 obligation.

## 4. Acceptance criterion — round-trip identity (FR-041)

The **primary, deterministic** metric for every IL-touching seed is

> **`decode(encode(p)) ≡ p`**

— encode a GLP IL fragment `p` into the dialect, render and re-parse it, decode back, and require
structural identity with `p`. This is the byte-/structure-parity oracle of `REFINEMENT-METHOD.md`
§4 slot 3, specialized to the dialect.

**Claude is restricted to structural generation only.** The model may *author* the structural
encoder; it does **not** sit on the verification path. The MLIR parser/printer + the structural
equality check are the deterministic oracle that decides pass/fail. This directly mitigates risk
**U4** ("LLMs struggle with IR control flow"): correctness never depends on model judgement, only on
a deterministic round-trip. No external-LM API is reachable on this path (FR-073; grep-gated at
T010/T025).

### 4.1 Validation (FR-043, recorded)

The criterion is not asserted on paper. The runnable experiment
(`../spikes/mlir/harness.py`) realizes the four primitives as `glp.*` ops in **real compiled-LLVM
MLIR** (`mlir-python-bindings 22.0.0`, WSL2) and demonstrates both the structural identity
`decode(encode(p)) == p` and textual idempotence on ILFRAG-1 — recorded **PASS** in
`../spikes/mlir/RESULT.md`. Reproduce via `../spikes/mlir/run.sh` (or `run.ps1`).

## 5. Citation to pin (FR-042, DEF-B2 — open, non-blocking)

The Typed-Multi-level-Datalog-IR reference `arXiv:2502.06854` is **mis-attributed** — that paper is
an *LLM-comprehension-of-LLVM-IR* study, not a Datalog/relational-IR-on-MLIR work. The correct
reference is most likely **LingoDB (Jungmair et al., VLDB 2022)** — a relational/Datalog-style query
engine built on MLIR. This is recorded **open**, anchored to the **#4 / #12** spike (DEF-B2), and
**must not block** this feature (FR-042). Pin/confirm it when #4 selects its IL-on-MLIR references.

## 6. Scope boundary

In scope (here): the four-primitive vocabulary, the round-trip oracle, the Claude-role restriction,
the citation-to-pin — plus the minimal real-MLIR validation spike (FR-074). **Out of scope** (the
work this spec de-risks): the full opcode set, the production `glp` dialect + TableGen + verifier,
and the lowering passes — all at **#4 / #11** (DEF-B1/H1). "Identical IL" definition forks
(byte vs exec-equivalent, raw vs `CombinedProgram`, obsolete v1 opcodes) are deferred to the #4 spec
(DEF-B3).
