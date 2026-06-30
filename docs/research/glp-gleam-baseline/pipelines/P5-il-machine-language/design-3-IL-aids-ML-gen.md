# Seam Design — IL-Aids-Machine-Language-Generation (owner's Q3 hypothesis)

**Thesis.** The **v2.16.3 bytecode ISA is the seam** (the contract that crosses front↔back, in-process and over-the-wire). A logic-centric **GLP/FCP IL lives strictly inside the front-end** as a generation/verification/optimization layer that produces that bytecode *better* than today's syntax-directed emitter. The IL never crosses the boundary. This is exactly what BinProlog shipped — binarization is "a source-level transform that *produces* the ISA," a documented design not speculation ([arxiv 1102.1178](https://arxiv.org/pdf/1102.1178); W3 Option C).

## 1. What crosses the seam

**Artifact:** a serialized `BytecodeProgram` — the flat instruction list + derivable label table (`runner.dart:50-64`) — in the WAM/FCP-shaped v2.16.3 ISA (`docs/glp-bytecode-v216-complete.md`; "directly follows the Flat Concurrent Prolog Abstract Machine" `bytecode-v216:548`), plus a self-contained **result envelope** (`Status/Bindings/Error` resolved *server-side*, var-name→writer-id map, suspended-goal detail, captured output; G4 §1.3, INV-5 design-dossier line 416).

- **In-process (combined instance):** the compiler hands the engine an in-memory `BytecodeProgram` object; the envelope is a struct. Same shape as today (`codegen.dart:130`).
- **Over-the-wire (split front/back):** identical payload, framed via the byte-codec. Section 15 "Instruction Encoding" is currently **NOT IMPLEMENTED** (`bytecode-v216:1350-1352`); feature 029's `IlCodec` proved `BytecodeProgram`↔bytes round-trips (G4 dossier line 160). Two cleanups are mandatory: define the codec and **de-embed `Object?`/`rt.StructTerm` operands** that codegen currently inlines (`codegen.dart:652-653`; G1 §5).

**Distinct seam — the maGLP agent-link (M2):** inter-agent linked semantics do **not** carry bytecode. The over-the-wire unit there is a **globalized GLP term + global name** (`_w(p,i):=T↑` / `_r(p,i):=T↑`; GLP_IMPLEMENTATION.pdf Def 6.12-6.13, Def 6.25; G2 §3). Two seams, two payloads: compiled ML across front/back; globalized terms across agent/agent. Both must hit the **byte-identical Dart↔C# FrameCodec/Crc32 standard** (G4 §2.5).

## 2. ANTLR → AST → IL → ML; where the compiler lives

The **compiler lives in the front-end** (relocated from engine-internal `GlpEngine._compiler`, `glp_engine.cs:148`; "primarily a project/reference boundary change," 11-…:69-70). Pipeline:

```
.g4 grammar → ANTLR parser → adapt to AstNode hierarchy
   → partial-eval + type-check + analyze (existing)
   → GLP-IL (4-primitive dialect)  ← NEW phase
       · verify (phase order, single-writer)
       · optimize (clause indexing, guard simplification)
   → lower IL → v2.16.3 bytecode → codec → SEAM
```

The **IL is the 4-primitive GLP/FCP dialect** — `HEAD-unify`, `GUARD-test`, `BODY-spawn`, `suspend-reactivate` — mapping 1:1 onto three-phase + two-cell vars (`MLIR-GLP-DIALECT.md:22-31`), spike-PASS round-tripping through real MLIR (`RESULT.md:1-7`). It replaces today's direct annotated-AST→opcode walk (`codegen.dart:107-133`; "no separate intermediate language," G1 §2). **ANTLR:** no `.g4` exists today (`12-…:50`); ANTLR has **no BEAM target** ([antlr4/doc/targets.md](https://github.com/antlr/antlr4/blob/master/doc/targets.md)) — so the grammar generates a parser in a *supported* target (C#/Dart), giving a thin heterogeneous front-end emitting bytecode to the pure-Gleam engine (W4 Option A).

## 3. M1 + M2 preservation

The IL is the **enforcement point** for faithfulness *before* a single opcode is emitted — its named obligation is op-level verifiers asserting **phase ordering HEAD<GUARD<BODY and single-writer (SRSW) discipline** (`MLIR-GLP-DIALECT.md:46-48`). This is strictly *more* analyzable than syntax-directed emission, which can only check locally.

