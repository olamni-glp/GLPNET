---
path: test/glp_runtime_test.dart
cycle_group_id: 125
scc_siblings: []
generated_at: 2026-05-21T14:51:58Z
source_sha256: e69ec0f7f9041d7bb48efd7c5f4ded57459daa5ddb952de7cf4e3beabb451887
schema_version: 1
---

# Conversion Plan: test/glp_runtime_test.dart

## 1. Source Analysis

The Dart source file is a 9-line `package:test` smoke test for the
`glp_runtime` package's top-level `calculate()` function. Verbatim
contents (line-numbered as read):

```dart
1  import 'package:glp_runtime/glp_runtime.dart';
2  import 'package:test/test.dart';
3
4  void main() {
5    test('calculate', () {
6      expect(calculate(), 42);
7    });
8  }
9
```

Structural inventory (only what is actually present in the source):

- **Line 1** — internal-package import directive
  `import 'package:glp_runtime/glp_runtime.dart';` (same-package
  import bringing the top-level `calculate()` function into lexical
  scope as an unqualified identifier).
- **Line 2** — test-framework import directive
  `import 'package:test/test.dart';` (brings `test`, `expect`, and the
  matcher surface into scope).
- **Line 4** — `void main()` declaration — the test-registration root
  required by `package:test`.
- **Line 5** — single `test('calculate', () { ... })` registration
  call: positional `String description` (`'calculate'`) + closure
  `dynamic Function()` test body.
- **Line 6** — single assertion `expect(calculate(), 42);` using the
  BARE-VALUE matcher form (`expect`'s second argument is the integer
  literal `42`, not a `Matcher` — `package:test_api` auto-wraps it as
  `equals(42)`).
- **Lines 7–8** — closing braces; line 9 trailing newline.

Surface NOT present in this file (recorded so the conversion does NOT
synthesise unjustified scaffolding):

- No `async` / `Future` / `Stream` / `Completer` / `Timer` / isolate
  surface (the test body is fully synchronous).
- No `setUp` / `tearDown` / `setUpAll` / `tearDownAll` / `group` —
  exactly one top-level `test()` call.
- No `late` / `mixin` / `extension` / generics on the SUT (Dart
  `calculate()` is non-generic and takes no arguments).
- No `sealed` / `abstract` / bitwise / shift operators.
- No null-safety surface (`int` is non-nullable; `calculate()`'s
  return is non-nullable; the literal `42` is non-nullable).
- No multiple assertions, no per-test fixtures, no shared state.

Single dependency (per tombstone `dependencies` list):
`lib/glp_runtime.dart` (resolves the symbol `calculate`).

## 2. Dart → C#/.NET Conversion Plan

This section mirrors the four `constructs` rows in the ratified
convspec verbatim (FR-011, FR-012 / SC-007 KB-reuse). Each Dart
construct maps to its C#/.NET decision exactly as the convspec
records it; no decisions are introduced here that are not derivable
from convspec + sibling lib spec + sibling smoke_test.dart spec.

### Construct 1 — `dart.package_test.import_directive`

- **Dart source form**: `import 'package:test/test.dart';`
- **C# decision**: Drop the Dart import directive entirely; emit
  `using Xunit;` at the file level. xUnit is the project's chosen
  .NET test framework, settled batch-wide in the sibling spec
  `.codeconv/conversion-specs/test/smoke_test.dart.md`
  (rf-dart-package-test-to-dotnet-xunit). Per FR-012 / SC-007 this
  finding is REUSED, not re-researched.
- **Out of scope (langpair-level)**: the `.csproj` NuGet wiring for
  `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` is
  NOT part of this per-file artifact — same out-of-scope boundary as
  the sibling spec.

### Construct 2 — `dart.internal_package_import.same_package`

