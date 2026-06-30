# ANTLR-Integration Pipeline — DOSSIER

- **Feature:** 036-glp-gleam-baseline-program
- **Run:** mrun-5611c436ba95
- **Task:** T008
- **Date:** 2026-06-29
- **Contract honored:** `docs/research/glp-gleam-baseline/contracts/pipeline-contract.md` — every claim below cites a `file:line` / `file:page` read directly; read-only on sibling repos (`D:/bstdev/research/qhstate/...`) and on all live code; nothing written outside `docs/research/glp-gleam-baseline/`; judged on **separability / maintainability / analyzability / multi-target reach** (NO fastest-path rubric); FR-005 satisfied by a built-and-run artifact (the p5 spike).

This dossier records the P2 grammar-**packaging** fork for the ANTLR front-end (ED-4): how the GLP grammar source is organized before it is generated to a target. It does NOT decide grammar *content* (that is the P2 scope analysis) — only **combined-vs-split packaging**, plus the verified base and the scaling risks from the spike's single-clause spine to the full GLP surface.

---

## 1. The verified BASE (FR-005 anchor) — `spike/p5-il-merge`

All paths under `D:/bstdev/research/glp/glpnet/spike/p5-il-merge/`.

The built-and-run artifact is the **P5 spike**: an ANTLR 4.13.2 grammar generated to Dart, walked into the production `glp_runtime` AST, and carried through the real compiler to **byte-identical v2.16.3 bytecode** and **execution-equivalent** behaviour on the live runner.

**What it is.** A minimal **combined** grammar (lexer + parser in one file, declared `grammar merge;` at `grammar/merge.g4:14`) scoped to exactly the tokens of `merge/3` clause 1 — `merge([X|Xs], Ys, [X?|Zs?]) :- merge(Ys?, Xs?, Zs).` (`merge.g4:3-5`, sourced from `programs/paper/merge.glp:8`). Parser rules `program : clause+ EOF` (`merge.g4:17`), `clause : head (NECK (guards BAR)? body)? DOT` (`merge.g4:20`), `compound : ATOM LPAREN termList RPAREN` (`merge.g4:28`), `term : var | list | compound | atom` (`merge.g4:32-36`), `var : VAR QUESTION?` (the reader marker, `merge.g4:39`), list-cons (`merge.g4:45-46`); lexer rules `NECK ':-'`, `BAR '|'` shared by list-cons AND guard-separator (`merge.g4:49,53`), `ATOM [a-z]…`/`VAR [A-Z_]…` (`merge.g4:59-60`), `%`-comments + WS to HIDDEN (`merge.g4:62-63`).

**What it proves.** Generated `antlr4 -Dlanguage=Dart merge.g4` (`SPIKE-RESULT.md:27,188`) to the vendored `lib/antlr_gen/merge{Lexer,Parser,Listener,BaseListener}.dart` (`SPIKE-RESULT.md:189`), driven by `bin/phase_b.dart` + `lib/antlr_adapter.dart` (which imports the production `package:glp_runtime/compiler/ast.dart`, `antlr_adapter.dart:7`, and walks the ANTLR tree into the same `Program/Procedure/Clause/Atom/Goal/Guard/VarTerm/ListTerm/StructTerm/ConstTerm` nodes Phase A used). Phase B de-risk, three sub-steps all **PASS** (`SPIKE-RESULT.md:179-208`):
- **B1** grammar authored + generated to Dart + vendored (`SPIKE-RESULT.md:181-189`).
- **B2** parse tree → AstNode **structural-agreement gate** vs the production glp parser: head + body diff `true`/`true`, identical registers `X0,X1,X2,X3` (`SPIKE-RESULT.md:191-201`).
- **B3** re-run A2→A6 from the ANTLR AST: IL identical, `V1 PASS`/`V2 PASS`, **A5(B) bytecode IDENTICAL to stock `CodeGenerator`** (same 17-op listing, `SPIKE-RESULT.md:205-206`), **A6(B) execution-equivalent** on the real runner — Suspend-not-Fail on unbound reader, reactivate+commit on bind (`SPIKE-RESULT.md:206-207`).

