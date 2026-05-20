> Conversion-spec artifact for test/glp_runtime_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/glp_runtime_test.dart
source_sha256: e69ec0f7f9041d7bb48efd7c5f4ded57459daa5ddb952de7cf4e3beabb451887
target_code_unit: test/GlpRuntimeTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it at the file level with `using Xunit;` (the xUnit
      equivalent surface). REUSE the batch-wide test-framework idiom
      decided in the sibling spec
      `.codeconv/conversion-specs/test/smoke_test.dart.md` — xUnit is
      the project's chosen .NET test framework (Microsoft Learn's
      canonical .NET unit-testing tutorial walks through xUnit
      end-to-end at
      learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit;
      the .NET Foundation hosts xUnit at xunit.net). Per FR-012 / SC-007
      this construct is NOT re-researched in this file — it reuses the
      research finding recorded for the sibling test file. The .NET
      test project's `.csproj` (referencing `xunit`,
      `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) is OUT OF
      SCOPE for this per-file artifact — same langpair-level concern
      recorded in the sibling spec.
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice reuse (explicitly addressed, no re-derivation):
      the framework decision (xUnit vs MSTest vs NUnit) was settled in
      the sibling smoke_test.dart spec; this file is the second test
      file in the same conversion batch and MUST reuse the same
      framework via the KB rather than re-research it (FR-012). The
      module / discovery / lifecycle nuances (top-level `test()` ⇒
      `[Fact]` instance methods, fresh test-class instance per `[Fact]`
      per xunit.net "Shared Context between Tests", no top-level
      function surface in xUnit) carry forward verbatim from the
      sibling. No async / Future / Stream / isolate surface in this
      file either, so the synchronous `void`-returning `[Fact]` shape
      (not `async Task`) still applies. Strict-bool / strict-equality
      semantics are unaffected by the import directive itself.
  - construct_key: dart.internal_package_import.same_package
    source_form: "import 'package:glp_runtime/glp_runtime.dart';"
    target_decision: >-
      Drop the Dart `import 'package:glp_runtime/glp_runtime.dart';`
      directive and replace it with a C# `using` directive that brings
      the converted package's root namespace into scope so the test
      method body can name `Calculate` (the converted form of Dart's
      top-level `calculate()` — see
      `.codeconv/conversion-specs/lib/glp_runtime.dart.md`). The target
      symbol `Calculate` lives as a `public static` method on a
      distinctly-named host static class (`GlpRuntimeRoot` per the lib
      spec — chosen to avoid colliding with the package's root
      namespace), inside the converted package's namespace. The test
      file emits: `using <GlpRuntimeRootNamespace>;` (the exact
      namespace identifier mirrors the converted lib spec's emission;
      the load-bearing requirement is that the host static class is
      reachable so the test body can write `GlpRuntimeRoot.Calculate()`
      — or, with an unqualified `using static`, just `Calculate()`).
      Spec default: emit the qualified form
      (`GlpRuntimeRoot.Calculate()` in the method body), which does NOT
      require `using static` and matches the test source's intent of
      naming the function explicitly.
    idiom_id: null
    research_finding_id: rf-dart-same-package-import-to-csharp-using
    nuance: >-
      Module-system nuance (explicitly addressed): Dart `package:`
      imports are path-based and bring the imported library's top-level
      identifiers into the file's lexical scope directly (no
      qualification required) — `calculate()` after the import is an
      unqualified call. C# `using` directives bring NAMESPACES into
      scope, not individual top-level identifiers — there are no
      top-level identifiers in C# (every method must live in a type).
      So the round-trip is not a 1-to-1 replacement: the Dart import
      gives unqualified access to the function; the C# `using` gives
      access to the host class which the test body must then qualify
      (`HostClass.Method()`), unless the conversion also emits a
      `using static <ns>.<HostClass>;` directive (which would
      unqualify `Calculate` at the call site). Spec default = the
      qualified form (no `using static`), because (a) the converted
      identifier `Calculate` is unique enough to be unambiguous either
      way, (b) qualification makes the cross-file dependency explicit
      at the call site (Microsoft's C# Coding Conventions guidance at
      learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
      recommends using static members via the type name for clarity in
      typical cases — `using static` is appropriate for ubiquitous
      helper types like `Math`, not for one-off package roots).
      Visibility: Dart top-level `calculate` (no leading underscore) is
      library-public — same mapping as the lib spec ⇒ `public`. No
      cross-package, cross-isolate, or transitive-export semantics
      apply to this single internal import. Same-batch reuse: this
      idiom row is recorded so any subsequent internal-package test
      import in the same conversion batch reuses it via the KB rather
      than re-deriving the module mapping.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely and lift the
      single registered `test(...)` call into one `[Fact]`-attributed
      public instance method on a `public class GlpRuntimeTest`
      (mirroring the file name `glp_runtime_test.dart` ⇒
      `GlpRuntimeTest.cs`). The Dart test name `'calculate'` becomes
      method identifier `Calculate` (PascalCased) with
      `[Fact(DisplayName = "calculate")]` to preserve the original
      reporting name. Body of the Dart closure becomes the body of the
      C# `[Fact]` method, verbatim modulo the assertion translation
      handled by the `expect`-matcher construct below. REUSE the
      idiom recorded in the sibling smoke_test.dart spec — same
      structural lift; no re-research (FR-012).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery-model nuance (explicitly addressed, carry-forward from
      sibling): xUnit discovers tests by reflection over `[Fact]`
      attributes, with a FRESH instance of the test class per `[Fact]`
      invocation (xunit.net "Shared Context between Tests": "xUnit.net
      creates a new instance of the test class for every test that is
      run."). The Dart `main()` registration pass has no xUnit
      equivalent and is dropped. Name-mapping footgun: the Dart test
      name `'calculate'` collides on identifier shape with the function
      under test (`calculate`) — in C# both become `Calculate`, so the
      `[Fact]` method `Calculate` and the host method
      `GlpRuntimeRoot.Calculate` share an identifier but live on
      different types; no actual collision in the body because the call
      is qualified (`GlpRuntimeRoot.Calculate()`). `DisplayName` is
      preserved verbatim (`"calculate"`) for the test runner's report.
      No setUp / tearDown / group / async — synchronous `void` `[Fact]`,
      no constructor / `IDisposable.Dispose` / `IAsyncLifetime`
      surface. Per-test fresh-instance lifecycle nuance recorded but
      does not fire (no shared state).
  - construct_key: dart.package_test.expect_value_equals_matcher
    source_form: "expect(calculate(), 42);"
    target_decision: >-
      Translate the Dart `expect(actual, expected_value)` call (the
      bare-value matcher form, where the second argument is a VALUE
      rather than a `Matcher` object — `package:test`'s `expect`
      auto-wraps the value as `equals(value)`) into xUnit's
      `Assert.Equal(expected, actual)`. The bare-value form
      `expect(calculate(), 42)` is semantically `expect(calculate(),
      equals(42))` (per `package:test_api`'s `expect` documentation:
      "non-`Matcher` values are wrapped in `equals(value)`"). xUnit's
      counterpart per xunit.net `Assert` API is
      `public static void Equal<T>(T expected, T actual)` (with
      `IEquatable<T>`-based equality). Emitted call:
      `Assert.Equal(42L, GlpRuntimeRoot.Calculate());` — argument
      order is EXPECTED-FIRST (xUnit), the OPPOSITE of Dart's
      ACTUAL-FIRST `expect()` shape. The `L` suffix on `42` reflects
      `Calculate`'s `long` return type (per the lib spec's
      `rf-dart-int-to-csharp-long-width` mapping — Dart `int` ⇒ C#
      `long`); both arguments must be `long` for the generic `Equal<T>`
      overload to bind without an implicit-conversion warning. REUSE
      the `expect`-matcher-routing-table idiom row for `equals(x)` ⇒
      `Assert.Equal(x, actual)` that the sibling smoke_test.dart spec
      recorded as load-bearing context (the row was documented in that
      spec but did not fire there; this file is the first occurrence
      where the row actually applies).
    idiom_id: null
    research_finding_id: rf-dart-expect-bare-value-to-xunit-assert-equal
    nuance: >-
      Argument-order footgun (load-bearing, explicitly addressed):
      Dart `expect(actual, equals(expected))` is ACTUAL-FIRST; xUnit
      `Assert.Equal<T>(expected, actual)` is EXPECTED-FIRST — per
      xunit.net Assert API reference and the Microsoft Learn xUnit
      tutorial. A naive textual transposition that preserved positional
      order would emit `Assert.Equal(calculate(), 42)`, which would
      still pass on success but produce REVERSED diagnostic output on
      failure ("Expected: <calculate-result>, Actual: 42" instead of
      "Expected: 42, Actual: <calculate-result>"). The spec explicitly
      records the swap to prevent this. Integer-width nuance
      (carry-forward from the lib spec): Dart `int` literal `42` maps
      to C# `long` (64-bit) because the lib spec converts
      `calculate()`'s return type to `long`; the C# emission uses the
      `long` literal `42L` so the generic `Assert.Equal<T>` binds with
      `T = long` unambiguously (without the `L` the compiler would
      bind `T = long` via implicit `int`-to-`long` conversion on the
      first argument, which works, but emitting `42L` is explicit and
      avoids any reader doubt). Bare-value matcher semantics (Dart):
      `package:test_api`'s `expect` documents that non-`Matcher`
      values are wrapped in `equals(value)` — the bare `42` IS
      `equals(42)` for matching purposes. Strict equality: Dart
      `equals` (from `package:matcher`) uses `==` for primitives —
      strict numeric equality on `int`; xUnit `Assert.Equal<T>` for
      value types uses `EqualityComparer<T>.Default.Equals` (which for
      `long` is value-equality) — semantically identical. Failure
      exception: Dart `expect` throws `TestFailure`; xUnit
      `Assert.Equal` throws `Xunit.Sdk.EqualException` (subclass of
      `Xunit.Sdk.XunitException` → `Exception`) — both runner-caught,
      semantically equivalent. No null / nullable surface (`long` is
      a non-nullable value type, `calculate()` returns a non-nullable
      `long`); no reference-vs-value identity hazard.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "using <GlpRuntimeRootNamespace>; (file-level using directive replacing the internal `import 'package:glp_runtime/glp_runtime.dart';` — exact namespace identifier mirrors the converted lib spec's emission)"
  - "public class GlpRuntimeTest { ... } (single public test class, name mirrors the .dart file name, no base class needed)"
  - "[Fact(DisplayName = \"calculate\")] public void Calculate() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original test name)"
  - "method body: Assert.Equal(42L, GlpRuntimeRoot.Calculate()); (1-to-1 translation of expect(calculate(), 42) with EXPECTED-FIRST xUnit argument order and the `42L` long literal to match Calculate's long return type)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely (same as the sibling smoke_test.dart conversion)"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the sibling spec
  `.codeconv/conversion-specs/test/smoke_test.dart.md` (the first test
  file in this conversion batch). Authoritative sources cited there
  (Microsoft Learn unit-testing-csharp-with-xunit, xunit.net,
  pub.dev/package:test, pub.dev/documentation/test/latest/test/test.html)
  carry forward verbatim. The framework choice is the project's
  batch-wide test-framework idiom; this file MUST reuse it rather than
  re-research (per the decision-order in
  `specs/018-codeconv-builder/contracts/convspec_idiom_schema.md`).
- **Conclusion**: drop `import 'package:test/test.dart';`, emit
  `using Xunit;`. The `.csproj`-level NuGet wiring (xunit,
  xunit.runner.visualstudio, Microsoft.NET.Test.Sdk) is out of scope
  for this per-file artifact (langpair-level emission). Zero
  escalation — same as the sibling.

### rf-dart-same-package-import-to-csharp-using — internal package import ⇒ `using <namespace>;`

- **Deep analysis**: the file imports `package:glp_runtime/glp_runtime.dart`
  to gain access to the top-level Dart function `calculate()`. The
  conversion of `calculate()` itself is recorded in the sibling lib
  spec `.codeconv/conversion-specs/lib/glp_runtime.dart.md` — it lifts
  to `public static long Calculate()` on a distinctly-named static
  host class (the lib spec records `GlpRuntimeRoot` as the host name,
  chosen to avoid colliding with the package's root namespace). The
  test file's import therefore becomes a C# `using` directive that
  brings the host class's namespace into scope so the test body can
  qualify the call as `GlpRuntimeRoot.Calculate()`.
- **Authoritative Dart**: Dart's official language tour documents the
  `package:` URI scheme at
  `https://dart.dev/tools/pub/dependencies` and
  `https://dart.dev/guides/libraries/create-packages` — `package:`
  imports are path-based, resolved via the package_config, and bring
  the imported library's top-level identifiers into the importing
  file's lexical scope directly (no qualification required at the call
  site).
