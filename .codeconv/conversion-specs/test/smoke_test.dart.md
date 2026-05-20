> Conversion-spec artifact for test/smoke_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/smoke_test.dart
source_sha256: b0355fee58d4216a3a181c91b018351513d8928d2bc9d78f6fb22eaf98748f7a
target_code_unit: test/SmokeTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it with the xUnit-equivalent surface area at the file level:
      `using Xunit;` (per Microsoft Learn xUnit getting-started docs).
      The .NET test project itself (a separate .csproj outside this
      file's conversion unit) MUST reference the `xunit` and
      `xunit.runner.visualstudio` (or `Microsoft.NET.Test.Sdk` + xUnit)
      NuGet packages — that project-file emission is OUT OF SCOPE for
      this single-file artifact (a sibling `langpair`-level concern,
      not per-file). The choice of xUnit (over MSTest or NUnit) is the
      batch-wide default for this conversion (recorded as the project's
      test-framework idiom; sibling test files in the same batch MUST
      reuse it via the idiom KB rather than re-deciding).
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework choice (load-bearing, explicitly addressed): Dart
      `package:test` provides a function-style runner (`test('name',
      fn)`) with `expect(actual, matcher)` and a parallel/isolate-based
      execution model. .NET has three mainstream choices: xUnit, NUnit,
      MSTest. The conversion selects **xUnit** as the modern default
      because (a) it is the framework Microsoft Learn's current
      "unit-testing C# code" tutorial walks through end-to-end
      (learn.microsoft.com /en-us/dotnet/core/testing/unit-testing-csharp-
      with-xunit), (b) its `[Fact]` / `Assert` surface is the closest
      semantic shape to `test()` / `expect()` (single-method-per-test,
      attribute-driven discovery, no fixture inheritance required for
      the trivial case), and (c) the .NET Foundation hosts xUnit
      (xunit.net) — official, not third-party. Module/namespace
      semantics: Dart `import 'package:test/test.dart'` exposes
      top-level functions (`test`, `expect`, matchers like `isTrue`);
      xUnit has no top-level test function — tests are PUBLIC
      INSTANCE METHODS on a public test class, discovered by `[Fact]`
      attributes. So the import is not 1-to-1 replaced by `using
      Xunit;` alone — the import-plus-`main` shape on the Dart side
      becomes a class-plus-methods shape on the C# side (see next two
      constructs). No async / Future / Stream surface in this file.
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { test('...', () { ... }); }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely. In
      `package:test` the file-level `main()` is the test-registration
      entry point invoked by the test runner; xUnit has NO equivalent
      `main` — test discovery is attribute-driven on a public test
      class. The Dart `void main() { test('name', body); }` shape
      becomes a `public class SmokeTest { [Fact] public void
      ProjectSkeletonExists() { body } }` shape: each Dart `test(...)`
      call in `main()` lifts into one `[Fact]`-attributed public
      instance method on the class; the body of the Dart callback
      becomes the body of the C# method (verbatim translation of the
      assertions). The class name `SmokeTest` mirrors the file name
      (`smoke_test.dart` → `SmokeTest.cs` → `class SmokeTest`), a
      consistent .NET-test convention.
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery model nuance (explicitly addressed): Dart
      `package:test` discovers tests by EXECUTING `main()` (which calls
      `test()` to register closures); the runner runs registered
      closures afterward. xUnit discovers tests by REFLECTION over
      `[Fact]` (and `[Theory]`) attributes — no equivalent registration
      pass exists, and there is no place to put cross-test imperative
      setup beyond constructor / `IClassFixture<T>` / `IAsyncLifetime`
      (none used here — the source has no setUp/tearDown). Constructor
      semantics: xUnit creates a FRESH instance of the test class per
      `[Fact]` invocation (Microsoft Learn / xunit.net: "xUnit.net
      creates a new instance of the test class for every test that is
      run"), so any per-test setup goes in the constructor and any
      teardown in `IDisposable.Dispose` — neither needed for this
      one-assertion smoke test. The Dart inner closure `() {
      expect(true, isTrue); }` translates 1-to-1 into the body of the
      `[Fact]` method; no closure object is materialised in C# because
      the body executes directly. Name mapping: Dart's
      string-identified test name `'project skeleton exists'`
      (human-readable, with spaces) becomes a C# method identifier
      `ProjectSkeletonExists` (PascalCased, no spaces — C# method
      identifiers cannot contain whitespace). The human-readable form
      MAY be preserved by emitting `[Fact(DisplayName = "project
      skeleton exists")]` so the test runner's report shows the
      original name; spec default = emit `DisplayName` to preserve
      reporting fidelity (Microsoft Learn xUnit reference documents
      `FactAttribute.DisplayName`). No async: the closure is
      synchronous, so the method returns `void` (xUnit also supports
      `async Task` for async tests; not applicable here). Reference vs
      value: no allocations in either source or target beyond the
      string literals and the test class instance itself. Null-safety:
      no nullable surface in this file.
  - construct_key: dart.package_test.expect_true_isTrue_matcher
    source_form: "expect(true, isTrue);"
    target_decision: >-
      Translate the Dart `expect(actual, matcher)` call into the
      equivalent xUnit `Assert.*` static call. For the literal pair
      `expect(true, isTrue)` (a degenerate-but-canonical smoke
      assertion: "is this boolean literal true?"), the
      semantically-tightest xUnit form is `Assert.True(true);` (per
      xunit.net `Assert.True(bool)` — passes when the argument is
      true, otherwise throws `Xunit.Sdk.TrueException`). The Dart
      matcher `isTrue` is from `package:matcher` (re-exported by
      `package:test`) and asserts strict boolean truth (not
      truthiness — Dart booleans are strict). xUnit's `Assert.True`
      mirrors that exactly: `bool` argument only, no truthy/coercion
      surface.
    idiom_id: null
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Matcher-vs-assertion model (explicitly addressed): Dart's
      `expect(actual, matcher)` is a TWO-ARGUMENT shape where the
      second argument is a matcher object that the runner invokes to
      determine pass/fail and to produce a failure-message description.
      xUnit's `Assert` is a ONE-OR-TWO-ARGUMENT shape per assertion
      method (no matcher object; the assertion is encoded by the
      method name itself — `Assert.True`, `Assert.Equal`, `Assert.Null`,
      etc.). The conversion ROUTES each Dart matcher to its xUnit
      counterpart: `isTrue` ⇒ `Assert.True(actual)`. (For broader
      coverage in this conversion's scope, the recorded routing-table
      idiom — to be elaborated as more tests are converted — is:
      `isTrue` ⇒ `Assert.True`, `isFalse` ⇒ `Assert.False`,
      `isNull` ⇒ `Assert.Null`, `isNotNull` ⇒ `Assert.NotNull`,
      `equals(x)` ⇒ `Assert.Equal(x, actual)`. NOTE the
      argument-order swap for `Assert.Equal`: Dart `expect(actual,
      equals(expected))` has actual-first, but xUnit `Assert.Equal`
      has EXPECTED-FIRST then ACTUAL — `Assert.Equal<T>(T expected,
      T actual)` per xunit.net. This is an EASY-TO-INVERT footgun
      that the conversion explicitly records, even though the
      smoke test here uses only `isTrue` which is single-argument
      and unaffected.) Exception-on-failure semantics: Dart `expect`
      throws `TestFailure` (subclass of `Exception`) on mismatch;
      xUnit `Assert.True` throws `Xunit.Sdk.TrueException` (subclass
      of `Xunit.Sdk.XunitException` → `Exception`). Both are caught
      by the respective runner — semantically equivalent.
      Diagnostic-message quality: Dart's matcher API auto-generates
      "Expected: true / Actual: <false>"-style descriptions from the
      matcher object; xUnit's `Assert.True(bool)` produces only the
      generic "Assert.True() Failure" unless the optional `userMessage`
      overload is used. For this trivial smoke check ("did the
      project skeleton's most basic possible assertion still pass?")
      the generic message is sufficient — spec default = emit
      `Assert.True(true);` without a custom message. Reference/value:
      `bool` is a value type in both languages; no boxing.
conversion_units:
  - "using Xunit; (file-level using directive replacing `import 'package:test/test.dart';`)"
  - "public class SmokeTest { ... } (single public test class, name mirrors the .dart file name, no base class needed for this smoke case)"
  - "[Fact(DisplayName = \"project skeleton exists\")] public void ProjectSkeletonExists() { ... } (one Fact-attributed method per Dart test() call; DisplayName preserves the original human-readable test name)"
  - "method body: Assert.True(true); (1-to-1 translation of expect(true, isTrue) — xUnit Assert.True with the literal boolean)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit framework choice

- **Deep analysis**: the source file imports exactly one symbol family —
  `package:test`'s top-level test-registration API (`test`, `expect`,
  `isTrue`). There is no `setUp`, `tearDown`, `group`, `setUpAll`,
  `tearDownAll`, `async`/`Future` assertion, isolate, stream-matcher,
  custom matcher, or tagging surface in this file. So the conversion's
  required surface is the minimum: a way to register one test, run it,
  and assert a single boolean.
- **Authoritative Dart**: Dart's official `package:test` lives at
  pub.dev (`https://pub.dev/packages/test`, official package published
  under the `dart-lang` org). The pub.dev page documents the
  `test('name', body)` registration shape and the `expect(actual,
  matcher)` assertion shape as the canonical surface. The full API
  reference at `https://pub.dev/documentation/test/latest/test/test.html`
  defines `test` as a top-level function: "Creates a new test case with
  the given description (converted to a string) and body."
- **Authoritative .NET**: Microsoft Learn's canonical unit-testing
  tutorial for C# is
  `https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit`
  ("Unit testing C# in .NET Core using dotnet test and xUnit"),
  walking through `[Fact]` discovery, `Assert.*` calls, and the
  `dotnet test` runner. This is the .NET-foundation-blessed default
  example in the official .NET docs. The xUnit project itself is
  hosted at `https://xunit.net/` with reference docs at
  `https://xunit.net/docs/getting-started/v2/getting-started`.
- **Conclusion**: xUnit is the authoritative .NET counterpart for
  Dart's `package:test` when the conversion is choosing a single
  modern default. MSTest and NUnit would be authoritative-alternative
  choices (Microsoft Learn also documents both:
  `unit-testing-csharp-with-mstest` and `unit-testing-csharp-with-nunit`),
  so the framework choice is a project-policy decision rather than a
  forced technical one. Per the task brief ("Consistent target-framework
  choice with the other test file in this batch (xUnit modern default)"),
  xUnit is selected as the batch-wide default and recorded here as the
  project's test-framework idiom — sibling test files in the same
  conversion run MUST reuse this idiom rather than re-research the
  framework choice (FR-012 / SC-007 KB reuse).
- **Why this is not an escalation**: both Dart and .NET sides are
  authoritatively documented; the only choice (which .NET framework)
  has a clear modern default backed by Microsoft Learn's primary
  tutorial. The task brief explicitly directs xUnit as the default,
  removing the policy ambiguity that would otherwise have warranted an
  escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() { test(...) }` ⇒ `class { [Fact] }`

- **Deep analysis**: the file's structure is
  `void main() { test('project skeleton exists', () { expect(true,
  isTrue); }); }` — a single Dart `main()` containing a single `test()`
  registration containing a single `expect()`. There is no shared
  setup, no group hierarchy, no parametrisation. The conversion's
  structural transformation must drop `main()` and lift the one
  registered closure into a `[Fact]` method on a test class.
- **Authoritative Dart**: same pub.dev `test` API reference as above
  (`https://pub.dev/documentation/test/latest/test/test.html`). The
  `test()` API entry documents: registration of a test case via a
  top-level function call inside the file's `main`. Dart's
  language-level requirement that every Dart executable has a
  `void main()` entry point is at
  `https://dart.dev/language#hello-world` (Dart official language
  tour, verbatim: "Every app requires the top-level `main()`
  function, where execution starts.").
- **Authoritative .NET**: Microsoft Learn xUnit tutorial cited above
  documents the class-with-`[Fact]`-methods shape verbatim — "Test
  methods are decorated with the `[Fact]` attribute" — and the per-test
  instance lifecycle is documented at xunit.net's "Shared Context
  between Tests" page (`https://xunit.net/docs/shared-context`):
  "xUnit.net creates a new instance of the test class for every test
  that is run." This authoritative source documents the discovery and
  lifecycle model that REPLACES Dart's `main()`-registration model.
  `FactAttribute.DisplayName` is documented in the xunit.net API
  reference (and visible in the xUnit source); it preserves the Dart
  test name (which can contain spaces and punctuation that C# method
  identifiers cannot).
- **Conclusion**: drop `main()` entirely; emit `public class SmokeTest`
  with one `[Fact(DisplayName = "project skeleton exists")]
  public void ProjectSkeletonExists()` method whose body is the Dart
  closure body. PascalCased method identifier preserves the C# naming
  rule; `DisplayName` preserves the original human-readable test name
  for the test report. Authoritative both sides; no escalation. The
  per-test fresh-instance lifecycle nuance is recorded but does not
  fire here (no shared state).

### rf-dart-expect-isTrue-to-xunit-assert-true — `expect(true, isTrue)` ⇒ `Assert.True(true)`

- **Deep analysis**: the assertion is the canonical
  smoke-test-degenerate-form — `expect(true, isTrue)` is literally
  "assert that the constant `true` is true", which is structurally
  trivial but semantically important (it proves the test runner is
  alive, the assertion path is wired, and the framework's failure
  mode would have surfaced any plumbing break).
- **Authoritative Dart (`expect`)**: pub.dev/`package:test` references
  `https://pub.dev/documentation/test_api/latest/expect/expect.html`
  (the `expect` function definition; `package:test` re-exports
  `test_api`'s `expect`). It documents the two-argument shape
  `expect(actual, matcher)` and says matchers can be `Matcher`
  instances or values (which are wrapped in `equals(value)`).
- **Authoritative Dart (`isTrue`)**: pub.dev `package:matcher` (also
  re-exported by `package:test`) documents `isTrue` at
  `https://pub.dev/documentation/matcher/latest/matcher/isTrue-constant.html`
  — "A matcher that matches if the value is `true`." Strict boolean
  comparison, no truthiness coercion (Dart `bool` is strict).
- **Authoritative .NET (`Assert.True`)**: xunit.net's `Assert` API
  documentation (`https://xunit.net/docs/comparisons` and the API
  reference for `Xunit.Assert.True`) — verbatim: "Verifies that an
  expression is `true`." Signature `public static void True(bool
  condition)` (and an overload with a user message). Throws
  `Xunit.Sdk.TrueException` on failure (subclass of
  `Xunit.Sdk.XunitException` → `System.Exception`). Strict boolean,
  no truthiness — matches Dart `isTrue` exactly.
- **Conclusion**: 1-to-1 mapping `expect(true, isTrue)` ⇒
  `Assert.True(true)`. The recorded broader routing table (for
  sibling test conversions in this batch) is documented in the
  nuance: `isTrue` ⇒ `Assert.True`, `isFalse` ⇒ `Assert.False`,
  `isNull` ⇒ `Assert.Null`, `isNotNull` ⇒ `Assert.NotNull`,
  `equals(x)` ⇒ `Assert.Equal(x, actual)` — with the explicit
  argument-order-swap warning (xUnit `Assert.Equal<T>(expected,
  actual)` per xunit.net). Only the `isTrue` row is needed for THIS
  file's spec, but the broader table is the load-bearing context for
  the test-framework idiom and is recorded for reuse. Authoritative
  both sides; no escalation.

## Notes

- No async / `Future` / `Stream` / isolate / `Completer` / `Timer`
  surface in this file — the smoke test is fully synchronous, so the
  xUnit method is `void` (not `async Task`). The well-known
  async-Dart-vs-.NET-async nuance is deliberately not asserted here
  (it does not apply to this file's source surface).
- No `late`, `mixin`, `extension`, generics, sealed/abstract,
  bitwise/shift, isolates, or null-safety nuance — all absent.
- The file-level shape is asymmetric: Dart has ONE `main()` containing
  ONE `test()`; the C# emission is ONE `class` containing ONE
  `[Fact]` method. The structural lift drops `main()` entirely
  (xUnit has no equivalent), which is documented in
  rf-dart-test-main-to-xunit-class-with-facts.
- The Dart `.csproj`-emission (test-project metadata, xUnit + Microsoft
  test SDK NuGet references, `dotnet test` runner wiring) is
  intentionally OUT OF SCOPE for this single-file artifact — that is
  langpair-level / project-skeleton emission, not per-file conversion.
  Recorded here so codegen knows: emitting `SmokeTest.cs` alone is
  insufficient; a sibling `.csproj` must reference `xunit` and
  `Microsoft.NET.Test.Sdk` (or `xunit.runner.visualstudio`) for the
  test to actually run.
- Framework choice (xUnit) is recorded as a batch-wide idiom: every
  subsequent test-file conversion in this conversion run MUST reuse
  the same framework via the KB rather than re-research (FR-012 /
  SC-007). The companion test file in this same batch
  (`glp_runtime_test.dart`, which asserts `expect(calculate(), 42)`)
  will reuse `rf-dart-package-test-to-dotnet-xunit` and the
  `equals`-routing row of the `expect`-matcher table (with the
  `Assert.Equal(expected, actual)` argument-order swap).
- Zero escalations: every construct in this file is
  authoritative-supported on both sides, and the only project-policy
  decision (which .NET test framework) has been pre-resolved by the
  task brief in favour of xUnit.
