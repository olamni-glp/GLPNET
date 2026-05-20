> Conversion-spec artifact for test/multiagent/global_writers_table_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/global_writers_table_test.dart
source_sha256: e94c973b8effdbc9fc3bc538634735c630dab2064acb5ec8dcd9f856a0c5e45e
target_code_unit: test/multiagent/GlobalWritersTableTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the batch-wide test
      framework already pinned by the sibling test-file specs
      (test/smoke_test.dart.md and test/multiagent/mad_error_handling_test.dart.md);
      this file MUST reuse that idiom (FR-012 / SC-007) — no
      re-research. The .NET test project (.csproj — out of this single-file
      artifact's scope) provides `xunit` + `xunit.runner.visualstudio` +
      `Microsoft.NET.Test.Sdk` NuGet refs. Codegen also adds `using System;`
      at file scope for completeness (no exception-typed asserts in THIS
      file, but the namespace is referenced by future maintenance edits) and
      projects to a single namespace mirroring the Dart `test/multiagent`
      directory (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit and MSTest are recorded alternatives in the
      research-finding row but are NOT used here. The full
      `package:test`-to-xUnit shape mapping (import drop + class-with-Facts
      + matcher routing table) is detailed in the sibling test-file specs
      and reused verbatim — this file introduces NO new framework-level
      surface (no setUp/tearDown, no setUpAll/tearDownAll, no skip, no
      tags, no async). Module/namespace nuance: Dart's `package:test`
      exposes top-level functions (`group`, `test`, `expect`, `isTrue`,
      `isNotNull`, `isNull`) re-exported via the one import; xUnit has NO
      top-level test functions — tests are public instance methods on a
      public class discovered via `[Fact]` reflection. No async/Future/
      Stream/isolate surface in this file.
  - construct_key: dart.package_test.import_sut_relative_package
    source_form: "import 'package:glp_runtime/multiagent/global_writers_table.dart';"
    target_decision: >-
      The second import is a SUT (system-under-test) reference — the Dart
      `package:glp_runtime/...` URI resolves to the converted C# namespace
      for the same source unit. Replace with a C# `using` directive that
      names the namespace the converted `global_writers_table.dart` will
      emit into, e.g. `using <RootNs>.Multiagent;`. The exact namespace
      string is determined by the SUT file's own conversion-spec
      (`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`,
      a sibling spec produced separately); this test-file spec records the
      DEPENDENCY relationship — codegen MUST emit a `using` that resolves
      the symbols `GlobalWritersTable` (the class), `GlobalizeEntry`,
      `LocalizeEntry` (the entry types referenced indirectly via the
      class API), since the test calls `GlobalWritersTable('p')` and
      `table.addGlobalizeEntry(...)`/`table.addLocalizeEntry(...)`/
      `table.findByRemote(...)` etc. Per-file working-directory
      convention from feature 016/017 (`<file>__/`) means the SUT and
      test live in sibling working dirs; the `using` resolves through
      the test .csproj's project-reference to the runtime .csproj
      (langpair-level concern, OUT OF SCOPE here — recorded for codegen
      cross-file wiring).
    idiom_id: null
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed): a `package:`
      import that resolves to an in-repo Dart library (NOT to a
      pub.dev third-party package) maps to a C# `using <Namespace>;`
      that targets the OUTPUT namespace of the converted Dart library —
      NOT a separate NuGet reference. This contrasts with
      `package:test`, which IS a third-party dependency and maps to a
      NuGet reference + `using Xunit;`. The conversion MUST distinguish
      the two cases by inspecting the `package:` URI: `package:glp_runtime/...`
      is the in-repo Dart library (Dart `pubspec.yaml` `name: glp_runtime`);
      any other `package:foo/...` would be a third-party dep needing its
      own NuGet decision. Project-file wiring (a `<ProjectReference>` from
      the test .csproj to the runtime .csproj) is langpair/project-skeleton
      level, not per-file — recorded so codegen knows a `using` alone is
      insufficient without the project reference.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('GlobalWritersTable', () { test(...); ... }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The single `group(...)` call inside `main` becomes the
      enclosing test class (next construct).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed): Dart `main` runs once per
      test-file process and registers tests; xUnit has no per-file hook —
      only per-class (constructor + `IDisposable.Dispose`) and
      per-collection fixtures. THIS file's `main` body is exactly one
      `group()` call with no other statements, so omitting `main` is
      lossless. If future maintenance adds top-of-main setup, that setup
      MUST migrate into the enclosing class's constructor or an
      `IClassFixture<>` — same rule as the sibling
      mad_error_handling_test.dart spec.
  - construct_key: dart.package_test.group_block
    source_form: "group('GlobalWritersTable', () { test(...); test(...); ... });"
    target_decision: >-
      The Dart `group('GlobalWritersTable', body)` maps to a `public
      class GlobalWritersTableTests` whose name encodes the group label
      in PascalCase with the conventional `Tests` suffix. The original
      label MAY be preserved via `[Trait("Group", "GlobalWritersTable")]`
      on the class for reporter parity. No nested `group(...)`, no
      `setUp`/`tearDown` inside the group — each test constructs its
      own `GlobalWritersTable` instance locally (the
      Given/When/Then-prologue pattern), so xUnit's per-test fresh-instance
      lifecycle (Microsoft Learn / xunit.net: "xUnit.net creates a new
      instance of the test class for every test that is run") maps
      cleanly with NO shared state and NO constructor-side fixture
      needed.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (explicitly addressed): the Dart group label
      `'GlobalWritersTable'` is already a valid C# identifier, so the
      mangle is trivial (append `Tests`). Where Dart labels contain
      spaces or punctuation (e.g. `'index 0 is reserved for serializer'`
      on individual tests below), the per-test method-name mangling
      strips non-identifier chars and PascalCases. Lifecycle nuance:
      no `setUp`/`tearDown` in this file's group — but the IDIOM record
      MUST capture the mapping (Dart group `setUp` → xUnit constructor;
      group `tearDown` → `IDisposable.Dispose`) since it will fire on
      any sibling test file that uses them. Nested-group nuance: not
      used here; would map to nested classes or collection fixtures
      (recorded but not emitted).
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('<label>', () { /* Given/When/Then with executable
      arrange-act-assert */ });" — applied to all 9 test cases in this
      file (none use `skip:`).
    target_decision: >-
      Each Dart `test(label, body)` (no `skip` argument) becomes a
      `public void` method on the enclosing class, decorated with `[Fact]`
      (NOT `[Fact(Skip=...)]` — this file's tests are executable,
      contrast with mad_error_handling_test.dart where all 5 are
      `[Fact(Skip="Not yet implemented")]`). Method name = label
      PascalCased with non-identifier chars stripped (e.g.
      `'index 0 is reserved for serializer'` →
      `Index0IsReservedForSerializer`,
      `'addGlobalizeEntry allocates sequential indices starting at 1'` →
      `AddGlobalizeEntryAllocatesSequentialIndicesStartingAt1`). Original
      label preserved verbatim via `[Fact(DisplayName = "<label>")]` so
      runner output keeps the sentence-form name. Method body translates
      the Dart arrange-act-assert verbatim, with `expect(actual, matcher)`
      calls routed to xUnit `Assert.*` per the matcher-routing idiom (next
      constructs). The Given/When/Then comments MUST be carried into the
      target as a `/// <summary>` doc-comment block per method so spec
      traceability (Spec Section 3.x / 4.1 / 8.3 / 11.2 references)
      survives the conversion.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Method-body translation nuance (explicitly addressed): every `test`
      callback in THIS file is synchronous (no `async`/`Future`/`await`);
      target method returns `void` (xUnit also supports `async Task` for
      async tests — not applicable here). Closure-capture nuance: no
      `setUp` variables — every `final table = GlobalWritersTable('p');`
      is local to the test body, mapping 1-to-1 to a local `var table =
      new GlobalWritersTable("p");` in the C# method (see next
      construct on `final` ⇒ `var`). No `Future` await, no `Stream`,
      no `Completer`. Skip-semantics nuance (NOT firing here, but
      contrasting with mad_error_handling_test.dart): no `skip:`
      argument anywhere, so NO `Skip=` property on `[Fact]`.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: "final table = GlobalWritersTable('p');  // and similar `final i1 = ...`, `final entry = ...`"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a constructor invocation or a method call
      that returns a non-null reference, AND translate to `<Type> <name>
      = <expr>;` with the explicit type ONLY where C# type inference
      would otherwise lose information (not applicable in this file —
      every `final` here binds a reference whose static type is
      inferable from the initializer). Specifically: `final table =
      GlobalWritersTable('p')` ⇒ `var table = new GlobalWritersTable("p");`
      (note the C# `new` keyword — Dart's optional-`new` constructor call
      requires C#'s mandatory `new`); `final i1 = table.addGlobalizeEntry(100,
      'q')` ⇒ `var i1 = table.AddGlobalizeEntry(100, "q");` (camelCase
      method names PascalCase in C# per language convention); `final
      entry = table.findByRemote('p', 5)` ⇒ `var entry =
      table.FindByRemote("p", 5);` (the return is nullable — see
      null-aware constructs below).
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (explicitly addressed): Dart `final
      <local>` prevents REBINDING the local after init but does NOT
      prevent mutation of the referenced object's state — exactly the
      same semantics as C# `var` (which is `readonly`-style only when
      declared `readonly` at field scope; LOCAL `var` is freely
      rebindable). The semantic-tightest C# equivalent of Dart's local
      `final` is actually no direct equivalent — C# 7+ has no
      `readonly` modifier for locals. The conversion ACCEPTS this minor
      semantic loss because (a) Dart `final`'s no-rebind constraint is
      enforced by the compiler at the same point in time C# would
      detect a rebind anyway (in the same method body, by code review
      / linting), and (b) C# 12 `readonly` locals do not exist; the only
      alternative — `using var` or wrapping in a `record` — is heavier
      than the readability win. Constructor-syntax nuance: Dart allows
      `Foo(...)` without `new`; C# requires `new Foo(...)`. String
      literals: Dart `'p'` and `"p"` are equivalent (both string
      literals); C# uses ONLY `"..."` (single quotes are `char`).
      Codegen MUST emit `new GlobalWritersTable("p")`, NOT
      `new GlobalWritersTable('p')` (the latter is a `char`-arg
      constructor that does not exist on the SUT).
  - construct_key: dart.package_test.expect_isTrue_matcher
    source_form: "expect(table.hasSerializerEntry, isTrue);"
    target_decision: >-
      `expect(x, isTrue)` ⇒ `Assert.True(x);` per the matcher-routing
      table already pinned by smoke_test.dart's
      rf-dart-expect-isTrue-to-xunit-assert-true idiom. THIS file uses
      it twice: `expect(table.hasSerializerEntry, isTrue);` (×2, lines
      40 + 67). Codegen MUST also rename the Dart getter
      `hasSerializerEntry` to C# property `HasSerializerEntry` (Dart
      lowerCamelCase → C# PascalCase for public members) per the
      cross-cutting Dart-getter-to-C#-property idiom (sibling
      lib-spec rf-dart-getter-to-csharp-property already records this
      naming convention for getters; reused here verbatim).
    idiom_id: null
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Strict-boolean nuance (explicitly addressed): Dart `isTrue` and
      xUnit `Assert.True(bool)` both REQUIRE a `bool` argument — no
      truthiness coercion, no null acceptance. The SUT's
      `hasSerializerEntry` is a bool-returning getter, so the mapping
      is direct. Diagnostic message: xUnit's `Assert.True(bool)`
      produces a generic "Assert.True() Failure" on failure; Dart's
      matcher produces a rich "Expected: true / Actual: false" message
      — minor diagnostic-quality loss, accepted (smoke_test.dart spec
      records the same trade-off).
  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: "expect(entry, isNotNull);  // and `expect(table.lookupByIndex(2), isNotNull);`, `expect(table.findByRemote('p', 5), isNotNull);`"
    target_decision: >-
      `expect(x, isNotNull)` ⇒ `Assert.NotNull(x);` per the
      matcher-routing table pinned by smoke_test.dart. Used 3× in this
      file (lines 99, 153, 168). xUnit `Assert.NotNull(object)` throws
      `NotNullException` on null, otherwise passes — strict
      null-vs-not-null semantics identical to Dart `isNotNull`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Null-safety nuance (explicitly addressed): Dart's `package:test`
      `isNotNull` matches any non-null value (including `false`, `0`,
      empty string — Dart has no truthiness coercion); xUnit
      `Assert.NotNull(object?)` is identically strict. The xUnit
      signature is `Assert.NotNull(object? @object)` — the parameter is
      a nullable `object?`, so the argument is implicitly upcast.
      Nullable-reference-types (C# NRT) nuance: in `#nullable enable`
      mode, after `Assert.NotNull(entry)` the C# flow-analyzer does
      NOT narrow `entry`'s static type to non-nullable (xUnit's
      `Assert.NotNull` is not flow-annotated with `[NotNull]` in older
      versions, though xUnit ≥2.5 adds `[NotNull]` post-condition).
      Codegen SHOULD prefer the `Assert.NotNull(actual)` form; downstream
      uses of `entry.WriterAddr` rely on either xUnit's `[NotNull]`
      annotation OR an explicit null-forgiving operator `entry!.WriterAddr`
      (the latter matches the Dart source's `entry!` operator at line
      100 — see next construct).
  - construct_key: dart.package_test.expect_isNull_matcher
    source_form: "expect(table.findByRemote('p', 2), isNull);  // and 4 other uses"
    target_decision: >-
      `expect(x, isNull)` ⇒ `Assert.Null(x);` per the matcher-routing
      table. Used 5× in this file (lines 136, 137, 152, 176, and the
      composed `?.writerAddr` cases below). xUnit `Assert.Null(object?)`
      throws `NotNullException` on non-null (asymmetric name vs.
      `Assert.NotNull`), otherwise passes.
    idiom_id: null
    research_finding_id: rf-dart-expect-isNull-to-xunit-assert-null
    nuance: >-
      Null-strictness nuance (explicitly addressed): Dart `isNull` and
      xUnit `Assert.Null` are both strict reference-null checks; no
      truthy/falsy coercion on either side. The composed source
      expression `table.findByRemote('p', 2)` returns a NULLABLE
      `LocalizeEntry?` in Dart; the converted SUT returns
      `LocalizeEntry?` in C# (NRT enabled per the project-wide
      null-safety idiom rf-dart-nullsafety-to-csharp-nrt, already pinned
      by lib/analysis/analysis_phase.dart.md). xUnit `Assert.Null`
      accepts the nullable reference directly — no extra cast.
  - construct_key: dart.package_test.expect_equals_implicit_matcher
    source_form: >-
      "expect(table.nextIndex, 1);  // and `expect(i1, 1);`, `expect(i2, 2);`,
      `expect(table.serializerWriterAddr, 999);`, `expect(entry!.writerAddr, 100);`,
      `expect(entry.remoteAgent, 'p');`, `expect(entry.remoteIndex, 5);`,
      `expect(table.findByRemote('p', 0)?.writerAddr, 100);`, etc."
    target_decision: >-
      Dart `expect(actual, value)` (where the second argument is a
      non-matcher value rather than a `Matcher`) is sugar for
      `expect(actual, equals(value))` per the `package:test` /
      `package:matcher` rule: the matcher second-argument auto-wraps
      bare values in `equals(...)`. Translate to
      `Assert.Equal(expected, actual);` with the EXPECTED value FIRST
      and the ACTUAL second — this is the xUnit argument order, which
      is the INVERSE of Dart's `expect(actual, equals(expected))`.
      Codegen MUST swap the argument order. Used ≈14× in this file.
      Examples:
      `expect(table.nextIndex, 1)` ⇒ `Assert.Equal(1, table.NextIndex);`;
      `expect(entry.remoteAgent, 'p')` ⇒ `Assert.Equal("p", entry.RemoteAgent);`;
      `expect(table.findByRemote('p', 0)?.writerAddr, 100)` ⇒
      `Assert.Equal(100, table.FindByRemote("p", 0)?.WriterAddr);`.
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order footgun (explicitly addressed): Dart
      `expect(actual, equals(expected))` has actual-first; xUnit
      `Assert.Equal<T>(T expected, T actual)` has expected-first. This
      is the EASY-TO-INVERT inversion that smoke_test.dart's spec
      pre-flagged for sibling reuse. Codegen MUST swap. Value-vs-reference
      nuance: this file's expected values are
      `int` literals (1, 2, 3, 5, 100, 200, 300, 999, 1001) and
      `String` literals ('p', 'q'). C# `int` and `string` both implement
      structural equality via `IEquatable<T>`, so `Assert.Equal` does the
      right thing without overload selection. Width nuance: per the
      cross-cutting idiom rf-dart-int-to-csharp-long-width (pinned by
      lib/bytecode/opcodes_v2.dart.md), Dart `int` ⇒ C# `long` for
      generic numeric semantics. THIS file's literal values (≤1001) are
      well within `int` range, but the SUT's `addGlobalizeEntry`
      RETURN-type (Dart `int`) converts to C# `long` under the
      pinned idiom — therefore `Assert.Equal(1, i1)` works because xUnit
      `Assert.Equal<long>(long expected, long actual)` selects the
      `long` overload and the literal `1` is implicitly widened. NO
      argument-order issue here other than the Dart/xUnit inversion.
      Tuple-equality / list-equality nuance: not used in this file —
      all comparisons are scalar (`int`, `bool`, `string`).
  - construct_key: dart.expression.null_assertion_bang_operator
    source_form: "entry!.writerAddr  // and similar `entry!.writerAddr` at line 100"
    target_decision: >-
      Dart's null-assertion operator `entry!` (asserts non-null at
      runtime, throws `TypeError` if null) maps to C#'s null-forgiving
      operator `entry!` (compile-time annotation only — does NOT throw,
      just silences the NRT warning). The semantic difference is
      load-bearing and MUST be addressed: in C#, after `Assert.NotNull(entry)`
      on the preceding line, the runtime guarantee is already in place
      (xUnit threw if null); the `!` then silences the NRT warning
      without adding a runtime check. Translate `entry!.writerAddr` ⇒
      `entry!.WriterAddr` (PascalCased property name). If
      `Assert.NotNull` were absent before the dereference, codegen would
      emit `entry!.WriterAddr` AND insert an explicit
      `Assert.NotNull(entry);` line to preserve the Dart runtime-throw
      semantics — but in THIS file every `!` usage IS preceded by
      `expect(entry, isNotNull)` on the immediately previous line, so
      no extra assert is needed.
    idiom_id: null
    research_finding_id: rf-dart-bang-operator-to-csharp-null-forgiving
    nuance: >-
      Runtime-vs-compile-time nuance (explicitly addressed, NOT glossed):
      Dart `!` is a RUNTIME null-check that throws `TypeError` if the
      operand is null; C# `!` is a COMPILE-TIME NRT annotation that
      emits no runtime code (it only suppresses the warning). The
      semantic gap is closed in this file because every `!` follows an
      `Assert.NotNull` (xUnit throws on null, so the program never
      reaches the `!` with a null operand). Codegen MUST audit each
      `!` translation against this precondition: if the preceding
      statement is NOT an `Assert.NotNull` of the same expression,
      codegen MUST insert one (or use `entry ?? throw new InvalidOperationException()`
      as the runtime-throw equivalent). This is a CONVERSION INVARIANT
      that any future Dart-`!`→C#-`!` mapping MUST preserve.
  - construct_key: dart.expression.null_aware_member_access_operator
    source_form: "table.findByRemote('p', 0)?.writerAddr  // and 2 other uses"
    target_decision: >-
      Dart `x?.y` (null-aware member access — returns `null` if `x` is
      null, otherwise `x.y`) maps DIRECTLY to C# `x?.y` (same
      semantics, same syntax). Translate
      `table.findByRemote('p', 0)?.writerAddr` ⇒
      `table.FindByRemote("p", 0)?.WriterAddr`. Used inside
      `expect(..., 100)` ⇒ `Assert.Equal(100, ...)` — the result type is
      `long?` (Dart `int?` ⇒ C# `long?` under the project's width
      idiom + NRT), and `Assert.Equal<long?>(long? expected, long? actual)`
      handles the nullable-int comparison correctly (the literal `100`
      is implicitly widened from `int` to `long?` via implicit
      conversion + nullable wrapping).
    idiom_id: null
    research_finding_id: rf-dart-null-aware-access-to-csharp-null-conditional
    nuance: >-
      Direct-mapping nuance (explicitly addressed): Dart `?.` and C# `?.`
      are 1-to-1 in both syntax and semantics — both short-circuit on
      `null` and return `null` from the entire expression. No
      conversion-time decision needed beyond renaming
      `findByRemote`/`writerAddr` to PascalCase
      `FindByRemote`/`WriterAddr` (member-naming idiom). Generic
      argument-inference nuance: xUnit's `Assert.Equal<T>` infers `T`
      from the EXPECTED argument first; here `100` is `int` literal,
      `?.WriterAddr` is `long?` — the implicit conversion `int` →
      `long?` is fine, but if the compiler picks `T = int` based on
      `expected`, the `long?` actual would fail compilation. Codegen
      SHOULD emit an explicit cast `Assert.Equal<long?>(100L, table.FindByRemote("p", 0)?.WriterAddr)`
      OR `Assert.Equal((long?)100, ...)` to pin the generic type — this
      is the only non-trivial generic-inference nuance in the file.
