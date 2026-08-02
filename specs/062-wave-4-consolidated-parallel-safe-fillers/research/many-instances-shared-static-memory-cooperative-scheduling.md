<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 5c9a4e17-8b30-42df-bf6e-1a02d7c95e84
-->

# Feasibility Study — Many engine instances over shared static memory with cooperative scheduling

**Feature**: 062 Wave 4 consolidated (US2 / FR-003 / SC-002) — task T014
**Roadmap item**: `many-instances-shared-static-memory-cooperative-scheduling`
**Type**: ADR-style feasibility study (decision-ready). No runtime code changes.
**Author date**: 2026-07-29
**Status**: Delivered — recommendation below.

---

## Question

Can we run **many GLP engine instances** on one machine over a **two-tier memory model** — an
immutable, shared-static code/construct region plus a small per-instance dynamic tier — with
**cooperative run-to-completion scheduling** (each atomic reduction chain runs, then returns
control to the OS/host), at a per-instance footprint low enough to make the "many tiny REPL
backends / virtual-thread" model (roadmap; MEMORY 050 madGLP → BEAM/AtomVM; the yngenios
front/back split) real?

Sub-questions:
- Is memory-sharing *safe* under GLP's SRSW discipline?
- Is cooperative (not preemptive) scheduling sufficient and safe for GLP, and where is the safe
  yield/persist boundary?
- How does this map onto the existing C# / Gleam / (candidate C++) substrates and onto
  BEAM/AtomVM?

---

## Evidence base (repo state + prior art)

Verified this session:

- **Per-instance heap isolation already exists and is by-design single-owner.** The C# mirror
  `out/csharp/lib/runtime/heap_fcp.cs` (memo #15, lines 133-141) documents: "NOT static — the
  runtime supports multiple concurrent heaps (one per agent/MadContext) … Every HeapFCP instance
  is accessed by exactly one execution context. Fields are plain (no lock, Interlocked,
  ConcurrentDictionary, or volatile)." The Dart `glp_runtime/lib/runtime/` and the Gleam
  `glp_gleam/src/glp/runtime/heap.gleam` follow the same per-instance model. This is the
  per-instance dynamic tier the design wants — **already present**.
- **A proto-cooperative yield signal already exists** but at the wrong granularity: the tail-
  recursion budget (`out/csharp/.../runtime.cs` TailReduce, `fairness.cs` NextTailBudget/
  ResetTailBudget, `machine_state.cs` `TailRecursionBudgetInit=26`) yields *per tail step*, not
  *per complete reduction chain* (memo #15 T1/U4). The many-instances design specifies chain-
  granular yield.
- **A quiescence boundary exists in the drain loop.** `out/csharp/lib/engine/glp_engine.cs`
  `DrainAsyncWithStatus` (+ the InboundPump re-drain) runs to quiescence between link-frame
  servicing (memo #15). This is the natural "return control to the OS" boundary; at it the engine
  is effectively **stackless**, so preempt = "stop scheduling new chains", resume = "re-enter the
  loop" (research-programme.md Axis 3).
- **What is confirmed MISSING (zero in code, memo #15)**: any two-tier shared-static region;
  instance-relative (root-register) addressing (heap addresses are plain `int` indices); a
  pooling/CoW allocator; a chain-boundary preempt flag / epoch signal; any multi-instance
  orchestrator. The item is a genuine "deep design unknown."
- **Shared static code across many instances is standard practice** (research-programme.md Axis 2,
  each pinned to a primary source): V8 embedded builtins move cost from `c*(1+n)` to `c*1` by
  placing builtins in read-only `.text` the OS shares across processes; **BEAM** spawns a process
  in **327 words (~2.6 KB)** with a private heap/stack/mailbox and shared module/literal/atom
  areas (literals sent by pointer, never copied); JVM CDS/AppCDS mmaps one read-only archive into
  every process; Wasmtime's pooling allocator + CoW gives fast same-module instantiation; **Akka
  is the anti-pattern** (shared bytecode but one shared GC heap — couples GC/preempt/crash), which
  is exactly what GLP must avoid.
- **Cooperative run-to-completion with safe yield points is precedented**: BEAM's reduction
  budget yields only at safe boundaries ("a process can only be suspended at certain points");
  Wasmtime epoch-interruption (a flag checked between chains, 2-3× cheaper than fuel); GraalVM
  Espresso continuations prove a single-threaded interpreter can be snapshot+resumed *if*
  suspension is confined to safe points; KL1/KLIC ran concurrent committed-choice logic on stock
  Unix. All in the same committed-choice lineage as GLP.
- **The roadmap is already moving toward BEAM/AtomVM for this goal** (MEMORY: 050 madGLP Gleam
  port A-then-B — "refactor to BEAM/AtomVM process model, many tiny REPL backends"; AtomVM Node
  wrapper). The Gleam engine spine (`glp_gleam/`) runs on BEAM, whose process model *is* a
  production "many lightweight isolated instances + per-process heap + shared literals" system.
- **GLP invariants that bound the design** (CLAUDE.md, manuals; memo #15 "Shapiro criteria"):
  committed-choice (a committed clause is irrevocable), SRSW, suspension-exactly-once, monotone
  write-once binding, three-valued unification. The safe boundary must be *after* BODY completes
  and all writer binds are flushed, and *before* the next HEAD begins (a clause-entry `kappa`).

---

## Options considered

Two orthogonal axes.

### Axis 1 — deployment / sharing substrate

**D-1. Many OS processes, one engine each; shared-static via OS page-sharing of R2R/AOT `.text`
(or an mmap'd read-only archive).** Matches per-instance OS liveness/supervised restart; crash
isolation is free; heavier per-process overhead. (V8/JVM-CDS/Wasmtime model.)

**D-2. One OS process hosting N in-process engine objects; shared-static via in-process CLR/BEAM
static (single copy of compiled code + shared dispatch tables).** Cheapest sharing; but a crash
or runaway in one instance is closer to its siblings (Akka anti-pattern warns here unless heaps
stay strictly per-instance).

**D-3. BEAM/AtomVM process-per-instance.** The BEAM *is* D-1's benefits with D-2's cheapness:
~2.6 KB isolated processes, per-process heap, shared literals by pointer, preemptive-at-safe-
points scheduling already built. AtomVM extends this to embedded targets. The Gleam engine already
runs here.

### Axis 2 — scheduling / yield granularity

**S-1. Reuse the existing per-tail-step budget** as "cooperative scheduling." Cheapest, but it is
a fairness knob inside one engine, not an OS-yield returning control between whole chains (memo
#15 T1/U4) — under-delivers the "return control to the OS" requirement.

**S-2. Add a coarser chain-boundary yield** at the `DrainAsyncWithStatus` level: an epoch-style
preempt flag checked between complete reduction chains, with an optional per-chain fuel cap to
catch a single non-terminating chain. This is the BEAM-safe-point + Wasmtime-epoch design and the
one the research programme recommends. Requires formally defining "one reduction chain" (memo #15
U1 — per-goal step vs maximal reactivation cascade vs one drain epoch).

**S-3. GLP-level meta-scheduler**: express the yield in GLP on the link layer. Cleaner single-
sourcing but adds a language-surface question (is there a `yield/0` safe-point primitive? — a
§1.14 concern, out of scope for a feasibility study).

---

## Recommendation: GO to design/experiment — the feasibility is real; sequence it behind the C++ footprint number, and treat BEAM as the leading substrate

**Feasibility verdict: FEASIBLE.** Nothing in GLP's model blocks many-instances + shared-static +
cooperative scheduling; on the contrary, GLP's committed-choice + stackless-at-quiescence
structure makes cooperative run-to-completion *easier* than for a backtracking Prolog (no
choice-points/trail to snapshot), and the per-instance single-owner heap is already the right
tier boundary. The dominant industry technique (shared read-only code + per-instance heap) applies
directly, and a production system in GLP's own lineage — the **BEAM** — already implements exactly
this contract. **SRSW is safe under sharing** *provided* the shared tier is strictly read-only
(code/dispatch tables/constant pool) and all mutable logic variables stay in the per-instance
heap; the only SRSW hazard is aliasing between a shared wrapper and a per-instance variable, which
the read-only discipline forecloses (memo #15 Shapiro criterion 2).

But this is a **design/experiment GO, not an implementation GO**, and it should be **sequenced**:

1. It is an EXPERIMENT with "zero implementation substrate" for the two-tier split and chain-level
   scheduling (memo #15). Its output is a design + a safe-preempt protocol + a footprint verdict,
   not shipped runtime behaviour.
2. It **depends on the C++ footprint number** (companion `cpp-engine-feasibility.md`) and on a
   deployment-model decision (D-1 vs D-2 vs D-3) — memo #15 calls these "the two decisions that
   determine almost everything else." Do not design the two-tier memory layout before those are
   set.
3. **Lead with BEAM/AtomVM (D-3) as the reference substrate**, not a from-scratch C#/C++ shared-
   static build. The roadmap is already headed there (MEMORY 050), the Gleam engine already runs
   there, and BEAM gives the many-instance + per-instance-heap + shared-literal + safe-point-
   scheduling contract *for free*. A bespoke C#/C++ shared-static implementation (D-1/D-2) is
   worth pursuing **only** if the C++ spike shows BEAM/AtomVM's footprint or embedded reach is
   inadequate for the target (e.g. the bare-metal embedded-switch / QHSM-actor purpose).

On scheduling, **adopt S-2** (chain-boundary epoch yield) as the design target; S-1's per-step
budget is retained as an in-chain fairness knob, not the OS-yield.

---

## Staged plan (the design/experiment)

Owner settles first (memo #15 interactive-spec inputs): the "reduction chain" definition (U1),
the deployment model (T2: D-1/D-2/D-3), the footprint target (U3), and which formal property is
in scope.

| Step | Work | Success signal |
|---|---|---|
| M-0 | **Define "one atomic reduction chain"** precisely (per-goal HEAD→GUARD→BODY step, vs maximal reactivation cascade, vs one drain epoch). Everything downstream turns on this. | a written, testable definition |
| M-1 | **Chain-boundary quiescence proof** (research-programme SPIKE-1): instrument the runner (Dart / C# mirror) to assert that at the inter-chain boundary there are zero live HEAD-phase artifacts (`_TentativeStruct` / `_ClauseVar`, `sigmaHat` empty), then snapshot {heap, goal queue, suspension lists, scheduler cursor}, deserialize into a fresh engine, and resume to the same result. | full REPL corpus resumes identically; 0 mid-phase preemptions |
| M-2 | **Shared-static classification**: enumerate which constructs are immutable/shareable (compiled clauses, dispatch tables, constant pool) vs per-instance (heap, goal queue, suspension tables, scheduler cursor). | a two-tier boundary table |
| M-3 | **Substrate spike on BEAM/AtomVM (D-3)**: spawn N Gleam engine instances; measure per-instance resident memory and shared-literal behaviour against the target and the ~2.6 KB BEAM baseline. | footprint number vs target |
| M-4 | **(Conditional) bespoke shared-static spike (D-1/D-2)** only if M-3 shows BEAM/AtomVM inadequate for the embedded target: R2R/AOT `.text` COW page-sharing or mmap archive across N processes; per-instance dynamic footprint measured. | footprint number vs target |
| M-5 | **Safe-preempt/resume protocol spec** + cooperative epoch-yield design (S-2), with the chain-boundary as the shared safe-persist point (ties to the persistence feature). | written protocol + preempt/resume correctness evidence |
| M-6 | **Verdict + follow-up gate**: feasible-and-how, on which substrate, with which footprint; hand off an implementation spec (the EXPERIMENT's FOLLOW-UP half — memo #15 U2). | go/no-go per substrate + a follow-up feature seed |

Formal option (recommended in-scope minimum): the **suspension-reactivation-across-preempt/resume**
invariant (a suspended goal is reactivated exactly once, at the same heap address, when its writer
binds post-resume) — mechanizable in Lean 4; the footprint sub-linearity bound is a deferrable
extra (memo #15 metrics).

---

## Risks (named)

- **R1 — Footprint target undefined.** **Insufficient evidence flagged**: as with the C++ and LLVM
  studies, no per-instance footprint budget exists in the repo (memo #15 U3). M-3/M-4 cannot
  conclude "feasible" or "not" without one. The BEAM ~2.6 KB figure is an *aspirational* anchor,
  not an adopted budget. Set it at spec time.
- **R2 — "Reduction chain" ambiguity poisons the whole design.** The safe-yield/persist boundary,
  the scheduling granularity, and the footprint trade-off all depend on M-0's definition (memo #15
  U1). Getting it wrong means either too-frequent yields (overhead) or mid-phase preemption
  (correctness violation). *Mitigation*: M-0 is the first gate; M-1's quiescence assertion is the
  mechanical check.
- **R3 — Mid-phase preemption corrupts committed-choice / SRSW.** A preempt that lands mid-HEAD or
  mid-GUARD would expose partial-commit or partial-unification state to a sibling instance,
  breaking committed-choice and monotone binding (memo #15 Shapiro criteria 1,4,5). *Mitigation*:
  yield only at a clause-entry `kappa` after all writer binds are flushed; M-1 asserts zero live
  `_TentativeStruct`/`_ClauseVar` at the boundary; this is a stop-and-report if violated, not a
  guard-patch.
- **R4 — Shared-tier aliasing breaks SRSW.** If any mutable logic variable leaked into the shared
  region, or a shared wrapper aliased a per-instance variable, SRSW's single-writer guarantee
  would fail. *Mitigation*: the shared tier is strictly read-only; M-2 classification is the gate;
  inter-instance communication routes through a defined boundary (serialize-and-copy across OS
  processes; by-pointer only for read-only shared literals, BEAM-style).
- **R5 — Substrate duplication.** Building a bespoke C#/C++ shared-static engine (D-1/D-2) while
  the roadmap already pursues BEAM/AtomVM (D-3) risks solving the many-instance goal twice.
  *Mitigation*: D-3 leads; D-1/D-2 are conditional on a measured BEAM/AtomVM shortfall for the
  embedded target.
- **R6 — Cross-study dependency chain.** This study depends on the C++ footprint number and feeds
  the persistence work (the chain boundary is also the safe-persist point). Sequencing matters:
  M-0/M-1/M-2 are substrate-independent and can start now; M-3+ wait on the deployment decision.
- **R7 — Cooperative ≠ safe against a runaway chain.** A single non-terminating reduction chain
  never reaches a yield boundary, starving siblings (the BEAM "long-BIF/dirty-scheduler" problem).
  *Mitigation*: the optional per-chain fuel cap (S-2) bounds a single chain; long foreign calls
  must be fenced so they never straddle a boundary.

---

## One-line verdict

**GO to design/experiment — many-instances + shared-static + cooperative run-to-completion is
FEASIBLE for GLP (committed-choice + stackless-at-quiescence make it easier than for backtracking
Prolog; SRSW is safe under a strictly read-only shared tier), with BEAM/AtomVM as the leading
substrate and a chain-boundary epoch yield (S-2) as the scheduler — but it is a design/experiment,
not an implementation commitment: it must be gated on a footprint target being set and on the C++
spike's number, and a bespoke C#/C++ shared-static build is justified only if BEAM/AtomVM proves
inadequate for the embedded target.**
