> Conversion-spec artifact for test/multiagent/mad_error_handling_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/mad_error_handling_test.dart
source_sha256: ca5a6a1cb4d3979172f347c655657ba5cab213c030390ad80a23d58023c0e0b4
target_code_unit: test/multiagent/MadErrorHandlingTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Dart `package:test` is the only test-runtime import in this file. Map to
      a .NET test framework via `using Xunit;` at file scope (xUnit chosen
      authoritatively as the modern default — see rationale prose for the
      xUnit vs NUnit vs MSTest trade-off). No other Dart import dependency
      surfaces here. The codegen stage MUST also add `using System;` (used by
      exception-typed assertions referenced in the skipped tests' Then-clauses:
      StateError, ArgumentError mapped under construct keys below) and project
      to a single namespace mirroring the Dart `test/multiagent` directory
      (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test framework selection is a project-wide policy nuance, NOT a
      file-local choice: every `package:test` file in the inventory MUST map
      to the same .NET framework so test discovery, runner config, and
      attribute vocabulary stay consistent. Recording xUnit here pins the
      idiom for all future test-file convspecs. xUnit was selected over
      NUnit/MSTest because (a) xUnit's `[Fact]`/`[Theory]` attribute model
      maps 1:1 onto Dart `test()`/parameterised `test()`, (b) xUnit's
      constructor-per-test isolation matches `package:test`'s fresh-state
      semantics (no per-class shared `[SetUp]` lifetime), and (c) xUnit is
      the modern .NET default for new projects. NUnit's `[Test]` /
      `[SetUp]`/`[TearDown]` is a viable alternative and would require
      switching ALL test-file idioms together.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group(...); }"
    target_decision: >-
      Dart `void main()` is the per-file test-runner entrypoint that the
      `package:test` runner invokes; it does NOT correspond to any C# member.
      Eliminate `main` entirely in the target: xUnit discovers `[Fact]`
      methods on `public` classes via reflection — there is no per-file
      entrypoint to emit. The `group(...)` body content (see next construct)
      becomes the enclosing test class.
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook — only per-class
      (constructor + IDisposable.Dispose) and per-collection fixtures. If a
      future test file needs per-file setup that today sits in `main` before
      `group`, that setup MUST migrate into the enclosing class's constructor
      OR an `IClassFixture<>`; recording the omission here is correct because
      THIS file's `main` body is exactly one `group()` call with no other
      statements.
  - construct_key: dart.package_test.group_block
    source_form: "group('Error Handling', () { test(...); test(...); ... });"
    target_decision: >-
      Dart `group(name, body)` defines a named container for nested
      `test()` calls and (when used) shared `setUp`/`tearDown`. In xUnit
      this maps to a `public class` whose name encodes the group label,
      e.g. `public class ErrorHandlingTests`. The string literal
      `'Error Handling'` becomes the class name in PascalCase with the
      conventional `Tests` suffix; the original label MAY be preserved as a
      `[Trait("Group", "Error Handling")]` attribute on the class for
      reporter parity (codegen choice — record alongside the class).
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Test-naming nuance (explicitly addressed): Dart `group` labels are
      arbitrary strings that may contain spaces / punctuation; C# class names
      are restricted identifiers. The transformation is name-mangling
      (PascalCase, strip non-identifier chars). Lifecycle nuance: a Dart
      `group`'s `setUp`/`tearDown` (not used in this file) would map to xUnit
      class constructor + `IDisposable.Dispose`; nested `group(...)` (also
      not used in this file) would require nested classes or a collection
      fixture — flagged for the project-wide idiom even though this file
      uses neither.
  - construct_key: dart.package_test.test_call_skipped
    source_form: >-
      "test('<label>', () { /* Given/When/Then comments only */ },
      skip: 'Not yet implemented');" — applied to all 5 test cases in this
      file.
    target_decision: >-
      Each Dart `test(label, body, {skip})` with a non-null `skip` string
      becomes a `public void` method on the enclosing class, decorated with
      `[Fact(Skip = "Not yet implemented")]`. The method name is the
      label PascalCased with non-identifier characters stripped (e.g.
      `'receive for non-existent GlobalizeEntry throws'` →
      `ReceiveForNonExistentGlobalizeEntryThrows`). The original label MUST
      be preserved verbatim via `[Fact(Skip = "Not yet implemented",
      DisplayName = "<original label>")]` so test-runner output keeps the
      sentence-form name. The body in the source is comments only (no
      executable statements), so the target method body is empty — but the
      Given/When/Then comments MUST be carried into the target as a `///
      <summary>` doc-comment block per method so the spec-link traceability
      (Spec Section 8.3 / Spec Section 12 references) survives the
      conversion.
    idiom_id: null
    research_finding_id: rf-dart-package-test-skip-to-xunit-fact-skip
    nuance: >-
      Skip-semantics nuance (explicitly addressed, not glossed): Dart
      `package:test` `skip:` takes EITHER `true` OR a `String` reason and
      reports the reason in output. xUnit `[Fact(Skip = "...")]` takes ONLY
      a string (a non-empty string is required to skip; `Skip = ""` does NOT
      skip). The Dart `skip: 'Not yet implemented'` string maps directly,
      lossless. Counter-direction (xUnit `[Fact(Skip="...")]` → Dart
      `skip:'...'`) is also lossless — recorded for the bidirectional idiom.
      NUnit's equivalent is `[Test, Ignore("reason")]` and MSTest's is
      `[TestMethod, Ignore]` (MSTest's `[Ignore]` accepts an optional reason
      string only in newer versions) — both alternatives recorded under the
      research finding so the idiom can be reconciled if the project-wide
      framework is later changed away from xUnit.
  - construct_key: dart.package_test.test_callback_arrow_or_block
    source_form: "() { /* comments only, no statements */ }"
    target_decision: >-
      The Dart test callback `() { ... }` is a closure passed to `test()`.
      In the target there is NO closure: the test body becomes the body of
      the `[Fact]`-decorated method directly. Since every callback in this
      file is comment-only (Given/When/Then), every target method body is
      empty `{ }` and every Given/When/Then comment migrates to the
      method's `/// <summary>` XML-doc. No async / `Future` is present in
      this file's test callbacks, so no `async`/`await`/`Task` target shape
      is required here.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async nuance (explicitly addressed even though absent in this file):
      a Dart `test()` body declared `() async { ... }` would target a
      `public async Task` xUnit method (xUnit awaits returned `Task`/
      `ValueTask`); none of THIS file's callbacks are async, so no target
      method is async. Closure-capture nuance: Dart closures here capture
      nothing from `main()` scope (no `setUp` variables); xUnit per-test
      constructor isolation is the equivalent — no captured state to
      translate.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System)
  - cu-2: namespace declaration mirroring test/multiagent path
  - cu-3: top-level class ErrorHandlingTests (from group label "Error Handling")
  - cu-4: 5 [Fact(Skip="Not yet implemented", DisplayName="...")] methods, one per Dart `test(...)` call, each with `/// <summary>` carrying the original Given/When/Then + spec-section references, all with empty bodies (no asserts — original tests are not-yet-implemented)
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

