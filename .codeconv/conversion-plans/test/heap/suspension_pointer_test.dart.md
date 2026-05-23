---
path: test/heap/suspension_pointer_test.dart
cycle_group_id: 129
scc_siblings: []
generated_at: 2026-05-21T16:25:15Z
source_sha256: 8f0020be2a925a63f498abf316c3e5ea71c60b9a6e5ef23bbffbc1615d4b2a95
schema_version: 1
---

# Conversion Plan: test/heap/suspension_pointer_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/heap/suspension_pointer_test.dart` (sha256 8f0020be…, 242 lines):

- File lead doc-comment (lines 1-10): cites `docs/heap-pointer-architecture-spec.md v3.0`; documents three load-bearing API facts — (a) "Suspensions now live on WRITER cells (not reader cells)", (b) `suspendOnWriter`/`suspendOnReader` API, (c) `bindWriter` replaces `bindVariable`.
- `library;` directive (line 10).
- Five imports (lines 12-17):
  - `package:test/test.dart`
  - `package:glp_runtime/runtime/runtime.dart`
  - `package:glp_runtime/runtime/machine_state.dart`
  - `package:glp_runtime/runtime/suspension.dart`
  - `package:glp_runtime/runtime/heap_fcp.dart` with `show HeapFCP, Pointer, SuspensionListNode, WriterContent`
  - `package:glp_runtime/runtime/terms.dart`
- `void main()` entrypoint (line 19) containing three sibling top-level `group(...)` calls — no `setUp`, no `tearDown`, no `late` field.
- Group 1 — `'Suspension and Reactivation - Pointer Architecture'` — 7 `test(...)` calls (lines 20-183):
  - `'On wake, activation pc equals kappa (restart at clause 1)'`
  - `'Multiple suspensions on same variable all activate'`
  - `'Disarmed suspensions do not activate'`
  - `'Suspension forwarding when binding to another variable'`
  - `'Chain of variable bindings forwards suspensions correctly'`
  - `'suspendOnWriter adds directly to writer cell'`
  - `'suspendOnReader follows pointer to find writer'`
- Group 2 — `'Fairness Budget - Pointer Architecture'` — 1 `test(...)` call (lines 185-205):
  - `'26-step tail recursion budget yields and resets'`
- Group 3 — `'CommitOps Equivalent - Pointer Architecture'` — 1 `test(...)` call (lines 207-241):
  - `'applySigmaHat binds multiple variables and collects activations'`
- 9 `test(...)` calls total, all synchronous (no `async`, no `Future`).
- Per-test fresh state: every test begins with `final rt = GlpRuntime(); final heap = rt.heap as HeapFCP;` (no closure over outer mutable state).
- Local construct inventory (verified against source):
  - `final` locals: rt, heap, record, r1, r2, r3, activations, acts1, acts2, goalIds, wc, sigmaHat, allActivations, acts, y, y26, y1, and writerAddr/readerAddr/w1/r1/w2/r2/w3/r3 via tuple destructure.
  - `const GoalId g = 77;`, `const Pc kappa = 1;`, `const GoalId g = 123;`.
  - Tuple destructure: `final (writerAddr, readerAddr) = heap.allocateVariable();`, `final (writerAddr, _) = ...`, `final (w1, r1) = ...`, etc.
  - `as` cast: `rt.heap as HeapFCP`, `heap.cells[writerAddr].content as WriterContent`, `(heap.cells[writerAddr].content as WriterContent).suspensions`.
  - Null-assertion: `wc.suspensions!.record.goalId` (one site).
  - Constructor calls (no `new`): `GlpRuntime()`, `SuspensionRecord(g, kappa)` (11 sites total counting inline forms), `ConstTerm('ground'|'value'|'final'|'end'|'a'|'b'|'c')`.
  - Method calls: `heap.suspendOnReader(...)` (8 sites), `heap.suspendOnWriter(...)` (1 site), `heap.bindWriter(...)` (5 sites), `heap.bindWriterToReader(...)` (3 sites), `rt.tailReduce(g)` (3 sites + 25 in loop), `rt.budgetOf(g)` (2 sites), `r1.disarm()` (1 site), `allActivations.addAll(acts)` (1 site).
  - Indexer + member access: `heap.cells[<addr>].content`.
  - Iterable: `activations.map((a) => a.id).toSet()` (2 sites).
  - Literals: `<GoalRef>[]` (typed empty list); `<int, Term>{ w1: ConstTerm('a'), w2: ConstTerm('b'), w3: ConstTerm('c') }` (typed map literal).
  - Loop: `for (var i = 0; i < 25; i++)` (classical for); `for (final entry in sigmaHat.entries)` (map-entry iteration).
  - String interpolation: `'should not yield on step ${i + 1}'`.
  - `expect` matchers used: `hasLength`, `isA<T>`, `isEmpty`, `isNotNull`, `isFalse` (with `reason:`), `isTrue` (with `reason:`), `equals(literal)`, `containsAll([...])`, `equals({1, 2, 3})` (set literal), bare-expected shorthand (e.g. `expect(rt.budgetOf(g), 26, reason: ...)`).

