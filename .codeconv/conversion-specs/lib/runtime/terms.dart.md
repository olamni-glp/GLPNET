# Conversion Spec — lib/runtime/terms.dart

```yaml
schema_version: 1
source_path: lib/runtime/terms.dart
source_sha256: afe71bc74cd4474271002cce5b0665e0af46c36775f404102f6c3c7fe30e7a61
target_code_unit: lib/runtime/terms.cs
constructs:
  - construct_key: dart.abstract_base_class.empty_open_marker_for_closed_sum_type
    source_form: >-
      abstract class Term {}
    target_decision: >-
      Emit `public abstract class Term { protected Term() {} }` as the
      hierarchy root. Concrete leaves (`ConstTerm`, `StructTerm`, `VarRef`,
      `MutualRefTerm`, `ModuleTerm`) become `public sealed class <Name> :
      Term`. Although Dart `abstract class` is OPEN (any library may extend
      it), every consumer in this codebase enumerates the five concrete
      leaves by `is`-test / type-switch, so the hierarchy is treated as a
      closed algebraic sum type. The C# `abstract` modifier is applied to
      the base; the C# `sealed` modifier is NOT applied to the base because
      Microsoft Learn: "It's an error to use the abstract modifier with a
      sealed class" — closure is expressed by sealing the LEAVES and by an
      exhaustive type-pattern `switch` (with a throwing discard arm) in
      consumers. The Dart `implements Term` declaration on each leaf
      becomes C# inheritance `: Term` — Dart `implements` on an
      empty-bodied class is structurally identical to `extends` here (no
      methods to re-stub), and converting to inheritance lets the leaves
      share the protected constructor and participate in normal C# subtype
      dispatch.
    idiom_id: null
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Sealed-class-hierarchy nuance is load-bearing here. Microsoft Learn
      forbids `abstract sealed` on the same class, so the customary
      "exhaustive sum type" idiom cannot put `sealed` on the root in C#;
      closure is shifted to (a) `sealed` on each leaf and (b) exhaustive
      type-pattern switch in consumers with a throwing default arm to
      preserve Dart's closed-set totality. Reference-vs-value: `Term` is a
      reference-type hierarchy in both Dart (every `class` is a reference)
      and C# (`class`, NEVER `struct`/`record struct`) so shared sub-term
      aliasing and identity are preserved — terms can appear inside other
      terms (`StructTerm.args`) and identity must survive that aliasing.
      Dart `implements` vs `extends`: an empty marker base is identical
      under both; converting to C# inheritance is the unique faithful
      mapping (C# has no structural-typing `implements`).
  - construct_key: dart.sum_type_leaf.value_carrying_no_eq_override_reference_identity
    source_form: >-
      class ConstTerm implements Term { final Object? value; ConstTerm(this.value);
      @override String toString() => 'Const($value)'; }
    target_decision: >-
      `public sealed class ConstTerm : Term` with a read-only auto-property
      `public object? Value { get; }` set via the constructor
      `public ConstTerm(object? value) { Value = value; }` and an override
      `public override string ToString() => $"Const({Value})";`. Equality is
      DELIBERATELY NOT overridden — the Dart source carries no `==` /
      `hashCode` override, so two `ConstTerm(1)` are NOT equal in Dart
      (reference identity). The C# class therefore keeps the default
      `object.Equals` reference identity. Explicitly REJECTED: emitting
      `ConstTerm` as a `record` (would synthesise structural equality and
      silently change semantics — see Microsoft Learn Records: synthesized
      equality "uses the declared data members"). `Object?` → `object?`
      (NRT) preserves the Dart `Object?` nullable-of-top mapping (Dart's
      `Object?` and C# `object?` are both the nullable top of the
      reference hierarchy in their respective null-safety models).
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-no-eq-to-csharp-class-no-record
    nuance: >-
      Value-vs-reference nuance applied at construct level. `ConstTerm`
      carries data (`Object? value`) but has NO `==` override, so the Dart
      source semantics are reference identity (two `ConstTerm(1)` instances
      are not `==`). Mapping to a C# `record` would silently introduce
      structural equality — a behavioural regression. The spec mandates
      `sealed class` (NOT `record`/`record struct`) so equality remains
      reference identity matching Dart. Nullability: `Object?` → `object?`
      under C# NRT (Microsoft Learn nullable reference types: `T?` on a
      reference type "doesn't allow assignment of null values without a
      warning"); the `null`-bearing `Const(null)` case is preserved.
  - construct_key: dart.sum_type_leaf.functor_args_list_reference_identity
    source_form: >-
      class StructTerm implements Term { final String functor; final List<Term>
      args; StructTerm(this.functor, this.args); @override String toString() =>
      '$functor(${args.join(",")})'; }
    target_decision: >-
      `public sealed class StructTerm : Term` with read-only properties
      `public string Functor { get; }` and
      `public IReadOnlyList<Term> Args { get; }`, set via the constructor.
      `Args` is exposed as `IReadOnlyList<Term>` to mirror Dart's
      `final List<Term>` field (cannot rebind the reference; the *list*
      itself remains mutable in Dart — preserved here by keeping the
      underlying `List<Term>` backing reference shared, NOT defensively
      copied). `ToString()` emits `$"{Functor}({string.Join(",", Args)})"`
      to match Dart's `args.join(",")` exactly (ordinal join with
      element-`ToString`, no surrounding `[ ]`). Equality is NOT overridden
      — `StructTerm` in Dart uses reference identity (no `==` override), so
      C# keeps default `object.Equals`. Explicitly REJECTED: positional
      `record StructTerm(string Functor, List<Term> Args)` — record
      equality on a `List<>` member is reference equality (Microsoft Learn
      Records), AND it would synthesise structural equality on `Functor`
      which the Dart source does NOT have.
    idiom_id: null
    research_finding_id: rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist
    nuance: >-
      Two nuances. (1) Value-vs-reference: `StructTerm` is a reference-type
      term with NO `==` override in Dart, so two `StructTerm('f', [x])`
      must not be equal — emit a plain `sealed class`, never a `record`
      (Microsoft Learn Records: synthesized equality "uses the declared
      data members" and `List<>` members compare by reference anyway, but
      the deeper bug is synthesising structural eq on `Functor`/`Args` that
      Dart does not have). (2) `final List<Term>` is "rebind-final, body-
      mutable" in Dart — the `args` reference cannot be reassigned, but
      `args.add(...)` is legal. C# `IReadOnlyList<Term>` for the public
      property + a `List<Term>` backing field preserves "external sees
      read-only handle, internal mutability allowed if needed"; the spec
      aliases the constructor's incoming `IReadOnlyList<Term>` (or
      `List<Term>`) by reference — NO defensive copy — to match Dart
      `this.args = args` semantics where the term shares the caller's list
      identity.
  - construct_key: dart.sum_type_leaf.variable_ref_int_address_value_equality
    source_form: >-
      class VarRef implements Term { final int addr; VarRef(this.addr);
      @override bool operator ==(Object other) => other is VarRef && other.addr
      == addr; @override int get hashCode => addr.hashCode; @override String
      toString() => 'Var@$addr'; }
    target_decision: >-
      `public sealed class VarRef : Term, IEquatable<VarRef>` with a
      read-only `public int Addr { get; }`. Override `Equals(object?)`,
      `bool Equals(VarRef?)` (implementing `IEquatable<VarRef>`), and
      `GetHashCode()` manually: equality returns
      `other is not null && other.Addr == Addr`; hash returns
      `Addr.GetHashCode()`. Override `==`/`!=` operators (C# convention:
      whenever you override `Equals`, override the operators too so
      `a == b` works on a `VarRef` reference; Microsoft Learn: "If you
      overload the == operator, you must also overload the != operator"
      and "if you override Equals you must override GetHashCode"). NOT a
      `record` — although `VarRef` semantically wants by-value equality on
      a single `int` field, the surrounding sum type uses plain `sealed
      class` everywhere (consistency); `IEquatable<VarRef>` + manual
      `Equals`/`GetHashCode` reproduces the Dart semantics exactly with
      no record-synthesis surprises. Mapping is reference-typed (`class`,
      not `struct`): `VarRef` is shared inside larger terms, and putting
      it on the heap matches the Dart Term-as-reference model — but two
      different `VarRef` instances with the same `Addr` ARE equal by
      `==`/`Equals` (this is value-equality on an int identifier, not
      reference identity).
    idiom_id: null
    research_finding_id: rf-dart-class-eq-on-single-int-field-to-csharp-iequatable
    nuance: >-
      Value-vs-reference nuance at the construct level: `VarRef` is the
      ONE leaf in this file that overrides `==`/`hashCode`, giving it
      structural by-value equality on its `addr` field (two `VarRef(7)` ARE
      `==`). However, the *cell that a VarRef points to* — the live
      reader/writer cell in the heap — is a separate concern (heap
      address, not stored in this type). Per the source's spec comment
      "MUST NOT: Code must not assume reader_addr == writer_addr + 1 or
      derive reader/writer identity from address parity" — `VarRef.addr`
      is an OPAQUE handle; equality of handles is a fast comparator, NOT
      a claim about the referent. The spec maps this to
      `IEquatable<VarRef>` + manual `Equals`/`GetHashCode` (reproducing
      `addr.hashCode` as `Addr.GetHashCode()`) rather than a `record
      struct` (would be a value type, breaking shared-aliasing inside
      `StructTerm.args`) or a `record class` (would synthesise the
      operators but adds clone-with-`with` and `EqualityContract` baggage
      not in the Dart source). C# `==` operator overload required to
      keep `VarRef a == VarRef b` working (vs C# default `==` on
      reference types being reference identity).
  - construct_key: dart.entity_class.mutable_field_with_auto_id_eq_by_id_nonthreadsafe_counter
    source_form: >-
      class MutualRefTerm implements Term { int _currentWriterAddr; final int
      id; static int _nextId = 0; MutualRefTerm(this._currentWriterAddr) : id =
      _nextId++; int get currentWriterAddr => _currentWriterAddr; set
      currentWriterAddr(int addr) => _currentWriterAddr = addr; @override bool
      operator ==(Object other) => other is MutualRefTerm && other.id == id;
      @override int get hashCode => id.hashCode; @override String toString() =>
      'MutualRef#$id(@$_currentWriterAddr)'; }
    target_decision: >-
      `public sealed class MutualRefTerm : Term, IEquatable<MutualRefTerm>`
      with a private mutable backing field `private int _currentWriterAddr`
      exposed via a R/W property `public int CurrentWriterAddr { get =>
      _currentWriterAddr; set => _currentWriterAddr = value; }`, a
      read-only `public int Id { get; }`, and a private static counter
      `private static int _nextId = 0`. Constructor:
      `public MutualRefTerm(int currentWriterAddr) { _currentWriterAddr =
      currentWriterAddr; Id = _nextId++; }`. Override `Equals(object?)` /
      `bool Equals(MutualRefTerm?)` / `GetHashCode()` and `==`/`!=`
      operators comparing by `Id` only — matches Dart's "two MutualRefTerms
      are equal iff they share the SAME unique id" (entity identity, not
      structural equality over mutable state). Comment carried forward
      verbatim: "SRSW: MutualRefTerm is treated as ground (can be read
      multiple times)" — informational, no codegen consequence here. The
      static counter `_nextId` is NOT made thread-safe (no
      `Interlocked.Increment`) because the Dart source uses a plain
      non-atomic post-increment `_nextId++` — preserving Dart semantics
      exactly. If the .NET runtime later needs multi-threaded id
      allocation that becomes a SEPARATE design decision, NOT a silent
      target-side "improvement" here.
    idiom_id: null
    research_finding_id: rf-dart-entity-eq-by-id-mutable-field-to-csharp-class-iequatable
    nuance: >-
      Two intertwined nuances. (1) Equality-by-id with mutable internal
      state is the textbook "entity object" pattern; it MUST be a
      reference-type `class` (NEVER `record class` or `record struct`)
      because synthesised record equality would compare `_nextId`'s
      visible projection — but more critically, equality must be stable
      across mutations of `_currentWriterAddr`, which it would not be if
      a `record` synthesised structural equality over both fields.
      Reference identity of the C# object also matters: a `MutualRefTerm`
      is the load-bearing handle a stream's writers/readers share — they
      must see the same C# object so `CurrentWriterAddr` mutations are
      visible to all sharers (Dart `class` and C# `class` agree on this).
      (2) `static int _nextId = 0` + `_nextId++` is non-atomic in Dart's
      single-thread isolate model; faithfully kept non-atomic in C#
      (Microsoft Learn ECMA-335: `int` reads/writes are atomic but
      read-modify-write `_nextId++` is NOT — preserved as-is, NOT
      silently "fixed" with `Interlocked.Increment` because that would
      diverge from source semantics). Nullability: `currentWriterAddr` is
      a non-nullable `int` in both languages.
  - construct_key: dart.sum_type_leaf.opaque_payload_wrapper_named_param_default_string
    source_form: >-
      class ModuleTerm implements Term { final Object bytecode; final String
      name; ModuleTerm(this.bytecode, {this.name = ''}); @override String
      toString() => 'Module($name)'; }
    target_decision: >-
      `public sealed class ModuleTerm : Term` with read-only properties
      `public object Bytecode { get; }` (non-nullable `object` — Dart
      source field type is non-nullable `Object`, not `Object?`) and
      `public string Name { get; }`. Constructor:
      `public ModuleTerm(object bytecode, string name = "") { Bytecode =
      bytecode; Name = name; }`. The Dart named parameter `{this.name =
      ''}` maps to a C# default-valued positional parameter (C# has no
      true named-only parameters before C#11; default-valued positional
      parameters can still be used as named arguments at call sites:
      `new ModuleTerm(bc, name: "foo")` is valid). `ToString()` returns
      `$"Module({Name})"`. Equality NOT overridden — Dart source has no
      `==` override; reference identity is intentional (a module-term is
      identified by being the same compiled-module object, not by name).
      Bytecode is typed `object` (Dart `Object`) per the source's
      explicit comment "BytecodeProgram (untyped to avoid circular
      import)"; the spec preserves the same `object`-typing decision to
      keep the same circular-import-avoidance choice (the conversion of
      `BytecodeProgram` is a separate file).
    idiom_id: null
    research_finding_id: rf-dart-named-default-param-to-csharp-default-positional
    nuance: >-
      Named-with-default-vs-positional-with-default nuance. Dart named
      parameters with `{this.name = ''}` make `name` callable as
      `ModuleTerm(bc, name: 'm')` and `name` defaults to `''` if omitted.
      C# has no named-only-parameter syntax (pre-C#11) — but default-
      valued positional parameters can be supplied by name at the call
      site (`new ModuleTerm(bc, name: "m")`), so the spec maps to
      `string name = ""`. This is the canonical idiom (Microsoft Learn
      named/optional arguments: "Named arguments enable you to specify an
      argument for a parameter by matching the argument with its name");
      semantics differ only if a caller relied on Dart's positional-
      forbidding of `name` — no such caller exists here. Nullability:
      `Object` (Dart) → `object` (C#, non-nullable); `String` (Dart) →
      `string` (C#, non-nullable). The `''` default is preserved exactly.
  - construct_key: dart.toString_override_using_string_interpolation
    source_form: >-
      @override String toString() => 'Const($value)';   // and analogous on
      ConstTerm/StructTerm/VarRef/MutualRefTerm/ModuleTerm
    target_decision: >-
      Each leaf gets `public override string ToString()` returning the
      same interpolated string with `$"…"`: `ConstTerm` → `$"Const({Value})"`,
      `StructTerm` → `$"{Functor}({string.Join(",", Args)})"`, `VarRef` →
      `$"Var@{Addr}"`, `MutualRefTerm` →
      `$"MutualRef#{Id}(@{_currentWriterAddr})"`, `ModuleTerm` →
      `$"Module({Name})"`. Dart `${expr}` and C# `{expr}` are the same
      interpolation primitive in both languages (Microsoft Learn
      interpolated strings: `$"{expr}"` "are recognized starting in
      C# 6"). `args.join(",")` (Dart) → `string.Join(",", Args)` (C#,
      Microsoft Learn `String.Join(string?, IEnumerable<T>)` —
      element-wise `ToString` join with the separator), which matches
      Dart's `Iterable.join` exactly: no surrounding `[ ]`, ordinal
      separator, element `ToString` per item.
    idiom_id: null
    research_finding_id: rf-dart-string-interpolation-join-to-csharp-interpolation-string-join
    nuance: >-
      Trivial-but-explicit: Dart's `'$x'` and C#'s `$"{X}"` are 1:1; the
      only subtlety is `args.join(",")` vs `string.Join(",", Args)` where
      Dart `Iterable.join` and C# `String.Join(string, IEnumerable<T>)`
      both emit element-`ToString` separated by the given string with no
      surrounding brackets (Microsoft Learn `String.Join`). Null-safety:
      no element here is nullable except `ConstTerm.Value` (`object?`),
      where C# interpolation calls `ToString()` on the boxed nullable
      and emits the empty string for `null` — matching Dart's
      `'$null'` → `'null'` *only* if the original is actually `null`
      (Dart prints "null"; C# interpolation prints "" for `object?`
      null). The codegen of `ConstTerm.ToString` must therefore use
      `Value?.ToString() ?? "null"` inside the interpolation to preserve
      Dart's "null" rendering exactly — NOT a plain `{Value}` which would
      drop the literal "null" word.
conversion_units:
  - abstract class Term (protected ctor; root of closed sum type)
  - sealed class ConstTerm : Term (object? Value; ToString override; default reference identity equality)
  - sealed class StructTerm : Term (string Functor; IReadOnlyList<Term> Args; ToString override using string.Join; default reference identity equality; backing list aliased not copied)
  - sealed class VarRef : Term, IEquatable<VarRef> (int Addr; ToString override; Equals/GetHashCode/== /!= overrides comparing Addr)
  - sealed class MutualRefTerm : Term, IEquatable<MutualRefTerm> (private mutable _currentWriterAddr; readonly Id; static non-atomic _nextId counter; CurrentWriterAddr R/W property; ToString override; Equals/GetHashCode/== /!= overrides comparing Id only)
  - sealed class ModuleTerm : Term (object Bytecode; string Name with default ""; ToString override; default reference identity equality; bytecode typed as object to preserve circular-import-avoidance)
escalations: []
```

