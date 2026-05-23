---
path: test/multiagent/boot_loader_test.dart
cycle_group_id: 141
scc_siblings: []
generated_at: 2026-05-21T14:52:16Z
source_sha256: fbe7c999ea5524d849532628fdf73dc76056f9c3a49930c86d0432d4fb50baff
schema_version: 1
---

# Conversion Plan: test/multiagent/boot_loader_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/boot_loader_test.dart`
(238 lines, sha256 `fbe7c999…baff`) yields the following inventory:

- **Imports (lines 1–2)**:
  - `import 'package:test/test.dart';` — Dart `package:test` framework.
  - `import 'package:glp_runtime/multiagent/boot_loader.dart';` — SUT
    package import for `BootLoader` + `BootLoaderException`.
- **Entrypoint (line 4)**: `void main()` containing a single statement
  — the outer `group('BootLoader', ...)` call.
- **Outer group (lines 5–237)**: `group('BootLoader', () { ... })` with:
  - A `late BootLoader loader;` field (line 6) closed over by setUp and
    every nested `test`.
  - A `setUp(() { loader = BootLoader(); });` block (lines 8–10).
  - THREE inner groups:
    - `group('valid boot files', ...)` (lines 12–118) — 6 tests.
    - `group('error cases', ...)` (lines 120–203) — 5 tests.
    - `group('real file content', ...)` (lines 205–236) — 1 test.
- **12 `test(...)` calls** total, all synchronous (no `async`/`Future`).
  Each test has the shape: triple-quoted multi-line `.glp` source
  fixture → `loader.load(source)` (or `() => loader.load(source)` for
  error cases) → `expect(...)` assertions.
- **`expect` matcher uses** observed:
  - `equals(<int>)` and `equals(<String>)` for `.length`, `.agentId`,
    `.goalFunctor` (most-used).
  - `equals(<String>)` against `config.fullSource`.
  - `equals(['alice', 'bob', 'charlie'])` against
    `directives.map((d) => d.agentId).toList()`.
  - `isNot(contains('@'))` and `isNot(contains('procedure boot'))`.
  - `contains('procedure agent')`.
  - `throwsA(isA<BootLoaderException>().having((e) => e.message,
    'message', contains('<substr>')))` — 5 occurrences.
  - `isTrue` once against `directives.every((d) => d.goalFunctor ==
    'agent_init')`.
- **String literals**: every test fixture uses Dart triple-single-quoted
  multi-line strings (`'''…'''`) containing literal `.glp` source text
  (no `\n`/`\t` escape processing; no embedded `"`).
- **No `tearDown`** anywhere — only `setUp`.
- **No `skip:` arguments** anywhere — every `test` runs.
- **No `async`** anywhere — every closure is synchronous.

The file is purely a `package:test` xUnit-shaped test file exercising the
`BootLoader.load(source)` API surface and its `BootLoaderException`
error paths.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec `.codeconv/conversion-specs/test/multiagent/
boot_loader_test.dart.md` (sha256-pinned at construct-block level).

### 2.1 `dart.package_test.import_directive`
`import 'package:test/test.dart';` → `using Xunit;` at file scope.
Codegen MUST also emit `using System;` (forward-compat for any future
`IDisposable.Dispose` mapping; harmless here as no `tearDown` is
present), `using System.Linq;` (required for the `Select`/`All` LINQ
mappings below), and project to a namespace mirroring the Dart
`test/multiagent` subtree (e.g. `<RootNs>.Test.Multiagent`). Framework
choice (xUnit) is the project-wide pin set by the first `package:test`
file specced (`mad_error_handling_test.dart`) — re-used verbatim per
SC-007.

### 2.2 `dart.package_under_test.import_directive`
`import 'package:glp_runtime/multiagent/boot_loader.dart';` → a
`using <RootNs>.Multiagent;` directive resolving to the C# namespace
produced when `glp_runtime/lib/multiagent/boot_loader.dart` is itself
converted. THIS spec records only the cross-file dependency shape; the
exact SUT namespace string is decided when the SUT file is converted.
No `as` alias / partial import is used in this file → simple `using`
form suffices.