- **Authoritative .NET**: Microsoft Learn's C# language reference for
  the `using` directive at
  `https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive`
  documents: "The `using` directive allows you to use types defined in
  a namespace without specifying the fully qualified namespace of that
  type." The `using static` variant at the same reference: "The
  `using static` directive imports the static members of types and
  enumeration values from a single type" — would unqualify member
  access. Microsoft's C# Coding Conventions guidance at
  `https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions`
  recommends qualifying static-member access via the type name in
  typical cases (reserving `using static` for ubiquitous helper types
  like `Math`).
- **Conclusion**: emit a regular `using <namespace>;` (NOT `using
  static <namespace>.GlpRuntimeRoot;`); the test body qualifies the
  call as `GlpRuntimeRoot.Calculate()`. This makes the cross-file
  dependency explicit at the call site and matches the Dart source's
  intent of naming the function explicitly (the Dart source writes
  `calculate()` unqualified only because Dart has no other choice —
  there is no qualification syntax for same-package top-level
  identifiers in Dart). Authoritative both sides; no escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` ⇒ `class { [Fact] }` (REUSED)

- **KB reuse, not re-research (FR-012 / SC-007)**: this finding was
  authoritatively researched and recorded in the sibling
  smoke_test.dart spec. Same structural lift: drop `main()`, lift the
  one `test()` registration into a `[Fact]` method on a class whose
  name mirrors the .dart file name. Authoritative sources cited in
  the sibling (Microsoft Learn xUnit tutorial, xunit.net "Shared
  Context between Tests", pub.dev `test` API reference, Dart language
  tour `#hello-world`) carry forward verbatim.