Scorecard row **B = "PASS — structural agreement + identical bytecode + equivalent execution"** with **"No BLOCKERS"** (`SPIKE-RESULT.md:218,220`). `dart analyze lib bin` → "No issues found" (`SPIKE-RESULT.md:16`). Byte-identity is **exact, not "up to ordering"**, because the IL lowering reads the *same* analyzer-assigned register table `CodeGenerator` reads (`SPIKE-RESULT.md:146-148,230-232`).

**The shape of this artifact is Option A (combined).** Therefore Option A is FR-005-verified *as a whole*, on the **Dart** target, today, with zero new build. Toolchain: Dart SDK 3.10.1; ANTLR 4.13.2 (antlr4-tools / Java 17); `antlr4` pub package 4.13.2 (`SPIKE-RESULT.md:31`).

**Caveat (in-record):** single clause **by design** ("Single clause, by design", `SPIKE-RESULT.md:226`; `DOSSIER.md:54` of the spike). The other three `merge` clauses, guards, and nested-struct args are supported by the lowering code paths (mirrored from `glp_runtime/lib/compiler/codegen.dart`) but only clause 1 is exercised end-to-end (`SPIKE-RESULT.md:224-228`).

---

## 2. The integration OPTIONS — packaging fork (A combined vs B split)

The grammar can be packaged two ways. Both keep the **adapter, not replacement** model (a thin visitor maps the ANTLR parse tree → existing `AstNode`; PartialEvaluator / TypeChecker / Analyzer / CodeGenerator untouched), and both are invisible to the engine: the bytecode-on-wire seam is the front/back boundary (ED-4), ANTLR has no BEAM target, so the Gleam engine never consumes ANTLR output directly.

### Option A — combined `Glp.g4` (one file: `grammar Glp;`)

This is the spike's shape (`merge.g4:14`).

**Pros**
- *FR-005 / verification-risk (separability of risk, today):* the **only option verified end-to-end right now** — combined→Dart→byte-identical bytecode + equivalent execution, "No BLOCKERS" (`SPIKE-RESULT.md:218,220`). Zero new build to claim FR-005.
- *Maintainability (one surface):* a single source file for one syntax; multiple **start rules** (e.g. `compilationUnit`, `replGoal`) can share the one in-file lexer trivially, covering the `Module` (production, `parser.dart:59`) vs REPL-goal (`parser.dart:17`) two-entry-point need without any cross-file plumbing.
- *Multi-target reach (mechanically equal):* a combined `.g4` generates to Dart **and** C# from one source via `-Dlanguage=`; same bytecode-on-wire seam to Gleam (ED-4) — packaging is invisible to the engine.

**Cons**
- *Maintainability / analyzability at GLP density:* lexer-ordering constraints (maximal-munch, multi-char-before-single-char) sit in the same file as, but **far from**, the dependent parser rules; at ~25 operators (full-glp-scope §9; `glp_runtime/lib/compiler/token.dart`) the load-bearing token-order discipline is scattered through one large file rather than quarantined.
- *Convention divergence:* the in-repo production front-ends are **all split** (qhstate, below). Combined adopts a shape the production repo abandoned.
- *Multi-target gap (unbuilt half):* **combined→C# has no built artifact** — only combined→Dart is built. A C# parity claim still owes a combined→C# build + cross-target B2 gate. The "single-source feeds both languages" economy is a file-count maintainability point, not a *verified* reach advantage (the only csproj that proves combined-style C# generation does not exist; the existing csproj is **split**, see B).

### Option B — split `GlpLexer.g4` + `GlpParser.g4` (one versioned unit; parser `options { tokenVocab = GlpLexer; }`)

This replicates the in-repo production precedent.