### 2.3 `dart.package_test.main_entrypoint`
`void main() { group('BootLoader', () { ... }); }` → ELIMINATED.
xUnit discovers `[Fact]` methods by reflection, no per-file entrypoint
exists. The outer group's body becomes the enclosing test class
(see 2.4). The omission is lossless because `main`'s body is exactly
one `group(...)` call with no other statements.

### 2.4 `dart.package_test.group_block`
Outer `group('BootLoader', …)` + 3 inner groups → ONE flat xUnit test
class `BootLoaderTests` containing all 12 test methods. Topology rules:

- Each test method carries `[Trait("Group", "<inner-group-label>")]`
  preserving the inner-group label verbatim (`"valid boot files"`,
  `"error cases"`, `"real file content"`).
- Each test method carries `[Fact(DisplayName = "<original test
  label>")]` preserving the human-readable Dart test label.
- The C# method NAME is the group-prefixed PascalCased label, e.g.
  `ValidBootFiles_ParsesThreeAgentBootClause`,
  `ErrorCases_ThrowsIfNoProcedureBootDeclaration`,
  `RealFileContent_ParsesPlayAliceBobCharlieActorBootGlpContent`.
  Prefixing prevents same-label collisions across inner groups.

FLATTEN over nested-classes / `IClassFixture` is chosen because every
inner group reads the same `loader` set up by the outer `setUp` —
splitting classes would duplicate the field.

### 2.5 `dart.package_test.late_field_in_group`
`late BootLoader loader;` (closed-over by setUp + every test) →
`private BootLoader _loader = null!;` instance field on
`BootLoaderTests`. The constructor (2.6) assigns it before any reader
runs, semantically equivalent to Dart `late + setUp`. The alternative
`BootLoader? _loader;` + `!` at every read site is REJECTED (inverts
the "guaranteed-initialised" contract that `late` encodes).

