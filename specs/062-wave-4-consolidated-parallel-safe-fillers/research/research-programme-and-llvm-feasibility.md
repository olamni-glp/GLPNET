<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 3f7c1a94-2d6e-4b81-9c05-8a1e7d4f2b60
-->

# Feasibility Study — Staged research programme + LLVM-based backend

**Feature**: 062 Wave 4 consolidated (US2 / FR-003 / SC-002) — task T012
**Roadmap item**: `research-programme-and-llvm-feasibility`
**Type**: ADR-style feasibility study (decision-ready). No runtime code changes.
**Author date**: 2026-07-29
**Status**: Delivered — recommendation below.

---

## Question

Two coupled questions the roadmap item asks the technical lead to settle on evidence:

1. **Research programme** — what is the staged research path for the GLP-engine work (the
   REPL/engine-separation spine: a stable binary IL on the wire and in persistence, an
   embeddable engine, many-instances), and what is already answered versus still open?
2. **LLVM backend** — is an LLVM-based backend (compile the GLP IL → LLVM IR → native)
   worth pursuing, and if so on what staged path? The concrete sub-question: should the
   GLP intermediate language, its persistence format, or the engine's codegen substrate be
   built on LLVM IR / bitcode / MLIR, or is LLVM at most an optional downstream accelerator?

Both must be answered against what the repo already has, not in the abstract.

---

## Evidence base (what exists in the repo today)

Traceable, verified this session:

- **A GLP-native binary IL codec already exists and is shipped** on the C# line:
  `csharp/glp_il_codec/IlCodec.cs` encodes a compiled `BytecodeProgram` (both the v1 `IOp`
  and v2 `IOpV2` opcode families, plus `Label` markers and an optional `VariableMap`) to a
  self-describing byte payload with a versioned header (`csharp/glp_il_codec/PayloadHeader.cs`,
  `Version = 0x01`, payload-type single-sourced from `csharp/glp_wire_registry`), and decodes
  it back with loud, never-silent failure (`IlCodecException` on any corruption, truncation,
  unknown discriminant, or trailing bytes). This is the codec the earlier reconciliation memos
  (`docs/research/repl-engine-separation/reconciliation/`) said did not yet exist — those memos
  read the stale `out/csharp/` conversion mirror; the live hand-maintained `csharp/` line has
  since delivered it.
- **The scout stage of the LLVM question is already written**:
  `docs/research/repl-engine-separation/llvm-feasibility.md` (authored 2026-06-08, status
  "Scout complete") reaches a CONDITIONAL / off-critical-path verdict with ~40 cited sources.
- **The four-axis prior-art programme is already written**:
  `docs/research/repl-engine-separation/research-programme.md` (logic-IL binary formats;
  shared/instance memory; cooperative scheduling + preempt/resume; orthogonal persistence),
  with a staged plan (engine-review → design spikes → pipeline) and five prioritised code-level
  spikes (SPIKE-1..5).
- **Three engines / one transport line coexist**: the authoritative Dart engine
  (`glp_runtime/`, with `lib/compiler/` = lexer/parser/codegen/partial_evaluator + PMT type
  checker); a full Gleam engine spine (`glp_gleam/src/glp/` — runtime, unify, heap, suspension,
  compiler, analysis/type_checker, codec, link); a codeconv Dart→C# conversion mirror engine
  (`out/csharp/lib/engine|runtime/`); and a hand-maintained C# **transport/codec** line
  (`csharp/` — glp_link/FrameCodec/TcpTransport, glp_il_codec, glp_result_codec,
  glp_wire_registry). The hand `csharp/` line has **no execution engine** of its own.
- **GLP's execution model** (CLAUDE.md, `docs/typed-glp-manual.md`): committed-choice, SRSW,
  three-phase HEAD/GUARD/BODY reduction, goal suspension on an unbound reader + reactivation
  when a writer binds, monotone (write-once) variable binding, three-valued unification
  (Success / Suspend / Fail).

External facts cited below are drawn from `llvm-feasibility.md`, which pins each to a primary
source; the load-bearing ones are re-stated with their citation.

---

## Part A — The staged research programme

### Recommendation (Part A): GO — but as documentation-completion, not new investigation

The programme is **substantially complete**. Its two deliverables (`research-programme.md`,
`llvm-feasibility.md`) exist and are comprehensive; reconciliation memo #16
(`.../reconciliation/16-research-programme-and-llvm-feasibility.md`) independently classifies
this item as "ALIGNED … essentially at its scout-stage completion point." The remaining work is
**coordination**, not research:

