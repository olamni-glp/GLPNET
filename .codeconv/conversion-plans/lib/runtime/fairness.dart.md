---
path: lib/runtime/fairness.dart
cycle_group_id: 33
scc_siblings: []
generated_at: 2026-05-21T14:51:47Z
source_sha256: 6369072893e370601775ebc950258a4d98b7a1b1a66bf89aaa52968216245bb6
schema_version: 1
---

# Conversion Plan: lib/runtime/fairness.dart

## 1. Source Analysis

The Dart source `glp_runtime_net/lib/runtime/fairness.dart` is an 8-line library
file (3 lines of effective code + doc comments + 1 import). Inspected verbatim:

- Line 1: `import 'machine_state.dart';` — a relative same-package import of the
  sibling library `lib/runtime/machine_state.dart`. No `show` / `hide` clause;
  the full public surface is imported, but only `tailRecursionBudgetInit` is
  referenced (consumed by `resetTailBudget` on line 8).
- Line 3: `/// Returns the next tail budget after one tail reduction.` —
  documentation comment for `nextTailBudget`.
- Line 4: `/// If zero, the scheduler should yield and reset to tailRecursionBudgetInit.`
  — second documentation line.
- Line 5: `int nextTailBudget(int current) => (current <= 0) ? 0 : current - 1;`
  — Dart top-level (library-level) function. Single non-nullable `int`
  parameter `current`. `int` return type. Expression body (arrow `=>`). Single
  conditional (ternary) expression. Pure (no IO, no mutation, no captured
  state). Deterministic.
- Line 7: `/// Reset the budget after a yield.` — doc comment for
  `resetTailBudget`.
- Line 8: `int resetTailBudget() => tailRecursionBudgetInit;` — Dart top-level
  zero-arity function. `int` return type. Expression body. References the
  imported `tailRecursionBudgetInit` (the compile-time `const int` from
  `machine_state.dart`, value `26` — single source of truth, by-name
  forwarding, NOT a copied literal). Pure, deterministic, side-effect-free;
  semantically a *named accessor* for the spec constant.

No async, no Stream, no Future, no isolate, no mixin, no sealed type, no
generic, no class, no field, no closure over mutable state. No nullability
annotations (everything is non-nullable `int`). Three constructs total
(itemised in §2 below).

## 2. Dart → C#/.NET Conversion Plan

Three constructs, mirroring the convspec one-for-one.

### Construct 1 — dart.import_directive.relative-same-package.machine_state

**Source form:** `import 'machine_state.dart';` — relative same-package import of
`lib/runtime/machine_state.dart`. No `show` / `hide`. Brings the top-level
`const int tailRecursionBudgetInit` into scope (consumed by `resetTailBudget`).

