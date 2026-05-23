---
path: test/smoke_test.dart
cycle_group_id: 158
scc_siblings: []
generated_at: 2026-05-21T14:46:22Z
source_sha256: b0355fee58d4216a3a181c91b018351513d8928d2bc9d78f6fb22eaf98748f7a
schema_version: 1
---

# Conversion Plan: test/smoke_test.dart

## 1. Source Analysis

Inspected `glp_runtime_net/test/smoke_test.dart` directly (7 source lines + 1 trailing newline; sha256 `b0355fee58d4216a3a181c91b018351513d8928d2bc9d78f6fb22eaf98748f7a` matches both the tombstone front-matter and the convspec `source_sha256`). The file contains exactly three source-level constructs:

1. A single import directive — `import 'package:test/test.dart';` — pulling in `package:test`'s top-level test-registration API (`test`, `expect`, `isTrue`). No other imports; no part-of directive; no library declaration.
2. A single `void main()` function whose body is exactly one call to `test('project skeleton exists', () { expect(true, isTrue); });`. The `main()` here serves `package:test`'s registration-via-`main` execution model (running `main()` registers test closures with the runner, which then executes them).
3. A single assertion `expect(true, isTrue);` inside the test closure: the canonical degenerate-but-load-bearing smoke check that proves the test runner is alive, the assertion path is wired, and any framework plumbing break would surface.

Surface absent from the file (load-bearing in the conversion sense because their absence narrows the target shape): no `setUp`/`tearDown`/`group`/`setUpAll`/`tearDownAll`; no `async`/`Future`/`Stream`/`Completer`/`Timer`; no `late`/`mixin`/`extension`/generics/sealed/abstract; no isolate, RPC, or stream-matcher surface; no custom matcher; no tagging; no nullable surface; no allocations beyond the string literal `'project skeleton exists'`. The Dart source is fully synchronous and single-statement-bodied.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim in its target decision; the plan adds a per-construct execution outline (file/line emission shape, ordering invariants) without altering any decision.

### 2.1 `dart.package_test.import_directive` — `import 'package:test/test.dart';` → `using Xunit;`

- **Target decision (mirrors convspec)**: drop the Dart `import 'package:test/test.dart';` directive; emit the xUnit-equivalent file-level surface as `using Xunit;` (per Microsoft Learn xUnit getting-started docs). The .NET test project itself (a separate `.csproj` outside this file's conversion unit) MUST reference the `xunit` and `xunit.runner.visualstudio` (or `Microsoft.NET.Test.Sdk` + xUnit) NuGet packages — that project-file emission is OUT OF SCOPE for this single-file artefact (a sibling `langpair`-level concern, not per-file). The choice of xUnit (over MSTest or NUnit) is the batch-wide default for this conversion (recorded as the project's test-framework idiom; sibling test files in the same batch MUST reuse it via the idiom KB rather than re-deciding).
- **Emission shape**: one `using Xunit;` statement at the top of `SmokeTest.cs`, file-scoped (top-of-file, before the class declaration). No `namespace` declaration is mandated by the convspec; if a project-wide namespace idiom is in effect, codegen would honour it but no per-file namespace decision is asserted here.
- **Nuance preserved (mirrors convspec)**: Dart `import 'package:test/test.dart'` exposes top-level functions (`test`, `expect`, matchers like `isTrue`); xUnit has no top-level test function — tests are PUBLIC INSTANCE METHODS on a public test class, discovered by `[Fact]` attributes. So the import is not 1-to-1 replaced by `using Xunit;` alone — the import-plus-`main` shape on the Dart side becomes a class-plus-methods shape on the C# side (see §2.2 and §2.3). No async / Future / Stream surface in this file.

### 2.2 `dart.test_file.void_main_as_test_registration_root` — `void main() { test('...', () { ... }); }` → `public class SmokeTest { [Fact] public void ProjectSkeletonExists() { ... } }`

