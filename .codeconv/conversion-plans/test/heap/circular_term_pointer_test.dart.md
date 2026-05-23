---
path: test/heap/circular_term_pointer_test.dart
cycle_group_id: 128
scc_siblings: []
generated_at: 2026-05-21T16:30:14Z
source_sha256: b239cd5fda24cb63efa4f4406ee8b94ad54679a79761f06f188268f7cbd60dff
schema_version: 1
---

# Conversion Plan: test/heap/circular_term_pointer_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/heap/circular_term_pointer_test.dart` (235 lines, sha256 `b239cd5f...d60dff`):

- File-level doc-comment (lines 1–9): description of circular-term handling, provenance `Adapted from: test/circular_term_test.dart`, citation `For spec: docs/heap-pointer-architecture-spec.md v3.0`.
- Library directive (line 10): `library;` (no name).
- Imports (lines 12–17): `package:test/test.dart`; five `package:glp_runtime/runtime/*` imports — `runtime.dart`, `terms.dart`, `heap_fcp.dart`, `system_predicates.dart`, `system_predicates_impl.dart`.
- Entrypoint (line 19): `void main()`.
- Single outer group (line 20): `'Circular Term Handling - Pointer Architecture'`.
- Hoisted runtime field (line 21): `late GlpRuntime rt;`.
- `setUp` (lines 23–25): `rt = GlpRuntime();`.
- Five inner groups, NINE total tests:
  - "Ground Guard with Circular Terms" (2 tests, lines 27–68).
  - "Equality (=?=) with Circular Terms" (2 tests, lines 70–110).
  - "Deep Copy with Circular Terms" (2 tests, lines 112–178).
  - "Term Formatter with Circular Terms" (1 test, lines 180–194).
  - "Dereferencing Circular Terms" (2 tests, lines 196–233).
- Core SUT surfaces exercised: `rt.heap.allocateVariable()` (returns record), `VarRef(int)`, `StructTerm(functor, List<Term>)`, `ConstTerm(value)`, `rt.heap.bindWriter(addr, term)`, `rt.heap.getValue(addr)`, `rt.heap.derefAddr(addr)`, `rt.heap.isFullyBound(addr)`, `SystemCall(name, args)`, top-level `copyTermPredicate(rt, call)`, `SystemResult.success`.
- Local-var keyword collision (line 45): `final struct = value as StructTerm;` — `struct` is a C# keyword.
- Matchers exercised: `isA<T>()`, `equals(...)`, `isNot(equals(...))`, `isTrue`, `isFalse`, `returnsNormally`.
- All NINE tests are synchronous (no `async`/`await`).

## 2. Dart → C#/.NET Conversion Plan

Mirroring the 24 ratified convspec constructs:

1. `library;` (line 10) → elided entirely; file-level doc-comment (lines 1–9) preserved as XML doc-comment on the test class `CircularTermPointerTests`.
2. `import 'package:test/test.dart';` (line 12) → `using Xunit;` at file scope; codegen also adds `using System;` (for `Action` lambdas where needed) and projects the file under namespace `<RootNs>.Test.Heap`.
3. Five `package:glp_runtime/runtime/...` imports (lines 13–17) → collapse to ONE `using <RootNs>.Runtime;`. The test assembly references the SUT assembly via its `.csproj` (out of scope here).
4. `void main()` (line 19) → eliminated; xUnit auto-discovers `[Fact]` methods.
5. Outer `group('Circular Term Handling - Pointer Architecture', () { ... })` (line 20) + `late GlpRuntime rt;` (line 21) + `setUp(() { rt = GlpRuntime(); });` (lines 23–25) + five inner groups → ONE flat xUnit class `CircularTermPointerTests` with `private readonly GlpRuntime _rt;` and `public CircularTermPointerTests() { _rt = new GlpRuntime(); }`. Each inner group label is preserved as `[Trait("Group", "<label>")]` on every test method of that group.
6. Each `test('<label>', () { ... })` (9 occurrences) → `public void <MethodName>()` decorated with `[Fact(DisplayName = "<original label>")]` plus the inner-group `[Trait]`. Method names are inner-group-prefixed PascalCased identifier-safe forms (e.g. `GroundGuardWithCircularTerms_CircularTermWithoutUnboundVariablesIsGround`).
7. `late GlpRuntime rt; setUp(() { rt = GlpRuntime(); });` → `private readonly GlpRuntime _rt;` field + constructor body `_rt = new GlpRuntime();`. xUnit's per-test class instantiation preserves the per-test isolation contract of Dart `setUp`. Codegen MUST NOT promote `_rt` to `static`.
8. Local declarations:
   - `final (writerAddr, readerAddr) = rt.heap.allocateVariable();` → `var (writerAddr, readerAddr) = _rt.Heap.AllocateVariable();` with both names typed `long` (per `heap_fcp.dart.md` idiom `rf-dart-record-return-to-csharp-valuetuple`).
   - `final (xWriter, xReader) = rt.heap.allocateVariable();` / `final (yWriter, yReader) = rt.heap.allocateVariable();` → analogous.
   - `final (copyWriter, _) = rt.heap.allocateVariable();` → `var (copyWriter, _) = _rt.Heap.AllocateVariable();` (C# 7+ discard `_`).
   - `final value = rt.heap.getValue(writerAddr);` → `var value = _rt.Heap.GetValue(writerAddr);`.
   - `final struct = value as StructTerm;` → keyword rename: `var structTerm = (StructTerm)value;`.
   - Same rename strategy applies for any later `as StructTerm` assigned to a local that would clash with C# keywords; otherwise no rename.
   - All other `final X = ...` map to `var X = ...`.
9. `ConstTerm('a')`, `ConstTerm('b')` → `new ConstTerm("a")`, `new ConstTerm("b")` (per `terms.dart.md` `sealed class ConstTerm : Term { object? Value; }`).
10. `StructTerm('f', [VarRef(...)])` (and the seven other forms in lines 35, 57, 76, 98, 116, 153, 184, 201, 218, 219) → `new StructTerm("f", new List<Term> { new VarRef(...) })` (per `terms.dart.md` `sealed class StructTerm : Term { string Functor; IReadOnlyList<Term> Args; }`).
11. `VarRef(<addr>)` → `new VarRef(<addr>)` (per `terms.dart.md` `sealed class VarRef : Term, IEquatable<VarRef> { long Addr; }`). Reference-identity preserved; cycle lives in the heap cell graph, not the C# object graph.
12. `SystemCall('copy_term', [VarRef(xReader), VarRef(copyWriter)])` (lines 126, 160) → `new SystemCall("copy_term", new List<object?> { new VarRef(xReader), new VarRef(copyWriter) })` (per `system_predicates.dart.md`: `SystemCall.Args` is `IReadOnlyList<object?>`; explicit `object?` element typing required in C#).
13. `copyTermPredicate(rt, call)` (lines 132, 166) → `SystemPredicates.CopyTermPredicate(_rt, call)` (top-level free function → static method on host class per `system_predicates_impl.dart.md`). Return type `SystemResult` (PascalCase enum: `Success | Failure | Suspend`).
14. `rt.heap.allocateVariable()` → `_rt.Heap.AllocateVariable()`. `rt.heap` is a public field on `GlpRuntime` in Dart; C# maps to public property `Heap { get; }` (per `runtime.dart.md`).
15. `rt.heap.bindWriter(<writer>, <term>)` (7 occurrences) → `_rt.Heap.BindWriter(<writer>, <term>);` as a statement. The `List<SuspensionRecord>` return is discarded (no capture); codegen MUST NOT introduce a capture variable and MUST NOT emit `_ = ...` (CA1806 not in scope here).
16. `rt.heap.getValue(<addr>)` → `_rt.Heap.GetValue(<addr>)` returning `Term?`.
17. `rt.heap.derefAddr(<addr>)` → `_rt.Heap.DerefAddr(<addr>)` returning `Term`. Cycle-safety relies on the SUT-pinned visited-set parameter in `heap_fcp.dart.md` (`dart.deref_addr.large_switch_with_visited_cycle_detection_wxw_violation_and_three_tag_cases`).
18. `rt.heap.isFullyBound(<addr>)` → `_rt.Heap.IsFullyBound(<addr>)` returning `bool`.
19. `<expr> as StructTerm` / `<expr> as ConstTerm` → `(StructTerm)<expr>` / `(ConstTerm)<expr>` — explicit cast (throws `InvalidCastException` on mismatch, matching Dart `as`'s `TypeError`). NOT `expr as T` (C# `as` yields `null` on mismatch — wrong semantics).
20. `.functor` → `.Functor`; `.args` → `.Args`; `.value` → `.Value`; `.args.length` → `.Args.Count` (per `terms.dart.md` casing rules; `IReadOnlyList<T>.Count`).
21. `struct.args[0]` / `copyStruct.args[0]` → `structTerm.Args[0]` / `copyStruct.Args[0]` — identical indexer syntax (`int` index in both languages).
22. `value.toString()` (line 192, inside the `returnsNormally` lambda) → `value.ToString();` as a statement. Cycle-safe `ToString` is load-bearing for the test "circular term does not cause infinite loop in toString" — per `terms.dart.md` the Term-leaf `ToString` overrides must inherit cycle-safety.
23. Matchers:
    - `expect(<actual>, isA<T>())` (10 occurrences) → `Assert.IsType<T>(<actual>);` (every target is a sealed concrete Term leaf, so exact-type matches subtype-tolerant Dart semantics).
    - `expect(<actual>, equals(<expected>))` (9 occurrences) → `Assert.Equal(<expected>, <actual>);` — ARGUMENT-ORDER FLIPPED. Enum-equality flip: `equals(SystemResult.success)` → `Assert.Equal(SystemResult.Success, result)`.
    - `expect(xValue.functor, isNot(equals(yValue.functor)));` (1 occurrence, line 108) → `Assert.NotEqual(((StructTerm)yValue).Functor, ((StructTerm)xValue).Functor);` — argument-order flipped.
    - `expect(<bool>, isTrue)` / `expect(<bool>, isFalse)` → `Assert.True(<bool>);` / `Assert.False(<bool>);` (1 of each).
    - `expect(() => value.toString(), returnsNormally);` (1 occurrence, line 192) → bare call `value.ToString();` as a statement (xUnit fails on any uncaught exception; positive matcher needs no wrapper).
24. Keyword-collision rename: the single Dart local `struct` (line 45) is renamed to `structTerm` in the C# port; subsequent member accesses use `structTerm.Functor`, `structTerm.Args[0]`. Other `as StructTerm` casts that bind to non-keyword names (`xValue`, `yValue`, `copyValue`, `resultX`, `resultY`) need no rename.

## 3. Decomposed Task Units

- T1: file-scope `using` directives (Xunit + System + `<RootNs>.Runtime`) — one-line done.
- T2: `namespace <RootNs>.Test.Heap` declaration mirroring `test/heap/` — one-line done.
- T3: emit test class `CircularTermPointerTests` with file-level + outer-group XML doc-comments preserved — one-line done.
- T4: emit `private readonly GlpRuntime _rt;` field and constructor `public CircularTermPointerTests() { _rt = new GlpRuntime(); }` (replaces Dart `late GlpRuntime rt; setUp(...)`) — one-line done.
- T5: emit 2 `[Fact]` methods in the "Ground Guard with Circular Terms" group (`GroundGuardWithCircularTerms_CircularTermWithoutUnboundVariablesIsGround`, `GroundGuardWithCircularTerms_CircularTermWithUnboundVariableInsideIsNotGround`) each with `[Fact(DisplayName = "<label>")]` + `[Trait("Group", "Ground Guard with Circular Terms")]` — one-line done.
- T6: emit 2 `[Fact]` methods in the "Equality (=?=) with Circular Terms" group (`EqualityWithCircularTerms_IdenticalCircularTermsAreEqual`, `EqualityWithCircularTerms_DifferentCircularTermsAreNotEqual`) — one-line done.
- T7: emit 2 `[Fact]` methods in the "Deep Copy with Circular Terms" group (`DeepCopyWithCircularTerms_CopyOfCircularTermPreservesStructure`, `DeepCopyWithCircularTerms_CopyOfAcyclicTermCreatesIndependentCopy`), each with the `SystemCall` + `SystemPredicates.CopyTermPredicate(...)` two-step — one-line done.
- T8: emit 1 `[Fact]` method `TermFormatterWithCircularTerms_CircularTermDoesNotCauseInfiniteLoopInToString` using the BARE-CALL shape for `returnsNormally` — one-line done.
- T9: emit 2 `[Fact]` methods in the "Dereferencing Circular Terms" group (`DereferencingCircularTerms_DereferenceThroughCircularStructureTerminates`, `DereferencingCircularTerms_NestedCircularReferencesWorkCorrectly`) — one-line done.
- T10: apply keyword-collision rename `struct` → `structTerm` at the single Dart line-45 site (and dependent member accesses two lines below) — one-line done.
- T11: apply `Assert.Equal` / `Assert.NotEqual` argument-order flip at all 10 sites (9 `equals` + 1 `isNot(equals(...))`) — one-line done.
- T12: emit member-casing renames (`.functor`→`.Functor`, `.args`→`.Args`, `.value`→`.Value`, `.args.length`→`.Args.Count`) across all test bodies — one-line done.
- T13: emit `(StructTerm)expr` / `(ConstTerm)expr` explicit casts (NOT `expr as T`) at every Dart `as`-cast site — one-line done.
- T14: emit `_rt.Heap.<Method>(...)` for every `rt.heap.<method>(...)` (PascalCase property + PascalCase method) — one-line done.
- T15: emit `SystemPredicates.CopyTermPredicate(_rt, call)` for every `copyTermPredicate(rt, call)` site — one-line done.
- T16: emit `new SystemCall("copy_term", new List<object?> { ... })` with explicit `object?` element typing at every `SystemCall(...)` construction site — one-line done.

## 4. Research Findings

none required — every construct decision is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/test/heap/circular_term_pointer_test.dart.md`, the SUT specs cited therein (`terms.dart.md`, `heap_fcp.dart.md`, `runtime.dart.md`, `cells.dart.md`, `system_predicates.dart.md`, `system_predicates_impl.dart.md`), and precedent test convspecs (`binding_pointer_test.dart.md`, `varref_pointer_test.dart.md`, `suspension_pointer_test.dart.md`, `partial_evaluator_test.dart.md`, `boot_loader_test.dart.md`).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/heap/circular_term_pointer_test.dart.md` (24 ratified constructs, `escalations: []`). Cross-checks performed:

- Construct coverage: every Dart construct in §1 (source analysis) maps to a §2 conversion decision and a §3 task unit.
- xUnit framework choice: consistent with project-wide policy pinned by `rf-dart-package-test-import-to-xunit-using` and reused across all `package:test` heap-test files.
- Address width: `long` for all addresses (consistent with `cells.dart.md` `dart.int.fixed_width_identity_field` / `rf-dart-int-to-csharp-long-width`).
- Member casing: PascalCase across all SUT-derived members (consistent with `terms.dart.md`, `heap_fcp.dart.md`, `runtime.dart.md`).
- `Assert.Equal` argument order: flipped at every site (consistent with `rf-dart-expect-equals-to-xunit-assert-equal-argorder`).
- Keyword-collision rename: `struct` → `structTerm` at exactly one site (line 45), no other identifier in this file collides with C# reserved keywords.
- Cycle-safety: relies on SUT-pinned visited-set semantics in `HeapFcp.DerefAddr` / `HeapFcp.GetValue` / `Term.ToString` / `SystemPredicates.CopyTermPredicate` — no escalation needed because all four SUT specs already pin the visited-set contract.

## 6. Escalations

None.
