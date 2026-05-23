---
path: test/heap/binding_pointer_test.dart
cycle_group_id: 127
scc_siblings: []
generated_at: 2026-05-21T16:05:00Z
source_sha256: 60cea5fbe3415839b21caf214b4f3ca09470e8fb038192fc05274f68924360e7
schema_version: 1
---

# Conversion Plan: test/heap/binding_pointer_test.dart

## 1. Source Analysis

Dart test file (395 lines) exercising heap-FCP binding operations under the
Pointer Architecture. Structure:

- `library;` directive (line 9) with preceding doc-comment block (lines
  1-8) citing spec `docs/heap-pointer-architecture-spec.md v3.0` and
  enumerating three scenario classes (bindWriter, bindWriterToReader,
  WxW violation).
- Four `package:` imports (lines 11-15): `package:test/test.dart` plus
  three runtime imports (`heap_fcp.dart`, `terms.dart`, `suspension.dart`,
  `machine_state.dart`).
- `void main() { ... }` entry point (line 17) containing exactly six flat
  sibling `group(...)` blocks (no nesting):
  1. `bindWriter - Ground Values` — 8 tests (integer, string, double,
     null, boolean, StructTerm, nested StructTerm, StructTerm with VarRef).
  2. `bindWriterToReader - Variable Chains` — 4 tests (basic-binding,
     chain-of-bindings, long-chain-deref, unbound-chain returns final
     writer VarRef including a second-arena `heap2` allocation).
  3. `WxW Violation Detection` — 2 tests (`bindWriterToWriter` throws,
     indirect WxW through deref).
  4. `Binding with Suspensions` — 3 tests (ground-value activates all,
     binding-to-variable forwards without activation, disarmed
     suspensions not activated).
  5. `Binding State Transitions` — 4 tests (unbound writer Pointer→reader,
     suspension WriterContent state, bound-to-variable Pointer content,
     bound-to-ground ValueTag + Term content).
  6. `isFullyBound and getValue` — 6 tests (unbound false, bound true,
     follows-chain, getValue null for unbound, getValue term, getValue
     chain).
- 27 `test(...)` calls total; ALL synchronous (no `async`/`await`/`Future`).
- ALL tests use method-local `final heap = HeapFCP();` (one test also
  introduces `final heap2 = HeapFCP();` for a second arena). NO `late`
  field, NO `setUp`, NO shared state.
- Constructs exercised: `HeapFCP()` ctor; `heap.allocateVariable()`
  returning record `(int, int)` destructured as `(w, _)`, `(_, r)`, or
  `(w, r)`; `ConstTerm(<lit>)` (int/string/double/null/bool); `StructTerm(<func>, <list>)`;
  `VarRef(<addr>)`; `Pointer(<addr>)` (direct content overwrite for the
  WxW corruption simulation); `SuspensionRecord(<goalId>, <resumePc>)`;
  `r1.disarm()` mutation; `heap.bindWriter`, `heap.bindWriterToReader`,
  `heap.bindWriterToWriter`, `heap.suspendOnWriter`, `heap.derefAddr`,
  `heap.isFullyBound`, `heap.getValue`; cell-state inspection via
  `heap.cells[i].tag` / `heap.cells[i].content` and direct mutation
  `heap.cells[w1].content = Pointer(w2)`; enum literals `CellTag.ValueTag`,
  `CellTag.WrtTag`; `as`-casts to `ConstTerm`/`StructTerm`/`VarRef`/
  `Pointer`/`WriterContent`; `value.args.length`, `activations.first.id`,
  `activations.map((a) => a.id).toSet()`; set literal `{1, 2, 3}`;
  matchers `equals`, `isA<T>`, `isNull`, `isTrue`, `isFalse`, `isEmpty`,
  `throwsStateError`.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the convspec one-for-one; target file
`test/heap/BindingPointerTest.cs`.

