---
path: lib/runtime/abandon.dart
cycle_group_id: 30
scc_siblings: []
generated_at: 2026-05-21T15:05:00Z
source_sha256: 2171582c81708de2497bd8a474d148beec0ca20dbfb4f687e5e7b4dfc4a2653a
schema_version: 1
---

# Conversion Plan: lib/runtime/abandon.dart

## 1. Source Analysis

Inspection of `glp_runtime_net/lib/runtime/abandon.dart` (12 lines, sha256
`2171582c81708de2497bd8a474d148beec0ca20dbfb4f687e5e7b4dfc4a2653a`) shows
exactly four syntactic constructs:

1. `import 'machine_state.dart';` — a single relative sibling-module import
   that resolves the `GoalRef` identifier used in the return-type position
   of the static method.
2. `class AbandonOps { ... }` — a Dart class with no fields, no instance
   constructor, no instance members; used purely as a namespacing container
   for FCP "abandon" operations.
3. `static List<GoalRef> abandonWriter({required int writerId}) { throw
   UnimplementedError('Abandon operation not implemented in FCP design'); }`
   — the sole member: a static method with one named-required parameter
   returning `List<GoalRef>`, whose body is a single `throw
   UnimplementedError(...)` statement (deliberate platform-stub).
4. Two `///` doc-comment lines on the static method:
   `/// FCP-exact design: Abandon operation not yet implemented` and
   `/// TODO: Implement FCP-compatible abandon semantics`.

No fields, no instance state, no async/`Future`/`Stream`, no generics-with-
bounds, no mixins/extensions, no `late`, no `sealed`, no nullable-of-value
scenarios, no value-equality contract, no `IDisposable`-style resources, no
bitwise/shift operations. The file is a purely synchronous static throw-stub
that exists to satisfy the `AbandonOps` API surface for FCP-exact callers
while signalling that abandon semantics are not yet implemented in this
layer.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec construct-for-construct (four constructs:
two non-trivial, two trivial).

