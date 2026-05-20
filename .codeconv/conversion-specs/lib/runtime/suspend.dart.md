> Conversion-spec artifact for lib/runtime/suspend.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/suspend.dart
source_sha256: a654fdbd8a51e7a83cc8be4d6a6a653efef4f2e7378c28eebd6ecc7ecc421c8f
target_code_unit: lib/runtime/suspend.cs
constructs:
  - construct_key: "dart.import_directive.relative-same-package.machine_state"
    source_form: >-
      "`import 'machine_state.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/machine_state.dart`. The
      directive brings the typedef `ReaderId` (= `int`) into this file's
      scope (consumed as the type of the `readerId` field of
      `SuspensionNote`). No `show`/`hide` clause -- the full public
      surface is imported but only the `ReaderId` alias is referenced."
    target_decision: >-
      NO standalone target artefact for the import; instead the
      converted `lib/runtime/suspend.cs` adds a `using` directive that
      names the .NET namespace hosting the converted `machine_state.cs`
      (where the ported `ReaderId` alias lives, per the convspec at
      .codeconv/conversion-specs/lib/runtime/machine_state.dart.md
      construct "typedef opaque-int-identifier GoalId Pc ReaderId
      WriterId"). The namespace name is decided by the downstream
      depgraph/namespace step, not this spec. The Dart relative-import
      is NOT a 1:1 file-to-file `using`: in .NET the import unit is the
      namespace, not the file, and .NET has no per-symbol `show` clause
      to translate. Codegen MUST NOT emit a textual relative-path
      `using` (e.g. `using ./machine_state.cs`) -- that is not valid
      C#. The consumed alias `ReaderId` is reached transparently as
      `int` once the `global using ReaderId = int;` directive (decided
      in machine_state.dart.md) is in scope. Idiom reused verbatim from
      .codeconv/conversion-specs/lib/runtime/hanger.dart.md (same
      relative import, same sibling target).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Import-unit nuance: Dart imports a *library/file*; C# imports a
      *namespace*. The 1:1 mapping is "each Dart import line -> one C#
      `using <namespace>;` line that resolves to the namespace of the
      converted target file"; the depgraph/namespace stage owns the
      filename->namespace mapping (it knows where converted siblings
      live). Show/hide nuance: ABSENT here (no `show`/`hide`); when
      present elsewhere, per the goal_queue.dart.md / fairness.dart.md
      precedent there is no faithful C# counterpart for per-symbol
      narrowing because `using` imports the full public surface.
      Value-vs-reference / null-safety / async / Stream / isolate: NOT
      APPLICABLE -- a directive declares no values/types and has no
      runtime form. Reference-identity: NOT APPLICABLE -- imports do
      not produce instances.
  - construct_key: "dart.import_directive.relative-same-package.hanger"
    source_form: >-
      "`import 'hanger.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/hanger.dart`. The
      directive brings the class `Hanger` into this file's scope
      (consumed as the type of the `hanger` field of `SuspensionNote`).
      No `show`/`hide` clause -- the full public surface is imported
      but only `Hanger` is referenced."
    target_decision: >-
      NO standalone target artefact for the import; instead the
      converted `lib/runtime/suspend.cs` adds a `using` directive that
      names the .NET namespace hosting the converted `hanger.cs` (where
      the ported `Hanger` class lives, per the convspec at
      .codeconv/conversion-specs/lib/runtime/hanger.dart.md construct
      "dart.class.mutable-state-container-identity-equality-three-fields-named-only-ctor-two-required-one-defaulted-bool").
      The namespace name is decided by the downstream depgraph/namespace
      step, not this spec. The Dart relative-import is NOT a 1:1
      file-to-file `using`: in .NET the import unit is the namespace,
      not the file, and .NET has no per-symbol `show` clause to
      translate. Codegen MUST NOT emit a textual relative-path `using`
      (e.g. `using ./hanger.cs`) -- that is not valid C#. The consumed
      `Hanger` type is reached as the namespace-qualified `Hanger`
      class once the `using` is in scope. Idiom reused verbatim from
      .codeconv/conversion-specs/lib/runtime/hanger.dart.md (same
      relative-import mapping, different sibling target).
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Import-unit nuance: identical to the machine_state.dart import
      above -- Dart imports a *library/file*; C# imports a
      *namespace*. The 1:1 mapping is "each Dart import line -> one C#
      `using <namespace>;` line that resolves to the namespace of the
      converted target file". If the converted `hanger.cs` and
      `suspend.cs` ultimately land in the same `lib/runtime/` namespace
      (the project's mirroring convention), the `using` may be elided
      as redundant -- the depgraph/namespace stage decides. Show/hide
      nuance: ABSENT (no `show`/`hide`). Value-vs-reference /
      null-safety / async / Stream / isolate: NOT APPLICABLE -- a
      directive declares no values/types and has no runtime form.
      Reference-identity: NOT APPLICABLE -- imports do not produce
      instances.
  - construct_key: "dart.class.immutable-aggregate-aggregating-mutable-reference-two-final-fields-positional-const-ctor-identity-equality"
    source_form: >-
      "`class SuspensionNote { final ReaderId readerId; final Hanger
      hanger; const SuspensionNote(this.readerId, this.hanger); }` -- a
      Dart class with (a) two `final` non-nullable fields: `readerId`
      (type `ReaderId` = `int`, opaque identifier value type) and
      `hanger` (type `Hanger`, reference to a mutable Hanger
      instance); (b) a single `const` positional constructor with the
      Dart field-formal-parameter shorthand `this.readerId,
      this.hanger`. No `==`/`hashCode` override -- default identity
      equality. No `toString` override. No mutator methods. No body on
      the constructor. The class is an immutable note pairing a reader
      identity with the Hanger that arms the suspended goal -- the
      class itself is immutable (both fields `final`) but it
      AGGREGATES a reference to a mutable `Hanger` whose `armed` field
      is flipped by the wake path (per hanger.dart.md: 'armed=true at
      creation; first wake sets to false')."
    target_decision: >-
      Map to a reference-type .NET `class` `SuspensionNote` (NOT
      `record`, NOT `record class`, NOT `struct`, NOT `record
      struct`). The class has: a `public ReaderId ReaderId { get; }`
      (init-only via constructor, mirroring Dart `final`); a `public
      Hanger Hanger { get; }` (init-only, non-nullable reference to a
      Hanger instance). Single non-optional-params constructor `public
      SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId =
      readerId; Hanger = hanger; }`. The PascalCase identifier rename
      is per the .NET capitalisation conventions (`readerId` ->
      `ReaderId`, `hanger` -> `Hanger`). NOT a `record` / `record
      class`: would inject value-equality on every field -- a
      correctness bug because the runtime stores `SuspensionNote`
      instances in per-reader / per-goal suspension structures and
      asks 'is this *the* note I attached?' which is reference
      identity (two notes with coincidentally identical
      `readerId`+`Hanger`-reference are NOT the same note -- they
      could be sequential attachments produced by distinct suspend
      calls; the Dart source has NO `==` override and the .NET port
      MUST preserve that). NOT a `struct` / `record struct`: would
      copy on assignment. The `SuspensionNote.Hanger` field IS the
      reference whose `Armed` flip propagates to every observer
      (single-reactivation invariant per hanger.dart.md); a struct
      copy still holds the SAME `Hanger` reference and would not
      itself break that propagation (the inner aggregated reference is
      the load-bearing carrier), BUT the OUTER class' identity is
      ALSO load-bearing because the runtime uses the SuspensionNote
      reference itself as the per-(reader, goal) attachment handle in
      its lookup tables; two distinct attachments must be
      distinguishable, and value-equality / struct-copy would silently
      coalesce them. The Dart-source SuspensionRecord / SuspensionListNode
      precedent in suspension.dart.md is the canonical FCP-suspension
      shape (reference-only); SuspensionNote sits in the same design
      family and the same reference-class decision applies. The Dart
      `const` constructor permits compile-time-canonicalised literal
      instances (`const SuspensionNote(0, h)`); .NET has NO
      `const`-constructor concept for reference classes -- the
      `const`-ness is unobservable for a reference type (each call
      site allocates a new instance, identity is per-allocation), and
      the C# port preserves the runtime semantics by emitting a plain
      constructor. The `const`-ness is correctly elided; no `readonly
      record struct` (which is the .NET counterpart for compile-time
      value-type literals) because the identity-equality + aggregates-
      a-mutable-reference combination forbids a value type here.
    idiom_id: null
    research_finding_id: rf-dart-final-field-class-to-csharp-getonly-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY -- the LOAD-BEARING nuance for this
      construct. Dart `class SuspensionNote` uses default identity
      equality (no `==` override). The .NET counterpart MUST be `class`
      (reference type), NEVER `record`/`record class`/`record
      struct`/`struct`. Reasoning: (i) the runtime attaches one
      `SuspensionNote` per (reader, goal) pairing and stores those
      references in per-reader suspension structures; the lookup /
      removal protocol asks 'is this *the* note I attached?' which is
      reference identity -- the same scheduler-identity reasoning that
      drives GoalState/Hanger in machine_state.dart.md and
      hanger.dart.md. (ii) Two distinct notes with coincidentally
      identical `readerId` and `Hanger` reference (e.g. when two
      separate suspend calls happen to reuse the same Hanger for a
      retry path) are NOT the same note -- value-equality (record /
      record class) would silently make them compare equal, breaking
      the per-attachment bookkeeping. (iii) Aggregation: the `Hanger`
      field is itself a mutable reference; the SuspensionNote being a
      reference type means every alias to the same note observes the
      same `Hanger` reference, and (via the Hanger's reference-type
      decision in hanger.dart.md) the same `Armed` mutation. A
      `record class` would not break the inner-Hanger propagation but
      would break the outer identity, and a `struct` would force
      copy-on-assignment of the OUTER note (each copy holds the same
      Hanger reference, so single-reactivation still works via the
      inner mutable, BUT the outer attachment-handle identity is
      lost -- the scheduler can no longer ask 'is this *the*
      attachment I stored?'). Init-only-vs-set nuance: both fields
      are Dart `final` and become C# `{ get; }` (init-only via
      constructor, conservative form -- consistent with
      machine_state.dart.md / GoalState and cells.dart.md /
      WriterCell). C# 9 `{ get; init; }` is acceptable but
      unnecessary; get-only matches the Dart `final` invariant
      (written exactly once, at construction) and avoids exposing a
      `with`-expression mutation surface the Dart side does not have.
      Public mutable field (`public Hanger Hanger;`) is rejected
      because the Dart side does NOT expose a setter (the field is
      `final`); a public mutable field would advertise a write
      surface the source does not have. Null-safety: every field is
      non-nullable on the Dart side (no `?` annotation on either
      field); under enabled NRT every corresponding C# property is
      non-nullable -- `ReaderId` is a value type (`int` alias) and
      cannot be null; `Hanger` is a non-nullable reference (the
      contract REQUIRES a Hanger to be present at construction --
      there is no "note without a hanger"). Const-constructor nuance:
      Dart `const SuspensionNote(this.readerId, this.hanger)` enables
      compile-time-canonicalised literal instances when called with
      `const` prefix; .NET reference classes have NO equivalent
      compile-time-canonicalisation mechanism -- the C# counterpart is
      a plain constructor and each call site allocates a new instance.
      The Dart const-canonicalisation is unobservable from this file
      (no `const SuspensionNote(...)` literal call sites in the source
      shown), and the file's identity-equality semantics make
      canonicalisation semantically irrelevant -- two coincidentally-
      equal notes are still distinct attachments. Const-ness is
      correctly elided. Mutable Hanger aggregation nuance (explicitly
      addressed, NOT glossed): the OUTER `SuspensionNote` is
      immutable (both `final`), the INNER `Hanger` is mutable
      (`Armed` flips). This is the textbook "immutable aggregate of a
      mutable reference" shape -- the .NET port preserves it exactly
      by making the outer a reference class with `{ get; }`
      properties; the inner Hanger's mutability is provided by the
      Hanger conversion (hanger.dart.md), not by this file. No
      defensive copy / no clone-on-construction -- the Dart source
      stores the reference verbatim, and the .NET port does the
      same. Reference-identity equality contract: `SuspensionNote`
      has NO `==`/`hashCode` override on the Dart side; the .NET
      port preserves this by being a `class` (default reference
      equality) with no `Equals`/`GetHashCode` overrides. No
      `Deconstruct`/`with` machinery is introduced. ASYNC / STREAM /
      FUTURE / COMPLETER / ISOLATE / MIXIN / SEALED / GENERIC /
      ABSTRACT: ABSENT -- correctly not asserted (FR-009: address the
      nuances that exist, do not invent). The class has no methods at
      all (only the constructor); no async surface. The .NET port
      MUST NOT introduce `Task` / `async` / `Channel<T>` /
      `IAsyncEnumerable` here. Trail / choice-points / WAM-style
      backtracking: ABSENT -- `SuspensionNote` is a per-(reader,
      goal) suspension attachment handle, NOT a trail or
      choice-point entry; the .NET port MUST NOT introduce trail /
      choice-point fields here.
  - construct_key: "dart.positional-ctor-field-formal-shorthand-no-body"
    source_form: >-
      "`const SuspensionNote(this.readerId, this.hanger);` -- a
      single positional constructor using Dart's field-formal-
      parameter shorthand (`this.X` binds the parameter directly to
      the same-named instance field). Two positional parameters; no
      initialiser list; no body; `const`-qualified so the constructor
      can be invoked as a compile-time-constant expression."
    target_decision: >-
      A single C# constructor with positional parameters: `public
      SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId =
      readerId; Hanger = hanger; }`. Body assigns each backing
      property explicitly (no field-formal-parameter shorthand in
      C#). Parameter order preserved verbatim (`readerId, hanger`).
      NOT a positional record (`record SuspensionNote(int ReaderId,
      Hanger Hanger)`) because of the identity-equality requirement
      established in the class-mapping construct above. NOT C# 11
      `required` properties (would force object-initialiser call
      sites, inconsistent with the rest of the runtime port -- same
      reasoning as GoalState / Hanger). NOT a primary constructor
      (C# 12, only saves boilerplate; consistency with the rest of
      the runtime port favours the explicit-body constructor form
      used by hanger.dart.md and machine_state.dart.md). The Dart
      `const` qualifier is elided (see const-constructor nuance on
      the class-mapping construct above). Reused verbatim from the
      rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment
      idiom established in cells.dart.md / WriterCell-ReaderCell ctors.
    idiom_id: null
    research_finding_id: rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment
    nuance: >-
      Field-formal-parameter shorthand nuance: Dart's
      `this.fieldName` parameter form binds the parameter value to
      the same-named instance field at construction time; C# has no
      equivalent shorthand so the .NET counterpart MUST emit explicit
      assignments in the constructor body (`Field = field;` per
      parameter). Positional-vs-named-argument nuance: this is a
      positional Dart ctor (no `{...}` named-args block); the C#
      counterpart is straightforward positional parameters -- callers
      MAY use C# named-argument call-site syntax (`new
      SuspensionNote(readerId: 0, hanger: h)`) but the construct does
      not require it. No defaults: both parameters are required at
      the call site (Dart positional without `=` default == required).
      Const-constructor nuance: identical to the class-mapping
      construct above -- Dart `const SuspensionNote(...)` enables
      compile-time-canonicalised literal instances; .NET has no
      reference-type counterpart; const-ness is correctly elided. The
      run-time-allocation semantics are unchanged. Null-safety: both
      parameters are non-nullable; no `?` annotations; no
      null-coalesce needed. ASYNC / STREAM / mixin / sealed: ABSENT
      -- correctly not asserted. The named-required-with-defaults
      idiom established in machine_state.dart.md / hanger.dart.md is
      NOT applicable here (this ctor has no defaults and no named
      args); the simpler positional-with-explicit-assignment idiom
      from cells.dart.md applies instead.
conversion_units:
  - "`using` directive in lib/runtime/suspend.cs pointing at the namespace of the converted lib/runtime/machine_state.cs (depgraph/namespace step owns the exact namespace name); transparently brings `global using ReaderId = int;` into scope."
  - "`using` directive in lib/runtime/suspend.cs pointing at the namespace of the converted lib/runtime/hanger.cs (may be elided if the depgraph/namespace step places hanger.cs and suspend.cs in the same namespace mirroring lib/runtime/)."
  - "`public class SuspensionNote` in the namespace mirroring lib/runtime/ -- reference type, identity equality, NO `record`/`record class`/`struct`/`record struct` (load-bearing per the rf-dart-final-field-class-to-csharp-getonly-class idiom; immutable outer aggregating a mutable Hanger reference)."
  - "  - get-only properties: `public ReaderId ReaderId { get; }`, `public Hanger Hanger { get; }` (init via ctor; Dart `final` -> conservative `{ get; }` form; both non-nullable)"
  - "  - constructor: `public SuspensionNote(ReaderId readerId, Hanger hanger) { ReaderId = readerId; Hanger = hanger; }` -- positional params, explicit-assignment body (no field-formal shorthand in C#); Dart `const` qualifier elided (no .NET counterpart for reference classes)"
  - "  - NO XML doc-comment on the class (the Dart source has none); NO `==`/`hashCode`/`Equals`/`GetHashCode` override; NO `toString`/`ToString` override; NO mutator methods"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-namespace-using -- relative-import mapping (carry-over)

The Dart spec defines `import 'machine_state.dart';` and
`import 'hanger.dart';` as directives that make the public top-level
identifiers of the imported library available in the importing
library. The official Dart language tour
(https://dart.dev/language/libraries) documents that an import names a
*library* (one Dart file `≡` one library by default) and that the
imported library's public surface becomes available unqualified.

The official C# language reference (Microsoft Learn:
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
documents that the `using` directive names a *namespace*, and that
namespaces are decoupled from filenames (a single namespace can be
split across many files; a single file can declare many namespaces).
This asymmetry is authoritative: there is no C# directive that imports
a single file, and no mechanism for narrowing the imported surface to
a specific symbol.

Consequence: the faithful conversion of a Dart relative-import is a
`using <namespace>;` line in the converted target file, where the
namespace name is the namespace of the converted sibling -- decided by
the downstream depgraph/namespace stage, not by this convspec. The
precedent at
`.codeconv/conversion-specs/lib/runtime/hanger.dart.md` (the same
relative-import shape, same machine_state sibling) is reused here
verbatim. The consumed alias `ReaderId` is reached as plain `int` once
the `global using ReaderId = int;` established in
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` is in
scope -- no qualified-name path is needed because the alias is
transparent. The consumed `Hanger` type is reached as the
namespace-qualified `Hanger` class once the `using` is in scope. Idiom
reused verbatim; `rf-dart-relative-import-to-csharp-namespace-using` is
the recurring project idiom for relative-import mapping (SC-007).

