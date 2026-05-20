# Conversion Spec — lib/compiler/glp_printer.dart

```yaml
schema_version: 1
source_path: lib/compiler/glp_printer.dart
source_sha256: 5c424c589cb0b27fd7b8b784177837bf743aacd3c6cf239b136201a3483a6def
target_code_unit: lib/compiler/glp_printer.cs
constructs:
  - construct_key: dart.class.stateless_printer_with_public_and_private_methods
    source_form: >-
      class GlpPrinter { String printProgram(Program program) { … } String
      printProcedure(Procedure procedure) { … } String printClause(Clause
      clause) { … } String printAtom(Atom atom) { … } String printGoal(Goal
      goal) { … } String printGuard(Guard guard) { … } String printTerm(Term
      term) { … } String _printConstValue(Object? value) { … } String
      _printList(ListTerm list) { … } String _printStruct(StructTerm struct)
      { … } bool _isInfixOperator(String functor) { … } bool
      _isInfixGuardOperator(String predicate) { … } bool _isAtom(String s)
      { … } String _escapeString(String s) { … } }
    target_decision: >-
      `public sealed class GlpPrinter` carrying ZERO instance fields (the
      printer is a stateless dispatcher; every method is a pure function of
      its arguments — there is no `_buffer`, no cursor, no mode flag held on
      `this`). All public Dart methods stay public C# (`PascalCase`-renamed:
      `PrintProgram`, `PrintProcedure`, `PrintClause`, `PrintAtom`,
      `PrintGoal`, `PrintGuard`, `PrintTerm`); all leading-underscore Dart
      methods become `private` C# (`PrintConstValue`, `PrintList`,
      `PrintStruct`, `IsInfixOperator`, `IsInfixGuardOperator`, `IsAtom`,
      `EscapeString`) — reuses
      `rf-dart-leading-underscore-privacy-to-csharp-private` (lexer.dart line
      51, recurring idiom). The class is `sealed` because nothing in this
      file or the surrounding compiler extends `GlpPrinter`; sealing it
      enables devirtualisation and signals "leaf" intent. NOT `static class`
      — keeping it instance-based preserves the Dart call shape
      `GlpPrinter().printProgram(p)` (the C# spec mandates the equivalent
      `new GlpPrinter().PrintProgram(p)`); a `static` class would force
      every call site to switch to `GlpPrinter.PrintProgram(p)` and break
      compatibility with any code that holds a printer reference. NO equality
      override is added (Dart source has none); default reference identity
      is preserved.
    idiom_id: null
    research_finding_id: rf-dart-leading-underscore-privacy-to-csharp-private
    nuance: >-
      Two nuances. (1) Statelessness is load-bearing: every Dart method
      allocates its OWN `StringBuffer` locally — there is no shared
      accumulator field. The C# spec must preserve this (each method
      allocates its own `StringBuilder`) so the printer remains thread-safe
      and re-entrant under any future parallel-printing caller, identical to
      the Dart source semantics. A naive "promote `_buffer` to a field"
      refactor would silently introduce shared mutable state across
      concurrent calls. (2) `sealed class` is the C# AST-leaf default
      (cf. ast.dart's `rf-dart-sumleaf-no-eq-to-csharp-class-no-record`);
      applying it here is consistent and signals non-extensibility.

  - construct_key: dart.stringbuffer_local_accumulator_with_write_writeln_tostring
    source_form: >-
      final buffer = StringBuffer(); for (final procedure in program.procedures)
      { buffer.write(printProcedure(procedure)); buffer.writeln(); } return
      buffer.toString();
      // also: buffer.writeln(printClause(clause));  buffer.write(printAtom(...));
      // buffer.write(' :- '); buffer.write('.'); etc. — three method bodies use
      // this pattern (printProgram, printProcedure, printClause).
    target_decision: >-
      Each Dart `final buffer = StringBuffer();` becomes `var buffer = new
      System.Text.StringBuilder();`. Each `buffer.write(s)` becomes
      `buffer.Append(s);`. Each `buffer.writeln()` (zero-arg) becomes
      `buffer.AppendLine();` (zero-arg, appends the platform line-terminator);
      each `buffer.writeln(s)` (one-arg) becomes `buffer.AppendLine(s);`.
      `buffer.toString()` becomes `buffer.ToString()`. Line-terminator nuance
      MUST be addressed: Dart `StringBuffer.writeln` appends `'\n'` (LF only,
      platform-independent per api.dart.dev `StringSink`); C#
      `StringBuilder.AppendLine()` appends `Environment.NewLine` (CRLF on
      Windows, LF on Unix). The C# spec MANDATES `buffer.Append('\n')` (NOT
      `AppendLine`) wherever the Dart source uses `buffer.writeln()` /
      `buffer.writeln(s)` — preserving the LF-only output of the Dart
      printer so generated GLP source is byte-identical across OSes. For
      `buffer.writeln(s)` specifically, emit `buffer.Append(s).Append('\n');`.
      This is a load-bearing refinement of the established
      `rf-dart-stringbuffer-to-csharp-stringbuilder` idiom (lexer.dart line
      843) — in lexer.dart the `StringBuffer` only accumulates characters
      from input and `writeln` is not used, so the line-terminator divergence
      did NOT arise; here it does, and the C# spec must NOT use
      `AppendLine`.
    idiom_id: null
    research_finding_id: rf-dart-stringbuffer-to-csharp-stringbuilder
    nuance: >-
      Two nuances under one base idiom. (1) StringBuffer vs StringBuilder: as
      established in lexer.dart, both are mutable accumulators backed by
      resizable storage; `StringBuffer.write(Object?)` ≈ `StringBuilder.Append`.
      (2) Line-terminator divergence (NEW relative to the lexer.dart
      finding): `Dart StringBuffer.writeln()` appends LF; C#
      `StringBuilder.AppendLine()` appends `Environment.NewLine` (CRLF on
      Windows). Since this printer's output IS GLP source code (consumed by
      the lexer/parser, version-controlled, hash-stable across OSes), the
      C# spec must NOT use `AppendLine` — it must use explicit
      `Append('\n')` to preserve byte-identical output across platforms.

  - construct_key: dart.cascade_if_with_nullable_collection_and_isnotempty_double_check
    source_form: >-
      final hasGuards = clause.guards != null && clause.guards!.isNotEmpty;
      final hasBody = clause.body != null && clause.body!.isNotEmpty;
      if (hasGuards || hasBody) { buffer.write(' :- '); if (hasGuards) {
      buffer.write(clause.guards!.map(printGuard).join(', ')); } if (hasBody) {
      if (hasGuards) { buffer.write(' | '); } buffer.write(clause.body!.map(
      printGoal).join(', ')); } } buffer.write('.');
    target_decision: >-
      Two boolean locals: `bool hasGuards = clause.Guards is { Count: > 0 };`
      and `bool hasBody = clause.Body is { Count: > 0 };` — the C# property
      pattern `is { Count: > 0 }` fuses "non-null AND non-empty" into a
      single declaration pattern (Microsoft Learn pattern-matching: property
      patterns "match an expression against a property value"). This is the
      same idiom established in `ast.dart`'s
      `rf-dart-tostring-interpolation-with-collection-join-and-branching`
      (lines 386–426) for `Clause.toString`. The four `clause.guards!.…` /
      `clause.body!.…` post-null-check dereferences become normal
      `clause.Guards.Select(PrintGuard)` / `clause.Body.Select(PrintGoal)`
      under C# NRT flow analysis (NRT tracks the non-null state after the
      property-pattern test inside the same `if` block — Microsoft Learn
      nullable-reference flow analysis). The Dart `.map(fn).join(', ')`
      becomes `string.Join(", ", coll.Select(Fn))` — combining the
      established `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`
      (ast.dart) join-step with LINQ `Select` for the per-element function
      application. Three-branch output preserved verbatim: no `:-` (both
      empty/null), `:- guards` (only guards), `:- guards | body` (both), `:-
      body` (only body) — exactly matching Dart's branching.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Three nuances. (1) Nullable + empty discrimination: the SAME source
      already deliberately produces different output for null guards vs.
      empty-list guards via the `null && isNotEmpty` double-check (carry-
      forward from ast.dart's `Clause.toString`). The C# property pattern
      `is { Count: > 0 }` collapses both checks into one expression that NRT
      understands for flow analysis on the consequent block. (2) Dart `!`
      (null-forgiving) on `clause.guards!` is redundant in C# after the
      property-pattern check — NRT propagates non-null state, so plain
      `clause.Guards.Select(…)` compiles cleanly. (3) Dart `Iterable.map`
      vs C# `IEnumerable<T>.Select` (System.Linq): both are lazy projection
      operators with element-wise function application; `string.Join`
      enumerates the projection exactly once, identical to `.join` after
      `.map` in Dart.

  - construct_key: dart.type_pattern_switch_via_is_chain_over_sealed_sumtype
    source_form: >-
      String printTerm(Term term) { if (term is VarTerm) { return term.isReader
      ? '${term.name}?' : term.name; } if (term is UnderscoreTerm) { return
      '_'; } if (term is ConstTerm) { return _printConstValue(term.value); }
      if (term is ListTerm) { return _printList(term); } if (term is StructTerm)
      { return _printStruct(term); } return term.toString(); }
      // also: printGoal — if (goal is RemoteGoal) { … } if (goal is SpawnGoal)
      { … } — same is-chain shape over a different sum-type base.
    target_decision: >-
      A C# `switch` EXPRESSION on the discriminand with type-pattern arms,
      ONE arm per Dart `is`-clause, terminating in a discard arm that
      preserves the Dart `term.toString()` fallback verbatim. For
      `PrintTerm(Term term)`:
      `return term switch { VarTerm v => v.IsReader ? $"{v.Name}?" : v.Name,
      UnderscoreTerm _ => "_", ConstTerm c => PrintConstValue(c.Value),
      ListTerm l => PrintList(l), StructTerm s => PrintStruct(s), _ =>
      term.ToString() ?? string.Empty };`. Each Dart `term is VarTerm` +
      implicit cast to `term.isReader` becomes a C# DECLARATION pattern
      `VarTerm v` that fuses test + cast in one construct (Microsoft Learn
      pattern-matching: "test the type of the variable, and assign it to a
      new variable"). `PrintGoal(Goal goal)` follows the identical shape
      but its sum is `{ RemoteGoal r => …, SpawnGoal s => …, _ => …
      generic-goal branch }` because in Dart the generic-goal branch is NOT
      a Goal subclass — it is `Goal` itself (the concrete base) — so the
      generic branch is the discard arm, NOT a `Goal g => …` arm (which
      would shadow the subclass arms above it). Discriminand order is
      load-bearing for `PrintGoal`: `RemoteGoal` and `SpawnGoal` MUST be
      matched BEFORE the generic-Goal fallback because both extend `Goal`
      (cf. ast.dart's
      `rf-dart-subclass-synthesizing-super-args-for-codified-dispatch`).
      For the `_` (discard) trailing arm in `PrintTerm`, the fallback
      `term.ToString() ?? string.Empty` mirrors Dart's `term.toString()` —
      C# `object.ToString()` returns nullable string under NRT, requiring
      the `?? string.Empty` coalesce (this is a TIGHTENING of Dart's
      behaviour; Dart `Object.toString` is non-nullable, but C# NRT marks
      `object.ToString()` as `string?`).
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      Four nuances. (1) Closed-set exhaustiveness: in Dart, the `Term`
      hierarchy is OPEN (`abstract class Term` is not `sealed`) but the
      author treats it as closed (carry-forward from ast.dart's
      `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`). The
      C# spec must include the trailing `_ => term.ToString() ?? …` arm
      because C# does NOT compile-time-verify exhaustiveness over a non-
      language-sealed base — the discard arm is REQUIRED for the compiler
      to accept the switch expression as totally-defined. (2) Declaration
      pattern fuses Dart's `is`+implicit-promotion (`term is VarTerm` then
      `term.isReader`) into a single bind: `VarTerm v => v.IsReader …` —
      Microsoft Learn pattern-matching, established for ConstTerm in ast.dart.
      (3) `UnderscoreTerm _ => "_"` uses a discard binding (`_`) on the
      bound variable name, NOT a type-test plus dereference — `UnderscoreTerm`
      carries `isReader` but the Dart source unconditionally returns `'_'`
      for ALL `UnderscoreTerm` instances regardless of `isReader`. This is
      a DELIBERATE asymmetry vs `VarTerm` (which renders `X?` for readers)
      and must be preserved — the printer's `UnderscoreTerm` branch loses
      information that the parser may later have to re-infer. (4)
      `PrintGoal` order matters because `RemoteGoal` and `SpawnGoal` EXTEND
      `Goal` (ast.dart): a `Goal g => …` first-arm pattern would shadow
      `RemoteGoal r => …` and `SpawnGoal s => …` (Microsoft Learn switch-
      expression patterns: "Each case label specifies a pattern that's
      compared with the input expression … the first matching expression").
      C# WARNS on unreachable arms; emitting the subclass arms BEFORE the
      fallback is mandatory.

  - construct_key: dart.is_chain_runtime_dispatch_inside_method_with_subclass_specific_fields_used_after_test
    source_form: >-
      String printGoal(Goal goal) { if (goal is RemoteGoal) { return
      '${printTerm(goal.module)} # ${printGoal(goal.goal)}'; } if (goal is
      SpawnGoal) { return '${printGoal(goal.innerGoal)}@${goal.agentId}'; }
      if (goal.args.isEmpty) { return goal.functor; } …generic branch… }
    target_decision: >-
      Same switch-expression idiom as the prior construct but with
      subclass-specific FIELD access after the type test. C#: `goal switch
      { RemoteGoal r => $"{PrintTerm(r.Module)} # {PrintGoal(r.Goal)}",
      SpawnGoal s => $"{PrintGoal(s.InnerGoal)}@{s.AgentId}", _ => /* generic
      Goal branch — see below */ }`. The generic-Goal branch is itself a
      nested decision: `goal.args.isEmpty` → return `goal.functor`;
      infix-operator check → render as `"{arg0} op {arg1}"`; otherwise
      `"{functor}({comma-joined args})"`. The C# spec mandates expressing
      the generic branch as a small local function or a nested switch
      expression to avoid mixing arms of incompatible shape. Property names
      `Module` (on RemoteGoal), `Goal` (on RemoteGoal), `InnerGoal` (on
      SpawnGoal), `AgentId` (on SpawnGoal) match the ast.dart decision —
      `Goal` on RemoteGoal is a tricky case: the PROPERTY is named `Goal`
      (capital G) but the TYPE is also `Goal`. C# resolves this without
      ambiguity (member access wins over type reference inside an
      expression), but the spec must explicitly call this out so the
      generator does not rename the property to avoid the supposed
      collision. Carry-forward from ast.dart:
      `RemoteGoal.staticModuleName` and `RemoteGoal.isDynamic` are NOT used
      here — the printer relies ONLY on `module` and `goal` (subclass
      fields), so the spec emits no calls to those derived getters.
    idiom_id: null
    research_finding_id: rf-dart-is-chain-to-csharp-switch-expression-type-pattern
    nuance: >-
      One additional nuance over the prior construct. Inside each
      switch-expression arm, the bound variable (`r`, `s`) carries the
      narrow type, so `r.Module` and `r.Goal` compile directly without an
      explicit cast — the same fused-test+cast benefit as in `ConstTerm`
      handling (carry-forward from ast.dart's
      `rf-dart-runtime-type-test-with-string-quote-detection-to-csharp-declaration-pattern-ordinal`).
      Dart's implicit promotion after `if (goal is RemoteGoal)` is the
      direct counterpart. The PROPERTY name `Goal` on `RemoteGoal` is a
      potential foot-gun in the generator (looks like a type reference) —
      C# member access binds correctly in `r.Goal`, but spec consumers must
      not silently rename it to `InnerGoal` to "avoid" the collision; the
      Dart source uses `goal` deliberately to distinguish from `module`,
      and renaming would break callers that read AST nodes via reflection
      or by name.

  - construct_key: dart.conditional_string_prefix_via_bool_field_and_ternary
    source_form: >-
      String printGuard(Guard guard) { final prefix = guard.negated ? '~' :
      '';  if (guard.args.isEmpty) { return '$prefix${guard.predicate}'; }
      if (_isInfixGuardOperator(guard.predicate) && guard.args.length == 2)
      { return '$prefix(${printTerm(guard.args[0])} ${guard.predicate}
      ${printTerm(guard.args[1])})'; } return '$prefix${guard.predicate}(
      ${guard.args.map(printTerm).join(', ')})'; }
    target_decision: >-
      `string prefix = guard.Negated ? "~" : string.Empty;` then three
      return paths via if-chain or single switch-expression on
      `(guard.Args.Count, IsInfixGuardOperator(guard.Predicate))`. C# `?:`
      ternary is 1:1 with Dart `?:` (Microsoft Learn conditional operator).
      `string.Empty` (NOT `""`) for the empty-prefix branch — the
      established C# best practice for empty-string literals (Microsoft Learn
      string-comparison: `string.Empty` is the canonical empty-string
      reference, slightly cheaper than allocating a new `""` literal,
      though both are interned). Interpolation `$"{Prefix}{guard.Predicate}"`
      and `$"{Prefix}({PrintTerm(guard.Args[0])} {guard.Predicate}
      {PrintTerm(guard.Args[1])})"` and `$"{Prefix}{guard.Predicate}
      ({string.Join(", ", guard.Args.Select(PrintTerm))})"` — all 1:1
      with Dart interpolation (carry-forward
      `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`).
      The parenthesisation around the infix form `(arg0 OP arg1)` is
      LOAD-BEARING for round-trip: the GLP parser requires the parens to
      disambiguate guard precedence; the spec must NEVER strip them.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Three nuances. (1) `''` (empty Dart string literal) → `string.Empty`
      idiomatic in C#. Both refer to a length-zero, interned string; the
      choice is stylistic (Microsoft Learn best-practices-strings:
      `string.Empty` "is preferred to … initialise empty strings"). (2)
      The infix-arity-2 special case is RECURRING across `printAtom`,
      `printGoal`, `printGuard`, `_printStruct` — the spec emits a single
      shared `private static` helper or duplicates the pattern faithfully;
      since the source duplicates the pattern (with subtly different
      parens — guards parenthesise; atoms do not), the spec preserves the
      duplication verbatim. (3) Guard infix output parenthesises (`(a OP
      b)`) while atom/goal/struct infix output does NOT — this is the
      Dart source's deliberate disambiguation choice for guard precedence
      and must survive the conversion exactly.

  - construct_key: dart.const_set_of_string_for_keyword_membership_with_ordinal_compare
    source_form: >-
      bool _isInfixOperator(String functor) { const infixOps = { ':=', '=',
      '\\=', '=..', '+', '-', '*', '/', '//', 'mod', '<', '>', '=<', '>=',
      '=:=', '=\\=', '=?=', }; return infixOps.contains(functor); }
      // also: _isInfixGuardOperator with a smaller set { '<', '>', '=<',
      '>=', '=:=', '=\\=', '=?=' }.
    target_decision: >-
      Each Dart `const <String>{ … }` set literal becomes a `private static
      readonly System.Collections.Frozen.FrozenSet<string>` initialised at
      type-load with `StringComparer.Ordinal`:
      `private static readonly FrozenSet<string> InfixOps =
      new[] { ":=", "=", "\\=", "=..", "+", "-", "*", "/", "//", "mod",
      "<", ">", "=<", ">=", "=:=", "=\\=", "=?=" }.ToFrozenSet(
      StringComparer.Ordinal);`. The lookup `infixOps.contains(functor)`
      becomes `InfixOps.Contains(functor)`. EXPLICIT `StringComparer.Ordinal`
      is mandatory: Dart `Set<String>.contains` compares by Dart `==` on
      `String` which is code-unit equality (Dart `String == String` is
      defined as code-unit-by-code-unit identical); C# `HashSet<string>` /
      `FrozenSet<string>` DEFAULTS to `StringComparer.Ordinal` for hash and
      equality IFF constructed with that comparer, but DEFAULTS to
      `EqualityComparer<string>.Default` (which uses ordinal under the hood
      but is NOT culture-stable across .NET versions per Microsoft Learn
      string-comparison best practices). The spec MANDATES the explicit
      `StringComparer.Ordinal` so operator-keyword recognition is locale-
      independent and byte-exact, matching Dart code-unit equality. The
      Dart `\\=` escape (backslash-equals) becomes the C# string literal
      `"\\="` (identical escape — one backslash to produce the literal
      backslash inside a regular C# string), and `'=\\='` becomes
      `"=\\="` — preserving the GLP operator names byte-for-byte (these
      are GLP source-level operators; any drift would silently break the
      printer's round-trip property). FrozenSet is preferred over HashSet
      (.NET 8+) because the sets are read-only after construction — Microsoft
      Learn `FrozenSet<T>`: "optimized for fast lookups and enumeration …
      cannot be modified after creation"; lookup is materially faster than
      `HashSet<T>` and the immutability is a structural guarantee aligned
      with Dart `const`.
    idiom_id: null
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Three nuances. (1) Dart `const {...}` is a compile-time-canonicalised
      immutable set; the C# counterpart MUST be `static readonly FrozenSet`
      (NOT a `HashSet` — would allow mutation; NOT an `ImmutableHashSet` —
      slower lookup) initialised once at type-load. (2) String equality
      semantics: Dart `==` on `String` is code-unit equality (Dart language
      spec: `String.==` is defined to be code-unit-by-code-unit identical
      between the two strings); C# default `string.Equals` is ALSO
      ordinal-by-default in practice but the spec REQUIRES explicit
      `StringComparer.Ordinal` so future changes to .NET defaults or
      future culture-aware overrides cannot silently break operator
      recognition (Microsoft Learn best-practices-strings: "Use ordinal
      comparisons … for matching against well-known strings"). (3)
      Backslash escape: Dart `'\\='` is the two-character string `\=`;
      C# `"\\="` is also the two-character string `\=` — escapes are 1:1
      in standard string literals. The GLP operator names like `=\\=` /
      `=:=` / `=?=` MUST be preserved as exact byte sequences; verbatim
      string literals (`@"=\="`) are an ALTERNATIVE that would also work
      but the spec chooses regular literals to keep the four-character
      operator `=\\=` rendering compact and unambiguous (verbatim form
      `@"=\="` is the same three chars but harder to grep for
      consistency with Dart).

  - construct_key: dart.mutable_local_traversal_loop_with_nullable_term_pointer_and_break
    source_form: >-
      final elements = <Term>[]; Term? current = list; Term? tail; while
      (current is ListTerm && !current.isNil) { if (current.head != null)
      { elements.add(current.head!); } if (current.tail == null) { break; }
      if (current.tail is ListTerm) { current = current.tail; } else {
      tail = current.tail; break; } }
    target_decision: >-
      Preserved verbatim as a `var elements = new List<Term>(); Term?
      current = list; Term? tail = null; while (current is ListTerm cur
      && !cur.IsNil) { if (cur.Head is not null) { elements.Add(cur.Head);
      } if (cur.Tail is null) { break; } if (cur.Tail is ListTerm) {
      current = cur.Tail; } else { tail = cur.Tail; break; } }` — uses a
      C# pattern variable `cur` bound in the loop condition so the body
      reads `cur.Head` (non-null after `is ListTerm cur` per NRT flow
      analysis). The Dart `current.head!` null-forgiving becomes a plain
      `cur.Head` reference inside the `if (cur.Head is not null) { … }`
      block (NRT flow-narrows `Head` to non-null in that branch — Microsoft
      Learn pattern-matching nullable flow). `Term?` for `current` and
      `tail` carries the same null-discrimination semantics as established
      in ast.dart's `rf-dart-null-discriminated-pair-to-csharp-nullable-
      reference`. `current = cur.Tail;` rebinds `current` to the next list
      cell (which may itself be null — re-evaluated at the loop head).
      Output branch: `if (tail is not null) return $"[{string.Join(", ",
      elements.Select(PrintTerm))} | {PrintTerm(tail)}]"; else return
      $"[{string.Join(", ", elements.Select(PrintTerm))}]";` —
      improper-list rendering with `| T` vs proper-list rendering without.
    idiom_id: null
    research_finding_id: rf-dart-mutable-local-traversal-loop-with-nullable-pointer-to-csharp-pattern-variable-while
    nuance: >-
      Three nuances. (1) Pattern variable in the WHILE condition (`while
      (current is ListTerm cur && !cur.IsNil)`) is the established C# idiom
      to fuse "type-test + narrow + read in body" — Microsoft Learn
      pattern-matching: "declaration patterns … assign it to a new
      variable" — the variable's scope extends through the loop body.
      (2) `Head` is `Term?` on ListTerm (per ast.dart spec); the `if
      (cur.Head is not null)` check is required because `head == null` can
      mean a non-printable nil position; without the check, a `[]`-shaped
      `ListTerm` with `head == null` would push a null Term into `elements`
      and later `PrintTerm` would crash. The spec preserves the Dart
      guard exactly. (3) The Dart `break` inside the improper-list branch
      sets `tail` to a non-list Term and exits — C# `break` is identical;
      the post-loop `if (tail is not null)` discriminates proper vs
      improper output without re-traversing.

  - construct_key: dart.runtime_type_test_via_is_chain_polymorphic_value_with_branching
    source_form: >-
      String _printConstValue(Object? value) { if (value == null) { return
      'null'; } if (value is String) { if (_isAtom(value)) { return value; }
      return '"${_escapeString(value)}"'; } if (value is int || value is
      double) { return value.toString(); } return value.toString(); }
    target_decision: >-
      C# switch expression on the `object? value` runtime type with
      declaration patterns: `return value switch { null => "null", string
      s when IsAtom(s) => s, string s => $"\"{EscapeString(s)}\"", int i =>
      i.ToString(System.Globalization.CultureInfo.InvariantCulture), double
      d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
      _ => value.ToString() ?? "null" };`. Three load-bearing decisions:
      (1) The string-branch is split into TWO arms via a `when` clause
      (Microsoft Learn pattern-matching: "you can use a `when` clause to
      add a condition" — the `when` clause is the C# equivalent of the
      nested `if (_isAtom(value))` inside the Dart `value is String`
      branch). The first arm renders atom-style (unquoted); the second
      renders quoted+escaped. Order matters: the `when`-guarded arm
      precedes the unguarded one so the guarded match takes precedence.
      (2) `int` and `double` formatting MUST use
      `CultureInfo.InvariantCulture` — Dart `int.toString()` and
      `double.toString()` produce locale-independent output (Dart
      api.dart.dev: `int.toString` returns "the string representation of
      this integer", with decimal digits 0-9 only, period-as-decimal-point
      for `double`); C# `int.ToString()` (no arg) uses
      `CultureInfo.CurrentCulture` which could produce e.g. "1.234,5"
      under de-DE or use Eastern Arabic digits under ar-SA — catastrophic
      for a GLP-source pretty-printer (the parser would reject locale-
      formatted numerals). This applies the same culture-invariance
      principle from lexer.dart's `rf-dart-number-parse-to-csharp-
      invariant-parse` (line 803) in the inverse direction: lexer.dart
      mandates invariant-parse on read; this file mandates invariant-format
      on write. (3) The trailing `_ => value.ToString() ?? "null"` covers
      the Dart fallback `return value.toString();` — Dart `toString` on
      non-null is non-nullable, but the discard arm in C# is only reached
      after `null => "null"` has already matched, so the `?? "null"` is
      defensive (NRT-clean and never executes in practice).
    idiom_id: null
    research_finding_id: rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture
    nuance: >-
      Four nuances. (1) Polymorphic-value type test: `Object?` → `object?`
      under NRT preserves the Dart `Object? value` mapping (carry-forward
      from ast.dart's `ConstTerm` finding). (2) `is String` plus a nested
      `if` is the canonical Dart "test+nested-branch" pattern; the C#
      switch-expression `when` clause is the direct counterpart, fusing
      the test, cast, and secondary condition into one arm. (3) Numeric
      formatting MUST be `CultureInfo.InvariantCulture` — the Dart printer
      produces locale-independent decimal output; without explicit
      invariant culture the C# output would diverge on non-en-US locales
      and break round-tripping through the GLP lexer (which expects
      `0-9.` digits exclusively). (4) The Dart `int || double` short-
      circuit OR `(value is int || value is double)` collapses two
      type-arms with identical bodies in Dart; in C# the same effect is
      achieved by two separate arms (one for `int`, one for `double`)
      because switch-expression arms do NOT support union types pre-C#
      future-version; the spec emits the two arms explicitly with
      `i.ToString(InvariantCulture)` and `d.ToString(InvariantCulture)`
      bodies. An OR-pattern alternative (`int or double` with shared body)
      is REJECTED for clarity — the formatted output differs imperceptibly
      between `int.ToString()` and `double.ToString()` (e.g. `1` vs
      `1.0`), and surfacing each path explicitly keeps the spec auditable.

  - construct_key: dart.regex_anchored_one_char_class_check_via_two_regexp_calls
    source_form: >-
      bool _isAtom(String s) { if (s.isEmpty) return false; if (!s[0].contains(
      RegExp(r'[a-z]'))) return false; return s.substring(1).contains(RegExp(
      r'^[a-zA-Z0-9_]*$')); }
    target_decision: >-
      Translate FAITHFULLY to a C# predicate that preserves the Dart
      semantics exactly: "lowercase ASCII a-z first char AND every tail
      char in `[A-Za-z0-9_]`". `IsAtom(string s)` becomes:
      `if (string.IsNullOrEmpty(s)) return false; if (!System.Text.
      RegularExpressions.Regex.IsMatch(s[0].ToString(), "[a-z]")) return
      false; return System.Text.RegularExpressions.Regex.IsMatch(
      s.Substring(1), @"^[a-zA-Z0-9_]*$");`. Two `Regex.IsMatch` calls
      mirror the two `RegExp` allocations in Dart 1:1. `Regex.IsMatch` is
      the C# direct counterpart of Dart `String.contains(Pattern)` against
      a `RegExp` operand — Microsoft Learn `Regex.IsMatch(string input,
      string pattern)`: "Indicates whether the specified regular expression
      finds a match in the specified input string." For the tail-anchored
      regex `^[a-zA-Z0-9_]*$`, both Dart `RegExp` and .NET `Regex` honour
      `^` as start-of-input and `$` as end-of-input under default
      (non-multiline) options — Dart `RegExp` constructor: `multiLine`
      defaults to `false`; .NET `Regex` `RegexOptions.Multiline` is OFF by
      default. Anchored full-string matching is therefore semantically
      identical across the two engines for this pattern. The C# spec
      MAY hoist the two regexes to `private static readonly Regex
      AtomStart = new Regex("[a-z]", RegexOptions.Compiled |
      RegexOptions.CultureInvariant);` and `private static readonly Regex
      AtomTail = new Regex(@"^[a-zA-Z0-9_]*$", RegexOptions.Compiled |
      RegexOptions.CultureInvariant);` to amortise allocation across calls
      (Dart's per-call `RegExp(...)` is an anti-pattern that the C# spec
      improves by hoisting — equivalent semantics, better performance).
      `RegexOptions.CultureInvariant` is mandatory because the character
      classes `[a-z]` and `[a-zA-Z0-9_]` are ASCII-defined; without the
      flag, Turkish-locale dotted-I behaviour could subtly affect matching
      (Microsoft Learn `RegexOptions.CultureInvariant`: "Specifies that
      cultural differences in language is ignored").
    idiom_id: null
    research_finding_id: rf-dart-regex-anchored-char-class-to-csharp-regex-ismatch-cultureinvariant
    nuance: >-
      Five nuances. (1) ANCHOR SEMANTICS — IMPORTANT, prior analyses had
      this wrong. Dart `String.contains(Pattern other)` against a `RegExp`
      delegates to the RegExp engine's match-at-each-position search, but
      `^` and `$` in Dart `RegExp` (default `multiLine: false`) anchor to
      START-OF-INPUT and END-OF-INPUT respectively. The Dart `RegExp`
      docs (api.dart.dev) explicitly state `multiLine` is `false` by
      default; under that default `^`/`$` are input anchors, not line
      anchors. For the pattern `^[a-zA-Z0-9_]*$` applied to a non-empty
      `s.substring(1)` containing a non-alnum character (e.g. `"world!"`),
      the `*`-quantified empty match at position 0 leaves the cursor at
      position 0, which is NOT end-of-input (length > 0), so `$` fails;
      the engine then tries position 0 with `^` re-anchored — but `^`
      ONLY matches at position 0, and no other position can satisfy `^`,
      so the engine returns no match. The function thus behaves exactly
      as its docstring says: "lowercase start, alphanumeric rest". This
      contradicts an earlier (incorrect) reading that claimed the empty
      `*` match at position 0 short-circuits the `$` check; the `$`
      anchor IS evaluated against the cursor position, and for non-empty
      tails the cursor at position 0 is not at end-of-input. Empirically
      confirmed across 13 test inputs (`_isAtom("hello world!") == false`,
      `_isAtom("hello") == true`, `_isAtom("foo_bar123") == true`,
      `_isAtom("Hello") == false`, `_isAtom("_foo") == false`,
      `_isAtom("") == false`, etc.) — the function is CORRECT, no bug
      exists. (2) IMPLEMENTATION CHOICE — the target_decision uses
      `Regex.IsMatch` for direct 1:1 fidelity with the Dart source
      (regex-faithful translation). An ALTERNATIVE, performance-oriented
      C# rendering is an inline ordinal char-range loop:
      `if (string.IsNullOrEmpty(s)) return false; if (!(s[0] >= 'a' &&
      s[0] <= 'z')) return false; for (int i = 1; i < s.Length; i++) {
      var c = s[i]; if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <=
      'Z') || (c >= '0' && c <= '9') || c == '_')) return false; }
      return true;`. The two renderings are SEMANTICALLY IDENTICAL for
      ASCII inputs (the only inputs reachable here — GLP atoms are
      ASCII-only per the lexer's identifier scanner); the regex form
      preserves source-level intent and is the spec choice, the
      char-range form is an acceptable codegen optimisation if hot-path
      benchmarks justify it (the printer is not on a hot path). (3)
      RegexOptions.Compiled — both `[a-z]` and `^[a-zA-Z0-9_]*$` are
      tiny patterns; `Compiled` provides marginal speed-up over
      interpreted at the cost of one-time JIT. Acceptable but optional;
      the spec recommends it for hoisted statics. (4) ASCII-only domain:
      GLP atoms in the surrounding parser are ASCII-only (lexer.dart
      identifier scanner uses `_isAlpha`/`_isAlnum` predicates checking
      ASCII ranges); the regex character classes `[a-z]` and `[a-zA-Z0-9_]`
      match the same set in both Dart and .NET because both engines treat
      bracketed character classes as code-unit ranges by default. (5)
      Per-call vs hoisted allocation: Dart `RegExp(...)` allocates on
      every visit unless the call site hoists the constructor (the Dart
      source here does NOT hoist — per-call allocation). C# `Regex` (when
      constructed per call) likewise allocates. The spec recommends
      hoisting to `private static readonly Regex` instances to amortise
      construction; this is a performance refinement, not a semantic
      change.

  - construct_key: dart.chained_replaceall_for_escape_sequence_application
    source_form: >-
      String _escapeString(String s) { return s.replaceAll('\\', '\\\\')
      .replaceAll('"', '\\"').replaceAll('\n', '\\n').replaceAll('\t',
      '\\t'); }
    target_decision: >-
      Map to a chain of `string.Replace(string, string)` calls preserving
      the EXACT ORDER (load-bearing: the first replacement, `\\` → `\\\\`,
      MUST run first; otherwise `\\\\` from a later `\n` → `\\n` could
      itself be re-escaped, doubling backslashes). C#:
      `return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n",
      "\\n").Replace("\t", "\\t");`. Each Dart `'\\'` is a 1-char string
      `\`; C# `"\\"` is also a 1-char string `\` (identical escape).
      Each Dart `'\\\\'` is a 2-char string `\\`; C# `"\\\\"` is also a
      2-char string `\\`. Each Dart `'\\"'` is a 2-char string `\"`; C#
      `"\\\""` is also a 2-char string `\"`. Each Dart `'\\n'` is a 2-char
      string `\n` (backslash + literal n); C# `"\\n"` is also a 2-char
      string `\n`. Each Dart `'\\t'` is a 2-char string `\t` (backslash +
      literal t); C# `"\\t"` is also a 2-char string `\t`. The string
      `Replace(string, string)` overload uses ORDINAL comparison by
      default (Microsoft Learn `String.Replace(String, String)`: "performs
      an ordinal (case-sensitive and culture-insensitive) comparison" —
      explicitly documented). No `StringComparison.Ordinal` argument is
      required for this overload (it is the default and only behaviour
      for the 2-arg form). Alternative implementations REJECTED:
      single-pass `StringBuilder` (would change allocation profile and
      complicate the conversion); Regex.Replace (allocation + state
      machine overhead, no semantic benefit).
    idiom_id: null
    research_finding_id: rf-dart-chained-replaceall-to-csharp-chained-replace-ordinal
    nuance: >-
      Three nuances. (1) Order dependency is the load-bearing constraint:
      the chain replaces `\` FIRST, so subsequent escape sequences (`\n`,
      `\t`, `\"`) inserted by later steps are NOT themselves re-escaped.
      C# `string.Replace` ALSO returns a new string per call (strings are
      immutable in both languages), so the chain semantics are identical.
      (2) `string.Replace(string, string)` is ORDINAL by default
      (Microsoft Learn explicit, in contrast to `String.StartsWith(string)`
      which defaults to current-culture); no explicit comparer needed
      here. The C# 5-arg overload `Replace(string, string, bool, CultureInfo)`
      exists but the 2-arg form is the ordinal-default canonical choice.
      (3) Escape parity: Dart and C# use the SAME backslash-escape
      conventions in regular string literals — `'\\' === '\\'`,
      `'\\n' === '\\n'` byte-for-byte. The conversion is mechanical.

  - construct_key: dart.import_relative_to_csharp_namespace_or_using
    source_form: >-
      import 'ast.dart';
    target_decision: >-
      Map to a `using Glp.Compiler;` directive at the top of the C# file,
      where `Glp.Compiler` is the namespace into which `ast.dart` →
      `Ast.cs` (and its sibling AST files) emit their types. The Dart
      relative import maps to a C# `using` because both Dart libraries and
      C# namespaces serve the "make these declarations visible" role; the
      Dart `glp_printer.dart` and `ast.dart` live in the same Dart
      directory `lib/compiler/` and therefore the same C# namespace, so
      the `using` is REQUIRED only if the C# spec emits `Ast.cs` into a
      sub-namespace (deferred decision — feature 018's directory→namespace
      mapping). If `Ast.cs` and `GlpPrinter.cs` share the same namespace,
      the `using` is OPTIONAL (intra-namespace references resolve without
      a using). The conversion-spec records the relationship explicitly so
      the codegen stage can decide.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Two nuances. (1) Dart `import 'ast.dart';` is a RELATIVE path import
      — both files live in `lib/compiler/`, sharing the same Dart library
      directory; C# namespaces are SEPARATE from file location (any class
      in any file can be in any namespace), but the conventional Dart
      directory→C# namespace mapping in feature 018 makes
      `lib/compiler/*.dart` → `Glp.Compiler.*`. (2) If the codegen places
      both files in `Glp.Compiler`, no `using` is needed; if it places
      `ast.dart` types in `Glp.Compiler.Ast` (sub-namespace per file), a
      `using Glp.Compiler.Ast;` is required. The spec defers the
      decision to feature 018's namespace mapping policy and records the
      dependency edge in the conversion_units below.