- **File-specific application**: `glp_runtime_test.dart` ⇒
  `GlpRuntimeTest.cs` ⇒ `public class GlpRuntimeTest`; the test name
  `'calculate'` ⇒ method identifier `Calculate` with
  `[Fact(DisplayName = "calculate")]` to preserve the original
  human-readable name. The naming collision between the `[Fact]`
  method `Calculate` and the host method
  `GlpRuntimeRoot.Calculate` is benign — they live on different types
  and the call site qualifies. Zero escalation — same as the sibling.

### rf-dart-expect-bare-value-to-xunit-assert-equal — `expect(actual, value)` ⇒ `Assert.Equal(value, actual)`

- **Deep analysis**: the assertion `expect(calculate(), 42)` uses the
  `expect` BARE-VALUE matcher form (the second argument is a value,
  not a `Matcher`). `package:test_api`'s `expect` documents that
  non-`Matcher` second arguments are auto-wrapped as
  `equals(value)`; the assertion is semantically
  `expect(calculate(), equals(42))`. The xUnit counterpart is
  `Assert.Equal<T>(T expected, T actual)`, with EXPECTED-FIRST argument
  order — the OPPOSITE of Dart's `expect(actual, equals(expected))`
  ACTUAL-FIRST shape. This is the argument-order footgun that the
  sibling smoke_test.dart spec recorded as load-bearing context (in
  the broader matcher routing table); this file is the first occurrence
  in the batch where the row actually fires.
