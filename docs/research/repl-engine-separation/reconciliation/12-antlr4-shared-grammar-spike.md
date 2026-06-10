# Seed Reconciliation Memo — #12 antlr4-shared-grammar-spike

**Feature id:** `antlr4-shared-grammar-spike`
**Dossier entry:** §11 #12
**Kind (dossier):** EXPERIMENT
**Date:** 2026-06-09
**Branch:** 026-engine-review-dossier

---

## Dossier cross-references

- §10.10 — Deferred research dimension §2a: "ANTLR4 shared grammar, C++ engine, two-tier shared/instance memory + cooperative run-to-completion, LLVM staging — deferred to EXPERIMENT features (#12, #14, #15, #16). Do not gate the MVP; framed there, not as forks here."
- §10.1 — Compiler-location fork Opt 2: "relocate compiler to front-end/standalone; wire carries compiled IL. Consequence: enables thin clients + the §2a ANTLR4 single-grammar vision. Trade-off: large refactor."
- §9.1 — Premise reconciliation: compiler is engine-internal (`glp_engine.cs:487-493,251`); #11 (compiled-IL-on-wire + factor-out-compiler) is the prerequisite for this experiment to be meaningful.
- §12 risk 7 — "Cross-runtime byte-parity for new codecs if the Dart mirror is kept — v1/v2 opcode split complicates a stable format"; the ANTLR4 spike (#12) "single-sources the grammar" (cited as mitigation).
- §2.5 — Cross-runtime parity caveat: `FrameCodec`/`Crc32` carry byte-parity remarks (FR-060/061); the Dart mirror `glp_runtime/lib/engine/glp_engine.dart:34-37` is byte-identical; new codecs must meet the same standard.
- §0.4 — Classification table: compiler relocation row is `refactor (large)` at `glp_engine.cs:487-493,:251`.
- Appendix B — Seed registry row #12: motivating §-anchor §10.10; reconciliation memo this file.
- SEED-RECONCILIATION-BRIEF.md §3.2 — grammar-as-example-verifier: "define the GLP grammar once in ANTLR4; use the generated parser as an example-coverage verifier — parse every working-definition example to prove the grammar accepts the language before any compiler exists."

---

## Seed-vs-dossier-vs-code

### Roadmap brief (stored profile)

The `buildkit-roadmap brief` output:

> EXPERIMENT. Define GLP grammar once in ANTLR4, generate C# (trial C++/Dart) parser front-ends, confirm same IL. Single-sources the language across C#/C++/Dart. depends-on: #11. (§7 #12)

Stored WSJF=2.4, RICE=640. Effort=M. No problem/need/risk fields filled in the roadmap profile.

### Dossier §11 entry

> `antlr4-shared-grammar-spike | EXPERIMENT | Define the GLP grammar once in ANTLR4; generate C# (trial C++/Dart) parser front-ends; confirm identical IL. | single-sources the language; verification spike. Underpins the formal grammar metric for all language-touching seeds. | depends_on: 11 | §ref: §10.10`

### Divergence: roadmap vs dossier

The roadmap brief says "depends-on: #11" but records it under "(§7 #12)" — a stale note referencing the original §11 numbering from the investigation (pre-dossier). The dossier's numbering at §11 #12 is authoritative. The "(§7 #12)" note in the roadmap should be "(§10.10, §11 #12)" when the profile is enriched.

The roadmap profile mentions the "formal metric" role only obliquely ("verification spike") and omits the dossier's explicit statement that this seed "underpins the formal grammar metric for all language-touching seeds." This is an under-specification gap in the stored roadmap profile, not a conflict.

### As-built code check

1. **Recursive-descent hand-written parsers — both runtimes, no ANTLR4**

   - C# parser: `out/csharp/lib/compiler/parser.cs:1-2` (header) — "Recursive-descent parser for GLP source code. Converted from Dart source: lib/compiler/parser.dart / source_sha256: d5b6f4a7c81d0dcfd0fb32be8b28f7da3d3b77dc84571a10f063188114b2e9eb". This is a hand-converted, hand-written recursive-descent parser. No ANTLR4 anywhere in `out/csharp/lib/compiler/`.
   - Dart parser: `glp_runtime/lib/compiler/parser.dart` — similarly a hand-written recursive-descent parser, class `Parser`, identical structure.
   - **Confirmed zero ANTLR4 grammar files**: no `.g4` or `.g` files anywhere in the repo.

2. **Token type set at `out/csharp/lib/compiler/token.cs:1-71`** — this is the *implicit* grammar's token vocabulary. It defines 40+ token types including GLP-specific ones (`READER` for `X?`, `GUARD_SEP` for `|`, `UNIV`/`UNIV_DECOMPOSE` for `=..`/`..=`, `AT_LESS`/`AT_GREATER`/`AT_LESS_EQUAL`/`AT_GREATER_EQUAL` for standard-order comparisons, `COLONCOLONEQ` for `::=`, `PROCEDURE` keyword, `TILDE` for `~`, `HASH` for `#`, `BACKSLASH` for `\`, `AT` for `@`). This is the concrete formal vocabulary that a `.g4` grammar must cover.

3. **Six-phase compiler pipeline at `out/csharp/lib/compiler/compiler.cs:50-90`** — phases: (1) Lexer, (2) Parser→Module, (2.4) PartialEvaluator, (2.5) TypeCheckerDriver.CheckModule, (3) Analyzer, (4) CodeGenerator. An ANTLR4 grammar would replace phases 1+2 only; the pipeline is otherwise preserved.

4. **Compiler is engine-internal** — `GlpEngine._compiler` field at `out/csharp/lib/engine/glp_engine.cs:148` instantiates `new GlpCompiler()` directly. The engine calls `_compiler.CompileWithMetadata(source)` at `:487-493`. There is no front-end-accessible compiler; it lives entirely inside the engine library. An ANTLR4 spike must either (a) be a standalone prototype, or (b) wait for #11 to relocate the compiler first.

5. **Dart mirror parity** — `glp_runtime/lib/compiler/lexer.dart` and `out/csharp/lib/compiler/lexer.cs` are structurally identical (confirmed via `source_sha256` in the C# file header matching the Dart source). Both are hand-written. Any ANTLR4-generated parser would need to produce the same AST as both — raising the identical-IL confirmation task from the dossier.

6. **No C++ engine substrate** — the repo contains no C++ source at all. The C++ target mentioned in the dossier is a trial/aspirational target, not an as-built substrate. The C++ experiment (#14) is a separate follow-on seed that also depends on #12.

7. **IL parity baseline** — the mechanism for "confirm identical IL" is an execute-equivalence test: compile the same source text with the ANTLR4-generated parser and with the existing hand-written parser; run both through the shared `CodeGenerator`/`BytecodeRunner`; confirm identical `BytecodeProgram` instruction sequences. This relies on #4 (il-codec-spike) having established a bytecode serialization format for deterministic comparison. The dossier correctly records depends_on: 11 (not 4), but a practical identical-IL test also needs #4's round-trip foundation.

---

## Classification check

**Dossier kind: EXPERIMENT** — correct. This is a verification spike with no production deliverable mandated ("throwaway-or-keep" in the brief language used for #4). The grammar may eventually replace the hand-written parsers (PREP/REFACTOR), but the spike itself is exploratory and de-risking. File:line confirmation: no `.g4` grammar exists (`zero in repo`), the hand-written parsers at `out/csharp/lib/compiler/parser.cs:1` and `glp_runtime/lib/compiler/parser.dart:1` are the as-built baseline. The EXPERIMENT classification is correct and the scope (generate C# front-end, trial C++/Dart, confirm identical IL) is technically feasible given the code baseline — but the "identical IL" confirmation step has a hidden dependency on #4 that the dossier does not make explicit in the depends_on list.

---

## Tensions

### T1 — Depends-on #11 but #4 is also required for the IL-parity check

**Evidence:** The dossier records `depends_on: 11` (compiled-IL-on-wire + factor-out-compiler). But "confirm identical IL" — the spike's success criterion — requires a deterministic byte-level comparison of `BytecodeProgram` artifacts, which is exactly what #4 (il-codec-spike) establishes. Without a serialization format from #4, "identical IL" degrades to a run-time behavioral equivalence test (execute the same programs, compare results), which is weaker and does not catch opcode-ordering differences.

**Options:**
1. Add `4` to depends_on (blocking): this makes the dependency graph reflect reality; the spike cannot complete its stated goal without #4's serialization. Consequence: delays #12 until after both #4 and #11.
2. Redefine "identical IL" as behavioral equivalence only (non-blocking on #4): run the same GLP programs through both parsers→engines, compare `ExecutionResult`. Weaker but sufficient to validate the grammar. Consequence: cannot detect silent opcode divergences, reducing the formal value of the spike.
3. Run the spike in two sub-phases: Phase-A (ANTLR4 grammar + parse-only, without IL check) gated only on #11; Phase-B (byte-level IL identity) gated on both #4 and #11. Consequence: cleanest separation, but increases planning overhead.

### T2 — Grammar-as-example-verifier role vs grammar-as-production-parser role

**Evidence:** The SEED-RECONCILIATION-BRIEF.md §3.2 defines the grammar-as-example-verifier role: "parse every working-definition example to prove the grammar accepts the language before any compiler exists." The dossier §11 #12 scopes the spike as "generate C# (trial C++/Dart) parser front-ends; confirm identical IL" — a different (production-parser) role. These two roles have different requirements: the verifier role needs only acceptance/rejection per example (no AST needed, ANTLR4 lexer+parser rules suffice); the production role needs full AST construction, visitor/listener generation in multiple targets, and downstream compatibility with `CodeGenerator`.

**Options:**
1. Scope the spike as verifier-first, production second: deliver grammar-as-verifier (parse all examples from `programs/` and type-manual) in one phase; then add production-parser generation as a separate deliverable. Consequence: the formal metric value arrives earlier.
2. Scope the spike as production-parser first: generate a C# ANTLR4 parser that produces the same AST as `out/csharp/lib/compiler/parser.cs`, confirm IL identity. Consequence: the formal metric value is implicit in the IL check rather than an explicit example-coverage gate.
3. Explicitly separate into two features: `antlr4-grammar-verifier` (lightweight, only depends on #1a) and `antlr4-multi-target-parser` (depends on #11). Consequence: the verifier feature could run early, decoupled from the compiler-relocation work.

### T3 — C++ target is aspirational, not grounded in any as-built substrate

**Evidence:** The dossier says "generate C# (trial C++/Dart) parser front-ends." The C++ target has no corresponding runtime in the repo — the C++ engine feasibility is a separate seed (#14) that itself depends on #12. The ANTLR4 C++ target exists in principle (ANTLR4 does support it), but the generated C++ parser would have nowhere to plug in without the C++ engine (#14). Trialing C++ at this stage produces an artifact with no test harness.

**Options:**
1. Drop C++ from #12 scope; defer to #14: the C++ ANTLR4 parser is only meaningful when a C++ engine exists to run IL against. Consequence: #12 becomes C# + trial Dart only, which is fully grounded in existing runtimes.
2. Keep C++ as a "build only" validation: generate the C++ parser, verify it compiles cleanly with ANTLR4 C++ runtime, but don't run it against an engine. Consequence: confirms portability without requiring the C++ engine; lower value than full IL check.
3. Keep C++ as an explicit out-of-scope note in the spec: mark it `deferred until #14` in the feature spec but keep the mention in the dossier as intent. Consequence: no change to the roadmap; just documents the limitation.

---

## Under-specifications

### U1 — Grammar-as-verifier corpus not defined

**Why it matters:** The BRIEF §3.2 mandates "parse every working-definition example to prove the grammar accepts the language." But the corpus — which `.glp` files, which manual examples — is not specified. `programs/self.glp` is the bootstrap prelude; `programs/tests/`, `programs/book/` contain test programs; the typed manual has inline examples. The grammar's acceptance of all of these is necessary for the verifier role to be meaningful.

**Options:**
1. Use the unified REPL test suite as corpus: every `.glp` file that must load under `test/run_all_tests.sh` is a required acceptance case. This is fully defined, mechanically checkable, and tied to a living regression baseline.
2. Use `programs/self.glp` + `programs/tests/typed/` as the minimal typed corpus: these are the type-annotated programs most likely to stress GLP-specific syntax (readers, SRSW, type declarations). A smaller but more tightly scoped corpus.
3. Define a separate grammar-test corpus of smallest-possible programs covering each token type and grammar rule: a grammar-unit-test approach, independent of the existing REPL test suite. Most rigorous but requires authoring new test programs.

### U2 — AST compatibility and downstream pipeline integration not specified

**Why it matters:** An ANTLR4-generated parser produces a parse tree (or listener/visitor-driven AST) whose node types are ANTLR4-generated classes, not the hand-written `GlpRuntime.Compiler.AstNode` hierarchy (`out/csharp/lib/compiler/ast.cs`). The downstream `Analyzer`, `PartialEvaluator`, `CodeGenerator` all consume `AstNode`-typed objects. "Confirm identical IL" requires the ANTLR4 parser's output to be either (a) an adapter mapping ANTLR4 parse-tree to the existing AST hierarchy or (b) a complete replacement of the AST hierarchy. The spike does not specify which.

**Options:**
1. Adapter approach: write a thin visitor over the ANTLR4 parse tree that produces `GlpRuntime.Compiler.AstNode` instances. Downstream pipeline is untouched. Consequence: validates the grammar without a full pipeline refactor; the spike is self-contained and reversible.
2. Full AST replacement: replace the `AstNode` hierarchy with ANTLR4-generated nodes. Consequence: a larger refactor, no longer a throwaway spike, and changes the classification from EXPERIMENT to PREP.
3. Behavioral-only check: do not integrate with the downstream pipeline; compare execution results (not IL artifacts) between the two parsers. Consequence: simplest spike; weakest IL-identity claim.

### U3 — "Identical IL" success criterion not quantified

**Why it matters:** Two parser front-ends targeting the same grammar rules will produce semantically equivalent parse trees, but code-generated parsers and hand-written parsers may produce structurally different but semantically equivalent ASTs (e.g., different handling of operator precedence, list-tail elision, reader-variable annotation). The downstream `CodeGenerator` may produce different-but-equivalent opcode sequences from these different ASTs. "Identical IL" needs to be defined as either (a) byte-identical `BytecodeProgram` instruction lists, (b) execution-equivalent (same final bindings for all test goals), or (c) trace-equivalent (same opcode sequence when run).

**Options:**
1. Byte-identical instruction list: strongest; requires #4's serialization; confirms no latent parser divergence. Gate: `Seq(generated-ANTLR4-path) == Seq(hand-written-path)` for all corpus programs.
2. Execution-equivalent: compare `ExecutionResult` for a corpus of goals. Weaker (cannot detect opcode divergences that cancel out); does not require #4.
3. Trace-equivalent: compare disassembly via `runner.cs:88 ToDisassembly()`. Intermediate; works without #4; catches most opcode divergences except label-renaming.

---

## GEPA/DSPy refinement

### Applicability

**methodological** — the spike itself is a grammar-engineering and parser-generation task (ANTLR4 `.g4` authoring, multi-target codegen, AST bridging), not an LM-program-generation task that DSPy/GEPA literally optimizes. However, the grammar-as-example-verifier role maps directly to GEPA/DSPy methodology: define a grammar candidate → evaluate it against the example corpus (acceptance/rejection for all `.glp` programs) → identify grammar gaps/ambiguities as the "refinement signal" → iterate. The GEPA reflective-mutation loop applies to grammar rules, not LM programs.

The `codeconv-codegen-opt` skill precedent (offline GEPA/DSPy optimizer, no API) applies here at the methodological level: Claude drives grammar refinement by evaluating against the corpus, identifying failing/ambiguous cases, proposing rule fixes, and iterating until all examples parse correctly.

### Seed definition

Define the GLP grammar once in ANTLR4 (`.g4` format, action-free, portable) capturing the full GLP token vocabulary (`token.cs:1-71`) and syntax (clause structure, three-phase HEAD/GUARD/BODY, readers, type declarations, module declarations, guard operators, mode declarations). Generate at minimum a C# parser using the ANTLR4 C# target. Trial the Dart target. Confirm the generated C# parser produces a parse tree from which the downstream `CodeGenerator` generates byte-identical (or execution-equivalent) IL to the hand-written parser for all programs in the unified REPL corpus (`test/run_all_tests.sh`). Use the ANTLR4 grammar as a formal example-coverage verifier: every `.glp` program in `programs/` must be accepted.

### Metrics combination

| Name | Kind | Tool/Harness | Threshold |
|---|---|---|---|
| Grammar acceptance rate on REPL corpus | pragmatic | ANTLR4 parser vs `programs/` corpus; pass/fail per file | 100% of files accepted without parse error |
| Grammar rejection rate on negative corpus | pragmatic | Parse each program from REPL suite Section C (negative type) and Section D (SRSW violations); these must NOT fail at the grammar level (syntax vs type/SRSW error boundary) | 100% syntactically valid (grammar accepts them; type/SRSW checker rejects them) |
| IL identity: execution equivalence | pragmatic | Run all REPL test suite goals through ANTLR4 front-end + existing CodeGenerator; compare `ExecutionResult` against baseline (hand-written parser) | 100% agreement on Status + Bindings for all test goals |
| IL identity: byte-level instruction parity | formal | `BytecodeProgram` serialization from #4 (if available); `Seq(ANTLR4) == Seq(hand-written)` for all corpus programs | 100% byte-identical instruction sequences for all programs |
| Grammar formal coverage: all token types exercised | formal | Static grammar analysis: every rule in the `.g4` file is reachable; every `TokenType` in `token.cs` is covered | Zero unreachable rules; zero uncovered token types |
| ANTLR4 C# parser generates SRSW-preserving AST | formal | Property: for every clause in the corpus, SRSW validity (at most one occurrence of each writer) is preserved through parse→AST→annotate | Pass for all clauses in `programs/tests/typed/` |

### Interactive spec step

At the start of `/buildkit-specify` for this seed: confirm (a) the "identical IL" success criterion — byte-level (#4 required) or execution-equivalent (independent of #4); (b) the grammar-verifier corpus scope — full `programs/` tree or REPL test suite files only; (c) whether C++ target is in scope (blocked on #14 readiness) or deferred; (d) whether the spike is explicitly throwaway (grammar stays as a standalone test harness) or becomes the production front-end (scope of #11); (e) the ANTLR4 AST integration strategy (adapter vs replacement).

### Refinement loop

1. **Seed**: author an initial GLP `.g4` grammar from `token.cs:1-71` and the hand-written parser's production rules (`parser.cs` ParseModule/ParseProcedure/ParseClause etc.).
2. **Candidate evaluation**: run the ANTLR4 tool to generate the C# parser; attempt to parse every `.glp` file in `programs/`; collect failures.
3. **GEPA reflective mutation**: Claude analyzes parse failures, identifies grammar ambiguities or missing rules, proposes targeted rule edits. Key failure categories expected: (a) operator precedence in guards, (b) `=..`/`..=` operator disambiguation, (c) reader `X?` annotation inside compound terms, (d) list-tail syntax with `\` difference-list operator.
4. **Evaluate**: re-parse corpus; compute acceptance rate. Repeat until 100% acceptance.
5. **IL identity check**: compile a test corpus of goals through ANTLR4 front-end + CodeGenerator; compare `ExecutionResult` to baseline. Identify any AST-to-IR bridging gaps.
6. **Terminate**: when grammar accepts 100% of corpus, IL identity holds for test goals, and the C# parser round-trips the same programs as the hand-written parser. No external API; Claude drives the tactic-loop.

---

## Formal tooling

### Lean 4 vs Rocq evaluation

**Lean 4 fit:** Lean 4 is the stronger fit for this seed's formal needs. The grammar-as-verifier role calls for mechanized proofs about *grammar properties* (coverage, unambiguity, completeness), which align with Lean 4's dependent-type foundation and mathlib. A Lean 4 formalization of the GLP token alphabet and grammar rules as an inductive type system allows proofs that: (a) every `TokenType` in `token.cs` has a corresponding lexer rule; (b) every GLP production in `parser.cs` has a corresponding grammar rule; (c) the grammar is unambiguous for the HEAD/GUARD/BODY structure. Lean-LSP-MCP provides a Claude-native tactic loop (no API). The APOLLO/Lean Copilot/Copra tools for model-agnostic agentic proving apply directly.

**Rocq fit:** Rocq (Coq) has stronger prior art for *verified compilers* (CompCert, Vellvm) and for mechanizing operational semantics of logic programs (TWAM uses LF/Twelf but the Coq ecosystem has similar precedents via the Verified Prolog→WAM compiler, ScienceDirect 1992). For this seed specifically — grammar correctness rather than compiler correctness — Rocq's advantage is in the *IL verification* connection: if the grammar spike is a stepping-stone toward a verified GLP→IL compiler, Rocq's CompCert/Vellvm heritage is more directly applicable. AutoRocq provides a tactic loop, but its GPT-4 dependency must be replaced with Claude per the no-API rule.

**Primary: lean4.** Grammar formalization (coverage, completeness, unambiguity for GLP syntax) is a better fit for Lean 4's type-theoretic approach and the model-agnostic agentic tools (Lean-LSP-MCP). Rocq is the natural choice if/when this seed's grammar work feeds into a verified compilation proof — retain Rocq as an alternative in that specific circumstance.

**Alternative when:** If the grammar spike is explicitly scoped to feed a verified GLP→IL compiler proof (i.e., the scope expands to include a formal proof that the ANTLR4-generated parser + `CodeGenerator` preserve operational semantics), use Rocq as co-primary (CompCert/Vellvm heritage directly applicable).

### IL verification

This seed touches the wire/byte contract indirectly: the "confirm identical IL" check is a byte-parity check on `BytecodeProgram` instruction sequences. The relevant IL-verification approach is:

- **Byte-parity**: `decode(encode(ANTLR4-generated-BytecodeProgram)) ≡ decode(encode(hand-written-parser-BytecodeProgram))` — requires #4's round-trip harness.
- **MLIR dialect framing (forward-looking)**: the SEED-RECONCILIATION-BRIEF.md §3.2 specifies a GLP/FCP MLIR dialect with primitives HEAD-unify / GUARD-test / BODY-spawn / suspend-reactivate. A grammar spike that confirms identical IL at the bytecode level is the necessary *precondition* for the MLIR dialect: if two parsers produce different IL for the same source, the dialect's semantics are ambiguous. The ANTLR4 grammar spike is therefore a prerequisite for the higher-level MLIR verification work, not a consumer of it.
- **Verification dialect (PLDI'25)**: the "First-Class Verification Dialects for MLIR" paper (Regehr et al., PLDI'25) makes semantics first-class in MLIR, enabling dialect-level verification. If/when a GLP MLIR dialect is defined, this spike's grammar forms the front-end that feeds it.

---

## Shapiro criteria preserved

This spike must not undermine the following original Shapiro/GLP design criteria as it installs a new front-end parser and confirms IL identity:

1. **SRSW (Single-Reader/Single-Writer):** the parser front-end must produce an AST in which writer-occurrences and reader-occurrences (`X` vs `X?`) are correctly distinguished. The `READER` token type (`token.cs:9`) and its AST representation must be preserved verbatim. Any ANTLR4 grammar ambiguity in reader annotation would silently violate SRSW in generated IL. The IL-identity check must include SRSW-annotated programs.
2. **Three-valued unification (SUCCESS / SUSPEND / FAIL):** the grammar must correctly parse the guard separator `|` (`GUARD_SEP`) as distinct from the list tail `|` (`PIPE`), which disambiguates HEAD from GUARD from BODY at the syntactic level. Misparse here would cause GUARD goals to land in the BODY or vice versa, breaking the three-phase execution semantics.
3. **Committed-choice concurrency:** clause ordering is load-bearing in GLP (committed choice is deterministic per selection order). The parser must preserve clause order exactly as written; the ANTLR4 grammar must not introduce any reordering or normalization of clauses within a procedure.
4. **Monotone variable binding:** the `READER` annotation (`X?`) in the AST determines whether a variable occurrence is a reader (suspension candidate) or a writer (binding site). The grammar must preserve this annotation faithfully; a parser that strips or normalizes `?` annotations would violate monotone binding.
5. **Suspension correctness (embedded-switch context):** for the embedded-switch purpose (connectivity SWITCH + QHSM/HSM actor host + OS tasks), the parser front-end is the entry point for GLP programs that define the switch's routing logic. A parser divergence (ANTLR4 vs hand-written) in handling guard conditions or reader annotations could cause the switch to mis-classify a connection or fail to suspend correctly on an unbound reader.

---

## Recommendation

The seed is **correctly classified as EXPERIMENT** and its role is accurately described in the dossier. The most important clarification needed before `/buildkit-specify` is the explicit disambiguation of the two roles (grammar-as-verifier vs grammar-as-production-parser) and the confirmation of the "identical IL" success criterion. The hidden dependency on #4 for byte-level IL comparison should be surfaced explicitly.

**Recommended scope for the spike** (advisory): deliver Phase-A (grammar-as-verifier only, action-free `.g4`, parse 100% of `programs/` corpus) as a standalone deliverable gated only on #11 (or even earlier — the verifier role does not require compiler relocation; it only needs the grammar to be written and the corpus to exist). Phase-B (production parser integration + IL identity) can follow as a separate sub-task gated on both #4 and #11. This allows the formal grammar metric to become available earlier in the roadmap, which the BRIEF §3.2 identifies as valuable ("before any compiler exists").

The C++ target should be explicitly deferred to #14 in the feature spec; keeping it as a note in the dossier is sufficient.

---

## Options for owner

1. **Scope = verifier-first (recommended):** define the grammar + example-coverage verifier as Phase-A (depends only on #11 or standalone); add production-parser IL-identity as Phase-B (depends on #4 + #11). Consequence: formal grammar metric arrives early; Phase-B is naturally triggered by #4 readiness.
2. **Scope = production-parser only:** skip the standalone verifier role; go directly to generating a C# parser that replaces the hand-written one and confirms IL identity. Consequence: requires #4 + #11; formally stronger end state but all value delayed to both prerequisites.
3. **Scope = grammar-verifier as an early-stage independent feature:** split off `antlr4-grammar-verifier` as a new seed depending only on #1a (methodology) and the `programs/` corpus. The production-parser seed retains depends_on: #11. Consequence: adds a roadmap entry but enables the formal grammar metric to be a gate for all subsequent language-touching seeds (as the BRIEF §3.2 intends).

---

## Open questions

1. Does the grammar-as-verifier role (BRIEF §3.2) require the full ANTLR4 toolchain (generate parser, run on corpus) or only the `.g4` grammar to exist as a formal specification? If the latter, the verifier could be as simple as manually parsing all examples against the grammar rules without code generation.
2. The dossier's §11 #12 says "generate C# (trial C++/Dart) parser front-ends." Should the Dart target be co-primary (because the Dart runtime `glp_runtime/` is an active first-class target) rather than "trial"? The byte-parity requirement (FR-060/061) from `FrameCodec.cs:31-32` applies to the Dart mirror — a grammar-generated Dart parser must produce byte-identical IL with the C# parser, which is a non-trivial constraint.
3. The dossier lists `depends_on: 11` (compiled-IL-on-wire + factor-out-compiler). Should `4` (il-codec-spike) also be listed if byte-level IL identity is the success criterion? Or is execution-equivalence the accepted fallback?
4. If the spike is explicitly "throwaway-or-keep" — what is the decision criterion for keeping (replacing the hand-written parsers)? The spec should define this up front so the spike does not drift into a de-facto production refactor.

---

## External references

1. [ANTLR4 multi-target support + Dart target](https://github.com/antlr/antlr4/blob/master/doc/dart-target.md) — official Dart runtime target documentation; ANTLR4 supports C#, Dart, C++ natively.
2. [antlr/grammars-v4 Prolog grammar](https://github.com/antlr/grammars-v4/tree/master/prolog) — prior art: an action-free ISO Prolog grammar in ANTLR4; directly applicable as a starting point for the GLP grammar (GLP is Prolog-lineage, extending ISO syntax with readers, guards, modules, type declarations).
3. [TWAM: A Certifying Abstract Machine for Logic Programs (arxiv:1801.00471)](https://arxiv.org/pdf/1801.00471) — WAM-lineage verified IL; the IL-verification model for GLP bytecode; relevant to the "confirm identical IL" formal check.
4. [First-Class Verification Dialects for MLIR (PLDI'25)](https://users.cs.utah.edu/~regehr/papers/pldi25.pdf) — MLIR verification dialect making semantics first-class; the forward-looking IL-verification layer the grammar spike feeds into.
5. [KLIC — KL1 portable implementation via C translation](https://github.com/GunterMueller/KLIC/blob/master/klic/documents/klic.tex) — concurrent logic language (KL1/FCP lineage) compiled to C; prior art for language-level IL design in GLP's ancestor family.
6. [BinProlog: Architecture and Implementation Choices for Continuation Passing Prolog (ResearchGate)](https://www.researchgate.net/publication/48202631_The_BinProlog_Experience_Architecture_and_Implementation_Choices_forContinuation_Passing_Prolog_and_First-Class_Logic_Engines) — BinWAM/continuation-passing Prolog IL; prior art for a logic-language-specific simplified IL format.
