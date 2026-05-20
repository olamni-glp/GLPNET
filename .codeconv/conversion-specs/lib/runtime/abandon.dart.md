> Conversion-spec artifact for lib/runtime/abandon.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/abandon.dart
source_sha256: 2171582c81708de2497bd8a474d148beec0ca20dbfb4f687e5e7b4dfc4a2653a
target_code_unit: lib/runtime/abandon.cs
constructs:
  - construct_key: dart.utility_class.static_only_holder
    source_form: >-
      "class AbandonOps { static List<GoalRef> abandonWriter({required int
      writerId}) { throw UnimplementedError('Abandon operation not
      implemented in FCP design'); } }" — a Dart class containing exactly
      one static method, no fields, no instance constructor, no instance
      members. Used as a namespacing container for FCP "abandon"
      operations.
    target_decision: >-
      Emit a C# `public static class AbandonOps` (sealed, abstract,
      cannot be instantiated — the .NET counterpart of a Dart
      "static-methods-only" holder class per Microsoft Learn "Static
      Classes and Static Class Members": "A static class is basically the
      same as a non-static class, but there's one difference: a static
      class can't be instantiated. In other words, you can't use the new
      operator to create a variable of the class type. Because there's no
      instance variable, you access the members of a static class by
      using the class name itself"). Containing exactly the one static
      member `AbandonWriter`. A non-static class with all-static members
      is REJECTED here because (a) the Dart source's class is callable
      only via `AbandonOps.abandonWriter(...)` (never instantiated) and
      (b) `static class` makes the no-instantiation contract a compile-
      time guarantee on the .NET side, matching the source's design
      intent. Do NOT emit a `namespace`-only translation (free-floating
      static functions): C# does not permit top-level free functions
      outside a type, and the Dart source explicitly groups via a class
      identifier `AbandonOps` that the conversion preserves as a callable
      identifier. Reference/value semantics: a static class has no
      instances → the value-vs-reference distinction does not apply at
      the class level (only at the member-signature level, handled per
      member).
    idiom_id: null
    research_finding_id: rf-dart-static-only-holder-to-csharp-static-class
    nuance: >-
      Static-class contract (explicitly addressed): in Dart, a class with
      only static members is still an instantiable type (`AbandonOps()`
      would compile and yield a zero-field instance); the convention is
      "don't instantiate it, treat the class name as a namespace". C#'s
      `static class` makes that convention a compile-time invariant
      (Microsoft Learn: "a static class can't be instantiated") — a
      strictly tighter contract than the Dart source. The narrowing is
      strictly correct here because the Dart source never instantiates
      `AbandonOps` and has no instance state to preserve. Value-vs-
      reference: static classes have no instances, so neither value nor
      reference semantics apply to the type itself; the per-method
      signature governs argument/return semantics (handled by the
      construct below). No `Stream`/`Future`/async/isolate concerns
      (synchronous static dispatch). Null-safety at the type level: not
      applicable (no instances are ever held or compared). The
      .NET-idiomatic narrowing also disables the `:base()` /
      `:this()` pitfalls and forbids subclassing (a `static class` is
      implicitly sealed), reinforcing the source's "this is a
      namespacing container, not a polymorphism surface" intent.
  - construct_key: dart.static_method.named_required_param_returning_list_throws_unimplemented
    source_form: >-
      "static List<GoalRef> abandonWriter({required int writerId}) {
      throw UnimplementedError('Abandon operation not implemented in FCP
      design'); }"
    target_decision: >-
      Emit a `public static IList<GoalRef> AbandonWriter(long writerId)`
      (or `List<GoalRef>` — see nuance for the interface-vs-concrete
      choice; spec default = `IList<GoalRef>` to expose the most
      restrictive faithful contract that still matches Dart `List<T>`).
      Body: `throw new NotImplementedException("Abandon operation not
      implemented in FCP design");`. The Dart `{required int writerId}`
      named-required parameter maps to a plain C# positional parameter
      `long writerId` (no default) — per the established project idiom
      `dart.named_required_parameters.required_kwargs` (from
      moded_term.dart construct): C# has no per-parameter `required`
      keyword at the constructor/method level (`required` is a member-
      modifier only); a positional parameter without a default IS
      required by the compiler, and the call-site readability of
      `AbandonOps.AbandonWriter(writerId: 42)` is preserved by C#'s
      named-argument call syntax (Microsoft Learn: "Named arguments
      enable you to specify an argument for a parameter by matching the
      argument with its name"). `int` widens to `long` per the recurring
      project idiom `dart.int.to.csharp.long_width` (from opcodes.dart /
      error.dart precedent). Return-type element `GoalRef` is the C#
      type produced by the sibling spec for `machine_state.dart` —
      same identifier preserved. The body is a single throwing
      statement; no executable conversion semantics beyond the throw.
    idiom_id: null
    research_finding_id: rf-dart-unimplemented-error-to-csharp-notimplemented
    nuance: >-
      Three well-known nuances explicitly addressed here. (1) Exception-
      class nuance (the load-bearing decision): Dart `UnimplementedError`
      extends `Error` (a programming-defect signal, per
      api.dart.dev/dart-core/UnimplementedError-class.html: "Thrown by
      operations that have not been implemented yet"); .NET
      `NotImplementedException` derives from `SystemException` (Microsoft
      Learn: "The exception that is thrown when a requested method or
      operation is not implemented"). .NET has no `Error` vs `Exception`
      hierarchy split — every throwable is `Exception` or derived. The
      mapping is by INTENT (both signal "this method is intentionally
      not implemented in this layer"), not by class-hierarchy — same
      basis as the prior boot_loader.dart spec (rf-dart-unimplemented-
      error-to-csharp-notimplemented). (2) Named-required nuance: Dart
      `{required int writerId}` → C# positional `long writerId` with
      named-argument call style preserved at sites; documented as the
      reuse of `dart.named_required_parameters.required_kwargs`. (3)
      Return-type collection nuance: Dart `List<T>` is a mutable ordered
      collection (api.dart.dev: "An indexable collection of objects with
      a length"); C# has `List<T>` (concrete, BCL `System.Collections.
      Generic.List<T>`) and `IList<T>` (the interface). The faithful
      counterpart is the abstract contract `IList<T>` for a return type
      that the caller will iterate / index (Microsoft Learn `IList<T>`:
      "Represents a non-generic collection of objects that can be
      individually accessed by index"); using the concrete `List<T>`
      would leak the implementation type. Spec default = `IList<GoalRef>`;
      codegen MAY emit `List<GoalRef>` if a future call-site needs a
      method exclusive to the concrete `List<T>` (none observed in this
      file — the method only ever throws, so no observable consumer
      shape exists yet). Value-vs-reference at the signature level:
      `IList<GoalRef>` is a reference type (heap-allocated interface
      reference), matching Dart `List<GoalRef>` (reference type). `long
      writerId` is a value type in C# (`Int64`), matching Dart `int`'s
      pass-by-value semantics. Null-safety: under enabled NRT, the
      return type is non-nullable `IList<GoalRef>` (matches Dart's
      non-nullable `List<GoalRef>` in null-safe mode); the parameter
      `long writerId` is a non-nullable value type (matches Dart `int`).
      The body's `throw` means the non-null return contract is never
      observably reached — but the type declaration must still be
      faithful to the source signature (FR-013 / spec-faithfulness).
      No async/Stream/Future/isolate semantics in scope (synchronous
      throw).
  - construct_key: dart.docblock_triple_slash
    source_form: >-
      "/// FCP-exact design: Abandon operation not yet implemented" and
      "/// TODO: Implement FCP-compatible abandon semantics" — two
      triple-slash doc lines on the static method.
    target_decision: >-
      Map to C# XML-doc comments on the method: `/// <summary>FCP-exact
      design: Abandon operation not yet implemented.</summary>
      <remarks>TODO: Implement FCP-compatible abandon semantics.
      </remarks>` (or two adjacent `///` lines under a single `<summary>`
      element — codegen's call). Trivial mechanical mapping; the TODO
      semantics are preserved in source for the eventual
      implementer. Trivial.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: dart.import.relative_sibling_module
    source_form: "import 'machine_state.dart';"
    target_decision: >-
      Dart relative imports do not have a syntactic counterpart in C#;
      the `GoalRef` identifier becomes resolvable via the C# `using` of
      the namespace produced for `lib/runtime/machine_state.cs` (codegen
      stage emits the appropriate `using ...;` once it knows the target
      namespace — out of scope for this per-file spec, deferred to the
      project-wide namespace decision recorded in glp_runtime.dart.md /
      machine_state.dart.md). Trivial mechanical mapping at the
      identifier-resolution level. Trivial.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "public static class AbandonOps (sealed/abstract by virtue of `static`; no instances; namespacing holder)"
  - "public static IList<GoalRef> AbandonWriter(long writerId) — single positional parameter (named-required call style preserved via C# named arguments); body throws new NotImplementedException(\"Abandon operation not implemented in FCP design\")"
  - "doc-comments → /// <summary>...</summary>/<remarks>TODO...</remarks> on the static method"
  - "import 'machine_state.dart' → using of the namespace produced for machine_state.cs (project-wide namespace decision; deferred)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-static-only-holder-to-csharp-static-class — static-only utility class

- Deep analysis: `AbandonOps` is a Dart class containing exactly one
  `static` method, no fields, no instance constructor, no instance members.
  The class identifier is used purely as a namespace
  (`AbandonOps.abandonWriter(...)`). The class is never instantiated in
  this file, and the source's intent ("this is a holder for FCP abandon
  operations, not a stateful object") is preserved exactly by C#'s `static
  class` keyword, which compile-time-enforces the no-instantiation
  contract.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
  (Microsoft Learn). Verbatim: "A static class is basically the same as a
  non-static class, but there's one difference: a static class can't be
  instantiated. In other words, you can't use the new operator to create a
  variable of the class type. Because there's no instance variable, you
  access the members of a static class by using the class name itself."
  Also verbatim: "the C# compiler will ensure that instances of this class
  cannot be created" and "Static classes are sealed and therefore cannot
  be inherited." Both invariants (no instances, no subclasses) are
  desirable here — the Dart source has zero instances and zero subclasses,
  and the .NET narrowing makes both invariants compile-time guarantees.
- Authoritative Dart (corroboration that the source-class is a static-only
  holder by convention, not by language feature): the Dart language tour
  (`https://dart.dev/language/classes`) documents classes as
  instantiable-by-default; Dart has no `static class` keyword. The source
  follows the conventional Dart idiom "class with only static members,
  never instantiated" — the conversion lifts the convention to a compile-
  time invariant on the .NET side. Authoritative both sides; no
  escalation.

### rf-dart-unimplemented-error-to-csharp-notimplemented — platform-stub throw

- Deep analysis: `abandonWriter` exists to satisfy the `AbandonOps` API
  surface (callable from FCP-exact callers that expect the method to
  exist) but signals that the FCP-compatible abandon semantics are not
  yet implemented in this layer. The body is a single throwing statement
  with a human-readable explanation; no executable conversion semantics
  beyond the throw.
- Authoritative Dart: WebFetch
  `https://api.dart.dev/dart-core/UnimplementedError-class.html` (Dart
  official). Verbatim: "Thrown by operations that have not been
  implemented yet." Extends `Error` — Dart's programming-defect signal
  (distinct from recoverable `Exception`).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.notimplementedexception`
  (Microsoft Learn). Verbatim: "The exception that is thrown when a
  requested method or operation is not implemented." Derives from
  `SystemException`; semantically a defect indicator. The .NET
  Error-vs-Exception hierarchy has no analogue of Dart's `Error`
  superclass split — every throwable is `Exception` or derived. The
  mapping is by INTENT (both signal "intentionally not implemented in
  this layer"), not by class-hierarchy. The same idiom basis was
  established in the prior boot_loader.dart spec for
  `_readFile` — explicitly reused here for consistency
  (SC-007).
- Authoritative .NET (named arguments): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`
  (Microsoft Learn). Verbatim: "Named arguments enable you to specify an
  argument for a parameter by matching the argument with its name rather
  than with its position in the parameter list." This is the basis for
  the Dart `{required int writerId}` → C# positional `long writerId` +
  call-site `AbandonWriter(writerId: 42)` mapping — the
  `dart.named_required_parameters.required_kwargs` idiom established in
  the moded_term.dart spec is reused verbatim (SC-007: ≥95% of recurring
  constructs resolved via a recorded idiom, not re-derived).
- Authoritative .NET (collection-return type): WebFetch
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ilist-1`
  (Microsoft Learn). Verbatim under `IList<T>`: "Represents a collection
  of objects that can be individually accessed by index." This is the
  faithful counterpart of Dart `List<T>` (indexable mutable collection,
  per api.dart.dev/dart-core/List-class.html: "An indexable collection of
  objects with a length"). Both interfaces expose `Add`/`Insert`/indexer/
  `Count` — the contract preserved exactly. Concrete `List<T>` would also
  work but leaks the implementation type; the spec default chooses
  `IList<T>` as the strictly-faithful contract.
- Authoritative Dart (List corroboration): WebFetch
  `https://api.dart.dev/dart-core/List-class.html` — verbatim "An
  indexable collection of objects with a length" and "Lists are
  Iterable. Their iteration order is just the sequence of indices."
  Matches the `IList<T>` contract verbatim.
- Conclusion: emit `public static IList<GoalRef> AbandonWriter(long
  writerId) { throw new NotImplementedException("Abandon operation not
  implemented in FCP design"); }` inside `public static class
  AbandonOps`. Three idiom bases reused (static-class, named-required,
  unimplemented→notimplemented) plus one width-widening idiom (`int →
  long`) carried forward from the project's recurring numeric-width
  convention. Authoritative both sides; no escalation.

## Notes

- File-absent nuances (deliberately not asserted): no `Stream`/`Future`/
  async/`isolate`, no `late`, no `mixin`, no `extension`, no generics-
  with-bounds, no `sealed` (the C# `static` modifier already implies
  sealed), no bitwise/shift operations, no nullable-of-value-type
  scenarios, no value-equality contract, no `IDisposable`/resource-
  management. The file is purely a synchronous static throw-stub.
- Load-bearing semantic decisions:
  (1) `AbandonOps` → `static class AbandonOps` (Microsoft Learn explicit
  no-instantiation invariant, strictly-correct narrowing of Dart's
  convention).
  (2) `UnimplementedError` → `NotImplementedException` (intent-based
  mapping, since .NET has no `Error` vs `Exception` hierarchy split).
  (3) `{required int writerId}` → positional `long writerId` (named-
  required idiom reuse + int-width widening).
  (4) `List<GoalRef>` return → `IList<GoalRef>` (faithful contract over
  concrete implementation type).
- All four non-trivial constructs cite both a deep-analysis basis and a
  researched authoritative source (Microsoft Learn for .NET, api.dart.dev
  for Dart) per SC-006. The two trivial constructs (doc comments,
  relative import) are marked `trivial: true` and skip research per the
  contract.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and .NET documentation, with the established
  project idioms (`static-class`, `named-required`, `unimplemented→
  notimplemented`, `int→long`, `List→IList`) reused per SC-007.
