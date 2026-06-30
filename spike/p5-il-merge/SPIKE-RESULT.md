# P5 Verification Spike — Logic-IL + Verifiers → v2.16.3 bytecode (merge/3)

**Branch:** `036-glp-gleam-baseline-program` · **Date:** 2026-06-26
**Owner question under test (Q3, DOSSIER.md:13):** does a front-end-internal
logic IL with verifiers help generate *faithful* v2.16.3 bytecode, **verifiably**?

**Verdict: YES — fully demonstrated, end-to-end, both phases PASS.** A
dependency-free 4-primitive IL with two real verifiers lowers `merge/3` clause 1
to bytecode that is **disassembly-identical** to the stock `CodeGenerator` and
**execution-equivalent** on the real `glp_runtime` runner (Suspend, not Fail, on
an unbound reader; reactivate + commit on bind), and the verifiers **catch a
real SRSW violation** (and a phase-order violation) before emission.

All spike code is under `spike/p5-il-merge/`. `glp_runtime/` and `programs/`
were **not modified** (verified via `git status`); the front-end + runtime are
reused read-only via a path dependency. `dart analyze lib bin` → *No issues found*.

---

## How to reproduce

```
cd spike/p5-il-merge
C:/Users/gavri/dart-sdk/bin/dart.exe pub get
C:/Users/gavri/dart-sdk/bin/dart.exe run bin/phase_a.dart    # Phase A (no ANTLR)
# regenerate the ANTLR parser (already vendored under lib/antlr_gen/):
#   cd grammar && antlr4 -Dlanguage=Dart merge.g4
C:/Users/gavri/dart-sdk/bin/dart.exe run bin/phase_b.dart    # Phase B (ANTLR)
```

Toolchain used: Dart SDK 3.10.1; ANTLR 4.13.2 (antlr4-tools wrapper, Java 17);
`antlr4` pub package 4.13.2.

---

## Grounding reads (file:line)

- **Spike spec** — `docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/DOSSIER.md:50-60`
  ("Proposed verifiable SPIKE": ANTLR→IL→bytecode execution-equivalence + verifier firing).
- **Ratified architecture** — `.../P5-il-machine-language/DECISIONS.md:7-24`
  (Fork A=a1 keep v2.16.3; Fork B=b2 front-end-internal 4-primitive IL; placement
  `analyze → 4-primitive logic-IL (+verifiers) → v2.16.3 bytecode`, DECISIONS.md:28-30)
  and `DECISIONS.md:36-37` (b2 sub-fork = **lightweight in-language IR**, verifiers
  are "simple structural checks").
- **The 4 IL primitives** — `docs/research/repl-engine-separation/reconciliation/MLIR-GLP-DIALECT.md:22-31`
  (`head_unify / guard_test / body_spawn / suspend_reactivate`, each with precise
  GLP semantics) and `:46-48` (op-verifiers = the #11 obligation: phase order +
  single-writer discipline).
- **v2.16.3 ISA / opcodes** — `glp_runtime/lib/bytecode/opcodes.dart` (HEAD/COMMIT/BODY
  families: `HeadStructure:122`, `Commit:13`, `Spawn:317`, `NoMoreClauses:29`,
  `Proceed:44`) and `glp_runtime/lib/bytecode/opcodes_v2.dart` (unified
  reader/writer ops with `isReader`: `GetVariable:44`, `UnifyVariable:84`,
  `PutVariable:105`).
- **Reference emitter matched** — `glp_runtime/lib/compiler/codegen.dart`
  (`_generateProcedure:135`, `_generateClause:190`, `_generateHeadArgument:252`,
  `_generateStructureElement:321`, `_generateBody:461`, `_generatePutArgument:537`).
- **Register assignment** — `glp_runtime/lib/compiler/analyzer.dart:823-831`
  (`_assignRegisters`: first-occurrence insertion order, base name).