### rf-dart-final-field-class-to-csharp-getonly-class -- SuspensionNote (carry-over from cells.dart / WriterCell + ReaderCell)

Deep analysis. `SuspensionNote` is an immutable identifier-bearing
attachment handle -- two `final` fields (`readerId`, `hanger`) and a
positional `this.x` constructor with no body. The runtime stores
SuspensionNote references in per-reader suspension structures and uses
the reference itself as the per-(reader, goal) attachment handle in
its lookup / removal protocol -- reference identity is the only
equality semantics the source has (no `==` override). Two distinct
notes with coincidentally identical (readerId, hanger-reference) are
NOT the same note (they could be sequential attachments produced by
distinct suspend calls). The class additionally AGGREGATES a reference
to a mutable `Hanger` (per hanger.dart.md, `Hanger.armed` flips on the
first wake); the outer-class-being-reference is what guarantees every
holder of the same SuspensionNote sees the same Hanger reference, and
through that, the same `armed` mutation.

Authoritative Dart. The Dart language tour
(https://dart.dev/language/class-modifiers) -- Dart class instances are
heap objects with identity; `final` instance fields are write-once
(https://dart.dev/language/classes#instance-variables documents
'`final` -- must be initialized exactly once'). The `const`
constructor form is documented at
https://dart.dev/language/constructors#constant-constructors --
'creating compile-time constants' -- it permits literal instances to
be canonicalised at compile time, but does NOT change run-time
allocation or identity semantics for non-`const`-invoked call sites.

Authoritative .NET. Microsoft Learn's reference-types documentation
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types)
is unambiguous: 'Variables of reference types store references to
their data (objects), while variables of value types directly contain
their data. With reference types, two variables can reference the same
object; therefore, operations on one variable can affect the object
referenced by the other variable.' This is precisely the
aggregated-mutable-Hanger propagation contract `SuspensionNote` relies
on. The records page
(https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record)
states: 'Records use value-based equality. Two record instances are
equal if they're of the same type and store the same values.'
Value-based equality is the OPPOSITE of what this file requires; two
distinct suspension notes with identical (readerId, hanger-reference)
represent distinct attachments and MUST NOT compare equal. `record
class` is rejected. The structs page
(https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/struct)
states: 'Structure types have value semantics. That is, a variable of
a structure type contains an instance of the type.' Struct copies of
the outer SuspensionNote would lose the per-attachment-handle identity
the runtime relies on (even though the aggregated inner Hanger
reference would still propagate `Armed` correctly through the copy --
the inner mutation channel is preserved by the Hanger's own reference-
type decision, but the OUTER attachment-handle identity is lost).
Both rejections are documented in the official language reference, not
stylistic preferences.

