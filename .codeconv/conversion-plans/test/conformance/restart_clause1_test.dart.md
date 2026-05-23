---
path: test/conformance/restart_clause1_test.dart
cycle_group_id: 120
scc_siblings: []
generated_at: 2026-05-21T16:25:00Z
source_sha256: 15baa98d14a52a37cc739a1867d3ad3a7c68c3c6c5c10ee75ad9ce59b23ac517
schema_version: 1
---

# Conversion Plan: test/conformance/restart_clause1_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/conformance/restart_clause1_test.dart` (37 lines, sha256 `15baa98d...23ac517`) yields the following Dart surface:

- **Line 1**: `import 'package:test/test.dart';` — Dart test framework import.
- **Lines 2–7**: six internal-package imports under `package:glp_runtime/runtime/`: `runtime.dart`, `machine_state.dart`, `suspend_ops.dart`, `heap_fcp.dart`, `commit.dart`, `terms.dart`.
- **Line 9**: `void main()` — Dart test-registration entrypoint.
- **Line 10**: single `test('On wake, activation pc equals kappa (restart at clause 1)', () { ... })` registration.
- **Line 11**: `final rt = GlpRuntime();` — final-typed local with constructor invocation, optional-`new` omitted.
- **Line 12**: `final heap = rt.heap as HeapFCP;` — final-typed local with Dart `as` downcast (throws on mismatch).
- **Line 14**: `const GoalId g = 77;` — typedef-typed const local with integer literal.
- **Line 15**: `const Pc kappa = 1;` — typedef-typed const local with integer literal.
- **Line 18**: `final (writerAddr, readerAddr) = heap.allocateVariable();` — record-positional destructuring of a two-int return.
- **Lines 21–26**: `SuspendOps.suspendGoalFCP(heap: heap, goalId: g, kappa: kappa, readerVarIds: {readerAddr});` — static-method call with four all-named arguments; the fourth arg is a single-element Set literal `{readerAddr}`.
- **Line 30**: `final sigmaHat = {writerAddr: ConstTerm('ground')};` — final-typed local with single-pair Map literal containing a `ConstTerm` constructor call on a single-quoted string `'ground'`.
- **Line 31**: `final acts = CommitOps.applySigmaHatFCP(heap: heap, sigmaHat: sigmaHat);` — static-method call with two named arguments returning an activation collection.
- **Line 33**: `expect(acts, hasLength(1));` — `package:test`/`matcher` length assertion (N=1).
- **Line 34**: `expect(acts.first.id, g);` — bare-value `expect` with `.first.id` chained property access.
- **Line 35**: `expect(acts.first.pc, kappa);` — bare-value `expect` with `.first.pc` chained property access.

No `async`/`Future`/`Stream`/isolate/`Completer`/`Timer` surface. No `late`/`mixin`/`extension`/sealed/abstract/null-safety/bitwise/shift constructs. No `setUp`/`tearDown`/`group`. Single `test()` registration, synchronous closure body, void return.

## 2. Dart → C#/.NET Conversion Plan

Each construct mirrors the ratified convspec row-by-row:

- **`import 'package:test/test.dart';`** → drop directive; emit file-level `using Xunit;`. KB-reuse `rf-dart-package-test-to-dotnet-xunit` from `smoke_test.dart` and `fairness_26_test.dart` siblings.
- **Six `import 'package:glp_runtime/runtime/*.dart';`** → collapse into ONE file-level `using <RootNs>.Runtime;`. All six lift into the same Runtime sub-namespace per their SUT specs; C# `using` is per-namespace, not per-file. KB-reuse `rf-dart-internal-package-import-to-csharp-using`.
- **`void main() { test('On wake, ...', () { ... }); }`** → drop `main()`; lift the one `test(...)` into `[Fact(DisplayName = "On wake, activation pc equals kappa (restart at clause 1)")] public void OnWakeActivationPcEqualsKappaRestartAtClause1() { ... }` on `public class RestartClause1Test`. PascalCased identifier strips punctuation; `DisplayName` preserves the original sentence verbatim. KB-reuse `rf-dart-test-main-to-xunit-class-with-facts`.
- **`final rt = GlpRuntime();`** → `var rt = new GlpRuntime();`. Dart `final` local ⇒ C# `var` (no method-local `readonly` in C#; `const` requires compile-time-constant initializer); explicit `new` required in C#. KB-reuse `rf-dart-final-local-to-csharp-var`.
- **`final heap = rt.heap as HeapFCP;`** → `var heap = (HeapFCP)rt.Heap;`. FILE-NEW load-bearing: Dart `as T` throws; C# `as T` returns `null` on mismatch; the semantic match for Dart's throwing `as` is the C# EXPLICIT-CAST `(T)x` which throws `InvalidCastException` on mismatch. Property `.heap` PascalCases to `.Heap` per `runtime.dart.md` SUT spec. Idiom `rf-dart-as-cast-to-csharp-explicit-cast`.
- **`const GoalId g = 77;`** → `const GoalId g = 77;` (or `77L` if the SUT-decided width is `long`). C# `const` IS supported on method locals and accepts integer-literal initializers. KB-reuse `rf-dart-const-local-typed-int-to-csharp-const`.
- **`const Pc kappa = 1;`** → `const Pc kappa = 1;` (or `1L`). Same shape as `GoalId` line; tracked as a separate construct row because `Pc` and `GoalId` MAY decode to different widths per the machine_state SUT spec.
- **`final (writerAddr, readerAddr) = heap.allocateVariable();`** → `var (writerAddr, readerAddr) = heap.AllocateVariable();`. Dart record-positional destructuring ⇒ C# tuple-deconstruction with `var`. Both names typed `long` per `cells.dart.md`'s `rf-dart-int-to-csharp-long-width`. Method name PascalCases per SUT spec. KB-reuse `rf-dart-record-return-to-csharp-valuetuple`.
- **`{readerAddr}`** (Dart Set literal at named-arg position) → `new HashSet<long> { readerAddr }` (universally supported); alternative `[readerAddr]` collection-expression form on C# 12+/.NET 8+. FILE-NEW; Dart `{e}` (one expression, no `:`) unambiguously disambiguates to Set. Idiom `rf-dart-set-literal-to-csharp-hashset-or-collection-expr`.
- **`SuspendOps.suspendGoalFCP(heap: heap, goalId: g, kappa: kappa, readerVarIds: {readerAddr});`** → `SuspendOps.SuspendGoalFcp(heap: heap, goalId: g, kappa: kappa, readerVarIds: new HashSet<long> { readerAddr });`. Named-arg surface is 1-to-1 between Dart and C#; method name PascalCases per SUT spec (`FCP` ⇒ `Fcp` per .NET Framework Design Guidelines for 3+-letter acronyms — SUT spec is authoritative). KB-reuse `rf-dart-named-args-call-to-csharp-named-args`.
- **`{writerAddr: ConstTerm('ground')}`** (Dart single-pair Map literal) → `new Dictionary<long, Term> { { writerAddr, new ConstTerm("ground") } }`. FILE-NEW; key type `long` from `writerAddr`, value type `Term` (supertype owned by `terms.dart.md` and pinned by `commit.dart.md`'s `applySigmaHatFCP` parameter type). Dart single-quoted ⇒ C# double-quoted. Idiom `rf-dart-map-literal-to-csharp-dictionary`.
- **`ConstTerm('ground')`** → `new ConstTerm("ground")`. Dart optional-`new` omitted ⇒ C# requires `new`; single-quoted Dart string ⇒ double-quoted C# string (C# single quotes are for `char`). KB-reuse `rf-dart-const-term-constructor-call-to-csharp-new`.
- **`final acts = CommitOps.applySigmaHatFCP(heap: heap, sigmaHat: sigmaHat);`** → `var acts = CommitOps.ApplySigmaHatFcp(heap: heap, sigmaHat: sigmaHat);`. Same named-arg parity; return-type owned by `commit.dart.md` SUT spec. KB-reuse `rf-dart-named-args-call-to-csharp-named-args`.
- **`expect(acts, hasLength(1));`** → `Assert.Single(acts);`. For `N==1` the most specific and most diagnostic xUnit assertion; general-N fallback would be `Assert.Equal(N, acts.Count);`. Idiom `rf-dart-expect-hasLength-to-xunit-assert-single-or-count`.
- **`expect(acts.first.id, g);`** → `Assert.Equal(g, acts.First().Id);`. EXPECTED-FIRST argument-order swap; Dart `.first` getter ⇒ LINQ `.First()` extension; property `.id` ⇒ `.Id` PascalCase per SUT spec. KB-reuse `rf-dart-expect-bare-value-int-to-xunit-assert-equal`.
- **`expect(acts.first.pc, kappa);`** → `Assert.Equal(kappa, acts.First().Pc);`. Same shape; property `.pc` ⇒ `.Pc` PascalCase per SUT spec.

## 3. Decomposed Task Units

- T1: Emit `using Xunit;` file-level directive — done.
- T2: Emit single collapsed `using <RootNs>.Runtime;` file-level directive — done.
- T3: Emit `public class RestartClause1Test` declaration with file name mirroring `restart_clause1_test.dart` ⇒ `RestartClause1Test.cs` — done.
- T4: Emit `[Fact(DisplayName = "On wake, activation pc equals kappa (restart at clause 1)")] public void OnWakeActivationPcEqualsKappaRestartAtClause1()` method — done.
- T5: Emit method body line 1 `var rt = new GlpRuntime();` — done.
- T6: Emit method body line 2 `var heap = (HeapFCP)rt.Heap;` (explicit-cast, NOT C# `as`) — done.
- T7: Emit method body line 3 `const GoalId g = 77;` (width per machine_state SUT spec) — done.
- T8: Emit method body line 4 `const Pc kappa = 1;` (width per machine_state SUT spec) — done.
- T9: Emit method body line 5 `var (writerAddr, readerAddr) = heap.AllocateVariable();` with both names typed `long` — done.
- T10: Emit method body line 6 `SuspendOps.SuspendGoalFcp(heap: heap, goalId: g, kappa: kappa, readerVarIds: new HashSet<long> { readerAddr });` — done.
- T11: Emit method body line 7 `var sigmaHat = new Dictionary<long, Term> { { writerAddr, new ConstTerm("ground") } };` — done.
- T12: Emit method body line 8 `var acts = CommitOps.ApplySigmaHatFcp(heap: heap, sigmaHat: sigmaHat);` — done.
- T13: Emit method body line 9 `Assert.Single(acts);` (N==1 specialization of `hasLength`) — done.
- T14: Emit method body line 10 `Assert.Equal(g, acts.First().Id);` (EXPECTED-FIRST, LINQ `.First()`, PascalCased `.Id`) — done.
- T15: Emit method body line 11 `Assert.Equal(kappa, acts.First().Pc);` (EXPECTED-FIRST, LINQ `.First()`, PascalCased `.Pc`) — done.
- T16: Drop Dart `void main()` entirely (xUnit discovery is attribute-driven) — done.

## 4. Research Findings

None required. Every construct decision is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/test/conformance/restart_clause1_test.dart.md`, which itself cites authoritative Dart and Microsoft Learn / xunit.net references for each FILE-NEW idiom (`as`-cast `(T)x` vs C# `as`, Set-literal `HashSet<T>`, Map-literal `Dictionary<K,V>`, `hasLength` ⇒ `Assert.Single`) and reuses prior batch-KB findings for the rest (`package:test` ⇒ xUnit, internal-package-import collapse, `void main()` ⇒ `[Fact]` class, `final` ⇒ `var`, `const` typedef-typed locals, record-destructuring ⇒ tuple-deconstruction, named-arg parity, `ConstTerm` constructor, bare-value `expect` ⇒ `Assert.Equal` EXPECTED-FIRST).

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/conformance/restart_clause1_test.dart.md`. All §2 emissions mirror the convspec's `target_decision` rows verbatim; all §3 task units correspond 1-to-1 with the convspec's `conversion_units` list (15 emission rows + 1 explicit drop-of-`main` row = 16 tasks). The convspec records zero open escalations (`escalations: []`), and every construct cites either a KB-reused finding (FR-012 / SC-007) or a FILE-NEW finding with authoritative Dart + Microsoft Learn / xunit.net references. SUT-owned decisions (PascalCasing of method/property names, `GoalId`/`Pc` typedef widths, `HeapFCP` casing, return-collection type of `ApplySigmaHatFcp`, `Term` supertype) are deferred to the cited SUT specs per the convspec's explicit ownership annotations and are not re-decided here.

## 6. Escalations

None.
