# G1 — Current GLP Machine Language & Compiler Path

## (1) What the machine language IS, and how compact

GLP already has a complete logic abstract-machine language: the **v2.16.3 bytecode ISA** (`docs/glp-bytecode-v216-complete.md`). It is normative and explicitly self-describes as WAM/FCP-shaped.

**Concrete opcode set** lives in two files:
- `opcodes_v2.dart` — the 7 "unified" variable opcodes, the heart of the writer/reader model: `HeadVariable`, `GetVariable`, `GetValue`, `UnifyVariable`, `PutVariable`, `SetVariable` (each carrying an `isReader` flag, `opcodes_v2.dart:27-133`), plus `Unknown` (`:142`).
- `opcodes.dart` — ~53 base `Op` classes (`opcodes.dart:4`): control (`ClauseTry`, `Commit`, `ClauseNext`, `NoMoreClauses`, `Proceed` `:11-44`), HEAD (`HeadConstant`, `HeadStructure`, `HeadNil`, `HeadList`, `UnifyConstant`, `UnifyVoid` `:114-155`), structure nesting (`Push`, `Pop`, `UnifyStructure` `:182-212`, documented "Following FCP AM" `bytecode-v216:548`), GUARD (`Guard`, `Ground`, `Known`, `NoReaders`, `GroundEqual`, `Otherwise` `:177-272`), BODY (`PutStructure`, `SetConstant`, `PutBoundConst`, `PutBoundNil` `:60-109`), control flow (`Spawn`, `Requeue`, `Allocate`, `Deallocate` `:317-340`), and module RPC (`Distribute`, `Transmit` `:358-383`, "Following FCP rpc.cp:164-175").

So ~60 classes total, but many are explicitly **deprecated/unused**: `UnionSiAndGoto`/`ResetAndGoto` are `@deprecated` (`opcodes.dart:32,37`); `try_next_clause` is "IMPLEMENTED but UNUSED" (`bytecode-v216:182`); the `*Arg` slot variants and `HeadBindWriter`/`GuardNeedReader` are legacy (`opcodes.dart:275-307`). The **actually-emitted** working set (per codegen, below) is **~30 opcodes** — genuinely compact, comparable to CARMEL-2's 29.

**Variable model** is FCP's two-cell writer/reader pair with shared single-shot suspension records (`bytecode-v216:42-75`): writer cell + reader cell, suspension lists stored *in* reader cells. **Mode is NOT a bit-field** — writer/reader is the per-instruction `isReader` bool (`opcodes_v2.dart:29,47,65`); READ/WRITE structure-traversal mode is a runtime register (`UnifyMode mode` `runner.dart:172`) set by `HeadStructure`/`PutStructure`, not encoded in the opcode. Three-phase HEAD/GUARD/BODY is enforced by `ClauseTry`→guards→`Commit` (`bytecode-v216:171-237`); **σ̂w** (tentative writer bindings) and **U** (goal suspension set) are runtime context fields (`runner.dart:166-168`), discarded/accumulated per clause. Clause selection is committed-choice via `ClauseTry`/`ClauseNext`/`NoMoreClauses` with κ as the reactivation entry PC (`bytecode-v216:175-196, 1441-1446`).

## (2) How the front-end produces it

Pipeline in `compiler.dart:56-128`: lexer → `parser.parseModule()` → `Program` AST → `PartialEvaluator.transformDefinedGuards` → optional `checkModule` type-check → `Analyzer.analyze` (register allocation into `varTable`, reduce/`_select` generation) → `CodeGenerator.generateWithMetadata` → `BytecodeProgram`. **There is no separate intermediate language**: codegen walks the *annotated AST* directly and emits opcodes (`codegen.dart:107-133`). The `AnnotatedProgram`/`varTable` is the closest thing to an IR, but it is just an annotated AST, not a distinct IL.

Codegen is a straightforward syntax-directed emitter: head args → `GetVariable`/`GetValue` (first vs subsequent occurrence, `codegen.dart:266-273`); constants/lists/structs → `HeadConstant`/`HeadNil`/`HeadStructure` (`:275-313`); nested structures → `Push`/`UnifyStructure`/`Pop`/`UnifyVariable` (`:345-385`, the FCP-AM nesting protocol); guards → dedicated `Ground`/`Known`/`GroundEqual`/`Otherwise` or generic `Guard` (`:393-459`); body goals → `PutVariable`/`PutStructure`/`SetVariable` then `Spawn` (`:461-575`); RPC → `Distribute`/`Transmit` (`:503-535`). Output is `BytecodeProgram(ctx.instructions)` (`codegen.dart:130`).

## (3) What the runner consumes — could THIS be the seam?

The runner consumes a `BytecodeProgram` = `List<dynamic> ops` + auto-indexed `Map<LabelName,int> labels` (`runner.dart:50-64`). Execution is one giant `if (op is X)` dispatch chain (~60 branches, `runner.dart:442-4096`).

The **compiled-code artifact (`BytecodeProgram`) is a strong seam candidate**: it is a flat instruction list with a derivable label table, cleanly separated from the front-end (codegen returns it; the runner only reads it). It is exactly the "machine-language-as-seam" the owner asks about in Q2 — the front-end already compiles *directly* to it with no IL.

## (4) Is it WAM/FCP-shaped?

Yes, unambiguously. The doc cites WAM A/X/Y registers, S register, READ/WRITE modes, get/put/unify/set families, `allocate`/`deallocate` frames (`bytecode-v216:122-167, 632-635`), and FCP for the two-cell variable model, `Push`/`Pop`/`unify_structure` nesting ("directly follows the Flat Concurrent Prolog Abstract Machine" `:548`), `commit` semantics ("FCP emulate.h do_commit1 lines 217-258" `:204`), and import/RPC convention (`:500-501`). It is a WAM skeleton specialized to FCP committed-choice + writer/reader.

## (5) Serializable / clean enough to be a wire contract?

**Partially — code yes, state no, and no codec exists today.**

- The **opcode operands** are mostly serializable scalars (ints, functor strings, bools). BUT some carry arbitrary embedded Dart objects: `value` fields are `Object?` (`opcodes.dart:99,116,133`), and codegen can embed a runtime `rt.StructTerm` *inside* a `UnifyConstant` operand (`codegen.dart:652-653`). That entangles a few operands with in-process term objects.
- **Section 15 "Instruction Encoding" is `NOT IMPLEMENTED`** (`bytecode-v216:1350-1352`): "the current Dart implementation represents instructions as Dart class instances … not as byte-encoded binary." There is **no defined wire format** for the bytecode today. (The separate C# `GlpRuntime.IlCodec` / 029 spike was a *new* effort precisely because this is absent.)
- The **machine STATE** is deeply entangled with in-process Dart objects: `CallEnv` holds heterogeneous `Term` (`VarRef`/`ConstTerm`/`StructTerm`, `runner.dart:108-114`); `RunnerContext` holds `sigmaHat`, `Si`, `U`, `clauseVars`, `parentStack`, `E`/`CP` (`runner.dart:161-193`); heap/ROQ/GQ live in `GlpRuntime`. None of this is serializable as-is.

**Bottom line for the seam debate:** the *compiled program* is a compact (~30 active opcodes), WAM/FCP-shaped logic machine language that the front-end already targets directly with no IL — a clean **in-process** seam. To make it an **over-the-wire** seam (Gleam/AtomVM, Dart↔C# parity) requires defining the missing binary codec (Section 15) and de-embedding `Object?`/`StructTerm` operands; the runtime *state* would additionally need a serialization model it currently lacks.