- `library;` → elide; doc-comment block above it becomes the
  `BindingPointerTests` class XML doc-comment (preserves "Tests for binding
  operations with Pointer Architecture Heap" lead and the
  `docs/heap-pointer-architecture-spec.md v3.0` provenance).
- `import 'package:test/test.dart';` → `using Xunit;` (project-pinned
  xUnit, per convspec rf-dart-package-test-import-to-xunit-using).
  Codegen also emits `using System;` (for `InvalidOperationException`),
  `using System.Collections.Generic;` (for `List<T>` / `HashSet<T>`),
  `using System.Linq;` (for `Select` / `First` / `ToHashSet` /
  `SetEquals`).
- Four `package:glp_runtime/runtime/...` imports → ONE
  `using <RootNs>.Runtime;` (collapsed; the namespace string is owned by
  the four SUT specs).
- Namespace declaration mirroring `test/heap` → `<RootNs>.Test.Heap`.
- `void main() { ... }` → omitted (xUnit reflects `[Fact]` methods; no
  per-file entrypoint).
- Six flat `group(...)` blocks → SINGLE PascalCase test class
  `BindingPointerTests` with all 27 methods; each method decorated with
  `[Trait("Group", "<original group label>")]` + `[Fact(DisplayName =
  "<original test label>")]`. Method names are group-prefixed PascalCased
  identifiers (e.g. `BindWriterGroundValues_BindToConstTermInteger`,
  `BindWriterToReaderVariableChains_BasicBindingCreatesPointer`,
  `WxwViolationDetection_BindWriterToWriterThrowsStateError`,
  `BindingWithSuspensions_BindingGroundValueActivatesAllSuspensions`,
  `BindingStateTransitions_UnboundWriterHasPointerToReader`,
  `IsFullyBoundAndGetValue_GetValueFollowsChain`).
- Each `test(label, () { ... })` (sync) → `public void <Name>()` method
  with `[Fact(DisplayName = "<label>")]` + `[Trait]`. Body translates
  statement-for-statement (arrange / act / assert). NO constructor: each
  method opens with its own `var heap = new HeapFcp();` (and
  `var heap2 = new HeapFcp();` where the source had a second arena).
- `final heap = HeapFCP();` (and all other `final` locals) → `var heap =
  new HeapFcp();` (method-local, single-assignment by construction).
- `final (writerAddr, _) = heap.allocateVariable();` → `var (writerAddr,
  _) = heap.AllocateVariable();` (C# 7+ tuple deconstruction with `_`
  discard). All deconstructed names typed as `long` because
  `AllocateVariable` returns `(long, long)` per heap_fcp.dart.md.
  Symmetric for `(_, r2)` and `(w1, r1)` patterns.
- `ConstTerm(<lit>)` → `new ConstTerm(<lit>)` (sealed class with single
  `object? Value`). Int / string / double / null / bool literals box
  transparently.
- `StructTerm(<func>, [<terms>])` → `new StructTerm(<func>, new List<Term>
  { <terms> })` (mutable List<T> assignable to `IReadOnlyList<Term>` SUT
  field). Nested-struct case: `new StructTerm("outer", new List<Term> {
  inner, new ConstTerm("y") })` preserves the local reference (no clone).
- `VarRef(r2)` → `new VarRef(r2)` (sealed class `: Term, IEquatable<VarRef>`).
- `Pointer(w2)` → `new Pointer(w2)` (plain reference wrapper around a
  single `long TargetAddr`).
- `SuspensionRecord(<id>, <pc>)` → `new SuspensionRecord(<id>L, <pc>L)`
  (plain class; reference identity preserved because `Disarm()` is
  observable). The L suffixes make the literal `long`.
- Heap mutator calls → PascalCase: `heap.BindWriter(...)`,
  `heap.BindWriterToReader(...)`, `heap.BindWriterToWriter(...)`,
  `heap.SuspendOnWriter(...)`. Return-value capture preserved:
  `var activations = heap.BindWriter(...);`, `var acts1 = heap.
  BindWriterToReader(...);`, `var acts2 = heap.BindWriter(...);`.
