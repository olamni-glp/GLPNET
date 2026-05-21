# Conversion Spec — lib/compiler/analyzer.dart

> Conversion-spec artifact for lib/compiler/analyzer.dart (FR-011).
> Spec-only (FR-023): describes the Dart→C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> File is the GLP semantic analyzer — 1454 lines, two top-level classes
> per major concern: `VariableInfo` (per-variable bookkeeping),
> `VariableTable` (per-clause SRSW table + grounded/type-grounded sets),
> three "annotated AST" wrappers (`AnnotatedProgram` /
> `AnnotatedProcedure` / `AnnotatedClause`), the coordinator class
> `Analyzer` (the public 4-step pipeline: SRSW-validate → partial-eval →
> reduce-gen → register-assign), AND a second compile-time partial-
> evaluator class — now called **`DefinedGuardEvaluator`** — that owns
> compile-time GLP three-valued unification for *defined guard*
> unfolding. `DefinedGuardEvaluator` is the STRICT variant (throws
> `CompileError` on suspend/fail because defined guards must be fully
> reducible at compile time); the LENIENT variant lives in
> `partial_evaluator.dart` and is still called `PartialEvaluator`
> (returns failure via the `UnifyResult` ADT for reduce/2 unfolding).
> The `UnifyResult` sealed ADT was lifted out of both files into a
> SHARED sibling file `lib/compiler/unify_result.dart`; both this file
> and `partial_evaluator.dart` now import it. This refactor resolved
> two previously-recorded escalations (duplicate `UnifyResult` ADT and
> duplicate `PartialEvaluator` class name). Heavy reuse from ast.dart,
> error.dart, parser.dart, and partial_evaluator.dart prior specs:
> ~70 % of constructs map onto already-cached research findings +
> already-recorded idiomatic decisions. New idioms here are small: a
> Dart-only "guard-name dispatch table" (a string-keyed cascade over
> `guard.predicate`) and the SRSW counter aggregation pattern.

