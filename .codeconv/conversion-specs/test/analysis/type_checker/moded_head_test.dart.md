> Conversion-spec artifact for test/analysis/type_checker/moded_head_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/analysis/type_checker/moded_head_test.dart
source_sha256: e04d3f734b2cdac1abe63bc1acdd4cf4178743725224834979a741c750a82a9b
target_code_unit: test/analysis/type_checker/ModedHeadTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit is the project-wide pinned
      target framework (precedent established by
      test/multiagent/mad_error_handling_test.dart.md and reused verbatim by
      test/multiagent/boot_loader_test.dart.md — idiom
      rf-dart-package-test-import-to-xunit-using). Codegen MUST also add
      `using System;` (referenced by `ArityMismatchError` exception assertions
      under dart.package_test.expect_throwsA_isA) and project to a namespace
      mirroring the Dart `test/analysis/type_checker` directory (e.g.
      `<RootNs>.Test.Analysis.TypeChecker`).
    idiom_id: null
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy nuance, NOT a
      file-local choice (reused verbatim from boot_loader_test.dart.md and
      mad_error_handling_test.dart.md): every `package:test` file MUST map to
      the SAME .NET framework so test discovery, runner config, and attribute
      vocabulary stay consistent. xUnit chosen because (a) `[Fact]`/`[Theory]`
      maps 1:1 to Dart `test()`/parameterised `test()`, (b) xUnit's
      constructor-per-test isolation matches `package:test`'s fresh-state
      semantics, (c) xUnit is the modern .NET default. NUnit and MSTest are
      corroborating alternatives recorded once at the idiom level — not
      re-derived here.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/analysis/type_checker/moded_head.dart';
      import 'package:glp_runtime/analysis/type_checker/moded_term.dart';
      import 'package:glp_runtime/analysis/type_checker/mode.dart';
      import 'package:glp_runtime/analysis/type_checker/type_ast.dart';
      import 'package:glp_runtime/compiler/ast.dart' as ast;"
    target_decision: >-
      Map each `package:glp_runtime/...` import to a `using` directive that
      names the C# namespace produced by converting the corresponding Dart
      library file. Concretely the spec records FIVE cross-file dependencies
      whose target namespaces are determined when those Dart files are
      themselves specced/converted (out of scope for THIS artifact — this
      spec records only the SHAPE of the dependency). The `as ast` prefix
      import maps to a C# `using ast = <RootNs>.Compiler.Ast;` namespace
      alias so the `ast.Goal` / `ast.VarTerm` / `ast.ListTerm` / `ast.ConstTerm`
      / `ast.UnderscoreTerm` / `ast.StructTerm` / `ast.Term` references in
      this file resolve via the alias rather than the bare namespace
      (preserves the Dart authoring intent that AST node types are namespaced
      to disambiguate from the type-checker's own AST).
    idiom_id: null
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (reused from boot_loader_test.dart.md):
      Dart `package:glp_runtime/...` is a pubspec-anchored URI; C# has no
      per-file URI — only assembly + namespace. The test assembly must
      reference the SUT assembly via the project file (project-system idiom,
      out of scope for this artifact). NEW nuance specific to THIS file:
      Dart `import '<uri>' as <prefix>;` (prefix import) maps to C#
      `using <prefix> = <Namespace>;` (namespace alias) — NOT `using static`,
      because the file references types under the prefix (`ast.Goal`,
      `ast.VarTerm`) as types, not static members. This is the canonical
      C# mapping documented at
      https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/using-directive
      ("Using alias directive"). The non-prefixed imports each become a
      bare `using <RootNs>.Analysis.TypeChecker;` directive.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('modedHead', () { ... }); group('producedTerm', () { ... }); group('explicit dual type definitions', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint. xUnit
      discovers `[Fact]` methods by reflection — there is NO per-file
      entrypoint to emit. Eliminate `main` entirely. Its body contains
      THREE sibling outer `group(...)` calls (NOT one as in boot_loader);
      each maps to a distinct xUnit test class (see group_block below).
    idiom_id: null
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (reused from boot_loader_test.dart.md): Dart `main`
      runs once per test-file process; xUnit has no per-file hook — only
      per-class (constructor + IDisposable.Dispose) and per-collection
      fixtures. NEW nuance specific to THIS file: `main` contains THREE
      sibling outer `group` calls, NOT one. Each top-level `group` becomes
      its OWN test class (see group_block) so the omission of `main` is
      still lossless — its only role here is to host the three sibling
      group blocks, and that role is taken by file-level class
      declarations.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('modedHead', () { ast.Goal goal(...); ...; group('basic
      construction', ...); group('variable replacement', ...);
      group('anonymous variables', ...); group('error handling', ...); });
      group('producedTerm', () { ...; test(...); test(...); });
      group('explicit dual type definitions', () { ...; test(...);
      test(...); });"
    target_decision: >-
      Three OUTER groups (`modedHead`, `producedTerm`, `explicit dual type
      definitions`) become THREE sibling xUnit test classes:
      `ModedHeadTests`, `ProducedTermTests`, `ExplicitDualTypeDefinitionsTests`,
      all in the same file (a single .cs file may contain multiple
      non-nested classes — xUnit discovers each independently). Inside
      `ModedHeadTests` the four INNER groups (`basic construction`,
      `variable replacement`, `anonymous variables`, `error handling`)
      FLATTEN to methods on `ModedHeadTests`, each preserving its inner
      group via `[Trait("Group", "<inner-label>")]` and prefixing the
      method name with the PascalCased inner-group label
      (`BasicConstruction_MergeHead`,
      `VariableReplacement_WriterAtConsumePositionBecomesReader`,
      `AnonymousVariables_EachAnonymousVariableGetsUniqueName`,
      `ErrorHandling_ArityMismatchThrowsArityMismatchError`). Every test
      method also carries `[Fact(DisplayName = "<original label>")]` so
      the human-readable sentence form survives. The outer-group label
      `modedHead` becomes the CLASS name (PascalCased) and is NOT emitted
      as a `[Trait]` (the class already encodes it). `ProducedTermTests`
      and `ExplicitDualTypeDefinitionsTests` are single-level (no inner
      groups) and therefore have no `[Trait("Group", ...)]` attributes.
    idiom_id: null
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Nested-group nuance (reused from boot_loader_test.dart.md, with
      additional choice in THIS file): three target shapes are viable —
      (i) FLATTEN all into one class, (ii) NESTED public classes per inner
      group sharing a base, (iii) `IClassFixture<>` per inner group.
      Choice (i) is taken for `ModedHeadTests`'s inner groups because the
      file-local HELPER closures (`goal`, `writer`, `reader`, `nil`,
      `cons`, `constTerm`) are defined ONCE in the outer `modedHead`
      group's body and closed over by every inner `test` — see
      dart.test.local_helper_closures below — splitting into per-inner-group
      classes would force duplicating those helpers. Choice across the
      THREE outer groups: each outer group has its OWN local helper
      closures (`goal`/`writer`/`reader` re-declared inside `producedTerm`
      and `explicit dual type definitions` because Dart's lexical scoping
      does not propagate them from `modedHead`), so the three outer groups
      MUST be three sibling classes — one class with `[Trait]` per outer
      group would force a SINGLE set of helper methods shared across
      groups, but the THIRD outer group's helpers add a `struct` helper
      that the others do not have. Three sibling classes give each its
      own scope. PascalCasing nuance: `modedHead` (camelCase) ->
      `ModedHeadTests`; `producedTerm` -> `ProducedTermTests`; `explicit
      dual type definitions` (whitespace + lower-case) ->
      `ExplicitDualTypeDefinitionsTests`. The "Tests" suffix is a
      project-wide convention (already used by boot_loader.dart.md's
      `BootLoaderTests`).
  - construct_key: dart.test.local_helper_closures
    source_form: >-
      "ast.Goal goal(String f, List<ast.Term> args) => ast.Goal(f, args, 0, 0);
      ast.VarTerm writer(String name) => ast.VarTerm(name, false, 0, 0);
      ast.VarTerm reader(String name) => ast.VarTerm(name, true, 0, 0);
      ast.ListTerm nil() => ast.ListTerm(null, null, 0, 0);
      ast.ListTerm cons(ast.Term h, ast.Term t) => ast.ListTerm(h, t, 0, 0);
      ast.ConstTerm constTerm(Object v) => ast.ConstTerm(v, 0, 0);
      ast.StructTerm struct(String f, List<ast.Term> args) =>
        ast.StructTerm(f, args, 0, 0);"
    target_decision: >-
      Each local helper closure declared inside a `group` callback maps to
      a `private static` HELPER METHOD on the enclosing xUnit test class
      (or a `private static class TestHelpers` nested under it). Expression-
      body Dart arrows (`=>`) map directly to C# expression-bodied methods
      (`=>`). Concretely on `ModedHeadTests`:
      `private static ast.Goal Goal(string f, IReadOnlyList<ast.Term> args)
        => new ast.Goal(f, args, 0, 0);`
      `private static ast.VarTerm Writer(string name)
        => new ast.VarTerm(name, false, 0, 0);`
      `private static ast.VarTerm Reader(string name)
        => new ast.VarTerm(name, true, 0, 0);`
      `private static ast.ListTerm Nil()
        => new ast.ListTerm(null, null, 0, 0);`
      `private static ast.ListTerm Cons(ast.Term h, ast.Term t)
        => new ast.ListTerm(h, t, 0, 0);`
      `private static ast.ConstTerm ConstTerm(object v)
        => new ast.ConstTerm(v, 0, 0);`
      `ProducedTermTests` and `ExplicitDualTypeDefinitionsTests` each
      receive their OWN copy of `Goal`/`Writer`/`Reader` (matching the
      Dart re-declarations); `ExplicitDualTypeDefinitionsTests` additionally
      receives `private static ast.StructTerm Struct(string f,
      IReadOnlyList<ast.Term> args) => new ast.StructTerm(f, args, 0, 0);`.
    idiom_id: null
    research_finding_id: rf-dart-local-helper-closure-to-csharp-static-method
    nuance: >-
      Closure-capture nuance (explicitly addressed): the Dart helpers
      capture NOTHING from the enclosing scope (no `late` field, no
      surrounding state) — they are pure functions of their arguments.
      C# `private static` methods are therefore the precise mapping; no
      lambda field, no instance method needed. Naming nuance: Dart
      `constTerm` (lowercase-c) conflicts with C# `const` keyword if
      used unqualified — PascalCasing to `ConstTerm` resolves the
      collision. Method-name `Cons` does NOT collide with anything in
      C# (no `cons` keyword). Argument-type nuance: Dart `List<T>` in a
      formal parameter position maps idiomatically to
      `IReadOnlyList<T>` in C# (read-only view; matches the helper's
      use which only reads the list). If a different in-file convention
      prefers `IList<T>` or `T[]`, the same rule applies uniformly —
      this is a project-wide collection-type idiom recorded once.
  - construct_key: dart.test.constructor_call_implicit_new
    source_form: "ast.Goal(f, args, 0, 0); ast.VarTerm(name, false, 0, 0); ProcDecl('merge', [...], 0, 0); TypeRef('Stream', 0, 0, isInput: true); TypeEnvironment({}, {}); TypeDef('Channel', [...], 0, 0); StructAlt('ch', [...], 0, 0); ListNilAlt(0, 0); ListConsAlt(...); PrimitiveModeAlt(false, 0, 0); DiffListAlt(...); ast.UnderscoreTerm(0, 0);"
    target_decision: >-
      Dart 2+ allows implicit `new` for constructor calls
      (`ast.Goal(...)` instead of `new ast.Goal(...)`). C# requires the
      `new` keyword OR uses C# 9+ target-typed `new()` where the target
      type is inferred. Codegen MUST emit either the explicit
      `new ast.Goal(f, args, 0, 0)` form (works on every C# version) OR
      the target-typed `new(f, args, 0, 0)` form (requires C# 9+ and a
      known target type — applies where the result is assigned to a
      typed variable or returned from a typed method). The conservative
      default is explicit `new`. ALL constructor calls in this file are
      affected: `ast.Goal`, `ast.VarTerm`, `ast.ListTerm`, `ast.ConstTerm`,
      `ast.UnderscoreTerm`, `ast.StructTerm`, `ProcDecl`, `TypeRef`,
      `TypeEnvironment`, `TypeDef`, `StructAlt`, `ListNilAlt`,
      `ListConsAlt`, `PrimitiveModeAlt`, `DiffListAlt`.
    idiom_id: null
    research_finding_id: rf-dart-implicit-new-to-csharp-explicit-or-targettyped-new
    nuance: >-
      Constructor-syntax nuance (explicitly addressed): Dart's implicit
      `new` is purely syntactic and has identical semantics to explicit
      `new` — there is no behavioural difference to preserve. The
      target-typed `new()` form is shorter where the target type is
      already visible (variable declarations, return statements, named
      argument values), but explicit `new T(...)` is mandatory inside
      collection initialisers without target types and inside
      `Assert.Equal(...)` argument lists where the expected-type slot
      may be `object`. Codegen rule: emit target-typed `new(...)` ONLY
      where the C# compiler can infer the target type (assignment to
      typed local, named parameter with typed signature); otherwise
      explicit `new T(...)`. Authoritative basis:
      https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator#target-typed-new
  - construct_key: dart.test.named_argument_passing
    source_form: "TypeRef('Stream', 0, 0, isInput: true); ListConsAlt(PrimitiveModeAlt(false, 0, 0), TypeRef('Stream', 0, 0, isInput: false), 0, 0); modedHead(head, decl, typeEnv: typeEnv);"
    target_decision: >-
      Dart `name: value` named argument syntax maps directly to C# named
      arguments `name: value` — identical syntax, identical semantics
      (positional-first, then named, named args may appear in any order).
      Concretely `TypeRef('Stream', 0, 0, isInput: true)` ->
      `new TypeRef("Stream", 0, 0, isInput: true)` and
      `modedHead(head, decl, typeEnv: typeEnv)` ->
      `ModedHead(head, decl, typeEnv: typeEnv)`. The named argument
      `isInput` MUST be preserved by name in the C# constructor signature
      for `TypeRef` (when `type_ast.dart` is itself converted) so this
      call-site does not need to be rewritten. If the converted C#
      signature uses a different name (e.g. `IsInput`), this site must
      track that rename — the convention is to PascalCase parameter names
      in C# (`IsInput`), at which point this call-site becomes
      `isInput: true` -> `IsInput: true`. Codegen MUST adopt one
      convention project-wide.
    idiom_id: null
    research_finding_id: rf-dart-named-arguments-to-csharp-named-arguments
    nuance: >-
      Named-argument convention nuance (explicitly addressed): C# style
      guides PascalCase public/protected member names but camelCase
      parameter names — so `isInput` REMAINS camelCase in C# (matches
      official C# naming conventions:
      https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#parameter-names).
      Therefore the named-argument call sites `isInput: true` /
      `isInput: false` / `typeEnv: typeEnv` translate UNCHANGED. Order
      nuance: Dart and C# both allow named args in any order after
      positional args — semantically identical.
  - construct_key: dart.test.test_call_simple
    source_form: "test('<label>', () { /* construct AST, call SUT, expect(...) */ });"
    target_decision: >-
      Each Dart `test(label, body)` with no `skip:` argument and a
      synchronous closure body becomes a `public void` instance method
      on the enclosing test class, decorated with
      `[Fact(DisplayName = "<original label>")]`. The method name is the
      group-prefixed (where applicable) PascalCased label. The closure
      body converts statement-for-statement. ALL tests in this file are
      synchronous (no `async`/`Future`) so no target method is
      `async Task`. Test inventory: 7 tests on `ModedHeadTests` (1 in
      `basic construction`, oops — re-counted: 2 in `basic construction`
      + 4 in `variable replacement` + 1 in `anonymous variables` + 1 in
      `error handling` = 8 tests), 2 tests on `ProducedTermTests`, 2
      tests on `ExplicitDualTypeDefinitionsTests` = 12 tests total.
    idiom_id: null
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async nuance (reused from boot_loader_test.dart.md): a Dart
      `test('...', () async { ... })` would target `public async Task
      <Name>()`; xUnit awaits the returned Task. NOT used in this file.
      Closure-capture nuance: each callback captures local helpers
      (`goal`, `writer`, `reader`, etc.) from the enclosing outer group
      scope; the xUnit translation calls `Goal(...)`, `Writer(...)`,
      `Reader(...)` as `private static` methods on `this` class
      (equivalent — see dart.test.local_helper_closures).
  - construct_key: dart.package_test.expect_equals
    source_form: "expect(<actual>, equals(<expected>));"
    target_decision: >-
      Map to xUnit `Assert.Equal(<expected>, <actual>)`. ARGUMENT-ORDER
      FLIP: Dart `expect(actual, equals(expected))` puts actual first;
      xUnit `Assert.Equal(expected, actual)` puts expected first.
      Concretely the dozens of `expect(x.name, equals('X'))` -style
      assertions in this file become `Assert.Equal("X", x.Name);`. The
      `equals` matcher uses Dart `==` equality; `Assert.Equal` uses
      `IEquatable<T>.Equals` / `Object.Equals` — equivalent for the
      value types used here (`String`, `int`, `Mode` enum).
    idiom_id: null
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (reused from boot_loader_test.dart.md —
      well-known footgun): reversing actual/expected silently produces
      misleading failure messages. Codegen MUST emit
      `Assert.Equal(expected, actual)`. NEW nuance specific to THIS file:
      `equals(Mode.consume)` / `equals(Mode.produce)` are ENUM comparisons.
      Dart enums and C# enums both use value equality, so
      `Assert.Equal(Mode.Consume, arg1.Mode)` is correct. Enum-name
      casing: Dart `Mode.consume` (lowercase) -> C# `Mode.Consume`
      (PascalCase enum members per
      https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#names-of-enumeration-types).
      Field-access casing: Dart `compound.functor`, `compound.arity`,
      `compound.args`, `x.name`, `x.isReader`, `x.mode`, `arg1.isListCons`,
      `arg1.mode`, `nilArg.isNil` ALL PascalCase to `Functor`, `Arity`,
      `Args`, `Name`, `IsReader`, `Mode`, `IsListCons`, `Mode`, `IsNil`.
      Field-vs-property nuance: Dart auto-properties (no parens needed)
      map naturally to C# properties (no parens); if the SUT exposes
      these as fields, they convert as fields — same call-site syntax.
  - construct_key: dart.package_test.expect_isA_type_check
    source_form: "expect(result, isA<ModedCompound>()); expect(result, isA<ModedConstant>()); expect(result, isA<ModedVariable>());"
    target_decision: >-
      Map to xUnit `Assert.IsType<ModedCompound>(result)` (or
      `Assert.IsAssignableFrom<ModedCompound>(result)` for the subtype-
      tolerant variant). Dart `isA<T>()` accepts SUBTYPES — strictly the
      faithful translation is `Assert.IsAssignableFrom<T>` — but the
      types tested here (`ModedCompound`, `ModedConstant`, `ModedVariable`)
      are the EXACT runtime types of the asserted values (no subtype
      polymorphism observed), so `Assert.IsType<T>` is observably
      equivalent and is the more diagnostic xUnit form (reports exact
      type mismatch). Codegen rule: emit `Assert.IsType<T>` UNLESS the
      tested type has known subtypes in the converted code, in which
      case emit `Assert.IsAssignableFrom<T>`.
    idiom_id: null
    research_finding_id: rf-dart-expect-isa-to-xunit-istype
    nuance: >-
      Type-check nuance (explicitly addressed): `isA<T>` is subtype-
      tolerant (matches `Assert.IsAssignableFrom<T>`); `Assert.IsType<T>`
      is exact-type. The file follows `expect(result, isA<ModedCompound>())`
      with `final compound = result as ModedCompound;` (Dart downcast) —
      this is a strong signal that the test intends the EXACT type, not a
      subtype. The combined pattern (isA-then-cast) maps cleanly to
      xUnit's TYPED `Assert.IsType<T>(result)` overload which RETURNS the
      typed value, so the cast disappears:
      `var compound = Assert.IsType<ModedCompound>(result);` — eliminates
      the cast statement AND covers the type assertion in one call. This
      is the canonical xUnit idiom for "assert type and use as that type"
      (https://xunit.net/docs/comparisons#assertions, "Type checks"). The
      research finding records both the simple form (assert-only) and the
      combined form (assert-and-extract).
  - construct_key: dart.test.downcast_as_expression
    source_form: "final compound = result as ModedCompound; final arg1 = compound.args[0] as ModedCompound; final x1 = arg1.args[0] as ModedVariable; final nilArg = result.args[1] as ModedConstant;"
    target_decision: >-
      Dart `expr as T` is a checked downcast that throws `TypeError` on
      failure. The C# equivalent depends on whether the cast is paired
      with a prior `isA<T>` assertion:
      (a) When PAIRED with `expect(x, isA<T>())` immediately above —
      FOLD into `var x = Assert.IsType<T>(value);` (the xUnit assertion
      RETURNS the typed value, eliminating the separate cast — see the
      `expect_isA_type_check` construct above).
      (b) When STANDALONE (no preceding `isA<T>` check, e.g.
      `final arg1 = compound.args[0] as ModedCompound;`) — emit
      `var arg1 = (ModedCompound)compound.Args[0];` (C# checked cast,
      throws `InvalidCastException` on failure — semantically equivalent
      to Dart's `TypeError`). Pattern (b) applies to every deeply nested
      destructuring downcast (e.g. `compound.args[0] as ModedCompound`,
      `arg1.args[0] as ModedVariable`, `arg2.args[0] as ModedVariable`).
    idiom_id: null
    research_finding_id: rf-dart-as-cast-to-csharp-explicit-cast-or-istype-fold
    nuance: >-
      Cast-failure-semantics nuance (explicitly addressed): Dart `as T`
      and C# `(T)expr` both throw on type mismatch (Dart `TypeError`, C#
      `InvalidCastException`). The Dart `expr as? T` (conditional cast)
      maps to C# `expr as T` (returns null on mismatch, NOT throwing) —
      NOT used in this file. Element-access nuance: Dart `list[i]` and
      C# `list[i]` are syntactically identical; the helper return type
      `List<ast.Term>` (Dart) -> `IReadOnlyList<ast.Term>` (C#) preserves
      indexer access. Folding nuance: pattern (a) — collapsing
      `expect(r, isA<T>()); var x = r as T;` into
      `var x = Assert.IsType<T>(r);` — is the IDIOMATIC xUnit reduction
      and is RECOMMENDED; codegen should detect this pair and emit the
      combined form to keep the converted test concise and one-statement-
      per-assertion.
  - construct_key: dart.package_test.expect_isTrue_isFalse
    source_form: "expect(<bool-expr>, isTrue); expect(<bool-expr>, isFalse);"
    target_decision: >-
      `isTrue` maps to xUnit `Assert.True(<bool-expr>)`. `isFalse` maps
      to xUnit `Assert.False(<bool-expr>)`. Concretely
      `expect(x1.isReader, isTrue)` -> `Assert.True(x1.IsReader);` and
      `expect(x3.isReader, isFalse)` -> `Assert.False(x3.IsReader);`.
      Both `isTrue` and `isFalse` are heavily used in this file
      (per-variable `isReader` assertions, `isListCons`, `isNil`
      assertions). Diagnostic nuance from boot_loader.dart.md applies:
      codegen MAY add the optional `userMessage` overload
      (`Assert.True(b, "<msg>")`); this file's predicates are simple
      property accesses so the bare form suffices.
    idiom_id: null
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      Boolean-matcher nuance (explicitly addressed, extends
      boot_loader.dart.md): `isTrue` and `isFalse` are the SIMPLEST
      matchers in `package:test`. They have no composition (`isNot(isTrue)`
      is identical to `isFalse`, never used in this file). xUnit's flat
      `Assert.True`/`Assert.False` mirror this exactly. The Dart matcher
      `isTrue` matches ONLY the boolean `true` (not truthy values — Dart
      is strongly typed); C# is also strongly typed (`bool` is not
      convertible from `int`/`object`) so the mapping is symmetric.
  - construct_key: dart.package_test.expect_isNot_equals
    source_form: "expect(v1.name, isNot(equals(v2.name)));"
    target_decision: >-
      Map to xUnit `Assert.NotEqual(v2.Name, v1.Name)`. The composed
      matcher `isNot(equals(x))` collapses to the dedicated
      `Assert.NotEqual` assertion. Note the same ARGUMENT-ORDER FLIP as
      `Assert.Equal` (expected first, actual second). Used ONCE in this
      file (`expect(v1.name, isNot(equals(v2.name)))` — verifying anonymous
      variables get unique names).
    idiom_id: null
    research_finding_id: rf-dart-expect-isnot-equals-to-xunit-assertnotequal
    nuance: >-
      Matcher-composition nuance (reused from boot_loader.dart.md):
      Dart's matcher package builds matchers compositionally
      (`isNot(equals(x))`); xUnit has flat per-assertion methods
      (`Assert.NotEqual`). The generic `isNot(X)` -> `Assert.False(X as
      bool)` reduction would lose the diagnostic message — instead each
      common composition has a dedicated xUnit assertion. THIS file uses
      `isNot(equals(...))` exactly once; the broader idiom table also
      maps `isNot(contains(...))` -> `Assert.DoesNotContain` (NOT used
      here) and `isNot(isA<T>())` -> `Assert.IsNotType<T>` (NOT used
      here).
  - construct_key: dart.package_test.expect_startsWith
    source_form: "expect(v1.name, startsWith('_#'));"
    target_decision: >-
      Map to xUnit `Assert.StartsWith("_#", v1.Name)`. xUnit's
      `Assert.StartsWith(expectedPrefix, actualString)` is the dedicated
      string-prefix assertion (same argument-order discipline as other
      xUnit asserts: expected first, actual second). Used twice in this
      file (anonymous-variable name-prefix assertions). The Dart
      `startsWith` matcher uses simple substring containment at offset 0,
      identical to xUnit `Assert.StartsWith`.
    idiom_id: null
    research_finding_id: rf-dart-expect-startswith-to-xunit-assertstartswith
    nuance: >-
      String-matcher nuance (explicitly addressed): Dart's `startsWith(s)`
      matcher and `String.startsWith(s)` method are distinct — this file
      uses the MATCHER form inside `expect`. xUnit `Assert.StartsWith`
      has an overload taking a `StringComparison` argument for
      culture/case-insensitive comparison; the default is ordinal
      case-sensitive, matching Dart's default. Diagnostic nuance:
      `Assert.StartsWith` produces a clear "Expected prefix" message;
      using `Assert.True(v1.Name.StartsWith("_#"))` would lose this
      diagnostic and is REJECTED. Substring-search counterpart
      `expect(s, contains(x))` -> `Assert.Contains(x, s)` is NOT used
      in this file (recorded once at the idiom level).
  - construct_key: dart.package_test.expect_throwsA_isA
    source_form: "expect(() => modedHead(head, decl), throwsA(isA<ArityMismatchError>())); expect(() => producedTerm(atom, decl), throwsA(isA<ArityMismatchError>()));"
    target_decision: >-
      Map to xUnit `Assert.Throws<ArityMismatchError>(() =>
      ModedHead(head, decl));`. Unlike boot_loader.dart.md's
      `throwsA(isA<T>().having((e) => e.message, 'message',
      contains(...)))`, THIS file uses the SIMPLE form — only the
      exception type is asserted, no message-substring follow-on. The
      target reduces to a single `Assert.Throws<T>` call without the
      Throws-then-Assert.Contains pattern. Dart lambda `() =>
      modedHead(head, decl)` -> C# `() => ModedHead(head, decl)`
      (identical syntax).
    idiom_id: null
    research_finding_id: rf-dart-throwsa-isa-to-xunit-throws-simple
    nuance: >-
      Exception-matcher nuance (explicitly addressed, narrower than
      boot_loader.dart.md): `throwsA(isA<T>())` asserts EXACT type match
      in xUnit terms — `Assert.Throws<T>` fails if a SUBTYPE of `T` is
      thrown (use `Assert.ThrowsAny<T>` for subtype-tolerant). Dart
      `isA<T>` ALSO accepts subtypes, so strictly faithful is
      `Assert.ThrowsAny<T>`. However `ArityMismatchError` is a leaf
      exception in this codebase (no documented subclasses), so
      `Assert.Throws<T>` is observably equivalent here. Codegen rule:
      emit `Assert.Throws<T>` UNLESS the target exception has known
      subtypes. NEW nuance: this file's two uses are the SIMPLE form
      (no `having` clause), so codegen MUST detect the absence of
      `having` and emit a one-line `Assert.Throws<T>(() => SUT())` with
      NO follow-on assertion (do NOT introduce a `var ex =` capture if
      it is never read). Error-type-naming nuance: Dart
      `ArityMismatchError` is exposed from `type_ast.dart` (one of the
      five imports) — the C# type lives in the converted
      `<RootNs>.Analysis.TypeChecker` namespace and is referenced
      unqualified after the namespace `using`.
  - construct_key: dart.collection.empty_map_literal
    source_form: "TypeEnvironment({}, {});"
    target_decision: >-
      Dart `{}` (empty collection literal) is context-dependent: an
      empty MAP `<K, V>{}` when targeting a `Map<K, V>` parameter,
      otherwise an empty SET `<T>{}`. The `TypeEnvironment({}, {})`
      constructor in this file takes two `Map<String, TypeDef>` (or
      similar) arguments per the imported `type_ast.dart` signature, so
      `{}` here is an empty MAP, NOT an empty set. Map to C# `new
      Dictionary<string, TypeDef>()` per argument (or target-typed
      `new()` if the constructor parameter is typed `Dictionary<K, V>`).
      Concretely:
      `new TypeEnvironment(new Dictionary<string, TypeDef>(), new
      Dictionary<string, TypeDef>())` OR with target-typed new:
      `new TypeEnvironment(new(), new())`.
    idiom_id: null
    research_finding_id: rf-dart-empty-map-literal-to-csharp-dictionary
    nuance: >-
      Collection-literal nuance (explicitly addressed): Dart's `{}`
      depends on TARGET TYPE — codegen MUST resolve the target type
      from the constructor signature (recorded when `type_ast.dart` is
      converted) and emit the matching C# collection. The two arguments
      passed to `TypeEnvironment` in this file are documented in the
      source as maps (type-name-keyed lookups). C# 12 collection
      expressions (`[]`) MAY also be used in target-typed positions
      (https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions)
      but C# 12 collection expressions DO NOT yet support
      `Dictionary<K,V>` as a target type (only `IEnumerable`-like and
      arrays as of C# 12), so the explicit
      `new Dictionary<string, TypeDef>()` form is mandatory here.
  - construct_key: dart.collection.list_literal_in_constructor
    source_form: >-
      "ProcDecl('merge', [TypeRef('Stream', 0, 0, isInput: true),
      TypeRef('Stream', 0, 0, isInput: true), TypeRef('Stream', 0, 0,
      isInput: false)], 0, 0);"
    target_decision: >-
      Dart `[a, b, c]` (list literal) used as a constructor argument
      maps to C# in two equivalent ways depending on target version:
      (i) C# 12+: collection expression `[a, b, c]` where the target
      parameter type is `IList<T>` / `IReadOnlyList<T>` / `T[]` /
      `List<T>` — the compiler builds the right collection;
      (ii) Pre-C# 12: explicit `new[] { a, b, c }` (array) or `new
      List<T> { a, b, c }` (list). For maximum target-version
      portability the spec recommends (ii) with `new List<T> { a, b, c }`
      matching the Dart `List<T>` shape. Used in every `ProcDecl(...)`
      and `TypeDef(...)` constructor in this file (the second argument
      is a `List<TypeRef>` / `List<TypeAlt>`).
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-or-collection-expression
    nuance: >-
      Collection-literal nuance (extends empty_map_literal above):
      Dart `[]` is ALWAYS an empty list (no map ambiguity — Dart lists
      use `[`, maps use `{`). Dart list elements are evaluated left-to-
      right, identical to C# list initialisers. The Dart list MAY
      contain expressions (`TypeRef(...)`); C# list initialisers also
      accept expressions per element. Where the constructor's parameter
      type is `IReadOnlyList<T>` (the recommended Dart `List<T>` ->
      C# mapping in helper closures, see local_helper_closures), passing
      a `List<T>` literal is implicit (since `List<T>` implements
      `IReadOnlyList<T>`). C# 12 collection expression `[a, b, c]`
      works when target is `IReadOnlyList<T>` / `IList<T>` / `T[]` /
      `Span<T>` etc., per
      https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions
      — recommended IF the project targets C# 12+.
  - construct_key: dart.string.raw_string_literal
    source_form: "struct(r'\\', [writer('A'), reader('B')])"
    target_decision: >-
      Dart `r'\\'` is a raw string literal (NO escape processing) that
      contains a single backslash character. Map to C# verbatim string
      `@"\\"` (which would be a TWO-character string in a normal C#
      string but is a SINGLE backslash in verbatim form? — CORRECTION:
      `@"\\"` is the verbatim form for a single backslash because `@`
      strings DO NOT process `\\` as escape; the literal contains
      exactly the characters between the delimiters). Alternative: C# 11
      raw string literal `"""\\""" `(no escape processing). The simplest
      mapping is the standard C# string `"\\"` (one backslash escape) —
      both Dart raw and C# escaped forms produce the same one-character
      string. Recommended: C# standard `"\\"` for maximum readability
      in single-character cases like this one (the DiffList functor
      symbol `\`); use verbatim `@"..."` for multi-character raw
      content; use C# 11 raw `"""..."""` for content containing both
      `\` and `"`.
    idiom_id: null
    research_finding_id: rf-dart-raw-string-to-csharp-verbatim-or-escaped
    nuance: >-
      String-literal nuance (extends boot_loader.dart.md's triple-quoted
      idiom): Dart prefixes a string literal with `r` to suppress
      escape processing (`r'\n'` is two characters: `\` and `n`). C#
      has THREE mechanisms with similar effect: (i) verbatim `@"..."`
      (no `\` escape but `""` for embedded quote), (ii) C# 11 raw
      `"""..."""` (no escape processing, no special quote handling),
      (iii) normal string `"..."` with `\\` for backslash. THIS file
      uses Dart raw strings only for the single-character DiffList
      functor `\`, so any of the three C# forms works. Codegen rule:
      prefer normal escaped `"\\"` for short raw content (≤3 chars);
      prefer verbatim `@"..."` for paths and longer raw content;
      prefer C# 11 raw `"""..."""` only when the content contains
      both `\` and `"`. Documented at
      https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/string
  - construct_key: dart.test.final_local_variable
    source_form: "final decl = ProcDecl(...); final head = goal(...); final result = modedHead(head, decl); final compound = result as ModedCompound; final typeEnv = TypeEnvironment({}, {});"
    target_decision: >-
      Dart `final T x = expr;` (single-assignment local) maps to C#
      `var x = expr;` (implicit-typed local, single-assignment by
      convention) OR `T x = expr;` (explicit typed). The Dart `final`
      keyword forbids REASSIGNMENT (does not prevent mutation of the
      referenced object) — C# has no direct equivalent at the local
      level (`readonly` applies to fields, not locals). Codegen MUST
      emit `var x = expr;` and rely on the test-method body's small
      scope to make non-reassignment obvious; if explicit immutability
      is desired, use the explicit type `T x = expr;` AND a code-review
      convention (no compiler enforcement at the local scope). Either
      form is correct.
    idiom_id: null
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Single-assignment-local nuance (explicitly addressed): Dart
      `final` at the LOCAL level enforces single assignment at
      compile time; C# `var` does NOT (the variable may be reassigned).
      In test method bodies the convention is to NEVER reassign locals,
      so the loss of compile-time enforcement is acceptable. C# has no
      `let`-style construct at the local level
      (https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/readonly
      documents `readonly` for fields only; there is no proposal for
      `readonly` locals in stable C# as of C# 12). Type-inference
      nuance: `var x = expr;` infers the COMPILE-TIME type of `expr`,
      matching Dart `final`'s behaviour. There is no `dynamic` ambiguity
      because every right-hand side here has a known static type.
  - construct_key: dart.test.list_in_constructor_typedef_alts
    source_form: >-
      "TypeDef('Stream', [ ListNilAlt(0, 0), ListConsAlt(
      PrimitiveModeAlt(false, 0, 0), TypeRef('Stream', 0, 0,
      isInput: false), 0, 0), ], 0, 0)"
    target_decision: >-
      Same rule as `list_literal_in_constructor` above (Dart `[...]`
      list literal as a constructor argument). Specific to THIS file:
      `TypeDef` accepts a list of `TypeAlt` (the alternatives — the
      file uses `StructAlt`, `ListNilAlt`, `ListConsAlt`,
      `PrimitiveModeAlt`, `DiffListAlt` — all conform to a common
      base/interface in `type_ast.dart`). Map to C#
      `new List<TypeAlt> { new ListNilAlt(0, 0),
      new ListConsAlt(new PrimitiveModeAlt(false, 0, 0),
      new TypeRef("Stream", 0, 0, isInput: false), 0, 0) }`.
      Heterogeneous list element types are accepted as long as they
      share a common base/interface (`TypeAlt`) — same in C# (the
      element type is the common base).
    idiom_id: null
    research_finding_id: rf-dart-list-literal-to-csharp-list-or-collection-expression
    nuance: >-
      Heterogeneous-list nuance (explicitly addressed): Dart infers
      the element type as the LEAST UPPER BOUND of the element static
      types. The Dart compiler synthesises `List<TypeAlt>` from the
      mixed `ListNilAlt`/`ListConsAlt` elements automatically. C#
      target-typed list initialisers do the same — element types are
      coerced upward to the target's element type (`TypeAlt`).
      Variance nuance: C# `List<TypeAlt>` is invariant in `T`, so
      passing a `List<ListNilAlt>` where `List<TypeAlt>` is expected
      would FAIL — but constructing the list inline with mixed elements
      directly as `new List<TypeAlt> { ... }` works because the
      compiler infers `TypeAlt` from the target slot.
  - construct_key: dart.test.bool_named_arg_default
    source_form: "TypeRef('T', 0, 0)  // no isInput: argument"
    target_decision: >-
      Dart named arguments with defaults: omitting the named argument
      uses the parameter's declared default. C# named arguments behave
      identically — if `isInput` has a default in the converted
      `TypeRef` constructor (e.g. `isInput: false`), omitting it at
      the call site is permitted. THIS file omits `isInput` ONLY in
      the arity-mismatch test cases (`TypeRef('T', 0, 0)`) — implying
      the default applies. Codegen MUST preserve the default value
      from the converted `TypeRef` constructor signature; if the C#
      signature mandates `isInput`, codegen MUST add the default
      explicitly.
    idiom_id: null
    research_finding_id: rf-dart-named-arguments-to-csharp-named-arguments
    nuance: >-
      Default-value nuance (explicitly addressed): Dart constructors
      declare defaults with `[name = value]` (positional) or
      `{name = value}` (named); C# uses `name = value` in the
      signature, with the SAME omit-at-call-site semantics. The
      observability is identical. This file's two arity-mismatch tests
      do not depend on the default value (they only need any
      `TypeRef` with the right arity-1 procedure shape), so the
      default's specific value is immaterial for the converted
      behaviour. Forward-compat: if `type_ast.dart`'s `TypeRef`
      changes its `isInput` default, both Dart and C# call sites
      change in lockstep.
conversion_units:
  - cu-1: file-scope using directives — `using Xunit;` + `using System;` + 4 plain SUT namespace imports + 1 namespace alias for `ast` (from glp_runtime/compiler/ast.dart)
  - cu-2: namespace declaration mirroring the test/analysis/type_checker path
  - cu-3: top-level test class `ModedHeadTests` (from outer group label "modedHead") with `private static` helper methods Goal/Writer/Reader/Nil/Cons/ConstTerm and 8 `[Fact]` methods grouped by `[Trait("Group", "<inner-label>")]` across the 4 inner groups (basic construction / variable replacement / anonymous variables / error handling)
  - cu-4: top-level test class `ProducedTermTests` (from outer group label "producedTerm") with `private static` helper methods Goal/Writer/Reader and 2 `[Fact]` methods (body atom has produce mode at root; arity mismatch throws)
  - cu-5: top-level test class `ExplicitDualTypeDefinitionsTests` (from outer group label "explicit dual type definitions") with `private static` helper methods Goal/Writer/Reader/Struct and 2 `[Fact]` methods (Channel with explicit dual; DiffList with explicit dual)
  - cu-6: per `[Fact]` method — preserve original Dart label as `[Fact(DisplayName = "<original label>")]`; PascalCase the method name with the inner-group prefix where the enclosing class has multiple inner groups (ModedHeadTests only)
  - cu-7: per assertion — apply the dart.package_test.expect_* mappings recorded above; fold each `expect(r, isA<T>()); var x = r as T;` pair into `var x = Assert.IsType<T>(r);` (see expect_isA_type_check and downcast_as_expression)
  - cu-8: per constructor call — emit explicit `new T(...)` (or target-typed `new(...)` where target type is inferable) for every Dart implicit-new in the file (see constructor_call_implicit_new)
  - cu-9: per Dart `r'\\'` raw string — emit C# `"\\"` (standard escaped) for single-backslash content; the broader idiom is recorded for verbatim/raw forms
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

This is the THIRD `package:test` file specced; the first
(`test/multiagent/mad_error_handling_test.dart.md`) pinned xUnit project-
wide and the second (`test/multiagent/boot_loader_test.dart.md`) reused
that pin. Maintaining the pin satisfies SC-007 (consistency via recorded
idiom, not re-derivation). Authoritative basis: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp model; Dart `package:test`
README on `pub.dev` (`https://pub.dev/packages/test`) for the `group` /
`setUp` / `expect` / matcher semantics. NUnit and MSTest remain
corroborating alternatives, recorded once at the import-idiom level.

### Three sibling outer groups -> three sibling test classes

Unlike the precedent test files which each had ONE outer group, this
file has THREE sibling outer groups (`modedHead`, `producedTerm`,
`explicit dual type definitions`). Each outer group RE-DECLARES its own
local helper closures (`goal`, `writer`, `reader` — and `producedTerm`'s
group omits `nil`/`cons`/`constTerm`; the third group adds `struct`).
Dart's lexical scoping means each outer-group callback has its own
helper scope; the C# equivalent is THREE sibling classes, each with its
own `private static` helper-method scope. Collapsing into one class with
a shared helper bank would force the third group's `struct` helper to
appear on classes that do not use it (no compilation error but spurious
API surface). Three sibling classes preserve the Dart authoring
boundaries 1:1.

### Inner-group FLATTENing for ModedHeadTests (reused decision)

The outer `modedHead` group contains four inner groups: `basic
construction` (2 tests), `variable replacement` (4 tests), `anonymous
variables` (1 test), `error handling` (1 test) — 8 tests total. The same
FLATTEN-with-`[Trait]` choice as boot_loader.dart.md applies for the
same reason: the file-local helper closures defined in the outer
`modedHead` group are closed over by every inner test, so splitting the
inner groups into separate classes would force duplicating the helpers
or introducing a base class. FLATTEN preserves the helper sharing and
records the grouping via `[Trait("Group", "<label>")]` on each method
(reporter-parity in VS Test Explorer / `dotnet test --logger trx` /
Rider per `https://xunit.net/docs/comparisons#categories`).

### `isA<T>` + `as T` folding into `Assert.IsType<T>`

This file has a recurring pattern: `expect(result, isA<T>())` followed
by `final x = result as T;` (the assertion verifies the type, the cast
extracts the typed value for further inspection). xUnit's
`Assert.IsType<T>(value)` overload RETURNS the typed value, so the
two-statement Dart form folds to one statement:
`var x = Assert.IsType<T>(value);`. Authoritative basis:
`https://xunit.net/docs/comparisons#assertions` ("Type checks"). This
fold is recommended (not mandatory): codegen detects the
isA-immediately-followed-by-cast pair on the same expression and emits
the combined form. Where the file has a STANDALONE cast (no preceding
`isA<T>`, e.g. nested `compound.args[0] as ModedCompound`), emit
`(ModedCompound)compound.Args[0]` (C# checked cast — throws
`InvalidCastException` on mismatch, equivalent to Dart's `TypeError`).

### Argument-order flip on `Assert.Equal` and `Assert.NotEqual`

Reused from boot_loader.dart.md. Dart `expect(actual, equals(expected))`
puts actual first; xUnit `Assert.Equal(expected, actual)` puts expected
first. EVERY `equals(...)` and `isNot(equals(...))` assertion in this
file MUST be flipped at the boundary. Authoritative basis:
`https://xunit.net/docs/comparisons#assertions`. The Dart `equals`
matcher uses `==`; xUnit `Assert.Equal` uses `IEquatable<T>.Equals` /
`Object.Equals` — equivalent for the value-typed comparisons in this
file (`String`, `int`, `Mode` enum).

### `throwsA(isA<T>())` simple form -> `Assert.Throws<T>`

This file uses the SIMPLE `throwsA(isA<T>())` form (no `having(...)`
clause) for both arity-mismatch tests. The mapping reduces to a single
`Assert.Throws<ArityMismatchError>(() => SUT(...));` with no follow-on
`Assert.Contains` — the Throws-then-Assert two-step from
boot_loader.dart.md is NOT applicable here. The exact-vs-subtype
nuance is recorded in the construct (`Assert.Throws<T>` requires exact
type; `Assert.ThrowsAny<T>` is subtype-tolerant); `ArityMismatchError`
has no documented subclasses, so `Assert.Throws<T>` is observably
equivalent. Authoritative basis:
`https://xunit.net/docs/comparisons#exceptions`.

### Constructor-call: implicit-new -> explicit (or target-typed) new

Dart 2+ permits omitting `new` for constructor calls. C# requires
`new` OR uses C# 9+ target-typed `new()`. Authoritative basis:
`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/new-operator#target-typed-new`.
Codegen rule: emit `new T(...)` (explicit) unconditionally OR
`new(...)` (target-typed) where the C# compiler can infer the target.
The file has dozens of these (every AST-node construction and every
type-system AST construction); a consistent codegen choice keeps the
output readable.

### Named arguments: `isInput:` / `typeEnv:` translate unchanged

Both Dart and C# use `name: value` for named arguments with identical
semantics. C# parameter names remain camelCase per
`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#parameter-names`,
so `isInput: true` and `typeEnv: typeEnv` translate UNCHANGED. The
field/property access casing IS PascalCased per the standard C#
convention (`x.IsReader`, `compound.Functor`, etc.); the named-argument
call-site casing is NOT.

### Empty map literal `{}` -> `new Dictionary<K, V>()`

`TypeEnvironment({}, {})` passes two empty Dart maps. C# 12 collection
expressions DO NOT yet support `Dictionary<K, V>` as a target
(`https://learn.microsoft.com/dotnet/csharp/language-reference/operators/collection-expressions`
— supports `IEnumerable`-like and arrays only as of C# 12), so the
explicit `new Dictionary<string, TypeDef>()` form (or target-typed
`new()` against a typed slot) is mandatory. The actual key/value types
are recorded when `type_ast.dart` is converted; THIS spec records only
the call-site shape.

### Local helper closures -> `private static` methods

The Dart group-scoped helper closures (`goal`, `writer`, `reader`,
`nil`, `cons`, `constTerm`, `struct`) capture NOTHING from the
enclosing scope — they are pure functions. C# `private static` methods
are therefore the precise mapping (no instance state, no lambda
field). PascalCasing follows
`https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names#names-of-methods`:
`goal` -> `Goal`, `writer` -> `Writer`, `constTerm` -> `ConstTerm`
(the C# `const` keyword is reserved but `ConstTerm` (PascalCase) is
not a keyword and does not collide).

### Raw string `r'\\'` -> standard escaped `"\\"`

The single occurrence of a Dart raw string in this file is `r'\'`
(one-character backslash, used as the DiffList functor symbol). C# has
three mechanisms (verbatim `@"..."`, raw `"""..."""`, escaped
`"..."`). For one character, the standard escaped form `"\\"` is the
most readable; verbatim and raw forms would be `@"\\"` and `"""\\"""`
respectively (all three produce the same one-character string).
Authoritative basis:
`https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/string`.

### Why no escalations

Every construct in this file resolves to a clear, single-decision C#
shape grounded in official .NET and `package:test` documentation. The
three "soft" decisions (xUnit vs NUnit/MSTest; FLATTEN vs nested
classes for inner groups; sibling classes vs one-class-with-traits for
outer groups) are documented project-wide policy with corroborating
alternatives recorded in their research findings, not unresolved
choices. `Assert.IsType<T>` vs `Assert.IsAssignableFrom<T>` and
`Assert.Throws<T>` vs `Assert.ThrowsAny<T>` are deliberate in-file-
justified choices (no known subtypes for `ModedCompound`/`ModedConstant`/
`ModedVariable`/`ArityMismatchError`), not undecidable points. The
`empty map literal` and `list literal in constructor` mappings depend
on the converted `type_ast.dart` signatures (recorded as a cross-file
dependency, not an escalation: the SUT's parameter types are known
in the Dart source). `escalations: []` is therefore intentional, not
a placeholder.
