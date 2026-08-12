<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# ANTLR4 shared-grammar feasibility report (feature 065, US1/Scope A)

**Spike:** `spike/antlr4-glp-grammar/` · **Date:** 2026-08-04 · **Author:** olamnit session.
Reviewable without running the spike (SC-003). All claims trace to committed artifacts.

---

## 1. Verdict — **GO-WITH-CONDITIONS**

A **single, faithful ANTLR4 grammar can describe the GLP surface syntax**, and the generated C#
parser reproduces the hand-written parser's accept/reject decision on **7/7** of the representative
corpus (SC-001, 100%). This is strong evidence that a shared grammar is feasible and that it can be
authored *faithfully* (no syntax change was required or made — SC-004).

The **condition** is IL parity (SC-002): demonstrating byte-identical `BytecodeProgram` output
requires an **ANTLR-parse-tree → engine-AST lowering bridge** that was **not built in this spike**.
Coverage parity establishes the front-ends accept the same *language*; it does not by itself prove
the two produce the same *IL*. The bridge is the one remaining engineering item and is scoped in §3.
Recommend proceeding to a bounded PREP feature that builds the bridge and closes SC-002 before any
production adoption.

---

## 2. Grammar coverage (SC-001) — **7/7 = 100 %**

Corpus per `corpus/MANIFEST.md`, parsed by the ANTLR-generated `GlpParser.module()` and by the
production hand-written `GlpRuntime.Compiler.Parser.ParseModule()` (ground truth). Harness:
`harness/Program.cs` (runnable: `dotnet run` in `harness/`).

| # | corpus file | neg? | ANTLR | hand-written | parity |
|---|-------------|------|-------|--------------|--------|
| 1 | append_dl.glp | | accept | accept | MATCH |
| 2 | arith_comparison.glp | | accept | accept | MATCH |
| 3 | arith_diseq.glp | | accept | accept | MATCH |
| 4 | arith_guard_ground.glp | | accept | accept | MATCH |
| 5 | abandon_stream.glp | | accept | accept | MATCH |
| 6 | typed_social_agent.glp | | accept | accept | MATCH |
| 7 | abandon_reader_bad.glp | yes | accept | accept | MATCH |

**No non-covered construct remains** across the corpus. One gap was found and faithfully closed
during the spike: the initial grammar mis-hoisted the directive keyword predicate and rejected
`-mode(system).` (file 6); factoring the shared `MINUS` prefix so each soft-keyword predicate sits
at the inner-alternative left edge fixed it — a grammar-engineering fix, **not** a syntax change.

**Negative control (file 7):** `abandon_reader_bad.glp` is accepted by *both* front-ends **at the
parse level** — correctly. Its badness is **semantic** (SRSW / reader-mode), rejected downstream by
the type-checker, not the parser; so parse-level parity (both accept) is the faithful outcome. The
spike verifies the parser, not the checker.

---

> **UPDATE (feature 069, 2026-08-11): SC-002 CLOSED.** The lowering bridge scoped below was built
> under feature `069-sc-002-il-parity-bridge`; IL parity is now demonstrated example-by-example with
> **0 un-caused divergences** across the 7-file corpus (SC-001 7/7), the expanded corpus
> (`tests/typed`, `lib`, `typed_book`, `dynamic_dispatch`), and a 10 000-case bounded fuzz (SC-003).
> Evidence: [`RESULTS.md`](RESULTS.md). Adoption verdict (**ADOPT-WITH-CONDITIONS**) + bounded
> conditions: [`DECISION.md`](DECISION.md). The `mod`-functor divergence noted in §6/§7 is RESOLVED
> (lexer predicate, Gabi + Udi approved). The residual risks in §7 are superseded by DECISION.md's
> bounded conditions BC-1…BC-4.

## 3. IL parity (SC-002) — ~~not demonstrated in-spike; bridge scoped~~ **CLOSED by feature 069 (see update above)**

The generated parser emits an ANTLR parse tree; the engine's downstream pipeline
(SRSW → partial-eval → type-check → compile → `BytecodeProgram`) consumes the engine's own AST
(`GlpRuntime.Compiler` term/clause nodes). Producing IL from the ANTLR side therefore requires a
**lowering bridge**: an ANTLR visitor that maps each grammar rule to the corresponding engine AST
node, after which both front-ends feed the *identical* shared downstream pipeline and their
`BytecodeProgram` instruction sequences are compared.

- **Built:** the two front-ends + the coverage harness (accept/reject parity).
- **Not built:** the lowering bridge, so **no IL-parity numbers are reported** (reporting any would
  be unfounded — this spike does not fake SC-002).
- **Estimated cost:** one visitor method per grammar rule (~22 rules) → engine node — roughly
  **250–400 LOC of mechanical mapping**, plus the pipeline-invocation glue (the engine already
  exposes a compile path; `il-codec` #4 gives deterministic `BytecodeProgram` serialization for the
  comparison — see §5). No new engine capability is required. This is the whole of the SC-002 gap.

