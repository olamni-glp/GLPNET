# Conversion Spec — lib/analysis/type_checker/type_ast.dart

```yaml
schema_version: 1
source_path: lib/analysis/type_checker/type_ast.dart
source_sha256: f80349aefb8cc777764548f29d5c6bc663809f9dfffde921c141ae2f7028d38a
target_code_unit: lib/analysis/type_checker/type_ast.cs
constructs:
  - construct_key: dart.enum.plain_three_member_no_members
    source_form: >-
      enum TypeClassification { output, input, interactive }
    target_decision: >-
      Plain C# `enum TypeClassification { Output, Input, Interactive }`. No
      members/getters/toString attached in Dart, so this is a 1:1 value-type
      map. Member order preserved (Output==0) so a default value matches the
      Dart declaration order; only the `output` / `interactive` members are
      ever produced by `TypeDef.classification`, but `input` is retained to
      keep ordinal parity with the source enum.
    idiom_id: null
    research_finding_id: rf-dart-plain-enum-to-csharp-enum
    nuance: >-
      Both Dart and C# enums are value types compared by value/identity, so
      equality semantics are preserved with no boxing/reference hazard. This
      enum carries no behaviour (contrast mode.dart's enhanced enum), so the
      enum-needs-extension-class nuance does NOT apply here — declaration order
      is the only thing that must be preserved.
  - construct_key: dart.abstract_base_class.open_ast_hierarchy_with_positional_fields
    source_form: >-
      abstract class TypeExpr { final int line; final int column;
      TypeExpr(this.line, this.column); }  with subclasses TypeRef,
      ConstantAlt, StructAlt, ListNilAlt, ListConsAlt, PrimitiveModeAlt,
      DiffListAlt
    target_decision: >-
      Emit an `abstract class TypeExpr` carrying two `public int Line { get; }`
      / `public int Column { get; }` read-only auto-properties set via a
      protected constructor `protected TypeExpr(int line, int column)`. The
      seven concrete node types become `sealed class <Name> : TypeExpr`.
      Although Dart `abstract class` is NOT a `sealed` declaration (it is
      open), this hierarchy is treated as a closed sum type because every
      consumer (`isInputMode`, `_containsComplement`) enumerates the concrete
      subtypes by `is`-test; the spec records the hierarchy as effectively
      closed but does NOT add the C# `sealed` modifier to `TypeExpr` itself
      (an `abstract` class cannot be `sealed` — Microsoft Learn: "It's an
      error to use the abstract modifier with a sealed class") — closure is
      expressed by sealing the leaf classes and by an exhaustive
      type-`switch` with a throwing discard arm in the codegen of consumers.
    idiom_id: null
    research_finding_id: rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Reference-vs-value: AST nodes are reference types in both Dart and C#;
      they map to C# `class` (NOT `struct`/`record struct`) so identity and
      heap aliasing of shared sub-trees is preserved. Dart's `abstract class`
      is open (any library may extend it) whereas the GLP type-AST is used as
      a closed algebraic sum; the conversion documents the closed-set intent
      and pushes exhaustiveness enforcement to the consuming `switch` (C# does
      NOT compile-time-verify subtype exhaustiveness over a non-sealed-by-the-
      language base, so a throwing default arm preserves Dart totality).
  - construct_key: dart.extension_on_abstract_base.is_as_dispatch_getters
    source_form: >-
      extension ProcArgTypeExpr on TypeExpr { bool get isInputMode { if (this
      is TypeRef) return (this as TypeRef).isInput; if (this is
      PrimitiveModeAlt) return (this as PrimitiveModeAlt).isInput; return
      false; } String? get typeName {...} bool get isPrimitive => this is
      PrimitiveModeAlt; }
    target_decision: >-
      The Dart extension does NOT add genuinely new behaviour to a closed
      external type — it is a façade over `is`/`as` subtype dispatch on the
      project's own hierarchy. Convert to instance members on `TypeExpr`:
      `public bool IsInputMode`, `public string? TypeName`, `public bool
      IsPrimitive` implemented with C# type-pattern `switch` expressions
      (`this switch { TypeRef r => r.IsInput, PrimitiveModeAlt p => p.IsInput,
      _ => false }`). The Dart `if (this is T) return (this as T).m` chain
      collapses to a single declaration-pattern arm `TypeRef r => r.IsInput`
      (test + cast fused). NOT emitted as a C# `static class` extension —
      since we own `TypeExpr` the members belong on the type, removing the
      static-resolution / no-dynamic-dispatch caveat of Dart extensions.
    idiom_id: null
    research_finding_id: rf-dart-extension-is-as-to-csharp-type-pattern-switch
    nuance: >-
      Semantics shift made explicit: Dart extension members are resolved
      *statically* on the receiver's static type (api docs: "Extension
      methods are resolved statically … they can't be invoked on dynamic"),
      so `isInputMode` would NOT dispatch through a `dynamic`. Moving them to
      real instance members on `TypeExpr` makes them virtual-free instance
      methods resolved on the *runtime* object — strictly safer and behaviour-
      equivalent for all statically-typed call sites in this codebase. The
      `is`+`as` pair maps to a single C# declaration pattern (no double type
      check, no InvalidCast risk). Nullability: `typeName` returns Dart
      `String?` → C# `string?` (NRT) — the null case (primitive nodes) is
      preserved as the `_ => null` arm.
  - construct_key: dart.value_class.manual_eq_hashcode_with_list_element_equality
    source_form: >-
      class TypeRef extends TypeExpr { final String name; final bool isInput;
      final List<TypeExpr> typeArgs; @override bool operator ==(Object other)
      => other is TypeRef && other.name==name && other.isInput==isInput &&
      _listEquals(other.typeArgs, typeArgs); @override int get hashCode =>
      Object.hash(name, isInput, Object.hashAll(typeArgs)); static bool
      _listEquals(...) {...} }
    target_decision: >-
      Emit `sealed class TypeRef : TypeExpr` (a plain class, NOT a `record`)
      that overrides `Equals(object?)`, `bool Equals(TypeRef?)`
      (IEquatable<TypeRef>) and `GetHashCode()` manually, comparing
      `Name`/`IsInput` plus an element-wise `TypeArgs` sequence comparison
      (`TypeArgs.SequenceEqual(other.TypeArgs)` over the recursively
      value-equal `TypeExpr` elements) and a combined hash
      (`HashCode.Add` over name, isInput, each element). A C# positional
      `record` is explicitly REJECTED here: record value equality uses the
      *default* equality of each member, and for a `List<TypeExpr>` member
      that is reference equality (Microsoft Learn Records: synthesized
      equality "uses the declared data members" with their default equality;
      reference-type members compare by reference), which would make two
      structurally-identical `Stream(Integer)` refs unequal — the exact bug
      the Dart `_listEquals` exists to avoid. Note only `TypeRef` defines
      `==`/`hashCode`; the other six node types keep default reference
      identity (their Dart classes do NOT override `==`), so they map to
      plain reference-identity C# classes — this asymmetry is preserved
      deliberately, not "fixed".
    idiom_id: null
    research_finding_id: rf-dart-list-element-value-equality-to-csharp-sequenceequal
    nuance: >-
      Value-vs-reference equality is the load-bearing nuance. Dart `==`
      override gives `TypeRef` *structural* equality with deep, element-wise
      list comparison (`_listEquals` recurses through `TypeExpr ==`). A naive
      C# `record TypeRef(string Name, bool IsInput, List<TypeExpr> TypeArgs)`
      would compile but silently regress: record equality on a `List<>`
      member is reference equality, so `new TypeRef("Stream",false,[Integer])
      != new TypeRef("Stream",false,[Integer])`. The spec mandates a hand-
      written `IEquatable<TypeRef>` with `SequenceEqual` and a structural
      `HashCode` to reproduce `Object.hash`/`Object.hashAll` semantics
      exactly. `dual()` returns a NEW TypeRef sharing the SAME `typeArgs`
      list reference (shallow) — preserved as a shallow copy in C# (pass the
      same `IReadOnlyList`/`List` reference), matching Dart immutability-by-
      convention (fields `final`, list aliased not cloned).
  - construct_key: dart.static_const_set_literal_membership_test
    source_form: >-
      static const builtins = {'Integer','Real','Number','String'}; static
      const systemTypes = {'Any','List'}; bool get isBuiltin =>
      builtins.contains(name);
    target_decision: >-
      Convert the two `static const` Dart set literals to
      `private static readonly System.Collections.Generic.HashSet<string>`
      (or `FrozenSet<string>`) initialised once, exposed via the membership
      getters `public bool IsBuiltin => Builtins.Contains(Name);`. C# `const`
      cannot hold a collection; `static readonly` with a collection
      initializer is the established mapping. Ordinal string comparison is
      specified for the set to match Dart's exact-string `Set<String>`
      membership (no culture-sensitive matching).
    idiom_id: null
    research_finding_id: rf-dart-const-set-to-csharp-frozenset-ordinal
    nuance: >-
      Dart `const {...}` is a compile-time canonicalised immutable set; C#
      has no const collection, so the idiom is `static readonly` HashSet
      (or `FrozenSet` for true immutability + read-optimised membership). The
      load-bearing behavioural nuance is string-comparison semantics: Dart
      `Set<String>.contains` is exact ordinal; the C# set MUST be built with
      `StringComparer.Ordinal` to avoid culture-sensitive drift altering
      builtin/system-type recognition.
  - construct_key: dart.factory_constructor_and_named_ctor_default_maps
    source_form: >-
      TypeEnvironment(this.types, this.procedures, {Map? paramProcDecls,
      this.typeTemplates = const {}}) : paramProcDecls = paramProcDecls ?? {};
      factory TypeEnvironment.empty() => TypeEnvironment({}, {});
    target_decision: >-
      Map the generative constructor to a normal C# constructor with optional
      parameters: `TypeEnvironment(IDictionary<string,TypeDef> types,
      IDictionary<string,ProcDecl> procedures, IDictionary<string,ProcDecl>?
      paramProcDecls = null, IReadOnlyDictionary<string,TypeDef>?
      typeTemplates = null)` with body `ParamProcDecls = paramProcDecls ?? new
      Dictionary<>(); TypeTemplates = typeTemplates ?? <empty>`. The Dart
      `factory TypeEnvironment.empty()` becomes a `public static
      TypeEnvironment Empty() => new TypeEnvironment(new(), new());` static
      factory method (C# has no `factory` keyword; a static method is the
      canonical equivalent). The `const {}` default map argument becomes a
      fresh empty dictionary per call (or a shared immutable empty), NOT a
      shared mutable default — preserving Dart's const-empty-immutable
      semantics.
    idiom_id: null
    research_finding_id: rf-dart-factory-ctor-const-default-to-csharp-static-factory
    nuance: >-
      Two nuances: (1) Dart `factory` ctor → C# static factory method (no
      language `factory`); behaviour identical here (always returns a fresh
      empty instance — the Dart factory does not cache). (2) Dart `const {}`
      default for `typeTemplates` is an *immutable* compile-time empty map;
      naively reusing one mutable static default in C# would alias mutable
      state across instances — the spec requires either a fresh `Dictionary`
      per construction or an immutable empty
      (`ImmutableDictionary<,>.Empty` / `ReadOnlyDictionary`).
  - construct_key: dart.mutable_map_environment_spread_merge_and_mutators
    source_form: >-
      final Map<String,TypeDef> types; ... TypeEnvironment merge(other) =>
      TypeEnvironment({...types,...other.types}, {...procedures,
      ...other.procedures}, ...); void addType(td){types[td.name]=td;} void
      addProcedure(pd){procedures[pd.qualifiedKey]=pd;}
    target_decision: >-
      `types`/`procedures`/`paramProcDecls` become
      `Dictionary<string,TypeDef>` / `Dictionary<string,ProcDecl>` instance
      fields (mutable — `addType`/`addProcedure` mutate in place, so C# must
      NOT use `IReadOnlyDictionary` for these). `merge` returns a NEW
      `TypeEnvironment` built from spread-merge: Dart `{...a, ...b}` (b wins
      on key clash) maps to constructing a new dictionary then applying
      `foreach` upsert of `other` entries (or LINQ
      `a.Concat(b).ToDictionary` with last-wins) — the right-bias of Dart map
      spread (later key overwrites) MUST be preserved. `addType` keys by
      `typeDef.Name` while `addProcedure` keys by `procDecl.QualifiedKey`
      (note the asymmetry vs the read path `getProcedure` which keys by
      `name/arity`; preserved verbatim, not normalised).
    idiom_id: null
    research_finding_id: rf-dart-map-spread-merge-to-csharp-dictionary-upsert
    nuance: >-
      Dart map spread `{...x, ...y}` has documented last-wins key semantics;
      C# `Dictionary` indexer-upsert or `ToDictionary` must replicate
      right-bias exactly (a `dict.Add` would throw on duplicate keys — wrong;
      use indexer assignment). Mutability nuance: the environment is a
      mutable accumulator (`addType`/`addProcedure`), so reference-type
      `Dictionary` semantics (shared, in-place mutation) are intentional and
      preserved — this is NOT a value/immutable type and must not be modelled
      as a record. Direct collection mapping; no external research required.
conversion_units:
  - enum TypeClassification { Output, Input, Interactive }
  - abstract class TypeExpr (Line/Column read-only props, protected ctor)
  - sealed class TypeRef : TypeExpr (Name, IsInput, TypeArgs; IsParameterized; Dual(); ToString(); static Builtins/SystemTypes sets; IsBuiltin; IEquatable<TypeRef> Equals/GetHashCode with SequenceEqual)
  - sealed class ConstantAlt : TypeExpr (object Value; ToString())
  - sealed class StructAlt : TypeExpr (Functor, Args; Arity; ToString())
  - sealed class ListNilAlt : TypeExpr (ToString())
  - sealed class ListConsAlt : TypeExpr (Head, Tail; ToString())
  - sealed class PrimitiveModeAlt : TypeExpr (IsInput; ToString())
  - sealed class DiffListAlt : TypeExpr (Content, Hole; ToString())
  - TypeExpr instance members IsInputMode / TypeName / IsPrimitive via type-pattern switch (replaces extension ProcArgTypeExpr)
  - class TypeDef (Name, TypeParams, Alternatives, Line, Column; IsParameterized; Classification getter; static ContainsComplement recursion; ToString())
  - class ProcDecl (Name, ArgTypes, TypeParams, Line, Column, IsBuiltin, Exported, Imported, ModulePath; IsParameterized; Arity; Key; QualifiedKey; IsInputArg; GetTypeName; QualifiedName; ToString())
  - class TypeEnvironment (mutable Dictionary fields; ctor + static Empty(); Merge; GetType; GetProcedure; HasType; HasProcedure; AddType; AddProcedure; ToString via StringBuilder)
escalations: []
```