conversion_units:
  - "cu-1: file-scope using directives (Xunit + System + <RootNs>.Multiagent for SUT)"
  - "cu-2: namespace declaration mirroring test/multiagent path (<RootNs>.Test.Multiagent)"
  - "cu-3: top-level class GlobalWritersTableTests (from group label 'GlobalWritersTable') with optional [Trait(\"Group\", \"GlobalWritersTable\")]"
  - "cu-4: 9 [Fact(DisplayName=\"<original label>\")] public void methods, one per Dart test() call, all executable (NO Skip), all with /// <summary> carrying the original Given/When/Then comments + spec-section references (3.2, 4.1, 8.3, 11.2)"
  - "cu-5: per-method body: arrange-act-assert translation with `var table = new GlobalWritersTable(\"p\")`-style local declarations, expect() ⇒ Assert.* per matcher-routing idiom (isTrue/isNotNull/isNull/equals-implicit), `!`/`?.` operators preserved 1-to-1 with the Dart runtime-vs-compile-time-semantics caveat documented under construct dart.expression.null_assertion_bang_operator"
  - "cu-6: explicit generic-type pinning on Assert.Equal<long?>(...) where the expected literal+actual nullable-long pair would otherwise force the compiler to infer T from the literal — applies only to the 3 `?.writerAddr` cases (lines 133–135)"
