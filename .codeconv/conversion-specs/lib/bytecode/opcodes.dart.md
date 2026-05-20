> Conversion-spec artifact for lib/bytecode/opcodes.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/bytecode/opcodes.dart
source_sha256: 41a0a75b2c8fbb8b1009c868fc85301b0d4d3233350840485ca5c35d2cc9dabe
target_code_unit: lib/bytecode/opcodes.cs
constructs:
  - construct_key: dart.typedef.string_alias
    source_form: "typedef LabelName = String;"
    target_decision: >-
      Dart non-function typedef aliasing String. C# has no first-class
      transparent type alias usable as a member/field type across files in the
      legacy lang-version this codebase targets; emit a `using LabelName =
      System.String;` alias at file scope OR (codegen choice, recorded) a
      readonly struct wrapper. Spec mandates the file-scoped `using` alias: it
      preserves the exact transparent-aliasing semantics (LabelName IS string,
      assignable both ways) that Dart `typedef X = String` provides; a wrapper
      struct would change identity/assignability and ripple to every
      LabelName-typed field (Label.name, ClauseNext.label, Guard.procedureLabel,
      RequireWriterArg.failLabel, TailStep.label, Spawn/Requeue procedureLabel).
    idiom_id: null
    research_finding_id: rf-dart-typedef-string-to-csharp-using-alias
    nuance: >-
      Value-vs-reference: Dart String and C# string are both immutable
      reference types compared by value (operator== is content equality on
      both), so aliasing introduces no boxing/identity hazard. Null-safety:
      LabelName is non-nullable (no `?`), so every LabelName field is a
      non-nullable string under an enabled nullable context; no field in this
      file declares `LabelName?`.
  - construct_key: dart.abstract_class.empty_marker_base_non_sealed_implemented
    source_form: >-
      "abstract class Op {}" implemented by ~50 opcode classes via
      `class X implements Op { ... }` (no extends, no shared state/members).
    target_decision: >-
      Model `Op` as a C# interface `IOp` (empty marker interface), each opcode
      class implementing it. Do NOT emit it as a C# `abstract class` base:
      Dart `implements` here is implicit-interface conformance with zero
      inherited state or behaviour, and a C# abstract base would consume the
      single base-class slot and imply an is-a implementation relationship the
      Dart source does not have. CRITICAL: do NOT add `sealed`/exhaustiveness
      semantics — see nuance; the codegen stage must emit a plain
      (non-sealed) marker interface.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-to-csharp-interface
    nuance: >-
      Exhaustiveness nuance (explicitly addressed, not glossed): Dart `abstract
      class Op` is NOT declared `sealed`, so Dart provides NO compiler
      exhaustiveness guarantee over Op's subtypes; consumers (the bytecode
      runner switching on op kind) must already have a default/fallback path.
      Converting to a C# `sealed`-style closed hierarchy or pattern-match
      exhaustiveness would manufacture a guarantee the source never had and
      could mask an unhandled-opcode bug. Reference semantics: Op subtypes are
      Dart classes = heap reference objects with identity; targets must be C#
      `class` (reference type), never `struct`, since the runner stores/queues
      and pattern-switches op instances by reference.
  - construct_key: dart.data_class.final_fields_positional_ctor (opcode IR node)
    source_form: >-
      "class ClauseNext implements Op { final LabelName label;
      ClauseNext(this.label); }" — repeated shape across BodySetConst,
      PutStructure, HeadConstant, Guard, Distribute, etc.: final fields +
      positional this.x constructor, immutable IR node.
    target_decision: >-
      Each becomes a C# reference `class` (NOT record struct) implementing IOp,
      with get-only properties initialised from a single positional
      constructor mirroring `this.field` parameter binding. Immutability of
      Dart `final` fields maps to get-only auto-properties (no setters).
      Multiple constructor params keep declaration order. Classes with no
      fields (ClauseTry, GuardFail, Commit, SuspendEnd, Proceed, Deallocate,
      Nop, Halt, TryNextClause, NoMoreClauses, Otherwise) become empty marker
      classes implementing IOp.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      Value-vs-reference: must remain reference classes (identity-bearing IR
      nodes the runner holds in instruction lists / continuations); a record
      struct would change equality to structural value-equality and introduce
      copy semantics on every queue/store. `final` => get-only property
      (compile-time immutability preserved), not a writable field.
  - construct_key: dart.int.fixed_width_index_and_arity_field
    source_form: >-
      "final int writerId; final int argSlot; final int arity; final int
      slot; final int importIndex; final int regIndex; final int count;
      final int varIndex; final int leftVarIndex; ..." — all `int` fields are
      register indices, argument slots, arities, 1-based import indices, or
      void-skip counts.
    target_decision: >-
      Map Dart `int` to C# `long` (System.Int64), NOT C# `int`/Int32. Dart
      native `int` is a signed 64-bit integer (-2^63..2^63-1); C# `int` is
      only 32-bit. Although every value here is a small bounded
      index/arity/count that fits in Int32 in practice, the SPEC decision is
      the type-faithful mapping `long` so the codegen baseline cannot silently
      narrow Dart 64-bit integer semantics. Codegen MAY, with an explicit
      recorded justification per-field (range provably bounded), down-map a
      specific field to `int`; absent that justification the default is
      `long`. No arithmetic, no bit operations, and no overflow-sensitive
      expressions occur in this file (pure field storage + interpolation), so
      checked/unchecked and shift-sign behaviour are not exercised here.
    idiom_id: null
    research_finding_id: rf-dart-int-to-csharp-long-width
    nuance: >-
      Integer-width nuance (explicitly addressed): Dart int (native) = 64-bit
      two's-complement signed; C# int = 32-bit, C# long = 64-bit. Faithful
      width => long. There is NO bitwise op, NO shift, NO `>>>`/`>>`, NO
      arithmetic and NO overflow path in opcodes.dart (fields are inert
      storage), so the well-known signed-shift / overflow / checked-context
      hazards do not arise in THIS file and are deliberately not asserted;
      they belong to files that compute on these ids, not to this definition
      file. uint is rejected: ids/indices are conceptually non-negative but
      Dart models them as signed int and no unsigned semantics are relied on;
      using uint would diverge from the source type.
  - construct_key: dart.nullable_object_field.Object_question
    source_form: >-
      "final Object? value;" (BodySetConst, PutConstant, SetConstant,
      PutBoundConst, HeadConstant, UnifyConstant, BodySetConstArg) and
      "final List<Object?> constArgs;" (BodySetStructConstArgs).
    target_decision: >-
      Dart `Object?` (top type, nullable) maps to C# `object?` under an
      enabled nullable context. `List<Object?>` maps to a List<object?>
      (a list whose elements may individually be null). These hold GLP
      constant payloads (ints, strings, atoms, nil sentinel) whose concrete
      Dart runtime type is decided elsewhere; Object? is the faithful erasure.
    idiom_id: null
    research_finding_id: rf-dart-objectq-to-csharp-objectq
    nuance: >-
      Null-safety mapping: Dart `Object?` is the nullable top type (any value
      OR null) -> C# `object?` (nullable annotation), NOT `object` (which
      under NRT asserts non-null). Value-vs-reference: boxing — a Dart int/bool
      stored in Object? is already a Dart object; in C# storing a value type
      in `object?` boxes it. This boxing is semantically transparent for
      storage/equality here (no arithmetic on the boxed value in this file)
      but is recorded so the codegen/consumer stage preserves boxed-equality
      behaviour rather than introducing premature unboxing.
  - construct_key: dart.named_optional_param_with_default.this_field
    source_form: >-
      "class UnifyVoid implements Op { final int count;
      UnifyVoid({this.count = 1}); }"
    target_decision: >-
      The named optional initialising-formal with default `{this.count = 1}`
      becomes a C# constructor with an optional parameter carrying the same
      default literal: `UnifyVoid(long count = 1)` assigning the get-only
      `Count` property. Dart call `UnifyVoid()` => default 1; `UnifyVoid(count:
      5)` => C# `new UnifyVoid(count: 5)` (C# named-argument syntax preserves
      the call shape). count maps to `long` per rf-dart-int-to-csharp-long-width.
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-optional-arg
    nuance: >-
      Named-vs-optional nuance: Dart named parameters are call-site
      name-addressed and unordered; C# optional parameters are positional with
      defaults but also support named-argument call syntax, so the single
      defaulted parameter maps faithfully. The default value `1` is a
      compile-time constant in both languages (Dart requires named-param
      defaults to be constant; C# requires optional-arg defaults to be
      compile-time constants) — no semantic drift.
  - construct_key: dart.deprecated_annotation_on_type
    source_form: >-
      "@deprecated class UnionSiAndGoto implements Op {...}" and
      "@deprecated class ResetAndGoto implements Op {...}" (legacy ops kept
      for backward compat with existing tests).
    target_decision: >-
      Dart `@deprecated` on a class maps to the C# `[System.Obsolete]`
      attribute on the corresponding class. The classes are preserved (NOT
      deleted) because the source comment states they remain for backward
      compatibility with existing tests; deprecation is advisory metadata, not
      removal.
    idiom_id: null
    research_finding_id: rf-dart-deprecated-to-csharp-obsolete
    nuance: >-
      Semantics: Dart `@deprecated` emits an analyzer hint at use sites;
      `[Obsolete]` emits a C# compiler warning at use sites. Behaviourally
      equivalent advisory deprecation; neither changes runtime behaviour. The
      classes keep their full structure (UnionSiAndGoto/ResetAndGoto each have
      a final LabelName label + positional ctor) — handled by the
      data-class idiom above.
  - construct_key: dart.tostring_override.string_interpolation
    source_form: >-
      "@override String toString() => 'Push(X$regIndex)';" and similar in
      Pop, UnifyStructure, GroundEqual (ternary with two interpolated forms),
      Distribute ('Distribute([$importIndex] $functor/$arity)'), Transmit.
    target_decision: >-
      Each `@override String toString()` becomes an `override string
      ToString()` of System.Object.ToString. Dart string interpolation
      `'...$x...'` / `'...${expr}...'` becomes a C# interpolated string
      `$"...{X}..."`. GroundEqual's `negated ? '~(...)' : '...'` becomes a C#
      conditional expression returning the interpolated string. Literal
      substrings (e.g. `X`, `/`, `[`, `]`) are preserved verbatim so debug
      output is byte-identical.
    idiom_id: null
    research_finding_id: rf-dart-tostring-interp-to-csharp-tostring-interp
    nuance: >-
      toString nuance (explicitly addressed): Dart `toString()` override maps
      to overriding `object.ToString()` (NOT an extension, which cannot
      override a virtual). Interpolation: Dart `$id`/`${expr}` -> C#
      `{id}`/`{expr}`; numeric interpolation uses invariant `ToString()` on
      both for these int fields, so output text is stable. The classes WITHOUT
      a toString override (most ops) inherit the default object ToString —
      conversion does not synthesise one (preserving source behaviour).