- **Target decision (mirrors convspec)**: eliminate the Dart `void main()` function entirely. In `package:test` the file-level `main()` is the test-registration entry point invoked by the test runner; xUnit has NO equivalent `main` — test discovery is attribute-driven on a public test class. The Dart `void main() { test('name', body); }` shape becomes a `public class SmokeTest { [Fact] public void ProjectSkeletonExists() { body } }` shape: each Dart `test(...)` call in `main()` lifts into one `[Fact]`-attributed public instance method on the class; the body of the Dart callback becomes the body of the C# method (verbatim translation of the assertions). The class name `SmokeTest` mirrors the file name (`smoke_test.dart` → `SmokeTest.cs` → `class SmokeTest`), a consistent .NET-test convention.
- **Emission shape**: after the `using Xunit;` directive (§2.1), emit a single `public class SmokeTest { ... }`. Inside it, emit exactly one `[Fact]`-attributed `public void ProjectSkeletonExists()` method. The `[Fact]` attribute MUST include `DisplayName = "project skeleton exists"` (the original Dart test-name string verbatim) so the test runner's report preserves human-readable fidelity — i.e. `[Fact(DisplayName = "project skeleton exists")]`. The method body is the body of the Dart inner closure (one statement: see §2.3).
- **Name mapping (mirrors convspec)**: Dart's string-identified test name `'project skeleton exists'` (human-readable, with spaces) becomes a C# method identifier `ProjectSkeletonExists` (PascalCased, no spaces — C# method identifiers cannot contain whitespace). The human-readable form is preserved via `[Fact(DisplayName = "project skeleton exists")]`.
- **Nuance preserved (mirrors convspec)**: Dart `package:test` discovers tests by EXECUTING `main()` (which calls `test()` to register closures); xUnit discovers tests by REFLECTION over `[Fact]` (and `[Theory]`) attributes — no equivalent registration pass exists, and there is no place to put cross-test imperative setup beyond constructor / `IClassFixture<T>` / `IAsyncLifetime` (none used here — the source has no setUp/tearDown). Constructor semantics: xUnit creates a FRESH instance of the test class per `[Fact]` invocation, so any per-test setup goes in the constructor and any teardown in `IDisposable.Dispose` — neither needed for this one-assertion smoke test. The Dart inner closure `() { expect(true, isTrue); }` translates 1-to-1 into the body of the `[Fact]` method; no closure object is materialised in C# because the body executes directly. No async: the closure is synchronous, so the method returns `void` (xUnit also supports `async Task` for async tests; not applicable here). Reference vs value: no allocations in either source or target beyond the string literals and the test class instance itself. Null-safety: no nullable surface in this file.

### 2.3 `dart.package_test.expect_true_isTrue_matcher` — `expect(true, isTrue);` → `Assert.True(true);`

- **Target decision (mirrors convspec)**: translate `expect(true, isTrue)` into `Assert.True(true);` (per xunit.net `Assert.True(bool)` — passes when the argument is true, otherwise throws `Xunit.Sdk.TrueException`). The Dart matcher `isTrue` is from `package:matcher` (re-exported by `package:test`) and asserts strict boolean truth (not truthiness — Dart booleans are strict). xUnit's `Assert.True` mirrors that exactly: `bool` argument only, no truthy/coercion surface.
- **Emission shape**: a single statement `Assert.True(true);` as the entire body of the `[Fact]` method emitted in §2.2. No `userMessage` overload (the smoke check is sufficient as-is; the convspec records the optional `Assert.True(bool, string)` overload exists but spec default = no custom message).
- **Nuance preserved (mirrors convspec)**: Dart's `expect(actual, matcher)` is a TWO-ARGUMENT shape where the second argument is a matcher object; xUnit's `Assert` is a ONE-OR-TWO-ARGUMENT shape per assertion method (no matcher object; the assertion is encoded by the method name itself). The conversion ROUTES each Dart matcher to its xUnit counterpart: `isTrue` ⇒ `Assert.True(actual)`. The broader recorded routing-table idiom (for sibling test-file conversions in this batch) is: `isTrue` ⇒ `Assert.True`, `isFalse` ⇒ `Assert.False`, `isNull` ⇒ `Assert.Null`, `isNotNull` ⇒ `Assert.NotNull`, `equals(x)` ⇒ `Assert.Equal(x, actual)` — with the explicit argument-order-swap footgun: xUnit `Assert.Equal<T>(T expected, T actual)` has EXPECTED-FIRST then ACTUAL, opposite of Dart `expect(actual, equals(expected))`. This file uses only `isTrue` (single-argument; unaffected by the swap) but the warning is recorded for sibling conversions. Exception-on-failure semantics: Dart `expect` throws `TestFailure` (subclass of `Exception`) on mismatch; xUnit `Assert.True` throws `Xunit.Sdk.TrueException` (subclass of `Xunit.Sdk.XunitException` → `Exception`). Both are caught by the respective runner — semantically equivalent. Reference/value: `bool` is a value type in both languages; no boxing.

### 2.4 Target file structure (composition of §2.1–§2.3)

The emitted `test/SmokeTest.cs` is a single file composed in the following deterministic order (matches convspec `conversion_units` exactly):

