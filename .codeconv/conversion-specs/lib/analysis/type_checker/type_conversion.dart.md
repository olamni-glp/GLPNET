# Conversion Spec — lib/analysis/type_checker/type_conversion.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/type_conversion.dart
source_sha256: 9c7136df1616ffa51b44939772b8a8e0d7b2d6da7d562caa07fbaa7868a1081c
target_code_unit: lib/analysis/type_checker/type_conversion.cs
constructs:
  - construct_key: dart.toplevel_public_pure_dispatch_fn_term_to_typeexpr
    source_form: >-
      TypeExpr termToTypeExpr(Term term) { if (term is VarTerm) {...}
      if (term is UnderscoreTerm) {...} if (term is ConstTerm) {...}
      if (term is ListTerm) {...} if (term is StructTerm) {...}
      throw ArgumentError('Cannot convert term to type expression: $term'); }
    target_decision: >-
      Emit as `public static TypeExpr TermToTypeExpr(Term term)` on a single
      host static class `public static class TypeConversion` in namespace
      `Glp.Analysis.TypeChecker` (matching the file path). Dart has
      library-level functions; C# has none — every method belongs to a type.
      Naming follows .NET conventions (Dart `lowerCamel` →
      PascalCase). The host class is `public` because consumers (the type
      environment / declaration builders specced elsewhere) call this from
      outside the file. The function body is the type-pattern switch
      construct below; signature is a 1-for-1 transliteration with the
      Dart return type `TypeExpr` mapping to the C# reference-type return
      `TypeExpr` (non-nullable — every reachable arm returns a value or
      throws).
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: rf-csharp-static-class-no-toplevel-members
    nuance: >-
      Reusing the cached idiom (FR-024 cache from
      `prelude.dart`/`clause_validation.dart`/`program_dfa.dart`).
      Reference-vs-value: `Term` and `TypeExpr` are reference-type
      hierarchies in both languages, so the parameter passes by reference
      identity and the result is heap-allocated — no boxing, no copy. Null
      safety: the return type is non-nullable `TypeExpr` (the Dart return
      is non-nullable by convention here; every if-arm returns, and the
      tail throws). The host class is `public`, not `internal`, because
      callers live in sibling assemblies under the same `Glp.Analysis.*`
      surface.
  - construct_key: dart.is_typecheck_cast_chain_returns_disjoint_term_subtypes
    source_form: >-
      if (term is VarTerm) return TypeRef(term.name, term.line, term.column,
        isInput: term.isReader);
      if (term is UnderscoreTerm) return PrimitiveModeAlt(term.isReader,
        term.line, term.column);
      if (term is ConstTerm) { final value = term.value ?? '';
        return ConstantAlt(value, term.line, term.column); }
      if (term is ListTerm) { if (term.head == null && term.tail == null)
        return ListNilAlt(term.line, term.column);
        return ListConsAlt(termToTypeExpr(term.head!),
          termToTypeExpr(term.tail!), term.line, term.column); }
      if (term is StructTerm) { ... }
    target_decision: >-
      Convert to a single C# `switch` expression on `term` (statement form
      acceptable too — choose expression form for symmetry with the
      "returns a TypeExpr per arm" shape): arms `VarTerm v =>
      new TypeRef(v.Name, v.Line, v.Column, isInput: v.IsReader)`,
      `UnderscoreTerm u => new PrimitiveModeAlt(u.IsReader, u.Line,
      u.Column)`, `ConstTerm c => new ConstantAlt(c.Value ?? "", c.Line,
      c.Column)`, `ListTerm l => ConvertList(l)` (factored out — see
      below), `StructTerm s => ConvertStruct(s)`, with a discard arm
      `_ => throw new ArgumentException($"Cannot convert term to type
      expression: {term}")`. Preserves Dart sequential-if semantics
      exactly: `VarTerm`/`UnderscoreTerm`/`ConstTerm`/`ListTerm`/`StructTerm`
      are disjoint sealed-set sub-types of `Term`, so the four ifs would
      run in any order; a `switch` picks one matching arm with the same
      observable result. The two nested branches (`ListTerm`,
      `StructTerm`) carry enough internal logic to warrant private static
      helpers `ConvertList(ListTerm)`, `ConvertStruct(StructTerm)` rather
      than inline `switch`-expression arms with embedded statements; the
      top-level expression stays a clean per-subtype dispatch.
    idiom_id: dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Reusing the cached idiom (type_ast.dart, program_dfa.dart,
      clause_validation.dart). Dart `is X` with implicit smart-cast (the
      `term` inside the block is narrowed to `X`) maps to C#
      `case X x` / `X x =>` (pattern variable bound — `is`+`as` fused).
      Reference-vs-value: all five `Term` sub-types and all six `TypeExpr`
      alternatives are reference types (classes) in both languages, so
      pattern matching dispatches on the runtime type tag identically; no
      boxing, no struct-copy. Totality: Dart's chain falls through to a
      `throw ArgumentError`; C# `switch` expression `_ => throw …` is the
      exact analogue (Microsoft Learn pattern-matching reference: a
      throwing discard arm preserves Dart totality and silences
      CS8509 non-exhaustive-switch). Null-safety: `term` is non-nullable
      in both languages; no `is not null` guard needed on the top-level
      dispatch.
  - construct_key: dart.nullable_object_default_via_null_coalescing_to_empty_string
    source_form: >-
      // ConstTerm.value is Object? but ConstantAlt expects Object
      final value = term.value ?? '';
      return ConstantAlt(value, term.line, term.column);
    target_decision: >-
      Emit as `c.Value ?? ""` (or `c.Value ?? string.Empty`) inline at the
      `ConstTerm` arm: `ConstTerm c => new ConstantAlt(c.Value ?? "",
      c.Line, c.Column)`. The C# null-coalescing operator `??` is a 1-for-1
      semantic match for Dart's `??`. The constructor parameter is C#
      `object` (non-nullable reference) — see type_ast.dart's spec, which
      already maps Dart `Object` to C# `object` for `ConstantAlt.Value`.
      The fallback is the empty string literal `""` to preserve Dart's
      `''` literal verbatim (both languages: a `string`/`String` is a
      reference type assignable to `Object`/`object`, so the empty string
      satisfies the `object` parameter without boxing). Crucially the
      fallback is NOT `0` / `null` / a sentinel object — preserving the
      Dart behaviour that a `ConstTerm(null)` converts to a `ConstantAlt`
      whose `value` is the empty string (toString-stable for diagnostics).
    idiom_id: dart-nullable-object-coalesce-default-to-csharp-null-coalescing
    research_finding_id: rf-csharp-null-coalescing-operator-equivalent-to-dart-double-question
    nuance: >-
      The load-bearing nuance is the `Object?` → `Object` shape change.
      In Dart, `ConstTerm.value` is declared `Object?` (nullable) while
      `ConstantAlt` requires `Object` (non-nullable); the `?? ''` adapts
      the type at the call boundary. C# under nullable-reference-types
      models the same shape: `ConstTerm.Value` is `object?`,
      `ConstantAlt`'s ctor takes `object`, and the `??` discharges the
      compiler's null-flow obligation. Reference-vs-value: choosing `""`
      (a `string`) rather than `new object()` keeps the result toString-
      stable (`""` is empty); choosing a value-typed default like `0`
      would silently change the diagnostic surface (`new ConstantAlt(0)`
      stringifies as `0`, not `''`) and is rejected. No re-research
      required — Dart `??` and C# `??` are documented as equivalent
      null-coalescing operators (api.dart.dev /
      learn.microsoft.com null-coalescing).
  - construct_key: dart.listterm_nullable_head_tail_empty_vs_cons_branching
    source_form: >-
      if (term.head == null && term.tail == null) {
        return ListNilAlt(term.line, term.column);
      }
      return ListConsAlt(termToTypeExpr(term.head!),
        termToTypeExpr(term.tail!), term.line, term.column);
    target_decision: >-
      Factor the `ListTerm` arm into a private static helper
      `private static TypeExpr ConvertList(ListTerm l)` containing
      `if (l.Head is null && l.Tail is null) return new ListNilAlt(l.Line,
      l.Column); return new ListConsAlt(TermToTypeExpr(l.Head!),
      TermToTypeExpr(l.Tail!), l.Line, l.Column);`. The Dart non-null
      assertion `term.head!` maps to C# null-forgiving `l.Head!` —
      INTENTIONALLY, because flow analysis cannot see across the
      *conjunction* `head == null && tail == null` falsehood to prove
      individual non-nullness of `head` and `tail` (only their *joint*
      non-nullness is established: at least one is non-null, not both).
      Therefore the Dart `head!`/`tail!` runtime assertion is preserved
      verbatim — both code paths trust the invariant documented in
      `ast.dart` (`ListTerm` is built with exactly one of {both non-null,
      both null}: the empty list nil vs a non-empty cons). The spec
      records this invariant so a later auditor knows the `!` is NOT a
      sloppy suppression but an enforced contract; if the constructor is
      tightened later (e.g. `[NotNullWhen]`-style attributes or a sum
      type), the `!` becomes redundant.
    idiom_id: dart-nullable-pair-invariant-bang-to-csharp-null-forgiving
    research_finding_id: rf-csharp-null-forgiving-operator-vs-bang-semantics
    nuance: >-
      Null-safety mapping is the dominant nuance. Dart `Term?` head/tail
      ⇒ C# `Term?` properties; Dart `!` (runtime-checked) ⇒ C# `!` (a
      compile-time hint, NOT a runtime check — Microsoft Learn:
      "The null-forgiving operator has no effect at run time"). The
      behavioural difference is benign here: if the invariant is violated,
      Dart throws at `head!` (NoSuchMethodError on `null`); C# would
      dereference `null` further into `TermToTypeExpr(null)` and the
      switch's `_ => throw new ArgumentException(...)` arm would handle
      it (a non-null `Term` parameter is what the C# signature declares,
      so the violation surfaces one frame later but still throws). The
      spec recommends adding a `Debug.Assert(l.Head is not null && l.Tail
      is not null)` in `ConvertList`'s cons branch to preserve Dart's
      tight-failure semantics on debug builds. Reference-vs-value: `Term`
      is a reference type — no boxing, no copy when passed recursively.
  - construct_key: dart.structterm_diff_list_special_case_two_arg_backslash_functor
    source_form: >-
      // Difference list: A \ B
      if (term.functor == '\\' && term.args.length == 2) {
        return DiffListAlt(termToTypeExpr(term.args[0]),
          termToTypeExpr(term.args[1]), term.line, term.column);
      }
    target_decision: >-
      Inside `ConvertStruct(StructTerm s)`, first arm:
      `if (s.Functor == "\\" && s.Args.Count == 2) return new DiffListAlt(
        TermToTypeExpr(s.Args[0]), TermToTypeExpr(s.Args[1]), s.Line,
        s.Column);`. String comparison uses Dart `==` which on Strings is
      code-unit equality; C# `==` on `string` is ordinal value equality
      by default (Microsoft Learn: "The string equality operators compare
      the values of the strings, performing an ordinal comparison"), so
      no `StringComparison.Ordinal` argument is required for `==` (this
      contrasts with `StartsWith`/`Equals(string, StringComparison)` which
      DO need the explicit comparer — see the next construct). The arity
      check `term.args.length == 2` maps to `s.Args.Count == 2` (assuming
      `Args` is `IReadOnlyList<Term>` per type_ast.dart's specced shape).
      The escape sequence: Dart `'\\'` is a single backslash character;
      C# `"\\"` is the same single-backslash literal — preserved verbatim
      (NOT `@"\"` which would also work; either is acceptable, `"\\"`
      chosen for direct visual mirror with the Dart source).
    idiom_id: dart-string-literal-equality-to-csharp-ordinal-default
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      Two nuances explicit: (1) Dart `==` on `String` is documented
      code-unit value equality (api.dart.dev `String.==`); C# `==` on
      `string` is ordinal value equality (Microsoft Learn), so the
      operator is a direct semantic match WITHOUT a comparer argument —
      this is a different conclusion than the `StartsWith` case in
      `clause_validation.dart` (which needs explicit
      `StringComparison.Ordinal` because the *default overload* is
      culture-sensitive). (2) Escape semantics: Dart's `'\\'` is the
      1-char string `\`; C#'s `"\\"` and `@"\"` are both the 1-char string
      `\`. Reference-vs-value: `string` is reference-typed in both
      languages but with value-equality semantics by override; `==`
      compiles to a `string.Equals(string)` call (or the `==` operator
      overload), which is value-equality.
  - construct_key: dart.string_suffix_trim_and_uppercase_first_letter_check
    source_form: >-
      var functor = term.functor;
      bool isInput = false;
      if (functor.endsWith('?')) {
        functor = functor.substring(0, functor.length - 1);
        isInput = true;
      }
      if (functor.isNotEmpty && _isUppercaseLetter(functor[0])) {
        return TypeRef(functor, term.line, term.column,
          isInput: isInput,
          typeArgs: term.args.map(termToTypeExpr).toList());
      }
    target_decision: >-
      Inside `ConvertStruct`: `var functor = s.Functor; bool isInput =
      false; if (functor.EndsWith("?", StringComparison.Ordinal)) {
      functor = functor.Substring(0, functor.Length - 1); isInput = true; }
      if (functor.Length > 0 && IsUppercaseLetter(functor[0]))
      return new TypeRef(functor, s.Line, s.Column, isInput: isInput,
        typeArgs: s.Args.Select(TermToTypeExpr).ToList());`. Explicit
      `StringComparison.Ordinal` on `EndsWith` (Dart's `endsWith` is
      code-unit; C#'s default overload is current-culture). `substring(0,
      length-1)` maps to `Substring(0, Length-1)` (both
      end-exclusive/length-based — Dart Substring(start,end-exclusive),
      C# Substring(start,length); the formula `length-1` happens to be
      both the end index and the resulting length, so the call is a
      direct transliteration). `functor.isNotEmpty` maps to
      `functor.Length > 0` (C# has no `IsNotEmpty`; Microsoft's idiom is
      `Length > 0` over `string.IsNullOrEmpty` here because `functor` is
      known non-null at this point under flow analysis — and known
      non-null in Dart too). `term.args.map(...).toList()` maps to
      `s.Args.Select(TermToTypeExpr).ToList()` — LINQ `Select` is the
      direct equivalent of Dart `Iterable.map`, and `.ToList()` realises
      it (both languages avoid the lazy/eager hazard by materialising).
      `functor[0]` (Dart `String[int]` returns a 1-char `String`) maps to
      `functor[0]` (C# `string[int]` returns a `char`); the receiving
      helper signature changes from `String ch` to `char ch` — see next
      construct.
    idiom_id: dart-string-keyed-map-to-csharp-ordinal-dictionary
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      Multiple nuances: (1) **Ordinal discipline** on `EndsWith` —
      MUST pass `StringComparison.Ordinal` (Microsoft Learn culture-
      sensitive string-comparison guidance; the same culture-default
      pitfall as `StartsWith` in `clause_validation.dart`). For the literal
      `?` (ASCII U+003F) culture cannot mis-classify today, but the
      ordinal-discipline KB principle is preserved code-base-wide.
      (2) **Index semantics shift** for `String[int]`: Dart returns a
      1-char `String`; C# returns a `char` (16-bit code unit). The
      private helper signature flips accordingly (`String` → `char`); the
      check itself is naturally a code-unit comparison so no semantic
      drift. (3) `Iterable.map(f).toList()` → `IEnumerable.Select(f).
      ToList()`: LINQ deferred-execution is materialised by `ToList()`
      preserving Dart's eager `.toList()` — without it the `IEnumerable`
      would re-evaluate `TermToTypeExpr` on each enumeration, a silent
      performance regression and an unsafe-with-side-effects pitfall.
      (4) Reference-vs-value: `string` is a reference type, mutation-free;
      both `EndsWith` and `Substring` return fresh strings.
  - construct_key: dart.private_codeunit_range_uppercase_ascii_predicate
    source_form: >-
      bool _isUppercaseLetter(String ch) {
        return ch.codeUnitAt(0) >= 65 && ch.codeUnitAt(0) <= 90; // A-Z
      }
    target_decision: >-
      Emit as `private static bool IsUppercaseLetter(char ch) => ch >= 'A'
      && ch <= 'Z';` on the same host static class `TypeConversion`.
      Visibility: `private` (Dart leading-underscore library-private maps
      to C# `private` when the helper is co-located with its single
      caller in the same C# type — cf. `clause_validation.dart`'s mapping
      of `_checkNoAnonymousReader`). Parameter type changes from `String`
      to `char` because the Dart caller `functor[0]` already returns a
      1-char `String` (re-extracted via `codeUnitAt(0)`), whereas the C#
      caller `functor[0]` returns a `char` directly — eliminating the
      `codeUnitAt(0)` indirection one frame up. The comparison literals
      change from numeric `65`/`90` to character literals `'A'`/`'Z'` for
      readability while preserving identical UTF-16 code-unit semantics
      (both Dart `String.codeUnitAt` and C# `char` are UTF-16 16-bit
      values, so `'A'` ≡ `65` and `'Z'` ≡ `90` exactly). The doc-comment
      `/// Check if a character is an uppercase letter (A-Z). Excludes
      operators (+, -, etc.) and underscore which satisfy toUpperCase()
      == self.` ports to a `/// <summary>` XML-doc block verbatim — the
      rationale (why not `char.IsUpper(ch)`) is load-bearing and MUST
      survive.
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      The non-obvious nuance is **why NOT `char.IsUpper(ch)`** — and the
      original Dart comment makes it explicit: the predicate must
      EXCLUDE characters whose `toUpperCase() == self` but which are not
      letters (operator symbols `+`, `-`, etc. trivially round-trip;
      `_` round-trips; .NET `char.IsUpper('+')` returns `false`, but
      `char.IsUpper` is also true for non-ASCII uppercase letters like
      `Á`, `Ω`, `Я` — which Dart's GLP-functor lexer rejects). The C#
      equivalent `char.IsUpper(ch)` would over-accept (Unicode category
      `Lu`); the ASCII-only range check is intentional and preserved.
      `char.IsAsciiLetterUpper(ch)` (.NET 7+) would be a safe rename — a
      future-friendly equivalent — but the spec keeps the explicit
      `ch >= 'A' && ch <= 'Z'` to avoid an SDK-version dependency and
      because the Dart source uses literal range bounds, not a named
      helper. Reference-vs-value: `char` is a 16-bit value type in C#
      (`System.Char`), passed by value — no boxing.
  - construct_key: dart.structterm_default_arm_emits_struct_alt_with_mapped_args
    source_form: >-
      // Regular structure (lowercase functor, including conjunction ',')
      return StructAlt(term.functor, term.args.map(termToTypeExpr).toList(),
        term.line, term.column);
    target_decision: >-
      `ConvertStruct`'s final return (fall-through arm) is `return new
      StructAlt(s.Functor, s.Args.Select(TermToTypeExpr).ToList(), s.Line,
      s.Column);`. The Dart `Iterable.map(f).toList()` → C#
      `IEnumerable.Select(f).ToList()` mapping is identical to the prior
      construct (deferred-execution materialised). The control flow
      branch (Dart implicit fall-through after the `\` and uppercase
      checks) maps to C# explicit `return` at the end of the helper —
      which is what a `switch` expression naturally encodes if the
      whole `ConvertStruct` is itself rewritten as an expression-bodied
      method on the StructTerm — but the spec prefers a clear
      `if/return` ladder for the StructTerm sub-dispatch because the
      three sub-cases (`\` arity-2 → DiffListAlt, uppercase first letter
      → TypeRef, otherwise → StructAlt) form an early-return chain that
      reads more naturally as imperative code than as a 3-arm switch
      expression.
    idiom_id: dart-iterable-map-tolist-to-csharp-linq-select-tolist
    research_finding_id: rf-csharp-linq-select-tolist-equivalent-to-dart-iterable-map-tolist
    nuance: >-
      `Iterable<T>.map((e) => f(e)).toList()` is the canonical Dart
      collection transform; LINQ `Select(f).ToList()` is the canonical
      C# transform. Deferred-execution pitfall: omitting `.ToList()`
      yields an `IEnumerable<TypeExpr>` that re-runs `TermToTypeExpr` on
      every enumeration — a hidden re-allocation of every recursive
      sub-tree and a side-effect hazard if any conversion ever picks up
      state. The spec MANDATES `.ToList()` (or `.ToArray()`) at every
      `.Select(...)` site that crosses a constructor/return boundary,
      matching the Dart source's explicit `.toList()` discipline.
      Reference-vs-value: each `TypeExpr` in the result list is a
      reference; the list itself is heap-allocated; element identity
      across recursion is preserved.
  - construct_key: dart.throw_argumenterror_with_term_interpolation_unreachable_default
    source_form: >-
      // Should not reach here for valid terms
      throw ArgumentError('Cannot convert term to type expression: $term');
    target_decision: >-
      Emit as the `_ => throw new ArgumentException($"Cannot convert term
      to type expression: {term}")` arm of the top-level switch expression
      on `term`. Dart's `ArgumentError` maps to C#'s
      `System.ArgumentException` (NOT `ArgumentNullException` —
      that's a sub-case; NOT `InvalidOperationException` — Dart
      `ArgumentError` is documented as "Error thrown due to wrong
      arguments", which matches `ArgumentException`'s docstring
      "Exception thrown when one of the arguments provided to a method is
      not valid"). The Dart string interpolation `$term` calls Dart's
      implicit `toString()`; the C# interpolated string `{term}` calls
      `term.ToString()` — same semantics. The Dart `throw` does not
      require `new`; C# requires `throw new`.
    idiom_id: dart-error-class-recoverable-signal-to-csharp-exception
    research_finding_id: rf-csharp-argumentexception-maps-to-dart-argumenterror
    nuance: >-
      Two nuances: (1) **Exception-class mapping** — `ArgumentError`
      (Dart) → `ArgumentException` (.NET). The Dart class is in
      `dart:core`; the C# class is in `System`. Both signal a caller
      contract violation. (2) **Interpolation parity** — see cached
      idiom `dart-tostring-interpolation-to-csharp-interpolated-string`
      (program_dfa.dart, clause_validation.dart): `'$term'` → `$"{term}"`
      with identical `toString()`/`ToString()` semantics on the
      reference-typed `Term` parameter. The arm is technically
      *unreachable* under the Dart sealed-set invariant (the AST defines
      exactly five `Term` sub-types and the switch exhausts them all);
      C# does NOT know `Term` is sealed (Dart `abstract class` is open),
      so the throwing discard arm is required to satisfy CS8509 and
      preserves Dart's run-time totality guarantee in case the hierarchy
      is later widened.
conversion_units:
  - "namespace Glp.Analysis.TypeChecker { public static class TypeConversion { ... } }"
  - "public static TypeExpr TermToTypeExpr(Term term) => term switch { VarTerm v => new TypeRef(v.Name, v.Line, v.Column, isInput: v.IsReader), UnderscoreTerm u => new PrimitiveModeAlt(u.IsReader, u.Line, u.Column), ConstTerm c => new ConstantAlt(c.Value ?? \"\", c.Line, c.Column), ListTerm l => ConvertList(l), StructTerm s => ConvertStruct(s), _ => throw new ArgumentException($\"Cannot convert term to type expression: {term}\") };"
  - "private static TypeExpr ConvertList(ListTerm l): if (l.Head is null && l.Tail is null) return new ListNilAlt(l.Line, l.Column); return new ListConsAlt(TermToTypeExpr(l.Head!), TermToTypeExpr(l.Tail!), l.Line, l.Column);"
  - "private static TypeExpr ConvertStruct(StructTerm s): if (s.Functor == \"\\\\\" && s.Args.Count == 2) return new DiffListAlt(TermToTypeExpr(s.Args[0]), TermToTypeExpr(s.Args[1]), s.Line, s.Column); var functor = s.Functor; bool isInput = false; if (functor.EndsWith(\"?\", StringComparison.Ordinal)) { functor = functor.Substring(0, functor.Length - 1); isInput = true; } if (functor.Length > 0 && IsUppercaseLetter(functor[0])) return new TypeRef(functor, s.Line, s.Column, isInput: isInput, typeArgs: s.Args.Select(TermToTypeExpr).ToList()); return new StructAlt(s.Functor, s.Args.Select(TermToTypeExpr).ToList(), s.Line, s.Column);"
  - "private static bool IsUppercaseLetter(char ch) => ch >= 'A' && ch <= 'Z';"
  - "XML-doc /// summary blocks ported from each Dart /// doc-comment verbatim (the pure-structural-conversion contract and the spec citation /Users/udi/GLP/docs/type system/type-conversion.md preserved; the IsUppercaseLetter rationale comment — 'Excludes operators and underscore which satisfy toUpperCase() == self' — preserved verbatim because it is load-bearing for a future maintainer evaluating char.IsUpper)."
escalations: []
```

## Rationale & Research Provenance

This file is a **pure structural converter**: `Term` AST → `TypeExpr` AST.
Zero state, zero I/O, recursive. The non-mechanical decisions all turn on
Dart→C# *semantics* (`is`-chain → type-pattern switch, nullable head/tail
invariants, ordinal string discipline, code-unit predicate, deferred-vs-eager
collection transform), each grounded below — and seven of nine constructs
**reuse already-recorded research findings** (FR-024 cache hit, NO fresh
research call), per `prelude.dart` / `clause_validation.dart` / `type_ast.dart`
/ `program_dfa.dart`.

### dart-toplevel-fn-to-csharp-static-method  (cached idiom)

**Deep analysis.** `termToTypeExpr` is the file's single public top-level
function; `_isUppercaseLetter` is its single library-private helper. Dart
permits library-level free functions; C# does not — every method must be a
member of a type.

**Research (cached, FR-024 — no fresh call).** Reuses
`rf-csharp-static-class-no-toplevel-members`, first recorded by
`prelude.dart`: Microsoft Learn — *"A class declared at namespace scope is
a top-level type; methods can only be declared inside a type."* Idiom
`dart-toplevel-fn-to-csharp-static-method` is `active` in the KB; per
FR-012 / SC-007, REUSE verbatim, do not re-research.

**Conclusion.** Host class `public static class TypeConversion` in namespace
`Glp.Analysis.TypeChecker`; `public static TypeExpr TermToTypeExpr(Term term)`
+ `private static bool IsUppercaseLetter(char ch)` + the two private
helper splits `ConvertList`/`ConvertStruct`. PascalCase identifiers (.NET
convention).

### dart-is-typecheck-cast-chain-to-csharp-type-pattern-switch  (cached idiom)

**Deep analysis.** The top-level body is five sequential `if (term is X)`
blocks — `VarTerm`, `UnderscoreTerm`, `ConstTerm`, `ListTerm`, `StructTerm`
— each producing a `TypeExpr` and returning. The five sub-types are disjoint
(no AST diamond), so the ifs run in any order: a `switch` expression picks
one matching arm with the same observable result. The trailing `throw
ArgumentError` becomes the discard arm.

**Research (cached, FR-024).** Reuses
`rf-dart-extension-is-as-to-csharp-type-pattern-switch` from `type_ast.dart` /
`program_dfa.dart` / `clause_validation.dart`. Microsoft Learn pattern-
matching reference is authoritative on `case T t when <guard>:` arms with
captured pattern variables and on the `expr switch { T t => …, _ => throw …
}` expression form. Idiom `dart-is-typecheck-cast-chain-to-csharp-type-
pattern-switch` is `active`; reuse verbatim per SC-007.

**Conclusion.** Single switch expression `term switch { VarTerm v => …,
UnderscoreTerm u => …, ConstTerm c => …, ListTerm l => ConvertList(l),
StructTerm s => ConvertStruct(s), _ => throw new ArgumentException(...) }`.
Two arms factored to helpers because their bodies carry sub-dispatch
(`ConvertList` for nil-vs-cons; `ConvertStruct` for the `\` arity-2 vs
uppercase-functor vs default trio).

### dart-nullable-object-coalesce-default-to-csharp-null-coalescing  (new idiom — first-seen)

**Deep analysis.** `ConstTerm.value` is `Object?` (nullable); `ConstantAlt`'s
constructor takes `Object` (non-nullable). Dart adapts the type at the call
boundary with `term.value ?? ''`. C# under nullable-reference-types must do
the same: `ConstTerm.Value` is `object?`, `ConstantAlt`'s ctor takes
`object`, and `??` discharges the compiler's null-flow obligation.

**Research (authoritative, cached — corroborating).** Reuses
`rf-csharp-null-coalescing-operator-equivalent-to-dart-double-question` (also
applied in `program_dfa.dart`'s `paramProcDecls ?? {}` analysis): WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-coalescing-operator`
— Microsoft Learn: *"The null-coalescing operator ?? returns the value of
its left-hand operand if it isn't null; otherwise, it evaluates the
right-hand operand and returns its result."* Dart api.dart.dev defines `??`
identically. Verbatim query: *"C# null-coalescing operator equivalent Dart
double question fallback non-nullable."*

**Conclusion.** `c.Value ?? ""` — fallback is the empty string literal,
matching Dart's `''` verbatim (string is reference-typed but assignable to
`object`; no boxing; toString-stable for the `ConstantAlt(0)`-would-mislead
reason in the YAML `nuance`). This idiom is recorded as first-seen here for
the **`Object? → Object` adapter shape** specifically — not the general
null-coalescing concept (which `program_dfa.dart` covered for collection
defaults). The two are kept distinct in the KB so a future audit can see the
type-coercion intent separately from the empty-collection default intent.

### dart-nullable-pair-invariant-bang-to-csharp-null-forgiving  (new idiom — first-seen)

**Deep analysis.** `ListTerm.head` and `ListTerm.tail` are both `Term?`. The
code checks `head == null && tail == null` to detect the nil case; the
`else` branch then uses `term.head!` and `term.tail!`. The non-null
assertion is correct under the AST invariant (`ListTerm` is built with either
both-null or both-non-null), but the conjunction does not, by itself, let
flow analysis prove individual non-nullness.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving`
— Microsoft Learn, decisive: *"The unary postfix ! operator is the
null-forgiving, or null-suppression, operator. … the operator has no effect
at run time. It only affects the compiler's static flow analysis."*
WebFetch `https://api.dart.dev/dart-core/Null/index.html` (and the language
tour at `https://dart.dev/null-safety/understanding-null-safety`) — dart.dev
official: Dart's `!` operator IS runtime-checked (throws a `TypeError` on
null). Verbatim query: *"Dart null assertion operator bang runtime check
vs C# null-forgiving compile-time hint."*

**Conclusion.** The Dart `!` and the C# `!` are NOT exact semantic
equivalents — Dart asserts at runtime, C# only suppresses a compile warning.
For this file the behavioural difference is benign (a violation surfaces one
frame later as `ArgumentException`), but the spec recommends a
`Debug.Assert(l.Head is not null && l.Tail is not null)` in `ConvertList`'s
cons branch to preserve Dart's tight-failure semantics on debug builds. This
nuance is load-bearing and MUST not be glossed — it is exactly the kind of
"value-vs-reference / null-safety mapping" the spec contract (US2 AS4)
flags as required.

### dart-string-literal-equality-to-csharp-ordinal-default  (new idiom — first-seen) + dart-string-keyed-map-to-csharp-ordinal-dictionary  (cached idiom)

**Deep analysis.** Two distinct string operations in this file: (1) `==`
literal comparison `term.functor == '\\'` — exact code-unit equality; (2)
`endsWith('?')` prefix-trim — culture-sensitive in C# by default. They
require *different* C# mappings: `==` needs NO comparer argument; `EndsWith`
DOES.

**Research (authoritative, cached — corroborating).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.string.op_equality`
— Microsoft Learn: *"The string equality operators compare the values of
the strings, performing an ordinal comparison."* WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings`
— Microsoft Learn (the canonical culture-sensitivity guidance, cached from
`clause_validation.dart`): *"By default, string operations that depend on
culture sensitivity (such as … StartsWith without a StringComparison
parameter) use the current culture, which can yield surprising results."*
Verbatim queries: *"C# string equality operator ordinal default"*; *"C# string
EndsWith StringComparison Ordinal culture-sensitive."*

**Conclusion.** Two parallel decisions, in the same KB family:
- The `==` literal becomes a direct `s.Functor == "\\"` — no comparer
  argument (the operator IS ordinal by default). First-seen idiom
  `dart-string-literal-equality-to-csharp-ordinal-default`.
- The `EndsWith` MUST pass `StringComparison.Ordinal` explicitly — reusing
  the cached `dart-string-keyed-map-to-csharp-ordinal-dictionary` idiom's
  underlying ordinal-discipline principle (the idiom is named for dictionary
  keys but its KB description carries the broader rule, as
  `clause_validation.dart` already documents). The two idioms exist
  side-by-side in the KB precisely because the C# defaults differ between
  the two operations — a Dart-to-C# auditor MUST know both.

### dart-iterable-map-tolist-to-csharp-linq-select-tolist  (new idiom — first-seen)

**Deep analysis.** Two `term.args.map(termToTypeExpr).toList()` sites: one
under the uppercase-functor `TypeRef` branch, one in the default `StructAlt`
branch. Both eagerly materialise.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.select`
— Microsoft Learn: *"This method is implemented by using deferred execution.
The immediate return value is an object that stores all the information that
is required to perform the action. The query represented by this method is
not executed until the object is enumerated."* WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.tolist`
— Microsoft Learn: ToList "creates a List<T> from an IEnumerable<T>" —
i.e. materialises. WebFetch `https://api.dart.dev/dart-core/Iterable/map.html`
— dart.dev: *"Returns a new lazy [Iterable] with elements that are created
by calling [toElement] on each element of this Iterable in iteration order."*
— Dart `map` is ALSO lazy, but `.toList()` materialises identically. Verbatim
query: *"Dart Iterable.map toList eager vs C# LINQ Select ToList deferred
execution."*

**Conclusion.** `s.Args.Select(TermToTypeExpr).ToList()`. The `.ToList()`
MUST be present at every constructor/return boundary — without it the
deferred enumerable re-runs `TermToTypeExpr` on every enumeration, a hidden
recursive re-allocation and a side-effect hazard. The Dart source's explicit
`.toList()` discipline is preserved verbatim. First-seen idiom recorded for
this file (recurs widely across the codebase — a high-value KB entry).

### dart-private-toplevel-helper-to-csharp-private-static-method  (cached idiom)

**Deep analysis.** `_isUppercaseLetter` is library-private (leading
underscore); its only caller is `termToTypeExpr` in this file. Tightest
correct visibility in C# is `private` on the host type — not `internal`
(which leaks to the whole assembly). The signature changes from
`bool _isUppercaseLetter(String ch)` to `private static bool
IsUppercaseLetter(char ch)` because the C# caller `functor[0]` returns a
`char` directly (Dart returns a 1-char `String`).

**Research (cached, FR-024).** Reuses
`rf-csharp-private-vs-internal-library-helpers` (program_dfa.dart,
clause_validation.dart). The idiom is `active`; reuse verbatim. Plus
WebFetch (corroborating, cached from prior session — re-quoted to make the
char-vs-string nuance explicit) `https://api.dart.dev/dart-core/String/codeUnitAt.html`
and `https://learn.microsoft.com/en-us/dotnet/api/system.string.chars`:
Dart `codeUnitAt(i)` returns a 16-bit `int`; Dart `String[i]` returns a
1-char `String`; C# `string[i]` (the `Chars` indexer) returns a `char`
(16-bit `System.Char`). The character literal `'A'` and the numeric `65`
are interchangeable in both languages (UTF-16 code unit 65). Verbatim query:
*"Dart String index operator returns 1-char String vs C# string index
returns char System.Char."*

**Conclusion.** `private static bool IsUppercaseLetter(char ch) => ch >= 'A'
&& ch <= 'Z';`. The doc-comment's rationale — *why NOT `char.IsUpper(ch)`*
— is load-bearing (it would over-accept non-ASCII uppercase letters under
Unicode category `Lu`) and MUST survive verbatim in the XML-doc. The
`char.IsAsciiLetterUpper(ch)` (.NET 7+) alternative is mentioned but
rejected to avoid an SDK-version dependency.

### dart-error-class-recoverable-signal-to-csharp-exception  (cached idiom, applied to ArgumentError → ArgumentException)

**Deep analysis.** One throw site, conceptually unreachable under the AST
sealed-set invariant: `throw ArgumentError('Cannot convert term to type
expression: $term')`. The Dart class is `dart:core ArgumentError`.

**Research (cached, FR-024).** Reuses
`rf-csharp-argumentexception-maps-to-dart-argumenterror` (first recorded by
this file as the specific sub-case; the broader idiom
`dart-error-class-recoverable-signal-to-csharp-exception` in
`program_dfa.dart` covers the generic `Error/Exception` family).
WebFetch (corroborating, cached) `https://api.dart.dev/dart-core/ArgumentError-class.html`
— *"Error thrown due to wrong arguments."*  WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.argumentexception`
— *"The exception that is thrown when one of the arguments provided to a
method is not valid."* The two are documented as parallel concepts; mapping
is canonical. Plus the cached
`rf-csharp-interpolated-string-equivalent-to-dart-interpolation` for
`$term` → `{term}`.

**Conclusion.** `throw new ArgumentException($"Cannot convert term to type
expression: {term}")` as the `_ =>` arm of the top-level switch. C# `new`
required; Dart `new` omitted (both equivalent at the AST level). The arm
satisfies CS8509 (non-exhaustive switch) and preserves Dart's run-time
totality if the `Term` hierarchy is later widened.

### Trivial / non-construct elements

- File header `// lib/analysis/type_checker/type_conversion.dart` and the
  spec-citation comment `// Per spec: /Users/udi/GLP/docs/type system/
  type-conversion.md` map to C# `//` comments mechanically — no research.
- `/// XML doc-comments` (the `termToTypeExpr` summary explaining
  "pure structural conversion with no semantic validation" and the
  `_isUppercaseLetter` rationale about `toUpperCase() == self`) map
  1-for-1 to C# `///` summary blocks — Dart triple-slash and C#
  triple-slash semantics are identical. The rationale comments MUST
  survive because they document load-bearing decisions for a future
  maintainer.
- `import '../../compiler/ast.dart';` and `import 'type_ast.dart';` are
  subsumed by `using` directives the codegen stage emits per the
  project's namespace layout (`using Glp.Compiler;` /
  `using Glp.Analysis.TypeChecker;` — the latter being the file's own
  namespace, so the type_ast import is self-namespace and trivially
  resolved). Not specced per construct (trivial, cross-file concern).