Because the grammar is a *faithful description of the same accepted language* (7/7) and the
downstream pipeline is shared and unchanged, the risk that lowered-IL diverges is low and localized
to the bridge's own correctness — which the harness's IL comparison would then verify example-by-
example.

---

## 4. Multi-target cost (T014)

- **C# (primary):** generated with `-Dlanguage=CSharp` and **built clean** against
  `Antlr4.Runtime.Standard` 4.13.1 + the engine (`harness/` builds 0-error). This is the demonstrated
  target.
- **C++ / Dart / Gleam:** **explicitly deferred** (documented, not attempted). Rationale: ANTLR4
  generates all of C#, Java, Python3, JavaScript, TypeScript, Go, C++, Swift, PHP, and Dart from the
  **same `Glp.g4`** via `-Dlanguage=<T>` — so the *grammar* cost of a second target is ≈ 0. The real
  per-target cost is (a) an ANTLR runtime for that language and (b) a target-language lowering bridge
  (§3). Notes: ANTLR's **Dart** target is supported but less battle-tested than C#/Java (a maturity
  risk to weigh); **Gleam is NOT an ANTLR target** — a Gleam consumer would parse via the C#/other
  generated parser as a side-process, or use a hand-written/ different-tool parser, so "one grammar,
  every runtime" holds for the ANTLR-supported languages but *not* for Gleam directly.

---

## 5. Dependency posture (T002)

Confirmed available and relied upon for the (scoped) IL comparison:
- **Compiled-IL-on-the-wire (#11)** — delivered (wave-4/062, specs/050): bytecode can be produced
  and transported.
- **il-codec round-trip (#4)** — delivered: deterministic `BytecodeProgram` (de)serialization, which
  is exactly the equality oracle the SC-002 comparison uses.
No residual external dependency. Toolchain used: Java 17 (OpenJDK), ANTLR 4.13.2 complete jar
(vendored), dotnet 10.0.301, `Antlr4.Runtime.Standard` 4.13.1 (NuGet).

---

## 6. §1.14 status — faithful; **zero accepted-syntax changes landed** (SC-004)

Authored under **Gabi + Udi approval** (`PROPOSAL-1.14.md`). `Glp.g4` is a description of the
*existing* accepted syntax only, derived from `lexer.cs`/`token.cs` and `parser.cs` (cross-checked
against `parser.dart`, which is line-for-line equivalent), with file:line citations inline. **No
change to the accepted GLP syntax was required or made.** Two documentation premises were found
false against the code and are corrected here (DISCIPLINE §1.5/§1.7):
1. **`=..` is NOT clause-head-only.** It is reachable in body/guard positions too; the genuine
   head-only case is a *leading `_` lvalue* for `:= / =.. / ..= / =` (accepted by `ParseAtom` only).
   `docs/known-issues.md` contains no `=..`-in-heads entry.
2. **Struct-elements-inside-lists in REPL goals are ACCEPTED**, not restricted, by the current
   parser+engine.

Two items are **grammar engineering, not syntax changes**, and are carried as findings:
- **`mod`-as-functor (DIVERGENCE):** the hand-lexer emits atom `mod` when the next char is `(`
  (a `mod(...)` call) else keyword `MOD`; ANTLR cannot peek past a token, so `mod` is always `MOD`.
  The corpus uses `mod` only as the infix operator, so parity holds; a production grammar would need
  a lexer predicate or island handling.
- **soft keywords:** `module/stdlib/mode/user/system/exported/imported` are ATOMs matched by a
  `SoftKw` predicate, preserving hand-lexer tokenization (only `mod`/`procedure` are keywords).

---

## 7. Residual risks & recommendation

> **UPDATE (feature 069): this recommendation is DONE.** The PREP feature was
> `069-sc-002-il-parity-bridge`: the bridge is built, SC-002 is closed with example-by-example IL
> parity over the expanded corpus, and the adoption decision is delivered
> (**ADOPT-WITH-CONDITIONS**, [`DECISION.md`](DECISION.md)). The residual risks below are now the
> enumerated bounded conditions BC-1…BC-4 in DECISION.md (the `mod`-functor risk is RESOLVED).
> Production parsers remain untouched (FR-010).

**Recommendation:** GO to a bounded **PREP feature** that (1) builds the parse-tree→engine-AST
lowering bridge (§3), (2) closes SC-002 with example-by-example IL parity over an expanded corpus,
and (3) decides adoption. Production parsers stay untouched until that feature (FR-010).

**Residual risks:**
- **SC-002 unproven** until the bridge exists — the load-bearing open item.
- **Corpus breadth:** 7 files exercise the distinctive constructs but are not exhaustive; expand
  before adoption (book/lib/plays, all guard/operator/type-alt corners).
- **`mod`-functor** lexer divergence (above) — needs a predicate/island in production.
- **Var-vs-comparison dispatch & deep type-alt corners** rely on ANTLR ALL(*) full-context
  prediction; verified sufficient on the corpus, but worth adversarial fuzzing before adoption.
- **Dart-target maturity** and **Gleam not being an ANTLR target** (§4) bound the "one grammar,
  every runtime" claim.