Auto-property with get-only. Microsoft Learn's auto-implemented
properties page
(https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/auto-implemented-properties)
documents `public T Name { get; } = ...` (or constructor-initialised)
as the canonical shape for write-once compile-time-or-construction-time
properties -- the exact .NET counterpart of Dart `final` fields. C# 9
init-only properties (`{ get; init; }`) are acceptable but unnecessary
here; get-only matches the Dart `final` invariant (written exactly
once, at construction) and avoids exposing a `with`-expression
mutation surface the Dart side does not have.

Const-constructor handling. Dart `const SuspensionNote(...)` permits
the constructor to be invoked in a `const` expression context for
compile-time-canonicalised literals; .NET reference classes have NO
equivalent (`const` in C# is limited to compile-time-constant
primitive types and string literals --
https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constants).
The Dart const-canonicalisation is unobservable from this file
(no `const SuspensionNote(...)` literal call sites are shown), and the
file's identity-equality semantics make canonicalisation semantically
irrelevant (two coincidentally-equal notes are still distinct
attachments). The C# port emits a plain constructor and each call site
allocates a new instance -- preserving the runtime semantics exactly.
A `readonly record struct` is REJECTED here despite being the .NET
counterpart for compile-time value-type literals (per GoalRef in
machine_state.dart.md) because the identity-equality + aggregates-a-
mutable-reference combination forbids a value type.

