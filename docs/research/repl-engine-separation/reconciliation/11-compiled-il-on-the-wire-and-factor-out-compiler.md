# Seed Reconciliation Memo — #11 compiled-il-on-the-wire-and-factor-out-compiler

**Feature ID:** `compiled-il-on-the-wire-and-factor-out-compiler`
**Dossier entry:** §11 #11 · Kind: FOLLOW-UP
**Date:** 2026-06-09
**Branch:** `026-engine-review-dossier`
**Methodology:** `reconciliation/SEED-RECONCILIATION-BRIEF.md`

---

## Dossier cross-references

| §-anchor | Subject |
|---|---|
| §9.1 | Premise reconciliation: compiler location — as-built vs requirement assumption |
| §2.4 | How runtime "generated IL" (ModuleTerm-wrapped BytecodeProgram) crosses; same codec required both directions |
| §2.1 | BytecodeProgram structure — heterogeneous IReadOnlyList<object>, v1 IOp + v2 IOpV2, ground-constant optimization |
| §2.2 | What an IL codec must capture: ordered Instructions, both opcode families, Label markers, VariableMap, ModuleTerm-embedded programs |
| §3 | Wire-reuse decision — FrameCodec/TcpTransport reused; two dedicated net-new codecs, one per Kind byte |
| §0.4 | Classification table row "Compiler relocation (front-end IL)" — class: refactor (large) |
| §10.1 | Open fork: compiler location (Opt 1 vs Opt 2); advisory: Opt 1 MVP, Opt 2 follow-up |
| §10.10 | §2a ANTLR4 shared grammar — deferred, depends on #11 |
| §12 risk 7 | Cross-runtime byte-parity for v1/v2 opcode split |

Back-reference in dossier Appendix B row #11.

---

## Seed-vs-dossier-vs-code

### Roadmap brief (as stored)

The stored profile (via `buildkit-roadmap brief`) reads:

