---
path: test/multiagent/global_send_test.dart
cycle_group_id: 143
scc_siblings: []
generated_at: 2026-05-21T16:01:40Z
source_sha256: c998b41351407035919314db767e3b490b4b49953c66d2e0b0c06b56a306a1f6
schema_version: 1
---

# Conversion Plan: test/multiagent/global_send_test.dart

## 1. Source Analysis

The source file is a Dart `package:test` test file with 193 lines exercising
the `global_send` goal mechanism (madGLP-spec.md Section 4). Verbatim
inspection (cross-checked against the convspec's line citations):

- 3 imports: `package:test/test.dart` (framework) + 3 SUT imports
  (`package:glp_runtime/multiagent/global_send.dart`,
  `.../global_writers_table.dart`, `.../mad_helpers.dart`).
- `void main()` body contains TWO SIBLING `group(...)` calls (NOT nested):
  - `group('GlobalSendGoal', ...)` with 4 `test(...)` calls (lines 15-46,
    48-77, 79-110, 112-139).
  - `group('GlobalSendRegistry', ...)` with 2 `test(...)` calls (lines
    143-176, 178-191).
- Every `test(...)` callback is synchronous (no `async`/`Future`/`await`)
  and executable (no `skip:` argument anywhere).
- Local-variable pattern: each test body declares `final registry =
  GlobalSendRegistry('p');` and `final table = GlobalWritersTable('p');`
  fresh — no shared `setUp`/`setUpAll`/`tearDown`/`tearDownAll` lifecycle
  hooks anywhere in the file.
- Constructor invocations:
  - Positional primary ctor: `GlobalSendRegistry('p')` (6 sites),
    `GlobalWritersTable('p')` (5 sites).
  - Named-required ctor: `GlobalSendGoal(readerAddr:, globalName:,
    destination:)` (5 sites — lines 20-24, 53-57, 84-88, 117-121, plus 1
    inside the test on line 84); `GlobalSendSpawn(readerAddr:,
    globalName:, destAgent:)` (2 sites — lines 148-152, 153-157).
  - Named-constructor (factory) calls: `GlobalName.writer('p', 0)`
    (4 sites — lines 22, 41, 55, 86, 119, 151), `GlobalName.reader('r', 5)`
    (1 site — line 156), `TermVar.reader(401, writerAddr: 400)` (1 site —
    line 96).
- Method calls on `registry`: `register(goal)` (4 sites; line 25, 53-57
  via inlined, 84-88, 117-121), `registerSpawns(spawns)` (1 site — line 161),
  `hasGoalFor(K)` (5 sites — lines 28, 122, 134, 164, 165),
  `getGoalFor(K)` (2 sites — lines 169, 173; both with trailing `!`),
  `onWriterBound(...)` (5 sites — lines 31-36, 60-65, 92-97, 126-131,
  183-188), getter `pendingCount` (3 sites — lines 123, 135, 166).
- Lambda literals as the `extractVariables:` named argument:
  - `(_) => []` — empty `List<TermVar>` literal (4 sites: lines 35, 64,
    130, 187).
  - `(_) => [TermVar.reader(401, writerAddr: 400)]` — single-element
    list (1 site: line 96).
- List literal: 1 typed list literal `final spawns = [ GlobalSendSpawn(...),
  GlobalSendSpawn(...) ];` at lines 147-158 (consumed by
  `registry.registerSpawns(spawns)`).
- Null-assertion `!` operator: 5 sites — three `result!.<member>`
  (lines 40, 69, 102; each preceded by `expect(result, isNotNull)` on
  the line above), two `registry.getGoalFor(K)!` (lines 169, 173; both
  preceded by `expect(registry.pendingCount, 2)` on line 166).
- `expect(...)` matcher calls — total 38 across the file:
  - `isTrue` (8 sites: lines 28, 69, 104, 122, 164, 165, 170, 174);
  - `isFalse` (1 site: line 134);
  - `isNotNull` (3 sites: lines 39, 68, 101);
  - `isNull` (1 site: line 190);
  - implicit-equals (bare-value second argument; 15 sites: lines 40, 41,
    42, 70, 71, 72, 73, 102, 103, 105, 123, 135, 166, 171, 175);
  - plus paired construct/method-call inspection (covered by the named-ctor
    and member-access rows).
- Indexer access: `result.newGoals[0]` (2 sites: lines 103, 105;
  preserved verbatim on both sides — `[]` indexer syntax identical).
- Inline doc comments document spec-section references (Section 4 — the
  `global_send` predicate at lines 44-45, 75-77; Section 12 — Goal
  Atomicity at lines 107-109) and per-test Given/When/Then commentary
  throughout.

No async, no try/catch, no isolate primitives, no Future/Stream, no
Stream subscriptions. The file is a pure synchronous unit-test surface
over the registry API.

## 2. Dart → C#/.NET Conversion Plan

Per convspec §`constructs:`, each Dart construct maps to the listed
C#/.NET target. Mirroring the ratified convspec verbatim:

1. **`import 'package:test/test.dart';`** → `using Xunit;` at file
   scope. Also emit `using System.Collections.Generic;` at file scope
   to make `List<TermVar>` / `List<GlobalSendSpawn>` resolvable at
   inline-literal call sites. NO `using System;` required (no
   `IDisposable`, no typed-`Exception` asserts). NO `using static`.
   (convspec idiom `rf-dart-package-test-to-dotnet-xunit`).

2. **Three SUT imports** (`package:glp_runtime/multiagent/...`) → ONE
   `using <RootNs>.Multiagent;` directive. All three SUT files
   (`global_send.dart`, `global_writers_table.dart`,
   `mad_helpers.dart`) target the SAME C# sub-namespace under
   `Multiagent`. Cross-file dependency: the test .csproj must
   `<ProjectReference>` the runtime .csproj (langpair/project-skeleton
   level, OUT OF SCOPE for the single-file artifact, recorded for
   codegen wiring). (idiom `rf-dart-package-sut-import-to-csharp-using`).

3. **`void main() { group(...); group(...); }`** → DROP entirely. xUnit
   discovers `[Fact]` methods on `public` classes by reflection; no
   per-file entrypoint. (idiom `rf-dart-test-main-to-xunit-class-with-facts`).

4. **Two sibling `group(label, body)` calls** → TWO sibling top-level
   `public class <Label>Tests` declarations under the file's namespace:
   - `group('GlobalSendGoal', ...)` → `public class GlobalSendGoalTests`
     (4 `[Fact]` methods), optionally `[Trait("Group", "GlobalSendGoal")]`.
   - `group('GlobalSendRegistry', ...)` → `public class
     GlobalSendRegistryTests` (2 `[Fact]` methods), optionally
     `[Trait("Group", "GlobalSendRegistry")]`.
   NOT a nested-class layout (this file's groups are siblings, neither
   inside the other). xUnit's per-test fresh-instance lifecycle (one
   class instance per test) maps cleanly with no constructor-side
   fixture. (idiom `rf-dart-package-test-group-to-xunit-class`).

