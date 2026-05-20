# Conversion Spec — lib/analysis/type_checker/prelude.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/prelude.dart
source_sha256: a2cc710565ab37de28ec936b315c082d6c9b766c0fc3f59861b98f5724281bde
target_code_unit: lib/analysis/type_checker/prelude.cs
constructs:
  - construct_key: dart.toplevel.const-string-field
    source_form: "const String typePrelude = '';"
    target_decision: >-
      const string field on a static container class (public const string
      TypePrelude = "";). Empty string literal maps directly; const because
      the value is a compile-time string constant (allowed for string in C#).
    idiom_id: dart-toplevel-const-string-to-csharp-const-string
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Dart has top-level members; C# requires every member inside a type. The
      Dart `const String` is a compile-time constant — C# `const` is permitted
      for `string` (unlike collections). No value/reference concern: string is
      an immutable reference type interned by the CLR.
  - construct_key: dart.toplevel.const-set-string
    source_form: "const Set<String> predefinedTypeNames = { 'Number', ... };"
    target_decision: >-
      static readonly FrozenSet<string> initialised once at type load via a
      collection-expression / ToFrozenSet(...). Applies identically to
      predefinedTypeNames, predefinedProcedureNames, builtinGoals,
      builtinProcedures.
    idiom_id: dart-const-set-string-to-csharp-frozenset
    research_finding_id: dotnet-frozenset-immutable-readheavy
    nuance: >-
      Dart `const Set` is a deeply-immutable compile-time value with
      deterministic insertion order. C# has NO `const` for collections; the
      faithful mapping is `static readonly` + an immutable set type. FrozenSet
      preserves set semantics and is optimised for the read-only `.Contains`
      lookups these collections exist for. Ordering is NOT semantically used
      here (only membership tests via Contains), so set-vs-ordered-set
      divergence is benign and explicitly noted rather than glossed. Element
      type `string` is an immutable reference type — no value-copy concern.
  - construct_key: dart.toplevel.bool-fn-expression-body-contains
    source_form: "bool isPredefinedType(String name) => predefinedTypeNames.contains(name);"
    target_decision: >-
      public static bool method with an expression body (=>) on the same
      static container class, delegating to FrozenSet<string>.Contains(name).
      Covers isPredefinedType, isBuiltinGoal, isPredefinedProcedure,
      isBuiltinProcedure (all 1-arg String->bool predicates).
    idiom_id: dart-toplevel-fn-to-csharp-static-method
    research_finding_id: csharp-static-class-no-toplevel-members
    nuance: >-
      Dart top-level function -> C# static method inside a static class
      (C# forbids free functions). Dart `Set.contains` -> C# `ICollection<T>
      .Contains` / FrozenSet.Contains, both O(1) membership. Dart `=>`
      expression body maps 1:1 to C# expression-bodied member syntax. `String`
      is non-nullable here; C# parameter is non-nullable `string` (nullable
      reference types enabled).
conversion_units:
  - static-class-shell: static class Prelude { ... } (container for all members)
  - const-field: TypePrelude (const string)
  - frozenset-field: PredefinedTypeNames
  - frozenset-field: PredefinedProcedureNames
  - frozenset-field: BuiltinGoals
  - frozenset-field: BuiltinProcedures
  - static-method: IsPredefinedType(string)
  - static-method: IsBuiltinGoal(string)
  - static-method: IsPredefinedProcedure(string)
  - static-method: IsBuiltinProcedure(string)
escalations: []
```

## Rationale & Research Provenance

This file is structurally simple: four top-level immutable string collections,
one top-level empty `const String`, and four pure `String -> bool` membership
predicates. There is no async, no `Stream`, no isolate, no generics beyond
`Set<String>`, and no mutable state — so the only genuine Dart→C# nuances are
(a) the absence of top-level members in C# and (b) the absence of a `const`
immutable-collection construct in C#.

### research_finding_id: csharp-static-class-no-toplevel-members

- is_authoritative: true (official C# documentation, learn.microsoft.com)
- Verbatim query / source: WebFetch of
  `https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members`
- Authoritative conclusion: C# has no top-level functions or fields; library
  helpers belong in a `static class` ("a convenient container for sets of
  methods that just operate on input parameters"). The doc confirms a `const`
  field "belongs to the type, not to instances" and is accessed as
  `ClassName.MemberName`, and that expression-bodied static members
  (`public static double CelsiusToFahrenheit(string s) => ...`) are idiomatic.
  Therefore the four Dart top-level functions become public static
  expression-bodied methods on a `static class Prelude`, and `typePrelude`
  becomes a `const string` field on the same class. Conclusion: faithful,
  no semantic loss.

### research_finding_id: dotnet-frozenset-immutable-readheavy

- is_authoritative: true (official .NET API documentation, learn.microsoft.com)
- Verbatim query / source: WebFetch of
  `https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozenset`
- Authoritative conclusion: `System.Collections.Frozen.FrozenSet` provides
  immutable set instances created via `ToFrozenSet` / `FrozenSet.Create`,
  designed for collections created once and then read many times. Every use
  site in this file is a read-only `.Contains` lookup
  (`isPredefinedType`, `isBuiltinGoal`, `isPredefinedProcedure`,
  `isBuiltinProcedure`), so `static readonly FrozenSet<string>` is the
  semantically and performance-faithful target for Dart's compile-time
  immutable `const Set<String>`. Corroboration: the static-class doc confirms
  `static readonly` field initialisation occurs at type load, matching Dart's
  one-time `const` materialisation.

### Explicitly addressed well-known nuance: ordered-set vs set

Dart's `Set` literal preserves insertion order; `FrozenSet`/`HashSet` do not
guarantee enumeration order. This is examined, not glossed: these four
collections are consumed **only** through membership tests
(`.contains(name)`), never enumerated for order-dependent behaviour, so the
divergence has zero observable effect. Recorded here to satisfy the SC-006 /
US2-AS4 "never gloss a well-known nuance" bar. If a future caller enumerates
these sets, the idiom must be revisited (would then require an ordered
immutable collection).

### No escalations

All three non-trivial constructs resolved against official Dart/.NET
documentation with consistent conclusions; no undecidable points, no
idiom/research conflicts. `open_escalation_count` = 0.
