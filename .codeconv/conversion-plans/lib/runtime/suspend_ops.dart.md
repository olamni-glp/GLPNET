---
path: lib/runtime/suspend_ops.dart
cycle_group_id: 34
scc_siblings: []
generated_at: 2026-05-21T16:05:00Z
source_sha256: b557f12dac0174dffbd0ebd4fc417e345711aed7c8ea434784d4e64ac7288069
schema_version: 1
---

# Conversion Plan: lib/runtime/suspend_ops.dart

## 1. Source Analysis

The file `lib/runtime/suspend_ops.dart` (67 lines, sha256
`b557f12da...c7288069`) defines the `SuspendOps` static-only holder
class that implements the FCP-exact "ONE shared `SuspensionRecord`,
MANY `SuspensionListNode` wrappers" suspension idiom from
heap-pointer-architecture-spec.md v3.0.

Inspection of the .dart source line-by-line:

- **Lines 1-5: five import directives.** Four relative-same-package
  imports (`machine_state.dart`, `heap_fcp.dart`, `suspension.dart`,
  `terms.dart`, none with `show`/`hide`) and one `package:`-URI
  import with a `show VariableEntry` narrowing clause
  (`package:glp_runtime/multiagent/variable_table.dart`).
- **Lines 7-11: class-level `///` doc-comment.** Documents the
  FCP-exact contract: "Suspensions are stored on WRITER cells (not
  reader cells); for imported readers, suspensions are stored in
  VariableEntry."
- **Line 12: `class SuspendOps {`.** Plain Dart class -- no `final`,
  no `sealed`, no `mixin`, no superclass clause.
- **Lines 13-19: `suspendGoalFCP` `///` doc-comment.** Includes a
  bulleted `Parameters:` section documenting `heap`, `goalId`,
  `kappa`, `readerVarIds`.
- **Lines 20-33: `static void suspendGoalFCP({...}) {...}`.** Four
  named-required parameters (`HeapFCP heap`, `int goalId`, `int
  kappa`, `Set<int> readerVarIds`). Body allocates ONE shared
  `SuspensionRecord(goalId, kappa)` (line 27), then
  `for (final addr in readerVarIds)` (lines 30-32) calls
  `_suspendOnVariable(heap, addr, sharedRecord)` -- the LOAD-BEARING
  shared-reference fan-out idiom.
- **Lines 35-57: `static void _suspendOnVariable(HeapFCP heap, int
  addr, SuspensionRecord record) {...}`.** Three positional
  parameters. Body: `heap.derefAddr(addr)` (line 38), then a
  three-branch dispatch on the runtime type of the result:
  - **Branch 1 (lines 40-46): `if (result is VariableEntry)`** ->
    `final node = SuspensionListNode(record); node.next = result
    .suspensions; result.suspensions = node; return;` -- O(1)
    linked-list head-insert into the imported-reader's per-cell
    suspension chain; assignment order matters (Next set BEFORE
    Suspensions is reassigned).
  - **Branch 2 (lines 48-53): `if (result is VarRef)`** -> extract
    `result.addr` as `writerAddr`, delegate to
    `heap.suspendOnWriter(writerAddr, record)`.
  - **Branch 3 (lines 55-57, implicit fall-through):** silent no-op
    for the already-bound-to-ground case. The in-body comment
    documents that this "shouldn't normally happen if we're
    suspending on unbound vars" but accepts the no-op as faithful
    behaviour.
- **Lines 59-66: `static void suspendGoal({...}) { throw
  UnimplementedError(...); }`.** Deprecated stub preserving the
  pre-FCP API surface; three named-required parameters; body throws
  `UnimplementedError('Legacy suspendGoal deprecated - use
  suspendGoalFCP')`.
- **Line 67: closing `}`** of the class.

Cross-file dependencies (all upstream-pinned per convspec):
- `HeapFCP.derefAddr` / `HeapFCP.suspendOnWriter` (heap_fcp.dart.md)
- `SuspensionRecord` / `SuspensionListNode` reference-type classes
  (suspension.dart.md)