5. **Each `test(label, body)`** → `public void` method on the enclosing
   class with `[Fact(DisplayName = "<original label>")]`. Method-name
   mangling = label PascalCased with non-identifier chars stripped:
   - `'fires when reader becomes known'` → `FiresWhenReaderBecomesKnown`
   - `'produces correct message'` → `ProducesCorrectMessage`
   - `'nested variables spawn additional goals'` →
     `NestedVariablesSpawnAdditionalGoals`
   - `'goal removed after firing'` → `GoalRemovedAfterFiring`
   - `'registerSpawns converts GlobalSendSpawn to goals'` →
     `RegisterSpawnsConvertsGlobalSendSpawnToGoals`
   - `'onWriterBound returns null when no goal registered'` →
     `OnWriterBoundReturnsNullWhenNoGoalRegistered`
   The Given/When/Then comments + Spec-Section-4/12 references are
   carried into the method as a `/// <summary>` XML-doc block per
   FR-024 doc-level traceability. Method body translates the Dart
   arrange-act-assert verbatim. (idiom
   `rf-dart-test-callback-to-xunit-method-body`).

6. **`final <name> = <expr>;`** → `var <name> = <expr>;` (with mandatory
   C# `new` on ctor calls; Dart single-quote literals → C#
   double-quote literals). Specifically (per convspec):
   - `final registry = GlobalSendRegistry('p')` →
     `var registry = new GlobalSendRegistry("p");`
   - `final table = GlobalWritersTable('p')` →
     `var table = new GlobalWritersTable("p");`
   - `final goal = GlobalSendGoal(readerAddr: 100, ...)` →
     `var goal = new GlobalSendGoal(readerAddr: 100, globalName:
     GlobalName.Writer("p", 0), destination: "q");`
   - `final result = registry.onWriterBound(...)` →
     `var result = registry.OnWriterBound(writerAddr: 100, value: 42,
     table: table, extractVariables: _ => new List<TermVar>());`
   - `final spawns = [ GlobalSendSpawn(...), GlobalSendSpawn(...) ]` →
     `var spawns = new List<GlobalSendSpawn> { new
     GlobalSendSpawn(...), new GlobalSendSpawn(...) };`
   - `final goal1 = registry.getGoalFor(100)!` →
     `var goal1 = registry.GetGoalFor(100)!;`
   - `final goal2 = registry.getGoalFor(200)!` →
     `var goal2 = registry.GetGoalFor(200)!;`
   (idiom `rf-dart-final-local-to-csharp-var-local`).