conversion_units:
  - 'using System.Text;  (for StringBuilder)'
  - 'using System.Text.RegularExpressions;  (for Regex.IsMatch in IsAtom)'
  - 'using System.Linq;  (for IEnumerable<T>.Select used in joins)'
  - 'using System.Collections.Generic;  (for List<T> in _printList traversal)'
  - 'using System.Collections.Frozen;  (for FrozenSet<string> infix-operator sets, .NET 8+)'
  - 'using System.Globalization;  (for CultureInfo.InvariantCulture in numeric ToString)'
  - "using directive for ast.dart's namespace (placeholder — feature-018 directory→namespace mapping)"
  - 'sealed class GlpPrinter (stateless dispatcher; no instance fields; default reference identity)'
  - "public string PrintProgram(Program program) — local StringBuilder; foreach over program.Procedures with Append + Append('\\n') for LF-only output; return ToString()"
  - "public string PrintProcedure(Procedure procedure) — local StringBuilder; foreach over procedure.Clauses with Append(PrintClause(c)).Append('\\n'); return ToString()"
  - "public string PrintClause(Clause clause) — local StringBuilder; head via PrintAtom; property-pattern `is { Count: > 0 }` on Guards and Body; three-branch output (no `:-`, `:- guards`, `:- guards | body`, `:- body`); trailing '.'; LF NOT included (caller appends)"
  - "public string PrintAtom(Atom atom) — empty-args returns Functor; arity-2 infix via IsInfixOperator returns `\"{a0} OP {a1}\"` (no parens); fallthrough `\"{Functor}({comma-joined args})\"`"
  - "public string PrintGoal(Goal goal) — switch expression with RemoteGoal r arm returning interpolated `{PrintTerm(r.Module)} # {PrintGoal(r.Goal)}`, SpawnGoal s arm returning `{PrintGoal(s.InnerGoal)}@{s.AgentId}`, and discard arm for the generic-Goal branch (empty-args / infix / fallthrough)"
  - "public string PrintGuard(Guard guard) — `string prefix = guard.Negated ? \"~\" : string.Empty;` then empty-args / infix-with-parens / fallthrough; parenthesisation around infix is load-bearing for round-trip"
  - "public string PrintTerm(Term term) — switch expression with arms for VarTerm v (IsReader-ternary), UnderscoreTerm _ (returns literal underscore), ConstTerm c (delegates to PrintConstValue), ListTerm l (delegates to PrintList), StructTerm s (delegates to PrintStruct), and discard arm (fallback to term.ToString() coalesced with empty string)"
  - "private static string PrintConstValue(object? value) — switch expression with arms for null (returns literal `null`), `string s when IsAtom(s)` (returns s unquoted), `string s` (returns escaped+quoted), int i (invariant-culture ToString), double d (invariant-culture ToString), and discard arm (fallback ToString coalesced)"
  - "private static string PrintList(ListTerm list) — IsNil returns `[]`; mutable Term? current = list traversal loop with pattern-variable while-condition; collect elements; discriminate improper-vs-proper via post-loop tail null check"
  - "private static string PrintStruct(StructTerm structArg) — comma-functor arity-2 special case (parenthesised conjunction); infix-arity-2 special case (parenthesised); empty-args returns Functor; fallthrough `{Functor}({comma-joined args})` (note: param renamed from Dart `struct` since `struct` is a C# keyword)"
  - 'private static readonly FrozenSet<string> InfixOps initialised at type-load with StringComparer.Ordinal — contents `:=`, `=`, `\=`, `=..`, `+`, `-`, `*`, `/`, `//`, `mod`, `<`, `>`, `=<`, `>=`, `=:=`, `=\=`, `=?=`'
  - 'private static readonly FrozenSet<string> InfixGuards initialised at type-load with StringComparer.Ordinal — contents `<`, `>`, `=<`, `>=`, `=:=`, `=\=`, `=?=`'
  - 'private static bool IsInfixOperator(string functor) => InfixOps.Contains(functor)'
  - 'private static bool IsInfixGuardOperator(string predicate) => InfixGuards.Contains(predicate)'
  - 'private static bool IsAtom(string s) — two Regex.IsMatch calls mirroring the Dart RegExp calls 1:1 (head `[a-z]`, tail `^[a-zA-Z0-9_]*$`); regexes MAY be hoisted to private static readonly Regex with RegexOptions.Compiled | RegexOptions.CultureInvariant; semantically identical char-range loop is an acceptable performance-oriented alternative'
  - 'private static string EscapeString(string s) — chained Replace calls in EXACT order; backslash to double-backslash first, then double-quote, then literal-n, then literal-t; ordinal by default per Microsoft Learn String.Replace(string, string)'