### 2.6 `dart.package_test.setUp_block`
`setUp(() { loader = BootLoader(); });` → xUnit class constructor:
```
public BootLoaderTests() { _loader = new BootLoader(); }
```
xUnit instantiates the test class once per test method (constructor-
per-test isolation), matching `package:test`'s per-test fresh-state
semantics exactly. NO `[SetUp]` attribute (that is NUnit's idiom). No
`tearDown` is present → no `IDisposable.Dispose` emitted.

### 2.7 `dart.package_test.test_call_simple` (×12)
Each `test('<label>', () { … })` → a `public void` instance method on
`BootLoaderTests` decorated with `[Fact(DisplayName = "<label>")]` and
`[Trait("Group", "<inner-group-label>")]`. Body translates statement-
for-statement:

- Arrange: the triple-quoted string literal `source` → C# raw string
  literal `var source = """…""";` (see 2.9).
- Act (valid cases): `final config = loader.load(source);` →
  `var config = _loader.Load(source);`.
- Act (error cases): `() => loader.load(source)` → `() =>
  _loader.Load(source)` inside an `Assert.Throws<>` call (see 2.11).
- Assert: matcher-by-matcher per 2.8–2.12.

All 12 are synchronous → none target `async Task`.

### 2.8 `dart.package_test.expect_equals`
`expect(<actual>, equals(<expected>));` →
`Assert.Equal(<expected>, <actual>);` (ARGUMENT ORDER FLIPS — Dart
puts actual first, xUnit puts expected first). Applied throughout
this file to `int` (list `.Count` via `.length`) and `String`
(`.AgentId`, `.GoalFunctor`, `.FullSource`) — both value-typed in
both languages, so default `Equals`/`IEquatable<T>.Equals`
semantics match Dart `==`.

Property-name nuance: `config.directives.length` → `config.Directives.
Count` (`List<T>.Count` in C# replaces Dart `List.length`). The Dart
camelCase `agentId`/`goalFunctor`/`fullSource` → C# PascalCase
`AgentId`/`GoalFunctor`/`FullSource` (handled when `BootDirective` /
`BootConfig` themselves are converted — this spec records only the
call-site shape).

### 2.9 `dart.string.triple_quoted_raw_literal`
Each `'''…'''` fixture → C# 11 raw string literal `"""…"""`. No
escape processing, multi-line, leading-indent normalisation matched
to the closing delimiter column. Codegen MUST emit the closing
`"""` at column 0 (or match the payload indentation) so the literal
payload is byte-identical to the Dart source. Pre-C#11 fallback:
verbatim `@"…"` — equivalent here because no fixture contains a `"`.

### 2.10 `dart.package_test.expect_isNot_contains` + `contains`
`expect(s, isNot(contains('x')));` → `Assert.DoesNotContain("x", s);`
(xUnit has no compositional `isNot`; the dedicated assertion is used).
`expect(s, contains('x'));` → `Assert.Contains("x", s);`. Both forms
appear in this file (test "preserves full source and strips boot
clause").

### 2.11 `dart.package_test.expect_throwsA_isA_having` (×5)
```
expect(
  () => loader.load(source),
  throwsA(isA<BootLoaderException>().having(
    (e) => e.message, 'message', contains('<substr>'))),
);
```
→
```
var ex = Assert.Throws<BootLoaderException>(() => _loader.Load(source));
Assert.Contains("<substr>", ex.Message);
```

Exact-vs-subtype nuance: `isA<T>` is subtype-tolerant (faithful match
= `Assert.ThrowsAny<T>`), but `BootLoaderException` has no known
subclasses in this codebase, so `Assert.Throws<T>` is observably
equivalent and emitted. Property mapping: Dart `e.message` (camelCase
exception convention) → C# `ex.Message` (PascalCase from
`System.Exception.Message`).

### 2.12 `dart.package_test.expect_isTrue`
`expect(<bool-expr>, isTrue);` → `Assert.True(<bool-expr>);`. Used
once: `expect(config.directives.every((d) => d.goalFunctor ==
'agent_init'), isTrue);` → `Assert.True(config.Directives.All(d =>
d.GoalFunctor == "agent_init"));`. (LINQ `All` = Dart `every`.)

### 2.13 `dart.iterable.map_tolist_equals`
`expect(config.directives.map((d) => d.agentId).toList(),
equals(['alice', 'bob', 'charlie']));` →
```
Assert.Equal(
    new[] { "alice", "bob", "charlie" },
    config.Directives.Select(d => d.AgentId).ToList());
```
LINQ mapping table (recorded once, reused everywhere): Dart
`Iterable.map` = `Select`; `Iterable.toList` = `ToList`;
`Iterable.every` = `All`; `Iterable.any` = `Any`. `Assert.Equal`
over `IEnumerable<T>` does element-wise equality matching Dart
`equals` over `List`. Materialisation via `.ToList()` is preserved
(matches Dart eager `.toList()` and produces identical diagnostic
output).

### 2.14 Conversion units (recap from convspec §conversion_units)
- **cu-1** — file-scope `using`s (`Xunit`, `System`, `System.Linq`,
  SUT namespace).
- **cu-2** — namespace declaration `<RootNs>.Test.Multiagent`.
- **cu-3** — top-level test class `BootLoaderTests`.
- **cu-4** — `private BootLoader _loader = null!;` field.
- **cu-5** — `public BootLoaderTests() { _loader = new BootLoader(); }`.
- **cu-6** — 6 `[Fact]` methods, `[Trait("Group", "valid boot files")]`.
- **cu-7** — 5 `[Fact]` methods, `[Trait("Group", "error cases")]`,
  Throws-then-`Assert.Contains` pattern.
- **cu-8** — 1 `[Fact]` method, `[Trait("Group", "real file content")]`,
  LINQ `Select`/`All` for collection assertions.
- **cu-9** — raw-string-literal payloads (`"""…"""`) at the correct
  column for every embedded `.glp` source fixture (byte-identical
  preservation).

## 3. Decomposed Task Units

- **T1**: emit cu-1 file-scope using directives (`Xunit`, `System`,
  `System.Linq`, SUT namespace). done.
- **T2**: emit cu-2 namespace `<RootNs>.Test.Multiagent { … }`. done.
- **T3**: emit cu-3 `public class BootLoaderTests` skeleton. done.
- **T4**: emit cu-4 `private BootLoader _loader = null!;` field. done.
- **T5**: emit cu-5 constructor `public BootLoaderTests() { _loader =
  new BootLoader(); }`. done.
- **T6**: emit the 6 cu-6 `[Fact]` methods of "valid boot files"
  (group-prefixed PascalCased names, `[Fact(DisplayName=…)]`,
  `[Trait("Group", "valid boot files")]`, raw-string fixtures, flipped
  `Assert.Equal(expected, actual)`, plus
  `Assert.DoesNotContain`/`Assert.Contains` for the strip-source
  test). done.
- **T7**: emit the 5 cu-7 `[Fact]` error-case methods (`[Trait("Group",
  "error cases")]`, Throws-then-`Assert.Contains(<substr>, ex.Message)`
  pattern, `Assert.Throws<BootLoaderException>` exact-type form). done.
- **T8**: emit the 1 cu-8 `[Fact]` real-file-content method
  (`[Trait("Group", "real file content")]`, LINQ
  `Select(d => d.AgentId).ToList()` + `Assert.Equal(new[]{…}, …)` +
  `Assert.True(config.Directives.All(d => d.GoalFunctor ==
  "agent_init"))`). done.
- **T9**: emit cu-9 raw-string-literal payloads `"""…"""` at column 0
  (or matched indent) for every embedded `.glp` fixture, byte-identical
  to the Dart source. done.

## 4. Research Findings

none required — every construct decision in the convspec carries an
`rf-…` research_finding_id grounded in the convspec rationale section
(xUnit v3 docs, Microsoft Learn C# null-safety + raw-strings + LINQ
references, Dart `package:test` README) and is verbatim-derivable.

## 5. Consistency Pass

- 2.1 (xUnit framework pin) — fixed; derived from convspec construct
  `dart.package_test.import_directive` + rationale §"Why xUnit (FR-024
  official-docs authoritative)" (precedent file
  `mad_error_handling_test.dart.md`).