- The five code-level spikes (SPIKE-1 chain-boundary quiescence; SPIKE-2 one codec for
  wire+persistence; SPIKE-3 DBOS-shaped `glpengine` schema; SPIKE-4 .NET shared-static
  feasibility; SPIKE-5 restore-and-rebuild a link) are *owned by later features*, not by this
  research item. SPIKE-2 in particular is already partly discharged: `csharp/glp_il_codec`
  is the "one codec" for the wire direction.
- The one substantive housekeeping item is a **spike→feature ownership table** so no spike is
  silently dropped or double-owned (memo #16 U2/U3, T3), plus one **citation fix** (the
  `arxiv:2502.06854` link is mis-attributed to a Typed-Datalog-IR paper; the correct precedent
  is LingoDB, VLDB vol.15 p.2389 — memo #16 T2).

### Staged plan (Part A)

| Stage | Deliverable | Status | Owner |
|---|---|---|---|
| P-0 Scout / prior-art survey | `research-programme.md` + `llvm-feasibility.md` | DONE | this item |
| P-1 Ownership + citation reconcile | spike→feature table appended to `research-programme.md` §5; LingoDB citation pinned | small residual (this wave, doc-only) | this item |
| P-2 IL codec (wire) | `csharp/glp_il_codec` | DONE | il-codec feature |
| P-3 Compiled-IL-on-the-wire (hardened) | factor compiler out; IL both directions | in-flight (062 US3) | US3 |
| P-4 Persistence / snapshot / restore | `glpengine` schema, chain-boundary snapshot+WAL | future feature | engine-snapshot |
| P-5 Many-instances shared-static + cooperative sched | see companion study | future feature | see `many-instances-…md` |

No new research is gated by P-1; it is a tidy-up that keeps the roadmap honest.

---

## Part B — LLVM-based backend

### Options considered

**B-1. LLVM IR / bitcode as the GLP intermediate language and persistence format.**
Compile GLP → LLVM IR, persist bitcode, ship bitcode on the wire.

- *Against, decisive*: LLVM SSA has no representation for a logic variable being destructively
  bound — SSA values are immutable; only memory objects mutate. Unification, the SRSW heap, and
  suspension would all live in LLVM *memory* (loads/stores), where LLVM's value-level
  optimizations cannot see the unification that dominates GLP execution (llvm-feasibility.md
  §2.1, citing the LLVM IR mutable-variables tutorial). LLVM **bitcode is explicitly
  non-portable and non-stable** — it "changes over time as optimization and language
  requirements change," is architecture-dependent, and guarantees only *backward* decode, not
  forward or cross-implementation compatibility (LLVM `BitCodeFormat` docs, verified in
  llvm-feasibility.md §2.5). The GLP IL must be stable, versioned, DB-persisted across
  long-lived restarts, and byte-identical across C#/Dart/Gleam — the opposite of what bitcode
  offers. **This option is a hard structural mismatch.**

**B-2. MLIR custom dialect (`glp.unify`, `glp.suspend`, `glp.commit`, …) → progressive
lowering → LLVM dialect.**
MLIR is infrastructure, not a fixed IR, and matches "define the IL once and lower it" better
than raw LLVM IR (llvm-feasibility.md §1.3). But: MLIR supplies dialect scaffolding and rewrite
passes only — it does **not** supply a GC, term tagging, a suspension scheduler, or a
unification engine; you build all of those. MLIR's own authors flag GC'd languages and
higher-order/polymorphic type inference as **open challenges** (MLIR paper, arXiv 2002.11054,
verified). Every declarative MLIR uptake cited (LingoDB relational-algebra, `bollu/lz` lazy STG)
proves MLIR can host a declarative front-end — *not* unification + SRSW + suspension. High build
weight and per-instance footprint make it antithetical to the many-instances goal (§2.4).

**B-3. LLVM as an optional, gated, downstream native-code accelerator** for *ground,
deterministic, post-commit* numeric/guard-arithmetic kernels only — never the IL, never
carrying unification/suspension/committed-choice semantics, JIT'd via ORCv2 after the hand-built
runtime has already unified and committed. Bounded, evidence-backed payoff: the GHC LLVM-backend
experience is **~20% on numeric/array-heavy code and little on average** (Terei & Chakravarty,
Haskell Symposium 2010, cited §1.1). ORCv2 genuinely matches the "engine generates new IL at
runtime" need (§1.2).

**B-4. No LLVM. GLP-native compact self-versioned binary IL in the WAM/FCP/KL1 lineage, lowered
to C#/Dart/Gleam/C.** This is what the field converges on: KLIC (KL1→C — the *same*
committed-choice family as GLP, deliberately chose C not LLVM), Mercury (C/Java/C#, no LLVM
backend), GNU Prolog (WAM → purpose-built mini-assembly), Ciao/imProlog (WAM-emulator-in-Prolog
→ C, within ~8% of hand-tuned YAP), Souffle (Datalog → specialised C++) — all cited and verified
in llvm-feasibility.md §2.7. It is also **what the repo already did**: `csharp/glp_il_codec` is a
GLP-native, versioned, self-describing serialization of the existing v2.16.x ISA.