## Rationale & Research Provenance

This file is the GLP term sum-type: an empty `abstract class Term` base plus
five leaves (`ConstTerm`, `StructTerm`, `VarRef`, `MutualRefTerm`, `ModuleTerm`).
The non-trivial decisions all turn on Dart→C# *semantics* — sum-type closure
without `abstract sealed`, reference-vs-value identity at each leaf
(intentional asymmetry: only `VarRef` and `MutualRefTerm` override `==`),
mutable entity-id state on `MutualRefTerm`, null-safety, named-default
parameters, and a non-thread-safe static counter that must be preserved
faithfully.

### rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves

**Deep analysis.** `abstract class Term {}` is an empty open marker class.
Dart `implements Term` on each of the five leaves treats `Term` as an
interface; since the base has no members, `implements` and `extends` are
structurally identical here. Every consumer in this codebase enumerates the
five leaves by `is`-test or type-switch, so the hierarchy is closed in
practice.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed`
— Microsoft Learn, decisive: *"It's an error to use the abstract modifier
with a sealed class, because an abstract class must be inherited by a class
that provides an implementation of the abstract methods or properties."*
Verbatim query: "C# sealed abstract class hierarchy exhaustive". Therefore
the base CANNOT be C# `sealed`; closure is expressed by sealing the LEAF
classes and an exhaustive type-pattern `switch` (with throwing default arm)
in consumers (corroborated by Microsoft Learn pattern-matching:
`switch` "throws an exception if the object … doesn't match any of the
switch arms").

**Conclusion.** `public abstract class Term { protected Term() {} }` +
five `public sealed class … : Term` leaves. Dart `implements Term` becomes
C# `: Term` inheritance — Dart implements-on-empty-class and C# inheritance
are observationally identical when the base has no methods, and inheritance
is the unique faithful C# mapping (C# has no structural `implements`).

### rf-dart-sumleaf-no-eq-to-csharp-class-no-record

**Deep analysis.** `ConstTerm` has a single `final Object? value` field and
NO `==` / `hashCode` override. In Dart, this means two `ConstTerm(1)`
instances are NOT equal (reference identity). The source comment is silent
on this — it is the language default.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
— Microsoft Learn, decisive: synthesised record equality "uses the
declared data members"; a positional record `record ConstTerm(object?
Value)` would silently *introduce* value equality where the Dart source
has reference equality. Verbatim query: "C# record value equality versus
class reference equality default". WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references` —
Microsoft Learn nullable reference types: `object?` is the nullable-of-top
counterpart of Dart's `Object?` and propagates null-tracking through
constructor assignment.