Why this idiom rather than `rf-dart-mutable-state-class-identity-
equality-to-csharp-class` (Hanger / GoalState). SuspensionNote is
IMMUTABLE outer (both fields `final`, no mutators) but aggregates a
mutable inner reference; the cells.dart.md / WriterCell-ReaderCell
shape (immutable `final` fields, positional ctor, reference identity,
NOT a record) is the structural match. The mutable-state-container
idiom applies when the outer class itself mutates; here the outer is
immutable, the inner is mutable, and the reference-class decision is
driven by identity-equality of the attachment handle, not by outer
mutability.

Decision. `public class SuspensionNote { public ReaderId ReaderId
{ get; } public Hanger Hanger { get; } public SuspensionNote(ReaderId
readerId, Hanger hanger) { ReaderId = readerId; Hanger = hanger; } }`.
Authoritative both sides (api.dart.dev / dart.dev for Dart, Microsoft
Learn for .NET); no escalation. Same idiom reused as for WriterCell
and ReaderCell in cells.dart.md -- the project's canonical immutable-
identifier-bearing-reference-class idiom (SC-007).

### rf-dart-positional-ctor-with-this-shorthand-to-csharp-positional-ctor-with-explicit-assignment -- SuspensionNote ctor (carry-over from cells.dart)