### Recommendation (Part B): NO-GO on LLVM as the IL / backend; CONDITIONAL-GO on a gated accelerator spike only

**Do NOT** base the GLP intermediate language, the persistence format, or the engine's mandatory
codegen substrate on LLVM IR, bitcode, or MLIR (rejects B-1 and B-2). The GLP-native IL path
(B-4) is already chosen and shipped (`csharp/glp_il_codec`); it satisfies stability +
cross-runtime + persistence + minimal-footprint, all of which LLVM actively works against. This
confirms and ratifies the existing scout verdict (llvm-feasibility.md §3/§5) rather than
re-opening it.

**Conditionally pursue** an LLVM/ORCv2 accelerator (B-3) **only behind a hard gate**: only if and
when a high-performance native (C++, per the companion `cpp-engine-feasibility.md` study) engine
variant is actually being built **and** profiling shows ground deterministic numeric/guard
kernels are a *measured* bottleneck. Absent both conditions, the answer is **no** — close the
LLVM thread.

### Staged plan (Part B) — the gated accelerator, if and only if the gate opens

1. **Gate check** (not an LLVM task): a native engine variant exists AND a profiler attributes a
   material fraction of runtime to ground arithmetic/struct kernels. If either is false, stop.
2. **Timeboxed throwaway spike (~1 week, off the critical path)**: pick one hot, fully-ground,
   deterministic reduction chain (no unbound readers, no suspension, no choice); lower *that
   slice only* to LLVM IR (or an MLIR `glp`→LLVM dialect); JIT it with ORCv2, invoked by the
   existing runtime *after* commit. Keep unification, SRSW binding, the suspension queue, and the
   scheduler entirely in the hand-built runtime — the spike compiles none of them.
3. **Measure three numbers**: (a) speedup of the JIT'd kernel vs the interpreted IL (GHC bar:
   ~20%+ on numeric-heavy chains, or negligible?); (b) per-instance footprint delta (static-link
   size + resident JIT memory + cold warm-up) against the many-instances budget — the *gating*
   number; (c) containment — prove the wire/DB IL stays unchanged GLP-native bytecode with zero
   LLVM version coupling.
4. **Decision rule**: proceed past the spike only if speedup is material AND the footprint delta
   is acceptable AND containment holds. Any one failing → drop LLVM, keep GLP-native → C/C++.

---

## Risks (named)

- **R1 — Sunk-cost re-litigation.** Risk that the accelerator spike is attempted before its gate
  (before a native engine exists), duplicating rejected B-1/B-2 work. *Mitigation*: make the gate
  a formal blocking criterion, not human memory (memo #16 U1).
- **R2 — Footprint budget is undefined.** The gating number in the spike (footprint delta) has no
  threshold today. **Insufficient evidence flagged**: no per-instance footprint target has been
  set anywhere in the repo (memos #14 U1, #15 U3 both flag this). The accelerator spike cannot
  return a pass/fail on footprint until the companion many-instances study sets a target.
- **R3 — MLIR "looks close."** MLIR's dialect story is genuinely attractive and could tempt scope
  creep into B-2; the honest finding is that MLIR gives scaffolding, not the GC/tagging/
  suspension/unification GLP needs, and its authors flag exactly GLP's needs as open. *Mitigation*:
  the accelerator, if built, is downstream-only; MLIR-as-IL stays rejected.
- **R4 — Bitcode version coupling in persistence.** If any LLVM artifact ever leaked into the
  persisted IL, durable state would couple to an LLVM version and break restart-resume. *Mitigation*:
  containment test (step 3c) is a hard gate; the persisted IL is `csharp/glp_il_codec`'s versioned
  GLP-native payload, never bitcode.
- **R5 — Stale citation propagation.** The `arxiv:2502.06854` mis-attribution (memo #16 T2) could
  propagate into downstream specs. *Mitigation*: the P-1 doc fix pins LingoDB (VLDB vol.15 p.2389).

---

## One-line verdict

**Research programme: GO (documentation-complete; residual is a spike-ownership table + one
citation fix). LLVM backend: NO-GO as the IL/persistence/backend — ratify the shipped GLP-native
codec — with a hard-gated, off-critical-path ORCv2 accelerator spike as the only defensible LLVM
role, and only once a native engine variant and a measured kernel bottleneck both exist.**