**Precedent (read directly, read-only).** qhstate ships **four** `.g4` in **two** parallel units, each SPLIT: `Csharp/qhxm/grammar/QhxmLexer.g4` declares `lexer grammar QhxmLexer;` (`QhxmLexer.g4:25`) + `QhxmParser.g4` declares `parser grammar QhxmParser;` (`QhxmParser.g4:23`) bound by `options { tokenVocab = QhxmLexer; }` (`QhxmParser.g4:25`); the second unit `grammar-cs/{QhxmCsLexer,QhxmCsParser}.g4` is the same convention for a C#-flavoured surface. In-file rationale: "This lexer + QhxmParser.g4 form ONE versioned grammar unit — the single source of truth… Never define QHxM syntax anywhere else" (`QhxmLexer.g4:5-7`). Generated to **C#** by `regen/Qhxm.Regen.csproj` via Antlr4BuildTasks 12.14.0 against `Antlr4.Runtime.Standard` 4.13.1, inside `dotnet build`, no system Java (`Qhxm.Regen.csproj:26-29`), with both lexer and parser listed as separate `<Antlr4 Include>` items, `Listener=true Visitor=true Error=true` (`Qhxm.Regen.csproj:31-42`); committed-generated sources guarded by a byte-identical drift gate via `scripts/regen-qhxm-frontend.ps1` (header byte-normalized, ps1:13-19,58-68).

