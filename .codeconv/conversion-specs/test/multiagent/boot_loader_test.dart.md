> Conversion-spec artifact for test/multiagent/boot_loader_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/boot_loader_test.dart
source_sha256: fbe7c999ea5524d849532628fdf73dc76056f9c3a49930c86d0432d4fb50baff
target_code_unit: test/multiagent/BootLoaderTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit chosen project-wide as the
      modern .NET default — same pinning as the precedent file
      test/multiagent/mad_error_handling_test.dart.md, idiom
      rf-dart-package-test-import-to-xunit-using). Codegen MUST also add
      `using System;` (needed for `IDisposable` if setUp/tearDown maps map
      to constructor + Dispose — see dart.package_test.setUp_block below)
      and project to a single namespace mirroring the Dart `test/multiagent`
      directory (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance, NOT a
      file-local choice: every `package:test` file in the inventory MUST
      map to the SAME .NET framework so test discovery, runner config, and
      attribute vocabulary stay consistent. xUnit was selected over
      NUnit/MSTest because (a) `[Fact]`/`[Theory]` map 1:1 onto Dart
      `test()`/parameterised `test()`, (b) xUnit's constructor-per-test
      isolation matches `package:test`'s fresh-state semantics, and (c)
      xUnit is the modern .NET default. NUnit and MSTest are recorded as
      corroborating alternatives in the research finding.
  - construct_key: dart.package_under_test.import_directive
    source_form: "import 'package:glp_runtime/multiagent/boot_loader.dart';"
    target_decision: >-
      Map to a `using` directive that names the C# namespace produced by
      converting `glp_runtime/lib/multiagent/boot_loader.dart` (e.g.
      `using <RootNs>.Multiagent;` — the SUT namespace is decided when
      `boot_loader.dart` itself is converted; this spec records only the
      shape of the cross-file dependency, not the SUT namespace string).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: in Dart `package:glp_runtime/...` is an
      explicit pubspec-anchored URI; in C# there is no per-file URI — only
      assembly + namespace. The conversion must therefore (a) ensure the
      converted SUT lives in a deterministic namespace derived from its
      relative path, and (b) ensure the test assembly references the SUT
      assembly via the project file (out of scope for THIS artifact — a
      project-system idiom). No `as` alias / partial import is used in
      this file, so the simple `using <Ns>;` form suffices.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('BootLoader', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint. xUnit
      discovers `[Fact]` methods by reflection — there is NO per-file
      entrypoint to emit. Eliminate `main` entirely; its single statement
      (the outer `group('BootLoader', ...)`) becomes the enclosing test
      class (see dart.package_test.group_block below).
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook — only per-class
      (constructor + IDisposable.Dispose) and per-collection fixtures
      (`IClassFixture<>`, `ICollectionFixture<>`). THIS file's `main` body
      is exactly one `group(...)` call with no other statements, so the
      omission is lossless.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('BootLoader', () { late BootLoader loader; setUp(...);
      group('valid boot files', ...); group('error cases', ...);
      group('real file content', ...); });"
    target_decision: >-
      Nested `group` topology: outer `BootLoader` + 3 inner groups
      (`valid boot files`, `error cases`, `real file content`). Map to a
      single PascalCase xUnit test class `BootLoaderTests` containing ALL
      test methods, with each inner-group label preserved as a
      `[Trait("Group", "<label>")]` on every test method belonging to that
      inner group (reporter-parity preserves the grouping the Dart runner
      shows). Per-test method names are prefixed with a PascalCased,
      identifier-safe form of the inner-group label so name collisions
      across groups are impossible (e.g. `ValidBootFiles_ParsesThreeAgentBootClause`,
      `ErrorCases_ThrowsIfNoProcedureBootDeclaration`,
      `RealFileContent_ParsesPlayAliceBobCharlieActorBootGlpContent`).
      The original test label MUST be preserved verbatim via
      `[Fact(DisplayName = "<original label>")]` so the human-readable
      sentence form survives.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-group nuance (explicitly addressed, present in this file):
      `package:test` permits arbitrary `group` nesting; xUnit has no
      first-class nested-group construct. THREE viable target shapes exist
      and are recorded in the research finding: (i) FLATTEN to a single
      class with `[Trait]` per inner group (CHOSEN — minimises file count,
      preserves grouping for reporters that surface traits, keeps the
      shared `loader` field on one class for the setUp mapping below);
      (ii) NESTED public classes (one per inner group) sharing a base
      class for setUp; (iii) `IClassFixture<>` per inner group. (i) is
      chosen because every inner group in this file shares the SAME
      `late BootLoader loader` from the outer group's setUp — splitting
      classes would duplicate that field. Name-mangling nuance:
      `'real file content'` and similar group labels contain spaces and
      must be PascalCased and stripped of non-identifier characters.
  - construct_key: dart.package_test.late_field_in_group
    source_form: "late BootLoader loader;"
    target_decision: >-
      Dart `late` field declared in the `group` callback (closed-over by
      setUp + every test) maps to a `private BootLoader _loader = null!;`
      instance field on the xUnit test class. The field is assigned by
      the class constructor (the setUp mapping — see next construct), so
      `null!` is the non-nullable "assigned-later" idiom that matches
      Dart's `late` semantics (initialised before any reader runs;
      throws if read uninitialised).
    idiom_id: null
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart `late T x;` is a
      non-null `T` that throws `LateInitializationError` if read before
      assignment; the closest C# equivalent for an xUnit per-test field
      is `private T _x = null!;` (non-nullable reference, suppressed
      initialiser warning, assigned in the constructor). Because the
      xUnit constructor runs BEFORE every `[Fact]`, the `null!` is
      replaced before any reader runs — semantically equivalent to
      Dart `late + setUp`. Alternative `private T? _x;` (nullable + `!`
      at every read site) was REJECTED because it inverts the
      "guaranteed-initialised" contract that `late` encodes; recorded in
      the research finding.
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { loader = BootLoader(); });"
    target_decision: >-
      Dart `setUp` registered inside the outer group maps to the xUnit
      test class's CONSTRUCTOR body: `public BootLoaderTests() { _loader
      = new BootLoader(); }`. xUnit instantiates the test class once per
      test method (constructor-per-test isolation), which matches
      `package:test`'s per-test fresh-state semantics exactly. NO
      `[SetUp]` attribute exists in xUnit (that is NUnit's idiom); using
      the constructor is the documented xUnit pattern. No `tearDown` is
      present in this file, so no `IDisposable.Dispose` is emitted.
    idiom_id: null
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (explicitly addressed): `package:test`'s `setUp`
      is per-test and runs in the same isolate; xUnit's constructor is
      per-test and runs on the same thread — both give a fresh `_loader`
      per test, identical observable semantics. If a future test file
      adds `tearDown`, the idiom extends to `IDisposable.Dispose`
      (recorded in the research finding for forward-compat). Async-setUp
      nuance: Dart `setUp(() async { ... })` would map to xUnit
      `IAsyncLifetime.InitializeAsync` — NOT used here, recorded in the
      research finding only.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange, act (loader.load), assert */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance method
      on the enclosing class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The method name is the
      group-prefixed PascalCased label (see group_block). The closure body
      converts statement-for-statement into the method body (arrange =
      raw string literal `source`; act = `var config = _loader.Load(source);`;
      assert = the `expect(...)` translations below). All eleven `test`
      calls in this file are synchronous (no `async`/`Future`) so no
      target method is `async Task`.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async nuance (explicitly addressed even though absent in this
      file): a Dart `test('...', () async { ... })` would target `public
      async Task <Name>()`; xUnit awaits the returned Task. Closure-
      capture nuance: each callback captures `loader` from the outer
      `group` scope; the xUnit translation captures `this._loader` from
      the test-class instance, which is equivalent because the
      constructor (setUp) has already assigned it before the method
      runs.
  - construct_key: dart.string.triple_quoted_raw_literal
    source_form: "''' procedure boot. boot :- ... '''"
    target_decision: >-
      Dart triple-single-quoted multi-line string literals (used to embed
      every `.glp` source fixture in this file) map to C# verbatim
      multi-line string literals — prefer C# 11 raw string literals
      (`""" ... """`) which match Dart triple-quote semantics most
      closely (no escape processing, leading-whitespace stripping
      matched to the closing delimiter's indentation). If targeting an
      older C# language version, fall back to `@" ... "` verbatim
      strings — but those require `""` to escape an embedded `"`, which
      is not needed in any literal in this file.
    idiom_id: null
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      String-literal nuance (explicitly addressed): Dart triple-quoted
      strings do NOT process `\n`/`\t` escapes (they are literal). C# raw
      string literals (`"""`) also do not process escapes. C# verbatim
      strings (`@"..."`) also do not process escapes but DO require `""`
      for embedded double-quotes. No literal in this file contains a `"`,
      so both fallbacks are equivalent in behaviour; choose raw strings
      where the target compiles at C# 11+. Whitespace nuance: Dart
      triple-quoted preserves leading whitespace exactly as written; C#
      raw strings strip a common indent matched to the closing `"""`
      column — codegen MUST emit the closing `"""` at column 0 (or
      adjust indentation) so the literal payload is byte-identical.
  - construct_key: dart.package_test.expect_equals
    source_form: "expect(<actual>, equals(<expected>));"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)` — note the
      ARGUMENT-ORDER FLIP (Dart `expect(actual, equals(expected))` puts
      actual first; xUnit `Assert.Equal(expected, actual)` puts expected
      first). The `equals` matcher uses Dart `==` equality; `Assert.Equal`
      uses `IEquatable<T>.Equals` / `Object.Equals`, which is equivalent
      for the value-typed comparisons in this file (`int`, `String`).
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (explicitly addressed — well-known footgun):
      reversing actual/expected silently produces correct-looking but
      misleading failure messages ("Expected: 'alice', Actual: 'alice'"
      vs the truth). Codegen MUST emit `Assert.Equal(expected, actual)`
      and the spec records the rule. Value-vs-reference nuance: the
      `equals` matcher in this file is only applied to `int` (list
      `.length`) and `String` (`.agentId`, `.goalFunctor`) — both have
      value semantics in both languages. Were `equals` applied to a
      reference type in another file, the idiom would need to be split
      (`Assert.Equal` uses structural equality via `Equals`;
      `Assert.Same` is reference equality — Dart `equals` matches the
      former).
  - construct_key: dart.package_test.expect_isNot_contains
    source_form: "expect(<actual>, isNot(contains('<substr>')));"
    target_decision: >-
      Map to xUnit `Assert.DoesNotContain("<substr>", <actual>)` for the
      string-containment cases in this file. xUnit has no `isNot`
      matcher-composition primitive; the composed `isNot(contains(...))`
      collapses to the dedicated `DoesNotContain` assertion. The
      counterpart positive form `expect(s, contains('x'))` maps to
      `Assert.Contains("x", s)` (used by one assertion in this file —
      see `expect(config.source, contains('procedure agent'))`).
    idiom_id: null
    research_finding_id: rf-dart-expect-isnot-contains-to-xunit-doesnotcontain
    nuance: >-
      Matcher-composition nuance (explicitly addressed): Dart's `matcher`
      package builds matchers compositionally (`isNot`, `allOf`,
      `anyOf`); xUnit has flat per-assertion methods. The conversion is
      NOT a generic `isNot(X)` -> `Assert.False(X-as-bool)` (that would
      lose the diagnostic message). Instead each common composition has
      a dedicated xUnit assertion: `isNot(contains(...))` ->
      `Assert.DoesNotContain`, `isNot(equals(...))` ->
      `Assert.NotEqual`, `isNot(isA<T>())` -> `Assert.IsNotType<T>`. The
      research finding enumerates the table; this file uses only the
      `DoesNotContain` and `Contains` mappings.
  - construct_key: dart.package_test.expect_throwsA_isA_having
    source_form: >-
      "expect(() => loader.load(source),
      throwsA(isA<BootLoaderException>().having((e) => e.message,
      'message', contains('<substr>'))));"
    target_decision: >-
      Map to xUnit `Assert.Throws<BootLoaderException>` capturing the
      returned exception, then a follow-on `Assert.Contains("<substr>",
      ex.Message)` against the captured exception's `Message` property.
      Concretely: `var ex = Assert.Throws<BootLoaderException>(() =>
      _loader.Load(source)); Assert.Contains("<substr>", ex.Message);`.
      The Dart `having((e) => e.message, 'message', contains('<substr>'))`
      describes a derived-field predicate over the thrown exception — the
      idiomatic xUnit form is the two-statement (Throws-then-Assert)
      pattern, not a single composite call.
    idiom_id: null
    research_finding_id: rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert
    nuance: >-
      Exception-matcher nuance (explicitly addressed): `throwsA(isA<T>())`
      asserts EXACT type match in xUnit terms — `Assert.Throws<T>` fails
      if a SUBTYPE of `T` is thrown (use `Assert.ThrowsAny<T>` for the
      subtype-tolerant variant); Dart `isA<T>` ALSO accepts subtypes, so
      strictly the faithful translation is `Assert.ThrowsAny<T>`.
      However, `BootLoaderException` in this file has no documented
      subclasses, so `Assert.Throws<BootLoaderException>` is observably
      equivalent here. The research finding records BOTH the precise
      mapping (`isA<T>` -> `Assert.ThrowsAny<T>`) and the in-this-file
      equivalence; codegen should emit `Assert.Throws<T>` UNLESS the
      target exception type has known subtypes in the converted code.
      Lambda nuance: Dart `() => loader.load(source)` (expression-body
      arrow) maps to C# `() => _loader.Load(source)` (identical
      syntax). Property-name nuance: Dart `e.message` (camelCase
      convention on exceptions) maps to C# `ex.Message` (PascalCase
      convention from `System.Exception.Message`).
  - construct_key: dart.package_test.expect_isTrue
    source_form: "expect(<bool-expr>, isTrue);"
    target_decision: >-
      Map to xUnit `Assert.True(<bool-expr>)`. Used once in this file
      (`expect(config.directives.every((d) => d.goalFunctor ==
      'agent_init'), isTrue)`). The Dart `Iterable.every(predicate)`
      maps to C# LINQ `Enumerable.All(predicate)`, so the full target
      becomes `Assert.True(config.Directives.All(d => d.GoalFunctor ==
      "agent_init"))`.
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      LINQ nuance (explicitly addressed): Dart `Iterable.every` =
      C# `Enumerable.All`; Dart `Iterable.any` = C# `Enumerable.Any`;
      Dart `Iterable.map(...).toList()` = C# `Enumerable.Select(...).ToList()`.
      Diagnostic nuance: `Assert.True(b)` without a message produces a
      generic "Assert.True() Failure" — if the converted test needs the
      Dart matcher's richer message, codegen may add the optional
      `userMessage` overload (`Assert.True(b, "<msg>")`); this file's
      single use is on a comprehensible predicate so the bare form
      suffices.
  - construct_key: dart.iterable.map_tolist_equals
    source_form: "expect(config.directives.map((d) => d.agentId).toList(), equals(['alice', 'bob', 'charlie']));"
    target_decision: >-
      Map to `Assert.Equal(new[] { "alice", "bob", "charlie" },
      config.Directives.Select(d => d.AgentId).ToList())` — xUnit
      `Assert.Equal` performs element-wise equality on `IEnumerable<T>`,
      matching Dart `equals` over `List<String>`.
    idiom_id: null
    research_finding_id: rf-dart-list-equality-to-xunit-assertequal-collection
    nuance: >-
      Collection-equality nuance (explicitly addressed): Dart `equals`
      over a `List` does element-wise comparison via the elements'
      `==`; xUnit `Assert.Equal(IEnumerable, IEnumerable)` uses the
      default `IEqualityComparer<T>` (which falls through to
      `IEquatable<T>.Equals`/`Object.Equals`). For `string` elements
      both behave identically. `List<T>` order-sensitivity matches in
      both languages (sequence-equality). The LINQ `.Select(...).ToList()`
      MUST materialise (matches Dart's eager `.toList()`); using just
      `.Select(...)` (deferred `IEnumerable<T>`) would also pass
      `Assert.Equal` (it iterates) but the materialisation makes
      diagnostic output identical.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System + SUT namespace from glp_runtime/multiagent/boot_loader.dart)
  - cu-2: namespace declaration mirroring the test/multiagent path
  - cu-3: top-level test class BootLoaderTests (from outer group label "BootLoader")
  - cu-4: private BootLoader _loader field (= null!) — late-field mapping
  - cu-5: constructor BootLoaderTests() assigning _loader = new BootLoader() — setUp mapping
  - cu-6: 6 `[Fact]` methods in the "valid boot files" group (parses-three-agent / two-agent / single-agent / handles-comments / flexible-whitespace / preserves-full-source-and-strips-boot), each `[Trait("Group", "valid boot files")]`, each with `[Fact(DisplayName = "<original label>")]`
  - cu-7: 5 `[Fact]` methods in the "error cases" group (no-procedure-boot / no-boot-clause / agent-ID-mismatch / duplicate-agent-IDs / no-spawn-directives), each `[Trait("Group", "error cases")]`, each using the Throws-then-Assert.Contains pattern
  - cu-8: 1 `[Fact]` method in the "real file content" group (parses-play_alice_bob_charlie_actor_boot_glp-content), `[Trait("Group", "real file content")]`, using LINQ All/Select for the list-equality assertions
  - cu-9: raw-string-literal payloads (`"""..."""`) for every embedded `.glp` source fixture, emitted at column 0 to preserve indentation byte-identically
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

This file is the SECOND `package:test` file specced; the first
(`test/multiagent/mad_error_handling_test.dart.md`) pinned xUnit as the
project-wide target framework. Maintaining that pin satisfies SC-007
(consistency via recorded idiom, not re-derivation). The authoritative
basis is the same: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp model, and the Dart
`package:test` README on `pub.dev` (`https://pub.dev/packages/test`)
for the `group` / `setUp` / `expect` / matcher semantics. NUnit and
MSTest remain corroborating alternatives, recorded once at the
import-idiom level — not re-derived per file.

