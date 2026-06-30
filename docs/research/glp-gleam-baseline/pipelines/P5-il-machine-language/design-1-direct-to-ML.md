# DIRECT-TO-MACHINE-LANGUAGE Seam Design — the v2.16.3 ISA *is* the front/back interface

**Thesis.** GLP already owns a compact (~30 actively-emitted opcodes), WAM/FCP-shaped logic machine language — the v2.16.3 bytecode ISA (`docs/glp-bytecode-v216-complete.md`; opcodes in `opcodes.dart`/`opcodes_v2.dart`; emitted by `codegen.dart`; executed by `runner.dart`). The front-end already compiles the annotated AST *directly* to it with **no separate IL** (`compiler.dart:56-128`; `codegen.dart:107-133`). Make that compiled artifact the seam. This is sufficient and clean **on one condition: the ISA is frozen and versioned** (and the v1/v2 opcode duality resolved). The literature backs the route — BinWAM (`arXiv:1102.1178`) and SWI `.qlf` (`swi-prolog.org/pldoc/man?section=qlf`) both make a committed-choice/Prolog machine language the *only* interface above the VM, with no optimizer IL between AST and ISA.

## 1. What crosses the seam

The seam carries **two artifacts, both directions of one boundary** (compiler→engine, results back):

- **Forward: a `BytecodeProgram`** = flat instruction list + auto-derived `Map<LabelName,int>` label table (`runner.dart:50-64`). Each instruction is one of the ~30 active opcodes: HEAD (`GetVariable`/`GetValue`/`HeadConstant`/`HeadStructure` + `Push`/`Pop`/`UnifyStructure` nesting, "directly follows the FCP Abstract Machine" `bytecode-v216:548`), GUARD (`Ground`/`Known`/`GroundEqual`/`Otherwise`/`Guard`), BODY (`PutVariable`/`PutStructure`/`SetVariable`/`Spawn`), control (`ClauseTry`/`Commit`/`ClauseNext`/`NoMoreClauses`/`Proceed`), RPC (`Distribute`/`Transmit`). Mode is the per-instruction `isReader` bool (`opcodes_v2.dart:29,47,65`), not a bit-field.
- **Back: a heap-independent result envelope** — `ExecutionResult{Status, Bindings, Error}` (G4 §1.2) extended with the three computed-but-currently-dropped components (G4 §1.3): var-name→writer-id map, suspended-goal detail, captured output. **All three outcomes Success/Suspend/Fail** ride it.

**In-process (combined instance):** the live `BytecodeProgram` object passes by reference from front-end to `BytecodeRunner`; the envelope is built by resolving bindings **server-side** (INV-5) so the front-end never re-derefs engine heap. This kills "the seam's biggest leak" — today `Bindings` are live `VarRef`s into engine-owned heap (G4 §1.3 line 84).

**Over-the-wire (split/remote/peer):** the same two artifacts, serialized. The forward codec is the missing **Section 15** (`bytecode-v216:1350-1352`, "NOT IMPLEMENTED") — but feasibility is proven: feature 029's `GlpRuntime.IlCodec` round-trips `BytecodeProgram`↔bytes (45/45 gates, Lean `decode∘encode=id` sorry-free, per MEMORY 029). Two clean-ups are mandatory: **de-embed `Object?`/`StructTerm` operands** (`opcodes.dart:99,116,133`; `codegen.dart:652-653` embeds a runtime `StructTerm` inside `UnifyConstant`), and define one codec for **both** wire and persistence (SICStus `.po`==saved-state precedent, `research-programme.md:48-52`).

The crucial scope clarity: **this front/back seam is NOT the maGLP peer link.** Peer engines exchange *globalized terms* `T↑/T↓` and global names (Shapiro Def 6.12/6.13, `GLP_IMPLEMENTATION.pdf` p.17), never bytecode. That is an *internal back-end* protocol. The bytecode seam sits between compiler and one engine instance; M2 parity is achieved because every engine executes the *same* ISA (§3).

## 2. ANTLR grammar → AST → ML; compiler placement

**The compiler lives in the front-end** (G4 Opt 2): lexer→parser→AST→partial-eval→type-check→analyzer (register allocation into `varTable`)→`codegen`→`BytecodeProgram`. Codegen is a syntax-directed emitter over the *annotated AST* (`codegen.dart:107-133`); the `varTable` is a proto-IR but **not a distinct IL**. So the pipeline is literally `parser → AST → ML`, no IL stage.

ANTLR has **no BEAM/Gleam target** — exactly 10 targets, none Erlang (`github.com/antlr/antlr4/.../targets.md`). The genuine fork (owner-decidable):