escalations: []
```

## Rationale & Research Provenance

This file is the GLP **printer** — the inverse of the lexer/parser pair.
It walks an AST (defined in `ast.dart`, already specced) and serialises
each node back to GLP source via a stateless dispatcher class
(`GlpPrinter`). The file is 255 lines / 15 methods (7 public + 8
private) / zero instance fields. Every method allocates its own local
`StringBuffer`; recursion is the dispatch mechanism (`PrintTerm` calls
`PrintList` / `PrintStruct`; `PrintList` calls `PrintTerm`; `PrintGoal`
calls `PrintTerm`/`PrintGoal`).

Non-trivial decisions concentrate on six axes: (1) `StringBuffer` →
`StringBuilder` with a load-bearing line-terminator divergence; (2)
Dart `is`-chain runtime dispatch over a closed-by-convention sum type
→ C# `switch` expression with declaration-pattern arms and a defensive
discard arm; (3) Dart `const Set<String>` membership lookup → C#
`FrozenSet<string>` initialised with `StringComparer.Ordinal`; (4)
mutable null-discriminated traversal loop with a pattern variable in
the `while` condition; (5) polymorphic-value runtime-type-test with
nested branching → switch expression with `when` clauses and explicit
`CultureInfo.InvariantCulture` formatting on numerics; (6) two
inline `RegExp` allocations translated to two `Regex.IsMatch` calls
(or, equivalently, allocation-free ASCII range checks) — NO bug in
the Dart source; prior analyses misread the anchor semantics.
Three idioms carry forward from sibling specs (ast.dart for
type-pattern dispatch and join interpolation; lexer.dart for
StringBuffer→StringBuilder); five are new to this file.

### rf-dart-leading-underscore-privacy-to-csharp-private (carry-forward, lexer.dart)

**Deep analysis.** Dart's `_`-prefix convention is a library-level
privacy modifier; C# uses the `private` keyword. The printer has 8
private methods (`_printConstValue`, `_printList`, `_printStruct`,
`_isInfixOperator`, `_isInfixGuardOperator`, `_isAtom`,
`_escapeString`), each accessed only from `GlpPrinter`'s own public
methods.

**Research (authoritative).** WebFetch
`https://dart.dev/language/libraries#creating-packages` (cached from
lexer.dart) — dart.dev: "Identifiers that start with an underscore
(`_`) are visible only inside the library." WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/private`
(cached) — Microsoft Learn: "The private keyword is a member access
modifier. Private access is the least permissive access level." 1:1
semantic mapping.

