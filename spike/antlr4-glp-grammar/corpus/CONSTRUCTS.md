<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Construct coverage floor + lowering mapping — feature 069 (T002)

Two checklists. **(A)** every `Glp.g4` parser rule → the engine AST node the bridge lowers it to
(data-model LoweringMapping; the G1 total-coverage contract). **(B)** the per-construct coverage
floor (FR-005 / contract C2): every distinct guard / operator / type-alternative construct must be
exercised by ≥1 corpus program. `[ ]` = not yet ticked by a corpus entry, `[x]` = covered.

## A. Grammar rule → engine AST node (bridge visitor coverage — G1)

Every rule below MUST have a `GlpLoweringVisitor` handler; an unmapped rule throws (T012).

| # | Glp.g4 rule | Lowers to (ast.cs / type_ast.cs) |
|---|-------------|----------------------------------|
| 1 | `module` | `Module(decl, typeDefs, procDecls, null, procedures, mode, 1, 1)` |
| 2 | `item` | dispatch → `ProcDecl` \| `TypeDef` \| `Clause` |
| 3 | `directive` | `-module`→`ModuleDeclaration`; `-mode`/`-stdlib`→`CompileMode` (no node) |
| 4 | `moduleName` | dotted `string` (name of `ModuleDeclaration`) |
| 5 | `procDecl` | `ProcDecl(name, argTypes, …, exported, imported, modulePath)` |
| 6 | `procName` | `string` name (+ `modulePath` for imported `#`-path) |
| 7 | `procNameToken` | operator/atom lexeme `string` |
| 8 | `argType` | `TypeExpr` (`PrimitiveModeAlt` \| `TypeRef`) |
| 9 | `typeDef` | `TypeDef(name, alternatives, …, typeParams)` |
| 10 | `typeAlt` | `TypeExpr` via `TypeConversion.TermToTypeExpr(term)` |
| 11 | `typeExpr` | term → (then TermToTypeExpr); binary op = `StructTerm(op,[l,r])` |
| 12 | `typePrimary` | term primary (var/reader/`_`/num/str/atom/list/paren/neg) |
| 13 | `typeList` | `ListTerm` chain (nil / cons / `|`-tail) |
| 14 | `clause` | `Clause(headAtom, guards?, body?)` (fact = both null) |
| 15 | `head` | `Atom(functor, args)` (incl. `:=`/`=..`/`..=`/`=` lvalue forms) |
| 16 | `atomApp` | functor + args (→ `Atom`/`StructTerm`/`ConstTerm` by position) |
| 17 | `goal` | `Goal` / `RemoteGoal`(`#`) / `SpawnGoal`(`@`) |
| 18 | `goalOrGuard` | `~`→negation flag; else `goalOrGuardInner` |
| 19 | `goalOrGuardInner` | `Goal`/`RemoteGoal`/`Goal(";",…)` disjunction / comparison `Goal` |
| 20 | `cmpOp` | operator lexeme `string` |
| 21 | `arith` | `StructTerm(op,[l,r])` (arith operand of a comparison guard) |
| 22 | `arithPrimary` | primary term (var/num/str/list/paren/atom/op-as-functor/neg) |
| 23 | `term` | Pratt expr → `StructTerm(op,[l,r])` / `primary` |
| 24 | `primary` | var/reader/`_`/num/str/atom/struct/list/paren/op-as-functor/neg |
| 25 | `list` | `ListTerm` chain (nil / cons / `|`-tail) |

## B. Per-construct coverage floor (FR-005 / C2)

Every box is ticked by ≥1 corpus program that carries the construct through the comparator. Coverage
source is cited per box; **(IL)** = the covering file is an IL-parity MATCH (construct reaches codegen
identically); **(parse)** = covered at parse-acceptance parity only (both front-ends agree accept/reject —
the construct does not reach codegen, e.g. type-alternatives, or the file is BC-1-bounded). All cited
sweeps recorded in `../RESULTS.md` (0 un-caused divergences).

### B1. Guards
- [x] defined-guard / predicate call (`ground(X?)`) — `arith_guard_ground.glp` (IL)
- [x] arithmetic comparison `<` — `arith_comparison.glp` (IL)
- [x] arithmetic comparison `>` — `arith_comparison.glp` (IL)
- [x] arithmetic comparison `=<` — `arith_comparison.glp` (IL)
- [x] arithmetic comparison `>=` — `arith_comparison.glp` (IL)
- [x] arithmetic comparison `=` (unify-guard) — bounded fuzz (non-cyclic `A? = B?`) + `test_defined_guards.glp` (IL)
- [x] arithmetic equality `=:=` — `arith_comparison.glp` (IL)
- [x] arithmetic disequality `=\=` — `arith_diseq.glp` (IL)
- [x] ground-equality `=?=` — `test_ground_equal.glp`, `order_guards.glp` (IL)
- [x] term order `@<` / `@>` / `@=<` / `@>=` — `order_guards.glp` (IL)
- [x] guard negation `~G` — `test_guard_negation.glp` (IL)
- [x] parenthesized disjunction `(G1 ; G2)` — `policy_guard_vectors.glp` (IL)
- [x] guard separator `|` (guard region present) — every guarded clause, e.g. `arith_guard_ground.glp` (IL)