No async, no `late`, no nested groups, no per-group `setUp/tearDown`, no `skip:`/`timeout:`/`retry:`, no mixins, no extensions, no isolates, no streams, no completers.

## 2. Dart → C#/.NET Conversion Plan

Per construct (each entry mirrors the RATIFIED convspec; carry-forward KB hits flagged "CF", first-seen idioms flagged "NEW"; null `idiom_id` rows in convspec are anchored by `research_finding_id`):

- **`library;` directive** → DROP. C# has no per-file library declaration; file participates in compilation via the .csproj. The lead doc-comment block (Tests for suspension and reactivation… / Adapted from… / For spec docs/heap-pointer-architecture-spec.md v3.0 / three "Key changes" bullets) is preserved as the XML doc-comment on the first emitted test class (`SuspensionAndReactivationPointerArchitectureTests`); the other two classes get a one-line back-reference. File-scoped namespace `<RootNs>.Test.Heap;` mirrors the `test/heap` directory shape (precedent: binding_pointer_test / varref_pointer_test). [CF: rf-dart-library-directive-to-csharp-namespace-elision]

- **`import 'package:test/test.dart';`** → `using Xunit;`. Codegen MUST also emit `using System;`, `using System.Collections.Generic;` (for the `Dictionary<int, Term>` literal in CommitOps group), and `using System.Linq;` (for `.Select(...).ToHashSet()`). [CF: rf-dart-package-test-import-to-xunit-using]

- **Five `package:glp_runtime/runtime/...` imports (one with `show`)** → collapse to single `using <RootNs>.Runtime;`. The `show HeapFCP, Pointer, SuspensionListNode, WriterContent` clause has NO C# counterpart — C# `using` brings the whole namespace in scope; the four names are visible by virtue of namespace membership. Codegen MUST NOT use `using <Alias> = <Type>;` (per-name aliasing, not surface narrowing). [CF: rf-dart-import-relative-to-csharp-using-namespace + rf-dart-import-show-clause-no-csharp-counterpart]

- **`void main()` entrypoint** → ELIMINATE. xUnit discovers `[Fact]` methods by reflection. Lossless because the body is only three top-level `group(...)` calls with no statements between them. [CF: rf-dart-package-test-main-omit-in-xunit]

- **Three sibling top-level `group(...)` blocks (flat, no shared setUp)** → three top-level test classes in the same `.cs` file, same `namespace <RootNs>.Test.Heap;`:
  1. `public class SuspensionAndReactivationPointerArchitectureTests` — 7 `[Fact]` methods.
  2. `public class FairnessBudgetPointerArchitectureTests` — 1 `[Fact]` method.
  3. `public class CommitOpsEquivalentPointerArchitectureTests` — 1 `[Fact]` method.

  Class-name shape: PascalCase + `Tests` suffix; hyphens / spaces / parentheses stripped (matches varref_pointer_test precedent). No constructor / `IDisposable.Dispose` (no `setUp`/`tearDown` in source). No `IClassFixture<T>` (no shared cross-test state). Single-class+`[Trait("Group",…)]` alternative REJECTED because the three groups address structurally distinct concerns (suspension vs fairness vs commit-ops). [CF: rf-dart-package-test-group-to-xunit-class — flat-siblings sub-case]