- **Programmatic run path mirrored** — `glp_runtime/lib/engine/glp_engine.dart:485-558`
  (`_runSingleGoal`) + `_setupArgument:919`; scheduler status enum
  `glp_runtime/lib/runtime/scheduler.dart:7-21` (`ExecutionStatus`, `DrainResult`).
- **GLP faithfulness** — `docs/glp-cheat-sheet.md:9` (outputs built in HEADS, not
  `=`/`:=` in body), `:78-81` (SRSW), `:238` (three-valued guards).

## The REAL merge/3 — clause 1 used VERBATIM

Source of truth: `programs/paper/merge.glp:6,8` (identical in
`programs/tests/typed/merge_standalone.glp:3-4` and
`programs/typed_book/streams/producers_consumers/merge_simple.glp:8,10`):

```prolog
procedure merge(Stream(X)?, Stream(X)?, Stream(X)).
merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).
```

> NOTE: the DOSSIER's illustration `merge([X|Xs?], Ys?, Zs) :- Zs := [X|Zs1?], ...`
> (DOSSIER.md:32) uses `:=` **in the body**, which contradicts the cheat-sheet rule
> "outputs are constructed in CLAUSE HEADS, never via `=` in the body"
> (glp-cheat-sheet.md:9). Per the spike instructions I used the **repo** clause
> verbatim, where the output stream `[X?|Zs?]` is constructed in the **head** (arg 3).

**SRSW (confirmed satisfied), 1 writer + 1 reader each:**
- `X`: writer in `[X|Xs]` (head arg0) / reader `X?` in `[X?|Zs?]` (head arg2).
- `Xs`: writer in head arg0 / reader `Xs?` in body.
- `Ys`: writer in head arg1 / reader `Ys?` in body.
- `Zs`: reader `Zs?` in head arg2 / writer `Zs` in body.

Analyzer register assignment (real output): `X→X0, Xs→X1, Ys→X2, Zs→X3`.

---

## PHASE A (core — no ANTLR)

### A1 — AST via glp_runtime's own front-end (read-only). **PASS**
Lexer→Parser→Analyzer produce `head = merge([X|Xs], Ys, [X?|Zs?])`,
`body = [merge(Ys?, Xs?, Zs)]`, registers `X0..X3` as above.

### A2 — AST → 4-primitive IL. **PASS**
```
IlClause merge/3 #0
  [head] head_unify(A0, [X|Xs])
  [head] head_unify(A1, Ys)
  [head] head_unify(A2, [X?|Zs?])
  [body] body_spawn(merge(Ys?, Xs?, Zs))
  suspend_reactivate(readers=X,Zs, structMatch=true)
```
(`guard_test` count = 0; clause 1 has no guard.) The four primitives are defined
as plain Dart data classes in `lib/il.dart`.

### A3 — verifiers fire correctly. **PASS**
- **Faithful IL:** `V1 phase-order : PASS`, `V2 SRSW : PASS`.
- **Mutated #1 (SRSW)** — body hand-mutated to `merge(Ys?, Ys?, Zs)`:
  ```
  V2 SRSW : FAIL:
      - writer "Xs" occurs but its paired reader "Xs?" is absent
      - reader "Ys?" occurs 2 times (must be exactly 1)
  ```
- **Mutated #2 (phase order)** — `body_spawn` placed before `head_unify`:
  ```
  V1 phase-order : FAIL:
      - op #1 head_unify(A0, [X|Xs]) is in phase "head" but a later phase "body"
        already appeared — HEAD<GUARD<BODY violated   (+2 more)
  ```