- `VariableEntry.suspensions` mutable property (variable_table.dart.md)
- `VarRef.addr` property + `VarRef` sealed reference-type (terms.dart.md)
- `GoalId` / `Pc` typedefs (machine_state.dart.md; both = `int`)

The file is small (67 lines, 6 non-trivial constructs + 1 trivial
doc-comment row) but semantically load-bearing -- the FCP-exact
shared-reference invariant is what makes goal-resume's atomic
`disarm()` propagation correct.

## 2. Dart → C#/.NET Conversion Plan

The following construct-by-construct table mirrors the ratified
convspec verbatim (no re-derivation; FR-024 / SC-007). The
`→` arrow is U+2192.

- **`import 'machine_state.dart';` → `using <namespace>;`.** One C#
  `using` directive naming the namespace hosting the converted
  `machine_state.cs` (where the ported `GoalId`/`Pc` global-using
  aliases live). The depgraph/namespace stage owns the
  filename→namespace mapping; codegen MUST NOT emit a textual
  relative-path `using`. No `show`/`hide` to drop. Idiom:
  `rf-dart-relative-import-to-csharp-namespace-using` (cache hit
  from fairness/suspend/hanger).

- **`import 'heap_fcp.dart';` → `using <namespace>;`.** One C#
  `using` directive naming the namespace hosting the converted
  `heap_fcp.cs`. The consumed methods `derefAddr` and
  `suspendOnWriter` map to PascalCase `DerefAddr` / `SuspendOnWriter`
  -- both pinned in heap_fcp.dart.md, CONSUMED not re-derived. Same
  idiom.

- **`import 'suspension.dart';` → `using <namespace>;`.** One C#
  `using` directive naming the namespace hosting the converted
  `suspension.cs`. `SuspensionRecord` and `SuspensionListNode` are
  pinned as reference-type classes per suspension.dart.md
  (`rf-dart-shared-mutable-record-by-reference-to-csharp-class`) --
  load-bearing for the ONE-shared-record idiom. Same import idiom.

- **`import 'terms.dart';` → `using <namespace>;`.** One C# `using`
  directive naming the namespace hosting the converted `terms.cs`.
  Only `VarRef` is referenced (as a type-discriminator in the
  `is VarRef varRef` pattern) and `result.addr` (→ `varRef.Addr`).
  Both pinned in terms.dart.md, CONSUMED. Same import idiom.

- **`import 'package:glp_runtime/multiagent/variable_table.dart'
  show VariableEntry;` → `using <namespace>;`.** One C# `using`
  directive naming the namespace hosting the converted
  `variable_table.cs`. The `package:`-URI prefix is a
  Dart-resolution detail with no .NET counterpart; the `show
  VariableEntry` narrowing clause has NO .NET counterpart either
  (C# `using` imports a namespace's full publicly-visible surface,
  no per-symbol narrowing form). Loss of narrowing is documented
  nuance, NOT escalated (precedent: goal_queue / suspend /
  fairness). Same idiom.

