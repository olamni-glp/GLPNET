> Conversion-spec artifact for lib/runtime/commit.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/commit.dart
source_sha256: e6f154b9dd2433d9bce1b5112b75ec042c7827d520e8d8b82e85f1c20c7812dd
target_code_unit: lib/runtime/commit.cs
constructs:
  - construct_key: "dart.import.relative-same-package.machine_state"
    source_form: "`import 'machine_state.dart';` -- relative import of sibling library; brings `GoalRef` (the readonly-record-struct value carrier of (id, pc)) into scope. No `show`/`hide` clause."
    target_decision: >-
      NO standalone target artefact for the import; instead `lib/runtime/commit.cs`
      emits a `using <namespace>;` for the namespace of the converted sibling
      `lib/runtime/machine_state.cs`. The namespace name is decided by the
      downstream depgraph/namespace step (same precedent as
      `.codeconv/conversion-specs/lib/runtime/fairness.dart.md`,
      `.codeconv/conversion-specs/lib/runtime/abandon.dart.md`, and
      `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`). Dart imports
      a *library/file*; C# imports a *namespace* -- there is no faithful
      file-to-file `using` form (no `using "./machine_state.cs";`) and no
      per-symbol narrowing (Dart absence of `show`/`hide` here is benign).
      Codegen reaches `GoalRef` via its containing namespace once `using`-imported.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Import-unit nuance: Dart imports a library/file, C# imports a namespace
      (asymmetry recorded in the fairness/abandon precedents and reused
      verbatim). No `show`/`hide` to translate. Value-vs-reference,
      null-safety, async, Stream, isolate: NOT APPLICABLE -- an import
      directive declares no values/types and has no runtime form.
  - construct_key: "dart.import.relative-same-package.heap_fcp"
    source_form: "`import 'heap_fcp.dart';` -- relative import of sibling library; brings `HeapFCP`, `HeapCell`, `CellTag` (enum: `WrtTag`/`RoTag`/`ValueTag`), `Pointer`, plus the methods `isWriter`/`isReader`/`isFullyBound`/`derefAddr`/`bindWriterToReader`/`bindWriterNoCallback`/`firePendingCallback` into scope. No `show`/`hide`."
    target_decision: >-
      Same shape as the `machine_state` import: a single `using <namespace>;`
      directive in `lib/runtime/commit.cs` naming the namespace of the
      converted `lib/runtime/heap_fcp.cs`. The heap-fcp convspec
      (.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md) decides the
      target shapes for `HeapFCP`/`HeapCell`/`CellTag`/`Pointer` and their
      method names (PascalCase); this convspec consumes those decisions by
      name. No per-symbol narrowing.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Same import-unit asymmetry as above. The set of consumed symbols is
      large (one class type, one enum, one helper record/class, and eight
      methods) but the surface contract is wholly governed by the imported
      sibling's convspec -- this file is a consumer, not a definer. NOT
      APPLICABLE: value/reference, null-safety, async at the directive
      level.
  - construct_key: "dart.import.relative-same-package.suspension"
    source_form: "`import 'suspension.dart';` -- relative import; brings `SuspensionRecord` and `SuspensionListNode` (with `.armed`/`.goalId`/`.resumePC`/`.record`/`.next` surface and `disarm()` mutator) into scope. No `show`/`hide`."
    target_decision: >-
      Same shape as the other relative imports: a single `using
      <namespace>;` directive naming the namespace of the converted
      `lib/runtime/suspension.cs`. The suspension convspec
      (.codeconv/conversion-specs/lib/runtime/suspension.dart.md) decides
      the target shape of `SuspensionRecord` and `SuspensionListNode`; this
      convspec consumes them by name (reference-type classes per that spec).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: Same as above.
  - construct_key: "dart.import.relative-same-package.terms"
    source_form: "`import 'terms.dart';` -- relative import; brings `Term` (the term hierarchy base), `VarRef` (variable-reference term with `addr` int field), `ConstTerm` (constant-wrapping term) into scope. No `show`/`hide`."
    target_decision: >-
      Same shape: a single `using <namespace>;` directive naming the
      namespace of the converted `lib/runtime/terms.cs`. The terms convspec
      (.codeconv/conversion-specs/lib/runtime/terms.dart.md) decides the
      target shapes for the term hierarchy; this file consumes them by
      name. The three identifiers `Term`, `VarRef`, `ConstTerm` are
      preserved verbatim at every use site.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: Same as above.
  - construct_key: "dart.utility_class.static_only_holder.CommitOps"
    source_form: >-
      "`class CommitOps { static List<GoalRef> applySigmaHatFCP({...}) {...}
      static void _walkAndActivate(...) {...} static void
      _forwardSuspensions(...) {...} }`" -- a Dart class containing exactly
      three static members (one public, two underscore-private), no fields,
      no instance constructor, no instance members. Used as a namespacing
      container for the FCP "commit" / writer-substitution operations.
      Identical class-shape pattern to the AbandonOps class converted in
      `.codeconv/conversion-specs/lib/runtime/abandon.dart.md`.
    target_decision: >-
      Emit a C# `public static class CommitOps` (sealed, abstract by virtue
      of `static`, cannot be instantiated -- the .NET counterpart of a Dart
      "static-methods-only" holder class per the
      rf-dart-static-only-holder-to-csharp-static-class idiom established
      in abandon.dart.md). Containing the three converted static members
      (one public `ApplySigmaHatFCP`, two private `_WalkAndActivate` and
      `_ForwardSuspensions`). A non-static class with all-static members is
      REJECTED here because (a) the Dart source's class is callable only
      via `CommitOps.applySigmaHatFCP(...)` (never instantiated) and (b)
      `static class` makes the no-instantiation contract a compile-time
      guarantee on the .NET side, matching the source's design intent. Do
      NOT emit free-floating static functions at namespace scope: C# does
      not permit top-level free functions outside a type, and the Dart
      source explicitly groups via the class identifier `CommitOps` that
      the conversion preserves as a callable identifier.
    idiom_id: null
    research_finding_id: rf-dart-static-only-holder-to-csharp-static-class
    nuance: >-
      Static-class contract: in Dart, a class with only static members is
      still instantiable by convention only (`CommitOps()` would compile);
      C# `static class` makes the no-instantiation a compile-time invariant
      and also makes the class implicitly sealed -- both invariants are
      desirable here (the Dart source never instantiates `CommitOps` and
      has no subclasses). Value-vs-reference: not applicable at the type
      level (no instances). Async / Stream / Future / isolate: ABSENT --
      every member is synchronous. Null-safety at the type level: not
      applicable. The narrowing is strictly correct here -- same
      reasoning as abandon.dart.md.
  - construct_key: "dart.static_method.named_required_params.applySigmaHatFCP"
    source_form: >-
      "`static List<GoalRef> applySigmaHatFCP({required HeapFCP heap,
      required Map<int, Object?> sigmaHat}) { ... }`" -- a public static
      method with two named-required parameters (`heap`, `sigmaHat`), a
      `List<GoalRef>` return type, and a non-trivial body that iterates
      `sigmaHat.entries` twice (once to validate WxW, once to apply
      bindings), accumulates `activations` into a mutable `<GoalRef>[]`
      list, defers callbacks via a `writersWithCallbacks` mutable
      `<int>[]` list, walks `sigmaHat.keys` to re-dereference indirectly-
      bound cells, then fires the deferred callbacks. Returns the
      accumulated activations.
    target_decision: >-
      Emit a `public static IList<GoalRef> ApplySigmaHatFCP(HeapFCP heap,
      SigmaHat sigmaHat)` method on `public static class CommitOps`. The
      Dart `{required HeapFCP heap, required Map<int, Object?> sigmaHat}`
      named-required parameters map to plain C# positional parameters (no
      defaults) -- per the project-recurring idiom
      `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`
      reused from machine_state.dart.md / abandon.dart.md: C# has no
      per-parameter `required` keyword for methods (the C# 11 `required`
      modifier is for properties only), and a positional parameter without
      a default IS required by the compiler; call-site readability is
      preserved by C# named-argument syntax
      (`CommitOps.ApplySigmaHatFCP(heap: h, sigmaHat: s)`). The second
      parameter is the resolved `SigmaHat` alias (= `Dictionary<int,
      object?>`) decided in machine_state.dart.md, NOT a fresh `Map<int,
      Object?>` re-derivation here -- the alias is the single source of
      truth for the writer-substitution shape across the runtime. Return
      `IList<GoalRef>` (interface contract) per the project idiom from
      abandon.dart.md (`List<T>` Dart return -> `IList<T>` C# return is
      the faithful contract; the concrete `List<GoalRef>` would leak the
      implementation type). Body is a translated sequence of foreach +
      conditional + method calls; see the body-construct rows below for
      each sub-decision.
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      Named-required nuance: Dart `{required HeapFCP heap, required Map<...>
      sigmaHat}` -> C# positional `HeapFCP heap, SigmaHat sigmaHat` (no
      defaults -> compiler-required). Call sites preserve readability by
      using named-argument syntax; this is the recurring project idiom.
      Map-type nuance: the Dart parameter type `Map<int, Object?>` is the
      same shape that `machine_state.dart`'s `typedef SigmaHat` resolves to;
      the .NET port reuses the global-using `SigmaHat` alias rather than
      restating the raw `Dictionary<int, object?>` -- this preserves
      readability and pins the single source of truth (codegen MUST use the
      alias, not the expanded form). List-return nuance: `List<GoalRef>`
      -> `IList<GoalRef>` per abandon.dart.md precedent; the method MUST
      return a fresh mutable list (the body writes `activations.add(...)`
      throughout), so `IList<GoalRef>` (mutable interface) is correct and
      `IReadOnlyList<GoalRef>` would be wrong (would lose the post-return
      mutation surface the body relies on internally; the *contract* of
      the return is the populated set -- callers iterate it but do not
      mutate; codegen MAY narrow to `IReadOnlyList<GoalRef>` if downstream
      usage confirms no caller mutation, but the spec default is the
      faithful `IList<GoalRef>`). Value-vs-reference: `HeapFCP` is a
      reference type (the heap is shared mutable state -- the .NET port
      preserves reference semantics per heap_fcp.dart.md); `SigmaHat` is
      a reference type (the dictionary itself is mutated and observed by
      the caller's `GoalState.SigmaHat` -- same instance, no copy);
      `IList<GoalRef>` is a reference type whose elements are value-type
      record-structs (per machine_state.dart.md GoalRef decision).
      Null-safety: under enabled NRT both parameters are non-nullable
      (Dart `required` named params are non-nullable by their type); the
      return is non-nullable `IList<GoalRef>` (the body always returns the
      `activations` local, never null). Async / Stream / Future: ABSENT
      -- the method is synchronous and the body has no `await`; codegen
      MUST NOT introduce `async`/`Task<IList<GoalRef>>` -- inventing async
      would change the calling contract (state-machine allocation,
      exception propagation through the task) for no semantic reason.
      Determinism: the method is deterministic given fixed (heap,
      sigmaHat) inputs; order of iteration over `sigmaHat.entries` is the
      Dart `Map` insertion order
      (api.dart.dev/dart-core/Map/entries.html -- "iterates in the order
      they appear in the map"); the .NET `Dictionary<TKey,TValue>` does
      NOT formally guarantee insertion order but the implementation since
      .NET Core 1.x has preserved it
      (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2
      -- "The order in which the items are returned is undefined" --
      DELIBERATELY left as a runtime implementation detail). For
      writer-substitution application the iteration order is observable
      via the WxW-validation first-pass (an unbound writer that is
      ALSO a key in sigmaHat may resolve on a later pass via the
      re-dereference loop) -- but the algorithm is order-independent by
      design: the second loop (`for (final varId in sigmaHat.keys) {
      ... derefAddr(targetAddr) ... }`) re-dereferences ALL bound cells
      AFTER all writers have been applied, precisely to handle any
      iteration order. This is recorded as a nuance, NOT an escalation
      -- the algorithm is robust to dictionary iteration-order
      differences between Dart Map and .NET Dictionary.
  - construct_key: "dart.foreach.map_entries.applySigmaHatFCP-wxw-prevalidation-loop"
    source_form: >-
      "`for (final entry in sigmaHat.entries) { final value = entry.value;
      if (value is VarRef && heap.isWriter(value.addr)) { ... if
      (!heap.isFullyBound(clauseWriterId) && !heap.isFullyBound(
      queryWriterAddr)) { throw StateError('WxW violation in
      applySigmaHatFCP: W$clauseWriterId -> W$queryWriterAddr (both
      unbound)'); } } }`" -- a pre-application validation loop that walks
      every (writer-id, value) pair in σ̂w and asserts the FCP invariant
      "no two unbound writers may be bound to each other" (the writer-MGU
      constraint per the heap-pointer architecture spec). Uses Dart `is
      VarRef` type-test, `&&` short-circuit, `heap.isWriter`/`isFullyBound`
      method calls, and a `throw StateError(...)` with `$`-interpolation.
    target_decision: >-
      Emit `foreach (var entry in sigmaHat) { var value = entry.Value; if
      (value is VarRef varRef && heap.IsWriter(varRef.Addr)) { var
      clauseWriterId = entry.Key; var queryWriterAddr = varRef.Addr; if
      (!heap.IsFullyBound(clauseWriterId) && !heap.IsFullyBound(
      queryWriterAddr)) { throw new InvalidOperationException($"WxW
      violation in ApplySigmaHatFCP: W{clauseWriterId} ->
      W{queryWriterAddr} (both unbound)"); } } }`. Idiom reuse: `foreach
      (var e in dict)` over a `Dictionary<TKey,TValue>` yields
      `KeyValuePair<TKey,TValue>` with `.Key`/`.Value` properties (NOT
      `MapEntry<K,V>` -- the .NET counterpart of Dart `Map.entries` is the
      direct enumeration of the dictionary
      (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.getenumerator)
      -- no `.entries` accessor is needed; the dictionary itself
      enumerates as `KeyValuePair`. The `is VarRef varRef` C# pattern uses
      the C# 7+ type-pattern syntax to bind a typed local in one step
      (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#type-pattern)
      -- mirroring Dart's `is VarRef` promotion of `value` to `VarRef`
      within the guarded branch. `&&` is preserved as C# `&&` (short-circuit
      logical AND, identical semantics). `heap.isWriter(...)` /
      `heap.isFullyBound(...)` PascalCase per .NET capitalisation
      conventions and per the heap_fcp.dart.md convspec which decides these
      method names. `throw StateError(...)` -> `throw new
      InvalidOperationException(...)` per the project-recurring idiom
      `rf-dart-staterror-to-csharp-invalidoperationexception` (heap_fcp
      precedent, line 662 / 869 of heap_fcp.dart.md). `$`-interpolation in
      the Dart message -> `$"..."` C# interpolated string literal per
      `rf-dart-string-interpolation-join-to-csharp-interpolation-string-join`.
    idiom_id: null
    research_finding_id: rf-dart-is-not-type-test-to-csharp-is-not-pattern
    nuance: >-
      Type-test nuance (load-bearing): Dart `value is VarRef` is a runtime
      type-test that, in a guarded scope, promotes `value` to `VarRef` so
      that `value.addr` resolves to `VarRef.addr` without an explicit cast.
      The faithful C# counterpart is the type-pattern `value is VarRef
      varRef` which both tests and binds in one step
      (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#type-pattern).
      An older C# form (`value is VarRef` + `((VarRef)value).Addr`) would
      also be correct but loses the single-step bind-on-success that
      mirrors Dart's promotion; the convspec mandates the pattern form for
      consistency with heap_fcp.dart.md (which uses the same pattern at
      construct row 1061). Short-circuit nuance: Dart `&&` and C# `&&` both
      short-circuit and have identical truth tables; the
      `heap.isWriter(value.addr)` second conjunct is never evaluated when
      `value is VarRef` is false -- the C# counterpart is identical. Throw-
      mapping nuance: see rf-dart-staterror-to-csharp-invalidoperationexception
      below -- a Dart `StateError` ('state error' for invariant violations)
      maps to .NET `InvalidOperationException` ('thrown when a method call
      is invalid for the object's current state') by INTENT, not by class-
      hierarchy. String-interpolation nuance: Dart `'... W$clauseWriterId
      -> W$queryWriterAddr ...'` -> C# `$"... W{clauseWriterId} ->
      W{queryWriterAddr} ..."` -- the `$`-prefix-and-curly-brace form is
      the .NET-canonical interpolated string literal
      (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated).
      Verbatim message preserved (including the `W` letter prefix that
      tags writer-ids in the diagnostic). Value-vs-reference: `KeyValuePair`
      is a value type (struct) in .NET -- but iteration yields a value
      semantically equivalent to a Dart `MapEntry` (immutable view of one
      pair). Null-safety: `entry.Value` is `object?` (the SigmaHat value
      type is nullable per machine_state.dart.md); the `is` test only
      succeeds for non-null `VarRef` references, so the cast-binding
      `varRef` is non-null in its scope. Async: ABSENT (synchronous
      iteration over an in-memory dictionary).
  - construct_key: "dart.local_var.list_literal.activations-and-writers-with-callbacks"
    source_form: >-
      "`final activations = <GoalRef>[];` and `final writersWithCallbacks
      = <int>[];`" -- two locally-scoped Dart mutable `List<T>` literals
      (empty list with explicit element type), each mutated via `add(...)`
      / `addAll(...)` later in the body. `activations` is returned at the
      end of the method.
    target_decision: >-
      Emit `var activations = new List<GoalRef>();` and `var
      writersWithCallbacks = new List<int>();`. The concrete `List<T>` (not
      `IList<T>`) is correct here because these are LOCAL variables, not
      return-types or fields: codegen needs the concrete type's
      `.Add(...)`/`.AddRange(...)` mutator surface, and the BCL `List<T>`
      is the canonical Dart-`List<T>`-equivalent for local mutable
      sequence-builders
      (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1).
      Use `var` (target-typed local-variable declaration) for terseness;
      the explicit type `List<GoalRef> activations = new();` is also
      acceptable but `var` mirrors the Dart inference cleanly. The return
      statement returns `activations` -- the type narrows to `IList<GoalRef>`
      at the boundary (implicit reference conversion from `List<T>` to
      `IList<T>`); no explicit cast needed.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Local-vs-field nuance: `final` in Dart on a local declaration means
      "single-assignment local variable" (the local cannot be reassigned,
      though the referenced list can be mutated). The C# counterpart for a
      single-assignment local is... no direct keyword (`readonly` is for
      fields only); C# locals are not reassignment-restricted by language.
      Two acceptable forms: (a) `var` -- the convention for non-reassigned
      locals (the convspec prefers this), (b) explicit type. Codegen MUST
      NOT use `const` (would force a compile-time-constant initialiser,
      which `new List<T>()` is not) and MUST NOT wrap the list in a
      `readonly` field-level container. The single-assignment-of-local
      property is a *static check*, not a runtime invariant; the .NET port
      relies on review/lint to enforce it. The list itself is mutable in
      both languages -- the source pattern is "single-assignment local
      pointing at a mutable container", which is the .NET-default for any
      `var` holding a `new List<T>()`.
      The reused idiom name `rf-dart-final-field-class-to-csharp-getonly-class`
      is the closest existing project idiom (the `final`-mapping family);
      the local-variant is recorded under the same family because the
      decision basis is identical (final-means-not-reassigned).
  - construct_key: "dart.foreach.map_entries.applySigmaHatFCP-body-binding-loop"
    source_form: >-
      "`for (final entry in sigmaHat.entries) { final varId = entry.key;
      var value = entry.value; if (value == null) continue; if (value is
      VarRef) { if (heap.isReader(value.addr)) { final acts =
      heap.bindWriterToReader(varId, value.addr); activations.addAll(acts);
      continue; } else if (heap.isWriter(value.addr)) { final derefResult =
      heap.derefAddr(value.addr); if (derefResult is Term && derefResult is!
      VarRef) { value = derefResult; } else { throw StateError('σ̂w contains
      unbound writer address ${value.addr} - HEAD instruction bug'); } } }
      final valueAsTerm = value is Term ? value : ConstTerm(value); final
      acts = heap.bindWriterNoCallback(varId, valueAsTerm);
      activations.addAll(acts); writersWithCallbacks.add(varId); }`" --
      the main writer-substitution-application loop. Cases handled (per
      Section 5.3 of heap-pointer-architecture-spec.md): (a) null skip, (b)
      VarRef-to-reader -> bindWriterToReader, (c) VarRef-to-writer ->
      deref to ground or throw, (d) non-null non-VarRef ground term ->
      bindWriterNoCallback (callback deferred). Uses Dart type-test
      promotion, `is!` (is-not), conditional `?:`, `continue`,
      list-`addAll`, `throw StateError` with `$`-interpolation including
      `${value.addr}` (compound expression form).
    target_decision: >-
      Emit a `foreach (var entry in sigmaHat) { var varId = entry.Key; var
      value = entry.Value; if (value == null) continue; if (value is VarRef
      varRef0) { if (heap.IsReader(varRef0.Addr)) { var acts =
      heap.BindWriterToReader(varId, varRef0.Addr);
      activations.AddRange(acts); continue; } else if (heap.IsWriter(
      varRef0.Addr)) { var derefResult = heap.DerefAddr(varRef0.Addr); if
      (derefResult is Term derefTerm && derefResult is not VarRef) { value
      = derefTerm; } else { throw new InvalidOperationException($"σ̂w
      contains unbound writer address {varRef0.Addr} - HEAD instruction
      bug"); } } } var valueAsTerm = value is Term termValue ? termValue :
      new ConstTerm(value); var acts2 = heap.BindWriterNoCallback(varId,
      valueAsTerm); activations.AddRange(acts2); writersWithCallbacks.Add(
      varId); }`. Decisions: (i) `is VarRef varRef0` pattern-binds in one
      step per rf-dart-is-not-type-test-to-csharp-is-not-pattern (reused
      from heap_fcp.dart.md row 1061); (ii) `is! VarRef` (Dart is-not test)
      -> C# `is not VarRef` (C# 9 logical-not pattern,
      https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#logical-patterns)
      -- same idiom rf-dart-is-not-type-test-to-csharp-is-not-pattern;
      (iii) `addAll` -> `AddRange` (`List<T>.AddRange(IEnumerable<T>)`,
      https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange);
      (iv) `value is Term ? value : ConstTerm(value)` -> `value is Term
      termValue ? termValue : new ConstTerm(value)` -- type-test-then-
      ternary, with the type-pattern binding `termValue` to avoid the
      otherwise-required redundant cast; (v) `ConstTerm(value)` -> `new
      ConstTerm(value)` (C# requires `new` for constructor calls; the
      ConstTerm constructor surface is decided in terms.dart.md); (vi) the
      Dart compound interpolation `${value.addr}` -> C# `{varRef0.Addr}`
      (the local `varRef0` from the outer `is VarRef varRef0` pattern is
      in scope on the `else if (heap.IsWriter(varRef0.Addr))` branch);
      (vii) the `continue` statement is the C# keyword `continue`, same
      semantics in both languages
      (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/jump-statements#the-continue-statement);
      (viii) the `var value = entry.Value;` declaration is REASSIGNED
      inside the body (`value = derefTerm`) -- this is a mutable local in
      both languages; Dart's `var` and C#'s `var` both permit reassignment
      (Dart `final` is the single-assignment form; the source uses `var`
      here precisely to permit the reassignment). The local-typed binding
      `varRef0` from the outer `is`-pattern is in scope for the entire
      `if (value is VarRef varRef0) { ... }` block, including the nested
      `else if` branch.
    idiom_id: null
    research_finding_id: rf-dart-is-not-type-test-to-csharp-is-not-pattern
    nuance: >-
      Type-test-and-cast nuance (load-bearing across this loop): Dart `is`
      promotes `value` within a guarded scope; C# `is T t` both tests and
      binds. The two type-tests in this construct (`value is VarRef`
      outer, `derefResult is Term && derefResult is! VarRef` inner) both
      use the binding pattern form. The `is!` (is-not) -> `is not` (C# 9+
      logical-not pattern) preserves the exact source shape -- an older
      `!(derefResult is VarRef)` is also correct but loses the parity with
      the source. Reassign-after-promotion nuance: in Dart, `var value =
      entry.value;` is declared once and reassigned inside the `else if`
      branch (`value = derefResult;`); the C# port mirrors this -- `var
      value = entry.Value;` declares once, `value = derefTerm;` reassigns.
      Crucially, the SECOND use of `value` in `var valueAsTerm = value is
      Term termValue ? termValue : new ConstTerm(value);` reads the
      possibly-reassigned value (after the `else if` branch may have
      updated it to a non-VarRef Term). Reference identity nuance: Dart's
      promotion is on the local LVALUE; reassigning `value` is permitted
      because the declaration is `var` (not `final`). The C# `var` form
      preserves this faithfully. AddRange-vs-foreach-Add nuance: `addAll`
      -> `AddRange` is correct because `List<T>.AddRange` is the documented
      bulk-insertion method
      (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.addrange);
      a manual `foreach (var a in acts) activations.Add(a);` is
      semantically equivalent but verbose and not the BCL-canonical form.
      ConstTerm-wrap nuance: the source's `value is Term ? value :
      ConstTerm(value)` is a "leave Term values unwrapped, wrap non-Term
      ground values" rule; the .NET port preserves it exactly. Codegen MUST
      NOT optimise away the `is Term` test on the assumption that ground
      values are always Terms -- the SigmaHat value type is `object?` and
      may carry non-Term ground values (per the Dart `Object?` placeholder
      in machine_state.dart.md). Continue/throw shapes: identical to Dart;
      `continue` skips to the next foreach iteration, `throw new
      InvalidOperationException(...)` is the StateError counterpart per
      rf-dart-staterror-to-csharp-invalidoperationexception. Null-safety:
      the `if (value == null) continue;` guard is the project's standard
      "skip null entries" form; under enabled NRT, after the guard `value`
      narrows to non-null `object` (C# null-flow analysis tracks this);
      no `value!` non-null-assertion is needed downstream. Async: ABSENT.
      Determinism: per-iteration deterministic given input.
  - construct_key: "dart.foreach.map_keys.applySigmaHatFCP-re-deref-loop"
    source_form: >-
      "`for (final varId in sigmaHat.keys) { final wAddr = varId; final
      cell = heap.cells[wAddr]; if (cell.tag == CellTag.WrtTag &&
      cell.content is Pointer) { final targetAddr = (cell.content as
      Pointer).targetAddr; final derefResult = heap.derefAddr(targetAddr);
      if (derefResult is Term && derefResult is! VarRef) { cell.content =
      derefResult; cell.tag = CellTag.ValueTag; } } }`" -- the second-pass
      re-dereference loop. For every writer-id key in σ̂w, look up the
      heap cell; if it is still a WrtTag-with-Pointer-content (i.e. bound
      indirectly to another variable), re-deref the pointer target; if the
      target is now a ground Term, update the cell in place (set content
      to the ground term and tag to ValueTag). Uses Dart `is Pointer`
      (type-test), `as Pointer` (explicit cast), enum equality
      `cell.tag == CellTag.WrtTag`, two `is`/`is!` tests, and direct field
      mutation on the cell.
    target_decision: >-
      Emit `foreach (var varId in sigmaHat.Keys) { var wAddr = varId; var
      cell = heap.Cells[wAddr]; if (cell.Tag == CellTag.WrtTag && cell.Content
      is Pointer ptr) { var targetAddr = ptr.TargetAddr; var derefResult =
      heap.DerefAddr(targetAddr); if (derefResult is Term derefTerm &&
      derefResult is not VarRef) { cell.Content = derefTerm; cell.Tag =
      CellTag.ValueTag; } } }`. Decisions: (i) `sigmaHat.keys` -> `sigmaHat.Keys`
      (`Dictionary<TKey,TValue>.Keys` returns `Dictionary<TKey,TValue>.KeyCollection`
      enumerable, https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.keys);
      (ii) `heap.cells[wAddr]` -> `heap.Cells[wAddr]` (indexer access; the
      `cells` field becomes the `Cells` property per heap_fcp.dart.md);
      (iii) `cell.content is Pointer` -> the binding pattern `cell.Content
      is Pointer ptr` so the subsequent `ptr.TargetAddr` access is direct
      WITHOUT the Dart `as`-cast (this is a SIMPLIFICATION over Dart's
      two-step `is Pointer` + `as Pointer` idiom -- C# pattern-binding
      eliminates the redundant cast); (iv) the original Dart `(cell.content
      as Pointer).targetAddr` is the `as`-cast idiom
      `rf-dart-as-cast-to-csharp-explicit-cast` (heap_fcp precedent row
      1081); in the .NET port the pattern-binding `ptr` REPLACES the
      explicit cast, but the underlying idiom is the same -- recorded as
      `rf-dart-as-cast-to-csharp-explicit-cast` for traceability; (v) enum
      equality `cell.tag == CellTag.WrtTag` -> `cell.Tag == CellTag.WrtTag`
      (C# enums support `==` by default; the enum member names PascalCase
      mapping is decided in cells.dart.md / heap_fcp.dart.md); (vi) the
      direct field mutations `cell.content = derefResult; cell.tag =
      CellTag.ValueTag;` -> `cell.Content = derefTerm; cell.Tag =
      CellTag.ValueTag;` -- C# property setters with the same observable
      semantics, assuming `HeapCell.Content` and `HeapCell.Tag` are
      `get/set` properties per the heap_fcp.dart.md convspec.
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      As-cast elimination nuance (load-bearing here): Dart `(cell.content
      as Pointer).targetAddr` first tests-then-casts as a two-step idiom
      (the `is Pointer` guard above ensures the cast cannot fail); .NET
      offers a SINGLE-step alternative -- the binding-pattern `is Pointer
      ptr` -- which both tests and binds in one statement. The .NET port
      MUST use the binding-pattern form (`is Pointer ptr`) because it (a)
      removes the redundant cast, (b) preserves the type-safety guarantee
      at compile time (an `as` cast that returns null on mismatch is the
      Dart pattern; the .NET `as`-cast also returns null on mismatch, but
      the binding pattern avoids the null-temporary entirely), and (c)
      mirrors the heap_fcp.dart.md row 1081 precedent. The underlying
      idiom name is preserved (`rf-dart-as-cast-to-csharp-explicit-cast`)
      because the SEMANTIC decision is "Dart `as` -> C# strongly-typed
      access"; the SYNTACTIC form (pattern vs explicit cast) is a
      codegen-level refinement. Type-test consistency nuance: the inner
      `derefResult is Term derefTerm && derefResult is not VarRef` mirrors
      the same shape as the binding loop above -- recorded under
      rf-dart-is-not-type-test-to-csharp-is-not-pattern (reused). Cell
      mutation nuance: the Dart source mutates `cell.content` and `cell.tag`
      in place; the .NET port preserves this by using setter properties
      on a reference-type `HeapCell`. If heap_fcp.dart.md decides
      `HeapCell` is a `class` (reference type), this is correct; if it
      decides `HeapCell` is a `struct` (value type), the `cell = heap.Cells[
      wAddr]` would copy and the in-place mutation would silently NOT
      propagate -- this is a CROSS-CONVSPEC INVARIANT load-bearing
      observation: heap_fcp.dart.md's HeapCell convspec MUST be a reference
      type (class), and the heap_fcp.dart.md spec at row 25 confirms it is
      a mutable class with `CellTag tag` and `Object? content` fields
      (reference type). The cross-spec dependency is recorded here for
      future review. Null-safety: `derefResult` is `Object` per heap_fcp;
      the `is Term derefTerm` narrows to non-null Term. Enum-equality
      nuance: Dart enum `==` and C# enum `==` are both value-equality
      (identical bit-pattern comparison for closed underlying-int enums);
      identical semantics. Async / Stream / Future: ABSENT.
  - construct_key: "dart.foreach.list.applySigmaHatFCP-fire-callbacks-loop"
    source_form: >-
      "`for (final writerAddr in writersWithCallbacks) {
      heap.firePendingCallback(writerAddr); }`" -- a third-pass loop that
      walks the `writersWithCallbacks` mutable list (populated by the body
      binding loop above) and invokes `heap.firePendingCallback(...)` on
      each. The callbacks are deferred until after all bindings and the
      re-deref loop complete (so nested VarRefs in bound structures can be
      fully dereferenced before any goal-activation callback runs).
    target_decision: >-
      Emit `foreach (var writerAddr in writersWithCallbacks) {
      heap.FirePendingCallback(writerAddr); }`. Trivial mechanical
      translation: `for-in` over `List<int>` -> `foreach` over
      `List<long>` (or `List<int>` if the heap-spec keeps int width; per
      heap_fcp.dart.md the heap-addr surface widens to `long`/`int`
      consistently with the project's `rf-dart-int-to-csharp-long-width`
      idiom -- the consuming spec governs); method-call PascalCase. The
      callback-firing is a void method; no return-value handling. The list
      is iterated in insertion order (Dart `List` is index-ordered, .NET
      `List<T>` is index-ordered -- identical foreach traversal order).
    idiom_id: null
    research_finding_id: rf-dart-postincrement-and-method-shape-to-csharp-equivalent
    nuance: >-
      Trivial-but-recorded: this loop is a straight 1:1 translation. The
      ordering invariant (callbacks fire in writers-bound-order, not
      writers-id-order) is preserved by the foreach over the
      insertion-ordered `List<int>` / `List<long>`. The deferred-callback
      protocol itself (defer-then-fire) is the ALGORITHMIC contract of
      `applySigmaHatFCP`; it is implemented identically in Dart and C# (a
      list of pending writer-ids, fired in order at the end). Null-safety:
      `writersWithCallbacks` is non-nullable and contains non-nullable
      ints; no null concerns. Async: ABSENT.
  - construct_key: "dart.return.local.applySigmaHatFCP"
    source_form: "`return activations;` -- the final statement of the method returns the accumulated `List<GoalRef>` of activations."
    target_decision: >-
      Emit `return activations;`. Implicit reference conversion from
      `List<GoalRef>` (concrete local) to `IList<GoalRef>` (declared return
      type) is a built-in C# language feature
      (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#reference-conversions);
      no explicit cast needed. The local's contents (a mutable list of
      value-type GoalRef structs) are returned by reference (the list
      reference), so the caller receives the same list instance that
      `applySigmaHatFCP` populated -- identical Dart semantics.
    idiom_id: null
    research_finding_id: null
    nuance: Trivial.
    trivial: true
  - construct_key: "dart.static_method.private.walkAndActivate-suspension-list-walker"
    source_form: >-
      "`static void _walkAndActivate(SuspensionListNode? list, List<GoalRef>
      acts) { var current = list; while (current != null) { if
      (current.armed) { acts.add(GoalRef(current.goalId!, current.resumePC));
      current.record.disarm(); } current = current.next; } }`" -- a
      private (underscore-prefixed) static helper that walks a possibly-
      null suspension list and, for each armed record, appends a new
      GoalRef to the `acts` accumulator and disarms the record. Uses Dart
      nullable-while-loop (`while (current != null)`), `current.goalId!`
      non-null assertion (the armed-guard above proves goalId is non-null),
      and direct list-`add` mutation. Currently NOT referenced anywhere
      in commit.dart (verified via grep across the runtime tree -- the
      LIVE caller of an identically-named helper lives in heap_fcp.dart at
      lines 374 and 544 as a heap-fcp-internal static; commit.dart's copy
      is unreferenced).
    target_decision: >-
      Emit `private static void _WalkAndActivate(SuspensionListNode? list,
      IList<GoalRef> acts) { var current = list; while (current != null) {
      if (current.Armed) { acts.Add(new GoalRef(current.GoalId!.Value,
      current.ResumePC)); current.Record.Disarm(); } current = current.Next;
      } }` on `public static class CommitOps`. Decisions: (i) the method
      MUST be preserved verbatim in shape -- it is part of the source
      file's public-API-by-class-membership surface, even if currently
      unreferenced (the source author kept it -- the convspec preserves
      it); a future caller in commit.cs may use it. The .NET port
      preserves the symbol; codegen MUST NOT delete it as dead code (FR-013:
      no silent transformations beyond decisions recorded in this spec).
      (ii) private-by-underscore-prefix -> C# `private` access modifier
      (the Dart `_` prefix is a library-private contract; on the static-
      class re-housing, `private` is the strictly-correct narrowing). NOT
      `internal` (would change visibility from "private to this type" to
      "private to this assembly", a silent surface expansion). (iii)
      Suspension-list null-walk: the Dart `while (current != null) { ...;
      current = current.next; }` -> C# identical shape with NRT-aware null
      narrowing inside the loop (after `while (current != null)`, the
      compiler narrows `current` to non-null `SuspensionListNode` within
      the loop body, but NOT after `current = current.Next` -- the .NET
      flow analysis re-widens; the loop guard re-narrows on the next
      iteration; identical semantics). (iv) `current.goalId!` (Dart non-
      null assertion) -- the suspension.dart.md convspec decides whether
      `GoalId` on `SuspensionListNode` is `int?` (nullable value type, the
      Dart shape with the `disarm()` invariant "armed iff goalId != null")
      or non-nullable on a fresh-record-and-update model. Per
      suspension.dart at line 9 `int? goalId;`, the .NET port mirrors as
      `int? GoalId` (nullable value-type property); the `!` non-null
      assertion -> C# `current.GoalId!.Value` (the `!` null-forgiving
      operator removes the nullable-warning; `.Value` extracts the
      underlying int from the `Nullable<int>` struct). NOT `current.GoalId
      ?? 0` (would silently substitute 0 -- a wrong default; the armed-
      guard above guarantees non-null and the bang-then-Value is the
      faithful counterpart). (v) `acts.add(GoalRef(...))` -> `acts.Add(new
      GoalRef(...))` -- list-mutation with explicit `new` for the record-
      struct constructor (decided in machine_state.dart.md as `readonly
      record struct GoalRef(int Id, int Pc)`). Note: the constructor
      argument names in Dart are POSITIONAL (`GoalRef(int id, int pc)`);
      the .NET port preserves positional ordering. (vi) `current.record.
      disarm()` -> `current.Record.Disarm();` -- direct method call.
    idiom_id: null
    research_finding_id: rf-dart-nullable-int-fallback-to-csharp-equivalent
    nuance: >-
      Unreferenced-helper nuance (LOAD-BEARING, recorded per FR-013): this
      private static method is currently NOT called from anywhere in
      commit.dart (the live `_walkAndActivate` consumed by the runtime
      lives in heap_fcp.dart at lines 374 and 544 as a heap-fcp-internal
      helper; commit.dart's copy is structurally equivalent but
      unreferenced -- likely a legacy duplicate from an earlier refactor
      that moved the logic to heap_fcp). The .NET port preserves the
      symbol verbatim because (a) the Dart source preserves it (no
      `unreachable_code` or `unused_element` lint waiver implies the
      author considers it part of the file's contract); (b) the spec is
      SPEC-ONLY and must not silently delete code -- this is an
      observation, not a decision to remove; (c) a future caller in
      commit.cs (or a converted future test) may rely on the symbol's
      existence. Nullable-walk nuance: Dart `while (current != null) {
      ...; current = current.next; }` and C# identical-shape iteration
      have IDENTICAL semantics under NRT -- the C# compiler's flow
      analysis narrows `current` to non-null inside the loop body
      (https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
      and re-widens after the `current = current.Next` reassignment;
      same as Dart's promotion-and-loss. Non-null-assertion nuance: Dart
      `current.goalId!` is the bang operator for "I know this is non-null,
      compiler trust me"; C# `current.GoalId!.Value` is the analogous
      "null-forgiving operator + Nullable<T>.Value extract" -- the bang
      tells the NRT analyzer to suppress the warning, the `.Value` is the
      runtime extract from `int?`. The armed-guard above (`if
      (current.Armed)`) is the LOGICAL proof of non-nullness -- both
      languages enforce the guard by convention, not by type-narrowing
      (the `armed` getter returns `goalId != null` per suspension.dart
      line 20; the compiler in neither language can prove that the getter
      and the field are correlated; the bang/forgiving is the documented
      escape hatch). Value-vs-reference: `SuspensionListNode` is a
      reference-type class per suspension.dart.md (mutable `next`
      pointer); `GoalRef` is a value-type record-struct per
      machine_state.dart.md; `IList<GoalRef>` is a reference-type
      interface holding value-type elements. Param-type narrowing: Dart
      `List<GoalRef> acts` -> C# `IList<GoalRef> acts` (matches the public
      method's return-type narrowing and is the faithful contract; the
      method only calls `.Add(...)` on `acts`, which is in the `IList<T>`
      surface). Async / Stream: ABSENT.
  - construct_key: "dart.static_method.private.forwardSuspensions-suspension-list-forwarder"
    source_form: >-
      "`static void _forwardSuspensions(HeapFCP heap, SuspensionListNode?
      list, int targetWriterAddr) { var current = list; while (current !=
      null) { if (current.armed) { final newNode = SuspensionListNode(
      current.record); final targetCell = heap.cells[targetWriterAddr]; if
      (targetCell.content is SuspensionListNode) { newNode.next =
      targetCell.content as SuspensionListNode; } targetCell.content =
      newNode; } current = current.next; } }`" -- a private static helper
      that, for each armed node in `list`, allocates a new
      `SuspensionListNode` wrapping the same shared `record`, prepends it
      to the suspension chain currently stored on the target writer cell,
      and stores the new node as the new chain head. Currently NOT
      referenced anywhere in commit.dart (the LIVE forwarder lives in
      heap_fcp.dart at lines 436 and 519). Uses Dart `is` + `as` two-step
      idiom on `targetCell.content`.
    target_decision: >-
      Emit `private static void _ForwardSuspensions(HeapFCP heap,
      SuspensionListNode? list, int targetWriterAddr) { var current =
      list; while (current != null) { if (current.Armed) { var newNode =
      new SuspensionListNode(current.Record); var targetCell =
      heap.Cells[targetWriterAddr]; if (targetCell.Content is
      SuspensionListNode existingHead) { newNode.Next = existingHead; }
      targetCell.Content = newNode; } current = current.Next; } }` on
      `public static class CommitOps`. Decisions: (i) the method is
      preserved verbatim in shape -- same unreferenced-helper rationale
      as `_WalkAndActivate`. (ii) `SuspensionListNode(current.record)` ->
      `new SuspensionListNode(current.Record)` (constructor call with the
      shared record reference; the SuspensionListNode constructor decided
      in suspension.dart.md takes the record by reference). (iii) `(target
      Cell.content as SuspensionListNode)` (Dart is-then-as two-step) ->
      C# `is SuspensionListNode existingHead` binding pattern (single-step
      test-and-bind, per the same elimination logic as the re-deref loop
      above; the underlying idiom is rf-dart-as-cast-to-csharp-explicit-cast).
      (iv) `targetCell.content = newNode;` -- direct property setter
      mutation on the reference-type HeapCell (cross-spec invariant: the
      heap_fcp.dart.md convspec MUST keep HeapCell as a class; confirmed
      at heap_fcp.dart.md row 25). (v) `int targetWriterAddr` parameter
      type stays as the heap_fcp-spec's chosen addr-int width (per
      heap_fcp.dart.md the heap addresses are .NET `int` matching Dart
      `int` -- NO `long` widening here because address arithmetic stays
      in `int` per the runtime convention; codegen MUST consult
      heap_fcp.dart.md for the authoritative addr type).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Same unreferenced-helper nuance as `_WalkAndActivate` -- preserved
      verbatim per FR-013. Same as-cast elimination via binding-pattern
      (`is T t`) -- the underlying idiom recorded for traceability.
      Constructor-allocation nuance: `SuspensionListNode(current.record)`
      is a Dart constructor call without `new` (Dart 2+ made `new`
      optional); C# requires explicit `new`. Codegen MUST emit `new
      SuspensionListNode(...)`. Prepend-chain semantics: the helper
      builds a linked-list prepend in place -- new node's `next` is set
      to the previous head (if any), then the cell's `content` is updated
      to the new head; both languages execute this as two reference
      writes with identical observable effect (the linked-chain
      invariant -- "head is reachable from the cell, all old armed nodes
      are reachable from the head's next-chain" -- is preserved).
      Value-vs-reference: `SuspensionListNode` is a reference type; the
      shared `record` field is a reference to the same SuspensionRecord
      instance across multiple list nodes (the FCP-design "shared
      record" invariant per suspension.dart.md). Heap mutation: writes
      to `targetCell.Content` propagate via the reference-type HeapCell
      to all observers of the cell -- identical Dart semantics. Async:
      ABSENT.
  - construct_key: "dart.docblock_triple_slash.applySigmaHatFCP-and-private-helpers"
    source_form: >-
      Three triple-slash doc comments: "/// Apply tentative writer
      substitution σ̂w (FCP-exact two-cell semantics)" / "/// Per
      heap-pointer-architecture-spec.md v3.0: ..." on `applySigmaHatFCP`;
      "/// Walk suspension list and activate armed records" on
      `_walkAndActivate`; "/// Forward suspension list to target writer"
      / "/// Per heap-pointer-architecture-spec.md v3.0: Suspensions are
      stored on writer cells" on `_forwardSuspensions`. Plus the inline
      `//` line-comments scattered through the body explaining each
      sub-section of the binding loop.
    target_decision: >-
      Map the `///` doc-comments to C# XML-doc comments (`/// <summary>
      ...</summary>`) on each method; the body line-comments (`//`) map
      to C# line-comments (`//`) verbatim. Trivial mechanical mapping;
      the spec-reference text ("Per heap-pointer-architecture-spec.md
      v3.0: ...") MUST be preserved verbatim because it is load-bearing
      provenance pointing at the authoritative runtime specification.
      The σ̂w character (U+03C3 + U+0302 + U+0077) MUST be preserved
      verbatim in the C# source (C# source files are UTF-8 by convention;
      identifiers and string/comment content can carry any Unicode
      character); codegen MUST NOT transliterate it to "sigma_w" or
      similar.
    idiom_id: null
    research_finding_id: null
    nuance: Trivial. Doc-comment preservation is mechanical; the σ̂w preservation is the only non-trivial sub-decision and is covered above.
    trivial: true
conversion_units:
  - "four using directives at top of lib/runtime/commit.cs: one for each of the namespaces converted from machine_state.dart, heap_fcp.dart, suspension.dart, terms.dart (exact namespace names decided by the depgraph/namespace stage)"
  - "public static class CommitOps -- namespacing holder (sealed/abstract by virtue of `static`; no instances)"
  - "public static IList<GoalRef> ApplySigmaHatFCP(HeapFCP heap, SigmaHat sigmaHat) -- positional params (named-required call style preserved via C# named-argument syntax); body translated as the three-pass loop below"
  - "  - first pass: foreach over sigmaHat KeyValuePair; if (value is VarRef varRef && heap.IsWriter(varRef.Addr)) -- WxW pre-validation; throw new InvalidOperationException($\"WxW violation ... W{clauseWriterId} -> W{queryWriterAddr} (both unbound)\") on violation"
  - "  - var activations = new List<GoalRef>(); var writersWithCallbacks = new List<int>();"
  - "  - second pass: foreach over sigmaHat; null-skip; if (value is VarRef varRef0) {  ifs for IsReader-> BindWriterToReader+AddRange+continue / IsWriter-> DerefAddr+throw-or-rebind }; var valueAsTerm = value is Term t ? t : new ConstTerm(value); var acts = heap.BindWriterNoCallback(varId, valueAsTerm); activations.AddRange(acts); writersWithCallbacks.Add(varId);"
  - "  - third pass: foreach over sigmaHat.Keys; if (cell.Tag == CellTag.WrtTag && cell.Content is Pointer ptr) { var derefResult = heap.DerefAddr(ptr.TargetAddr); if (derefResult is Term derefTerm && derefResult is not VarRef) { cell.Content = derefTerm; cell.Tag = CellTag.ValueTag; } }"
  - "  - fourth pass: foreach (var writerAddr in writersWithCallbacks) heap.FirePendingCallback(writerAddr);"
  - "  - return activations; (implicit reference conversion List<GoalRef> -> IList<GoalRef>)"
  - "private static void _WalkAndActivate(SuspensionListNode? list, IList<GoalRef> acts) -- preserved verbatim; armed-guard + acts.Add(new GoalRef(current.GoalId!.Value, current.ResumePC)) + current.Record.Disarm(); current = current.Next loop. Currently unreferenced in commit.cs (mirrors source: an unreferenced helper retained for surface preservation)"
  - "private static void _ForwardSuspensions(HeapFCP heap, SuspensionListNode? list, int targetWriterAddr) -- preserved verbatim; armed-guard + linked-list prepend via `is SuspensionListNode existingHead` binding-pattern; currently unreferenced in commit.cs (mirrors source)"
  - "XML doc-comments preserved on every method (/// <summary>...</summary>); inline line-comments preserved as // in body; the spec-reference text 'Per heap-pointer-architecture-spec.md v3.0: ...' preserved verbatim; the σ̂w Unicode glyph in the public method's summary preserved verbatim (UTF-8 source)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-namespace-using -- four relative-import directives

The four `import '<sibling>.dart';` directives all bind a sibling Dart
library into this file's scope. The Dart language tour
(https://dart.dev/language/libraries) documents that an import names a
*library/file*; the C# language reference for the `using` directive
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
documents that a `using` names a *namespace*. The asymmetry is the same
recorded in the fairness.dart.md / abandon.dart.md / heap_fcp.dart.md
precedents and is reused verbatim here: each Dart import becomes a
single `using <namespace>;` line in the converted target file; the
exact namespace name is decided by the downstream depgraph/namespace
stage. There is no per-symbol narrowing form in C# (no `show`/`hide`
counterpart), and there is no file-to-file `using` form (no `using
"./machine_state.cs";`). Codegen MUST NOT invent either.

### rf-dart-static-only-holder-to-csharp-static-class -- CommitOps utility class

`CommitOps` is a Dart class containing exactly three static methods
(`applySigmaHatFCP`, `_walkAndActivate`, `_forwardSuspensions`), no
fields, no instance constructor, no instance members. The class
identifier is used purely as a namespace (`CommitOps.applySigmaHatFCP(...)`);
the class is never instantiated. Same structural pattern as the
`AbandonOps` class in abandon.dart, and the conversion rule is
identical: emit a C# `public static class CommitOps`. The
authoritative Microsoft Learn page for static classes
(https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
makes the no-instantiation contract a compile-time invariant ("A
static class cannot be instantiated... static classes are sealed and
therefore cannot be inherited"). This idiom is already established in
the runtime convspecs (abandon.dart.md and machine_state.dart.md both
re-house top-level bindings into static classes) and reused here
unchanged.

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults -- applySigmaHatFCP named-required params

The public method `applySigmaHatFCP` declares two named-required
parameters: `{required HeapFCP heap, required Map<int, Object?>
sigmaHat}`. Dart's `required` keyword on a named parameter is a
compile-time "callers must supply this argument by name" enforcement
(https://dart.dev/language/functions#named-parameters). C# has no
method-level `required` keyword (the C# 11 `required` modifier
applies to properties only,
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required).
The faithful counterpart is a positional parameter without a default
value -- the compiler enforces "must supply" on a positional-no-default
parameter (https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments).
Call-site readability is preserved by C#'s named-argument syntax
(`CommitOps.ApplySigmaHatFCP(heap: h, sigmaHat: s)`) -- documented in
the same Microsoft Learn page as the canonical .NET form.

This is the same rf-* idiom established in
machine_state.dart.md (GoalState constructor) and reused verbatim in
abandon.dart.md (`abandonWriter`); reused here for SC-007 (≥95% of
recurring constructs resolved via a recorded idiom, not re-derived).

### rf-dart-staterror-to-csharp-invalidoperationexception -- two throw sites

Two `throw StateError(...)` call sites in `applySigmaHatFCP`: one for
the WxW invariant violation, one for the σ̂w-contains-unbound-writer
invariant violation. Dart `StateError`
(https://api.dart.dev/dart-core/StateError-class.html) is documented as
"The operation was not allowed by the current state of the object";
.NET `InvalidOperationException`
(https://learn.microsoft.com/en-us/dotnet/api/system.invalidoperationexception)
is documented as "The exception that is thrown when a method call is
invalid for the object's current state" -- a one-to-one INTENT match,
modulo the Dart `Error`-vs-`Exception` hierarchy split which .NET does
not have (every throwable is `Exception` or derived). The mapping is
the established project idiom from heap_fcp.dart.md rows 662 and 869
and is reused verbatim. String-interpolation in the message is
covered by the rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
idiom: Dart `'$x'` and `'${x.field}'` become C# `$"{x}"` and
`$"{x.Field}"` (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated).

### rf-dart-is-not-type-test-to-csharp-is-not-pattern -- four type-test sites

Four sites use Dart `is`/`is!` type-tests with implicit promotion of
the tested expression to the narrowed type within the guarded scope:
(a) `value is VarRef` (binding-loop, outer), (b) `derefResult is Term
&& derefResult is! VarRef` (binding-loop, inner -- one positive + one
negative), (c) `cell.content is Pointer` (re-deref loop), (d)
`derefResult is Term && derefResult is! VarRef` (re-deref loop,
inner). Plus (e) `value is Term ? value : ConstTerm(value)` (ternary
type-test). Plus (f) `targetCell.content is SuspensionListNode` in
`_forwardSuspensions`. All map to C# binding patterns
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#type-pattern)
of the form `expr is T t` (test + bind in one step), and `is!` ->
`is not` (C# 9 logical-not pattern,
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#logical-patterns).
This is the same idiom established in heap_fcp.dart.md row 1061 and
reused verbatim. The binding-pattern form is preferred over the
two-step `is T` + `(T)expr` cast because it (a) eliminates the
redundant cast, (b) prevents a null-temporary on mismatch (`is T t`
fails closed; the older `as T` cast returns null on mismatch),
(c) mirrors the source's `is`-with-promotion shape cleanly.

### rf-dart-as-cast-to-csharp-explicit-cast -- two as-cast sites

Two sites use Dart `as`-casts: (a) `(cell.content as Pointer).
targetAddr` in the re-deref loop, and (b) `targetCell.content as
SuspensionListNode` in `_forwardSuspensions`. Both follow a guarded
`is`-test of the same expression, so the cast cannot fail at runtime;
the .NET port collapses the two-step idiom into a single binding-
pattern (`is Pointer ptr` / `is SuspensionListNode existingHead`)
per the elimination logic recorded in heap_fcp.dart.md row 1081 (the
same idiom-name is preserved for traceability). The underlying
semantic decision is "Dart `as` cast -> .NET strongly-typed access";
the syntactic refinement (pattern vs explicit cast) is a codegen-
level concern.

### rf-dart-nullable-int-fallback-to-csharp-equivalent -- _walkAndActivate non-null assertion

`current.goalId!` in `_walkAndActivate` is a Dart bang-operator
non-null assertion: the developer asserts that `goalId` is non-null
at this point (proven by the armed-guard `if (current.armed)` above,
which per suspension.dart line 20 `bool get armed => goalId != null`
is the documented proof). The .NET port maps to `current.GoalId!.Value`
where `!` is the C# null-forgiving operator
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving)
which suppresses the NRT analyzer warning, and `.Value` extracts the
underlying `int` from the `Nullable<int>` struct
(https://learn.microsoft.com/en-us/dotnet/api/system.nullable-1.value).
This is the faithful counterpart: the bang-operator semantics in
Dart are "I assert non-null; if I'm wrong, throw `TypeError` at the
access"; the C# `!` + `.Value` semantics are "I assert non-null at
compile-time; if I'm wrong, throw `InvalidOperationException` at the
`.Value` access". Both languages share the "compile-time assertion
+ runtime trap on violation" contract.

The reuse of `rf-dart-nullable-int-fallback-to-csharp-equivalent`
(established by an earlier convspec elsewhere in the project) is the
closest existing project idiom for nullable-int handling and is
reused here for consistency. The armed-guard logical invariant
(armed iff goalId != null) is NOT something either compiler can
prove from the property-getter shape; both languages rely on the
explicit operator to bypass the analyzer.

## Notes

- File is 131 lines, one Dart class with 1 public + 2 private static
  methods, no constructor, no fields. The public method
  `applySigmaHatFCP` is the load-bearing FCP writer-substitution
  applicator (per heap-pointer-architecture-spec.md v3.0); the two
  private helpers are currently unreferenced in commit.dart (verified)
  -- the live implementations live in heap_fcp.dart. The convspec
  preserves all three symbols verbatim per FR-013 (no silent deletion).
- The σ̂w Unicode glyph (Greek sigma with combining circumflex, U+03C3
  U+0302) appears in both the doc-comment and one of the exception
  messages. C# source files are UTF-8 by convention; the glyph must
  be preserved verbatim in both contexts. Codegen MUST NOT transliterate
  it.
- Cross-spec invariants this spec relies on (forward references):
  (1) machine_state.dart.md: `GoalRef` is a `readonly record struct`
      with positional `(int Id, int Pc)` -- the constructor call
      `new GoalRef(GoalId!.Value, ResumePC)` in `_WalkAndActivate`
      depends on this shape.
  (2) machine_state.dart.md: `SigmaHat` is a global-using alias for
      `Dictionary<int, object?>` -- the method-parameter type uses
      the alias, not the expanded form.
  (3) heap_fcp.dart.md: `HeapCell` is a reference-type `class` with
      mutable `Content` and `Tag` properties -- the re-deref loop's
      in-place mutation relies on reference semantics.
  (4) heap_fcp.dart.md: `HeapFCP` is a reference-type `class` with
      PascalCase method names (`IsWriter`, `IsReader`, `IsFullyBound`,
      `DerefAddr`, `BindWriterToReader`, `BindWriterNoCallback`,
      `FirePendingCallback`) and a `Cells` indexer/property surface.
  (5) suspension.dart.md: `SuspensionListNode` and `SuspensionRecord`
      are reference-type classes; the `Armed`/`GoalId`/`ResumePC`/
      `Record`/`Next` properties are PascalCase; `Disarm()` is a void
      method.
  (6) terms.dart.md: `Term`, `VarRef`, `ConstTerm` are reference-type
      classes; `VarRef.Addr` is the int field; `new ConstTerm(value)`
      wraps a `object?` value.
- File-absent nuances (deliberately not asserted per FR-009): no `async`/
  `Future`/`Stream`/`isolate`/`Completer`, no `late` declarations, no
  `mixin`, no `extension`, no `sealed`, no generics-with-bounds, no
  bitwise/shift operations, no value-class equality override on
  CommitOps, no `IDisposable`/resource-management, no `record` or
  value-type within CommitOps's own surface. The file is purely a
  synchronous static-method holder operating on the heap reference
  passed in.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and .NET documentation (Microsoft Learn for .NET,
  api.dart.dev / dart.dev/language for Dart), with the established
  project idioms (`relative-import`, `static-class`, `named-required`,
  `staterror -> invalidoperationexception`, `is-not -> is-not pattern`,
  `as-cast -> binding pattern`, `nullable-int fallback`, `string
  interpolation`) reused verbatim per SC-007. No idiom-vs-research or
  idiom-vs-idiom conflicts detected.
- The two private static methods (`_WalkAndActivate`,
  `_ForwardSuspensions`) being currently unreferenced in commit.dart
  is recorded as an observation, NOT an escalation: the conversion
  decision is unambiguous (preserve the symbols verbatim per FR-013),
  and the dead-code analysis is out of scope for this spec (a later
  code-cleanup pass may remove either side, but the conversion stage
  is faithful by design).