The SRSW mutant is constructed at the **IL level** so the demonstration isolates
the *IL verifier* (the #11 obligation), independent of the analyzer's own SRSW
pass. This is the load-bearing result: the analyzability win is **real, not paper**.

### A4 — IL → v2.16.3 BytecodeProgram. **PASS**
17 ops, `labels = {merge/3: 0, merge/3_end: 15}`. Lowering reuses the glp_runtime
opcode classes and mirrors `CodeGenerator` (`lib/lowering.dart`).

### A5 — disassembly diff vs `CodeGenerator.generateWithMetadata`. **PASS (IDENTICAL)**
Both renderings (one field-level disassembler applied to both programs):
```
PC  0: Label("merge/3")              PC  9: Commit
PC  1: ClauseTry                     PC 10: PutVariable(X2, A0, reader)
PC  2: HeadStructure(".", 2, A0)     PC 11: PutVariable(X1, A1, reader)
PC  3: UnifyVariable(X0, writer)     PC 12: PutVariable(X3, A2, writer)
PC  4: UnifyVariable(X1, writer)     PC 13: Spawn("merge/3", 3)
PC  5: GetVariable(X2, A1, writer)   PC 14: Proceed
PC  6: HeadStructure(".", 2, A2)     PC 15: Label("merge/3_end")
PC  7: UnifyVariable(X0, reader)     PC 16: NoMoreClauses
PC  8: UnifyVariable(X3, reader)
```
**No differences** — byte-for-byte the same op sequence (cross-checked against the
stock `BytecodeProgram.toDisassembly()` in the probe). The IL lowering reads the
*same* analyzer-assigned registers `CodeGenerator` reads, so identity is exact
rather than merely "equivalent up to ordering".

### A6 — execution-equivalence on the real runner. **PASS**
Goal `merge(As?, Bs?, Cs)` with `As?`, `Bs?` unbound readers, `Cs` a writer, run
on `glp_runtime`'s `BytecodeRunner` + `Scheduler` (path mirrors
`GlpEngine._runSingleGoal`). Real output, **identical for both programs**:
```
### stock-codegen program ###            ### IL-derived program ###
  drain#1 status        : suspended        drain#1 status        : suspended
  suspended goals       : [merge(X1?, X2?, Cs)]   (same)
  blocked on input As?  : true             blocked on input As?  : true
  bind As?=[a|As1?] then re-drain:
  drain#2 status        : suspended        drain#2 status        : suspended
  Cs binding            : [a | X16]        Cs binding            : [a | X16]
```
- **Suspend, NOT Fail** on the unbound reader — the canonical GLP correctness
  point (matching `[X|Xs]` against an unbound reader suspends). Both programs
  block on the goal's `As?` reader.
- **Reactivate + commit on bind:** after `As? := [a|As1?]` the suspended goal
  wakes, commits clause 1, **constructs the output stream in the head** →
  `Cs = [a | X16]` (head `a`, fresh reader tail), and spawns the recursive
  `merge(Bs?, As1?, Zs)`.

> **Honest caveat on `drain#2 status = suspended` (not `succeeded`):** the spike
> isolates **clause 1 only**, so the spawned recursive call `merge(Bs?, …)`
> immediately suspends on the unbound `Bs?`. This is the *correct* GLP outcome and
> is **identical** for both programs; the reactivate-and-commit is evidenced by the
> head-constructed `Cs = [a | …]` binding, not by a terminal success.

---

## PHASE B (ANTLR de-risk)

### B1 — `merge.g4` authored. **PASS**
`grammar/merge.g4` — a minimal **combined** grammar (lexer + parser in one `.g4`)
covering exactly clause 1's tokens: `ATOM` (functor/constant), `VAR`, the reader
marker `?` (`QUESTION`), list cons `[ H | T ]`, `:-` (`NECK`), `,` (`COMMA`), the
guard separator `|` (`BAR` — the *same lexeme* as the list-cons bar; a guarded
clause `head :- guards | body .` reuses this token), compound terms `f(...)`, and
the `.` clause end (`DOT`). Styled after `Csharp/qhxm/grammar/{QhxmLexer,QhxmParser}.g4`.
Generated with `antlr4 -Dlanguage=Dart merge.g4` → `mergeLexer/mergeParser/
mergeListener/mergeBaseListener.dart` (vendored under `lib/antlr_gen/`).

