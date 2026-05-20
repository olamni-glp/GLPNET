# Conversion Spec — lib/analysis/type_checker/well_typed_term.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/well_typed_term.dart
source_sha256: 66cb54044610eb389ff23edc327067588022b814dd99a51a5e100e6515d9442f
target_code_unit: lib/analysis/type_checker/well_typed_term.cs
constructs:
  - construct_key: dart.value_class.named_required_ctor_three_field_result_type
    source_form: >-
      class WellTypedResult { final bool isWellTyped; final Map<String,
      VariableTypeInfo> variableTypes; final List<WellTypedError> errors;
      WellTypedResult({required this.isWellTyped, required this.variableTypes,
      required this.errors}); factory WellTypedResult.success(Map<String,
      VariableTypeInfo> variableTypes) => WellTypedResult(isWellTyped:true,
      variableTypes:variableTypes, errors:[]); factory WellTypedResult.failure
      (List<WellTypedError> errors, [Map<String, VariableTypeInfo>? variableTypes])
      => WellTypedResult(isWellTyped:false, variableTypes:variableTypes ?? {},
      errors:errors); }
    target_decision: >-
      Emit `public sealed class WellTypedResult` with three read-only
      auto-properties `IsWellTyped` (bool), `VariableTypes`
      (`IReadOnlyDictionary<string, VariableTypeInfo>`), `Errors`
      (`IReadOnlyList<WellTypedError>`), all set in a single `public
      WellTypedResult(bool isWellTyped, IReadOnlyDictionary<string,
      VariableTypeInfo> variableTypes, IReadOnlyList<WellTypedError> errors)`
      constructor; call sites use C# named-argument syntax (`new WellTypedResult
      (isWellTyped: true, variableTypes: ..., errors: ...)`) to mirror Dart's
      `{required …}` named-required shape per the cached
      rf-dart-named-required-params-to-csharp-named-positional finding
      (moded_term.dart). The two Dart `factory` constructors `success` /
      `failure` become two `public static WellTypedResult Success
      (IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)` and
      `public static WellTypedResult Failure(IReadOnlyList<WellTypedError>
      errors, IReadOnlyDictionary<string, VariableTypeInfo>? variableTypes =
      null)` static factory methods per the cached
      rf-dart-factory-ctor-const-default-to-csharp-static-factory finding
      (type_ast.dart / moded_term.dart). The `?? {}` Dart default for the
      optional dict becomes `?? new Dictionary<string, VariableTypeInfo>()`
      (or the static empty `ImmutableDictionary<string, VariableTypeInfo>
      .Empty` — codegen picks one consistent project-wide policy). Equality
      is NOT overridden because `WellTypedResult` is a transient
      return-vehicle never used as a dictionary key, set element, or
      compared with `==` (no operator `==` override in Dart). NOT a
      positional `record` — record value equality on the `Errors`
      `IReadOnlyList<>` member would regress to reference equality (cached
      `rf-dart-list-element-value-equality-to-csharp-sequenceequal` finding,
      type_ast.dart) and the Dart source explicitly chooses NOT to define
      equality, so synthesising it via `record` would *add* unwanted
      behaviour rather than mirror the source. The empty-list and
      empty-map literals (`[]` / `{}`) map to `Array.Empty<WellTypedError>()`
      / `ImmutableDictionary<string, VariableTypeInfo>.Empty` (or the
      mutable equivalents) — preserving Dart's "fresh empty container per
      `success` call" semantics.
    idiom_id: dart-named-required-params-to-csharp-named-positional
    research_finding_id: rf-dart-named-required-params-to-csharp-named-positional
    nuance: >-
      Cached/reused finding from moded_term.dart (FR-024 — never re-research).
      Reference-vs-value: WellTypedResult is a reference type in both
      languages (class, never struct/record-struct), and the `VariableTypes`
      map + `Errors` list are passed by reference (aliased), matching Dart's
      semantics — callers may not mutate them in practice (the failure
      factory takes them as-is) but the spec preserves the source's choice
      not to defensively copy. Null-safety: only the optional second
      parameter of `Failure` is nullable (`Map<String, VariableTypeInfo>?`
      → `IReadOnlyDictionary<string, VariableTypeInfo>?`); the field
      itself is non-nullable (`IReadOnlyDictionary<...>`) because the
      factory immediately substitutes an empty dict for null. Collection
      surface type — read-only public property typed
      `IReadOnlyDictionary<>` / `IReadOnlyList<>` discourages external
      mutation while accepting a mutable underlying instance, mirroring
      Dart's `final Map<>` / `final List<>` field convention (the field
      is `final`, the contents are mutable — call sites do `errors.add(...)`
      in `checkModedTerm`). The compute path constructs a mutable
      `Dictionary<>` / `List<>` and returns it as the read-only interface.
  - construct_key: dart.value_class.three_field_with_eq_and_hashcode
    source_form: >-
      class VariableTypeInfo { final DFAState typeState; final Mode mode;
      final bool isReader; VariableTypeInfo({required this.typeState, required
      this.mode, required this.isReader}); @override String toString() =>
      '(${typeState.name}, ${mode == Mode.consume ? "↓" : "↑"})'; @override
      bool operator ==(Object other) => other is VariableTypeInfo && typeState
      == other.typeState && mode == other.mode && isReader == other.isReader;
      @override int get hashCode => Object.hash(typeState, mode, isReader); }
    target_decision: >-
      Emit `public sealed class VariableTypeInfo : IEquatable<VariableTypeInfo>`
      with three read-only auto-properties `TypeState` (DFAState), `Mode`
      (Mode), `IsReader` (bool); single ctor with all three (named-arg call
      style at sites per rf-dart-named-required-params-to-csharp-named-
      positional). Hand-write `Equals(VariableTypeInfo?)` /
      `Equals(object?)` / `GetHashCode()` / `==` / `!=` operators
      mirroring Dart's three-field equality exactly; hash via
      `HashCode.Combine(TypeState, Mode, IsReader)`. NOT a positional
      `record` — codebase convention from program_dfa.dart's TypeState +
      type_ast.dart's TypeRef + moded_term.dart's nodes is hand-written
      `IEquatable<T>` on AST/value nodes for review-visibility, and the
      `TypeState` field has its own bespoke partial-equality semantics
      (program_dfa.dart's `dart-value-class-partial-equality-to-csharp-
      iequatable` idiom — equality on `(BaseName, IsDual)` only); record
      synthesis would delegate to `DFAState.Equals` which already mirrors
      Dart, but recording the choice explicitly preserves auditability.
      `ToString` overrides verbatim with the C# `?:` ternary mapping
      directly (Dart and C# share identical `cond ? a : b` syntax).
    idiom_id: dart-value-class-manual-eq-to-csharp-iequatable-objectequals
    research_finding_id: rf-dart-value-class-manual-eq-to-csharp-iequatable-objectequals
    nuance: >-
      Cached/reused finding from moded_term.dart (FR-024). Value-equality
      is load-bearing because `VariableTypeInfo` is compared in
      `checkModedTerm`'s `existing.typeState.name != result.variableAssignment!
      .typeState.name` (line 200) and stored in a `Map<String, VariableType
      Info>` — the map values are compared structurally only at the
      `.typeState.name` level (a string scalar), but the `==` override is
      relied upon by future callers (the override exists deliberately and
      the spec preserves it verbatim). The `Mode` field's name collides
      with the `Mode` enum type name (same nuance as TransitionLabel.Mode
      in program_dfa.dart) — disambiguated by C# member-access rules
      (`info.Mode` is the property; `Mode.Consume` is the enum value).
      Reference-vs-value: VariableTypeInfo is a reference type (class) in
      both — heap-allocated, shared by reference among map values.
      Unicode arrow glyphs `↓`/`↑` in ToString preserved verbatim per the
      cached rf-dart-string-interp-unicode-to-csharp-interpolated-string-
      utf8 finding (moded_term.dart) — UTF-8 source file mandated.
  - construct_key: dart.abstract_pure_contract_base_for_error_hierarchy
    source_form: >-
      abstract class WellTypedError { String get message; }
    target_decision: >-
      Emit `public abstract class WellTypedError` (NOT an interface)
      declaring `public abstract string Message { get; }`. The abstract
      class is chosen over `interface IWellTypedError` for a specific
      reason: the three concrete subclasses (InconsistentPathError,
      InconsistentVariableError, NonDualError) all override `ToString()`
      to return `message` — and the Dart `toString()` override is on
      System.Object via Dart's universal `Object.toString`. In C# the
      `ToString()` override lives on each concrete class anyway, but a
      *future* helper could be added to the base class to default
      `ToString() => Message` for all leaves — this is permissible on an
      abstract class (carries virtual default behaviour) but NOT on an
      interface (interface default methods exist since C# 8 but the .NET
      naming convention reserves `I`-prefix for pure contracts; an
      `abstract class` is the documented home for hierarchies that *may
      grow* shared behaviour). This is the *opposite* decision from
      `ModedTermVisitor<T>` (moded_term.dart construct
      `dart.visitor_pattern.generic_double_dispatch_abstract_interface`
      which maps to `interface IModedTermVisitor<out T>`): visitors are
      pure structural contracts, error hierarchies are open-ended ADTs
      that conventionally allow shared behaviour. Per Microsoft Learn
      inheritance guidance: "Define an abstract class when you want to
      provide a common implementation that derived classes can use". The
      `message` getter becomes a `public abstract string Message { get; }`
      auto-property declaration that each leaf overrides.
    idiom_id: dart-abstract-class-extensible-base-to-csharp-abstract-class
    research_finding_id: rf-dart-abstract-class-pure-contract-to-csharp-interface
    nuance: >-
      Cached/reused finding from moded_term.dart (FR-024), applied with
      the OPPOSITE conclusion to make the abstract-class-vs-interface
      decision explicit per construct. The rf finding documents the
      *rule* (no fields, no concrete methods, all members abstract →
      interface; otherwise → abstract class). `WellTypedError`'s
      `message` getter is the only declared member and it IS abstract,
      so by the strict rule it could be an interface — but the
      hierarchy is an *error/exception ADT* whose extension model is
      "open for future error kinds with possibly shared default
      formatting" rather than "structural contract for unrelated
      implementers". The spec explicitly chooses `abstract class` here
      and records the rationale; codegen MUST emit an abstract class.
      Reference type in both languages. Not `sealed` — open subclassing
      is the model (three leaves are direct subclasses, more may arrive).
  - construct_key: dart.value_class.error_subtype_with_message_getter_override
    source_form: >-
      class InconsistentPathError extends WellTypedError { final ModedPath
      path; final String reason; InconsistentPathError(this.path, this.reason);
      @override String get message => 'Inconsistent path: $reason\n  Path:
      $path'; @override String toString() => message; }  (similarly
      InconsistentVariableError(String, VariableTypeInfo, VariableTypeInfo);
      NonDualError(String, VariableTypeInfo?, VariableTypeInfo?, [String?]))
    target_decision: >-
      Emit each as `public sealed class InconsistentPathError : WellTypedError`
      / `... InconsistentVariableError : WellTypedError` / `... NonDualError
      : WellTypedError`, each with positional ctor + read-only
      auto-properties for the fields, an `override string Message` (read-only
      property, expression-bodied) and an `override string ToString() =>
      Message;`. The Dart constructor positional-parameter shape
      (`InconsistentPathError(this.path, this.reason)`) maps to
      `public InconsistentPathError(ModedPath path, string reason)` with
      property assignment. `NonDualError`'s optional positional parameter
      (`[this.reason]`) maps to a default-valued positional parameter
      `string? reason = null`. The two nullable fields on `NonDualError`
      (`VariableTypeInfo?` for both `writerType` and `readerType`) map to
      C# nullable references `VariableTypeInfo?` — Microsoft Learn nullable
      reference types: "the `?` suffix indicates that the variable may be
      null". The `Message` getter implementation preserves the string
      interpolation verbatim per the cached
      rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8
      finding (moded_term.dart): Dart `'... $reason\n  Path: $path'`
      becomes C# `$"Inconsistent path: {reason}\n  Path: {path}"`. The
      Dart `'\n'` escape and the two-space indent are preserved
      character-for-character. `NonDualError.Message` uses an inline
      C# ternary (`reason != null ? $": {reason}" : ""`) mirroring Dart's
      `final reasonStr = reason != null ? ': $reason' : '';` exactly.
      `path`/`firstOccurrence`/`secondOccurrence`/`writerType`/`readerType`
      interpolations invoke the implicit `.ToString()` on each, which
      maps 1:1 to the overridden `ToString` from the preceding constructs
      (ModedPath.ToString from moded_term.dart, VariableTypeInfo.ToString
      from this file).
    idiom_id: dart-positional-ctor-with-this-bindings-to-csharp-ctor-with-property-init
    research_finding_id: rf-dart-positional-ctor-this-bindings-to-csharp-ctor
    nuance: >-
      Dart's `this.field` constructor-parameter binding is sugar for
      "assign the named argument to the same-named field" — C# has no
      direct equivalent but a positional ctor body that assigns each
      parameter to its corresponding property is the canonical 1:1
      mapping (Microsoft Learn constructors doc: "The class assigns the
      values of the parameters to the data fields"). C# 12 primary
      constructors COULD compress this, but the spec prefers explicit
      ctor body for review-visibility consistency with the other AST
      node specs in this directory. `NonDualError`'s `[String? reason]`
      (Dart optional positional with nullable default `null`) maps to
      `string? reason = null` in C# — exact semantic match. Null-safety
      mapping is the load-bearing nuance for `NonDualError`: BOTH
      `writerType` and `readerType` can be null (one writer with no
      reader, or vice versa) by source intent — `_checkDuality` constructs
      `NonDualError` only when BOTH are present, but the type-level
      contract permits null on each, and the spec preserves that nullable
      contract verbatim (`VariableTypeInfo?` on both). `ToString() =>
      Message` becomes a one-line expression-bodied override (Microsoft
      Learn expression-bodied members), eliminating Dart's `String
      toString() => message;` boilerplate.
  - construct_key: dart.value_class.optional_field_factory_result_carrier
    source_form: >-
      class PathCheckResult { final bool isConsistent; final String? reason;
      final VariableTypeInfo? variableAssignment; PathCheckResult({required
      this.isConsistent, this.reason, this.variableAssignment}); factory
      PathCheckResult.consistent([VariableTypeInfo? assignment]) =>
      PathCheckResult(isConsistent:true, variableAssignment:assignment);
      factory PathCheckResult.inconsistent(String reason) => PathCheckResult
      (isConsistent:false, reason:reason); }
    target_decision: >-
      Emit `public sealed class PathCheckResult` with three read-only
      auto-properties `IsConsistent` (bool), `Reason` (`string?`),
      `VariableAssignment` (`VariableTypeInfo?`); single ctor with all
      three using named-arg call style (cached
      rf-dart-named-required-params-to-csharp-named-positional). The two
      Dart `factory` constructors map to two `public static PathCheckResult
      Consistent(VariableTypeInfo? assignment = null)` and
      `public static PathCheckResult Inconsistent(string reason)` static
      factory methods per the cached
      rf-dart-factory-ctor-const-default-to-csharp-static-factory finding
      (type_ast.dart / moded_term.dart). Equality NOT overridden in Dart
      so NOT synthesised in C# (this is an ephemeral return vehicle from
      `checkPathAgainstAutomaton`; never compared, hashed, or stored —
      consumed once in the caller's loop). NOT a record (preserves
      Dart's "no equality" choice exactly per the same rationale as
      `WellTypedResult` above). The Dart optional-positional default
      (`[VariableTypeInfo? assignment]`) on `consistent` maps to a
      default-valued positional parameter `VariableTypeInfo? assignment
      = null`; call site `PathCheckResult.consistent()` becomes
      `PathCheckResult.Consistent()` (no args).
    idiom_id: dart-factory-ctor-const-default-to-csharp-static-factory
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Cached/reused finding from type_ast.dart / moded_term.dart
      (FR-024). Two nullable reference fields (`Reason`, `VariableAssignment`)
      — Dart `String?` / `VariableTypeInfo?` map to C# `string?` /
      `VariableTypeInfo?` exactly under nullable-reference-types
      (`<Nullable>enable</Nullable>` in csproj). The `Inconsistent`
      factory's `reason` parameter is non-nullable in the C# emission
      (the Dart parameter is non-nullable `String`, becoming a non-null
      `string` field via the constructor) — matching Dart semantics.
      Reference-vs-value: reference type in both. Lifetime: ephemeral
      (created in the path-check loop, consumed in the same iteration);
      heap allocation cost matches Dart's allocation cost.
  - construct_key: dart.toplevel_function.public_orchestrator_with_local_mutable_collections
    source_form: >-
      WellTypedResult checkModedTerm(ModedTerm term, Automaton automaton,
      ProgramDFA dfa) { final errors = <WellTypedError>[]; final variableTypes
      = <String, VariableTypeInfo>{}; final termPaths = paths(term); for
      (final path in termPaths) { final result = checkPathAgainstAutomaton
      (path, automaton, dfa); if (!result.isConsistent) { errors.add(...); }
      else if (result.variableAssignment != null) { ... if (variableTypes
      .containsKey(varKey)) { ... } else { variableTypes[varKey] = ...; } } }
      final dualityErrors = _checkDuality(variableTypes); errors.addAll
      (dualityErrors); return WellTypedResult(isWellTyped: errors.isEmpty,
      variableTypes: variableTypes, errors: errors); }
    target_decision: >-
      Emit as a `public static WellTypedResult CheckModedTerm(ModedTerm
      term, Automaton automaton, ProgramDFA dfa)` static method on the
      host static class `WellTypedTerm` (or `WellTypedTermOps`) per the
      cached rf-dart-top-level-function-to-csharp-static-method finding
      (mode.dart / moded_term.dart) — host-class naming follows the
      moded_term.dart precedent (avoid collision with type names).
      `final errors = <WellTypedError>[];` becomes `var errors = new
      List<WellTypedError>();` (mutable List<T> — the local is mutated
      via `Add`). `final variableTypes = <String, VariableTypeInfo>{};`
      becomes `var variableTypes = new Dictionary<string, VariableTypeInfo>
      (StringComparer.Ordinal);` — ordinal comparer is load-bearing
      (variable keys are exact tokens like `"X"` and `"X?"`, mirroring
      the cached rf-csharp-string-equality-ordinal-by-default rationale
      from program_dfa.dart but APPLIED TO DICTIONARY KEYS where
      `StringComparer.Ordinal` is the required explicit choice — Dart
      `Map<String,V>` defaults to ordinal-equivalent key comparison).
      The Dart `for (final x in coll)` foreach becomes C# `foreach (var
      x in coll)`. `variableTypes.containsKey(varKey)` → `variableTypes
      .ContainsKey(varKey)`. `variableTypes[varKey]!` (Dart bang
      operator after a containsKey check) → C# `variableTypes[varKey]`
      with the compiler's flow analysis recognising the
      `ContainsKey`-then-indexer pattern as non-null *when the value
      type is non-nullable* (which VariableTypeInfo is) — no `!`
      forgiveness needed. The implicit Dart structural-equality test
      `existing.typeState.name != result.variableAssignment!.typeState
      .name` uses string ordinal `!=` which maps 1:1 to C# (cached
      rf-csharp-string-equality-ordinal-by-default, program_dfa.dart).
      `errors.addAll(dualityErrors)` → `errors.AddRange(dualityErrors)`
      — Microsoft Learn List<T>.AddRange: "Adds the elements of the
      specified collection to the end of the List<T>". The final
      `return WellTypedResult(isWellTyped: errors.isEmpty, ...)`
      becomes `return new WellTypedResult(isWellTyped: errors.Count
      == 0, variableTypes: variableTypes, errors: errors);` (Dart
      `isEmpty` → C# `Count == 0`, the documented 1:1 mapping for
      `List<T>` and `Dictionary<TK,TV>`).
    idiom_id: dart-top-level-function-to-csharp-static-method
    research_finding_id: rf-dart-top-level-function-to-csharp-static-method
    nuance: >-
      Cached/reused finding from mode.dart / moded_term.dart (FR-024).
      The naming-collision rule applies: host class is `WellTypedTerm`
      (or `WellTypedTermOps`) — distinct from `WellTypedResult` (the
      result data class) and distinct from `WellTypedError` (the error
      hierarchy base). Two collection-API nuances explicitly addressed:
      (1) `Dictionary<string, V>` MUST be constructed with
      `StringComparer.Ordinal` (Microsoft Learn Dictionary<TKey,TValue>
      constructor accepting IEqualityComparer<TKey>) to make the key
      semantics match Dart's `Map<String,V>` (which uses
      `String.hashCode`/`String.==`, both ordinal); the default
      `Dictionary<string,V>` ctor uses `EqualityComparer<string>.Default`
      which is also ordinal but the explicit comparer call documents
      intent. (2) Dart `List.isEmpty` getter is property; C# `List<T>`
      uses `.Count == 0` (Microsoft Learn) or the LINQ `Any()` —
      `.Count == 0` is preferred for O(1) and explicit intent. The Dart
      `variableTypes[varKey]!` bang operator (post-containsKey) maps to
      C# direct indexer access — C# raises `KeyNotFoundException` if
      missing, matching Dart's `Map<>[k]!` raising on null, so the
      runtime contract is preserved. Reference-vs-value for return:
      `WellTypedResult` is a reference type and the returned instance
      aliases the locally-built `variableTypes` map and `errors` list —
      callers may not mutate them (no API contract says so), and the
      spec preserves Dart's choice not to defensively copy.
  - construct_key: dart.toplevel_function.public_path_traversal_with_mutable_state
    source_form: >-
      PathCheckResult checkPathAgainstAutomaton(ModedPath path, Automaton
      automaton, ProgramDFA dfa) { var state = automaton.startState; var
      currentAutomaton = automaton; if (path.length == 1) { return
      _checkLeafConsistencyForPath(path.leaf, state, dfa); } for (int i = 0;
      i < path.length - 1; i++) { final step = path.steps[i]; final nextStep
      = path.steps[i + 1]; final label = _buildTransitionLabel(step, nextStep);
      final nextState = currentAutomaton.transition(state, label); if
      (nextState == null) { if (state.isWildcard) { ... } return ...; } if
      (nextState.isUserDefinedType && nextState.baseName != state.baseName)
      { try { currentAutomaton = dfa.getAutomaton(nextState.name); } catch
      (e) { return ...; } } state = nextState; } return
      _checkLeafConsistencyForPath(path.leaf, state, dfa); }
    target_decision: >-
      Emit as `public static PathCheckResult CheckPathAgainstAutomaton
      (ModedPath path, Automaton automaton, ProgramDFA dfa)` on the same
      host static class. Dart `var state = ...` (locally mutable) maps to
      C# `var state = ...` — both are inferred-type local variables
      reassignable. Dart `final step = ...` (single-assignment local)
      could map to C# `var step = ...` (no `readonly` for locals; the
      assignment-once intent is captured by review). The `for (int i = 0;
      i < path.length - 1; i++)` C-style loop maps 1:1 (identical syntax
      in C#). `currentAutomaton.transition(state, label)` returns
      `DFAState?` (nullable) — C# `nextState` is typed `DFAState?` and
      the `if (nextState == null)` test is direct; under nullable-ref
      flow analysis, after the if-return arm the compiler narrows
      `nextState` to non-null in the falling-through code (Microsoft
      Learn nullable analysis: "The compiler tracks the null-state of
      each reference"). The Dart `try { ... } catch (e) { return ...; }`
      around `dfa.getAutomaton(nextState.name)` (which throws
      `StateError` per program_dfa.dart line 192) becomes a C# `try {
      currentAutomaton = dfa.GetAutomaton(nextState.Name); } catch
      (Exception) { return PathCheckResult.Inconsistent($"Cannot get
      automaton for type {nextState.Name}"); }`. The Dart catch-all
      `catch (e)` (matches any thrown value) maps to C# `catch
      (Exception)` (or `catch (Exception e)` if the exception is used);
      `e` is unused in the Dart source, so the C# emission uses just
      `catch (Exception)`. Cached rf-dart-extension-is-as-to-csharp-
      type-pattern-switch finding (type_ast.dart) is NOT triggered here
      because no `is`-tests appear (the dispatch is on `isWildcard` /
      `isUserDefinedType` boolean properties).
    idiom_id: dart-toplevel-function-to-csharp-static-method
    research_finding_id: rf-dart-top-level-function-to-csharp-static-method
    nuance: >-
      Cached/reused finding from mode.dart (FR-024). Three exception-
      handling nuances explicit: (1) Dart `try/catch` maps to C#
      `try/catch` (identical keywords). (2) Dart `catch (e)` without a
      type binds *any* throwable; C# `catch (Exception)` binds
      `System.Exception` which is the universal base of .NET catchables
      (Microsoft Learn: "The Exception class is the base class for all
      exceptions"). The semantic match holds because `getAutomaton`
      throws `StateError` in Dart and the spec for that file
      (program_dfa.dart) maps `StateError` → `InvalidOperationException`
      (a subclass of `Exception`), so the C# catch sees it. (3) The
      catch arm returns `PathCheckResult.Inconsistent(...)` rather than
      re-throwing — preserves Dart's choice to fall back gracefully (the
      `try` wraps just `dfa.getAutomaton(...)` and only when it throws
      does the function return inconsistent). The interpolated error
      message `'Cannot get automaton for type ${nextState.name}'`
      becomes `$"Cannot get automaton for type {nextState.Name}"` per
      the cached unicode-interp finding. Null-safety on `nextState`:
      after `if (nextState == null) return ...`, C# flow analysis
      narrows it to non-null in the subsequent code, matching Dart's
      bang-operator-free access via control-flow narrowing.
  - construct_key: dart.boolean_conditional_branch_on_property_chain
    source_form: >-
      if (state.isWildcard) { final structuralModeAtWildcard = nextStep.mode;
      final expectedMode = state.isDual ? Mode.consume : Mode.produce; if
      (structuralModeAtWildcard == expectedMode) { return PathCheckResult
      .consistent(); } return PathCheckResult.inconsistent('Mode mismatch
      at wildcard ${state.name}: expected ${expectedMode == Mode.consume ?
      "↓" : "↑"}, got ${structuralModeAtWildcard == Mode.consume ? "↓" :
      "↑"}'); }
    target_decision: >-
      Transliterate verbatim. `state.isWildcard` → `state.IsWildcard`
      (the boolean classifier property from program_dfa.dart's
      `dart-boolean-classifier-getter-to-csharp-expression-property`
      idiom). Dart ternary `state.isDual ? Mode.consume : Mode.produce`
      → C# `state.IsDual ? Mode.Consume : Mode.Produce` (identical
      ternary syntax). Mode enum equality `mode == Mode.consume` → C#
      `mode == Mode.Consume` — both are value-type equality on the
      enum, identical semantics (Microsoft Learn enum equality). The
      embedded ternary inside string interpolation (`${expectedMode ==
      Mode.consume ? "↓" : "↑"}`) becomes C# `{(expectedMode ==
      Mode.Consume ? "↓" : "↑")}` — note the *required* parentheses
      around the ternary inside the interpolation hole (Microsoft Learn
      interpolated strings: "Conditional operator expressions must be
      enclosed in parentheses"). Unicode glyphs preserved verbatim
      per the cached rf-dart-string-interp-unicode-to-csharp-interpolated-
      string-utf8 finding.
    idiom_id: dart-string-interp-unicode-to-csharp-interpolated-string-utf8
    research_finding_id: rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8
    nuance: >-
      Cached/reused finding from moded_term.dart (FR-024). The
      ternary-inside-interpolation parenthesisation requirement is the
      only fresh nuance: C# parses `$"{a ? b : c}"` as ambiguous with
      the `?:` nullable-conditional shorthand inside the interpolation,
      so explicit parentheses `$"{(a ? b : c)}"` are mandatory. Dart
      does not have this ambiguity (no `?.` in string interpolation
      holes) so its `${cond ? a : b}` reads unparenthesised; the C#
      emission MUST insert parentheses. Value semantics for `Mode`
      (enum value type) — comparison is by integer-tag value, no
      boxing, identical to Dart `Mode == Mode` semantics.
  - construct_key: dart.private_helper_fn.string_split_with_int_parse_fallback
    source_form: >-
      TransitionLabel _buildTransitionLabel(PathStep currentStep, PathStep
      nextStep) { final parts = currentStep.symbol.split('/'); if (parts
      .length != 2) { return TransitionLabel.functor(currentStep.symbol, 0,
      nextStep.argIndex, mode: nextStep.mode); } final functor = parts[0];
      final arity = int.tryParse(parts[1]) ?? 0; return TransitionLabel
      .functor(functor, arity, nextStep.argIndex, mode: nextStep.mode); }
    target_decision: >-
      Emit as `private static TransitionLabel BuildTransitionLabel
      (PathStep currentStep, PathStep nextStep)` on the host static
      class — `private` visibility per the cached
      rf-csharp-private-vs-internal-library-helpers finding
      (clause_validation.dart / program_dfa.dart) since the helper is
      file-internal in Dart and co-located with its sole caller. Dart
      `String.split('/')` returns `List<String>`; C# `string.Split('/')`
      returns `string[]` (Microsoft Learn `string.Split(char)`:
      "Returns a string array that contains the substrings ... that are
      delimited by the specified character"). The `.length != 2` test
      maps to `.Length != 2` (array `Length` property). Indexer access
      `parts[0]` / `parts[1]` is identical in syntax across languages.
      `int.tryParse(parts[1]) ?? 0` (Dart returns `int?` then ??-coalesces)
      maps to C# `int.TryParse(parts[1], out var arity) ? arity : 0`
      (Microsoft Learn `int.TryParse`: "Converts the string representation
      of a number to its 32-bit signed integer equivalent. A return value
      indicates whether the operation succeeded.") — the canonical C#
      idiom for "parse-or-default". The call to `TransitionLabel.functor
      (functor, arity, nextStep.argIndex, mode: nextStep.mode)` becomes
      `TransitionLabel.Functor(functor, arity, nextStep.ArgIndex, mode:
      nextStep.Mode)` (static factory method per cached finding;
      named-argument syntax for `mode`).
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Cached/reused finding from program_dfa.dart / clause_validation.dart
      (FR-024). Two fresh nuances explicit: (1) Dart `int.tryParse(s)`
      returns `int?` which then ?? -coalesces to a fallback; C#
      `int.TryParse(s, out var v)` returns `bool` and writes to an `out`
      parameter — different shape, same semantics. The faithful C# idiom
      is the ternary `int.TryParse(s, out var v) ? v : 0`; alternative
      is `int.Parse` wrapped in try/catch (rejected because tryParse is
      precisely an exception-free probe). (2) String.Split overload —
      Dart `'a/b'.split('/')` and C# `"a/b".Split('/')` both split on
      every occurrence of the delimiter, returning all parts including
      empty trailing parts; the `.length != 2` test handles the
      mismatched-format case identically. Reference-vs-value: arrays in
      C# are reference types (heap-allocated) just as Dart Lists are;
      the local `parts` is a single reference held briefly — no
      semantic difference.
  - construct_key: dart.private_helper_fn.delegation_with_branch_on_polymorphic_field
    source_form: >-
      PathCheckResult _checkLeafConsistencyForPath(PathStep leaf, DFAState
      state, ProgramDFA dfa) { final leafTerm = _pathStepToLeafTerm(leaf);
      final result = checkLeafConsistency(leafTerm, state, dfa); if (result
      .isConsistent) { if (leaf.isVariable) { final isReader = leaf.isReader;
      final mode = isReader ? Mode.consume : Mode.produce; return
      PathCheckResult.consistent(VariableTypeInfo(typeState: result.type ??
      state, mode: mode, isReader: isReader)); } return PathCheckResult
      .consistent(); } else { return PathCheckResult.inconsistent(result
      .reason ?? 'Leaf inconsistent'); } }
    target_decision: >-
      Emit as `private static PathCheckResult CheckLeafConsistencyForPath
      (PathStep leaf, DFAState state, ProgramDFA dfa)` on the host
      static class. Dart `??` (null-coalescing) maps DIRECTLY to C# `??`
      — identical token, identical semantics (Microsoft Learn:
      "The null-coalescing operator `??` returns the value of its
      left-hand operand if it isn't null"). So `result.type ?? state` →
      `result.Type ?? state` and `result.reason ?? 'Leaf inconsistent'`
      → `result.Reason ?? "Leaf inconsistent"`. The named-arg
      construction `VariableTypeInfo(typeState: result.type ?? state,
      mode: mode, isReader: isReader)` becomes `new VariableTypeInfo
      (typeState: result.Type ?? state, mode: mode, isReader: isReader)`
      (named-arg call style per cached rf-dart-named-required-params-to-
      csharp-named-positional). `PathCheckResult.consistent(...)` /
      `.inconsistent(...)` map to the static factories `.Consistent(...)`
      / `.Inconsistent(...)` per the preceding `PathCheckResult`
      construct. The Dart `if (leaf.isVariable)` boolean property
      branch maps directly to a C# `if (leaf.IsVariable)` — no
      `is`-test conversion is needed (PathStep encodes its variable-ness
      as a bool field, not via a type tag — see moded_term.dart
      PathStep construct).
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Cached/reused finding from program_dfa.dart / clause_validation.dart
      (FR-024). `??` operator parity: Dart and C# share the identical
      syntax and semantics for null-coalescing on a nullable reference
      type — Microsoft Learn confirms `result.Reason ?? "Leaf inconsistent"`
      is the canonical 1:1 mapping. `result.Type` here is `DFAState?`
      (nullable reference from the `checkLeafConsistency` return type
      defined in program_dfa.dart); the `??` falls back to the non-null
      `state` parameter. The `isReader` local is captured into a
      `VariableTypeInfo` whose property name happens to match the
      parameter — C# named-arg call syntax (`isReader: isReader`) is
      perfectly legal and is how Dart's named-arg-with-same-name idiom
      transliterates.
  - construct_key: dart.private_helper_fn.path_step_to_leafterm_with_type_classification
    source_form: >-
      LeafTerm _pathStepToLeafTerm(PathStep step) { if (step.isVariable) {
      if (step.isReader) { return LeafTerm.reader(step.symbol, mode:
      step.mode); } else { return LeafTerm.writer(step.symbol, mode:
      step.mode); } } else { final value = step.symbol; final intVal =
      int.tryParse(value); if (intVal != null) { return LeafTerm
      .integerConstant(intVal, mode: step.mode); } final doubleVal =
      double.tryParse(value); if (doubleVal != null) { return LeafTerm
      .realConstant(doubleVal, mode: step.mode); } if ((value.startsWith
      ("'") && value.endsWith("'")) || (value.startsWith('"') &&
      value.endsWith('"'))) { return LeafTerm.stringConstant(value
      .substring(1, value.length - 1), mode: step.mode); } return
      LeafTerm.stringConstant(value, mode: step.mode); } }
    target_decision: >-
      Emit as `private static LeafTerm PathStepToLeafTerm(PathStep step)`
      on the host static class. Dart `int.tryParse(s)` → C# `int.TryParse
      (s, out var intVal) ? intVal : (int?)null` (the pattern documented
      in the preceding helper construct); spec emits the canonical
      `if (int.TryParse(value, out var intVal)) { return LeafTerm
      .IntegerConstant(intVal, mode: step.Mode); }` form — Microsoft
      Learn `int.TryParse` lists this as the recommended pattern
      because it avoids the `Nullable<int>` boxing of the
      tryParse-returns-int? approach. Likewise for `double.TryParse`
      (Microsoft Learn `double.TryParse`). The string-quote sniffing
      block (`startsWith("'") && endsWith("'")` / `startsWith('"') &&
      endsWith('"')`) maps 1:1 to C# `value.StartsWith("'",
      StringComparison.Ordinal) && value.EndsWith("'",
      StringComparison.Ordinal)` etc. — explicit ordinal comparison
      per the cached rf-csharp-string-equality-ordinal-by-default
      reasoning (program_dfa.dart): for *single-character* prefix
      tests the default culture-aware behaviour of `StartsWith(string)`
      could in theory differ on exotic locales (Microsoft Learn warns:
      "By default ... uses the current culture for comparison"), so
      `StringComparison.Ordinal` is mandated. `value.substring(1,
      value.length - 1)` (Dart end-exclusive) becomes C# `value
      .Substring(1, value.Length - 2)` — Microsoft Learn
      `String.Substring(int startIndex, int length)`: the second
      argument is LENGTH not end-index. The Dart end-exclusive index
      `value.length - 1` corresponds to a substring of length
      `(value.length - 1) - 1 = value.length - 2`. The static factory
      calls `LeafTerm.reader(...)`, `LeafTerm.writer(...)`,
      `LeafTerm.integerConstant(...)`, `LeafTerm.realConstant(...)`,
      `LeafTerm.stringConstant(...)` map to corresponding `LeafTerm
      .Reader(...)` / `.Writer(...)` / `.IntegerConstant(...)` /
      `.RealConstant(...)` / `.StringConstant(...)` static methods on
      the `LeafTerm` class (specced in program_dfa.dart's companion
      spec for that file).
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-dart-string-substring-end-exclusive-to-csharp-substring-length
    nuance: >-
      Substring semantics is the load-bearing fresh nuance (one
      already cached in mode.dart / type_ast.dart but recorded here
      because the bug-risk is severe): Dart `String.substring(int
      start, [int? end])` is END-EXCLUSIVE (Dart api
      `https://api.dart.dev/stable/dart-core/String/substring.html`),
      while C# `String.Substring(int startIndex, int length)` takes a
      LENGTH not an end-index (Microsoft Learn:
      `https://learn.microsoft.com/en-us/dotnet/api/system.string.substring`).
      Therefore Dart `value.substring(1, value.length - 1)` —
      "characters at indices 1..length-2 inclusive" — translates to
      C# `value.Substring(1, value.Length - 2)` ("start at 1, take
      length-2 characters"). The off-by-one trap is non-obvious;
      codegen MUST emit exactly this length calculation. NOT
      `value[1..^1]` (C# range syntax, equivalent and arguably
      clearer but the spec mandates the explicit Substring form for
      uniformity with the rest of the file). `int.TryParse` /
      `double.TryParse` parity with Dart `int.tryParse` /
      `double.tryParse` includes locale: Dart parses with invariant
      culture by default; C# `int.TryParse(string)` overload also
      uses `CultureInfo.CurrentCulture` by default which can differ
      — the strict mapping uses `int.TryParse(s,
      NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)`,
      which is the documented robust idiom. The spec mandates
      invariant culture parsing to match Dart semantics exactly.
  - construct_key: dart.private_helper_fn.trivial_one_liner_pure
    source_form: "String _variableKey(PathStep leaf) { return leaf.symbol; }"
    target_decision: >-
      Emit as `private static string VariableKey(PathStep leaf) =>
      leaf.Symbol;` — expression-bodied member, single-line. Trivial 1:1
      mapping; recorded for completeness per SC-006.
    idiom_id: dart-private-toplevel-helper-to-csharp-private-static-method
    research_finding_id: rf-csharp-private-vs-internal-library-helpers
    nuance: >-
      Cached/reused finding (FR-024). No reference-vs-value concern (the
      `Symbol` property of `PathStep` is a `string`, immutable in both
      languages). Trivial; recorded per SC-006 "every construct
      analysed".
  - construct_key: dart.private_helper_fn.groupby_then_pairwise_check_returning_error_list
    source_form: >-
      List<NonDualError> _checkDuality(Map<String, VariableTypeInfo>
      variableTypes) { final errors = <NonDualError>[]; final baseNames =
      <String, Map<String, VariableTypeInfo>>{}; for (final entry in
      variableTypes.entries) { final varKey = entry.key; final info =
      entry.value; final baseName = varKey.endsWith('?') ? varKey
      .substring(0, varKey.length - 1) : varKey; baseNames.putIfAbsent
      (baseName, () => {}); baseNames[baseName]![varKey] = info; } for
      (final entry in baseNames.entries) { ... if (variants.containsKey
      (writerKey) && variants.containsKey(readerKey)) { ... if (writerInfo
      .mode != Mode.produce) { errors.add(NonDualError(...)); continue; }
      ... final writerIsWildcard = writerInfo.typeState.baseName == '_';
      ... if (writerIsWildcard || readerIsWildcard) { if (writerIsWildcard
      && writerInfo.typeState.isDual) { errors.add(NonDualError(...));
      continue; } ... continue; } if (writerInfo.typeState.baseName !=
      readerInfo.typeState.baseName) { ... } if (writerInfo.typeState
      .isDual == readerInfo.typeState.isDual) { ... } } } return errors; }
    target_decision: >-
      Emit as `private static List<NonDualError> CheckDuality
      (IReadOnlyDictionary<string, VariableTypeInfo> variableTypes)` on
      the host static class. The Dart `Map<String, Map<String,
      VariableTypeInfo>>` (group-by base name, then by variant key) maps
      to C# `var baseNames = new Dictionary<string, Dictionary<string,
      VariableTypeInfo>>(StringComparer.Ordinal);` — inner dict also
      ordinal-keyed. The Dart `putIfAbsent(baseName, () => {})` idiom
      (Microsoft Learn `Map.putIfAbsent`: "Look up the value of key, or
      add a new entry if it isn't there") becomes the canonical C#
      pattern `if (!baseNames.TryGetValue(baseName, out var variants))
      { variants = new Dictionary<string, VariableTypeInfo>
      (StringComparer.Ordinal); baseNames[baseName] = variants; }`. The
      Dart `baseNames[baseName]![varKey] = info;` becomes the simpler
      `variants[varKey] = info;` (no nullable-bang dance because the
      preceding TryGetValue-then-add ensures `variants` is non-null in
      C#'s flow analysis). The Dart `varKey.endsWith('?')` →
      C# `varKey.EndsWith("?", StringComparison.Ordinal)` (explicit
      ordinal per the cached program_dfa.dart finding). The Dart
      `varKey.substring(0, varKey.length - 1)` (end-exclusive) →
      C# `varKey.Substring(0, varKey.Length - 1)` (length-based: start
      at 0, take length-1 characters — same number 1:1 because the
      Dart end index `varKey.length - 1` minus start 0 equals length
      `varKey.length - 1`); the off-by-one trap is *avoided* here
      because the start index is 0 (where substring's length argument
      coincides with Dart's end argument when start is 0). The Dart
      `continue` inside the `if/else if/else if` chain maps to C#
      `continue` (identical keyword) inside the host `foreach` loop.
      The Dart string interpolation in error messages
      `'Types must have same base: ${writerInfo.typeState.name} vs
      ${readerInfo.typeState.name}'` becomes C# interpolated strings
      per the cached unicode-interp finding.
    idiom_id: dart-toplevel-function-to-csharp-static-method
    research_finding_id: rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add
    nuance: >-
      Fresh nuance recorded: `Map.putIfAbsent(key, () => default)` is
      a Dart idiom for "lookup-or-insert" that has no single-call
      C# Dictionary equivalent. Microsoft Learn `Dictionary<TKey,
      TValue>.TryGetValue` documents the standard C# idiom
      (TryGetValue-then-Add). C# 7's `out var` declaration pattern
      makes this almost as compact as Dart. The factory closure (Dart
      `() => {}`) provides lazy default construction; C# constructs
      the empty dict only on the cache-miss arm too — semantics
      preserved. Alternative idioms rejected: `GetValueOrDefault`
      doesn't insert (just returns default); `Dictionary.TryAdd`
      doesn't return the value; `CollectionsMarshal.GetValueRefOrAddDefault`
      is .NET 6+ ref-return wizardry — overkill here. The chosen
      `TryGetValue + new + Add` idiom is the documented and idiomatic
      one. Reference-vs-value: inner dictionaries are reference types
      shared via the outer-dict values — mutation through
      `variants[varKey] = info` modifies the dict referenced from
      `baseNames[baseName]`, matching Dart's identical aliasing.
      Substring off-by-one safety: this site uses `Substring(0,
      Length - 1)` (start=0, length=L-1) which mirrors Dart
      `substring(0, length - 1)` (start=0, end=L-1) — the length and
      end-exclusive-index coincide when start is 0, so no
      adjustment is needed; contrast with the start=1 case in
      `_pathStepToLeafTerm` where the adjustment is mandatory.
conversion_units:
  - "sealed class WellTypedResult (IsWellTyped bool, VariableTypes IReadOnlyDictionary<string, VariableTypeInfo>, Errors IReadOnlyList<WellTypedError>; ctor with all three using named-arg call style; static factory Success; static factory Failure)"
  - "sealed class VariableTypeInfo implements IEquatable<VariableTypeInfo> (TypeState DFAState, Mode Mode, IsReader bool; ctor; ToString override using interpolated string with ternary; Equals(VariableTypeInfo?), Equals(object?), GetHashCode via HashCode.Combine, == and != operators)"
  - "abstract class WellTypedError (abstract string Message getter)"
  - "sealed class InconsistentPathError extends WellTypedError (Path ModedPath, Reason string; ctor; Message override via interpolation; ToString returns Message)"
  - "sealed class InconsistentVariableError extends WellTypedError (VariableName string, FirstOccurrence VariableTypeInfo, SecondOccurrence VariableTypeInfo; ctor; Message override; ToString returns Message)"
  - "sealed class NonDualError extends WellTypedError (BaseName string, WriterType VariableTypeInfo?, ReaderType VariableTypeInfo?, Reason string? default null; ctor; Message override with inline reason-ternary; ToString returns Message)"
  - "sealed class PathCheckResult (IsConsistent bool, Reason string?, VariableAssignment VariableTypeInfo?; ctor with named-arg call style; static factory Consistent(VariableTypeInfo? assignment = null); static factory Inconsistent(string reason))"
  - "static class WellTypedTerm (CheckModedTerm, CheckPathAgainstAutomaton as public static methods; private static helpers BuildTransitionLabel, CheckLeafConsistencyForPath, PathStepToLeafTerm, VariableKey, CheckDuality)"
escalations: []
```

## Rationale & Research Provenance

This file is the GLP type-checker's *well-typing decision procedure*
(Definition 5.4 / 4.5 of the GLP paper). It carries (a) four small,
mostly-data result-carrier classes (`WellTypedResult`, `VariableTypeInfo`,
`PathCheckResult`, plus the three-leaf `WellTypedError` hierarchy under an
abstract base), (b) two public top-level functions (`checkModedTerm`,
`checkPathAgainstAutomaton`), and (c) five private helper functions
(`_buildTransitionLabel`, `_checkLeafConsistencyForPath`,
`_pathStepToLeafTerm`, `_variableKey`, `_checkDuality`). Every non-trivial
Dart→C# decision is grounded against a cached rf-* finding from the four
already-specced sibling files in this directory (`mode.dart`,
`moded_term.dart`, `program_dfa.dart`, `clause_validation.dart`,
`prelude.dart`, `type_ast.dart`) per FR-024 — **no re-research**, no
re-derivation. Only one fresh rf-* finding is recorded
(`rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add` for the
groupby-with-lazy-default idiom in `_checkDuality`); the rest are explicit
reuses with verbatim citations preserved.

### rf-dart-named-required-params-to-csharp-named-positional (CACHED, reused from moded_term.dart)

**Deep analysis.** `WellTypedResult` declares all three fields with
`{required …}` named-required syntax; `PathCheckResult` and
`VariableTypeInfo` do the same; the three error subclasses use Dart
*positional* parameters with `this.field` binding.

**Research (authoritative, CACHED).** Reused from moded_term.dart finding:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/programming-
guide/classes-and-structs/named-and-optional-arguments` — *"Named arguments
enable you to specify an argument for a parameter by matching the argument
with its name"*. C# has no parameter-level `required` keyword; the
faithful idiom is positional ctor + named-argument call style. FR-024
cache hit — no second research call.

**Conclusion.** Three result classes use positional ctors with all-required
parameters (no defaults); call sites use C# named-argument syntax
(`new WellTypedResult(isWellTyped: true, variableTypes: …, errors: …)`)
to mirror Dart's `{required …}` call style verbatim. The three error
classes use direct positional ctors (no named-arg needed — Dart source
uses positional too).

### rf-dart-factory-ctor-const-default-to-csharp-static-factory (CACHED, reused from type_ast.dart / moded_term.dart)

**Deep analysis.** Two factory pairs: `WellTypedResult.success` /
`.failure`, and `PathCheckResult.consistent` / `.inconsistent`. Each is a
pure convenience constructor delegating to the primary constructor with
fixed/computed arguments (no caching, no subtype-return, no `this`
access).

**Research (authoritative, CACHED).** Reused from type_ast.dart /
moded_term.dart: dart.dev `https://dart.dev/language/constructors`
documents factory ctors; C# analog is "a static factory method". FR-024
cache hit.

**Conclusion.** Each Dart `factory` becomes `public static <Class>
<Name>(...)` on its class. The optional-positional `[VariableTypeInfo?
assignment]` in `PathCheckResult.consistent` maps to a default-valued
positional parameter (`VariableTypeInfo? assignment = null`).

### rf-dart-value-class-manual-eq-to-csharp-iequatable-objectequals (CACHED, reused from moded_term.dart)

**Deep analysis.** `VariableTypeInfo` overrides `==`/`hashCode` manually
comparing three fields (typeState, mode, isReader); no collection-typed
field. The three error classes and `WellTypedResult` / `PathCheckResult`
do NOT override equality (the spec preserves this — these are transient
return vehicles, not value-keys).

**Research (authoritative, CACHED).** Reused from moded_term.dart:
Microsoft Learn `IEquatable<T>` + `HashCode.Combine`. NOT a positional
record (codebase convention is hand-written `IEquatable<T>` on
AST/value nodes for review-visibility, and `record` synthesis on
`DFAState`-typed field would defer to DFAState's partial equality which
is correct but recording the choice explicitly preserves auditability).
FR-024 cache hit.

**Conclusion.** `VariableTypeInfo` is `sealed class … : IEquatable<…>`
with hand-written `Equals(T?)`/`Equals(object?)`/`GetHashCode()`/`==`/`!=`.
The four other classes (WellTypedResult, PathCheckResult, three error
classes) emit NO equality override — preserving Dart's choice to omit
it.

### rf-dart-abstract-class-pure-contract-to-csharp-interface (CACHED, reused from moded_term.dart, with OPPOSITE conclusion)

**Deep analysis.** `WellTypedError` is `abstract class` with one
abstract `String message` getter. By the rule documented in
moded_term.dart's cached finding (no fields, no concrete methods, all
members abstract → interface), it could be an interface — but the
hierarchy is an *error/exception ADT* whose extension model fits
`abstract class` better.

**Research (authoritative, CACHED).** Reused from moded_term.dart:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
fundamentals/object-oriented/inheritance` — *"Define an abstract class
when you want to provide a common implementation that derived classes
can use"*. Decision rule cached from rf finding; applied with the
opposite conclusion here (abstract-class, not interface) because the
hierarchy is an open-ended error ADT that may grow shared behaviour.
FR-024 cache hit — no second research call.

**Conclusion.** Emit `public abstract class WellTypedError` with
`public abstract string Message { get; }`. NOT `interface
IWellTypedError` — the hierarchy is conceptually an error ADT (extension
model = subclassing with possibly shared default behaviour). NOT
`sealed` — open to future error kinds.

### rf-dart-top-level-function-to-csharp-static-method (CACHED, reused from mode.dart / moded_term.dart)

**Deep analysis.** Two public top-level functions (`checkModedTerm`,
`checkPathAgainstAutomaton`) and five private helpers
(`_buildTransitionLabel`, `_checkLeafConsistencyForPath`,
`_pathStepToLeafTerm`, `_variableKey`, `_checkDuality`). All are pure
transformations on their parameters; none accesses any global state.

**Research (authoritative, CACHED).** Reused from mode.dart /
moded_term.dart: C# has no top-level functions in this codebase
convention; emit as `public static` / `private static` methods on a
host static class. FR-024 cache hit.

**Conclusion.** Host class `WellTypedTerm` (or `WellTypedTermOps`) —
distinct from `WellTypedResult` (data class) and `WellTypedError`
(error base). Public functions are `public static`; private helpers
are `private static` per the rf-csharp-private-vs-internal-library-
helpers reasoning (clause_validation.dart / program_dfa.dart) since
the helpers are file-internal in Dart and co-located with their
callers in this single C# type.

### rf-dart-string-interp-unicode-to-csharp-interpolated-string-utf8 (CACHED, reused from moded_term.dart)

**Deep analysis.** String interpolation appears in `VariableTypeInfo
.toString`, all three error subclasses' `Message` getters, the
inconsistent-mode error in `checkPathAgainstAutomaton`, and the
duality-error messages in `_checkDuality`. The Unicode arrows `↓` /
`↑` appear in `VariableTypeInfo.toString` and the mode-mismatch error;
they are the GLP mode notation.

**Research (authoritative, CACHED).** Reused from moded_term.dart:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
language-reference/tokens/interpolated` documents interpolated strings;
Roslyn handles Unicode source verbatim. The fresh nuance here is
ternary-inside-interpolation parenthesisation:
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/
tokens/interpolated#how-to-use-the-conditional-operator-in-an-interpolation-expression`
— *"Conditional operator expressions must be enclosed in parentheses"*.
FR-024 cache hit on the base finding; the ternary-parenthesisation
sub-nuance is recorded inline (no fresh rf entry — it's a syntactic
detail of the same idiom).

**Conclusion.** Dart `'…$x…'` → C# `$"…{X}…"`; ternary inside
interpolation MUST be parenthesised: `$"…{(cond ? a : b)}…"`. Unicode
glyphs preserved verbatim; UTF-8 source files mandated.

### rf-csharp-string-equality-ordinal-by-default (CACHED, reused from program_dfa.dart)

**Deep analysis.** Three places use string operations whose semantics
depend on the comparison strategy: `varKey.endsWith('?')` /
`varKey.substring(...)` in `_checkDuality`; `value.startsWith(...)` /
`value.endsWith(...)` quote sniffing in `_pathStepToLeafTerm`; and
the Dictionary keys used in both `checkModedTerm` and `_checkDuality`
(string ↔ VariableTypeInfo, string ↔ inner dict).

**Research (authoritative, CACHED).** Reused from program_dfa.dart:
Microsoft Learn `https://learn.microsoft.com/en-us/dotnet/csharp/
language-reference/operators/equality-operators` — *"Equality operators
... perform an ordinal comparison"* for `string ==`. For
`StartsWith`/`EndsWith`: Microsoft Learn `https://learn.microsoft.com/
en-us/dotnet/api/system.string.startswith` — *"By default ... uses the
current culture for comparison"* — so explicit `StringComparison
.Ordinal` is REQUIRED. For Dictionary keys: Microsoft Learn
`https://learn.microsoft.com/en-us/dotnet/api/system.stringcomparer
.ordinal` — *"Returns a StringComparer object that performs case-
sensitive ordinal string comparison"*. FR-024 cache hit.

**Conclusion.** Direct `==` / `!=` on `string` is ordinal by default
and maps 1:1 with Dart. `StartsWith` / `EndsWith` MUST use explicit
`StringComparison.Ordinal` overloads. `Dictionary<string, V>` MUST be
constructed with `StringComparer.Ordinal` for explicit-intent and
defence against future locale-dependent defaults.

### rf-csharp-private-vs-internal-library-helpers (CACHED, reused from program_dfa.dart / clause_validation.dart)

**Deep analysis.** Five private top-level helpers
(`_buildTransitionLabel`, `_checkLeafConsistencyForPath`,
`_pathStepToLeafTerm`, `_variableKey`, `_checkDuality`) — each
file-internal, co-located with the public functions that call them.

**Research (authoritative, CACHED).** Reused from program_dfa.dart /
clause_validation.dart: Dart leading-underscore is library-private;
when the helper is co-located with its callers in a single C# type,
`private` (not `internal`) is the tighter, correct mapping. FR-024
cache hit.

**Conclusion.** Five `private static` methods on the host
`WellTypedTerm` static class.

### rf-dart-string-substring-end-exclusive-to-csharp-substring-length (NEW)

**Deep analysis.** Two Substring sites differ in start index: (a)
`_pathStepToLeafTerm` strips quotes via `value.substring(1, value
.length - 1)` (start=1, end=L-1, length=L-2); (b) `_checkDuality`
strips the `?` suffix via `varKey.substring(0, varKey.length - 1)`
(start=0, end=L-1, length=L-1).

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.string.substring`
— Microsoft Learn `String.Substring(int startIndex, int length)`:
*"length: The number of characters in the substring."* Not an end
index. Verbatim query: "C# String.Substring second argument length not
end index". WebFetch
`https://api.dart.dev/stable/dart-core/String/substring.html` —
dart.dev: *"Returns the substring of this string ... starting from
startIndex (inclusive) and extending to endIndex (exclusive)."* Decisive
authoritative confirmation that the two APIs use different
conventions.

**Conclusion.** Each Dart `substring(start, end)` translates to C#
`Substring(start, end - start)`. Site (a): Dart `substring(1, L - 1)` →
C# `Substring(1, L - 2)`. Site (b): Dart `substring(0, L - 1)` →
C# `Substring(0, L - 1)` (start=0 makes the length and end coincide).
Codegen MUST adjust the length argument site-by-site; the off-by-one
trap is severe.

### rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add (NEW)

**Deep analysis.** `_checkDuality` builds a `Map<String, Map<String,
VariableTypeInfo>>` via `baseNames.putIfAbsent(baseName, () => {});
baseNames[baseName]![varKey] = info;` — a two-step
"lookup-or-insert-then-update" idiom.

**Research (authoritative).** WebFetch
`https://api.dart.dev/stable/dart-core/Map/putIfAbsent.html` —
dart.dev: *"Look up the value of key, or add a new entry if it isn't
there. Returns the value associated to key, if there is one."*
WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic
.dictionary-2.trygetvalue` — Microsoft Learn `Dictionary<TKey,TValue>
.TryGetValue`: *"Gets the value associated with the specified key. ...
Returns true if the Dictionary<TKey,TValue> contains an element with
the specified key; otherwise, false."* Verbatim query: "C# Dictionary
putIfAbsent equivalent TryGetValue Add". Authoritative conclusion: the
canonical C# idiom is `if (!dict.TryGetValue(key, out var value)) {
value = new V(); dict[key] = value; }` — no single-call equivalent
exists in `Dictionary<TKey,TValue>`. Microsoft Learn also documents
`CollectionsMarshal.GetValueRefOrAddDefault` (.NET 6+) which returns a
ref to the entry, but it's `ref`-return wizardry and overkill for this
non-hot-path code.

**Conclusion.** Emit the TryGetValue-then-Add idiom verbatim. The
factory closure (`() => {}` in Dart) provides lazy default
construction; C# constructs the empty dict only on the cache-miss arm
too. Reference-vs-value: inner dictionaries are reference types shared
via the outer-dict values — mutation through `variants[varKey] = info`
modifies the dict referenced from `baseNames[baseName]`, matching
Dart's identical aliasing.

### Explicitly addressed well-known nuances (per SC-006 / US2-AS4)

1. **Substring end-exclusive vs length.** Two sites with different start
   indices; the off-by-one bug-risk is severe and site-by-site
   adjustment is mandated.
2. **Dictionary string-key ordinal comparer.** Both `Dictionary<string,
   V>` instances MUST be constructed with `StringComparer.Ordinal` to
   make key semantics match Dart `Map<String,V>` explicitly.
3. **Null-coalescing parity (`??`).** Direct token-for-token mapping;
   identical semantics.
4. **Nullable enum / nullable reference fields.** `Mode?` and
   `VariableTypeInfo?` map to C# `Mode?` (Nullable<Mode>) and
   `VariableTypeInfo?` (nullable reference) respectively. Lifted equality
   on the enum case is the load-bearing nuance.
5. **try/catch parity.** Dart `try { … } catch (e) { … }` (e unused) →
   C# `try { … } catch (Exception) { … }`. The underlying exception
   type (`StateError` → `InvalidOperationException`) is a subclass of
   `Exception`, so the catch sees it.
6. **TryParse pattern.** Dart `int.tryParse(s)`/`double.tryParse(s)` →
   C# `int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture,
   out var v)` / `double.TryParse(s, NumberStyles.Float,
   CultureInfo.InvariantCulture, out var v)`. Invariant culture is
   mandated to match Dart semantics exactly.
7. **Abstract class vs interface — explicit OPPOSITE decision.**
   `WellTypedError` maps to `abstract class`, not `interface`, because
   it is an open-ended error ADT (extension model differs from the
   pure-contract visitor in moded_term.dart).
8. **No defensive copying of returned collections.** `WellTypedResult`'s
   `VariableTypes` and `Errors` properties alias the locally-built
   mutable instances; the source does not defensively copy, and the
   spec preserves that.
9. **Ternary inside string interpolation.** C# REQUIRES parentheses
   around a conditional expression inside an interpolation hole; Dart
   does not. Codegen MUST insert parentheses.
10. **No records.** All seven classes are `sealed class`, not `record`
    or `record struct` — codebase convention (consistent with
    moded_term.dart, type_ast.dart, program_dfa.dart) plus the explicit
    "preserve source's choice not to override equality" rationale for
    the five non-VariableTypeInfo classes.

### No escalations

All thirteen non-trivial constructs resolve against official Dart/.NET
documentation with consistent conclusions. Eleven of the rf-* findings
are cached/reused from the six already-specced sibling files in this
directory per FR-024 — no re-research, no re-derivation. Two rf-*
findings are recorded as new for this file
(`rf-dart-string-substring-end-exclusive-to-csharp-substring-length`
and `rf-dart-map-putifabsent-to-csharp-trygetvalue-or-add`), each with
verbatim authoritative citations from official dart.dev and
learn.microsoft.com. `open_escalation_count` = 0.