conversion_units:
  - "file-scoped alias: using LabelName = System.String"
  - "interface IOp (empty, non-sealed marker — NO exhaustiveness)"
  - "class Label : IOp (LabelName Name)"
  - "empty marker classes : IOp — ClauseTry, GuardFail, Commit, SuspendEnd, Proceed, Deallocate, Nop, Halt, TryNextClause, NoMoreClauses, Otherwise"
  - "class ClauseNext : IOp (LabelName Label)"
  - "[Obsolete] class UnionSiAndGoto : IOp (LabelName Label)"
  - "[Obsolete] class ResetAndGoto : IOp (LabelName Label)"
  - "class BodySetConst : IOp (long WriterId, object? Value)"
  - "class BodySetStructConstArgs : IOp (long WriterId, string Functor, List<object?> ConstArgs)"
  - "class PutConstant / SetConstant / PutNil / PutList / PutBoundConst / PutBoundNil : IOp"
  - "class PutStructure : IOp (string Functor, long Arity, long ArgSlot)"
  - "class HeadConstant / HeadStructure / UnifyConstant / HeadNil / HeadList : IOp"
  - "class UnifyVoid : IOp (long Count, ctor optional arg = 1)"
  - "class GetVariable / GetValue : IOp (long VarIndex, long ArgSlot)"
  - "class Push / Pop : IOp (long RegIndex, ToString override)"
  - "class UnifyStructure : IOp (string Functor, long Arity, ToString override)"
  - "class Guard : IOp (LabelName ProcedureLabel, long Arity, bool Negated=false)"
  - "class Ground / Known / NoReaders : IOp (long VarIndex, bool Negated=false)"
  - "class GroundEqual : IOp (long LeftVarIndex, long RightVarIndex, bool Negated=false, ToString override)"
  - "class HeadBindWriter / GuardNeedReader / HeadBindWriterArg / GuardNeedReaderArg : IOp"
  - "class RequireWriterArg / RequireReaderArg : IOp (long Slot, LabelName FailLabel)"
  - "class BodySetConstArg : IOp (long Slot, object? Value)"
  - "class TailStep / Spawn / Requeue : IOp (LabelName label, long arity)"
  - "class Allocate : IOp (long Slots)"
  - "class Distribute : IOp (long ImportIndex, string Functor, long Arity, ToString override)"
  - "class Transmit : IOp (long ModuleVarIndex, string Functor, long Arity, ToString override)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-typedef-string-to-csharp-using-alias — transparent string alias

