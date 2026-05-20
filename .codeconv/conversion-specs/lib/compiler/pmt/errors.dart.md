> Conversion-spec artifact for lib/compiler/pmt/errors.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/compiler/pmt/errors.dart
source_sha256: 37c3d4a451199f6d875bcfbadc4d5b9b5bd80ed139d6d833de4e479dac9e3339
target_code_unit: lib/compiler/pmt/errors.cs
constructs:
  - construct_key: dart.value_class.error_record_final_fields_message_line_column_tostring_override
    source_form: >-
      class PmtError { final String message; final int line; final int column;
      PmtError(this.message, this.line, this.column); @override String
      toString() => 'PMT Error at $line:$column: $message'; @override bool
      operator ==(Object other) => other is PmtError && message==other.message
      && line==other.line && column==other.column; @override int get hashCode
      => Object.hash(message, line, column); }
    target_decision: >-
      Emit a `sealed` reference-type C# class `PmtError` (NOT a record, NOT an
      exception). Three get-only auto-properties `Message` (string, non-
      nullable), `Line` (int), `Column` (int), initialised from a single
      positional constructor `PmtError(string message, int line, int column)`.
      Manually implement value equality: `IEquatable<PmtError>` with a type-
      specific `Equals(PmtError? other)` comparing the three components,
      `override Equals(object? obj)` delegating to it, `override GetHashCode()`
      built with `HashCode.Combine(Message, Line, Column)` (mirrors Dart
      `Object.hash` semantics — order-sensitive structural hash of three
      values), and `override ToString()` returning the interpolated string
      `"PMT Error at {Line}:{Column}: {Message}"`. Per Microsoft Learn ("How
      to define value equality for a class or struct"), also overload `==`
      and `!=` for consistency with `Equals`, with the documented null-safe
      pattern (both-null → true, one-null → false, otherwise delegate to
      `Equals`). The class is declared `sealed` so the documented
      `this.GetType() != p.GetType()` symmetry concern (the polymorphic-
      equality pitfall in the same doc) is moot — there are no subclasses.
      A record (positional or otherwise) is REJECTED here: not because the
      three members would mis-compare (`string`/`int` are fine under record
      equality), but to keep the conversion idiom uniform with the established
      project precedent (lib/analysis/type_checker/type_ast.dart construct
      `dart.value_class.manual_eq_hashcode_with_list_element_equality` — a
      hand-rolled `IEquatable<T>` class) and because the Dart source already
      hand-wrote `==` / `hashCode`, signalling explicit value-equality intent
      that the conversion should preserve verbatim, not silently relax to
      compiler-synthesised record equality.
    idiom_id: null
    research_finding_id: rf-csharp-class-value-equality-iequatable
    nuance: >-
      Value-vs-reference is the load-bearing nuance. Dart `PmtError` is a
      reference object that hand-overrides `==`/`hashCode` → it has value
      equality semantics layered on reference identity. The C# counterpart
      must preserve that exact contract: `IEquatable<PmtError>` so HashSet /
      Dictionary keys / `errors.Contains(...)` work content-wise. The Dart
      contract "Hash codes must be the same for objects that are equal to each
      other according to operator ==" (Dart core API `Object.hashCode`) maps
      directly onto Microsoft Learn rule 4 ("two objects that have value
      equality produce the same hash code"). Null-safety: Dart `String` is
      non-nullable in null-safe mode (no `?` here) → C# `string` under enabled
      NRT (not `string?`). `Object` in the `==` signature → `object?` in the
      override. String interpolation `'PMT Error at $line:$column: $message'`
      is a faithful syntactic translation to `$"PMT Error at {Line}:{Column}:
      {Message}"` — no culture-sensitive formatting issue because the
      interpolated members are plain ints and an already-formed string
      (composite formatting of `int` defaults to invariant decimal digits in
      both languages for these small values, matching Dart's `int.toString`).
  - construct_key: dart.exception_aggregate_class.implements_Exception_list_of_errors_tostring_with_empty_branch
    source_form: >-
      class PmtErrors implements Exception { final List<PmtError> errors;
      PmtErrors(this.errors); @override String toString() { if
      (errors.isEmpty) return 'PmtErrors: (none)'; return
      'PmtErrors:\n${errors.map((e) => '  $e').join('\n')}'; } }
    target_decision: >-
      Emit a C# user-defined exception `class PmtErrors : Exception` (derives
      from `System.Exception` — Microsoft Learn "How to: Create user-defined
      exceptions": "if none of the predefined exceptions meet your needs, you
      can create your own exception class by deriving from the Exception
      class"). Provide a single primary constructor
      `PmtErrors(IReadOnlyList<PmtError> errors)` that stores the list in a
      get-only property `Errors` AND forwards a synthesised summary to
      `base(message)` so that `Exception.Message` is non-empty (use the same
      formatted summary as `ToString`, computed once and passed via a private
      static factory helper). Override `ToString()` with the empty-branch
      logic: when `Errors.Count == 0` return `"PmtErrors: (none)"`; otherwise
      return `"PmtErrors:\n"` followed by each error joined with `"\n"`, each
      element prefixed by two spaces (mirrors Dart `errors.map((e) => '  $e').
      join('\n')`). The interior `'  $e'` invokes `PmtError.ToString()` via
      `object`-to-string interpolation — preserved verbatim because both
      languages resolve `$"{e}"` / `'$e'` to a virtual `ToString` call. The
      three constructors suggested by Microsoft Learn (parameterless,
      `(string)`, `(string, Exception)`) are NOT applied here: the Dart class
      has exactly one logical constructor (taking the list); the .NET
      "implement three common constructors" guidance is a recommendation, not
      a contract — adding constructors that have no Dart counterpart would
      enlarge the surface beyond the source. Storing the list reference (not
      cloning) preserves Dart's semantics: `PmtErrors(this.errors)` aliases
      the caller's list; the C# property holds the same reference behind
      `IReadOnlyList<PmtError>` (read-only view; the caller's mutable
      `List<PmtError>` underneath is unchanged).
    idiom_id: null
    research_finding_id: rf-dart-exception-marker-to-csharp-exception-subclass
    nuance: >-
      Dart `Exception` is a MARKER INTERFACE (Dart core API `Exception`
      class: "A marker interface implemented by all core library exceptions"
      — no fields, no message contract, no stack trace built in). Implementing
      it conveys throwability and a programmatic catch type, nothing more.
      The .NET equivalent of "this type is throwable + catchable + has a
      message" is `class : Exception`, NOT an interface — `System.Exception`
      is the universal throwable base in .NET and there is no marker-interface
      idiom for exceptions ("user-defined exceptions … by deriving from the
      Exception class"). So Dart `implements Exception` → C# `: Exception`
      (inheritance, not interface implementation). Critical behavioural
      nuance: in Dart `toString()` is the only message-bearing surface; in
      .NET `Exception.Message` is the property reflected by debuggers,
      logging, and `WhenAll`-aggregated exception printers — so the
      conversion MUST seed `base(message)` with the same formatted summary,
      not leave `Message` as the default `"Exception of type 'PmtErrors' was
      thrown."`. `ToString` is still overridden for parity with the Dart
      source, but the seeded `base(message)` is what makes the C# exception
      observably equivalent under standard .NET tooling. List aliasing (not
      deep-copy) is preserved: Dart `this.errors` shares the caller's list;
      `IReadOnlyList<PmtError>` over the same reference matches.
conversion_units:
  - "class PmtError (sealed; get-only Message/Line/Column properties; positional ctor; IEquatable<PmtError> Equals + Equals(object?) + GetHashCode via HashCode.Combine + ToString override + == / != operator overloads with null-safe pattern)"
  - "class PmtErrors (derives from System.Exception; Errors IReadOnlyList<PmtError> get-only property; primary ctor seeding base(message) with the synthesised summary via a private static formatter; ToString override with empty-branch and joined-with-two-space-indent branches)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-csharp-class-value-equality-iequatable — value equality on PmtError

- Deep analysis: the Dart source hand-writes BOTH `operator ==` (member-wise
  comparison of `message`, `line`, `column`) and `hashCode` (`Object.hash` of
  the same three components). That is a deliberate, explicit declaration of
  value equality, not record-syntax sugar. Plus `toString` is overridden for
  human-readable formatting. The faithful C# counterpart is the textbook
  reference-type-with-value-equality recipe (`IEquatable<T>` on a `sealed`
  class with manual `Equals` / `GetHashCode` / `==` / `!=`), NOT a `record`
  (records would also work for these three primitive members, but the
  project's established precedent is hand-rolled `IEquatable<T>` for any
  Dart class that hand-rolled `==`/`hashCode`, and silently switching to a
  record would mask the explicit Dart intent).
- Authoritative .NET: WebFetch
  https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type
  — verbatim guidance "both classes and structs require the same basic
  steps for implementing equality: 1. Override the virtual
  Object.Equals(Object) method ... 2. Implement the System.IEquatable<T>
  interface by providing a type-specific Equals method ... 3. Optional but
  recommended: Overload the == and != operators ... 4. Override
  Object.GetHashCode so that two objects that have value equality produce
  the same hash code." Further verbatim "On classes (reference types), the
  default implementation of both Object.Equals(Object) methods performs a
  reference equality comparison, not a value equality check. When an
  implementer overrides the virtual method, the purpose is to give it value
  equality semantics." The doc also gives the null-safe `==` operator
  template (both-null → true, one-null → false, otherwise delegate to
  `Equals`) which the spec adopts verbatim. The `sealed` modifier addresses
  the polymorphic-equality pitfall the same doc highlights (`p1 = new
  ThreeDPoint(...) as TwoDPoint` mis-compare): with no subclasses possible,
  the runtime-type symmetry concern vanishes.
- Authoritative Dart (corroboration of the contract being preserved):
  WebFetch https://api.dart.dev/dart-core/Object/hashCode.html — verbatim
  "Hash codes must be the same for objects that are equal to each other
  according to operator ==" and "If operator == is overridden to use the
  object state instead, the hash code must also be changed to represent
  that state." This is the same contract Microsoft Learn rule 4 expresses,
  so the round-trip preserves semantics exactly. Authoritative both sides;
  no escalation.

### rf-dart-exception-marker-to-csharp-exception-subclass — exception modelling

- Deep analysis: `PmtErrors` is an aggregate exception that bundles a list
  of `PmtError` records and renders them. Dart `implements Exception`
  declares it as a throwable type; the only behaviour it gives is the right
  to be `throw`n and `catch`-ed as an `Exception`. In .NET, there is no
  marker-interface idiom for throwables — every throwable derives from
  `System.Exception`. So the conversion is `class PmtErrors : Exception`,
  with the `toString()` override translated to `ToString` and the synthesised
  message also fed to `base(message)` so the standard .NET `Message`
  property is informative.
- Authoritative Dart: WebFetch
  https://api.dart.dev/dart-core/Exception-class.html — verbatim "A marker
  interface implemented by all core library exceptions" and "is intended to
  convey information to the user about a failure, so that the error can be
  addressed programmatically. It is intended to be caught, and it should
  contain useful data fields." Confirms `Exception` provides NO fields, NO
  message contract, NO stack trace — purely a catchable-type marker.
- Authoritative .NET: WebFetch
  https://learn.microsoft.com/en-us/dotnet/standard/exceptions/how-to-create-user-defined-exceptions
  — verbatim ".NET provides a hierarchy of exception classes ultimately
  derived from the Exception base class. However, if none of the predefined
  exceptions meet your needs, you can create your own exception class by
  deriving from the Exception class." The doc recommends three constructors
  (`()`, `(string)`, `(string, Exception)`) and the `Exception`-suffixed
  naming convention; the suffix is already satisfied (`PmtErrors`). The
  three-constructor recommendation is intentionally NOT applied verbatim
  here — the Dart class has one logical constructor (list-of-errors); a
  faithful conversion preserves that single entry point. The seeded
  `base(message)` covers the `(string)` overload's intent (informative
  `Message`) without adding constructors the Dart source does not have.
  Authoritative both sides; no escalation.

## Notes

- No isolates, no async/Stream/Future, no late, no sealed-hierarchy
  modelling beyond `sealed class PmtError` for equality-symmetry: the file
  is purely synchronous value-type + exception. Those well-known nuances
  are absent and correctly not asserted.
- The interior `'  $e'` interpolation invokes `PmtError.ToString()`
  polymorphically in both languages — no semantic shift; `ToString` on the
  C# side is a virtual override of `object.ToString`, dispatched the same
  way Dart dispatches `toString`.
- List storage: `PmtErrors(this.errors)` aliases the caller's list (no
  copy). The C# `Errors` property holds the same reference, exposed as
  `IReadOnlyList<PmtError>` (read-only VIEW over the caller's mutable
  list — preserving Dart `final List<PmtError> errors` semantics where the
  reference is final but the contents are not, mirrored from the
  established `dart-list-element-value-equality` precedent in
  type_ast.dart.md). No deep copy is introduced because the Dart source
  does not perform one.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or .NET official documentation, with deep-analysis
  AND researched-pattern bases recorded (SC-006).