### `late` field + `setUp` -> constructor + `null!`-initialised field

`late BootLoader loader;` inside the outer `group` callback is closed
over by `setUp` (which assigns it) and by every nested `test` (which
reads it). The xUnit constructor-per-test lifetime gives observably
identical "fresh per test" semantics
(`https://xunit.net/docs/shared-context`, "Constructor and Dispose").
The C# field shape `private BootLoader _loader = null!;` is the
documented "assigned-later non-nullable" pattern from the C# null-
safety reference
(`https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/nullable-reference-types`,
"Null-forgiving operator"). The alternative `BootLoader?` + `!` at
every read site was rejected because it inverts the "guaranteed-
initialised" semantics that `late` is meant to encode.

### Nested-group topology: FLATTEN with `[Trait]`

This file has 1 outer group + 3 inner groups + 12 tests. Three
target topologies are viable (see the construct's nuance). FLATTEN
into a single `BootLoaderTests` class with `[Trait("Group", "...")]`
per method is chosen because (a) every inner group shares the SAME
`_loader` from the outer setUp — splitting into per-inner-group
classes would force duplicating that field or introducing a shared
base; (b) `[Trait]` is the documented xUnit mechanism for ad-hoc
categorisation
(`https://xunit.net/docs/comparisons#categories`); and (c) reporters
(VS Test Explorer, `dotnet test --logger trx`, Rider) render `[Trait]`
groupings, so the human-readable group structure survives. The
test-method names are group-prefixed to prevent collisions
(`ErrorCases_ThrowsIfNoBootClause` vs a hypothetical `ValidBootFiles_*`
of the same label).

### Argument-order flip on `Assert.Equal`

The Dart `expect(actual, equals(expected))` convention puts actual
first; xUnit `Assert.Equal(expected, actual)` puts expected first
(`https://xunit.net/docs/comparisons#assertions`). Every `equals(...)`
call in this file MUST be flipped at the boundary. This is the most
common silent-bug source when porting test code between the two
ecosystems and is flagged here so codegen does not lose it.

### `throwsA(isA<T>().having(...))` -> Throws-then-Assert

Each of the 5 error-case tests asserts both the exception TYPE and a
substring of `e.message`. xUnit's idiomatic two-step
(`var ex = Assert.Throws<T>(...); Assert.Contains("...", ex.Message);`)
is preferred over a single composite call. xUnit docs
(`https://xunit.net/docs/comparisons#exceptions`) document this as
the canonical exception-and-message assertion pattern. The exact-
vs-subtype nuance is recorded in the construct's `nuance` field:
`isA<T>` is subtype-tolerant (matches `Assert.ThrowsAny<T>`), but
`BootLoaderException` has no known subclasses in this codebase, so
`Assert.Throws<T>` is observably equivalent here. If `boot_loader.dart`
later introduces a subclass, this file's spec must be revisited
(recorded as a forward-looking note, not an escalation).

### Triple-quoted multi-line string fixtures

Every test embeds a `.glp` source fixture via Dart triple-single-quoted
strings. C# 11 raw string literals (`"""..."""`) are the closest match
(`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/raw-string`):
no escape processing, multi-line, leading-indent normalisation matched
to the closing delimiter's column. Codegen MUST emit the closing
`"""` at the appropriate column (typically 0 if the payload is at
column 0 in the fixture) so the literal payload is byte-identical to
the Dart source; mis-indented closing delimiters silently change the
parsed input. Verbatim strings (`@"..."`) are a fallback for pre-C#11
targets — equivalent here because no fixture contains a `"`.

### LINQ mappings (in-file uses)

The "real file content" test uses `directives.map((d) => d.agentId)
.toList()` and `directives.every((d) => d.goalFunctor == 'agent_init')`.
The official `System.Linq` reference
(`https://learn.microsoft.com/dotnet/api/system.linq.enumerable`) gives
the canonical mappings: `Iterable.map` = `Select`, `Iterable.toList` =
`ToList`, `Iterable.every` = `All`, `Iterable.any` = `Any`. These are
recorded under `rf-dart-list-equality-to-xunit-assertequal-collection`
and `rf-dart-expect-istrue-to-xunit-asserttrue` so they are reused
verbatim in every subsequent test convspec that touches collections.

### Why no escalations

Every construct has a clear, single-decision target shape grounded in
official documentation for both Dart `package:test` and xUnit/.NET.
The two "soft" decisions (xUnit vs NUnit/MSTest; FLATTEN vs nested
classes) are documented project-wide policy with corroborating
alternatives in their research findings, not unresolved choices. The
`Assert.Throws<T>` vs `Assert.ThrowsAny<T>` exact-vs-subtype call is a
deliberate, in-file-justified choice (no known subclasses), not an
undecidable point. `escalations: []` is therefore intentional, not a
placeholder.
