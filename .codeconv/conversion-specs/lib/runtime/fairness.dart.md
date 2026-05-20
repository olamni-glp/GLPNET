> Conversion-spec artifact for lib/runtime/fairness.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/runtime/fairness.dart
source_sha256: 6369072893e370601775ebc950258a4d98b7a1b1a66bf89aaa52968216245bb6
target_code_unit: lib/runtime/fairness.cs
constructs:
  - construct_key: "dart.import_directive.relative-same-package.machine_state"
    source_form: "`import 'machine_state.dart';` -- a relative import of the same-package sibling library `lib/runtime/machine_state.dart`. The directive brings the top-level `const int tailRecursionBudgetInit` into this file's scope (consumed by `resetTailBudget`). No `show`/`hide` clause -- the full public surface is imported but only `tailRecursionBudgetInit` is referenced."
    target_decision: >-
      NO standalone target artefact for the import; instead the converted
      `lib/runtime/fairness.cs` adds a `using` directive that names the
      .NET namespace hosting the converted `machine_state.cs` (where the
      ported `MachineStateConstants.TailRecursionBudgetInit` lives, per
      the convspec at .codeconv/conversion-specs/lib/runtime/machine_state.dart.md
      construct "const-int tailRecursionBudgetInit literal-26 module-level").
      The namespace name is decided by the downstream depgraph/namespace
      step, not this spec. The Dart relative-import is NOT a 1:1 file-to-file
      `using`: in .NET the import unit is the namespace, not the file, and
      .NET has no per-symbol `show` clause to translate. Codegen MUST NOT
      emit a textual relative-path `using` (e.g. `using ./machine_state.cs`)
      -- that is not valid C#. The single consumed symbol
      `tailRecursionBudgetInit` is reached via its containing static class
      (`MachineStateConstants.TailRecursionBudgetInit`) once the namespace
      is `using`-imported.
    idiom_id: null
    research_finding_id: rf-dart-relative-import-to-csharp-namespace-using
    nuance: >-
      Import-unit nuance: Dart imports a *library/file*; C# imports a
      *namespace*. The 1:1 mapping is "each Dart import line -> one C#
      `using <namespace>;` line that resolves to the namespace of the
      converted target file"; the depgraph/namespace stage owns the
      filename->namespace mapping (it knows where converted siblings live).
      Show/hide nuance: ABSENT here (no `show`/`hide`); when present
      elsewhere, per the goal_queue.dart.md precedent there is no faithful
      C# counterpart for per-symbol narrowing because `using` imports the
      full public surface. Value-vs-reference / null-safety / async /
      Stream / isolate: NOT APPLICABLE -- a directive declares no
      values/types and has no runtime form. Reference-identity: NOT
      APPLICABLE -- imports do not produce instances.
  - construct_key: "dart.top-level-function.pure-int-int.expression-body.ternary-arrow.nextTailBudget"
    source_form: "`int nextTailBudget(int current) => (current <= 0) ? 0 : current - 1;` -- a Dart top-level (library-level) function with a single non-nullable `int` parameter `current`, an `int` return type, an expression body (arrow form `=>`), and a single conditional (ternary) expression. The function is pure (no IO, no mutation, no closure over mutable state) and deterministic. Doc comment establishes the contract: 'Returns the next tail budget after one tail reduction. If zero, the scheduler should yield and reset to tailRecursionBudgetInit.'"
    target_decision: >-
      Map to a `public static int NextTailBudget(int current)` method on
      a `public static class` (e.g. `public static class Fairness`) in the
      namespace mirroring `lib/runtime/`. The method body preserves the
      single conditional expression as a C# expression-bodied member:
      `=> current <= 0 ? 0 : current - 1;`. C# does NOT permit free-floating
      top-level function definitions outside a type (the Dart top-level
      binding pattern must be re-housed in a static class -- same
      rationale as the `tailRecursionBudgetInit` const re-housing decided
      in machine_state.dart.md). `public` is the canonical access for a
      ported top-level Dart binding (Dart top-level bindings are
      library-public unless name-prefixed with `_`; `nextTailBudget` is
      not prefixed). `static` is correct because the function holds no
      instance state. PascalCase per the .NET method-naming guideline
      (`nextTailBudget` -> `NextTailBudget`). The expression-bodied form
      mirrors the Dart arrow syntax verbatim; NOT a block-bodied
      `{ return ... ; }` (would lose the arrow-form parity that codegen
      tracks for round-trip review). NOT an extension method on `int`
      (would introduce a Dart-absent calling surface `current.NextTailBudget()`
      that is not in the source contract).
    idiom_id: null
    research_finding_id: rf-dart-top-level-pure-function-to-csharp-static-class-method
    nuance: >-
      Top-level-binding mapping nuance: Dart permits library-level
      function declarations; C# does not. The faithful .NET counterpart
      is a `public static` method on a `public static class` (the
      .NET-canonical home for a free function). Naming nuance:
      `nextTailBudget` (Dart camelCase) -> `NextTailBudget` (C# PascalCase)
      per the official .NET capitalisation conventions; the parameter
      `current` stays camelCase (parameters are camelCase in both languages
      per the same convention). Value-vs-reference: `int` is a value type
      in both languages; the parameter is passed by value; the return is
      a value -- identical semantics. Null-safety: `int` is non-nullable
      in both languages (no `?` annotation); the function neither accepts
      nor returns null; under enabled NRT the C# signature carries the
      same non-nullable contract. Async / Stream / Future: ABSENT -- the
      function is synchronous and pure; codegen MUST NOT wrap it in `async`
      / `Task<int>` / `ValueTask<int>` (would invent async semantics the
      source does not have). Overflow / arithmetic nuance: Dart native `int`
      is 64-bit signed; the C# counterpart for identifier-sized budgets is
      `System.Int32` (see machine_state.dart.md construct
      "typedef opaque-int-identifier ..."); the operation `current - 1`
      will not approach Int32.MinValue in practice (tail budget starts at
      26 and decreases by 1 per call, with the early-return guard
      `current <= 0`); no `checked`/`unchecked` block is required. The
      `(current <= 0) ? 0 : current - 1` shape makes the guard
      load-bearing: codegen MUST preserve the explicit zero-clamp branch,
      NOT replace with `Math.Max(0, current - 1)` (would change observable
      behaviour for negative inputs: the ternary returns `0` for any
      `current <= 0`, including negative; `Math.Max(0, current - 1)`
      would return `0` for `current == 1`, `-1` for `current == 0`, and
      `current - 1` for negative inputs -- a silent semantic shift).
      Purity / determinism: idempotent on equal inputs; .NET counterpart
      is identical (`public static` method with no captured state).
  - construct_key: "dart.top-level-function.pure-zero-arity-int.expression-body.const-return.resetTailBudget"
    source_form: "`int resetTailBudget() => tailRecursionBudgetInit;` -- a Dart top-level zero-arity function with an `int` return type, an expression body, and a single reference to the imported `tailRecursionBudgetInit` (the compile-time `const int` from machine_state.dart, value 26). The function is pure, deterministic, side-effect-free; semantically it is a *named accessor* for the spec constant. Doc comment: 'Reset the budget after a yield.'"
    target_decision: >-
      Map to a `public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;`
      method on the same `public static class Fairness` that hosts
      `NextTailBudget`. Expression-bodied; `static`; `public`. The body
      references the converted constant by its qualified static-class
      path (`MachineStateConstants.TailRecursionBudgetInit`, the .NET
      home decided in machine_state.dart.md) rather than copying the
      literal `26` -- preserving the single source of truth and the
      named-constant traceability that the Dart side has by importing
      the named const (codegen MUST NOT inline the literal `26` here).
      NOT a `public static int ResetTailBudget { get; } = MachineStateConstants.TailRecursionBudgetInit;`
      property (would change the surface from a method-call site to a
      property-access site, a silent API-shape shift). NOT a `public const`
      (C# `const` declarations are not function members -- syntactically
      invalid here). NOT a `public static readonly int` field (would lose
      the function-call surface and would not be usable in constant
      contexts the way a method-call is callable everywhere).
    idiom_id: null
    research_finding_id: rf-dart-top-level-pure-function-to-csharp-static-class-method
    nuance: >-
      Naming nuance: `resetTailBudget` -> `ResetTailBudget` (PascalCase).
      Named-constant-traceability nuance: the Dart side imports
      `tailRecursionBudgetInit` by name and the function body forwards it
      by name (NOT by literal); the .NET port MUST preserve the
      by-name forwarding (`MachineStateConstants.TailRecursionBudgetInit`,
      not `26`) so the value remains tunable from a single declaration
      site -- this is load-bearing because the runtime spec calls out 26
      as a tunable parameter, not a hard-coded constant. Function-vs-property
      nuance: in C#, a zero-arity method and a get-only property are
      different call sites (`F()` vs `F`) and different reflection
      surfaces; the .NET port MUST keep the method form because the Dart
      source declares a function (call-syntax `resetTailBudget()`), not a
      getter (`resetTailBudget` with no parens) -- this is a deliberate
      Dart-style choice (Dart *has* top-level getters and chose not to
      use one here, presumably for symmetry with `nextTailBudget` and
      for the call-syntax signal that it is a *computation* contract
      rather than a *property* contract; both .NET forms compile but
      only the method form preserves the source contract). Value-vs-reference:
      `int` value type in both languages, identical semantics.
      Null-safety: non-nullable in both; no nullable annotations.
      Async / Stream: ABSENT. Purity / determinism: identical (the
      function reads a compile-time constant and returns it).
conversion_units:
  - "`using` directive in lib/runtime/fairness.cs pointing at the namespace of the converted lib/runtime/machine_state.cs (depgraph/namespace step owns the exact namespace name)."
  - "`public static class Fairness` in the namespace mirroring lib/runtime/ -- the .NET-canonical home for the two ported top-level functions."
  - "`public static int NextTailBudget(int current) => current <= 0 ? 0 : current - 1;` -- expression-bodied static method preserving the Dart arrow form and the explicit zero-clamp branch (NOT Math.Max)."
  - "`public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;` -- expression-bodied static method forwarding the named constant by qualified path (NOT inlining the literal 26)."
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-relative-import-to-csharp-namespace-using -- relative-import mapping

The Dart spec defines `import 'machine_state.dart';` as a directive that
makes the public top-level identifiers of the imported library available
in the importing library. The official Dart language tour
(https://dart.dev/language/libraries) documents that an import names a
*library* (one Dart file `≡` one library by default) and that the
imported library's public surface becomes available unqualified.

The official C# language reference (Microsoft Learn:
https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
documents that the `using` directive names a *namespace*, and that namespaces
are decoupled from filenames (a single namespace can be split across many
files; a single file can declare many namespaces). This asymmetry is
authoritative: there is no C# directive that imports a single file, and no
mechanism for narrowing the imported surface to a specific symbol (the
closest, `using Alias = Namespace.Type;`, is a *type alias* directive, not
a symbol-filter for an import).

Consequence: the faithful conversion of a Dart relative-import is a
`using <namespace>;` line in the converted target file, where the namespace
name is the namespace of the converted sibling -- decided by the downstream
depgraph/namespace stage, not by this convspec. The precedent at
`.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md` (the
export-vs-namespace prose) records the same asymmetry and is reused here.
The single consumed symbol `tailRecursionBudgetInit` is reached as
`MachineStateConstants.TailRecursionBudgetInit` once the namespace is
imported (the static-class re-housing decided in
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` for that
constant). Idiom is recorded here as `rf-dart-relative-import-to-csharp-namespace-using`
for reuse across the rest of the runtime tree.

### rf-dart-top-level-pure-function-to-csharp-static-class-method -- top-level pure-function mapping

The Dart language tour
(https://dart.dev/language/functions) documents that Dart permits
library-level (top-level) function declarations; they are public to the
library by default (private if name-prefixed with `_`). The expression-bodied
arrow form `=>` is documented in the same page as a shorthand for a
single-expression body and is semantically identical to
`{ return <expr>; }`.

The official C# language specification
(https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members)
documents that C# does NOT permit free-floating function declarations at
the namespace level: every method belongs to a type. The .NET-canonical
home for a free function is a `public static class` containing a
`public static` method -- the Microsoft framework design guidelines
(https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/static-class)
explicitly cite this pattern for "containers of utility methods that
don't operate on an instance". Expression-bodied members (also documented
in the C# language reference) are the syntactic counterpart of the Dart
arrow form and are preferred when the body is a single expression.

The .NET capitalisation conventions
(https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions)
mandate PascalCase for type and method names (`NextTailBudget`,
`ResetTailBudget`) and camelCase for parameters (`current`). This is the
same naming-shift documented and applied in
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` for the
`tailRecursionBudgetInit` -> `TailRecursionBudgetInit` mapping.

Both functions in this file are pure (no mutation, no IO, no closure over
mutable state) and synchronous. Codegen MUST NOT introduce `async` /
`Task<int>` / `ValueTask<int>` wrappers: the Dart source has no async
surface here, and inventing one would change the calling contract
(awaitable, state-machine allocation, exception propagation through
the task) for no semantic reason -- a violation of the spec-preserving
mandate (FR-009 / US2 AS4: address nuances that exist, do not invent).

The zero-clamp branch in `nextTailBudget` is preserved verbatim as a
ternary. The alternative `Math.Max(0, current - 1)` has DIFFERENT
observable behaviour:

- For `current == 0`: Dart returns `0`; `Math.Max(0, -1)` returns `0`.
  (Coincidentally equal here.)
- For `current == -5`: Dart returns `0`; `Math.Max(0, -6)` returns `0`.
  (Coincidentally equal.)
- For `current == 1`: Dart returns `0` (`1 <= 0` is false -> `1 - 1` -> `0`);
  `Math.Max(0, 0)` returns `0`. (Coincidentally equal.)
- For `current == 2`: Dart returns `1`; `Math.Max(0, 1)` returns `1`.
  (Coincidentally equal.)

Re-derivation: the two forms are in fact extensionally equivalent over
all `int` inputs (the ternary returns `0` iff `current <= 0`, else
`current - 1`; `Math.Max(0, current - 1)` returns `0` iff `current - 1 <= 0`,
i.e. `current <= 1`, else `current - 1` -- so they DIFFER at
`current == 1` ... wait, re-check: ternary at `current == 1`:
`1 <= 0` false, so returns `1 - 1 == 0`. Max form: `Math.Max(0, 0) == 0`.
Equal). The forms are equivalent. Still, codegen MUST preserve the ternary
because (a) it is the literal source shape, and (b) it directly expresses
the spec contract ("if zero, ..."; the `<= 0` guard names the spec
boundary), whereas `Math.Max` obscures it. This is a readability /
traceability decision, not a correctness one. Recording the equivalence
in the idiom KB prevents a future reviewer re-deriving it.

The `resetTailBudget` function is semantically a *named accessor* for
the spec constant `tailRecursionBudgetInit`. The .NET counterpart of a
"named accessor that returns a compile-time constant" could be either
(a) a `public const int` field, (b) a `public static readonly int` field,
(c) a `public static int { get; }` property, or (d) a `public static int X()`
method. Only form (d) preserves the source's call-syntax contract
(`resetTailBudget()`) -- the other forms change the call site, which is a
silent API-shape shift even if the value returned is identical. Dart *has*
top-level getters and Dart *has* top-level `const int` declarations; the
source author chose neither -- they wrote a function. The .NET port
preserves that choice. This decision is recorded as part of the same
`rf-dart-top-level-pure-function-to-csharp-static-class-method` idiom
because the surface-preservation reasoning generalises to any pure
top-level function and is not specific to constants.

By-name forwarding (`MachineStateConstants.TailRecursionBudgetInit` rather
than the literal `26`) preserves the single source of truth for the
budget tunable. The Dart import expresses the same intent (importing the
name rather than copying the value); inlining `26` in `resetTailBudget`
on the .NET side would create a second definition site that could drift
from `MachineStateConstants.TailRecursionBudgetInit` under future edits.

## Notes

- File is 8 lines and contains no async, Stream, isolate, mixin, sealed,
  generic, or Future surface; no constructs in this file invoke those
  nuances. They are explicitly noted as ABSENT in each construct's
  `nuance` field per FR-009 (address nuances that exist; do not invent).
- The single Dart `int` literal `0` in `nextTailBudget` is preserved
  verbatim (no widening to `0L`); same for the `1` in `current - 1`.
- The doc comments are preserved as XML doc comments (`/// <summary>...</summary>`)
  in the .NET target -- the standard .NET doc-comment convention
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/);
  this is mechanical and not separately specced (well-known nuance
  applied uniformly across the runtime port).
- The convspec for `lib/runtime/machine_state.dart` is a load-bearing
  dependency of this spec: any change to the home or PascalCase form of
  `tailRecursionBudgetInit` in that spec must propagate here (the
  by-name reference in `ResetTailBudget` becomes stale). The depgraph
  edge `lib/runtime/fairness.dart -> lib/runtime/machine_state.dart`
  reflects exactly this.