## Rationale & Research Provenance

This file is a GLP type-declaration AST: a small closed algebraic hierarchy
(`TypeExpr` + seven leaves), one behaviour-free enum, an extension that fakes
subtype dispatch, one value-equality node (`TypeRef`), and a mutable
environment aggregate. The non-mechanical decisions all turn on Dart→C#
*semantics* (sum-type closure, extension static resolution, list-element value
equality, const-collection defaults), each grounded below.

### rf-dart-plain-enum-to-csharp-enum

**Deep analysis.** `TypeClassification` has no fields, methods, or `toString`
— a pure tag enum. Only `output` and `interactive` are ever produced
(`TypeDef.classification`), but `input` is retained to preserve source ordinal
order.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum`
(reused, cached from mode.dart finding) — Microsoft Learn: an enumeration type
"is a value type defined by a set of named constants of the underlying
integral numeric type." A plain Dart enum and a plain C# enum are both
value types compared by value. Verbatim query: "C# enum value type named
constants".

**Conclusion.** 1:1 plain `enum TypeClassification { Output, Input,
Interactive }`, declaration order preserved so default == `Output`. The
behaviour-rich-enum nuance (mode.dart) does not arise.

### rf-dart-abstract-ast-base-to-csharp-abstract-sealed-leaves

**Deep analysis.** `TypeExpr` is `abstract` with two `final` positional
fields and a forwarding constructor. It is *open* in Dart (not a `sealed
class`), but every consumer enumerates the concrete leaves, so it is used as a
closed sum type.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/sealed`
— Microsoft Learn, decisive: *"It's an error to use the abstract modifier
with a sealed class, because an abstract class must be inherited by a class
that provides an implementation of the abstract methods or properties."*
Verbatim query: "C# sealed abstract class hierarchy exhaustive". Therefore the
base cannot be C# `sealed`; closure is expressed by sealing the **leaf**
classes and by exhaustive type-`switch` with a throwing discard arm in
consumers (corroborated by the pattern-matching doc below: the compiler
"throws an exception if the object … doesn't match any of the switch arms").