escalations: []
```

## Rationale + research provenance

### Why all 9 tests are `[Fact]` (NOT `[Fact(Skip=...)]`)

Every `test(...)` call in this file has executable arrange-act-assert in
the body (no `skip:` argument anywhere). Contrast with the sibling
mad_error_handling_test.dart, where all 5 tests are `skip: 'Not yet
implemented'` and map to `[Fact(Skip="Not yet implemented")]`. The same
test-framework-mapping idiom (xUnit, `[Fact]` per Dart `test()`) applies
to both files; the only difference is the absence of the `Skip=`
argument. THIS file therefore reuses the framework idiom and the
test-callback idiom verbatim from the sibling specs and adds NO new
skip-related surface.

### Reuse from sibling test-file specs (FR-012 / SC-007)

Idiom KB reuse (no re-research) per FR-012:

- `rf-dart-package-test-to-dotnet-xunit` — framework choice pinned by
  smoke_test.dart.md and mad_error_handling_test.dart.md. xUnit selected
  as batch-wide default. Authoritative source: Microsoft Learn
  `unit-testing-csharp-with-xunit` + xunit.net docs.
- `rf-dart-test-main-to-xunit-class-with-facts` — drop `main`, lift
  registered tests to `[Fact]` methods on a class. Authoritative
  source: pub.dev `test` API + Microsoft Learn + xunit.net "Shared
  Context between Tests".
