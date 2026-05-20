# Conversion Spec — lib/compiler/parser.dart

> Conversion-spec artifact for lib/compiler/parser.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> This file is the largest single source unit in the codebase (1761
> lines). It implements a hand-rolled recursive-descent parser plus a
> Pratt expression sub-parser, a Yardeni-Shapiro type-declaration
> sub-parser, and a procedure-declaration sub-parser. Constructs are
> consolidated by family to avoid fragmentation (FR-011 quality bar).

```yaml
schema_version: 1
source_path: lib/compiler/parser.dart
source_sha256: d5b6f4a7c81d0dcfd0fb32be8b28f7da3d3b77dc84571a10f063188114b2e9eb
target_code_unit: lib/compiler/parser.cs
constructs:
  - construct_key: dart.class.stateful_recursive_descent_parser_with_final_token_list_and_int_cursors
    source_form: >-
      "class Parser { final List<Token> tokens; int _current = 0; Clause?
      _pendingClause; Parser(this.tokens); ... }" — one immutable input
      (the token stream), one mutable cursor (`_current`), and one
      mutable single-slot look-back buffer (`_pendingClause`, holding a
      clause that was parsed but belongs to a different procedure).
    target_decision: >-
      Emit a C# reference `class Parser` (NOT a `record`, NOT a `struct`)
      with one get-only auto-property `Tokens` of type `IReadOnlyList<Token>`
      (semantically a non-null, indexable, length-known sequence —
      preserves `final List<Token> tokens` shape), one private mutable
      `long _current = 0` cursor field, and one private mutable
      `Clause? _pendingClause = null` slot (nullable reference). The Dart
      positional-initialising-formal `this.tokens` expands to an explicit
      ctor assignment `Tokens = tokens;`. The parser is identity-and-
      mutation bound — `Parse()` / `ParseModule()` mutate `_current` while
      walking the stream and observe a single `_pendingClause` set/clear
      across calls. A record/struct would defensive-copy at every call
      boundary and silently break the look-back. `Tokens` is `IReadOnlyList<Token>`
      (not `List<Token>`) because the parser only reads — exposing the
      growable concrete type is a leak of internals (consistent with
      lexer.dart Tokenize()'s contract decision to return `List<Token>`
      from the producer side and consume read-only on the parser side).
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Mutability nuance (load-bearing): one `final` field (`tokens`) +
      two mutable fields (`_current`, `_pendingClause`); `Tokens` becomes
      a get-only auto-property, the cursor + look-back stay as mutable
      private fields. Reference-vs-value (load-bearing): Parser MUST be a
      reference class so the cursor mutation observed by `_parseClause()`
      is the SAME cursor `_parseProcedure()` re-reads on the next loop
      iteration; a `struct` would force defensive copy at every method
      call. Integer-width: `_current` is Dart `int` ⇒ C# `long` per the
      recurring width idiom (token.dart / lexer.dart family). Nullable-
      slot: Dart `Clause? _pendingClause` ⇒ C# `Clause? _pendingClause`
      with `#nullable enable` semantics; the spec relies on the runtime
      bookkeeping that a non-null pending clause is consumed (cleared to
      null) by the very next `_parseProcedure()` call (parser-state
      invariant preserved verbatim). Privacy: Dart `_`-prefix library-
      private ⇒ C# `private` (class-scoped) per rf-dart-leading-
      underscore-privacy-to-csharp-private; strictly tighter, correct
      here because the cursor and look-back are only touched by methods
      of this class.
  - construct_key: dart.recursive_descent.lookahead_helpers_match_check_advance_peek_consume_previous_isatend
    source_form: >-
      "bool _match(TokenType type) { if (_check(type)) { _advance();
      return true; } return false; }" plus "bool _check(TokenType type)
      { if (_isAtEnd()) return false; return _peek().type == type; }",
      "Token _advance() { if (!_isAtEnd()) _current++; return _previous(); }",
      "Token _peek() => tokens[_current];", "Token _previous() =>
      tokens[_current - 1];", "bool _isAtEnd() => _peek().type ==
      TokenType.EOF;", "Token _consume(TokenType type, String message)
      { if (_check(type)) return _advance(); throw CompileError(message,
      _peek().line, _peek().column, phase: 'parser'); }" — the canonical
      recursive-descent toolkit.
    target_decision: >-
      Emit each helper as a private instance method on Parser, signatures
      tightened to `TokenType` enum (NOT raw int). `Match(TokenType type)
      -> bool`, `Check(TokenType type) -> bool`, `Advance() -> Token`,
      `Peek() -> Token`, `Previous() -> Token`, `IsAtEnd() -> bool`,
      `Consume(TokenType type, string message) -> Token`. The Dart arrow-
      bodied `Token _peek() => tokens[_current];` ⇒ C# expression-bodied
      `private Token Peek() => Tokens[(int)_current];` (cast long→int
      because `IList<T>` indexer is `int`-typed — Microsoft Learn). The
      arrow-bodied `_isAtEnd() => _peek().type == TokenType.EOF;` ⇒
      `private bool IsAtEnd() => Peek().Type == TokenType.EOF;`. `_advance`
      increments `_current` (when not at EOF) THEN returns the PREVIOUS
      token (post-increment-then-fetch idiom) — preserve verbatim:
      `if (!IsAtEnd()) _current++; return Previous();`. `_consume` throws
      `CompileError` with `phase: "parser"` named arg on mismatch — keep
      the named-arg call shape per rf-dart-named-default-param-to-csharp-
      optional-arg (cached from error.dart). Method-name capitalisation
      ('_'-prefix → no prefix, leading-cap PascalCase, `private`
      modifier) per rf-dart-leading-underscore-privacy-to-csharp-private.
    idiom_id: null
    research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
    nuance: >-
      Recursive-descent toolkit nuance (load-bearing): these are the
      atomic primitives that EVERY parsing method calls thousands of
      times; their semantics MUST be preserved bit-for-bit. (a) `Match`
      is conditional-consume — advances iff the type matches; `Check` is
      pure look-ahead — never advances; conflating them silently breaks
      the parser. (b) `Advance` performs post-increment-then-return-
      previous — i.e. it returns the token JUST CONSUMED (not the new
      `Peek`), and clamps at EOF (does NOT advance past). (c) `Previous`
      reads `Tokens[_current - 1]` — undefined when `_current == 0`,
      relied on by callers that have just advanced. (d) `Peek` reads
      `Tokens[_current]` — undefined past EOF, but the EOF sentinel
      added by the lexer (lexer.dart spec) makes the position always
      readable. (e) `Consume` is `Check` + `Advance` with error reporting
      on miss — error position is `Peek()`'s line/column (the offending
      token, NOT the previously consumed token). Width nuance: `_current`
      is `long` but List/IList indexers are `int`; cast `(int)_current` at
      every indexer use. Safe because `_current` is always
      0 <= _ <= Tokens.Count and Tokens.Count is `int`. Throw vs result
      nuance: every error path is `throw new CompileError(...)` —
      preserved verbatim per rf-dart-implements-exception-to-csharp-
      derive-system-exception (cached from error.dart).
  - construct_key: dart.parser_entry.parse_legacy_skipping_declarations_returning_program
    source_form: >-
      "Program parse() { _skipDeclarations(); final procedures =
      <Procedure>[]; while (!_isAtEnd()) { procedures.add(_parseProcedure()); }
      _checkContiguousClauses(procedures); return Program(procedures, 1, 1); }"
      plus "void _skipDeclarations() { while (!_isAtEnd() && _check(TokenType.
      MINUS)) { final startPos = _current; _advance(); if (!_check(TokenType.
      ATOM)) { _current = startPos; break; } final keyword = _peek().lexeme;
      if (['module', 'stdlib', 'mode'].contains(keyword)) { while (!_isAtEnd()
      && !_check(TokenType.DOT)) _advance(); if (_check(TokenType.DOT))
      _advance(); } else { _current = startPos; break; } } }".
    target_decision: >-
      Emit `public Program Parse()` returning `Program`. Body: call
      `SkipDeclarations()`, accumulate a `var procedures = new
      List<Procedure>();` via `while (!IsAtEnd()) procedures.Add(ParseProcedure());`,
      then call `CheckContiguousClauses(procedures);` and `return new
      Program(procedures, 1, 1);` (hard-coded position 1,1 for the synthetic
      top-level program node — preserved verbatim). `SkipDeclarations()`
      mirrors the Dart loop branch-for-branch; the literal-list-contains
      check `['module', 'stdlib', 'mode'].contains(keyword)` ⇒ C# uses a
      `static readonly` ordinal `FrozenSet<string>` (preferred — .NET 8+
      Microsoft Learn `System.Collections.Frozen`) keyed by the three
      declaration keywords, OR a `switch (keyword) { case "module": case
      "stdlib": case "mode": return true; default: return false; }`
      inline. For consistency with glp_printer.dart's idiom (rf-dart-
      const-set-to-csharp-frozenset-ordinal — cached) use the FrozenSet
      static field with `StringComparer.Ordinal` to guarantee culture-
      invariant matching. Cursor rollback `_current = startPos;` ⇒
      `_current = startPos;` verbatim — both languages support direct
      cursor assignment.
    idiom_id: null
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Cursor-rollback nuance (load-bearing): `_skipDeclarations` uses
      try-then-rollback to detect "is this MINUS the start of a declaration
      or the first token of a clause body?" — saving `startPos` BEFORE the
      `Advance` and restoring on failure. This is the same look-ahead-with-
      rollback idiom that appears in `parseModule()` and `_isTypeDefinition()`.
      Direct cursor assignment is faithful in both languages — both treat
      the cursor as plain mutable state. List-contains nuance: the Dart
      `['module', 'stdlib', 'mode'].contains(keyword)` is a constructed-on-
      each-call growable list scan; the C# faithful mapping is the FrozenSet
      hoisted to a `static readonly` field (one allocation per program, O(1)
      lookup) — Microsoft Learn `FrozenSet<T>`: "provides an immutable, read-
      only set optimized for fast lookup and enumeration." Ordinal comparer
      is REQUIRED to avoid culture-sensitive equality, identical to the Dart
      `String.==` value-equality which is also code-unit ordinal. Hard-coded
      `(1, 1)` position for the synthetic Program node is preserved
      verbatim — both languages encode int literals identically.
  - construct_key: dart.parsemodule.declaration_dispatcher_loop_with_state_machine
    source_form: >-
      "Module parseModule() { ModuleDeclaration? moduleDecl; CompileMode
      compileMode = CompileMode.user; while (!_isAtEnd() && _check(TokenType.
      MINUS)) { final startPos = _current; final startLine = _peek().line;
      final startCol = _peek().column; _advance(); if (!_check(TokenType.
      ATOM)) { _current = startPos; break; } final keyword = _advance();
      switch (keyword.lexeme) { case 'module': ... case 'stdlib': ... case
      'mode': ... case 'export': throw CompileError('The -export() ...');
      case 'import': throw CompileError('The -import() ...'); default:
      _current = startPos; break; } } ... — then the type-defs / proc-
      declarations / clauses loop with its full state machine: pendingProcDecl
      tracking, builtinProcedures.contains check, imported-decl-no-clauses
      handling, type-def vs clause-head disambiguation via _isTypeDefinition,
      non-contiguous-clauses check via seenProcedures map — finally returns
      Module(declaration: moduleDecl, typeDefs: typeDefs, procDeclarations:
      procDeclarations, procedures: procedures, compileMode: compileMode,
      line: 1, column: 1)".
    target_decision: >-
      Emit `public Module ParseModule()` returning `Module`. Body in two
      phases mirroring Dart: (1) declaration loop — a `while (!IsAtEnd() &&
      Check(TokenType.MINUS))` with cursor-rollback try-pattern and a `switch
      (keyword.Lexeme) { case "module": ... case "stdlib": ... case "mode":
      ... case "export": throw new CompileError("The -export() declaration
      is no longer supported. Use 'exported procedure' instead.", startLine,
      startCol, phase: "parser"); case "import": throw new CompileError("The
      -import() declaration is no longer supported. Use 'imported procedure'
      instead.", startLine, startCol, phase: "parser"); default: _current
      = startPos; break; }`. C# switch on `string` matches by ordinal
      equality (Microsoft Learn — string switch is ordinal at the IL level
      via `string.Equals` with ordinal comparer for compile-time-constant
      cases). The literal strings ("module"/"stdlib"/"mode"/"export"/"import")
      stay as C# string literals. Compile-mode-tracking field is `var
      compileMode = CompileMode.User;` (Dart enum case `user`/`system` ⇒
      C# `User`/`System` per rf-dart-plain-enum-to-csharp-enum cached from
      token.dart). (2) The body-element loop — preserved branch-for-branch:
      a `while (!IsAtEnd())` with discriminating prefix checks (PROCEDURE
      keyword OR `exported`/`imported` atom keyword + PROCEDURE look-ahead,
      VARIABLE/READER → type-def via `IsTypeDefinition()` or clause head,
      ATOM → clause). Each branch maintains `ProcDecl? pendingProcDecl` and
      `Dictionary<string, Procedure> seenProcedures`. The pending-declaration
      flush logic (a pending decl is OK to clear without error iff it names
      a builtin or is `imported`) is preserved literally. `builtinProcedures.
      Contains(pendingSig)` ⇒ `BuiltinProcedures.Contains(pendingSig)`
      where the prelude module exposes a `static readonly FrozenSet<string>
      BuiltinProcedures` (idiom alignment with rf-dart-const-set-to-csharp-
      frozenset-ordinal). String-interpolated signature `'${proc.name}/$
      {proc.arity}'` ⇒ C# interpolated string `$"{proc.Name}/{proc.Arity}"`
      per rf-dart-tostring-interp-to-csharp-tostring-interp (cached, token.dart
      family). The terminating return constructs `new Module(declaration:
      moduleDecl, typeDefs: typeDefs, procDeclarations: procDeclarations,
      procedures: procedures, compileMode: compileMode, line: 1, column: 1)`
      — C# named-argument call syntax 1:1 with Dart per rf-dart-named-required-
      and-default-params-to-csharp-positional-default (cached, ast.dart).
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Declaration-dispatcher nuance (load-bearing): the loop is a tiny
      DFA — each iteration tries to consume `-keyword(...)` as a
      declaration; on a token that is NOT a declaration keyword it ROLLS
      BACK to the leading MINUS (so a clause whose head accidentally
      starts with `-` can be parsed). This try-rollback shape is preserved
      verbatim. Switch-on-string nuance: C# `switch (string)` is ordinal
      equality at compile-constant cases (Microsoft Learn: "When the case
      label is a string, the switch is compiled to a sequence of string
      comparisons" with ordinal semantics — the BCL's `string.Equals`
      uses ordinal comparison when both sides are compile-time-constants),
      IDENTICAL to Dart `String.==` which is also code-unit ordinal. No
      locale hazard. Pending-decl-flush nuance: the `pendingProcDecl` slot
      is consumed iff (a) a clause matches its signature, or (b) the pending
      sig is a builtin (no clauses expected), or (c) the pending decl is
      `imported`. Any other transition with a non-null pending and a
      non-matching clause throws. Builtin-vs-imported asymmetry is preserved
      literally — both Dart and C# need this disambiguation to allow a
      builtin proc to share a name with a user proc. Throw-with-named-arg
      nuance: every `throw CompileError(..., phase: 'parser')` ⇒ `throw
      new CompileError(..., phase: "parser")` per cached idiom. Module-
      constructor nuance: the final `Module(declaration: ..., typeDefs: ...,
      procDeclarations: ..., procedures: ..., compileMode: ..., line: 1,
      column: 1)` uses named-arguments throughout; preserved 1:1 in C#.
  - construct_key: dart.contiguity_check.signature_keyed_dictionary_first_occurrence_wins
    source_form: >-
      "void _checkContiguousClauses(List<Procedure> procedures) { final
      seen = <String, Procedure>{}; for (final proc in procedures) { final
      sig = '${proc.name}/${proc.arity}'; if (seen.containsKey(sig)) {
      final first = seen[sig]!; throw CompileError('Non-contiguous clauses
      for \"$sig\".\\n  First group at line ${first.line}, second group at
      line ${proc.line}.\\n  All clauses for a predicate must be together
      in the source file.', proc.line, proc.column, phase: 'parser'); }
      seen[sig] = proc; } }".
    target_decision: >-
      Emit `private void CheckContiguousClauses(IList<Procedure> procedures)`.
      Body: `var seen = new Dictionary<string, Procedure>(StringComparer.
      Ordinal);` per rf-dart-map-to-csharp-dictionary (cached, pmt/type_table.dart);
      `foreach (var proc in procedures) { var sig = $"{proc.Name}/{proc.Arity}";
      if (seen.TryGetValue(sig, out var first)) { throw new CompileError($
      "Non-contiguous clauses for \"{sig}\".\n  First group at line
      {first.Line}, second group at line {proc.Line}.\n  All clauses for a
      predicate must be together in the source file.", proc.Line, proc.Column,
      phase: "parser"); } seen[sig] = proc; }`. TryGetValue is preferred over
      ContainsKey+indexer (Microsoft Learn: "Avoid the cost of two lookups
      by using TryGetValue") — matches the rf-dart-map-lookup-to-csharp-
      trygetvalue cached idiom (pmt/type_table.dart). Ordinal comparer is
      REQUIRED for culture-invariant signature keying. Multi-line error
      message with embedded `\n` is preserved as C# verbatim escape `\n`.
      The dictionary value-retrieval `seen[sig]!` (Dart non-null assertion
      on the inner indexer) is replaced by the out-parameter pattern (no
      double-lookup, no null-check needed).
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: >-
      Non-contiguous-clause detection nuance (load-bearing): GLP requires
      all clauses for a predicate to be grouped together (the compiler
      bytecode-generation pass assumes this and produces incorrect code
      otherwise — preserved exactly because it's a load-bearing language
      invariant). The check is implemented as a first-occurrence dictionary
      keyed by `name/arity` — second occurrence ⇒ error pointing at the
      second group's line and the first group's line (good diagnostic).
      Map-lookup-vs-trygetvalue nuance: Dart `seen.containsKey(sig)` then
      `seen[sig]!` is two map lookups; C# idiomatic `TryGetValue` is one
      lookup with the value bound to an out variable. Both languages
      yield the same observable behaviour; .NET-idiomatic shape preferred
      for performance and reviewer-clarity. Ordinal-vs-default-comparer
      nuance: a `Dictionary<string, T>` without explicit comparer uses
      `EqualityComparer<string>.Default` which is `StringComparer.Ordinal`
      in .NET (Microsoft Learn) — but specifying it explicitly is a robust
      reviewer-clear default that survives BCL changes. Interpolated-
      string nuance: Dart `'${...}/${...}'` ⇒ C# `$"{...}/{...}"` — direct
      counterpart, no locale hazard for these `int` arities.
  - construct_key: dart.module_name_parser.dot_separated_qualified_name
    source_form: >-
      "String _parseModuleName() { final parts = <String>[]; parts.add(
      _consume(TokenType.ATOM, 'Expected module name').lexeme); while
      (_match(TokenType.DOT) && _check(TokenType.ATOM)) { parts.add(
      _consume(TokenType.ATOM, 'Expected module name part').lexeme); } if
      (_previous().type == TokenType.DOT && !_check(TokenType.ATOM))
      { _current--; } return parts.join('.'); }".
    target_decision: >-
      Emit `private string ParseModuleName()`. Body: accumulate via `var
      parts = new List<string>(); parts.Add(Consume(TokenType.ATOM,
      "Expected module name").Lexeme); while (Match(TokenType.DOT) &&
      Check(TokenType.ATOM)) parts.Add(Consume(TokenType.ATOM, "Expected
      module name part").Lexeme); if (Previous().Type == TokenType.DOT &&
      !Check(TokenType.ATOM)) _current--; return string.Join(".", parts);`.
      Dart `parts.join('.')` ⇒ C# `string.Join(".", parts)` (Microsoft
      Learn: "Concatenates the elements of a specified array or the members
      of a collection, using the specified separator between each element").
      The cursor decrement `_current--;` is preserved verbatim — both
      languages permit post-decrement on a mutable long/int field.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Look-ahead-with-rollback nuance (load-bearing): the `_match(DOT) &&
      _check(ATOM)` test is short-circuit (Dart `&&` and C# `&&` both
      short-circuit — Microsoft Learn `&&` operator: "the right-hand
      operand is evaluated only if necessary"); if the second test fails,
      the DOT was already consumed by `_match` and must be UN-consumed via
      `_current--` so the outer caller can see the DOT as the clause-
      terminator. This precise dance is preserved bit-for-bit in C#.
      `string.Join` nuance: identical contract to Dart `List<String>.join`
      — both insert the separator between elements, neither emits a leading
      or trailing separator. Empty-list edge case: Dart returns `''`,
      C# returns `""` — but the loop guarantees `parts` has >= 1 element
      (the first `Consume` is unconditional), so the edge case doesn't
      arise here. Cursor-decrement nuance: `_current--` on a `long` is
      well-defined and matches Dart `int` semantics (no overflow path;
      cursor is >= 1 at this point because we have already consumed at
      least the leading ATOM).
  - construct_key: dart.procedure_aggregator.collect_clauses_with_same_functor_arity_and_pending_lookback
    source_form: >-
      "Procedure _parseProcedure() { final clauses = <Clause>[]; final
      Clause firstClause; if (_pendingClause != null) { firstClause =
      _pendingClause!; _pendingClause = null; } else { firstClause =
      _parseClause(); } clauses.add(firstClause); final name = firstClause.
      head.functor; final arity = firstClause.head.arity; while (!_isAtEnd())
      { bool couldBeSameProcedure = false; if (_peek().type == TokenType.
      ATOM && _peek().lexeme == name) { couldBeSameProcedure = true; }
      else if (name == ':=' && (_peek().type == TokenType.VARIABLE || ...
      ASSIGN look-ahead)) { ... } else if (name == '=..' && ... UNIV
      look-ahead) { ... } else if (name == '..=' && ... UNIV_DECOMPOSE
      look-ahead) { ... } else if (name == '=' && ... EQUALS look-ahead)
      { ... } if (!couldBeSameProcedure) break; final clause =
      _parseClause(); if (clause.head.functor == name && clause.head.arity
      != arity) { _pendingClause = clause; break; } if (clause.head.functor
      != name) throw CompileError('Clause for ${...}/${...} found, expected
      $name/$arity', clause.line, clause.column, phase: 'parser'); clauses.
      add(clause); } return Procedure(name, arity, clauses, firstClause.
      line, firstClause.column); }".
    target_decision: >-
      Emit `private Procedure ParseProcedure()`. Body: drain
      `_pendingClause` if non-null (the carry-over slot from the previous
      `ParseProcedure()` invocation), else parse the first clause.
      Accumulate `var clauses = new List<Clause> { firstClause };` and
      capture `var name = firstClause.Head.Functor;` and `var arity =
      firstClause.Head.Arity;`. Loop: each iteration computes
      `couldBeSameProcedure` via a discriminating switch — `Peek().Type
      == TokenType.ATOM && Peek().Lexeme == name` for ordinary procedures,
      OR for the four special operator-procedures `:=`/`=..`/`..=`/`=`
      whose clauses START with a VARIABLE/READER/UNDERSCORE token (because
      the operator is in clause-head INFIX position, not prefix), look-
      ahead at `Tokens[(int)(_current + 1)].Type` to confirm the operator
      token follows. Use bounds-check `_current + 1 < Tokens.Count`
      verbatim (cast long→int at indexer). Mid-procedure arity-mismatch
      ⇒ stash the clause in `_pendingClause` and break (the next
      ParseProcedure call drains it). Functor-mismatch (different name,
      same prefix-token) ⇒ throw with interpolated message `$"Clause for
      {clause.Head.Functor}/{clause.Head.Arity} found, expected {name}/
      {arity}"`. Final `return new Procedure(name, arity, clauses,
      firstClause.Line, firstClause.Column);`.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Pending-clause look-back nuance (load-bearing): the `_pendingClause`
      slot is the parser's one-token-ahead memory across `ParseProcedure`
      calls; without it the parser would have to peek into the next
      clause's head (which requires parsing it) to decide whether the
      current procedure has ended. Preserving the SLOT and its set/clear
      sequence is critical — moving the slot to local state would force
      a deeper rewrite. Same-functor-different-arity nuance: GLP
      permits `foo/2` and `foo/3` to be distinct procedures; encountering
      a clause that mismatches arity inside the loop means "current
      procedure is finished, this clause belongs to the next" — preserved
      verbatim. Operator-procedure-clause-head-form nuance: the four
      operator procedures `:=`/`=..`/`..=`/`=` are written infix in
      clause heads (`X := Y :- ...`), so their clauses start with the
      LHS token, NOT the operator. The look-ahead at `Tokens[_current + 1]`
      confirms the next-but-one token IS the operator. UNDERSCORE is
      allowed for `:=` only (anonymous-variable assignments like `_ :=
      X / 0` for abort clauses) — preserved exactly. Look-ahead bounds-
      check nuance: `_current + 1 < tokens.length` ⇒ `_current + 1 <
      Tokens.Count` — both languages 0-based indexing, `.length` ⇒
      `.Count` (IList semantics).
  - construct_key: dart.clause_parser.head_then_optional_guards_pipe_body_dot
    source_form: >-
      "Clause _parseClause() { final head = _parseAtom(); List<Guard>?
      guards; List<Goal>? body; if (_match(TokenType.IMPLIES)) { final
      predicates = <dynamic>[]; predicates.add(_parseGoalOrGuard()); while
      (_match(TokenType.COMMA)) predicates.add(_parseGoalOrGuard()); if
      (_match(TokenType.PIPE)) { guards = predicates.map((g) { final
      isNegated = g.functor.startsWith('~'); final actualFunctor =
      isNegated ? g.functor.substring(1) : g.functor; return Guard(actualFunctor,
      g.args, g.line, g.column, negated: isNegated); }).toList(); body =
      <Goal>[]; body.add(_parseGoal()); while (_match(TokenType.COMMA))
      body.add(_parseGoal()); } else { body = predicates.cast<Goal>(); } }
      _consume(TokenType.DOT, 'Expected \".\" at end of clause'); return
      Clause(head, guards: guards, body: body, line: head.line, column:
      head.column); }".
    target_decision: >-
      Emit `private Clause ParseClause()`. Parse the head via `var head =
      ParseAtom();`. If `Match(TokenType.IMPLIES)` matches `:-`, accumulate
      a `var predicates = new List<object>();` (mixed Goal-vs-Guard, the
      Dart `<dynamic>` list — see below for the `dynamic` mapping). Loop
      collects `predicates.Add(ParseGoalOrGuard())` separated by COMMA.
      If `Match(TokenType.PIPE)` matches `|`, the accumulated predicates
      are GUARDS (everything before `|`) and the remaining tokens are
      BODY goals: convert each predicate via `var guards = predicates.
      Select(g => { var goal = (Goal)g; bool isNegated = goal.Functor.
      StartsWith("~", StringComparison.Ordinal); var actualFunctor =
      isNegated ? goal.Functor.Substring(1) : goal.Functor; return new
      Guard(actualFunctor, goal.Args, goal.Line, goal.Column, negated:
      isNegated); }).ToList();`. The negation-stripping (a `~` prefix on
      the functor is the convention for guard negation, recorded as a
      separate `negated` flag on the Guard) is preserved verbatim. If no
      `|`, the accumulated predicates are body goals: `body = predicates.
      Cast<Goal>().ToList();` — Dart `List.cast<Goal>()` ⇒ C# LINQ
      `Cast<Goal>().ToList()` (Microsoft Learn: "Casts the elements of an
      IEnumerable to the specified type" — throws InvalidCastException on
      mismatch, matching Dart's CastError). Terminate with `Consume(
      TokenType.DOT, "Expected \".\" at end of clause");`. Return `new
      Clause(head, guards: guards, body: body, line: head.Line, column:
      head.Column);` with named-argument call syntax matching Dart.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Guard-vs-body discriminator nuance (load-bearing): the `|` token is
      the GLP guard-vs-body separator (`Head :- Guards | Body.`). If `|`
      is absent, EVERYTHING after `:-` is body (no guards). Preserved
      exactly. Negation-encoding nuance: the parser uses a `~` prefix on
      the functor as an in-band signal that the predicate is negated
      (e.g. `~(X > Y)` parses as Goal with functor `~>`); the negation
      is stripped and recorded on the `Guard.negated` flag during conversion.
      This bit-twiddling-via-functor-prefix is preserved verbatim because
      changing it would propagate into `_parseGoalOrGuard` and several
      callers. `dynamic`-list nuance: Dart `<dynamic>[]` holds heterogeneous
      Goal/Guard values; C# uses `List<object>` plus an explicit `(Goal)g`
      cast at the consumption site. NOT `dynamic` keyword (which would
      enable late-binding via the DLR — Microsoft Learn `dynamic`: "skip
      compile-time type checking" — a wholly different semantic; we want
      runtime cast, which is what the Dart code is actually doing). StartsWith-
      with-ordinal nuance: `goal.Functor.StartsWith("~")` defaults to
      culture-sensitive comparison in .NET pre-7 (Microsoft Learn `String.
      StartsWith(String)`: "Determines whether the beginning of this string
      instance matches the specified string"); we MUST pass `StringComparison.
      Ordinal` to match Dart's code-unit semantics — silent default would
      change behaviour under locales like tr-TR (Turkish dotless-I). Same
      rule applies everywhere in this file that does `.startsWith` /
      `.contains` / `.endsWith` on a string. Cast<T>-throws nuance: `predicates.
      Cast<Goal>().ToList()` throws `InvalidCastException` if any element is
      not a Goal — matches Dart `cast<Goal>()` semantics.
  - construct_key: dart.goal_or_guard_parser.parenthesized_disjunction_assignment_remote_negation_comparison
    source_form: >-
      "dynamic _parseGoalOrGuard() { bool negated = false; ... if (_match
      (TokenType.TILDE)) { negated = true; ...; if (_check(TokenType.TILDE))
      throw CompileError('Double negation ~~G is not allowed', ...); } if
      (_check(TokenType.LPAREN)) { ... parenthesized disjunction or
      negated single goal ... } if (_check(TokenType.VARIABLE) || _check(
      TokenType.READER)) { ... look-ahead for ASSIGN/UNIV/UNIV_DECOMPOSE/
      EQUALS/HASH and dispatch to assignment / univ / decompose / unify /
      remote ... } if (_check(TokenType.ATOM)) { final functorToken =
      _consume(TokenType.ATOM, ...); final args = ...; ... HASH ⇒
      RemoteGoal; ... EQUALS ⇒ Goal('=', [leftTerm, rightTerm], ...); ...
      negation-restriction guards on disjunction / remote / unification;
      ... AT ⇒ SpawnGoal; ... } final left = _parseExpression(6); ...
      comparison-operator dispatch (LESS/GREATER/LESS_EQUAL/GREATER_EQUAL/
      EQUALS/ARITH_EQUAL/ARITH_NOT_EQUAL/GROUND_EQUAL) ⇒ Goal(opFunctor,
      [left, right], ...); ... else throw CompileError('Expected predicate
      name or comparison', ...); }".
    target_decision: >-
      Emit `private object ParseGoalOrGuard()` (return type `object`
      because the Dart `dynamic` here means "either a Goal or a Guard
      depending on context" — see the clause parser which casts). Body
      preserves the four-way disambiguation: (1) Optional negation `~`
      prefix — captured as `var negated = false;` flipped on `Match(
      TokenType.TILDE)`; double-negation `~~` is FORBIDDEN by language
      design (preserved verbatim: throws CompileError "Double negation
      ~~G is not allowed"). (2) Parenthesized expression — disambiguates
      between `(Goal)` (single, allows negation on outer) and `(Goal1 ;
      Goal2)` (disjunction, REJECTS negation). The disjunction case
      builds `new Goal(";", new[] { firstTerm, secondTerm }, startToken.
      Line, startToken.Column)` via `GoalToTerm(...)` conversion (see
      next construct). (3) Variable/Reader prefix — five-way ASSIGN /
      UNIV / UNIV_DECOMPOSE / EQUALS / HASH dispatch by look-ahead at
      `Tokens[(int)(_current + 1)].Type`; HASH triggers `RemoteGoal`
      construction (REJECTS negation). (4) Atom prefix — parse functor +
      optional arg list, check HASH for static remote, check EQUALS for
      unification, check AT for spawn annotation; if no special suffix,
      return as `Goal`. (5) Fall-through — try `ParseExpression(6)` for
      an infix comparison (`X < Y`, `X mod P =:= 0`, etc.); on success
      build `Goal(opLexeme, new[] { left, right }, ...)`. On total failure
      throw CompileError "Expected predicate name or comparison". The
      negation-encoding-via-functor-prefix (`var functor = negated ?
      "~" + functorToken.Lexeme : functorToken.Lexeme;`) is preserved
      verbatim. C# does NOT use `dynamic` — `object` is the semantic
      equivalent of Dart `dynamic` for this purpose (returning
      heterogeneous values, with the caller responsible for the type
      check / cast). Microsoft Learn `dynamic`: "the type is bypassed at
      compile-time; that is, the compiler performs no type checking"
      — DIFFERENT from Dart `dynamic` here, which the caller IMMEDIATELY
      casts/uses via duck-typed property access (`g.functor`, `g.args`).
      To preserve the duck-typed access pattern in C#, expose `Functor`/
      `Args`/`Line`/`Column` as a SHARED INTERFACE `IGoalOrGuard` (or
      explicit cast each call site) — strongly-typed alternative is
      preferred to keep the conversion semantically tight.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Polymorphic-return nuance (load-bearing): Dart `dynamic` here is
      used as a true sum type — the function can return EITHER `Goal`
      OR `Guard` depending on which branch fires (e.g. the variable-prefix
      `Goal(':=', ...)` branch always returns a Goal; the negation-stripped-
      atom-prefix returns a Goal whose functor encodes negation; the
      comparison branch returns a Goal). The callers immediately downcast.
      Faithful mapping in C# is `object` (the equivalent of `dynamic`'s
      duck-typed semantics) — or a shared interface `IGoalOrGuard` exposing
      the four common members (Functor/Args/Line/Column). C# `dynamic`
      keyword is REJECTED: Microsoft Learn warns dynamic-typed expressions
      bypass compile-time checking entirely, defeating the conversion's
      tightness goal. Negation-restriction nuance: the parser EXPLICITLY
      rejects `~(A ; B)` (negation of disjunction), `~(A # B)` (negation
      of remote goal), and `~(X = Y)` (negation of unification) with
      specific error messages — preserved verbatim, all three are
      semantic-clarity rules of GLP. Look-ahead-with-five-way-dispatch
      nuance: the Variable/Reader prefix branches at
      `Tokens[_current + 1].Type` on the five token types ASSIGN/UNIV/
      UNIV_DECOMPOSE/EQUALS/HASH; each case advances TWICE (consume
      variable + consume operator) and builds the appropriate Goal/
      RemoteGoal. Preserved branch-for-branch. Spawn-annotation `@`
      nuance: ATOM-prefix goals can be followed by `@Agent` to construct
      a `SpawnGoal` (an agent-spawn annotation in maGLP). Preserved
      verbatim. Comparison-infix nuance: `_parseExpression(6)` parses
      with min-precedence 6 (just above arithmetic, see precedence table)
      so comparison operators don't recurse into the expression — they
      stop the expression at the LEFT side, then the explicit check
      promotes to a Goal. The exhaustive list of comparison tokens
      (LESS/GREATER/LESS_EQUAL/GREATER_EQUAL/EQUALS/ARITH_EQUAL/ARITH_
      NOT_EQUAL/GROUND_EQUAL) is preserved literally as a chained
      `Check(...) || Check(...)` — DO NOT collapse into a HashSet probe
      (the OR-chain is the more direct C# idiom and reads identically).
  - construct_key: dart.goal_to_term_helper_for_disjunction
    source_form: >-
      "Term _goalToTerm(dynamic goal) { if (goal is Goal) { return
      StructTerm(goal.functor, goal.args, goal.line, goal.column); }
      throw CompileError('Expected goal', 0, 0, phase: 'parser'); }".
    target_decision: >-
      Emit `private Term GoalToTerm(object goal)`. Body: `if (goal is
      Goal g) return new StructTerm(g.Functor, g.Args, g.Line, g.Column);
      throw new CompileError("Expected goal", 0, 0, phase: "parser");`.
      Dart `is` test with property access ⇒ C# `is` pattern with capture
      (declaration pattern) — Microsoft Learn `is` operator: "Tests
      whether the run-time type of an expression result is compatible
      with a given type" plus "introduces a variable that the test
      succeeds binds the value to." Direct counterpart of Dart's
      promote-on-`is`. The hard-coded `(0, 0)` position is a "should
      never happen" sentinel preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal
    nuance: >-
      Type-test nuance (load-bearing): Dart `if (goal is Goal)` AUTO-
      PROMOTES `goal` to type `Goal` inside the if-branch (Dart's flow
      analysis); C# requires the explicit declaration pattern `if (goal
      is Goal g)` to obtain the typed reference. Both languages reject
      the conversion if `goal` is null (Dart's `is` is false for null
      against a non-nullable type; C# `is` is false for null in the
      declaration-pattern form) — matching behaviour. Sentinel-position
      nuance: `(0, 0)` is used for synthetic / never-thrown errors and
      is reviewer-clear; preserved verbatim. Error-message preserved
      byte-for-byte ("Expected goal") for log/test stability.
  - construct_key: dart.atom_parser.head_form_assignment_univ_decompose_unify_or_functor_args
    source_form: >-
      "Atom _parseAtom() { if (_check(TokenType.VARIABLE) || _check(
      TokenType.READER) || _check(TokenType.UNDERSCORE)) { final
      varToken = _advance(); final isReader = varToken.type == TokenType.
      READER; final isUnderscore = varToken.type == TokenType.UNDERSCORE;
      if (_match(TokenType.ASSIGN)) { ... ':='(LHS, Expr) ... } else if
      (_match(TokenType.UNIV)) { ... } else if (_match(TokenType.UNIV_
      DECOMPOSE)) { ... } else if (_match(TokenType.EQUALS)) { ... } else
      { _current--; } } final functorToken = _consume(TokenType.ATOM,
      'Expected predicate name'); final args = <Term>[]; if (_match(
      TokenType.LPAREN)) { ... arg list ... } if (_match(TokenType.UNIV))
      { ... } if (_match(TokenType.EQUALS)) { ... } return Atom(functorToken.
      lexeme, args, functorToken.line, functorToken.column); }".
    target_decision: >-
      Emit `private Atom ParseAtom()` returning `Atom` (the clause-head
      AST node — distinct from the lexer's ATOM token which is a string
      atom). Body: (1) Variable/Reader/Underscore head — the four-way
      operator-as-head-functor dispatch (`:=`, `=..`, `..=`, `=`),
      capturing `isReader`/`isUnderscore` flags and constructing either
      a `VarTerm` or `UnderscoreTerm` for the LHS. The UnderscoreTerm
      branch is permitted ONLY for `:=` (per GLP grammar: anonymous-
      variable assignment is the abort-clause idiom). If none of the four
      operators follows the variable, ROLL BACK (`_current--;`) so the
      atom-parse path can re-read it (this covers cases where the
      capitalised-token is actually a regular predicate name in an
      unusual position — defensive parsing). (2) Atom-prefix head —
      consume the ATOM, optional `(args)` parsed as `_parseTerm()` comma-
      separated, optional trailing `=..` or `=` (also recognised in clause
      heads — produces Atom(':=' / '=..' / '=', [lhs, rhs], ...)). Use
      named-argument call syntax for the Atom constructor. Note the
      asymmetry vs `_parseGoal`: `_parseAtom` is for clause HEADS and
      thus does NOT recognise `#` (remote) or `@` (spawn) — those are
      goal-body-only.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Clause-head-form nuance (load-bearing): GLP clause heads have five
      shapes — (a) ordinary `foo(...)` atom-prefix, (b) `Var := Expr`
      assignment, (c) `Var =.. List` univ, (d) `Var ..= List` univ-
      decompose, (e) `Var = Term` unification, plus (f) `_ := Expr` for
      abort clauses. The Atom AST node encodes (b)-(f) by using the
      operator as functor (`:=`, `=..`, `..=`, `=`). Preserved verbatim.
      Rollback-on-no-operator nuance: if the leading variable is followed
      by NONE of the four operators, we `_current--` to un-consume it
      and fall through to the atom-prefix path — this allows misclassified
      tokens to be handled by the broader path. Underscore-only-for-`:=`
      nuance: the parser enforces (silently — by structure, not by
      explicit check) that UNDERSCORE is only valid before ASSIGN; if
      UNDERSCORE is followed by UNIV/UNIV_DECOMPOSE/EQUALS, the rollback
      restores the cursor and the atom-prefix path throws because
      `_consume(TokenType.ATOM, ...)` fails. Asymmetric goal-vs-atom-
      head set nuance: `_parseAtom` does NOT recognise `#` (remote) or
      `@` (spawn) — both are body-only annotations. Preserved exactly.
  - construct_key: dart.goal_parser.body_form_assignment_univ_unify_remote_spawn_or_functor_args
    source_form: >-
      "Goal _parseGoal() { if (_check(TokenType.VARIABLE) || _check(
      TokenType.READER)) { final varToken = _advance(); final isReader =
      varToken.type == TokenType.READER; if (_match(TokenType.HASH)) {
      ... RemoteGoal(varTerm, innerGoal, ...) ... } else if (_match(
      TokenType.ASSIGN)) { ... Goal(':=', [varTerm, expr], ...) ... }
      else if (_match(TokenType.UNIV)) { ... } else if (_match(TokenType.
      UNIV_DECOMPOSE)) { ... } else if (_match(TokenType.EQUALS)) { ... }
      else throw CompileError('Expected predicate name or assignment, got
      variable \"$lexeme\"', ...); } final functorToken = _consume(
      TokenType.ATOM, 'Expected predicate name'); final args = ...; if
      (_match(TokenType.HASH)) { ... static remote ... } if (_match(
      TokenType.UNIV)) { ... } final goal = Goal(functorToken.lexeme,
      args, ...); if (_match(TokenType.AT)) { final agentToken = _consume(
      TokenType.ATOM, ...); return SpawnGoal(goal, agentToken.lexeme,
      ...); } return goal; }".
    target_decision: >-
      Emit `private Goal ParseGoal()` returning `Goal`. Mirror `_parseAtom`
      shape but accepting BODY-only annotations: variable-prefix dispatches
      on HASH (dynamic remote: `Var # Goal`), ASSIGN (`:=`), UNIV (`=..`),
      UNIV_DECOMPOSE (`..=`), EQUALS (`=`); none-of-the-above throws
      with `$"Expected predicate name or assignment, got variable
      \"{varToken.Lexeme}\""`. Atom-prefix path additionally recognises
      `# Goal` (static remote: `atom # Goal`), trailing `=..`, and
      trailing `@Agent` (spawn annotation). Errors throw with `phase:
      "parser"` named arg per cached idiom. Module-name-with-args is
      explicitly rejected (`if (args.Count > 0) throw new CompileError($
      "Module name cannot have arguments: {functorToken.Lexeme}", ...)`)
      — preserved verbatim.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Goal-form nuance (load-bearing): goals (clause-body predicates)
      add three forms over atoms — `Var # Goal` (dynamic remote, module
      name is a runtime-resolved variable), `atom # Goal` (static remote,
      module name is a compile-time atom), and `Goal @ Agent` (spawn
      annotation, agent name is a compile-time atom). These produce
      `RemoteGoal` and `SpawnGoal` AST nodes respectively. Module-name-
      with-args nuance: the static remote `mod#foo(X)` is parsed by
      reading `foo` as functor with args `[X]`, then seeing `#` — but
      ONLY if there are NO arguments after `foo` (because `mod#foo(X)`
      means "module `mod` applied to goal `foo(X)`", not "goal `mod`
      with args `[foo(X)]`"). The explicit `args.Count > 0` rejection
      preserves this disambiguation; mis-tokenised `mod(X)#foo` errors
      with the explicit message. Throw-message preservation nuance: every
      error message string is preserved byte-for-byte for log/test
      stability, with `$"..."` interpolation replacing Dart `'...$x...'`.
  - construct_key: dart.guard_parser.simple_functor_arglist
    source_form: >-
      "Guard _parseGuard() { final functorToken = _consume(TokenType.ATOM,
      'Expected guard predicate name'); final args = <Term>[]; if (_match
      (TokenType.LPAREN)) { ... arg list ... } return Guard(functorToken.
      lexeme, args, functorToken.line, functorToken.column); }".
    target_decision: >-
      Emit `private Guard ParseGuard()` returning `Guard`. Body: `var
      functorToken = Consume(TokenType.ATOM, "Expected guard predicate
      name"); var args = new List<Term>(); if (Match(TokenType.LPAREN))
      { if (!Check(TokenType.RPAREN)) { args.Add(ParseTerm()); while
      (Match(TokenType.COMMA)) args.Add(ParseTerm()); } Consume(TokenType.
      RPAREN, "Expected \")\" after arguments"); } return new Guard(
      functorToken.Lexeme, args, functorToken.Line, functorToken.Column);`.
      The simpler shape (no operator dispatch, no remote, no spawn) is
      preserved — guards are syntactically the simplest predicate form
      because they cannot contain compound operations.
    idiom_id: null
    research_finding_id: rf-dart-list-to-csharp-list-of-T
    nuance: >-
      Note that this entry point is the WRITTEN-but-not-CALLED variant
      — the in-use guard parsing happens via `_parseGoalOrGuard` then
      conversion in `_parseClause`. Preserved for parity even though
      effectively dead code (a `// TODO: remove?` is implied). Marking
      it private static if practical — but it captures no state and
      could safely move to a helper class. The simpler shape is intentional.
  - construct_key: dart.expression_pratt.precedence_climbing_min_prec_loop
    source_form: >-
      "Term _parseTerm() { return _parseExpression(); } Term _parseExpression
      ([int minPrecedence = 0]) { var left = _parsePrimary(); while (_isOperator
      (_peek()) && _precedence(_peek()) >= minPrecedence) { final op =
      _advance(); final right = _parseExpression(_precedence(op) + 1); left =
      StructTerm(_operatorFunctor(op), [left, right], op.line, op.column); }
      return left; }".
    target_decision: >-
      Emit `private Term ParseTerm() => ParseExpression();` (expression-
      body delegate). Emit `private Term ParseExpression(int minPrecedence =
      0)`. Body: `var left = ParsePrimary(); while (IsOperator(Peek()) &&
      Precedence(Peek()) >= minPrecedence) { var op = Advance(); var right =
      ParseExpression(Precedence(op) + 1); left = new StructTerm(OperatorFunctor
      (op), new List<Term> { left, right }, op.Line, op.Column); } return
      left;`. The Dart optional positional argument `[int minPrecedence = 0]`
      ⇒ C# default-valued positional parameter `int minPrecedence = 0`
      (Microsoft Learn: "Named and Optional Arguments"). Left-associative
      operators are encoded by recursing with `minPrecedence = Precedence(op)
      + 1` (the +1 makes a left-recursive call refuse to consume an equally-
      precedented operator, so the LEFT side stays as already-built tree).
      Preserved exactly.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Pratt-parser nuance (load-bearing): this is the canonical precedence-
      climbing algorithm (Vaughan Pratt 1973). Left-associativity is encoded
      by `Precedence(op) + 1`; right-associativity would use `Precedence(op)`
      (not used in this file — all infix operators are left-associative).
      The precedence table is hard-coded in `_precedence` (see next entry).
      Pratt recursion depth is O(distinct precedence levels) per nested
      infix, not O(input length), so stack-depth is bounded — safe to
      mirror in C# without iterative rewrite. Operator-functor mapping
      (token type → string functor) is hard-coded in `_operatorFunctor`.
      Min-precedence argument nuance: `_parseExpression(6)` is invoked
      from `_parseGoalOrGuard` to stop the expression at comparison
      operators (which have precedence 5 < 6) — preserving this exact
      threshold is load-bearing because dropping it would let the
      expression swallow the comparison operator and break the guard-
      vs-arithmetic split.
  - construct_key: dart.expression_primary.unary_minus_variable_underscore_number_string_list_paren_atom
    source_form: >-
      "Term _parsePrimary() { if (_check(TokenType.PLUS) || _check(TokenType.
      MINUS) || ... ) { if (_current + 1 < tokens.length && tokens[_current
      + 1].type == TokenType.LPAREN) { ... operator-as-functor: +(X, Y) ... } }
      if (_match(TokenType.MINUS)) { ... unary minus: -X becomes neg(X) ... }
      if (_check(TokenType.VARIABLE) || _check(TokenType.READER)) { ... Var
      [:= Expr] ... } if (_match(TokenType.UNDERSCORE)) { ... _ [?] ... } if
      (_check(TokenType.NUMBER)) { ... reject trailing ? ... ConstTerm(literal,
      ...) ... } if (_check(TokenType.STRING)) { ... reject trailing ? ...
      ConstTerm('\"$literal\"', ...) ... } if (_check(TokenType.LBRACKET))
      return _parseList(); if (_match(TokenType.LPAREN)) { ... (Expr) or
      tuple (A,B,C) builds right-associative ','(A, ','(B, C)) ... } if
      (_check(TokenType.ATOM)) { ... functor[(args)] or constant atom; reject
      trailing ? ... } throw CompileError('Expected term, got ${_peek().type}',
      ...); }".
    target_decision: >-
      Emit `private Term ParsePrimary()`. The body is a 9-branch dispatcher
      preserving Dart shape: (1) Operator-as-functor (`+(X,Y)`, `-(X,Y)`,
      `*`, `/`, `//`, `mod`): look-ahead at `Tokens[_current + 1].Type ==
      TokenType.LPAREN`; if so, consume the operator token as functor,
      consume `(`, recurse for args, return `new StructTerm(functorToken.
      Lexeme, args, ...)`. (2) Unary minus: `-X` ⇒ `new StructTerm("neg",
      new List<Term> { operand }, ...)`. (3) Variable/Reader, optional
      `:= Expr` ⇒ `new StructTerm(":=", new List<Term> { varTerm, expr },
      ...)` or plain `VarTerm(...)`. (4) Underscore, optional `?` ⇒ `new
      UnderscoreTerm(..., isReader: Match(TokenType.QUESTION))`. (5)
      Number: reject trailing `?` (reader-mark valid only on variables);
      build `new ConstTerm(token.Literal, ...)`. (6) String: reject
      trailing `?`; build `new ConstTerm($"\"{token.Literal}\"", ...)`
      preserving the quote-wrapping convention for downstream type-
      checker string-detection (per ast.dart spec construct `dart.ast_
      leaf.const_term_polymorphic_value_with_branching_string_quoting_
      tostring`). (7) List: delegate to `ParseList()`. (8) Parenthesized
      expression or tuple: parse first via `ParseExpression()`; on COMMA,
      keep parsing and build right-associative comma-structure `new
      StructTerm(",", new List<Term> { terms[i], result }, ...)`. (9)
      Atom: with-args ⇒ StructTerm; bare ⇒ ConstTerm; reject trailing
      `?`. Fall-through ⇒ throw CompileError with `$"Expected term, got
      {Peek().Type}"`.
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal
    nuance: >-
      Term-primary nuance (load-bearing): 9 branches, each a distinct
      shape that builds a different AST node. Preserved exactly because
      every reordering is observable (e.g. the operator-as-functor
      branch MUST come BEFORE the unary-minus branch so that `-(X, Y)`
      is parsed as the struct `-(X, Y)` rather than as `neg((X, Y))`).
      Quote-wrapping convention nuance: string literals are stored in
      the ConstTerm.value with explicit surrounding quotes (`"..."`)
      so the type-checker can distinguish string from atom at term-
      inspection time — preserved verbatim per ast.dart spec (which
      ALREADY records this idiom). Reader-mark-restriction nuance:
      `?` is valid ONLY on variables (including underscore). The
      parser explicitly rejects `?` on number, string, list, parenthesized
      expression, structure, and constant-atom — six error sites with
      tailored messages preserved byte-for-byte. Right-associative-
      comma-tuple nuance: `(A, B, C)` builds `,(A, ,(B, C))` (right-
      associative), NOT `,(,(A, B), C)` — this convention is shared
      with Prolog and is consumed by downstream passes that walk the
      `,` structure as a cons-list. Preserved exactly. Unary-minus-
      builds-neg nuance: `-X` produces `StructTerm("neg", [X], ...)`,
      NOT a sign-flipped numeric — the negation is symbolic so the
      partial-evaluator can constant-fold or leave it for runtime. C#
      conversion preserves this.
  - construct_key: dart.operator_classification.is_operator_precedence_functor_tables
    source_form: >-
      "bool _isOperator(Token token) { return token.type == TokenType.PLUS
      || token.type == TokenType.MINUS || ... TokenType.BACKSLASH; } int
      _precedence(Token op) { switch (op.type) { case TokenType.STAR: case
      TokenType.SLASH: case TokenType.SLASH_SLASH: case TokenType.MOD:
      return 20; case TokenType.PLUS: case TokenType.MINUS: return 10;
      case TokenType.HASH: return 2; case TokenType.BACKSLASH: return 1;
      case TokenType.LESS: ... ARITH_NOT_EQUAL: return 5; default: return
      0; } } String _operatorFunctor(Token op) { switch (op.type) { case
      TokenType.PLUS: return '+'; ... case TokenType.BACKSLASH: return
      '\\\\'; default: throw CompileError('Unknown operator: ${op.type}',
      ...); } }".
    target_decision: >-
      Emit three small private static helpers: `private static bool
      IsOperator(Token token)` returning an OR-chain over `Type ==` checks
      (the 14 token types: PLUS, MINUS, STAR, SLASH, SLASH_SLASH, MOD,
      LESS, GREATER, LESS_EQUAL, GREATER_EQUAL, EQUALS, ARITH_EQUAL,
      ARITH_NOT_EQUAL, HASH, BACKSLASH). `private static int Precedence
      (Token op)` returning a switch-expression over `op.Type` with case-
      stacking for equal-precedence groups (Microsoft Learn: switch
      expressions support stacked patterns via `or`). Modern C# switch-
      expression syntax: `op.Type switch { TokenType.Star or TokenType.
      Slash or TokenType.SlashSlash or TokenType.Mod => 20, TokenType.
      Plus or TokenType.Minus => 10, TokenType.Hash => 2, TokenType.
      Backslash => 1, TokenType.Less or TokenType.Greater or TokenType.
      LessEqual or TokenType.GreaterEqual or TokenType.Equals or
      TokenType.ArithEqual or TokenType.ArithNotEqual => 5, _ => 0, };`.
      `private static string OperatorFunctor(Token op)` switch-expression
      mapping each token type to its functor string; default arm THROWS
      `new CompileError($"Unknown operator: {op.Type}", op.Line, op.
      Column, phase: "parser");` (NOT returns a sentinel — preserved
      verbatim). The functor strings are: `"+"`, `"-"`, `"*"`, `"/"`,
      `"//"`, `"mod"`, `"<"`, `">"`, `"=<"`, `">="`, `"="`, `"=:="`,
      `"=\\="`, `"#"`, `"\\"`. Note `>=` maps to `>=` (not `=>`) — Prolog
      convention; preserved exactly. `=\\=` is the arith-not-equal functor
      with embedded backslash, preserved exactly.
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Precedence-table nuance (load-bearing): the precedences are 20
      (multiplicative), 10 (additive), 5 (comparison), 2 (module `#`),
      1 (diff-list `\`), 0 (none). These are CALIBRATED so that `_parseExpression
      (6)` (used by `_parseGoalOrGuard` to stop at comparisons) leaves
      comparison operators (prec 5) for the goal-level handler. Changing
      any number changes the parse. Preserved exactly. Switch-expression-
      vs-statement nuance: modern C# 8+ switch expressions are preferred
      over switch statements for value-returning dispatch (Microsoft
      Learn: "switch expression provides switch-like semantics in an
      expression context"). Default-arm-throws nuance: `OperatorFunctor`
      with an unmatched token throws CompileError; preserved verbatim
      because this is a programming error (the parser invoked the
      function with a non-operator token, which it should never do).
      Functor-strings-are-runtime-meaningful nuance: these strings flow
      through the AST and end up as predicate names in the bytecode —
      they MUST be exact (byte-for-byte) matches with the runtime's
      operator-handler table. C# string-literal syntax matches Dart
      character-for-character for these ASCII-only operator names.
  - construct_key: dart.list_parser.elements_optional_tail_pipe_right_associative_cons
    source_form: >-
      "Term _parseList() { final bracketToken = _consume(TokenType.
      LBRACKET, 'Expected \"[\"'); if (_match(TokenType.RBRACKET)) {
      ... reject trailing ? ... return ListTerm(null, null, ...); } final
      elements = <Term>[]; Term? tail; elements.add(_parseTerm()); while
      (_match(TokenType.COMMA)) elements.add(_parseTerm()); if (_match(
      TokenType.PIPE)) { tail = _parseTerm(); _consume(TokenType.RBRACKET,
      'Expected \"]\" after list tail'); ... reject trailing ? ... Term
      result = tail; for (int i = elements.length - 1; i >= 0; i--) {
      result = ListTerm(elements[i], result, ...); } return result; }
      _consume(TokenType.RBRACKET, 'Expected \"]\" after list elements');
      ... reject trailing ? ... Term result = ListTerm(null, null, ...);
      for (int i = elements.length - 1; i >= 0; i--) { result = ListTerm
      (elements[i], result, ...); } return result; }".
    target_decision: >-
      Emit `private Term ParseList()`. Body mirrors Dart branch-for-branch:
      consume `[`, special-case empty list `[]` returning `new ListTerm
      (null, null, bracketToken.Line, bracketToken.Column)`, otherwise
      accumulate elements via `var elements = new List<Term>(); elements.
      Add(ParseTerm()); while (Match(TokenType.COMMA)) elements.Add(
      ParseTerm());` then dispatch on optional PIPE: with PIPE ⇒ tail =
      `ParseTerm()`, consume `]`, build right-associative cons from tail
      backwards via `Term result = tail; for (int i = elements.Count - 1;
      i >= 0; i--) result = new ListTerm(elements[i], result, ...);`;
      without PIPE ⇒ consume `]`, build right-associative cons from
      empty-list backwards via `Term result = new ListTerm(null, null,
      ...); for (...) result = new ListTerm(elements[i], result, ...);`.
      Reader-mark rejection (after EVERY of the three list-completion
      paths) throws CompileError "Reader mark \"?\" can only be applied
      to variables, not lists" verbatim.
    idiom_id: null
    research_finding_id: rf-dart-list-to-csharp-list-of-T
    nuance: >-
      List-construction nuance (load-bearing): GLP/Prolog lists are
      right-associative cons cells — `[X, Y, Z]` ⇒ `[X|[Y|[Z|[]]]]` and
      `[X, Y, Z | T]` ⇒ `[X|[Y|[Z|T]]]`. The fold-from-tail-backwards
      loop preserves this convention exactly. Empty list nuance: `ListTerm
      (null, null, ...)` represents `[]` (no head, no tail); both
      languages use the same null-discriminated representation per
      ast.dart spec (cached idiom `dart.ast_leaf.discriminated_nullable_
      pair_with_derived_predicate`). For-loop downward iteration nuance:
      `for (int i = elements.length - 1; i >= 0; i--)` is identical in
      both languages; cast-free because `elements.Count` returns `int`
      and the loop index is `int`. Reader-mark-rejection nuance: the
      `?` (reader-mark) is invalid on lists (lists are structural, not
      a reader/writer mode); preserved as three explicit error sites
      with byte-for-byte message preservation.
  - construct_key: dart.type_definition_lookahead.is_type_definition_via_colon_colon_eq_scan
    source_form: >-
      "bool _isTypeOrProcDeclaration() { if (_check(TokenType.PROCEDURE))
      return true; if (_check(TokenType.VARIABLE) || _check(TokenType.
      READER)) { final saved = _current; _advance(); final isTypeDef =
      _check(TokenType.COLONCOLONEQ); _current = saved; return isTypeDef;
      } return false; } bool _isTypeDefinition() { if (_check(TokenType.
      VARIABLE) || _check(TokenType.READER)) { final saved = _current;
      _advance(); if (_check(TokenType.LPAREN)) { _advance(); int depth =
      1; while (!_isAtEnd() && depth > 0) { if (_check(TokenType.LPAREN))
      depth++; if (_check(TokenType.RPAREN)) depth--; _advance(); } } final
      isTypeDef = _check(TokenType.COLONCOLONEQ); _current = saved; return
      isTypeDef; } return false; }".
    target_decision: >-
      Emit two private helpers: `private bool IsTypeOrProcDeclaration()`
      and `private bool IsTypeDefinition()`. The save-cursor-then-restore
      pattern uses `var saved = _current;` and `_current = saved;` —
      direct cursor manipulation, no wrapping. For `IsTypeDefinition`,
      the parenthesised-type-parameter scan uses depth-counter `int depth
      = 1;` and a loop `while (!IsAtEnd() && depth > 0) { if (Check(
      TokenType.LPAREN)) depth++; if (Check(TokenType.RPAREN)) depth--;
      Advance(); }` — preserved verbatim. Both helpers are pure look-
      ahead — they ALWAYS restore the cursor before returning. Test the
      `COLONCOLONEQ` (the `::=` token) post-restore-prep ⇒ the discriminator.
    idiom_id: null
    research_finding_id: rf-dart-string-indexing-to-csharp-char-indexing
    nuance: >-
      Save-and-restore-cursor nuance (load-bearing): both helpers
      explicitly restore `_current = saved` before returning, even on
      the early-return path (the depth-loop falls out without explicit
      restore for the inner scan, but the OUTER `_current = saved`
      undoes everything). Preserving this exact pattern is critical
      because the helpers are LOOK-AHEAD (`_isTypeDefinition` does not
      consume anything observable). Depth-counter nuance: the loop
      counts `(` / `)` nesting to skip past type parameters like
      `TypeName(X, Y)` before the `::=`. Note the check order: it tests
      LPAREN first (so depth++ runs), then RPAREN (depth--), then
      ALWAYS advances — meaning the loop terminates one iteration AFTER
      depth reaches 0 (the closing `)` has been advanced past). Preserved
      exactly. Edge case: an unmatched `(` at end-of-tokens exits via
      `!IsAtEnd()` — but the helpers DO NOT throw; they just return false
      from the COLONCOLONEQ check that follows (because EOF is not
      COLONCOLONEQ). Preserved verbatim.
  - construct_key: dart.type_definition_parser.name_optional_params_alt_alt_alt_dot
    source_form: >-
      "TypeDef _parseTypeDef() { final typeNameToken = _check(TokenType.
      READER) ? _advance() : _consume(TokenType.VARIABLE, 'Expected type
      name'); final typeName = typeNameToken.type == TokenType.READER ?
      '${typeNameToken.lexeme}?' : typeNameToken.lexeme; ... type
      parameters (X, Y, ...) ... _consume(TokenType.COLONCOLONEQ, ...);
      final alternatives = <TypeExpr>[]; alternatives.add(_parseTypeAlt
      ()); while (_match(TokenType.SEMICOLON)) alternatives.add(_parseTypeAlt
      ()); _consume(TokenType.DOT, 'Expected \".\" after type definition');
      return TypeDef(typeName, alternatives, line, column, typeParams:
      typeParams); }".
    target_decision: >-
      Emit `private TypeDef ParseTypeDef()`. Body: type name accepts
      either VARIABLE or READER (explicit dual form `Foo? ::= ...`) —
      `var typeNameToken = Check(TokenType.READER) ? Advance() : Consume(
      TokenType.VARIABLE, "Expected type name"); var typeName = typeNameToken.
      Type == TokenType.READER ? $"{typeNameToken.Lexeme}?" : typeNameToken.
      Lexeme;`. The `?` suffix encoding in the typeName string is the
      convention type_conversion.dart decodes; preserved verbatim.
      Optional type parameters `(X, Y, ...)` parsed via `if (Match(
      TokenType.LPAREN)) { ... }` consuming VARIABLE-typed parameter names
      into `List<string>`. Consume `::=`, then accumulate alternatives
      via `var alternatives = new List<TypeExpr> { ParseTypeAlt() }; while
      (Match(TokenType.SEMICOLON)) alternatives.Add(ParseTypeAlt());`.
      Consume `.`. Construct `new TypeDef(typeName, alternatives, line,
      column, typeParams: typeParams)` with named-arg syntax.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Dual-type-name encoding nuance (load-bearing): explicit dual
      definitions write `Foo? ::= alt.` (capitalised type name with
      reader-mark suffix); the parser receives the name as a READER
      token (which is "Foo" with reader-mark flag set in the lexer)
      and re-encodes the `?` into the string ("Foo?"). This name-with-
      `?`-suffix convention is what `type_conversion.dart` then decodes
      to set the dual-mode flag on the type. Preserved verbatim — DO
      NOT separate the flag into a struct member, because the type-
      checker depends on the string-encoded form. Semicolon-separated-
      alternatives nuance: GLP type alternatives are `;`-separated
      (Prolog convention), not `|`. Preserved exactly.
  - construct_key: dart.type_alternative_parser.parallel_primary_with_trailing_question_tolerated
    source_form: >-
      "TypeExpr _parseTypeAlt() { final term = _parseTypeAltTerm(); return
      termToTypeExpr(term); } Term _parseTypeAltTerm() { return _parseType
      AltExpression(); } Term _parseTypeAltExpression([int minPrecedence
      = 0]) { var left = _parseTypeAltPrimary(); while (_isOperator(_peek
      ()) && _precedence(_peek()) >= minPrecedence) { final op = _advance
      (); final right = _parseTypeAltExpression(_precedence(op) + 1); left
      = StructTerm(_operatorFunctor(op), [left, right], op.line, op.column);
      } _match(TokenType.QUESTION); return left; } Term _parseTypeAltPrimary
      () { ... 8-branch primary that allows trailing ? on each branch ... }
      Term _parseTypeAltList() { ... list parser that allows trailing ? ... }".
    target_decision: >-
      Emit four parallel methods mirroring the term-parsing pipeline but
      with one twist: trailing `?` (QUESTION token) is TOLERATED and
      consumed on most term shapes. `private TypeExpr ParseTypeAlt() =>
      TermToTypeExpr(ParseTypeAltTerm());` (the `TermToTypeExpr` import is
      already in scope — see type_conversion.dart spec). `private Term
      ParseTypeAltTerm() => ParseTypeAltExpression();`. `private Term
      ParseTypeAltExpression(int minPrecedence = 0)` — Pratt loop
      identical to `ParseExpression` but with `Match(TokenType.QUESTION)`
      AFTER the loop (tolerated trailing `?` on the whole expression).
      `private Term ParseTypeAltPrimary()` — 8-branch dispatcher
      mirroring `ParsePrimary` BUT (a) ALLOWS trailing `?` on every
      branch (consumed and either discarded for value contexts OR
      encoded into the type name for parameterized-type-reference and
      structure contexts), (b) handles the special parameterised-type-
      reference case: capitalised name followed by `(` is encoded as
      `StructTerm(name + (isReader || trailingQ ? "?" : ""), args, ...)`
      so the type-conversion stage can decode mode. `private Term
      ParseTypeAltList()` — list parser identical to `ParseList` but
      allowing trailing `?` on every list-completion path. Total
      duplication of ~150 LOC is preserved verbatim — the type-alt
      parser is a parallel grammar, not a delta on the term parser
      (changing one would silently desync the other).
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Parallel-parser nuance (load-bearing): `_parseTypeAlt*` is a near-
      duplicate of `_parseTerm`/`_parseExpression`/`_parsePrimary`/
      `_parseList`, with one specific difference — trailing `?` is
      TOLERATED in type-alt context (because type alternatives may use
      `?` to mark dual-mode types: `Channel? ::= ch(Stream?, Stream)?.`).
      The duplication is INTENTIONAL — collapsing it to a parameterised
      single parser would entangle the two grammars and is rejected.
      Preserved exactly. Reader-encoding nuance: `(isReader || trailingQ)
      ? "{name}?" : name` encodes the reader-mark into the StructTerm's
      functor name; type_conversion.dart's `termToTypeExpr` decodes
      this. Cross-spec dependency on type_conversion.dart's contract is
      preserved — DO NOT change the encoding without coordinating both
      sides. Param-type-reference vs struct disambiguation nuance:
      capitalised name (VARIABLE/READER token) followed by `(` is a
      PARAMETERISED TYPE REFERENCE (e.g. `List(Number)`), NOT a struct
      — encoded with the trailing-`?` convention. Lowercase name (ATOM)
      followed by `(` is a STRUCT — encoded as a plain `StructTerm`.
      Preserved exactly. Trailing-`?` on parenthesized-expression nuance:
      `(X + Y)?` is tolerated by `_parseTypeAltExpression` at the end of
      the operator loop (because the parenthesized form returns directly
      and the `?` is consumed by the outer match) — preserved verbatim.
  - construct_key: dart.proc_declaration_parser.exported_imported_path_name_args_dot
    source_form: >-
      "ProcDecl _parseProcDeclaration() { bool exported = false; bool
      imported = false; ...; if (_check(TokenType.ATOM) && _peek().lexeme
      == 'exported') { _advance(); exported = true; } else if (_check(
      TokenType.ATOM) && _peek().lexeme == 'imported') { _advance();
      imported = true; } _consume(TokenType.PROCEDURE, 'Expected \"procedure
      \" keyword'); ...; String? modulePath; Token nameToken; if (_check(
      TokenType.ATOM)) nameToken = _advance(); else if (_check(TokenType.
      LESS)) ... — eleven branches over operator tokens — ... else throw
      CompileError('Expected procedure name', ...); var name = nameToken.
      lexeme; if (imported) { final parts = <String>[name]; while (_match
      (TokenType.HASH)) { if (!_check(TokenType.ATOM)) throw CompileError
      ('Expected module path component or procedure name after \"#\"',
      ...); parts.add(_advance().lexeme); } name = parts.last; if (parts.
      length > 1) modulePath = parts.sublist(0, parts.length - 1).join(
      '#'); } final argTypes = <TypeExpr>[]; if (_match(TokenType.LPAREN))
      { if (!_check(TokenType.RPAREN)) { argTypes.add(_parseProcArgType());
      while (_match(TokenType.COMMA)) argTypes.add(_parseProcArgType()); }
      _consume(TokenType.RPAREN, 'Expected \")\" after procedure arguments');
      } _consume(TokenType.DOT, 'Expected \".\" after procedure declaration');
      return ProcDecl(name, argTypes, line, column, exported: exported,
      imported: imported, modulePath: modulePath); }".
    target_decision: >-
      Emit `private ProcDecl ParseProcDeclaration()`. Body sequence:
      (1) Optional `exported`/`imported` modifier ATOM consumed via two
      explicit checks. (2) Consume PROCEDURE keyword. (3) Procedure name —
      an 11-way dispatch accepting either ATOM or any of LESS, GREATER,
      LESS_EQUAL, GREATER_EQUAL, ARITH_EQUAL, ARITH_NOT_EQUAL,
      GROUND_EQUAL, EQUALS, UNIV, UNIV_DECOMPOSE, ASSIGN (the 10 operator-
      token-names allowed as procedure names — preserved literally).
      Implementation in C# uses a switch-statement on `Peek().Type` with
      cases for each accepted type, default arm throws. (4) If `imported`,
      parse an optional `#`-separated module path: `modulePath` ends up
      as the join-with-`#` of all parts except the last; the last part
      is the actual procedure name. Preserved literally with `string.
      Join("#", parts.GetRange(0, parts.Count - 1))` (Microsoft Learn
      `List<T>.GetRange` — "Creates a shallow copy of a range of elements
      in the source"). (5) Optional `(argTypes)`: each arg parsed via
      `ParseProcArgType()`; the `)` is consumed even on the no-args case
      via the comma-loop pattern. Nullary procedures may omit the parens
      entirely (preserved — `procedure play_introduction.` is valid).
      (6) Consume `.`. (7) Construct via named-arg call `new ProcDecl(
      name, argTypes, line, column, exported: exported, imported:
      imported, modulePath: modulePath)`.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      Operator-as-procedure-name nuance (load-bearing): GLP allows
      operators (`<`, `>`, `=<`, `>=`, `=:=`, `=\\=`, `=?=`, `=`, `=..`,
      `..=`, `:=`) to be declared as procedure names — this is how the
      runtime exposes those as user-overridable predicates. Preserved
      literally; the 11-branch dispatch is the canonical shape. Imported-
      procedure-path nuance: imported procedures may carry a `#`-separated
      module path (e.g. `imported procedure ui#actors#render`); the LAST
      `#`-segment is the procedure name, the earlier segments form the
      module path. Preserved exactly. Nullary-procedure nuance: a procedure
      with no arguments may omit the parens entirely; the LPAREN-optional
      check handles both `procedure foo.` and `procedure foo().` — both
      yield empty argTypes. Preserved. Throw-with-trailing-comma-named-
      arg nuance: the Dart source uses a trailing comma after the named
      arg (`phase: 'parser',`) — C# allows this as a trailing comma in
      method calls (C# 12+ permits it; older versions need no trailing
      comma) — recommend dropping the trailing comma in the C# emission
      for broadest compatibility.
  - construct_key: dart.proc_arg_type_parser.primitive_qualified_typeref_with_optional_typeargs_and_mode
    source_form: >-
      "TypeExpr _parseProcArgType() { ... primitive: _ or _? ⇒ PrimitiveModeAlt
      ... qualified: atom # ... # TypeName[?] ⇒ TypeRef('mod#path#Name', ...,
      isInput: ...) ... type reference with optional type arguments and
      optional mode: VARIABLE/READER, optional (Type1, Type2, ...), optional
      trailing ? ⇒ TypeRef(baseName, ..., isInput: isInput, typeArgs:
      typeArgs); ... else throw CompileError('Expected type in procedure
      argument', ...) }".
    target_decision: >-
      Emit `private TypeExpr ParseProcArgType()`. Body dispatches on
      three forms: (1) Primitive `_` (with optional `?`) ⇒ `new
      PrimitiveModeAlt(isInput: Match(TokenType.QUESTION), line, column)`.
      (2) Qualified type reference `atom # atom # ... # TypeName[?]` —
      walked via `var pathParts = new List<string>(); while (Check(
      TokenType.ATOM) && _current + 1 < Tokens.Count && Tokens[(int)(
      _current + 1)].Type == TokenType.HASH) { pathParts.Add(Advance().
      Lexeme); Advance(); }` consuming alternating ATOM/HASH pairs;
      then expect VARIABLE or READER for the type-name; build qualified-
      name string via `var qualifiedName = $"{string.Join("#",
      pathParts)}#{typeToken.Lexeme}";` and return `new TypeRef(
      qualifiedName, line, column, isInput: isInput)`. (3) Plain type
      reference with optional type arguments: VARIABLE/READER, optional
      `(Type1, ...)` parsed RECURSIVELY via `ParseProcArgType()`,
      optional trailing `?`. Build via `new TypeRef(baseName, line,
      column, isInput: isInput, typeArgs: typeArgs)`. Fall-through throws
      `CompileError("Expected type in procedure argument", ...)`. The
      `isInput` flag is `token.Type == TokenType.Reader || Match(
      TokenType.Question)` — either the lexer-detected reader-mark OR an
      explicit trailing `?`.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Mode-detection nuance (load-bearing): the `isInput` flag (Dart's
      naming) corresponds to the reader-mark `?` — a type-mode annotation
      indicating that the parameter is a reader (input) rather than a
      writer (output) in the unification mode-analysis. Preserved literally.
      Recursive-type-arg nuance: `_parseProcArgType()` recursively calls
      itself to parse nested parameterised types (`List(Number)`, `Map(
      String, List(Int))`) — preserved verbatim. Qualified-type-name
      encoding nuance: `social#agent#AgentChannel` is encoded as a single
      string with embedded `#` separators in the TypeRef.name slot —
      consumed by downstream module-resolution; preserved exactly.
      Trailing-`?`-on-type-name nuance: the trailing `?` is consumed
      AFTER any nested type arguments — so `List(Number)?` correctly
      assigns the `?` to the outer List, not to Number. Preserved
      exactly by ordering: parse type args first, THEN match `?`.
  - construct_key: dart.relative_imports_with_show_filter
    source_form: >-
      "import 'token.dart'; import 'ast.dart'; import 'error.dart';
      import '../analysis/type_checker/type_ast.dart'; import '../analysis/
      type_checker/type_conversion.dart'; import '../analysis/type_checker/
      prelude.dart' show builtinProcedures;" — five relative imports
      with one show-filter to restrict the prelude import to a single
      symbol.
    target_decision: >-
      Map Dart relative imports to C# `using` directives on the target
      namespaces (or rely on same-namespace co-location, depending on
      the project layout chosen at codegen time). All target types
      (`Token`/`TokenType`, AST nodes, `CompileError`, `TypeExpr`/`TypeRef`/
      `PrimitiveModeAlt`/`TypeDef`/`termToTypeExpr`, `builtinProcedures`)
      come from the compiler-pass namespaces. The Dart `show
      builtinProcedures` restriction has no direct C# equivalent —
      C# uses namespace-and-class-level access control; the restriction
      becomes "reference `Prelude.BuiltinProcedures` by fully-qualified
      name" or "`using static Prelude;` with reviewer-discipline to
      avoid other Prelude members". Apply rf-dart-relative-import-to-
      csharp-using-or-same-namespace (cached, glp_printer.dart).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: trivial
    trivial: false
  - construct_key: dart.doc_comment_triple_slash
    source_form: >-
      "/// Parser for GLP source code", "/// Parse tokens into an AST
      (legacy method, skips declarations)", "/// Check that all clauses
      for each procedure are contiguous in the source.", "/// Parse a
      type definition: TypeName ::= alt ; alt ; alt.", etc. — Dart
      triple-slash doc comments on the class and many methods.
    target_decision: >-
      Map each `///` Dart doc comment to a C# XML-doc `/// <summary>...
      </summary>` placed on the corresponding declaration. Multi-line
      doc comments wrap the body in `<summary>...</summary>` with the
      original line breaks preserved as XML-doc line breaks. Trivial
      mechanical mapping per cached convention (lexer.dart spec).
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: dart.line_comment_inline
    source_form: >-
      "// Skip any module declarations at the start", "// Check for
      non-contiguous clauses (same name/arity appearing multiple times)",
      "// Parse declarations at the start of the file", "// consume '-'",
      "// Back up, not a declaration", "// -stdlib. is deprecated —
      treated as -mode(system).", "// -mode(user). or -mode(system).
      declaration", "// Unknown declaration, back up to the '-'", "//
      Parse type definitions, procedure declarations, and clauses in
      order.", "// Track pending procedure declaration (waiting for its
      first clause)", "// Track which procedures we've seen clauses for
      (signature -> first Procedure)", "// Check for procedure declaration",
      "// Procedure declaration (possibly exported or imported)", "//
      Check if the pending declaration is for a builtin or imported (no
      clauses needed)", "// Builtin or imported - clear pending without
      error", "// Imported procedures are declaration-only — no clauses
      expected", "// Might be a type definition (TypeName ::= ...) or a
      clause head", "// Look ahead to see if this is a type definition
      (has ::=)", "// Type definition", "// It's a clause - parse the
      procedure", "// Check if this matches pending declaration", "// This
      clause matches the pending declaration - good", "// Pending was a
      builtin (no clauses needed) - clear it", "// Check for non-
      contiguous clauses", "// Clause starting with atom (procedure
      name)", "// Unexpected token", "// Check for dangling procedure
      declaration at end of file", "// Use pending clause if available,
      otherwise parse first clause", "// Parse additional clauses with
      same functor/arity", "// Special case: := clauses start with
      VARIABLE, not ATOM", "// Same predicate name", "// := clauses start
      with variable or underscore", "// Look ahead to see if it's followed
      by :=", "// =.. clauses start with variable or underscore", "// ..=
      clauses start with variable or underscore", "// = clauses start
      with variable or underscore", "// If functor matches but arity
      differs, this is a different procedure", "// Store it as pending
      and break", "// Verify same functor (arity already checked above
      for same-name case)", "// Check for :- (clause with guards/body)",
      "// Parse everything before | as guards (or body if no |)", "//
      Check for | separator", "// Everything before | were guards -
      convert Goal to Guard", "// Detect negated guards (functor starts
      with ~)", "// Parse body after |", "// No | separator, so everything
      was body goals", "// Convert a Goal to a Term representation (for
      disjunction)", "// Atom: functor(arg1, arg2, ...) or Var := Expr or
      Var =.. Expr (for clause heads)", "// Check for := or =.. pattern",
      "// Parse as ':='(Var, Expr) or ':='(_, Expr)", "// Parse as '=..'
      (Var, Expr)", "// Parse as '..='(Var, Expr)", "// Parse as '='(Var,
      Term) - unification", "// Not an assignment - put variable back by
      rewinding", "// Check if this is followed by =..", "// Convert the
      already-parsed atom to a StructTerm", "// Check if this is followed
      by =", "// Goal: same as Atom, or assignment (Var := Expr) or univ
      (Var =.. Expr)", "// Also handles remote goals: Module # Goal", "//
      Check for assignment or univ: Var := Expr or Var =.. Expr", "// Also
      check for dynamic remote goal: Var # Goal", "// Check for dynamic
      remote goal: Var # Goal (e.g., M # factorial(5, R))", "// Parse as
      ':='(Var, Expr)", "// Not an assignment or univ - this is an error
      in goal position", "// Check for static remote goal: Module # Goal
      (e.g., math # factorial(5, R))", "// Module name cannot have
      arguments", "// Check for spawn annotation: Goal@AgentId", "//
      Guard: same as Goal but marked as guard", "// Term: variable,
      structure, list, constant, underscore, tuple, or expression", "//
      Try to parse as expression (handles arithmetic operators)", "//
      Expression parsing with precedence (Pratt parsing)", "// This
      handles arithmetic operators with proper precedence", "// Primary
      expression: variable, number, string, list, structure, parenthesized,
      unary minus", "// Operator as functor (for type definitions like
      Exp ::= +(Exp?, Exp?))", "// Must check BEFORE unary minus so
      -(X,Y) is parsed as struct, not neg((X,Y))", "// Look ahead: if
      followed by (, treat as functor", "// Otherwise fall through - will
      be handled as unary minus or infix operator", "// Unary minus: -X
      becomes neg(X)", "// Variable or Reader - check for := assignment",
      "// Check for := assignment (Var := Expr)", "// Underscore
      (anonymous variable) - can have reader mark: _ or _?", "// Number",
      "// Check for invalid reader mark on number", "// String - preserve
      quotes for type checking string detection", "// Wrap in quotes so
      type checker can distinguish strings from atoms", "// List", "//
      Parenthesized expression - could be tuple (A, B) or single term (A)
      or arithmetic (A + B)", "// Parse first term (which may be an
      expression)", "// Check for comma - indicates tuple/conjunction",
      "// Build right-associative tuple: (A, B, C) = ','(A, ','(B, C))",
      "// Single parenthesized expression - return it", "// Structure or
      Constant Atom", "// Structure with arguments", "// Check for
      invalid reader mark on structure", "// Constant atom - check for
      invalid reader mark", "// Check if token is an arithmetic operator,
      # (module operator), or \\ (difference list)", "// Get operator
      precedence", "// Multiplicative", "// Additive", "// Module
      operator (very low, so M # foo(X,Y) parses correctly)", "//
      Difference list operator (lowest, so [H|T]\\T parses correctly)",
      "// Comparison (lower than arithmetic)", "// Get operator functor
      name for AST", "// List: [], [H|T], [X], [X,Y,Z], [X,Y,Z|T]", "//
      Empty list []", "// Check for invalid reader mark on list", "//
      Parse elements", "// Parse remaining elements and check for tail",
      "// Check for tail syntax [H|T] or [X,Y|T]", "// Build right-
      associative list", "// Helper methods", "// ===
      Yardeni-Shapiro Type Declaration Parser Methods === ", "/// Check
      if we're at a type definition or procedure declaration", "//
      procedure keyword", "// TypeName ::= ... (type names are
      capitalized, tokenized as VARIABLE)", "// Look ahead for ::=", "//
      consume type name", "// restore position", "/// Check if we're at a
      type definition", "// Look ahead for ::=, skipping optional type
      parameters (X, Y, ...)", "// consume (", "/// Parse a type
      definition", "// For READER tokens (e.g., Channel?), append '?' to
      the name", "// This supports explicit dual type definitions", "//
      Parse optional type parameters: (X, Y, ...)", "// Parse
      alternatives separated by ;", "/// Parse a single type alternative
      using unified term parsing.", "/// For explicit dual definitions
      like `Channel? ::= ch(Stream?, Stream)?.`,", "/// the trailing `?`
      on the structure is allowed and consumed. The duality", "/// is
      captured in the type name (Channel?), so the trailing `?` is", "///
      documentation that confirms the definition is for the dual form.",
      "/// Parse a term in type alternative context.", "/// Similar to
      _parseTerm() but allows trailing `?` on structures.", "/// Parse
      expression in type alternative context.", "/// Handles operators
      like \\ for difference lists.", "// Check for trailing ? on the
      whole expression (for explicit duals)", "// This is allowed in
      type definitions and simply consumed", "/// Parse primary term in
      type alternative context.", "/// Allows trailing `?` on structures
      (for explicit dual definitions).", "// Parameterized type
      reference in type body: TypeName(Arg1, Arg2, ...)", "// Uppercase
      names followed by ( are parameterized type refs, not structs.", "//
      Encode reader mode in functor name for type_conversion to decode.",
      "// Variable or Reader (simple, non-parameterized)", "// Allow
      trailing ? on structure in type definitions", "// Allow trailing ?
      on parenthesized expression", "// Allow trailing ? on structure in
      type definitions (for explicit duals)", "/// Parse list in type
      alternative context.", "/// Allows trailing ? on lists (for
      explicit duals).", "// Allow trailing ? on empty list in type
      definitions", "// Allow trailing ? on list in type definitions",
      "/// Parse a procedure declaration", "/// or: exported procedure
      name(Type?, Type).", "/// or: imported procedure
      [path#]name(Type?, Type).", "// Check for 'exported' or 'imported'
      keyword before 'procedure'", "// Parse procedure name, possibly
      with module path for imported procedures.", "// For imported:
      'social#agent' → modulePath='social', name='agent'", "//
      'ui#actors#render' → modulePath='ui#actors', name='render'", "//
      'merge' → modulePath=null, name='merge'", "// Procedure name can
      be atom or operator (<, >, =<, >=, =:=, =\\=, =?=, =)", "// For
      imported procedures, parse #-separated path", "// The last
      component is the procedure name, everything before is the module
      path", "// Next token should be an atom (next path component or
      procedure name)", "// Last part is the procedure name, rest is
      the module path", "// Parentheses are optional for nullary
      procedures", "// procedure play_introduction.    (valid - nullary)",
      "// procedure play_introduction().  (valid - nullary with explicit
      parens)", "// procedure double(Number?, Number). (valid - with
      args)", "// Parse argument types if not empty", "// If no LPAREN,
      argTypes remains empty (nullary procedure)", "/// Parse a procedure
      argument type", "/// or qualified: mod#TypeName, mod#TypeName?",
      "// Primitive: _ or _?", "// Qualified type reference: atom #
      TypeName or atom # TypeName?", "// e.g., social#AgentChannel,
      social#AgentChannel?", "// Collect path: atom # atom # ... #
      TypeName", "// consume atom", "// consume #", "// Now parse the
      final type name (must be VARIABLE or READER)", "// Type reference
      with optional type arguments and optional mode", "// Parse
      optional type arguments: (Type1, Type2, ...)", "// recursive —
      supports nested parameterized types", "// _parseProcRefList,
      _parseProcRef, _parseAtomList removed in Phase 1.", "// These were
      only used for -export([...]) and -import([...]) syntax." — inline
      `//` line comments throughout the file.
    target_decision: >-
      Preserve as C# `//` line comments at the same source positions for
      byte-identical documentation shape. Trivial mechanical mapping.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "class Parser (reference type, NOT record, NOT struct)"
  - "  property: IReadOnlyList<Token> Tokens { get; } (initialised from ctor; Dart `final List<Token> tokens` → C# get-only auto-property)"
  - "  private long _current = 0 (mutable cursor)"
  - "  private Clause? _pendingClause = null (mutable single-slot look-back across ParseProcedure calls)"
  - "  ctor: Parser(IReadOnlyList<Token> tokens) — assigns Tokens = tokens"
  - "  public Program Parse() — legacy entry point: SkipDeclarations + ParseProcedure loop + CheckContiguousClauses; returns Program(procedures, 1, 1)"
  - "  public Module ParseModule() — full entry point: declaration loop (-module / -stdlib / -mode / -export-error / -import-error) + body-element loop (procDecl / typeDef / clause) with pending-decl state machine + non-contig check + dangling-decl-at-EOF check; returns Module(declaration, typeDefs, procDeclarations, procedures, compileMode, line=1, column=1)"
  - "  private void SkipDeclarations() — skip leading -module/-stdlib/-mode declarations for legacy Parse()"
  - "  private string ParseModuleName() — hierarchical name parser: ATOM (DOT ATOM)*; joined with '.'"
  - "  private void CheckContiguousClauses(IList<Procedure>) — Dictionary<string,Procedure> first-occurrence check; non-contig ⇒ CompileError"
  - "  private Procedure ParseProcedure() — drain _pendingClause OR parse first clause; loop collecting same-name-same-arity clauses; arity-mismatch ⇒ stash pending and break"
  - "  private Clause ParseClause() — Head [:- (Predicate (,Predicate)*) [| Goal (,Goal)*]] DOT; pre-PIPE predicates become Guards (with negation-prefix stripping), post-PIPE become Goals"
  - "  private object ParseGoalOrGuard() — six-way dispatcher: ~negation / (disjunction) / Var(:=|=..|..=|=|#) / atom(args)[#|@|=] / infix comparison; returns boxed Goal-or-Guard"
  - "  private Term GoalToTerm(object goal) — wrap a Goal as StructTerm for disjunction encoding"
  - "  private Atom ParseAtom() — clause-head form: (Var|_)(:=|=..|..=|=) Expr OR atom(args)[=..|=]"
  - "  private Goal ParseGoal() — clause-body form: Var(#|:=|=..|..=|=) Expr OR atom(args)[#|@|=]; Module-name-with-args rejected"
  - "  private Guard ParseGuard() — simpler guard parser (functor + optional args); effectively dead code but preserved"
  - "  private Term ParseTerm() — delegates to ParseExpression(); arrow-bodied"
  - "  private Term ParseExpression(int minPrecedence = 0) — Pratt precedence-climbing loop; left-associative via Precedence(op)+1"
  - "  private Term ParsePrimary() — 9-branch primary: operator-as-functor / unary-minus / Var[:= Expr] / Underscore[?] / Number / String(quote-wrapped) / List / (Expr | tuple) / Atom[args]; rejects `?` on non-variable forms"
  - "  private static bool IsOperator(Token token) — OR-chain check against 14 operator token types"
  - "  private static int Precedence(Token op) — switch-expression: STAR/SLASH/SLASH_SLASH/MOD=20, PLUS/MINUS=10, comparison-group=5, HASH=2, BACKSLASH=1, default=0"
  - "  private static string OperatorFunctor(Token op) — switch-expression mapping token type → functor string; default arm throws CompileError"
  - "  private Term ParseList() — [] OR [elements] OR [elements|tail]; right-associative cons; rejects trailing `?` on three completion paths"
  - "  private bool IsTypeOrProcDeclaration() — pure look-ahead: PROCEDURE keyword OR VARIABLE/READER followed by ::="
  - "  private bool IsTypeDefinition() — pure look-ahead: VARIABLE/READER optionally followed by (parenthesised params) then ::=; uses depth-counter LPAREN/RPAREN tracking"
  - "  private TypeDef ParseTypeDef() — typeName[?] (typeParams) ::= alt (; alt)* .; READER-token type-name re-encoded with '?' suffix into string"
  - "  private TypeExpr ParseTypeAlt() — delegates to ParseTypeAltTerm + termToTypeExpr"
  - "  private Term ParseTypeAltTerm() — delegates to ParseTypeAltExpression"
  - "  private Term ParseTypeAltExpression(int minPrecedence = 0) — Pratt loop with trailing `?` tolerated"
  - "  private Term ParseTypeAltPrimary() — 8-branch primary mirroring ParsePrimary but with trailing `?` tolerated/encoded on each branch; param-type-ref vs struct disambiguation via VARIABLE/READER+LPAREN look-ahead"
  - "  private Term ParseTypeAltList() — list parser allowing trailing `?` on every completion path"
  - "  private ProcDecl ParseProcDeclaration() — [exported|imported] procedure (ATOM|operator-token-x11) [#path]* [(argTypes)] DOT; modulePath join with '#'; nullary parens optional"
  - "  private TypeExpr ParseProcArgType() — primitive (_[?]) OR qualified (atom#...#TypeName[?]) OR typeref (Var[?] [(typeArgs)] [?])"
  - "doc comments → /// <summary>...</summary> on class and selected methods"
  - "// line comments preserved at the same positions"
  - "relative imports → using directives or same-namespace co-location; prelude `show builtinProcedures` ⇒ Prelude.BuiltinProcedures fully-qualified"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-final-field-class-to-csharp-getonly-class — Parser class shape (reuse, lexer.dart / token.dart family)

- Deep analysis: Parser is identity-and-mutation bound. One `final List<Token> tokens` (write-once via ctor), one `int _current = 0` (mutated by every Advance / cursor-rollback), one `Clause? _pendingClause` (a single-slot look-back consumed by the next ParseProcedure call). Records and structs are wrong because parsing is a sequenced mutation flow.
- Authoritative cached basis: identical to the rf-dart-final-field-class-to-csharp-getonly-class reused from token.dart / lexer.dart (no fresh research required — FR-024 cache reuse). The Lexer/Parser pair share the same shape: one immutable input + a few mutable cursors, packaged as a reference class.
- Conclusion: C# reference `class Parser`, one get-only `IReadOnlyList<Token> Tokens` auto-property, one mutable `long _current`, one mutable `Clause? _pendingClause`. Authoritative both sides; no escalation.

### rf-dart-string-indexing-to-csharp-char-indexing — recursive-descent helpers (reuse from lexer.dart but applied to List indexing)

- Deep analysis: the helpers (`_match`, `_check`, `_advance`, `_peek`, `_previous`, `_isAtEnd`, `_consume`) form the canonical recursive-descent toolkit. They share the same cast-`long`-to-`int` pattern as lexer.dart's `_source[(int)_current]` (here it's `Tokens[(int)_current]`). The toolkit is preserved verbatim because every other parsing method depends on its exact semantics.
- Authoritative .NET (IList indexer is `int`-typed): cached `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ilist-1.item` — "Gets or sets the element at the specified index" with `int index` parameter. Same as token-positions (`Tokens[(int)_current]`) — direct counterpart of Dart `tokens[_current]` where Dart `List.[](int index)` is also `int`-typed at the language level (Dart `int` is 64-bit on native but the indexer signature is still parametric).
- Conclusion: a private-method toolkit (`Match`/`Check`/`Advance`/`Peek`/`Previous`/`IsAtEnd`/`Consume`) on Parser. Cursor stays `long`; cast to `int` at every IList indexer. Authoritative both sides; no escalation.

### rf-dart-const-set-to-csharp-frozenset-ordinal — `['module','stdlib','mode'].contains(keyword)` (reuse from glp_printer.dart)

- Deep analysis: the `_skipDeclarations` and `parseModule` methods each consult a tiny set of declaration-keyword strings to dispatch. The Dart source constructs a fresh `[...]` list on every call and scans linearly (O(N) per call, with N=3) — semantically fine but not the .NET idiom. The cached idiom `rf-dart-const-set-to-csharp-frozenset-ordinal` from glp_printer.dart maps this to `static readonly FrozenSet<string>` with `StringComparer.Ordinal`.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozenset-1` (Microsoft Learn) — "Provides an immutable, read-only set optimized for fast lookup." StringComparer.Ordinal is REQUIRED to match Dart's code-unit ordinal equality.
- Conclusion: declare `static readonly FrozenSet<string> DeclarationKeywords = FrozenSet.Create(StringComparer.Ordinal, "module", "stdlib", "mode");` and call `DeclarationKeywords.Contains(keyword)`. Authoritative both sides; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — string interpolation throughout the file (reuse from token.dart family)

- Deep analysis: dozens of sites use Dart `'$x/$y'` string interpolation to build signatures (`name/arity`), error messages with embedded line/column, and module-path joins. The cached idiom maps these directly to C# `$"{x}/{y}"`.
- Authoritative cached basis: identical to token.dart spec's recorded rf-dart-tostring-interp-to-csharp-tostring-interp. No locale hazard because interpolated values are all `int` (line/column) or `string` (functor name) — no `double` formatting that would invoke locale-sensitive `ToString`.
- Conclusion: Dart `'${expr}'` ⇒ C# `$"{expr}"` at every interpolation site. Authoritative; no escalation.

### rf-dart-map-lookup-to-csharp-trygetvalue — signature-keyed first-occurrence dictionary (reuse from pmt/type_table.dart)

- Deep analysis: `_checkContiguousClauses` (legacy `parse()`) and the body-element loop in `parseModule()` both use `Map<String, Procedure>` keyed by `name/arity` signature with first-occurrence-wins semantics. The Dart pattern `if (seen.containsKey(sig)) { final first = seen[sig]!; ... } seen[sig] = proc;` performs two map lookups (containsKey + indexer). The cached `rf-dart-map-lookup-to-csharp-trygetvalue` idiom from pmt/type_table.dart replaces this with single-lookup `TryGetValue`.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue` (Microsoft Learn) — "Gets the value associated with the specified key … Returns true if the key was found; otherwise, false." Single-lookup, no double-cost.
- Conclusion: `if (seen.TryGetValue(sig, out var first)) { throw new CompileError(...); } seen[sig] = proc;`. Authoritative; no escalation.

### rf-dart-named-default-param-to-csharp-optional-arg — Pratt-loop optional min-precedence (reuse from error.dart)

- Deep analysis: `_parseExpression([int minPrecedence = 0])` is the Pratt-loop entry point with an optional positional argument. The cached idiom maps this directly to C# optional positional parameters.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments` (Microsoft Learn) — "An argument is optional if it has a default value." The optional-default `= 0` shape is identical in both languages.
- Conclusion: C# `private Term ParseExpression(int minPrecedence = 0)`. Authoritative; no escalation.

### rf-dart-list-to-csharp-list-of-T — accumulator lists (reuse from lexer.dart)

- Deep analysis: every accumulator in the file is a `<T>[]`/`tokens.add(t)` pattern over Term, Goal, Guard, TypeExpr, Procedure, Clause, ProcDecl, TypeDef, string. Cached `<T>[]` ⇒ `new List<T>()` and `.add` ⇒ `.Add`.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1` (Microsoft Learn) — "Represents a strongly typed list of objects that can be accessed by index."
- Conclusion: every Dart growable-list site becomes `new List<T>()` with `.Add` calls. Authoritative; no escalation.

### rf-dart-is-chain-to-csharp-switch-expression-type-pattern — Precedence/IsOperator/OperatorFunctor tables (reuse from glp_printer.dart)

- Deep analysis: three small private helpers (`_isOperator`, `_precedence`, `_operatorFunctor`) form lookup-style dispatch on `token.type`. The cached idiom maps these to modern C# switch-expressions with case-stacking via `or` patterns (C# 9+).
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression` (Microsoft Learn) — "The switch expression provides switch-like semantics in an expression context" plus C# 9+ pattern-combinator `or`.
- Conclusion: C# switch-expressions with stacked patterns: `op.Type switch { TokenType.Star or TokenType.Slash or ... => 20, ... default => 0 }`. Authoritative; no escalation.

### rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture — polymorphic ParseGoalOrGuard return + dynamic-list cast (reuse from glp_printer.dart)

- Deep analysis: `_parseGoalOrGuard` returns `dynamic` (Dart) representing "either Goal or Guard"; `_parseClause` casts via `predicates.cast<Goal>()`; `_goalToTerm` uses `goal is Goal`. The cached idiom from glp_printer.dart's `dart.is-chain` maps these to (a) `object` return type (NOT C# `dynamic` which has different semantics), (b) `Cast<T>().ToList()` LINQ for cast-throws-on-mismatch, (c) `is Goal g` declaration-pattern for typed-promotion. The "invariant-culture" part of the rf-name refers to `string.StartsWith("~", StringComparison.Ordinal)` for the negation-prefix check.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#declaration-and-type-patterns` (Microsoft Learn) — declaration pattern syntax. Plus `https://learn.microsoft.com/en-us/dotnet/api/system.string.startswith` — "Determines whether the beginning of this string instance matches the specified string when compared using the specified comparison option" — REQUIRES StringComparison.Ordinal for code-unit equivalent to Dart `startsWith`.
- Conclusion: ParseGoalOrGuard returns `object`; cast sites use `Cast<Goal>().ToList()`; type tests use `is Goal g`; StartsWith uses `StringComparison.Ordinal`. Authoritative; no escalation.

### rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal — `goal is Goal` + string-quote-detection (reuse from ast.dart)

- Deep analysis: `_goalToTerm(dynamic goal)` uses `goal is Goal { ... }`; `_parsePrimary` builds `ConstTerm('"${token.literal}"', ...)` wrapping string literals in explicit quotes for downstream type-checker detection. The cached ast.dart idiom maps both: type-test via declaration pattern (`is Goal g`); string-quote-detection wrapping preserved as `$"\"{token.Literal}\""` interpolation.
- Authoritative cached basis: identical to ast.dart spec's recorded rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal. The string-quote-wrapping is INTENTIONAL: the type-checker uses `value.startsWith('"')` to distinguish strings from atoms at term-inspection time.
- Conclusion: declaration patterns for type tests; preserve string-quote-wrapping verbatim. Authoritative; no escalation.

### rf-dart-relative-import-to-csharp-using-or-same-namespace — relative imports with show-filter (reuse from glp_printer.dart)

- Deep analysis: Dart `import '../analysis/type_checker/prelude.dart' show builtinProcedures;` restricts the import to one symbol. C# has no per-symbol `using`; the equivalent is either same-namespace co-location, fully-qualified reference, or `using static Prelude;` with reviewer discipline.
- Authoritative cached basis: identical to glp_printer.dart spec's recorded rf-dart-relative-import-to-csharp-using-or-same-namespace.
- Conclusion: prefer fully-qualified `Prelude.BuiltinProcedures` for the one restricted symbol; the other four relative imports become `using` directives on the target namespaces. Authoritative; no escalation.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join — `parts.join('.')` and `parts.join('#')` (reuse from glp_printer.dart)

- Deep analysis: two sites use `List<String>.join(separator)` — `_parseModuleName` for the `.`-separated module name, and `_parseProcDeclaration` for the `#`-separated module path of imported procedures. The cached idiom maps `parts.join(s)` to `string.Join(s, parts)`.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/api/system.string.join` (Microsoft Learn) — "Concatenates the elements of a specified array or the members of a collection, using the specified separator between each element." Identical contract.
- Conclusion: `parts.join('.')` ⇒ `string.Join(".", parts)`; `parts.join('#')` ⇒ `string.Join("#", parts)`. Authoritative; no escalation.

### rf-dart-map-to-csharp-dictionary — Dictionary<string, Procedure> seen-map (reuse from pmt/type_table.dart)

- Deep analysis: `final seen = <String, Procedure>{};` is a growable hash-map. The cached idiom maps Dart `Map<K, V>` to C# `Dictionary<K, V>`.
- Authoritative cached basis: `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2` (Microsoft Learn) — "Represents a collection of keys and values."
- Conclusion: `new Dictionary<string, Procedure>(StringComparer.Ordinal)` (explicit comparer for reviewer-clear ordinal semantics). Authoritative; no escalation.

## Notes

- The parser is purely synchronous: NO async, NO Stream, NO Future, NO isolate, NO mixin, NO extension method, NO bitwise operator, NO shift, NO overflow path. All those well-known Dart→C# nuances are ABSENT and are correctly not asserted.
- The recursive-descent depth is bounded by the deepest nested term structure plus the deepest nested type alternative — both finite per-file. Stack-overflow on deeply nested expressions is a theoretical risk that EXISTS IN BOTH languages (Dart and C#) and would manifest identically — no conversion-specific mitigation required.
- Cursor-rollback (try-then-restore via `_current = startPos;`) is used in five places: `_skipDeclarations`, `parseModule` (declaration loop), `_parseAtom` (rewind on no-operator), `_isTypeOrProcDeclaration`, `_isTypeDefinition`. Direct cursor assignment is faithful in both languages; preserved verbatim.
- Look-ahead-with-`_current+1`-index is used in `parseModule` (procedure-decl detection), `_parseProcedure` (operator-procedure-clause detection x4), `_parseGoalOrGuard` (Variable+ASSIGN/UNIV/UNIV_DECOMPOSE/EQUALS/HASH x5), `_parsePrimary` (operator-as-functor LPAREN check), `_parseTypeAltPrimary` (param-type-ref VARIABLE+LPAREN check), `_parseProcArgType` (qualified-typeref ATOM+HASH chain), and the operator-procedure-name dispatch. Every site uses `tokens.length > _current + 1` bounds-check FIRST — preserved verbatim.
- Error messages are preserved BYTE-FOR-BYTE because the test suite asserts on diagnostic strings (the GLP project carries phase-specific error stability tests). Every `throw CompileError(...)` site keeps its message string and `phase: 'parser'` named argument.
- The `_pendingClause` slot is the parser's one-token-ahead memory across `ParseProcedure` invocations and is LOAD-BEARING — without it, deciding "is the next clause part of the current procedure or a new procedure?" would require parsing the clause head (a side-effecting operation). Preserved verbatim.
- The Type-alt parser methods (`_parseTypeAlt*` family) are an intentional PARALLEL grammar to the term parser methods (`_parseTerm`/`_parseExpression`/`_parsePrimary`/`_parseList`) — they share the same shape but tolerate trailing `?` (reader-mark) on most term forms. Collapsing the duplication into a parameterised single parser would entangle the two grammars and is REJECTED — preserved as a parallel grammar.
- The expression Pratt-parser precedence table is CALIBRATED: 20 (multiplicative), 10 (additive), 5 (comparison), 2 (module `#`), 1 (diff-list `\`). `_parseExpression(6)` from `_parseGoalOrGuard` exploits the 6 > 5 threshold to stop the expression at comparison operators. Changing any number changes the parse.
- Zero escalations: every non-trivial construct is resolved by reuse of a recorded idiom — five idioms reused from token.dart (rf-dart-final-field-class-to-csharp-getonly-class, rf-dart-plain-enum-to-csharp-enum, rf-dart-int-to-csharp-long-width, rf-dart-objectq-to-csharp-objectq, rf-dart-tostring-interp-to-csharp-tostring-interp), three from error.dart (rf-dart-named-default-param-to-csharp-optional-arg, rf-dart-leading-underscore-privacy-to-csharp-private, rf-dart-implements-exception-to-csharp-derive-system-exception), three from lexer.dart (rf-dart-string-indexing-to-csharp-char-indexing, rf-dart-list-to-csharp-list-of-T, rf-dart-string-interpolation-join-to-csharp-interpolation-string-join), five from ast.dart (rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal, rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture, rf-dart-named-required-and-default-params-to-csharp-positional-default, rf-dart-discriminated-nullable-pair-with-derived-predicate, rf-dart-const-empty-list-default-to-csharp-array-empty), three from glp_printer.dart (rf-dart-const-set-to-csharp-frozenset-ordinal, rf-dart-is-chain-to-csharp-switch-expression-type-pattern, rf-dart-relative-import-to-csharp-using-or-same-namespace), and two from pmt/type_table.dart (rf-dart-map-to-csharp-dictionary, rf-dart-map-lookup-to-csharp-trygetvalue). No new idiom required, no undecidable construct, no idiom-vs-research conflict, no idiom-vs-idiom conflict.