Deep analysis. The Dart ctor `const SuspensionNote(this.readerId,
this.hanger)` uses Dart's field-formal-parameter shorthand
(https://dart.dev/language/constructors#using-initializing-formal-parameters)
-- the parameter's value is assigned to the same-named field at
construction time. There is no initialiser list, no body, and no
defaults; both parameters are positional and required.

Authoritative Dart.
https://dart.dev/language/constructors#using-initializing-formal-parameters
documents the `this.X` form as a shorthand: 'Most programming languages
have constructor parameters that initialize instance variables. Dart's
initializing formal parameters let you do this more concisely.' The
shorthand is purely syntactic sugar for `param; ... this.field =
param;`.

Authoritative .NET. The C# constructor reference
(https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/constructors)
documents the standard form with positional parameters and explicit
assignments to backing properties/fields. There is no shorthand
counterpart; the explicit-body form is the .NET-canonical translation.
The C# 12 primary constructor (introduced for classes in
https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-12#primary-constructors)
is REJECTED here for consistency with the rest of the runtime port
(hanger.dart.md, machine_state.dart.md, cells.dart.md all use the
explicit-body constructor form); the primary-constructor form has
subtle scoping semantics (parameters are captured into the class body)
that the runtime port has consistently chosen not to engage with.

Why not a positional `record` (`record SuspensionNote(int ReaderId,
Hanger Hanger)`). Positional records synthesise value-equality and
`Deconstruct`/`with` machinery that the Dart source does not have; the
identity-equality requirement established in the class-mapping
construct above forbids this.

Why not C# 11 `required` modifier on properties. Requires callers to
use object-initialiser syntax, which the runtime port consistently
avoids (same reasoning as GoalState / Hanger -- the constructor form
is the runtime-port idiom).

Decision. `public SuspensionNote(ReaderId readerId, Hanger hanger) {
ReaderId = readerId; Hanger = hanger; }`. Authoritative; no
escalation. Same idiom as the WriterCell / ReaderCell constructors in
cells.dart.md -- the project's canonical positional-ctor-with-explicit-
assignment idiom (SC-007). The const-constructor nuance is handled at
the class level (see above) and is independent of the ctor-body shape.

## Notes -- well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Mutable state container vs record/struct/record-struct**: LOAD-
  BEARING. One reference class (`SuspensionNote`) -- the decision
  rule is identical to WriterCell / ReaderCell in cells.dart.md
  (immutable identifier-bearing class with reference identity ->
  `class`; NEVER `record` / `record class` / `struct` / `record
  struct`). The outer class is immutable (both fields `final`), the
  inner Hanger is mutable (per hanger.dart.md); the reference-class
  decision is driven by identity-equality of the per-(reader, goal)
  attachment handle, not by outer mutability.
- **Reference identity** for `SuspensionNote` is the only equality
  semantics the source has (no `==` override); preserved verbatim.
  The class is used as a per-attachment handle in the runtime's
  suspension lookup tables; reference identity is the only contract
  that distinguishes two coincidentally-equal attachments.
- **Aggregation of mutable sub-components**: ADDRESSED -- the
  `Hanger` field is itself a mutable reference; the SuspensionNote
  being a reference type ensures every alias to the same note
  observes the same `Hanger` reference, and through Hanger's own
  reference-type decision (hanger.dart.md), the same `Armed`
  mutation. No defensive copy / no clone-on-construction; the Dart
  source stores the reference verbatim and the .NET port does the
  same. The textbook "immutable aggregate of a mutable reference"
  shape is preserved exactly.
- **Const-constructor**: ADDRESSED -- Dart `const SuspensionNote(...)`
  enables compile-time-canonicalised literal instances; .NET
  reference classes have no equivalent; the `const`-ness is
  correctly elided. A `readonly record struct` is REJECTED because
  identity-equality forbids a value type here. (Contrast: GoalRef in
  machine_state.dart.md DOES get the `readonly record struct`
  treatment because GoalRef has an explicit `==`/`hashCode`
  override -- value equality -- and SuspensionNote does not.)
- **Trail / choice-points / WAM-style backtracking**: ABSENT from
  this file. `SuspensionNote` is a per-(reader, goal) suspension
  attachment handle, NOT a trail or choice-point entry; the .NET
  port MUST NOT introduce trail / choice-point fields here. Correctly
  not asserted.
- **Null-safety**: every field/parameter is non-nullable on both
  sides (no `?` annotations in the Dart source); under enabled NRT
  every corresponding C# property is non-nullable. `ReaderId` is a
  value type (`int` alias) and cannot be null; `Hanger` is a
  non-nullable reference (the contract REQUIRES a Hanger to be
  present at construction -- there is no "note without a hanger").
- **Concurrency model**: this file is single-threaded as declared
  (no isolates, no locks, no async, no Future/Stream/Completer in
  the source). The `SuspensionNote` is constructed by the suspend
  path and read by the wake path; the single-owning-context
  invariant of the surrounding runtime is what makes the plain
  get-only properties safe; if a future cross-isolate suspension
  ever arises that is an explicit re-scope and should be ESCALATED
  at that point, not silently upgraded to `Interlocked` / `volatile`
  here.
- **Async / Stream / IAsyncEnumerable / Future / Completer / Task /
  TaskCompletionSource**: ABSENT. The class is a plain data holder
  with no methods at all (only the constructor); no async surface.
  The .NET port MUST NOT introduce `Task` / `async` / `Channel<T>`
  / `IAsyncEnumerable` here.
- **Mixin / sealed / abstract / generic**: ABSENT. The Dart class is
  plain (no `mixin`, no `sealed`, no `abstract`, no type
  parameters); the C# class is plain (no `sealed`, no `abstract`,
  no type parameters); correctly not asserted.