**Conclusion.** `abstract class TypeExpr` + seven `sealed class … : TypeExpr`
leaves; AST nodes stay reference types (`class`, never `struct`/`record
struct`) so shared sub-tree aliasing and identity are preserved. Totality of
Dart's closed-set consumers is preserved by a throwing default arm, since C#
does not compile-time-verify subtype exhaustiveness over a non-language-sealed
base.

### rf-dart-extension-is-as-to-csharp-type-pattern-switch

**Deep analysis.** `extension ProcArgTypeExpr on TypeExpr` adds three getters
that are pure `is`/`as` subtype dispatch over the project's own hierarchy — no
genuinely external augmentation.

**Research (authoritative).** WebFetch `https://dart.dev/language/extension-methods`
— api/dart.dev official: *"Extension methods are resolved statically … as
fast as calling a static function"* and cannot be invoked on `dynamic`
(NoSuchMethodError). WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching`
— Microsoft Learn: the declaration/type pattern "test the type of the
variable, and assign it to a new variable", fusing `is` test + cast into one
construct; a `switch` expression "perform[s] actions based on the first
matching pattern". Verbatim queries: "Dart extension methods resolved
statically dynamic"; "C# type pattern is declaration switch expression".

**Conclusion.** Because the codebase owns `TypeExpr`, the three getters become
real instance members implemented with C# type-pattern `switch` expressions
(`TypeRef r => r.IsInput`, etc.), collapsing each Dart `is`+`as` pair into one
declaration-pattern arm — strictly safer (runtime dispatch, no
static-resolution-on-dynamic pitfall, no InvalidCast). `String?`→`string?`
preserves the primitive-node null case as `_ => null`.

### rf-dart-list-element-value-equality-to-csharp-sequenceequal

**Deep analysis.** Only `TypeRef` overrides `==`/`hashCode`; equality is
structural and recurses element-wise through `typeArgs` via `_listEquals`
(which calls `TypeExpr ==`). `dual()` returns a new `TypeRef` *sharing the
same* `typeArgs` list (shallow). The other six nodes keep default reference
identity.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record`
— Microsoft Learn, decisive: synthesized record equality "uses the declared
data members"; init-only/reference members have *shallow* semantics and the
documented example shows two records sharing an array are `==` while
structurally-equal-but-distinct collections are not — i.e. a `List<>` member
compares by **reference**, not element-wise. Verbatim query: "C# record value
equality collection member reference equality". This is exactly the regression
`_listEquals` exists to prevent.