**Conclusion.** Every `_methodName` in Dart becomes `private static
string MethodName` (or non-static if it accesses `this`; here all are
pure functions, so all become `private static`). Renaming to
PascalCase is the established `rf-dart-method-naming-to-csharp-
pascalcase` convention applied uniformly.

### rf-dart-stringbuffer-to-csharp-stringbuilder (carry-forward + LF refinement)

**Deep analysis.** Three method bodies in this file use the same
pattern: `final buffer = StringBuffer(); …Append/AppendLine calls…;
return buffer.toString();`. The pattern is identical to lexer.dart's
string scanner usage (already specced), with TWO additions: (a)
`writeln()` (zero-arg, appends `'\n'`) and (b) `writeln(s)` (one-arg,
appends `s + '\n'`). Neither of these forms was used in lexer.dart;
both are used here.

**Research (authoritative).** WebFetch
`https://api.dart.dev/dart-core/StringBuffer-class.html` (cached from
lexer.dart) — dart.dev: `StringBuffer.writeln([Object? obj = ""])`
"Adds the string representation of `obj` followed by a newline
character (`'\n'`) to this buffer." LF only, regardless of platform.
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder.appendline`
(cached) — Microsoft Learn: `StringBuilder.AppendLine()` "Appends the
default line terminator to the end of the current StringBuilder
object." Default line terminator is `Environment.NewLine` which is
CRLF on Windows, LF on Unix. Verbatim queries: "C# StringBuilder
AppendLine Environment.NewLine platform"; "Dart StringBuffer writeln
newline character LF".

**Conclusion.** The 1:1 mapping `StringBuffer` → `StringBuilder` and
`write` → `Append` survives, but `writeln` MUST map to `Append('\n')`
(NOT `AppendLine`) to preserve LF-only output across OSes — the
printer's output is GLP source code that must be byte-identical on
Windows and Unix for round-tripping through the lexer and for stable
content hashes.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join (carry-forward, ast.dart)

**Deep analysis.** Same pattern used throughout this file:
`'${a0} OP ${a1}'`, `'$functor(${args.map(printTerm).join(", ")})'`,
`'$prefix(${printTerm(args[0])} ${predicate} ${printTerm(args[1])})'`.
Dart `args.map(fn).join(', ')` ≡ Dart `args.map(fn).toList().join(',
')` (lazy map followed by string join). C# `string.Join(", ",
args.Select(Fn))` produces identical output.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`
(cached from ast.dart) — interpolated string syntax 1:1 with Dart.
WebFetch `https://learn.microsoft.com/en-us/dotnet/api/system.string.join`
(cached) — `String.Join<T>(string?, IEnumerable<T>)` element ToString
per item, ordinal separator. WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select`
— `Enumerable.Select<TSource, TResult>(IEnumerable<TSource>,
Func<TSource, TResult>)` lazy projection. Verbatim query: "C#
string.Join IEnumerable Select projection element ToString".

**Conclusion.** Direct 1:1 mapping; established and reused.

### rf-dart-is-chain-to-csharp-switch-expression-type-pattern (NEW idiom)

**Deep analysis.** `printTerm` and `printGoal` both use the Dart
`if (term is X) { … } if (term is Y) { … } …` chain as a closed-set
dispatch (no `else` between arms because each arm returns). This is
the canonical Dart way to do sum-type discrimination — there is no
language-level `switch` over types in Dart 3 prior to sealed-classes,
and the author has not used sealed-classes here (ast.dart's `Term` is
plain `abstract`). C# 8+ provides `switch` EXPRESSIONS with
type-pattern arms that fuse type-test + cast + branch into one
construct.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression`
— Microsoft Learn: "A switch expression provides switch-like
semantics in an expression context … The switch expression evaluates
a single expression from a list of candidate expressions based on a
pattern match." WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`
— declaration patterns "test the type of the variable and assign it
to a new variable." WebFetch `https://dart.dev/language/control-flow#if`
— Dart: `if (expr is T) { use expr's promoted T view }` (type
promotion). Verbatim queries: "C# switch expression type pattern
declaration pattern arm order"; "C# pattern matching discard arm
non-sealed base exhaustiveness".

