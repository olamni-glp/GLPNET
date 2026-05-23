---
path: lib/runtime/heap_fcp.dart
cycle_group_id: 31
scc_siblings: []
generated_at: 2026-05-21T16:08:00Z
source_sha256: 18b5962454f8a7e7d8d1b48c9d711bfe92b3699180dcc4d9ac7a3288a26378f3
schema_version: 1
---

# Conversion Plan: lib/runtime/heap_fcp.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/lib/runtime/heap_fcp.dart` (sha256
`18b5962454f8a7e7d8d1b48c9d711bfe92b3699180dcc4d9ac7a3288a26378f3`, 890
lines). The file is the load-bearing FCP two-cell heap with pointer
architecture (per `docs/heap/heap-pointer-architecture-spec.md` v3.0/v3.2).

Top of file: `library;` directive (no name) preceded by doc-comments
describing the FCP two-cell architecture (reader cells point TO writer
cells; writer cells contain null/Pointer/SuspensionListNode/Pointer-chain;
suspensions live on writer cells; ValueTag indicates bound-to-ground).
Four package-internal imports: `terms.dart` (Term hierarchy), `suspension.dart`
(SuspensionRecord + SuspensionListNode), `machine_state.dart` (GoalRef +
friends), and `multiagent/variable_table.dart` with `show VariableEntry`
(single-symbol narrowing).

Top-level declarations (in source order):

1. `enum CellTag { WrtTag, RoTag, ValueTag }` — three-member tag-only
   enum (line 16-20); SHOUTcase-acronymed member spellings are documented
   spec names.

2. `class HeapCell` (line 23-31) — the mutable heap-slot container.
   `dynamic content` + `CellTag tag`, both publicly mutable (no `final`).
   Single positional ctor `HeapCell(this.content, this.tag);`. Two
   expression-bodied getters: `hasValue => tag == CellTag.ValueTag;`
   and `hasSuspensions => content is WriterContent && (content as
   WriterContent).suspensions != null;`. No `==`/`hashCode` override.
   Mutated in place by `bindWriterWithCallbackControl`, `bindWriterToReader`,
   `suspendOnWriter`, `bindImportedReader`, `_forwardSuspensions`, and
   `addSuspension`.

3. `class Pointer` (line 34-41) — `final int targetAddr;` + positional
   ctor + `toString() => 'Ptr($targetAddr)';`. No equality override
   (reference identity).

4. `class WriterContent` (line 48-56) — `final int readerAddr;`
   (immutable) + `SuspensionListNode? suspensions;` (mutable, nullable).
   Positional ctor with OPTIONAL POSITIONAL parameter `[this.suspensions]`
   (Dart `[]` brackets, default null). `toString() =>
   'WriterContent(reader=$readerAddr, sus=$suspensions)';`. No equality
   override.