- `rf-dart-package-test-group-to-xunit-class` — `group(label, body)` ⇒
  `public class <Label>Tests`. Authoritative source: pub.dev `test`
  group API + Microsoft Learn xUnit test-class discovery.
- `rf-dart-test-callback-to-xunit-method-body` — Dart test callback
  closure becomes the method body of the `[Fact]` method (no closure
  object materialised). Sibling specs cover this verbatim.
- `rf-dart-expect-isTrue-to-xunit-assert-true` — pinned by smoke_test.
  dart's matcher-routing table. Authoritative source: pub.dev `package:matcher`
  `isTrue` constant + xunit.net `Assert.True`.

### New matcher-routing rows (recorded here as new research findings)

- `rf-dart-expect-isNotNull-to-xunit-assert-notnull`: pub.dev
  `package:matcher` `isNotNull` constant
  (`https://pub.dev/documentation/matcher/latest/matcher/isNotNull-constant.html`)
  documents strict non-null matching; xunit.net's `Assert.NotNull(object?)`
  (`https://xunit.net/docs/comparisons` and the API reference) is the
  direct counterpart. Both sides authoritative.
- `rf-dart-expect-isNull-to-xunit-assert-null`: pub.dev
  `package:matcher` `isNull` constant + xunit.net `Assert.Null(object?)`.
  Strict reference-null semantics on both sides. Both authoritative.