- Heap query calls → PascalCase: `heap.DerefAddr(r1)` returns `Term`;
  `heap.IsFullyBound(writerAddr)` returns `bool`; `heap.GetValue(writerAddr)`
  returns `Term?`.
- `r1.disarm()` → `r1.Disarm();` (observable mutation across heap's
  suspension list).
- `heap.cells[i].tag` / `heap.cells[i].content` → `heap.Cells[i].Tag` /
  `heap.Cells[i].Content` (writable property; `Cells` is `List<Cell>`).
  The direct mutation `heap.cells[w1].content = Pointer(w2);` →
  `heap.Cells[w1].Content = new Pointer(w2);` (compiles because Cell is
  a class, NOT a struct; `Content` is a writable property).
- `CellTag.ValueTag` / `CellTag.WrtTag` → identical identifiers in C#
  (casing preserved verbatim per cells.dart.md precedent).
- `(<expr> as <T>).<member>` → `((<T>)<expr>).<Member>` (explicit cast,
  NOT `expr as T` — Dart `as` throws on mismatch, `(T)expr` matches that
  failure mode). Members PascalCased: `.value` → `.Value`, `.functor` →
  `.Functor`, `.args` → `.Args`, `.addr` → `.Addr`, `.targetAddr` →
  `.TargetAddr`.
- `value.args.length` → `value.Args.Count` (IReadOnlyList<T>.Count).
- `activations.first.id` → `activations.First().Id` (LINQ; requires
  `using System.Linq;`).
- `activations.map((a) => a.id).toSet()` → `activations.Select(a => a.Id).
  ToHashSet()`.
- `{1, 2, 3}` → `new HashSet<long> { 1L, 2L, 3L }` (set literal; longs
  because `SuspensionRecord.GoalId` is `long?`).
- `expect(actual, equals(expected))` → `Assert.Equal(expected, actual)`
  (ARGUMENT-ORDER FLIP — well-known footgun).
- `expect(actual, isA<T>())` → `Assert.IsType<T>(actual)` (exact type
  match; safe here because every target is a sealed leaf).
- `expect(actual, isNull)` → `Assert.Null(actual)`.
- `expect(actual, isTrue)` → `Assert.True(actual)`. `isFalse` → `Assert.
  False(actual)`.
- `expect(acts1, isEmpty)` → `Assert.Empty(acts1)`.
- `expect(ids, equals({1, 2, 3}))` (set-equality case) → `Assert.True(
  ids.SetEquals(new[] { 1L, 2L, 3L }));` (preserves set-equality
  semantics; `Assert.Equal` on two `HashSet<T>` would degrade to
  sequence-equality).
- `expect(() => heap.bindWriterToWriter(w1, w2), throwsStateError);` →
  `Assert.Throws<InvalidOperationException>(() => heap.BindWriterToWriter(
  w1, w2));`. Same for `heap.DerefAddr(w1)` in the indirect-WxW case.

## 3. Decomposed Task Units

- T1: emit file-scope using directives (`using Xunit;`, `using System;`,
  `using System.Collections.Generic;`, `using System.Linq;`, `using
  <RootNs>.Runtime;`) — done.
- T2: emit namespace declaration `<RootNs>.Test.Heap { ... }` — done.
- T3: emit `BindingPointerTests` test class with XML doc-comment lifted
  from the Dart file's leading doc block — done.
- T4: emit 8 `[Fact]` methods for group `bindWriter - Ground Values`
  (BindToConstTermInteger / String / Double / Null / Boolean / StructTerm
  / NestedStructTerm / StructTermContainingVarRef), each with
  `[Trait("Group", "bindWriter - Ground Values")]` and `[Fact(DisplayName
  = "<label>")]` — done.
- T5: emit 4 `[Fact]` methods for group `bindWriterToReader - Variable
  Chains` (BasicBindingCreatesPointer / ChainOfBindings /
  LongChainDereferencesCorrectly / UnboundChainReturnsFinalWriterVarRef
  including the second-arena `var heap2 = new HeapFcp();`) — done.
