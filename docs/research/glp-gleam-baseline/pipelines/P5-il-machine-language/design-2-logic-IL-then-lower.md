# Seam Design — LOGIC-IL-THEN-LOWER (the GLP/FCP MLIR dialect IS the seam)

**Thesis.** The front-end emits a logic-centric IL — the four-primitive GLP/FCP MLIR dialect `glp.head_unify` / `glp.guard_test` / `glp.body_spawn` / `glp.suspend_reactivate` (`MLIR-GLP-DIALECT.md:22-31`). That IL *is* the seam: the single verifiable/optimizable/multi-target contract between front-end and any back-end. Lowering IL→machine-language is a **back-end step** — to the v2.16.3 bytecode for the Dart/C#/Gleam engines, and (gated, optional) to LLVM/C++ for a native engine.

## 1. What crosses the seam (in-process AND over-the-wire)

Two "over-the-wire" bindings must not be conflated:

- **(a) Deployment/compilation seam — this design's seam.** The artifact is a **GLP-IL module**: a sequence of the four dialect ops with writer/reader-role operands; in-process as IL value objects, over-the-wire as serialized MLIR. The spike round-tripped `GLP-IL→MLIR-text→GLP-IL` with structural identity `decode(encode(p))==p` plus textual idempotence (`RESULT.md:1-7,22-46`). The *same* artifact crosses in-process and remotely — identical payload — because the ops name writer/reader **roles**, never engine heap addresses (unlike today's bindings, which leak live `VarRef`s into engine heap, "the seam's biggest leak", `G4 §2`).
- **(b) maGLP runtime linkage (M2) — NOT this seam.** Live engines exchange **globalized terms + global names**, never IL/bytecode (`G2 §3`: madGLP messages `_w(p,i):=T↑`/`_r(p,i):=T↑`; "no variable ever migrates"). The IL-as-seam coexists with M2 untouched.

This separation is the design's leverage: lowering one IL onto N back-ends yields N engines that all speak the *same* dGLP/madGLP term protocol, so M2 parity becomes a property of the lowering *targets*, not of the seam artifact.

## 2. ANTLR grammar → AST → IL → ML; where the compiler lives