- **Authoritative Dart (`expect` bare-value)**: pub.dev
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  documents `expect`'s second-argument contract: "If [matcher] is not
  a [Matcher], it will be implicitly wrapped in [equals]." Strict
  equality semantics via `==` on primitives — for Dart `int`
  comparison this is numeric equality on the boxed-or-primitive
  integer values.
- **Authoritative Dart (`equals`)**: pub.dev
  `https://pub.dev/documentation/matcher/latest/matcher/equals.html`
  — "Returns a matcher that matches if the value is equal to
  [expected]." Uses `Equality<T>`'s default `==`-based comparison for
  scalars.
- **Authoritative .NET (`Assert.Equal`)**: xunit.net Assert API
  reference for `Equal<T>(T expected, T actual)` — verbatim signature
  with EXPECTED-FIRST argument order, equality via
  `IEquatable<T>.Equals` (falling back to `Object.Equals` /
  `EqualityComparer<T>.Default`); for the `long` value type both
  reduce to value equality. The Microsoft Learn xUnit tutorial at
  `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit`
  uses `Assert.Equal(expected, actual)` form throughout, confirming
  the canonical argument order.
- **Integer-width carry-forward**: per the lib spec
  `.codeconv/conversion-specs/lib/glp_runtime.dart.md`, Dart
  `calculate()` returns `int` ⇒ C# `long`
  (rf-dart-int-to-csharp-long-width — faithful 64-bit mapping per the
  Dart language tour, `https://dart.dev/language/built-in-types#numbers`,
  which documents Dart `int` as a 64-bit integer on native platforms).
  The emitted xUnit assertion uses `42L` so the generic `Assert.Equal<T>`
  binds with `T = long` unambiguously and a reader is not left
  wondering whether silent `int`→`long` promotion is occurring on the
  first argument.