**Conclusion.** `public sealed class ConstTerm : Term` (NEVER `record`),
read-only `object? Value`, default reference-identity equality preserved.
`Object?` → `object?` under NRT preserves the "may carry null" case.

### rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist

**Deep analysis.** `StructTerm` carries `String functor` and `List<Term>
args` and has NO `==` override. Two `StructTerm('f', [x])` are NOT equal
in Dart. The list is `final` (rebind-final), but its contents are mutable
by language default — though in practice arg lists are not mutated after
construction.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.ireadonlylist-1`
— Microsoft Learn: `IReadOnlyList<T>` "represents a read-only collection
of elements that can be accessed by index" — the canonical idiom for
exposing a list reference that callers cannot rebind/resize. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
(cached): record equality on a `List<>` member is reference equality
*anyway*, but worse here, record synthesis would introduce value equality
on `Functor` which Dart does NOT have. Verbatim queries:
"C# IReadOnlyList<T> expose list immutable handle"; "C# record collection
member reference equality".

**Conclusion.** `sealed class StructTerm : Term` with `string Functor` and
`IReadOnlyList<Term> Args`, both set via the constructor. The backing list
is ALIASED (not defensively copied) to match `this.args = args` Dart
semantics. Equality stays reference identity. `ToString()` uses
`string.Join(",", Args)` to match `args.join(",")` exactly.

### rf-dart-class-eq-on-single-int-field-to-csharp-iequatable

**Deep analysis.** `VarRef` is the unique leaf with `==`/`hashCode`
overrides: two `VarRef` instances are equal iff their `int addr` fields
are equal. The doc-comment is emphatic that `addr` is OPAQUE — reader vs
writer identity is determined by the *heap cell at that address*, NOT by
address arithmetic. The class itself is a thin handle.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1` —
Microsoft Learn: `IEquatable<T>` "Defines a generalized method that a
value type or class implements to create a type-specific method for
determining equality of instances" — the canonical idiom for a class with
custom value-on-fields equality. WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type`
— Microsoft Learn: when overriding `Equals` you MUST override
`GetHashCode`, and "if you overload the `==` operator, you must also
overload the `!=` operator". Verbatim queries: "C# IEquatable<T>
class equality single field"; "C# override Equals GetHashCode == != pair".

**Conclusion.** `sealed class VarRef : Term, IEquatable<VarRef>` with
manual `Equals(object?)`, `Equals(VarRef?)`, `GetHashCode()`, and
`==`/`!=` operators comparing `Addr` only. NOT a `record class` (would
add `EqualityContract` + `with`-expression baggage not in Dart source);
NOT a `record struct` (would break shared-aliasing inside `StructTerm`).
Plain class, reference-typed, with by-value equality on a single int —
the documented C# idiom.

### rf-dart-entity-eq-by-id-mutable-field-to-csharp-class-iequatable

**Deep analysis.** `MutualRefTerm` is an entity: a private mutable
`_currentWriterAddr` (the live "stream tail" pointer, mutated as the
stream grows), a unique read-only `id` allocated from a static
non-atomic counter, and `==`/`hashCode` overrides that compare `id`
ONLY (not `_currentWriterAddr`). Two `MutualRefTerm`s with the same
`id` are the same logical entity even as the writer-address mutates.
The source explicitly notes "Multiple goals can share a MutualRef and
append to the same stream in constant time" — sharing semantics rely on
identity-on-id + mutable state visible through that identity.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1`
(cached) — `IEquatable<T>` is the canonical idiom for a reference type
with a custom equality comparator. WebFetch
`https://learn.microsoft.com/en-us/dotnet/standard/threading/interlocked-operations`
— Microsoft Learn `Interlocked`: documents that `x++` on a shared
`static int` field is "not safe for concurrent access" without
`Interlocked.Increment`. Verbatim queries: "C# IEquatable<T> identity
mutable entity object"; "C# Interlocked.Increment static int counter
thread safety". The Dart source uses a plain non-atomic `_nextId++`;
the spec preserves the same non-atomic semantics in C# rather than
silently "fixing" it (FR-013: escalate-don't-guess applies in the
opposite direction here — do not silently *improve* either).

