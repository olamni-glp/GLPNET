# 036 — Front/Back Seam Architecture Decisions

**Ruled by owner (Gabi), 2026-06-26.** Grounding: `DOSSIER.md` (this dir).

## Ratified

- **Fork A = a1 — Machine language = the v2.16.3 bytecode ISA, KEPT.** Freeze + version it
  (resolve the v1/v2 opcode split; stabilize before it crosses any boundary). It is the
  GLP-correct heir of Shapiro's FCP Sequential Abstract Machine — the two-cell writer/reader
  extension over FCP's single-cell read-only reference is exactly what keeps M1 faithful
  (writer-MGU). Do **not** adopt Shapiro's machine verbatim (it lacks writer-MGU).

- **Fork B = b2 — A logic-centric 4-primitive IL lives FRONT-END-INTERNAL.** Primitives:
  `head_unify` / `guard_test` / `body_spawn` / `suspend_reactivate`. Role = a codegen +
  verification aid that makes **SRSW / phase-order (HEAD<GUARD<BODY) / writer-MGU**
  machine-checkable *before* bytecode emission (closes the gap behind the two live F4 review
  bugs) and hosts shared optimization + multi-target codegen. **The IL never crosses the wire;
  only the bytecode does.**

- **Fork C = c1 — compiler in front-end, compiled-ML-on-wire (owner-confirmed 2026-06-26).**
  The compiler relocates to the **front-end**; the seam carries **serialized bytecode
  (compiled-ML-on-wire) + a server-resolved, heap-independent result envelope**, identical
  in-process and over-the-wire. This **is** the clean front/back separation. Obligates the
  Section-15 byte codec + an AtomVM bit-syntax decode spike (see Obligations).

## Resulting architecture

`ANTLR-defined grammar (parser generated in a supported target — C#/Dart; engine stays pure
Gleam) → AST → partial-eval → type-check → analyze → 4-primitive logic-IL (+ verifiers) →
v2.16.3 bytecode → [binary codec] → engine`. The engine does no parsing/compilation. The
**maGLP agent-link (M2) is a SEPARATE term-level seam** (globalized GLP term + global name,
`_w(p,i):=T↑`), NOT bytecode — two seams, two payloads.

## Resolved sub-decisions (owner, 2026-06-26)

- **b2 sub-fork = lightweight in-language IR** on the 4 primitives (dependency-free; verifiers are
  simple structural checks; keeps the front-end portable C#/Dart/Gleam). Real-MLIR dialect
  **deferred** — revisit only if an LLVM/C++ backend is greenlit.
- **Spike = RUN NOW, standalone de-risk** — the `merge/3` ANTLR→IL→bytecode execution-equivalence
  + verifier-firing spike (no codec, no MLIR-on-device, no Gleam port).

## Obligations created (not yet built — track into Full-Gleam epic)

1. **Section-15 bytecode binary codec** — none exists today; de-embed `Object?`/`StructTerm`
   operands; Dart↔C#↔Gleam byte-parity; **+ an AtomVM bit-syntax decode spike** (do NOT cite the
   C# `029 IlCodec` as proof for the Dart/Gleam path).
2. **The IL op-verifiers** (phase-order, SRSW, writer-MGU) — the `#11` obligation; only a 4-op
   MLIR round-trip smoke has passed so far.
3. **Freeze + version the v2.16.3 ISA** (resolve v1/v2 opcode split) before it crosses.
4. **M2 parity ≠ ISA-identity** — parity is the term protocol + byte-identical codec; keep distinct.

## Verification — SPIKE PASS (2026-06-26)

The ratified seam (lightweight front-end IL + verifiers → v2.16.3 bytecode) is **empirically
verified** by `spike/p5-il-merge/` (`SPIKE-RESULT.md`), production tree untouched (`git status`
clean):
- **Byte-identical** bytecode: the 4-primitive IL lowering of `merge/3` cl.1 (real clause
  `programs/paper/merge.glp:8` `merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).`) produces a
  17-op `BytecodeProgram` **identical** to stock `CodeGenerator` (field-level diff = 0).
- **Execution-equivalent** on the real runner: both Suspend (not Fail) on an unbound reader, then
  reactivate + commit on bind → `Cs=[a|…]` (output built in the HEAD). Identical for both.
- **Verifiers fire**: V2 (SRSW) FAILs a mutated `merge(Ys?, Ys?, Zs)` body ("reader Ys? occurs 2×";
  "writer Xs paired reader absent"); V1 (phase-order) FAILs a body-before-head reorder — the
  analyzability win is real, not paper. (Q3 = confirmed.)
- **ANTLR phase**: `merge.g4` → generated Dart parser → adapter → same AST → identical bytecode +
  equivalent execution (ANTLR4 has no BEAM target → parser in C#/Dart, engine pure-Gleam, as designed).
- **Honest scope:** single clause; `suspend_reactivate` emits no opcode (suspension = HEAD
  three-valued semantics + trailing `NoMoreClauses`, kept as analyzable metadata); codec/AtomVM/Gleam
  out of scope (separate spike, gates Fork C obligations).
- **Correction surfaced:** the DOSSIER's `merge` illustration used body `Zs := …`, which violates the
  cheat-sheet head-construction rule; the repo clause (head construction) is the faithful one.