- `rf-dart-expect-equals-to-xunit-assert-equal-argorder`: pub.dev
  `expect` documentation
  (`https://pub.dev/documentation/test_api/latest/expect/expect.html`)
  states that bare-value second arguments are wrapped in `equals(...)`;
  xunit.net `Assert.Equal<T>(T expected, T actual)` signature
  (`https://xunit.net/docs/comparisons#assertions` and the
  `Xunit.Assert.Equal` API reference) is expected-first / actual-second.
  Argument-order inversion is the load-bearing nuance, explicitly
  recorded so future codegen does NOT silently swap. Both sides
  authoritative.

### New non-test-framework idioms recorded by this file

- `rf-dart-package-sut-import-to-csharp-using`: in-repo `package:<this-pubspec-name>/...`
  imports map to `using <ConvertedNamespace>;` rather than NuGet
  references. Authoritative Dart side: dart.dev language tour
  `imports` (`https://dart.dev/language/libraries#using-libraries`)
  + Dart pubspec resolution
  (`https://dart.dev/tools/pub/pubspec`). Authoritative .NET side:
  Microsoft Learn `using` directive
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/using-directive`)
  + `<ProjectReference>` MSBuild docs
  (`https://learn.microsoft.com/visualstudio/msbuild/common-msbuild-project-items#projectreference`).
  Both sides authoritative.