**Conclusion.** `TypeRef` must be a hand-written `sealed class` implementing
`IEquatable<TypeRef>` with `Name`/`IsInput` checks plus
`TypeArgs.SequenceEqual(other.TypeArgs)` and a structural `HashCode`
mirroring `Object.hash`/`Object.hashAll`. A positional `record` is explicitly
rejected (would silently break `Stream(Integer)` equality). The six
non-overriding nodes map to plain reference-identity classes — the asymmetry
is preserved deliberately. `dual()` keeps the shared-list shallow-copy
semantics (alias the same backing list, do not clone).

### rf-dart-const-set-to-csharp-frozenset-ordinal

**Deep analysis.** `TypeRef.builtins` and `TypeRef.systemTypes` are Dart
`static const` set literals used only for `.contains(name)` membership tests
(`isBuiltin`, `hasType`). They are immutable and exact-string keyed.

**Research (authoritative).** WebFetch
`https://learn.microsoft.com/en-us/dotnet/api/system.collections.frozen.frozenset`
— Microsoft Learn: `FrozenSet` "Provides a set of initialization methods" and
its `Create<T>(IEqualityComparer<T>, ReadOnlySpan<T>)` / `ToFrozenSet<T>(…,
IEqualityComparer<T>)` overloads accept an `IEqualityComparer<T>`, the
authoritative hook for supplying `StringComparer.Ordinal`. C# has no `const`
collection; the documented immutable-set idiom is `FrozenSet<string>` (or
`static readonly HashSet<string>`) initialised once. Verbatim query: "C#
FrozenSet immutable string set ordinal comparer membership".

