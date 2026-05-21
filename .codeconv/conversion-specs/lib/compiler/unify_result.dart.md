# Conversion Spec — lib/compiler/unify_result.dart

> Conversion-spec artifact for lib/compiler/unify_result.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> ## Provenance — why this file exists
>
> This file is a refactor extraction performed to resolve **escalation
> #3 of the 018 live-pass** (the duplicate-`UnifyResult` clash recorded
> in `analyzer.dart.md` and `partial_evaluator.dart.md`). In Dart each
> library has its own scope, so `analyzer.dart` and `partial_evaluator.
> dart` were each free to declare a byte-identical `sealed class
> UnifyResult` + `UnifySuccess` / `UnifyFail` / `UnifySuspend` family —
> `analyzer.dart` imported `partial_evaluator.dart` with `show
> getPreludeUnitClauses` (narrowed import), so the duplicate name never
> crossed library boundaries. C# does NOT allow two types with the same
> fully-qualified name in the same assembly (CS0101). Of the three
> options enumerated in the escalation (rename / lift-to-shared /
> nested-private), the human gate chose **lift-to-shared**: the ADT now
> lives in this single file and both call-sites import it. This spec
> is therefore the canonical site of the cached idiom
> `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`
> instantiated for the `UnifyResult` ADT; the entries in
> `partial_evaluator.dart.md` and `analyzer.dart.md` reduce to a
> single-line cross-reference once their escalations close.

