> Conversion-spec artifact for test/module/module_parser_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/module/module_parser_test.dart
source_sha256: 474ecc4372558bbbf10fc78a96bb1fb3f4eaf64c5b23d034bf0d40b3096689dc
target_code_unit: test/module/ModuleParserTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope (xUnit pinned project-wide as the
      modern .NET default — same idiom as the precedent files
      test/multiagent/mad_error_handling_test.dart.md and
      test/multiagent/boot_loader_test.dart.md, recorded under
      `rf-dart-package-test-import-to-xunit-using`). Codegen MUST also add
      `using System;` (used for `Assert.Throws<Exception>` in the
      `parser rejects module with arguments` test — see
      `dart.package_test.expect_throws_anything` below) and `using
      System.Collections.Generic;` (used for `HashSet<string>` element-wise
      equality in the `exportedSignatures` set-equality assertion — see
      `dart.collections.set_literal_string_equality`). Project to a single
      namespace mirroring the Dart `test/module` directory (e.g.
      `<RootNs>.Test.Module`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom — reused verbatim, no re-research. xUnit selection is
      project-wide policy (NOT a file-local choice); reversing it would
      invalidate every prior test-file convspec. NUnit and MSTest remain
      corroborating alternatives recorded once at the import-idiom level.
  - construct_key: dart.package_under_test.import_directive
    source_form: |-
      "import 'package:glp_runtime/compiler/lexer.dart';
       import 'package:glp_runtime/compiler/parser.dart';
       import 'package:glp_runtime/compiler/ast.dart';
       import 'package:glp_runtime/compiler/token.dart';"
    target_decision: >-
      Each of the four `package:glp_runtime/compiler/<...>.dart` imports maps
      to a `using` directive that names the C# namespace produced by
      converting the corresponding SUT file. Concretely the four imports
      collapse to ONE `using` line if all four target files share the same
      C# namespace (the conventional outcome — `<RootNs>.Compiler` —
      because the converted `lib/compiler/lexer.dart`,
      `lib/compiler/parser.dart`, `lib/compiler/ast.dart`,
      `lib/compiler/token.dart` all live in the same C# project folder
      `Compiler/`). The exact SUT namespace string is decided when those
      four files are converted (see their respective convspecs
      .codeconv/conversion-specs/lib/compiler/lexer.dart.md,
      parser.dart.md, ast.dart.md, token.dart.md); this spec records only
      the SHAPE of the cross-file dependency.
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedent: boot_loader_test.dart.md). Multiple Dart
      package-imports collapsing to a single C# `using` when the four SUT
      files share a namespace is a documented C# convention (one `using`
      per namespace, not per file). No `as`-alias / show / hide directives
      appear in this file, so simple `using <Ns>;` suffices. The
      `TokenType.HASH` / `TokenType.MINUS` / `TokenType.LPAREN` /
      `TokenType.ATOM` / `TokenType.PROCEDURE` / `TokenType.DOT` /
      `TokenType.RPAREN` enum-member accesses (Dart enum-dot-access) work
      identically in C# (`TokenType.HASH`, etc.) — recorded under
      `dart.enum.value_access` below.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Module Parser - Lexer', () { ... }); group(...); group(...); group(...); group(...); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint; xUnit
      discovers `[Fact]` methods by reflection — there is NO per-file
      entrypoint to emit. Eliminate `main` entirely; its body (five
      top-level `group(...)` calls) becomes five enclosing test classes
      (see `dart.package_test.group_block` below).
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Cached idiom (precedent: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md). Lifecycle nuance: Dart `main` is invoked
      once per test-file process; xUnit has no per-file hook. THIS file's
      `main` body is exactly five sibling `group()` calls with no
      file-level setUp / no shared state, so the omission is lossless —
      no migration into `IClassFixture<>` needed.
  - construct_key: dart.package_test.group_block
    source_form: |-
      "group('Module Parser - Lexer', () { test(...); ... });
       group('Module Parser - Module Declaration', () { test(...); ... });
       group('Module Parser - Remote Goal', () { test(...); ... });
       group('Module Parser - Complete Module', () { test(...); ... });
       group('Module Parser - Procedure Declarations', () { test(...); ... });
       group('Module Parser - Remote Goal in Module', () { test(...); ... });"
    target_decision: >-
      Six sibling top-level `group(...)` calls (NOT nested). Map each to its
      own PascalCase xUnit test class within the same `.cs` file. The
      labels become class names with non-identifier characters stripped
      and dashes converted to camel-joins: `ModuleParserLexerTests`,
      `ModuleParserModuleDeclarationTests`, `ModuleParserRemoteGoalTests`,
      `ModuleParserCompleteModuleTests`,
      `ModuleParserProcedureDeclarationsTests`,
      `ModuleParserRemoteGoalInModuleTests`. The original label MUST be
      preserved verbatim via `[Fact(DisplayName = "<original label>")]`
      on every test method so reporter output keeps the Dart sentence
      form. SIBLING (not nested) groups in this file do NOT share state
      (no `late` field, no `setUp`), so each group becomes a FULLY
      INDEPENDENT class — no shared base class, no `IClassFixture<>`. The
      multi-class-per-file shape is documented xUnit usage
      (`https://xunit.net/docs/getting-started/v3/getting-started`); the
      one-class-per-`group` decision avoids name collisions across groups
      and matches the precedent boot_loader_test.dart.md's per-group
      flatten rule, applied at the OUTER group level here (because there
      is no outer-group shared `late` field forcing flatten-everything).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: mad_error_handling_test.dart.md,
      boot_loader_test.dart.md). Topology nuance EXPLICITLY addressed:
      this file's groups are SIBLINGS (six top-level `group(...)` calls in
      `main`), not NESTED (unlike boot_loader_test.dart.md's outer +
      three-inner topology). Because no outer group exists, there is no
      shared `late` field to force a single-class FLATTEN; each sibling
      group becomes its own class. Name-mangling nuance: every group
      label here contains the substring `"Module Parser - "` plus a
      hyphen-separated suffix; the hyphens MUST be stripped (or
      camel-joined) because `-` is not a C# identifier character.
      Reporter-trait alternative (a single class with `[Trait("Group",
      "<label>")]` per method) is recorded as an option in the research
      finding but rejected here because six independent classes produce
      cleaner Visual Studio Test Explorer grouping for the
      no-shared-state case.
  - construct_key: dart.package_test.test_call_simple
    source_form: "test('<label>', () { /* arrange, act, assert */ });"
    target_decision: >-
      Each Dart `test(label, body)` with a synchronous closure body and no
      `skip:` argument becomes a `public void` instance method on the
      enclosing xUnit class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The method name is the
      label PascalCased with non-identifier characters stripped (e.g.
      `'lexer recognizes HASH token'` → `LexerRecognizesHashToken`;
      `'parser parses static remote goal'` → `ParserParsesStaticRemoteGoal`).
      All sixteen `test(...)` calls in this file are synchronous (no
      `async`/`Future`), so NO target method is `async Task`. The
      arrange/act/assert closure body translates statement-for-statement
      into the C# method body (final-local declarations → `var`
      declarations — see `dart.local.final_var_declaration` below;
      `expect(...)` calls → `Assert.*` calls — see the matcher-routing
      constructs below).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Cached idiom. Async nuance (explicitly addressed even though absent
      in this file): a Dart `test('...', () async { ... })` would target
      `public async Task <Name>()` (xUnit awaits the returned Task). None
      of THIS file's callbacks are async, so no target method is async.
      Closure-capture nuance: callbacks here capture NOTHING from `main()`
      scope (no `setUp` variables, no `late` field) — each test
      constructs its own `Lexer`, `Parser`, etc. inside its own body, so
      the xUnit translation needs no instance fields and no constructor.
  - construct_key: dart.local.final_var_declaration
    source_form: "final lexer = Lexer(source); final tokens = lexer.tokenize(); final parser = Parser(tokens); final module = parser.parseModule(); final program = parser.parse(); final clause = ...; final goal = ...; final remote = goal as RemoteGoal;"
    target_decision: >-
      Every `final <name> = <expr>;` local in this file maps to
      `var <name> = <expr>;` in C#. `final` in Dart on a LOCAL is the
      "single-assignment, type-inferred" idiom; C# `var` is identical
      except `var` is NOT single-assignment (it's just type-inferred and
      mutable). For test method locals — which are never reassigned in
      any of this file's bodies — the looser `var` is observably
      equivalent. Stricter equivalents (`readonly` for fields, `const`
      for compile-time constants, `in` for parameter-direction) DO NOT
      apply to method-body locals. Constructor calls without `new` in
      Dart 2.x (`Lexer(source)`) map to `new Lexer(source)` in C#
      (C# requires `new` for instance construction — Dart 2.x dropped
      the requirement; this is a well-known Dart→C# nuance).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Cached idiom (precedents: boot_loader_test.dart.md,
      global_send_test.dart.md, binding_pointer_test.dart.md,
      varref_pointer_test.dart.md). Single-assignment nuance (explicitly
      addressed): Dart `final` enforces no-reassignment at the language
      level; C# `var` does NOT. For these test bodies the distinction is
      invisible (no local is ever reassigned in this file). If a future
      test reassigned a `final`-target local, codegen would need to flag
      it — but since `final` PROHIBITS reassignment in Dart, that case
      cannot arise from a valid Dart source. The `new`-keyword nuance is
      well-known (Dart 2 made `new` optional / discouraged; C# requires
      it).
  - construct_key: dart.package_test.expect_equals_implicit
    source_form: |-
      "expect(tokens.length, 4);
       expect(tokens[0].type, TokenType.ATOM);
       expect(tokens[0].lexeme, 'a');
       expect(module.declaration!.name, 'math');
       expect(module.procedures.length, 1);
       expect(module.procedures[0].name, 'factorial');
       ...
       expect(module.name, 'main');
       expect((remote.module as VarTerm).name, 'M');"
    target_decision: >-
      Dart `expect(<actual>, <literal-value>)` (implicit-equals, no
      `equals(...)` wrapper) is the matcher-library implicit-equality
      shorthand and maps to xUnit `Assert.Equal(<expected>, <actual>)`
      with the ARGUMENT-ORDER FLIP (Dart puts actual first; xUnit puts
      expected first). All ~30 `expect(actual, literal)` calls in this
      file use this shorthand on `int` (list `.length`, `.arity`), `bool`
      (`isDynamic`, `isReader`, `false`), and `String` (`.lexeme`,
      `.name`, `.functor`); all three have value-equality semantics
      identical between Dart `==` and C# `Equals`. Enum-value comparisons
      (`tokens[0].type, TokenType.ATOM`) likewise use value-equality
      identically.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Cached idiom (precedent: mad_error_handling_test.dart.md prose,
      moded_head_test.dart.md, boot_loader_test.dart.md). Argument-order
      flip is the well-known footgun. Implicit-equals nuance (explicitly
      addressed): `package:test` treats a literal second argument to
      `expect` as an implicit `equals(literal)` matcher
      (`https://pub.dev/documentation/matcher/latest/matcher/equals.html`);
      the C# target collapses to the SAME `Assert.Equal` as the explicit
      `expect(x, equals(y))` form, so the implicit/explicit Dart
      distinction is NOT preserved in the target (lossless because both
      use value-equality). Enum-equality nuance: `TokenType.ATOM` and
      friends are Dart enum values; in C# `enum` values are value-typed
      and use `==` / `Equals` identically — `Assert.Equal` works directly.
  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: "expect(module.declaration, isNotNull); expect(clause.body, isNotNull);"
    target_decision: >-
      Dart `expect(x, isNotNull)` maps to xUnit `Assert.NotNull(x);` per
      the cached idiom. Used 2× in this file (line 59 — `module.declaration`
      in `parser parses module declaration`; line 87 —
      `clause.body` in `parser parses static remote goal`). `Assert.NotNull`
      semantics are identical to Dart `isNotNull`: pass iff non-null
      reference. After `Assert.NotNull(x)` the C# null-flow analyser
      narrows `x`'s type to non-nullable, so subsequent `x!.<member>`
      accesses become flow-narrowed `x.<member>` accesses — see
      `dart.nullable_bang.property_access` below.
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Cached idiom (precedent: global_send_test.dart.md,
      localize_test.dart.md, smoke_test.dart.md). Null-safety nuance: in
      both ecosystems, `isNotNull` / `Assert.NotNull` accept ANY
      non-null value (including falsy primitives like `0` / `""` /
      `false`). xUnit's `Assert.NotNull` additionally produces a flow-
      analysis narrowing on the asserted variable for subsequent
      statements in the same method (documented at
      `https://learn.microsoft.com/dotnet/api/xunit.assert.notnull`).
  - construct_key: dart.package_test.expect_isNull_matcher
    source_form: |-
      "expect(remote.staticModuleName, isNull);
       expect(module.declaration, isNull);
       expect(module.name, isNull);
       expect((remote.module as VarTerm).isReader, ...);  // isNull case absent; the isNull uses are the first three"
    target_decision: >-
      Dart `expect(x, isNull)` maps to xUnit `Assert.Null(x);` per the
      cached idiom. Three uses in this file (line 115 — `remote.staticModuleName`;
      lines 210/211 — `module.declaration` / `module.name`). `Assert.Null`
      semantics: pass iff `x is null` (reference equality with `null`),
      identical to Dart `isNull`.
    idiom_id: rf-dart-expect-isNull-to-xunit-assert-null
    research_finding_id: rf-dart-expect-isNull-to-xunit-assert-null
    nuance: >-
      Cached idiom (precedent: global_send_test.dart.md,
      binding_pointer_test.dart.md, global_writers_table_test.dart.md).
      Argument-order: `Assert.Null` is the unary form (one arg, no flip
      needed) — distinct from the binary `Assert.Equal(null, x)` which
      would also pass but reads less idiomatically. xUnit
      (`https://xunit.net/docs/comparisons#assertions`) recommends
      `Assert.Null` for null-checks specifically.
  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(module.procDeclarations, isEmpty);"
    target_decision: >-
      Dart `expect(x, isEmpty)` maps to xUnit `Assert.Empty(x);` per the
      cached idiom. One use in this file (line 212 — `module.procDeclarations`
      in `parser handles module without declarations`). `Assert.Empty`
      accepts any `IEnumerable<T>` and short-circuits via the enumerator
      (no full materialisation) — observably equivalent to Dart
      `isEmpty` which short-circuits via the `isEmpty` getter (O(1) for
      List).
    idiom_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Cached idiom (precedent: localize_test.dart.md). Collection-shape
      nuance: Dart `isEmpty` works on any `Iterable`/`String`/`Map` with
      an `isEmpty` getter; xUnit `Assert.Empty` requires `IEnumerable`
      (covers `List<T>`, `string`, `IDictionary<K,V>` — all the same
      shapes). `procDeclarations` is a `List<ProcedureDeclaration>` per
      ast.dart, which trivially satisfies `IEnumerable`.
  - construct_key: dart.package_test.expect_isA_matcher
    source_form: |-
      "expect(goal, isA<RemoteGoal>());
       expect(clause.body![0], isA<RemoteGoal>());
       expect(clause.body![1], isA<RemoteGoal>());
       expect(remote.module, isA<VarTerm>());
       expect(bootClause.body![0], isA<RemoteGoal>());
       expect(bootClause.body![1], isA<RemoteGoal>());"
    target_decision: >-
      Dart `expect(x, isA<T>())` (bare `isA<T>()`, NOT wrapped in
      `throwsA`) maps to xUnit `Assert.IsType<T>(x);` per the cached idiom
      `rf-dart-expect-isA-to-xunit-assert-istype` (FIRST-SEEN in
      varref_pointer_test.dart.md). Six uses in this file (lines 92, 112,
      132, 152, 153, 300, 301). `Assert.IsType<T>` asserts EXACT type
      match (no subtype tolerance) — see the nuance row below for the
      subtype-tolerant alternative.
    idiom_id: rf-dart-expect-isA-to-xunit-assert-istype
    research_finding_id: rf-dart-expect-isA-to-xunit-assert-istype
    nuance: >-
      Cached idiom (precedent: varref_pointer_test.dart.md prose,
      binding_pointer_test.dart.md). Exact-vs-subtype nuance (explicitly
      addressed — well-known footgun): Dart `isA<T>` accepts subtypes
      (`x is T` semantics); xUnit `Assert.IsType<T>` requires EXACT type
      match (fails on subtype). The strict-faithful translation of
      `isA<T>` is `Assert.IsAssignableFrom<T>(x)`. However, in THIS file
      all six uses target `RemoteGoal` and `VarTerm`, which per ast.dart
      are LEAF concrete classes (not extended by any other class in the
      compiler hierarchy — `RemoteGoal extends Goal` is the entirety of
      its hierarchy; `VarTerm extends Term`); EXACT type and assignable
      type coincide. Codegen MUST emit `Assert.IsType<T>` (strictest
      assertion that is observably equivalent), and the research finding
      records the `Assert.IsAssignableFrom<T>` fallback for non-leaf
      target types in other files. Distinct from the `throwsA(isA<T>())`
      composite which maps under
      `rf-dart-throwsa-isa-to-xunit-throws-simple`.
  - construct_key: dart.package_test.expect_throws_anything
    source_form: "expect(() => parser.parse(), throwsA(anything));"
    target_decision: >-
      Dart `expect(<closure>, throwsA(anything))` asserts that the closure
      throws ANY error/exception type (no type constraint, no message
      check). The faithful xUnit mapping is
      `Assert.Throws<Exception>(() => parser.Parse());` — `Exception` is
      the root of the user-throwable hierarchy in .NET (under
      `System.Exception`); `Assert.Throws<Exception>` passes for any
      thrown `Exception` or subtype, matching Dart `throwsA(anything)`'s
      "any thrown thing" semantics. The strict-faithful alternative is
      `Assert.ThrowsAny<Exception>(...)`, which additionally tolerates
      subtypes (`Assert.Throws<T>` requires EXACT type but `Exception` as
      the root makes EXACT-match and ANY-match equivalent in practice).
      Used once in this file (line 164, `parser rejects module with
      arguments`).
    idiom_id: null
    research_finding_id: rf-dart-throwsa-anything-to-xunit-assert-throws-exception
    nuance: >-
      FIRST-SEEN idiom (defines a new active row in the KB). The Dart
      `anything` matcher (from package:matcher) is "always-passes" — used
      inside `throwsA` it means "the thrown thing's identity / type /
      message is irrelevant; only THAT it throws". xUnit has no
      `Assert.ThrowsAnything` primitive; the canonical translation is
      `Assert.Throws<Exception>` (root type) because all .NET user-throws
      derive from `System.Exception`. Distinguished from
      `rf-dart-throwsa-isa-to-xunit-throws-simple` (which constrains the
      thrown TYPE via `isA<T>`) and
      `rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert` (which
      ALSO constrains the thrown message). Authoritative bases: Dart
      matcher `anything` constant
      (`https://pub.dev/documentation/matcher/latest/matcher/anything-constant.html`),
      xUnit `Assert.Throws<T>` documentation
      (`https://xunit.net/docs/comparisons#exceptions`),
      `System.Exception` as the base of throwable user types
      (`https://learn.microsoft.com/dotnet/api/system.exception`).
  - construct_key: dart.collections.set_literal_string_equality
    source_form: |-
      "expect(module.exportedSignatures, {'factorial/2', 'gcd/3'});
       expect(module.exportedSignatures, {'boot/1'});"
    target_decision: >-
      Dart untyped string set literals `{'factorial/2', 'gcd/3'}` and
      `{'boot/1'}` (each is a `Set<String>` literal — Dart infers `Set`
      from the curly-brace literal because no key-value pairs are
      present) compared against `module.exportedSignatures` (a
      `Set<String>` per ast.dart `Module.exportedSignatures`) map to xUnit
      `Assert.Equal(new HashSet<string> { "factorial/2", "gcd/3" },
      module.ExportedSignatures);` and the analogous single-element form
      for `{'boot/1'}`. xUnit `Assert.Equal(IEnumerable, IEnumerable)`
      compares element-wise using the default equality comparer; for
      `HashSet<string>` this is set-equality (membership-based, NOT
      order-sensitive — `HashSet<T>` overrides `Equals` to use
      `SetEquals` semantics per `IEquatable<ISet<T>>`).
    idiom_id: rf-dart-set-literal-typed-to-csharp-hashset-initializer
    research_finding_id: rf-dart-set-literal-typed-to-csharp-hashset-initializer
    nuance: >-
      Cached idiom (precedent: varref_pointer_test.dart.md). Set-equality
      nuance (explicitly addressed): Dart `Set<T>` literal `{a, b}` is a
      `LinkedHashSet` (insertion-ordered, hash-based dedup via
      `==`/`hashCode`); C# `HashSet<T>` is UNORDERED. The
      `expect(actualSet, literalSet)` assertion (implicit-equals) tests
      MEMBERSHIP equality, NOT ordering — both Dart and C# pass under the
      MEMBERSHIP interpretation. Codegen MUST emit
      `Assert.Equal<HashSet<string>>(expected, actual)` or use the
      `SetEquals`-based form
      `Assert.True(expected.SetEquals(actual))` for crystal-clear
      semantics. Either is observably equivalent here because both
      assertion values contain unique strings only (no duplicates that
      could disambiguate multiset-vs-set). xUnit
      `Assert.Equal(IEnumerable<T>, IEnumerable<T>)` for
      `HashSet<string>` operands uses the `IEquatable<T>`-compliant
      element comparer plus the collection's own equality — documented
      at `https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1.setequals`.
      Untyped vs typed set-literal nuance: Dart `{...}` (no `<T>` prefix)
      infers element type from the elements (`String` here); C#
      `new HashSet<string> { ... }` requires the explicit `<string>` —
      this is the standard Dart-implicit / C#-explicit gap, recorded in
      the cached idiom.
  - construct_key: dart.nullable_bang.property_access
    source_form: |-
      "module.declaration!.name;
       clause.body!.length;
       clause.body![0];
       clause.body![1];
       bootClause.body!.length;
       bootClause.body![0];
       bootClause.body![1];"
    target_decision: >-
      Dart `<expr>!.<member>` and `<expr>![<index>]` (null-assertion
      followed by member-access or indexer) on a Dart-nullable target
      map to a plain C# `<expr>.<member>` / `<expr>[<index>]` PROVIDED
      THAT the previous statement either (a) called `Assert.NotNull(x)`
      on `x` (xUnit flow-narrows the local to non-nullable for the rest
      of the method body), or (b) the C# property's declared type is
      already non-nullable (the converted `Clause.Body` may end up
      `IReadOnlyList<Goal>` non-nullable per parser.dart.md — TBD when
      that spec converts). When neither (a) nor (b) applies, codegen
      MUST emit the C# null-forgiving operator `<expr>!.<member>` /
      `<expr>![<index>]` (same `!` syntax — Dart and C# coincide here).
      In THIS file, every `body!` is preceded by `expect(clause.body,
      isNotNull)` (line 87) OR is in a test where `parseModule` /
      `parse` is documented to return a non-null `body` per
      ast.dart.md's `Clause.body` typing — codegen consults
      ast.dart.md's final decision; if `Clause.body` is non-nullable in
      C#, the `!` simply drops; if nullable, the `!` is preserved.
    idiom_id: null
    research_finding_id: rf-dart-nullable-bang-after-assertnotnull-flow-narrowed
    nuance: >-
      FIRST-SEEN idiom for the SPECIFIC pattern of `body!.length` AFTER
      `expect(body, isNotNull)`. Two related cached idioms exist:
      `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`
      (compiler internals — body!/head!/tail! handling after explicit
      null-check guards) and `rf-dart-expect-isNotNull-to-xunit-assert-notnull`
      (the assertion mapping). Neither precisely covers the
      test-file pattern of `Assert.NotNull` followed by member-access on
      the asserted local. This idiom records the test-specific
      composition. Null-safety nuance (explicitly addressed): xUnit's
      `Assert.NotNull` is documented to perform flow-analysis null-state
      narrowing — see Microsoft Learn null-state-analysis
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/nullable-reference-types`)
      and xUnit's `Assert.NotNull(object?)` attribute decoration
      (`[NotNull]` parameter) which the compiler honours. Authoritative
      both sides. Dart `!` is the runtime-null-check operator that
      throws `TypeError` on null
      (`https://dart.dev/null-safety#null-aware-operators`); C# `!` is
      the COMPILE-TIME null-forgiving operator (no runtime check). The
      runtime semantics are equivalent ONLY if the prior assertion holds
      — which is precisely what the `Assert.NotNull` precondition
      guarantees. Without that prior assertion, `Assert.NotNull` would
      be the canonical translation regardless.
  - construct_key: dart.as_cast.downcast_to_subtype
    source_form: |-
      "final remote = goal as RemoteGoal;
       (remote.module as VarTerm).name;
       (remote.module as VarTerm).isReader;"
    target_decision: >-
      Dart `<expr> as T` (binary as-cast — runtime-checked downcast that
      throws `TypeError` on mismatch) maps to C# `(T)<expr>` (explicit
      cast — runtime-checked, throws `InvalidCastException` on
      mismatch). Three uses in this file (line 94 — `goal as RemoteGoal`
      stored in `remote`; lines 118/136 — `(remote.module as VarTerm)`
      inline-cast for `.name`/`.isReader` access). The first use can be
      rewritten as `var remote = (RemoteGoal)goal;` and the latter two
      as `((VarTerm)remote.Module).Name` / `((VarTerm)remote.Module).IsReader`.
      Codegen MAY alternatively emit the `is`-pattern-match form
      (`Assert.IsType<RemoteGoal>(goal); var remote = (RemoteGoal)goal;`
      — but in this file the test already has `Assert.IsType<RemoteGoal>`
      on the previous line for each `goal as RemoteGoal`, so the
      pattern-match form is the preferred shape — see nuance).
    idiom_id: rf-dart-as-cast-to-csharp-explicit-cast
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast
    nuance: >-
      Cached idiom (precedents: binding_pointer_test.dart.md `dart.as_cast.type_assertion_on_term_subtype`,
      moded_head_test.dart.md `dart.test.downcast_as_expression`,
      varref_pointer_test.dart.md prose, several lib/runtime/*.dart.md
      uses including commit.dart.md, body_kernels.dart.md, heap_fcp.dart.md).
      Exception-type nuance (explicitly addressed): Dart `as` throws
      `TypeError` (subclass of `Error`); C# `(T)x` throws
      `InvalidCastException` (subclass of `System.Exception`). For a test
      whose preceding `Assert.IsType<T>` already proved the runtime type,
      neither exception is ever thrown — observably equivalent. Pattern-
      matching nuance: C# 7+ `is T t` (`Assert.IsType<T>(goal); var
      remote = (RemoteGoal)goal;`) is the canonical follow-on shape
      documented at
      `https://learn.microsoft.com/dotnet/csharp/language-reference/operators/type-testing-and-cast`.
      For inline (T)x.member access, an alternative `(x as T)!.member` is
      possible but less idiomatic; codegen should prefer the explicit
      `((T)x).Member` form for clarity.
  - construct_key: dart.string.triple_quoted_raw_literal
    source_form: |-
      "final source = '''
       -module(math).
       factorial(0, 1).
       ''';
       ... (used in 13 of the 16 test bodies — every parser-test arrange step)"
    target_decision: >-
      Dart triple-single-quoted multi-line string literals (used to embed
      every `.glp` source fixture in this file) map to C# 11 raw string
      literals (`""" ... """`) — same as the cached idiom from
      boot_loader_test.dart.md. The literal payload is byte-identical
      across the boundary; codegen MUST emit the closing `"""` at the
      appropriate column to preserve indentation. Fallback to C#
      verbatim strings (`@"..."`) for pre-C#11 targets is equivalent
      here because no fixture in this file contains a `"`.
    idiom_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      Cached idiom (precedent: boot_loader_test.dart.md). Whitespace
      nuance: Dart triple-quoted strings preserve leading whitespace
      exactly; C# raw strings strip a common indent matched to the
      closing `"""` column. Newline-encoding nuance: Dart `'''...'''`
      uses `\n` line endings on all platforms (LF in the source);
      C# 11 raw strings preserve the source-file line endings — on
      Windows-edited source files this could be `\r\n`. Codegen MUST
      normalise to `\n` (LF) line endings inside the raw string literal
      so the byte-identity invariant holds against the Dart fixture.
  - construct_key: dart.string.single_quoted_literal
    source_form: "'a', '#', 'b', 'math', 'module', 'exported', 'procedure', 'foo', 'play_introduction', 'Hello', 'factorial', 'gcd', 'boot', 'main', etc."
    target_decision: >-
      Dart single-quoted single-line string literals (every label /
      identifier / fixture-content fragment in this file's `expect`
      calls) map to C# double-quoted string literals (`"a"`, `"#"`,
      etc.). Escape-sequence nuance is trivial here because no literal
      in this file uses Dart-specific escapes (`\$`, `\u{...}`) — all
      content is ASCII printable. The Dart `'Hello'` inside
      `print("Hello")` in the procedure-declarations group fixtures is
      part of the embedded `.glp` source (triple-quoted payload), not
      a Dart string at the host-language level, so its mapping is
      handled by the raw-string idiom above.
    idiom_id: null
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      FIRST-SEEN idiom row. Quote-character nuance (explicitly addressed):
      Dart accepts BOTH `'...'` and `"..."` for single-line literals (no
      semantic difference); C# accepts ONLY `"..."`. Embedded-quote
      nuance: Dart `'don\\'t'` becomes C# `"don't"` (no escape needed
      because the C# quote character is different); Dart `"He said \\\"hi\\\""`
      becomes C# `"He said \\\"hi\\\""` (same escape needed). No literal
      in this file requires either transformation, but the idiom row
      documents the rule for downstream files. Authoritative both sides:
      Dart language tour "Strings"
      (`https://dart.dev/language/built-in-types#strings`); C# language
      reference "String literals"
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-string-type`).
  - construct_key: dart.enum.value_access
    source_form: |-
      "TokenType.HASH; TokenType.ATOM; TokenType.MINUS; TokenType.LPAREN;
       TokenType.PROCEDURE; TokenType.RPAREN; TokenType.DOT;"
    target_decision: >-
      Dart enum value-access `<EnumType>.<VALUE>` maps DIRECTLY to C#
      enum value-access `<EnumType>.<VALUE>` (identical syntax). All
      seven `TokenType.*` references in this file translate without
      modification. The case of the enum members is a separate naming-
      convention concern (Dart uses `SCREAMING_SNAKE` for some enums and
      `lowerCamel` for others; C# `enum` convention is PascalCase) —
      codegen MUST consult `lib/compiler/token.dart.md` for the final
      naming decision, but the access syntax itself is identical.
    idiom_id: null
    research_finding_id: rf-dart-enum-value-access-to-csharp-enum-value-access
    nuance: >-
      FIRST-SEEN idiom row (trivial — recorded for KB completeness so
      future files do not re-research). Naming-convention nuance
      (explicitly addressed): Dart's enum-member case is per-enum (the
      original `token.dart` uses `HASH`, `ATOM`, etc. — already
      SCREAMING_SNAKE); C# convention is PascalCase but `SCREAMING_SNAKE`
      is permitted by the language and is the documented choice when
      preserving cross-language fidelity. The `token.dart.md` convspec
      decides the final case; this file inherits it. Enum-storage
      nuance: Dart enums are reference-equal singletons; C# `enum` is a
      value type backed by an integer. Equality comparison (`==`) is
      identical for both. Authoritative: Dart language tour "Enums"
      (`https://dart.dev/language/enums`); C# language reference "Enums"
      (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/enum`).
  - construct_key: dart.list.indexer_access
    source_form: |-
      "tokens[0]; tokens[1]; tokens[2]; tokens[3]; tokens[4]; tokens[5];
       module.procedures[0]; module.procedures[1];
       program.procedures[0]; program.procedures[0].clauses[0];
       clause.body![0]; clause.body![1];
       bootClause.body![0]; bootClause.body![1];"
    target_decision: >-
      Dart list / `List<T>` zero-indexed `[i]` access maps DIRECTLY to
      C# `List<T>` / `IList<T>` zero-indexed `[i]` access (identical
      syntax). Both throw on out-of-range (`RangeError` in Dart,
      `ArgumentOutOfRangeException` in C#). No mapping logic needed;
      this row exists to record the trivial-but-pervasive idiom so
      downstream files reuse it.
    idiom_id: null
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      FIRST-SEEN idiom row (trivial — recorded for KB completeness).
      Exception-type nuance (explicitly addressed): Dart `RangeError`
      vs C# `ArgumentOutOfRangeException` are distinct exception types
      but the test bodies never exercise the OOB case (every indexer in
      this file targets a known-good position established by the
      preceding `tokens.length == N` / `procedures.length == N` /
      `body!.length == N` assertion). Authoritative: Dart core library
      `List` operator `[]`
      (`https://api.dart.dev/stable/dart-core/List/operator_get.html`);
      C# `IList<T>.Item`
      (`https://learn.microsoft.com/dotnet/api/system.collections.generic.ilist-1.item`).
conversion_units:
  - cu-1: file-scope using directives (Xunit + System + System.Collections.Generic + SUT namespace from glp_runtime/compiler/{lexer,parser,ast,token}.dart)
  - cu-2: namespace declaration mirroring the test/module path (e.g. <RootNs>.Test.Module)
  - cu-3: class ModuleParserLexerTests with 3 `[Fact(DisplayName="...")]` methods (LexerRecognizesHashToken, LexerHandlesModuleDeclarationTokens, LexerHandlesExportedKeywordTokens)
  - cu-4: class ModuleParserModuleDeclarationTests with 2 `[Fact]` methods (ParserParsesModuleDeclaration, ParserParsesHierarchicalModuleName)
  - cu-5: class ModuleParserRemoteGoalTests with 5 `[Fact]` methods (ParserParsesStaticRemoteGoal, ParserParsesDynamicRemoteGoal, ParserParsesReaderVariableRemoteGoal, ParserParsesChainedRemoteGoals, ParserRejectsModuleWithArguments) — last method uses Assert.Throws<Exception>
  - cu-6: class ModuleParserCompleteModuleTests with 3 `[Fact]` methods (ParserParsesCompleteModuleFileWithExportedProcedure, ParserHandlesModuleWithoutDeclarations, LegacyParseSkipsModuleDeclaration) — first uses HashSet<string> set-equality
  - cu-7: class ModuleParserProcedureDeclarationsTests with 3 `[Fact]` methods (ParserParsesNullaryProcedureWithoutParentheses, ParserParsesNullaryProcedureWithEmptyParentheses, ParserParsesProcedureWithArguments)
  - cu-8: class ModuleParserRemoteGoalInModuleTests with 1 `[Fact]` method (ParserParsesModuleWithRemoteGoals) — uses HashSet<string> set-equality on exportedSignatures
  - cu-9: raw-string-literal payloads (`"""..."""`) for every embedded `.glp` source fixture across the 16 test methods, with LF line endings and column-0 closing delimiter so the literal payload is byte-identical to the Dart fixture
escalations: []
```

## Rationale + research provenance

### Cached-idiom reuse profile (SC-007 / FR-012)

13 of the 18 constructs in this file resolve via a CACHED idiom_id from
prior test-file convspecs:

- `rf-dart-package-test-import-to-xunit-using` (precedent:
  mad_error_handling_test.dart.md)
- `rf-dart-internal-package-import-to-csharp-using` (precedent:
  boot_loader_test.dart.md, varref_pointer_test.dart.md)
- `rf-dart-package-test-main-omit-in-xunit` (precedent:
  mad_error_handling_test.dart.md, boot_loader_test.dart.md)
- `rf-dart-package-test-group-to-xunit-class` (precedent:
  mad_error_handling_test.dart.md, boot_loader_test.dart.md,
  global_send_test.dart.md)
- `rf-dart-test-callback-to-xunit-method-body` (multiple precedents)
- `rf-dart-final-local-to-csharp-var-local` (precedents:
  boot_loader_test.dart.md, varref_pointer_test.dart.md,
  global_send_test.dart.md, binding_pointer_test.dart.md)
- `rf-dart-expect-equals-to-xunit-assertequal` (precedent:
  mad_error_handling_test.dart.md prose, moded_head_test.dart.md,
  boot_loader_test.dart.md)
- `rf-dart-expect-isNotNull-to-xunit-assert-notnull` (precedents:
  global_send_test.dart.md, localize_test.dart.md, smoke_test.dart.md)
- `rf-dart-expect-isNull-to-xunit-assert-null` (precedents:
  global_send_test.dart.md, binding_pointer_test.dart.md,
  global_writers_table_test.dart.md)
- `rf-dart-expect-isEmpty-to-xunit-assert-empty` (precedent:
  localize_test.dart.md)
- `rf-dart-expect-isA-to-xunit-assert-istype` (precedents:
  varref_pointer_test.dart.md, binding_pointer_test.dart.md)
- `rf-dart-set-literal-typed-to-csharp-hashset-initializer`
  (precedent: varref_pointer_test.dart.md)
- `rf-dart-as-cast-to-csharp-explicit-cast` (precedents:
  binding_pointer_test.dart.md, moded_head_test.dart.md, several
  lib/runtime/*.dart.md convspecs)
- `rf-dart-triple-quoted-string-to-csharp-raw-string` (precedent:
  boot_loader_test.dart.md)

Reusing these cached idioms verbatim (no re-research, no re-derivation)
satisfies the FR-012 / SC-007 consistency guarantee. The KB-lookup
decision-order from `convspec_idiom_schema.md` was applied per construct:
KB lookup hit → REUSE.

### Five FIRST-SEEN idiom rows (research-justified, NO escalation)

Five constructs require new idiom rows because no precedent covers them.
Each was researched against official Dart + .NET documentation per
FR-024:

1. **`rf-dart-throwsa-anything-to-xunit-assert-throws-exception`** —
   Dart `throwsA(anything)` (line 164 of the source) is the
   "throws-anything" matcher. xUnit has no `Assert.ThrowsAnything`;
   the canonical translation uses the root throwable type
   `System.Exception`. Authoritative bases: Dart matcher `anything`
   constant (`https://pub.dev/documentation/matcher/latest/matcher/anything-constant.html`);
   xUnit exception assertions
   (`https://xunit.net/docs/comparisons#exceptions`); `System.Exception`
   reference (`https://learn.microsoft.com/dotnet/api/system.exception`).
   Distinguished from the cached
   `rf-dart-throwsa-isa-to-xunit-throws-simple` (constrains TYPE) and
   `rf-dart-throwsa-isa-having-to-xunit-throws-plus-assert`
   (constrains TYPE + MESSAGE).

2. **`rf-dart-nullable-bang-after-assertnotnull-flow-narrowed`** —
   the test-specific composition of `Assert.NotNull(x)` followed by
   `x!.<member>` access. xUnit's `Assert.NotNull(object?)` is decorated
   with `[NotNull]` so the C# compiler null-flow analyser narrows the
   asserted local to non-nullable for the remainder of the method
   scope. Authoritative bases: Microsoft Learn nullable-reference-types
   spec
   (`https://learn.microsoft.com/dotnet/csharp/language-reference/proposals/csharp-9.0/nullable-reference-types`);
   Dart null-safety reference
   (`https://dart.dev/null-safety#null-aware-operators`). Distinct from
   the cached
   `rf-dart-nullable-bang-inside-null-check-to-csharp-flow-narrowed-access`
   (which covers compiler-internal `body!`/`head!`/`tail!` accesses
   inside explicit null-check guards in non-test code).

3. **`rf-dart-single-quoted-string-to-csharp-double-quoted-string`** —
   trivial but recorded for KB completeness. Dart accepts `'...'` and
   `"..."`; C# accepts only `"..."`. Authoritative bases: Dart language
   tour "Strings" (`https://dart.dev/language/built-in-types#strings`);
   C# language reference "String literals"
   (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types#the-string-type`).

4. **`rf-dart-enum-value-access-to-csharp-enum-value-access`** —
   trivial. `<EnumType>.<VALUE>` is identical syntax both sides.
   Authoritative bases: Dart language tour "Enums"
   (`https://dart.dev/language/enums`); C# language reference "Enums"
   (`https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/enum`).

5. **`rf-dart-list-indexer-to-csharp-list-indexer`** — trivial.
   `xs[i]` is identical syntax both sides on `List<T>` /
   `IList<T>`. Authoritative bases: Dart core `List.operator []`
   (`https://api.dart.dev/stable/dart-core/List/operator_get.html`);
   C# `IList<T>.Item`
   (`https://learn.microsoft.com/dotnet/api/system.collections.generic.ilist-1.item`).

Trivial idioms 3, 4, and 5 are recorded explicitly (rather than glossed)
because (a) the file uses them pervasively and the KB benefits from one
authoritative entry per Dart→C# transform, (b) the convspec quality bar
requires every non-trivial nuance to be explicitly addressed and the
single-vs-double-quote / enum-syntax / indexer-syntax transforms have
SUBTLE per-language differences worth one row each (escape sequences,
naming conventions, OOB exception types), and (c) downstream test-file
convspecs can resolve via cached lookup instead of re-deriving.

### Sibling-group topology (six independent classes)

This file has SIX SIBLING top-level groups — distinct from the precedent
`boot_loader_test.dart.md` topology (outer + three-inner groups with a
shared `late` field). Because no outer group exists and no `setUp`
appears anywhere in `main`, none of the per-group test classes share
state. The cleanest target shape is therefore one C# `public class` per
Dart `group`, all in the same `.cs` file. Multi-class-per-file is fully
supported by xUnit's reflection-based test discovery
(`https://xunit.net/docs/getting-started/v3/getting-started`). The
alternative — FLATTEN to one class with `[Trait("Group","...")]` per
method — was rejected here because (i) zero shared state removes the
boot-loader rationale, (ii) six classes produce cleaner VS Test Explorer
grouping for the test-method counts in each group (3 + 2 + 5 + 3 + 3 +
1), and (iii) each class can be opened/edited as a focused unit.

### `parser rejects module with arguments` — `throwsA(anything)` mapping

The lone use of `throwsA(anything)` at line 164 is the test that asserts
the parser refuses `foo(x) # bar.` (modules cannot take arguments).
`Assert.Throws<Exception>` is the canonical mapping because `Exception`
is the .NET root user-throwable type — every `ParserError` /
`FormatException` / `ArgumentException` etc. that the converted parser
might throw is a subtype, so the assertion holds. The strict-faithful
alternative `Assert.ThrowsAny<Exception>` additionally tolerates
subtypes, but since `Exception` is the ROOT type the two are observably
identical here. The research finding records both forms for downstream
files that may target intermediate exception roots
(e.g. `Assert.Throws<SystemException>` if a future convspec narrows the
expected exception family).

### `as`-cast composition with `Assert.IsType` precondition

Every `as`-cast in this file (lines 94, 118, 136) is paired with a
PRECEDING `Assert.IsType<T>` assertion on the same value — line 92
asserts `isA<RemoteGoal>` before line 94 stores `goal as RemoteGoal` as
`remote`; lines 117/135 assert `isA<VarTerm>` before the inline
`(remote.module as VarTerm)` casts on lines 118/136. The C# pattern-
matching form `Assert.IsType<T>(x); var t = (T)x;` is canonical and
preferred (documented at
`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/type-testing-and-cast`).
For inline access, `((T)x).Member` is the explicit form; an alternative
`(x as T)!.Member` is permitted in C# but less idiomatic — codegen
should prefer the explicit cast for clarity.

### Set-literal vs HashSet — set-equality semantics

`module.exportedSignatures` is declared `Set<String>` per ast.dart's
`Module` class; the two `expect(exportedSignatures, {...})` calls (lines
194, 296) compare against Dart untyped `Set<String>` literals. The
correctness requirement is set-equality (membership-based, NOT order-
sensitive). C# `HashSet<string>` overrides `Equals` via `SetEquals`
semantics
(`https://learn.microsoft.com/dotnet/api/system.collections.generic.hashset-1.setequals`),
so xUnit `Assert.Equal(HashSet<string>, HashSet<string>)` correctly
compares set-equality. The alternative `Assert.True(expected.SetEquals(
actual))` is even more explicit and codegen may prefer it for clarity.

### Why no escalations (FR-013)

Every construct has a clear, single-decision target shape grounded in
official Dart and .NET documentation. The two "soft" decisions (one-
class-per-group vs FLATTEN; `Assert.Throws<T>` vs `Assert.ThrowsAny<T>`
on the `throwsA(anything)`) are documented project-wide policy
(corroborating alternatives recorded in the relevant research findings),
not unresolved choices. The cast-vs-pattern-match call on `as`-cast is
a deliberate, in-file-justified preference (post-`Assert.IsType` =
pattern-match form). No construct involves an idiom-vs-research
conflict or an idiom-vs-idiom conflict, and nothing is undecidable.
`escalations: []` is therefore intentional, not a placeholder.