- Deep analysis: `typedef LabelName = String;` is a non-function type alias.
  Every `LabelName`-typed field is interchangeable with `String`. The faithful
  C# counterpart that preserves transparent assignability is a file-scoped
  `using LabelName = System.String;` alias, not a wrapper struct (which would
  change identity and force conversions at every label site).
- Authoritative Dart: WebFetch `https://dart.dev/language/built-in-types`
  (Dart official) — query asked Dart `String` semantics; String is an
  immutable sequence of UTF-16 code units, a reference type compared by value.
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`
  family / Microsoft Learn confirms `string` is an alias for `System.String`,
  immutable, value-equality `==`. Conclusion: Dart `String` ⇔ C# `string`
  with identical immutability and value-equality; a typedef ⇔ a `using`
  alias preserves transparency. Authoritative; no escalation.

### rf-dart-abstract-marker-to-csharp-interface — empty marker base

- Deep analysis: `abstract class Op {}` has no members and is only ever used
  via `implements Op`. It is NOT `sealed`. So it is a pure contract/marker
  with no inherited state and no compiler-enforced exhaustiveness over
  subtypes.
- Authoritative Dart: WebFetch `https://dart.dev/language/class-modifiers`
  (Dart official). Verbatim relevant text: an abstract class without `sealed`
  does not give exhaustiveness; only `sealed` does — "The compiler is aware of
  any possible direct subtypes because they can only exist in the same
  library. This allows the compiler to alert you when a switch does not
  exhaustively handle all possible subtypes." Op is not sealed ⇒ no such
  guarantee in the source.