```yaml
schema_version: 1
source_path: lib/compiler/analyzer.dart
source_sha256: 531b9f57edc68a07f95f78381c3c38b6953c8506cc799a21dfec8bc73dca32d7
target_code_unit: lib/compiler/analyzer.cs
constructs:
  - construct_key: dart.module.relative_imports_four_sibling_plus_one_cross_package
    source_form: >-
      "import 'ast.dart'; import 'error.dart';
      import 'partial_evaluator.dart' show getPreludeUnitClauses;
      import 'unify_result.dart';
      import '../analysis/type_checker/type_ast.dart';" — four same-folder
      whole-library / selective imports of sibling compiler files plus one
      cross-package whole-library import of the type-checker's type_ast.
      `ast.dart`, `error.dart`, `unify_result.dart` are full whole-library
      imports; `partial_evaluator.dart` is `show`-filtered to a single
      function `getPreludeUnitClauses` (the lenient `PartialEvaluator`
      class in that file is deliberately NOT imported here — this file
      has its own `DefinedGuardEvaluator`).
    target_decision: >-
      The four sibling imports (`ast.dart`, `error.dart`,
      `partial_evaluator.dart`, `unify_result.dart`) collapse to ZERO
      `using` directives because the C# port places all lib/compiler/*
      files into a SINGLE namespace (e.g. `Glp.Runtime.Compiler`) —
      Microsoft Learn: "All types in the same namespace are accessible
      without a `using` directive". The `show getPreludeUnitClauses`
      filter is functionally vacuous in C# because the surrounding-
      namespace rule already restricts what is visible; if
      `getPreludeUnitClauses` is hoisted to a static method on a
      `PartialEvaluatorPrelude` static class (per
      csharp-static-class-no-toplevel-members), the call site reads
      `PartialEvaluatorPrelude.GetPreludeUnitClauses()` — no narrowing
      using-directive needed. The cross-package import
      `'../analysis/type_checker/type_ast.dart'` becomes
      `using Glp.Runtime.Analysis.TypeChecker;` (whole-namespace),
      matching the unfiltered Dart import. Reuse cached
      rf-dart-relative-import-to-csharp-using-or-same-namespace verbatim
      (no new research).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-using-or-same-namespace
    nuance: >-
      Reuse-only — the surface shape is identical to the import sections
      already specced in error.dart / partial_evaluator.dart / parser.dart.
      Two nuances specific to this file: (1) the new `unify_result.dart`
      import resulted from lifting the shared `UnifyResult` ADT out of
      both analyzer.dart and partial_evaluator.dart into a single shared
      sibling file; under the C# same-namespace rule the `using` for it
      collapses just like the other siblings. (2) the `show
      getPreludeUnitClauses` filter is the ONLY visibility-narrowing
      import; partial_evaluator.dart exposes other top-level names
      (notably the lenient `PartialEvaluator` class) that this file
      deliberately does not consume. Under the C# same-namespace rule
      those names are visible regardless — a faithfulness gap that is
      observationally invisible (the consuming code does not reference
      them) but worth recording: if a future analyzer change accidentally
      referred to a name that was previously hidden by `show`, the C#
      port would silently compile while Dart would have flagged it. The
      risk is low and monitored by code-review, not the conversion.

  - construct_key: dart.data_class.variable_info_mutable_counters_with_late_register
    source_form: >-
      "class VariableInfo { final String name; final bool isWriter;
      int writerOccurrences = 0; int readerOccurrences = 0;
      int writerOccurrencesHeadBody = 0; int readerOccurrencesHeadBody = 0;
      AstNode? firstOccurrence; int? registerIndex;
      String? pairedWriter; bool isTemporary = false; bool isPermanent = false;
      VariableInfo(this.name, this.isWriter);
      bool get isAnonymous => name.startsWith('_');
      bool get isSRSWValid { ... derived predicate over the counters ... }
      @override String toString() => 'VariableInfo($name, ...)'; }" — a
      classic mutable bookkeeping struct with two `final` (set-once at ctor)
      identity fields plus eight mutable counter/flag fields, two nullable
      back-references, and two derived boolean getters (`isAnonymous`,
      `isSRSWValid`).
    target_decision: >-
      Emit a `public sealed class VariableInfo` (NOT a record — equality
      MUST stay reference-identity because consumers store these in
      identity-keyed dictionaries via `_vars[name]`, and side tables
      pile up on them during analysis). Two `final` ctor-set fields
      become `public string Name { get; }` and `public bool IsWriter { get; }`
      with a constructor `public VariableInfo(string name, bool isWriter)
      { Name = name; IsWriter = isWriter; }`. Each mutable `int field = 0;`
      becomes `public int WriterOccurrences { get; set; }` (auto-property
      with default-initialised `0` — Microsoft Learn: "Auto-implemented
      properties have an implicit backing field"). The two nullable
      reference-typed back-refs (`AstNode? firstOccurrence`,
      `String? pairedWriter`) become NRT-nullable `public AstNode?
      FirstOccurrence { get; set; }` / `public string? PairedWriter
      { get; set; }`. The nullable `int? registerIndex` becomes
      `public int? RegisterIndex { get; set; }` (nullable-value-type =
      `Nullable<int>`; cf. rf-dart-nullable-enum-to-csharp-nullable-of-enum
      family — same kind, applied to `int`). Derived getters become
      expression-bodied read-only properties: `public bool IsAnonymous
      => Name.StartsWith('_');` and the multi-line `IsSRSWValid` becomes
      a multi-statement get-only property body. The Dart
      `final bool isWriter` initial value is set ONLY in the ctor; mark
      the C# property with `{ get; init; }` ONLY if init-only is preferred
      — but for parity with the explicit ctor assignment the spec uses
      `{ get; }` + ctor-set.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Three intertwined nuances. (1) Reference vs value: this class is a
      mutable bookkeeping object that ACCUMULATES counts across multiple
      `recordWriterOccurrence` / `recordReaderOccurrence` calls — value
      semantics (a struct/record) would silently break the accumulator (a
      copy would be incremented). C# `sealed class` (reference type) is
      mandatory; explicitly REJECT `record struct` and `record class` —
      the latter would also (silently) add structural equality, breaking
      side-table identity. (2) `String.startsWith('_')` (Dart) → C#
      `Name.StartsWith('_')` (char overload, ordinal by default — Microsoft
      Learn `String.StartsWith(Char)`: "uses ordinal sort rules") — NOT
      `StartsWith("_")` (which is culture-sensitive); the char overload
      avoids the StringComparison.Ordinal boilerplate. (3) The `int?
      registerIndex` field carries TWO meanings: `null` ⇒ not yet
      assigned, any value ⇒ assigned. C# `int?` (`Nullable<int>`) is
      the faithful mapping — explicitly REJECT a sentinel `-1` (would
      conflate with a real register 0; Microsoft Learn explicitly warns
      "Use Nullable<T> for value types that may not have a value"). The
      `firstOccurrence ??= node;` Dart compound assignment becomes C#
      `FirstOccurrence ??= node;` (Microsoft Learn null-coalescing
      assignment operator).

  - construct_key: dart.method.srsw_validity_derived_predicate_with_short_circuit_returns
    source_form: >-
      "bool get isSRSWValid {
        if (isAnonymous) return true;
        if (writerOccurrences != 1) return false;
        if (readerOccurrences == 0) return false;
        return true;
      }" — a multi-statement derived getter chained on early-return
      booleans; the comment explicitly warns this is a *partial* test
      that does NOT check ground-guard relaxation.
    target_decision: >-
      `public bool IsSRSWValid { get { if (IsAnonymous) return true;
      if (WriterOccurrences != 1) return false; if (ReaderOccurrences == 0)
      return false; return true; } }`. Direct 1:1 mapping — early-return
      style is idiomatic in both languages. NO conversion to a single
      boolean expression (`return IsAnonymous || (WriterOccurrences == 1
      && ReaderOccurrences > 0);` — equivalent but the spec preserves the
      ordered short-circuit form because (a) the source's intent is
      documented step-by-step via inline comments and (b) the reordering
      would obscure which clause "wins" during a future debugger trace.
      Microsoft Learn confirms: "C# evaluates conditional expressions
      left-to-right" — same semantics. The XML-doc comments translate
      directly from the Dart `///` doc-comments.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Semantic-preservation nuance: this getter is documented to be the
      *fast partial* check (anonymous + writer-count + reader-count) and
      DOES NOT consult `_groundedVars` / `_typeGroundedVars` on the
      enclosing `VariableTable`. The complete check is `verifySRSW` /
      `collectSRSWViolations`. The C# port MUST preserve the partial-vs-
      complete split (do NOT "improve" `IsSRSWValid` to consult the
      enclosing table — it has no access to the table, intentionally).
      This separation is load-bearing for the test suite: a number of
      unit tests exercise `VariableInfo.IsSRSWValid` in isolation against
      a `VariableInfo` not attached to a table.

  - construct_key: dart.class.variable_table_with_dict_and_two_sets_and_recording_methods
    source_form: >-
      "class VariableTable {
        final Map<String, VariableInfo> _vars = {};
        bool _hasGroundGuard = false;
        final Set<String> _groundedVars = {};
        final Set<String> _typeGroundedVars = {};
        void recordWriterOccurrence(String name, AstNode node, {bool inHeadOrBody = true}) { ... };
        void recordReaderOccurrence(String name, AstNode node, {bool inHeadOrBody = true}) { ... };
        void markGrounded(String varName) { _groundedVars.add(varName); }
        bool isGrounded(String varName) => _groundedVars.contains(varName);
        void markTypeGrounded(String varName) { _typeGroundedVars.add(varName); }
        bool isTypeGrounded(String varName) => _typeGroundedVars.contains(varName);
        bool allowsMultipleOccurrences(String varName) => isGrounded(varName) || isTypeGrounded(varName);
        List<String> collectSRSWViolations() { ... };
        void verifySRSW() { ... };
        List<VariableInfo> getAllVars() => _vars.values.toList();
        VariableInfo? getVar(String name) => _vars[name];
        @override String toString() => 'VariableTable(${_vars.length} vars)'; }" — a per-clause
      bookkeeping aggregate: dictionary of named `VariableInfo` + two string-sets
      (`_groundedVars`, `_typeGroundedVars`) tracking grounded-by-guard /
      grounded-by-type-declaration, plus a `_hasGroundGuard` flag that the
      current source initialises but NEVER reads (dead state).
    target_decision: >-
      `public sealed class VariableTable` with three private backing
      collections: `private readonly Dictionary<string, VariableInfo>
      _vars = new(StringComparer.Ordinal);` and two `private readonly
      HashSet<string> _groundedVars = new(StringComparer.Ordinal);` /
      `_typeGroundedVars = new(StringComparer.Ordinal);`. Microsoft Learn:
      `HashSet<T>` is the .NET equivalent of Dart `Set<T>`. The two
      `record*Occurrence` methods take the same signature shape
      `public void RecordWriterOccurrence(string name, AstNode node, bool
      inHeadOrBody = true)` and `RecordReaderOccurrence(...)`. The
      `_vars.putIfAbsent(name, () => VariableInfo(name, true))` Dart call
      is the cached idiom rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add:
      `if (!_vars.TryGetValue(name, out var info)) { info = new
      VariableInfo(name, true); _vars[name] = info; }`. The Dart `??=`-on-
      field (`info.firstOccurrence ??= node;`) becomes C# null-coalescing
      assignment on a property. Read accessors (`markGrounded`,
      `isGrounded`, etc.) become 1:1 `void Mark...` / `bool Is...` methods
      delegating to `HashSet.Add` / `HashSet.Contains`. The
      `allowsMultipleOccurrences` expression-body getter becomes an
      expression-bodied method:
      `public bool AllowsMultipleOccurrences(string varName) =>
      IsGrounded(varName) || IsTypeGrounded(varName);`. The unused
      `_hasGroundGuard` field is RETAINED in the port (with a TODO comment
      noting "appears unread; preserved for parity — remove only via the
      preserve-working-code discipline §0"). `getAllVars()` returns
      `IReadOnlyList<VariableInfo>` (cf. rf-dart-list-to-csharp-list-of-)
      via `_vars.Values.ToList()` — Microsoft Learn:
      `Dictionary<TKey,TValue>.Values` is a live view; the explicit
      `.ToList()` MUST be preserved to match Dart's snapshot semantics
      (mutation during enumeration would otherwise throw
      `InvalidOperationException`).
    idiom_id: null
    research_finding_id: rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add
    nuance: >-
      Five intertwined nuances. (1) StringComparer.Ordinal on BOTH the
      dictionary AND each HashSet is load-bearing: variable names like
      `X` and `x` are distinct in GLP (lower-case first letter ⇒ atom,
      upper-case ⇒ variable); the parser preserves source casing exactly
      and the analyzer compares by identity, NOT cultural folding —
      Microsoft Learn `StringComparer.Ordinal`: "performs a simple byte
      comparison". (2) The anonymous-variable short-circuit on the FIRST
      line of `recordWriterOccurrence` / `recordReaderOccurrence` (`if
      (name.startsWith('_')) return;`) is preserved verbatim in C#
      (`if (name.StartsWith('_')) return;`); this means underscore-named
      variables are NEVER inserted into `_vars` — a semantic invariant
      the SRSW check depends on. (3) Reader/writer asymmetry is
      load-bearing: a reader call still INSERTS the writer slot if absent
      and bumps the writer's reader-count — so the dictionary key is
      always the BARE writer name, never `X?`. The C# port MUST preserve
      this — explicitly: `RecordReaderOccurrence` does NOT key under
      `name + "?"`. (4) The `inHeadOrBody = true` default parameter is
      semantically critical for SRSW (guard occurrences pass `false` to
      bump only the *total* counter, not the *head-body* counter — the
      SRSW pairing only counts head/body readers). The C# default-
      parameter mechanism preserves this. (5) `Set<String>` ⇒
      `HashSet<string>` (NOT `SortedSet`/`ImmutableHashSet` —
      add-then-contains-only access; order does not matter and
      immutability is not used).

  - construct_key: dart.method.collect_srsw_violations_iterating_dictionary_emitting_formatted_strings
    source_form: >-
      "List<String> collectSRSWViolations() {
        final violations = <String>[];
        for (final info in _vars.values) {
          if (info.isAnonymous) continue;
          if (info.writerOccurrences > 1 && !allowsMultipleOccurrences(info.name)) {
            final line = info.firstOccurrence?.line ?? 0;
            violations.add('Line $line: Writer variable "${info.name}" occurs ...');
          }
          if (info.readerOccurrencesHeadBody > 1 && !allowsMultipleOccurrences(info.name)) {
            final line = info.firstOccurrence?.line ?? 0;
            violations.add('Line $line: Reader variable "${info.name}?" occurs ...');
          }
          if (info.writerOccurrences == 0) { ... };
          if (info.readerOccurrences == 0 && info.writerOccurrences > 0) { ... };
        }
        return violations;
      }" — a classic accumulator-loop over the dictionary's values, with
      four guarded-emit branches into a single result list. Uses Dart's
      null-aware operator `?.` chained with `??` for the `line` field.
    target_decision: >-
      `public IReadOnlyList<string> CollectSRSWViolations() { var
      violations = new List<string>(); foreach (var info in _vars.Values) {
      if (info.IsAnonymous) continue; ... } return violations; }`.
      Microsoft Learn `Dictionary<TKey,TValue>.Values` is a live view
      that supports `foreach` directly — no `.ToList()` needed here (no
      mutation of `_vars` inside the loop, AND `Dictionary.Values` is a
      `ValueCollection` whose enumerator is the same shape as Dart's
      `Iterable<V>` enumeration). The Dart `?.` + `??` chain
      `info.firstOccurrence?.line ?? 0` becomes C#
      `info.FirstOccurrence?.Line ?? 0` — direct 1:1 (Microsoft Learn
      null-conditional + null-coalescing combine identically). The
      string-interpolation messages `'Line $line: ...'` become C# `$"Line
      {line}: ..."`. Reuse cached rf-dart-tostring-interp-to-csharp-
      tostring-interp for the interpolation and
      rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
      for the embedded `${info.name}` form. Return type tightened from
      `List<String>` to `IReadOnlyList<string>` (the result is appended to
      and returned; callers iterate and never mutate — cf.
      rf-dart-list-to-csharp-list-of-).
    idiom_id: null
    research_finding_id: rf-dart-list-to-csharp-list-of-
    nuance: >-
      Three intertwined nuances. (1) The four branches are NOT mutually
      exclusive — a single `VariableInfo` can emit two violations (e.g.
      both an over-count AND a missing-reader-pairing — though in
      practice the over-count implies a writer exists). The C# port
      MUST preserve the four sequential `if` branches (NOT chained
      `else if`) — each is an independent check. (2) The Dart message
      strings are *user-visible* error strings consumed by the unified
      REPL test suite, which inspects them for substring matches (e.g.
      "occurs 2 times without ground guard"); the C# port MUST preserve
      the EXACT string format (including the bare `"` quoting around the
      variable name and the `?` suffix for reader variants) — this is
      stricter than a typical .NET-PascalCase nudge would tolerate.
      Microsoft Learn culture-invariant interpolation does NOT apply (no
      numbers are formatted that could differ by locale; only counts and
      names). (3) The `info.firstOccurrence?.line ?? 0` fallback to 0
      is *informational* (line 0 means "unknown" in this codebase); the
      `??` MUST default to literal `0`, NOT `-1` — the error-reporter
      side treats 0 as a sentinel.

  - construct_key: dart.method.verify_srsw_throws_on_first_violation_with_searched_node_for_location
    source_form: >-
      "void verifySRSW() {
        final violations = collectSRSWViolations();
        if (violations.isNotEmpty) {
          final firstVar = _vars.values.firstWhere(
            (v) { if (v.writerOccurrences > 1) return true; ... return false; },
            orElse: () => _vars.values.first,
          );
          throw CompileError(
            'SRSW violation: ${violations.first.replaceFirst(RegExp(r'^Line \d+: '), '')}',
            firstVar.firstOccurrence?.line ?? 0,
            firstVar.firstOccurrence?.column ?? 0,
            phase: 'analyzer'
          );
        }
      }" — legacy/backwards-compat wrapper that picks the first violation
      and re-throws as a `CompileError` with file-position from a SEARCHED
      `VariableInfo` (NOT necessarily the same one whose violation message
      appears first — they are usually the same but the search is a
      separate `firstWhere` pass). Uses a regex strip to remove the
      "Line N: " prefix from the message before re-throwing.
    target_decision: >-
      `public void VerifySRSW() { var violations = CollectSRSWViolations();
      if (violations.Count == 0) return; var firstVar = _vars.Values
      .FirstOrDefault(v => { if (v.WriterOccurrences > 1) return true; ...
      return false; }) ?? _vars.Values.First(); throw new CompileError(
      "SRSW violation: " + RegExSrswLinePrefix.Replace(violations[0], ""),
      firstVar.FirstOccurrence?.Line ?? 0, firstVar.FirstOccurrence?.Column
      ?? 0, phase: "analyzer"); }`. Two LINQ pieces: `FirstOrDefault(...)
      ?? First()` matches `firstWhere(orElse: () => first)` (Microsoft Learn
      `Enumerable.FirstOrDefault`: "returns the first element ... or a
      default value if no such element exists" — for ref types, `null`;
      the `??` then falls back to `First()` which Microsoft Learn says
      "throws InvalidOperationException if the sequence contains no
      elements" — acceptable because `verifySRSW` is only called after
      `collectSRSWViolations().isNotEmpty`, which means `_vars.Values` is
      non-empty). The Dart `RegExp(r'^Line \d+: ')` becomes a class-scoped
      `private static readonly Regex RegExSrswLinePrefix = new(@"^Line
      \d+: ", RegexOptions.Compiled);` — Microsoft Learn `Regex`:
      `RegexOptions.Compiled` is appropriate for module-static patterns
      reused across many calls. The named-argument `phase: 'analyzer'`
      becomes named in C# as well: `phase: "analyzer"` — preserved verbatim.
      Reuse rf-dart-implements-exception-to-csharp-derive-system-exception
      from error.dart for the `CompileError` throw.
    idiom_id: null
    research_finding_id: rf-dart-implements-exception-to-csharp-derive-system-exception
    nuance: >-
      Three intertwined nuances. (1) The `firstWhere`+`orElse` Dart idiom
      maps EXACTLY to `FirstOrDefault` followed by `??` (the only
      observable difference is that `FirstOrDefault` returns `null` for
      reference types, while `firstWhere(orElse:...)` evaluates the
      `orElse` callback — same observable result, different sequencing of
      the fallback computation; immaterial here because the fallback is a
      pure `_vars.values.first` which never throws given the
      already-guarded non-empty precondition). (2) The Dart pre-`var`
      `RegExp` compiles on every call (no caching shown); the C# port
      explicitly LIFTS the regex to a `static readonly Regex` with
      `RegexOptions.Compiled` — a performance improvement that is
      semantics-preserving, justified because `verifySRSW` may be called
      many times during a compile and the pattern is a literal. Microsoft
      Learn explicitly recommends compiled regex for "patterns that are
      executed numerous times". (3) The `phase: 'analyzer'` named
      argument on the `CompileError` throw — error.dart spec mandates this
      stays a named argument in C# call sites (the `CompileError` ctor
      exposes `phase` as a named-optional). The Dart `replaceFirst`
      becomes C# `Regex.Replace(input, "")` with `count` defaulting to
      replace-all, but because the pattern is anchored to `^` only the
      first (and only) match occurs anyway — semantically identical.

  - construct_key: dart.ast_leaf.annotated_program_wrapper_holding_original_plus_per_node_annotations
    source_form: >-
      "class AnnotatedProgram { final Program ast; final List<AnnotatedProcedure>
      procedures; AnnotatedProgram(this.ast, this.procedures); }
      class AnnotatedProcedure { final Procedure ast; final String name;
      final int arity; final List<AnnotatedClause> clauses;
      int? entryPC; String? entryLabel;
      AnnotatedProcedure(this.ast, this.name, this.arity, this.clauses);
      String get signature => '$name/$arity'; @override String toString() => ...; }
      class AnnotatedClause { final Clause ast; final VariableTable varTable;
      bool hasGuards; bool hasBody;
      AnnotatedClause(this.ast, this.varTable, {this.hasGuards = false,
      this.hasBody = false}); @override String toString() => ...; }" — three
      shallow wrapper classes that pair an original AST node with its
      analysis annotations. `AnnotatedProcedure` ALSO carries two MUTABLE
      codegen-only fields (`entryPC`, `entryLabel`) that the bytecode-
      generation stage fills in later.
    target_decision: >-
      Three `public sealed class` types (NOT records — `AnnotatedProcedure`
      has mutable `EntryPC`/`EntryLabel` and reference-identity matters
      because compiler passes attach to instances). Each `final`
      field becomes a `public ... { get; }` get-only auto-property set in
      the ctor. The mutable nullable codegen fields become
      `public int? EntryPC { get; set; }` and `public string? EntryLabel
      { get; set; }` (NRT-nullable; default-null is implicit for `int?` and
      explicit for `string?`). The expression-bodied getter `String get
      signature => '$name/$arity';` becomes `public string Signature =>
      $"{Name}/{Arity}";`. `AnnotatedClause`'s `{this.hasGuards = false,
      this.hasBody = false}` named-default parameters become C# positional
      defaults `bool hasGuards = false, bool hasBody = false` — reuse
      rf-dart-named-default-param-to-csharp-optional-arg. The boolean
      `hasGuards`/`hasBody` fields are MUTABLE in Dart but in practice set
      ONLY in the ctor and read thereafter — the C# port emits them as
      `{ get; }` get-only auto-properties (a faithful tightening; if a
      future pass needs to MUTATE them, switch to `{ get; init; }` and
      add a setter at that point — preserve-working-code §0 allows
      tightening that is observationally identical, which this is).
      Lists exposed as `IReadOnlyList<...>` per the ast.dart family.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Three intertwined nuances. (1) These are pure data wrappers; the
      `ast` back-reference holds the parser-produced AST identity (the C#
      port shares it — NOT a defensive copy — to preserve identity-keyed
      side-tables in downstream passes). (2) `AnnotatedProcedure.entryPC`
      / `entryLabel` are the ONLY mutable cross-stage fields in this
      file (codegen back-fills them); the spec records them as
      `{ get; set; }` (not `init`) to preserve the back-fill capability.
      (3) The Dart `bool hasGuards = false` on the named-default parameter
      is semantically the same as a positional default; both are passed
      by name at every observed call site in the analyzer body, so the
      C# port's positional-with-default form is callable with the same
      call shape `new AnnotatedClause(clause, table, hasGuards: true,
      hasBody: false)`.

  - construct_key: dart.toplevel.const_string_set_isconstanttype_test
    source_form: >-
      "const _constantTypes = {'Integer', 'Real', 'Number', 'String', 'Constant'};
      bool _isConstantType(String? typeName) {
        return typeName != null && _constantTypes.contains(typeName);
      }" — a top-level `const` set of five type-name strings plus a
      top-level test function that null-guards then queries the set.
    target_decision: >-
      Wrap in the file-private `Analyzer` static-class hosting block (per
      csharp-static-class-no-toplevel-members idiom — cf. error.dart
      `_categoryFromPhase`): `private static readonly FrozenSet<string>
      ConstantTypes = new[] { "Integer", "Real", "Number", "String",
      "Constant" }.ToFrozenSet(StringComparer.Ordinal);` and
      `private static bool IsConstantType(string? typeName) => typeName
      is not null && ConstantTypes.Contains(typeName);`. Reuse cached
      rf-dart-const-set-to-csharp-frozenset-ordinal (parser.dart family)
      — Microsoft Learn `FrozenSet<T>`: "Optimized for fast read-only
      access ... not designed for mutation". The Dart `String?` parameter
      preserved as C# `string?` under NRT. Expression body `=> typeName
      is not null && ConstantTypes.Contains(typeName);` — Microsoft Learn
      `is not null` pattern preserves the null-check pretty exactly.
    idiom_id: null
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Three intertwined nuances. (1) Dart `const` set is canonicalised at
      compile time (single shared instance); C# `FrozenSet<T>` initialised
      at static-ctor time is similar (single shared instance; immutable
      after build) — Microsoft Learn explicit: "FrozenSet<T> is optimized
      for fast lookup at the cost of slower construction". The five-string
      set is a literal; the build cost is paid once. (2) The chosen
      StringComparer.Ordinal matches Dart's default string equality (byte-
      identity; no culture folding) — critical because the consumer
      `procDecl.getTypeName(i)` returns type-name tokens with case
      preserved. (3) The `_isConstantType` function is the SOLE consumer
      of the set; the C# port keeps both inside the `Analyzer` private
      surface (NOT exposed publicly). The function-style call site
      `_isConstantType(typeName)` (Dart) becomes
      `IsConstantType(typeName)` (C#) — same call shape.

  - construct_key: dart.class.analyzer_coordinator_with_defined_guard_evaluator_field_and_proc_decl_map_and_mode
    source_form: >-
      "class Analyzer {
        final DefinedGuardEvaluator _definedGuardEvaluator = DefinedGuardEvaluator();
        Map<String, ProcDecl> _procDecls = {};
        CompileMode _compileMode = CompileMode.user;
        Analyzer();
        AnnotatedProgram analyze(Program program, {bool generateReduce = false,
          List<ProcDecl>? procDeclarations, CompileMode compileMode = CompileMode.user,
          bool skipGlobalSRSW = false}) { ... 4-step pipeline ... }
        ...many private helpers... }" — the analyzer entry point. Owns ONE
      `DefinedGuardEvaluator` instance constructed eagerly (a `final` field;
      renamed from `_partialEvaluator` / `PartialEvaluator()` after the
      duplicate-class-name refactor — the analyzer-internal STRICT variant
      is now `DefinedGuardEvaluator` to disambiguate from the LENIENT
      `PartialEvaluator` in partial_evaluator.dart), and TWO mutable state
      fields (`_procDecls`, `_compileMode`) that are reset every call to
      `analyze`. The public method takes four named-optional parameters
      and returns the annotated AST.
    target_decision: >-
      `public sealed class Analyzer` with three private fields:
      `private readonly DefinedGuardEvaluator _definedGuardEvaluator = new();`
      (the `readonly` is preserved from Dart `final`; Microsoft Learn
      `readonly` field: "can only be assigned during declaration or in
      the constructor"), `private Dictionary<string, ProcDecl> _procDecls
      = new(StringComparer.Ordinal);`, and `private CompileMode _compileMode
      = CompileMode.User;`. The empty Dart ctor `Analyzer();` becomes the
      implicit default constructor in C# (no explicit body needed) — but
      the spec emits an explicit `public Analyzer() { }` for parity (cf.
      ast.dart no-explicit-ctor → explicit-ctor decision). The public
      method becomes `public AnnotatedProgram Analyze(Program program,
      bool generateReduce = false, IReadOnlyList<ProcDecl>?
      procDeclarations = null, CompileMode compileMode = CompileMode.User,
      bool skipGlobalSRSW = false)`. Reuse rf-dart-named-default-param-
      to-csharp-optional-arg for the four optional parameters; reuse
      rf-dart-list-to-csharp-list-of- for `IReadOnlyList<ProcDecl>?`. The
      mutation `_compileMode = compileMode;` at method top is preserved
      verbatim; the proc-decls rebuild loop becomes a `foreach (var decl
      in procDeclarations) { _procDecls[decl.Key] = decl; }`. The
      `procDeclarations` null-guard (`if (procDeclarations != null)`)
      stays as a top-level if; C# NRT correctly narrows inside.
    idiom_id: null
    research_finding_id: rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields
    nuance: >-
      Four intertwined nuances. (1) The `Analyzer` instance is REUSABLE —
      `analyze()` resets `_procDecls` and `_compileMode` at the top of
      every call; the C# port MUST preserve the reset (do NOT make the
      fields `readonly` — only `_definedGuardEvaluator` is genuinely
      ctor-fixed). (2) `DefinedGuardEvaluator()` is constructed eagerly at
      field-init time in Dart; the equivalent C# default-ctor invocation
      `new()` at field-init runs BEFORE any user ctor body (Microsoft
      Learn "Field initialization occurs before constructor execution") —
      same semantics. (3) The `_procDecls = {};` reset at the top of
      `Analyze` ALLOCATES a fresh empty dictionary — do NOT replace with
      `.Clear()` (which would mutate the same instance; observationally
      identical here since no external reference is retained, but the
      Dart source allocates fresh and the spec preserves that). (4) The
      `_compileMode = compileMode;` write must occur BEFORE step 1 (SRSW
      validation) — `_compileMode` is read deep in `_analyzeTerm`
      validating reserved constants; the ordering is load-bearing and
      the C# port MUST preserve method-statement order. NOTE: the field
      name and dependency type both renamed from `_partialEvaluator` /
      `PartialEvaluator` after the duplicate-class-name refactor that
      resolved two prior escalations — the strict evaluator here is
      semantically distinct (throws on suspend/fail) from the lenient
      `PartialEvaluator` in partial_evaluator.dart (returns failure via
      `UnifyResult`); the rename is a Dart-source rename that the C# port
      mirrors literally.

  - construct_key: dart.method.analyze_four_step_pipeline_with_skip_flag_and_throwing_aggregator
    source_form: >-
      "AnnotatedProgram analyze(... arguments ...) {
        _compileMode = compileMode;
        _procDecls = {};
        if (procDeclarations != null) { for (final decl in procDeclarations) {
          _procDecls[decl.key] = decl; } }
        // STEP 1: SRSW validation on original program (skip if linked)
        if (!skipGlobalSRSW) {
          final allViolations = <String>[];
          for (final proc in program.procedures) {
            final violations = _collectSRSWViolationsForProcedure(proc);
            allViolations.addAll(violations);
          }
          if (allViolations.isNotEmpty) {
            final message = 'SRSW violations found:\n${allViolations.map((v) => '  • $v').join('\n')}';
            throw CompileError(message, 0, 0, phase: 'analyzer');
          }
        }
        // STEP 2: partial-eval defined guards
        final transformed = _definedGuardEvaluator.transformDefinedGuards(program);
        // STEP 3: optionally generate reduce/2 clauses
        final withReduce = generateReduce ? _generateReduceClauses(transformed) : transformed;
        // STEP 4: register-assign per procedure (skip SRSW; already validated)
        final annotatedProcs = <AnnotatedProcedure>[];
        for (final proc in withReduce.procedures) {
          final (annotatedProc, _) = _analyzeProcedureCollectingErrors(proc, skipSRSW: true);
          annotatedProcs.add(annotatedProc);
        }
        return AnnotatedProgram(withReduce, annotatedProcs);
      }" — the 4-step coordinator. Uses Dart record-style destructuring
      `final (annotatedProc, _) = ...` to ignore the second tuple field.
      Step 2 now dispatches through the renamed `_definedGuardEvaluator`
      field.
    target_decision: >-
      The C# port emits the body as four numbered comment-banner regions
      preserving the same step order. (1) The aggregation loop becomes
      `var allViolations = new List<string>(); foreach (var proc in
      program.Procedures) { allViolations.AddRange(
      _collectSRSWViolationsForProcedure(proc)); }` —
      `AddRange(IEnumerable<T>)` replaces `addAll` (Microsoft Learn
      `List<T>.AddRange`). (2) The Dart `.map(...).join('\n')` chain
      becomes `string.Join('\n', allViolations.Select(v => $"  • {v}"))`
      — reuse rf-dart-list-join-to-csharp-string-join-separator-first
      from type_checker.dart family. (3) The `throw CompileError(...)`
      preserves named `phase: "analyzer"` argument. (4) The Dart positional-
      record destructuring `final (annotatedProc, _) = _analyzeProcedure
      CollectingErrors(proc, skipSRSW: true);` becomes a C# `(var
      annotatedProc, _) = _AnalyzeProcedureCollectingErrors(proc,
      skipSRSW: true);` — Microsoft Learn "Tuple deconstruction" supports
      the `_` discard. The method's return type is `(AnnotatedProcedure,
      IReadOnlyList<string>)` (a ValueTuple). Reuse rf-dart-record-named-
      fields-to-csharp-value-tuple-named-fields from occurrence.dart for
      the tuple shape (though here the tuple is POSITIONAL not named).
      Step 2's call becomes `var transformed = _definedGuardEvaluator
      .TransformDefinedGuards(program);` — the renamed field/class is
      mirrored verbatim from the Dart source.
    idiom_id: null
    research_finding_id: rf-dart-record-named-fields-to-csharp-value-tuple-named-fields
    nuance: >-
      Five intertwined nuances. (1) The `skipGlobalSRSW` flag exists to
      bypass SRSW for linked programs (the comment explains: forwarding-
      pattern alias clauses are safe at program level but locally violate
      SRSW). The C# port MUST preserve the SRSW-bypass semantics — do
      NOT "improve" by trying to validate-but-suppress; the SRSW work
      is genuinely skipped. (2) Step 1 runs on the ORIGINAL program (NOT
      the partial-evaluated one) because partial eval removes defined
      guards, and guard readers must be counted for SRSW pairing. This
      ordering is load-bearing — the C# port MUST run SRSW BEFORE calling
      `_definedGuardEvaluator.TransformDefinedGuards`. (3) Step 2's
      `transformDefinedGuards` may THROW `CompileError` if a guard cannot
      be reduced (suspend) or always fails — this is the STRICT-evaluator
      contract; the C# port lets that exception propagate. (4) Step 3 is
      optional via `generateReduce`; the ternary `condition ? a : b` maps
      directly. (5) Step 4 deliberately passes `skipSRSW: true` because
      step 1 already validated; without this, auto-generated reduce/2
      clauses (which use a forwarding pattern) would re-trigger SRSW
      errors.

  - construct_key: dart.method.collect_srsw_violations_for_procedure_per_clause_walk_with_optional_section_analysis
    source_form: >-
      "List<String> _collectSRSWViolationsForProcedure(Procedure proc) {
        final allViolations = <String>[];
        for (final clause in proc.clauses) {
          final varTable = VariableTable();
          _markConstantTypeVars(clause.head, proc.name, proc.arity, varTable);
          _analyzeAtom(clause.head, varTable);
          if (clause.guards != null && clause.guards!.isNotEmpty) {
            for (final guard in clause.guards!) { _analyzeGuard(guard, varTable); }
          }
          if (clause.body != null && clause.body!.isNotEmpty) {
            for (final goal in clause.body!) { _analyzeGoal(goal, varTable); }
          }
          final violations = varTable.collectSRSWViolations();
          final contextViolations = violations.map((v) => '${proc.name}/${proc.arity}: $v').toList();
          allViolations.addAll(contextViolations);
        }
        return allViolations;
      }" — per-clause walk: build fresh VariableTable, prefix-mark constant-
      type vars from procDecl, analyze head/guards/body, gather violations
      with proc-signature prefix.
    target_decision: >-
      `private IReadOnlyList<string> _CollectSRSWViolationsForProcedure(
      Procedure proc)` returning an aggregated list. Inside: `foreach
      (var clause in proc.Clauses) { var varTable = new VariableTable();
      _MarkConstantTypeVars(clause.Head, proc.Name, proc.Arity, varTable);
      _AnalyzeAtom(clause.Head, varTable); if (clause.Guards is
      { Count: > 0 }) { foreach (var guard in clause.Guards) {
      _AnalyzeGuard(guard, varTable); } } if (clause.Body is
      { Count: > 0 }) { foreach (var goal in clause.Body) { _AnalyzeGoal(
      goal, varTable); } } var contextViolations = varTable.
      CollectSRSWViolations().Select(v => $"{proc.Name}/{proc.Arity}: {v}");
      allViolations.AddRange(contextViolations); }`. Reuse cached
      rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access
      from type_checker.dart for the `guards != null && guards!.isNotEmpty`
      → `Guards is { Count: > 0 }` C# property-pattern (Microsoft Learn:
      "A property pattern matches an expression when the expression's
      result is non-null and every nested pattern matches the corresponding
      property"). The `.map(...).toList()` Dart chain becomes
      `.Select(...)` returning `IEnumerable<string>` — passed directly to
      `AddRange` (no eager `.ToList()` needed; `AddRange` enumerates once).
    idiom_id: null
    research_finding_id: rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access
    nuance: >-
      Three intertwined nuances. (1) The PROC-PREFIX `${proc.name}/${proc
      .arity}: ${v}` is what the REPL test suite greps for to attribute a
      violation to a procedure ("foo/3: Line 7: Writer variable ..."); the
      C# port MUST preserve the exact format including the colon-space
      separator. (2) The `clause.guards != null && clause.guards!
      .isNotEmpty` Dart pattern — empty-vs-null distinction — was already
      established in ast.dart as observable; here the analyzer only walks
      guards if BOTH non-null AND non-empty; the C# port collapses to
      `Guards is { Count: > 0 }` because (a) both `null` and empty mean
      "no guards to walk", AND (b) the property-pattern handles both
      correctly. (3) The fresh `var varTable = new VariableTable();`
      inside the loop is critical for ISOLATION: each clause has its OWN
      SRSW table; do NOT hoist the allocation outside the loop.

  - construct_key: dart.method.generate_reduce_clauses_with_existing_index_lookup_and_merge_or_append
    source_form: >-
      "Program _generateReduceClauses(Program program) {
        final sourceClauses = <Clause>[];
        for (final proc in program.procedures) {
          if (proc.name == 'reduce' && proc.arity == 2) continue;
          sourceClauses.addAll(proc.clauses);
        }
        if (sourceClauses.isEmpty) return program;
        final reduceClauses = <Clause>[];
        for (final clause in sourceClauses) {
          reduceClauses.add(_generateReduceClause(clause));
        }
        final existingReduceIdx = program.procedures.indexWhere(
          (p) => p.name == 'reduce' && p.arity == 2);
        final newProcedures = List<Procedure>.from(program.procedures);
        if (existingReduceIdx >= 0) {
          final existing = newProcedures[existingReduceIdx];
          final mergedClauses = [...existing.clauses, ...reduceClauses];
          newProcedures[existingReduceIdx] = Procedure('reduce', 2, mergedClauses, existing.line, existing.column);
        } else {
          final firstClause = reduceClauses.first;
          newProcedures.add(Procedure('reduce', 2, reduceClauses, firstClause.line, firstClause.column));
        }
        return Program(newProcedures, program.line, program.column);
      }" — collect non-reduce clauses, generate one reduce/2 clause per
      source clause, then merge into existing reduce/2 (if any) or append a
      new procedure. Uses Dart's spread operator `[...a, ...b]` and
      `indexWhere`.
    target_decision: >-
      `private Program _GenerateReduceClauses(Program program) { var
      sourceClauses = new List<Clause>(); foreach (var proc in program.
      Procedures) { if (proc.Name == "reduce" && proc.Arity == 2) continue;
      sourceClauses.AddRange(proc.Clauses); } if (sourceClauses.Count
      == 0) return program; var reduceClauses = sourceClauses.Select(c =>
      _GenerateReduceClause(c)).ToList(); int existingReduceIdx = -1; for
      (int i = 0; i < program.Procedures.Count; i++) { if (program.
      Procedures[i].Name == "reduce" && program.Procedures[i].Arity == 2)
      { existingReduceIdx = i; break; } } var newProcedures = new
      List<Procedure>(program.Procedures); if (existingReduceIdx >= 0) {
      var existing = newProcedures[existingReduceIdx]; var mergedClauses =
      new List<Clause>(existing.Clauses); mergedClauses.AddRange(
      reduceClauses); newProcedures[existingReduceIdx] = new Procedure(
      "reduce", 2, mergedClauses, existing.Line, existing.Column); } else
      { var firstClause = reduceClauses[0]; newProcedures.Add(new
      Procedure("reduce", 2, reduceClauses, firstClause.Line, firstClause.
      Column)); } return new Program(newProcedures, program.Line, program.
      Column); }`. The `indexWhere` Dart helper becomes an explicit
      indexed `for` loop with `break` — Microsoft Learn shows `.Select
      ((p, i) => ...).Where(...).FirstOrDefault()` is possible but
      uglier; the manual for-break loop is more idiomatic .NET. The Dart
      spread `[...a, ...b]` becomes a fresh `new List<T>(a)` followed by
      `AddRange(b)` — Microsoft Learn collection initialiser with spread
      `[..a, ..b]` IS supported in C# 12+ and may be used by the
      generator at codegen time; the spec records both forms as
      equivalent. Reuse rf-dart-list-typed-literal-and-addall-to-csharp-
      list-and-addrange from checker.dart family.
    idiom_id: null
    research_finding_id: rf-dart-list-typed-literal-and-addall-to-csharp-list-and-addrange
    nuance: >-
      Three intertwined nuances. (1) The `continue` on `proc.name ==
      'reduce' && proc.arity == 2` is critical to prevent infinite
      recursion (reduce/2 generated from reduce/2 source). The C# port
      MUST preserve this guard. (2) `indexWhere(... )` returns `-1` if
      no match (Dart documented behaviour); the C# port preserves the
      `>= 0` test against a `-1` sentinel for the manual loop, NOT
      `FirstOrDefault` returning `null` — the index sentinel is what's
      actually needed because the next line writes-by-index. (3)
      `List<Procedure>.from(program.procedures)` creates a SHALLOW copy
      (the procedures themselves are aliased) — the C# `new
      List<Procedure>(program.Procedures)` (Microsoft Learn `List<T>(
      IEnumerable<T>)` "creates a new list that contains elements
      copied from the specified collection") matches that exact aliasing
      semantics: the LIST is fresh, the items are aliased.

  - construct_key: dart.method.generate_one_reduce_clause_with_atom_to_term_and_goals_to_term
    source_form: >-
      "Clause _generateReduceClause(Clause source) {
        final head = source.head; final guards = source.guards; final body = source.body;
        final line = head.line; final col = head.column;
        final headTerm = _atomToTerm(head);
        Term bodyTerm;
        if (body == null || body.isEmpty) { bodyTerm = ConstTerm('true', line, col); }
        else { bodyTerm = _goalsToTerm(body, line, col); }
        final reduceHead = Atom('reduce', [headTerm, bodyTerm], line, col);
        List<Goal>? reduceBody;
        if (guards != null && guards.isNotEmpty) { reduceBody = [Goal('true', [], line, col)]; }
        return Clause(reduceHead, guards: guards, body: reduceBody, line: line, column: col);
      }
      Term _atomToTerm(Atom atom) {
        if (atom.args.isEmpty) return ConstTerm(atom.functor, atom.line, atom.column);
        return StructTerm(atom.functor, atom.args, atom.line, atom.column);
      }
      Term _goalsToTerm(List<Goal> goals, int line, int col) {
        if (goals.isEmpty) return ConstTerm('true', line, col);
        if (goals.length == 1) return _goalToTerm(goals.first);
        var result = _goalToTerm(goals.last);
        for (var i = goals.length - 2; i >= 0; i--) {
          result = StructTerm(',', [_goalToTerm(goals[i]), result], line, col);
        }
        return result;
      }
      Term _goalToTerm(Goal goal) {
        if (goal.args.isEmpty) return ConstTerm(goal.functor, goal.line, goal.column);
        return StructTerm(goal.functor, goal.args, goal.line, goal.column);
      }" — four short methods that lift Atom/Goal AST nodes into Term
      nodes, with the conjunction reduction right-associative
      (`,(A,(B,C))`) and the 0-arity case collapsing to ConstTerm.
    target_decision: >-
      Four 1:1 C# private methods. Reuse the cached AST node mappings from
      ast.dart (`ConstTerm`, `StructTerm`, `Atom`, `Goal`, `Clause` — all
      already specced). The `body == null || body.isEmpty` Dart compound
      becomes C# `body is null or { Count: 0 }` declaration-pattern
      (Microsoft Learn list-pattern + property-pattern combine) — or the
      explicit form `body is null || body.Count == 0` is equally
      acceptable; the spec records both as equivalent. The Dart `for
      (var i = goals.length - 2; i >= 0; i--)` reverse-index loop maps
      EXACTLY to C# `for (int i = goals.Count - 2; i >= 0; i--)` —
      Microsoft Learn "C# for statement" matches Dart's loop semantics
      verbatim. Reuse rf-dart-indexed-min-length-for-loop-to-csharp-
      imperative-for from type_checker.dart family for the explicit
      reverse loop. The Dart `[Goal('true', [], line, col)]` singleton-
      list literal becomes `new List<Goal> { new Goal("true",
      Array.Empty<Term>(), line, col) }` — reuse rf-dart-const-empty-list-
      default-to-csharp-array-empty from ast.dart for the empty-args
      shape.
    idiom_id: null
    research_finding_id: rf-dart-indexed-min-length-for-loop-to-csharp-imperative-for
    nuance: >-
      Three intertwined nuances. (1) The right-associative conjunction
      build is *load-bearing* for downstream consumers (Microsoft Learn
      foldRight equivalent — but here a manual reverse for-loop is used,
      no LINQ Aggregate, because the seed value differs from the first
      element). The C# port MUST preserve the reverse-iteration order:
      seed = last goal; for each i from length-2 down to 0, wrap into
      `,(goal[i], result)`. (2) The `Goal('true', [], line, col)` synthetic
      sentinel inside the singleton-list body is the GLP "always-succeed"
      goal; the C# port preserves the exact functor `"true"` string. (3)
      The `_atomToTerm` and `_goalToTerm` helpers BOTH collapse 0-arity
      AST nodes to `ConstTerm` and keep n-arity as `StructTerm` — Dart
      `args.isEmpty` ⇒ C# `Args.Count == 0`. The `isEmpty` Dart property
      is on `Iterable`, mapped to the `.Count == 0` form for `IReadOnly
      List<T>` per the rf-dart-length-isempty-to-csharp-count cached
      idiom (mode_table.dart family).

  - construct_key: dart.method.analyze_procedure_and_clause_collecting_errors_with_skip_flag
    source_form: >-
      "AnnotatedProcedure _analyzeProcedure(Procedure proc) { ... per-clause
      _analyzeClause loop ... return AnnotatedProcedure(proc, proc.name,
      proc.arity, annotatedClauses); }
      (AnnotatedProcedure, List<String>) _analyzeProcedureCollectingErrors(
        Procedure proc, {bool skipSRSW = false}) { ... per-clause
        _analyzeClauseCollectingErrors loop, accumulating violations ...
        return (AnnotatedProcedure(...), allViolations); }
      AnnotatedClause _analyzeClause(Clause clause) { ... walks head/guards/body,
        calls varTable.verifySRSW() (throwing), assigns registers ... }
      (AnnotatedClause, List<String>) _analyzeClauseCollectingErrors(
        Clause clause, String procName, int procArity, {bool skipSRSW = false}) {
        ... same shape but uses collectSRSWViolations() (non-throwing),
        contextual prefix per violation ... }" — four sibling methods,
      a throwing pair and a collecting pair, sharing the same walk
      sequence.
    target_decision: >-
      Four methods preserving the throw/collect distinction. The
      throwing pair: `private AnnotatedProcedure _AnalyzeProcedure(
      Procedure proc)` and `private AnnotatedClause _AnalyzeClause(Clause
      clause)` — bodies essentially identical to the collecting pair
      except that the SRSW step calls `varTable.VerifySRSW()` (which
      throws on first violation) instead of accumulating. The collecting
      pair returns a `(AnnotatedProcedure, IReadOnlyList<string>)` /
      `(AnnotatedClause, IReadOnlyList<string>)` ValueTuple (reuse
      rf-dart-record-named-fields-to-csharp-value-tuple-named-fields).
      The four methods share the same head/guard/body walk skeleton; the
      C# port preserves duplication (do NOT refactor to a shared helper
      — the source duplication is a deliberate choice keeping the two
      reporting paths independent and the throwing path simple). The
      `skipSRSW: true` parameter (used by step 4 for already-validated
      programs) becomes `bool skipSRSW = false` named-default in C#;
      reuse rf-dart-named-default-param-to-csharp-optional-arg.
    idiom_id: null
    research_finding_id: rf-dart-record-named-fields-to-csharp-value-tuple-named-fields
    nuance: >-
      Three intertwined nuances. (1) The throw-vs-collect split is a
      load-bearing design: the throwing path is the "happy path" for
      callers that want bail-on-first-error semantics (unified REPL
      compile); the collecting path is for callers that want to surface
      ALL violations at once (linter mode, IDE diagnostics). The C# port
      MUST preserve both — do NOT collapse to a single
      `IReadOnlyList<string>`-returning method with optional throw.
      (2) `_analyzeProcedure` (the throwing one) is DEAD CODE in the
      current source — only `_analyzeProcedureCollectingErrors` is
      invoked from `analyze()`. The C# port retains it for parity per
      the preserve-working-code discipline (preserve unused but defined
      methods unless explicitly approved for removal). (3) The varTable
      register-assignment via `_assignRegisters(varTable);` happens AFTER
      SRSW collection even if violations were found — the comment "even
      if there are violations, for partial analysis" justifies this; the
      C# port MUST preserve the post-violation register assignment so
      partial-error tooling can still emit register names.

  - construct_key: dart.method.analyze_atom_and_goal_argument_walk
    source_form: >-
      "void _analyzeAtom(Atom atom, VariableTable varTable) {
        for (final arg in atom.args) { _analyzeTerm(arg, varTable); }
      }
      void _analyzeGoal(Goal goal, VariableTable varTable) {
        for (final arg in goal.args) { _analyzeTerm(arg, varTable); }
      }" — two trivial dispatchers that walk an Atom's or Goal's
      arguments, defaulting `inHeadOrBody` to true.
    target_decision: >-
      `private void _AnalyzeAtom(Atom atom, VariableTable varTable) {
      foreach (var arg in atom.Args) { _AnalyzeTerm(arg, varTable); } }`
      and likewise `_AnalyzeGoal(Goal goal, ...)`. Both use the default
      `inHeadOrBody: true` (positional default in C#). Direct 1:1; no
      novel research needed.
    trivial: true
    nuance: >-
      Trivial walkers; no nuance. Two distinct methods are preserved
      (rather than a single generic `IReadOnlyList<Term> args` helper)
      because the Dart source has two — and `Atom` and `Goal` are
      DISTINCT C# types per ast.dart spec (a `Goal` is NOT an `Atom`),
      so a generic helper would require an interface that the source
      does not need.

  - construct_key: dart.class_static_const_string_sets_negatable_nonnegatable_invalid_guard_dispatch_tables
    source_form: >-
      "static const _negatableGuards = {
        'ground', 'known', 'unknown', 'integer', 'number', 'atom', 'string',
        'constant', 'compound', 'tuple', 'list', 'is_list', 'module',
        'is_mutual_ref', 'no_readers', '=?=',
      };
      static const _nonNegatableGuards = {
        '<', '>', '=<', '>=', '=:=', '=\\=', 'otherwise', 'wait', 'wait_until',
      };
      static const _invalidInGuardPosition = {'true', 'false', 'fail'};" — three
      `static const` string-set fields on the `Analyzer` class, used by
      `_analyzeGuard` as dispatch tables for negation/validity checks.
    target_decision: >-
      Three `private static readonly FrozenSet<string>` fields on
      `Analyzer`: `private static readonly FrozenSet<string>
      NegatableGuards = new[] { "ground", "known", ..., "=?=" }
      .ToFrozenSet(StringComparer.Ordinal);` and likewise `NonNegatable
      Guards` and `InvalidInGuardPosition`. Reuse cached
      rf-dart-const-set-to-csharp-frozenset-ordinal verbatim (parser.dart,
      checker.dart, glp_printer.dart all use it). Microsoft Learn:
      "FrozenSet<T> is optimized for fast read-only access ... ideal for
      sets that are populated once and used many times". The three sets
      are initialised at static-ctor time (build-once, query-many) — a
      perfect fit. The escape-sequence `'=\\='` in the Dart source becomes
      C# `"=\\="` — same backslash escape rules (Microsoft Learn string
      literals).
    idiom_id: null
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Two intertwined nuances. (1) These three sets discriminate the
      legality of negation (`~`) and guard-position usage. Their CONTENTS
      are the GLP language spec — the C# port MUST preserve set members
      verbatim (NOT add or remove anything), because adding a member
      would silently relax/tighten the language. Microsoft Learn warns
      against modifying `FrozenSet` after construction (it cannot be
      modified — perfect semantics). (2) The StringComparer.Ordinal is
      critical: GLP operator strings (`'<'`, `'=:='`) and predicate names
      (`'ground'`, `'wait_until'`) are matched byte-for-byte; culture
      folding would silently break the legality check for any non-ASCII
      future extension (e.g. if `'<'` ever varied by culture, which it
      won't, but the discipline is the same).

  - construct_key: dart.method.analyze_guard_dispatch_table_over_predicate_and_arity
    source_form: >-
      "void _analyzeGuard(Guard guard, VariableTable varTable) {
        if (_invalidInGuardPosition.contains(guard.predicate)) { throw CompileError(...); }
        if (guard.negated) {
          if (_nonNegatableGuards.contains(guard.predicate)) { throw CompileError(...); }
        }
        if (guard.predicate == 'ground' && guard.args.length == 1) {
          final arg = guard.args[0]; if (arg is VarTerm) { varTable.markGrounded(arg.name); }
        }
        final typeCheckOps = ['number', 'integer', 'atom', 'string', 'list', 'tuple', 'compound', 'constant'];
        if (typeCheckOps.contains(guard.predicate) && guard.args.length == 1) { ... markGrounded ... }
        if (guard.predicate == 'module' && guard.args.length == 1) { ... markGrounded ... }
        if (guard.predicate == 'is_mutual_ref' && guard.args.length == 1) { ... markGrounded ... }
        if (guard.predicate == 'unknown' && guard.args.length == 1) { ... markGrounded ... }
        if (guard.predicate == 'wait_until' && guard.args.length == 1) { ... markGrounded ... }
        if (guard.predicate == 'wait' && guard.args.length == 1) { ... markGrounded ... }
        final comparisonOps = ['<', '>', '=<', '>=', '=:=', '=\\='];
        if (comparisonOps.contains(guard.predicate) && guard.args.length == 2) {
          for (final arg in guard.args) { _extractAndMarkGroundedVars(arg, varTable); }
        }
        if (guard.predicate == '=?=' && guard.args.length == 2) { ... markGrounded both args ... }
        for (final arg in guard.args) { _analyzeTerm(arg, varTable, inHeadOrBody: false); }
      }" — a long cascade of `if (predicate == X && arity == N)` tests
      that mark guard arguments as grounded for SRSW relaxation, plus
      the legality checks at the top and the final argument walk.
    target_decision: >-
      `private void _AnalyzeGuard(Guard guard, VariableTable varTable)`
      with the same dispatch cascade. The cascade COULD be rewritten as
      a C# `switch (guard.Predicate, guard.Args.Count) { case ("ground",
      1): ...; case ("module", 1): ...; case ("=?=", 2): ...; default:
      break; }` (Microsoft Learn tuple-switch + relational pattern
      combine), but the spec PRESERVES the `if`-cascade because (a) the
      Dart source has many branches that share the same body
      (`markGrounded` on the single VarTerm arg), (b) several branches
      use SET lookups (`typeCheckOps.contains`, `comparisonOps.contains`)
      that don't fit a single switch case cleanly, and (c) the C# code
      generator may freely rewrite to a switch if profitable — both forms
      are semantically equivalent. The `final typeCheckOps = [...]` and
      `final comparisonOps = [...]` LOCAL lists are HOISTED in the C#
      port to class-scoped `private static readonly FrozenSet<string>
      TypeCheckOps` and `ComparisonOps` — reuse the same
      rf-dart-const-set-to-csharp-frozenset-ordinal idiom (the Dart
      local-`const`-list-then-`contains` is an anti-pattern that
      allocates per-call; C# Frozen-static-set is the canonical fix and
      semantics-preserving). The final argument walk `for (final arg in
      guard.args) { _analyzeTerm(arg, varTable, inHeadOrBody: false); }`
      becomes a C# `foreach (var arg in guard.Args) { _AnalyzeTerm(arg,
      varTable, inHeadOrBody: false); }` — the `false` is named-passed
      to match the Dart named argument.
    idiom_id: null
    research_finding_id: rf-dart-is-test-smart-cast-to-csharp-declaration-pattern
    nuance: >-
      Five intertwined nuances. (1) The `arg is VarTerm` smart-cast in
      Dart (the analyzer auto-narrows `arg` to `VarTerm` inside the if-
      branch) maps EXACTLY to C# `if (arg is VarTerm varArg) { varTable.
      MarkGrounded(varArg.Name); }` declaration-pattern. Reuse the
      cached rf-dart-is-test-smart-cast-to-csharp-declaration-pattern
      from type_checker.dart. (2) The cascade is NOT exclusive — multiple
      `if`s can match (e.g. an `=?=` is also `args.length == 2`); the C#
      port MUST preserve the cascade, not chain `else if`. (3) The
      LOCAL `typeCheckOps`/`comparisonOps` Dart lists ALLOCATE per call
      — hoisting to module-static `FrozenSet` is a *performance*
      improvement that is observationally identical (the lists are
      logically constant; their contents are documented in the
      surrounding comments). (4) `inHeadOrBody: false` on the final
      argument walk is what makes guard occurrences NOT count toward
      SRSW pairing — load-bearing; the C# port MUST pass `false` (NOT
      omit the argument relying on a default — the default is `true`).
      (5) The `_extractAndMarkGroundedVars(arg, varTable)` branch for
      comparison ops handles ARITHMETIC EXPRESSION arguments (e.g.
      `X? + 1 < Y? * 2`); the recursive helper walks the expression tree
      marking every `VarTerm` as grounded. The C# port preserves the
      recursion. The `comparisonOps` set membership check uses set-
      Contains which is O(1) under `FrozenSet` (Microsoft Learn).

  - construct_key: dart.method.recursive_var_walk_extract_and_mark_grounded_and_mark_type_grounded
    source_form: >-
      "void _extractAndMarkGroundedVars(Term term, VariableTable varTable) {
        if (term is VarTerm) { varTable.markGrounded(term.name); }
        else if (term is StructTerm) { for (final arg in term.args) {
          _extractAndMarkGroundedVars(arg, varTable); } }
        else if (term is ListTerm) {
          if (term.head != null) { _extractAndMarkGroundedVars(term.head!, varTable); }
          if (term.tail != null) { _extractAndMarkGroundedVars(term.tail!, varTable); }
        }
      }
      void _markVarsInTermAsTypeGrounded(Term term, VariableTable varTable) {
        if (term is VarTerm) { varTable.markTypeGrounded(term.name); }
        else if (term is StructTerm) { for (final arg in term.args) {
          _markVarsInTermAsTypeGrounded(arg, varTable); } }
        else if (term is ListTerm) {
          if (term.head != null) _markVarsInTermAsTypeGrounded(term.head!, varTable);
          if (term.tail != null) _markVarsInTermAsTypeGrounded(term.tail!, varTable);
        }
      }" — two structurally-identical recursive Term walkers that differ
      only in WHICH `mark*` method on `VariableTable` they invoke for
      `VarTerm` leaves.
    target_decision: >-
      Two C# methods preserving the structural duplication: `private void
      _ExtractAndMarkGroundedVars(Term term, VariableTable varTable) {
      switch (term) { case VarTerm v: varTable.MarkGrounded(v.Name);
      break; case StructTerm s: foreach (var arg in s.Args)
      _ExtractAndMarkGroundedVars(arg, varTable); break; case ListTerm l:
      if (l.Head is not null) _ExtractAndMarkGroundedVars(l.Head,
      varTable); if (l.Tail is not null) _ExtractAndMarkGroundedVars(
      l.Tail, varTable); break; } }` — Microsoft Learn type-pattern
      switch with declaration-pattern. The two methods are NOT refactored
      to a single helper taking a callback (`Action<string>`) because
      (a) the Dart source has two methods and (b) the call-site shape
      (`varTable.markGrounded(arg.name)` vs `varTable.markTypeGrounded(
      arg.name)`) is too small a delta to justify a callback that would
      ALLOCATE per call (a delegate has heap overhead). Reuse
      rf-dart-is-type-test-chain-to-csharp-pattern-switch from checker.dart
      family for the if-cascade → switch translation. The Dart null-
      checks `term.head != null` followed by `term.head!` become C# `is
      not null` declaration-pattern; reuse rf-dart-nullable-field-bang-
      after-null-check-to-csharp-flow-analysis from occurrence.dart.
    idiom_id: null
    research_finding_id: rf-dart-is-type-test-chain-to-csharp-pattern-switch
    nuance: >-
      Three intertwined nuances. (1) The two walkers differ ONLY in the
      VarTerm-leaf action; the C# port MUST preserve TWO methods (NOT
      one shared helper with a callback) because (a) the Dart source has
      two, (b) a delegate-callback would allocate, and (c) the analyzer
      walks may be called in hot paths during type-check compilation.
      Microsoft Learn shows generic delegates have heap allocation cost
      that a direct call does not. (2) `term is VarTerm` smart-cast →
      `case VarTerm v:` declaration-pattern — auto-narrows on the case
      branch. (3) `ConstTerm` and `UnderscoreTerm` are SILENTLY ignored
      (fall through the if-cascade without matching) — the C# `switch`
      omits a default arm (or uses `default: break;`) to preserve that
      behaviour. Microsoft Learn: a `switch` statement without an
      exhaustive default falls through harmlessly when no arm matches.

  - construct_key: dart.method.analyze_term_recursive_record_var_then_validate_const_in_user_mode
    source_form: >-
      "void _analyzeTerm(Term term, VariableTable varTable, {bool inHeadOrBody = true}) {
        if (term is VarTerm) {
          if (term.name.startsWith('_')) return;
          if (term.isReader) { varTable.recordReaderOccurrence(term.name, term, inHeadOrBody: inHeadOrBody); }
          else { varTable.recordWriterOccurrence(term.name, term, inHeadOrBody: inHeadOrBody); }
        } else if (term is StructTerm) { for (final arg in term.args) {
          _analyzeTerm(arg, varTable, inHeadOrBody: inHeadOrBody); } }
        else if (term is ListTerm) { ... recursive ... }
        else if (term is ConstTerm) {
          final value = term.value;
          if (_compileMode == CompileMode.user && value is String && value.startsWith('_')) {
            throw CompileError("Constants starting with '_' are reserved for system use: '$value'. "
              "Use -mode(system). directive for system code.", term.line, term.column, phase: 'analyzer');
          }
        }
      }" — the central term-walker. Branches on Term subtype, records
      occurrences for VarTerm, recurses for compound terms, and validates
      ConstTerm strings against the "reserved underscore-prefix in user
      mode" rule.
    target_decision: >-
      `private void _AnalyzeTerm(Term term, VariableTable varTable,
      bool inHeadOrBody = true) { switch (term) { case VarTerm v: if
      (v.Name.StartsWith('_')) return; if (v.IsReader) varTable.
      RecordReaderOccurrence(v.Name, v, inHeadOrBody); else varTable.
      RecordWriterOccurrence(v.Name, v, inHeadOrBody); break; case
      StructTerm s: foreach (var arg in s.Args) _AnalyzeTerm(arg,
      varTable, inHeadOrBody); break; case ListTerm l: if (l.Head is
      not null) _AnalyzeTerm(l.Head, varTable, inHeadOrBody); if (l.Tail
      is not null) _AnalyzeTerm(l.Tail, varTable, inHeadOrBody); break;
      case ConstTerm c: var value = c.Value; if (_compileMode ==
      CompileMode.User && value is string s && s.StartsWith('_')) throw
      new CompileError($"Constants starting with '_' are reserved for
      system use: '{value}'. Use -mode(system). directive for system
      code.", c.Line, c.Column, phase: "analyzer"); break; } }` —
      Microsoft Learn type-pattern switch. The `value is string s` C# is-
      pattern auto-narrows; the `&& s.StartsWith('_')` runs only after
      narrowing succeeds. The string interpolation `'$value'` (Dart) →
      `{value}` (C#); reuse rf-dart-tostring-interp-to-csharp-tostring-
      interp.
    idiom_id: null
    research_finding_id: rf-dart-is-type-test-chain-to-csharp-pattern-switch
    nuance: >-
      Four intertwined nuances. (1) The anonymous-variable short-circuit
      (`if (term.name.startsWith('_')) return;`) is the SECOND copy of
      this rule (the FIRST is in `VariableTable.recordWriterOccurrence`/
      `recordReaderOccurrence`); the C# port MUST preserve BOTH copies
      (the inner one is defensive — if `recordReaderOccurrence` were
      ever called with an underscore name from elsewhere, the table
      would still skip it). This redundancy is intentional. (2) The
      `_compileMode == CompileMode.user` reads the analyzer-instance
      state set by `analyze()` at top-of-method; under `CompileMode.
      system` the reserved-underscore check is DISABLED. The C# port
      MUST preserve the mode-dependence — do NOT make it a static check.
      (3) `term.value` Dart is `Object?` (any constant value); the C#
      port's `ConstTerm.Value` is `object?` (per ast.dart); the
      `value is string s` declaration-pattern handles both the null case
      and the non-string case correctly (only matches non-null strings).
      (4) The `inHeadOrBody` flag plumbs through all recursive calls
      verbatim — the C# port MUST pass it through, not default it.

  - construct_key: dart.method.assign_registers_sequential_temporary_loop_with_todo
    source_form: >-
      "void _assignRegisters(VariableTable varTable) {
        int nextIndex = 0;
        for (final info in varTable.getAllVars()) {
          info.isTemporary = true;
          info.registerIndex = nextIndex++;
        }
      }" — a trivial register-assignment loop that marks every variable
      as temporary (an X-register) with a sequential index. The TODO
      comment notes that a future lifetime analysis should distinguish
      X (temporary) from Y (permanent) registers.
    target_decision: >-
      `private void _AssignRegisters(VariableTable varTable) { int
      nextIndex = 0; foreach (var info in varTable.GetAllVars()) { info.
      IsTemporary = true; info.RegisterIndex = nextIndex++; } }` — direct
      1:1. The TODO comment carries over as a `// TODO:` C# comment.
      Microsoft Learn: post-increment `nextIndex++` returns the value
      then increments; same semantics as Dart.
    trivial: true
    nuance: >-
      Trivial loop. One nuance: `getAllVars()` returns a SNAPSHOT (Dart
      `_vars.values.toList()` — see the VariableTable construct above),
      so the loop is safe even though the assignment writes to
      `info.registerIndex` (which is on the VariableInfo objects, NOT on
      `_vars` — no mutation-during-enumeration risk). The C# port
      preserves the snapshot via `_vars.Values.ToList()` in `GetAllVars`.

  - construct_key: dart.method.mark_constant_type_vars_in_head_via_proc_decl_lookup
    source_form: >-
      "void _markConstantTypeVars(Atom head, String procName, int procArity, VariableTable varTable) {
        final key = '$procName/$procArity';
        final procDecl = _procDecls[key];
        if (procDecl == null) return;
        for (int i = 0; i < head.args.length && i < procDecl.argTypes.length; i++) {
          final typeName = procDecl.getTypeName(i);
          if (_isConstantType(typeName)) {
            _markVarsInTermAsTypeGrounded(head.args[i], varTable);
          }
        }
      }" — looks up the head's procedure declaration in `_procDecls` by
      signature, and for each arg whose declared type is a constant type,
      marks every variable in that arg subtree as type-grounded.
    target_decision: >-
      `private void _MarkConstantTypeVars(Atom head, string procName, int
      procArity, VariableTable varTable) { var key = $"{procName}/{
      procArity}"; if (!_procDecls.TryGetValue(key, out var procDecl))
      return; for (int i = 0; i < head.Args.Count && i < procDecl.
      ArgTypes.Count; i++) { var typeName = procDecl.GetTypeName(i); if
      (IsConstantType(typeName)) _MarkVarsInTermAsTypeGrounded(head.Args
      [i], varTable); } }`. The Dart `_procDecls[key]` returning `null`
      for absent maps to C# `TryGetValue` returning `false` — reuse
      cached rf-dart-map-lookup-to-csharp-trygetvalue (parser.dart,
      pmt/type_table.dart family). The `min(a,b)`-style guard via
      `i < a && i < b` becomes idiomatic C# `for` with the same
      conjoined condition (Microsoft Learn for-statement: any boolean
      expression in the condition). Reuse rf-dart-indexed-min-length-for-
      loop-to-csharp-imperative-for.
    idiom_id: null
    research_finding_id: rf-dart-map-lookup-to-csharp-trygetvalue
    nuance: >-
      Three intertwined nuances. (1) The `_procDecls` map is reset every
      `analyze()` call and populated optionally from the public
      `procDeclarations` parameter — if no declarations are passed, the
      map is empty and `_MarkConstantTypeVars` is a no-op. The C# port
      preserves the early-return on `procDecl == null` (or
      `TryGetValue` returning `false`). (2) The `i < head.args.length &&
      i < procDecl.argTypes.length` guard handles the case where the
      head has more or fewer args than the declaration (defensive — the
      type checker would normally have already caught arity mismatch).
      The C# port preserves both bounds. (3) `procDecl.getTypeName(i)`
      returns a `String?` (nullable type-name); `_isConstantType` null-
      guards. The C# port preserves the nullable-string-then-set-lookup
      chain.

  - construct_key: dart.class.analyzer_internal_defined_guard_evaluator_class_with_guard_unfolding
    source_form: >-
      "class DefinedGuardEvaluator {
        int _varCounter = 0;
        Program transformDefinedGuards(Program program) { ... }
        Map<String, List<Term>> _collectUnitClauses(Program program) { ... }
        Clause _transformClause(Clause clause, Map<String, List<Term>> unitClauses) { ... }
        List<Term> _renameUnitClauseVars(List<Term> args) { ... }
        void _collectVarNames(Term term, Set<String> names) { ... }
        Term _applyRenaming(Term term, Map<String, String> renaming) { ... }
        UnifyResult _glpUnifyForPE(List<Term> callArgs, List<Term> unitArgs) { ... }
        void _substSet(Map<String, Term> subst, String key, Term value) { ... }
        UnifyResult? _unifyTerms(Term callArg, Term unitArg, Map<String, Term> subst, Set<String> suspSet) { ... }
        UnifyResult? _checkCompatible(Term existing, Term newTerm, Map<String, Term> subst, Set<String> suspSet) { ... }
        bool _isAnonymous(Term term) { ... }
        Map<String, Term> _resolveSubstitution(Map<String, Term> subst) { ... }
        Term _resolveTerm(Term term, Map<String, Term> subst, Set<String> visited) { ... }
        Term _applySubstitution(Term term, Map<String, Term> subst) { ... }
        Atom _applySubstitutionToAtom(Atom atom, Map<String, Term> subst) { ... }
        Guard _applySubstitutionToGuard(Guard guard, Map<String, Term> subst) { ... }
        Goal _applySubstitutionToGoal(Goal goal, Map<String, Term> subst) { ... }
      }" — the STRICT compile-time guard evaluator (renamed from
      `PartialEvaluator` to `DefinedGuardEvaluator` to disambiguate from
      the LENIENT class of the same shape in partial_evaluator.dart).
      Operates on DEFINED GUARDS (single-clause unit predicates).
      Performs compile-time GLP three-valued unification, renaming of
      unit-clause variables, a fixpoint guard-reduction loop, and
      recursive substitution machinery. THROW behaviour on suspend/fail
      is the distinctive feature: `_transformClause` throws CompileError
      (the lenient sibling in partial_evaluator.dart returns failure as
      part of the `UnifyResult` ADT). Uses the SHARED `UnifyResult`
      sealed ADT (lifted to `lib/compiler/unify_result.dart` after the
      duplicate-class-name refactor — same ADT instance type as
      `partial_evaluator.dart`'s `PartialEvaluator` consumes).
    target_decision: >-
      Emit a `public sealed class DefinedGuardEvaluator` in the
      `Glp.Runtime.Compiler` namespace (no name clash — the lenient class
      remains `PartialEvaluator`; the two names are distinct in C# as
      they are in the refactored Dart source). DELEGATE the
      construct-by-construct mapping for every helper method to the
      cached idioms / construct-keys established by
      partial_evaluator.dart.md — each helper has a 1:1 counterpart
      already specced there, and the codegen stage REUSES those
      decisions:
        * `_varCounter` int field → cf. dart.classfield.int_counter_for_fresh_variable_names_with_prefix_PE
        * `transformDefinedGuards` → cf. dart.method.transform_program_via_per_procedure_per_clause_loop_returning_new_immutable_program
        * `_collectUnitClauses` → cf. dart.procedure.unit_clause_extractor_filter_predicate_with_nested_body_shape_test
        * `_transformClause` (the fixpoint loop with throw-on-fail/suspend) → cf. dart.method.transform_clause_fixpoint_loop_with_three_unify_arms_throwing_on_fail_or_suspend
        * `_renameUnitClauseVars` → cf. dart.method.rename_clause_variables_fresh_with_underscore_preservation
        * `_collectVarNames` → cf. dart.method.var_name_collector_recursive_descent_term_walk
        * `_applyRenaming` → cf. dart.method.apply_renaming_recursive_term_rebuild_with_underscore_demotion
        * `_glpUnifyForPE` → cf. dart.method.glp_compile_time_three_valued_unification_phase1_collection_phase2_resolution
        * `_substSet` → cf. dart.method.substset_helper_propagating_through_alias_chain
        * `_unifyTerms` → cf. dart.method.unify_terms_recursive_six_arm_branching_writer_reader_const_struct_list_underscore
        * `_checkCompatible` → cf. dart.method.check_compatible_structural_compatibility_const_struct_loose_default_accept
        * `_isAnonymous` → cf. dart.method.is_underscore_test_unioning_two_dart_runtime_types
        * `_resolveSubstitution` / `_resolveTerm` → cf. dart.method.resolve_substitution_flatten_chains_with_cycle_protection
        * `_applySubstitution` / `_applySubstitutionToAtom` / `_applySubstitutionToGuard` / `_applySubstitutionToGoal` → cf. dart.method.apply_substitution_to_term_atom_guard_goal_with_remoteGoal_spawnGoal_preservation
      The `UnifyResult` ADT (sealed `UnifyResult` + `UnifySuccess` /
      `UnifyFail` / `UnifySuspend`) is NOT specced here — it lives in
      `unify_result.dart` and the spec is in
      `.codeconv/conversion-specs/lib/compiler/unify_result.dart.md`
      (planned as a separate convspec). The C# port references the same
      shared type from both `DefinedGuardEvaluator` and the lenient
      `PartialEvaluator`. Reuse cached
      rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields
      for the class shape; reuse the per-helper cached findings listed
      above.
    idiom_id: null
    research_finding_id: rf-dart-coordinator-class-with-final-deps-to-csharp-class-with-readonly-fields
    nuance: >-
      Five intertwined nuances. (1) RENAME and DEDUPLICATION resolved
      previously-recorded escalations: the Dart source was refactored
      so the strict-and-throwing variant is `DefinedGuardEvaluator`
      (this file) and the lenient-and-returning variant remains
      `PartialEvaluator` (in partial_evaluator.dart); both consume a
      SHARED `UnifyResult` ADT imported from `unify_result.dart`. In the
      C# port both classes coexist in the same `Glp.Runtime.Compiler`
      namespace with distinct names — no clash. (2) The throw-vs-return
      behaviour difference between the two evaluator classes is
      load-bearing: `DefinedGuardEvaluator._transformClause` THROWS
      `CompileError` on `UnifyFail` and `UnifySuspend` (defined guards
      that cannot reduce at compile time ARE compile errors); the
      lenient `PartialEvaluator` (reduce/2 unfolding) consumes the same
      `UnifyResult` arms but treats failure as "leave the goal alone".
      The C# port MUST preserve this throw-vs-return distinction even
      though both classes consume the same shared ADT type. (3) The
      compile-time unification semantics are IDENTICAL between the two
      files (writer-vs-writer alias, writer-vs-const bind, reader-vs-any
      add to suspension, structure recurses, list recurses, underscore
      always succeeds) — so the cached decisions apply verbatim to both
      C# classes. (4) The `_varCounter` fresh-variable naming uses
      prefix `PE_` (NOT `_` — would be confused with anonymous); the C#
      port preserves the literal `"PE_"` string for source-level
      identity with the lenient sibling. (5) Per-pipeline-call decision:
      `Compiler.compile` (per the user's Option (b) change) now also
      imports `partial_evaluator.dart`, meaning a free `PartialEvaluator()`
      call site in `compiler.dart` resolves to the LENIENT variant; the
      analyzer's own internal field is the STRICT `DefinedGuardEvaluator`
      — the C# port mirrors the same disambiguation at call sites
      (different class names, no ambiguity).

conversion_units:
  - analyzer.cs (top-level — host the public Analyzer class plus the
    file-private VariableInfo, VariableTable, AnnotatedProgram,
    AnnotatedProcedure, AnnotatedClause, and the file-internal
    DefinedGuardEvaluator. The shared `UnifyResult` ADT lives in a
    separate conversion unit unify_result.cs, consumed by both this
    file and partial_evaluator.cs.)

escalations: []
```

## Embedded rationale + provenance

### Imports and module shape
The analyzer's imports map to the established same-namespace-collapse
rule (rf-dart-relative-import-to-csharp-using-or-same-namespace); no new
research needed. The new `unify_result.dart` sibling import is a
direct consequence of the refactor that lifted the shared `UnifyResult`
ADT out of analyzer.dart and partial_evaluator.dart into a single
shared file (resolving two prior escalations). The `show
getPreludeUnitClauses` filter on `partial_evaluator.dart` is
observationally vacuous in C# once the same-namespace rule is applied.
See partial_evaluator.dart.md and unify_result.dart.md (planned) for
the full provenance of the related idioms.

### VariableInfo + VariableTable
This is the canonical "mutable per-clause analysis state" pattern.
`VariableInfo` is a sealed reference class (NEVER a record — equality
must stay reference-identity because instances are stored in identity-
keyed maps `_vars[name]`, and a record's structural equality would
cause two `VariableInfo("X", true)` instances to collide). `VariableTable`
owns one dictionary plus two HashSets, all with `StringComparer.Ordinal`
(GLP variable names are case-sensitive — `X` and `x` are distinct).
Microsoft Learn `HashSet<T>` and `Dictionary<TKey,TValue>` are the
canonical .NET equivalents of Dart `Set<T>` / `Map<K,V>`.

The four-branch `CollectSRSWViolations` accumulator emits user-visible
strings that the unified REPL test suite greps for verbatim — the
C# port MUST preserve message strings byte-for-byte (including the bare
double-quote around variable names and the `?` suffix on reader
variants). This is the strictest culture-invariance constraint in
the file.

### `verifySRSW` and the regex-strip
The legacy `verifySRSW` re-throws the first violation as a `CompileError`
with file-position from a SEARCHED variable (NOT the same one whose
message came first; usually the same in practice). The regex strip of
the `"Line N: "` prefix uses a compiled static regex
(`RegexOptions.Compiled`, Microsoft Learn). The `firstWhere` + `orElse`
Dart pair maps cleanly to `FirstOrDefault` + `??` + `First()` — the
non-empty precondition is guaranteed by the caller's prior `isNotEmpty`
test.

### Annotated AST wrappers
Three shallow data classes; reuse the cached
rf-dart-final-field-class-to-csharp-getonly-class verbatim. The mutable
codegen-only fields on `AnnotatedProcedure` (`entryPC`, `entryLabel`) are
preserved as `{ get; set; }` (not `{ get; init; }`) so the codegen pass
can back-fill them.

### `_constantTypes` set + `_isConstantType` function
Reuse rf-dart-const-set-to-csharp-frozenset-ordinal (cached, used in
parser.dart, checker.dart, glp_printer.dart) verbatim. `FrozenSet<T>` +
`StringComparer.Ordinal` matches Dart's byte-identity string equality.
Microsoft Learn explicit on `FrozenSet`: "Optimized for fast read-only
access".

### Analyzer class — 4-step pipeline + dispatch helpers
The public `Analyze` method runs (1) SRSW validation, (2) defined-guard
partial-eval via the renamed `_definedGuardEvaluator` field, (3)
optional reduce/2 generation, (4) per-procedure register-assignment.
The step ordering is load-bearing — partial-eval removes defined
guards, which means SRSW must precede it (otherwise guard readers would
be uncounted for pairing). The C# port MUST preserve the order
verbatim. Microsoft Learn on `List<T>.AddRange` / `string.Join` /
`Enumerable.Select` covers the LINQ pieces.

The `_partialEvaluator` field rename to `_definedGuardEvaluator` (and
the class rename `PartialEvaluator` → `DefinedGuardEvaluator`) is a
Dart-source refactor that the C# port mirrors literally. It carries no
semantic change inside this file: the analyzer continues to construct
its own strict evaluator instance eagerly at field-init time, and the
4-step pipeline continues to invoke `TransformDefinedGuards` at step 2.

### `_analyzeGuard` — the long dispatch cascade
A long cascade of `if (predicate == X && arity == N)` branches that
mark grounded variables for SRSW relaxation. Several branches use SET
lookups against three LOCAL `<String>[]` arrays (`typeCheckOps`,
`comparisonOps`) that ALLOCATE per call — the C# port HOISTS these to
module-static `FrozenSet<string>` fields (observationally identical,
strictly faster). The `arg is VarTerm` smart-cast maps to C# `if (arg
is VarTerm v)` declaration-pattern. Microsoft Learn type-pattern docs
cover the conversion.

### Recursive Term walkers
Two structurally-identical walkers (`_extractAndMarkGroundedVars`,
`_markVarsInTermAsTypeGrounded`) that differ only in which `mark*`
method they invoke. The C# port preserves TWO methods rather than
refactoring to a shared callback-based helper (delegate allocation
overhead would be paid in hot paths). Reuse cached
rf-dart-is-type-test-chain-to-csharp-pattern-switch.

### Central `_analyzeTerm`
The central recursive term walker that records VarTerm occurrences,
recurses into compound terms, and validates ConstTerm strings against
the reserved-underscore-prefix rule (`CompileMode.user` only). The
`value is String s && s.startsWith('_')` Dart smart-cast composes
into C# `value is string s && s.StartsWith('_')` (Microsoft Learn
declaration pattern).

### `DefinedGuardEvaluator` and the shared `UnifyResult` ADT
HEAVY REUSE from partial_evaluator.dart.md — the two evaluator classes
have structurally near-identical helpers; this spec delegates the
construct-by-construct decisions for every helper to the cached
construct keys in partial_evaluator.dart.md. The `UnifyResult` sealed
ADT is NOT specced in this artifact — it was lifted (per the
duplicate-class-name refactor) into the shared sibling file
`lib/compiler/unify_result.dart`, and its own convspec artifact
(unify_result.dart.md) is planned separately. Both `DefinedGuardEvaluator`
(strict, here) and the lenient `PartialEvaluator` (in
partial_evaluator.dart) reference the same shared C# type — no name
clash, no duplicate definitions. The throw-vs-return distinction
between the two evaluators is preserved by their class-level behaviour,
not by their (shared) result type.

### Escalations resolved
Both previously-recorded escalations are RESOLVED by a Dart-source
refactor (Gabi-approved): (a) the duplicate `UnifyResult` ADT — lifted
to a single shared `unify_result.dart` consumed by both files; (b) the
duplicate `PartialEvaluator` class name — the analyzer-internal strict
variant was renamed to `DefinedGuardEvaluator`, leaving
`partial_evaluator.dart`'s lenient variant as the sole `PartialEvaluator`.
The C# port mirrors the rename literally; no name clash remains. Per
the user's accompanying Option (b) decision, `compiler.dart` now imports
`partial_evaluator.dart` directly so that free `PartialEvaluator()` call
sites in the main compile pipeline resolve to the lenient version — a
documented semantic change (defined guards that don't reduce now return
failure rather than throwing `CompileError` *at that call site only*),
but it does NOT affect this file: the analyzer continues to use its own
strict `DefinedGuardEvaluator` at step 2 of the 4-step pipeline. The
conversion is therefore NOT blocked for this file (`escalations: []`,
`open_escalation_count = 0`).