- T6: emit 2 `[Fact]` methods for group `WxW Violation Detection`
  (BindWriterToWriterThrowsStateError /
  IndirectWxwThroughDerefDetected), each using
  `Assert.Throws<InvalidOperationException>` — done.
- T7: emit 3 `[Fact]` methods for group `Binding with Suspensions`
  (BindingGroundValueActivatesAllSuspensions /
  BindingToVariableForwardsSuspensionsWithoutActivation /
  DisarmedSuspensionsNotActivated) — done.
- T8: emit 4 `[Fact]` methods for group `Binding State Transitions`
  (UnboundWriterHasPointerToReader / WriterWithSuspensionHasWriterContent
  / WriterBoundToVariableHasPointerContent /
  WriterBoundToGroundHasValueTagAndTermContent) — done.
- T9: emit 6 `[Fact]` methods for group `isFullyBound and getValue`
  (IsFullyBoundFalseForUnbound / IsFullyBoundTrueForBoundToGround /
  IsFullyBoundFollowsChain / GetValueReturnsNullForUnbound /
  GetValueReturnsTermForBound / GetValueFollowsChain) — done.
- T10: in every method, emit the per-test arena `var heap = new HeapFcp();`
  (plus `var heap2 = new HeapFcp();` in T5's last method) as method-local
  `var`, never an instance field — done.
- T11: emit tuple-deconstruction locals
  (`var (writerAddr, _) = heap.AllocateVariable();`, `var (_, r2) = ...;`,
  `var (w1, r1) = ...;`) with `long` typing throughout — done.
- T12: emit all heap mutator/query calls in PascalCase (`BindWriter`,
  `BindWriterToReader`, `BindWriterToWriter`, `SuspendOnWriter`,
  `DerefAddr`, `IsFullyBound`, `GetValue`, `AllocateVariable`) and
  preserve return-value capture for activation-list returns — done.
- T13: emit all term constructors with `new` and PascalCased member
  access on downcasts (`((ConstTerm)x).Value`, `((StructTerm)x).Functor`,
  `.Args.Count`, `((VarRef)x).Addr`, `((Pointer)x).TargetAddr`) — done.
- T14: emit the direct cell-content mutation `heap.Cells[w1].Content =
  new Pointer(w2);` in the indirect-WxW test — done.
- T15: translate every matcher assertion per convspec (argument-order
  flip for `Assert.Equal`; `Assert.IsType<T>` for `isA<T>`; `Assert.Null`
  / `Assert.True` / `Assert.False` / `Assert.Empty`; LINQ `Select`/`First`/
  `ToHashSet`/`SetEquals` for set-equality case;
  `Assert.Throws<InvalidOperationException>` for `throwsStateError`) —
  done.

## 4. Research Findings

none required — every construct reuses an idiom_id-equivalent rf-*
already pinned by precedent specs (heap_fcp.dart.md, cells.dart.md,
terms.dart.md, suspension.dart.md, boot_loader_test.dart.md,
smoke_test.dart.md, localize_test.dart.md). FR-024 cache hits across the
board; no re-research.

## 5. Consistency Pass

fixed — derived from convspec `.codeconv/conversion-specs/test/heap/
binding_pointer_test.dart.md` (schema_version 1, ratified). Every
construct decision in §2 mirrors a `target_decision` from the convspec
verbatim; every Task in §3 maps to a `conversion_units` entry (cu-1
through cu-11). No drift from cited rf-* idioms. xUnit pinning carries
over from boot_loader_test (rf-dart-package-test-import-to-xunit-using).
The two in-file justified choices — `Assert.IsType<T>` over
`Assert.IsAssignableFrom<T>` (justified by sealed leaves) and `SetEquals`
over `Assert.Equal` on two HashSets (justified by Dart `Set` equality
semantics) — are recorded in the convspec's research findings and
reproduced here without modification.

## 6. Escalations

None.