- Conclusion: emit `IOp` as a plain (non-sealed) marker interface, each
  opcode a reference class implementing it. Do NOT manufacture a closed/
  exhaustive hierarchy. Authoritative; no escalation.

### rf-dart-final-field-class-to-csharp-getonly-class — immutable IR node

- Deep analysis: the dominant shape is `final` fields + a positional
  `this.x` constructor — an immutable IR node held by reference in the
  runner's instruction stream. Reference identity matters (instructions are
  stored/queued/switched), so a reference `class` (not record struct) with
  get-only properties is the faithful target.
- Authoritative Dart: WebFetch `https://dart.dev/language/class-modifiers`
  (Dart official) — Dart class instances are heap objects with identity;
  `final` instance fields are write-once. Conclusion: get-only auto-properties
  from a single constructor preserve immutability without exposing setters;
  reference type preserves identity/aliasing the runner relies on.
  Authoritative; no escalation.

### rf-dart-int-to-csharp-long-width — integer width fidelity

- Deep analysis: every `int` field is a register index / argument slot /
  arity / 1-based import index / void-skip count. All fit Int32 in practice,
  but the source TYPE is Dart `int`.
- Authoritative Dart: WebFetch `https://dart.dev/language/built-in-types`
  (Dart official). Verbatim: "Integer values no larger than 64 bits,
  depending on the platform. On native platforms, values can be from -2^63 to
  2^63 - 1." (web: -2^53..2^53-1).