**Construct A — `dart.utility_class.static_only_holder`** →
`public static class AbandonOps` (sealed/abstract by virtue of `static`;
no instances; the .NET counterpart of a Dart "static-methods-only" holder
class per Microsoft Learn "Static Classes and Static Class Members":
*"A static class is basically the same as a non-static class, but there's
one difference: a static class can't be instantiated."* The narrowing is
strictly correct: the Dart source never instantiates `AbandonOps` and has
no instance state to preserve. Reject (a) non-static class with all-static
members (loses the compile-time no-instantiation invariant) and (b)
namespace-only translation with free functions (C# disallows top-level
free functions outside a type).

**Construct B —
`dart.static_method.named_required_param_returning_list_throws_unimplemented`**
→ `public static IList<GoalRef> AbandonWriter(long writerId) { throw new
NotImplementedException("Abandon operation not implemented in FCP
design"); }`. Three sub-decisions, each idiom-backed:

- `{required int writerId}` → positional `long writerId` (no default), per
  the project idiom `dart.named_required_parameters.required_kwargs`:
  C# has no per-parameter `required` keyword at the method level; a
  positional parameter without a default IS required by the compiler; the
  call-site readability of `AbandonOps.AbandonWriter(writerId: 42)` is
  preserved by C#'s named-argument syntax (Microsoft Learn: *"Named
  arguments enable you to specify an argument for a parameter by matching
  the argument with its name…"*).
- `int` → `long` per the recurring project idiom
  `dart.int.to.csharp.long_width` (Dart's `int` is a 64-bit integer; C#'s
  64-bit primitive is `long`).
- `List<GoalRef>` return → `IList<GoalRef>` per spec default (faithful
  abstract contract over the concrete implementation type, per Microsoft
  Learn `IList<T>`: *"Represents a collection of objects that can be
  individually accessed by index."* Matches Dart `List<T>` *"An indexable
  collection of objects with a length."*). Codegen MAY substitute
  concrete `List<GoalRef>` only if a future call-site needs a
  `List<T>`-exclusive member (none observed; the method only ever throws).
- Body: a single `throw new NotImplementedException(...)`. The mapping is
  by INTENT — Dart `UnimplementedError` (extends `Error`, *"Thrown by
  operations that have not been implemented yet."*) → .NET
  `NotImplementedException` (derives from `SystemException`, *"The
  exception that is thrown when a requested method or operation is not
  implemented."*). .NET has no `Error` vs `Exception` hierarchy split;
  same idiom established in the prior `boot_loader.dart` spec
  (`rf-dart-unimplemented-error-to-csharp-notimplemented`).
- Value-vs-reference: `IList<GoalRef>` is a heap-allocated reference type
  (matches Dart `List<GoalRef>`); `long writerId` is a value type
  (`Int64`, matches Dart `int` pass-by-value).
- Null-safety: under enabled NRT, return is non-nullable `IList<GoalRef>`;
  parameter `long` is a non-nullable value type. Both match the Dart
  null-safe source signature. The body's `throw` means the non-null
  return contract is never observably reached, but the type declaration
  remains faithful to the source signature per FR-013.

**Construct C — `dart.docblock_triple_slash`** (trivial) → C# XML-doc
comments on the static method: `/// <summary>FCP-exact design: Abandon
operation not yet implemented.</summary>` and `/// <remarks>TODO:
Implement FCP-compatible abandon semantics.</remarks>` (codegen may
collapse to two adjacent `///` lines under a single `<summary>` element —
codegen's call). TODO semantics preserved verbatim in source for the
eventual implementer.

**Construct D — `dart.import.relative_sibling_module`** (trivial) →
Dart relative imports have no syntactic counterpart in C#; the `GoalRef`
identifier becomes resolvable via a C# `using` of the namespace produced
for `lib/runtime/machine_state.cs`. The concrete `using` directive is
emitted by the codegen stage once the project-wide namespace decision is
known (deferred to `glp_runtime.dart` / `machine_state.dart` specs;
out of scope for this per-file plan).

## 3. Decomposed Task Units

- **T1**: Emit `public static class AbandonOps` (per construct A) — done.
- **T2**: Emit `public static IList<GoalRef> AbandonWriter(long writerId)`
  with body `throw new NotImplementedException("Abandon operation not
  implemented in FCP design");` (per construct B) — done.
- **T3**: Emit XML-doc `/// <summary>…</summary>` + `/// <remarks>TODO:
  …</remarks>` on `AbandonWriter` (per construct C) — done.
- **T4**: Emit `using` for the namespace of `lib/runtime/machine_state.cs`
  at codegen time once the project-wide namespace decision is settled
  (per construct D) — done (deferred to codegen / sibling spec; this
  per-file plan records the requirement only).

## 4. Research Findings

None required — the convspec already cites authoritative sources verbatim
for every non-trivial decision (Microsoft Learn for `static class`, named
arguments, `NotImplementedException`, `IList<T>`; api.dart.dev for
`List<T>`, `UnimplementedError`), and the four project idioms reused
(`static-class` narrowing, `named_required_parameters.required_kwargs`,
`int.to.csharp.long_width`, `unimplemented-error-to-notimplemented`) are
established in prior ratified specs (`moded_term.dart`, `boot_loader.dart`,
`opcodes.dart`, `error.dart`). Construct C (doc-comments) and D (relative
import) are marked `trivial` in the convspec and skip research per the
contract.

## 5. Consistency Pass

- Construct A (static-class narrowing): fixed — derived from
  ratified convspec construct
  `dart.utility_class.static_only_holder` and its cited
  Microsoft Learn "Static Classes and Static Class Members" source.
- Construct B (static method + named-required + `int→long` widening +
  `List→IList` + `UnimplementedError→NotImplementedException`):
  fixed — derived from ratified convspec construct
  `dart.static_method.named_required_param_returning_list_throws_unimplemented`,
  its cited Microsoft Learn / api.dart.dev sources, and the four
  reused project idioms recorded in §4.
- Construct C (`///` doc-comments → XML-doc): fixed — derived from
  ratified convspec construct `dart.docblock_triple_slash`
  (`trivial: true`).
- Construct D (relative import → deferred `using`): fixed — derived
  from ratified convspec construct `dart.import.relative_sibling_module`
  (`trivial: true`).

All four constructs in this plan map one-to-one to the four constructs
in the ratified convspec; no construct is omitted, none added; the §3
task units (T1–T4) align construct-for-construct. Sibling-spec coupling
(`GoalRef` identifier from `machine_state.dart`) is preserved verbatim
per the convspec.

## 6. Escalations

None.