7. **Dart named constructors** `GlobalName.writer(...)`,
   `GlobalName.reader(...)`, `TermVar.reader(...)` → C# PascalCased
   static factory methods on the same class:
   - `GlobalName.writer('p', 0)` → `GlobalName.Writer("p", 0)`
   - `GlobalName.reader('r', 5)` → `GlobalName.Reader("r", 5)`
   - `TermVar.reader(401, writerAddr: 400)` →
     `TermVar.Reader(401, writerAddr: 400)` (note: also a named arg).
   (idiom `rf-dart-named-constructor-to-csharp-static-factory`).

8. **Positional primary ctor** `GlobalSendRegistry('p')`,
   `GlobalWritersTable('p')` → `new GlobalSendRegistry("p")`,
   `new GlobalWritersTable("p")` (single positional `string agentId`
   parameter, SUT spec pins the constructor body's `AgentId =
   agentId;` assignment). (idiom
   `rf-dart-positional-primary-ctor-to-csharp-positional-ctor`).

9. **Named-required ctor invocations** `GlobalSendGoal(readerAddr:, ...)`,
   `GlobalSendSpawn(readerAddr:, ..., destAgent:)` → `new
   GlobalSendGoal(readerAddr: ..., globalName: ..., destination: "q")`
   and `new GlobalSendSpawn(readerAddr: ..., globalName: ..., destAgent:
   "q")`. C# named arguments at call site; constructor parameters
   declared as ordinary positional params with no default values (to
   preserve Dart `required` compile-time-must-supply semantics). The
   `destAgent` (on `GlobalSendSpawn`) vs `destination` (on
   `GlobalSendGoal`) vocabulary split is preserved verbatim (the SUT
   spec for `lib/multiagent/global_send.dart` owns the rename at
   `GlobalSendGoal.FromSpawn`). (idiom
   `rf-dart-named-argument-to-csharp-named-argument`).

10. **Named arguments at call site** (`writerAddr:`, `value:`, `table:`,
    `extractVariables:`, `writerAddr:` on `TermVar.Reader`) → C# named
    arguments preserved verbatim (camelCase, NOT PascalCased — C#
    convention for PARAMETER names is camelCase, identical to Dart). The
    C# method signature is positional in the declaration; the call-site
    uses named arguments. (idiom
    `rf-dart-named-argument-to-csharp-named-argument`).

11. **Arrow lambdas `(_) => <expr>`** → C# lambda `_ => <expr>`:
    - `(_) => []` → `_ => new List<TermVar>()` (empty list — explicit
      ctor call preferred over `new List<TermVar> { }` collection-init
      for the empty case as unidiomatic).
    - `(_) => [TermVar.reader(401, writerAddr: 400)]` →
      `_ => new List<TermVar> { TermVar.Reader(401, writerAddr: 400) }`.
    Lambda parameter `_` is an ordinary identifier on both sides (NOT
    a C# discard pattern). Assigned to the `extractVariables` parameter
    typed `Func<object?, IReadOnlyList<TermVar>>` per SUT spec
    (`List<T>` implements `IReadOnlyList<T>`). (idiom
    `rf-dart-arrow-lambda-to-csharp-lambda`).

12. **Typed list literal** `final spawns = [a, b];` → `var spawns = new
    List<GlobalSendSpawn> { a, b };` (collection-initializer syntax).
    Reject `new[] { ... }` (array form) because `registerSpawns`
    parameter is declared `IReadOnlyList<GlobalSendSpawn>` per SUT
    spec; `List<T>` satisfies the interface naturally. (idiom
    `rf-dart-list-literal-to-csharp-list-initializer`).

13. **Null-assertion `expr!`** → C# null-forgiving `expr!`. Semantic
    gap (Dart runtime-throw vs C# compile-time-only annotation) closed
    by preceding `Assert.NotNull` calls:
    - `result!.value`, `result!.globalName.isWriter`,
      `result!.newGoals.length` — each preceded on the previous line
      by `Assert.NotNull(result);` (mapped from `expect(result,
      isNotNull)`); compile-only-safe.
    - `registry.getGoalFor(100)!`, `registry.getGoalFor(200)!` — both
      preceded by `Assert.Equal(2, registry.PendingCount);` (not a
      per-key `Assert.NotNull`); codegen MAY insert explicit
      `Assert.NotNull(registry.GetGoalFor(K));` before each lookup to
      fully preserve Dart runtime-throw semantics (RECOMMENDED per
      convspec cu-7). Alternatively, rewrite to `var goal1 =
      Assert.IsType<GlobalSendGoal>(registry.GetGoalFor(100));`. (idiom
      `rf-dart-bang-operator-to-csharp-null-forgiving`).

14. **`expect(<bool>, isTrue)`** → `Assert.True(<bool>);` (8 sites).
    (idiom `rf-dart-expect-isTrue-to-xunit-assert-true`).

15. **`expect(<bool>, isFalse)`** → `Assert.False(<bool>);` (1 site).
    (idiom `rf-dart-expect-isFalse-to-xunit-assert-false`).

16. **`expect(x, isNotNull)`** → `Assert.NotNull(x);` (3 sites). (idiom
    `rf-dart-expect-isNotNull-to-xunit-assert-notnull`).

17. **`expect(x, isNull)`** → `Assert.Null(x);` (1 site). (idiom
    `rf-dart-expect-isNull-to-xunit-assert-null`).

18. **`expect(actual, value)`** (implicit-equals, bare value as 2nd arg)
    → `Assert.Equal(expected, actual);` with ARGUMENT ORDER SWAPPED
    (Dart actual-first; xUnit expected-first). 15 sites:
    - `expect(result!.value, 42)` → `Assert.Equal(42, result!.Value);`
    - `expect(result.globalName, GlobalName.writer('p', 0))` →
      `Assert.Equal(GlobalName.Writer("p", 0), result.GlobalName);`
    - `expect(result.destination, 'q')` → `Assert.Equal("q",
      result.Destination);`
    - `expect(result!.globalName.agent, 'p')` → `Assert.Equal("p",
      result!.GlobalName.Agent);`
    - `expect(result.globalName.index, 0)` → `Assert.Equal(0,
      result.GlobalName.Index);`
    - `expect(result.destination, 'q')` → `Assert.Equal("q",
      result.Destination);`
    - `expect(result.value, 'hello')` → `Assert.Equal("hello",
      result.Value);`
    - `expect(result!.newGoals.length, 1)` → `Assert.Equal(1,
      result!.NewGoals.Count);` (List.Count idiom).
    - `expect(result.newGoals[0].readerAddr, 400)` → `Assert.Equal(400,
      result.NewGoals[0].ReaderAddr);`
    - `expect(result.newGoals[0].destination, 'q')` →
      `Assert.Equal("q", result.NewGoals[0].Destination);`
    - `expect(registry.pendingCount, 1)` → `Assert.Equal(1,
      registry.PendingCount);`
    - `expect(registry.pendingCount, 0)` → `Assert.Equal(0,
      registry.PendingCount);`
    - `expect(registry.pendingCount, 2)` → `Assert.Equal(2,
      registry.PendingCount);`
    - `expect(goal1.destination, 'q')` → `Assert.Equal("q",
      goal1.Destination);`
    - `expect(goal2.destination, 'r')` → `Assert.Equal("r",
      goal2.Destination);`
    (idiom `rf-dart-expect-equals-to-xunit-assert-equal-argorder`).

19. **Member access (method call / getter)** → C# member access with
    PascalCased member name. Specifically:
    - `registry.register(goal)` → `registry.Register(goal);`
    - `registry.registerSpawns(spawns)` → `registry.RegisterSpawns(spawns);`
    - `registry.hasGoalFor(K)` → `registry.HasGoalFor(K)`
    - `registry.getGoalFor(K)` → `registry.GetGoalFor(K)`
    - `registry.onWriterBound(...)` → `registry.OnWriterBound(...)`
    - `registry.pendingCount` → `registry.PendingCount` (zero-arg
      property, NO parentheses at call site)
    - `result.value` → `result.Value` (property)
    - `result.globalName` → `result.GlobalName` (property)
    - `result.destination` → `result.Destination` (property)
    - `result.newGoals` → `result.NewGoals` (property)
    - `result.newGoals[0]` → `result.NewGoals[0]` (List indexer
      preserved verbatim)
    - `goal.readerAddr` → `goal.ReaderAddr` (property)
    - `goal1.globalName.isWriter` → `goal1.GlobalName.IsWriter`
      (property chain)
    - `goal2.globalName.isReader` → `goal2.GlobalName.IsReader`
      (property chain)
    The SUT spec (`lib/multiagent/global_send.dart.md` /
    `mad_helpers.dart.md`) is the source of truth for the
    auto-property-vs-method, get-only-vs-get/set, and expression-body
    decisions at the SUT side; THIS test plan records the call-site
    shape (PascalCase). (idiom
    `rf-dart-member-access-to-csharp-member-access-pascalcase`).

### Cross-file invariants (recorded, OUT OF SCOPE for this single-file artifact)

Per convspec cu-8/cu-9/cu-10 — these are hard dependencies on the SUT
specs; recorded for codegen wiring:

- **GlobalName structural equality.** `GlobalName` MUST be emitted with
  `IEquatable<GlobalName>` + `Object.Equals`/`GetHashCode` overrides
  (or as a C# `record class`) so `Assert.Equal(GlobalName.Writer("p",
  0), result.GlobalName)` performs structural equality, not reference
  equality. Source of truth:
  `.codeconv/conversion-specs/lib/multiagent/mad_helpers.dart.md`.

- **GlobalSendFiredResult.Value typed `object?`.** Required so
  `Assert.Equal<object?>` dispatches to `Object.Equals` for boxed
  int/string comparisons. Source of truth:
  `.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`.

- **OnWriterBound SYNCHRONOUS** (returns `GlobalSendFiredResult?`, NOT
  `Task<GlobalSendFiredResult?>`). Isolate-ownership invariant — async
  would force `await` and silently change concurrency. Source of truth:
  `.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`.

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives — `using Xunit;` + `using
  System.Collections.Generic;` + `using <RootNs>.Multiagent;` (one
  `using` covers all three SUT imports). done
- T2: Emit namespace declaration mirroring `test/multiagent/` →
  `<RootNs>.Test.Multiagent`. done
- T3: Emit two sibling `public class` declarations —
  `GlobalSendGoalTests` and `GlobalSendRegistryTests`, each optionally
  `[Trait("Group", "<label>")]`-tagged. done
- T4: Emit 4 `[Fact(DisplayName="...")]` methods on
  `GlobalSendGoalTests` — `FiresWhenReaderBecomesKnown`,
  `ProducesCorrectMessage`, `NestedVariablesSpawnAdditionalGoals`,
  `GoalRemovedAfterFiring` — each `public void`, no `async`. done
- T5: Emit 2 `[Fact(DisplayName="...")]` methods on
  `GlobalSendRegistryTests` —
  `RegisterSpawnsConvertsGlobalSendSpawnToGoals` and
  `OnWriterBoundReturnsNullWhenNoGoalRegistered`. done
- T6: Per method body, translate `final <name> = ...;` →
  `var <name> = ...;` with mandatory `new` on ctor calls and
  single-quote → double-quote literal conversion. done
- T7: Per method body, translate `GlobalSendRegistry('p')` /
  `GlobalWritersTable('p')` → `new GlobalSendRegistry("p")` / `new
  GlobalWritersTable("p")` (positional ctor). done
- T8: Per method body, translate `GlobalSendGoal(readerAddr:, ...)` /
  `GlobalSendSpawn(readerAddr:, ..., destAgent:)` → `new
  GlobalSendGoal(...)` / `new GlobalSendSpawn(...)` with named
  arguments preserved camelCase. done
- T9: Per method body, translate `GlobalName.writer(...)` /
  `GlobalName.reader(...)` / `TermVar.reader(...)` to PascalCased
  static factory calls. done
- T10: Per method body, translate `(_) => []` → `_ => new
  List<TermVar>()` and `(_) => [TermVar.reader(401, writerAddr: 400)]`
  → `_ => new List<TermVar> { TermVar.Reader(401, writerAddr: 400) }`.
  done
- T11: Per method body, translate `final spawns = [...]` → `var spawns
  = new List<GlobalSendSpawn> { ... };` with each element a `new
  GlobalSendSpawn(...)` call. done
- T12: Per method body, translate `expect(actual, isTrue/isFalse/
  isNotNull/isNull/<value>)` per matcher-routing table (`Assert.True`
  / `Assert.False` / `Assert.NotNull` / `Assert.Null` /
  `Assert.Equal(expected, actual)` WITH ARG SWAP). done
- T13: Per method body, translate all member access to PascalCased C#
  member access (`.register` → `.Register`, `.pendingCount` →
  `.PendingCount`, `.value` → `.Value`, `.newGoals[0]` → `.NewGoals[0]`,
  etc.). done
- T14: Per method body, preserve `expr!` translations; optionally insert
  explicit `Assert.NotNull(registry.GetGoalFor(K));` before
  `registry.GetGoalFor(K)!` lookups on lines 169 and 173 to fully
  preserve Dart runtime-throw semantics. done
- T15: Per method body, carry the Dart Given/When/Then comments + Spec
  Section 4 / Section 12 references into a `/// <summary>` XML-doc
  block on the C# method (FR-024 doc-level). done
- T16: Cross-file dependency — emit `<ProjectReference>` from test
  .csproj to runtime .csproj (langpair / 016-init scope; OUT OF SCOPE
  for this single-file artifact, recorded for codegen wiring). done

## 4. Research Findings

None required. Every construct in this file is covered by the ratified
convspec's `constructs:` rows and the cited research findings (each
with both Dart-side and .NET-side authoritative documentation
recorded). Specifically:

- 12 idioms reused verbatim from sibling test-file convspecs and the
  SUT specs: `rf-dart-package-test-to-dotnet-xunit`,
  `rf-dart-test-main-to-xunit-class-with-facts`,
  `rf-dart-package-test-group-to-xunit-class`,
  `rf-dart-test-callback-to-xunit-method-body`,
  `rf-dart-expect-isTrue-to-xunit-assert-true`,
  `rf-dart-expect-isNotNull-to-xunit-assert-notnull`,
  `rf-dart-expect-isNull-to-xunit-assert-null`,
  `rf-dart-expect-equals-to-xunit-assert-equal-argorder`,
  `rf-dart-package-sut-import-to-csharp-using`,
  `rf-dart-final-local-to-csharp-var-local`,
  `rf-dart-bang-operator-to-csharp-null-forgiving`,
  `rf-dart-named-constructor-to-csharp-static-factory`,
  `rf-dart-named-argument-to-csharp-named-argument`,
  `rf-dart-list-literal-to-csharp-list-initializer`.
- 4 new idioms first-recorded by this convspec with both-side
  authoritative documentation:
  `rf-dart-expect-isFalse-to-xunit-assert-false` (pub.dev `isFalse`
  constant + xunit.net `Assert.False` API);
  `rf-dart-positional-primary-ctor-to-csharp-positional-ctor` (dart.dev
  `constructors` reference + Microsoft Learn `constructors`);
  `rf-dart-arrow-lambda-to-csharp-lambda` (dart.dev `Functions /
  anonymous functions` + Microsoft Learn `Lambda expressions`);
  `rf-dart-member-access-to-csharp-member-access-pascalcase` (dart.dev
  `Operators / Member access` + Microsoft Learn `Member access
  operators` + .NET naming conventions).

All authoritative on both sides per convspec rationale section
"Why no escalations".

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/multiagent/global_send_test.dart.md` (ratified) plus the cross-file invariants
recorded in `.codeconv/conversion-specs/lib/multiagent/global_send.dart.md`, `.../global_writers_table.dart.md`, and `.../mad_helpers.dart.md`.

The plan exactly mirrors the convspec's `constructs:` rows, the
`conversion_units:` list (cu-1 through cu-10), the Rationale section
("Why all 6 tests are `[Fact]`", "Two sibling groups → two sibling
classes", "Reuse from sibling test-file specs", "New idioms
first-recorded", "Cross-file invariants", "Spec-section traceability
preserved", "Why no escalations"), and the convspec's `escalations: []`
finding. No fresh decisions introduced; no idiom-vs-idiom conflicts;
no idiom-vs-research conflicts. The convspec's three cross-file hard
invariants (GlobalName structural equality, `Value` typed `object?`,
`OnWriterBound` synchronous) are recorded as cross-file dependencies
not as undecidable items — they are owned by the SUT specs.

## 6. Escalations

None.