### B2 — parse tree → glp_runtime `AstNode`. **PASS**
`lib/antlr_adapter.dart` drives the generated parser
(`InputStream.fromString` → `mergeLexer` → `CommonTokenStream` → `mergeParser`,
`BailErrorStrategy` for loud failure) and walks the tree into the **same**
`Program/Procedure/Clause/Atom/Goal/VarTerm/ListTerm/StructTerm/ConstTerm` nodes
used in Phase A. **Structural-agreement gate vs the production glp parser:**
```
ANTLR-built head: merge([X|Xs], Ys, [X?|Zs?])    glp-parser head: (same)  -> head=true
ANTLR-built body: [merge(Ys?, Xs?, Zs)]          glp-parser body: (same)  -> body=true
```
Registers from analyzing the ANTLR AST: `X0, X1, X2, X3` (identical).

### B3 — re-run A2→A6 from the ANTLR AST. **PASS**
- IL identical to Phase A; `V1 PASS`, `V2 PASS`.
- **A5(B): bytecode IDENTICAL** to stock `CodeGenerator` (same 17-op listing).
- **A6(B): execution-equivalent** — `drain#1 suspended`, blocked on `As?`,
  after bind `Cs = [a | X16]`.

---

## Pass-bar scorecard

| Item | Bar | Result |
|---|---|---|
| **A3** | verifiers PASS on faithful, FAIL on mutated SRSW (and phase-order) | **PASS** — real failure messages shown |
| **A5** | two disassemblies equivalent (identical or justified) | **PASS** — byte-identical |
| **A6** | both Suspend (not Fail) on unbound reader; both reactivate+commit on bind | **PASS** — identical real runner output |
| **B**  | ANTLR parser generated, adapts to `AstNode`, reproduces the A-result | **PASS** — structural agreement + identical bytecode + equivalent execution |

**No BLOCKERS.**

---

## Scope, method honesty, and what this does NOT prove

- **Single clause, by design** (spike spec DOSSIER.md:54). The other 3 merge
  clauses, guards, and nested-structure args are supported by the lowering code
  paths (mirrored from `codegen.dart`) but only clause 1 is exercised end-to-end.
- **IL placement is faithful to the ratified design:** the IL consumes the
  *analyzer-annotated* AST (registers + SRSW already computed), exactly where
  DECISIONS.md:28-30 puts it (`analyze → IL → bytecode`). Because the IL reads the
  same register table `CodeGenerator` reads, bytecode identity is exact.
- **`suspend_reactivate` emits no opcode.** In the v2.16.3 ISA suspension is the
  three-valued runtime behaviour of the HEAD ops against an unbound reader,
  finalized by the procedure-trailing `NoMoreClauses` (Si≠∅ ⇒ suspend). The IL
  keeps it as explicit, analyzable metadata (its lowering obligation per
  MLIR-GLP-DIALECT.md:44 is "reader-wait + wake-list wiring", realized by those
  ops). This is documented, not hand-waved.
- **Out of scope (per spike spec, not attempted):** the Section-15 byte codec,
  AtomVM bit-syntax decode, and the Gleam port. Those gate Fork C/c1 and are a
  separate spike (DOSSIER.md:60, DECISIONS.md Obligations).

## Files

```
spike/p5-il-merge/
  pubspec.yaml                 path dep on ../../glp_runtime + antlr4 ^4.13.2
  grammar/merge.g4             minimal combined ANTLR grammar (B1)
  lib/il.dart                  4-primitive IL data classes + var-occurrence model
  lib/verifiers.dart           V1 phase-order, V2 single-writer/SRSW
  lib/lowering.dart            AST→IL, IL→v2.16.3 bytecode, field-level disassembler
  lib/exec.dart                execution-equivalence harness (runner+scheduler)
  lib/antlr_adapter.dart       ANTLR parse tree → glp_runtime AstNode (B2)
  lib/antlr_gen/               vendored ANTLR-generated Dart parser
  bin/probe.dart               empirical ground-truth dump of stock codegen
  bin/phase_a.dart             A1–A6 driver
  bin/phase_b.dart             B1–B3 driver (re-runs A2–A6 from ANTLR AST)
```