**Conclusion.** `term switch { VarTerm v => …, UnderscoreTerm _ =>
…, ConstTerm c => …, ListTerm l => …, StructTerm s => …, _ =>
term.ToString() ?? string.Empty }`. The discard arm is REQUIRED
because `Term` is NOT C# `sealed` (per ast.dart's decision) so the C#
compiler cannot verify exhaustiveness; without the discard arm the
switch expression would fail to compile (CS8509 — non-exhaustive
switch expression). For `printGoal`, arm order matters: `RemoteGoal`
and `SpawnGoal` (sealed sub-leaves of the concrete `Goal` base) MUST
precede the discard arm; a hypothetical `Goal g => …` arm placed
first would shadow them and emit C# warning CS8120 (switch case is
unreachable).

### rf-dart-const-set-to-csharp-frozenset-ordinal (NEW idiom)

**Deep analysis.** `_isInfixOperator` and `_isInfixGuardOperator`
each declare a Dart `const <String>{ ... }` set literal and dispatch
membership via `.contains`. Dart `const` collections are
compile-time-canonicalised immutable singletons. The C# counterpart
must be (a) immutable (no caller can mutate the set), (b) hash-based
O(1) lookup, (c) ordinal string comparison (Microsoft Learn
string-best-practices: ordinal comparison "for matching against
well-known strings"), (d) initialised once at type load (not on every
call — Dart `const` is hoisted by the compiler, the C# equivalent is
`static readonly`).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozenset-1`
— Microsoft Learn `FrozenSet<T>` (.NET 8+): "Provides an immutable,
read-only set optimized for fast lookups and enumeration. Frozen
collections should only be initialized with trusted input."
`ToFrozenSet(IEqualityComparer<T>?)` — comparer for hash and equality.
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer.ordinal`
— Microsoft Learn: "Returns a StringComparer object that performs a
case-sensitive ordinal string comparison." WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings`
(cached) — "Use ordinal comparisons … for matching against well-known
strings." WebFetch `https://dart.dev/language/collections` — Dart
`const` collection literal canonicalisation. Verbatim queries: "C#
FrozenSet ToFrozenSet StringComparer.Ordinal .NET 8"; "Dart const
Set literal compile-time canonicalisation".