**Conclusion.** `sealed class MutualRefTerm : Term,
IEquatable<MutualRefTerm>` with a private mutable
`_currentWriterAddr` + `CurrentWriterAddr` R/W property + read-only `Id`
+ private static `_nextId = 0` (NOT made atomic — faithful to Dart);
`Equals`/`GetHashCode`/`==`/`!=` compare `Id` only. Reference-typed C#
class so all sharers see the same mutating object — matching Dart's
identity-with-mutable-state semantics exactly.

### rf-dart-sumleaf-with-list-no-eq-to-csharp-class-ireadonlylist (carry-forward)

See `StructTerm` rationale above; `ModuleTerm.bytecode` is the
analogous "opaque payload" case (typed `object` rather than
`IReadOnlyList<Term>`). The same "don't synthesise structural equality"
discipline applies.

### rf-dart-named-default-param-to-csharp-default-positional

**Deep analysis.** `ModuleTerm(this.bytecode, {this.name = ''})` makes
`name` a Dart named parameter with default `''`. Call sites use
`ModuleTerm(bc, name: 'foo')`.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments`
— Microsoft Learn: *"Named arguments enable you to specify an argument
for a parameter by matching the argument with its name"* and *"Optional
arguments enable you to omit arguments for some parameters"*. C# has no
*named-only* parameter syntax (a parameter is always positionally
callable); but a default-valued positional parameter can be supplied by
name at the call site, which reproduces every legal Dart call site
exactly. WebFetch `https://dart.dev/language/functions` — dart.dev
official: Dart named parameters with `{p = default}` "are optional
unless they're explicitly marked as `required`"; default expressions
must be const. Verbatim query: "C# named optional default parameter
versus Dart named parameter".

