> Conversion-spec artifact for lib/runtime/hanger.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/hanger.dart
source_sha256: 162457ab2f6db96de5e7e7beb5ae3acd6ed5dea548ea9ef7106b32fa0522403f
target_code_unit: lib/runtime/hanger.cs
constructs:
  - construct_key: "dart.import_directive.relative-same-package.machine_state"
    source_form: >-
      "`import 'machine_state.dart';` -- a relative import of the
      same-package sibling library `lib/runtime/machine_state.dart`. The
      directive brings the typedefs `GoalId` (= `int`) and `Pc` (= `int`)
      into this file's scope (consumed as the types of the `goalId` and
      `kappa` fields of `Hanger`). No `show`/`hide` clause -- the full
      public surface is imported but only the two typedef aliases are
      referenced."
    target_decision: >-
      NO standalone target artefact for the import; instead the converted
      `lib/runtime/hanger.cs` adds a `using` directive that names the .NET
      namespace hosting the converted `machine_state.cs` (where the
      ported `GoalId` and `Pc` aliases live, per the convspec at
      .codeconv/conversion-specs/lib/runtime/machine_state.dart.md
      construct "typedef opaque-int-identifier GoalId Pc ReaderId
      WriterId"). The namespace name is decided by the downstream
      depgraph/namespace step, not this spec. The Dart relative-import is
      NOT a 1:1 file-to-file `using`: in .NET the import unit is the
      namespace, not the file, and .NET has no per-symbol `show` clause
      to translate. Codegen MUST NOT emit a textual relative-path `using`
      (e.g. `using ./machine_state.cs`) -- that is not valid C#. The two
      consumed aliases `GoalId` and `Pc` are reached transparently as
      `int` once the `global using GoalId = int;` and `global using Pc
      = int;` directives (decided in machine_state.dart.md) are in
      scope. Idiom reused verbatim from
      .codeconv/conversion-specs/lib/runtime/fairness.dart.md (same
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
  - construct_key: "dart.docblock_triple_slash"
    source_form: >-
      "`/// Hanger: ensures single reactivation when a goal suspended on
      multiple readers.`" -- a single triple-slash doc line above the
      `Hanger` class declaration, plus two `//`-line comments inside the
      class body (`// restart at clause selection`,
      `// true at creation; first wake sets to false`) -- the latter two
      are non-doc trailing comments on the `kappa` and `armed` field
      declarations.
    target_decision: >-
      Map the `///` doc-comment to a C# XML-doc comment on the class:
      `/// <summary>Hanger: ensures single reactivation when a goal
      suspended on multiple readers.</summary>`. Map the two trailing
      `//`-line comments verbatim as trailing `//`-line comments on the
      converted `Kappa` and `Armed` property declarations (NOT as XML
      doc-comments -- they are implementation notes, not API
      documentation, and the source explicitly chose `//` over `///`).
      Trivial mechanical mapping per the project doc-comment idiom (same
      treatment as abandon.dart.md / machine_state.dart.md). Trivial.
    idiom_id: null
    research_finding_id: null
    nuance: trivial
    trivial: true
  - construct_key: "dart.class.mutable-state-container-identity-equality-three-fields-named-only-ctor-two-required-one-defaulted-bool"
    source_form: >-
      "`class Hanger { final GoalId goalId; final Pc kappa; bool armed;
      Hanger({required this.goalId, required this.kappa, this.armed =
      true}); }` -- a Dart class with: (a) two `final` non-nullable
      identifier-typed fields `goalId` (type `GoalId` = `int`) and `kappa`
      (type `Pc` = `int`); (b) one mutable non-nullable `bool` field
      `armed`; (c) a single named-only constructor with two `required`
      named params (`goalId`, `kappa`) and one defaulted named param
      (`armed = true`). No `==`/`hashCode` override -- default identity
      equality. No `toString` override. No mutator methods -- mutation
      happens via direct field assignment from the runtime (the doc
      comment establishes the lifecycle: 'armed=true at creation; first
      wake sets to false', which is the single mutation observable on
      `armed`)."
    target_decision: >-
      Map to a reference-type .NET `class` `Hanger` (NOT `record`, NOT
      `struct`, NOT `record class`, NOT `record struct`). The class has:
      a `public GoalId GoalId { get; }` (init-only via constructor,
      mirroring Dart `final`); a `public Pc Kappa { get; }` (init-only);
      a `public bool Armed { get; set; } = true;` (mutable, default
      `true`). Single non-optional-params constructor `public
      Hanger(GoalId goalId, Pc kappa, bool armed = true) { GoalId =
      goalId; Kappa = kappa; Armed = armed; }`. The PascalCase
      identifier rename is per the .NET capitalisation conventions
      (`goalId` -> `GoalId`, `kappa` -> `Kappa`, `armed` -> `Armed`);
      private fields would be `_camelCase` but all three fields here are
      public surface. NOT a `record` / `record class`: would inject
      value-equality on every field -- a correctness bug because the
      runtime stores `Hanger` references in per-goal/per-reader
      suspension structures and asks 'is this *the* hanger I attached?'
      which is reference identity (two hangers with coincidentally
      identical `goalId`/`kappa`/`armed` are NOT the same hanger). NOT a
      `struct` / `record struct`: would copy on assignment -- the
      `armed` field is mutated in place by the wake path ('first wake
      sets to false'); a struct copy would mutate the copy, not the
      canonical scheduler-held state, breaking the single-reactivation
      invariant the class exists to enforce. The identity-equality +
      mutable-field combination is the same load-bearing reason
      `machine_state.dart`'s `GoalState` is a reference-type `class` --
      reused verbatim here.
    idiom_id: null
    research_finding_id: rf-dart-mutable-state-class-identity-equality-to-csharp-class
    nuance: >-
      VALUE-VS-REFERENCE / IDENTITY -- the LOAD-BEARING nuance for this
      construct. Dart `class Hanger` uses default identity equality (no
      `==` override). The .NET counterpart MUST be `class` (reference
      type), NEVER `record`/`record class`/`record struct`/`struct`.
      Reasoning: (i) the doc-comment establishes that `Hanger` exists to
      ensure 'single reactivation when a goal suspended on multiple
      readers' -- the runtime attaches multiple `Hanger` references (one
      per reader) to the same suspended goal and races them on wake; the
      first wake flips `armed` from `true` to `false` and that
      transition is observable from EVERY reference to the same hanger.
      A `struct` would copy on assignment so the wake side would mutate
      a private copy and the other-reader references would still see
      `armed == true`, double-reactivating the goal -- a correctness bug
      in the single-reactivation invariant. (ii) Two distinct hangers
      with coincidentally identical `goalId`/`kappa`/`armed=true` at
      construction-time are NOT the same hanger (they belong to
      different `(reader, goal)` attachments); value-equality (record /
      record class) would silently make them compare equal -- a
      correctness bug in the runtime's hanger-lookup tables. (iii)
      Aggregation: there is no inner mutable sub-component (all three
      fields are value types -- `int`, `int`, `bool`); the
      outer-being-reference is the load-bearing identity decision.
      Init-only-vs-set nuance: Dart `final` fields become C# `{ get; }`
      (init-only via constructor, conservative form -- consistent with
      machine_state.dart.md / GoalState); Dart mutable field becomes
      `{ get; set; }` with a default value initialiser preserving the
      Dart `= true` default. Null-safety: every field is non-nullable
      (no `?` annotations on the Dart side); under enabled NRT every
      corresponding C# property is non-nullable; the `bool` parameter
      defaults to `true` (the Dart `this.armed = true` default).
      Default-true initialiser nuance: the `= true` in the Dart ctor is
      preserved both as a property initialiser (`{ get; set; } = true;`
      -- the .NET-canonical initial-state-set) AND as the constructor
      parameter default (`bool armed = true`); the parameter default
      governs the call site, the property initialiser governs the
      default state if a future codegen path supports object-initialiser
      construction. Async / Stream / Future / Completer / isolate /
      mixin / sealed / generic: ABSENT -- correctly not asserted (FR-009:
      address nuances that exist, do not invent). No `==`/`hashCode`
      override means no `Deconstruct`/`with`/value-equality machinery is
      needed on the .NET side -- the class deliberately does not provide
      it.
  - construct_key: "dart.named-required-ctor-with-default-bool"
    source_form: >-
      "`Hanger({required this.goalId, required this.kappa, this.armed =
      true});` -- a Dart named-only constructor with (a) two `required`
      named field-binding params (`goalId`, `kappa`) and (b) one
      defaulted named field-binding param `armed = true` (defaulted to a
      `bool` literal). No initialiser list, no body, no `assert`."
    target_decision: >-
      A single C# constructor with positional parameters (Dart named
      arguments map to C# positional or named -- choose positional with
      an explicit default for `armed`, since C# has no `named-required`
      keyword and 'required' on a Dart named arg is semantically just
      'non-optional'). Signature: `public Hanger(GoalId goalId, Pc
      kappa, bool armed = true)`. Body: explicit assignment to each
      backing property (`GoalId = goalId; Kappa = kappa; Armed = armed;`).
      The default `armed = true` is preserved verbatim. NOT a `required`
      modifier on the properties (the C# 11 `required` keyword forces
      callers to set the property at construction site -- semantically
      aligned with Dart `required` named args, but using it would
      require callers to use object-initialiser syntax which the rest of
      the runtime port does not; consistency with machine_state.dart.md
      / GoalState ctor favours the constructor form). Callers may use
      the C# named-argument call syntax at the call site (`new
      Hanger(goalId: g, kappa: k)`) to mirror the Dart named-call
      readability. Reused verbatim from the
      rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
      idiom established in machine_state.dart.md for GoalState.
    idiom_id: null
    research_finding_id: rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults
    nuance: >-
      Named-vs-positional argument nuance: Dart 'named-only constructor'
      is a call-site readability/refactor-safety feature; the closest C#
      counterpart is positional parameters with named-argument syntax at
      the call site (`new Hanger(goalId: g, kappa: k)`). Defaulted-args
      nuance: Dart `= true` (a `bool` literal) maps to C# `= true` -- the
      literal compiles to a compile-time-constant default value, which C#
      permits for `bool` parameters (https://learn.microsoft.com/
      dotnet/csharp/programming-guide/classes-and-structs/named-and-
      optional-arguments). Null-safety nuance: NOT APPLICABLE here --
      every parameter is non-nullable on both sides; no `??` collapse is
      needed. The named-required pattern recurs across the runtime port
      (machine_state.dart GoalState ctor, abandon.dart AbandonOps.
      abandonWriter, this file's Hanger ctor) and is resolved by the
      same idiom each time per SC-007 (>=95% of recurring constructs
      resolved via a recorded idiom, not re-derived).
conversion_units:
  - "`using` directive in lib/runtime/hanger.cs pointing at the namespace of the converted lib/runtime/machine_state.cs (depgraph/namespace step owns the exact namespace name); transparently brings `global using GoalId = int;` and `global using Pc = int;` aliases into scope."
  - "`public class Hanger` in the namespace mirroring lib/runtime/ -- reference type, identity equality, NO `record`/`record class`/`struct`/`record struct` (load-bearing per the rf-dart-mutable-state-class-identity-equality-to-csharp-class idiom)."
  - "  - get-only properties: `public GoalId GoalId { get; }`, `public Pc Kappa { get; }` (init via ctor; Dart `final` -> conservative `{ get; }` form)"
  - "  - get/set property with default: `public bool Armed { get; set; } = true;` (Dart mutable `bool armed` with `= true` ctor default; first wake sets to false per the doc-comment lifecycle)"
  - "  - constructor: `public Hanger(GoalId goalId, Pc kappa, bool armed = true) { GoalId = goalId; Kappa = kappa; Armed = armed; }` -- positional params with named-argument call-site convention; `armed = true` default preserved verbatim"
  - "  - XML doc-comment on the class: `/// <summary>Hanger: ensures single reactivation when a goal suspended on multiple readers.</summary>` (Dart `///` -> C# XML doc); trailing `//` comments on `Kappa` and `Armed` preserved verbatim as trailing `//` comments (not XML doc, mirroring the Dart-side choice of `//` over `///` for those two)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-namespace-using -- relative-import mapping (carry-over)

The Dart spec defines `import 'machine_state.dart';` as a directive that
makes the public top-level identifiers of the imported library available
in the importing library. The official Dart language tour
(https://dart.dev/language/libraries) documents that an import names a
*library* (one Dart file `≡` one library by default) and that the
imported library's public surface becomes available unqualified.

The official C# language reference (Microsoft Learn:
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
documents that the `using` directive names a *namespace*, and that
namespaces are decoupled from filenames (a single namespace can be split
across many files; a single file can declare many namespaces). This
asymmetry is authoritative: there is no C# directive that imports a
single file, and no mechanism for narrowing the imported surface to a
specific symbol.

Consequence: the faithful conversion of a Dart relative-import is a
`using <namespace>;` line in the converted target file, where the
namespace name is the namespace of the converted sibling -- decided by
the downstream depgraph/namespace stage, not by this convspec. The
precedent at
`.codeconv/conversion-specs/lib/runtime/fairness.dart.md` (the same
sibling import, same namespace target) is reused here verbatim. The two
consumed aliases `GoalId` and `Pc` are reached as plain `int` once the
`global using` aliases established in
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` are in
scope -- no qualified-name path is needed because the aliases are
transparent. Idiom reused verbatim;
`rf-dart-relative-import-to-csharp-namespace-using` is the recurring
project idiom for relative-import mapping (SC-007).

### rf-dart-mutable-state-class-identity-equality-to-csharp-class -- Hanger (carry-over from GoalState)

Deep analysis. `Hanger` is a mutable state container with reference
identity. It carries two `final` identifier fields (`goalId`, `kappa`)
and one mutable `bool` field (`armed`). The doc-comment establishes the
lifecycle: 'armed=true at creation; first wake sets to false'. The
runtime attaches `Hanger` references to suspended-goal/reader
structures and uses the `armed` transition to enforce the
single-reactivation invariant: when a goal suspends on multiple
readers, one `Hanger` per reader is attached, all sharing the goal;
when ANY of those readers binds, the wake path flips the goal's hanger
from `armed=true` to `armed=false` and reactivates the goal exactly
once -- subsequent reader-binds find `armed==false` and silently no-op.
The invariant REQUIRES that all references to the same `Hanger` see
the same `armed` value: a reference-type class with mutable property
is the only correct shape.

Authoritative .NET. The C# guide on classes vs records
(https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records
and https://learn.microsoft.com/dotnet/csharp/fundamentals/types/classes)
is explicit: use `class` for reference identity + mutable state; use
`record class` for value equality on an immutable bundle; use
`struct`/`record struct` for value-type semantics + no-allocation small
bundles. `Hanger` is mutable AND identity-equal AND held by reference
-- `class` is the only correct choice. Microsoft Learn's reference-type
documentation
(https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/reference-types)
confirms: 'Variables of reference types store references to their data
(objects), while variables of value types directly contain their
data.' The `Hanger` 'first wake sets to false' requirement is exactly
the case 'stored references to data observed from multiple variables'
-- the reference-type guarantee.

Why not `record class`. Records inject `Equals`/`GetHashCode` that
compare every field; two `Hanger` instances with coincidentally
identical `goalId`/`kappa`/`armed` (e.g. just after both are
constructed for different `(reader, goal)` attachments) would compare
equal -- a correctness bug in the hanger-lookup tables. Microsoft
Learn's record docs
(https://learn.microsoft.com/dotnet/csharp/fundamentals/types/records)
explicitly call out 'value equality means that two variables of a
record type are equal if the types match and all property and field
values match.' That is the wrong contract here.

Why not `struct` / `record struct`. Structs are copied on assignment;
the wake path's `hanger.Armed = false` on a struct copy would mutate
the copy, not the canonical scheduler-owned state. Microsoft Learn's
struct docs
(https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/struct)
state: 'When you assign one struct variable to another, the contents
are copied.' This is exactly the silent-state-divergence failure mode
the single-reactivation invariant cannot tolerate.

Init-only-vs-set-properties. Dart `final` -> C# `{ get; }` (init-only
via ctor, conservative form -- same choice as `GoalState` in
machine_state.dart.md); Dart mutable -> `{ get; set; }` with a default
initialiser. The C# 9 init-only `{ get; init; }` is acceptable but
unnecessary here -- get-only matches the Dart `final` invariant
(written exactly once, at construction) and avoids exposing a
`with`-expression mutation surface the Dart side does not have.

Authoritative Dart. The Dart language tour
(https://dart.dev/language/classes#instance-variables) documents
`final` instance variables as 'must be initialized exactly once' --
the structural match for C# get-only properties initialised in the
constructor body. The default-value form `this.armed = true` is
documented under
https://dart.dev/language/constructors#default-values-for-named-parameters
as a compile-time-constant default expression -- preserved verbatim as
a C# parameter default and a C# property initialiser.

Decision. `public class Hanger { public GoalId GoalId { get; } public
Pc Kappa { get; } public bool Armed { get; set; } = true; public
Hanger(GoalId goalId, Pc kappa, bool armed = true) { GoalId = goalId;
Kappa = kappa; Armed = armed; } }`. Authoritative both sides
(api.dart.dev / dart.dev for Dart, Microsoft Learn for .NET); no
escalation. Same idiom reused as for `GoalState` and `GoalQueue` in
machine_state.dart.md -- the project's canonical mutable-state-class
idiom (SC-007).

### rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults -- Hanger ctor (carry-over from GoalState)

Deep analysis. The Dart ctor `Hanger({required this.goalId, required
this.kappa, this.armed = true})` combines two `required` named
field-binding params and one defaulted named field-binding param. The
`this.X` syntax is Dart's field-formal-parameter shorthand
(https://dart.dev/language/constructors#using-initializing-formal-parameters)
-- the parameter's value is assigned to the same-named field at
construction time. Semantically, 'required' on a Dart named arg means
'non-optional': the call site MUST supply a value. The `= true`
default makes `armed` optional with a compile-time-constant default.

Authoritative .NET. The C# 'Named and optional arguments' doc
(https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/named-and-optional-arguments)
documents both features and confirms named-argument call-site syntax
is fully supported on positional ctors. A positional parameter
without a default IS required by the C# compiler (the closest
counterpart of Dart's `required` on a named arg). Parameters with a
default value are optional and may be omitted at the call site.

C# 11's `required` modifier on properties
(https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-11.0/required-members)
is REJECTED here for the same reason as `GoalState` in
machine_state.dart.md: it requires the caller to use object-
initialiser syntax, which the surrounding runtime port does not; the
constructor form is consistent across the runtime port (SC-007).

Decision. Positional ctor with named-argument call-site convention;
`armed = true` default preserved verbatim (both as a parameter default
on the constructor signature and as a property initialiser on the
`Armed` auto-property -- redundant but harmless and matches the
machine_state.dart.md / GoalState convention). Authoritative; no
escalation. Same idiom reused as for `GoalState` ctor in
machine_state.dart.md -- the project's canonical named-required-with-
defaults ctor idiom (SC-007).

## Notes -- well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Mutable state container vs record/struct/record-struct**: LOAD-
  BEARING. One reference class (`Hanger`) -- the decision rule is
  identical to `GoalState` in machine_state.dart.md (identity-equal +
  mutable + held-by-reference -> `class`; NEVER `record` / `record
  class` / `struct` / `record struct`). The single-reactivation
  invariant the class exists to enforce REQUIRES that all references
  to the same `Hanger` see the same `armed` value -- a guarantee only
  a reference type provides.
- **Reference identity** for `Hanger` is the only equality semantics
  the source has (no `==` override); preserved verbatim. The class is
  used as a key/value in scheduler-internal lookup tables and as a
  mutation target on the wake path -- both rely on reference identity.
- **Aggregation of mutable sub-components**: ABSENT here -- all three
  fields are value types (`int`, `int`, `bool`). The outer-being-
  reference is the load-bearing identity decision; there is no inner
  mutable sub-component that would otherwise need a reference-type
  outer to share. Correctly not asserted.
- **Trail / choice-points / WAM-style backtracking**: ABSENT from this
  file. `Hanger` is a per-(reader, goal) suspension marker, NOT a
  trail/choice-point entry. The .NET port MUST NOT introduce
  trail/choice-point fields here -- doing so would over-translate.
  Correctly not asserted.
- **Null-safety**: every field/parameter is non-nullable on both
  sides (no `?` annotations in the Dart source); under enabled NRT
  every corresponding C# property and parameter is non-nullable. The
  `bool armed = true` default is a value-type default with no null
  involved.
- **Concurrency model**: this file is single-threaded as declared
  (no isolates, no locks, no async, no Future/Stream/Completer in the
  source). The `Hanger.Armed` mutation MAY be observed from multiple
  reader-bind sites racing on the wake path -- the single-owning-
  context invariant of the surrounding runtime is what makes the
  plain `bool` field safe; if a future cross-isolate hanger ever
  arises that is an explicit re-scope and should be ESCALATED at that
  point, not silently upgraded to `Interlocked` / `volatile` here.
- **Async / Stream / IAsyncEnumerable / Future / Completer / Task /
  TaskCompletionSource**: ABSENT. The class is a plain data holder
  with no methods at all (only the constructor); no async surface.
  The .NET port MUST NOT introduce `Task` / `async` / `Channel<T>` /
  `IAsyncEnumerable` here.
- **Mixin / sealed / abstract / generic**: ABSENT. The Dart class is
  plain (no `mixin`, no `sealed`, no `abstract`, no type parameters);
  the C# class is plain (no `sealed`, no `abstract`, no type
  parameters); correctly not asserted.
- **Identifier casing**: Dart `camelCase` field names (`goalId`,
  `kappa`, `armed`) become `PascalCase` public properties per the .NET
  capitalisation guideline
  (https://learn.microsoft.com/dotnet/standard/design-guidelines/capitalization-conventions);
  parameter names stay `camelCase` (`goalId`, `kappa`, `armed`) per
  the same convention. The class name `Hanger` is already PascalCase
  on the Dart side and is preserved verbatim.
- **Top-level binding rehoming**: NOT APPLICABLE here -- this file
  contains no top-level functions, constants, or typedefs; just the
  single class `Hanger`. Classes are namespace-scoped types in both
  languages.
- **Equality contract**: `Hanger` has NO `==`/`hashCode` override
  -- default identity equality. The .NET port preserves this exactly
  by being a `class` (default reference equality), with no
  `Equals`/`GetHashCode` overrides. No `Deconstruct`/`with`
  machinery is introduced.
- **Zero escalations**: every non-trivial construct in this file is
  resolvable from authoritative Dart and .NET official documentation
  and from established project idioms reused verbatim from
  machine_state.dart.md and fairness.dart.md (FR-013).