- **9 `test(...)` calls** → one `[Fact(DisplayName = "<original Dart label>")] public void <PascalCasedIdentifier>() { … }` method per `test`, on the enclosing class. DisplayName preserves the verbatim Dart label (spaces / hyphens / commas / parens). Identifier mangling (PascalCase + punctuation-strip):
  - `OnWakeActivationPcEqualsKappaRestartAtClause1`
  - `MultipleSuspensionsOnSameVariableAllActivate`
  - `DisarmedSuspensionsDoNotActivate`
  - `SuspensionForwardingWhenBindingToAnotherVariable`
  - `ChainOfVariableBindingsForwardsSuspensionsCorrectly`
  - `SuspendOnWriterAddsDirectlyToWriterCell`
  - `SuspendOnReaderFollowsPointerToFindWriter`
  - `TwentySixStepTailRecursionBudgetYieldsAndResets` (leading-digit reshape — C# identifiers cannot start with a digit; spelled-out cardinal prefix; DisplayName keeps literal `"26-step …"`)
  - `ApplySigmaHatBindsMultipleVariablesAndCollectsActivations`

  All return `void` (no async surface anywhere). [CF: rf-dart-test-callback-to-xunit-method-body]

- **`final <name> = <expr>`** locals → `var <name> = <expr>`. Three sub-shapes all collapse:
  - bare ctor: `final rt = GlpRuntime()` → `var rt = new GlpRuntime()`
  - `as`-cast: `final heap = rt.heap as HeapFCP` → `var heap = (HeapFCP)rt.Heap` (explicit cast — NOT `x as T` which yields null on mismatch — WRONG semantics)
  - tuple destructure: `final (w, r) = heap.allocateVariable()` → `var (w, r) = heap.AllocateVariable()` (identical syntax both sides; `_` discard identical).

  Note: in the "Multiple suspensions" / "Disarmed suspensions" tests the local names `r1`/`r2`/`r3` denote SuspensionRecords (NOT reader addresses — the destructure in those tests is `(writerAddr, readerAddr)`, so no shadow collision). Codegen MUST preserve variable identity disjoint per-test. [CF: rf-dart-final-local-to-csharp-var-local + rf-dart-record-destructure-to-csharp-valuetuple-deconstruction + rf-dart-as-cast-to-csharp-explicit-cast]

- **`const GoalId g = …;` / `const Pc kappa = …;`** → preserve typedef alias names verbatim: `const GoalId g = 77;`, `const Pc kappa = 1;`, `const GoalId g = 123;`. Materialised via file-scoped or global `using GoalId = System.Int32; using Pc = System.Int32;` (per machine_state.dart.md idiom). C# `const` on a local of compile-time-primitive type is legal. Codegen MUST emit the typedef-alias name (NOT collapse to plain `int`) so the test documents the "opaque-int identifier kind" contract. [CF: rf-dart-typedef-int-to-csharp-global-using-alias]

- **`rt.heap`** (Dart lowerCamelCase property) → `rt.Heap` (C# PascalCase property of class `GlpRuntime`). The cast pattern `rt.heap as HeapFCP` → `(HeapFCP)rt.Heap` (explicit cast — `Runtime.Heap` is declared as base `Heap` interface; the cast accesses HeapFCP-specific methods). [CF: rf-dart-mutable-state-class-identity-equality-to-csharp-class]

- **`GlpRuntime()`** (no-arg constructor call, no `new`) → `new GlpRuntime()`. No constructor args; no target-typed-new shorthand (incompatible with `var` locals). [CF: rf-dart-mutable-state-class-identity-equality-to-csharp-class]

- **`SuspensionRecord(<int>, <int>)`** (11 sites, including 3 inline-in-argument-position in CommitOps test) → `new SuspensionRecord(<int>, <int>)`. SUT type is reference-type class with positional ctor `(int? goalId, int resumePC)`, mutable Armed state via `Disarm()`. Codegen MUST preserve REFERENCE identity (no `record class`, no `record struct`) — `r1.disarm()` mutates state observed via the heap's suspension list aliasing. [CF: rf-dart-shared-mutable-record-by-reference-to-csharp-class]

- **`ConstTerm('<str>')`** (7 sites: 'ground', 'value', 'final', 'end', 'a', 'b', 'c') → `new ConstTerm("<str>")`. Dart `'…'` → C# `"…"` (always double quotes — Dart single-char `'x'` is a string; C# `'x'` is a `char`). [CF: rf-dart-sumleaf-no-eq-to-csharp-class-no-record + rf-dart-string-literal-to-csharp-string-literal]

- **`heap.suspendOnReader(<readerAddr>, <record>)`** (8 sites) → `heap.SuspendOnReader(<readerAddr>, <record>)`. PascalCase. Return type `void`. Method walks the reader's `Pointer` to the writer cell and APPENDS the suspension to the writer's `WriterContent.Suspensions` (per file lead "Suspensions now live on WRITER cells"). [CF: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods]

- **`heap.suspendOnWriter(<writerAddr>, <record>)`** (1 site) → `heap.SuspendOnWriter(<writerAddr>, <record>)`. Return type `void`. Directly appends to writer cell's `WriterContent.Suspensions` without Pointer traversal. [CF: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods]

- **`heap.bindWriter(<writerAddr>, <term>)`** (5 sites) → `heap.BindWriter(<writerAddr>, <term>)`. Return type `List<GoalRef>` (concrete `List<T>`, not `IReadOnlyList<T>`; test code calls `.first.id`, `.map(...)`, `.addAll(...)`). `GoalRef` is `readonly record struct` (value-typed, copied by value into the list). [CF: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods]

- **`heap.bindWriterToReader(<writerAddr>, <readerAddr>)`** (3 sites, one captured: `final acts1 = …`) → `heap.BindWriterToReader(w1, r2)`. Return type `List<GoalRef>` (always EMPTY — forwarding does NOT activate). Semantics (LOAD-BEARING contract under test): (a) sets `cells[w1].Content = new Pointer(r2)` keeping `Tag = WrtTag`, (b) FORWARDS any suspensions from w1's previous WriterContent to the deref-chain end at r2, (c) returns empty list. The "Chain" test exercises a 3-hop chain w1→w2→w3; codegen MUST NOT activate eagerly NOR drop suspensions on intermediate writers. [CF: rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods]

- **`rt.tailReduce(g)`** (3 named sites + 25 in for-loop) → `rt.TailReduce(g)`. Return type `bool` (true iff budget exhausted and reset on this call). Contract: 25 calls return false, 26th returns true (reset), 27th returns false with budget back to 25. [CF: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary]

- **`rt.budgetOf(g)`** (2 sites) → `rt.BudgetOf(g)`. Return type `int` (remaining budget; defaults to `TailRecursionBudgetInit = 26` if `g` has no entry). [CF: rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary]

- **`r1.disarm()`** (1 site) → `r1.Disarm()`. Mutates `Armed` / sets `GoalId = null`. Observable through heap's suspension list (REFERENCE aliasing — load-bearing for the "Disarmed suspensions do not activate" test). Codegen MUST keep `SuspensionRecord` as reference-type class. [CF: rf-dart-shared-mutable-record-by-reference-to-csharp-class]

- **`heap.cells[<addr>].content`** (multiple sites) → `heap.Cells[<addr>].Content`. `Cells` is `List<Cell>` (reference-type cells). `List<T>` indexer returns reference for reference types. Cast variant: `(heap.cells[<addr>].content as WriterContent)` → `((WriterContent)heap.Cells[<addr>].Content)`. [CF: rf-dart-list-indexing-to-csharp-list-indexer + rf-dart-as-cast-to-csharp-explicit-cast]

- **`wc.suspensions!.record.goalId`** (1 site, in the "suspendOnWriter adds directly to writer cell" test) → `wc.Suspensions!.Record.GoalId`. Dart `!` (runtime + compile-time check) vs C# `!` (COMPILE-TIME ONLY) — runtime fail-fast in C# comes from the subsequent member-access `NullReferenceException`, observably equivalent for the test's contract (the test then asserts `equals(55)` on the unwrapped value; any null intermediate would have thrown anyway). For tests requiring explicit runtime throw at the `!` site itself, use `expr ?? throw new InvalidOperationException("…")` — not needed here. [NEW: rf-dart-null-assertion-bang-to-csharp-null-forgiving-bang]

- **`<GoalRef>[]`** (typed empty list literal, 1 site in CommitOps test) → `new List<GoalRef>()`. [CF: rf-dart-list-literal-to-csharp-list-initializer]

- **`<int, Term>{ w1: ConstTerm('a'), w2: ConstTerm('b'), w3: ConstTerm('c') }`** (typed map literal, 1 site) → `new Dictionary<int, Term> { { w1, new ConstTerm("a") }, { w2, new ConstTerm("b") }, { w3, new ConstTerm("c") } }` (collection-initialiser syntax). `Term` is abstract base; `ConstTerm : Term` (covariant assignment). Key is plain `int` → `EqualityComparer<int>.Default` (trivial; no `IEquatable` contract needed unlike varref_pointer_test's `<VarRef, String>` precedent). [CF: rf-dart-map-literal-typed-to-csharp-dictionary]

- **`for (final entry in sigmaHat.entries) { … entry.key … entry.value … }`** → `foreach (var entry in sigmaHat) { … entry.Key … entry.Value … }`. C# `Dictionary<K, V>` directly implements `IEnumerable<KeyValuePair<K, V>>` (no `.Entries` accessor needed). ORDERING NUANCE explicitly addressed: Dart's default map iterates in insertion order; C# `Dictionary<K, V>` iteration order is UNDEFINED. NOT load-bearing for this test (assertions are `.Count` and `.SetEquals` — both order-insensitive). Codegen MUST NOT depend on Dictionary enumeration order. [NEW: rf-dart-for-in-map-entries-to-csharp-foreach-kvp]

- **`allActivations.addAll(acts)`** (1 site) → `allActivations.AddRange(acts)`. NOT `Add(acts)` (would add the whole enumerable as a single element if generics permitted, OR not compile). [NEW: rf-dart-list-addall-to-csharp-list-addrange]

- **`activations.map((a) => a.id).toSet()`** (2 sites) → `activations.Select(a => a.Id).ToHashSet()`. Element type is `int` (or `GoalId` alias) per `GoalRef.Id : int`. Requires `using System.Linq;`. [CF: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset]

- **`for (var i = 0; i < 25; i++) { … }`** (classical counted for-loop, 1 site) → `for (int i = 0; i < 25; i++) { … }` (or `var i` — both legal). Body translation statement-for-statement: `final y = rt.tailReduce(g);` → `var y = rt.TailReduce(g);`. [NEW: rf-dart-classical-for-loop-to-csharp-for-loop]

- **String interpolation `'should not yield on step ${i + 1}'`** → `$"should not yield on step {i + 1}"`. C# requires leading `$` sigil; expression syntax inside braces identical. [NEW: rf-dart-string-interpolation-to-csharp-interpolated-string]

- **`expect(<iterable>, hasLength(<n>))`** (3 sites: lines 43, 67, 90, 117, 144, 237 — all target `List<GoalRef>`) → `Assert.Equal(<n>, <list>.Count)` (NOT `.Length`; NOT LINQ `.Count()`). Argument-order swap (expected first) per rf-dart-expect-equals-to-xunit-assert-equal-argorder. [NEW: rf-dart-expect-hasLength-to-xunit-assert-equal-count]

- **`expect(actual, isA<T>())`** (multiple sites with T ∈ {WriterContent, Pointer}) → `Assert.IsType<T>(actual)` (exact match; every target type is a sealed leaf per cells.dart.md / suspension.dart.md, so exact assertion is observably equivalent AND strictly tighter than `IsAssignableFrom<T>`). [CF: rf-dart-expect-isA-to-xunit-assert-istype]

- **`expect(<Suspensions>, isNotNull)`** (1 site) → `Assert.NotNull(<Suspensions>)`. [NEW: rf-dart-expect-isNotNull-to-xunit-assert-notnull]

- **`expect(acts1, isEmpty)`** (1 site) → `Assert.Empty(acts1)`. [CF: rf-dart-expect-isEmpty-to-xunit-assert-empty]

- **`expect(<int>, <int>)` bare-expected shorthand AND `expect(<int>, equals(<int>))` explicit matcher** (many sites) → `Assert.Equal(<expected>, <actual>)` with argument-order swap. The `reason:` named argument is NOT accepted by `Assert.Equal<T>` (only by `Assert.True/False`). Spec preference: PRESERVE the reason via `Assert.True(actual == expected, "<reason>")` when reason is present (e.g. `expect(rt.budgetOf(g), 26, reason: 'budget resets after yielding')` → `Assert.True(rt.BudgetOf(g) == 26, "budget resets after yielding")`); otherwise emit plain `Assert.Equal`. [CF: rf-dart-expect-equals-to-xunit-assert-equal-argorder]

- **`expect(y, isFalse, reason: 'should not yield on step ${i + 1}')`** (in for-loop body) → `Assert.False(y, $"should not yield on step {i + 1}")`. `Assert.False(bool, string)` overload preserves the diagnostic message. [CF: rf-dart-expect-isFalse-to-xunit-assert-false]

- **`expect(y26, isTrue, reason: 'should yield on step 26')`** → `Assert.True(y26, "should yield on step 26")`. [CF: rf-dart-expect-isTrue-to-xunit-assert-true]

- **`expect(goalIds, containsAll([10, 20, 30]))`** (1 site in "Multiple suspensions" test) → `Assert.Superset(new HashSet<int> { 10, 20, 30 }, goalIds)`. ARGUMENT ORDER: expected-subset FIRST (the OPPOSITE of `Assert.Equal`'s actual-last convention is NOT the case here — both put expected first, but xUnit's `Assert.Superset(expected, actual)` semantics put the expected-subset first and the actual-superset second). Per xunit.net Assertions reference. [NEW: rf-dart-expect-containsAll-to-xunit-assert-superset]

- **`expect(goalIds, equals({1, 2, 3}))`** (1 site, set-literal expected) → `Assert.True(goalIds.SetEquals(new[] { 1, 2, 3 }))`. NOT `Assert.Equal(new HashSet<int>{1,2,3}, goalIds)` — xUnit `Assert.Equal` on HashSet uses sequence semantics (order-sensitive — WRONG for sets, especially given undefined Dictionary enumeration order in C#). [CF: rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset]

## 3. Decomposed Task Units

- **T1**: File-scoped `namespace <RootNs>.Test.Heap;` mirroring `test/heap/` directory shape.
- **T2**: File-scope using directives — `using Xunit;`, `using System;`, `using System.Collections.Generic;`, `using System.Linq;`, `using <RootNs>.Runtime;` (single using covers all five SUT imports; `show` clause dropped).
- **T3**: File-scope using-alias directives mirroring `GoalId`/`Pc` typedefs — `using GoalId = System.Int32; using Pc = System.Int32;`.
- **T4**: Drop `void main()` entirely (xUnit attribute-driven discovery).
- **T5**: Emit class `SuspensionAndReactivationPointerArchitectureTests` with file-lead doc-comment as XML doc-comment; 7 `[Fact(DisplayName = …)]` methods.
- **T6**: Emit class `FairnessBudgetPointerArchitectureTests` with one `[Fact]` method (`TwentySixStepTailRecursionBudgetYieldsAndResets`).
- **T7**: Emit class `CommitOpsEquivalentPointerArchitectureTests` with one `[Fact]` method (`ApplySigmaHatBindsMultipleVariablesAndCollectsActivations`).
- **T8**: Per test body — translate `final` → `var`, ctor calls → `new`, `as`-cast → `(T)x` explicit cast, tuple destructure `final (a,b)=…` → `var (a,b)=…`, instance methods → PascalCase.
- **T9**: Per test body — translate `wc.suspensions!.record.goalId` → `wc.Suspensions!.Record.GoalId` (compile-time `!`; runtime fail-fast via subsequent member access NRE).
- **T10**: Per test body — translate `heap.suspendOnReader` / `heap.suspendOnWriter` / `heap.bindWriter` / `heap.bindWriterToReader` PascalCase + return-list shape `List<GoalRef>` preserved; suspension-forwarding (no-activate) contract on `BindWriterToReader` preserved.
- **T11**: Translate `rt.tailReduce(g)` / `rt.budgetOf(g)` → `rt.TailReduce(g)` / `rt.BudgetOf(g)` with bool/int return.
- **T12**: Translate the classical for-loop in `TwentySixStepTailRecursionBudgetYieldsAndResets` (`for (int i = 0; i < 25; i++)`) and interpolated diagnostic string `$"should not yield on step {i + 1}"` via `Assert.False(y, $"…")` + `Assert.True(y26, "…")` for diagnostic preservation.
- **T13**: Translate the `Dictionary<int, Term>` collection-initialiser literal + `foreach (var entry in sigmaHat)` map-iteration in `ApplySigmaHatBindsMultipleVariablesAndCollectsActivations`; use `entry.Key`/`entry.Value`; emit `allActivations.AddRange(acts)`.
- **T14**: Translate matcher mapping per construct — `hasLength(N)` → `Assert.Equal(N, list.Count)`; `isA<T>()` → `Assert.IsType<T>`; `isNotNull` → `Assert.NotNull`; `isEmpty` → `Assert.Empty`; `equals(X)` / bare-expected → `Assert.Equal(X, actual)`; `isFalse`+reason → `Assert.False(actual, reason)`; `isTrue`+reason → `Assert.True(actual, reason)`; `containsAll([…])` → `Assert.Superset(new HashSet<T>{…}, actual)`; `equals({set literal})` → `Assert.True(actual.SetEquals(new[]{…}))`.
- **T15**: Preserve `const GoalId g = …;` / `const Pc kappa = …;` verbatim (typedef alias names NOT collapsed to `int`).
- **T16**: Omit constructor and `IDisposable.Dispose` on all three classes (no setUp/tearDown in source); per-test fresh state via inline `var rt = new GlpRuntime(); var heap = (HeapFCP)rt.Heap;`.
- **T17**: Register 8 first-seen idioms in the KB (rf-dart-null-assertion-bang-to-csharp-null-forgiving-bang, rf-dart-for-in-map-entries-to-csharp-foreach-kvp, rf-dart-list-addall-to-csharp-list-addrange, rf-dart-classical-for-loop-to-csharp-for-loop, rf-dart-string-interpolation-to-csharp-interpolated-string, rf-dart-expect-hasLength-to-xunit-assert-equal-count, rf-dart-expect-isNotNull-to-xunit-assert-notnull, rf-dart-expect-containsAll-to-xunit-assert-superset) so subsequent convspecs reuse via KB rather than re-derive.

## 4. Research Findings

none required — convspec is RATIFIED and supplies authoritative-both-sides citations for every construct (FR-024). Twenty-one carry-forward idioms reused verbatim from prior convspecs per FR-012 / SC-007 (rf-dart-library-directive-to-csharp-namespace-elision, rf-dart-package-test-import-to-xunit-using, rf-dart-import-relative-to-csharp-using-namespace + rf-dart-import-show-clause-no-csharp-counterpart, rf-dart-package-test-main-omit-in-xunit, rf-dart-package-test-group-to-xunit-class, rf-dart-test-callback-to-xunit-method-body, rf-dart-final-local-to-csharp-var-local, rf-dart-as-cast-to-csharp-explicit-cast, rf-dart-record-destructure-to-csharp-valuetuple-deconstruction, rf-dart-typedef-int-to-csharp-global-using-alias, rf-dart-mutable-state-class-identity-equality-to-csharp-class, rf-dart-bind-writer-family-callsite-to-csharp-pascalcase-methods, rf-dart-shared-mutable-record-by-reference-to-csharp-class, rf-dart-sumleaf-no-eq-to-csharp-class-no-record, rf-dart-list-indexing-to-csharp-list-indexer, rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary, rf-dart-list-literal-to-csharp-list-initializer, rf-dart-map-literal-typed-to-csharp-dictionary, rf-dart-expect-equals-to-xunit-assert-equal-argorder, rf-dart-expect-isA-to-xunit-assert-istype, rf-dart-expect-isTrue-to-xunit-assert-true, rf-dart-expect-isFalse-to-xunit-assert-false, rf-dart-expect-isEmpty-to-xunit-assert-empty, rf-dart-iterable-map-toset-to-csharp-linq-select-tohashset). Eight first-seen idioms grounded in authoritative Dart + .NET docs (Dart language tour, dart.dev/null-safety, api.dart.dev for List/Map, pub.dev/matcher; Microsoft Learn for using-directive/const/interpolated/for-statement/Dictionary/List/HashSet/Assert, xunit.net Assertions reference).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/heap/suspension_pointer_test.dart.md` (RATIFIED convspec, schema_version: 1, escalations: []). Every construct in §2 mirrors a convspec entry verbatim (target_decision, idiom_id, research_finding_id, nuance preserved). The construct list cardinality matches: 29 distinct construct_key rows in convspec (library_directive, package_test.import_directive, package_under_test.import_directive_runtime_facade_…, package_test.main_entrypoint, package_test.group_block.three_sibling…, package_test.test_call_simple, local_var.final_constructor_instance_or_cast_or_record_destructure, local_var.const_typed_goalid_pc_int_alias, expression.member_property_access_runtime_heap_field, constructor_call.glp_runtime_no_args, constructor_call.suspension_record_two_ints_goalid_pc, constructor_call.const_term_with_string_literal, method_call.heap_mutator_suspend_on_reader, method_call.heap_mutator_suspend_on_writer, method_call.heap_mutator_bind_writer_returning_list_goalref, method_call.heap_mutator_bind_writer_to_reader_forwarding_returns_list_goalref, method_call.runtime_tail_reduce_returning_bool, method_call.runtime_budget_of_returning_int, instance_method_call.suspension_disarm, field_indexer.cells_at_addr_with_member_access…, as_cast.type_assertion_on_cell_content_or_runtime_heap, expression.null_assertion_operator_bang_then_member_chain, member_access.suspension_record_goalid, expression.list_literal_typed_empty_goalref, expression.map_literal_typed_int_to_term, expression.foreach_entry_iteration_over_map_entries, instance_method_call.list_addall, iterable.map_with_arrow_to_set_of_int, constructor_call.suspension_record_in_map_value_position, expression.for_loop_classical_with_int_counter, expression.string_interpolation_curly_dollar, package_test.expect_hasLength, package_test.expect_isA_T, package_test.expect_isNotNull, package_test.expect_equals_with_goalref_id_or_pc_int, package_test.expect_isFalse_with_reason, package_test.expect_isTrue_with_reason, package_test.expect_isEmpty, package_test.expect_containsAll_of_int_iterable, package_test.expect_equals_set_literal_of_int) — every one materialised as a corresponding §2 entry. cycle_group_id = 129 honoured (no SCC siblings — pure leaf test file; depends on lib/runtime/* SUT modules with their own already-ratified convspecs). scc_siblings: [] consistent with leaf-status. Source sha256 8f0020be… matches the convspec header (`source_sha256: 8f0020be2a925a63f498abf316c3e5ea71c60b9a6e5ef23bbffbc1615d4b2a95`).

## 6. Escalations

None.
