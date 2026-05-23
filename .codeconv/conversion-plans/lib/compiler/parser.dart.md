---
path: lib/compiler/parser.dart
cycle_group_id: 11
scc_siblings: []
generated_at: 2026-05-21T15:25:33Z
source_sha256: d5b6f4a7c81d0dcfd0fb32be8b28f7da3d3b77dc84571a10f063188114b2e9eb
schema_version: 1
---

# Conversion Plan: lib/compiler/parser.dart

## 1. Source Analysis

The source file `lib/compiler/parser.dart` (1762 lines, the largest single unit in the codebase) implements GLP's hand-rolled recursive-descent parser. Inspection of the actual `.dart` source confirms the following load-bearing facts:

- **Imports (lines 1-6)**: five relative imports — `'token.dart'`, `'ast.dart'`, `'error.dart'`, `'../analysis/type_checker/type_ast.dart'`, `'../analysis/type_checker/type_conversion.dart'`, plus `'../analysis/type_checker/prelude.dart' show builtinProcedures` (single-symbol restriction).
- **Class shape (lines 9-14)**: `class Parser { final List<Token> tokens; int _current = 0; Clause? _pendingClause; Parser(this.tokens); ... }` — one immutable input, one mutable cursor, one mutable single-slot look-back, plus a positional-initialising-formal ctor.
- **Public entry points**: `Program parse()` (legacy, lines 17-31) and `Module parseModule()` (full, lines 59-326).
- **State-machine in `parseModule`**: declaration loop (`-module` / `-stdlib` / `-mode` / `-export`-error / `-import`-error) followed by body-element loop with pending-decl tracking via `ProcDecl? pendingProcDecl` and `Dictionary<String, Procedure> seenProcedures` for non-contiguous-clause detection.
- **Helper toolkit (lines 1258-1284)**: `_match`/`_check`/`_advance`/`_peek`/`_previous`/`_isAtEnd`/`_consume` — the canonical recursive-descent atomic primitives. `_advance` post-increments then returns previous; `_consume` checks-then-advances or throws `CompileError` with `phase: 'parser'` named arg.
- **Clause parser (lines 460-503)**: `Head :- Guards | Body.` shape; pre-PIPE predicates become Guards with `~`-prefix-strip into a `negated` flag; post-PIPE become body Goals; missing PIPE means everything-was-body.
- **GoalOrGuard dispatcher (lines 506-707)**: returns Dart `dynamic`, handling `~`-negation (with `~~G` rejection), parenthesized expressions/disjunctions, Variable/Reader prefix with five-way ASSIGN/UNIV/UNIV_DECOMPOSE/EQUALS/HASH look-ahead, Atom prefix with HASH (static remote) / EQUALS (unification) / AT (spawn), and infix-comparison fall-through via `_parseExpression(6)`.
- **Atom (clause-head) parser (lines 718-785)**: five head shapes — (a) `foo(...)` atom-prefix, (b)-(e) `Var := Expr`, `Var =.. List`, `Var ..= List`, `Var = Term`, plus (f) `_ := Expr` for abort clauses. No `#` or `@` (those are body-only).
- **Goal (clause-body) parser (lines 789-880)**: like Atom but adds `Var # Goal` (dynamic remote), `atom # Goal` (static remote), and `Goal @ Agent` (spawn).
- **Pratt expression sub-parser (lines 910-1182)**: precedence-climbing with left-associative recursion via `minPrecedence + 1`; primary has 9 branches; `_isOperator`/`_precedence`/`_operatorFunctor` tables hard-code 14 operator token types and the precedence levels 20/10/5/2/1/0.
- **List parser (lines 1185-1255)**: empty list `[]`, dotted-pair `[H|T]`, mixed `[X,Y,Z|T]`; right-associative cons; rejects `?` reader-mark on three list-completion paths.
- **Type-decl helpers (lines 1290-1337)**: `_isTypeOrProcDeclaration` and `_isTypeDefinition` use save-cursor / `_current = saved` restore, with depth-counter LPAREN/RPAREN tracking for parameterised type-name lookahead.
- **TypeDef parser (lines 1342-1380)**: `TypeName[?] [(typeParams)] ::= alt (; alt)* .`; READER-token type-name re-encoded with `?` suffix.
- **Parallel type-alt grammar (lines 1389-1590)**: `_parseTypeAlt` / `_parseTypeAltTerm` / `_parseTypeAltExpression` / `_parseTypeAltPrimary` / `_parseTypeAltList` — near-duplicate of the term parsers but tolerating trailing `?` on most term shapes.
- **ProcDecl parser (lines 1595-1697)**: optional `exported`/`imported` ATOM modifier, PROCEDURE keyword, 11-way operator-or-ATOM name dispatch, `#`-separated module path for imported procs, optional `(argTypes)` (nullary parens optional), terminating `.`.
- **ProcArgType parser (lines 1701-1760)**: primitive (`_[?]`), qualified (`atom#...#TypeName[?]`), or plain typeref with optional `(typeArgs)` and optional `?`.
- **Cursor-rollback** is used in five places: `_skipDeclarations`, `parseModule` declaration loop, `_parseAtom` rewind-on-no-operator, `_isTypeOrProcDeclaration`, `_isTypeDefinition`.
- **Look-ahead via `tokens.length > _current + 1`** in eight sites.
- **Error messages** preserved byte-for-byte (the GLP test suite asserts on diagnostic strings).

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim. Construct keys, target decisions, idiom IDs, research findings, and nuance markers are reproduced from the convspec.

### Construct: dart.class.stateful_recursive_descent_parser_with_final_token_list_and_int_cursors → C# `class Parser`

- Source form: `class Parser { final List<Token> tokens; int _current = 0; Clause? _pendingClause; Parser(this.tokens); ... }`.
- Target decision: Emit a C# reference `class Parser` (NOT `record`, NOT `struct`) with one get-only auto-property `Tokens` of type `IReadOnlyList<Token>`, one private mutable `long _current = 0` cursor, one private mutable `Clause? _pendingClause = null` slot. The Dart positional-initialising-formal `this.tokens` expands to explicit ctor `Tokens = tokens;`. Reference identity is required so cursor mutations across method calls observe the same field.
- research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
- Nuance: one `final` field + two mutable; `Tokens` becomes get-only auto-property, cursor + look-back stay mutable private fields. `_current` is Dart `int` ⇒ C# `long`. `Clause?` ⇒ C# `Clause?` with `#nullable enable`. Privacy: `_`-prefix ⇒ `private`.