**Conclusion.** Map both `const` sets to `private static readonly
FrozenSet<string>` (fallback `HashSet<string>`) built with
`StringComparer.Ordinal` so membership recognition is byte-exact like Dart's
`Set<String>` — preventing culture-sensitive drift in builtin/system-type
classification. Exposed via the `IsBuiltin` getter unchanged.

### rf-dart-factory-ctor-const-default-to-csharp-static-factory

**Deep analysis.** `TypeEnvironment.empty()` is a Dart `factory` constructor
returning a fresh empty environment; the generative ctor uses a `const {}`
default for `typeTemplates` and `?? {}` for `paramProcDecls`.

**Research (authoritative).** WebFetch `https://dart.dev/language/constructors`
— dart.dev official: a factory constructor "doesn't always create a new
instance of its class" (may return a cache/subtype) and "can't access
`this`"; const constructors "create compile-time constants" when fields are
`final` and const-initialised — so the `const {}` default is a single
immutable compile-time map, not a fresh mutable allocation. dart.dev names the
C# analog explicitly: "a static factory method … enabling caching, subtype
returns, and conditional creation". Verbatim query: "Dart factory constructor
const default parameter C# static factory method".

**Conclusion.** `factory .empty()` → `public static TypeEnvironment Empty()`
static method (no language `factory` in C#); here it always returns a fresh
instance (no caching in the source). The `const {}` immutable default must NOT
become one shared mutable static `Dictionary` (would alias mutable state
across instances) — emit a fresh `Dictionary` per construction or an immutable
empty (`ImmutableDictionary<,>.Empty` / `ReadOnlyDictionary`).

### rf-dart-map-spread-merge-to-csharp-dictionary-upsert

**Deep analysis.** `merge` builds a new environment via Dart map spread
`{...types, ...other.types}` (and the same for the other three maps); `addType`
/`addProcedure` mutate the dictionaries in place. The environment is a mutable
accumulator, not a value object.

**Research (authoritative).** WebFetch
`https://dart.dev/language/collections` (dart.dev official, reused/cached) —
documents collection spread `...` and that in a map spread a later duplicate
key **overwrites** an earlier one (last-wins / right-bias). The C# counterpart
must replicate this with indexer upsert (`dict[k] = v`) or
`Concat(...).ToDictionary` with last-wins — `Dictionary.Add` would throw on a
duplicate key and is therefore wrong. Verbatim query: "Dart map spread
duplicate key last wins; C# Dictionary upsert last-wins".

**Conclusion.** `merge` → construct a new `Dictionary` then upsert `other`'s
entries (right-bias preserved). The mutable `addType`/`addProcedure` path means
`types`/`procedures`/`paramProcDecls` stay mutable `Dictionary` instance fields
(reference semantics, in-place mutation intentional) — `TypeEnvironment` is
therefore a `class`, never a `record`. The read/write key asymmetry
(`getProcedure` keys `name/arity`; `addProcedure` keys `qualifiedKey`) is
preserved verbatim, not normalised.

### Trivial constructs

File header `//` comments and `///` doc-comments are non-code and map
mechanically to C# `//` / XML-doc (trivial, no research). All other
constructs carry both a deep-analysis basis and an authoritative
research_finding_id above.