1. `using Xunit;` — file-level using directive replacing `import 'package:test/test.dart';` (§2.1).
2. `public class SmokeTest { ... }` — single public test class; name mirrors the `.dart` file name; no base class needed for this smoke case (§2.2).
3. Inside the class: `[Fact(DisplayName = "project skeleton exists")] public void ProjectSkeletonExists() { ... }` — one Fact-attributed method per Dart `test()` call; `DisplayName` preserves the original human-readable test name (§2.2).
4. Method body: `Assert.True(true);` — 1-to-1 translation of `expect(true, isTrue)` — xUnit `Assert.True` with the literal boolean (§2.3).
5. NO equivalent of Dart's `void main()` — xUnit discovery is attribute-driven; registration-via-`main` is dropped entirely (§2.2).

## 3. Decomposed Task Units

- T1: Emit file-level `using Xunit;` directive replacing the Dart `import 'package:test/test.dart';` (covers convspec construct `dart.package_test.import_directive`) — done.
- T2: Emit `public class SmokeTest` declaration as the single top-level type, name mirroring `smoke_test.dart` → `SmokeTest.cs` (covers convspec construct `dart.test_file.void_main_as_test_registration_root` — class half) — done.
- T3: Emit `[Fact(DisplayName = "project skeleton exists")] public void ProjectSkeletonExists()` method signature inside `SmokeTest`, lifting the one Dart `test('project skeleton exists', ...)` registration into a `[Fact]` method; PascalCase identifier; `DisplayName` preserves the original test-name string (covers convspec construct `dart.test_file.void_main_as_test_registration_root` — method half) — done.
- T4: Emit `Assert.True(true);` as the entire body of `ProjectSkeletonExists()`, translating `expect(true, isTrue)` 1-to-1 with no custom user message (covers convspec construct `dart.package_test.expect_true_isTrue_matcher`) — done.
- T5: Drop the Dart `void main()` function entirely — no C# counterpart emitted (xUnit discovery is attribute-driven; covers convspec construct `dart.test_file.void_main_as_test_registration_root` — drop-`main()` half) — done.
- T6: Record (no emission needed at the per-file level) that the sibling `.csproj` MUST reference `xunit` + `xunit.runner.visualstudio` (or `Microsoft.NET.Test.Sdk` + xUnit) NuGet packages; project-file emission is langpair-level, not per-file (covers the OUT-OF-SCOPE clause of convspec construct `dart.package_test.import_directive`) — done.

## 4. Research Findings

None required. Every construct's target decision and nuance is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/test/smoke_test.dart.md`, which already cites three authoritative research findings (`rf-dart-package-test-to-dotnet-xunit`, `rf-dart-test-main-to-xunit-class-with-facts`, `rf-dart-expect-isTrue-to-xunit-assert-true`) with provenance from pub.dev (`package:test`, `package:matcher`), Microsoft Learn (xUnit unit-testing tutorial), and xunit.net (Assert API + shared-context lifecycle). No additional research is required for this file's three trivial constructs.

## 5. Consistency Pass

Cross-checked the plan against the ratified convspec construct-by-construct:

- Convspec construct `dart.package_test.import_directive` ↔ plan §2.1 + T1 + T6: target decision (`using Xunit;` + drop import + .csproj-emission out-of-scope) mirrored verbatim — derived from convspec `target_decision` + `nuance`.
- Convspec construct `dart.test_file.void_main_as_test_registration_root` ↔ plan §2.2 + T2 + T3 + T5: target decision (drop `main()`, emit `public class SmokeTest` with one `[Fact(DisplayName = "project skeleton exists")] public void ProjectSkeletonExists()` method) mirrored verbatim — derived from convspec `target_decision` + `nuance` (per-test-instance lifecycle, name mapping, sync `void`, `DisplayName` preservation).
- Convspec construct `dart.package_test.expect_true_isTrue_matcher` ↔ plan §2.3 + T4: target decision (`Assert.True(true);` 1-to-1, no custom message, matcher-routing table preserved for sibling reuse, argument-order-swap footgun preserved) mirrored verbatim — derived from convspec `target_decision` + `nuance`.
- Convspec `conversion_units` (5-item list) ↔ plan §2.4 (5-item composition): matches 1-to-1 in count and order.
- Convspec `escalations: []` ↔ plan §6: both empty. Tombstone `open_escalation_count: 0` also concurs.
- Convspec `target_code_unit: test/SmokeTest.cs` ↔ tombstone `target_path: test/smoke_test.cs`: the tombstone's scaffold-produced path uses lowercase-with-underscore (a feature-016 scaffold naming convention recorded in the langpair); the convspec's `target_code_unit` uses PascalCased filename per .NET file-naming convention. Both refer to the same target file; the discrepancy is naming-convention-level (langpair-policy, not per-file) and out-of-scope for this artefact's decisions — no escalation needed. The plan does not assert a filename; it asserts only the file's emitted contents.
- No other gaps surfaced.

## 6. Escalations

None.
