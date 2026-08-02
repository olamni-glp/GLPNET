<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: b8e2c05d-6f13-49a7-a1d4-72c9e0836af5
-->

# Feasibility Study — C++ engine + scheduler + compiler for GLP

**Feature**: 062 Wave 4 consolidated (US2 / FR-003 / SC-002) — task T013
**Roadmap item**: `cpp-engine-feasibility`
**Type**: ADR-style feasibility study (decision-ready). No runtime code changes.
**Author date**: 2026-07-29
**Status**: Delivered — recommendation below.

---

## Question

Is a **native C++ GLP engine** — a concurrent-logic execution engine with its own scheduler,
heap, and (optionally) compiler front-end — worth building, and on what path? The roadmap frames
it as "decisive for the many-instance goal" (small footprint, high perf, portability incl.
embedded targets), following the FCP lineage — FCP being Ehud Shapiro's C reference
implementation of Flat Concurrent Prolog, the direct ancestor of GLP.

Sub-questions the scope must settle:
- **Scope**: a pure C++ *executor* (consumes pre-compiled IL over the wire, IL-in / result-out),
  or a full C++ *front-end + executor* (its own compiler producing the same IL)?
- **Prior art**: does FCP's C emulator (and the KLIC/BinProlog lineage) make this tractable?
- **Interop**: how does a C++ engine sit alongside the existing Dart, Gleam, and C# runtimes?
- **Maintenance**: what is the cost of carrying yet another full engine?

---

## Evidence base (repo state + prior art)

Verified this session:

- **The repo already carries three GLP engines plus a transport line**, all of which a C++
  engine would join or duplicate:
  - **Dart `glp_runtime/`** — the authoritative engine (REPL pipeline: SRSW → partial eval →
    type check → compile → execute; `lib/compiler/`, `lib/runtime/`, `lib/bytecode/`,
    `lib/multiagent/`). This is the source of truth per CLAUDE.md.
  - **Gleam `glp_gleam/`** — a full faithful engine spine: `src/glp/runtime` (heap, unify,
    suspension), `compiler` (partial_eval, codegen), `analysis/type_checker`, `codec`, `link`,
    `multiagent`. A large, actively-tested BEAM-hosted reimplementation already exists.
  - **`out/csharp/`** — a codeconv **machine-conversion** of the Dart engine to C#
    (`lib/engine/glp_engine.cs`, `lib/runtime/scheduler.cs`, `lib/runtime/heap_fcp.cs`),
    generated, not hand-authored.
  - **`csharp/` hand line** — transport/wire/codec only (glp_link/FrameCodec/TcpTransport,
    glp_il_codec, glp_result_codec, glp_wire_registry, glp_crdtmsg). **No execution engine.**
- **A GLP-native binary IL codec is shipped** (`csharp/glp_il_codec/IlCodec.cs`): a
  self-describing, versioned byte encoding of a `BytecodeProgram` (v1 `IOp` + v2 `IOpV2`
  families, `Label`, `VariableMap`) with loud failure. A C++ *executor* has a concrete,
  documented IL contract to consume — the prerequisite the reconciliation memos (#14, depends_on
  the il-codec spike) required now exists on the wire side.
- **Reconciliation memo #14** (`.../reconciliation/14-cpp-engine-feasibility.md`) is a detailed
  prior analysis: it classifies the item as EXPERIMENT, confirms no C++ engine code exists,
  identifies the scope fork (pure executor vs full front-end), and enumerates the C# substrate a
  C++ engine must re-implement (three-valued unification loop, SRSW checks, suspension/
  reactivation, fairness, heap). NB: its `out/csharp/...:line` citations are against the
  conversion mirror, not the hand `csharp/` line.