5. `class HeapFCP` (line 65-889) — the master runtime state container.
   Three direct fields:
   - `final List<HeapCell> cells = [];` (reference fixed; contents mutated
     via `add`).
   - `int HP = 0;` (mutable; UPPER-case is the documented WAM/spec name).
   - `final Map<int, void Function(Term)> _bindCallbacks = {};` (private
     by Dart leading-underscore convention; keyed by writerAddr; mutated
     via `[k]=v` set and `.remove(k)`).

   Methods (in source order — names map to .NET PascalCase per nuance):
   - `(int, int) allocateVariable()` — Dart 3 record return; allocates
     paired writer/reader cells (bidirectional Pointers); HP += 2.
   - `int allocateImportedReader()` / `int allocateImportedWriter()` —
     single-cell allocators with null content; HP++.
   - `bool isWriter(int) / isReader(int) / isValue(int)` — three
     expression-bodied bounds-checked tag predicates.
   - `int? tryWriterForReader(int)` — nullable; ~90-line doc-comment
     documenting three caller modes (suspending/read-only/binding) and
     common mistakes.
   - `int? readerForWriter(int)` — three cases: bidirectional-Pointer
     (verify), WriterContent (extract readerAddr), else null.
   - `int pairedReaderAddr(int)` — non-nullable; falls back to
     `writerAddr + 1` by allocation invariant.
   - `Object derefAddr(int startAddr)` — large method with `Set<int>
     visited` cycle-detection, `CellTag? previousTag` WxW-detection,
     `switch (cell.tag)` with three case arms (RoTag/WrtTag/ValueTag);
     throws `StateError` on cycle / WxW / invalid content.
   - `List<GoalRef> bindWriter(int, Term)` / `bindWriterNoCallback(int,
     Term)` — delegating wrappers to:
   - `List<GoalRef> bindWriterWithCallbackControl(int, Term,
     {required bool fireCallback})` — the worker; in-place mutation
     (Content + Tag), walks WriterContent suspensions, optional
     remove-and-fire callback.
   - `void firePendingCallback(int)` — Dart `Map.remove` returns value;
     invoke if value bound.
   - `List<GoalRef> bindWriterToReader(int, int)` — Tag REMAINS WrtTag
     (chain semantic); Content → Pointer; forwards suspensions; relocates
     callback to target writer.
   - `void bindWriterToWriter(int, int)` — always-throws WxW violation.
   - `void suspendOnWriter(int, SuspensionRecord)` — three-branch:
     WriterContent (cons) / Pointer (promote to WriterContent) / else
     throw.
   - `void suspendOnReader(int, SuspensionRecord)` — two-branch:
     VariableEntry (mutate entry.suspensions chain) / Pointer (delegate).
   - `void _forwardSuspensions(SuspensionListNode?, int)` — private;
     walks armed; clones WRAPPER node, SHARES record; prepends to target's
     WriterContent (promoting if needed); silently ignores bound targets.
   - `static void _walkAndActivate(SuspensionListNode?, List<GoalRef>)`
     — private static; walks armed; constructs GoalRef from
     goalId!/resumePC; calls `current.record.disarm()` to propagate.
   - `bool isFullyBound(int)` — deref-and-test (NOT VarRef && NOT
     VariableEntry).
   - `Term? getValue(int)` — nullable; deref-and-cast.
   - `Term dereference(Term)` — VarRef-chase; returns original term for
     imported unbound (LOAD-BEARING — preserves caller's VarRef handle).
   - `void onBind(int, void Function(Term))` — immediate-fire if bound,
     else register.
   - `void removeBindCallback(int)`.
   - `List<GoalRef> bindImportedReader(int, Term, VariableEntry)` —
     transforms unbound imported reader (Content=VariableEntry) to
     bound (Content=Pointer→ValueTag cell); `HP++/cells.add` co-located
     per source comment "Use HP++ to keep HP in sync with cells.length".
   - Compatibility wrappers: `bindVariable`, `bindVariableConst`,
     `bindVariableStruct`, `isWriterBound`, `valueOfWriter`,
     `bindWriterConst`, `bindWriterStruct`, `isBound`.
   - Reader abstraction (extensive doc-comment markdown table on
     `isImportedReader`): `isReaderBound`, `getReaderValue`,
     `isImportedReader`, `getWriterForReader`.
   - Legacy wrappers: `getSuspensions(int)` (reads WriterContent or
     null), `addSuspension(int, SuspensionListNode)` (same dispatch as
     `suspendOnWriter` but takes pre-built node).
   - `int storeTermOnHeap(Term)` — recursive per-variant dispatch:
     VarRef (return existing addr), ConstTerm/MutualRefTerm/ModuleTerm
     (allocate ValueTag), StructTerm (recurse on args, build new
     StructTerm with VarRef args, allocate ValueTag), default → throw
     `ArgumentError`. The HP++/cells.add pairing recurs for every
     non-VarRef variant.

No async, no Stream, no isolate primitives in the file. No `lock`/no
`Interlocked`/no concurrent collections in source. No `==`/`hashCode`
override on any type. No `[Flags]`-style enum semantics. Mutation is
exclusively single-threaded (single-owning-context invariant — ratified
in escalation #4 close, commit 497428c8).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the RATIFIED convspec construct-by-construct.
Every decision below is the verbatim target_decision from
`.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`. The
single-owning-context invariant is closed (escalation #4, commit
497428c8) — fields stay plain `int Hp;` / `List<HeapCell> Cells;` /
plain `Dictionary<int, Action<Term>> _bindCallbacks;` — NO `lock`, NO
`Interlocked`, NO `ConcurrentDictionary`, NO `volatile`.

**File-header directive.** Dart `library;` has no .NET counterpart;
elided. Library doc-comments (FCP Two-Cell Heap; reader cells point TO
writer cells; writer cells contain null/Pointer/SuspensionListNode;
suspensions live on writer cells; ValueTag = bound-to-ground) become
file-header XML doc on the namespace declaration (mirroring `lib/runtime/`).

**Imports.** Dart `import 'package:glp_runtime/runtime/terms.dart';`,
`import 'package:glp_runtime/runtime/suspension.dart';`, `import
'package:glp_runtime/runtime/machine_state.dart';` → single `using
<root>.Runtime;` (covers Term hierarchy, SuspensionRecord,
SuspensionListNode, GoalRef, GoalState, Pc, GoalId — all three sibling
files target the same `lib/runtime/` namespace). Dart `import
'package:glp_runtime/multiagent/variable_table.dart' show VariableEntry;`
→ `using <root>.Multiagent;` — the `show` allow-list has no .NET
counterpart (codegen MUST NOT synthesise a `using VariableEntry = ...;`
alias). Plus `using System.Collections.Generic;` (List/Dictionary/HashSet)
and `using System;` (Action/InvalidOperationException/ArgumentException).

**`enum CellTag`** → C# `public enum CellTag { WrtTag, RoTag, ValueTag }`
in declaration order (default `int` underlying type, no explicit member
values). Member spellings `WrtTag`/`RoTag`/`ValueTag` PRESERVED VERBATIM
— spec-string-fidelity precedent (cells.dart.md / opcodes.dart.md). NOT
`[Flags]`.

**`class HeapCell`** → reference-type `public class HeapCell`. Members:
`public object? Content { get; set; }` (Dart `dynamic` → nullable
`object?` — the faithful counterpart of an in-place sum-type slot; NOT
C# `dynamic`/DLR), `public CellTag Tag { get; set; }` (mutable enum),
single positional ctor `HeapCell(object? content, CellTag tag)`,
`public bool HasValue => Tag == CellTag.ValueTag;`, `public bool
HasSuspensions => Content is WriterContent wc && wc.Suspensions !=
null;` (C# pattern-match binds `wc` in the same expression). NOT a
`record class`/`struct`/`record struct` — reference identity is THE
load-bearing semantic (every binding mutates the canonical slot; a
value-type would split observers across copies).

**`class Pointer`** → `public sealed class Pointer`. One get-only
auto-property `public int TargetAddr { get; }`, single positional ctor
`Pointer(int targetAddr)`, `public override string ToString() =>
$"Ptr({TargetAddr})";`. Equality NOT overridden (default object
reference identity matches Dart's no-`==`-override). REJECTED: `record
class` (would inject value equality); `readonly record struct`
(tempting but stored as `object?` in `HeapCell.Content` → boxing per
assignment defeats value-type benefit AND loses reference identity
across box/unbox). `GoalRef`-as-`record-struct` rationale in
machine_state.dart.md does NOT apply here.

**`class WriterContent`** → `public sealed class WriterContent`.
Members: `public int ReaderAddr { get; }` (Dart `final`), `public
SuspensionListNode? Suspensions { get; set; }` (mutable nullable;
public setter required because `wc.suspensions = node` is the in-place
mutation in `suspendOnWriter` and `_forwardSuspensions`), single ctor
`WriterContent(int readerAddr, SuspensionListNode? suspensions = null)`
(Dart `[this.suspensions]` optional-positional → .NET default-valued
positional), `ToString` override:
`$"WriterContent(reader={ReaderAddr}, sus={Suspensions?.ToString() ?? "null"})"`
(explicit `?.ToString() ?? "null"` preserves Dart's null-as-"null"
interpolation, carry-forward from suspension.dart.md). NOT a
record/struct — same shared-mutable-by-reference rationale.

**`class HeapFCP`** → reference-type `public class HeapFCP`. Direct
members:
- `public List<HeapCell> Cells { get; } = new();` (Dart `final` → get-only
  auto-property + initialiser; MUST be concrete `List<HeapCell>` — we
  need indexer + Add + Count).
- `public int Hp { get; set; } = 0;` (Dart `HP` → PascalCase `Hp` per
  .NET two-letter-acronym capitalisation rule; spec name preserved
  syntactically; mutable property).
- `private readonly Dictionary<int, Action<Term>> _bindCallbacks =
  new();` (Dart leading-underscore preserved on the field per
  goal_queue.dart.md idiom; `void Function(Term)` → `Action<Term>`).
NOT record/struct/record-struct; NOT static; NOT partial.

**`(int, int) allocateVariable()`** → `public (int WriterAddr, int
ReaderAddr) AllocateVariable()`. Body: `int writerAddr = Hp; int
readerAddr = Hp + 1; Hp += 2; Cells.Add(new HeapCell(new Pointer(
readerAddr), CellTag.WrtTag)); Cells.Add(new HeapCell(new Pointer(
writerAddr), CellTag.RoTag)); return (writerAddr, readerAddr);`. The
two-Add order MUST be preserved (writer first, reader second — pairing
invariant per spec v3.2 §3.1). Dart 3 records → .NET `ValueTuple`
(named components, structural value type).

**Imported allocators** → `public int AllocateImportedReader() { int
readerAddr = Hp++; Cells.Add(new HeapCell(null, CellTag.RoTag)); return
readerAddr; }` and symmetric `AllocateImportedWriter` (CellTag.WrtTag).
Block body preserved (not expression-bodied — source uses blocks).
`null` content is the documented "caller will populate VariableEntry"
state.

**Tag predicates** → three expression-bodied methods: `public bool
IsWriter(int addr) => addr >= 0 && addr < Cells.Count && Cells[addr]
.Tag == CellTag.WrtTag;` and analogous `IsReader` (RoTag), `IsValue`
(ValueTag). Bounds check preserved; Dart `length` → .NET `Count`.

**`int? tryWriterForReader`** → `public int? TryWriterForReader(int
readerAddr)`. Body: `var cell = Cells[readerAddr]; if (cell.Tag !=
CellTag.RoTag) return null; if (cell.Content is Pointer ptr) return
ptr.TargetAddr; return null;`. Pattern-match `is Pointer ptr` binds
the typed reference. The ~90-line doc-comment (three caller-mode
subsections, common-mistakes list) PRESERVED VERBATIM as XML doc
(`<summary>`/`<remarks>`/`<example>` tags) — load-bearing API contract.

**`int? readerForWriter`** → `public int? ReaderForWriter(int
writerAddr)`. Three cases preserved exactly: (1) `if (cell.Content is
Pointer ptr1) { int target = ptr1.TargetAddr; if (target < Cells.Count
&& Cells[target].Tag == CellTag.RoTag && Cells[target].Content is
Pointer readerPtr && readerPtr.TargetAddr == writerAddr) return target;
return null; }` — BIDIRECTIONAL VERIFICATION MUST be preserved; (2)
`if (cell.Content is WriterContent wc) return wc.ReaderAddr;`; (3)
`return null;`.

**`int pairedReaderAddr`** → `public int PairedReaderAddr(int
writerAddr) { int? reader = ReaderForWriter(writerAddr); if (reader !=
null) return reader.Value; return writerAddr + 1; }`. The `+ 1`
literal encodes the FCP allocation invariant. (Source uses `if`;
either `if` or `??` is acceptable.)

**`Object derefAddr`** → `public object DerefAddr(int startAddr)`
returning non-nullable `object`. Body uses `while (true)` loop with
`var visited = new HashSet<int>();` (Dart `<int>{}` → `HashSet<int>`)
and `CellTag? previousTag = null;`. Cycle detection: `if (visited
.Contains(current)) throw new InvalidOperationException($"Cycle
detected at address {current} - SRSW violation!");`. WxW detection
preserved verbatim. `switch (cell.Tag)` with three case arms; within
each, pattern-match on `cell.Content` (`is VariableEntry entry`, `is
Pointer ptr`, `is WriterContent`). Dart `entry.boundValue!` → C#
`entry.BoundValue` (NRT flow analysis tracks the prior null-test; no
`!` needed). Dart `cell.content as Term` → C# `(Term)cell.Content!`
(explicit cast on the ValueTag arm; the `!` is safe by ValueTag
invariant). StateError → `InvalidOperationException` throughout.
Optional defensive `default: throw new InvalidOperationException(...);`
arm is acceptable additive safety net.

**`bindWriter` family** → three methods. Wrappers: `public
List<GoalRef> BindWriter(int writerAddr, Term value) =>
BindWriterWithCallbackControl(writerAddr, value, fireCallback: true);`
and `BindWriterNoCallback(...) => ...(... fireCallback: false);`
(expression-bodied). Worker: `public List<GoalRef>
BindWriterWithCallbackControl(int writerAddr, Term value, bool
fireCallback)` (Dart `{required bool fireCallback}` → non-optional
positional; callers pass `fireCallback: true` named-argument). Body:
validate tag (throw `InvalidOperationException`), allocate
`activations`, `if (cell.Content is WriterContent wc)
WalkAndActivate(wc.Suspensions, activations);`, in-place mutation
`cell.Content = value; cell.Tag = CellTag.ValueTag;`, optional
callback fire via `if (fireCallback) { if (_bindCallbacks.Remove(
writerAddr, out var callback)) callback(value); }`, return
activations. CRITICAL: use `Dictionary.Remove(TKey, out TValue)`
overload (Microsoft Learn; .NET Core 2.0+) — preserves Dart's
`Map.remove` atomic remove-and-get.

**`firePendingCallback`** → `public void FirePendingCallback(int
writerAddr) { if (_bindCallbacks.Remove(writerAddr, out var callback))
{ var value = GetValue(writerAddr); if (value != null) callback(value);
} }`. `Remove(out)` overload preserves remove-and-get atomicity.

**`bindWriterToReader`** → `public List<GoalRef> BindWriterToReader(int
writerAddr, int readerAddr)`. Body in order: validate writer cell tag
(throw if not WrtTag), validate reader cell tag (throw if not RoTag),
`int? targetWriterAddr = TryWriterForReader(readerAddr); if
(targetWriterAddr == null) throw new InvalidOperationException(...);`,
allocate `activations`, `if (writerCell.Content is WriterContent wc)
ForwardSuspensions(wc.Suspensions, targetWriterAddr.Value);`, mutate
`writerCell.Content = new Pointer(readerAddr);` (Tag REMAINS WrtTag —
NEVER set to ValueTag), callback relocation via `if (_bindCallbacks
.Remove(writerAddr, out var callback)) _bindCallbacks[
targetWriterAddr.Value] = callback;`, return activations.

**`bindWriterToWriter`** → `public void BindWriterToWriter(int w1, int
w2) => throw new InvalidOperationException($"WxW violation: cannot
bind writer {w1} to writer {w2}");`. Expression-bodied throw. Method
shape preserved (it's called by `BindVariable`).

**`suspendOnWriter`** → `public void SuspendOnWriter(int writerAddr,
SuspensionRecord record)`. Three-branch dispatch preserved:
WriterContent (cons via `node.Next = wc.Suspensions; wc.Suspensions =
node;` — ORDER MATTERS), Pointer (promote: `cell.Content = new
WriterContent(ptr.TargetAddr, node);`), else throw
`InvalidOperationException`.

**`suspendOnReader`** → `public void SuspendOnReader(int readerAddr,
SuspensionRecord record)`. Two-branch: `if (cell.Content is
VariableEntry entry) { var node = new SuspensionListNode(record);
node.Next = entry.Suspensions; entry.Suspensions = node; return; }`,
then `if (cell.Tag != CellTag.RoTag || cell.Content is not Pointer
ptr) throw new InvalidOperationException(...); SuspendOnWriter(ptr
.TargetAddr, record);`. Dart `is!` → C# `is not` (negated type
pattern, C# 9+).

**`_forwardSuspensions`** → `private void ForwardSuspensions(
SuspensionListNode? list, int targetWriterAddr)` (leading underscore
DROPPED for methods; private modifier is the canonical visibility
marker per .NET naming guideline). Body walks the list; for each armed
node creates `var newNode = new SuspensionListNode(current.Record);`
(SHARING the record by reference — CRITICAL for disarm propagation;
codegen MUST NOT clone the record itself); prepends to the target's
WriterContent (cons or promote-from-Pointer); silently no-ops on other
cases (bound or invalid target).

**`_walkAndActivate`** → `private static void WalkAndActivate(
SuspensionListNode? list, List<GoalRef> activations)`. Body: while
loop; for each armed node `activations.Add(new GoalRef(current.GoalId!
.Value, current.ResumePC)); current.Record.Disarm();` — `GoalId!
.Value` unwraps the nullable `int?` (safe by armed precondition).
`Record.Disarm()` mutates the shared record (propagates to every
wrapper pointing at the same record).

**`isFullyBound`** → `public bool IsFullyBound(int writerAddr) { var
result = DerefAddr(writerAddr); return result is not VarRef && result
is not VariableEntry; }`. `is not` is C# 9+ negated type pattern.

**`getValue`** → `public Term? GetValue(int writerAddr) { var result =
DerefAddr(writerAddr); if (result is VarRef || result is VariableEntry)
return null; return (Term)result; }`. Explicit cast `(Term)result` is
the .NET counterpart of Dart's throwing `as Term` (NOT `result as
Term` which returns null on mismatch).

**`dereference`** → `public Term Dereference(Term term) { if (term is
VarRef varRef) { var result = DerefAddr(varRef.Addr); if (result is
VariableEntry) return term; if (result is VarRef resultVar) return
resultVar; return (Term)result; } return term; }`. The "return
original `term` for imported unbound" branch is load-bearing —
preserves caller's VarRef handle.

**`onBind`** → `public void OnBind(int writerAddr, Action<Term>
callback) { if (IsFullyBound(writerAddr)) { var value = GetValue(
writerAddr); if (value != null) callback(value); return; }
_bindCallbacks[writerAddr] = callback; }`. Indexer-set replaces silently
(matches Dart `Map[k]=v`). NOT a C# `event` — single-subscriber-per-key.

**`removeBindCallback`** → `public void RemoveBindCallback(int
writerAddr) => _bindCallbacks.Remove(writerAddr);` (expression-bodied;
discard bool return).

**`bindImportedReader`** → `public List<GoalRef> BindImportedReader(
int readerAddr, Term value, VariableEntry entry)`. Body in this exact
order: validate cell.Tag is RoTag (throw), validate cell.Content is
VariableEntry (throw), allocate `activations`, `if (entry.Suspensions
!= null) WalkAndActivate(entry.Suspensions, activations);`, `int
valueCellAddr = Hp++; Cells.Add(new HeapCell(value, CellTag.ValueTag));
cell.Content = new Pointer(valueCellAddr);`, return activations. The
`Hp++` + `Cells.Add` MUST be co-located (HP-cells-length sync
invariant from source comment).

**Compatibility wrappers** → preserved as a set of one-line delegating
methods (codegen MUST emit each — they ARE part of the API surface):
`BindVariable(int, Term)` dispatches on `value is VarRef → IsReader
→ BindWriterToReader / IsWriter → BindWriterToWriter (throws) / else
BindWriter`; `BindVariableConst(int, object?) => BindWriter(...,
new ConstTerm(v));`; `BindVariableStruct(int, string, List<Term>) =>
BindWriter(..., new StructTerm(functor, args));`; plus
`BindWriterConst`/`BindWriterStruct`/`IsWriterBound`/`ValueOfWriter`/
`IsBound`. NOT marked `[Obsolete]` (source has no `@deprecated`).

**Reader abstraction** → four methods preserving doc-comment markdown
table verbatim as XML `<remarks>`: `IsReaderBound`, `GetReaderValue`,
`IsImportedReader`, `GetWriterForReader` (expression-bodied alias to
`TryWriterForReader`). The structural sentinel table (Unbound imported
= VariableEntry; Bound imported = Pointer→ValueTag; Local = Pointer→
WrtTag) is load-bearing reference doc — MUST preserve.

**Legacy wrappers** → `public SuspensionListNode? GetSuspensions(int)`
and `public void AddSuspension(int, SuspensionListNode)` preserved
with `/* Legacy: ... */` XML doc carry-forward.

**`storeTermOnHeap`** → `public int StoreTermOnHeap(Term term)`.
Recursive per-variant dispatch via pattern-match: `if (term is VarRef
varRef) return varRef.Addr;`, ConstTerm/MutualRefTerm/ModuleTerm each
allocate a single ValueTag cell, StructTerm recurses on `Args` building
`var heapArgs = new List<Term>();` then allocates a ValueTag cell
wrapping `new StructTerm(structTerm.Functor, heapArgs)`. Default arm
throws `new ArgumentException($"Unknown term type: {term.GetType()}");`
(Dart `ArgumentError` → .NET `ArgumentException`; `runtimeType` →
`GetType()`). The HP++/Cells.Add pair MUST be co-located in each arm.

## 3. Decomposed Task Units

- T1. Emit file-header XML doc on the namespace declaration mirroring
  `lib/runtime/` with the Dart library doc-comments carried forward; no
  `library;` directive in C#.
- T2. Emit `using` directives: `using <root>.Runtime;`, `using
  <root>.Multiagent;`, `using System.Collections.Generic;`, `using
  System;`. Do NOT synthesise a `VariableEntry` alias.
- T3. Emit `public enum CellTag { WrtTag, RoTag, ValueTag }` —
  declaration order preserved, default int underlying, SHOUTcase
  spellings verbatim, NOT `[Flags]`.
- T4. Emit `public class HeapCell` with `Content { get; set; }`
  (`object?`), `Tag { get; set; }` (`CellTag`), positional ctor,
  expression-bodied `HasValue` and `HasSuspensions` (pattern-match in
  the latter). Reference type only; reject record/struct.
- T5. Emit `public sealed class Pointer` with get-only `TargetAddr`,
  positional ctor, `ToString` override; no equality override; reject
  record/record-struct.
- T6. Emit `public sealed class WriterContent` with get-only
  `ReaderAddr`, mutable nullable `Suspensions { get; set; }`, ctor
  `(int readerAddr, SuspensionListNode? suspensions = null)`,
  `ToString` with explicit null-handling preserving Dart's "null"
  rendering.
- T7. Emit `public class HeapFCP` shell with three members: get-only
  `Cells` initialiser, mutable `Hp` (PascalCase, default 0),
  private-readonly `_bindCallbacks` Dictionary.
- T8. Emit `AllocateVariable` returning `(int WriterAddr, int
  ReaderAddr)`; writer-Add first, reader-Add second; `Hp += 2`.
- T9. Emit `AllocateImportedReader` / `AllocateImportedWriter` (block
  bodies; `Hp++` post-increment; `null` content).
- T10. Emit `IsWriter`/`IsReader`/`IsValue` as three expression-bodied
  bounds-checked tag predicates.
- T11. Emit `TryWriterForReader` with full XML-doc carry-forward of the
  ~90-line API contract; pattern-match `is Pointer ptr`.
- T12. Emit `ReaderForWriter` with three-case dispatch, preserving the
  bidirectional verification (target tag + target content + back-pointer
  equals writerAddr).
- T13. Emit `PairedReaderAddr` with `+ 1` allocation-invariant fallback.
- T14. Emit `DerefAddr(int) -> object`: `while (true)` + `HashSet<int>
  visited` + `CellTag? previousTag` + cycle/WxW throws + switch on Tag
  with three arms (RoTag/WrtTag/ValueTag) + inner pattern-matches on
  Content; map StateError → InvalidOperationException; preserve all
  three returns (VarRef / VariableEntry / Term-via-cast).
- T15. Emit `BindWriter` and `BindWriterNoCallback` as expression-bodied
  delegating wrappers to `BindWriterWithCallbackControl` with named
  `fireCallback:` argument.
- T16. Emit `BindWriterWithCallbackControl(int, Term, bool fireCallback)`
  with tag-validate throw, WriterContent-suspension walk, in-place
  Content+Tag mutation, optional `Dictionary.Remove(out var callback)`
  + invoke; return activations.
- T17. Emit `FirePendingCallback` using `Dictionary.Remove(out var
  callback)` + value-bound check.
- T18. Emit `BindWriterToReader` in source order: validate writer tag,
  validate reader tag, `TryWriterForReader` returning nullable (throw
  if null = imported-reader target), forward suspensions via
  `ForwardSuspensions`, mutate Content → Pointer (Tag REMAINS WrtTag),
  relocate callback via `Remove(out var)` + indexer-set, return
  activations.
- T19. Emit `BindWriterToWriter` as expression-bodied
  `InvalidOperationException` throw.
- T20. Emit `SuspendOnWriter` with three-branch (WriterContent cons /
  Pointer promote-to-WriterContent / else throw); preserve `node.Next
  = wc.Suspensions; wc.Suspensions = node;` order.
- T21. Emit `SuspendOnReader` with two-branch (VariableEntry mutate
  entry.Suspensions chain / Pointer delegate to `SuspendOnWriter`);
  `cell.Content is not Pointer ptr` negated type pattern.
- T22. Emit private `ForwardSuspensions(SuspensionListNode?, int)`
  with WRAPPER-clone but SHARED-record (`new SuspensionListNode(current
  .Record)`) and promote-on-bare-Pointer; silently no-op on bound
  targets. No leading underscore on the method name.
- T23. Emit private static `WalkAndActivate(SuspensionListNode?,
  List<GoalRef>)` with `new GoalRef(current.GoalId!.Value, current
  .ResumePC)` and `current.Record.Disarm()`.
- T24. Emit `IsFullyBound(int)` deref-and-`is not`-test.
- T25. Emit `GetValue(int)` deref-and-explicit-cast (`(Term)result`).
- T26. Emit `Dereference(Term)` with VarRef-chase and load-bearing
  return-original-on-VariableEntry branch.
- T27. Emit `OnBind(int, Action<Term>)` immediate-fire-if-bound +
  indexer-set register.
- T28. Emit `RemoveBindCallback(int)` expression-bodied
  `Dictionary.Remove`.
- T29. Emit `BindImportedReader(int, Term, VariableEntry)` with order:
  validate tag, validate Content is VariableEntry,
  `WalkAndActivate(entry.Suspensions, activations)` (guarded by null
  check), `Hp++` + `Cells.Add(...)` co-located, then `cell.Content =
  new Pointer(valueCellAddr);`, return activations.
- T30. Emit compatibility wrappers: `BindVariable(int, Term)` with
  VarRef-dispatch cascade; `BindVariableConst(int, object?)`;
  `BindVariableStruct(int, string, List<Term>)`; plus `BindWriterConst`,
  `BindWriterStruct`, `IsWriterBound`, `ValueOfWriter`, `IsBound` —
  each as a one-line delegate. NOT `[Obsolete]`.
- T31. Emit reader abstraction: `IsReaderBound`, `GetReaderValue` (use
  `(Term)targetCell.Content!` on ValueTag arm), `IsImportedReader`
  (with XML-doc markdown table preserved verbatim), `GetWriterForReader
  => TryWriterForReader(readerAddr);`.
- T32. Emit legacy wrappers `GetSuspensions(int)` and `AddSuspension(
  int, SuspensionListNode)` with `/* Legacy */` XML doc.
- T33. Emit `StoreTermOnHeap(Term)` with pattern-match per variant:
  VarRef no-op, ConstTerm/MutualRefTerm/ModuleTerm allocate ValueTag,
  StructTerm recurse-on-Args + new StructTerm with VarRef args + allocate
  ValueTag, default throw `ArgumentException` using `term.GetType()`.
  HP++/Cells.Add co-located in every allocating arm.
- T34. Compile-check the emitted unit with strict NRT on; verify the
  expected diagnostics list (zero warnings expected after the documented
  null-forgiving operators on `entry.BoundValue!.Value`, `targetCell
  .Content!`, and `GoalId!.Value`).
- T35. Sanity sweep: confirm NO occurrences of `lock`, `Monitor`,
  `SemaphoreSlim`, `Interlocked`, `ConcurrentDictionary`, or `volatile`
  anywhere in the emitted file — single-owning-context invariant
  (escalation #4 close, commit 497428c8). Confirm no `Task`/`async`/
  `IAsyncEnumerable` introduced (file is wholly synchronous in source).

## 4. Research Findings

None required. Every construct's `research_finding_id` in the convspec
is either an explicit FR-024 cache hit (already-cited authoritative
finding from sibling specs `suspension.dart.md` / `variable_table.dart.md`
/ `cells.dart.md` / `terms.dart.md` / `machine_state.dart.md` /
`goal_queue.dart.md` / `opcodes.dart.md`) or a Microsoft Learn citation
that is already woven into the convspec's `nuance` field verbatim. The
load-bearing citations referenced:

- Microsoft Learn — value-tuples (ValueTuple<int, int> as the .NET
  counterpart of Dart 3 records).
- Microsoft Learn — pattern matching (`is X x` type pattern with
  designation; `is not X` negated type pattern, C# 9+).
- Microsoft Learn — Dictionary<TKey, TValue>.Remove(TKey, TValue) out-
  overload (.NET Core 2.0+) for atomic remove-and-get.
- Microsoft Learn — System.Action<T> delegate (.NET counterpart of Dart
  `void Function(T)`).
- Microsoft Learn — InvalidOperationException (.NET counterpart of Dart
  `StateError` — both signal "object state inconsistent with operation").
- Microsoft Learn — ArgumentException (.NET counterpart of Dart
  `ArgumentError`).
- Microsoft Learn — HashSet<T> (Dart `Set<int>` literal `<int>{}` → .NET
  HashSet<int>).
- Microsoft Learn — capitalisation conventions (HP → Hp PascalCase per
  two-letter-acronym rule).
- Microsoft Learn — expression-bodied throw (C# 7+).
- Microsoft Learn — List<T> (stores `record struct` GoalRef elements
  unboxed).
- Dart official — records language reference (anonymous immutable
  aggregate types; `(int, int)` syntax).

All citations are already in the convspec. No WebSearch/WebFetch/Agent
calls made — none needed.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/lib/runtime/heap_fcp.dart.md`
(RATIFIED 2026-05-21, escalation #4 closed in commit 497428c8). Every
target_decision and nuance is carried forward verbatim. The
single-owning-context invariant (no lock/Interlocked/ConcurrentDictionary/
volatile in any HeapFCP method) is mirrored from the convspec's
post-escalation resolution block and from `CLAUDE.md`'s ban on
inventing concurrency primitives. Cross-file invariants (HeapCell as
reference type; Pointer as `sealed class` not record-struct due to
boxing in `object?` slot; WriterContent as reference type for shared
mutation; SuspensionRecord/SuspensionListNode/VariableEntry as
reference types per their own ratified convspecs; GoalRef as `readonly
record struct` per machine_state.dart.md; StructTerm.Args aliased not
copied per terms.dart.md) are all mirrored from sibling convspecs.
Naming policy (PascalCase methods/properties; SHOUTcase enum members
preserved verbatim per spec-string-fidelity precedent; private-field
underscore retained; private-method underscore dropped) is mirrored
from the convspec and from the cells.dart.md / opcodes.dart.md /
goal_queue.dart.md precedents. Hp-vs-HP naming decision is mirrored
from the convspec (PascalCase per .NET two-letter-acronym rule). No
inferences beyond the convspec text.

## 6. Escalations

None.