**Conclusion.** Dart `const <String>{ … }` → C# `private static
readonly FrozenSet<string> Name = new[] { … }.ToFrozenSet(
StringComparer.Ordinal);` initialised at type-load. Lookup
`name.contains(s)` → `Name.Contains(s)`. `FrozenSet` is chosen over
`HashSet` for immutability and over `ImmutableHashSet` for lookup
speed (Microsoft Learn `FrozenSet<T>`: "Optimized for fast lookups
and enumeration"). On .NET < 8 a fallback `ImmutableHashSet<string>`
with the same comparer is acceptable; the spec targets .NET 8+ per
the broader codeconv target framework.

### rf-dart-mutable-local-traversal-loop-with-nullable-pointer-to-csharp-pattern-variable-while (NEW idiom)

**Deep analysis.** `_printList` walks a possibly-improper list by
maintaining `Term? current` and `Term? tail` locals, advancing
`current` to `current.tail` on each cons cell, breaking out when the
tail is null (proper list) or not a `ListTerm` (improper list). The
loop relies on Dart's nullable-aware type promotion: `while (current
is ListTerm && !current.isNil) { … current.head, current.tail …}`
promotes `current` to `ListTerm` inside the body.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`
(cached) — Microsoft Learn: declaration patterns inside `while`
loop conditions bind the variable for the loop body's scope. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references`
(cached) — NRT flow analysis: `is null` / `is not null` narrows the
nullable state for the following block. Verbatim queries: "C# pattern
variable scope while loop body"; "C# nullable reference type flow
analysis is null narrowing".