- **M1 is observable-outcomes-only** — three-valued verdict, dereferenced result, activation set; **internal heap layout free to differ** (spec line 27; FR-009/SC-005). The IL changes only *how bytecode is generated*, not what the engine observes, so M1 is untouched: committed-choice (`ClauseTry`/`Commit`/`NoMoreClauses`), writer-MGU (binds writers only, `heap.gleam:59-64`), self-bind→`Unbound` recognizer (`heap.gleam:165-175`), suspension forwarding to terminal writer (`heap.gleam:251-278`) all remain engine behaviours.
- **M2:** bounded at **outcome equivalence of globalized-term exchange** (G2 §3; Remark 3.35). The IL is irrelevant to M2 — it's pre-seam — so it cannot perturb linked parity; the agent-link payload stays terms, not IL.

## 4. Gleam / AtomVM

The decisive AtomVM win of *this* approach: **all compile-time machinery — IL, MLIR dependency, verifiers, optimization passes — stays off-device.** You ship the device only the compact bytecode + a **bytecode interpreter, which "is plain sequential BEAM code (fine for AtomVM); only the spawn primitive needs the raw form"** (G4 dossier line 134). No `gleam_otp` (spec FR-010); process-cells via raw `erlang:spawn` + Subjects. The engine keeps the shipped **immutable threaded store** (`heap.gleam:69-71`) — most AtomVM-portable. The IL never touches the constrained runtime. **Verification gap (flag, don't assert):** the ML byte-codec on AtomVM bit-syntax was not spiked in F1 (G4 §3) — needs its own spike before relying on ML-on-wire to a device.

## 5. Multi-target reach

The IL is where multi-target codegen pays off (Mercury keeps mode/determinism analysis **high in HLDS before any backend is chosen**, [mercurylang compiler_design](https://www.mercurylang.org/development/developers/compiler_design.html)). One verified IL → bytecode for **Gleam/BEAM now**; the same IL feeds the **C#/Dart parity oracle** (they execute the identical ISA, satisfying M1/M2; W4). **C++/LLVM later is gated** — `llvm-feasibility` is CONDITIONAL-NO: SSA "cannot represent a destructively-bound logic variable" (`llvm-feasibility.md:110-122`). So the GLP-IL is **MLIR-as-infrastructure (custom `glp` dialect, lowered to bytecode), never lowered through LLVM's SSA core** (`llvm-feasibility.md:71-92, 212-237`).

## 6. Owner Q1–Q6 (from this design)

- **Q1.** Logic languages split persistent compile-unit vs ephemeral run-unit (Logix **module/computation**, W2); the abstract-machine language is normally the seam, with any IL sitting *inside* the compiler for analysis (Mercury HLDS, W3).
- **Q2.** Yes, the ML *can* be the seam with no IL (G1 §3) — but this design adds an IL behind it.
- **Q3 (this design).** Insert the IL **inside the front-end** to verify/optimize/retarget; emit v2.16.3 bytecode as the **only** thing that crosses. BinProlog is the shipped precedent (W3 Option C).
- **Q4.** Clean split because the contract (bytecode + envelope) is **identical in-process and on-wire**; ANTLR `.g4` in a supported target feeds a thin heterogeneous front-end; engine does "no parsing/compilation" (design-dossier §1.1).
- **Q5.** M1 (observable, layout-free) and M2 (byte-identical wire) preserved — they live at the seam/agent-link, *below* the IL; AtomVM constraints met by keeping the IL off-device.
- **Q6.** Committed-choice/three-phase/SRSW/two-cell are **verified in the IL** (`MLIR-GLP-DIALECT.md:22-31,46-48`) then realized by the unchanged engine (suspension/reactivation `heap.gleam:251-278`).

## 7. Honest costs / risks

1. **Net-new layer over a working emitter.** Today's syntax-directed codegen already produces correct bytecode (G1 §2). The IL is justified *only* by analyzability/optimization/multi-target reach — not "better" in the abstract. If the front-end stays single-target Gleam with no optimization ambition, the IL is unearned overhead (W3 Option A is then preferable).
2. **MLIR realization fork (owner-decidable).** (a) IL as **real MLIR** (the spiked dialect) — free verifier/rewrite infra, but a heavy C++/Python dependency the spike ran only on WSL2 (`RESULT.md:22-46`), awkward inside a C#/Dart front-end. (b) IL as a **lightweight in-language IR** modeled on the 4 primitives — no MLIR dependency, but you hand-build verifiers/passes. Consequence: (a) maximizes reuse + analyzability; (b) minimizes dependency surface. *Owner decides.*
3. **Two codecs to byte-stabilize.** The ML codec (Section 15, absent) *and* the maGLP term codec both need Dart↔C# byte-parity (G4 §2.5); v1/v2 opcode split complicates it (risk 7).
4. **AtomVM codec unspiked** (G4 §3).
5. **ISA-as-wire fragility.** An unfrozen ISA is a brittle contract ([SWI roadmap: QLF "Sensitive to VM instruction numbering"](https://github.com/SWI-Prolog/roadmap/wiki/Machine-independent-QLF-files-and-states)) — the v2.16.3 ISA must be versioned/frozen before it crosses the wire.