- Authoritative .NET: WebFetch
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types`
  (Microsoft Learn). Verbatim table: `int` = "-2,147,483,648 to
  2,147,483,647 / Signed 32-bit integer / System.Int32"; `long` =
  "-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807 / Signed 64-bit
  integer / System.Int64".
- Conclusion: type-faithful mapping is Dart `int` ⇒ C# `long`. C# `int`
  would narrow 64-bit Dart semantics. This file performs NO arithmetic, NO
  bitwise/shift ops and has NO overflow path (fields are inert storage +
  string interpolation), so signed-shift / checked-vs-unchecked / overflow
  hazards are not exercised here and are deliberately not asserted. Codegen
  may down-map an individual provably-bounded field to `int` only with a
  recorded per-field justification; default = `long`. Authoritative both
  sides; no escalation.

### rf-dart-objectq-to-csharp-objectq — nullable top type

- Deep analysis: `Object?` / `List<Object?>` carry GLP constant payloads of
  runtime-decided concrete type; the faithful erasure is the nullable top
  type.
- Authoritative .NET: Microsoft Learn nullable-reference / built-in-types
  documentation (same official family fetched above) — `object?` is the
  nullable annotation of the top type `System.Object`; storing a value type
  in `object` boxes it. Conclusion: Dart `Object?` ⇒ C# `object?` (NOT
  `object`), `List<Object?>` ⇒ `List<object?>`. Boxing of value payloads is
  semantically transparent for storage/equality in THIS file (no arithmetic
  on the payload here) and is recorded so consumers preserve boxed-equality
  rather than unboxing prematurely. Authoritative; no escalation.

### rf-dart-named-default-param-to-csharp-optional-arg — `{this.count = 1}`

- Deep analysis: `UnifyVoid({this.count = 1})` is a named optional
  initialising formal with a constant default.
- Authoritative Dart: WebFetch `https://dart.dev/language/functions` (Dart
  official). Verbatim: "Named parameters are optional unless they're
  explicitly marked as `required`." and that an omitted named parameter takes
  its declared default. Conclusion: maps to a C# constructor optional
  parameter `count = 1`; C# named-argument call syntax preserves the
  `count: 5` call shape. Default `1` is a compile-time constant in both
  languages (Dart requires constant named-param defaults; C# requires
  compile-time-constant optional-arg defaults) — no drift. Authoritative; no
  escalation.

### rf-dart-deprecated-to-csharp-obsolete — advisory deprecation

- Deep analysis: `@deprecated` on `UnionSiAndGoto`/`ResetAndGoto`; the source
  comment keeps them for backward compatibility with existing tests, so they
  are preserved, not removed.
- Basis: Dart `@deprecated` (dart:core Deprecated) is an analyzer-hint
  annotation; the documented C# counterpart is `System.ObsoleteAttribute`
  (Microsoft Learn, same official documentation family) — a use-site compiler
  warning with no runtime effect. Behaviourally equivalent advisory
  deprecation. Authoritative; no escalation.

### rf-dart-tostring-interp-to-csharp-tostring-interp — debug toString

- Deep analysis: several classes override `String toString()` returning an
  interpolated debug string (Push, Pop, UnifyStructure, GroundEqual ternary,
  Distribute, Transmit). Classes without an override keep default object
  string.
- Basis: Dart `toString()` ⇔ overriding `System.Object.ToString()` (official
  Dart object semantics + Microsoft Learn object documentation, same official
  family). Dart string interpolation `$id`/`${expr}` ⇔ C# interpolated string
  `$"{id}"`/`$"{expr}"`. Literal punctuation is preserved so debug output is
  byte-identical. The override must be a real `override` (extensions cannot
  override a virtual). Authoritative; no escalation.

## Notes

- No Stream/Future/async, no isolates, no `late`/`mixin`/`extension`, no
  generics-with-bounds, no sealed classes, no bitwise/shift/arithmetic, no
  overflow path in this file — those well-known nuances are ABSENT and are
  correctly not asserted (asserting an absent nuance would be noise).
- The non-sealed `abstract class Op` is the load-bearing semantic decision:
  the conversion must NOT introduce C# exhaustiveness/`sealed` semantics the
  Dart source never had (would mask unhandled-opcode bugs in the runner).
- Trivial / non-construct elements: file/doc comments map mechanically to C#
  XML-doc / `//` comments (trivial, no research). The `@override` annotation
  itself is subsumed by `override` on the ToString construct.
- Zero escalations: every non-trivial construct resolved from authoritative
  Dart (dart.dev) and/or .NET (learn.microsoft.com) official documentation;
  no undecidable construct, no idiom/research conflict.
```