- **(A) ANTLR in a supported target (C#/Dart) as a thin front-end process**, emitting serialized bytecode to the Gleam/AtomVM engine. Cheapest; reuses existing C#/Dart runtimes; heterogeneous front-end by construction. **Recommended.**
- **(B) Custom ANTLR Gleam target** — homogeneous BEAM stack, high build+maintenance cost (full runtime + version-lock churn).
- **(C) Keep hand-written recursive descent in Gleam** — no `.g4` artifact (none exists today, `12-antlr...:50,57); loses single-source-of-truth grammar.

A `.g4` (token vocab pinned at `token.cs:1-71`) is a single analyzable, multi-target source of truth; with (A) it generates the C#/Dart parser while the engine stays pure Gleam.

## 3. M1 + M2 preservation

**M1 is preserved because the ISA, not the codec, defines behaviour**, and the ISA already encodes the faithful model: three-phase HEAD/GUARD/BODY via `ClauseTry`→guards→`Commit` (`bytecode-v216:171-237`); two-cell writer/reader pairs with in-reader suspension records (`bytecode-v216:42-75`); writer-MGU/σ̂w and goal-set U as runtime context (`runner.dart:166-168`); committed choice via `ClauseTry/ClauseNext/NoMoreClauses` (no backtracking — there is nothing to trail). Faithfulness is **observable-outcomes-only** (deref result, three-valued verdict, activation set; spec 034 Clarification line 27), so each runtime's heap may differ. The shipped Gleam F4 kernel already realizes this (writer-only binding, suspend-not-fail on unbound reader, FCP self-bind→`Unbound`; `unify.gleam`, `heap.gleam`). **Alignment caveat:** "align with Shapiro/FCP" means *stay shaped like* the FCP machine (4 instruction categories ≈ HEAD/GUARD/BODY, `suspend`+activation-queue; US5222221A), **not** adopt it verbatim — FCP defines only a single-cell read-only reference and no writer-MGU (W2 §4); v2.16.3's two-cell extension is precisely what keeps M1.

**M2 is preserved** because all three runtimes execute the identical frozen ISA → identical madGLP transactions (Reduce/Send/Receive, Def 5.9) → outcome-equivalent globalized-term exchange. The byte-parity obligation lands on **the codec + envelope** (must be byte-identical Dart↔C#↔Gleam, mirroring `FrameCodec`/`Crc32` FR-060/061), not on the heap.

## 4. Gleam / AtomVM

A WAM-style bytecode interpreter is "plain sequential BEAM code (fine for AtomVM); only the spawn primitive needs the raw form" (F1 dossier line 134). Representation: `BytecodeProgram` as a Gleam type — a `List(Op)` of opcode variants + a label `Dict` — the wire codec decodes bytes→that list; the runner is a `case op { ... }` dispatch. `Spawn` uses raw `erlang:spawn`+`gleam_erlang` Subjects; **no `gleam_otp`/`proc_lib`** (FR-010 line 108). Sequential execution + heap-as-immutable-threaded-store (`heap.gleam:69-71`) is the most AtomVM-portable shape.

## 5. Multi-target reach

- **Gleam/BEAM + AtomVM now** — interpret the ISA (§4).
- **C#/Dart as the parity oracle** — both already execute this exact ISA (`runner.dart`, `GlpRuntime`); they are the byte-parity reference for the codec.
- **C++/LLVM later** — *lower the ISA*, don't replace the seam. `llvm-feasibility.md` verdict is CONDITIONAL-NO except as a gated downstream native accelerator for ground/post-commit numeric kernels (`:212-237`). The seam is unaffected; LLVM sits *below* the machine language, exactly as GNU-Prolog's mini-assembly sits below WAM (Diaz JFLP 2001).

## 6. Owner Q1–Q6, from this design

- **Q1.** Logic systems make the *abstract-machine language itself* the hand-off; an IL above it is the exception (BinWAM, SWI `.qlf`, KL1-B, CARMEL — all source→AST→ISA→runtime, W1/W2/W3).
- **Q2. Yes — directly, and that ISA is the seam.** glpnet already compiles AST→v2.16.3 with no IL; the FCP/CARMEL lineage proves a committed-choice ISA is tiny (CARMEL-2 = 29 instr) and directly targetable. Align *in shape* with FCP, keep v2.16.3's two-cell GLP correction.
- **Q3.** Not needed for the seam. A logic-centric IL would only be a **front-end-internal generation aid** (BinProlog binarization precedent, `arXiv:1102.1178`); the ML still crosses. Deferrable until optimization/analysis ambition grows.
- **Q4.** Compiler in the front-end (Opt 2) → thin/heterogeneous clients; same `BytecodeProgram`+envelope in-process and on-wire; ANTLR `.g4` in a supported target (fork A) feeds it.
- **Q5.** Yes — M1 (ISA executes the faithful model; outcomes-only parity), M2 (same ISA → same transactions; byte-parity on codec), Gleam/AtomVM (sequential interpreter, raw spawn).
- **Q6.** Committed-choice/three-phase/SRSW/suspension/two-cell are all *already in the ISA semantics* (`bytecode-v216:42-75,171-237`); the seam transmits compiled clauses, the engine runs them unchanged.

## 7. Costs / risks (honest)

1. **Section 15 codec is unbuilt** for this runtime; needs building + operand de-embedding (029 proves feasibility, not done here for v2.16.3).
2. **ISA must be frozen/versioned** — SWI warns `.qlf` is "Sensitive to VM instruction numbering"; an unstabilised ISA is a fragile contract.
3. **v1/v2 opcode split** complicates byte-parity (G4 risk 7).
4. **AtomVM binary/bit-syntax for the codec is unspiked** (G4 §3 gap) — needs its own spike before reliance.
5. **No shared analysis/optimization layer** — if multi-backend optimization ambition arrives, this design must grow a front-end IL (the Q3 path), which it deliberately defers.

**Forks for the owner:** (a) ANTLR target placement A/B/C (§2); (b) freeze-and-version the ISA + resolve v1/v2 (precondition for cleanliness); (c) whether to add a front-end-internal IL later (Q3) — not required by this seam.