- **Prior art (external, cited in memo #14 / research-programme.md), directly on point**:
  - **FCP sequential abstract machine** (Houri & Shapiro, CARMEL-2 = **29 instructions**, C
    emulator) — GLP's direct committed-choice ancestor; proof the ISA is tiny and C-implementable.
  - **KLIC** — KL1 (Flat GHC, same committed-choice family) → portable C on stock Unix.
  - **BinProlog / BinWAM** — ~4500 LOC C emulator, ~123 instructions, designed for embedding.
  - **InductorProlog** — lightweight embeddable C++ Prolog (game-AI/HTN, memory-constrained,
    Windows/Mac/iOS).
  - **ANTLR4 C++ target** — mature; one `.g4` grammar → C++ parser (relevant only to the
    full-front-end scope).
- **GLP semantic invariants a C++ engine must preserve** (CLAUDE.md, manuals): committed-choice,
  SRSW, three-phase HEAD/GUARD/BODY, suspension on unbound reader + exactly-once reactivation,
  monotone write-once binding, three-valued unification. Memo #14's "Shapiro criteria preserved"
  section enumerates these as hard constraints for the embedded-switch purpose.

---

## Options considered

**O-1. Pure C++ executor (scenario a).** Consumes pre-compiled GLP IL from the existing codec
over the existing `FrameCodec`/`TcpTransport` wire; re-implements heap + three-valued unification
+ SRSW + suspension/reactivation + a cooperative drain loop in C++. No C++ compiler. Test =
execute-equivalence against the Dart reference on the REPL corpus, plus IL round-trip fidelity.
*Effort*: bounded. *Payoff*: a footprint/perf number for the many-instances goal; a portable
embeddable engine core. *Dependency*: the IL codec (shipped) + optionally a shared grammar (only
for cross-checking).

**O-2. Full C++ front-end + executor (scenario b).** Adds an ANTLR4-C++ (or hand) compiler
producing byte-identical IL, so C++ is a standalone GLP system. *Effort*: much larger — requires
the compiler-relocation refactor (the "factor out compiler" line, 062 US3) as a hard prerequisite
and cross-compiler IL-identity verification. *Payoff*: validates the full
language-portability thesis. *Risk*: a fourth full compiler front-end to keep in lock-step with
Dart/Gleam/C# semantics forever.

**O-3. Split (#14a executor spike, #14b front-end spike).** Run O-1 first as a de-risking spike;
gate O-2 on its footprint verdict. Finer roadmap control; #14a can unblock the many-instances
work independently.

**O-4. Do not build a C++ engine; treat Gleam/BEAM (+ AtomVM) as the "small many-instance"
answer.** The roadmap's own many-tiny-backends / virtual-thread direction (MEMORY: 050 madGLP
Gleam port; AtomVM work) targets the same footprint/many-instance goal on the BEAM process model,
which already gives cheap per-process isolation. The Gleam engine spine already exists and is
tested. *Payoff*: no new engine; reuse in-flight work. *Against*: BEAM/AtomVM footprint and
embedded-C reach are different from a bare-metal C++ core; does not directly answer the
"embedded switch / QHSM actors in C++" purpose.

---

## Recommendation: CONDITIONAL-GO — a **narrow C++ executor spike (O-1)**, explicitly as an EXPERIMENT, not a committed build

The correct next step is a **feasibility spike of the pure C++ executor (O-1)**, and **NO-GO on
the full C++ front-end (O-2) for now**. Rationale, grounded:

- The item is genuinely an EXPERIMENT: **zero C++ engine substrate exists** (memo #14 confirms).
  The decision to build (or not) a production C++ engine cannot be made without a real
  footprint/perf number, which only a spike produces.
- O-1 is now *unblocked* on its hard dependency: the IL codec is shipped, so a C++ executor has a
  concrete, versioned, documented IL to decode. The FCP/KLIC/BinProlog prior art shows a
  committed-choice engine reduces to a **small** C emulator (29–123 instructions), so the spike
  is tractable.
- O-2 (full front-end) should **not** be committed: it multiplies the maintenance surface (a
  fourth compiler that must stay byte-identical to Dart/Gleam/C# — the repo already pays a
  parity tax via `out/csharp` conversion and the Gleam port), and it depends on the
  compiler-factor-out refactor (062 US3) which is itself in flight. Defer O-2 behind O-1's
  verdict.
- The spike must be allowed to **declare infeasibility** — a "C++ cannot hit the footprint
  target for the many-instance goal" verdict is as valuable as a go, and closes the thread
  cleanly (memo #14 recommendation).

This is a GO to *learn*, not a GO to *build a production engine*. Whether a C++ engine ships is a
later decision gated on the spike's numbers and on the companion many-instances study.

---

## Staged plan (the O-1 executor spike)

Before starting, the owner must settle five inputs (memo #14 open questions):

1. **Footprint target** (U1) — BEAM's ~2.6 KB/process order of magnitude, a project-specific
   number, or measurement-first with "beat the C#/Dart baseline" as the bar. **Must be set at
   spec time** or the spike has no pass/fail.
2. **Scope confirm** — pure executor (O-1), not front-end (O-2).
3. **Toolchain** — C++17 + CMake, clang/MSVC dual-verified, Windows+Linux (portable-first) vs
   MSVC-only (fastest to a number).
4. **Scheduler model** — a simple drain loop mirroring the existing cooperative scheduler (lowest
   risk, most faithful) vs C++20 coroutines vs an actor framework (CAF). Recommend the simple
   drain loop for the spike.
5. **Formal gate** — whether an SRSW-preservation invariant (writer-MGU never binds a reader) is
   proven in-spike (Lean 4) or deferred; recommend deferring formal proof, keeping the pragmatic
   execute-equivalence corpus as the in-spike gate.

Then:

| Step | Work | Success signal |
|---|---|---|
| C-0 | Minimal C++ heap (`std::vector<HeapCell>` / arena) + IL decoder mirroring the `csharp/glp_il_codec` payload format | decode a small IL corpus without error; round-trip fidelity vs the C# codec |
| C-1 | Executor skeleton: HEAD/GUARD/BODY dispatch for a minimal opcode subset (unify-constant, put-structure, spawn, commit) | a handful of ground goals run to the same result as the Dart reference |
| C-2 | Suspension + reactivation + three-valued unification + SRSW enforcement | suspend/resume corpus matches; no SRSW violation path |
| C-3 | Full opcode set; execute-equivalence against the REPL test corpus (goals compiled to IL) | 100% result match (bindings + status) on the corpus |
| C-4 | Footprint measurement: per-instance resident memory of a loaded-but-idle engine, N instances | number reported against the U1 target |
| C-5 | **Verdict**: feasible / feasible-with-scope-constraint / not-feasible | written go/no-go for a production C++ engine + for O-2 |

Interop note: the spike engine speaks the **existing wire** (`FrameCodec`/`TcpTransport`) and the
**existing IL** (`glp_il_codec` format), so it composes with the Dart/Gleam/C# runtimes as another
endpoint rather than replacing any — no cross-runtime rewrite is implied by the spike.

---

## Risks (named)

- **R1 — Maintenance-surface multiplication.** The repo already keeps Dart (authoritative), a
  Gleam port, and an `out/csharp` conversion in semantic lock-step; a hand-written C++ engine —
  especially the O-2 front-end — adds a fourth divergence point. Every language-level change
  (e.g. the two §1.14 items in this very wave) would need a C++ mirror. *Mitigation*: keep the
  spike to O-1 (executor consumes IL — no compiler to keep in sync); make the production-build
  decision explicitly weigh this recurring cost.
- **R2 — Footprint target undefined → no pass/fail.** **Insufficient evidence flagged**: no
  per-instance footprint budget exists in the repo (memos #14 U1, #15 U3). Without it C-4/C-5
  cannot conclude. Must be set at spec time. This is the single biggest blocker to a *decisive*
  verdict.
- **R3 — Semantic drift from the reference.** A hand C++ re-implementation of three-valued
  unification / suspension can subtly diverge from the Dart source of truth. *Mitigation*: the
  execute-equivalence corpus (C-3) is the gate; deviations are stop-and-report (Bug Protocol),
  not "robustness" patches.
- **R4 — Scope creep executor→front-end.** "C++ engine+scheduler+compiler-front-end" conflates
  O-1 and O-2; the compiler half silently pulls in the 062 US3 factor-out refactor and ANTLR4
  grammar work (memo #14 T1/T2). *Mitigation*: the recommendation hard-scopes to O-1; O-2 is a
  separate later feature gated on O-1.
- **R5 — Overlap with the BEAM/AtomVM many-instance direction.** The roadmap already pursues
  "many tiny backends" on Gleam/BEAM (O-4). A C++ engine and the BEAM direction could be solving
  the same many-instance goal twice. *Mitigation*: the companion `many-instances-…` study
  compares these substrates head-to-head; sequence the C++ spike's footprint number against the
  BEAM per-process baseline before committing to either as *the* many-instance path.
- **R6 — Prior-art currency.** FCP/KLIC/BinProlog are mature but old; toolchain/build-integration
  effort (CMake, embedded targets, ABI) is under-scoped in memo #14 U3 and would surface in C-0.
  *Mitigation*: treat toolchain as a first output of the spike, MSVC-first if a fast number is
  wanted.

---

## One-line verdict

**CONDITIONAL-GO on a narrow C++ *executor* feasibility spike (consumes the shipped GLP-native IL
over the existing wire; FCP/KLIC/BinProlog make it tractable), gated on the owner first setting a
per-instance footprint target; NO-GO for now on a full C++ compiler front-end (defer behind the
spike's verdict — its maintenance cost as a fourth semantic mirror is not yet justified) — and the
spike must be free to return "not feasible."**