- `rf-dart-final-local-to-csharp-var-local`: Dart language tour
  `final` and `const`
  (`https://dart.dev/language/variables#final-and-const`); C# `var`
  + locals reference
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/statements/declarations`).
  Minor semantic-loss noted (no C# `readonly` local) — accepted as
  the conventional mapping. Both sides authoritative.
- `rf-dart-bang-operator-to-csharp-null-forgiving`: dart.dev null-safety
  `!` operator
  (`https://dart.dev/null-safety/understanding-null-safety#null-assertion-operator`)
  documents the RUNTIME-throw semantics; Microsoft Learn null-forgiving
  operator
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/null-forgiving`)
  documents the COMPILE-TIME-annotation semantics. The runtime-vs-compile-time
  gap is the load-bearing nuance — codegen MUST audit that each `!`
  is preceded by an actual null-check (e.g. `Assert.NotNull`) to
  preserve Dart's throw-on-null guarantee. Both sides authoritative.
- `rf-dart-null-aware-access-to-csharp-null-conditional`: dart.dev
  `?.` operator
  (`https://dart.dev/null-safety/understanding-null-safety#null-aware-access-operators`)
  + Microsoft Learn `?.` and `?[]` operators
  (`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/member-access-operators#null-conditional-operators--and-`).
  Direct 1-to-1 mapping. Generic-inference nuance on
  `Assert.Equal<T>(...)` recorded but resolvable by explicit type
  argument. Both sides authoritative.