**Pros**
- *Separability:* the `tokenVocab` boundary quarantines GLP's worst lexical hazards — overloaded `.` (clause-`DOT` `merge.g4:57` vs Real decimal vs `=..`/`..=`), dual-role `|` (`merge.g4:53`; `token.dart:22,28`), SRSW `?` (`merge.g4:50`) — into **one** reviewable lexer file, distinct from the parser-rule file.
- *Maintainability:* matches the in-repo, drift-gated production convention (`Qhxm.Regen.csproj:31-42`; `regen-qhxm-frontend.ps1`) and **reuses** the qhstate Antlr4BuildTasks harness rather than reinventing one. Per-unit drift attribution (lexer change vs parser change is a separate file diff).
- *Analyzability:* two-level gating — token-stream agreement (lexer) and tree-shape agreement (parser) are separable checks; supports **one lexer backing two divergent parser surfaces** (qhxm proves this: `grammar/` SM04 vs `grammar-cs/` C#-flavoured).
- *Multi-target reach:* same bytecode-on-wire seam to Gleam (ED-4); **C# half is already built** in the precedent harness (`Qhxm.Regen.csproj:31-42`).

**Cons**
- *FR-005 gap (the decisive one):* **split→Dart is unbuilt.** B is verified only in *halves* — split→C# (qhstate) + combined→Dart (spike); a split grammar generated specifically to Dart is **not** in any corpus. The split→Dart bridge is stock ANTLR `tokenVocab` resolution but has not been run. To restore the FR-005 anchor *as a whole*, B must first re-run `antlr4 -Dlanguage=Dart` over a split `merge` lexer/parser pair and re-confirm the B2 structural-agreement + A5 byte-identity gates (the exact gates combined already passed, `SPIKE-RESULT.md:196-205`).
- *Reach overclaim to avoid:* the "one lexer backs multiple parser surfaces" advantage is **real only for a divergent dialect surface** (qhxm `grammar/` vs `grammar-cs/`); for the mere Module-vs-REPL-goal two-entry-point case (`parser.dart:17` vs `:59`), a combined grammar with two start rules suffices — so B's reach edge must not be claimed for that case.
- *Toolchain skew (shared with A, but B carries the extra unbuilt combination):* C# precedent pins `Antlr4.Runtime.Standard` **4.13.1** (`Qhxm.Regen.csproj:27`); the Dart spike uses antlr4 **4.13.2** (`SPIKE-RESULT.md:31`).

### The symmetric finding (sharpens the fork — neither author stated it plainly)

- **A is built on Dart, unbuilt on C#** (FR-005 Dart artifact uses the *combined* shape, `merge.g4:14`; no combined→C# artifact exists).
- **B is built on C#, unbuilt on Dart** (qhstate C# harness uses the *split* shape, `Qhxm.Regen.csproj:31-42`; no split→Dart artifact exists).

Each option has **exactly one built target and one unbuilt target.** The FR-005 anchor (a Dart result) lands on A's packaging; the production C# convention (qhstate, split, drift-gated) lands on B's packaging.

### Comparison (mandated axes)

| Axis | A — combined `Glp.g4` | B — split `GlpLexer.g4`+`GlpParser.g4` |
|---|---|---|
| FR-005 status today | **Verified as a whole** (combined→Dart→byte-identical, `SPIKE-RESULT.md:218,220`) | **Verified in halves only** (split→C# qhstate; combined→Dart spike); **split→Dart unbuilt** |
| Built / unbuilt target | Dart built / **C# unbuilt** | C# built / **Dart unbuilt** |
| Matches in-repo production convention? | **No** — qhstate front-ends are all split (`Qhxm.Regen.csproj:31-42`) | **Yes** — replicates the split, drift-gated convention |
| Separability | Lexer-order constraints far from dependent parser rules | `tokenVocab` seam quarantines worst hazards into one file |
| Maintainability | One file; spike-proven; convention-divergent | Per-unit drift attribution; reuses qhstate harness |
| Analyzability | Single tree-shape gate | Two-level token-stream + tree-shape gates |
| Multi-target reach | Both langs from one source (combined→C# unbuilt) | Both langs (split→Dart unbuilt); 1 lexer→N parser surfaces (only for divergent dialects) |
| BEAM/Gleam reach | **Identical** — bytecode-on-wire seam (ED-4); ANTLR has no BEAM target | **Identical** — same |
| Cost to close its gap | Build combined→C# + cross-target B2 gate | Build split→Dart: re-run `-Dlanguage=Dart` over a split `merge` pair, re-confirm B2 + A5 |

---

## 3. Verified-option statement (FR-005) + recommendation (OWNER-GATED)

**FR-005 satisfied:** YES. At least one option is anchored to a built/run artifact — **Option A (combined)** is verified end-to-end by `spike/p5-il-merge` (combined `grammar merge;` → ANTLR 4.13.2 → Dart → production AST → **byte-identical 17-op v2.16.3 bytecode** vs stock `CodeGenerator` + **execution-equivalent** on the live runner; scorecard **B = PASS**, **No BLOCKERS**, `SPIKE-RESULT.md:218,220`). Option B is verified in halves (split→C# qhstate `Qhxm.Regen.csproj:31-42`; combined→Dart spike) with **split→Dart unbuilt**.

**RECOMMENDATION — OWNER-GATED (read-only on the live roadmap/specs/code + all sibling repos until the migration gate; FR-010/FR-011).**

Recommend **Option B (split)**, **conditioned on first closing the split→Dart gap** — re-run the spike's `antlr4 -Dlanguage=Dart` over a split `merge` lexer/parser pair and re-confirm the B2 structural-agreement + A5 byte-identity gates (`SPIKE-RESULT.md:196-205`). Rationale strictly on the mandated axes:
- **Separability:** the `tokenVocab` seam quarantines GLP's worst lexical hazards (overloaded `.`/`=..`/`..=`, dual-role `|`, SRSW `?`) into one reviewable lexer file.
- **Maintainability:** matches the in-repo, drift-gated production convention (`Qhxm.Regen.csproj:31-42`; `regen-qhxm-frontend.ps1`) and reuses qhstate tooling rather than reinventing it.
- **Analyzability:** two-level token-stream + tree-shape gates; per-unit drift attribution; one lexer can back divergent parser surfaces (qhxm `grammar/` vs `grammar-cs/`).
- **Multi-target reach:** ties A on BEAM/Gleam reach (bytecode-on-wire seam, ED-4); the C# half is already built in the precedent harness.

**Fallback — Option A** if the owner prioritizes a **zero-new-verification FR-005 anchor *today*** over convention-fidelity: A is the only option verified end-to-end right now, and the small split→Dart build is the only thing between B and parity with that status.

**Why this is owner-gated, not self-decided.** Both A and B diverge in packaging from one of the two verified shapes; the choice trades "verified-as-a-whole today" (A) against "production-faithful + more separable, pending one small build" (B). The packaging choice also diverges from the in-repo production convention (split, qhstate). Per FR-010/FR-011 this dossier surfaces the fork; it does not ratify either authoring lens. Owner ratification required before any migration.

---

## 4. Scaling risks (merge.g4 → full GLP grammar)

The spike proves the **spine only** ("Single clause, by design", `SPIKE-RESULT.md:226`); everything below is net-new and unexercised end-to-end. Ordered by severity. (Risk list mirrored from `data-model.md` of full-glp-scope; citations read directly.)

1. **Infix-operator clause heads/goals (HIGHEST).** `merge.g4` pins `head : compound` (`merge.g4:22`) and `goal : compound` (`merge.g4:26`). Real heads/goals are infix-operator terms — `Result? := N :- …`, `X? = X.`, `X? =.. [Y|Ys]`, guards `X? > 0` / `Y? =\= 0` (`programs/self.glp:87,113,374`; full-glp-scope §5-6). Generalizing to a left-recursive term/expr rule introduces head/term and operator-precedence disambiguation the single-clause spike never touched.

2. **Type-declaration grammar `::=` (absent entirely).** No `::=` rule in `merge.g4`. Must add parameterized types `Stream(X)`/`Channel(In,Out)`, `;`-unions, `\` difflists, in-type `?` mode marks, primitive symbols `_`/`_?` (`programs/self.glp:10-18`; full-glp-scope §3). Hazard: the **same `QUESTION` lexeme** (`merge.g4:50`) means **reader marker in clause context** (`VarTerm.isReader`) vs **input-mode in type context** (`TypeRef.isInput`) — the adapter must route by context (compiler-memo). Untested: the spike built `Program`, not `Module` (`antlr_adapter.dart:31-32` known gap), so the whole `TypeDef`/`TypeExpr` envelope the TypeChecker needs is unbuilt.

3. **Module/directive envelope + `Module` target.** `-module(name).`/`-mode(system).` absent from `merge.g4` (`programs/self.glp:3`; full-glp-scope §2). Production must build `Module` (declaration/typeDefs/procDeclarations, `glp_runtime/lib/compiler/ast.dart:250-285`; `parser.dart:59`), not the spike's `Program`. The declaration-carrying top rule (`compilationUnit : item* EOF`) and clause→`Procedure` re-grouping (contiguity enforced, `parser.dart:36-56`) are net-new.

4. **Lexer operator-density / maximal-munch (where combined-vs-split bites).** `merge.g4` tokenizes 9 operators (`:- ? [ ] | ( ) , .`, `merge.g4:49-57`); production needs ~25 (full-glp-scope §9; `token.dart`), many shared-prefix: `::=`/`:-`/`:=`, `=`/`=..`/`=:=`/`=\=`/`=?=`, `@<`/`@=<`. Sharpest: the `.` lexeme overloads clause-`DOT` (`merge.g4:57`), the Real decimal point, and `=..`/`..=`. Strict multi-char-before-single-char ordering is load-bearing (qhstate documents exactly this discipline, `QhxmLexer.g4` header). **This is the concrete axis on which split (B) localizes the hazard and combined (A) scatters it** — a real, not hypothetical, maintainability/analyzability difference at GLP scale.

5. **Guard `;` disjunction + `otherwise` + BAR-split at scale.** `merge.g4` parses the guard *position* (`merge.g4:20,24`) and the dual-role `BAR` is proven **only on the no-guard clause 1** (B2 PASS). Net-new: `;`-disjunction `(X?<-1 ; X?>1)` (`programs/self.glp:289,301`), `otherwise` (`programs/.../metainterpreter.glp:13`), and — critically — a clause that *actually has guards* (`head :- g1,g2 | b.`) exercises Shapiro criterion #2 (guard-sep `|` must not leak GUARD goals into BODY) for real, which the spike never ran.

6. **Error-recovery posture (owner decision, unaddressed by both options).** The spike uses `BailErrorStrategy` — loud fail, **no recovery** (`antlr_adapter.dart:20`). Production wants located diagnostics over the whole corpus, and the negative corpus (`test/run_all_tests.sh` Sections C/D) must be **syntactically accepted** then rejected by the type/SRSW checker, not the grammar (compiler-memo U1; qhxm's "clean superset" stance). Risk: bail-on-first-error gives no recovery diagnostics, and a too-tight grammar may wrongly reject valid negative-corpus programs. Flag for owner.

7. **C#/Dart parity (compounded for B).** No GLP grammar has been built on **either** target yet; cross-target byte-identical-AST parity has never been shown for GLP. Toolchain skew: C# `Antlr4.Runtime.Standard` **4.13.1** (`Qhxm.Regen.csproj:27`), Dart antlr4 **4.13.2** (`SPIKE-RESULT.md:31`); left-recursion/predicate/Unicode handling can diverge subtly across targets. Parity must be gated by running the B2 structural-agreement diff on **both** targets. The qhstate drift gate guards C# committed-bytes, not cross-target AST equivalence. **Compounding for B:** its split→Dart half is itself unbuilt (overlaps risk 4's fork gap), so B carries one extra unverified target-combination than A does today.

8. **Accepted-language changes that must be flagged, not silent.** A clean ANTLR grammar would naturally accept `=..` **uniformly in head and body**, silently lifting the known head-only restriction (CLAUDE.md known-issues; full-glp-scope §7), and accept structs-in-lists in goals (CLAUDE.md known-issues). These are **language-surface decisions**, not grammar accidents — explicit owner decisions, not silent grammar side-effects.

9. **Source positions (mechanical, orthogonal to fork).** Spike hardcodes `(1,0)` everywhere (`antlr_adapter.dart`); production must thread real line/column from ANTLR tokens — `AstNode.line`/`column` are mandatory (`glp_runtime/lib/compiler/ast.dart:14-19`). Equal cost under A or B.

---

## Files of record

- `D:/bstdev/research/glp/glpnet/spike/p5-il-merge/grammar/merge.g4` — FR-005 verified base (combined `grammar merge;`), read.
- `D:/bstdev/research/glp/glpnet/spike/p5-il-merge/SPIKE-RESULT.md` — scorecard B PASS (`:218`) / No BLOCKERS (`:220`) / byte-identical 17-op disasm (`:134-148,205-206`), read.
- `D:/bstdev/research/glp/glpnet/spike/p5-il-merge/lib/antlr_adapter.dart` — adapter to production AST; `Program`-not-`Module` gap (`:31-32`); `BailErrorStrategy` (`:20`); hardcoded positions, read.
- `D:/bstdev/research/qhstate/Csharp/qhxm/grammar/QhxmLexer.g4` (`:5-7,25`) + `QhxmParser.g4` (`:23,25`) — split precedent (read-only), read.
- `D:/bstdev/research/qhstate/Csharp/qhxm/regen/Qhxm.Regen.csproj` (`:26-29,31-42`) — split→C# harness, runtime 4.13.1, read.
- `D:/bstdev/research/glp/glpnet/glp_runtime/lib/compiler/{parser.dart, ast.dart, token.dart}` — Module/AST/token contract, per grounding (compiler-memo).
- `D:/bstdev/research/glp/glpnet/programs/self.glp` — canonical prelude (types, operators, arithmetic), per grounding (full-glp-scope).