- **`class SuspendOps { ... only static members ... }` → `public
  static class SuspendOps`.** Sealed by virtue of `static`; the
  no-instantiation contract is compile-time-enforced (per Microsoft
  Learn: "A static class can't be instantiated. ... Static classes
  are sealed and therefore cannot be inherited"). Contains the three
  converted static members. Free-floating namespace-scope functions
  REJECTED (not legal C#). Idiom:
  `rf-dart-static-only-holder-to-csharp-static-class` (cache hit
  from abandon.dart.md / commit.dart.md).

- **`static void suspendGoalFCP({required HeapFCP heap, required int
  goalId, required int kappa, required Set<int> readerVarIds})` →
  `public static void SuspendGoalFCP(HeapFCP heap, long goalId, long
  kappa, ISet<long> readerVarIds)`.** Named-required → positional
  (C# 11 `required` is for properties only; positional without
  default IS required by the compiler); call-site readability via
  named-argument syntax (`SuspendOps.SuspendGoalFCP(heap: h, ...)`).
  `int` → `long` per `rf-dart-int-to-csharp-long-width`. `Set<int>`
  → `ISet<long>` (read-only parameter; preserves set-semantics
  documentation; codegen MAY narrow to `IEnumerable<long>` if no
  set-specific member is used -- observation shows only `foreach`,
  so either is valid; spec default = `ISet<long>`). Body: `var
  sharedRecord = new SuspensionRecord(goalId, kappa); foreach (var
  addr in readerVarIds) { SuspendOnVariable(heap, addr,
  sharedRecord); }` -- the LOAD-BEARING shared-reference invariant
  is preserved by `SuspensionRecord` being a reference-type class
  (per suspension.dart.md; do NOT change to `struct`/`record
  struct`). `final` → `var` (mutable local, never reassigned).
  Iteration-order observably irrelevant. Idioms:
  `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`,
  `rf-dart-int-to-csharp-long-width`,
  `rf-dart-shared-mutable-record-by-reference-to-csharp-class`.

- **`static void _suspendOnVariable(HeapFCP heap, int addr,
  SuspensionRecord record)` → `private static void
  SuspendOnVariable(HeapFCP heap, long addr, SuspensionRecord
  record)`.** Leading underscore DROPPED -- `private` is the
  canonical visibility marker; PascalCase per Microsoft Learn
  naming guidelines (underscore prefix reserved for private fields,
  not methods); precedent heap_fcp.dart.md `_ForwardSuspensions` →
  `ForwardSuspensions`. Body: `var result = heap.DerefAddr(addr);`
  followed by three-branch dispatch:
  - **`if (result is VariableEntry entry)`** → `var node = new
    SuspensionListNode(record); node.Next = entry.Suspensions;
    entry.Suspensions = node; return;` -- C# 7+ type-pattern binds
    typed local in one step; O(1) head-insert; assignment order
    matters (Next BEFORE Suspensions).
  - **`if (result is VarRef varRef)`** → `var writerAddr = varRef
    .Addr; heap.SuspendOnWriter(writerAddr, record); return;` --
    same type-pattern form.
  - **Fall-through silent no-op** for the already-bound-to-ground
    case -- preserved exactly; codegen MUST NOT add `throw` (source
    comment explicitly accepts no-op).
  Method-name decisions (`DerefAddr`, `SuspendOnWriter`) and
  property-name decisions (`entry.Suspensions`, `varRef.Addr`) are
  CONSUMED from upstream specs. `addr` `int` → `long`. Idiom:
  `rf-dart-shared-mutable-record-by-reference-to-csharp-class`
  (carries the reference-by-reference invariant).

- **`static void suspendGoal({required int goalId, required int
  kappa, required Set<int> readerVarIds}) { throw
  UnimplementedError(...); }` → `[System.Obsolete("Legacy
  SuspendGoal deprecated - use SuspendGoalFCP")] public static void
  SuspendGoal(long goalId, long kappa, ISet<long> readerVarIds) {
  throw new NotImplementedException("Legacy suspendGoal deprecated -
  use suspendGoalFCP"); }`.** `UnimplementedError` →
  `NotImplementedException` per
  `rf-dart-unimplemented-error-to-csharp-notimplemented` (INTENT
  mapping; .NET has no `Error` vs `Exception` split). Named-required
  → positional. `int` → `long`. `Set<int>` → `ISet<long>` (same
  reasoning as `SuspendGoalFCP`). `[Obsolete]` attribute LIFTS the
  Dart doc-comment `(deprecated)` marker to a compile-time warning
  -- tighter contract, strictly correct (the body's
  `UnimplementedError` already signals "do not call this"). Idiom:
  `rf-dart-unimplemented-error-to-csharp-notimplemented` (cache hit
  from abandon.dart.md / boot_loader.dart.md).

- **Doc-comments `///` → C# XML-doc `/// <summary>...</summary>`
  etc.** Class header: `/// <summary>...</summary>/// <remarks>Per
  heap-pointer-architecture-spec.md v3.0: ...</remarks>`.
  `SuspendGoalFCP`: `<summary>` + `<param name="heap">The
  heap</param>` etc. per Dart's `Parameters:` bullet list.
  `SuspendOnVariable`: `<summary>`. `SuspendGoal`: `<summary>` plus
  the `[Obsolete]` attribute (the "(deprecated)" tag is consumed by
  the attribute). In-body `//` comment preserved verbatim as C# `//`
  single-line comment. Trivial mechanical mapping.

## 3. Decomposed Task Units

- T1. Emit `using` directives for the five upstream namespaces
  (machine_state.cs / heap_fcp.cs / suspension.cs / terms.cs /
  variable_table.cs). One-line done.
- T2. Emit `public static class SuspendOps` shell with no fields,
  no instance constructor, no instance members. One-line done.
- T3. Emit `public static void SuspendGoalFCP(HeapFCP heap, long
  goalId, long kappa, ISet<long> readerVarIds)` with the
  shared-record fan-out body: allocate ONE `new
  SuspensionRecord(goalId, kappa)`, then `foreach (var addr in
  readerVarIds) SuspendOnVariable(heap, addr, sharedRecord);`. One-
  line done.
- T4. Emit `private static void SuspendOnVariable(HeapFCP heap,
  long addr, SuspensionRecord record)` with the three-branch
  dispatch (`is VariableEntry entry` head-insert with order-
  preserving assignment / `is VarRef varRef` delegate to
  `heap.SuspendOnWriter` / silent fall-through no-op). One-line done.
- T5. Emit `[System.Obsolete("Legacy SuspendGoal deprecated - use
  SuspendGoalFCP")] public static void SuspendGoal(long goalId,
  long kappa, ISet<long> readerVarIds) { throw new
  NotImplementedException("Legacy suspendGoal deprecated - use
  suspendGoalFCP"); }`. One-line done.
- T6. Map the seven `///` doc-comment blocks to C# XML-doc
  `<summary>`/`<remarks>`/`<param>` tags on the class and each
  method; preserve the in-body `// Already bound to ground...`
  comment verbatim as a C# `//` single-line comment. One-line done.

## 4. Research Findings

none required -- every construct resolves via cache-hit on
established convspec idioms:

- `rf-dart-relative-import-to-csharp-namespace-using` (five
  imports; established by fairness.dart.md, reused by suspend /
  hanger / variable_table specs).
- `rf-dart-static-only-holder-to-csharp-static-class` (SuspendOps
  class; established by abandon.dart.md, reused by commit.dart.md).
- `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`
  (named-required parameters on both public methods; established
  by machine_state.dart.md, reused widely).
- `rf-dart-shared-mutable-record-by-reference-to-csharp-class`
  (SuspendGoalFCP shared-reference fan-out + SuspendOnVariable
  linked-list mutation; established by suspension.dart.md, reused
  by variable_table.dart.md / heap_fcp.dart.md).
- `rf-dart-unimplemented-error-to-csharp-notimplemented` (SuspendGoal
  deprecated stub; established by abandon.dart.md, reused by
  boot_loader.dart.md).
- `rf-dart-int-to-csharp-long-width` (every `int` and `Set<int>`
  element type; established by cells.dart.md, reused throughout).

All cited authoritative-doc bases (Microsoft Learn for .NET,
dart.dev / api.dart.dev for Dart) per SC-006 are recorded inline
in the convspec's "Rationale and research provenance" section --
this plan CONSUMES, does not re-derive.

## 5. Consistency Pass

fixed -- derived from convspec
`.codeconv/conversion-specs/lib/runtime/suspend_ops.dart.md` (sha
`b557f12da...c7288069`, ratified mirror). Every construct row in §2
maps 1:1 to a convspec row; every idiom-id citation in §4 reuses
an established project idiom per FR-024 / SC-007; cross-file
decisions (method names `DerefAddr`/`SuspendOnWriter`, property
names `Suspensions`/`Addr`, reference-type pins for
`SuspensionRecord`/`SuspensionListNode`/`VariableEntry`/`VarRef`,
typedef aliases `GoalId`/`Pc`) are CONSUMED from upstream specs,
not re-derived here. The convspec records ZERO escalations
(`escalations: []` at YAML line 557) and the source sha256
matches the tombstone.

## 6. Escalations

None.