### Why no escalations

Every construct in this file is authoritative-supported on both
sides. The matcher routing table is mostly already pinned by sibling
test specs; the three new rows (`isNotNull`, `isNull`, implicit
`equals`) cite official Dart and xUnit documentation. The two null-aware
operators (`!`, `?.`) have direct C# equivalents with one load-bearing
nuance each (runtime-vs-compile-time for `!`, generic-inference for
`?.` inside `Assert.Equal`), both fully resolved in the construct
rows. The SUT-import mapping is straightforward in-repo cross-file
wiring. NO idiom-vs-research conflict, NO idiom-vs-idiom conflict,
NOTHING undecidable. The `escalations: []` is intentional, not a
placeholder.

### Cross-file dependency note

The SUT's own conversion-spec
(`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`,
produced by a separate convspec run) is the source of truth for the
exact emitted namespace + class signature + member names. THIS test
spec records the DEPENDENCY (`using <RootNs>.Multiagent;` +
`<ProjectReference>`) but does NOT pin the SUT's namespace string —
that pinning is the SUT spec's responsibility. Codegen wiring must
join the two specs at the project-skeleton level (langpair / 016-init
scope, OUT OF this single-file artifact).

### Spec-section traceability preserved

The Dart source documents 11 spec-section references in inline
comments (Spec Sections 3.1, 3.2, 4.1, 8.3, 11.2). Each must be
carried into the corresponding C# method's `/// <summary>` XML-doc
block — this is the spec-only-no-guessing discipline (FR-013/023) at
the doc-comment level: the conversion preserves the invariant-tracing
the test file documents, even though the doc-comment block is
non-executable. NOT a separate construct row because it is uniform
across all 9 tests and falls under the test-callback idiom's
already-recorded `/// <summary>` carry-over requirement.
