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

### B1. Guards
- [ ] defined-guard / predicate call (e.g. `ground(X?)`)
- [ ] arithmetic comparison `<`
- [ ] arithmetic comparison `>`
- [ ] arithmetic comparison `=<`
- [ ] arithmetic comparison `>=`
- [ ] arithmetic comparison `=` (unify-guard)
- [ ] arithmetic equality `=:=`
- [ ] arithmetic disequality `=\=`
- [ ] ground-equality `=?=`
- [ ] term order `@<` / `@>` / `@=<` / `@>=`
- [ ] guard negation `~G`
- [ ] parenthesized disjunction `(G1 ; G2)`
- [ ] guard separator `|` (guard region present)

### B2. Operators (term / expression)
- [ ] `+`  (addition)
- [ ] `-`  (subtraction / unary `neg`)
- [ ] `*`  (multiplication)
- [ ] `/`  (division)
- [ ] `//` (integer division)
- [ ] `mod` (infix modulo)
- [ ] `mod(...)` call form (functor) — **T016 / C3**
- [ ] `#`  (module qualification / remote call)
- [ ] `\`  (difference-list)
- [ ] `=..` (univ)
- [ ] `..=` (reverse univ, head-only)
- [ ] `:=` (arithmetic assignment)
- [ ] `=` (unification)
- [ ] op-as-functor e.g. `-(A,B)` / `*(A,B)`
- [ ] `@AgentId` spawn

### B3. Terms / structure
- [ ] struct `f(a,b)`
- [ ] nested struct
- [ ] struct-element-inside-list `[f(a)|T]`
- [ ] list cons `[H|T]`
- [ ] empty list `[]`
- [ ] proper list `[a,b,c]`
- [ ] variable writer `X`
- [ ] variable reader `X?`
- [ ] anonymous `_`
- [ ] anonymous reader `_?`
- [ ] named-anonymous `_Foo`
- [ ] number (integer)
- [ ] number (real)
- [ ] string literal
- [ ] atom constant
- [ ] quoted atom `'...'`

### B4. Declarations & type alternatives
- [ ] `-module(name).`
- [ ] `-mode(user).` / `-mode(system).`
- [ ] `procedure` declaration
- [ ] `exported procedure`
- [ ] `imported procedure` (with `#`-path)
- [ ] parameterized proc decl `Stream(X)`
- [ ] type def `::=`
- [ ] type union `;` alternative
- [ ] type-alt struct
- [ ] type-alt list (nil / cons)
- [ ] type-alt primitive mode `_` / `_?`
- [ ] type-alt difference-list `\`
- [ ] type-alt dual marker trailing `?`
- [ ] parameterized type ref `Channel(In, Out)`

**Coverage complete** ⇔ every box in B is `[x]` (ticked by a `corpus/MANIFEST.md` entry).
Boxes are ticked as corpus files are added/verified in T014–T016.