**Target decision (→ C#/.NET):** NO standalone target artefact for the import;
the converted `lib/runtime/fairness.cs` adds a `using` directive that names
the .NET namespace hosting the converted `machine_state.cs` (where the ported
`MachineStateConstants.TailRecursionBudgetInit` lives, per the convspec at
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` construct
"const-int tailRecursionBudgetInit literal-26 module-level"). The namespace
name is decided by the downstream depgraph/namespace step, not this plan. The
Dart relative-import is NOT a 1:1 file-to-file `using`: in .NET the import
unit is the *namespace*, not the file, and .NET has no per-symbol `show`
clause to translate. Codegen MUST NOT emit a textual relative-path `using`
(e.g. `using ./machine_state.cs`) — that is not valid C#. The single consumed
symbol `tailRecursionBudgetInit` is reached via its containing static class
(`MachineStateConstants.TailRecursionBudgetInit`) once the namespace is
`using`-imported.

**Nuance (mirror convspec):** Import-unit nuance — Dart imports a *library/file*;
C# imports a *namespace*. The 1:1 mapping is "each Dart import line → one C#
`using <namespace>;` line that resolves to the namespace of the converted
target file"; the depgraph/namespace stage owns the filename → namespace
mapping. Show/hide nuance: ABSENT here (no `show`/`hide`); per the
`goal_queue.dart.md` precedent there is no faithful C# counterpart for
per-symbol narrowing because `using` imports the full public surface.
Value-vs-reference / null-safety / async / Stream / isolate: NOT APPLICABLE
— a directive declares no values/types and has no runtime form.
Reference-identity: NOT APPLICABLE — imports do not produce instances.

**Idiom / research:** `rf-dart-relative-import-to-csharp-namespace-using`
(recorded in convspec).

### Construct 2 — dart.top-level-function.pure-int-int.expression-body.ternary-arrow.nextTailBudget

**Source form:** `int nextTailBudget(int current) => (current <= 0) ? 0 : current - 1;`
— Dart top-level (library-level) function. Single non-nullable `int`
parameter `current`. `int` return. Expression body (arrow `=>`). Single
ternary expression. Pure, deterministic. Doc comment: 'Returns the next tail
budget after one tail reduction. If zero, the scheduler should yield and
reset to tailRecursionBudgetInit.'

**Target decision (→ C#/.NET):** Map to a `public static int NextTailBudget(int current)`
method on a `public static class Fairness` in the namespace mirroring
`lib/runtime/`. Expression-bodied member preserving the single conditional:
the Dart `(current <= 0) ? 0 : current - 1` becomes the C# expression-bodied
`=> current <= 0 ? 0 : current - 1;`. C# does NOT permit free-floating
top-level function definitions outside a type (the Dart top-level binding
must be re-housed in a static class — same rationale as the
`tailRecursionBudgetInit` const re-housing decided in
`machine_state.dart.md`). `public` is canonical for a ported top-level Dart
binding (Dart top-level bindings are library-public unless name-prefixed
with `_`; `nextTailBudget` is not prefixed). `static` is correct (the
function holds no instance state). PascalCase per the .NET method-naming
guideline (`nextTailBudget` → `NextTailBudget`). Expression-bodied form
mirrors the Dart arrow verbatim; NOT block-bodied `{ return ... ; }` (would
lose the arrow-form parity codegen tracks for round-trip review). NOT an
extension method on `int` (would introduce a Dart-absent calling surface
`current.NextTailBudget()` not in the source contract). The XML doc-comment
form `/// <summary>Returns the next tail budget after one tail reduction. If
zero, the scheduler should yield and reset to tailRecursionBudgetInit.</summary>`
is the standard .NET doc-comment convention (well-known, applied uniformly).

**Nuance (mirror convspec):** Top-level-binding mapping nuance — Dart permits
library-level function declarations; C# does not. The faithful .NET
counterpart is a `public static` method on a `public static class` (the
.NET-canonical home for a free function). Naming nuance: `nextTailBudget`
(Dart camelCase) → `NextTailBudget` (C# PascalCase) per the official .NET
capitalisation conventions; the parameter `current` stays camelCase
(parameters are camelCase in both languages per the same convention).
Value-vs-reference: `int` is a value type in both languages; the parameter
is passed by value; the return is a value — identical semantics.
Null-safety: `int` is non-nullable in both languages (no `?` annotation);
the function neither accepts nor returns null; under enabled NRT the C#
signature carries the same non-nullable contract. Async / Stream / Future:
ABSENT — the function is synchronous and pure; codegen MUST NOT wrap it in
`async` / `Task<int>` / `ValueTask<int>` (would invent async semantics the
source does not have). Overflow / arithmetic nuance: Dart native `int` is
64-bit signed; the C# counterpart for identifier-sized budgets is
`System.Int32` (per `machine_state.dart.md` construct "typedef
opaque-int-identifier ..."); the operation `current - 1` will not approach
`Int32.MinValue` in practice (tail budget starts at 26 and decreases by 1
per call, with the early-return guard `current <= 0`); no `checked`/`unchecked`
block is required. The `(current <= 0) ? 0 : current - 1` shape makes the
guard load-bearing: codegen MUST preserve the explicit zero-clamp branch,
NOT replace with `Math.Max(0, current - 1)` — although the two forms are
extensionally equivalent over all `int` inputs (the convspec rationale
section re-derives this; ternary returns `0` iff `current <= 0`, else
`current - 1`; `Math.Max(0, current - 1)` returns `0` iff `current - 1 <= 0`
i.e. `current <= 1`, and `current - 1` otherwise — they agree at every
integer including `current == 1` where both return `0`), the spec mandates
preserving the literal source shape because (a) it is the literal source
shape and (b) the `<= 0` guard names the spec boundary that the doc
comment cites, whereas `Math.Max` obscures it (a readability / traceability
decision, not a correctness one). Purity / determinism: idempotent on
equal inputs; .NET counterpart is identical (`public static` method with no
captured state).

**Idiom / research:** `rf-dart-top-level-pure-function-to-csharp-static-class-method`
(recorded in convspec).

### Construct 3 — dart.top-level-function.pure-zero-arity-int.expression-body.const-return.resetTailBudget

**Source form:** `int resetTailBudget() => tailRecursionBudgetInit;` — Dart
top-level zero-arity function. `int` return. Expression body. Single
reference to the imported `tailRecursionBudgetInit` (the compile-time
`const int` from `machine_state.dart`, value `26`). Pure, deterministic,
side-effect-free; semantically a *named accessor* for the spec constant.
Doc comment: 'Reset the budget after a yield.'

**Target decision (→ C#/.NET):** Map to
`public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;`
on the same `public static class Fairness` that hosts `NextTailBudget`.
Expression-bodied; `static`; `public`. The body references the converted
constant by its qualified static-class path
(`MachineStateConstants.TailRecursionBudgetInit`, the .NET home decided in
`machine_state.dart.md`) rather than copying the literal `26` — preserving
the single source of truth and the named-constant traceability the Dart
side has by importing the named const (codegen MUST NOT inline the literal
`26` here). NOT a
`public static int ResetTailBudget { get; } = MachineStateConstants.TailRecursionBudgetInit;`
property (would change the surface from a method-call site to a
property-access site — a silent API-shape shift). NOT a `public const` (C#
`const` declarations are not function members — syntactically invalid here).
NOT a `public static readonly int` field (would lose the function-call
surface and would not be usable in constant contexts the way a method call
is callable everywhere). XML doc comment `/// <summary>Reset the budget
after a yield.</summary>` standard .NET form.

**Nuance (mirror convspec):** Naming nuance — `resetTailBudget` →
`ResetTailBudget` (PascalCase). Named-constant-traceability nuance: the
Dart side imports `tailRecursionBudgetInit` by name and the function body
forwards it by name (NOT by literal); the .NET port MUST preserve the
by-name forwarding (`MachineStateConstants.TailRecursionBudgetInit`, not
`26`) so the value remains tunable from a single declaration site — this
is load-bearing because the runtime spec calls out 26 as a tunable
parameter, not a hard-coded constant. Function-vs-property nuance: in C#,
a zero-arity method and a get-only property are different call sites
(`F()` vs `F`) and different reflection surfaces; the .NET port MUST keep
the method form because the Dart source declares a function (call-syntax
`resetTailBudget()`), not a getter (`resetTailBudget` with no parens) —
this is a deliberate Dart-style choice (Dart *has* top-level getters and
chose not to use one here, presumably for symmetry with `nextTailBudget`
and for the call-syntax signal that it is a *computation* contract rather
than a *property* contract; both .NET forms compile but only the method
form preserves the source contract). Value-vs-reference: `int` value type
in both languages, identical semantics. Null-safety: non-nullable in both;
no nullable annotations. Async / Stream: ABSENT. Purity / determinism:
identical (the function reads a compile-time constant and returns it).

**Idiom / research:** `rf-dart-top-level-pure-function-to-csharp-static-class-method`
(recorded in convspec).

### Conversion units (from convspec, verbatim)

1. `using` directive in `lib/runtime/fairness.cs` pointing at the namespace
   of the converted `lib/runtime/machine_state.cs` (depgraph/namespace step
   owns the exact namespace name).
2. `public static class Fairness` in the namespace mirroring `lib/runtime/`
   — the .NET-canonical home for the two ported top-level functions.
3. `public static int NextTailBudget(int current) => current <= 0 ? 0 : current - 1;`
   — expression-bodied static method preserving the Dart arrow form and the
   explicit zero-clamp branch (NOT `Math.Max`).
4. `public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;`
   — expression-bodied static method forwarding the named constant by
   qualified path (NOT inlining the literal `26`).

## 3. Decomposed Task Units

- **T1** — Emit `using <namespace-of-machine_state.cs>;` directive at the top
  of `lib/runtime/fairness.cs` (namespace name resolved by depgraph/namespace
  step; codegen MUST NOT emit a textual relative-path `using`).
- **T2** — Emit `namespace <namespace-mirroring-lib/runtime/>` wrapper for the
  converted file (depgraph/namespace step owns the exact namespace string).
- **T3** — Emit `public static class Fairness { ... }` as the .NET-canonical
  home for the two ported top-level functions.
- **T4** — Emit `public static int NextTailBudget(int current) => current <= 0 ? 0 : current - 1;`
  expression-bodied member; preserve the explicit ternary zero-clamp (do NOT
  rewrite as `Math.Max(0, current - 1)`); preserve `int` (System.Int32), no
  `checked`/`unchecked` block.
- **T5** — Emit `public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;`
  expression-bodied member; forward the named constant by qualified path
  (do NOT inline the literal `26`); keep the method form (do NOT lower to a
  property, `const`, or `static readonly` field).
- **T6** — Translate the two Dart `///` doc comments into XML doc comments
  (`/// <summary>...</summary>`) attached to `NextTailBudget` and
  `ResetTailBudget` respectively; standard .NET doc-comment form.

## 4. Research Findings

None required — every construct's target decision and nuance is verbatim-derivable
from the ratified convspec (`.codeconv/conversion-specs/lib/runtime/fairness.dart.md`)
and its cross-referenced sibling convspecs
(`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md`,
`.codeconv/conversion-specs/lib/runtime/goal_queue.dart.md`). The two idiom
identifiers (`rf-dart-relative-import-to-csharp-namespace-using`,
`rf-dart-top-level-pure-function-to-csharp-static-class-method`) are
recorded in the convspec and reused here without modification. CLAUDE.md
mandates that "Codegen MUST NOT emit ...", "MUST preserve the explicit
zero-clamp branch", and "MUST preserve the by-name forwarding" — all three
are direct re-statements of convspec target_decision text, not new research.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/lib/runtime/fairness.dart.md`
(every target_decision, nuance, idiom_id, and conversion_unit in this plan
is a verbatim mirror of the corresponding convspec field). Cross-file
consistency with
`.codeconv/conversion-specs/lib/runtime/machine_state.dart.md` (the
`MachineStateConstants.TailRecursionBudgetInit` static-class home for the
ported `tailRecursionBudgetInit`) is preserved by the by-name forwarding
in T5 and the depgraph-driven `using` in T1. The convspec's depgraph edge
`lib/runtime/fairness.dart → lib/runtime/machine_state.dart` is reflected
in the tombstone `dependencies: [lib/runtime/machine_state.dart]` and is
load-bearing for T1 / T5.

## 6. Escalations

None.
