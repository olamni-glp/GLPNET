# Conversion Spec — lib/analysis/type_checker/well_typed_clause.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/well_typed_clause.dart
source_sha256: 66445ae92069c7cdf6bc5871f1666b696eabd8a80a08118cb5114b32fe6cc918
target_code_unit: lib/analysis/type_checker/well_typed_clause.cs
constructs:
  - construct_key: dart.value_class.named_required_ctor_optional_collection_with_factory_helpers
    source_form: >-
      class ClauseCheckResult { final bool isWellTyped; final Map<String,
      VariableTypeInfo> variableTypes; final List<ClauseError> errors; final
      ModedTerm? modedHead; final List<ModedTerm> modedBodyAtoms;
      ClauseCheckResult({required this.isWellTyped, required this.variableTypes,
      required this.errors, this.modedHead, this.modedBodyAtoms = const []});
      factory ClauseCheckResult.success(Map<String, VariableTypeInfo>
      variableTypes, {ModedTerm? modedHead, List<ModedTerm> modedBodyAtoms =
      const []}) => ClauseCheckResult(isWellTyped:true, variableTypes:
      variableTypes, errors:[], modedHead:modedHead, modedBodyAtoms:
      modedBodyAtoms); factory ClauseCheckResult.failure(List<ClauseError>
      errors, [Map<String, VariableTypeInfo>? variableTypes, ModedTerm?
      modedHead, List<ModedTerm>? modedBodyAtoms]) =>
      ClauseCheckResult(isWellTyped:false, variableTypes:variableTypes ?? {},
      errors:errors, modedHead:modedHead, modedBodyAtoms:modedBodyAtoms ?? []); }
    target_decision: >-
      Emit `public sealed class ClauseCheckResult` with five read-only
      auto-properties `IsWellTyped` (bool), `VariableTypes`
      (`IReadOnlyDictionary<string, VariableTypeInfo>`), `Errors`
      (`IReadOnlyList<ClauseError>`), `ModedHead` (`ModedTerm?`),
      `ModedBodyAtoms` (`IReadOnlyList<ModedTerm>`); single primary ctor
      taking all five, called at sites with C# named-argument syntax to
      mirror Dart's `{required …}` shape per the cached
      rf-dart-named-required-params-to-csharp-named-positional finding
      (moded_term.dart / well_typed_term.dart). The two Dart `factory`
      constructors `success`/`failure` become two `public static
      ClauseCheckResult Success(IReadOnlyDictionary<string, VariableTypeInfo>
      variableTypes, ModedTerm? modedHead = null,
      IReadOnlyList<ModedTerm>? modedBodyAtoms = null)` and `public static
      ClauseCheckResult Failure(IReadOnlyList<ClauseError> errors,
      IReadOnlyDictionary<string, VariableTypeInfo>? variableTypes = null,
      ModedTerm? modedHead = null, IReadOnlyList<ModedTerm>? modedBodyAtoms
      = null)` static factory methods per the cached
      rf-dart-factory-ctor-const-default-to-csharp-static-factory finding
      (type_ast.dart / well_typed_term.dart). The Dart `const []` *default
      list literal* (compile-time-constant empty list) used both as a
      *parameter default* (`this.modedBodyAtoms = const []`) and as a
      factory default deserves the explicit cached
      rf-dart-const-empty-list-default-to-csharp-static-empty-array finding
      (moded_term.dart): C# parameter defaults must be compile-time
      constants and an `IReadOnlyList<T>` cannot be a constant — the
      canonical idiom is `IReadOnlyList<ModedTerm>? modedBodyAtoms = null`
      with the body coalescing `modedBodyAtoms ?? Array.Empty<ModedTerm>()`
      (Microsoft Learn `Array.Empty<T>()`: "Returns an empty array … the
      method is faster than creating an array by other means"). The
      coalescence preserves Dart's "empty list when omitted" semantics
      exactly. Equality is NOT overridden (transient return-vehicle, never
      keyed/compared — identical rationale to well_typed_term.dart's
      `WellTypedResult` construct). NOT a positional `record` — the
      `Errors`/`ModedBodyAtoms` `IReadOnlyList<T>` members would regress
      to reference equality under record synthesis (cached
      rf-dart-list-element-value-equality-to-csharp-sequenceequal finding,
      type_ast.dart), and the Dart source explicitly chooses no equality.
    idiom_id: dart-named-required-params-to-csharp-named-positional
    research_finding_id: rf-dart-named-required-params-to-csharp-named-positional
    nuance: >-
      Cached/reused finding (FR-024 — never re-research). Reference-vs-value:
      `ClauseCheckResult` is a reference type (class) in both languages, no
      struct/record-struct. Null-safety: two fields are nullable
      (`ModedHead`, the optional `VariableTypes` argument to `Failure`).
      `ModedHead` is `ModedTerm?` in both languages — exact 1:1 nullable
      mapping. The two `List<ModedTerm>?` factory parameters (Dart optional
      positional with default-null fallback to `[]`) map to C#
      `IReadOnlyList<ModedTerm>? = null` + coalesce-to-`Array.Empty<…>()`.
      The Dart `const []` default-list-literal shorthand has no direct C#
      analog because non-primitive parameter defaults must be compile-time
      constants — the cached idiom (moded_term.dart's `MockModedPath`
      defaults / well_typed_term.dart) settles on `= null` + body coalesce.
      Collection surface type — read-only public properties typed
      `IReadOnlyDictionary<>` / `IReadOnlyList<>` mirror Dart's
      `final Map<>` / `final List<>` field convention. Call sites build a
      mutable `Dictionary<>` / `List<>` and the property exposes the
      read-only interface (same pattern as well_typed_term.dart's
      `WellTypedResult` and `WellTypedTerm.CheckModedTerm`).
  - construct_key: dart.abstract_pure_contract_base_for_error_hierarchy
    source_form: >-
      abstract class ClauseError { String get message; }
    target_decision: >-
      Emit `public abstract class ClauseError` declaring `public abstract
      string Message { get; }`. Identical decision and rationale to
      well_typed_term.dart's `WellTypedError`: abstract class (not
      interface) because the error hierarchy is an open-ended ADT whose
      extension model is "open for future error kinds with possibly shared
      default formatting" rather than "structural contract for unrelated
      implementers". Microsoft Learn inheritance guidance: "Define an
      abstract class when you want to provide a common implementation that
      derived classes can use". The `message` getter becomes a `public
      abstract string Message { get; }` auto-property declaration that
      each leaf overrides. Reference type in both languages. Not `sealed`
      — five known leaves (`HeadError`, `BodyAtomError`,
      `ClauseDualityError`, `UndefinedProcedureError`,
      `ArityMismatchClauseError`); more may arrive.
    idiom_id: dart-abstract-class-extensible-base-to-csharp-abstract-class
    research_finding_id: rf-dart-abstract-class-pure-contract-to-csharp-interface
    nuance: >-
      Cached/reused finding from well_typed_term.dart / moded_term.dart
      (FR-024), applied with the SAME conclusion (abstract class, not
      interface) for the SAME reason: open-ended error-ADT extension
      model. Reference type in both languages. The `message` getter is
      the only declared member and is abstract; the rule that says
      "all-abstract → interface" is overridden by the project convention
      (well_typed_term.dart precedent) that error hierarchies are
      abstract-class-based for shared-behaviour extensibility.
  - construct_key: dart.value_class.error_subtype_with_message_getter_and_typed_inner_errors
    source_form: >-
      class HeadError extends ClauseError { final String procedureName; final
      List<WellTypedError> termErrors; HeadError(this.procedureName,
      this.termErrors); @override String get message => 'Head of
      $procedureName is not well-typed:\n  ${termErrors.map((e) =>
      e.message).join('\n  ')}'; @override String toString() => message; }  
      (similarly BodyAtomError(procedureName, atomIndex, termErrors))
    target_decision: >-
      Emit each as `public sealed class HeadError : ClauseError` / `...
      BodyAtomError : ClauseError`, each with positional ctor + read-only
      auto-properties for the fields, an `override string Message`
      (read-only property, expression-bodied) and an `override string
      ToString() => Message;`. Positional ctor mirrors Dart
      `this.field` binding per the cached
      rf-dart-positional-ctor-this-bindings-to-csharp-ctor finding
      (well_typed_term.dart). The Dart
      `termErrors.map((e) => e.message).join('\n  ')` is the canonical
      "stringify-each-then-join" idiom: maps to C#
      `string.Join("\n  ", TermErrors.Select(e => e.Message))` per the
      cached rf-dart-iterable-map-join-to-csharp-linq-select-string-join
      finding (recorded fresh here — first time this exact construct in
      the codebase, though Select+Join pairs are standard LINQ). Microsoft
      Learn `string.Join`: "Concatenates the elements of a specified
      array or the members of a collection, using the specified separator
      between each element". Microsoft Learn `Enumerable.Select`:
      "Projects each element of a sequence into a new form." The C#
      interpolated string preserves Dart's `\n  ` literal verbatim
      (newline + two spaces, matching Dart's `\n  ` escape). `IsWellTyped`
      / nullable / value-vs-reference identical to well_typed_term.dart's
      error leaves.
    idiom_id: dart-positional-ctor-with-this-bindings-to-csharp-ctor-with-property-init
    research_finding_id: rf-dart-iterable-map-join-to-csharp-linq-select-string-join
    nuance: >-
      Dart `Iterable.map(...).join(...)` is the universal Dart idiom for
      "stringify-each-then-concatenate"; C# `string.Join(separator,
      collection.Select(projection))` is the documented 1:1 idiom
      (Microsoft Learn). The fresh finding records that this specific
      pairing is the canonical mapping; the `string.Join` overload that
      takes `IEnumerable<T>` calls `.ToString()` on each element by
      default — but here the projection explicitly calls `.Message`
      (matching Dart `(e) => e.message`), so the `string.Join(string,
      IEnumerable<string>)` overload is the right one. Reference-vs-value:
      `termErrors` is a reference-typed list aliased into the
      `HeadError`/`BodyAtomError` field — no defensive copy in Dart
      source, no defensive copy in C# emission (matching exactly). The
      interpolation hole `{string.Join("\n  ", TermErrors.Select(e =>
      e.Message))}` is multi-expression; parentheses are NOT required
      around `string.Join(...)` (it's a method call, unambiguous), only
      around inline ternaries.
  - construct_key: dart.value_class.error_subtype_with_optional_reason_inline_ternary_message
    source_form: >-
      class ClauseDualityError extends ClauseError { final String baseName;
      final VariableTypeInfo? writerType; final VariableTypeInfo? readerType;
      final String writerLocation; final String readerLocation; final String?
      reason; ClauseDualityError(this.baseName, this.writerType,
      this.readerType, this.writerLocation, this.readerLocation,
      [this.reason]); @override String get message { final reasonStr =
      reason != null ? ': $reason' : ''; return 'Variable pair ($baseName,
      $baseName?) not dual across clause$reasonStr: writer at
      $writerLocation=$writerType, reader at $readerLocation=$readerType'; }
      @override String toString() => message; }
    target_decision: >-
      Emit `public sealed class ClauseDualityError : ClauseError` with
      positional ctor `public ClauseDualityError(string baseName,
      VariableTypeInfo? writerType, VariableTypeInfo? readerType,
      string writerLocation, string readerLocation, string? reason = null)`.
      Two nullable `VariableTypeInfo?` parameters/properties — Dart
      `VariableTypeInfo?` → C# `VariableTypeInfo?` exact 1:1 under
      nullable-reference-types (cached
      rf-dart-nullable-fields-to-csharp-nullable-reference-types finding,
      moded_term.dart). The Dart optional-positional `[this.reason]`
      (default-null) maps to a default-valued positional parameter
      `string? reason = null` — exact semantic match. The `Message` getter
      is a multi-statement getter in Dart with an intermediate local
      `reasonStr`; emit as a body-bodied property getter `public override
      string Message { get { var reasonStr = Reason != null ? $": {Reason}"
      : ""; return $"Variable pair ({BaseName}, {BaseName}?) not dual across
      clause{reasonStr}: writer at {WriterLocation}={WriterType}, reader
      at {ReaderLocation}={ReaderType}"; } }`. Inline interpolation of
      nullable `VariableTypeInfo?` (`{WriterType}`) — under C# nullable
      flow analysis the interpolation hole invokes `Object.ToString()`
      on a possibly-null reference, which is the documented behaviour
      (Microsoft Learn: "If the object is null, the empty string is used
      in its place") — matches Dart's `$writerType` interpolation which
      also stringifies-or-empty (`Object.toString()` on null in Dart
      yields "null" — DIFFERENCE noted below in nuance). `ToString() =>
      Message` becomes expression-bodied override.
    idiom_id: dart-positional-ctor-with-this-bindings-to-csharp-ctor-with-property-init
    research_finding_id: rf-csharp-interpolation-null-vs-dart-null-tostring
    nuance: >-
      Fresh nuance recorded: Dart `'$x'` where `x` is null yields the
      string "null" (per dart.dev: "If x is null, then x.toString() is
      called, which returns the string 'null'"). C# `$"{x}"` where `x` is
      null yields the EMPTY string (Microsoft Learn `String.Format`:
      "If the object is null, the empty string is used in its place").
      For `ClauseDualityError.Message`, the writer/reader-type interpolations
      can in principle be null (the spec preserves the nullable contract
      on the fields), but the `_checkClauseDuality` construction site only
      builds a `ClauseDualityError` when BOTH writer and reader are
      present, so in practice the divergence is unobserved. The spec
      MANDATES that codegen emit a literal `"null"` (matching Dart's
      observable semantics) by using `$"{WriterType?.ToString() ?? "null"}"`
      / `$"{ReaderType?.ToString() ?? "null"}"` in the two ambiguous holes;
      this preserves diagnostic output character-for-character even if
      the construction-site invariant ever weakens. The other
      interpolations (`{BaseName}`, `{WriterLocation}`, `{ReaderLocation}`)
      are non-nullable so behave identically in both languages. The
      `?:`-inline-ternary nuance (well_typed_term.dart's
      `dart-boolean-conditional-branch-on-property-chain` construct) does
      not apply here because the ternary lives in a *statement* assignment
      (`var reasonStr = ...`), not inside an interpolation hole — so no
      parenthesisation is required. Reference-vs-value: ClauseDualityError
      is a reference type (class) in both.
  - construct_key: dart.value_class.error_subtype_with_string_int_formatted_message
    source_form: >-
      class UndefinedProcedureError extends ClauseError { final String
      procedureName; final int arity; UndefinedProcedureError(this.procedureName,
      this.arity); @override String get message => 'Undefined procedure:
      $procedureName/$arity'; @override String toString() => message; }  
      (similarly ArityMismatchClauseError(procedureName, expectedArity,
      actualArity))
    target_decision: >-
      Emit each as `public sealed class UndefinedProcedureError :
      ClauseError` / `... ArityMismatchClauseError : ClauseError`, each
      with positional ctor + read-only auto-properties + expression-bodied
      override `public override string Message => $"Undefined procedure:
      {ProcedureName}/{Arity}";` / `... => $"Arity mismatch for
      {ProcedureName}: expected {ExpectedArity}, got {ActualArity}";`, and
      `public override string ToString() => Message;`. C# integer
      interpolation `{Arity}` invokes `Int32.ToString()` which uses the
      *current culture* by default (Microsoft Learn `String.Format`:
      "default culture-specific formatting"); Dart `'$arity'` uses
      `Object.toString()` which for `int` is culture-invariant. For
      arity values 0..9 (the universal range) the two are output-identical,
      but the spec mandates explicit `{Arity.ToString(CultureInfo.
      InvariantCulture)}` to guarantee character-for-character match
      across locales (cached rf-csharp-int-tostring-invariant-culture
      finding, type_ast.dart's similar arity formatting). Alternative —
      use composite-format string `string.Format(CultureInfo.
      InvariantCulture, "Undefined procedure: {0}/{1}", ProcedureName,
      Arity)` — rejected as less readable than interpolation; the
      `.ToString(CultureInfo.InvariantCulture)` per-hole approach is the
      idiomatic compromise.
    idiom_id: dart-positional-ctor-with-this-bindings-to-csharp-ctor-with-property-init
    research_finding_id: rf-csharp-int-interp-culture-invariant
    nuance: >-
      Fresh nuance recorded: Dart `int.toString()` is culture-invariant
      (always uses ASCII digits 0-9 and ASCII '-'). C# `int.ToString()` —
      called implicitly by `$"{x}"` — uses `CultureInfo.CurrentCulture`,
      which for most locales produces the same characters but in
      Arabic/Bengali/Persian locales can produce non-ASCII digit glyphs.
      The spec mandates `{Arity.ToString(CultureInfo.InvariantCulture)}`
      to preserve Dart's invariant-culture output exactly. Two
      alternatives explored: (1) wrap the whole interpolated string in
      `string.Format(CultureInfo.InvariantCulture, ...)` — verbose, hides
      the structure; (2) configure the project to a fixed culture — too
      broad (affects all interpolations). The per-hole
      `.ToString(CultureInfo.InvariantCulture)` is local, explicit, and
      maintainable. Reference-vs-value: reference type (class) in both;
      `int` is value type in both (system primitives — no boxing in either).
  - construct_key: dart.exception_class_implements_exception_marker
    source_form: >-
      class UndeclaredProcedureError implements Exception { final String functor;
      final int arity; UndeclaredProcedureError(this.functor, this.arity);
      @override String toString() => 'UndeclaredProcedureError: $functor/$arity'; }
    target_decision: >-
      Emit `public sealed class UndeclaredProcedureError : Exception`
      (extends, not implements — C# `Exception` is a concrete class, not a
      marker interface). The Dart `implements Exception` idiom (where Dart's
      `Exception` IS a marker interface — see dart.dev `dart:core` Exception
      documentation: "A marker interface implemented by all core library
      exceptions") has no direct C# analog. The cached
      rf-dart-implements-exception-to-csharp-extends-system-exception
      finding (recorded fresh here — first time this exact construct in
      the codebase) settles on: `UndeclaredProcedureError(string functor,
      int arity) : base($"UndeclaredProcedureError: {functor}/{arity.
      ToString(CultureInfo.InvariantCulture)}") { Functor = functor;
      Arity = arity; }` — pass the formatted message to the
      `Exception(string message)` base ctor (Microsoft Learn:
      "Initializes a new instance of the Exception class with a specified
      error message that describes the error"). This makes the standard
      `.Message` property carry the same content the Dart `toString()`
      returns. The Dart `@override String toString() => '…';` is preserved
      by overriding `Exception.ToString()` (which by default emits "type:
      message + stack") — for character-for-character parity with Dart,
      override: `public override string ToString() => $"UndeclaredProcedure
      Error: {Functor}/{Arity.ToString(CultureInfo.InvariantCulture)}";`
      so the C# string lacks the type-name prefix that base ToString would
      add. Note this DIVERGES from .NET convention (where ToString
      usually includes stack trace) but matches Dart semantics verbatim;
      the spec accepts the divergence because the diagnostic string is
      user-visible in tests. NOT `sealed` removed — `sealed` IS applied
      (subclassing not expected; the .NET guidance is "seal exception
      types you don't expect to be derived").
    idiom_id: dart-implements-exception-to-csharp-extends-system-exception
    research_finding_id: rf-dart-implements-exception-to-csharp-extends-system-exception
    nuance: >-
      Fresh nuance recorded — load-bearing because the Dart and C#
      exception models are structurally different. Dart's `Exception` is
      a marker interface; any class implementing it can be thrown.
      C#'s `Exception` is a concrete class with `Message`, `InnerException`,
      `StackTrace`, `Source`, `HelpLink`, etc. — derived classes inherit
      the surface. The spec preserves Dart's "tiny pure-data exception"
      shape by extending `Exception` directly (skipping the
      `ApplicationException` intermediate layer, which Microsoft Learn
      `https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-
      practices-for-exceptions` notes is "not necessary" for user-defined
      exceptions: "use Exception as the base class"). The two fields
      `Functor`, `Arity` are read-only auto-properties — exception
      payload, callers query them from the catch arm. ToString override
      drops the type-name prefix to match Dart's bare-string output —
      this is recorded explicitly because it diverges from .NET convention
      (.NET's default `Exception.ToString()` produces "TypeName: Message
      ----> stacktrace"); the spec MANDATES the override. Reference type
      in both languages. The cached rf-csharp-int-interp-culture-invariant
      finding (preceding construct) applies again to the arity
      interpolation.
  - construct_key: dart.simple_data_class_with_default_collection_fields_and_derived_getters
    source_form: >-
      class TypedClause { final ast.Goal head; final List<ast.Goal> bodyAtoms;
      final List<ast.Goal> guardAtoms; TypedClause({required this.head,
      this.bodyAtoms = const [], this.guardAtoms = const []}); String get
      headFunctor => head.functor; int get headArity => head.arity; }
    target_decision: >-
      Emit `public sealed class TypedClause` with three read-only
      auto-properties `Head` (`Goal`), `BodyAtoms` (`IReadOnlyList<Goal>`),
      `GuardAtoms` (`IReadOnlyList<Goal>`) plus two computed get-only
      properties `public string HeadFunctor => Head.Functor;` and
      `public int HeadArity => Head.Arity;` (expression-bodied — the
      direct 1:1 mapping for Dart's getter shorthand, Microsoft Learn
      expression-bodied members). The primary ctor takes
      `(Goal head, IReadOnlyList<Goal>? bodyAtoms = null,
      IReadOnlyList<Goal>? guardAtoms = null)` with body coalescing each
      collection to `Array.Empty<Goal>()` (same cached `const []` default
      idiom as the leading `ClauseCheckResult` construct —
      rf-dart-const-empty-list-default-to-csharp-static-empty-array,
      moded_term.dart). Call sites use C# named-argument syntax. The
      `ast.Goal` Dart type-prefixed reference (Dart `import ... as ast`)
      transliterates to the C# fully-qualified name `Glp.Compiler.Ast.Goal`
      or a using-alias `using Goal = Glp.Compiler.Ast.Goal;` at file head
      — codegen picks one consistent project-wide policy; the spec
      mandates the `using`-alias variant for cross-file consistency with
      the moded_head.dart spec which faces the same `ast.` prefix.
      Equality not overridden in Dart, not synthesised in C# (data
      carrier, never compared).
    idiom_id: dart-import-prefix-as-to-csharp-using-alias
    research_finding_id: rf-dart-import-prefix-as-to-csharp-using-alias
    nuance: >-
      Cached/reused finding from moded_head.dart (where `ast.Term` /
      `ast.Goal` prefix-references already require the same mapping).
      Dart `import '../../compiler/ast.dart' as ast;` introduces a
      file-scoped prefix; every `ast.Goal` reference is qualified. C#
      has no direct equivalent — namespaces give project-wide names but
      no file-scoped prefix. The two faithful idioms are (a)
      fully-qualified type references (`Glp.Compiler.Ast.Goal`) or (b)
      `using Goal = Glp.Compiler.Ast.Goal;` at file head. The spec
      mandates (b) — `using` alias — because it preserves the Dart
      source's *single point of name disambiguation* per file (one line
      at top, then bare references in the body). Microsoft Learn
      `using-directives` documents `using Identifier = Type;` as the
      "using alias directive". Reference-vs-value: TypedClause is a
      reference type. The `Head` property is a reference to an
      `ast.Goal` shared with whatever produced the clause — no defensive
      copy (matches Dart).
  - construct_key: dart.public_orchestrator_fn.three_phase_clause_checker
    source_form: >-
      ClauseCheckResult checkClause(TypedClause clause, ProgramDFA dfa,
      TypeEnvironment env) { final errors = <ClauseError>[]; final
      allVariableTypes = <String, VariableTypeInfo>{}; final variableLocations
      = <String, String>{}; ModedTerm? constructedModedHead; final
      constructedModedBodyAtoms = <ModedTerm>[]; final procDecl =
      env.getProcedure(clause.headFunctor, clause.headArity); if (procDecl ==
      null) return ClauseCheckResult.failure([UndefinedProcedureError(...)]);
      if (procDecl.arity != clause.headArity) return ClauseCheckResult.
      failure([ArityMismatchClauseError(...)]); final (headResult,
      modedHeadTerm) = _checkHeadWithTerm(clause, procDecl, dfa, env);
      constructedModedHead = modedHeadTerm; if (!headResult.isWellTyped)
      errors.add(HeadError(clause.headFunctor, headResult.errors)); for
      (final entry in headResult.variableTypes.entries) { allVariableTypes[
      entry.key] = entry.value; variableLocations[entry.key] = 'head'; }
      for (int i = 0; i < clause.bodyAtoms.length; i++) { final atom =
      clause.bodyAtoms[i]; final (atomResult, modedAtomTerm) =
      _checkBodyAtomWithTerm(atom, i, dfa, env, callerVarTypes:
      allVariableTypes); if (modedAtomTerm != null) constructedModedBodyAtoms.
      add(modedAtomTerm); if (!atomResult.isWellTyped) errors.add(BodyAtomError(
      atom.functor, i, atomResult.errors)); for (final entry in atomResult.
      variableTypes.entries) { ... if (allVariableTypes.containsKey(varKey)) {
      ... } else { allVariableTypes[varKey] = newInfo; variableLocations[
      varKey] = 'body atom $i'; } } } final dualityErrors =
      _checkClauseDuality(allVariableTypes, variableLocations, dfa); errors.
      addAll(dualityErrors); return ClauseCheckResult(isWellTyped:errors.
      isEmpty, ...); }
    target_decision: >-
      Emit as `public static ClauseCheckResult CheckClause(TypedClause
      clause, ProgramDFA dfa, TypeEnvironment env)` on a host static class
      `public static class WellTypedClause` (file-name-based PascalCase
      mirroring well_typed_term.dart's `WellTypedTerm` host class) per the
      cached rf-csharp-static-class-no-toplevel-members finding
      (prelude.dart / well_typed_term.dart). Local-variable mappings (all
      cached from well_typed_term.dart's CheckModedTerm construct):
      `final errors = <ClauseError>[];` → `var errors = new
      List<ClauseError>();`; `final allVariableTypes = <String,
      VariableTypeInfo>{};` → `var allVariableTypes = new
      Dictionary<string, VariableTypeInfo>(StringComparer.Ordinal);`;
      `variableLocations` → same with `string` values; `ModedTerm?
      constructedModedHead;` → `ModedTerm? constructedModedHead = null;`
      (C# requires explicit init for definite-assignment); `final
      constructedModedBodyAtoms = <ModedTerm>[];` → `var
      constructedModedBodyAtoms = new List<ModedTerm>();`. The Dart
      *record-destructuring* of a two-element tuple `(headResult,
      modedHeadTerm) = _checkHeadWithTerm(...)` is THE distinctive
      Dart-3.0 construct in this file (NOT seen in prior type_checker
      specs; flagged as fresh rf-* below). C# 7+ value tuples support the
      same: `var (headResult, modedHeadTerm) = CheckHeadWithTerm(...)`
      (Microsoft Learn `Deconstruct`: "Tuple types support deconstruction
      … Use deconstruction to assign the elements of a tuple to
      individual variables") — exact 1:1 syntactic match. The C-style
      `for (int i = 0; i < clause.bodyAtoms.length; i++)` loop maps 1:1
      (`Length` capitalised, not `Count`, because `bodyAtoms` is an
      `IReadOnlyList<Goal>` whose canonical property is `Count` —
      ACTUALLY: `IReadOnlyList<T>` exposes `Count`, NOT `Length`;
      mandate `for (int i = 0; i < clause.BodyAtoms.Count; i++)`). The
      Dart `containsKey` / `[varKey] = info` map to `ContainsKey` /
      indexer-set. The Dart `'body atom $i'` interpolation becomes
      `$"body atom {i.ToString(CultureInfo.InvariantCulture)}"` per
      cached rf-csharp-int-interp-culture-invariant. Dart `errors.isEmpty`
      → C# `errors.Count == 0` (cached, well_typed_term.dart).
    idiom_id: dart-tuple-destructuring-to-csharp-tuple-deconstruction
    research_finding_id: rf-dart-record-destructure-to-csharp-tuple-deconstruct
    nuance: >-
      Fresh finding recorded — Dart 3.0 *records* (positional tuples) with
      pattern-destructuring on the LHS of an assignment are a new
      language construct (dart.dev `https://dart.dev/language/records`:
      "Records are an anonymous, immutable, aggregate type"). C#
      ValueTuple + deconstruction is the documented 1:1 mapping
      (Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
      fundamentals/types/value-tuples`: "Tuple types are value types …
      Tuple elements are public fields. That makes them mutable value
      types"). Two subtle nuances are load-bearing: (1) Dart records are
      *immutable* (`final` fields under the hood), while C# `ValueTuple`
      elements are mutable; this difference is irrelevant for the
      destructuring use-case here (the tuple is consumed in the same
      expression and never reassigned), so codegen need not enforce
      immutability. (2) C# requires explicit naming OR positional
      deconstruction — both work; the spec uses positional `var
      (headResult, modedHeadTerm)` to mirror Dart's positional pattern
      exactly. The Dart `return ClauseCheckResult.failure([...])` early-
      returns map to `return ClauseCheckResult.Failure(new
      ClauseError[] { new UndefinedProcedureError(...) })`. Dart `<T>[x]`
      list-literal-of-one maps to C# `new T[] { x }` or `new List<T> { x }`
      — the array literal is preferred for caller-side ephemeral data
      that the receiving `IReadOnlyList<T>` parameter will treat as
      read-only (the cached rf-dart-list-literal-of-one-element finding,
      well_typed_term.dart). Reference-vs-value: dictionaries and lists
      aliased among locals — same as Dart, no defensive copies.
  - construct_key: dart.convenience_overload_dispatching_from_ast_clause_with_throw
    source_form: >-
      ClauseCheckResult checkClauseFromAst(ast.Clause clause, ProgramDFA dfa,
      TypeEnvironment env) { final head = ast.Goal(clause.head.functor,
      clause.head.args, clause.line, clause.column); final guardGoals =
      <ast.Goal>[]; if (clause.guards != null) { for (final guard in
      clause.guards!) { guardGoals.add(ast.Goal(guard.predicate, guard.args,
      guard.line, guard.column)); } } final bodyGoals = clause.body ?? [];
      final allBodyAtoms = [...guardGoals, ...bodyGoals]; final typedClause
      = TypedClause(head: head, bodyAtoms: allBodyAtoms, guardAtoms:
      guardGoals); if (!env.hasProcedure(typedClause.headFunctor,
      typedClause.headArity)) { throw UndeclaredProcedureError(typedClause.
      headFunctor, typedClause.headArity); } return checkClause(typedClause,
      dfa, env); }
    target_decision: >-
      Emit as `public static ClauseCheckResult CheckClauseFromAst(Clause
      clause, ProgramDFA dfa, TypeEnvironment env)` on the same host
      static class. The Dart *spread operator* `[...guardGoals,
      ...bodyGoals]` (Dart 2.3+ collection spreads, dart.dev
      `https://dart.dev/language/collections`: "Use the spread operator
      `...` to insert all the elements of a collection into another
      collection") becomes C# `guardGoals.Concat(bodyGoals).ToList()` (or
      `[..guardGoals, ..bodyGoals]` with C# 12 collection expressions —
      Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
      language-reference/operators/collection-expressions`: "A collection
      expression contains a sequence of elements between [ and ] brackets
      … The spread element `..` flattens the collection"). The spec
      MANDATES the C# 12 collection-expression form `[..guardGoals,
      ..bodyGoals]` typed `IReadOnlyList<Goal>` — direct 1:1 syntax
      mapping to Dart's `[...x, ...y]`. The fresh rf-dart-collection-
      spread-to-csharp-collection-expression-spread finding records
      this. Dart `clause.guards != null` followed by `clause.guards!`
      (bang-after-check) uses control-flow narrowing; C#'s nullable flow
      analysis narrows identically — emit as `if (clause.Guards != null)
      { foreach (var guard in clause.Guards) { ... } }` (no `!` needed,
      flow analysis tracks the narrowing). Dart `clause.body ?? []`
      coalesces to empty; C# `clause.Body ?? Array.Empty<Goal>()`
      preserves semantics. The Dart `throw UndeclaredProcedureError(...)`
      maps DIRECTLY to C# `throw new UndeclaredProcedureError(...)`
      (cached rf-dart-throw-bare-constructor-to-csharp-throw-new finding,
      type_ast.dart) — the only addition is the `new` keyword (Dart's
      `throw Expr` accepts any expression; the constructor invocation
      omits `new` in Dart but requires it in C#).
    idiom_id: dart-spread-operator-to-csharp-collection-expression
    research_finding_id: rf-dart-collection-spread-to-csharp-collection-expression-spread
    nuance: >-
      Fresh finding recorded — Dart's `...` collection-spread is one of
      the language's *most distinctive* literal-construction features
      (introduced Dart 2.3, ubiquitous in modern Dart code). C# 12
      (released Nov 2023, .NET 8) added *collection expressions* with a
      directly analogous `..` spread element. Three nuances are
      load-bearing: (1) Dart `[]` is `List<dynamic>` unless typed; C#
      `[..a, ..b]` infers the element type from the target. For
      `IReadOnlyList<Goal>` target, the inference is trivial. (2) The
      C# 12 collection expression `[]` may materialise as `List<T>`,
      `T[]`, or `ImmutableArray<T>` depending on target type — the
      compiler picks; the spec doesn't constrain. (3) The `clause.guards!`
      bang operator after a null-check is null-forgiveness-via-flow;
      C# does this automatically via "definite assignment / null state
      analysis", so the `!` token is dropped in emission. The `throw
      new UndeclaredProcedureError(...)` is a checked exception in
      neither language; both languages let an unhandled throw propagate
      out of `CheckClauseFromAst` — the caller catches by type, identical
      to Dart's `catch (e is UndeclaredProcedureError)`. Reference-vs-
      value: `ast.Clause`/`ast.Goal`/`Guard` are all reference types in
      both; `TypedClause` constructed locally and immediately consumed.
  - construct_key: dart.public_fn.argindex_extraction_from_clause_head_with_dispatch
    source_form: >-
      Set<String>? getAcceptedLabels(ast.Clause clause, int argIndex,
      TypeEnvironment env) { if (argIndex < 1 || argIndex > clause.head.args.
      length) { return {}; } final arg = clause.head.args[argIndex - 1];
      return getLabelsFromTerm(arg); }
    target_decision: >-
      Emit as `public static IReadOnlySet<string>? GetAcceptedLabels(Clause
      clause, int argIndex, TypeEnvironment env)` on the host static class.
      Dart `Set<String>?` nullable-set return type maps to C#
      `IReadOnlySet<string>?` (cached
      rf-dart-set-to-csharp-iset-or-hashset finding, prelude.dart /
      program_dfa.dart). Dart empty-set literal `{}` (which because the
      contextual type is `Set<String>?` parses as an empty set, NOT an
      empty map — dart.dev `https://dart.dev/language/collections#sets`:
      "If the type is a Set, the elements must conform to its type
      argument") becomes C# `new HashSet<string>(StringComparer.Ordinal)`
      typed back to `IReadOnlySet<string>` at the return — the cached
      rf-csharp-string-set-ordinal finding (subtyping.dart) mandates
      `StringComparer.Ordinal` for any string-keyed set. The 1-indexed
      bounds-check (`argIndex < 1 || argIndex > clause.head.args.length`)
      preserves verbatim. The Dart `args[argIndex - 1]` indexer becomes
      C# `args[argIndex - 1]` (same syntax). The delegation
      `return getLabelsFromTerm(arg);` becomes `return
      GetLabelsFromTerm(arg);` (capitalised, same-host-class private
      static helper).
    idiom_id: dart-nullable-set-return-to-csharp-ireadonlyset-nullable
    research_finding_id: rf-dart-set-to-csharp-iset-or-hashset
    nuance: >-
      Cached/reused finding from prelude.dart / program_dfa.dart (FR-024).
      Three load-bearing nuances: (1) Dart's parser disambiguation of
      `{}` between empty-set and empty-map based on contextual type is
      famously subtle; C# has no equivalent ambiguity because the type
      is always explicit at the construction site (`new HashSet<T>()`
      vs `new Dictionary<TK,TV>()`). (2) `Set<String>?` is nullable
      because the function returns `null` (NOT `{}`) for the "variable
      argument — accepts anything" case (see getLabelsFromTerm below);
      `{}` is reserved for "out of bounds — accepts nothing". The
      tri-state semantics (null = accept-all, {} = accept-nothing,
      non-empty = accept-subset) is preserved exactly in C# via
      nullable+empty distinction. (3) `IReadOnlySet<T>` is .NET 5+
      (Microsoft Learn `IReadOnlySet<T>` Interface); the project targets
      .NET 8 (FR-001) so it's available. Reference-vs-value: sets are
      reference types in both languages.
  - construct_key: dart.public_fn.type_dispatch_chain_with_pattern_test_on_ast_subclasses
    source_form: >-
      Set<String>? getLabelsFromTerm(ast.Term term) { if (term is ast.VarTerm
      || term is ast.UnderscoreTerm) { return null; } if (term is ast.ConstTerm)
      { return {term.value.toString()}; } if (term is ast.ListTerm) { if
      (term.isNil) { return {'[]'}; } else { return {'[|]'}; } } if (term is
      ast.StructTerm) { return {'${term.functor}/${term.arity}'}; } return {}; }
    target_decision: >-
      Emit as `public static IReadOnlySet<string>? GetLabelsFromTerm(Term
      term)` on the host static class. The Dart `if (term is X)` chain is
      the *type-test idiom*; C# `is`-pattern matching is the documented
      1:1 mapping (cached rf-dart-is-type-test-to-csharp-is-pattern
      finding, type_ast.dart's similar dispatch). The MORE IDIOMATIC C#
      construct is a `switch` expression with type patterns (Microsoft
      Learn `https://learn.microsoft.com/en-us/dotnet/csharp/language-
      reference/operators/patterns#type-pattern`): emit as `return term
      switch { VarTerm or UnderscoreTerm => null, ConstTerm c => new
      HashSet<string>(StringComparer.Ordinal) { c.Value.ToString() ?? "" },
      ListTerm l => new HashSet<string>(StringComparer.Ordinal) {
      l.IsNil ? "[]" : "[|]" }, StructTerm s => new HashSet<string>
      (StringComparer.Ordinal) { $"{s.Functor}/{s.Arity.ToString(
      CultureInfo.InvariantCulture)}" }, _ => new HashSet<string>
      (StringComparer.Ordinal) };`. The Dart `||`-disjunction of two
      type-tests collapses to a C# `or` *or-pattern*
      (`VarTerm or UnderscoreTerm`) — Microsoft Learn pattern combinators:
      "Use the or pattern combinator to match either of two patterns".
      The fresh rf-dart-type-test-chain-to-csharp-switch-expression-or-
      pattern finding records this construct. Dart `term.value.toString()`
      on a `ConstTerm.value` of unknown nullable: C# `c.Value.ToString()
      ?? ""` coalesces — the cached rf-dart-null-tostring-vs-csharp-
      null-empty finding (recorded above for `ClauseDualityError`)
      governs the difference. The Dart `{...}` set literal of one
      element maps to a HashSet collection initializer.
    idiom_id: dart-type-test-chain-to-csharp-switch-expression
    research_finding_id: rf-dart-type-test-chain-to-csharp-switch-expression-or-pattern
    nuance: >-
      Fresh finding recorded — Dart's `is`-test chain is the canonical
      AST-dispatch idiom in the compiler/ast.dart code (Microsoft Learn
      and dart.dev both document the equivalence). The C# 9 `switch`
      expression with type patterns + the C# 9 `or`-pattern combinator
      together produce a more concise and exhaustively-checkable form
      than a literal transliteration of `if/else if/else if`. Three
      nuances are load-bearing: (1) the discard pattern `_ => …` at
      the end mirrors Dart's final `return {}` (the catch-all "unknown
      term type" case); the spec retains the catch-all to preserve
      Dart's defensive default. (2) The `term.value.toString()` for a
      `ConstTerm.value` typed `Object` in Dart can in principle return
      null on some pathological subclass; C# `Object.ToString()` is
      declared `string?` in nullable-aware projects (Microsoft Learn
      `Object.ToString` Method: "Returns: A string that represents the
      current object"; documented in the override-`ToString` doc as
      "the method may return null"). So `c.Value.ToString() ?? ""`
      is the safe coalesce — preserves Dart's "stringify-or-empty"
      semantics. (3) The switch-expression form is exhaustive: if a
      new `Term` subclass is added, the compiler warns that the switch
      is non-exhaustive (Microsoft Learn `https://learn.microsoft.com/
      en-us/dotnet/csharp/fundamentals/functional/pattern-matching`),
      improving safety over the Dart if-chain which silently falls
      through to `return {}`. Reference-vs-value: all `Term` subclasses
      are reference types in both languages.
  - construct_key: dart.public_helper_fn.type_classifier_with_subclass_test_and_throw
    source_form: >-
      String getFullTypeName(TypeExpr typeExpr) { if (typeExpr is
      PrimitiveModeAlt) { return typeExpr.isInput ? '_?' : '_'; } if
      (typeExpr is TypeRef) { return typeExpr.isInput ? '${typeExpr.name}?'
      : typeExpr.name; } throw ArgumentError('Unknown type expression:
      $typeExpr'); }
    target_decision: >-
      Emit as `public static string GetFullTypeName(TypeExpr typeExpr)`
      on the host static class. Same switch-expression-with-type-patterns
      idiom as `GetLabelsFromTerm` (preceding construct): `return typeExpr
      switch { PrimitiveModeAlt p => p.IsInput ? "_?" : "_", TypeRef r =>
      r.IsInput ? $"{r.Name}?" : r.Name, _ => throw new ArgumentException(
      $"Unknown type expression: {typeExpr}") };` per the cached
      rf-dart-type-test-chain-to-csharp-switch-expression-or-pattern
      finding (preceding construct). Dart's `throw ArgumentError(msg)`
      maps to C# `throw new ArgumentException(msg)` per the cached
      rf-dart-argumenterror-to-csharp-argumentexception finding
      (recorded fresh here — first time in the codebase): Microsoft
      Learn `ArgumentException` Class: "The exception that is thrown
      when one of the arguments provided to a method is not valid".
      Dart's `ArgumentError` has the same role (dart.dev: "Error thrown
      when an argument is invalid"); the 1:1 mapping is the documented
      analog. The two ternaries (`isInput ? '_?' : '_'` etc.) map 1:1
      to C# ternaries.
    idiom_id: dart-argumenterror-to-csharp-argumentexception
    research_finding_id: rf-dart-argumenterror-to-csharp-argumentexception
    nuance: >-
      Fresh finding recorded — Dart's `ArgumentError` is the standard
      "bad-argument" sentinel, and .NET's `ArgumentException` is the
      standard analog (with subclasses `ArgumentNullException`,
      `ArgumentOutOfRangeException` for more specific cases). The spec
      mandates the plain `ArgumentException` here because the Dart
      source uses the plain `ArgumentError`, not `ArgumentError.value`
      or another subtype — the closest C# match is bare
      `ArgumentException`. Microsoft Learn best-practices for
      exceptions: "Use the predefined .NET exception types … only when
      they apply to the situation"; ArgumentException applies here.
      Reference-vs-value: all type-expression subclasses are reference
      types in both. The string-interpolation of `typeExpr` (an
      arbitrary `TypeExpr` reference) invokes `.ToString()` — the
      Dart-vs-C# nullable-tostring divergence noted in the
      `ClauseDualityError` construct does NOT apply here because
      `typeExpr` is the non-null function parameter (the surrounding
      `if`-tests exhaust all known subclasses except the throw arm).
  - construct_key: dart.private_helper_fn.try_arity_mismatch_around_moded_head_construction
    source_form: >-
      (WellTypedResult, ModedTerm?) _checkHeadWithTerm(TypedClause clause,
      ProcDecl procDecl, ProgramDFA dfa, TypeEnvironment env) { try { final
      modedHeadTerm = modedHead(clause.head, procDecl, typeEnv: env); final
      result = _checkModedTermPerArg(modedHeadTerm, procDecl, dfa); return
      (result, modedHeadTerm); } on ArityMismatchError catch (e) { return
      (WellTypedResult.failure([InconsistentPathError(ModedPath([PathStep(
      symbol: e.message, argIndex: 0, mode: Mode.produce)]), e.message)]),
      null); } }
    target_decision: >-
      Emit as `private static (WellTypedResult result, ModedTerm? term)
      CheckHeadWithTerm(TypedClause clause, ProcDecl procDecl, ProgramDFA
      dfa, TypeEnvironment env)` on the host static class. The Dart
      *record return type* `(WellTypedResult, ModedTerm?)` (Dart 3.0
      positional record) becomes C# `(WellTypedResult result, ModedTerm?
      term)` named ValueTuple per the cached
      rf-dart-record-destructure-to-csharp-tuple-deconstruct finding
      (`CheckClause` construct above). Dart `on ArityMismatchError catch
      (e)` — the *typed-exception-catch* idiom — maps to C# `catch
      (ArityMismatchError e)` per the cached
      rf-dart-on-typed-catch-to-csharp-typed-catch finding (recorded fresh
      here — first time in the codebase): Microsoft Learn `try-catch`
      statement: "Catching specific exception types is the recommended
      practice"; `catch (T e)` where `T` is the exception type is the
      direct analog of Dart's `on T catch (e)`. The Dart constructor call
      `ArityMismatchError(...)` (no `new`) becomes C# `new
      ArityMismatchError(...)` (cached rf-dart-throw-bare-constructor-
      to-csharp-throw-new). The named-arg construction `PathStep(symbol:
      e.message, argIndex: 0, mode: Mode.produce)` becomes `new PathStep
      (symbol: e.Message, argIndex: 0, mode: Mode.Produce)` (cached
      rf-dart-named-required-params-to-csharp-named-positional). The
      nested list-literal `[PathStep(...)]` and `[InconsistentPathError
      (...)]` become C# 12 collection expressions `[new PathStep(...)]`
      / `[new InconsistentPathError(...)]` per the cached
      rf-dart-collection-spread-to-csharp-collection-expression-spread
      finding. The tuple return `(result, modedHeadTerm)` becomes C#
      `(result, modedHeadTerm)` 1:1.
    idiom_id: dart-on-typed-catch-to-csharp-typed-catch
    research_finding_id: rf-dart-on-typed-catch-to-csharp-typed-catch
    nuance: >-
      Fresh finding recorded — Dart's `on T catch (e)` syntax is
      idiosyncratic (the `on T` discriminator is separate from the
      `catch` binding). C# `catch (T e)` combines both. Two nuances:
      (1) Dart `on T catch (e)` is filter-by-type-with-binding; C#
      `catch (T e)` is the identical semantics in one syntactic unit.
      (2) Dart also allows `on T` without `catch (e)` — type-filter
      without binding; C# allows `catch (T)` (no name) too. The 1:1
      mapping holds in both shapes. Reference-vs-value: ArityMismatchError
      is a reference type, thrown/caught by reference (no boxing). The
      construct uses *two* fresh findings recorded above — `(record,
      destructure)` for the return-shape and `on T catch / catch (T)`
      for the exception filter — both cached and reused for the
      symmetric `_checkBodyAtomWithTerm` and `_checkRemoteGoal`
      constructs below.
  - construct_key: dart.private_helper_fn.case_dispatch_with_typed_subtype_test_and_recursive_call
    source_form: >-
      (WellTypedResult, ModedTerm?) _checkBodyAtomWithTerm(ast.Goal atom,
      int atomIndex, ProgramDFA dfa, TypeEnvironment env, {Map<String,
      VariableTypeInfo>? callerVarTypes}) { if (atom is ast.SpawnGoal) {
      return _checkBodyAtomWithTerm(atom.innerGoal, atomIndex, dfa, env,
      callerVarTypes: callerVarTypes); } if (atom is ast.RemoteGoal) {
      return _checkRemoteGoal(atom, atomIndex, dfa, env); } if (isBuiltinGoal(
      atom.functor)) { return (WellTypedResult.success({}), null); } var
      procDecl = env.getProcedure(atom.functor, atom.arity); if (procDecl ==
      null) { return (WellTypedResult.failure([InconsistentPathError(...)]),
      null); } final paramTemplate = env.paramProcDecls[procDecl.key]; if
      (paramTemplate != null) { if (callerVarTypes != null && callerVarTypes.
      isNotEmpty) { final inferredDecl = _inferConcreteDecl(paramTemplate,
      atom, callerVarTypes, dfa, env); if (inferredDecl != null) procDecl =
      inferredDecl; else return (WellTypedResult.success({}), null); } else
      { return (WellTypedResult.success({}), null); } } try { final
      modedAtomTerm = producedTerm(atom, procDecl, typeEnv: env); final
      result = _checkModedTermPerArg(modedAtomTerm, procDecl, dfa); return
      (result, modedAtomTerm); } on ArityMismatchError catch (e) { return
      (WellTypedResult.failure([InconsistentPathError(...)]), null); } }
    target_decision: >-
      Emit as `private static (WellTypedResult result, ModedTerm? term)
      CheckBodyAtomWithTerm(Goal atom, int atomIndex, ProgramDFA dfa,
      TypeEnvironment env, IReadOnlyDictionary<string, VariableTypeInfo>?
      callerVarTypes = null)` on the host static class. The Dart
      *named-optional parameter* `{Map<String, VariableTypeInfo>?
      callerVarTypes}` maps to a C# default-valued *named-via-position*
      parameter with `= null` per the cached
      rf-dart-optional-named-param-to-csharp-default-named finding
      (recorded fresh here — Dart's `{T? x}` is "named, optional,
      default-null" whereas a leading `{required T x}` is "named,
      required, no default"; the named-optional flavour is a distinct
      construct from the `required` flavour). Microsoft Learn named
      arguments: "A named argument enables you to specify an argument
      for a parameter by matching the argument with its name rather
      than its position"; C# positional params with `= default` allow
      named-call syntax at the call site. The cascading `if (atom is
      ast.X) { ... }` chain becomes a C# `switch` expression OR a
      sequence of `is`-pattern early-returns; the spec mandates the
      explicit early-return chain for readability (mirroring Dart's
      shape exactly) — the switch-expression form would require
      hoisting the entire procedure-declaration lookup into the case
      arms, which obscures control flow. Each Dart `is`-test arm
      becomes `if (atom is SpawnGoal spawn) { return
      CheckBodyAtomWithTerm(spawn.InnerGoal, atomIndex, dfa, env,
      callerVarTypes: callerVarTypes); }` etc., using the C# *type
      pattern with capture* (Microsoft Learn: "The pattern matches when
      the expression is non-null and converts to the pattern's type;
      the resulting variable is named via the pattern's identifier").
      The local-variable reassignment `var procDecl = ...` followed by
      `procDecl = inferredDecl;` (Dart's mutable local) maps to C# `var
      procDecl = ...` (mutable inferred local — Microsoft Learn: "The
      var keyword instructs the compiler to infer the type … the
      variable is strongly typed but the type is determined by the
      compiler"). The Dart `env.paramProcDecls[procDecl.key]` map
      indexer returning `ProcDecl?` maps to `env.ParamProcDecls.
      TryGetValue(procDecl.Key, out var paramTemplate) ? paramTemplate
      : null` OR (more idiomatic) `env.ParamProcDecls.GetValueOrDefault
      (procDecl.Key)` (Microsoft Learn `CollectionExtensions.
      GetValueOrDefault<TKey,TValue>` — .NET 5+). The spec mandates
      `GetValueOrDefault` (the .NET-canonical idiom that mirrors Dart's
      indexer-returns-nullable semantics).
    idiom_id: dart-optional-named-param-to-csharp-default-named
    research_finding_id: rf-dart-optional-named-param-to-csharp-default-named
    nuance: >-
      Fresh finding recorded — distinguishes Dart's `{required T x}`
      (which has no compile-time-default and IS positional in C#) from
      `{T? x}` (which has a default-null and IS default-valued in C#).
      Two load-bearing nuances: (1) the named-only call style (Dart
      `_check(atom, i, dfa, env, callerVarTypes: x)` — name is mandatory
      at call site for the named-optional parameter) is preserved by
      using C# named-argument call syntax: `CheckBodyAtomWithTerm(atom,
      i, dfa, env, callerVarTypes: x)` — C# allows positional+named
      mix and the call site mirrors Dart's named-arg invocation exactly.
      (2) `env.paramProcDecls[k]` returns `ProcDecl?` in Dart (Map
      indexer-returns-nullable); C# `Dictionary<K,V>[k]` throws
      `KeyNotFoundException` on miss — the canonical C# nullable-style
      lookup is `GetValueOrDefault(k)` (Microsoft Learn:
      "CollectionExtensions.GetValueOrDefault<TKey,TValue>(IReadOnly
      Dictionary<TKey,TValue>, TKey) — Tries to get the value
      associated with the specified key in the dictionary"). Reference-
      vs-value: `ProcDecl` reference type aliased across callers;
      `callerVarTypes` map reference passed through unchanged. The
      *recursive* call to `CheckBodyAtomWithTerm` for `SpawnGoal` is
      tail-recursive in Dart and remains tail-positioned in C# but C#
      does NOT guarantee tail-call optimisation (Microsoft Learn:
      "The C# compiler does not perform tail call optimization");
      since the recursion depth is bounded by the (small) maximum
      nesting of `SpawnGoal@SpawnGoal@...` (in practice 1-2 levels),
      this is not a stack-overflow risk — the spec accepts the
      non-TCO emission.
  - construct_key: dart.private_helper_fn.while_loop_with_typed_cast_unfolding_nested_subclasses
    source_form: >-
      (WellTypedResult, ModedTerm?) _checkRemoteGoal(ast.RemoteGoal remote,
      int atomIndex, ProgramDFA dfa, TypeEnvironment env) { if (remote.
      isDynamic) { return (WellTypedResult.success({}), null); } final
      pathParts = <String>[]; ast.Goal innerGoal = remote; while (innerGoal
      is ast.RemoteGoal) { final rg = innerGoal as ast.RemoteGoal; if
      (rg.isDynamic) { return (WellTypedResult.success({}), null); }
      pathParts.add(rg.staticModuleName!); innerGoal = rg.goal; } final
      modulePath = pathParts.join('#'); final goalFunctor = innerGoal.functor;
      final goalArity = innerGoal.arity; final qualifiedKey = '$modulePath#$
      goalFunctor/$goalArity'; final procDecl = env.procedures[qualifiedKey];
      if (procDecl == null) { return (WellTypedResult.failure([...]), null); }
      try { final modedAtomTerm = producedTerm(innerGoal, procDecl, typeEnv:
      env); final result = _checkModedTermPerArg(modedAtomTerm, procDecl,
      dfa); return (result, modedAtomTerm); } on ArityMismatchError catch (e)
      { return (WellTypedResult.failure([...]), null); } }
    target_decision: >-
      Emit as `private static (WellTypedResult result, ModedTerm? term)
      CheckRemoteGoal(RemoteGoal remote, int atomIndex, ProgramDFA dfa,
      TypeEnvironment env)` on the host static class. Dart `while
      (innerGoal is ast.RemoteGoal)` — *type-test loop* — becomes C#
      `while (innerGoal is RemoteGoal rg) { if (rg.IsDynamic) { return
      (WellTypedResult.Success(...), null); } pathParts.Add(rg.
      StaticModuleName!); innerGoal = rg.Goal; }` — using C# 7+ *is-with-
      declaration-pattern* inline which both narrows and binds in one
      expression (Microsoft Learn: "The declaration pattern matches an
      expression against a type … When the result is true, the
      expression is converted to the pattern's type, and the
      matched-value is assigned to the declared variable"). This
      ELIMINATES the explicit `final rg = innerGoal as ast.RemoteGoal;`
      step from the Dart source (which is sugar for "narrow then bind"
      that C#'s declaration pattern does in one move). The fresh
      rf-dart-while-istest-with-cast-to-csharp-while-pattern-bind
      finding records this consolidation. The Dart bang operator
      `rg.staticModuleName!` (Dart bang asserts non-null at runtime;
      throws on null) maps to C# `rg.StaticModuleName!` (C# null-
      forgiving operator: Microsoft Learn `https://learn.microsoft.com
      /en-us/dotnet/csharp/language-reference/operators/null-forgiving`:
      "Available in nullable enable contexts, the null-forgiving
      operator (!) is a postfix unary operator … The operator has no
      effect at run time; it only affects the compiler's static flow
      analysis"). NOTE: this is a SEMANTIC DIVERGENCE — Dart's `!`
      throws `TypeError` at runtime if the value is null; C#'s `!`
      does NOT throw at runtime, the access of a null becomes
      `NullReferenceException` at the use-site instead. For this
      construct, `staticModuleName` is guaranteed non-null because of
      the surrounding `!isDynamic` check, so the runtime behaviour is
      moot; the cached rf-dart-bang-runtime-throw-vs-csharp-null-
      forgiving-static-only finding (recorded fresh here) documents
      this divergence explicitly. The Dart `'$modulePath#$goalFunctor/
      $goalArity'` interpolation becomes C# `$"{modulePath}#{goalFunctor}/
      {goalArity.ToString(CultureInfo.InvariantCulture)}"` per the
      cached culture-invariant rule.
    idiom_id: dart-while-istest-with-cast-to-csharp-while-pattern-bind
    research_finding_id: rf-dart-while-istest-with-cast-to-csharp-while-pattern-bind
    nuance: >-
      Fresh finding recorded — Dart's "while-type-test-then-cast" loop
      is a common AST-unfolding pattern. C#'s `is X x` declaration
      pattern inside a `while` condition collapses two steps into one,
      which is the more idiomatic emission. Two distinct fresh
      findings combine here: (1) the while-loop-pattern-bind itself,
      (2) the bang operator's *runtime semantics divergence* — Dart's
      `!` is a runtime check (Microsoft Learn explicitly notes C#'s `!`
      is "compile-time only, no runtime effect"). Mitigation: the spec
      mandates the bang operator's C# emission, but recommends a
      *separate* defensive nullcheck IF the construct is in a code
      path where the value could in fact be null at runtime — here
      the `if (rg.IsDynamic)` early-return guarantees non-null
      `StaticModuleName`, so the bang is safe. The Dart `Iterable.join`
      → C# `string.Join` mapping is already cached (HeadError
      construct). Reference-vs-value: `RemoteGoal` is reference type
      in both; the `innerGoal` variable is reassigned through the loop
      to walk the nested structure (same aliasing pattern as Dart).
  - construct_key: dart.private_helper_fn.per_argument_automaton_check_with_subtype_test
    source_form: >-
      WellTypedResult _checkModedTermPerArg(ModedTerm modedTerm, ProcDecl
      decl, ProgramDFA dfa) { final errors = <WellTypedError>[]; final
      variableTypes = <String, VariableTypeInfo>{}; if (modedTerm is!
      ModedCompound) { return WellTypedResult.failure([InconsistentPathError(
      ...)]); } for (int i = 0; i < decl.arity; i++) { final argType =
      decl.argTypes[i]; final argTypeName = getFullTypeName(argType);
      Automaton argAutomaton; try { argAutomaton = dfa.getAutomaton(argTypeName);
      } on StateError { errors.add(InconsistentPathError(...)); continue; }
      final argTerm = modedTerm.args[i]; final argPaths = paths(argTerm); for
      (final path in argPaths) { final result = checkPathAgainstAutomaton(
      path, argAutomaton, dfa); if (!result.isConsistent) { errors.add(
      InconsistentPathError(...)); } else if (result.variableAssignment !=
      null) { final varKey = path.leaf.symbol; if (variableTypes.containsKey(
      varKey)) { if (variableTypes[varKey]!.typeState.name != result.
      variableAssignment!.typeState.name) { errors.add(InconsistentVariableError(
      ...)); } } else { variableTypes[varKey] = result.variableAssignment!; } } }
      } final dualityErrors = _checkTermDuality(variableTypes); errors.addAll(
      dualityErrors); return WellTypedResult(isWellTyped:errors.isEmpty, ...); }
    target_decision: >-
      Emit as `private static WellTypedResult CheckModedTermPerArg(ModedTerm
      modedTerm, ProcDecl decl, ProgramDFA dfa)` on the host static class.
      Dart `is!` (*negated* type test) → C# `is not` pattern (Microsoft
      Learn negated patterns: "Use the not pattern combinator … to
      negate any pattern"): `if (modedTerm is not ModedCompound compound)
      { return WellTypedResult.Failure(...); }` — both narrows the
      `compound` local and short-circuits when wrong type. The fresh
      rf-dart-is-not-test-to-csharp-is-not-pattern finding records this.
      The `try { ... } on StateError { ... continue; }` block becomes
      C# `try { argAutomaton = dfa.GetAutomaton(argTypeName); } catch
      (InvalidOperationException) { errors.Add(new InconsistentPathError(
      ...)); continue; }` — Dart `StateError` maps to C#
      `InvalidOperationException` per the cached
      rf-dart-stateerror-to-csharp-invalidoperationexception finding
      (program_dfa.dart's `getAutomaton` raises `StateError` and is
      specced as throwing `InvalidOperationException`). The Dart `continue`
      inside the `for` loop maps 1:1 to C# `continue`. The Dart
      `variableTypes[varKey]!.typeState.name != result.variableAssignment!
      .typeState.name` — TWO bang operators chained — both become C#
      `!` (null-forgiving operator), but spec recommends restructuring
      to: `var existing = variableTypes[varKey]; var assignment = result.
      VariableAssignment!; if (existing.TypeState.Name != assignment.
      TypeState.Name) { ... }` — eliminating one bang because the
      preceding `ContainsKey` check makes the indexer access definitely
      non-null in C# flow analysis (a Dictionary value-type with
      ContainsKey-then-indexer pattern is documented Microsoft Learn
      idiom — even though the property has reference type, the value
      is guaranteed present and non-null when ContainsKey returns true
      AND the dict was populated only by non-null assignments — which
      is the case here). The second bang `result.VariableAssignment!`
      remains because flow analysis doesn't narrow across the
      `!result.IsConsistent` arm.
    idiom_id: dart-is-not-test-to-csharp-is-not-pattern
    research_finding_id: rf-dart-is-not-test-to-csharp-is-not-pattern
    nuance: >-
      Fresh finding recorded — Dart's `is!` operator (negated type-test)
      has been a language feature since Dart 1.0. C# 9+ `is not Pattern`
      is the documented analog (Microsoft Learn: "Available since
      C# 9 … the `not` pattern combinator inverts the result of any
      pattern"). The two semantic equivalents are exact: both succeed
      iff the runtime type does NOT match the static type, both produce
      a `bool` result. The C# 9+ form additionally supports binding the
      narrowed value in the negative arm (`is not ModedCompound compound`
      narrows `compound` in the *positive*, falling-through arm — which
      is the construct here). Reference-vs-value: `ModedTerm`,
      `ModedCompound` reference types; the cast is reference-identity
      preserving. The `paths(...)` call (from moded_term.dart) returns
      `List<ModedPath>` — preserved as `IReadOnlyList<ModedPath>` in
      C# return. The inner-loop `var result = CheckPathAgainstAutomaton(
      ...)` mirrors well_typed_term.dart's same construct (cached).
      The `_checkTermDuality(...)` private-helper invocation is cached
      from well_typed_term.dart's `_checkDuality` construct.
  - construct_key: dart.private_helper_fn.same_as_well_typed_term_check_duality
    source_form: >-
      List<NonDualError> _checkTermDuality(Map<String, VariableTypeInfo>
      variableTypes) { /* same logic as well_typed_term.dart's _checkDuality
      (group by base name, check writer-reader pair duality) */ }
    target_decision: >-
      Identical to well_typed_term.dart's `_checkDuality` private
      static helper. Emit as `private static IReadOnlyList<NonDualError>
      CheckTermDuality(IReadOnlyDictionary<string, VariableTypeInfo>
      variableTypes)` on the host static class. The Dart `endsWith('?')` /
      `substring(0, length - 1)` / `putIfAbsent` constructs are all
      cached from well_typed_term.dart's same-named helper (which
      records them exhaustively). The spec mandates literal-identical
      emission to well_typed_term.dart's `CheckDuality` (with the
      different return type name — `NonDualError` is from
      well_typed_term.dart, not redeclared here).
    idiom_id: dart-toplevel-function-to-csharp-static-method
    research_finding_id: rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add
    nuance: >-
      Cached/reused finding from well_typed_term.dart (FR-024). This
      construct is literally a duplicate of well_typed_term.dart's
      `_checkDuality` — the spec records the duplication for fidelity
      to the source (the source DOES contain duplicated logic across
      the two files) and the codegen MUST emit two distinct static
      methods (NOT factor into a shared helper — that would be a
      *refactor*, beyond the FR-023 spec-only-no-improvements scope).
      Reference-vs-value, ordinal-key, putIfAbsent-via-TryGetValue,
      substring-off-by-one — all identical to well_typed_term.dart's
      same construct.
  - construct_key: dart.private_helper_fn.string_classifier_two_branch
    source_form: >-
      String _normalizeLocation(String location) { if (location == 'head')
      return 'head'; if (location.startsWith('body')) return 'body'; return
      location; }
    target_decision: >-
      Emit as `private static string NormalizeLocation(string location) =>
      location == "head" ? "head" : location.StartsWith("body",
      StringComparison.Ordinal) ? "body" : location;` — expression-bodied
      with nested ternaries. Microsoft Learn `string.StartsWith` overload
      with `StringComparison.Ordinal` is mandated per the cached
      rf-csharp-string-equality-ordinal-by-default finding
      (program_dfa.dart). The Dart string equality `location == 'head'`
      maps to C# `location == "head"` — C# string `==` IS ordinal
      (cached rf-csharp-string-equality-ordinal-by-default finding —
      Microsoft Learn `String.op_Equality`: "Determines whether two
      specified strings have the same value. This method performs an
      ordinal (case-sensitive and culture-insensitive) comparison").
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-string-equality-ordinal-by-default
    nuance: >-
      Cached/reused finding from program_dfa.dart (FR-024). Two string-
      comparison sites: (1) the `==` operator on strings — C# default is
      ordinal, matches Dart exactly. (2) `StartsWith(...)` — the default
      overload `StartsWith(string)` uses `CultureInfo.CurrentCulture` per
      Microsoft Learn (different from Dart's ordinal `startsWith`), so
      the explicit `StringComparison.Ordinal` overload is MANDATED. The
      cached finding governs this choice and the spec preserves the
      explicit-comparer discipline. Reference-vs-value: strings are
      reference types in both, immutable, value-equality semantics on
      `==` in both.
  - construct_key: dart.private_helper_fn.clause_duality_check_with_location_branching
    source_form: >-
      List<ClauseDualityError> _checkClauseDuality(Map<String, VariableTypeInfo>
      variableTypes, Map<String, String> variableLocations, ProgramDFA dfa) {
      final errors = <ClauseDualityError>[]; final baseNames = <String, Map<
      String, VariableTypeInfo>>{}; final baseLocations = <String, Map<String,
      String>>{}; /* same group-by as _checkTermDuality */ for (final entry
      in baseNames.entries) { /* ... */ if (variants.containsKey(writerKey) &&
      variants.containsKey(readerKey)) { /* ... */ final writerNormLoc =
      _normalizeLocation(writerLoc); final readerNormLoc = _normalizeLocation(
      readerLoc); if (writerNormLoc == readerNormLoc) { if (writerNormLoc ==
      'head') { final (isCompat, reason) = _areDualTypesWithReason(writerInfo,
      readerInfo); if (!isCompat) { errors.add(ClauseDualityError(...)); } }
      else { final writerOutputState = writerInfo.typeState; final
      readerDualState = readerInfo.typeState; final readerOutputState =
      dfa.getState(readerDualState.baseName); final isSub = isSubtype(
      writerOutputState, readerOutputState, dfa); if (!isSub) { errors.add(
      ClauseDualityError(...)); } } } else { final (isSame, reason) =
      _areSameTypeWithReason(writerInfo, readerInfo); if (!isSame) { errors.add(
      ClauseDualityError(...)); } } } } return errors; }
    target_decision: >-
      Emit as `private static IReadOnlyList<ClauseDualityError>
      CheckClauseDuality(IReadOnlyDictionary<string, VariableTypeInfo>
      variableTypes, IReadOnlyDictionary<string, string> variableLocations,
      ProgramDFA dfa)` on the host static class. The same group-by-base-name
      construct as `_checkTermDuality` (cached). The triple-branch
      location dispatch (`head/head` → exact-dual, `body/body` → subtyping,
      `mixed` → same-type) maps to a sequence of `if`-statements
      mirroring Dart's shape — `switch` expression would obscure the
      side-effect (mutating `errors` list). The Dart *record-destructuring*
      assignment `final (isCompat, reason) = _areDualTypesWithReason(
      writerInfo, readerInfo);` becomes C# `var (isCompat, reason) =
      AreDualTypesWithReason(writerInfo, readerInfo);` (cached
      rf-dart-record-destructure-to-csharp-tuple-deconstruct finding).
      The cross-file call `isSubtype(writerOutputState, readerOutputState,
      dfa)` becomes C# `Subtyping.IsSubtype(writerOutputState,
      readerOutputState, dfa)` — note the *static-class-qualifier*
      `Subtyping.` prefix (cached from subtyping.dart's host-class spec).
      The `dfa.getState(readerDualState.baseName)` call returns
      `DFAState`, cached from program_dfa.dart.
    idiom_id: dart-record-destructure-to-csharp-tuple-deconstruct
    research_finding_id: rf-dart-record-destructure-to-csharp-tuple-deconstruct
    nuance: >-
      Cached/reused finding (FR-024) from this file's own `_checkHeadWithTerm`
      construct (recorded fresh up-spec; this is the second use). The
      load-bearing nuance is *cross-file static-class-qualifier*: Dart's
      top-level `isSubtype` function is callable without a qualifier from
      ANY file that imports `subtyping.dart`; C# requires the host
      `Subtyping.` prefix (or a `using static Subtyping;` directive at the
      top of the file). The spec mandates the explicit
      `Subtyping.IsSubtype` qualifier for review-visibility (matching the
      explicit-qualifier discipline used in well_typed_term.dart's
      cross-file calls). Reference-vs-value: same as preceding constructs.
      The error-message interpolations preserve verbatim per the
      culture-invariant integer rule.
  - construct_key: dart.private_helper_fn.thin_predicate_delegating_to_tuple_returning_helper
    source_form: >-
      bool _areDualTypes(VariableTypeInfo writerInfo, VariableTypeInfo
      readerInfo) { final (isCompat, _) = _areDualTypesWithReason(writerInfo,
      readerInfo); return isCompat; }
    target_decision: >-
      Emit as `private static bool AreDualTypes(VariableTypeInfo writerInfo,
      VariableTypeInfo readerInfo) { var (isCompat, _) = AreDualTypesWithReason(
      writerInfo, readerInfo); return isCompat; }` — expression-bodied could
      also be `=> AreDualTypesWithReason(writerInfo, readerInfo).isCompat;`
      using tuple-element access. The spec prefers the body-bodied form
      with explicit destructuring to mirror Dart's shape exactly. The Dart
      `_` *discard pattern* (Dart 3.0: dart.dev `https://dart.dev/language/
      pattern-types#wildcard`: "A pattern named _ matches any value
      without binding it") maps to C# `_` discard pattern (Microsoft Learn
      `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/
      functional/discards`: "Discards are placeholder variables that are
      intentionally unused in application code"). Syntactically identical
      (`_`), semantically identical (no binding, no warning).
    idiom_id: dart-record-destructure-to-csharp-tuple-deconstruct
    research_finding_id: rf-dart-discard-pattern-to-csharp-discard
    nuance: >-
      Fresh finding recorded — Dart 3.0 wildcard pattern `_` in
      destructuring positions and C# `_` discard pattern are the documented
      direct analogs (both Microsoft Learn and dart.dev describe the
      construct identically: "matches any value, doesn't bind"). The
      discard pattern is *syntactic only*; both languages elide the
      assignment entirely (no temp local generated). Reference-vs-value:
      no aliasing concern (the second tuple element is discarded). The
      function is a thin facade over the rich `AreDualTypesWithReason` —
      same pattern as the public `IsSubtype` over private `CheckSubtype`
      in subtyping.dart.
  - construct_key: dart.private_helper_fn.tuple_returning_same_type_predicate
    source_form: >-
      (bool, String?) _areSameTypeWithReason(VariableTypeInfo writerInfo,
      VariableTypeInfo readerInfo) { if (writerInfo.typeState.baseName !=
      readerInfo.typeState.baseName) { return (false, '...'); } return (true,
      null); }  (similarly _areDualTypesWithReason with mode + isDual checks)
    target_decision: >-
      Emit as `private static (bool isSame, string? reason)
      AreSameTypeWithReason(VariableTypeInfo writerInfo, VariableTypeInfo
      readerInfo) { if (writerInfo.TypeState.BaseName != readerInfo.TypeState.
      BaseName) { return (false, $"{writerInfo.TypeState.Name} (base:
      {writerInfo.TypeState.BaseName}) != {readerInfo.TypeState.Name}
      (base: {readerInfo.TypeState.BaseName})"); } return (true, null); }` —
      C# named-tuple-return-type per cached
      rf-dart-record-destructure-to-csharp-tuple-deconstruct finding (named
      tuple elements `(bool isSame, string? reason)` improve call-site
      readability over positional `(bool, string?)`). The `AreDualTypesWith
      Reason` sibling has the same shape with the three-check chain (mode,
      baseName, isDual). The Dart `Mode.produce` / `Mode.consume` enum
      references map to C# `Mode.Produce` / `Mode.Consume` (cached
      enum-equality finding from well_typed_term.dart). Reference-vs-value:
      `VariableTypeInfo` reference types, the comparison fields
      (`typeState.baseName`, `mode`, `typeState.isDual`) are all value
      types or reference-typed strings — all comparable with `==`/`!=`
      under ordinal/value semantics.
    idiom_id: dart-record-destructure-to-csharp-tuple-deconstruct
    research_finding_id: rf-dart-record-destructure-to-csharp-tuple-deconstruct
    nuance: >-
      Cached/reused finding (FR-024). Two sibling helpers
      (`_areSameTypeWithReason`, `_areDualTypesWithReason`) share the
      *tuple-returning predicate with optional reason* shape; the spec
      records them under one construct_key with the second construct's
      shape implied. The *named-tuple-element-name* preservation across
      Dart and C# is a fresh nuance: Dart records elide field names
      (`(bool, String?)`) so the destructuring uses positional binding
      (`final (isSame, reason)`); C# named ValueTuple elements
      (`(bool isSame, string? reason)`) make the field names available
      at the access site too (`.isSame` / `.reason`). The spec mandates
      named C# tuple elements for downstream-readability; Dart-side
      callsite-destructuring continues to use positional. Reference-vs-
      value: tuple is a value type in C# (`ValueTuple<bool, string?>`),
      cheap to construct and return; Dart records are also values.
  - construct_key: dart.private_helper_fn.type_param_inference_with_substitution
    source_form: >-
      ProcDecl? _inferConcreteDecl(ProcDecl paramTemplate, ast.Goal atom,
      Map<String, VariableTypeInfo> callerVarTypes, ProgramDFA dfa,
      TypeEnvironment env) { final bindings = <String, String>{}; for (int i
      = 0; i < paramTemplate.arity && i < atom.args.length; i++) { final
      declaredType = paramTemplate.argTypes[i]; final actualArg = atom.args[i];
      String? actualTypeName; if (actualArg is ast.VarTerm) { final varKey =
      actualArg.isReader ? '${actualArg.name}?' : actualArg.name; final info =
      callerVarTypes[varKey]; if (info != null) { actualTypeName = info.
      typeState.baseName; } } if (actualTypeName == null) continue;
      _matchTypeForInference(declaredType, actualTypeName, paramTemplate.
      typeParams, bindings); } if (bindings.isEmpty) return null; for (final
      tp in paramTemplate.typeParams) { if (!bindings.containsKey(tp)) return
      null; } final concreteArgTypes = <TypeExpr>[]; for (final argType in
      paramTemplate.argTypes) { concreteArgTypes.add(_substituteTypeParams(
      argType, bindings)); } for (final argType in concreteArgTypes) { final
      typeName = getFullTypeName(argType); if (!dfa.automata.containsKey(
      typeName)) { return null; } } return ProcDecl(paramTemplate.name,
      concreteArgTypes, paramTemplate.line, paramTemplate.column, exported:
      paramTemplate.exported, imported: paramTemplate.imported, modulePath:
      paramTemplate.modulePath); }
    target_decision: >-
      Emit as `private static ProcDecl? InferConcreteDecl(ProcDecl
      paramTemplate, Goal atom, IReadOnlyDictionary<string, VariableTypeInfo>
      callerVarTypes, ProgramDFA dfa, TypeEnvironment env)` on the host
      static class. The Dart `Map<String, String>` (param-name → concrete-
      type-name bindings) becomes C# `var bindings = new Dictionary<string,
      string>(StringComparer.Ordinal);` (cached ordinal-key idiom from
      well_typed_term.dart). The Dart `if (actualArg is ast.VarTerm)` →
      C# `if (actualArg is VarTerm v)` (declaration pattern, cached from
      preceding constructs). The Dart `actualArg.isReader ? '${actualArg.
      name}?' : actualArg.name` ternary inside string-interpolation maps
      to C# `v.IsReader ? $"{v.Name}?" : v.Name` — note the ternary is at
      *statement level* (not inside an interpolation hole), so no
      parenthesisation is required. The Dart `bindings.isEmpty` → C#
      `bindings.Count == 0` (cached). The `bindings.containsKey(tp)` →
      `bindings.ContainsKey(tp)` (cached). The Dart positional constructor
      with *trailing named arguments* `ProcDecl(paramTemplate.name,
      concreteArgTypes, paramTemplate.line, paramTemplate.column,
      exported: paramTemplate.exported, imported: paramTemplate.imported,
      modulePath: paramTemplate.modulePath)` is a Dart mixed-positional-
      and-named call. C# supports the same mixed style: `new ProcDecl(
      paramTemplate.Name, concreteArgTypes, paramTemplate.Line,
      paramTemplate.Column, exported: paramTemplate.Exported, imported:
      paramTemplate.Imported, modulePath: paramTemplate.ModulePath)` —
      direct 1:1 syntactic mapping (Microsoft Learn named arguments:
      "Named arguments must appear after positional arguments"; mixed
      positional-then-named is the standard C# convention). The
      `dfa.automata.containsKey(typeName)` access (a public Map property)
      maps to C# `dfa.Automata.ContainsKey(typeName)` (cached from
      program_dfa.dart, which exposes `Automata` as a public read-only
      property).
    idiom_id: dart-positional-ctor-with-this-bindings-to-csharp-ctor-with-property-init
    research_finding_id: rf-dart-mixed-positional-and-named-ctor-call-to-csharp-mixed-args
    nuance: >-
      Fresh finding recorded — Dart's mixed-positional-and-named
      *constructor call syntax* (positional args first, then `name:
      value` after) maps to the identical C# mixed-arg syntax. Microsoft
      Learn documents both arrangements as supported; the C# compiler
      enforces "positional before named" identically to Dart. The
      construct here uses 4 positional + 3 named, identical shape in
      both. Three additional load-bearing nuances: (1) the *type-binding-
      lookup* dict is a transient local; aliasing to the returned
      `ProcDecl.argTypes` (Dart `final List<TypeExpr>`) is via the
      passed-in collection; the `_substituteTypeParams` helper returns
      a NEW `TypeExpr` per substitution (no in-place mutation — cached
      from type_ast.dart's TypeExpr being a value-like immutable tree).
      (2) Null-safety: `actualTypeName` is `String?` (initial null,
      assigned conditionally) — C# `string? actualTypeName = null;`
      preserves the same definite-assignment-with-null shape. (3) The
      *early-return-on-empty-bindings* and *early-return-on-missing-
      type-in-dfa* discipline is preserved verbatim — both languages
      handle early returns from `for` loops identically.
  - construct_key: dart.private_helper_fn.type_expr_pattern_match_for_inference
    source_form: >-
      void _matchTypeForInference(TypeExpr declaredType, String actualTypeName,
      List<String> typeParams, Map<String, String> bindings) { if (declaredType
      is TypeRef) { if (declaredType.typeArgs.isEmpty && typeParams.contains(
      declaredType.name)) { bindings.putIfAbsent(declaredType.name, () =>
      actualTypeName); return; } if (declaredType.typeArgs.isNotEmpty) { final
      ltIdx = actualTypeName.indexOf('<'); if (ltIdx < 0) return; final
      actualTemplate = actualTypeName.substring(0, ltIdx); if (actualTemplate
      != declaredType.name) return; final argsStr = actualTypeName.substring(
      ltIdx + 1, actualTypeName.length - 1); final actualArgs = _splitTypeArgs(
      argsStr); if (actualArgs.length != declaredType.typeArgs.length) return;
      for (int j = 0; j < actualArgs.length; j++) { final declArg = declaredType.
      typeArgs[j]; if (declArg is TypeRef && declArg.typeArgs.isEmpty &&
      typeParams.contains(declArg.name)) { bindings.putIfAbsent(declArg.name,
      () => actualArgs[j]); } } } } }
    target_decision: >-
      Emit as `private static void MatchTypeForInference(TypeExpr
      declaredType, string actualTypeName, IReadOnlyList<string> typeParams,
      IDictionary<string, string> bindings)` on the host static class. NOTE
      the `IDictionary<string, string>` (NOT `IReadOnlyDictionary`) for
      `bindings` — the method MUTATES the dict via `putIfAbsent` (writes
      a new entry on each unbound param). Dart `Map.putIfAbsent(key, ()
      => value)` → C# `if (!bindings.ContainsKey(name)) bindings[name] =
      value;` (the cached rf-dart-map-putifabsent-to-csharp-trygetvalue-
      or-add finding from well_typed_term.dart, BUT in the simpler shape
      because the value is already computed — no lazy factory needed).
      The Dart `String.indexOf('<')` returns `int` (or -1 if not found);
      C# `string.IndexOf('<')` is the documented direct analog (Microsoft
      Learn `String.IndexOf(Char)`: "Reports the zero-based index of the
      first occurrence of the specified Unicode character … If the
      character is not found, the method returns -1"). The Dart
      `actualTypeName.substring(0, ltIdx)` (end-exclusive, start at 0)
      becomes C# `actualTypeName.Substring(0, ltIdx)` — the off-by-one
      trap is AVOIDED because start=0 and the second argument coincidence
      (cached from well_typed_term.dart's `_checkDuality` construct). The
      Dart `actualTypeName.substring(ltIdx + 1, actualTypeName.length - 1)`
      (end-exclusive, start at ltIdx+1) requires the off-by-one
      adjustment: C# `actualTypeName.Substring(ltIdx + 1, actualTypeName.
      Length - ltIdx - 2)` — the length is `(actualTypeName.length - 1) -
      (ltIdx + 1) = actualTypeName.length - ltIdx - 2`. The off-by-one is
      MANDATORY; cached from well_typed_term.dart but applied to a new
      site here. The Dart `is TypeRef` test with subsequent field access
      maps to a C# declaration pattern `if (declaredType is TypeRef ref)
      { ... ref.TypeArgs ... ref.Name ... }`. The Dart `List<String>.
      contains` → C# `IReadOnlyList<string>.Contains(string)` (LINQ
      extension) using ordinal string equality.
    idiom_id: dart-map-putifabsent-to-csharp-trygetvalue-or-add
    research_finding_id: rf-dart-string-substring-end-exclusive-to-csharp-substring-length
    nuance: >-
      Cached/reused finding (FR-024). The substring off-by-one
      *non-start-at-0* site is the load-bearing fresh nuance for this
      construct: `actualTypeName.substring(ltIdx + 1, actualTypeName.
      length - 1)` (Dart) is "characters at indices ltIdx+1 through
      length-2 inclusive". C# `Substring(int startIndex, int length)`
      needs the LENGTH = (length-1) - (ltIdx+1) = length - ltIdx - 2.
      The codegen MUST emit `actualTypeName.Substring(ltIdx + 1,
      actualTypeName.Length - ltIdx - 2)` exactly — getting this wrong
      produces a string truncated by 1 character which breaks the
      template-args extraction. ALTERNATIVE: use C# range syntax
      `actualTypeName[(ltIdx + 1)..^1]` (equivalent, terser; Microsoft
      Learn ranges: "the index from end operator `^` is the index
      counted from the end of the sequence"); the spec mandates the
      explicit `Substring(start, length)` form for review-visibility and
      consistency with the rest of the file. The Dart `IReadOnlyList<
      string>.Contains` (LINQ extension on IEnumerable<T>) is O(N) — not
      O(1) like a HashSet — but typeParams is universally short (1-3
      params), so the inefficiency is negligible and matches Dart's
      same-shape `List.contains` performance. Reference-vs-value: the
      `bindings` dict is the *out-parameter* (mutated in-place);
      reference type aliased through caller chain.
  - construct_key: dart.private_helper_fn.depth_tracking_split_string_by_top_level_comma
    source_form: >-
      List<String> _splitTypeArgs(String s) { final result = <String>[]; var
      depth = 0; var start = 0; for (int i = 0; i < s.length; i++) { if (s[i]
      == '<') depth++; if (s[i] == '>') depth--; if (s[i] == ',' && depth ==
      0) { result.add(s.substring(start, i).trim()); start = i + 1; } } if
      (start < s.length) { result.add(s.substring(start).trim()); } return
      result; }
    target_decision: >-
      Emit as `private static List<string> SplitTypeArgs(string s) { var
      result = new List<string>(); var depth = 0; var start = 0; for (int i =
      0; i < s.Length; i++) { if (s[i] == '<') depth++; if (s[i] == '>')
      depth--; if (s[i] == ',' && depth == 0) { result.Add(s.Substring(start,
      i - start).Trim()); start = i + 1; } } if (start < s.Length) {
      result.Add(s.Substring(start).Trim()); } return result; }`. Two
      load-bearing differences from Dart: (1) Dart's `s.substring(start, i)`
      (end-exclusive) becomes C# `s.Substring(start, i - start)` (length-
      based) — the cached off-by-one nuance; the *non-zero start* shape
      requires the explicit subtraction. (2) Dart's `s.substring(start)`
      single-arg overload (Dart `String.substring(int start, [int? end])`:
      "If end is not provided, it defaults to length") maps to C# `s.
      Substring(start)` single-arg (Microsoft Learn `String.Substring(int
      startIndex)`: "Retrieves a substring … that begins at a specified
      character position and continues to the end of the string"). The
      single-arg overload is end-irrelevant in both; direct 1:1 match.
      Dart `String.indexOf(int idx)` returns `String` (single-char string);
      C# `string[int idx]` returns `char` (value type) — for the
      comparison `s[i] == '<'`, Dart `'<' == '<'` is string-string
      equality (ordinal); C# `'<' == '<'` is char-char equality (value
      type) — both succeed for the literal character match. Dart `String.
      trim()` → C# `string.Trim()` (Microsoft Learn `String.Trim()`:
      "Removes all leading and trailing white-space characters … using
      char.IsWhiteSpace") — both trim the same set of whitespace
      characters per the Unicode whitespace definition (no behavioural
      divergence for normal ASCII whitespace).
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-dart-string-substring-end-exclusive-to-csharp-substring-length
    nuance: >-
      Cached/reused finding (FR-024). The TWO substring sites in this
      single function are a useful illustration: (a) `s.substring(start, i)`
      (two-arg, non-zero start) — REQUIRES the C# `Substring(start, i -
      start)` adjustment; (b) `s.substring(start)` (one-arg) — DIRECT
      `Substring(start)` mapping (no length needed). Reference-vs-value:
      `string` is reference type in C#, immutable; `List<string>` is
      reference type aliased through callers. The `s[i]` indexer in C#
      returns `char` (value type) — different from Dart's `s[i]` which
      returns a single-character `String`; the comparison semantics are
      compatible for ASCII literals but the spec records the difference
      because if codegen mistakenly used `s.Substring(i, 1) == "<"` it
      would be slower and odd-looking — direct `s[i] == '<'` (char equal
      char) is the canonical C# idiom. The `Trim()` whitespace set is
      Unicode-aware in both languages — `\t \n \r ' '` are universally
      trimmed.
  - construct_key: dart.private_helper_fn.recursive_substitution_returning_new_type_expr
    source_form: >-
      TypeExpr _substituteTypeParams(TypeExpr expr, Map<String, String>
      bindings) { if (expr is TypeRef) { if (expr.typeArgs.isEmpty && bindings.
      containsKey(expr.name)) { return TypeRef(bindings[expr.name]!, expr.line,
      expr.column, isInput: expr.isInput); } if (expr.typeArgs.isNotEmpty) {
      final newArgs = expr.typeArgs.map((a) => _substituteTypeParams(a,
      bindings)).toList(); final allConcrete = newArgs.every((a) => a is
      TypeRef && a.typeArgs.isEmpty && !bindings.containsKey(a.name)); if
      (allConcrete) { final expandedName = '${expr.name}<${newArgs.map((a) =>
      (a as TypeRef).name).join(',')}>'; return TypeRef(expandedName, expr.
      line, expr.column, isInput: expr.isInput); } return TypeRef(expr.name,
      expr.line, expr.column, isInput: expr.isInput, typeArgs: newArgs); }
      return expr; } if (expr is PrimitiveModeAlt) return expr; return expr; }
    target_decision: >-
      Emit as `private static TypeExpr SubstituteTypeParams(TypeExpr expr,
      IReadOnlyDictionary<string, string> bindings)` on the host static
      class. The Dart `Iterable.map(...).toList()` chain (project-then-
      materialise) maps to C# `expr.TypeArgs.Select(a =>
      SubstituteTypeParams(a, bindings)).ToList()` per the cached
      rf-dart-iterable-map-to-csharp-linq-select finding (well_typed_term.
      dart's HeadError construct). The `Iterable.every` (Dart) → `IEnumerable
      <T>.All` (LINQ) per Microsoft Learn `Enumerable.All`: "Determines
      whether all elements of a sequence satisfy a condition". The Dart
      `newArgs.map((a) => (a as TypeRef).name).join(',')` (inner-map-and-
      join) maps to C# `string.Join(",", newArgs.Cast<TypeRef>().Select(a
      => a.Name))` — using `Cast<T>()` (Microsoft Learn `Enumerable.Cast<
      TResult>`: "Casts the elements of an IEnumerable to the specified
      type") which is the C# analog of Dart's `as TypeRef` cast inside a
      `map`. ALTERNATIVE: `string.Join(",", newArgs.Select(a => ((TypeRef)
      a).Name))` (explicit cast inside lambda) — equivalent semantically,
      slightly less idiomatic; spec mandates `Cast<TypeRef>()`. The Dart
      `bindings[expr.name]!` bang on map-indexer (after a ContainsKey
      check) maps to direct indexer access in C# (the cached
      ContainsKey-then-indexer-non-null pattern from well_typed_term.dart).
      The recursive call `SubstituteTypeParams(a, bindings)` is structurally
      identical; not tail-recursive but the recursion depth is bounded by
      the nesting of `TypeRef.typeArgs` (in practice 1-2 levels for
      `Stream<X>` / `Channel<Pair<X, Y>>` etc.), so no stack-overflow risk.
      The final fallthrough `if (expr is PrimitiveModeAlt) return expr;
      return expr;` (BOTH return `expr` unchanged — the explicit type-test
      is dead code) is preserved literally because FR-023 mandates spec-
      only-no-improvements; codegen MUST emit both arms even though one is
      redundant.
    idiom_id: dart-iterable-map-to-csharp-linq-select
    research_finding_id: rf-dart-iterable-cast-to-csharp-enumerable-cast
    nuance: >-
      Fresh finding recorded — Dart's `Iterable.map((a) => (a as T).field)`
      pattern (downcast-then-project inside a map lambda) is common in
      AST-traversal code. C#'s LINQ provides `Cast<T>()` as a dedicated
      extension method that downcasts every element of an `IEnumerable` to
      `T` — the documented idiom (Microsoft Learn). It throws
      `InvalidCastException` if any element fails the cast, identical to
      Dart's runtime cast behaviour. The spec mandates `Cast<TypeRef>()`
      because it captures intent ("project this sequence as a sequence of
      TypeRef") more clearly than `Select(a => (TypeRef) a)`. Two
      additional load-bearing nuances: (1) the `allConcrete` predicate
      using `.every` → `.All` is exact 1:1 (Microsoft Learn `Enumerable.
      All`); both return `true` for the empty case, which is the
      intended vacuous-truth semantics. (2) The Dart `final allConcrete
      = ...; if (allConcrete)` two-step (assign-then-test) maps to C#
      `var allConcrete = ...; if (allConcrete)` — identical. The
      *fall-through-with-redundant-test* `if (expr is PrimitiveModeAlt)
      return expr; return expr;` is preserved literally per FR-023 spec-
      only (no improvements; the redundancy is Dart-source-of-truth).
      Reference-vs-value: `TypeExpr` reference types, returned-fresh on
      each substitution (no in-place mutation).
conversion_units:
  - "sealed class ClauseCheckResult (IsWellTyped bool, VariableTypes IReadOnlyDictionary<string, VariableTypeInfo>, Errors IReadOnlyList<ClauseError>, ModedHead ModedTerm?, ModedBodyAtoms IReadOnlyList<ModedTerm>; ctor with all five using named-arg call style; static factory Success; static factory Failure — optional collection params default null then coalesce to Array.Empty)"
  - "abstract class ClauseError (abstract string Message getter)"
  - "sealed class HeadError extends ClauseError (ProcedureName string, TermErrors IReadOnlyList<WellTypedError>; ctor; Message override via string.Join + Select; ToString returns Message)"
  - "sealed class BodyAtomError extends ClauseError (ProcedureName string, AtomIndex int, TermErrors IReadOnlyList<WellTypedError>; ctor; Message override; ToString returns Message)"
  - "sealed class ClauseDualityError extends ClauseError (BaseName string, WriterType VariableTypeInfo?, ReaderType VariableTypeInfo?, WriterLocation string, ReaderLocation string, Reason string? default null; ctor; Message override with intermediate reasonStr local + nullable-coalesce-to-\"null\" on writer/reader interpolation; ToString returns Message)"
  - "sealed class UndefinedProcedureError extends ClauseError (ProcedureName string, Arity int; ctor; Message override with Arity.ToString(InvariantCulture); ToString returns Message)"
  - "sealed class ArityMismatchClauseError extends ClauseError (ProcedureName string, ExpectedArity int, ActualArity int; ctor; Message override with both arities in InvariantCulture; ToString returns Message)"
  - "sealed class UndeclaredProcedureError extends System.Exception (Functor string, Arity int; ctor passes formatted message to base(string) + sets properties; override ToString returns bare formatted string matching Dart exactly — no type-name prefix)"
  - "sealed class TypedClause (Head Goal, BodyAtoms IReadOnlyList<Goal>, GuardAtoms IReadOnlyList<Goal>; ctor with named-arg call style; computed HeadFunctor / HeadArity get-only properties; using-alias for Goal at file head)"
  - "static class WellTypedClause (CheckClause, CheckClauseFromAst, GetAcceptedLabels, GetLabelsFromTerm, GetFullTypeName as public static methods; private static helpers CheckHead, CheckHeadWithTerm, CheckBodyAtom, CheckBodyAtomWithTerm, CheckRemoteGoal, CheckModedTermPerArg, CheckTermDuality, NormalizeLocation, CheckClauseDuality, AreDualTypes, AreSameTypeWithReason, AreDualTypesWithReason, InferConcreteDecl, MatchTypeForInference, SplitTypeArgs, SubstituteTypeParams)"
escalations: []
```

## Rationale & Research Provenance

This file is the GLP type-checker's **well-typed-clause decision procedure**
(Definition 4.8 / 5.7 of the GLP paper). It is the largest type-checker
module — 1045 lines — and consolidates the family-level decisions made in
its ten sibling specs (`mode.dart`, `moded_term.dart`, `moded_head.dart`,
`program_dfa.dart`, `clause_validation.dart`, `prelude.dart`, `type_ast.dart`,
`subtyping.dart`, `param_expansion.dart`, `well_typed_term.dart`,
`type_conversion.dart`). Every non-trivial Dart→C# decision in this spec
is grounded against a **cached rf-* finding** from a prior file (FR-024 —
**no re-research**), with a small set of fresh findings for constructs not
seen in the prior family — explicitly recorded here so subsequent files in
the analysis tree (and any other type_checker tree visited later) can reuse
them as cache hits.

### Cached findings reused (FR-024 cache hits — no second research call)

The following research findings are reused verbatim from prior sibling
specs in this directory. Per FR-024, none of these triggers a research
sub-agent in this file — the cached idiom is applied directly:

- `rf-dart-named-required-params-to-csharp-named-positional` (from
  `moded_term.dart` / `well_typed_term.dart`) — for `ClauseCheckResult`,
  `TypedClause` ctors with `{required …}` shape.
- `rf-dart-factory-ctor-const-default-to-csharp-static-factory` (from
  `type_ast.dart` / `well_typed_term.dart`) — for `Success` / `Failure`
  factory pairs on `ClauseCheckResult`.
- `rf-dart-positional-ctor-this-bindings-to-csharp-ctor` (from
  `well_typed_term.dart`) — for all six positional-ctor error subclasses.
- `rf-dart-abstract-class-pure-contract-to-csharp-interface` (from
  `well_typed_term.dart` / `moded_term.dart`) — for the `ClauseError`
  abstract base.
- `rf-csharp-static-class-no-toplevel-members` (from `prelude.dart` /
  `subtyping.dart`) — for the `WellTypedClause` host static class.
- `rf-dart-top-level-function-to-csharp-static-method` (from
  `mode.dart` / `well_typed_term.dart`) — for every public/private
  function emission on the host static class.
- `rf-csharp-private-vs-internal-library-helpers` (from
  `program_dfa.dart` / `clause_validation.dart`) — for the
  `_check…`/`_are…`/`_infer…`/`_match…`/`_split…`/`_substitute…` /
  `_normalize…` private helpers.
- `rf-csharp-string-equality-ordinal-by-default` (from `program_dfa.dart`) —
  for every string `==` / `endsWith` / `startsWith` site.
- `rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8`
  (from `moded_term.dart`) — for every interpolated error message.
- `rf-dart-string-substring-end-exclusive-to-csharp-substring-length`
  (from `well_typed_term.dart`) — for the substring sites in
  `_matchTypeForInference` and `_splitTypeArgs`.
- `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add` (from
  `well_typed_term.dart`) — for the two `Map.putIfAbsent` sites in
  `_checkClauseDuality` (group-by-base) and `_matchTypeForInference`
  (bindings dict).
- `rf-dart-iterable-map-to-csharp-linq-select` (from `well_typed_term.dart`) —
  for the `.map(...).toList()` chain in `_substituteTypeParams`.
- `rf-dart-set-to-csharp-iset-or-hashset` (from `prelude.dart` /
  `program_dfa.dart`) — for `Set<String>?` return type of
  `getAcceptedLabels` / `getLabelsFromTerm`.
- `rf-dart-import-prefix-as-to-csharp-using-alias` (from `moded_head.dart`) —
  for `ast.Goal`, `ast.Clause`, `ast.Term`, `ast.VarTerm`, etc.
  prefix references.
- `rf-csharp-int-tostring-invariant-culture` (from `type_ast.dart`) — for
  every integer interpolation (`{arity}`, `{atomIndex}`, etc.).
- `rf-dart-stateerror-to-csharp-invalidoperationexception` (from
  `program_dfa.dart`) — for the `try { dfa.getAutomaton(...) } on StateError`
  catch arm in `_checkModedTermPerArg`.
- `rf-csharp-string-set-ordinal` (from `subtyping.dart`) — for the
  `HashSet<string>(StringComparer.Ordinal)` returned by
  `getLabelsFromTerm`.

### Fresh findings recorded in this spec (first-time constructs)

Six findings are recorded fresh in this file. Each is grounded in
authoritative documentation (Microsoft Learn for C#, dart.dev for Dart)
per FR-024 (`is_authoritative=true`):

#### rf-dart-record-destructure-to-csharp-tuple-deconstruct

**Deep analysis.** Five Dart-3.0 *record-destructuring* call sites in this
file: `_checkHeadWithTerm`, `_checkBodyAtomWithTerm`, `_checkRemoteGoal`
return `(WellTypedResult, ModedTerm?)`; `_areSameTypeWithReason`,
`_areDualTypesWithReason` return `(bool, String?)`; `_areDualTypes` and
`_checkClauseDuality` consume them via `final (a, b) = ...;`.

**Research (authoritative).** dart.dev
`https://dart.dev/language/records` — *"Records are an anonymous,
immutable, aggregate type"*. Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/value-tuples`
— *"Tuple types are value types … Tuple elements are public fields"*.
Microsoft Learn `Deconstruct` — *"Tuple types support deconstruction …
Use deconstruction to assign the elements of a tuple to individual
variables"*.

**Conclusion.** Direct 1:1 mapping. Dart positional record `(T1, T2)` →
C# `(T1, T2)` `ValueTuple<T1, T2>`. Dart pattern `final (a, b) = expr;`
→ C# `var (a, b) = expr;`. Spec mandates **named** tuple elements at the
declaration site (`(WellTypedResult result, ModedTerm? term)`) for
downstream readability, **positional** binding at the use site.

#### rf-dart-iterable-map-join-to-csharp-linq-select-string-join

**Deep analysis.** Two sites: `HeadError.message` and `BodyAtomError.message`
each call `termErrors.map((e) => e.message).join('\n  ')`.

**Research (authoritative).** Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/api/system.string.join`
— *"Concatenates the members of a collection, using the specified
separator between each member"*. Microsoft Learn `Enumerable.Select`
— *"Projects each element of a sequence into a new form"*.

**Conclusion.** Dart `coll.map(proj).join(sep)` →
C# `string.Join(sep, coll.Select(proj))`. The `string.Join(string,
IEnumerable<string>)` overload is the right one because `Select` produces
strings directly.

#### rf-csharp-interpolation-null-vs-dart-null-tostring

**Deep analysis.** `ClauseDualityError.message` interpolates two nullable
`VariableTypeInfo?` fields (`$writerType`, `$readerType`).

**Research (authoritative).** dart.dev `Object.toString` — when called on
`null`, returns `"null"`. Microsoft Learn `String.Format` /
`String.Format(IFormatProvider, String, Object[])` — *"If the object is
null, the empty string is used in its place"*.

**Conclusion.** Diagnostic-output divergence. Spec MANDATES
`$"{WriterType?.ToString() ?? "null"}"` (and same for reader) to preserve
Dart's `"null"` literal output character-for-character — even though the
construction-site invariant in `_checkClauseDuality` means both are non-null
in practice.

#### rf-csharp-int-interp-culture-invariant

**Deep analysis.** Every integer interpolation in this file
(`{arity}`, `{atomIndex}`, `{goalArity}`) must produce ASCII digits
regardless of locale.

**Research (authoritative).** Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings`
— *"By default, formatting operations use the conventions of the current
culture"*. dart.dev `int.toString()` — culture-invariant by design.

**Conclusion.** Spec mandates `{x.ToString(CultureInfo.InvariantCulture)}`
at every integer-interpolation hole. Per-hole local fix, not a whole-string
`string.Format(InvariantCulture, ...)` wrap.

#### rf-dart-implements-exception-to-csharp-extends-system-exception

**Deep analysis.** `UndeclaredProcedureError implements Exception` — Dart's
marker-interface exception idiom.

**Research (authoritative).** dart.dev `dart:core` `Exception` — *"A marker
interface implemented by all core library exceptions"*. Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/standard/exceptions/best-practices-for-exceptions`
— *"Use Exception as the base class … do not derive new exceptions from
ApplicationException"*. Microsoft Learn `Exception(string)` ctor — *"Initializes
a new instance of the Exception class with a specified error message"*.

**Conclusion.** `: Exception` (extends, not implements; C# has no
marker-interface exception model). Pass formatted message to base ctor;
override `ToString` to emit bare formatted string matching Dart's
`toString()` verbatim (omitting the `.NET`-default `TypeName:` prefix).
`sealed` applied.

#### rf-dart-collection-spread-to-csharp-collection-expression-spread

**Deep analysis.** `checkClauseFromAst` uses `[...guardGoals, ...bodyGoals]`
to concatenate two lists.

**Research (authoritative).** dart.dev `https://dart.dev/language/collections`
— *"Use the spread operator `...` to insert all the elements of a collection
into another collection"*. Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/collection-expressions`
(C# 12 / .NET 8) — *"A collection expression contains a sequence of elements
between [ and ] brackets … The spread element `..` flattens the collection"*.

**Conclusion.** Direct 1:1 syntactic mapping: Dart `[...x, ...y]` →
C# 12 `[..x, ..y]`. Target type inference selects the materialised
collection (the spec targets `IReadOnlyList<Goal>`).

### Additional fresh findings inline (one-off / low-volume constructs)

- `rf-dart-type-test-chain-to-csharp-switch-expression-or-pattern` —
  `getLabelsFromTerm` and `getFullTypeName` (C# 9 switch + or-pattern).
- `rf-dart-on-typed-catch-to-csharp-typed-catch` — every
  `on ArityMismatchError catch (e)` site.
- `rf-dart-argumenterror-to-csharp-argumentexception` — `getFullTypeName`'s
  `throw ArgumentError(...)`.
- `rf-dart-while-istest-with-cast-to-csharp-while-pattern-bind` —
  `_checkRemoteGoal`'s `while (innerGoal is ast.RemoteGoal)` loop.
- `rf-dart-bang-runtime-throw-vs-csharp-null-forgiving-static-only` —
  the bang operator semantics divergence (runtime vs. static-only).
- `rf-dart-is-not-test-to-csharp-is-not-pattern` —
  `_checkModedTermPerArg`'s `if (modedTerm is! ModedCompound)`.
- `rf-dart-optional-named-param-to-csharp-default-named` —
  `_checkBodyAtomWithTerm`'s `{Map<String, VariableTypeInfo>? callerVarTypes}`.
- `rf-dart-mixed-positional-and-named-ctor-call-to-csharp-mixed-args` —
  the `ProcDecl(...)` constructor call in `_inferConcreteDecl`.
- `rf-dart-iterable-cast-to-csharp-enumerable-cast` —
  `_substituteTypeParams`'s `(a as TypeRef).name` inside a map.
- `rf-dart-discard-pattern-to-csharp-discard` — `_areDualTypes`'s
  destructuring `final (isCompat, _) = ...`.
- `rf-dart-const-empty-list-default-to-csharp-static-empty-array` —
  the `= const []` parameter defaults on `ClauseCheckResult` /
  `TypedClause`.

### Three nuances explicitly addressed (US2 AS4 — never glossed)

1. **Value vs. reference.** Every emitted class is a reference type (no
   struct/record-struct anywhere). Collections (`IReadOnlyList<>`,
   `IReadOnlyDictionary<>`) are reference-aliased through callers exactly
   as in Dart — no defensive copies. The two error subclasses with
   nullable-collection-typed fields preserve Dart's nullable contract
   verbatim.

2. **Null-safety mapping.** `Map<>?` / `List<>?` / `VariableTypeInfo?` /
   `ModedTerm?` / `String?` are all 1:1 to `IReadOnlyDictionary<>?` /
   `IReadOnlyList<>?` / `VariableTypeInfo?` / `ModedTerm?` / `string?`
   under `<Nullable>enable</Nullable>`. Two semantic divergences are
   explicitly recorded: (a) Dart `$nullValue` produces `"null"` vs C#
   `$"{nullValue}"` produces `""` — spec mandates explicit
   `?.ToString() ?? "null"` to preserve diagnostic output. (b) Dart `!`
   throws at runtime vs C# `!` is static-only — spec recommends a
   defensive null-check if the construct-site invariant could weaken.

3. **Stream vs. IAsyncEnumerable / isolate / async.** N/A — this file is
   pure synchronous type-checking; no `Stream`, `Future`, `async`, or
   `isolate` constructs appear. The compute model is identical in both
   languages: synchronous call-and-return, mutable local collections,
   final return-vehicle. (The cached `Stream → IAsyncEnumerable` nuance
   from prior specs does NOT apply.)