### B2. Operators (term / expression)
- [x] `+`  (addition) — `op_forms.glp`, `arith_comparison.glp`, bounded fuzz (IL)
- [x] `-`  (subtraction / unary `neg`) — `op_forms.glp` (unary neg), bounded fuzz (subtraction) (IL)
- [x] `*`  (multiplication) — `op_forms.glp`, `multiply.glp` (IL)
- [x] `/`  (division) — bounded fuzz (BinOp `/`) (IL)
- [x] `//` (integer division) — bounded fuzz (BinOp `//`) (IL)
- [x] `mod` (infix modulo) — `mod_functor_call.glp`, bounded fuzz (IL)
- [x] `mod(...)` call form (functor) — `mod_functor_call.glp` (IL) — **T016 / C3, RESOLVED**
- [x] `#`  (module qualification / remote call) — `programs/tests/dynamic_dispatch/dispatch_client.glp` (IL)
- [x] `\`  (difference-list operator) — `append_dl.glp`, `diff_list.glp`, `bb_diff.glp` (IL)
- [x] `=..` (univ) — `programs/typed_book/recursive/structure_processing/observe.glp` (IL)
- [x] `..=` (reverse univ, head-only) — `programs/typed_book/recursive/structure_processing/distribute_nonground.glp`, `observe.glp` (IL)
- [x] `:=` (arithmetic assignment) — `op_forms.glp`, `arith_comparison.glp` (IL)
- [x] `=` (unification) — bounded fuzz + `test_defined_guards.glp` (IL)
- [x] op-as-functor e.g. `-(A,B)` / `*(A,B)` / `+(A,B)` — `op_forms.glp` (IL)
- [x] `@AgentId` spawn — `programs/typed_book/multiagent_tests/bidirectional_exchange_boot.glp` (IL)

### B3. Terms / structure
- [x] struct `f(a,b)` — `struct_demo.glp` (IL)
- [x] nested struct — `two_struct_list.glp`, `struct_demo.glp` (IL)
- [x] struct-element-inside-list `[f(a)|T]` — `two_struct_list.glp` (IL)
- [x] list cons `[H|T]` — `nonground_list.glp` (+ most) (IL)
- [x] empty list `[]` — most list programs, e.g. `append_dl.glp` (IL)
- [x] proper list `[a,b,c]` — most list programs, e.g. `nonground_list.glp` (IL)
- [x] variable writer `X` — every clause (IL)
- [x] variable reader `X?` — every clause (IL)
- [x] anonymous `_` — `abandon_stream.glp` (IL)
- [x] anonymous reader `_?` (type position) — `abandon_reader_bad.glp` (parse; `_?` is a type-position mode symbol)
- [x] named-anonymous `_Foo` — lexes as VARIABLE (Glp.g4 VARIABLE rule `'_' [A-Z]…`) → `VarTerm`; covered by any variable-bearing MATCH file (IL)
- [x] number (integer) — most, e.g. `multiply.glp` (IL)
- [x] number (real) — `abandon_stream.glp`, `multi_client_control.glp` (IL)
- [x] string literal — `hello.glp` (IL)
- [x] atom constant — most, e.g. `arith_comparison.glp` (`equal`/`not_equal`) (IL)
- [x] quoted atom `'...'` — `quoted_functor_test.glp` (IL)

### B4. Declarations & type alternatives
- [x] `-module(name).` — `programs/tests/dynamic_dispatch/math_service.glp` (IL)
- [x] `-mode(user).` / `-mode(system).` — `typed_social_agent.glp` (IL)
- [x] `procedure` declaration — every file (IL)
- [x] `exported procedure` — `arith_comparison.glp`, `programs/tests/dynamic_dispatch/math_service.glp` (IL)
- [x] `imported procedure` (with `#`-path) — `programs/tests/dynamic_dispatch/dispatch_client.glp` (IL)
- [x] parameterized proc decl `Stream(X)` — `param_procedure_inference.glp`, `param_stream_integer.glp` (IL)
- [x] type def `::=` — `typed_social_agent.glp` (IL)
- [x] type union `;` alternative — `typed_social_agent.glp` (IL)
- [x] type-alt struct — `typed_social_agent.glp` (IL)
- [x] type-alt list (nil / cons) — `typed_social_agent.glp` (`PeerList`) (IL)
- [x] type-alt primitive mode `_` / `_?` — `abandon_reader_bad.glp` (parse)
- [x] type-alt difference-list `\` — `programs/typed_book/streams/buffered_communication/bounded_buffer.glp` (parse)
- [x] type-alt dual marker trailing `?` — `typed_social_agent.glp` (`Channel ::= ch(Stream, Stream?)`) (IL)
- [x] parameterized type ref `Channel(In, Out)` — `param_channel.glp` (IL)

**Coverage complete** ✅ — every box in B is ticked by a corpus entry (verified via the `../RESULTS.md`
sweeps, T014–T016). No residual un-covered construct.