**Conclusion.** The Dart pattern `while (current is ListTerm &&
!current.isNil) { use current.head, current.tail }` becomes the C#
pattern `while (current is ListTerm cur && !cur.IsNil) { use cur.Head
(after null check), cur.Tail }` — the pattern variable `cur` is
scoped to the loop body and refers to `current` typed as `ListTerm`
(non-null in the body). The reassignment `current = cur.Tail;`
rebinds `current` (an outer-scope nullable `Term?`) to the next
position; the loop head re-evaluates the pattern against the new
`current`.

### rf-dart-runtime-type-test-polymorphic-value-to-csharp-switch-expression-when-and-invariant-culture (NEW idiom)

**Deep analysis.** `_printConstValue` discriminates an `Object?
value` across: null → `"null"`; String + atom-shape → unquoted;
String otherwise → escaped+quoted; numeric (int or double) →
default `toString`; fallback → `toString`. The pattern is a runtime-
type dispatch with a SECONDARY condition on the string arm
(`_isAtom(value)`).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression`
(cached) — Microsoft Learn: "You can use a `when` clause to add a
condition to a pattern" — fuses pattern + extra Boolean into one arm.
WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings`
— "By default … numeric values format according to the current
culture." WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo.invariantculture`
— "The `InvariantCulture` property is the only culture that's never
language-specific and remains stable across machines, processes, and
applications." WebFetch `https://api.dart.dev/dart-core/double/toString.html`
— Dart: "Returns the shortest representation that converts back to the
original value." Locale-independent. Verbatim queries: "C# switch
expression when clause guarded pattern arm"; "C# int.ToString
CultureInfo.InvariantCulture digit normalization"; "Dart int.toString
double.toString locale independence".