- 2.2 (SUT `using`) — fixed; derived from convspec construct
  `dart.package_under_test.import_directive` nuance.
- 2.3 (drop `main`) — fixed; derived from convspec construct
  `dart.package_test.main_entrypoint` (lossless because body is one
  `group(...)`).
- 2.4 (FLATTEN topology + `[Trait]` + group-prefixed names + DisplayName)
  — fixed; derived from convspec construct `dart.package_test.group_block`
  nuance + rationale §"Nested-group topology: FLATTEN with `[Trait]`".
- 2.5 (`null!` field) — fixed; derived from convspec construct
  `dart.package_test.late_field_in_group` nuance + rationale §"`late`
  field + `setUp` -> constructor + `null!`".
- 2.6 (constructor-as-setUp, no `[SetUp]`, no `Dispose`) — fixed;
  derived from convspec construct `dart.package_test.setUp_block`.
- 2.7 (per-test method shape, all sync) — fixed; derived from convspec
  construct `dart.package_test.test_call_simple` + observed in §1
  inventory (no `async` anywhere).
- 2.8 (`Assert.Equal` argument flip + property casing) — fixed; derived
  from convspec construct `dart.package_test.expect_equals` + rationale
  §"Argument-order flip on `Assert.Equal`". Property-PascalCasing is a
  C# naming convention noted in the convspec nuance for
  `expect_throwsA_isA_having` (`e.message` → `ex.Message`); the same
  rule applies to the SUT type properties (`agentId` → `AgentId`,
  `goalFunctor` → `GoalFunctor`, `fullSource` → `FullSource`,
  `directives.length` → `Directives.Count`).
- 2.9 (`"""…"""` raw strings, column-0 closer) — fixed; derived from
  convspec construct `dart.string.triple_quoted_raw_literal` +
  rationale §"Triple-quoted multi-line string fixtures".
- 2.10 (`DoesNotContain` / `Contains`) — fixed; derived from convspec
  construct `dart.package_test.expect_isNot_contains`.
- 2.11 (Throws-then-`Assert.Contains`) — fixed; derived from convspec
  construct `dart.package_test.expect_throwsA_isA_having` + rationale
  §"`throwsA(isA<T>().having(...))` -> Throws-then-Assert"; exact-vs-
  subtype call (`Assert.Throws<T>` chosen over `Assert.ThrowsAny<T>`)
  recorded in convspec as in-file-justified.
- 2.12 (`Assert.True` + LINQ `All`) — fixed; derived from convspec
  construct `dart.package_test.expect_isTrue` + rationale §"LINQ
  mappings (in-file uses)".
- 2.13 (`Select`/`ToList` + `Assert.Equal` collection) — fixed;
  derived from convspec construct `dart.iterable.map_tolist_equals` +
  rationale §"LINQ mappings (in-file uses)".

## 6. Escalations

None.