**Conclusion.** Map Dart `{this.name = ''}` to C# `string name = ""`; the
default empty string is preserved, and call sites `ModuleTerm(bc, name:
"foo")` work identically in both languages.

### rf-dart-string-interpolation-join-to-csharp-interpolation-string-join

**Deep analysis.** Every leaf overrides `toString` with a single-line
string interpolation. `StructTerm` additionally calls `args.join(",")`
on a `List<Term>`. The only subtlety is `ConstTerm`'s `'Const($value)'`
when `value` is `null`: Dart prints the literal "null"; C#
`$"{Value}"` on an `object?` null prints the empty string.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated`
— Microsoft Learn interpolated strings: `$"{expr}"` "is replaced by the
result of the corresponding expression's `ToString` method"; null
expressions render as the empty string. WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.string.join` —
Microsoft Learn `String.Join`: `String.Join<T>(string?, IEnumerable<T>)`
"concatenates the members of a collection, using the specified separator
between each member" — element `ToString` per item, ordinal join, no
surrounding brackets — matching Dart `Iterable.join`. Verbatim queries:
"C# interpolated string null object ToString rendering"; "C#
String.Join IEnumerable element ToString separator".

**Conclusion.** All five `ToString` overrides map 1:1 with `$"…"`. For
`ConstTerm` specifically, the codegen must use
`$"Const({Value?.ToString() ?? "null"})"` (NOT a plain `{Value}`) so
that `new ConstTerm(null).ToString()` produces `Const(null)` exactly
like Dart. `string.Join(",", Args)` reproduces `args.join(",")` exactly.

### Trivial constructs

The seven `///` doc-comments on `VarRef` and `MutualRefTerm` (referencing
`irmaGLP-spec.md` Section 3.2.1 and `heap-pointer-architecture-spec.md`
v3.0) map mechanically to C# XML-doc `<summary>` comments and carry NO
behavioural conversion decision (informational only — trivial, no
research). The `// NOTE:` comment about removed `isReader`/`varId` is
preserved verbatim as a C# `//` comment. All non-trivial constructs
carry both a deep-analysis basis and an authoritative
`research_finding_id` above.