### Construct: dart.recursive_descent.lookahead_helpers_match_check_advance_peek_consume_previous_isatend → recursive-descent toolkit

- Source form: the seven helpers `_match`/`_check`/`_advance`/`_peek`/`_previous`/`_isAtEnd`/`_consume`.
- Target decision: emit each as a private instance method on Parser. Signatures: `Match(TokenType type) -> bool`, `Check(TokenType type) -> bool`, `Advance() -> Token`, `Peek() -> Token`, `Previous() -> Token`, `IsAtEnd() -> bool`, `Consume(TokenType type, string message) -> Token`. Use expression-bodied form for `Peek`/`Previous`/`IsAtEnd`. Cast `long → int` at every `Tokens[(int)_current]` indexer. `_advance` post-increments-then-returns-previous, clamping at EOF. `_consume` throws `CompileError(message, Peek().Line, Peek().Column, phase: "parser")` on mismatch.
- research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
- Nuance: `Match` is conditional-consume; `Check` is pure look-ahead. `Advance` returns the JUST-CONSUMED token. `Previous` reads `Tokens[_current - 1]`. `Peek` reads `Tokens[_current]` (EOF sentinel makes it always safe). `Consume` reports error at `Peek()`'s line/column. Every error path throws `CompileError`; preserved verbatim.

### Construct: dart.parser_entry.parse_legacy_skipping_declarations_returning_program → `Parse()` + `SkipDeclarations()`