Front-end (any ANTLR-supported host — C#/Dart/Go; ANTLR has **no BEAM target**, `W4 §3`): `.g4` grammar → parse-tree → existing `AstNode` hierarchy → **GLP-IL**. The whole compiler (lex/parse/typecheck/analyze/IL-emit) lives in the **front-end**; the back-end only *lowers + executes*. Today codegen walks the annotated AST straight to bytecode with no IL (`compiler.dart:56-128`, `codegen.dart:107-133`). This design inserts the IL between analyzer and machine-code — exactly where Mercury inserts HLDS before MLDS/LLDS (`mercurylang.org/.../compiler_design.html`) and LingoDB inserts `relalg` before LLVM (`VLDB p2389-jungmair`). One declared grammar → multiple front-ends → one IL is the single-source-of-truth the owner wants in Q4.

## 3. Preserving M1 faithfulness and M2 linked semantics

The four primitives map **1:1** onto GLP's model: `head_unify`=three-phase HEAD writer-MGU, `guard_test`=committed-choice gate, `body_spawn`=post-commit spawn, `suspend_reactivate`=reader-blocks/writer-wakes (`MLIR-GLP-DIALECT.md:26-31`). Crucially, IL **op-verifiers** make GLP's invariants statically checkable: phase ordering HEAD<GUARD<BODY and **single-writer (SRSW)** discipline as dialect verifier obligations (`MLIR-GLP-DIALECT.md:46-48`) — SRSW and writer-MGU stop being runtime assumptions and become machine-checked IL well-formedness. That is the **analyzability/verification payoff**: the seam can *reject* a non-faithful program before any engine runs it.

M1 is **observable-outcomes-only**, not internal heap layout (`spec 034 line 27`, FR-009/SC-005). So lowering IL→bytecode→Gleam immutable threaded store is faithful iff the lowered engine reproduces the observable bar: three-valued writer-MGU (FR-007), writer-only binding never W↔W (FR-004), role-from-tag (FR-002), self-bind→`Unbound` not `Cycle` (`heap.gleam:165-175`), suspension-forwarding to the terminal writer (FR-008, `heap.gleam:251-278`). The IL's `suspend_reactivate` is the explicit carrier of that suspension/reactivation contract, so every back-end inherits it from one definition rather than re-deriving it (the source of the two live F4 review bugs).

## 4. Running on Gleam/AtomVM

MLIR is C++ infrastructure and **cannot execute on AtomVM**, so the final IL→ML lowering is a back-end step done one of two ways (owner fork): **(i)** AOT in a host toolchain (MLIR lowers GLP-IL→v2.16.3 bytecode; the device loads bytecode); or **(ii)** a thin **pure-Gleam lowering/interpreter** for the 4-op IL — feasible precisely because the dialect is tiny and the shipped F4 kernel already implements the four primitives' semantics (`heap.gleam`/`unify.gleam`). Either way the executed form is sequential BEAM code: "a WAM-style bytecode interpreter is plain sequential BEAM code (fine for AtomVM); only the spawn primitive needs the raw form" (`dossier line 134`), with no `gleam_otp` (FR-010) and the AtomVM-portable immutable store (`heap.gleam:69-71`).

## 5. Multi-target reach

One IL, many lowerings — the documented Mercury/LingoDB win (`W3 Option B`): **GLP-IL → v2.16.3 bytecode** (Gleam/AtomVM now; Dart/C# parity oracle reuse the same lowering target); **GLP-IL → LLVM/C++** as a *gated, optional* native accelerator for ground/deterministic post-commit kernels (`llvm-feasibility.md:212-237`). Analysis/optimization (guard simplification, indexing decision-graphs, dead-clause elimination) run **once on the IL** and benefit every target, instead of per-codegen.

## 6. Owner Q1–Q6, from this design

- **Q1.** Logic systems split source→AST→abstract-machine; an IL above the machine is the Mercury/LingoDB choice when multiple backends + cross-target analysis are wanted (`W3`). This design takes that path.
- **Q2.** Yes, direct compile is *possible* (the v2.16.3 ISA is WAM/FCP-shaped, `G1`) — but this design deliberately does **not**, because a bare ISA seam gives no shared verification/optimization/multi-target layer.
- **Q3.** Stronger than the owner's hypothesis: the IL doesn't merely *help generate* the ML — it **is** the seam and the analysis layer, with the ML produced per-backend by lowering. The MLIR dialect makes writer/reader, three-phase, and SRSW first-class and verifiable.
- **Q4.** Clean separation: front-end owns grammar→AST→IL+verification; back-end owns lower+execute. Same IL in-process and over-the-wire (role-named, heap-independent); ANTLR grammar feeds it from any supported host.
- **Q5.** M1 preserved (4 ops ≅ GLP model; outcomes-only parity; SRSW/writer-MGU verified in-IL). M2 preserved and *untouched* — IL is the deployment seam; the maGLP link carries globalized terms. Gleam/AtomVM honored via per-backend lowering to sequential bytecode.
- **Q6.** Committed-choice = `guard_test`→commit→`body_spawn` (no backtracking); three-phase = enforced op order; SRSW/two-cell = verifier-checked writer/reader operands; suspension/reactivation = `suspend_reactivate` carried into every lowering.

## 7. Honest costs and risks of THIS approach

1. **Conflicts with the on-disk net recommendation** "serialize the existing v2.16.3 ISA — do **not** invent a new ISA" (`research-programme.md:212-213`) and the LLVM **CONDITIONAL-NO** (`llvm-feasibility.md:214-220`). Reconciliation (load-bearing): use MLIR-**as-infrastructure** dialect — *not* LLVM IR — with **bytecode as the primary lowering** and **LLVM only as the gated optional path**, which the same scout explicitly leaves open (`llvm-feasibility.md:71-92,212-237`). It is still a **second artifact to build and maintain** (Option B), and Mercury's *deleted* Erlang backend shows maintained surfaces rot (`release-notes-22.01`).
2. **Mostly unbuilt.** Only a 4-op smoke spike PASSes (`RESULT.md`); the production dialect, TableGen, verifiers, and all lowerings are deferred (`MLIR-GLP-DIALECT.md:38-48,84-90`). This is the heaviest of the candidate seams.
3. **AtomVM gap.** MLIR can't run on-device; the device gets *lowered* bytecode (AOT) or a hand-written Gleam IL-interpreter — and the AtomVM binary codec is **not yet spiked** (`G4 §3`). Choosing fork (i) vs (ii) is an owner decision with a spike prerequisite.
4. **M2 byte-parity.** Serialized MLIR text byte-identical across Dart/C#/Gleam is harder than a fixed bytecode codec (`G4 §4`) — but this only touches the deployment seam, since IL never crosses the maGLP runtime link.

**Net:** maximal analyzability/verification/multi-target reach (SRSW + writer-MGU machine-checked; one optimizer; bytecode-now/LLVM-later), bought at the price of being the largest, least-built option whose final lowering to the AtomVM machine language is unavoidably a back-end step.