- **Identifier casing**: Dart `camelCase` field names (`readerId`,
  `hanger`) become `PascalCase` public properties per the .NET
  capitalisation guideline
  (https://learn.microsoft.com/dotnet/standard/design-guidelines/capitalization-conventions);
  parameter names stay `camelCase` (`readerId`, `hanger`) per the
  same convention. The class name `SuspensionNote` is already
  PascalCase on the Dart side and is preserved verbatim. Note the
  property-named-same-as-its-type case for `Hanger Hanger { get; }`
  -- this is permitted in C# and matches the Dart-side `Hanger
  hanger` field declaration; no rename is needed.
- **Top-level binding rehoming**: NOT APPLICABLE here -- this file
  contains no top-level functions, constants, or typedefs; just the
  single class `SuspensionNote`. Classes are namespace-scoped types
  in both languages.
- **Equality contract**: `SuspensionNote` has NO `==`/`hashCode`
  override -- default identity equality. The .NET port preserves
  this exactly by being a `class` (default reference equality), with
  no `Equals`/`GetHashCode` overrides. No `Deconstruct`/`with`
  machinery is introduced.
- **Zero escalations**: every non-trivial construct in this file is
  resolvable from authoritative Dart and .NET official documentation
  and from established project idioms reused verbatim from
  hanger.dart.md (relative-import, ctor-positional-with-defaults),
  cells.dart.md (immutable identifier-bearing reference class with
  positional ctor + explicit-assignment body), and
  machine_state.dart.md (typedef alias, const-vs-readonly handling)
  (FR-013/FR-024).