```yaml
schema_version: 1
source_path: lib/compiler/unify_result.dart
source_sha256: 34a1261c94414c63dcf23e281a8ad8b6417c58d7157f3f491d3c87b2e4f82c52
target_code_unit: lib/compiler/UnifyResult.cs
constructs:
  - construct_key: dart.import_directive.relative_with_show_clause_ast_term
    source_form: >-
      "import 'ast.dart' show Term;" — a single relative import of the
      sibling `ast.dart` library, narrowed via Dart `show` to the type
      `Term`. `Term` is referenced exactly once, as the value-type
      parameter of the `Map<String, Term>` field on `UnifySuccess`.
    target_decision: >-
      Emit one C# `using <root>.Compiler;` directive — the namespace
      hosting the converted `Ast.cs` (the source of `Term` and the rest
      of the AST hierarchy). This is the carry-forward idiom recorded
      in `partial_evaluator.dart.md`
      (`rf-dart-relative-import-to-csharp-using-or-same-namespace`) and
      across the compiler/* spec family: relative-path imports within
      the same Dart package collapse to a single C# `using` of the
      target namespace, because the converted `UnifyResult.cs` lives in
      the SAME C# namespace as `Ast.cs` (both under `<root>.Compiler`).
      Codegen MAY emit the directive REDUNDANTLY for review parity
      with the Dart source, or it MAY elide it as a same-namespace
      no-op — either is acceptable; the spec records the redundant
      form as default for line-for-line review fidelity. The Dart
      `show Term` per-symbol filter is dropped per the established
      convention `rf-dart-import-show-clause-no-csharp-counterpart`
      (originated in `runtime/heap_fcp.dart.md`, reused project-wide):
      C# has no per-symbol allow-list at the import level; `using
      static <Type>;` imports type *members*, not *type* references,
      so it is not a counterpart for type-name narrowing.
    idiom_id: null
    research_finding_id: rf-dart-import-show-clause-no-csharp-counterpart
    nuance: >-
      Show-clause nuance (explicitly addressed): Dart `import '…' show
      A;` narrows the imported library's exposed surface to the named
      symbol(s) at THIS compilation unit; C# `using <Namespace>;` has
      no per-symbol narrowing. The drop is a one-way coarsening: every
      OTHER public symbol from `<root>.Compiler` (Procedure, Clause,
      Goal, Guard, VarTerm, StructTerm, ListTerm, …) becomes
      reachable in the converted `UnifyResult.cs` where the Dart
      source restricted it. The `show` was a code-hygiene affordance,
      not load-bearing semantic — the three leaf classes use only
      `Term` directly, and `Map<String, …>`/`Set<String>`/`String`
      from `dart:core` (implicit, always-imported). Dropping the
      filter preserves observable behaviour. Relative-vs-package
      nuance: a relative import (`'ast.dart'`) is semantically
      identical to the equivalent `package:`-qualified import after
      .NET conversion — both resolve to the same C# namespace
      `<root>.Compiler`. Value-vs-reference / null-safety / async /
      Stream / isolate: NOT APPLICABLE to import directives.
  - construct_key: dart.sealed_class.three_arm_unification_result_with_per_arm_payload
    source_form: >-
      "sealed class UnifyResult {}
       class UnifySuccess extends UnifyResult { final Map<String, Term>
         substitution; UnifySuccess(this.substitution); }
       class UnifyFail extends UnifyResult { final String reason;
         UnifyFail(this.reason); }
       class UnifySuspend extends UnifyResult { final Set<String>
         unboundReaders; UnifySuspend(this.unboundReaders); }" — a
      Dart-3 `sealed` discriminated-union with three subclass arms,
      each carrying a typed payload (substitution Map, failure
      reason String, unbound-readers Set). This file DECLARES the
      ADT; consumption (exhaustive `switch (result) { case … }`)
      happens in `analyzer.dart` and `partial_evaluator.dart`,
      which now import this file rather than each redeclaring the
      type. No `==` / `hashCode` / `toString` override in the
      source; default `Object` reference identity inherited.
    target_decision: >-
      Convert via the cached idiom
      `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`
      (canonical instance in `ast.dart`, reused in
      `partial_evaluator.dart.md` and `analyzer.dart.md`): emit a
      CLOSED hierarchy expressed as `public abstract class
      UnifyResult { protected UnifyResult() { } }` (the protected
      constructor pins instantiation to derived classes), plus
      three sealed leaves —
        `public sealed class UnifySuccess : UnifyResult` with
          `public IReadOnlyDictionary<string, Term> Substitution
          { get; }` and ctor `UnifySuccess(IReadOnlyDictionary<
          string, Term> substitution) { Substitution = substitution;
          }`,
        `public sealed class UnifyFail : UnifyResult` with
          `public string Reason { get; }` and ctor `UnifyFail(string
          reason) { Reason = reason; }`,
        `public sealed class UnifySuspend : UnifyResult` with
          `public IReadOnlySet<string> UnboundReaders { get; }`
          and ctor `UnifySuspend(IReadOnlySet<string> unboundReaders)
          { UnboundReaders = unboundReaders; }`.
      Microsoft Learn (cached, ast.dart citation): "It's an error to
      use the abstract modifier with a sealed class" — therefore the
      closure semantics of Dart 3 `sealed` are encoded by (a) sealing
      every LEAF and (b) requiring every consumer's `switch` to
      include a `_ => throw new InvalidOperationException(...)`
      default arm (the runtime guard for the static guarantee Dart
      provides). A `record` (positional or otherwise) is REJECTED:
      `record` synthesises by-value structural equality from all
      positional/property members, but every consumer of `UnifyResult`
      treats instances as short-lived reference IR — constructed,
      switched-on once, discarded — and the substitution Map and
      unbound-readers Set are reference values whose default
      record-equality would be `EqualityComparer<…>.Default.Equals`
      (which for `IReadOnlyDictionary` and `IReadOnlySet` is
      reference equality on the collection instances). The Dart
      source has NO `==`/`hashCode` override; identity-equality is
      the contract to preserve. A `record struct` is doubly
      rejected: each leaf carries a non-trivial reference payload
      and lifetime is heap-bound across the partial-evaluator /
      analyzer pipeline; value-type leaves would force boxing every
      time a `UnifyResult` value flowed through the abstract base.
      Pre-existing 1:1 mapping from sibling spec:
      `partial_evaluator.dart.md` already records this exact
      target_decision; this spec is now the canonical home of the
      idiom instance.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Five intertwined nuances explicitly addressed. (1)
      `sealed`-vs-`abstract`-collision: Dart 3 `sealed` on a base
      with concrete leaves is the official Dart way to declare a
      closed discriminated union (Dart language tour, "Class
      modifiers": "sealed gives the compiler enough information to
      enforce exhaustive switching"). C# has NO single keyword
      counterpart — the cached idiom (a) makes the base
      `abstract` so it cannot be instantiated directly, (b) makes
      each leaf `sealed` so the closure cannot be extended
      downstream, (c) requires consumers to emit exhaustive
      `switch` expressions with a throwing default arm.
      (2) Exhaustiveness-verification gap (load-bearing for
      downstream consumer specs): Dart 3 STATICALLY verifies a
      consumer's `switch` covers every subclass of a sealed base;
      C# 11+ pattern-match switches do NOT verify exhaustiveness
      across user-declared class hierarchies (only across enums /
      value types / nullable references). The
      `_ => throw new InvalidOperationException(...)` default arm
      preserves the RUNTIME contract (unknown arm explodes loudly,
      symmetric to a non-exhaustive Dart switch refusing to
      compile). The consumer specs `analyzer.dart.md` and
      `partial_evaluator.dart.md` ALREADY record this consumer-
      side obligation; this declaration spec records the source-
      side root and forwards.
      (3) Library-local-sealing nuance (the root cause of the
      original escalation): Dart `sealed` is LIBRARY-LOCAL — the
      two redeclared `UnifyResult`s in `analyzer.dart` and
      `partial_evaluator.dart` were DIFFERENT TYPES at the Dart
      level, each closed against its own library's subclass set.
      C# has no library-local sealing; the lift-to-shared
      refactor (this file) collapses both to ONE C# type,
      `<root>.Compiler.UnifyResult`. Closure semantics are
      PRESERVED: with the ADT lifted, the union of subclasses
      across both consumers is exactly the three declared here,
      so the C# closed-leaf set IS the same closed set Dart 3
      would have computed library-by-library.
      (4) Payload-immutability and read-only-interface nuance
      (explicitly addressed, cached carry-forward): each payload
      is captured ONCE at construction and never mutated by the
      ADT itself; declaring the property types as
      `IReadOnlyDictionary<string, Term>` and
      `IReadOnlySet<string>` (NOT the concrete `Dictionary` /
      `HashSet`) preserves this invariant by preventing
      accidental mutation downstream. Dart `final` instance
      fields are write-once at construction (Dart language tour
      "Classes"); C# get-only auto-properties (Microsoft Learn
      "Auto-implemented properties") with constructor assignment
      provide identical write-once semantics. NOTE: this is
      shallow immutability — the FIELD cannot be reassigned, but
      the Map / Set the FIELD points to is still mutable if a
      caller passed a mutable instance and retained the
      reference. The constructor signatures take
      `IReadOnly*` interfaces specifically to discourage that
      pattern at the type level. Microsoft Learn: "The
      IReadOnlySet<T> interface ... was introduced in .NET 5"
      and `IReadOnlyDictionary<TKey,TValue>` is documented in
      `System.Collections.Generic`.
      (5) Identity-vs-value nuance: `UnifyResult` instances are
      reference types in both languages (Dart classes are heap
      objects; the C# `abstract class` + `sealed class` leaves
      are reference types). Equality is REFERENCE IDENTITY (no
      `==` override in Dart, no `Equals`/`==` override in C#) —
      this is the intended contract for the short-lived
      pattern-match IR. The record/struct rejection in
      target_decision is the load-bearing application of this
      nuance.
  - construct_key: dart.map_string_term.substitution_payload_in_unify_success
    source_form: >-
      "final Map<String, Term> substitution;
      UnifySuccess(this.substitution);" — the payload field of
      `UnifySuccess`, a Dart `Map<String, Term>` keyed by Dart
      variable name and yielding the bound Term. Populated by the
      caller (the unifier in `partial_evaluator.dart`) and read
      by the consumer's success arm; never mutated AFTER
      construction in this declaration file (this file declares
      only — mutation happens, if at all, before construction).
    target_decision: >-
      Map Dart `Map<String, Term>` → C# `IReadOnlyDictionary<string,
      Term>` AT THE PROPERTY TYPE, NOT the concrete
      `Dictionary<string, Term>`. Two sub-decisions, both
      carrying forward established idioms:
        (1) `Map<K,V>` → `Dictionary<K,V>` (concrete) per
            `rf-dart-map-to-csharp-dictionary` (cached idiom from
            `runtime/machine_state.dart.md`, reused project-wide).
            Dart `Map` is the `dart:core` interface with default
            `LinkedHashMap` (insertion-ordered) implementation; C#
            `Dictionary<TKey,TValue>` is the .NET-idiomatic keyed
            lookup. The substitution Map is consumed PURELY by
            keyed lookup (the partial evaluator iterates `name ->
            term` only via explicit `[name]` reads); no insertion-
            ordered iteration appears in this declaration file
            (it would happen, if anywhere, in consumer files,
            whose specs already record the iteration-order delta).
            The CONSTRUCTOR parameter type is also
            `IReadOnlyDictionary<string, Term>` so callers may
            pass either a concrete `Dictionary` (the cached
            decision throughout the runtime) or any other read-
            only dictionary implementation.
        (2) Read-only interface (`IReadOnlyDictionary<string,
            Term>`) at the property type rather than the concrete
            `Dictionary<string, Term>`: this is the
            payload-immutability projection of the sealed-ADT
            cached idiom
            (`rf-dart-abstract-marker-base-to-csharp-abstract-
            sealed-leaves` already records the projection at the
            ADT level). Dart `final` field of a mutable collection
            type prevents only RE-BINDING the field; C# get-only
            auto-property of a CONCRETE `Dictionary` likewise only
            prevents rebinding. To express the contract that the
            substitution table is NEVER MUTATED after construction
            of the `UnifySuccess`, the C# property surface uses
            the read-only interface — accidental mutation
            downstream becomes a type error.
    idiom_id: null
    research_finding_id: rf-dart-map-to-csharp-dictionary
    nuance: >-
      Two-axis nuance explicitly addressed. Map-vs-Dictionary axis:
      Dart `Map<K,V>` is an interface (`dart:core` library) whose
      default `Map()`-literal implementation is `LinkedHashMap`
      (insertion-ordered); C# `Dictionary<TKey,TValue>`
      (`System.Collections.Generic`) is a CONCRETE generic class
      whose iteration order is undefined (Microsoft Learn: "The
      order in which the items are returned is undefined"). The
      iteration-order delta is LATENT in this file (the
      declaration site never iterates) and is recorded in the
      consumer specs (analyzer.dart.md, partial_evaluator.dart.md)
      where the substitution is consumed. Read-only-interface
      axis: `IReadOnlyDictionary<TKey,TValue>` exposes
      `Count`/`ContainsKey`/`TryGetValue`/`this[key]`/
      `GetEnumerator`/`Keys`/`Values` but NOT `Add`/`Remove`/
      `Clear`/`this[key] = …` — the minimum surface required by
      the consumer's `Substitution` access pattern (consumers
      iterate, lookup, and copy; never mutate). The
      missing-key-lookup divergence (Dart `Map[k]` returns null
      vs C# `Dictionary[k]` throws `KeyNotFoundException`) is the
      cross-file delta recorded by
      `rf-dart-map-to-csharp-dictionary`; in this declaration
      file the Map is never read by `[]`, so the delta is latent
      and recorded only by reference. Null-safety: neither type
      parameter is nullable in the Dart source (`Map<String,
      Term>`, not `Map<String?, Term?>`); under enabled NRT the C#
      counterpart is `IReadOnlyDictionary<string, Term>` (non-
      nullable key, non-nullable value). Value-vs-reference:
      `string` (Dart `String`) is a reference type with value-
      equality in both languages — appropriate dictionary key
      under either `Dictionary<TKey,TValue>`'s default
      `EqualityComparer<string>.Default` (which is ordinal-
      culture-invariant) or an explicit `StringComparer.Ordinal`
      (cached idiom recorded in `partial_evaluator.dart.md`).
      The default is acceptable for variable-name keys (ASCII /
      identifier characters only). `Term` is a reference type
      (per `ast.dart.md` AST hierarchy spec). Async / Stream /
      isolate / late / mixin: NOT APPLICABLE.
  - construct_key: dart.set_string.unbound_readers_payload_in_unify_suspend
    source_form: >-
      "final Set<String> unboundReaders;
      UnifySuspend(this.unboundReaders);" — the payload field of
      `UnifySuspend`, a Dart `Set<String>` listing the names of
      reader variables that are unbound at the suspension point.
      Populated by the caller and read by the consumer's suspend
      arm; never mutated AFTER construction in this declaration
      file.
    target_decision: >-
      Map Dart `Set<String>` → C# `IReadOnlySet<string>` AT THE
      PROPERTY TYPE, with the constructor parameter also typed
      `IReadOnlySet<string>`. Sub-decisions:
        (1) `Set<T>` → `HashSet<T>` (concrete) per the cached
            idiom `rf-dart-set-to-csharp-hashset` (recurring in
            runtime/* and analysis/* specs; rooted in `runtime/
            machine_state.dart.md`'s suspension-tracker family).
            Dart `Set` is the `dart:core` interface with default
            `LinkedHashSet` (insertion-ordered) implementation;
            C# `HashSet<T>` (`System.Collections.Generic`) is the
            .NET-idiomatic unique-element container. The
            iteration-order delta is identical to the
            Dictionary axis (LinkedHashSet insertion-ordered vs
            HashSet undefined-order); in this declaration file
            no iteration occurs, so the delta is latent and
            recorded only by reference to the consumer specs.
        (2) Read-only interface (`IReadOnlySet<string>`,
            introduced .NET 5 per Microsoft Learn) at the
            property surface, mirroring the
            `IReadOnlyDictionary` decision on the substitution
            payload above — same payload-immutability nuance
            (the `unboundReaders` set is captured once and not
            mutated by the ADT). Microsoft Learn: "The
            IReadOnlySet<T> interface ... was introduced in
            .NET 5" — already cited by the sealed-ADT cached
            idiom in `partial_evaluator.dart.md` (target_
            decision text).
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Set-vs-HashSet axis (mirror of the Map-vs-Dictionary
      axis): Dart `Set<E>` is an interface; default `Set()`-
      literal implementation is `LinkedHashSet` with insertion-
      ordered iteration. C# `HashSet<T>` is a concrete generic
      class with undefined enumeration order (Microsoft Learn).
      The cross-file iteration-order delta is recorded by the
      `rf-dart-set-to-csharp-hashset` cached idiom and is
      latent in this declaration file. Read-only-interface
      axis: the property surface MUST be `IReadOnlySet<string>`
      (NOT the concrete `HashSet<string>`) to express the
      "captured once, never mutated by the ADT" contract at the
      type level — the `Add`/`Remove`/`Clear`/`UnionWith`/
      `IntersectWith` surface is intentionally hidden. .NET-
      version nuance: `IReadOnlySet<T>` requires .NET 5 or
      later; the project target framework (per spec convention
      established by sibling specs, `net6.0` or newer) honours
      this. Null-safety: `Set<String>` has a non-nullable
      element type; `IReadOnlySet<string>` under enabled NRT is
      a non-nullable-element set; the consumer's iteration
      `foreach (var name in result.UnboundReaders)` yields
      non-null `string` items. Value-vs-reference: `string` is a
      reference type with value-equality; appropriate as a
      `HashSet<string>` element under the default
      `EqualityComparer<string>.Default`. Async / Stream /
      isolate: NOT APPLICABLE.
  - construct_key: dart.string_field.failure_reason_payload_in_unify_fail
    source_form: >-
      "final String reason; UnifyFail(this.reason);" — the
      payload field of `UnifyFail`, a Dart non-nullable `String`
      holding a human-readable failure message produced by the
      unifier (e.g. `"Arity mismatch"`, `"Incompatible bindings:
      …"`).
    target_decision: >-
      Map Dart non-nullable `String` → C# non-nullable `string`
      under enabled NRT, exposed as a get-only auto-property
      `public string Reason { get; }` initialised by ctor
      parameter `string reason`. Cached idiom
      `rf-dart-string-to-csharp-string` (project-wide, rooted in
      `error.dart.md`'s exception-message field family).
    idiom_id: null
    research_finding_id: rf-dart-string-to-csharp-string
    nuance: >-
      Null-safety nuance (explicitly addressed): the Dart source
      types the field `String` (not `String?`); under enabled
      NRT the C# counterpart is `string` (not `string?`). The
      constructor cannot be called with `null` for `reason`
      without an explicit `null!` override at the caller — this
      matches the Dart behaviour (`null` to a non-nullable
      formal is a compile error). Reference-vs-value nuance:
      both Dart `String` and C# `string` are reference types
      with by-value equality semantics (interning is an
      implementation detail in both); appropriate for a
      diagnostic-message payload. Encoding nuance: Dart strings
      are UTF-16 code units (api.dart.dev `String`); C# strings
      are UTF-16 code units (Microsoft Learn `System.String`) —
      byte-identical character storage. No interpolation /
      formatting occurs in THIS declaration file (the message
      is opaque to the ADT); interpolation at the caller's
      `throw new UnifyFail($"...")` site is handled by the
      caller's spec (`rf-dart-tostring-interp-to-csharp-tostring-
      interp` cached idiom). Async / Stream / isolate: NOT
      APPLICABLE.
  - construct_key: dart.docblock_triple_slash
    source_form: >-
      "/// Result of compile-time GLP unification for partial
      evaluation." — a single Dart triple-slash doc comment
      immediately above the `sealed class UnifyResult` base
      declaration.
    target_decision: >-
      Map to a C# XML-doc comment `/// <summary>Result of
      compile-time GLP unification for partial evaluation.
      </summary>` immediately above the abstract class
      declaration. Trivial mechanical mapping (cached convention
      across compiler/* and runtime/* specs).
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
conversion_units:
  - "using <root>.Compiler; (single using directive; show-clause dropped per rf-dart-import-show-clause-no-csharp-counterpart; may be elided if target file shares the namespace)"
  - "doc-comment → /// <summary>Result of compile-time GLP unification for partial evaluation.</summary> on the abstract class"
  - "public abstract class UnifyResult { protected UnifyResult() { } } (closed via sealed leaves, NOT via the impossible abstract+sealed on the base; reference type; default identity equality, NO record/struct)"
  - "public sealed class UnifySuccess : UnifyResult"
  - "  property: get-only IReadOnlyDictionary<string, Term> Substitution (non-nullable; Map<String,Term> → IReadOnlyDictionary<string,Term>; read-only interface preserves payload-immutability contract; concrete-Dictionary implementation supplied by caller per rf-dart-map-to-csharp-dictionary)"
  - "  constructor: UnifySuccess(IReadOnlyDictionary<string, Term> substitution) — positional, assigns Substitution"
  - "public sealed class UnifyFail : UnifyResult"
  - "  property: get-only string Reason (non-nullable; Dart String → C# string under enabled NRT)"
  - "  constructor: UnifyFail(string reason) — positional, assigns Reason"
  - "public sealed class UnifySuspend : UnifyResult"
  - "  property: get-only IReadOnlySet<string> UnboundReaders (non-nullable; Set<String> → IReadOnlySet<string>; .NET 5+; read-only interface preserves payload-immutability contract; concrete-HashSet implementation supplied by caller per rf-dart-set-to-csharp-hashset)"
  - "  constructor: UnifySuspend(IReadOnlySet<string> unboundReaders) — positional, assigns UnboundReaders"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-import-show-clause-no-csharp-counterpart — relative `show`-narrowed import of `Term` (cached idiom, reuse)

- Deep analysis: the source's only `import` is `import 'ast.dart' show
  Term;` — a relative import of the sibling library, narrowed to the
  single type `Term` via Dart `show`. `Term` appears exactly once, as
  the value type of `Map<String, Term>` on `UnifySuccess`.
- Provenance: cached idiom first recorded in `runtime/heap_fcp.dart.md`
  and elaborated across runtime/* and compiler/* specs (most recently
  in `compiler/result.dart.md`'s
  `dart.import_directive.package_with_show_clause_bytecode_program`
  construct). The authoritative bases were established there:
    - Dart official: relative-path imports resolve within the same
      Dart package; `show` narrows the imported surface at the
      compilation unit (Dart language tour: "import directives").
    - .NET official: `using <Namespace>;` imports a namespace's full
      public surface; per-symbol narrowing is not a `using`-directive
      feature (Microsoft Learn: `using` directive). `using static
      <Type>;` imports type *members*, not type references, so it
      does not narrow type-name imports.
- Conclusion: emit a bare `using <root>.Compiler;` (the namespace
  hosting `Ast.cs`); drop the `show Term` filter. Codegen MAY elide
  the directive when the target file lives in the same namespace
  (no-op) — either form is acceptable; default is the redundant form
  for review parity. FR-024 cache hit; no new research required.

### rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves — three-arm `UnifyResult` ADT (cached idiom, canonical home)

- Deep analysis: the file declares a Dart-3 `sealed class UnifyResult
  {}` base plus three subclasses `UnifySuccess` / `UnifyFail` /
  `UnifySuspend`, each `extends UnifyResult` with a single typed
  `final` field (Map / String / Set) and a positional constructor
  using initialising formals. No `==` / `hashCode` / `toString`
  override; default reference identity equality inherited from
  `Object`. The ADT itself has no methods. Consumers (in
  `analyzer.dart` and `partial_evaluator.dart`, both of which now
  `import 'unify_result.dart';`) pattern-match exhaustively via
  `switch (result) { case UnifySuccess(:final substitution): … case
  UnifyFail(:final reason): … case UnifySuspend(:final
  unboundReaders): … }` — Dart 3 statically verifies the switch is
  exhaustive because the base is `sealed`.
- Provenance: cached idiom first recorded in `compiler/ast.dart.md`
  (`rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`),
  reused verbatim in `compiler/partial_evaluator.dart.md` and
  `compiler/analyzer.dart.md` (the latter as part of the
  duplicate-`UnifyResult` escalation #3 chain that produced THIS
  file). The authoritative bases were established there:
    - Dart official: `sealed` class modifier (Dart 3, language
      tour "Class modifiers"): "sealed gives the compiler enough
      information to enforce exhaustive switching" — closed
      hierarchies of subclasses are statically known and exhaustive
      `switch` over them is checked at compile time.
    - .NET official: Microsoft Learn `abstract`/`sealed`
      modifiers — "It's an error to use the abstract modifier with
      a sealed class" (the base cannot be both); closure is encoded
      by `abstract` on the base + `sealed` on every leaf;
      exhaustiveness is NOT verified by the C# compiler across
      user-declared class hierarchies (only across enum / value
      type / nullable reference type switches). Therefore
      consumers' `switch` expressions MUST include a `_ => throw
      new InvalidOperationException(...)` default arm — the
      runtime guarantee that mirrors Dart 3's compile-time check.
    - .NET official: Microsoft Learn `IReadOnlyDictionary<TKey,
      TValue>` (`System.Collections.Generic`) — non-mutating
      interface exposing Count / ContainsKey / TryGetValue /
      this[key] / Keys / Values / GetEnumerator; appropriate for
      the `Substitution` property.
    - .NET official: Microsoft Learn `IReadOnlySet<T>` —
      "introduced in .NET 5"; non-mutating interface; appropriate
      for the `UnboundReaders` property.
- Why this file is the canonical home of the idiom instance: prior to
  the refactor that produced this file, the ADT was declared TWICE
  (once in `analyzer.dart`, once in `partial_evaluator.dart`), and
  the cached idiom was applied separately in each spec — with an
  unresolved namespace clash at the C# target level recorded as
  escalation #3 (`detail: "Both analyzer.dart and partial_
  evaluator.dart independently declare sealed class UnifyResult /
  UnifySuccess / UnifyFail / UnifySuspend — same fully-qualified C#
  type, CS0101 in the converted assembly"`). The escalation
  enumerated three options (rename / lift-to-shared / nested-
  private); the human gate chose **lift-to-shared** and produced
  `unify_result.dart`. This spec now records the SINGLE C#-level
  idiom instance; the consumer specs reduce to one-line cross-
  references when their escalations close.
- Why NOT `record` (or `record struct`): the Dart source has no
  `==`/`hashCode`/`toString` override. Default `record` synthesised
  by-value structural equality would observably change the equality
  contract from reference identity to "deep equality of the payload
  fields (using `EqualityComparer<…>.Default` per member)". For the
  reference-type collection payloads (`IReadOnlyDictionary`,
  `IReadOnlySet`) the default equality is reference equality on the
  collection instance itself — but record auto-generation ALSO
  injects a synthetic `ToString()`, `GetHashCode()`, and
  `Deconstruct(…)` surface that the Dart source DOES NOT have.
  Faithful conversion preserves the minimal Object surface (no
  `ToString` override beyond default, no `GetHashCode` beyond
  default reference hash). `record struct` is doubly rejected:
  each leaf carries a heap-bound reference payload; boxing would
  occur every time a `UnifyResult` value flowed through the base
  abstract type. Authoritative both sides; no escalation.
- Closure preservation under lift-to-shared: in the pre-refactor
  state, Dart 3's library-local `sealed` semantics meant
  `analyzer.dart.UnifyResult` and `partial_evaluator.dart.
  UnifyResult` were DIFFERENT closed types, each closed against
  its own library's three-leaf set. Under the lift, the union of
  subclasses across BOTH consumers is exactly the three declared
  in this file, so the C# closed-leaf set IS the closed set
  Dart 3 would have computed library-by-library — no semantic
  loss.

### rf-dart-map-to-csharp-dictionary — `Map<String, Term>` substitution payload (cached idiom, reuse)

- Deep analysis: the `substitution` field of `UnifySuccess` carries
  a Dart `Map<String, Term>` of variable-name → bound-Term entries.
  The map is constructed by the unifier and consumed by exhaustive
  pattern-match arms in `analyzer.dart` and `partial_evaluator.dart`;
  this declaration file never mutates or iterates it.
- Provenance: cached idiom first recorded in
  `runtime/machine_state.dart.md` and reused project-wide. The
  authoritative bases were established there:
    - Dart official: `Map<K,V>` is the `dart:core` keyed-collection
      interface; default `Map()`-literal implementation is
      `LinkedHashMap` (insertion-ordered).
    - .NET official: `Dictionary<TKey,TValue>` is the .NET-idiomatic
      keyed-lookup collection; Microsoft Learn documents that
      enumeration order is "undefined".
    - .NET official: `IReadOnlyDictionary<TKey,TValue>` is the
      non-mutating interface; instantiable by any concrete
      dictionary implementation including `Dictionary<TKey,TValue>`.
- Why `IReadOnlyDictionary` at the property surface: the payload
  immutability nuance of the cached sealed-ADT idiom — captured
  once at construction, never mutated by the ADT — is expressed at
  the type level by exposing only the non-mutating interface.
  Callers may supply any concrete dictionary; downstream consumers
  cannot mutate it through the `Substitution` property.
- Iteration-order delta: latent in THIS declaration file. The
  consumer specs (`analyzer.dart.md`, `partial_evaluator.dart.md`)
  record the cross-file delta. Authoritative; no escalation.

### rf-dart-set-to-csharp-hashset — `Set<String>` unbound-readers payload (cached idiom, reuse)

- Deep analysis: the `unboundReaders` field of `UnifySuspend`
  carries a Dart `Set<String>` of unbound-reader variable names.
  Constructed by the unifier and consumed by the suspend arm of
  consumer pattern-match switches; this declaration file never
  mutates or iterates it.
- Provenance: cached idiom recurring across runtime/* and analysis/*
  specs (rooted in `runtime/machine_state.dart.md` suspension-
  tracker family; reused in `runtime/suspend.dart.md`,
  `runtime/suspend_ops.dart.md`). The authoritative bases were
  established there:
    - Dart official: `Set<E>` is the `dart:core` unique-collection
      interface; default `Set()`-literal implementation is
      `LinkedHashSet` (insertion-ordered).
    - .NET official: `HashSet<T>` is the .NET-idiomatic unique-
      element collection; enumeration order is "undefined"
      (Microsoft Learn).
    - .NET official: `IReadOnlySet<T>` is the non-mutating
      interface; "introduced in .NET 5" (Microsoft Learn).
- Why `IReadOnlySet` at the property surface: identical reasoning
  to `IReadOnlyDictionary` above — payload immutability at the
  type level. Authoritative; no escalation.

### rf-dart-string-to-csharp-string — `UnifyFail.reason` non-nullable diagnostic message (cached idiom, reuse)

- Deep analysis: the `reason` field of `UnifyFail` is a non-
  nullable Dart `String` opaque to the ADT (the ADT does no
  formatting or comparison on it). Constructed at the unifier's
  failure site (where interpolation happens, governed by the
  caller's `rf-dart-tostring-interp-to-csharp-tostring-interp`
  cached idiom) and consumed by the fail arm of pattern-match
  switches.
- Provenance: cached idiom rooted in `compiler/error.dart.md`'s
  exception-message field family and reused project-wide. The
  authoritative bases were established there:
    - Dart official: api.dart.dev `String` — UTF-16 code-unit
      sequence, reference type with by-value equality.
    - .NET official: Microsoft Learn `System.String` — UTF-16
      code-unit sequence, reference type with by-value equality;
      under enabled NRT, non-nullable `string` is the default.
- Conclusion: `string Reason { get; }` non-nullable; ctor
  parameter `string reason` non-nullable. Authoritative; no
  escalation.

## Notes

- This file is **the product of the lift-to-shared resolution of
  escalation #3** in the 018 live-pass (per the analyzer.dart and
  partial_evaluator.dart spec escalations). Its existence and shape
  are determined by that human gate decision; this spec records the
  resulting source file faithfully and is the canonical instance
  site of the cached sealed-ADT idiom for `UnifyResult`. The
  duplicate-`UnifyResult` escalation in
  `compiler/analyzer.dart.md` and `compiler/partial_evaluator.dart
  .md` is THEREBY RESOLVED at the source-tree level; those specs
  may be re-baselined to point at this file's idiom instance in a
  follow-up pass.
- No `async`/`await`, `Future`, `Stream`, `Completer`, `Isolate`,
  `late`, `mixin`, `extension`, `record`, operator overloading,
  factory constructors, const constructors, named constructors,
  generics with bounds, FFI, web-only types, top-level variables,
  top-level functions, static members, inheritance beyond the
  sealed ADT, interface implementation, bitwise/shift/arithmetic,
  or `toString`/`==`/`hashCode` overrides in this file. The
  well-known nuances `Stream` → `IAsyncEnumerable`, `Future` →
  `Task`/`ValueTask`, isolate boundary semantics, async-
  cancellation, and value-vs-reference equality overrides are
  DELIBERATELY ABSENT from this spec — the source does not
  contain them, and inventing translations would violate the
  spec quality bar (FR-023, US2 AS4).
- The well-known nuances that ARE addressed (load-bearing): (a)
  Dart 3 `sealed` vs C# `abstract` + sealed leaves; (b) library-
  local sealing vs assembly-global types under the
  lift-to-shared refactor; (c) static exhaustiveness verification
  gap and the throwing-default-arm runtime contract; (d) payload
  immutability via `IReadOnly*` interfaces at the property
  surface; (e) Map/Set iteration-order deltas (latent here,
  recorded by reference); (f) null-safety preservation under
  enabled NRT for all three payload fields and all three
  constructor parameters; (g) Identity-vs-value equality
  (reference identity preserved; record / record struct
  rejected).
- Zero escalations: every construct is resolved from cached idioms
  with authoritative Dart and .NET official-documentation
  citations carried forward (FR-012 / FR-024 cache hit at every
  construct; SC-007 reuse compliance). No new research required.

## Cross-spec consistency

This spec reuses the following cached research findings from sibling
specs — no re-research, no re-derivation (FR-024 cache discipline):

- `rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves`
  (canonical site: `compiler/ast.dart.md`; reused in
  `compiler/partial_evaluator.dart.md`,
  `compiler/analyzer.dart.md`; this spec is now the **canonical
  instance home** for the `UnifyResult` ADT specifically)
- `rf-dart-import-show-clause-no-csharp-counterpart`
  (`runtime/heap_fcp.dart.md`)
- `rf-dart-map-to-csharp-dictionary`
  (`runtime/machine_state.dart.md` family)
- `rf-dart-set-to-csharp-hashset`
  (`runtime/machine_state.dart.md` / `runtime/suspend.dart.md`
  family)
- `rf-dart-string-to-csharp-string` (`compiler/error.dart.md`)
- `rf-dart-relative-import-to-csharp-using-or-same-namespace`
  (`compiler/parser.dart.md`)

Cross-file divergence recorded (not blocking THIS declaration
file): the iteration-order delta of `LinkedHashMap`/
`LinkedHashSet` → `Dictionary`/`HashSet` is recorded for
consumers (`analyzer.dart`, `partial_evaluator.dart`); the
`Map.operator[] → V?` vs `Dictionary[k] → throws` divergence
applies only to consumers that read the substitution (not to
this declaration). The consumer specs already record those
deltas.