- **Dart source form**: `import 'package:glp_runtime/glp_runtime.dart';`
- **C# decision**: Drop the Dart import directive; emit
  `using <GlpRuntimeRootNamespace>;` at the file level, where
  `<GlpRuntimeRootNamespace>` is the exact namespace identifier
  emitted by the converted lib spec
  `.codeconv/conversion-specs/lib/glp_runtime.dart.md` (the host
  static class `GlpRuntimeRoot` lives in that namespace). The test
  body then qualifies the call as `GlpRuntimeRoot.Calculate()`. Spec
  default: emit the regular `using` (NOT `using static
  <ns>.GlpRuntimeRoot;`) — qualification matches the Dart source's
  intent of naming the function explicitly and follows Microsoft's
  C# Coding Conventions preference for type-qualified static-member
  access in non-helper cases.
- **Visibility carry-forward**: Dart top-level `calculate` (no
  leading underscore) is library-public ⇒ `public` in C# (per the lib
  spec).

### Construct 3 — `dart.test_file.void_main_as_test_registration_root`

- **Dart source form**: `void main() { test('...', () { ... }); }`
- **C# decision**: Eliminate `void main()` entirely; lift the single
  registered `test()` call into one `[Fact]`-attributed public
  instance method on a `public class GlpRuntimeTest` (file name
  `glp_runtime_test.dart` ⇒ class `GlpRuntimeTest` in
  `GlpRuntimeTest.cs`). The Dart test description `'calculate'`
  becomes method identifier `Calculate` (PascalCased) carrying
  `[Fact(DisplayName = "calculate")]` to preserve the original
  reporting name. xUnit's discovery is attribute-driven; the Dart
  `main()` registration pass has no xUnit equivalent and is dropped.
  Fresh instance per `[Fact]` (xunit.net "Shared Context between
  Tests") — recorded as nuance, does not fire here (no shared state).
  REUSE of sibling-spec idiom rf-dart-test-main-to-xunit-class-with-facts.

### Construct 4 — `dart.package_test.expect_value_equals_matcher`

- **Dart source form**: `expect(calculate(), 42);`
- **C# decision**: Translate the bare-value `expect` form into
  `Assert.Equal(42L, GlpRuntimeRoot.Calculate());`. Three load-bearing
  details (all from the convspec, none invented here):
  1. **Argument-order swap** (footgun): xUnit
     `Assert.Equal<T>(T expected, T actual)` is EXPECTED-FIRST; Dart
     `expect(actual, matcher)` is ACTUAL-FIRST. The conversion
     explicitly swaps the operands so failure diagnostics read
     correctly (Expected: 42, Actual: <calculate-result>).
  2. **`long` literal `42L`**: per the lib spec
     (rf-dart-int-to-csharp-long-width) Dart `int` ⇒ C# `long`, so
     `Calculate()` returns `long`. The C# emission uses the `long`
     literal `42L` so the generic `Assert.Equal<T>` binds with
     `T = long` unambiguously (no implicit `int`→`long` promotion).
  3. **Bare-value matcher semantics**: `package:test_api` auto-wraps
     non-`Matcher` second arguments as `equals(value)`; `equals` uses
     `==` on primitives. xUnit `Assert.Equal<T>` for value types uses
     `EqualityComparer<T>.Default.Equals`, which for `long` reduces
     to value equality — semantically identical. Both runners surface
     a typed failure exception (`TestFailure` Dart-side,
     `Xunit.Sdk.EqualException` xUnit-side); semantics preserved.

## 3. Decomposed Task Units

- **T1** — Emit file-level `using Xunit;` directive (replaces the
  Dart `import 'package:test/test.dart';`). Construct 1. — done by spec
  decision.
- **T2** — Emit file-level
  `using <GlpRuntimeRootNamespace>;` directive (replaces the Dart
  `import 'package:glp_runtime/glp_runtime.dart';`; the exact
  namespace identifier mirrors the converted lib spec's emission).
  Construct 2. — done by spec decision.
- **T3** — Emit `public class GlpRuntimeTest` host class (no base
  class, no constructor, no `IDisposable`, no `IAsyncLifetime` — no
  shared state in the source). Construct 3. — done by spec decision.
- **T4** — Emit one `[Fact(DisplayName = "calculate")] public void
  Calculate()` method on `GlpRuntimeTest` (synchronous `void`, not
  `async Task` — no async surface in the source). Construct 3. — done
  by spec decision.
- **T5** — Emit the method body
  `Assert.Equal(42L, GlpRuntimeRoot.Calculate());` (EXPECTED-FIRST
  argument order; `42L` long literal; qualified call to the lib
  spec's host static class). Construct 4. — done by spec decision.
- **T6** — Confirm NO `void main()` (or any other entrypoint) is
  emitted — xUnit discovery is attribute-driven, registration-via-main
  is dropped entirely. Construct 3. — done by spec decision.
- **T7** — Confirm `.csproj` / NuGet wiring is NOT touched by this
  per-file artifact (langpair-level concern, recorded out of scope by
  the convspec and the sibling smoke_test.dart spec). — done by spec
  decision.

## 4. Research Findings

none required — all four conversion decisions are verbatim-derivable
from the ratified convspec at
`.codeconv/conversion-specs/test/glp_runtime_test.dart.md`, which
itself authoritatively reuses two sibling specs
(`.codeconv/conversion-specs/test/smoke_test.dart.md` for the
test-framework + main-lift + matcher-routing idioms, and
`.codeconv/conversion-specs/lib/glp_runtime.dart.md` for the
`Calculate` symbol + namespace + `int`⇒`long` decisions) per FR-012 /
SC-007 KB-reuse.

## 5. Consistency Pass

- **Construct 1 (test-framework import)** — fixed — derived from
  convspec construct `dart.package_test.import_directive` and
  rf-dart-package-test-to-dotnet-xunit (KB-reused from sibling
  `.codeconv/conversion-specs/test/smoke_test.dart.md`).
- **Construct 2 (internal package import)** — fixed — derived from
  convspec construct `dart.internal_package_import.same_package` and
  rf-dart-same-package-import-to-csharp-using (authoritative both
  sides: Dart language tour for `package:` URIs;
  learn.microsoft.com C# `using` directive reference + C# Coding
  Conventions; lib spec `.codeconv/conversion-specs/lib/glp_runtime.dart.md`
  for the `GlpRuntimeRoot` host class + namespace).
- **Construct 3 (void main ⇒ class with [Fact])** — fixed — derived
  from convspec construct
  `dart.test_file.void_main_as_test_registration_root` and
  rf-dart-test-main-to-xunit-class-with-facts (KB-reused from sibling
  smoke_test.dart spec; file-name lift `glp_runtime_test.dart` ⇒
  `GlpRuntimeTest`; DisplayName preserves `'calculate'`).
- **Construct 4 (expect bare-value ⇒ Assert.Equal)** — fixed —
  derived from convspec construct
  `dart.package_test.expect_value_equals_matcher` and
  rf-dart-expect-bare-value-to-xunit-assert-equal (argument-order
  swap, `42L` long literal carrying forward
  rf-dart-int-to-csharp-long-width from the lib spec, bare-value
  matcher auto-wrap per `package:test_api`).
- **Cross-construct check** — the qualified call
  `GlpRuntimeRoot.Calculate()` in Construct 4's emission is
  consistent with Construct 2's decision to emit a regular `using`
  (not `using static`); the `[Fact]` method name `Calculate` does NOT
  collide at the call site because the test class
  (`GlpRuntimeTest.Calculate`) and the host class
  (`GlpRuntimeRoot.Calculate`) live on different types — convspec
  nuance explicitly recorded.
- **Non-surface check** — no async / nullable / generics / shared
  state in the source ⇒ no `async Task` / `?` / type-parameter /
  `IDisposable` / `IAsyncLifetime` surface in the emission. All
  construct decisions are internally consistent and the conversion
  faithfully reproduces the source's nine-line shape.

## 6. Escalations

None.