**Conclusion.** `value switch { null => "null", string s when
IsAtom(s) => s, string s => $"\"{EscapeString(s)}\"", int i =>
i.ToString(CultureInfo.InvariantCulture), double d =>
d.ToString(CultureInfo.InvariantCulture), _ => value.ToString() ??
"null" }`. The `when`-guarded string arm precedes the unguarded one
(arm order is significant — Microsoft Learn switch-expression: "the
first matching expression"). Invariant culture is MANDATORY on
numerics — defaulting to current culture would produce locale-
dependent output (decimal separator, digit shapes) that the GLP
lexer would reject. This is the WRITE-SIDE counterpart to lexer.dart's
`rf-dart-number-parse-to-csharp-invariant-parse` READ-SIDE mandate.

### rf-dart-regex-anchored-char-class-to-csharp-regex-ismatch-cultureinvariant (NEW idiom)

**Deep analysis.** `_isAtom` makes two inline `RegExp` allocations:
`RegExp(r'[a-z]')` against `s[0]` and `RegExp(r'^[a-zA-Z0-9_]*$')`
against `s.substring(1)`. Both are ASCII character-class predicates
applied via `String.contains(Pattern)` (the Dart wrapper that
delegates to the RegExp engine's substring-search semantics).

**Anchor semantics — corrected reading.** A prior analysis claimed
the tail-regex `^[a-zA-Z0-9_]*$` always matches via the empty-string
`*`-match at position 0, making the tail check a no-op. THIS CLAIM
IS WRONG. Both Dart `RegExp` (with default `multiLine: false`) and
.NET `Regex` (with `RegexOptions.Multiline` off by default) treat
`^` as START-OF-INPUT and `$` as END-OF-INPUT. For an input like
`"world!"` (length 6), the engine tries position 0 with `^`; the
empty `*`-match leaves the cursor at position 0; `$` then requires
cursor == length, i.e. 0 == 6, which fails. The engine then has no
other start position to try because `^` only matches at position 0.
The overall match fails. Empirical verification (13 test inputs)
confirms `_isAtom` returns exactly the values its docstring promises:
`_isAtom("hello") == true`, `_isAtom("hello world!") == false`,
`_isAtom("foo_bar123") == true`, `_isAtom("Hello") == false`,
`_isAtom("_foo") == false`, `_isAtom("") == false`. There is no bug;
the function correctly enforces "lowercase ASCII start, alphanumeric
tail".

**Research (authoritative).** WebFetch
`https://api.dart.dev/dart-core/RegExp-class.html` — Dart: "If
`multiLine` is `true`, then the assertions `^` and `$` match the
beginning and end of a line, in addition to matching the beginning
and end of the input, respectively. If `multiLine` is `false` …
`^` only matches at the beginning of the input and `$` only at the
end." The `RegExp` constructor's `multiLine` parameter defaults to
`false`. WebFetch
`https://api.dart.dev/dart-core/String/contains.html` — Dart: `String.
contains(Pattern other, [int startIndex = 0])` "Whether this string
contains a match of `other`." When the Pattern is a RegExp with `^`
anchored, the only candidate start position the engine can use is
position 0 (or `startIndex` if specified); subsequent positions
cannot satisfy `^`. WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.ismatch`
— Microsoft Learn: `Regex.IsMatch(string input, string pattern)`
"Indicates whether the specified regular expression finds a match in
the specified input string." WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions`
— Microsoft Learn `RegexOptions.Multiline`: "Multiline mode. Changes
the meaning of `^` and `$` so they match at the beginning and end,
respectively, of any line, and not just the beginning and end of the
entire string." Off by default; `^` and `$` are input anchors.
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regexoptions`
— `RegexOptions.CultureInvariant`: "Specifies that cultural
differences in language is ignored." Verbatim queries: "Dart RegExp
multiLine default ^ $ anchor"; "C# Regex.IsMatch
RegexOptions.CultureInvariant ASCII character class"; "C# Regex
anchored pattern start of input behaviour".

**Conclusion.** Faithful 1:1 translation: each Dart `RegExp(...)`
call becomes a C# `Regex.IsMatch(...)` call with the same pattern
literal. `Regex.IsMatch(s[0].ToString(), "[a-z]")` mirrors `s[0].
contains(RegExp(r'[a-z]'))`; `Regex.IsMatch(s.Substring(1), @"^[a-zA-Z0-9_]*$")`
mirrors `s.substring(1).contains(RegExp(r'^[a-zA-Z0-9_]*$'))`. Both
engines preserve the same anchor and character-class semantics by
default; explicit `RegexOptions.CultureInvariant` is recommended on
the C# side to guard against Turkish-locale dotted-I oddities (the
ASCII character classes `[a-z]` and `[a-zA-Z0-9_]` are unaffected
in practice, but the flag documents intent). Hoisting both regexes
to `private static readonly Regex` instances with
`RegexOptions.Compiled | RegexOptions.CultureInvariant` is a
performance refinement (Dart's per-call `RegExp(...)` allocates each
visit; the C# hoist amortises construction).

**Alternative (performance-oriented, semantically equivalent).** An
inline ASCII char-range loop is an acceptable codegen optimisation:
`if (string.IsNullOrEmpty(s)) return false; if (!(s[0] >= 'a' && s[0]
<= 'z')) return false; for (int i = 1; i < s.Length; i++) { var c =
s[i]; if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c
>= '0' && c <= '9') || c == '_')) return false; } return true;`.
Both forms are SEMANTICALLY IDENTICAL for the reachable input domain
(ASCII-only, per the surrounding GLP lexer's identifier scanner).
The spec chooses the regex form for the target_decision because it
mirrors the Dart source line-for-line and removes any chance of
hand-translation drift; the char-range form is named as the
alternative for use by codegen when the printer's negligible call
volume does not justify regex overhead.

### rf-dart-chained-replaceall-to-csharp-chained-replace-ordinal (NEW idiom)

**Deep analysis.** `_escapeString` chains four `replaceAll` calls in
a load-bearing order: `\\` → `\\\\` MUST be first so subsequently
inserted backslashes (in the `\\n`, `\\t`, `\\"` outputs of later
steps) are NOT themselves re-escaped. The order is identical to
classic JSON-string escape implementations.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.string.replace`
— Microsoft Learn `String.Replace(String, String)`: "Returns a new
string in which all occurrences of a specified string in the current
instance are replaced with another specified string. … This method
performs an ordinal (case-sensitive and culture-insensitive)
comparison." WebFetch
`https://api.dart.dev/dart-core/String/replaceAll.html` — Dart:
`replaceAll(Pattern from, String replace)` "Replaces all
substrings that match `from` with `replace`. … If `from` is a String,
it's the exact substring to replace." Verbatim query: "C#
String.Replace ordinal default no culture argument".

**Conclusion.** Direct 1:1 chain preserving order:
`s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n",
"\\n").Replace("\t", "\\t");`. Both Dart and C# use ordinal
substring matching for string-argument replacement; no explicit
comparer needed. Order preservation is the load-bearing invariant.

### rf-dart-relative-import-to-csharp-using-or-same-namespace (NEW idiom — namespace-mapping deferred)

**Deep analysis.** The single Dart import `import 'ast.dart';` is a
relative import of a same-directory library. C# does not have
file-to-file imports; visibility is controlled via namespaces +
`using`. If feature 018's codegen places `lib/compiler/*.dart` →
`Glp.Compiler.*` (one namespace per directory), no `using` is needed;
if it places each file in a sub-namespace (`Glp.Compiler.Ast`,
`Glp.Compiler.GlpPrinter`), a `using Glp.Compiler.Ast;` is required.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive`
— Microsoft Learn `using` directive: "Allow the use of types in a
namespace so you don't have to qualify the use of a type in that
namespace." WebFetch `https://dart.dev/language/libraries#using-libraries`
— Dart: "Use `import` to specify how a namespace from one library is
used in the scope of another library." Verbatim queries: "C# using
directive namespace visibility"; "Dart import library namespace".

**Conclusion.** Emit a placeholder dependency on `ast.dart`'s
namespace; the codegen stage resolves it to either nothing
(same-namespace) or `using Glp.Compiler.Ast;` (sub-namespace per
file) when feature 018 fixes the directory→namespace policy. The
conversion-unit list above records the dependency explicitly.

### Trivial constructs

Doc-comments (`///`) on each method map mechanically to C# XML-doc
`<summary>` comments and carry NO behavioural decision (informational
only — trivial). The class-level doc comment "Converts GLP AST back
to source code" is preserved verbatim. Section banner `// ...`
comments (e.g. `// Head`, `// Guards and body`, `// Body separator -
only use | if there are guards`) preserve as C# `//` comments. The
public/private method-name renaming `camelCase` → `PascalCase` is
the established mechanical convention and is not researched per
construct.