- **Conclusion**: `Assert.Equal(42L, GlpRuntimeRoot.Calculate());` —
  EXPECTED-FIRST order, `long` literal on both sides. Authoritative
  both sides; zero escalation. The recorded idiom row is the
  load-bearing entry of the matcher routing table for the rest of the
  batch.

## Notes

- The two `import` directives map to two distinct C# `using`
  directives, recorded under two distinct construct keys
  (test-framework import vs internal-package import). They are NOT
  collapsed because the routing logic and the cited authoritative
  sources differ: the first is the xUnit framework-choice idiom
  (batch-wide); the second is the C# `using`-directive language
  feature applied to the converted package's own namespace.
- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the `[Fact]` method is `void` (not
  `async Task`). The well-known async-Dart-vs-.NET-async nuance is
  deliberately not asserted here (does not apply).
- No `late`, `mixin`, `extension`, generics on the SUT
  (`calculate()` is non-generic), sealed/abstract, bitwise/shift,
  isolate, or null-safety surface — all absent in this file's source.
- The integer-width nuance (Dart `int` ⇒ C# `long`) IS load-bearing
  here because the assertion compares a returned `long` against a
  literal — handled explicitly via the `42L` C# literal so the
  generic `Assert.Equal<T>` binds without implicit-conversion
  ambiguity. The lib spec already authoritatively recorded the
  `int`⇒`long` decision; this spec reuses it.
- The Dart-side test-name `'calculate'` collides on PascalCase shape
  with the converted function name `Calculate`. The C# emission
  resolves this by qualifying the call (`GlpRuntimeRoot.Calculate()`
  vs the test method `GlpRuntimeTest.Calculate`), so the two
  same-spelled identifiers live on different types and never collide
  at the call site.
- Zero escalations: every construct in this file is
  authoritative-supported on both sides, three of the four constructs
  REUSE idioms / findings recorded by the sibling smoke_test.dart and
  lib/glp_runtime.dart specs (per FR-012 / SC-007 KB-reuse decision
  order), and the one new construct
  (rf-dart-expect-bare-value-to-xunit-assert-equal) was load-bearing
  context already documented (but not fired) in the sibling
  smoke_test.dart spec's matcher routing table — this file is the
  first occurrence in the batch where the row actually applies.