> FOLLOW-UP. Move the compiler to the front-end/standalone; wire carries compiled IL (codec from #4) both directions incl. ModuleTerm IL. Large refactor; enables ANTLR4 (§2a). depends-on: #4,#6. (§7 #11)

**Discrepancy from dossier §11 entry:** the brief note references "§7 #11" (likely a legacy numbering artifact) — the canonical §-anchor is §9.1 + §2.4. The WSJF=2/RICE=1875 scores are stored; no problem/value/effort profile fields are populated (problem, target-user, value, risk all null). This is consistent with a FOLLOW-UP feature captured at minimal profile depth.

### Dossier scope verification (§9.1, §2.4, §2.1, §2.2)

**Scope as stated in §11 #11:** "Move the compiler to the front-end/standalone; wire carries compiled IL (codec from #4) both directions incl. `ModuleTerm` IL."

**Dossier §9.1 confirms (premise reconciliation, file:line):**
- `glp_engine.cs:349` — `RunGoalAsync(string goalText)` takes a raw goal **string**.
- `glp_engine.cs:487-493` — `_RunSingleGoalAsync` parses + compiles the goal string **inside the engine** (Lexer → Parser → Parse/ParseModule; confirmed in `compiler.cs:49-52`).
- `glp_engine.cs:251` — `LoadSource(string source, ...)` takes source text; the full Lexer/Parser/TypeChecker/PartialEvaluator/Compiler pipeline runs engine-side.
- `glp_engine.cs:534` — `new BytecodeRunner(program)` receives the compiled `BytecodeProgram`; this is constructed at `glp_engine.cs:534` (confirmed).
- `compiler.cs:29-46` — `GlpCompiler` is a standalone class in `GlpRuntime.Compiler` namespace with injected Lexer/Parser/Analyzer/CodeGenerator factories. It is **already factored from the engine logic** — it is a separate class — but it lives in the engine-side library (`out/csharp/lib/compiler/`) and is **instantiated by `GlpEngine` at `glp_engine.cs:148`** (`private readonly GlpCompiler _compiler = new GlpCompiler()`).

**The dossier scope is correct and the code confirms it:** the compiler is logically separate but physically co-located with and owned by the engine. Moving it to the front-end is a **real refactor** of where the instance lives and what the public contract of the engine becomes (the engine would need to accept `BytecodeProgram` directly instead of source strings).

**ModuleTerm IL (§2.4, file:line):**
- `out/csharp/lib/runtime/terms.cs:146-161` — `ModuleTerm : Term` wraps `object Bytecode` (untyped to avoid circular import) + `string Name`. Confirmed by code.
- `out/csharp/lib/runtime/glp_activation.cs:88` — `new ModuleTerm(moduleBytecode, name: moduleName)` creates the heap-side module term.
- `out/csharp/lib/runtime/body_kernels.cs:1032` — `if (bytecode is not BytecodeProgram bp)` — the `_activate` kernel casts the `ModuleTerm.Bytecode` back to `BytecodeProgram` at runtime.

This confirms that a `BytecodeProgram` **is a heap term** when wrapped as a `ModuleTerm`. Any result binding containing a `ModuleTerm` will therefore require the IL codec (§3-A / seed #4) to encode/decode the embedded `BytecodeProgram` — bidirectional IL-on-wire.

**BytecodeProgram structure (§2.1, file:line):**
- `out/csharp/lib/bytecode/runner.cs:41-73` — `BytecodeProgram.Instructions` is `IReadOnlyList<object>` (heterogeneous). Confirmed.
- `out/csharp/lib/bytecode/opcodes.cs:1-15` — v1 `IOp` marker interface, ~50 concrete classes.
- `out/csharp/lib/bytecode/opcodes_v2.cs:1-17` — v2 `IOpV2` marker interface; unified reader/writer with `IsReader` bool (`HeadVariable`, `GetVariable`, `GetValue`, etc.).
- `out/csharp/lib/compiler/codegen.cs:737-759` — ground list optimization: `ConvertListToStructTerm` embeds a runtime `Rt.StructTerm` as `UnifyConstant.Value`, confirmed at `codegen.cs:758-759`. This means constant-term operands can be **recursive `StructTerm` objects**, not scalar-only.
- `out/csharp/lib/compiler/result.cs:9` — `CompilationResult.VariableMap` is `Dictionary<string, long>` (variable name → register index).

**Zero serialization exists (§2.1, confirmed):**
Grep of `out/csharp/lib/bytecode/` confirms no `Serialize`, `Encode`, or `ToBytes` methods anywhere in `opcodes.cs`, `opcodes_v2.cs`, or `runner.cs`. `BytecodeProgram.ToDisassembly()` at `runner.cs:88` is human-readable text, not a wire format.

**Additional finding — compiler namespace already extracted:**
`GlpCompiler` (`out/csharp/lib/compiler/compiler.cs:29`) is already in its own namespace `GlpRuntime.Compiler`, separate from `GlpRuntime.Engine`. The `glp_engine.cs:28` `using GlpRuntime.Compiler;` import is the only coupling. Moving the compiler to the front-end/standalone is therefore primarily a **project/reference boundary change**, not a code rewrite: the engine's `RunGoalAsync` and `LoadSource` must be given new overloads accepting `BytecodeProgram` directly (or the existing source-text entry points must be removed/deprecated), and `GlpCompiler` + its transitive dependencies (`Lexer`, `Parser`, `Analyzer`, `CodeGenerator`, `PartialEvaluator`, `TypeCheckerDriver`) must move to the front-end project's reference graph.

**Additional finding — VariableMap not used after compilation:**
`CompilationResult.VariableMap` (`result.cs:9`) is produced by `CompileWithMetadata` but **not surfaced in `ExecutionResult`** (which has only `Status/Bindings/Error` at `glp_engine.cs:51-80`). When the compiler moves to the front-end, the `VariableMap` is the mechanism by which the front-end maps query variable names to writer register indices — it must cross the wire as part of the compiled-goal request frame (client→engine), or the engine must compute its own writer-name tracking from the IL. This is an underspecification in §11 #11.

**Additional finding — `_RunConjunctionAsync` also parses/compiles engine-side:**
`glp_engine.cs:621-637` — conjunctions (goals with commas) are wrapped into a `_conj_wrapper_` clause and re-parsed/compiled engine-side. Moving the compiler to the front-end must handle conjunction-wrapping **client-side**, not just simple goals.

**Dart mirror parity (§2.5, file:line):**
- `glp_runtime/lib/engine/glp_engine.dart:34-37` — `ExecutionResult` structurally identical to C#.
- `csharp/glp_link/reliability/FrameCodec.cs:31-32` — "Dart mirror is byte-identical (FR-060/061)". The Dart `GlpCompiler` at `glp_runtime/lib/compiler/compiler.dart` (Dart source of conversion) also holds the compiler engine-side. Any ANTLR4 single-grammar shared front-end (#12) must reconcile with the existing Dart compiler pipeline too.

---

## Classification check

**Kind: FOLLOW-UP** — correct. This is explicitly a post-MVP refactor that depends on both the IL codec spike (#4) and the MVP process-split (#6). It does not ship new runtime behavior; it relocates where compilation happens and what the engine's wire contract accepts.

**Does the code support the scope?**

Yes, with important nuances:
1. The compiler is logically a separate class (`GlpCompiler` at `compiler.cs:29`) — the refactor is a project/reference boundary change, not a code rewrite. This makes the scope *smaller than "large refactor" might imply* for the pure compiler-move part.
2. However, every engine entry point that accepts strings must gain a `BytecodeProgram` overload: `RunGoalAsync` (`glp_engine.cs:349`), `LoadSource` (`glp_engine.cs:251`), `LoadProject` (`glp_engine.cs:328`), and conjunction wrapping (`glp_engine.cs:621`). This is genuine interface surface change.
3. The `ModuleTerm`-in-binding path (`terms.cs:146`, `glp_activation.cs:88`) requires the IL codec from #4 to be integrated into the result envelope (#5) — confirming the `depends_on: [4, 6]` is correct. But §11 records `depends_on: 4, 6` while §9.1 also implies a dependency on #5 (result codec) for the reverse direction (engine→client carrying `ModuleTerm` bindings). The roadmap brief omits #5 from the depends_on list.

**Classification table §0.4** records this row as `refactor (large)`. The code confirms it is a refactor but the "large" qualifier primarily applies to the **interface surface** changes and the wire-format design (which overloads does the engine accept? what does the request frame carry?), not to the compiler code itself.

---

## Tensions

### T1 — depends_on list is incomplete: #5 (result codec) is a missing dep

**Evidence:** §11 #11 records `depends_on: 4, 6`. Seed #4 provides the IL codec; #6 provides the MVP split. But for compiled-IL-on-wire to work **both directions** (including `ModuleTerm` IL in result bindings, §2.4), the result codec (#5) must already encode/decode `ModuleTerm`-wrapped `BytecodeProgram`. Feature #5 depends on #2 and #3; those are already in the dep chain via #6, but #5 itself is not listed. Absent #5, the "IL in result bindings" half of the scope cannot ship.

**Options:**
1. Add `depends_on: [4, 5, 6]` to the stored profile — reflects the real dependency; the roadmap brief note implies it but does not record it formally.
2. Split the scope: "compiler relocation + client→engine IL" is `depends_on: [4, 6]`; "ModuleTerm-in-result-binding" is a further follow-up that depends on #5 as well.
3. Keep as-is and treat the #5 dependency as implicit (assumed from #6 which depends on #5) — but this obscures the direct dependency.

### T2 — VariableMap must cross the wire but is not in scope

**Evidence:** `CompilationResult.VariableMap` (`result.cs:9`) maps variable name→register index. When the compiler moves to the front-end, the `VariableMap` produced by client-side compilation must be sent to the engine alongside the `BytecodeProgram` so the engine can build its `queryVarWriters` mapping (`glp_engine.cs:515`). Without it, the engine cannot map query variable names to heap writer addresses for the result envelope. This coupling is not described in §11 #11 or §9.1.

**Options:**
1. Extend the client→engine request frame (built on the IL codec from #4) to carry both the `BytecodeProgram` and `VariableMap` — the natural solution; requires the IL codec spec to include `VariableMap` serialization (already listed in §2.2 as a codec obligation, so this is within #4's scope but the cross-seed coupling is unrecorded).
2. Have the engine recompute the writer-name mapping from the `BytecodeProgram` itself at execution time — possible if the variable names are embedded in the IL (they are not currently; register indices are integers without names in the emitted instructions).
3. Keep the engine responsible for source-text parsing in a transitional hybrid: the front-end compiles for optimization/distribution but the engine retains a source-path as a fallback.

### T3 — conjunction wrapping is engine-internal and not in scope

**Evidence:** `glp_engine.cs:621-637` — conjunction goals are wrapped `_conj_wrapper_ :- {trimmed}.` and re-parsed inside the engine. When the compiler moves to the front-end, the front-end must handle this wrapping and compile the conjunction as a `BytecodeProgram`. This is a behavior change in the REPL UX: what the user types and what gets compiled must be coordinated client-side. The dossier §9.1 and §11 #11 do not address this case.

**Options:**
1. Move conjunction detection and wrapping to the front-end alongside the compiler — the correct long-term solution; straightforward to implement.
2. Define a new `RunConjunctionAsync(BytecodeProgram)` engine entry point that accepts pre-compiled conjunctions; the conjunction-wrapping is part of the front-end compilation protocol.
3. Deprecate direct conjunction syntax in goals, requiring callers to structure goals as single-functor calls — more restrictive UX change.

---

## Under-specifications

### U1 — What is the new engine public contract when the compiler is removed?

**Why it matters:** The engine's current public API is source-string-based (`RunGoalAsync(string)`, `LoadSource(string, string?)`, `LoadProject(string, string?)`). After relocation, the new API must accept `BytecodeProgram` directly. The exact overload shape (do the old string methods stay as convenience wrappers? is the engine a pure executor?) determines backward compatibility and the wire frame format.

**Options:**
- A: Engine gains `RunGoalAsync(BytecodeProgram, Dictionary<string,int> varMap)` overloads; old string overloads are deprecated but kept (transitional period).
- B: Engine drops string overloads entirely; callers must compile before calling.
- C: Engine gains a "compiler plugin" injection point (a `Func<string, (BytecodeProgram, VariableMap)>?` hook), defaulting to the internal compiler for backward compat during migration.

### U2 — How does `self.glp` loading work when the compiler is front-end?

**Why it matters:** `GlpEngine` ctor at `glp_engine.cs:202-217` reads `self.glp`, compiles it, and stores it as `_loadedPrograms["__root_self__"]`. If the compiler is front-end only, the engine boot sequence must receive a pre-compiled `self.glp` `BytecodeProgram` — either shipped as a compiled artifact or compiled by the front-end on first start. This affects cold-start latency, distribution packaging, and the bootstrap protocol.

**Options:**
- A: Ship a pre-compiled `self.glp.il` artifact alongside the engine binary; engine loads it at boot without a compiler.
- B: The front-end compiles `self.glp` at startup and sends it to the engine as part of the initialization handshake over the wire.
- C: The engine retains a minimal "bootstrap-only" compiler path for `self.glp` specifically; all user-supplied goals are compiled front-end.

### U3 — How does `_MadPredicatesSource` (embedded constant) work front-end?

**Why it matters:** `GlpEngine._MadPredicatesSource` (`glp_engine.cs:143-144`) is a hardcoded source string embedded in the engine that is compiled at `EnableMadGlp` time (`glp_engine.cs:401`). After compiler relocation this must either be a pre-compiled artifact or the front-end must know to compile it when enabling madGLP mode. The coupling between engine feature flags (EnableMadGlp) and compilation is unaddressed.

**Options:**
- A: Pre-compile madGLP predicates to a `BytecodeProgram` and embed the binary constant in the engine (or load from file).
- B: Move the `EnableMadGlp` compilation step to the front-end; the engine receives the compiled madGLP program via the wire.
- C: Keep the compiler in the engine for embedded-source compilation only (a small residual, not a full front-end compiler).

---

## GEPA/DSPy refinement

### Applicability

**`methodological`** — this is a C# systems refactor (moving a compiler pipeline across a project/reference boundary + defining a new wire frame format for compiled IL). GEPA/DSPy cannot directly optimize C# class files. However, the *discipline* of GEPA/DSPy applies: iterating candidate interface designs against the metric combination (does the engine accept `BytecodeProgram`? does a round-trip compile→encode→send→decode→execute produce identical results?), using the REPL test suite as the primary pragmatic signal and the IL codec's byte-parity + round-trip identity as the formal signal. The `codeconv-codegen-opt` precedent (GEPA as offline optimizer with metric thresholds) is the model.

### Seed definition

Relocate `GlpCompiler` (and its transitive compiler dependencies: `Lexer`, `Parser`, `Analyzer`, `CodeGenerator`, `PartialEvaluator`, `TypeCheckerDriver`) from the engine-side library (`out/csharp/lib/compiler/`) to the front-end/standalone client project. Define and implement the client→engine request frame as the IL codec output (building on #4's `BytecodeProgram`↔bytes codec) carrying `BytecodeProgram` + `VariableMap`. The engine gains `BytecodeProgram`-accepting entry points. The wire carries compiled IL both directions: client→engine (goal/program IL) and engine→client (result bindings containing `ModuleTerm`-wrapped IL, via the result codec from #5).

### Metrics combination

| Name | Kind | Tool | Threshold |
|---|---|---|---|
| REPL test suite pass rate | pragmatic | `bash test/run_all_tests.sh` | 384/384 (zero regression from baseline) |
| Round-trip compile→encode→decode→execute equivalence | pragmatic | extend feature-020 `EquivTrace` harness: compile source → encode BytecodeProgram → decode → run → compare OUT records | 100% of existing REPL test corpus: decoded-and-run result ≡ direct-compile result |
| Engine-source-string path removed / deprecated | pragmatic | grep: no `RunGoalAsync(string)` calls from front-end after migration | 0 unmitigated caller sites (all migrated to `RunGoalAsync(BytecodeProgram, ...)`) |
| IL codec byte-parity (FR-060/061) | formal | byte-by-byte comparison of C# encode vs Dart encode on identical `BytecodeProgram` inputs | 100% parity across the opcode test corpus |
| Round-trip identity `decode(encode(p)) ≡ p` | formal | property-based test (FsCheck / xUnit) on synthesized `BytecodeProgram` instances | 100% over a generated corpus covering v1 IOp, v2 IOpV2, Label, recursive StructTerm constants |
| ModuleTerm IL round-trip | formal | encode a `ModuleTerm`-wrapped `BytecodeProgram` via the result codec; decode; verify `_activate` executes correctly | the `ModuleTerm` execution path in `body_kernels.cs:1032` yields identical trace |
| SRSW validity preserved in compiled output | formal | type-checker/SRSW gate applied to decoded program before execution | 0 SRSW violations introduced by encode→decode→re-execute path |

### Interactive spec step

At the start of `/buildkit-specify` for this seed, the owner confirms:
1. The new engine public contract (which string overloads are kept, which are removed — U1 options A/B/C).
2. The `self.glp` boot protocol under compiler relocation (U2 options A/B/C).
3. Whether `VariableMap` is part of the request frame or recomputed by the engine (T2 options 1/2/3).
4. The formal metric tools: byte-parity test harness (FsCheck property-based, or a seeded-corpus comparison), and the IL verification approach (MLIR dialect vs direct Lean 4 proof of round-trip identity).
5. Whether the Dart mirror must be kept in sync with the C# refactor (§2.5 parity constraint) — gating the scope of #12 (ANTLR4 spike).

### Refinement loop

Seed → candidate API design (engine overloads + wire frame schema) → evaluate against metric combination (round-trip equivalence + SRSW gate + REPL suite) → GEPA reflective mutation of the frame schema (e.g., adjust VariableMap encoding, handle conjunction-wrapping) → repeat until thresholds + roadmap-sequence fit hold. Each iteration is Claude-run via Agent-tool seams; no external API calls.

---

## Formal tooling

### Lean 4 evaluation

**Fit:** Good for proving the round-trip identity property `decode(encode(p)) ≡ p` as a mechanized theorem over the opcode discriminant format. Lean 4's dependent type system can encode the heterogeneous `Instructions : List Op` where `Op = IOp | IOpV2 | Label`, and a round-trip proof can be structured as a structural induction over the discriminant encoding. Lean 4 + Lean-LSP-MCP gives Claude a tactic-generation loop with immediate compiler feedback (no API needed). The `VariableMap` serialization (string→long bijection) is straightforward to prove in Lean 4.

**Weakness:** The v1/v2 opcode split means the `Op` type has two distinct interfaces; encoding a mixed-instruction list requires a sum type, which is expressible in Lean 4 but verbose. The ground-constant `StructTerm` recursive embedding (codegen.cs:737-759) requires a recursive term encoding lemma.

### Rocq evaluation

**Fit:** Strong fit for verified-compiler-style proofs in the WAM/TWAM lineage (see TWAM: certified abstract machine for logic programs, arXiv:1801.00471). The Rocq ecosystem has direct precedents for "compiled-exec ≡ source-interp" proofs (the verified Prolog→WAM compiler, ScienceDirect 0743-1066/92/90054-7). The v1/v2 heterogeneous instruction list maps naturally to a Rocq inductive type. The `StructTerm` recursive embedding is expressible via a well-founded recursion.

**Weakness:** Rocq's tactic language (Ltac/Ltac2) is less immediately accessible for Claude's tactic-generation loop than Lean 4's term-mode proof + Lean-LSP-MCP; AutoRocq has a GPT-4 dependency that must be adapted away per the no-API rule.

### Primary: `lean4`

Lean 4 is the primary choice. The round-trip identity `decode(encode(p)) ≡ p` over the heterogeneous opcode list is the decisive formal property here; Lean 4's tactic loop via Lean-LSP-MCP is the most Claude-native path. The `VariableMap` string→long bijection proof is straightforward.

**Alternative when:** Rocq is the alternative if the team chooses to build a verified-compiler-style proof in the TWAM/WAM-verification lineage (proving "decoded-and-executed ≡ direct-compiled" as a bisimulation, not just round-trip identity). This is a deeper formal property than round-trip identity alone and Rocq has stronger prior art for it.

### IL verification

This seed is the consumer of the IL codec produced by seed #4. The IL verification plan for #11 therefore layers on top of #4's codec:

- **MLIR-dialect layer:** the GLP/FCP dialect (`HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate` primitives, per the owner-specified MLIR hierarchy in BRIEF §3.2) is defined during #4. Seed #11 adds the **client-side compiler lowering pass**: the front-end lowers GLP source → GLP/FCP dialect IR → opcode stream, and this lowering pass is the target of the First-Class Verification Dialects approach (PLDI'25). The dialect's semantics are verifiable by construction.
- **Byte-contract formal metric:** byte-parity proofs (FR-060/061): `encode_C#(p)` = `encode_Dart(p)` byte-for-byte on identical `BytecodeProgram` inputs; round-trip identity `decode(encode(p)) ≡ p` (Lean 4 theorem); `ModuleTerm`-embedded program round-trip: the `BytecodeProgram` inside a `ModuleTerm` survives encode→decode and `_activate` produces identical execution traces.
- **Citation to pin:** the Typed-Multi-level-Datalog-IR citation (`2502.06854`) is flagged as mis-attributed in the brief (§6 open item); pin the correct reference during the #4/#11 spike.

---

## Shapiro criteria preserved

This seed (compiler relocation + IL-on-wire) must preserve the following Shapiro/GLP design criteria, framed for the embedded-switch purpose:

1. **Committed-choice concurrency:** the engine's execution model must not change when the wire carries compiled IL instead of source text. Goals compiled client-side and sent to the engine must execute under identical committed-choice semantics (one clause committed per reduction; no backtracking).

2. **SRSW (Single-Reader/Single-Writer):** the SRSW validity of a compiled `BytecodeProgram` is established at compile time (by the type-checker/SRSW gate). After encode→decode, the decoded program must preserve SRSW — i.e., the codec must be a bijection that does not introduce aliased variable indices. The formal SRSW-gate metric above enforces this.

3. **Suspension correctness:** when a goal compiled front-end suspends on an unbound reader, the engine's suspension machinery (reader-varId → blocked goals, `scheduler.cs:58-91`) must fire correctly — indistinguishable from a locally-compiled suspension. This requires the `VariableMap` (writer/reader register indices) to survive the wire faithfully.

4. **Monotone variable binding:** the engine's monotone heap (write-once binding, `heap_fcp.cs`) must not be compromised by a pre-compiled goal that has incorrect register indices (due to a faulty codec). The IL codec's round-trip identity proof (Lean 4) is the formal guarantee.

5. **Three-valued unification (Success/Suspend/Fail):** the HEAD/GUARD/BODY phases operate on the decoded `BytecodeProgram`; the codec must preserve the phase boundaries (HEAD opcodes before GUARD opcodes before BODY opcodes in the instruction stream). The MLIR-dialect lowering pass makes phase boundaries first-class, enforcing this structurally.

**Embedded-switch framing:** in the embedded GLP engine acting as a connectivity switch, the compiler relocation means that QHSM/HSM actors and classical OS-task clients compile their goals locally (on the client machine or in the actor's context) and send compiled IL to the switch. The switch evaluates the IL, routes results (bindings, suspension state) back. The Shapiro criteria must hold for this distributed execution model exactly as they do for the in-process model.

---

## Recommendation

Proceed with this FOLLOW-UP feature after seeds #4 (IL codec spike), #5 (result codec), and #6 (MVP process split) are complete. The dossier scope is **accurate** and the code confirms it. Two issues should be resolved before `/buildkit-specify`:

1. **Add #5 to `depends_on`** in the roadmap profile — the `ModuleTerm`-in-binding direction cannot ship without the result codec from #5.
2. **Resolve U1 (new engine public contract) and T2 (VariableMap crossing)** interactively at the spec step — these are the two underspecifications most likely to reshape the wire frame design.

The refactor risk is lower than "large" implies because `GlpCompiler` is already a standalone class in its own namespace; the main effort is (a) defining the new wire-frame format for client→engine compiled requests, (b) adding `BytecodeProgram`-accepting engine overloads, and (c) handling the embedded-source bootstrap cases (`self.glp`, madGLP predicates, conjunction wrapping).

---

## Options for owner

| Label | Consequence |
|---|---|
| A — ship after #4+#5+#6, update depends_on to include #5 | Correct dependency tracking; slight delay vs. the current brief's #4+#6-only deps |
| B — split scope: compiler-relocation only (depends_on: #4, #6), ModuleTerm-in-result deferred | Smaller first increment; ModuleTerm-in-binding becomes a separate follow-up (e.g., #11b); more manageable but adds a feature to the roadmap |
| C — retain source-text path as engine fallback indefinitely, don't remove it | Avoids bootstrap/madGLP/conjunction complexity at the cost of a permanently dual API; wire still carries source text for fallback clients |

---

## Open questions

1. Should the engine retain a source-text fallback path (for bootstrap and embedded predicates) after the compiler is "relocated," or is the intent a hard cut where the engine becomes a pure executor? (Informs U1/U2/U3.)
2. Is `VariableMap` part of the request frame format (alongside `BytecodeProgram`) or recomputed by the engine from the IL? (Informs T2; affects the codec spec in #4.)
3. Does the Dart mirror (`glp_runtime/lib/compiler/compiler.dart`) also need to be relocated to a Dart front-end project, or is the Dart path out of scope for this seed? (Informs §2.5 parity obligation and the scope of #12.)
4. Is the ANTLR4 spike (#12) a prerequisite for the wire format design, or can #11 proceed with the existing Dart/C# compiler grammar independently? (The ANTLR4 spike depends on #11 per §11 #12, so this is really a question of whether #11 should wait for a grammar decision.)
5. Pin the correct citation for the Typed-Multi-level-Datalog-IR (the `2502.06854` link is mis-attributed per BRIEF §6) during the #4/#11 spike.

---

## External refs

- Dossier §9.1 (compiler-location premise reconciliation): `docs/research/repl-engine-separation/design-dossier.md` §9.1
- Dossier §2.4 (ModuleTerm IL crossing): ibid. §2.4
- Dossier §2.1–§2.2 (BytecodeProgram structure + IL codec obligations): ibid. §2.1, §2.2
- Dossier §0.4 classification table row "Compiler relocation": ibid. §0.4
- TWAM: certifying abstract machine for logic programs: https://arxiv.org/pdf/1801.00471
- Verified Prolog→WAM compiler (compiled-exec ≡ source-interp): https://www.sciencedirect.com/science/article/pii/0743106692900547
- First-Class Verification Dialects for MLIR (PLDI'25): https://users.cs.utah.edu/~regehr/papers/pldi25.pdf
- APOLLO — model-agnostic agentic Lean proving: https://arxiv.org/abs/2505.05758
- Lean-LSP-MCP / Lean Copilot (Claude-native tactic generation)
- AutoRocq (adapt off GPT-4 dependency): https://github.com/NUS-Program-Verification/AutoRocq
- `SEED-RECONCILIATION-BRIEF.md` §3.2a (formal-tooling matrix + no-API resolution)