- Source form: `Program parse() { _skipDeclarations(); ... while (!_isAtEnd()) procedures.add(_parseProcedure()); _checkContiguousClauses(procedures); return Program(procedures, 1, 1); }` plus `_skipDeclarations()` with the `['module','stdlib','mode'].contains(keyword)` literal-list check.
- Target decision: emit `public Program Parse()` returning `Program`. Body: call `SkipDeclarations()`, accumulate via `while (!IsAtEnd()) procedures.Add(ParseProcedure());`, then `CheckContiguousClauses(procedures);` and `return new Program(procedures, 1, 1);`. For the literal-list-contains check use `static readonly FrozenSet<string> DeclarationKeywords = FrozenSet.Create(StringComparer.Ordinal, "module", "stdlib", "mode");` (per glp_printer.dart's cached idiom). Cursor rollback `_current = startPos;` preserved verbatim.
- research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
- Nuance: try-then-rollback look-ahead — save `startPos` before `Advance`, restore on failure. `FrozenSet<string>` with `StringComparer.Ordinal` for culture-invariant equality matching Dart code-unit ordinal `String.==`. Hard-coded `(1, 1)` position for synthetic Program node preserved verbatim.

### Construct: dart.parsemodule.declaration_dispatcher_loop_with_state_machine → `ParseModule()`

- Source form: the entire `parseModule()` body with declaration-loop + body-element-loop state machine.
- Target decision: emit `public Module ParseModule()` returning `Module`. Two-phase body: (1) declaration loop with cursor-rollback try-pattern and `switch (keyword.Lexeme) { case "module": ... case "stdlib": ... case "mode": ... case "export": throw new CompileError("The -export() declaration is no longer supported. Use 'exported procedure' instead.", startLine, startCol, phase: "parser"); case "import": throw new CompileError("The -import() declaration is no longer supported. Use 'imported procedure' instead.", startLine, startCol, phase: "parser"); default: _current = startPos; break; }`. C# string-switch matches by ordinal equality at compile-constant cases. (2) Body-element loop with three discriminating branches (PROCEDURE-keyword or `exported`/`imported`+PROCEDURE look-ahead, VARIABLE/READER → typeDef-or-clause via `IsTypeDefinition()`, ATOM → clause). Maintain `ProcDecl? pendingProcDecl` and `Dictionary<string, Procedure> seenProcedures`. Pending-decl flush iff builtin or `imported`. `builtinProcedures.contains(pendingSig)` ⇒ `Prelude.BuiltinProcedures.Contains(pendingSig)` (FrozenSet). String-interpolated signature `'${proc.name}/${proc.arity}'` ⇒ C# `$"{proc.Name}/{proc.Arity}"`. Final `return new Module(declaration: moduleDecl, typeDefs: typeDefs, procDeclarations: procDeclarations, procedures: procedures, compileMode: compileMode, line: 1, column: 1);` with named-args 1:1.
- research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
- Nuance: declaration loop is a tiny DFA with try-rollback. Switch-on-string is ordinal at compile-constant cases (identical semantics to Dart `String.==`). Pending-decl flush is conditional on builtin OR imported; any other non-matching clause throws. Every `throw CompileError(..., phase: 'parser')` ⇒ `throw new CompileError(..., phase: "parser")`. Module-constructor named-args preserved 1:1.

### Construct: dart.contiguity_check.signature_keyed_dictionary_first_occurrence_wins → `CheckContiguousClauses()`

- Source form: `void _checkContiguousClauses(List<Procedure> procedures) { final seen = <String, Procedure>{}; for (final proc in procedures) { final sig = '${proc.name}/${proc.arity}'; if (seen.containsKey(sig)) { ... throw CompileError(...); } seen[sig] = proc; } }`.
- Target decision: emit `private void CheckContiguousClauses(IList<Procedure> procedures)`. Body: `var seen = new Dictionary<string, Procedure>(StringComparer.Ordinal); foreach (var proc in procedures) { var sig = $"{proc.Name}/{proc.Arity}"; if (seen.TryGetValue(sig, out var first)) { throw new CompileError($"Non-contiguous clauses for \"{sig}\".\n  First group at line {first.Line}, second group at line {proc.Line}.\n  All clauses for a predicate must be together in the source file.", proc.Line, proc.Column, phase: "parser"); } seen[sig] = proc; }`.
- research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
- Nuance: GLP load-bearing invariant — non-contiguous clauses cause bytecode-gen bugs. `TryGetValue` is single-lookup (vs `containsKey` + indexer = 2 lookups). Ordinal comparer explicit for reviewer-clarity. Multi-line error preserved with `\n` escapes.

### Construct: dart.module_name_parser.dot_separated_qualified_name → `ParseModuleName()`

- Source form: `String _parseModuleName() { ... parts.add(_consume(TokenType.ATOM, ...).lexeme); while (_match(TokenType.DOT) && _check(TokenType.ATOM)) parts.add(_consume(...)); if (_previous().type == TokenType.DOT && !_check(TokenType.ATOM)) _current--; return parts.join('.'); }`.
- Target decision: emit `private string ParseModuleName()`. Body: `var parts = new List<string>(); parts.Add(Consume(TokenType.ATOM, "Expected module name").Lexeme); while (Match(TokenType.DOT) && Check(TokenType.ATOM)) parts.Add(Consume(TokenType.ATOM, "Expected module name part").Lexeme); if (Previous().Type == TokenType.DOT && !Check(TokenType.ATOM)) _current--; return string.Join(".", parts);`.
- research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
- Nuance: short-circuit `&&` look-ahead with DOT-rewind via `_current--`. `string.Join` identical contract to Dart `List<String>.join` — separator BETWEEN elements only.

### Construct: dart.procedure_aggregator.collect_clauses_with_same_functor_arity_and_pending_lookback → `ParseProcedure()`

- Source form: full `_parseProcedure()` with pending-clause drain, same-name detection, four operator-procedure look-aheads (`:=`/`=..`/`..=`/`=`), and arity-mismatch stash-and-break.
- Target decision: emit `private Procedure ParseProcedure()`. Body: drain `_pendingClause` if non-null (clear to null), else `var firstClause = ParseClause();`. Accumulate `var clauses = new List<Clause> { firstClause };`. Capture `var name = firstClause.Head.Functor; var arity = firstClause.Head.Arity;`. Loop: compute `couldBeSameProcedure` via `Peek().Type == TokenType.ATOM && Peek().Lexeme == name` OR for the four operator procedures look-ahead at `Tokens[(int)(_current + 1)].Type`. Use bounds-check `_current + 1 < Tokens.Count` verbatim. Mid-procedure arity-mismatch ⇒ `_pendingClause = clause;` and break. Functor-mismatch ⇒ `throw new CompileError($"Clause for {clause.Head.Functor}/{clause.Head.Arity} found, expected {name}/{arity}", clause.Line, clause.Column, phase: "parser");`. Return `new Procedure(name, arity, clauses, firstClause.Line, firstClause.Column);`.
- research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
- Nuance: `_pendingClause` is the parser's one-token-ahead memory across `ParseProcedure` calls — load-bearing. Operator procedures `:=`/`=..`/`..=`/`=` are infix in clause heads, so their clauses start with the LHS (VARIABLE/READER/UNDERSCORE) and the operator follows; UNDERSCORE allowed only for `:=` (abort clauses). Bounds-check `_current + 1 < Tokens.Count` matches Dart `tokens.length`.

### Construct: dart.clause_parser.head_then_optional_guards_pipe_body_dot → `ParseClause()`

- Source form: `Clause _parseClause() { final head = _parseAtom(); ... if (_match(TokenType.IMPLIES)) { ... if (_match(TokenType.PIPE)) { guards = predicates.map((g) { ... isNegated = g.functor.startsWith('~'); ... return Guard(...); }).toList(); body = ...; } else { body = predicates.cast<Goal>(); } } _consume(TokenType.DOT, ...); return Clause(head, guards: ..., body: ..., line: ..., column: ...); }`.
- Target decision: emit `private Clause ParseClause()`. Body: `var head = ParseAtom();`. On `Match(TokenType.IMPLIES)`: accumulate `var predicates = new List<object>();` (mixed Goal-or-Guard via Dart `dynamic` ⇒ C# `object`). Loop collects via `ParseGoalOrGuard()` COMMA-separated. On `Match(TokenType.PIPE)`: convert via `var guards = predicates.Select(g => { var goal = (Goal)g; bool isNegated = goal.Functor.StartsWith("~", StringComparison.Ordinal); var actualFunctor = isNegated ? goal.Functor.Substring(1) : goal.Functor; return new Guard(actualFunctor, goal.Args, goal.Line, goal.Column, negated: isNegated); }).ToList();`. Else `body = predicates.Cast<Goal>().ToList();`. `Consume(TokenType.DOT, "Expected \".\" at end of clause");`. Return `new Clause(head, guards: guards, body: body, line: head.Line, column: head.Column);`.
- research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
- Nuance: `|` is the GLP guard-vs-body separator; absent means everything is body. `~`-functor-prefix is an in-band negation signal, stripped during Guard conversion and recorded in `negated` flag. `<dynamic>` list ⇒ `List<object>` (NOT C# `dynamic` keyword — that's DLR late-binding, semantically different). `StartsWith` REQUIRES `StringComparison.Ordinal` to match Dart code-unit semantics. `Cast<Goal>()` throws `InvalidCastException` matching Dart `CastError`.

### Construct: dart.goal_or_guard_parser.parenthesized_disjunction_assignment_remote_negation_comparison → `ParseGoalOrGuard()`

- Source form: full `_parseGoalOrGuard()` with four-way disambiguation: optional `~` negation, parenthesized-expr/disjunction, Variable/Reader prefix with five-way look-ahead, Atom prefix with HASH/EQUALS/AT, fall-through infix-comparison via `_parseExpression(6)`.
- Target decision: emit `private object ParseGoalOrGuard()` (Dart `dynamic` ⇒ C# `object`, NOT C# `dynamic` keyword). Body preserves four-way disambiguation: (1) optional `~` prefix tracked via `var negated = false;` flipped on `Match(TokenType.TILDE)`; double-negation `~~` ⇒ `throw new CompileError("Double negation ~~G is not allowed", Peek().Line, Peek().Column, phase: "parser");`. (2) Parenthesized — disjunction (REJECTS negation with explicit error message) vs single-goal (allows negation). (3) Variable/Reader prefix — five-way ASSIGN/UNIV/UNIV_DECOMPOSE/EQUALS/HASH dispatch by `Tokens[(int)(_current + 1)].Type`; HASH ⇒ `RemoteGoal` (REJECTS negation). (4) Atom prefix — parse functor+args, check HASH (static remote, REJECTS negation), EQUALS (unification, REJECTS negation), AT (`SpawnGoal`); else regular Goal with `~`-prefix-encoded negation. (5) Fall-through ⇒ `ParseExpression(6)` + comparison-token dispatch (LESS/GREATER/LESS_EQUAL/GREATER_EQUAL/EQUALS/ARITH_EQUAL/ARITH_NOT_EQUAL/GROUND_EQUAL); on success build `new Goal(opLexeme, new List<Term> { left, right }, ...)`. Total-failure ⇒ `throw new CompileError("Expected predicate name or comparison", Peek().Line, Peek().Column, phase: "parser");`.
- research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
- Nuance: `dynamic` here is used as a sum type Goal-or-Guard; faithful C# is `object` (caller downcasts). Three negation rejections preserved verbatim (`~(A;B)`, `~(A#B)`, `~(X=Y)`). Spawn `@` preserved verbatim. Comparison fall-through via `_parseExpression(6)` exploits precedence 6 > 5 to stop at comparisons. OR-chain of comparison-token `Check(...)` calls preferred over HashSet probe.

### Construct: dart.goal_to_term_helper_for_disjunction → `GoalToTerm()`

- Source form: `Term _goalToTerm(dynamic goal) { if (goal is Goal) return StructTerm(goal.functor, goal.args, goal.line, goal.column); throw CompileError('Expected goal', 0, 0, phase: 'parser'); }`.
- Target decision: emit `private Term GoalToTerm(object goal) { if (goal is Goal g) return new StructTerm(g.Functor, g.Args, g.Line, g.Column); throw new CompileError("Expected goal", 0, 0, phase: "parser"); }`. Dart `is` test with property access ⇒ C# declaration pattern with capture.
- research_finding_id: rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal
- Nuance: `is Goal g` declaration pattern auto-promotes inside the if-branch — matches Dart's flow analysis. Sentinel `(0, 0)` position preserved verbatim. Error message preserved byte-for-byte.

### Construct: dart.atom_parser.head_form_assignment_univ_decompose_unify_or_functor_args → `ParseAtom()`

- Source form: full `_parseAtom()` with five head shapes — atom-prefix `foo(...)`, `Var := Expr`, `Var =.. List`, `Var ..= List`, `Var = Term`, and `_ := Expr` for abort clauses.
- Target decision: emit `private Atom ParseAtom()` returning `Atom`. Body: (1) Variable/Reader/Underscore head — capture `isReader`/`isUnderscore`, four-way operator-as-head-functor dispatch (`:=`, `=..`, `..=`, `=`). UnderscoreTerm permitted only for `:=`. Rollback `_current--;` if none of four operators follows. (2) Atom-prefix head — consume ATOM, optional `(args)` parsed as comma-separated `ParseTerm()`, optional trailing `=..` or `=`. Use named-argument call syntax for Atom ctor. NO `#` or `@` recognition (body-only).
- research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
- Nuance: five head shapes encoded by using operator as functor. Rollback-on-no-operator allows misclassified tokens to fall through. UNDERSCORE-only-for-`:=` enforced structurally — UNDERSCORE+other-operator paths fail via `Consume(TokenType.ATOM, ...)` throwing. Asymmetric goal-vs-atom-head set: NO HASH, NO AT here.

### Construct: dart.goal_parser.body_form_assignment_univ_unify_remote_spawn_or_functor_args → `ParseGoal()`

- Source form: full `_parseGoal()` mirroring `_parseAtom` shape but adding body-only HASH (`Var # Goal` and `atom # Goal`) and AT (`Goal @ Agent`) annotations.
- Target decision: emit `private Goal ParseGoal()` returning `Goal`. Variable-prefix dispatches on HASH (dynamic remote), ASSIGN, UNIV, UNIV_DECOMPOSE, EQUALS; none-of-the-above throws `$"Expected predicate name or assignment, got variable \"{varToken.Lexeme}\""`. Atom-prefix additionally recognises `# Goal` (static remote), trailing `=..`, and trailing `@Agent` (spawn). Module-name-with-args explicitly rejected: `if (args.Count > 0) throw new CompileError($"Module name cannot have arguments: {functorToken.Lexeme}", functorToken.Line, functorToken.Column, phase: "parser");`.
- research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
- Nuance: three forms added over atoms — dynamic remote (`Var # Goal`), static remote (`atom # Goal`), spawn (`Goal @ Agent`). Module-name-with-args rejection preserved verbatim for disambiguation. Every error message preserved byte-for-byte; `$"..."` interpolation replaces Dart `'...$x...'`.

### Construct: dart.guard_parser.simple_functor_arglist → `ParseGuard()`

- Source form: `Guard _parseGuard() { final functorToken = _consume(TokenType.ATOM, ...); ... return Guard(functorToken.lexeme, args, ...); }`.
- Target decision: emit `private Guard ParseGuard()`. Body: `var functorToken = Consume(TokenType.ATOM, "Expected guard predicate name"); var args = new List<Term>(); if (Match(TokenType.LPAREN)) { if (!Check(TokenType.RPAREN)) { args.Add(ParseTerm()); while (Match(TokenType.COMMA)) args.Add(ParseTerm()); } Consume(TokenType.RPAREN, "Expected \")\" after arguments"); } return new Guard(functorToken.Lexeme, args, functorToken.Line, functorToken.Column);`.
- research_finding_id: rf-dart-list-to-csharp-list-of-T
- Nuance: written-but-not-called variant — in-use guard parsing happens via `ParseGoalOrGuard` then conversion in `ParseClause`. Preserved for parity (effectively dead code; simpler shape intentional).

### Construct: dart.expression_pratt.precedence_climbing_min_prec_loop → `ParseTerm()` + `ParseExpression()`

- Source form: `Term _parseTerm() => _parseExpression();` and `Term _parseExpression([int minPrecedence = 0]) { var left = _parsePrimary(); while (_isOperator(_peek()) && _precedence(_peek()) >= minPrecedence) { final op = _advance(); final right = _parseExpression(_precedence(op) + 1); left = StructTerm(_operatorFunctor(op), [left, right], ...); } return left; }`.
- Target decision: emit `private Term ParseTerm() => ParseExpression();` (expression-bodied). Emit `private Term ParseExpression(int minPrecedence = 0)`. Body: `var left = ParsePrimary(); while (IsOperator(Peek()) && Precedence(Peek()) >= minPrecedence) { var op = Advance(); var right = ParseExpression(Precedence(op) + 1); left = new StructTerm(OperatorFunctor(op), new List<Term> { left, right }, op.Line, op.Column); } return left;`. Dart optional positional `[int minPrecedence = 0]` ⇒ C# default-valued positional `int minPrecedence = 0`.
- research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
- Nuance: canonical Vaughan-Pratt precedence-climbing. Left-associativity via `Precedence(op) + 1`. `ParseExpression(6)` from `ParseGoalOrGuard` stops at comparison operators (prec 5 < 6) — calibrated threshold.

### Construct: dart.expression_primary.unary_minus_variable_underscore_number_string_list_paren_atom → `ParsePrimary()`

- Source form: full `_parsePrimary()` with 9 branches.
- Target decision: emit `private Term ParsePrimary()`. 9-branch dispatcher: (1) Operator-as-functor (`+(X,Y)`/`-(X,Y)`/`*`/`/`/`//`/`mod`) via look-ahead `Tokens[(int)(_current + 1)].Type == TokenType.LPAREN`; consume operator-as-functor + LPAREN, recurse for args, return StructTerm. (2) Unary minus: `-X` ⇒ `new StructTerm("neg", new List<Term> { operand }, ...)`. (3) Variable/Reader + optional `:= Expr` ⇒ `new StructTerm(":=", ...)` or plain VarTerm. (4) Underscore + optional `?` ⇒ `new UnderscoreTerm(..., isReader: Match(TokenType.QUESTION))`. (5) Number: reject trailing `?`; `new ConstTerm(token.Literal, ...)`. (6) String: reject trailing `?`; `new ConstTerm($"\"{token.Literal}\"", ...)` preserving quote-wrapping for type-checker string detection. (7) List: delegate to `ParseList()`. (8) Parenthesized expression or tuple — right-associative comma-structure `new StructTerm(",", new List<Term> { terms[i], result }, ...)`. (9) Atom — with-args ⇒ StructTerm; bare ⇒ ConstTerm; reject trailing `?`. Fall-through ⇒ `throw new CompileError($"Expected term, got {Peek().Type}", ...)`.
- research_finding_id: rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal
- Nuance: 9 branches order-dependent (operator-as-functor MUST precede unary-minus so `-(X,Y)` parses as struct). Quote-wrapping convention preserved verbatim per ast.dart cross-spec contract. Reader-mark restricted to variables; six explicit rejection sites preserved. Right-associative comma-tuple `(A,B,C) = ,(A, ,(B,C))`. Unary minus is SYMBOLIC `neg(X)` for partial-evaluator.

### Construct: dart.operator_classification.is_operator_precedence_functor_tables → `IsOperator()` + `Precedence()` + `OperatorFunctor()`

- Source form: three private helpers — `_isOperator` (14-type OR-chain), `_precedence` (switch on token type returning 20/10/5/2/1/0), `_operatorFunctor` (switch on token type returning functor string; default throws).
- Target decision: emit three private static helpers. `private static bool IsOperator(Token token)` returning OR-chain over 14 `Type ==` checks. `private static int Precedence(Token op)` via switch-expression with case-stacking via `or` patterns: `op.Type switch { TokenType.Star or TokenType.Slash or TokenType.SlashSlash or TokenType.Mod => 20, TokenType.Plus or TokenType.Minus => 10, TokenType.Hash => 2, TokenType.Backslash => 1, TokenType.Less or TokenType.Greater or TokenType.LessEqual or TokenType.GreaterEqual or TokenType.Equals or TokenType.ArithEqual or TokenType.ArithNotEqual => 5, _ => 0 };`. `private static string OperatorFunctor(Token op)` via switch-expression mapping token type → functor string (`"+"`, `"-"`, `"*"`, `"/"`, `"//"`, `"mod"`, `"<"`, `">"`, `"=<"`, `">="`, `"="`, `"=:="`, `"=\\="`, `"#"`, `"\\"`); default arm THROWS `new CompileError($"Unknown operator: {op.Type}", op.Line, op.Column, phase: "parser")`.
- research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
- Nuance: precedence table CALIBRATED — `_parseExpression(6)` exploits 6 > 5 to stop at comparison. Functor strings are runtime-meaningful (flow into bytecode predicate names). `>=` maps to `>=` (Prolog convention, NOT `=>`). `=\\=` arith-not-equal preserved verbatim with embedded backslash.

### Construct: dart.list_parser.elements_optional_tail_pipe_right_associative_cons → `ParseList()`

- Source form: full `_parseList()` — empty `[]`, dotted-pair `[H|T]`, mixed `[X,Y,Z|T]`, with three list-completion paths each rejecting trailing `?`.
- Target decision: emit `private Term ParseList()`. Consume `[`. Empty `[]` ⇒ `new ListTerm(null, null, bracketToken.Line, bracketToken.Column)`. Else accumulate elements via `var elements = new List<Term>(); elements.Add(ParseTerm()); while (Match(TokenType.COMMA)) elements.Add(ParseTerm());`. Dispatch on optional PIPE: with PIPE ⇒ `tail = ParseTerm();`, consume `]`, build right-associative cons from tail backwards via `Term result = tail; for (int i = elements.Count - 1; i >= 0; i--) result = new ListTerm(elements[i], result, ...);`. Without PIPE ⇒ consume `]`, build cons from empty-list backwards. Reader-mark rejection (after EVERY completion path) throws `CompileError("Reader mark \"?\" can only be applied to variables, not lists", ...)`.
- research_finding_id: rf-dart-list-to-csharp-list-of-T
- Nuance: right-associative cons `[X,Y,Z] = [X|[Y|[Z|[]]]]`. Empty list `ListTerm(null, null, ...)` per ast.dart cached convention. Three explicit reader-mark error sites preserved byte-for-byte.

### Construct: dart.type_definition_lookahead.is_type_definition_via_colon_colon_eq_scan → `IsTypeOrProcDeclaration()` + `IsTypeDefinition()`

- Source form: both helpers with save-cursor `final saved = _current;` / restore `_current = saved;` pattern; `_isTypeDefinition` additionally has depth-counter LPAREN/RPAREN tracking for parameterised type-name lookahead.
- Target decision: emit `private bool IsTypeOrProcDeclaration()` and `private bool IsTypeDefinition()`. Save-and-restore via `var saved = _current;` / `_current = saved;`. For `IsTypeDefinition`, the parenthesised-type-parameter scan uses `int depth = 1;` and `while (!IsAtEnd() && depth > 0) { if (Check(TokenType.LPAREN)) depth++; if (Check(TokenType.RPAREN)) depth--; Advance(); }`. Both helpers always restore cursor before returning. Test `COLONCOLONEQ` post-restore-prep is the discriminator.
- research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
- Nuance: pure look-ahead — cursor always restored. Depth-counter terminates one iteration AFTER reaching 0 (closing `)` advanced past). Unmatched `(` at EOF returns false via the `COLONCOLONEQ` check.

### Construct: dart.type_definition_parser.name_optional_params_alt_alt_alt_dot → `ParseTypeDef()`

- Source form: `TypeDef _parseTypeDef()` with READER-or-VARIABLE type-name, optional `(typeParams)`, `::=`, `;`-separated alternatives, terminating `.`.
- Target decision: emit `private TypeDef ParseTypeDef()`. Type-name: `var typeNameToken = Check(TokenType.READER) ? Advance() : Consume(TokenType.VARIABLE, "Expected type name");`. `var typeName = typeNameToken.Type == TokenType.READER ? $"{typeNameToken.Lexeme}?" : typeNameToken.Lexeme;`. Optional `(typeParams)` consuming VARIABLE-typed names into `List<string>`. `Consume(TokenType.COLONCOLONEQ, "Expected \"::=\" in type definition");`. Accumulate alternatives via `var alternatives = new List<TypeExpr> { ParseTypeAlt() }; while (Match(TokenType.SEMICOLON)) alternatives.Add(ParseTypeAlt());`. `Consume(TokenType.DOT, "Expected \".\" after type definition");`. Return `new TypeDef(typeName, alternatives, line, column, typeParams: typeParams)`.
- research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
- Nuance: explicit dual `Foo? ::= ...` re-encodes `?` into the name string — convention type_conversion.dart decodes; DO NOT separate into struct member. `;`-separated alternatives (Prolog convention, NOT `|`).

### Construct: dart.type_alternative_parser.parallel_primary_with_trailing_question_tolerated → `ParseTypeAlt*` family

- Source form: `_parseTypeAlt`, `_parseTypeAltTerm`, `_parseTypeAltExpression`, `_parseTypeAltPrimary`, `_parseTypeAltList` — parallel grammar to the term parser tolerating trailing `?` on most term shapes.
- Target decision: emit four parallel methods. `private TypeExpr ParseTypeAlt() => TermToTypeExpr(ParseTypeAltTerm());`. `private Term ParseTypeAltTerm() => ParseTypeAltExpression();`. `private Term ParseTypeAltExpression(int minPrecedence = 0)` — Pratt loop identical to `ParseExpression` but with `Match(TokenType.QUESTION)` AFTER the loop. `private Term ParseTypeAltPrimary()` — 8-branch dispatcher mirroring `ParsePrimary` with trailing `?` tolerated/encoded on every branch, plus parameterised-type-reference disambiguation (VARIABLE/READER+LPAREN ⇒ `StructTerm(name + (isReader || trailingQ ? "?" : ""), args, ...)`). `private Term ParseTypeAltList()` — list parser allowing trailing `?` on every list-completion path. Total ~150 LOC duplication preserved verbatim — INTENTIONAL parallel grammar.
- research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
- Nuance: parallel grammar — collapsing would entangle. Cross-spec dependency on type_conversion.dart's `TermToTypeExpr` decoder; DO NOT change encoding without coordinating both sides. Capitalised + LPAREN ⇒ param-type-ref (NOT struct); lowercase + LPAREN ⇒ struct.

### Construct: dart.proc_declaration_parser.exported_imported_path_name_args_dot → `ParseProcDeclaration()`

- Source form: full `_parseProcDeclaration()` with optional `exported`/`imported`, PROCEDURE keyword, 11-way operator-or-ATOM name dispatch, `#`-separated path for imported, optional `(argTypes)`, terminating `.`.
- Target decision: emit `private ProcDecl ParseProcDeclaration()`. Sequence: (1) Optional `exported`/`imported` ATOM via two explicit checks. (2) `Consume(TokenType.PROCEDURE, "Expected \"procedure\" keyword");`. (3) Procedure name: 11-way switch-statement on `Peek().Type` accepting ATOM or LESS, GREATER, LESS_EQUAL, GREATER_EQUAL, ARITH_EQUAL, ARITH_NOT_EQUAL, GROUND_EQUAL, EQUALS, UNIV, UNIV_DECOMPOSE, ASSIGN. Default arm throws `CompileError("Expected procedure name", ...)`. (4) If `imported`, parse `#`-separated module path: collect parts via `while (Match(TokenType.HASH)) { ... parts.Add(Advance().Lexeme); }`. Last part is `name`; `modulePath` is `string.Join("#", parts.GetRange(0, parts.Count - 1))` when `parts.Count > 1`. (5) Optional `(argTypes)` via `ParseProcArgType()` comma-separated; nullary parens optional (preserved). (6) `Consume(TokenType.DOT, "Expected \".\" after procedure declaration");`. (7) Return `new ProcDecl(name, argTypes, line, column, exported: exported, imported: imported, modulePath: modulePath)`.
- research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
- Nuance: 11 operator tokens accepted as procedure names (`<`, `>`, `=<`, `>=`, `=:=`, `=\\=`, `=?=`, `=`, `=..`, `..=`, `:=`). Imported `#`-path: last segment is name, earlier segments form module path. Nullary parens optional (`procedure foo.` and `procedure foo().` both valid). Trailing-comma named-arg from Dart dropped in C# emission for broadest compatibility.

### Construct: dart.proc_arg_type_parser.primitive_qualified_typeref_with_optional_typeargs_and_mode → `ParseProcArgType()`

- Source form: full `_parseProcArgType()` with three forms — primitive `_[?]`, qualified `atom#...#TypeName[?]`, plain typeref `Var[?] [(typeArgs)] [?]`.
- Target decision: emit `private TypeExpr ParseProcArgType()`. Three forms: (1) `_[?]` ⇒ `new PrimitiveModeAlt(isInput: Match(TokenType.QUESTION), line, column)`. (2) Qualified: walk via `var pathParts = new List<string>(); while (Check(TokenType.ATOM) && _current + 1 < Tokens.Count && Tokens[(int)(_current + 1)].Type == TokenType.HASH) { pathParts.Add(Advance().Lexeme); Advance(); }` consuming alternating ATOM/HASH pairs; expect VARIABLE/READER for type-name; build `var qualifiedName = $"{string.Join("#", pathParts)}#{typeToken.Lexeme}";` and return `new TypeRef(qualifiedName, line, column, isInput: isInput)`. (3) Plain typeref: VARIABLE/READER, optional `(Type1, ...)` parsed RECURSIVELY via `ParseProcArgType()`, optional trailing `?`. Build via `new TypeRef(baseName, line, column, isInput: isInput, typeArgs: typeArgs)`. `isInput = token.Type == TokenType.Reader || Match(TokenType.Question)`. Fall-through ⇒ `throw new CompileError("Expected type in procedure argument", ...)`.
- research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
- Nuance: `isInput` is reader-mark — input-mode annotation in unification mode analysis. Recursive type-arg parsing for nested parameterised types. Qualified-name encoded as single string with embedded `#` separators in `TypeRef.name`. Trailing `?` consumed AFTER nested type-args (so `List(Number)?` assigns `?` to outer List).

### Construct: dart.relative_imports_with_show_filter → C# `using` directives

- Source form: five relative imports + one show-filter `import '../analysis/type_checker/prelude.dart' show builtinProcedures;`.
- Target decision: map relative imports to C# `using` directives on target namespaces. Prefer fully-qualified `Prelude.BuiltinProcedures` for the show-filtered symbol (no per-symbol `using` in C#).
- research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
- Nuance: trivial. C# alternative is `using static Prelude;` with reviewer-discipline to avoid leaking other Prelude members.

### Construct: dart.doc_comment_triple_slash → C# `/// <summary>...</summary>`

- Source form: `///` Dart doc comments on the class and many methods.
- Target decision: map each `///` Dart doc comment to a C# XML-doc `/// <summary>...</summary>` placed on the corresponding declaration. Trivial mechanical mapping per lexer.dart cached convention.
- research_finding_id: null
- Nuance: trivial.

### Construct: dart.line_comment_inline → C# `//` line comments

- Source form: `//` line comments throughout the file (catalogued in convspec).
- Target decision: preserve as C# `//` line comments at the same source positions for byte-identical documentation shape. Trivial mechanical mapping.
- research_finding_id: null
- Nuance: trivial.

## 3. Decomposed Task Units

- T1. Emit `class Parser` reference class with `IReadOnlyList<Token> Tokens` get-only property + `long _current` + `Clause? _pendingClause` mutable fields + ctor. — done.
- T2. Emit seven recursive-descent helpers (`Match`/`Check`/`Advance`/`Peek`/`Previous`/`IsAtEnd`/`Consume`) with `long → int` cast at `Tokens[(int)_current]`. — done.
- T3. Emit `public Program Parse()` + `private void SkipDeclarations()` using `FrozenSet<string> DeclarationKeywords` with `StringComparer.Ordinal`. — done.
- T4. Emit `public Module ParseModule()` with declaration-loop switch-on-string (`module`/`stdlib`/`mode`/`export`-error/`import`-error) + body-element-loop with `ProcDecl? pendingProcDecl` and `Dictionary<string, Procedure> seenProcedures` state machine. — done.
- T5. Emit `private void CheckContiguousClauses(IList<Procedure>)` using `Dictionary<string, Procedure>` with `StringComparer.Ordinal` and `TryGetValue` single-lookup. — done.
- T6. Emit `private string ParseModuleName()` using `string.Join(".", parts)` with DOT-rewind via `_current--`. — done.
- T7. Emit `private Procedure ParseProcedure()` with `_pendingClause` drain + four-way operator-procedure look-ahead (`:=`/`=..`/`..=`/`=`) + arity-mismatch stash-and-break. — done.
- T8. Emit `private Clause ParseClause()` with predicates-as-`List<object>` accumulation + PIPE-dispatch to Guards (with `~`-prefix-strip and `StringComparison.Ordinal`) vs body-Goals via `Cast<Goal>()`. — done.
- T9. Emit `private object ParseGoalOrGuard()` with four-way disambiguation (`~`-negation / paren-disjunction / Var-prefix-5-way / Atom-prefix / infix-comparison-fall-through). — done.
- T10. Emit `private Term GoalToTerm(object goal)` using `is Goal g` declaration pattern. — done.
- T11. Emit `private Atom ParseAtom()` with five clause-head shapes (atom-prefix + `:=`/`=..`/`..=`/`=`); UNDERSCORE-only-for-`:=` via structural rejection. — done.
- T12. Emit `private Goal ParseGoal()` with body-form additions (`#` dynamic remote, `#` static remote, `@` spawn); Module-name-with-args rejection. — done.
- T13. Emit `private Guard ParseGuard()` (dead-code helper preserved). — done.
- T14. Emit `private Term ParseTerm()` + `private Term ParseExpression(int minPrecedence = 0)` Pratt loop. — done.
- T15. Emit `private Term ParsePrimary()` 9-branch dispatcher (operator-as-functor / unary-minus / Var / Underscore / Number / String-quote-wrapped / List / Paren-or-tuple / Atom); six reader-mark rejection sites. — done.
- T16. Emit `private static bool IsOperator(Token)` + `private static int Precedence(Token)` + `private static string OperatorFunctor(Token)` using switch-expressions with stacked `or` patterns; default arm of `OperatorFunctor` throws. — done.
- T17. Emit `private Term ParseList()` with three list-completion paths each rejecting reader-mark. — done.
- T18. Emit `private bool IsTypeOrProcDeclaration()` + `private bool IsTypeDefinition()` using save-and-restore-cursor with depth-counter LPAREN/RPAREN. — done.
- T19. Emit `private TypeDef ParseTypeDef()` with READER-or-VARIABLE name (with `?` re-encoding) + optional typeParams + `;`-separated alternatives. — done.
- T20. Emit `private TypeExpr ParseTypeAlt()` + `private Term ParseTypeAltTerm()` + `private Term ParseTypeAltExpression(int minPrecedence = 0)` + `private Term ParseTypeAltPrimary()` + `private Term ParseTypeAltList()` parallel grammar with trailing `?` tolerated on every branch. — done.
- T21. Emit `private ProcDecl ParseProcDeclaration()` with optional `exported`/`imported` + PROCEDURE keyword + 11-way operator-or-ATOM name dispatch + imported `#`-path-with-`string.Join("#", ...)` + optional nullary-parens-or-args + terminating `.`. — done.
- T22. Emit `private TypeExpr ParseProcArgType()` with three forms (primitive `_[?]` / qualified `atom#...#TypeName[?]` / plain typeref with recursive typeArgs). — done.
- T23. Map five relative imports to C# `using` directives + fully-qualified `Prelude.BuiltinProcedures` for the show-filtered symbol. — done.
- T24. Map `///` Dart doc comments to C# `/// <summary>...</summary>`. — done.
- T25. Preserve `//` line comments at same source positions. — done.

## 4. Research Findings

None required. Every non-trivial construct is resolved by reuse of a previously-recorded idiom (FR-024 cache reuse): five from token.dart (rf-dart-final-field-class-to-csharp-getonly-class, rf-dart-plain-enum-to-csharp-enum, rf-dart-int-to-csharp-long-width, rf-dart-objectq-to-csharp-objectq, rf-dart-tostring-interp-to-csharp-tostring-interp), three from error.dart (rf-dart-named-default-param-to-csharp-optional-arg, rf-dart-leading-underscore-privacy-to-csharp-private, rf-dart-implements-exception-to-csharp-derive-system-exception), three from lexer.dart (rf-dart-string-indexing-to-csharp-char-indexing, rf-dart-list-to-csharp-list-of-T, rf-dart-string-interpolation-join-to-csharp-interpolation-string-join), five from ast.dart (rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal, rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture, rf-dart-named-required-and-default-params-to-csharp-positional-default, rf-dart-discriminated-nullable-pair-with-derived-predicate, rf-dart-const-empty-list-default-to-csharp-array-empty), three from glp_printer.dart (rf-dart-const-set-to-csharp-frozenset-ordinal, rf-dart-is-chain-to-csharp-switch-expression-type-pattern, rf-dart-relative-import-to-csharp-using-or-same-namespace), and two from pmt/type_table.dart (rf-dart-map-to-csharp-dictionary, rf-dart-map-lookup-to-csharp-trygetvalue).

## 5. Consistency Pass

- fixed — derived from convspec construct dart.class.stateful_recursive_descent_parser_with_final_token_list_and_int_cursors (Parser as reference class; `Tokens` get-only `IReadOnlyList<Token>`; `_current` mutable `long`; `_pendingClause` mutable nullable slot).
- fixed — derived from convspec construct dart.recursive_descent.lookahead_helpers_match_check_advance_peek_consume_previous_isatend (seven helpers preserved bit-for-bit; cast `long → int` at indexer).
- fixed — derived from convspec construct dart.parser_entry.parse_legacy_skipping_declarations_returning_program (Parse + SkipDeclarations; FrozenSet for declaration keywords).
- fixed — derived from convspec construct dart.parsemodule.declaration_dispatcher_loop_with_state_machine (ParseModule two-phase body; switch-on-string ordinal; pending-decl state machine).
- fixed — derived from convspec construct dart.contiguity_check.signature_keyed_dictionary_first_occurrence_wins (TryGetValue single-lookup; ordinal comparer; multi-line error).
- fixed — derived from convspec construct dart.module_name_parser.dot_separated_qualified_name (string.Join(".", parts); DOT-rewind via `_current--`).
- fixed — derived from convspec construct dart.procedure_aggregator.collect_clauses_with_same_functor_arity_and_pending_lookback (pending-clause drain; four operator-procedure look-aheads; arity-mismatch stash).
- fixed — derived from convspec construct dart.clause_parser.head_then_optional_guards_pipe_body_dot (List<object> predicates; PIPE dispatches Guards-vs-body; `~`-prefix-strip with StringComparison.Ordinal; Cast<Goal>()).
- fixed — derived from convspec construct dart.goal_or_guard_parser.parenthesized_disjunction_assignment_remote_negation_comparison (four-way disambiguation; three explicit negation rejections; comparison fall-through via ParseExpression(6)).
- fixed — derived from convspec construct dart.goal_to_term_helper_for_disjunction (declaration pattern `is Goal g`; sentinel `(0, 0)` position).
- fixed — derived from convspec construct dart.atom_parser.head_form_assignment_univ_decompose_unify_or_functor_args (five head shapes; UNDERSCORE-only-for-`:=` structural enforcement; rollback-on-no-operator).
- fixed — derived from convspec construct dart.goal_parser.body_form_assignment_univ_unify_remote_spawn_or_functor_args (three body-only forms added; Module-name-with-args rejection).
- fixed — derived from convspec construct dart.guard_parser.simple_functor_arglist (dead-code helper preserved for parity).
- fixed — derived from convspec construct dart.expression_pratt.precedence_climbing_min_prec_loop (Pratt loop with Precedence(op)+1 left-associativity).
- fixed — derived from convspec construct dart.expression_primary.unary_minus_variable_underscore_number_string_list_paren_atom (9-branch dispatcher; six reader-mark rejection sites; quote-wrapping for string literals; right-associative comma-tuple; unary minus → `neg(X)` symbolic).
- fixed — derived from convspec construct dart.operator_classification.is_operator_precedence_functor_tables (three private static helpers; switch-expressions with stacked `or`; default arm of OperatorFunctor throws).
- fixed — derived from convspec construct dart.list_parser.elements_optional_tail_pipe_right_associative_cons (right-associative cons; three reader-mark rejection sites).
- fixed — derived from convspec construct dart.type_definition_lookahead.is_type_definition_via_colon_colon_eq_scan (save-and-restore cursor; depth-counter LPAREN/RPAREN).
- fixed — derived from convspec construct dart.type_definition_parser.name_optional_params_alt_alt_alt_dot (READER-or-VARIABLE name with `?` re-encoding; `;`-separated alternatives).
- fixed — derived from convspec construct dart.type_alternative_parser.parallel_primary_with_trailing_question_tolerated (four parallel methods; trailing `?` tolerated; param-type-ref vs struct disambiguation).
- fixed — derived from convspec construct dart.proc_declaration_parser.exported_imported_path_name_args_dot (11-way name dispatch; imported `#`-path; nullary-parens-optional).
- fixed — derived from convspec construct dart.proc_arg_type_parser.primitive_qualified_typeref_with_optional_typeargs_and_mode (three forms; recursive typeArgs; trailing `?` after nested args).
- fixed — derived from convspec construct dart.relative_imports_with_show_filter (using directives + fully-qualified Prelude.BuiltinProcedures).
- fixed — derived from convspec construct dart.doc_comment_triple_slash (XML-doc `<summary>` mapping; trivial).
- fixed — derived from convspec construct dart.line_comment_inline (preserved at same source positions; trivial).

## 6. Escalations

None.
