# Conversion Spec — lib/compiler/pmt/type_checker.dart

> Conversion-spec artifact for `lib/compiler/pmt/type_checker.dart` (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/type_checker.dart
source_sha256: ec1d9f359ba877391bcc2acf4b8a4b99f7a37b8ae05d778864d89fb9940d5c95
target_code_unit: lib/compiler/pmt/type_checker.cs
constructs:
  - construct_key: dart.value_class.error_record_final_fields_message_line_column_nullable_suggestion_named_optional_tostring_override
    source_form: >-
      class TypeError { final String message; final int line; final int column;
      final String? suggestion; TypeError(this.message, this.line, this.column,
      {this.suggestion}); @override String toString() { var result = '[type]
      $message at Line $line, Column $column'; if (suggestion != null) {
      result += '\n  Suggestion: $suggestion'; } return result; } }
    target_decision: >-
      Emit a `sealed` reference-type C# class `TypeError` (NOT a record, NOT
      an exception). Three get-only non-nullable auto-properties `Message`
      (string), `Line` (int), `Column` (int) plus ONE nullable get-only
      auto-property `Suggestion` (string?), initialised from a single
      constructor `TypeError(string message, int line, int column, string?
      suggestion = null)`. The Dart named-optional `{this.suggestion}` (no
      default ⇒ `null` default per dart.dev `language/functions#parameters`)
      maps to a C# optional parameter with default `null` (Microsoft Learn
      "Named and Optional Arguments"); existing Dart call sites that pass
      `suggestion:` as a named argument continue to work because C# permits
      both positional and named for optional parameters (strict widening, no
      regression). Override `ToString()` with the two-branch shape: build a
      local `var result = $"[type] {Message} at Line {Line}, Column {Column}";`
      then `if (Suggestion is not null) result += $"\n  Suggestion: {Suggestion}";`
      then return `result`. The `\n` literal must remain `"\n"` (LF), NEVER
      `Environment.NewLine` — same load-bearing newline-portability nuance as
      `type_table.dart.md`'s `rf-dart-stringbuffer-to-csharp-stringbuilder`
      finding. The class is declared `sealed` (no subclasses; mirrors
      `errors.dart.md`'s `dart.value_class.error_record_final_fields_message_line_column_tostring_override`
      precedent for an equality-bearing reference type). UNLIKE `PmtError` in
      errors.dart, this Dart source does NOT hand-write `operator ==` or
      `hashCode` — so the C# port also does NOT implement `IEquatable<TypeError>`:
      reference equality is preserved (the Dart class is collected into
      `List<TypeError>` and consumed by `toString`, never put in a `HashSet`
      or used as a `Map` key). This is a DELIBERATE divergence from
      `errors.dart.md`'s value-equality recipe — driven by the Dart source's
      observable equality contract, NOT by relaxing it.
    idiom_id: null
    research_finding_id: rf-dart-error-class-no-equality-override-to-csharp-sealed-class
    nuance: >-
      Three load-bearing nuances. (1) Null-safety mapping: Dart `String?
      suggestion` → C# `string? Suggestion` under enabled NRT, the `if
      (suggestion != null)` Dart branch → C# `if (Suggestion is not null)`
      (the `is not null` pattern is the documented .NET idiom — Microsoft
      Learn "patterns" reference — over `!= null` for nullable reference
      types). (2) Equality-contract preservation: errors.dart's PmtError
      hand-wrote `==`/`hashCode`, dictating value equality on the C# side;
      THIS class does NOT, dictating reference equality on the C# side — the
      conversion preserves the source's observable contract verbatim, NOT a
      project-wide "always value equality" template. (3) Newline portability:
      the hard-coded `\n` in `'\n  Suggestion: $suggestion'` is U+000A LF in
      Dart (no host-OS dependence); the C# port emits `"\n"` literally (NOT
      `Environment.NewLine`) so test goldens stay bit-identical across
      Linux/macOS/Windows. The `[type]` prefix and the indented two-space
      `  Suggestion:` continuation are part of an observable error-formatting
      contract that downstream tooling parses; the spec preserves them
      verbatim.
  - construct_key: dart.coordinator_class.two_constructor_fields_module_traversal_returning_error_list
    source_form: >-
      class TypeChecker { final TypeTable typeTable; final ModeTable modeTable;
      TypeChecker(this.typeTable, this.modeTable); List<TypeError>
      checkModule(Module module) { ... } List<TypeError> checkClause(Clause
      clause, ModeDeclaration modeDecl) { ... } List<TypeError> checkTerm(...)
      { ... } ... }
    target_decision: >-
      Emit a non-sealed reference-type `class TypeChecker` with two private
      readonly fields populated by the primary positional constructor:
      `private readonly TypeTable _typeTable; private readonly ModeTable
      _modeTable;` constructed by `public TypeChecker(TypeTable typeTable,
      ModeTable modeTable) { _typeTable = typeTable; _modeTable = modeTable;
      }`. The Dart fields are `final` (reference-immutable; the table
      instances themselves are mutable containers, but the TypeChecker's
      handles on them never change) — `readonly` is the exact mirror.
      Constructor parameter aliasing (NOT cloning) is preserved: the Dart
      `this.typeTable, this.modeTable` initialising-formals store the
      caller's references; the C# port stores the same references. NO
      `IEquatable<TypeChecker>` (no equality semantics in Dart source). The
      three public methods (`CheckModule`, `CheckClause`, `CheckTerm`) plus
      five internal predicates / formatters (`IsValidConstant`,
      `IsValidStructConstructor`, `_IsCapitalized`, `_TypeContainsAtom`,
      `GetValidConstructors`) preserve their Dart access surface (PascalCase
      public; leading-underscore Dart private → C# `private`). The class
      itself is NOT static (it holds the two table references); it is also
      NOT sealed because no project precedent locks coordinator-style
      checkers and downstream test fakes might subtype.
    idiom_id: null
    research_finding_id: rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields
    nuance: >-
      Reference-vs-value: `TypeChecker` is a coordinator with TWO dependency
      handles (no own mutable state) — it is a `class`, NEVER a `record` or
      `struct` (the table references identify a specific checker instance;
      structural equality would be misleading). Privacy: Dart fields
      `typeTable` and `modeTable` are non-underscore — *public* in Dart
      package-private terms — but no code outside this file actually reads
      them. The C# port narrows to `private readonly` because (a) the field
      access is purely internal (`typeTable.getType(...)` etc.) and (b)
      `errors.dart.md`'s pattern (sealed value classes with get-only public
      props) doesn't apply — these are not value-bearing fields, they are
      injected dependencies. The spec MAY also expose `public TypeTable
      TypeTable { get; }` / `public ModeTable ModeTable { get; }` if a
      downstream consumer needs introspection; the current source has no
      such consumer, so codegen emits the minimal private form. Threading:
      synchronous, no `async`/`Future`/isolate (those well-known nuances
      are correctly absent — US2-AS4).
  - construct_key: dart.list_accumulator.errors_addAll_per_iteration_skip_when_null_lookup
    source_form: >-
      List<TypeError> checkModule(Module module) { final errors =
      <TypeError>[]; for (final proc in module.procedures) { final modeDecl =
      modeTable.getDeclaration(proc.name, proc.arity); if (modeDecl == null)
      continue; for (final clause in proc.clauses) { errors.addAll(
      checkClause(clause, modeDecl)); } } return errors; }
    target_decision: >-
      Emit `public List<TypeError> CheckModule(Module module) { var errors =
      new List<TypeError>(); foreach (var proc in module.Procedures) { var
      modeDecl = _modeTable.GetDeclaration(proc.Name, proc.Arity); if
      (modeDecl is null) continue; foreach (var clause in proc.Clauses) {
      errors.AddRange(CheckClause(clause, modeDecl)); } } return errors; }`.
      Dart `<TypeError>[]` → C# `new List<TypeError>()` (mutable list,
      identical semantics; `List<T>` is the documented direct equivalent —
      Microsoft Learn `List<T>`). Dart `errors.addAll(other)` → C#
      `errors.AddRange(other)` (Microsoft Learn `List<T>.AddRange`: "Adds
      the elements of the specified collection to the end of the
      List<T>."). Dart `for (final x in xs)` → C# `foreach (var x in xs)`
      (one-to-one; no `for (var i = 0; ...)` rewrite because the loop is
      not index-dependent). The `continue` statement maps verbatim
      (Microsoft Learn `continue` statement: "passes control to the next
      iteration of the enclosing iteration statement"). The `modeDecl ==
      null` Dart check → C# `modeDecl is null` (the `is null` pattern is
      the .NET-idiomatic nullable-reference branch — preferred over `==
      null` to bypass any user-defined `operator ==` and align with the
      pattern-matching style established across this 018 corpus).
      `modeTable.getDeclaration` returns `ModeDeclaration?` per
      `mode_table.dart.md`'s `dart.nullable_first_or_default_via_isnotempty_then_first_bang`
      construct — C# return type is `ModeDeclaration?` under enabled NRT,
      matching the Dart nullable contract.
    idiom_id: null
    research_finding_id: rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange
    nuance: >-
      The `<TypeError>[]` Dart typed-empty-list literal is a fresh allocation
      per invocation (NOT a shared static); the C# `new List<TypeError>()`
      preserves that — codegen MUST NOT hoist to a shared field or
      `Array.Empty<>()`. `module.procedures` and `proc.clauses` are
      `List<Procedure>` and `List<Clause>` per ast.dart's surface — both
      iterate in source order, both deterministic; preserved verbatim via
      `foreach`. The `if (modeDecl == null) continue;` early-skip is
      load-bearing: procedures WITHOUT a mode declaration are silently
      skipped (the doc-comment on `getDeclaration` says "No declaration,
      skip type checking"), NOT errored. Codegen MUST preserve the silent
      skip — promoting it to an error would change observable behaviour
      under FR-024 / FR-023. Reference-aliasing: the `modeDecl` returned
      from `_modeTable.GetDeclaration` is the SAME reference stored inside
      the mode table (per `mode_table.dart.md`'s reference-aliasing nuance);
      the C# port passes the same reference into `CheckClause`. No copy.
  - construct_key: dart.indexed_dual_list_for_loop.parallel_arg_iteration_with_min_length_guard
    source_form: >-
      for (int i = 0; i < clause.head.args.length && i < modeDecl.args.length;
      i++) { final arg = clause.head.args[i]; final declaredType =
      modeDecl.args[i].typeName; final typeParams = modeDecl.args[i]
      .typeParams; errors.addAll(checkTerm(arg, declaredType, typeParams)); }
      // and similar nested form inside the body-goals loop
    target_decision: >-
      Emit a classic indexed C# `for` loop with the SAME min-length guard:
      `for (int i = 0; i < clause.Head.Args.Count && i < modeDecl.Args.Count;
      i++) { var arg = clause.Head.Args[i]; var declaredType = modeDecl.Args
      [i].TypeName; var typeParams = modeDecl.Args[i].TypeParams; errors.
      AddRange(CheckTerm(arg, declaredType, typeParams)); }`. Dart `.length`
      on a `List<T>` → C# `.Count` on `List<T>` / `IReadOnlyList<T>` (NOT
      `.Length` — `.Length` is reserved for arrays, strings, StringBuilder
      per the Framework Design Guidelines, same as `mode_table.dart.md`'s
      `rf-dart-length-isempty-to-csharp-count` finding). Do NOT rewrite to
      LINQ `Zip(...)` or `Enumerable.Range(0, Math.Min(...)).Select(...)`:
      the loop has an observable side-effect (`errors.AddRange(...)`) and a
      short-circuit min-length semantic that LINQ would obscure; the
      imperative form preserves both. The loop body's three local reads
      (`arg`, `declaredType`, `typeParams`) are kept as locals (NOT inlined)
      to preserve diff stability with the Dart source. Both forms (head
      args at line 57-62 and goal args at line 70-75) use the same shape;
      codegen emits each independently.
    idiom_id: null
    research_finding_id: rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for
    nuance: >-
      The min-length-guard `i < a.length && i < b.length` (Dart) ↔ `i <
      a.Count && i < b.Count` (C#) is a deliberate parallel-iteration
      idiom that TOLERATES length mismatch: if `clause.head.args.length > modeDecl.args.length`
      the trailing head args go unchecked (and vice versa). This is a
      load-bearing semantic — promoting it to an assertion or a thrown
      `ArgumentException` would change the conversion's observable
      behaviour for malformed input (arity mismatch is silently tolerated
      at the type-checker layer; presumably an earlier compiler stage
      catches it). Codegen MUST preserve the silent-tolerance shape.
      Indexer access `args[i]` is the C# `List<T>` / `IReadOnlyList<T>`
      indexer (O(1); preserves Dart `List` indexer semantics — both throw
      `ArgumentOutOfRangeException` / `RangeError` on out-of-bounds, but
      the loop guard guarantees in-bounds access).
  - construct_key: dart.nullable_collection_iteration.body_questionmark_bang_pattern
    source_form: >-
      if (clause.body != null) { for (final goal in clause.body!) { ... } }
    target_decision: >-
      Emit `if (clause.Body is not null) { foreach (var goal in clause.Body)
      { ... } }`. The C# null-conditional pattern eliminates Dart's `clause
      .body!` (force-unwrap) entirely — within the `if (clause.Body is not
      null)` block, the flow-sensitive nullability analysis of the C#
      compiler (Microsoft Learn "Nullable reference types" — flow-sensitive
      null-state analysis under NRT) narrows `clause.Body` from
      `List<Goal>?` to `List<Goal>`, so the inner `foreach (var goal in
      clause.Body)` requires no `!`. Do NOT translate the Dart `clause.body!`
      to C# `clause.Body!` inside the conditional — the C# null-forgiving
      operator is documented as a last-resort tool (Microsoft Learn
      "Null-forgiving operator (postfix !)": "you use the operator to tell
      the compiler that you know that ... isn't null"); using it where the
      compiler's flow analysis already narrows the type is anti-idiomatic.
    idiom_id: null
    research_finding_id: rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access
    nuance: >-
      Null-safety mapping is the load-bearing nuance — Dart and C# have
      TEXTUALLY similar but SEMANTICALLY different null-assertion idioms.
      Dart `clause.body!` is the Dart bang-operator (force-unwrap):
      throws `TypeError` at runtime if `body` is null. C# `clause.Body!`
      is the null-forgiving operator: a COMPILE-TIME hint to the analyser
      with NO runtime check (Microsoft Learn: "The unary postfix `!`
      operator is the null-forgiving operator. ... It doesn't have any
      effect at run time"). The Dart safety guarantee (runtime throw on
      misuse) does NOT translate directly; only the FLOW-NARROWED access
      (`if (x is not null) { use x; }`) preserves both the safety check
      AND the static guarantee. Codegen MUST emit the flow-narrowed form,
      NOT the bang-for-bang translation. The same pattern applies later
      at `term.head!` (lines 144, 149, 154) and `term.tail!` (line 159)
      — each occurs inside an `if (term.head != null)` / `if (term.tail
      != null)` guard, so each C# `term.Head` / `term.Tail` access inside
      that branch is flow-narrowed (no `!` needed).
  - construct_key: dart.is_type_test_chain.checkTerm_dispatch_over_term_subtypes
    source_form: >-
      if (term is VarTerm) { return errors; } if (term is UnderscoreTerm)
      { return errors; } if (term is ConstTerm) { ... return errors; } if
      (term is ListTerm) { ... return errors; } if (term is StructTerm)
      { ... return errors; } return errors;
    target_decision: >-
      Emit a chain of C# `if (term is VarTerm) return errors;` `if (term
      is UnderscoreTerm) return errors;` `if (term is ConstTerm constTerm)
      { ... return errors; }` `if (term is ListTerm listTerm) { ... return
      errors; }` `if (term is StructTerm structTerm) { ... return errors;
      }` `return errors;`. The Dart `is` operator (Dart "type-test
      operator" — dart.dev `/language/operators`) and C# `is` operator
      (Microsoft Learn `is` operator: "Checks if an object is compatible
      with a given type") are direct equivalents. The Dart pattern relies
      on SMART CASTING (after `if (term is ConstTerm)` the variable
      `term` is statically `ConstTerm` inside the branch); C# requires
      DECLARATION-PATTERN-MATCHING with a binding (Microsoft Learn
      "Pattern matching - The `is` and `as` operators": "Use the
      declaration pattern with an existing variable to test whether an
      input expression is of an expected type and, if so, assign it to a
      pre-existing variable") to get the same narrowed access without
      writing `(ConstTerm)term` casts. The introduced binding names
      (`constTerm`, `listTerm`, `structTerm`) are required by the C#
      pattern-matching syntax — Dart's smart cast has no name-binding
      requirement. Do NOT rewrite to a `switch` expression: the Dart
      source uses sequential `if` chains with `return` statements; the
      `switch` rewrite would obscure the `return` early-exit semantics
      and the (currently dead, but reachable on a future term subtype)
      trailing `return errors;`.
    idiom_id: null
    research_finding_id: rf-dart-is-test-smart-cast-to-csharp-declaration-pattern
    nuance: >-
      Two nuances. (1) Smart-cast asymmetry: Dart `if (term is ConstTerm)
      { use term.value; }` works because the compiler narrows `term` to
      `ConstTerm` for the branch (Dart language doc, type promotion).
      C# does NOT promote the original variable's static type — without
      the `constTerm` binding, the inner access `term.value` would not
      compile (the static type is still `Term`). The declaration-pattern
      binding `if (term is ConstTerm constTerm)` introduces a fresh
      narrowed local for the branch (Microsoft Learn). Codegen MUST
      emit the binding wherever the Dart branch body accesses a
      subtype-specific member. (2) Exhaustiveness: the Dart `Term`
      hierarchy is sealed by convention (the 5 subtypes
      `VarTerm`/`UnderscoreTerm`/`ConstTerm`/`ListTerm`/`StructTerm` are
      the only subclasses observable to the type-checker); the trailing
      Dart `return errors;` is the fall-through for "unknown future
      subtype". The C# port preserves this fall-through verbatim — DO
      NOT mark the `Term` C# class `sealed` here unless the ast.dart
      conversion-spec asserts it; the trailing `return errors;` is the
      safe, source-preserving default. The 5 subtypes are referenced
      from ast.dart's import (`package:glp_runtime/compiler/ast.dart`)
      — their C# definitions are owned by ast.dart's conversion-spec,
      not this one (cross-file alignment, recorded in the Notes section
      below).
  - construct_key: dart.error_construction_with_optional_named_argument.suggestion_via_ternary_or_null
    source_form: >-
      errors.add(TypeError("'${term.value}' is not a valid '$expectedType'",
      term.line, term.column, suggestion: validConstructors.isNotEmpty ?
      "Valid constructors: ${validConstructors.join(', ')}" : null,));
    target_decision: >-
      Emit `errors.Add(new TypeError($"'{constTerm.Value}' is not a valid
      '{expectedType}'", constTerm.Line, constTerm.Column, suggestion:
      validConstructors.Count > 0 ? $"Valid constructors: {string.Join(",
      ", validConstructors)}" : null));`. The named-argument call site
      `suggestion: ...` maps verbatim — C# allows named-argument syntax
      at any optional-parameter call site (Microsoft Learn "Named and
      Optional Arguments"). Dart `validConstructors.isNotEmpty` (Dart
      `Iterable.isNotEmpty` — dart.dev `dart:core/Iterable`) → C#
      `validConstructors.Count > 0` (on `List<string>`; no built-in
      `IsNotEmpty` property on .NET collections). Dart
      `list.join(', ')` → C# `string.Join(", ", list)` (Microsoft Learn
      `string.Join(string, IEnumerable<string>)`: "Concatenates the
      members of a constructed IEnumerable<T> collection of type
      String, using the specified separator between each member") —
      verbatim semantic equivalent. The string interpolations
      `"'${term.value}' is not a valid '$expectedType'"` and `"Valid
      constructors: ${validConstructors.join(', ')}"` map to C# `$"..."`
      interpolated strings (cached `rf-csharp-interpolated-string-equivalent-to-dart-interpolation`
      from `mode_table.dart.md`). Composite-formatting culture
      sensitivity: `term.value` is `Object?` in Dart (the `ConstTerm
      .value` field is typed `Object?`, per ast.dart line 147) and its
      `toString()` invocation is polymorphic both ways — same nuance as
      the `'  $def'` interpolation in `type_table.dart.md`'s `toString`
      override.
    idiom_id: null
    research_finding_id: rf-dart-list-join-to-csharp-string-join-separator-first
    nuance: >-
      Three nuances. (1) Separator-position swap: Dart `list.join(sep)`
      is a method ON the list with the SEPARATOR as argument; C# `string.
      Join(sep, list)` is a STATIC method with the SEPARATOR FIRST and
      the list second. Codegen MUST emit the swap correctly. A naïve
      "method-to-method" mapping that wrote `validConstructors.Join(",
      ")` would not compile (no such instance method on `List<T>`). (2)
      Empty-list behaviour: Dart `[].join(', ')` returns `""` (empty
      string); C# `string.Join(", ", emptyList)` also returns `""`
      (Microsoft Learn: "If `values` is empty or contains no elements
      other than null, the method returns String.Empty.") — semantics
      preserved. (3) `ConstTerm.value` is Dart `Object?` — its
      `toString()` invocation in the interpolation `'${term.value}'`
      can return `"null"` if the value is null (Dart `null.toString()`
      returns `"null"`). The C# `{constTerm.Value}` interpolation
      invokes `object?.ToString()`, which the compiler synthesises as
      `value?.ToString() ?? ""` (Microsoft Learn — interpolated string
      formatting with null reference is `""`, NOT `"null"`). This is a
      SUBTLE DIVERGENCE; in practice `ConstTerm.value` is never null in
      the type-checker's input path (the parser only emits `ConstTerm`
      with a non-null value — `value is String` / `value is num`
      branches dominate `isValidConstant` below); but the codegen MUST
      be aware that if a `ConstTerm.value == null` ever leaked through,
      the error message would read `"'' is not a valid '...'"` in C# vs
      `"'null' is not a valid '...'"` in Dart. The spec accepts this
      divergence as benign (the parser invariant makes it unreachable);
      a stricter codegen could emit `{constTerm.Value?.ToString() ??
      "null"}` to preserve Dart's literal-null string, but the spec
      does NOT mandate it — the parser invariant + the rarity of a
      null `Object?` in this path makes the simpler form preferable.
  - construct_key: dart.checkterm_listterm_branch.element_type_resolution_via_substitution_then_recurse
    source_form: >-
      if (term is ListTerm) { if (term.isNil) { return errors; } final
      typeDef = typeTable.getType(expectedType); if (typeDef != null) {
      final hasListCtor = typeDef.constructors.any((c) => c is
      ListConstructor); if (!hasListCtor && expectedType != 'List') {
      errors.add(TypeError("List term not valid for type '$expectedType'",
      term.line, term.column,)); return errors; } final typeParamSubst =
      <String, String>{}; for (int i = 0; i < typeDef.typeParams.length
      && i < typeParams.length; i++) { typeParamSubst[typeDef.typeParams[i]]
      = typeParams[i]; } final listCtor = typeDef.constructors
      .whereType<ListConstructor>() .where((c) => !c.isNil) .firstOrNull;
      if (listCtor != null && listCtor.head != null && term.head != null)
      { final paramTypeName = listCtor.head!.typeName; final elementType =
      typeParamSubst[paramTypeName] ?? paramTypeName; if (elementType !=
      '_') { errors.addAll(checkTerm(term.head!, elementType, [])); } } }
      else if (typeParams.isNotEmpty && term.head != null) { errors.addAll(
      checkTerm(term.head!, typeParams[0], [])); } if (term.tail != null)
      { errors.addAll(checkTerm(term.tail!, expectedType, typeParams)); }
      return errors; }
    target_decision: >-
      Emit a C# branch under `if (term is ListTerm listTerm) { ... }` that
      preserves the FIVE distinct sub-paths verbatim. (a) Early-return
      empty-list: `if (listTerm.IsNil) return errors;`. (b) Type-resolved
      branch: `var typeDef = _typeTable.GetType(expectedType); if (typeDef
      is not null) { ... } else if (typeParams.Count > 0 && listTerm.Head
      is not null) { errors.AddRange(CheckTerm(listTerm.Head, typeParams
      [0], Array.Empty<string>())); }`. (c) Inside the type-resolved
      block: `bool hasListCtor = typeDef.Constructors.Any(c => c is
      ListConstructor); if (!hasListCtor && expectedType !=
      "List") { errors.Add(new TypeError($"List term not valid for type
      '{expectedType}'", listTerm.Line, listTerm.Column)); return errors;
      }`. (d) Type-parameter-substitution map: `var typeParamSubst = new
      Dictionary<string, string>(StringComparer.Ordinal); for (int i = 0;
      i < typeDef.TypeParams.Count && i < typeParams.Count; i++) {
      typeParamSubst[typeDef.TypeParams[i]] = typeParams[i]; }`. The
      `StringComparer.Ordinal` is mandatory per the established
      project-wide string-key-ordinality discipline
      (`rf-csharp-string-equality-ordinal-by-default`, cached from
      `mode_table.dart.md`). (e) `whereType<T>().where(...).firstOrNull`
      → `Constructors.OfType<ListConstructor>().FirstOrDefault(c =>
      !c.IsNil)` (Microsoft Learn `Enumerable.OfType<TResult>`:
      "Filters the elements of an IEnumerable based on a specified
      type"; `Enumerable.FirstOrDefault(predicate)`: "Returns the
      first element of the sequence that satisfies a condition or a
      default value if no such element is found" — for a reference
      type `ListConstructor`, the default is `null`, matching Dart
      `firstOrNull` semantics exactly). The combined LINQ chain
      replaces TWO Dart calls (`.whereType<T>()` + `.firstOrNull` over
      a `.where(...)`) with ONE C# call — but the order of operations
      is preserved: type-filter first, predicate-filter second,
      first-or-null third. (f) Element-type resolution + recurse:
      `if (listCtor is not null && listCtor.Head is not null &&
      listTerm.Head is not null) { var paramTypeName = listCtor.Head
      .TypeName; var elementType = typeParamSubst.TryGetValue(
      paramTypeName, out var substituted) ? substituted : paramTypeName;
      if (elementType != "_") { errors.AddRange(CheckTerm(listTerm.Head,
      elementType, Array.Empty<string>())); } }`. The Dart `typeParamSubst[
      paramTypeName] ?? paramTypeName` (read-fallback-to-key idiom) maps
      to the documented C# `TryGetValue(out var ...) ? ... : ...`
      pattern (cached `rf-csharp-dictionary-trygetvalue-then-fallback-null`
      from `mode_table.dart.md`, generalised: here the fallback is the
      LOOKUP KEY itself, not `null`). (g) Tail-recurse: `if (listTerm.Tail
      is not null) { errors.AddRange(CheckTerm(listTerm.Tail, expectedType,
      typeParams)); } return errors;`. The empty-list-of-strings literal
      `[]` (third argument to recursive `checkTerm`) → C# `Array.Empty
      <string>()` (Microsoft Learn `Array.Empty<T>`: "Returns an empty
      array. ... If `T` is a reference type, the method returns a
      cached, zero-length array" — singleton, no allocation per call;
      preserves Dart's semantics where `[]` in argument position is a
      FRESH empty list, but here the callee `CheckTerm` only iterates
      its `typeParams` parameter — never mutates it — so the singleton
      cache is observationally equivalent and strictly more efficient).
      Alternative: `new List<string>()` (fresh allocation); the spec
      prefers `Array.Empty<string>()` because `IReadOnlyList<string>`
      is sufficient for the callee.
    idiom_id: null
    research_finding_id: rf-dart-wheretype-firstornull-chain-to-csharp-oftype-firstordefault
    nuance: >-
      Four nuances. (1) `whereType<T>()` → `OfType<T>()`: Dart
      `Iterable.whereType<T>()` (dart.dev: "Returns a new lazy
      Iterable with all elements that have type T") and C# `Enumerable
      .OfType<TResult>()` are SEMANTIC EQUIVALENTS — both lazily filter
      by runtime type. Codegen MUST use `OfType` (NOT `Cast<T>()` —
      `Cast<T>` throws on a type mismatch, `OfType<T>` silently skips,
      matching Dart's `whereType` exactly; cached from
      `well_typed_clause.dart.md`'s `Cast<TypeRef>()` finding, which
      reasoned the OPPOSITE direction — that file's source had
      `cast<T>` not `whereType<T>`). (2) `firstOrNull` → `FirstOrDefault`:
      Dart `firstOrNull` (dart.dev `IterableExtensions`: "The first
      element, or null if the iterable is empty") and C# `FirstOrDefault`
      have the same null-on-empty semantics for REFERENCE types; for
      VALUE types `FirstOrDefault` returns `default(T)` (not null). Here
      `ListConstructor` is a reference type (constructor classes are
      reference types by convention), so the semantics match exactly.
      (3) Type-parameter-substitution map: `<String, String>{}` Dart map
      literal → C# `new Dictionary<string, string>(StringComparer.Ordinal)`
      with EXPLICIT ordinal comparer (cached idiom). The substitution
      keys are abstract type-parameter names (`A`, `B`, ...) — pure
      ASCII identifiers — but the discipline of explicit `StringComparer
      .Ordinal` on every `Dictionary<string, V>` is project-wide and
      MUST be preserved. (4) `typeParamSubst[k] ?? k` (read-with-fallback-
      to-key): Dart's `Map[k]` returns `V?`, `?? k` falls back to the key
      string; the C# `TryGetValue(out var v) ? v : k` reproduces this
      exactly without throwing on miss. A naïve C# `typeParamSubst[k]
      ?? k` would THROW `KeyNotFoundException` on miss before the `??`
      ever evaluates — same hazard as `type_table.dart.md`'s
      `rf-dart-map-lookup-to-csharp-trygetvalue` finding, generalised to
      a non-null fallback. The escape hatch `if (elementType != "_")`
      branch preserves the spec's "any-type-marker skip" convention
      (the Dart underscore `_` here is a STRING value used as a wildcard
      sentinel, NOT a Dart identifier) — codegen MUST emit the literal
      `"_"` comparison verbatim.
  - construct_key: dart.checkterm_structterm_branch.delegating_to_isvalidstructconstructor_with_error_emission
    source_form: >-
      if (term is StructTerm) { if (!isValidStructConstructor(term,
      expectedType, typeParams)) { final validConstructors =
      getValidConstructors(expectedType); errors.add(TypeError(
      "'${term.functor}(...)' is not a valid '$expectedType'", term.line,
      term.column, suggestion: validConstructors.isNotEmpty ? "Valid
      constructors: ${validConstructors.join(', ')}" : null,)); } return
      errors; }
    target_decision: >-
      Emit `if (term is StructTerm structTerm) { if (!IsValidStructConstructor(
      structTerm, expectedType, typeParams)) { var validConstructors =
      GetValidConstructors(expectedType); errors.Add(new TypeError($"'
      {structTerm.Functor}(...)' is not a valid '{expectedType}'",
      structTerm.Line, structTerm.Column, suggestion: validConstructors
      .Count > 0 ? $"Valid constructors: {string.Join(", ",
      validConstructors)}" : null)); } return errors; }`. Same shape as
      the `ConstTerm` branch above (same string-join + named-suggestion
      idiom; same TypeError construction). The interpolation differs
      only at the functor surface — `${term.functor}(...)` includes the
      literal `(...)` suffix indicating struct arity in error messages.
      The C# `{structTerm.Functor}(...)` interpolation preserves that
      literal `(...)` verbatim (the parens-dots-parens is observable in
      the error string for human reviewers and downstream test goldens
      — codegen MUST NOT helpfully expand to actual arity rendering).
    idiom_id: rf-dart-list-join-to-csharp-string-join-separator-first
    research_finding_id: rf-dart-list-join-to-csharp-string-join-separator-first
    nuance: >-
      Reuses the `rf-dart-list-join-to-csharp-string-join-separator-first`
      finding established for the ConstTerm branch above (SAME file —
      first use defines, second use reuses, satisfying SC-007 "recurring
      constructs resolved via a recorded idiom"). The struct-arity
      `(...)` literal is a cosmetic detail of the Dart source's error-
      formatting style — preserved verbatim, no synthesis. The delegate
      call `isValidStructConstructor(term, expectedType, typeParams)`
      becomes `IsValidStructConstructor(structTerm, expectedType,
      typeParams)` — the third positional `typeParams` argument is the
      Dart optional-positional default-`const []` form on the callee
      (line 226), which in C# is a regular `IReadOnlyList<string>`
      parameter with default `null` (caller passes the value
      explicitly here, so the default never engages at this site).
  - construct_key: dart.predicate_method.isvalidconstant_builtin_then_userdefined_then_type_reference
    source_form: >-
      bool isValidConstant(ConstTerm term, String typeName) { if
      (typeName == 'Num' || typeName == 'Number' || typeName == 'Int' ||
      typeName == 'Integer') { return term.value is num; } if (typeName
      == 'Atom' || typeName == 'String') { return term.value is String;
      } final typeDef = typeTable.getType(typeName); if (typeDef == null)
      { return true; } final termValue = term.value.toString(); for (final
      ctor in typeDef.constructors) { if (ctor is AtomConstructor && ctor
      .name == termValue) { return true; } } for (final ctor in typeDef
      .constructors) { if (ctor is AtomConstructor && _isCapitalized(ctor
      .name)) { final refTypeDef = typeTable.getType(ctor.name); if
      (refTypeDef != null && _typeContainsAtom(refTypeDef, termValue)) {
      return true; } } } return false; }
    target_decision: >-
      Emit `public bool IsValidConstant(ConstTerm term, string typeName)
      { ... }`. Body in four phases: (1) Built-in numeric types: `if
      (typeName is "Num" or "Number" or "Int" or "Integer") return term
      .Value is double or float or int or long or short or byte or
      decimal;`. The Dart `term.value is num` test (Dart `num` is the
      abstract superclass of `int` and `double`) → C# does NOT have a
      single `num` supertype: `int` and `double` are unrelated value
      types (System.Int32 and System.Double, both ultimately under
      `System.ValueType` but not via a numeric supertype). The
      documented C# idiom (Microsoft Learn "Patterns - type patterns")
      is a disjunctive pattern: `term.Value is double or float or int
      or long or short or byte or decimal`. Codegen MAY narrow the set
      to `int or double` if ast.dart's `ConstTerm.value` is known to
      only carry those two (per ast.dart line 147 comment `// String,
      int, double, or atom name`); the safer-and-broader form covers
      every .NET numeric. (2) Built-in atom/string types: `if (typeName
      is "Atom" or "String") return term.Value is string;`. (3)
      User-defined-type fast-path: `var typeDef = _typeTable.GetType(
      typeName); if (typeDef is null) return true;` (the `null` return
      means "unknown type — allow", a load-bearing permissive default;
      see nuance). (4) AtomConstructor match — direct then transitive
      via type-references-with-capitalised-names. The two `foreach`
      loops are emitted verbatim: `var termValue = term.Value?.ToString()
      ?? "";` (preserve Dart's `term.value.toString()` — on Dart `null
      .toString()` returns `"null"`, on C# `null?.ToString() ?? ""`
      returns `""` — see nuance for the deliberate divergence). `foreach
      (var ctor in typeDef.Constructors) { if (ctor is AtomConstructor
      atomCtor && atomCtor.Name == termValue) return true; }` (early-
      exit on first match). Then `foreach (var ctor in typeDef.Constructors)
      { if (ctor is AtomConstructor atomCtor && _IsCapitalized(atomCtor
      .Name)) { var refTypeDef = _typeTable.GetType(atomCtor.Name); if
      (refTypeDef is not null && _TypeContainsAtom(refTypeDef,
      termValue)) return true; } } return false;`.
    idiom_id: null
    research_finding_id: rf-dart-numeric-supertype-is-num-to-csharp-disjunctive-type-pattern
    nuance: >-
      Three load-bearing nuances. (1) `num` supertype gap: Dart `num` is
      the abstract numeric supertype (dart.dev `dart-core/num-class`:
      "An integer or floating-point number") — `int is num` and `double
      is num` are both true. C# has NO equivalent supertype: `int` and
      `double` are sibling value types under `System.ValueType`. The
      documented C# bridge is a disjunctive type pattern (Microsoft
      Learn "Patterns": `expr is T1 or T2 or T3`) — semantically a
      union-check. Codegen MUST emit the disjunctive form, NOT
      `term.Value is IComparable` (over-matches `string`) and NOT
      `term.Value is ValueType && term.Value is not bool && term.Value
      is not char` (over-engineering; the spec prescribes the simpler
      explicit-numeric list). (2) `term.value.toString()` null-handling:
      Dart `Object.toString()` is defined on every object including
      `null` — `(null).toString()` returns `"null"` (the literal four-
      character string). C# `term.Value` is `object?`; `.ToString()` on
      a null reference would throw `NullReferenceException`. The
      documented null-safe idiom is `term.Value?.ToString() ?? ""`
      (Microsoft Learn null-conditional operator + null-coalescing).
      The spec uses `?? ""` (empty string fallback) — DIVERGING from
      Dart's `"null"` string. This is the SAME divergence flagged in
      the `dart.error_construction_with_optional_named_argument`
      construct above; same justification (parser invariant makes a
      `null` value unreachable in this path). Codegen MAY choose `??
      "null"` to preserve Dart's literal-null-string verbatim — the
      spec leaves it as an acknowledged minor divergence, not an
      escalation, because the parser invariant guarantees `term.Value`
      is non-null when the type-checker runs. (3) "Unknown type =
      allow": the `if (typeDef is null) return true;` fast-path is a
      PERMISSIVE DEFAULT — types not declared in `_typeTable` are
      TOLERATED, not errored. The doc-comment on line 198 says "Type
      not defined - allow (might be built-in or external)" — this is
      a load-bearing semantic the C# port MUST preserve verbatim.
      Promoting it to `false` (strict-reject) would change observable
      behaviour catastrophically — every external-type reference would
      become a type error. Codegen MUST emit `return true;` literally.
  - construct_key: dart.recursive_predicate_method.isvalidstructconstructor_with_typeparam_substitution
    source_form: >-
      bool isValidStructConstructor(StructTerm term, String typeName,
      [List<String> typeParams = const []]) { final typeDef = typeTable
      .getType(typeName); if (typeDef == null) { return true; } final
      typeParamSubst = <String, String>{}; for (int i = 0; i < typeDef
      .typeParams.length && i < typeParams.length; i++) { typeParamSubst[
      typeDef.typeParams[i]] = typeParams[i]; } for (final ctor in typeDef
      .constructors) { if (ctor is StructConstructor && ctor.functor ==
      term.functor) { return true; } } for (final ctor in typeDef
      .constructors) { if (ctor is AtomConstructor && _isCapitalized(ctor
      .name)) { final ctorName = ctor.name; if (typeParamSubst.containsKey(
      ctorName)) { final substitutedType = typeParamSubst[ctorName]!; if
      (isValidStructConstructor(term, substitutedType)) { return true; }
      } final modeDecl = modeTable.getDeclarationByTypeName(ctorName); if
      (modeDecl != null && modeDecl.predicate == term.functor) { return
      true; } } } return false; }
    target_decision: >-
      Emit `public bool IsValidStructConstructor(StructTerm term, string
      typeName, IReadOnlyList<string>? typeParams = null) { ... }`. The
      Dart OPTIONAL-POSITIONAL parameter `[List<String> typeParams =
      const []]` maps to a C# OPTIONAL parameter with default `null`
      (cached `rf-dart-named-default-param-to-csharp-optional-arg` from
      `type_table.dart.md` — though that finding covered NAMED defaults,
      the underlying principle is the same: the documented C# rule is
      that the default must be a compile-time constant or `default
      (ValType)`; a `new List<string>()` is NOT a compile-time constant,
      so the default MUST be `null` with a runtime materialisation).
      The body coalesces: `var effectiveParams = typeParams ?? Array
      .Empty<string>();` (NOT `?? new List<string>()` because the
      callee only iterates — `Array.Empty<string>()` is the cached
      no-allocation singleton; matches the empty-list-of-strings
      pattern in the `checkTerm.ListTerm` branch above). The `var
      typeDef = _typeTable.GetType(typeName); if (typeDef is null)
      return true;` permissive-default is preserved verbatim (same
      semantic as `IsValidConstant`'s line 198 — line 228 here is
      structurally parallel). The type-parameter-substitution map is
      built with `StringComparer.Ordinal` (cached). Two `foreach`
      passes: pass-1 direct StructConstructor match (`if (ctor is
      StructConstructor structCtor && structCtor.Functor == term
      .Functor) return true;` — declaration-pattern binding required),
      pass-2 type-reference chase. The `typeParamSubst[ctorName]!`
      Dart force-unwrap → C# flow-narrowed access via `TryGetValue
      (ctorName, out var substitutedType)` (and only enter the
      recursive call inside the truthy branch — eliminates the bang
      entirely, same idiom as the `clause.body!` handling above).
      The recursive call `IsValidStructConstructor(term, substitutedType)`
      passes NO third argument — Dart relies on the optional-positional
      `[const []]` default; C# relies on the optional `typeParams =
      null` default which the body coalesces to `Array.Empty<string>
      ()`. Direct mode-table lookup via `_modeTable.GetDeclarationBy
      TypeName(ctorName)` (the `ModeTable` API surface defined in
      `mode_table.dart.md`'s `dart.collection.nested_for_in_search_returning_first_match_or_null`
      construct — returns `ModeDeclaration?`).
    idiom_id: null
    research_finding_id: rf-dart-optional-positional-default-const-empty-list-to-csharp-nullable-with-coalesce
    nuance: >-
      Three nuances. (1) Optional-positional vs optional-named: Dart
      DISTINGUISHES `[List<String> typeParams = const []]` (positional,
      square-bracket-wrapped) from `{List<String>? typeParams}` (named,
      curly-brace-wrapped); C# has only ONE optional-parameter mechanism
      (callers may pass by name or by position). The translation
      collapses the distinction — C# callers may invoke `IsValidStructConstructor
      (term, typeName, typeParams: someList)` OR `IsValidStructConstructor
      (term, typeName, someList)`, both legal. Dart callers were
      restricted to the positional form (the square brackets disable
      named-call syntax). This is a STRICT WIDENING — no behavioural
      regression at any existing call site. (2) `const []` Dart default
      vs C# default: Dart `const []` is a CANONICAL CONST EMPTY LIST
      (singleton, shared across calls) — C# has no compile-time-constant
      list literal, so the default must be `null` with a runtime fallback
      to `Array.Empty<string>()` (the .NET-equivalent shared singleton).
      Both forms allocate zero per call. Semantically equivalent. (3)
      Recursive call's missing argument: the Dart recursive call
      `isValidStructConstructor(term, substitutedType)` OMITS the third
      `typeParams` argument, relying on `const []`; the C# `IsValidStructConstructor
      (term, substitutedType)` similarly omits it, relying on the
      `typeParams = null` default. The body's `typeParams ?? Array
      .Empty<string>()` coalesce ensures the same empty-list semantics.
      Codegen MUST emit the same omission to preserve the recursive
      structure visually.
  - construct_key: dart.private_helper_predicate.iscapitalized_via_first_char_case_test
    source_form: >-
      bool _isCapitalized(String name) { if (name.isEmpty) return false;
      final first = name[0]; return first == first.toUpperCase() && first
      != first.toLowerCase(); }
    target_decision: >-
      Emit a private C# instance method `private bool _IsCapitalized(string
      name) { if (name.Length == 0) return false; var first = name[0];
      return char.IsUpper(first); }`. The Dart implementation tests
      `first == first.toUpperCase() && first != first.toLowerCase()` —
      a TWO-CONDITION compound that EXCLUDES case-less characters (digits,
      punctuation, symbols, ideographs without case): for a digit `'5'`,
      `'5'.toUpperCase() == '5'` AND `'5'.toLowerCase() == '5'` ⇒ both
      conditions true and false respectively ⇒ method returns `false`.
      The C# `char.IsUpper(first)` is the documented direct equivalent
      (Microsoft Learn `char.IsUpper(char)`: "Indicates whether the
      specified Unicode character is categorized as an uppercase
      letter") — returns `false` for digits/punctuation/case-less
      symbols, returns `true` for uppercase letters in any Unicode
      script, matching Dart's UPPERCASE-LETTER-ONLY test. Do NOT emit
      a literal transliteration `first == char.ToUpperInvariant(first)
      && first != char.ToLowerInvariant(first)` — it would WORK but is
      semantically obscure where `char.IsUpper` directly expresses the
      intent. The leading-underscore Dart name `_isCapitalized` maps
      to C# `_IsCapitalized` (PascalCase, underscore retained to mark
      it as the original private helper; codegen MAY drop the
      underscore to `IsCapitalized` if a consistent naming policy
      across the corpus is enforced — `errors.dart.md` and
      `mode_table.dart.md` both retain the underscore on private
      members, so this spec follows suit).
    idiom_id: null
    research_finding_id: rf-dart-uppercase-letter-test-to-csharp-char-isupper
    nuance: >-
      Unicode-correctness nuance: the Dart `first == first.toUpperCase()
      && first != first.toLowerCase()` idiom is the canonical Dart way
      to test "is this character an uppercase LETTER (not just a
      caseless character)" because Dart's `String.toUpperCase` /
      `toLowerCase` operate on the full string (not a single character)
      and return the original character for caseless input. The C#
      `char.IsUpper` is documented as Unicode-category-aware ("LU"
      = "Letter, Uppercase") — direct semantic match. SUBTLE
      DIVERGENCE: Dart's `toUpperCase`/`toLowerCase` are locale-INVARIANT
      (the dart.dev doc for `String.toUpperCase`: "uses the default
      locale" — but for single-character ASCII identifiers as found
      in this codebase, locale never matters). `char.IsUpper` is
      locale-invariant (Microsoft Learn: based on UnicodeCategory).
      For the practical input domain of this codebase (type names
      starting with ASCII letters), both implementations agree on
      every input. Edge cases that differ — surrogate pairs and
      composed graphemes — are not reachable in any current call site
      (type names are ASCII identifiers per the lexer's identifier
      rules); codegen MAY emit a comment recording this assumption.
  - construct_key: dart.recursive_search_predicate.typecontainsatom_visiting_capitalized_atomctors
    source_form: >-
      bool _typeContainsAtom(TypeDefinition typeDef, String atomName) {
      for (final ctor in typeDef.constructors) { if (ctor is
      AtomConstructor) { if (ctor.name == atomName) { return true; } if
      (_isCapitalized(ctor.name)) { final refType = typeTable.getType(
      ctor.name); if (refType != null && _typeContainsAtom(refType,
      atomName)) { return true; } } } } return false; }
    target_decision: >-
      Emit a private recursive method `private bool _TypeContainsAtom
      (TypeDefinition typeDef, string atomName) { foreach (var ctor in
      typeDef.Constructors) { if (ctor is AtomConstructor atomCtor) {
      if (atomCtor.Name == atomName) return true; if (_IsCapitalized(
      atomCtor.Name)) { var refType = _typeTable.GetType(atomCtor.Name);
      if (refType is not null && _TypeContainsAtom(refType, atomName))
      return true; } } } return false; }`. Direct foreach + declaration-
      pattern + recursive descent — same pattern as `IsValidConstant`'s
      second loop. RECURSION-TERMINATION: the recursion descends via
      `_TypeContainsAtom(refType, atomName)`, with `refType` looked up
      via `_typeTable.GetType(atomCtor.Name)`. The recursion is NOT
      bounded by the code itself — if the type table contained a
      cycle (`A := B; B := A`), this would stack-overflow in both
      Dart and C#. The Dart source has no cycle-detection; the C# port
      preserves the same vulnerability (FR-024 / FR-023: describe the
      conversion, do not improve the algorithm). Codegen MAY annotate
      with a `// no cycle detection; depends on TypeTable being
      acyclic, an invariant of the parser` comment.
    idiom_id: rf-dart-uppercase-letter-test-to-csharp-char-isupper
    research_finding_id: rf-dart-uppercase-letter-test-to-csharp-char-isupper
    nuance: >-
      Reuses the `_IsCapitalized` helper and the AtomConstructor-pattern-
      matching established above; no new idiom. The reuse-via-recorded-
      idiom satisfies SC-007 ("recurring constructs resolved via a
      recorded idiom, not re-derived"). String equality
      `atomCtor.Name == atomName` follows the project's bare-`==` on
      string convention (ordinal-by-project-discipline, established in
      `mode_table.dart.md`'s
      `dart.collection.nested_for_in_search_returning_first_match_or_null`
      construct).
  - construct_key: dart.formatter_method.getvalidconstructors_walks_typedef_emitting_displays
    source_form: >-
      List<String> getValidConstructors(String typeName) { final result =
      <String>[]; final typeDef = typeTable.getType(typeName); if (typeDef
      == null) return result; for (final ctor in typeDef.constructors) {
      if (ctor is AtomConstructor) { result.add(ctor.name); } else if
      (ctor is StructConstructor) { result.add('${ctor.functor}(...)'); }
      else if (ctor is ListConstructor) { result.add(ctor.isNil ? '[]' :
      '[...|...]'); } else if (ctor is TupleConstructor) { result.add(
      '(...)'); } } return result; }
    target_decision: >-
      Emit `public List<string> GetValidConstructors(string typeName) {
      var result = new List<string>(); var typeDef = _typeTable.GetType(
      typeName); if (typeDef is null) return result; foreach (var ctor
      in typeDef.Constructors) { switch (ctor) { case AtomConstructor
      atomCtor: result.Add(atomCtor.Name); break; case StructConstructor
      structCtor: result.Add($"{structCtor.Functor}(...)"); break; case
      ListConstructor listCtor: result.Add(listCtor.IsNil ? "[]" :
      "[...|...]"); break; case TupleConstructor: result.Add("(...)");
      break; } } return result; }`. The Dart if/else-if chain of `is`-
      tests over a `TypeConstructor` hierarchy maps idiomatically to a
      C# `switch` STATEMENT with type-pattern cases (Microsoft Learn
      "switch statement - type pattern": "test that the input expression
      is of a specified type and, if so, assign to a pre-existing
      variable"). The switch is NOT exhaustive over `TypeConstructor`
      — there is no `default:` arm, matching the Dart else-less chain
      (an unknown ctor subtype silently adds nothing). The four case
      arms preserve the Dart source order verbatim. NOTE on the
      `TupleConstructor` case: it has no binding pattern (`case
      TupleConstructor:` not `case TupleConstructor tupleCtor:`)
      because the case body doesn't reference any member — same
      compact form Dart uses (`else if (ctor is TupleConstructor) {
      result.add('(...)'); }`). The string literals `"[]"`, `"[...|...]"`,
      `"(...)"` are observable error-formatting tokens — codegen MUST
      emit them verbatim, no synthesis.
    idiom_id: null
    research_finding_id: rf-dart-is-else-chain-to-csharp-switch-with-type-patterns
    nuance: >-
      Style nuance: Dart `if (x is A) ... else if (x is B) ... else if
      (x is C) ...` is functionally equivalent to a C# `switch (x) {
      case A: ...; case B: ...; case C: ...; }` — but the `switch`
      form is preferred when the dispatch is OVER A TYPE HIERARCHY and
      there are 3+ cases (the convention from Microsoft Learn's
      pattern-matching section; established `errors.dart.md` and
      `well_typed_clause.dart.md` use if/else for 2-case dispatch and
      switch for 3+). With FOUR cases, `switch` is the documented
      idiom. The empty `default:` is intentionally OMITTED: the
      Dart source's chain has no `else` arm, so an unknown
      `TypeConstructor` subtype is silently skipped — codegen MUST
      preserve the silent-skip semantic, NOT add a `default: throw
      new InvalidOperationException(...)`. The empty list literal
      `<String>[]` Dart → C# `new List<string>()` (cached
      `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`
      reused).
conversion_units:
  - "class TypeError (sealed; get-only Message/Line/Column non-nullable + Suggestion nullable; positional+optional-named ctor; override ToString with two-branch shape preserving literal LF \"\\n\" — NO IEquatable<> override because Dart source did not hand-write ==/hashCode)"
  - "class TypeChecker (non-sealed; private readonly TypeTable _typeTable, ModeTable _modeTable; positional ctor with reference-aliasing)"
  - "method CheckModule(Module module) -> List<TypeError> (foreach over module.Procedures; if (modeDecl is null) continue; inner foreach over proc.Clauses calling CheckClause then errors.AddRange(...))"
  - "method CheckClause(Clause clause, ModeDeclaration modeDecl) -> List<TypeError> (indexed-for with min-length guard over head.Args; if (clause.Body is not null) inner indexed-for with min-length guard over goal.Args; flow-narrowed access — no null-forgiving operator)"
  - "method CheckTerm(Term term, string expectedType, IReadOnlyList<string> typeParams) -> List<TypeError> (is-test chain with declaration-pattern bindings — VarTerm/UnderscoreTerm trivial-return; ConstTerm via IsValidConstant + GetValidConstructors-formatted error; ListTerm five-sub-path branch with OfType<ListConstructor>().FirstOrDefault(c => !c.IsNil) + StringComparer.Ordinal typeParamSubst + TryGetValue-or-key-fallback + recursive head/tail descent; StructTerm via IsValidStructConstructor + same formatted-error idiom; final return errors;)"
  - "method IsValidConstant(ConstTerm term, string typeName) -> bool (four-phase: built-in numeric via disjunctive type pattern (double or float or int or long or short or byte or decimal); built-in atom/string; unknown-type permissive-return-true; AtomConstructor direct match then capitalised-name type-reference chase via _IsCapitalized + recursive _TypeContainsAtom)"
  - "method IsValidStructConstructor(StructTerm term, string typeName, IReadOnlyList<string>? typeParams = null) -> bool (optional parameter with null default + Array.Empty<string>() coalesce; permissive-return-true for unknown type; StringComparer.Ordinal typeParamSubst; pass-1 direct StructConstructor.Functor match; pass-2 capitalised-name chase with TryGetValue-recursive-substitute and modeTable.GetDeclarationByTypeName lookup)"
  - "method _IsCapitalized(string name) -> bool (private; uses char.IsUpper for Unicode-category-aware uppercase-letter test)"
  - "method _TypeContainsAtom(TypeDefinition typeDef, string atomName) -> bool (private recursive; foreach + AtomConstructor declaration-pattern + capitalised-name recursive descent via _typeTable.GetType + _IsCapitalized)"
  - "method GetValidConstructors(string typeName) -> List<string> (foreach + switch (ctor) over AtomConstructor/StructConstructor/ListConstructor/TupleConstructor type patterns; no default arm to preserve silent-skip; observable error-formatting tokens verbatim)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-error-class-no-equality-override-to-csharp-sealed-class — TypeError shape

- **Deep analysis.** `TypeError` is a value-bearing error record with three
  non-nullable fields (`message`, `line`, `column`) and one nullable
  (`suggestion`), plus a `toString` override that conditionally appends a
  multi-line suggestion. Crucially, it does NOT hand-write `operator ==`
  or `hashCode` — the Dart source relies on default reference equality.
  This is structurally similar to `errors.dart.md`'s `PmtError` but
  semantically DIFFERENT: `PmtError` hand-writes `==`/`hashCode` (value
  equality intended); `TypeError` does not (reference equality intended).
  The two specs MUST diverge accordingly — preserving the Dart contract,
  not the file-name similarity.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/object-oriented/equality-comparisons`
  — Microsoft Learn "Equality comparisons": "By default, two reference-
  type variables are equal if they refer to the same object." Reference
  equality is the .NET default for `class` (non-record); preserving it
  for `TypeError` requires NO explicit `IEquatable<>` implementation,
  which is exactly the spec's prescription. The `sealed` modifier
  applies for the same polymorphic-equality-safety reason recorded in
  `errors.dart.md`'s `rf-csharp-class-value-equality-iequatable` finding
  (no subclasses ⇒ no `GetType()` symmetry concern) — though here the
  concern is moot because the class has no value-equality contract to
  defend.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/Object/operator_equals.html` — dart.dev
  official: "The default behavior for all Objects is to return true if
  and only if this object and other are the same object." Reference
  equality is the Dart default; `TypeError` inherits it (no override).
- **Conclusion.** A `sealed class TypeError` with no `IEquatable<>`
  override is the faithful C# counterpart. The `Suggestion` nullable
  property uses the C# NRT `string?` annotation; the constructor's
  Dart `{this.suggestion}` named-optional maps to a C# optional
  parameter with default `null` per `mode_table.dart.md`'s cached
  named-default finding. Authoritative both sides; no escalation.

### rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields — TypeChecker shape

- **Deep analysis.** `TypeChecker` holds two injected dependencies
  (`typeTable`, `modeTable`) and zero own mutable state. It is a
  coordinator/service, not a value object — equality is identity, not
  structural. The Dart `final` fields are reference-immutable; the
  C# `readonly` field is the documented exact mirror (Microsoft Learn
  "readonly" keyword: "a field declared with the readonly modifier can
  only be assigned in the variable declaration or constructor").
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly`
  — Microsoft Learn: `readonly` field assignability constraints.
  WebFetch `https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/field`
  — Framework Design Guidelines on field design: "Do not use public
  instance fields. Prefer using properties." → narrows the access to
  `private readonly` since no external introspection is required.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/classes#using-this-in-a-constructor` —
  dart.dev official: initialising formals (`this.x` in constructor
  parameters) assign the parameter directly to the field. Direct
  semantic mapping to a C# constructor body with `_x = x;` (or a
  primary constructor in C# 12+ — both forms acceptable; the spec
  prefers the explicit assignment for diff-stability with the Dart
  source). Authoritative both sides; no escalation.

### rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange — accumulator pattern

- **Deep analysis.** Three accumulator sites use `final errors =
  <TypeError>[];` (typed empty list) followed by `errors.addAll(...)`.
  The Dart `<T>[]` typed-empty-list literal is allocation-per-call;
  `addAll` is the bulk-append. Both `CheckModule`, `CheckClause`, and
  `CheckTerm` follow this exact shape.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1`
  — Microsoft Learn `List<T>`: "Represents a strongly typed list of
  objects that can be accessed by index." WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange`
  — `List<T>.AddRange(IEnumerable<T>)`: "Adds the elements of the
  specified collection to the end of the List<T>." Direct semantic
  mapping; both O(n) where n = size of input.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/List/addAll.html` — dart.dev official
  `List.addAll`: "Appends all objects of iterable to the end of this
  list." Verbatim semantic match. Authoritative both sides.

### rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for — parallel arg iteration

- **Deep analysis.** Two sites at lines 57-62 and 70-75 use the SAME
  shape: `for (int i = 0; i < a.length && i < b.length; i++) { use
  a[i], use b[i]; }`. The min-length guard tolerates a length mismatch
  silently — trailing elements of the longer list are unchecked. This
  is a deliberate "type-check only what's declared" policy with the
  arity check assumed elsewhere.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/iteration-statements#the-for-statement`
  — Microsoft Learn `for` statement: "Executes a statement or a block
  of statements while a specified Boolean expression evaluates to
  true." Direct semantic equivalent of Dart's three-clause `for`.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/loops#for-loops` — dart.dev official:
  "for loops in Dart work the same as they do in JavaScript, C, Java,
  and other curly-brace languages." Direct mapping. Authoritative
  both sides; the imperative form preserves the silent-tolerance
  semantic, where a LINQ rewrite to `Zip` (which truncates to the
  shorter sequence, observationally equivalent here) would obscure
  the explicit guard. The spec prescribes the imperative form for
  diff-stability and clarity.

### rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access — body!/head!/tail! handling

- **Deep analysis.** The Dart source uses the bang operator
  (`clause.body!`, `term.head!`, `term.tail!`) INSIDE explicit
  null-guards (`if (clause.body != null) { for (final goal in clause
  .body!) { ... } }`). The bang is redundant from a SAFETY perspective
  (the guard already proves non-null) but required by Dart's type
  system (the smart cast doesn't reach across closure boundaries
  reliably). C#'s flow-sensitive nullable-reference-type analysis
  IS strong enough to narrow within an `if` body without a null-
  forgiving operator.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references`
  — Microsoft Learn "Nullable reference types": "The compiler uses
  static analysis to determine if a variable is potentially null.
  ... If you know the value isn't null, you can use the null-
  forgiving operator (!) to tell the compiler not to issue the
  warning. However, you should rarely need this operator. The
  compiler can usually determine the null state." WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving`
  — Microsoft Learn "Null-forgiving operator": "It doesn't have any
  effect at run time. ... The operator's only effect is to change
  the null state of the expression to non-null." Decisive: `clause.Body!`
  inside an `if (clause.Body is not null)` block is anti-idiomatic —
  the flow analysis already narrowed.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/null-safety/understanding-null-safety#null-assertion-operator`
  — dart.dev official: "A trailing `!` after an expression is the
  null-assertion operator. It casts away the nullability of the
  expression, throwing if the expression is null." Dart's `!` is a
  RUNTIME CHECK; C#'s `!` is a COMPILE-TIME hint. Conflating them
  would silently disable a runtime safety net — but the Dart source's
  guard pattern already establishes non-null statically, so the
  C# flow-narrowed access preserves BOTH the static guarantee AND
  the runtime safety (the `if (... is not null)` IS the runtime
  check). Authoritative both sides; no escalation.

### rf-dart-is-test-smart-cast-to-csharp-declaration-pattern — checkTerm dispatch

- **Deep analysis.** The five-way dispatch in `checkTerm` is the
  type-checker's central case-analysis: Var/Underscore/Const/List/Struct
  Term branches. The Dart smart-cast under `if (term is ConstTerm)`
  narrows the local `term` to `ConstTerm` for the branch (Dart
  language spec, type promotion). C# does not promote — pattern-
  matching with a binding (`if (term is ConstTerm constTerm)`) is the
  documented equivalent.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#declaration-and-type-patterns`
  — Microsoft Learn "Patterns - Declaration and type patterns": "You
  use a declaration pattern to check the run-time type of an expression
  and, if a match succeeds, assign the result of an expression to a
  declared variable." Decisive: `obj is T t` is the documented form
  to BOTH test and bind in one step.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/operators#type-test-operators` — dart.dev
  official: "Use the `is` operator to test whether an object has a
  given type." Type promotion (smart cast) is implicit; documented in
  the dart.dev type-promotion section.
- **Conclusion.** Five `if (term is X subtypeBinding) { ... }` arms
  with terminal `return errors;`. The Dart else-less fall-through
  (each Dart arm returns; final unguarded `return errors;` at line
  181 is the catch-all) is preserved verbatim — the trailing C#
  `return errors;` covers any unforeseen `Term` subtype. Authoritative
  both sides; no escalation.

### rf-dart-list-join-to-csharp-string-join-separator-first — error-message formatting

- **Deep analysis.** Two sites (ConstTerm error at line 100-107 and
  StructTerm error at line 169-176) use `validConstructors.isNotEmpty
  ? "Valid constructors: ${validConstructors.join(', ')}" : null` to
  build a conditional suggestion string. The empty-vs-nonempty
  branch + `join(', ')` formatting is observable in test goldens
  and error output.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.string.join`
  — Microsoft Learn `String.Join`: "Concatenates the elements of a
  specified array or the members of a collection, using the specified
  separator between each element or member." Multiple overloads;
  the spec uses `string.Join(string separator, IEnumerable<string>
  values)` which is the direct equivalent of Dart `list.join(sep)`.
  Documented behaviour for empty input: "If `values` is empty or
  contains no elements other than null, the method returns
  String.Empty." Direct semantic match with Dart `[].join(', ')`
  returning `""`.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/Iterable/join.html` — dart.dev
  official `Iterable.join`: "Converts each element to a String and
  concatenates the strings. ... If a separator is provided, it is
  inserted between any two elements." Verbatim semantic match.
- **Conclusion.** First use (ConstTerm branch) RECORDS the new idiom;
  second use (StructTerm branch) REUSES it (SC-007). Authoritative
  both sides; no escalation. The separator-position swap (`list.join
  (sep)` Dart vs `string.Join(sep, list)` C#) is the load-bearing
  syntactic nuance — codegen MUST emit the swap correctly.

### rf-dart-wheretype-firstornull-chain-to-csharp-oftype-firstordefault — ListTerm element-type discovery

- **Deep analysis.** The ListTerm branch (lines 113-163) is the most
  complex single block in the file. It performs (a) type resolution,
  (b) constructor-set check, (c) type-parameter substitution map
  build, (d) list-constructor isolation via a TWO-STEP filter
  (`whereType<ListConstructor>()` then `where((c) => !c.isNil)`),
  (e) element-type substitution lookup with key-fallback, (f)
  recursive head/tail descent. Each sub-step requires a distinct
  idiom mapping but they compose into a single logical block.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.oftype`
  — Microsoft Learn `Enumerable.OfType<TResult>`: "Filters the
  elements of an IEnumerable based on a specified type. ... Returns
  An IEnumerable<T> that contains elements from the input sequence
  of type TResult." Decisive: `OfType<T>` SKIPS non-matching elements
  (vs. `Cast<T>` which THROWS). WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.firstordefault`
  — `Enumerable.FirstOrDefault(predicate)`: "Returns the first
  element of the sequence that satisfies a condition or a default
  value if no such element is found. ... If `T` is a reference type,
  the default value is null." Direct semantic match with Dart
  `firstOrNull`.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/Iterable/whereType.html` — dart.dev
  official `Iterable.whereType<T>`: "Returns a new lazy Iterable with
  all elements that have type `T`." Verbatim semantic match for
  `OfType<T>`. WebFetch
  `https://api.dart.dev/dart-core/IterableExtensions/firstOrNull.html`
  — dart.dev official `firstOrNull`: "The first element, or null if
  the iterable is empty." Direct semantic match with `FirstOrDefault`.
- **Conclusion.** `Constructors.OfType<ListConstructor>().FirstOrDefault
  (c => !c.IsNil)` is the canonical .NET form for Dart
  `constructors.whereType<ListConstructor>().where((c) => !c.isNil)
  .firstOrNull`. The two-step Dart chain collapses to a one-step C#
  chain (OfType already filters by type; FirstOrDefault accepts the
  remaining predicate). The TypeParamSubst `Dictionary<string, string>
  (StringComparer.Ordinal)` is mandatory per the cached project-wide
  ordinal-comparer discipline. The `typeParamSubst[k] ?? k` Dart
  read-with-key-fallback maps to `TryGetValue(k, out var v) ? v : k`
  — the same null-on-miss-vs-throw hazard recorded in `type_table.dart.md`,
  generalised: the fallback here is the LOOKUP KEY, not null.
  Authoritative both sides; no escalation.

### rf-dart-numeric-supertype-is-num-to-csharp-disjunctive-type-pattern — built-in numeric type check

- **Deep analysis.** `IsValidConstant` line 188 tests `term.value is
  num`. Dart's `num` is the abstract supertype of `int` and `double`
  — a Dart `num`-check matches both. C# has no `num` supertype: `int`
  (System.Int32), `double` (System.Double), `float` (System.Single),
  `long` (System.Int64), `decimal` (System.Decimal), etc., are
  sibling value types under `System.ValueType`. The documented
  bridge is a disjunctive type pattern.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns`
  — Microsoft Learn "Patterns - Logical patterns": "You can use the
  `or` and `and` pattern combinators to combine relational patterns."
  WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types`
  — Microsoft Learn "Value types": lists the eight numeric primitives
  (sbyte/short/int/long/byte/ushort/uint/ulong and float/double/decimal).
  No common numeric supertype. The disjunctive type pattern `expr is
  double or float or int or long or short or byte or decimal` is the
  documented canonical "is this any number?" test.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/num-class.html` — dart.dev official
  `num`: "An integer or floating-point number. ... It is a compile-
  time error for any type other than the types int and double to
  attempt to extend or implement num." Decisive: in Dart 3+, `num`
  is closed to `int` + `double` only. So a narrower C# disjunction
  `term.Value is int or double` would PRECISELY match Dart `num`.
  The spec prescribes the broader 7-type list to be DEFENSIVE
  against future Dart-source changes that might pass through e.g.
  a `BigInt` (which Dart does NOT extend `num` from, but a
  conservative widening doesn't hurt). Codegen MAY narrow to `is int
  or double` based on the ast.dart line 147 comment "// String, int,
  double, or atom name" which restricts `ConstTerm.value` to
  `String`, `int`, `double` — a strict reading allows the narrower
  pattern. The spec records both options without escalation; either
  is correct under FR-024 (the broader form is more defensive, the
  narrower form is more precise).
- **Conclusion.** Disjunctive type pattern is the .NET-documented
  bridge for Dart `num`. Authoritative both sides; no escalation.

### rf-dart-optional-positional-default-const-empty-list-to-csharp-nullable-with-coalesce — IsValidStructConstructor signature

- **Deep analysis.** Line 226: `bool isValidStructConstructor(StructTerm
  term, String typeName, [List<String> typeParams = const []])`. The
  `[...]` square brackets denote OPTIONAL POSITIONAL parameters (Dart-
  specific syntax distinct from `{...}` named-optional). The default
  `const []` is a canonical const empty list — singleton, shared,
  immutable.
- **Authoritative .NET.** WebFetch (cached, same source as
  `type_table.dart.md`'s named-default finding)
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`
  — Microsoft Learn: optional parameter defaults must be compile-
  time constants or `default(T)` for value types. A `new List<string>
  ()` is NOT a compile-time constant. So the C# default MUST be
  `null` with a runtime coalesce. WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.array.empty`
  — `Array.Empty<T>`: "Returns an empty array. ... If `T` is a
  reference type, the method returns a cached, zero-length array."
  Singleton — semantically equivalent to Dart `const []` (also a
  singleton, shared). Both forms allocate zero per call.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/functions#parameters` — dart.dev
  official: "Functions can have two types of parameters: required
  and optional. ... Optional positional parameters: Wrapping a set
  of function parameters in [] marks them as optional positional
  parameters." Confirms the `[List<String> typeParams = const []]`
  syntax means "third positional argument, optional, defaults to
  const empty list".
- **Conclusion.** C# signature `IsValidStructConstructor(StructTerm
  term, string typeName, IReadOnlyList<string>? typeParams = null)`
  with body `var effective = typeParams ?? Array.Empty<string>();`
  preserves both the optional-third-argument shape AND the empty-
  default semantics. The named-vs-positional surface widening (C#
  allows both; Dart positional-only) is a strict widening — no
  behavioural regression. Authoritative both sides; no escalation.

### rf-dart-uppercase-letter-test-to-csharp-char-isupper — _IsCapitalized

- **Deep analysis.** Line 272-276 implements an Unicode-letter-class
  test: "is the first character an uppercase LETTER (not a digit, not
  a caseless symbol)". The Dart implementation uses the canonical
  two-condition Unicode-safe pattern `first == first.toUpperCase()
  && first != first.toLowerCase()` — TRUE only for characters whose
  case-toggled forms differ. C# has a direct documented equivalent.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.char.isupper`
  — Microsoft Learn `Char.IsUpper(Char)`: "Indicates whether the
  specified Unicode character is categorized as an uppercase letter."
  Decisive: Unicode-category-aware (UnicodeCategory.UppercaseLetter,
  "Lu"). Returns `true` for ASCII A-Z, Greek capital alpha, Cyrillic
  capital A, etc.; returns `false` for digits, punctuation, lowercase
  letters, ideographs without case (CJK), surrogates.
- **Authoritative Dart.** WebFetch
  `https://api.dart.dev/dart-core/String/toUpperCase.html` and
  `https://api.dart.dev/dart-core/String/toLowerCase.html` — dart.dev
  official: returns the input unchanged for case-less characters.
  Confirms the two-condition idiom correctly identifies UPPERCASE
  LETTERS (and not digits or symbols).
- **Conclusion.** `char.IsUpper(name[0])` is the direct semantic
  equivalent — clearer, more idiomatic, same Unicode-category
  guarantee. The empty-string guard `if (name.Length == 0) return
  false;` mirrors `if (name.isEmpty) return false;` verbatim. The
  recursive caller `_TypeContainsAtom` reuses this helper. Authoritative
  both sides; no escalation.

### rf-dart-is-else-chain-to-csharp-switch-with-type-patterns — GetValidConstructors

- **Deep analysis.** `getValidConstructors` is a four-case type-
  dispatch over `TypeConstructor` subtypes. The Dart else-less
  chain silently skips unknown subtypes (no `else` clause; the
  trailing fall-through to `return result;` covers it). With FOUR
  cases, the C# `switch` with type-pattern cases is the documented
  idiom; with 2-3 cases, if/else is acceptable.
- **Authoritative .NET.** WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/selection-statements#the-switch-statement`
  — Microsoft Learn "switch statement": "Selects for execution a
  statement list that has an associated switch section that matches
  the switch expression." WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#declaration-and-type-patterns`
  — Microsoft Learn "Type patterns in switch": demonstrates `case T
  t:` declaration-pattern arms.
- **Authoritative Dart.** WebFetch
  `https://dart.dev/language/branches#switch` — dart.dev "Switch
  statements": Dart 3+ supports pattern-matching switch but the
  source here uses the older if/else-if chain (the file was written
  pre-Dart-3 patterns or didn't migrate). The conversion does NOT
  need to preserve the if/else-if SHAPE — preserving the BEHAVIOUR
  (sequential type dispatch, silent skip on unknown subtype) is
  sufficient.
- **Conclusion.** A C# `switch` with four type-pattern cases is the
  more idiomatic form; the empty `default:` is OMITTED to preserve
  the silent-skip semantic. The four arms emit observable error-
  formatting tokens (`""`, `"[]"`, `"[...|...]"`, `"(...)"`, `"
  (...)"`) verbatim. Authoritative both sides; no escalation.

## Notes

- **Cross-file alignment.** This file imports from
  `package:glp_runtime/compiler/ast.dart` (line 6) which provides the
  AST node hierarchy (`Term`, `VarTerm`, `UnderscoreTerm`, `ConstTerm`,
  `ListTerm`, `StructTerm`), the constructor hierarchy (`TypeConstructor`
  with subtypes `ListConstructor`, `AtomConstructor`, `StructConstructor`,
  `TupleConstructor`), and the type-definition record `TypeDefinition`.
  The `ModeDeclaration` symbol is imported transitively through
  `mode_table.dart`. As of the current `glp_runtime_net/lib/compiler/ast
  .dart` snapshot, several of these symbols (`ListConstructor`,
  `AtomConstructor`, `StructConstructor`, `TupleConstructor`,
  `TypeDefinition`, `ModeDeclaration`) are NOT defined in the file — the
  same situation noted in `type_table.dart.md`'s Notes section
  (`TypeDef` vs `TypeDefinition` discrepancy). The conversion of THIS
  file is fully decidable given the source as written; cross-file
  symbol resolution is owned by the ast.dart / type_ast.dart / mode_table
  .dart specs at codegen-stitch time. No escalation — the discrepancy
  reflects a snapshot drift in the inventory tree, not a Dart→C#
  conversion ambiguity.
- **No async/Stream/Future, no isolates, no late.** The type-checker
  is a purely synchronous tree-walk. The four well-known nuances per
  US2-AS4 (value-vs-reference, async/Stream, null-safety, isolates):
  value-vs-reference is ADDRESSED for `TypeError` (sealed reference,
  no value-equality) and `TypeChecker` (coordinator class); async is
  ABSENT (correctly not asserted); null-safety is ADDRESSED at every
  `?` and `!` site (`suggestion`, `clause.body!`, `term.head!`/`tail!`,
  `Map[k]?`); isolates ABSENT.
- **Newline portability.** The `'\n  Suggestion: $suggestion'`
  hard-coded `\n` in `TypeError.toString` is U+000A LF; the C# port
  emits `"\n"` literally (NOT `Environment.NewLine`) — same load-
  bearing nuance recorded in `type_table.dart.md`'s
  `rf-dart-stringbuffer-to-csharp-stringbuilder` finding.
- **Reference aliasing.** Every accumulator list returned by
  `CheckModule` / `CheckClause` / `CheckTerm` is a FRESH allocation
  (the Dart `<TypeError>[]` literal); the C# `new List<TypeError>()`
  preserves that — codegen MUST NOT hoist to a shared static.
  Conversely, the `_typeTable` / `_modeTable` references are SHARED
  with the caller (constructor aliasing) — preserved verbatim, no
  defensive copy introduced (the tables are mutated by other code paths
  during compilation, and the type-checker MUST observe those
  mutations).
- **String-equality discipline.** Every `string`-keyed dictionary in
  this file uses `StringComparer.Ordinal` explicitly (two sites:
  `typeParamSubst` in `CheckTerm.ListTerm` and `typeParamSubst` in
  `IsValidStructConstructor`). The bare `==` on string equality in
  comparisons (`atomCtor.Name == termValue` etc.) follows the
  project-wide bare-`==`-is-ordinal-by-convention discipline from
  `mode_table.dart.md`.
- **Permissive-default semantics preserved.** Two sites
  (`IsValidConstant` line 198, `IsValidStructConstructor` line 228)
  return `true` when the type-table lookup returns `null` — the
  doc-comments at lines 197-198 and 229 establish this as a deliberate
  "unknown type ⇒ allow" policy. The C# port preserves it verbatim;
  promoting it to a thrown exception would change observable
  behaviour for external-type references.
- **Silent-tolerance semantics preserved.** Three sites tolerate
  malformed input silently: (a) the `getDeclaration` null-return
  skip in `CheckModule`, (b) the min-length guards in the indexed-
  for loops of `CheckClause`, (c) the trailing fall-through `return
  errors;` in `CheckTerm`. The C# port preserves all three verbatim.
- **Recursion termination caveat.** `_TypeContainsAtom` and
  `IsValidStructConstructor` are recursive; both depend on the
  `TypeTable` being acyclic (no `A := B; B := A` cycle). The Dart
  source has no cycle detection; the C# port preserves the same
  reliance on the parser-side invariant. Codegen MAY add a `//
  acyclic-TypeTable invariant` comment at each recursion site.
- **Every non-trivial construct above carries BOTH a deep-analysis
  basis AND an authoritative-research basis** (Dart or .NET official
  docs, never web-only), satisfying SC-006 / FR-009 / FR-010. New
  findings: `rf-dart-error-class-no-equality-override-to-csharp-sealed-class`,
  `rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields`,
  `rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange`,
  `rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for`,
  `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`,
  `rf-dart-is-test-smart-cast-to-csharp-declaration-pattern`,
  `rf-dart-list-join-to-csharp-string-join-separator-first`,
  `rf-dart-wheretype-firstornull-chain-to-csharp-oftype-firstordefault`,
  `rf-dart-numeric-supertype-is-num-to-csharp-disjunctive-type-pattern`,
  `rf-dart-optional-positional-default-const-empty-list-to-csharp-nullable-with-coalesce`,
  `rf-dart-uppercase-letter-test-to-csharp-char-isupper`,
  `rf-dart-is-else-chain-to-csharp-switch-with-type-patterns`. Cached
  findings reused (FR-024 — never re-research):
  `rf-csharp-string-equality-ordinal-by-default`,
  `rf-csharp-interpolated-string-equivalent-to-dart-interpolation`,
  `rf-csharp-dictionary-trygetvalue-then-fallback-null`,
  `rf-dart-length-isempty-to-csharp-count`,
  `rf-dart-named-default-param-to-csharp-optional-arg`,
  `rf-dart-stringbuffer-to-csharp-stringbuilder` (all from
  `mode_table.dart.md` / `type_table.dart.md` / `errors.dart.md`).
- **Zero escalations (SC-008):** every construct is decidable from
  authoritative Dart and .NET official documentation. The cross-file
  symbol-name discrepancy (`ListConstructor` et al. not defined in
  the current `ast.dart` snapshot) is a SNAPSHOT-ALIGNMENT issue, not
  a Dart→C# conversion ambiguity — handled by sibling specs at
  codegen-stitch time.
