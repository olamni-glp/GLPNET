> Conversion-spec artifact for test/module/module_syntax_v2_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/module/module_syntax_v2_test.dart
source_sha256: fb04dca7a515ac9c443e9a2a0e24262ce22a82dfee26382aae9b5131e153363a
target_code_unit: test/module/ModuleSyntaxV2Test.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and
      replace it with the xUnit-equivalent surface area at the file
      level: `using Xunit;` (per Microsoft Learn xUnit getting-started
      tutorial). The .NET test project (a separate .csproj) MUST
      reference the `xunit` + `xunit.runner.visualstudio` /
      `Microsoft.NET.Test.Sdk` NuGet packages — out of scope for this
      per-file artifact (langpair-level concern). xUnit is the
      batch-wide test-framework idiom established by sibling
      `test/smoke_test.dart` and MUST be reused here (FR-012 / SC-007),
      not re-decided.
    idiom_id: null
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework choice (batch-wide reuse, explicitly addressed): the
      framework choice was researched and decided in
      `test/smoke_test.dart`'s convspec (research finding
      rf-dart-package-test-to-dotnet-xunit) and is reused verbatim
      here without re-derivation, per KB decision order #2
      (idiom-KB hit ⇒ REUSE; no research, no re-derive). Same
      rationale: Microsoft Learn's "Unit testing C# in .NET Core using
      dotnet test and xUnit" is the .NET-blessed canonical tutorial,
      and xUnit's `[Fact]` / `Assert.*` surface is the closest
      semantic shape to Dart `test()` / `expect()`. Module/namespace
      semantics: Dart `import 'package:test/test.dart'` exposes the
      top-level test-registration API; xUnit replaces it with
      attribute-driven discovery on a public class — the import is
      not 1-to-1 with `using Xunit;` alone (the file's `void main()`
      shape also rewrites — see next construct).
  - construct_key: dart.intra_package.import_directive_to_using_namespace
    source_form: >-
      import 'package:glp_runtime/compiler/lexer.dart';
      (also: parser.dart, ast.dart, error.dart,
      analysis/type_checker/type_ast.dart)
    target_decision: >-
      Replace each `import 'package:glp_runtime/<sub>/<file>.dart';`
      with the C# `using` directive for the namespace produced by the
      langpair's namespace-mapping convention. For this batch the
      convention (recorded as a langpair-level idiom, not per-file)
      is: `package:glp_runtime/<a>/<b>/file.dart` ⇒ namespace
      `GlpRuntime.<A>.<B>` (PascalCased path segments, file name
      itself dropped — Dart files are libraries, C# namespaces are
      coarser-grained units that contain many type declarations). So
      `package:glp_runtime/compiler/lexer.dart` ⇒ `using
      GlpRuntime.Compiler;`, `package:glp_runtime/compiler/parser.dart`
      ⇒ `using GlpRuntime.Compiler;` (already covered — deduplicate),
      `package:glp_runtime/compiler/ast.dart` ⇒ `using
      GlpRuntime.Compiler;` (deduplicate), `package:glp_runtime/compiler/error.dart`
      ⇒ `using GlpRuntime.Compiler;` (deduplicate),
      `package:glp_runtime/analysis/type_checker/type_ast.dart` ⇒
      `using GlpRuntime.Analysis.TypeChecker;`. Net result for this
      file: TWO `using` lines plus `using Xunit;` (vs. five Dart
      imports), because four Dart imports collapse into the single
      `GlpRuntime.Compiler` namespace under the C# package-coarsening
      rule.
    idiom_id: null
    research_finding_id: rf-dart-package-import-to-csharp-using-namespace
    nuance: >-
      Granularity mismatch (explicitly addressed, load-bearing): Dart
      imports are FILE-GRAINED (one `import` per library file; each
      `.dart` file is its own library by default), while C# `using`
      directives are NAMESPACE-GRAINED (one `using` per namespace,
      and a namespace can be declared by many `.cs` files). The
      langpair convention coarsens directory→namespace; sibling Dart
      files in the same directory therefore share one C# `using`. No
      conditional imports / `show` / `hide` / `as` aliasing in this
      file (the well-known `as`-aliasing nuance — Dart `import 'x.dart'
      as foo;` ⇒ C# `using foo = Some.Namespace;` extern-alias /
      using-alias — is not exercised here). The langpair-level
      `<package>` ⇒ `<RootNamespace>` mapping (`glp_runtime` ⇒
      `GlpRuntime`) is a project-policy decision recorded once at
      langpair scope, reused by every file in the batch via KB hit
      (FR-012 / SC-007).
  - construct_key: dart.test_file.void_main_as_test_registration_root
    source_form: "void main() { Module parseModule(String src) {...} group(...); group(...); ... }"
    target_decision: >-
      Eliminate the Dart `void main()` function entirely. In
      `package:test` the file-level `main()` is the registration entry
      point invoked by the runner; xUnit has NO equivalent — discovery
      is attribute-driven on a public test class. The Dart shape `void
      main() { <helper-closure>; group(...); group(...); ... }`
      becomes `public class ModuleSyntaxV2Test { <private helper>; ...
      [Fact] methods ... }`: each `test('...', () { ... })` call lifts
      into one `[Fact]`-attributed public instance method whose body is
      the closure body (verbatim translation of the assertions); each
      `group('label', () { ... })` becomes either (a) a nested test
      class (modelling group nesting structurally) or (b) a `[Trait]`
      attribute on the lifted `[Fact]` methods (modelling group as
      categorisation metadata only). The conversion ELECTS option (b):
      flatten all groups, lift every `test(...)` to a top-level
      `[Fact]` method on the outer `ModuleSyntaxV2Test` class, and
      record the group label via `[Trait("Group", "Phase 1 - 2a: ...")]`
      (per xunit.net `TraitAttribute` docs). Rationale: xUnit's nested
      classes are heavyweight (each nested class gets its own
      constructor / fixture lifecycle) and obscure test runner output;
      `[Trait]` preserves the original Dart group label for filtering
      and reporting without disturbing the per-test fresh-instance
      lifecycle. Class name `ModuleSyntaxV2Test` mirrors the file name
      (`module_syntax_v2_test.dart` ⇒ snake-to-Pascal). The local
      closure `Module parseModule(String source) { ... }` lifts into a
      private instance helper method `private Module ParseModule(string
      source) { ... }` on the test class (NOT static — keeps the
      per-test-instance lifecycle uniform; the method has no shared
      state so static would also work, but private-instance is the
      idiom default).
    idiom_id: null
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Discovery model nuance (reused from
      rf-dart-test-main-to-xunit-class-with-facts established in
      smoke_test.dart): Dart `package:test` discovers tests by
      EXECUTING `main()` (which calls `test()` to register closures);
      xUnit discovers by REFLECTION over `[Fact]` / `[Theory]`
      attributes. xUnit creates a FRESH instance of the test class per
      `[Fact]` invocation (xunit.net "Shared Context between Tests"),
      so per-test setup goes in the class constructor and teardown in
      `IDisposable.Dispose` — neither needed here (no `setUp` /
      `tearDown` in the Dart source). Group nuance (NEW vs. smoke
      test — this file uses `group(...)` whereas smoke_test does
      not): Dart `group('label', body)` is a NESTING + LABELLING
      construct that prefixes child test names with the group label
      and supports per-group `setUp`/`tearDown`. xUnit has no direct
      `group` equivalent; the two idiomatic targets are nested test
      classes (one per group) or `[Trait]` attributes (flat methods,
      group as metadata). Spec default = `[Trait]` (flat) because the
      Dart source uses groups purely as LABELLING (no group-scoped
      `setUp`/`tearDown` in this file) — the lighter-weight target is
      semantically faithful. The closure-to-method lift for
      `parseModule` is a standard local-function ⇒ private-method
      transformation; Dart's `final` locals in the closure become
      C# `var` locals, function-scoped (same lexical scope semantics
      after the lift).
  - construct_key: dart.string.triple_quoted_multiline_literal
    source_form: "'''\nexported procedure factorial(Integer?, Integer).\nfactorial(0, 1).\n'''"
    target_decision: >-
      Translate Dart triple-single-quoted multi-line string literals
      (`'''...'''`) into C# 11+ raw-string literals (`"""..."""`).
      Both preserve newlines and embedded quote characters without
      escaping; both terminate on the matching triple-delimiter; both
      are the canonical multi-line-literal forms in their respective
      languages. The langpair MUST target C# 11 (.NET 7+) or newer to
      use raw strings; if the target framework version is older,
      fall back to verbatim strings (`@"..."`) with `""`-doubling for
      embedded double quotes (none in this file's literals — every
      multi-line source block in this test uses only Dart procedure
      syntax with backticks/apostrophes that are quote-free in
      double-quoted C#). Spec default = raw strings (`"""`) because
      the langpair targets modern .NET; record the verbatim-string
      fallback as the documented alternative.
    idiom_id: null
    research_finding_id: rf-dart-triple-quoted-to-csharp-raw-string
    nuance: >-
      Whitespace / interpolation nuance (explicitly addressed): Dart
      `'''...'''` is NON-interpolating only when single-quoted is
      chosen and no `${...}` syntax appears — the literals in this
      file are pure (no `$` substitution). C# raw strings `"""..."""`
      are also non-interpolating by default; the `$"""..."""` prefix
      would enable interpolation but is NOT used here. Indentation
      stripping: C# 11 raw strings strip the leading whitespace common
      to all lines based on the closing-delimiter indentation; Dart
      `'''` preserves all whitespace verbatim. The Dart literals in
      this file begin with a newline immediately after `'''` and the
      following lines have NO leading indentation (they start at
      column 0 inside the string). Translation MUST preserve this
      exactly — the C# raw string must place its closing `"""` at
      column 0 (or the content lines will be re-indented). Leading
      newline preservation: Dart `'''\nsource\n'''` preserves the
      leading `\n`; C# raw strings discard the newline immediately
      after the opening `"""` (per the C# 11 raw-string spec), which
      is a SEMANTIC DIFFERENCE — the resulting string body is one
      character shorter. For this file the leading newline is
      whitespace that the Dart lexer would consume anyway when
      tokenising the source-snippet, so the test outcome is
      unaffected; codegen MAY emit a verbatim string (`@"..."`) to
      preserve byte-for-byte equality if a future test asserts string
      length. Spec default = raw string (semantic, not byte-for-byte
      equivalent).
  - construct_key: dart.package_test.expect_value_equals_matcher
    source_form: "expect(module.procDeclarations.length, 1);"
    target_decision: >-
      Translate `expect(actual, value)` (the two-argument form where
      the second argument is a non-Matcher value — implicitly wrapped
      by `package:test` in `equals(value)`) into xUnit's
      `Assert.Equal(expected, actual)`. CRITICAL ARGUMENT-ORDER SWAP:
      Dart `expect` has `(actual, expected)`; xUnit `Assert.Equal<T>`
      has `(expected, actual)`. Every call in this file MUST be
      emitted with the arguments swapped. E.g.
      `expect(module.procDeclarations.length, 1)` ⇒
      `Assert.Equal(1, module.ProcDeclarations.Count);`. The
      `.length` (Dart `List.length` property) maps to `.Count` on
      C# `IList<T>` / `IReadOnlyCollection<T>`; the field/property
      casing (`procDeclarations` ⇒ `ProcDeclarations`) is the
      langpair's idiomatic PascalCase-public-property rule.
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal
    nuance: >-
      Argument-order footgun (explicitly addressed — load-bearing):
      this is the single highest-risk per-call transformation in the
      whole file. Dart's pub.dev `expect` reference
      (https://pub.dev/documentation/test_api/latest/expect/expect.html)
      defines `expect(actual, matcher)`; xunit.net's `Assert.Equal`
      reference defines `Assert.Equal<T>(T expected, T actual)`. A
      mechanical translation that preserves argument order produces
      passing-but-misleading failure messages ("Expected 1, got 42"
      vs "Expected 42, got 1"). Implicit-matcher-wrap nuance: Dart
      `expect(x, 1)` is sugar for `expect(x, equals(1))` per the
      `expect` API doc. Equality semantics: Dart `equals` uses `==`
      (structural for built-in collections via `package:matcher`'s
      deep equality), xUnit `Assert.Equal<T>` uses `T.Equals` /
      `IEquatable<T>` (object equality unless the type overrides
      it). For the primitive scalar comparisons in this file (`int`,
      `string`, `bool`) the two are semantically identical. List /
      collection deep-equality cases are not exercised here but
      WOULD need `Assert.Equal<IEnumerable<T>>(expected, actual)`
      (xUnit handles `IEnumerable` deep-equality by default).
      Reference vs value: all comparisons in this file are
      value-type or string scalars (no reference-equality footgun).
  - construct_key: dart.package_test.expect_isTrue_isFalse_isNull_matcher
    source_form: "expect(decl.exported, true); expect(decl.exported, false); expect(decl.modulePath, isNull);"
    target_decision: >-
      Route boolean / null matchers to the matching xUnit `Assert.*`
      method per the routing-table idiom recorded in the
      `package:test` matcher idiom (established by smoke_test.dart's
      convspec, extended here):
        - `expect(x, true)` ⇒ `Assert.True(x)`
          (Dart implicit-equals(true) is semantically equivalent to
          `isTrue` for a strictly-`bool` actual; the idiom collapses
          both forms onto `Assert.True`)
        - `expect(x, false)` ⇒ `Assert.False(x)`
        - `expect(x, isTrue)` ⇒ `Assert.True(x)` (same as above)
        - `expect(x, isNull)` ⇒ `Assert.Null(x)`
        - `expect(x, isNotNull)` ⇒ `Assert.NotNull(x)`
      Every occurrence in this file uses one of these forms.
    idiom_id: null
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Routing-table reuse (explicitly addressed): the matcher routing
      table was recorded as a load-bearing-context nuance in
      smoke_test.dart's convspec (`rf-dart-expect-isTrue-to-xunit-assert-true`)
      and is reused verbatim here without re-research (KB decision
      order #2). Boolean strictness: Dart `bool` is strict (no
      truthiness), C# `bool` is strict — `Assert.True(bool)` /
      `Assert.False(bool)` per xunit.net mirror the Dart semantics
      exactly. Null nuance: Dart `expect(x, isNull)` succeeds iff
      `x == null`; xUnit `Assert.Null(object)` succeeds iff `x is
      null` (reference null + nullable-value-type null). For the
      `decl.modulePath` (Dart `String?`) case this maps to C# `string?`
      and `Assert.Null` works identically. Implicit-equals collapse:
      Dart `expect(x, true)` (matcher-as-value) and `expect(x, isTrue)`
      (matcher object) produce the same outcome for a strict `bool`
      actual; both collapse onto `Assert.True` in the routing table.
  - construct_key: dart.package_test.expect_isA_type_matcher
    source_form: "expect(decl.argTypes[1], isA<TypeRef>());"
    target_decision: >-
      Translate Dart's `expect(actual, isA<T>())` runtime-type
      assertion into xUnit's `Assert.IsType<T>(actual)`. Both assert
      that the runtime type of `actual` is EXACTLY `T` (not a
      subtype) — semantically aligned. xUnit also provides
      `Assert.IsAssignableFrom<T>` for the subtype-accepting variant
      (matches Dart's `isA<T>()` only when extended via
      `isA<T>().having(...)` patterns); spec default for the bare
      `isA<T>()` form = `Assert.IsType<T>`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Exact-type vs assignable-from (explicitly addressed): Dart's
      `isA<T>()` matcher (from `package:matcher`,
      https://pub.dev/documentation/matcher/latest/matcher/TypeMatcher-class.html)
      asserts `actual is T`, which in Dart is the IS-A relation
      (includes subtypes). xUnit's `Assert.IsType<T>` is the
      EXACT-type assertion (rejects subtypes); `Assert.IsAssignableFrom<T>`
      is the IS-A assertion. For this file's call —
      `expect(decl.argTypes[1], isA<TypeRef>())` — `TypeRef` is a
      concrete (likely sealed or leaf) class in
      `lib/analysis/type_checker/type_ast.dart`, so exact-type and
      is-a coincide; either xUnit method is semantically correct.
      Spec default = `Assert.IsType<TypeRef>` for tightness; record
      `Assert.IsAssignableFrom<TypeRef>` as the documented fallback
      for cases where the Dart source uses `isA<AbstractBase>()` over
      a class hierarchy (not exercised in this file). Return value
      nuance: xUnit's `Assert.IsType<T>` RETURNS the cast value as
      `T` (per xunit.net API), allowing fluent follow-up assertions
      — useful when the next line is `var typeRef = decl.argTypes[1]
      as TypeRef;`, which collapses to `var typeRef =
      Assert.IsType<TypeRef>(decl.ArgTypes[1]);` in C#, removing the
      redundant cast.
  - construct_key: dart.package_test.expect_throwsA_with_isA_having
    source_form: "expect(() => parseModule(source), throwsA(isA<CompileError>().having((e) => e.message, 'message', contains('no longer supported'))));"
    target_decision: >-
      Translate Dart's `expect(() => fn(), throwsA(isA<T>().having(...,
      ..., matcher)))` exception-asserting pattern into xUnit's
      two-call composition: `var ex = Assert.Throws<T>(() => fn());
      Assert.Contains("<substring>", ex.<Property>);`. Concretely:
        - `expect(() => parseModule(src), throwsA(isA<CompileError>()
          .having((e) => e.message, 'message', contains('no longer
          supported'))))`
        - ⇒ `var ex = Assert.Throws<CompileError>(() =>
          ParseModule(src)); Assert.Contains("no longer supported",
          ex.Message);`
      The `Assert.Throws<T>(...)` call asserts that exactly `T` is
      thrown AND returns the caught exception, allowing the
      follow-up assertion on the message property.
    idiom_id: null
    research_finding_id: rf-dart-throwsA-having-to-xunit-assert-throws
    nuance: >-
      Exception-type strictness (explicitly addressed — high-risk):
      xUnit's `Assert.Throws<T>` rejects subtypes by default (per
      xunit.net `Assert.Throws<T>(Action)` reference — "Verifies that
      the exact exception is thrown"), use `Assert.ThrowsAny<T>` for
      the assignable-from variant. Dart's `throwsA(isA<T>())` uses
      the matcher's IS-A semantics (subtypes accepted). For this
      file's `CompileError` (a concrete error class in
      `lib/compiler/error.dart`) the difference is academic, but the
      idiom MUST be recorded: `throwsA(isA<T>())` ⇒ `Assert.Throws<T>`
      is correct ONLY when `T` is sealed / leaf-most expected; for
      base-class catches, codegen must emit `Assert.ThrowsAny<T>`.
      `.having((e) => prop, 'label', matcher)` decomposition:
      this is Dart's matcher-composition idiom (pub.dev/matcher
      `Matcher.having`) — extract property `prop` from `e`, label it
      `'label'` for diagnostic output, then apply the inner matcher
      to the extracted value. In xUnit there is no equivalent
      composing assertion; the idiom decomposes into the two-call
      shape (catch + inspect-property). Lambda nuance: Dart's
      `() => parseModule(src)` thunk ⇒ C# `Action` /
      `Func<TResult>` — `Assert.Throws<T>(Action)` takes `Action`
      (void-returning), `Assert.Throws<T>(Func<object?>)` takes a
      function (used when the call has a return value). Dart's
      `parseModule` returns `Module`, so the throwing call lives in
      a non-void thunk — `Assert.Throws<T>(Func<object?>)` is the
      precise overload. The `contains('no longer supported')` Dart
      matcher (substring on `String`) maps to
      `Assert.Contains(string, string)` per xunit.net. Async nuance:
      not exercised here (no `async` thunk); the async variant
      `Assert.ThrowsAsync<T>` is recorded only for completeness.
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(module.procedures, isEmpty);"
    target_decision: >-
      Translate `expect(collection, isEmpty)` into
      `Assert.Empty(collection)` per xunit.net `Assert.Empty(IEnumerable)`
      reference. Mirror form: `expect(collection, isNotEmpty)` ⇒
      `Assert.NotEmpty(collection)`. Recorded in the matcher
      routing-table idiom (extends smoke_test.dart's table).
    idiom_id: null
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Collection-type acceptance (explicitly addressed): Dart's
      `isEmpty` matcher checks `Iterable.isEmpty` (or `Map.isEmpty`,
      `String.isEmpty`) — works on any iterable-like. xUnit's
      `Assert.Empty` accepts `IEnumerable` — works on any
      C# enumerable. Both throw on non-iterable input (Dart's
      matcher fails with a descriptive error; `Assert.Empty` throws
      ArgumentNullException for null or compiles-out for
      non-IEnumerable). Reference null vs empty: Dart `isEmpty`
      throws on null actual; xUnit `Assert.Empty(null)` throws
      `Xunit.Sdk.EmptyException` (or `ArgumentNullException`
      depending on version) — semantically equivalent (null is NOT
      empty; assert separately with `Assert.NotNull` first when null
      is possible). Not exercised in this file (the asserted
      `module.procedures` is non-nullable per the AST contract).
  - construct_key: dart.null_safety.bang_operator_non_null_assertion
    source_form: "module.declaration!.name"
    target_decision: >-
      Translate Dart's null-forgiving `!` operator (post-fix bang on
      a nullable receiver, asserting non-null at the use site) into
      C#'s null-forgiving `!` operator (same syntax, same semantics).
      `module.declaration!.name` ⇒ `module.Declaration!.Name`. Both
      operators are STATIC-ANALYSIS-ONLY suppressions of the
      nullability warning and do NOT inject a runtime null check —
      they assert "I, the developer, know this is non-null here".
      If the value is null at runtime, both produce a
      `NullReferenceException` / Dart equivalent on the next member
      access.
    idiom_id: null
    research_finding_id: rf-dart-bang-to-csharp-null-forgiving
    nuance: >-
      Surface-identical, semantics-identical (explicitly addressed):
      this is one of the rare cases where Dart and C# converge on
      the same syntax for the same operation. Dart language tour
      (dart.dev/null-safety, "null assertion operator"): "appending
      `!` to any expression's name asserts that the value isn't
      null". C# language reference (Microsoft Learn,
      "Null-forgiving operator"): "The null-forgiving operator has
      no effect at run time. It only affects the compiler's static
      flow analysis." Both throw on the FOLLOWING member access if
      the value is in fact null — neither operator itself throws.
      Note: this is DIFFERENT from C#'s null-coalescing (`??`) or
      null-conditional (`?.`) operators, which DO have runtime
      effect; the `!` is purely a compiler hint. Reference-vs-value
      nuance: `Declaration` is a reference type on the C# side (per
      the langpair convention, Dart `class` ⇒ C# `class`); the bang
      operator's semantics apply to reference types and to
      `Nullable<T>` value types alike on the C# side.
  - construct_key: dart.list_indexer_with_typed_property_access
    source_form: "module.procDeclarations[0].name"
    target_decision: >-
      Translate Dart `List<T>` indexer + property access into the
      identical-syntax C# `IList<T>` (or `IReadOnlyList<T>`) indexer
      + property access: `module.procDeclarations[0].name` ⇒
      `module.ProcDeclarations[0].Name`. Indexer semantics
      (zero-based, throws on out-of-range) are identical in both
      languages. The langpair's field-naming rule applies (Dart
      camelCase ⇒ C# PascalCase for public properties).
    idiom_id: null
    research_finding_id: rf-dart-list-indexer-and-camelcase-property-mapping
    nuance: >-
      Trivial 1-to-1 mapping; recorded only because the file uses
      it many times and codegen MUST apply the camelCase ⇒
      PascalCase rule uniformly to AVOID compile errors on the
      target side. No bounds-check behavior difference: Dart throws
      `RangeError`; C# throws `ArgumentOutOfRangeException` — same
      semantic class (failure on bad index), and the test asserts
      length BEFORE indexing in every case, so the throw path is
      not exercised.
  - construct_key: dart.expression.dart_cast_as_typeref
    source_form: "decl.argTypes[1] as TypeRef"
    target_decision: >-
      Translate Dart's `expr as T` cast operator into the C#
      pattern-match equivalent. For codegen efficiency, FOLD this
      cast into the preceding `Assert.IsType<TypeRef>` return value
      (see `dart.package_test.expect_isA_type_matcher` above): the
      Dart sequence
      `expect(decl.argTypes[1], isA<TypeRef>()); final typeRef =
      decl.argTypes[1] as TypeRef;`
      collapses to `var typeRef =
      Assert.IsType<TypeRef>(decl.ArgTypes[1]);` (single call,
      asserted-and-typed). If the cast appears standalone (not after
      an `isA` assertion), translate as `(TypeRef)expr` (Dart `as` is
      a throwing cast — closer to C# explicit-cast `(T)x` than to
      `as` keyword in C#, which is non-throwing-returning-null).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-vs-csharp-as-keyword
    nuance: >-
      `as` keyword semantic FOOTGUN (explicitly addressed —
      load-bearing for any Dart→C# conversion): Dart `expr as T` is
      a CHECKED cast that THROWS `TypeError` on failure
      (dart.dev/language/operators "Type test operators"). C# `expr
      as T` is an UNCHECKED cast that RETURNS NULL on failure
      (Microsoft Learn "Cast and type tests"). Identical syntax,
      OPPOSITE failure mode. The correct Dart-`as` ⇒ C# translation
      is C#'s EXPLICIT cast `(T)expr` (throws `InvalidCastException`
      on failure) — NOT C#'s `as` keyword. Codegen MUST NEVER
      mechanically translate `x as T` ⇒ `x as T`; the semantics
      diverge. (The KB-recorded canonical mapping is: Dart `x as T`
      ⇒ C# `(T)x` for throwing-cast; Dart `x is T ? x as T : null` ⇒
      C# `x as T` for safe-cast.) For this file, every `as TypeRef`
      occurrence is preceded by an `isA<TypeRef>` assertion, so the
      fold into `Assert.IsType<TypeRef>` (which returns the typed
      value) is the cleanest target and SIDESTEPS the as-keyword
      footgun entirely.
  - construct_key: dart.local_variable.final_var_locals
    source_form: "final lexer = Lexer(source); final tokens = lexer.tokenize(); final module = parseModule(source);"
    target_decision: >-
      Translate Dart `final` local variables (immutable-binding,
      type-inferred) into C# `var` local variables. Both keywords
      drive type inference from the initializer; both produce a
      single-assignment-style local in practice for this file's
      usage (none of the locals are reassigned). For STRICT
      immutability matching Dart's `final` (compiler enforcement of
      no-reassignment), C# 7.0+ has no `let` / `final` keyword;
      the closest equivalent is `readonly` (fields only) or C# 9+
      pattern-`let` in switch arms. Spec default = `var` because the
      Dart source's `final` is effectively "type-inferred local"
      with no reassignment, and the codegen target's `var` matches
      that usage exactly. Record the no-direct-equivalent to
      Dart-`final`-as-keyword-on-locals as a documented limitation.
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability enforcement nuance (explicitly addressed): Dart's
      `final` on a local variable is a COMPILE-TIME guarantee that
      the binding is never reassigned (dart.dev/language/variables).
      C#'s `var` is type-inference only — the binding IS reassignable
      unless the developer follows the convention "don't reassign". A
      mechanical codegen MAY emit `var` and rely on the no-mutation
      convention; a stricter codegen MAY refactor the local into a
      `static readonly` field (overkill for a test) or compute it
      inline (eliminates the local entirely). For this file's
      one-shot test locals, `var` is the idiomatic and correct
      target. The Dart `final lexer = Lexer(source);` ⇒ C# `var lexer
      = new Lexer(source);` — note the C#-side `new` keyword
      (Dart's optional-`new` was made fully optional in Dart 2.0;
      C# 9 has target-typed `new()` but the langpair's default is
      explicit `new Lexer(source)` for clarity).
conversion_units:
  - "using Xunit; using GlpRuntime.Compiler; using GlpRuntime.Analysis.TypeChecker; (file-level using directives — three lines vs. five Dart imports because the four `package:glp_runtime/compiler/*.dart` imports collapse into one namespace)"
  - "namespace GlpRuntime.Test.Module { ... } (file-level namespace mirroring the langpair convention: test file's source directory ⇒ test project namespace)"
  - "public class ModuleSyntaxV2Test { ... } (single public test class, name PascalCased from the .dart file name; xUnit per-test fresh-instance lifecycle)"
  - "private Module ParseModule(string source) { ... } (private instance helper method on the test class, lifted from the Dart local closure `Module parseModule(String source)` inside `void main()`; body verbatim with Dart `final` ⇒ C# `var` and Dart constructor calls ⇒ C# `new Lexer(source)` etc.)"
  - "[Fact(DisplayName = \"<dart test name>\")] [Trait(\"Group\", \"<dart group label>\")] public void <PascalCasedTestName>() { ... } (one `[Fact]` method per Dart `test(...)` call, flattened from Dart's group nesting; `[Trait]` preserves group label as filtering metadata; DisplayName preserves the original human-readable test name)"
  - "exception-asserting tests use the pattern: var ex = Assert.Throws<CompileError>(() => ParseModule(src)); Assert.Contains(\"no longer supported\", ex.Message); (decomposes Dart's `throwsA(isA<T>().having(...))` into the two-call catch+inspect-property shape)"
  - "raw-string literals \"\"\"...\"\"\" replace Dart triple-quoted source-snippet strings (C# 11+; verbatim @\"...\" fallback documented for older targets)"
  - "matcher routing reuses the smoke_test.dart-established table: implicit-equals ⇒ Assert.Equal(expected, actual) WITH ARGUMENT-ORDER SWAP; isTrue/true ⇒ Assert.True; isFalse/false ⇒ Assert.False; isNull ⇒ Assert.Null; isNotNull ⇒ Assert.NotNull; isEmpty ⇒ Assert.Empty; isA<T>() ⇒ Assert.IsType<T> (returns the cast value, folds standalone `as T` casts into the assertion)"
  - "Dart `module.declaration!.name` ⇒ C# `module.Declaration!.Name` (null-forgiving `!` is identical surface and semantics on both sides)"
  - "Dart `decl.argTypes[1] as TypeRef` (throwing cast) ⇒ folded into `Assert.IsType<TypeRef>(decl.ArgTypes[1])` in this file (NEVER mechanically translated to C# `x as T`, which is non-throwing and would produce null on failure — opposite semantics)"
  - "NO equivalent of Dart's void main() — xUnit discovery is attribute-driven, registration-via-main is dropped entirely"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

This file extends and reuses several idioms established by
`test/smoke_test.dart`'s convspec; the reuse is itemised per construct
below. Per KB decision-order #2 (idiom-KB hit ⇒ REUSE verbatim, no
re-research, no re-derivation), cited reused findings are NOT
re-grounded against official docs here; their authoritative basis is
already on file in the smoke_test convspec.

### rf-dart-package-test-to-dotnet-xunit — `package:test` ⇒ xUnit (REUSED)

- **Reused** from smoke_test.dart's convspec (FR-012 / SC-007). The
  framework choice (xUnit) was authoritatively grounded against
  Microsoft Learn's "Unit testing C# in .NET Core using dotnet test
  and xUnit" tutorial and recorded as the batch-wide
  test-framework idiom. Sibling test files in the same batch MUST
  reuse it, not re-decide.
- **Why not an escalation**: KB hit, status active.

### rf-dart-package-import-to-csharp-using-namespace — intra-package import ⇒ `using`

- **Deep analysis**: this file imports five files from the
  `glp_runtime` package (`compiler/lexer.dart`, `compiler/parser.dart`,
  `compiler/ast.dart`, `compiler/error.dart`, and
  `analysis/type_checker/type_ast.dart`). All five name top-level
  types referenced in the test (`Lexer`, `Parser`, `Module`,
  `CompileError`, `TypeRef`). Dart imports are file-grained; C#
  `using` directives are namespace-grained.
- **Authoritative Dart**: dart.dev/language/libraries documents the
  `import 'package:<name>/<path>.dart'` form: "use a URI prefix of
  the form `package:` to specify packages provided by a package
  manager". Each `.dart` file is a library; `import` brings its
  top-level declarations into scope.
- **Authoritative .NET**: Microsoft Learn "Using directive"
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-directive)
  — `using <namespace>;` imports types from a namespace; one `using`
  per namespace, regardless of how many `.cs` files contribute to
  it. Microsoft Learn "Namespaces"
  (https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/namespaces)
  documents the directory-mirroring convention for namespace
  organisation.
- **Conclusion**: the langpair's directory-to-namespace mapping
  (`<package>/<a>/<b>/<file>.dart` ⇒ `<RootNamespace>.<A>.<B>`,
  PascalCased) is the standard .NET convention. The package-name
  ⇒ root-namespace mapping (`glp_runtime` ⇒ `GlpRuntime`) is a
  langpair-level project-policy decision recorded once, reused
  everywhere. Authoritative both sides; no escalation.

### rf-dart-test-main-to-xunit-class-with-facts — `void main() + group/test` ⇒ class + `[Fact]` (EXTENDED)

- **Reused** from smoke_test.dart's convspec for the
  `void main()`-drop and `test(...)` ⇒ `[Fact]` lift. The
  per-test-fresh-instance lifecycle and `DisplayName` attribute
  usage are unchanged.
- **Extended** for the `group(...)` construct, which smoke_test.dart
  did not exercise: spec default is to FLATTEN groups (lift every
  `test` directly to a top-level `[Fact]` on the outer class) and
  preserve the group label via `[Trait("Group", "<label>")]`
  attribute.
- **Authoritative**: xunit.net "Trait" reference
  (https://xunit.net/docs/getting-started) documents
  `TraitAttribute` for arbitrary metadata. Microsoft Learn
  "Running selective unit tests"
  (https://learn.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests)
  documents `--filter "Trait=Value"` for running tests by trait,
  preserving the group-as-filter use case of Dart's `group`.
- **Why flatten, not nest**: xUnit nested classes each carry their
  own constructor / fixture lifecycle (xunit.net "Shared Context"),
  which mechanically rewrites the test instance graph and is
  heavyweight when the Dart source uses `group` purely as
  labelling (no per-group `setUp`/`tearDown` in this file). The
  spec default chooses the lighter target; codegen MAY revisit if a
  future Dart source uses per-group setup.

### rf-dart-triple-quoted-to-csharp-raw-string — `'''...'''` ⇒ `"""..."""`

- **Deep analysis**: every group's `test(...)` body in this file
  builds a Dart source snippet via a triple-single-quoted string
  literal. Eight literals total; all non-interpolating; all begin
  with a newline and have no leading indentation on content lines.
- **Authoritative Dart**: dart.dev/language/built-in-types#strings
  documents triple-quoted strings: "Single-line strings are
  delimited by single or double quotation marks. Multiline strings
  use triple quotation marks". Both `'''...'''` and `"""..."""`
  forms preserve newlines verbatim.
- **Authoritative .NET**: Microsoft Learn "Raw string literals"
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/raw-string)
  introduced in C# 11: "A raw string literal starts with at least
  three double-quote (`"`) characters. ... The newline following
  the opening quote isn't included in the content". Indentation
  stripping is based on the closing-delimiter column.
- **Conclusion**: raw strings are the modern target; verbatim
  strings (`@"..."`) are the C# ≤ 10 fallback. The leading-newline
  consumption is the only documented semantic divergence; for this
  file's test-source-snippet use case it has no observable effect
  (Dart's lexer skips leading whitespace during tokenisation).
  Authoritative both sides; no escalation.

### rf-dart-expect-equals-to-xunit-assert-equal — `expect(actual, expected)` ⇒ `Assert.Equal(expected, actual)`

- **Deep analysis**: most assertions in this file are
  value-equality checks: `expect(decl.name, 'factorial')`,
  `expect(module.procDeclarations.length, 1)`,
  `expect(decl.argTypes.length, 2)`, and many more. The Dart sugar
  is `expect(x, y)` ⇒ `expect(x, equals(y))` per the `expect` API
  doc.
- **Authoritative Dart**: pub.dev/`package:test`
  (https://pub.dev/documentation/test_api/latest/expect/expect.html)
  — "If `matcher` is not a `Matcher`, it will implicitly be treated
  as `equals(matcher)`." Argument order is `(actual, matcher)`.
- **Authoritative .NET**: xunit.net `Assert.Equal` reference
  (https://xunit.net/docs/comparisons and the Xunit.Assert API) —
  signature `public static void Equal<T>(T expected, T actual)`.
  Argument order is `(expected, actual)`. Note the difference from
  many other test frameworks; xUnit has documented this as
  intentional.
- **Conclusion**: 1-to-1 mapping IS POSSIBLE but REQUIRES the
  argument-order swap. Codegen MUST swap every call. This is the
  highest-risk per-call transformation in the file — recorded here
  as a load-bearing routing-table row. Authoritative both sides;
  no escalation.

### rf-dart-expect-isTrue-to-xunit-assert-true (and isFalse / isNull / isNotNull / isEmpty) — matcher routing-table (REUSED + EXTENDED)

- **Reused** from smoke_test.dart's convspec for the `isTrue` ⇒
  `Assert.True` row.
- **Extended** for the additional matchers this file exercises:
  `isFalse` ⇒ `Assert.False`, `isNull` ⇒ `Assert.Null`,
  `isNotNull` ⇒ `Assert.NotNull`, `isEmpty` ⇒ `Assert.Empty`. Each
  was already documented as part of the broader routing table in
  smoke_test.dart's nuance section; this convspec promotes them to
  used / first-use here.
- **Authoritative .NET**: xunit.net `Assert.True(bool)`,
  `Assert.False(bool)`, `Assert.Null(object)`,
  `Assert.NotNull(object)`, `Assert.Empty(IEnumerable)` — all
  documented in the Xunit.Assert API reference. Strict (non-truthy)
  semantics on both sides.
- **Why not an escalation**: KB hit (smoke_test.dart's idiom is
  active and reused).

### rf-dart-expect-isA-to-xunit-assert-istype — `isA<T>()` ⇒ `Assert.IsType<T>` (with cast-fold)

- **Deep analysis**: this file uses `expect(decl.argTypes[1],
  isA<TypeRef>())` followed immediately by `final typeRef =
  decl.argTypes[1] as TypeRef;` — the canonical "assert type, then
  cast for further assertions" Dart idiom.
- **Authoritative Dart**: pub.dev/`package:matcher`
  (https://pub.dev/documentation/matcher/latest/matcher/TypeMatcher-class.html)
  — `TypeMatcher<T>` (returned by `isA<T>()`) asserts `actual is
  T`, which includes subtypes.
- **Authoritative .NET**: xunit.net Xunit.Assert API reference —
  `Assert.IsType<T>(object)` "Verifies that an object is of the
  given type" (EXACT type) and RETURNS the cast value as `T`;
  `Assert.IsAssignableFrom<T>(object)` is the subtype-accepting
  variant.
- **Conclusion**: `isA<T>()` ⇒ `Assert.IsType<T>` for leaf-most
  concrete types (this file's case); `Assert.IsAssignableFrom<T>`
  is the recorded fallback for hierarchy-base-class assertions
  (not exercised here). The cast-fold (collapsing `expect(x,
  isA<T>()); var y = x as T;` into `var y = Assert.IsType<T>(x);`)
  eliminates redundant work and sidesteps the
  `as`-keyword-semantic-divergence footgun on the C# side (see
  `rf-dart-as-cast-vs-csharp-as-keyword`). Authoritative both
  sides; no escalation.

### rf-dart-throwsA-having-to-xunit-assert-throws — `throwsA(isA<T>().having(...))` ⇒ `Assert.Throws<T> + property assert`

- **Deep analysis**: the file's two negative-syntax tests
  (`-export([...])` and `-import([...])` rejection) use the
  composed-matcher pattern
  `expect(() => parseModule(src), throwsA(isA<CompileError>()
  .having((e) => e.message, 'message', contains('no longer
  supported'))))` — assert that calling `parseModule(src)` throws
  exactly a `CompileError` whose `.message` property contains the
  substring `'no longer supported'`.
- **Authoritative Dart**: pub.dev/`package:test`
  (https://pub.dev/documentation/test/latest/test/throwsA.html) —
  `throwsA(matcher)` succeeds when the thunk throws an exception
  matched by the matcher. `isA<T>()` is the TypeMatcher;
  `Matcher.having((extractor), 'label', innerMatcher)`
  (pub.dev/matcher) decomposes a value via an extractor function
  and applies an inner matcher to the extracted value.
- **Authoritative .NET**: xunit.net Xunit.Assert API reference —
  `Assert.Throws<T>(Action)` and `Assert.Throws<T>(Func<object?>)`
  — "Verifies that the exact exception is thrown by the given
  delegate". Returns the caught exception, enabling follow-up
  property assertions. `Assert.Contains(string, string)` —
  "Verifies that a string contains a given substring". xUnit has no
  composing-matcher equivalent of `.having`; the idiom decomposes
  into a sequential two-call shape.
- **Conclusion**: `Assert.Throws<T>` + `Assert.Contains(substring,
  ex.Property)` is the canonical decomposition. Exact-type
  semantics (`Assert.Throws<T>` rejects subtypes) match Dart's
  `isA<T>()` IS-A semantics when `T` is leaf-most (this file's
  `CompileError` is a concrete class — leaf-most acceptable). The
  IS-A-base-class case (would require `Assert.ThrowsAny<T>`) is
  not exercised. The non-void-return-thunk overload
  `Assert.Throws<T>(Func<object?>)` is the precise match for
  `parseModule` which returns `Module`. Authoritative both sides;
  no escalation.

### rf-dart-bang-to-csharp-null-forgiving — `module.declaration!.name` ⇒ `module.Declaration!.Name`

- **Deep analysis**: one usage in the file, in the `-module(name)
  still parses` test: `expect(module.declaration!.name, 'math');`.
  `module.declaration` is a nullable field (Dart `Module?
  declaration`) per the AST contract; the test has already asserted
  `isNotNull` on the preceding line.
- **Authoritative Dart**: dart.dev/null-safety/understanding-null-safety
  ("null assertion operator") — "Appending `!` to any expression's
  name asserts that the value isn't null. If it is, a runtime
  exception is thrown."
- **Authoritative .NET**: Microsoft Learn "Null-forgiving operator"
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/null-forgiving)
  — "Available in C# 8.0 and later, the null-forgiving operator
  (`!`) suppresses all nullable warnings ... The null-forgiving
  operator has no effect at run time. It only affects the
  compiler's static flow analysis."
- **Conclusion**: identical syntax, semantically very similar
  (static-analysis suppression on both sides). The runtime
  exception on subsequent member access is implicit on both sides
  (Dart `RangeError`/`TypeError`; C# `NullReferenceException`) —
  authoritatively documented divergence in exception type, but the
  failure-PATH is identical. Authoritative both sides; no
  escalation.

### rf-dart-as-cast-vs-csharp-as-keyword — Dart throwing cast vs. C# null-returning `as`

- **Deep analysis**: the file has one `as` cast: `decl.argTypes[1]
  as TypeRef`. This is folded into the `Assert.IsType<TypeRef>`
  call (which returns the typed value), eliminating the standalone
  cast. But the idiom is recorded for general use (sibling test
  files will exercise standalone casts).
- **Authoritative Dart**: dart.dev/language/operators "Type test
  operators" — Dart `expr as T` is a CHECKED cast that THROWS
  `TypeError` (not `CastError`; renamed in Dart 2) on failure.
- **Authoritative .NET**: Microsoft Learn "Cast and type tests"
  (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/type-testing-and-cast)
  — C# `expr as T` is an UNCHECKED cast that RETURNS NULL on
  failure. C# `(T)expr` is the CHECKED cast that THROWS
  `InvalidCastException` on failure.
- **Conclusion**: Dart `as` ⇒ C# `(T)x` (explicit-cast, throwing),
  NOT C# `as`. Identical syntax with OPPOSITE failure mode — a
  high-priority footgun for the langpair. Recorded as a global
  idiom that codegen MUST apply. Authoritative both sides; no
  escalation.

### rf-dart-list-indexer-and-camelcase-property-mapping — `list[i].prop` ⇒ `list[i].Prop` + PascalCase

- **Deep analysis**: this file uses `module.procDeclarations[i].name`,
  `module.procDeclarations[i].exported`, `module.typeDefs.length`,
  `decl.argTypes[i]`, etc. — dozens of indexer + property-access
  chains. The structural pattern is identical on both sides, but the
  langpair's public-property casing rule must apply uniformly.
- **Authoritative Dart**: dart.dev/language/collections — Dart
  `List<T>` exposes the indexer `operator [](int index)` (zero-based,
  throws `RangeError` on out-of-range) and properties via lowerCamelCase
  identifiers (effective-dart/style "DO name types using `UpperCamelCase`
  ... DO name extensions using `UpperCamelCase` ... DO name libraries,
  packages, directories, and source files using `lowercase_with_underscores`
  ... DO name other identifiers using `lowerCamelCase`").
- **Authoritative .NET**: Microsoft Learn "Capitalization Conventions"
  (https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions)
  — "DO use PascalCasing for all public member, type, and namespace
  names consisting of multiple words". `IList<T>` / `IReadOnlyList<T>`
  indexer (`this[int index]`) is the same zero-based,
  `ArgumentOutOfRangeException`-on-overflow semantic as Dart's
  `List<T>` indexer (Microsoft Learn "IList<T>.Item[Int32]").
- **Conclusion**: 1-to-1 structural translation with the langpair's
  uniform camelCase ⇒ PascalCase rule applied to every property
  access. Authoritative both sides; no escalation.

### rf-dart-final-local-to-csharp-var-local — `final x = ...;` ⇒ `var x = ...;`

- **Deep analysis**: every Dart helper closure and many test bodies
  use `final` locals (`final lexer = Lexer(source); final tokens =
  lexer.tokenize(); final module = parseModule(source); final decl =
  module.procDeclarations[0];`). None are reassigned. The Dart `final`
  keyword on a local provides single-assignment compile-time enforcement.
- **Authoritative Dart**: dart.dev/language/variables — "If you never
  intend to change a variable, use `final` or `const`, either instead
  of `var` or in addition to a type. A `final` variable can be set
  only once". `final` is compile-time enforced.
- **Authoritative .NET**: Microsoft Learn "Implicitly typed local
  variables" (https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/declarations#implicitly-typed-local-variables)
  — `var` provides type inference from the initializer. C# has NO
  language-level `final` / `let` keyword on a local that enforces
  single-assignment (`readonly` is fields-only;
  `init`-only properties are not local scope). Compile-time enforcement
  of "this local is never reassigned" is not a C# feature.
- **Conclusion**: Dart `final` ⇒ C# `var` for the type-inference
  purpose; the single-assignment compile-time guarantee is LOST in
  the translation (documented limitation). For this file's use case
  (one-shot test locals never reassigned) the convention-based
  practice is sufficient; codegen MAY add a code-style analyzer
  rule ("locals should not be reassigned") if strictness is desired.
  Authoritative both sides; no escalation.

## Notes

- This file is a parser unit-test that builds Dart-source-snippet
  strings, parses them with the GLP `Lexer`+`Parser`, and asserts
  AST shape. No async / `Future` / `Stream` / `Completer` /
  `Timer` / isolate surface — the entire suite is synchronous; all
  `[Fact]` methods return `void` (not `async Task`).
- The Dart-source-snippet strings themselves are NOT translated to
  C# — they are GLP source code, opaque to the conversion. They
  remain verbatim string content in the C# raw-string literals
  (the test still parses GLP source, the GLP runtime is on the
  C# side, the GLP source itself does not change).
- The langpair-level concerns (test `.csproj`, NuGet
  references for xUnit + Microsoft.NET.Test.Sdk, namespace-mapping
  convention `glp_runtime` ⇒ `GlpRuntime`, directory ⇒ namespace
  PascalCasing rule) are OUT OF SCOPE for this per-file artifact;
  they are recorded once at langpair level and reused by every
  file in the batch.
- No `late`, `mixin`, `extension`, generic type parameters
  declared at the file level, `Future`/`Stream`/`Completer`, or
  isolate APIs appear in this file. The well-known async / stream
  nuances are deliberately NOT asserted here (do not apply to this
  source surface; would mislead reviewers if forced into the spec).
- Idiom reuse summary: this file reuses six idioms established by
  smoke_test.dart's convspec
  (`rf-dart-package-test-to-dotnet-xunit`,
  `rf-dart-test-main-to-xunit-class-with-facts`,
  `rf-dart-expect-isTrue-to-xunit-assert-true` — extended for
  isFalse/isNull/isNotNull/isEmpty rows of the matcher
  routing-table) and introduces six new constructs
  (`rf-dart-package-import-to-csharp-using-namespace`,
  `rf-dart-triple-quoted-to-csharp-raw-string`,
  `rf-dart-expect-equals-to-xunit-assert-equal`,
  `rf-dart-expect-isA-to-xunit-assert-istype`,
  `rf-dart-throwsA-having-to-xunit-assert-throws`,
  `rf-dart-bang-to-csharp-null-forgiving`,
  `rf-dart-as-cast-vs-csharp-as-keyword`). Per FR-012 / SC-007 the
  reused idioms are NOT re-derived here; their authoritative basis
  lives in smoke_test.dart's convspec.
- Zero escalations: every construct is authoritative-supported on
  both sides (Dart and .NET official docs); the only project-policy
  decisions (xUnit, namespace mapping, group-flatten-vs-nest) have
  pre-resolved batch-wide defaults recorded as idioms.