The task brief instructs picking xUnit as the modern default when the
target framework is unclear, and noting the trade-off. xUnit's
documentation
(`https://xunit.net/docs/getting-started/v3/getting-started`) is the
authoritative source for the `[Fact]` / `[Fact(Skip=…)]` /
`[Theory]` mapping decisions in this file. NUnit
(`https://docs.nunit.org/articles/nunit/intro.html`) and MSTest
(`https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-mstest`)
are recorded as corroborating alternatives in
`rf-dart-package-test-import-to-xunit-using`, but xUnit is the
authoritative idiom basis. The pinning rationale is consistency: a
mixed-framework target tree would multiply runner config and obscure
test-discovery. The trade-off: NUnit has richer assertion DSL and
parameterised-test ergonomics, and MSTest is the Visual-Studio default;
both are viable if Gabi later wants to switch the project-wide idiom —
the research finding holds the alternatives so the switch would be
idiom-level (one entry), not file-level (every test file).

### Mapping the five skipped tests

Every test in this file is `skip: 'Not yet implemented'` with a
comment-only body. The Dart `package:test` documentation
(`https://pub.dev/packages/test`, "Skipping tests") states the `skip:`
argument accepts `bool` or `String`, with strings used as the skip
reason. xUnit's `FactAttribute.Skip` property
(`https://xunit.net/docs/comparisons#skip`) is the canonical
counterpart: a non-empty `Skip` string causes the test to be reported as
skipped with that reason. The mapping is therefore lossless. The
Given/When/Then comments and Spec-Section references (Spec Section 8.3
for the receive-table lookups, Spec Section 12 for entry-lifecycle and
idempotent-removal invariants) are load-bearing for the not-yet-
implemented-test contract; they MUST be carried across the conversion
into `/// <summary>` doc-comment blocks so the conversion preserves the
invariant-tracing this test file documents.

### Exception-type cross-reference (out of scope for this file)

The Then-clauses mention `throws StateError`, `throws ArgumentError`,
and "no-op / idempotent" outcomes. No assertion code is present in this
file, so no `Assert.Throws<T>` / `throwsA` matcher needs to be emitted
NOW. The mapping (`Dart StateError → C# InvalidOperationException`,
`Dart ArgumentError → C# ArgumentException`, `throwsA(isA<T>()) →
Assert.Throws<T>(...)`) is recorded as future research-finding
candidates in the idiom KB once a sibling file with executable
assertions hits convspec; THIS file does not exercise them and so does
not write those idioms. This is the "spec-only / no guessing" discipline
(FR-013, FR-023) — recording mappings that the source does not actually
use would be speculation.

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official documentation for both Dart `package:test` and xUnit. No
construct in this file involves an idiom-vs-research conflict or an
idiom-vs-idiom conflict, and nothing is undecidable. The
`escalations: []` is therefore intentional, not a placeholder.
