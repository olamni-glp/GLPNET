# Conversion Spec — test/module/module_hierarchy_test.dart

> Conversion-spec artifact for test/module/module_hierarchy_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> Integration test for `lib/runtime/module_hierarchy.dart` (see that file's
> convspec for the SUT — `discoverSelfChain`, `assembleTypeScope`). The
> SUT discovers ancestor `self.glp` files starting from a target `.glp`
> file, layering each ancestor's type environment on top of the prelude
> and the target module's own definitions. The tests exercise the
> filesystem walk on a TEMPORARY directory tree (built per-test via
> `Directory.systemTemp.createTemp`), so the test surface combines (a)
> heavy `dart:io` use (file create / read / write, recursive directory
> tree creation, recursive delete via `Directory.delete(recursive: true)`),
> (b) `async`/`await` over `Future<Directory>` and `Future<void>`,
> (c) `try { ... } finally { tempDir.delete(recursive: true) }` cleanup
> idiom (test-scoped resource discipline), (d) per-file setup via top-
> level `setPreludeEnvironmentSource(rootSelfGlp.readAsStringSync())`
> from within `void main()` (a side-effecting registration call that
> runs ONCE per Dart test-file process; xUnit has no per-file hook —
> see the lift below), (e) the `Map<String, String>` argument shape for
> the `createTempHierarchy` helper (path → contents), and (f) the
> `expect(env.hasType('X'), isTrue)` / `expect(env.getType('X'),
> isNotNull)` / `responseDef!.alternatives.length` matcher and
> null-bang patterns reused from prior module/* test specs.
>
> Reused (KB-cached, FR-012) from prior module/* test specs:
>   - rf-dart-package-test-import-to-xunit-using (from
>     module_parser_test.dart.md)
>   - rf-dart-internal-package-import-to-csharp-using (from
>     module_parser_test.dart.md, module_syntax_v2_test.dart.md)
>   - rf-dart-package-test-main-omit-in-xunit (from
>     module_parser_test.dart.md)
>   - rf-dart-package-test-group-to-xunit-class (from
>     module_parser_test.dart.md)
>   - rf-dart-test-callback-to-xunit-method-body (from
>     module_parser_test.dart.md)
>   - rf-dart-final-local-to-csharp-var-local (from
>     module_parser_test.dart.md)
>   - rf-dart-expect-equals-to-xunit-assertequal (from
>     module_parser_test.dart.md)
>   - rf-dart-expect-isNotNull-to-xunit-assert-notnull (from
>     module_parser_test.dart.md)
>   - rf-dart-expect-isEmpty-to-xunit-assert-empty (from
>     module_parser_test.dart.md)
>   - rf-dart-expect-isTrue-to-xunit-assert-true (from
>     module_syntax_v2_test.dart.md)
>   - rf-dart-triple-quoted-string-to-csharp-raw-string (from
>     module_parser_test.dart.md) — applies to embedded `.glp` source
>     literals; this file uses SINGLE-quoted literals (no `'''` here)
>     but the per-file fixture-content idiom is the same
>   - rf-dart-single-quoted-string-to-csharp-double-quoted-string (from
>     module_parser_test.dart.md)
>   - rf-dart-bang-to-csharp-null-forgiving (from
>     module_syntax_v2_test.dart.md) — `responseDef!.alternatives.length`,
>     `fooDef!.alternatives.length`, `proc!.exported`
>   - rf-dart-package-import-to-csharp-using-namespace (from
>     module_syntax_v2_test.dart.md) — for the six SUT imports
>   - rf-dart-dart-io-to-csharp-system-io (from
>     module_hierarchy.dart.md, repl_play_runner.dart.md,
>     runtime.dart.md) — File / Directory subset; this test EXTENDS
>     it with `Directory.systemTemp.createTemp` and recursive delete
>
> FIRST-SEEN here (research-justified, no escalation):
>   - rf-dart-systemTemp-createTemp-to-csharp-path-getTempPath-plus-directory-createDirectory
>     (Dart `Directory.systemTemp.createTemp('prefix')` returns a fresh
>     unique `Directory` under the OS temp root; .NET counterpart
>     composes `Path.GetTempPath` + a unique sub-name)
>   - rf-dart-directory-delete-recursive-to-csharp-directory-delete-recursive
>     (Dart `Directory.delete(recursive: true)` ↔ .NET
>     `Directory.Delete(path, recursive: true)`)
>   - rf-dart-async-test-callback-to-xunit-async-task-method
>     (Dart `test('...', () async { ... })` ↔ xUnit `public async Task
>     <Name>() { ... }` — the `Func<Task>` overload that xUnit awaits)
>   - rf-dart-try-finally-cleanup-to-csharp-try-finally-or-await-using
>     (Dart `try { ... } finally { await tempDir.delete(...) }` ↔ C#
>     `try { ... } finally { Directory.Delete(...) }` — `await using`
>     and `IAsyncDisposable` recorded as the modernised alternative)
>   - rf-dart-map-literal-string-string-to-csharp-dictionary-string-string
>     (Dart `<String, String>{'a/b': 'contents'}` ↔ C#
>     `new Dictionary<string, string> { ["a/b"] = "contents" }`)
>   - rf-dart-file-writeAsString-await-to-csharp-file-writeallText-async
>     (Dart `await file.writeAsString(s)` ↔ .NET `await
>     File.WriteAllTextAsync(path, contents)`)
>   - rf-dart-file-parent-create-recursive-to-csharp-directory-createDirectory
>     (Dart `await file.parent.create(recursive: true)` ↔ .NET
>     `Directory.CreateDirectory(Path.GetDirectoryName(path)!)` —
>     `CreateDirectory` is idempotent + recursive by default)
>   - rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture
>     (the top-of-`main` `if (rootSelfGlp.existsSync()) { ... }` setup
>     block — once-per-file initialisation; in xUnit the lift is an
>     `IClassFixture<T>` or `IAssemblyFixture<T>` — see decision below)

```yaml
schema_version: 1
source_path: test/module/module_hierarchy_test.dart
source_sha256: 21a38c8225f5824cc125308c58c06dd808d7db35dfcb085086d0a265fba780aa
target_code_unit: test/module/ModuleHierarchyTest.cs
constructs:
  - construct_key: dart.import_directive.dart_io_to_csharp_using_system_io
    source_form: "import 'dart:io';"
    target_decision: >-
      Emit `using System.IO;` at the top of the target file (covers
      `File`, `Directory`, `Path`). Cached idiom — reused verbatim
      from lib/runtime/module_hierarchy.dart.md
      (rf-dart-dart-io-to-csharp-system-io); this test exercises an
      additional dart:io surface (`Directory.systemTemp.createTemp`,
      `Directory.delete(recursive: true)`, `File.writeAsString`,
      `file.parent.create(recursive: true)`) that maps under the same
      finding (extended below at the per-call construct level).
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Cached idiom (precedent: lib/runtime/module_hierarchy.dart.md,
      repl_play_runner.dart.md, runtime.dart.md). Sync-vs-async
      nuance (explicitly addressed): this test mixes SYNC
      (`existsSync`, `readAsStringSync` on `rootSelfGlp` in the
      top-of-main setup) and ASYNC (`createTemp`, `writeAsString`,
      `parent.create`, `delete`) calls. The C# render preserves
      each call's sync/async character per-call: the top-of-main
      setup uses `File.Exists` + `File.ReadAllText` (sync), the
      per-test temp-tree creation uses
      `await File.WriteAllTextAsync(...)` + `Directory.CreateDirectory`
      (the BCL `CreateDirectory` is sync; there is no async variant
      pre .NET 6, and the operation is fast enough that the sync
      call is the idiom). The `await tempDir.delete(recursive: true)`
      similarly maps to a synchronous `Directory.Delete(path, true)`
      because .NET has no `Directory.DeleteAsync` — the C# method
      is sync; the C# test method's overall shape is still
      `async Task` because the OTHER awaits (`WriteAllTextAsync`,
      and the SUT calls if they go async) keep the method in the
      async dimension. Where the .NET BCL exposes ONLY a sync
      variant, the C# render uses the sync call in a method that
      may otherwise be async — perfectly idiomatic.

  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop and replace with `using Xunit;` per the batch-wide xUnit
      idiom (cached, reused verbatim from the precedent module/*
      test convspecs — rf-dart-package-test-import-to-xunit-using).
      No re-research; project policy is xUnit and is recorded once
      at the import-idiom level.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md). Async-test-method nuance
      (explicitly addressed for this file): this file's tests are
      `async`-decorated (`test('...', () async { ... })`); xUnit
      runs `async Task`-returning test methods via the
      `Func<Task>`-aware test invoker (xUnit `[Fact]` discovers and
      awaits `async Task` methods identically to `void` methods —
      documented at xunit.net "Running async tests"). No additional
      `using` directive is needed for async support; `System.Threading
      .Tasks` is required (`Task`) and is emitted at file-scope by
      the file-imports unit below.

  - construct_key: dart.package_under_test.import_directive_sut_imports
    source_form: |-
      "import 'package:glp_runtime/compiler/lexer.dart';
       import 'package:glp_runtime/compiler/parser.dart';
       import 'package:glp_runtime/compiler/ast.dart';
       import 'package:glp_runtime/analysis/type_checker/type_ast.dart';
       import 'package:glp_runtime/analysis/type_checker/type_environment_builder.dart';
       import 'package:glp_runtime/runtime/module_hierarchy.dart';"
    target_decision: >-
      Each `package:glp_runtime/<sub>/<file>.dart` Dart import maps
      to the C# `using` directive for the namespace produced by
      converting that file. Per the langpair convention recorded in
      module_syntax_v2_test.dart.md
      (rf-dart-package-import-to-csharp-using-namespace),
      `glp_runtime` ⇒ `Glp` (root namespace; the
      module_hierarchy.dart.md convspec uses `Glp.Runtime` —
      consistent with the lib-side decision). Directory →
      namespace mapping: `lib/compiler/` ⇒ `Glp.Compiler`,
      `lib/analysis/type_checker/` ⇒ `Glp.Analysis.TypeChecker`,
      `lib/runtime/` ⇒ `Glp.Runtime`. Net result: THREE distinct
      `using` lines (six Dart imports collapse to three because
      multiple Dart files share each C# namespace) — `using
      Glp.Compiler;` (covers lexer + parser + ast),
      `using Glp.Analysis.TypeChecker;` (covers type_ast +
      type_environment_builder), `using Glp.Runtime;` (covers
      module_hierarchy — the SUT). No `as`-prefix on any of these
      imports (module_hierarchy.dart's `as ast;` is internal to
      the SUT, not propagated to test callers); plain `using`
      directives suffice. Codegen reads ast.dart.md /
      type_environment_builder.dart.md /
      module_hierarchy.dart.md for the exact namespace names and
      type identities. The test uses the following symbols from
      these namespaces: `Lexer`, `Parser` (Glp.Compiler — used
      via the parsed-out `parseModule` helper — see below);
      `Module` (Glp.Compiler — return of `Parser.parseModule()`);
      `TypeDef`, `TypeEnvironment` (Glp.Analysis.TypeChecker —
      `env.hasType`, `env.getType`, `env.hasProcedure`,
      `env.getProcedure`, and the `TypeDef.alternatives` accessor);
      `discoverSelfChain`, `assembleTypeScope`,
      `setPreludeEnvironmentSource` (Glp.Runtime — the file-level
      functions converted by module_hierarchy.dart.md to
      `ModuleHierarchy.DiscoverSelfChain` / `.AssembleTypeScope` /
      `.SetPreludeEnvironmentSource` static methods).
    idiom_id: rf-dart-package-import-to-csharp-using-namespace
    research_finding_id: rf-dart-package-import-to-csharp-using-namespace
    nuance: >-
      Cached idiom (precedent: module_syntax_v2_test.dart.md,
      module_parser_test.dart.md). Granularity mismatch nuance
      (cached, re-emphasised): Dart imports are file-grained, C#
      `using` is namespace-grained — six Dart imports collapse to
      three C# `using`s here (one per namespace). Cross-file
      consistency nuance: the test references identifiers
      (`discoverSelfChain`, `assembleTypeScope`,
      `setPreludeEnvironmentSource`) that are file-level FREE
      functions in Dart; per module_hierarchy.dart.md they become
      `public static` methods on `public static class
      ModuleHierarchy` in `Glp.Runtime`. C# does NOT have file-level
      free functions, so the test call sites become
      `ModuleHierarchy.DiscoverSelfChain(...)` /
      `ModuleHierarchy.AssembleTypeScope(...)` /
      `ModuleHierarchy.SetPreludeEnvironmentSource(...)` — see
      individual call-site constructs below.

  - construct_key: dart.package_test.main_entrypoint_with_top_of_file_setup
    source_form: |-
      "void main() {
         final rootSelfGlp = File('../programs/self.glp');
         if (rootSelfGlp.existsSync()) {
           setPreludeEnvironmentSource(rootSelfGlp.readAsStringSync());
         }
         Module parseModule(String source) { ... }
         Future<Directory> createTempHierarchy(Map<String, String> files) async { ... }
         group(...); group(...); group(...); group(...); group(...); group(...); group(...);
       }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint
      and is ELIMINATED entirely under the cached idiom
      (rf-dart-package-test-main-omit-in-xunit). However, THIS
      file's `main` body contains TWO load-bearing elements that
      do NOT exist in the prior module/* test files:
        (1) Top-of-file conditional setup that calls
            `setPreludeEnvironmentSource(rootSelfGlp.readAsStringSync())`
            — a once-per-test-file-process side-effecting
            registration that primes the prelude environment that
            the SUT will read during every test.
        (2) TWO local helper closures: synchronous `Module
            parseModule(String source) { ... }` and asynchronous
            `Future<Directory> createTempHierarchy(Map<String,
            String> files) async { ... }`.
      Decision for (1): lift the conditional setup into an
      `IClassFixture<PreludeFixture>` (xUnit's documented
      shared-context mechanism — xunit.net "Shared Context between
      Tests"). The fixture's constructor performs the
      `File.Exists` check + `File.ReadAllText` + the
      `ModuleHierarchy.SetPreludeEnvironmentSource` call exactly
      once for the test class lifetime; each `[Fact]` method
      receives the fixture instance via the test class's
      constructor parameter (xUnit dependency-injects fixtures).
      BECAUSE the seven groups in this file each become their own
      test class (under the cached group-to-class idiom — see the
      group-block construct below), the fixture is wired by the
      seven test classes ALL declaring `: IClassFixture<PreludeFixture>`
      — xUnit shares one `PreludeFixture` instance across every
      class that declares the dependency, so the side-effecting
      `SetPreludeEnvironmentSource` runs once total per test-run
      (NOT once per class). Codegen MAY alternatively use an
      `[CollectionDefinition]` + `[Collection]` attribute pair if
      cross-class fixture sharing is preferred over the
      per-class-declaration pattern; both produce the same
      once-per-run lifecycle. Decision for (2): lift each local
      closure into a `private` or `internal static` helper method
      on the same test class (synchronous `parseModule` → `private
      Module ParseModule(string source)`; asynchronous
      `createTempHierarchy` → `private async Task<DirectoryInfo>
      CreateTempHierarchyAsync(Dictionary<string, string> files)`).
      Both helpers are stateless (no captured `late` field, no
      shared instance state) and could equally be `static`; the
      idiom default is INSTANCE METHODS to mirror the per-test
      fresh-instance lifecycle of the rest of xUnit. Because the
      seven groups become seven independent test classes, EACH
      class needs its own copy of the helpers — codegen MUST
      either duplicate the helpers per class (simple) OR lift them
      to a single non-test `internal static class
      ModuleHierarchyTestHelpers` in the same file (DRY); spec
      default = DRY-shared helper class to avoid seven-fold
      duplication and to centralise the async cleanup discipline.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture
    nuance: >-
      Cached idiom for the main-omit (precedent:
      module_parser_test.dart.md, module_syntax_v2_test.dart.md);
      EXTENDED here with the once-per-file SIDE-EFFECTING setup
      lift. Lifecycle nuance (LOAD-BEARING, explicitly addressed):
      Dart `void main()` runs ONCE per test-file process — both
      the prelude bootstrap and the helper-closure DEFINITIONS
      execute exactly once before any `test` callback runs. xUnit
      has no per-file hook; the closest semantic match is
      `IClassFixture<T>` (per-class, but shared across classes
      that opt in via `IClassFixture<T>` declaration —
      xunit.net "Shared Context between Tests" + Microsoft Learn
      "Shared context in xUnit"). xUnit creates the fixture
      instance EXACTLY ONCE for the test-class lifetime AND shares
      it across multiple classes that declare the same
      `IClassFixture<T>` (provided the class-fixture is
      stateless and idempotent — which `PreludeFixture` is). The
      ASSEMBLY-WIDE alternative (`[CollectionDefinition]`) is
      stronger ("once per assembly") but introduces a
      `[Collection("name")]` attribute on every class — heavier
      surface for the same effect. Side-effecting-call nuance
      (LOAD-BEARING): `setPreludeEnvironmentSource(...)` mutates
      module-level global state (`Glp.Runtime.ModuleHierarchy`'s
      prelude template per module_hierarchy.dart.md's lib-side
      decisions); the fixture must run the call BEFORE any
      `[Fact]` is invoked. xUnit guarantees fixture construction
      precedes test-class construction precedes test invocation —
      ordering preserved. Conditional nuance: the `if
      (rootSelfGlp.existsSync())` guard ensures the test does NOT
      throw when running from a checkout where `../programs/self.glp`
      is absent — preserved in the fixture via `if
      (File.Exists(path))`. Path-resolution nuance: `'../programs/
      self.glp'` is RELATIVE to the test process CWD; in xUnit the
      process CWD is the test assembly's output directory (e.g.
      `bin/Debug/net8.0/`), so the relative path is NOT identical
      to Dart's `dart test` CWD (which is the package root). The
      C# fixture MUST resolve the path via
      `Path.Combine(AppContext.BaseDirectory, "..", "..", "..",
      "..", "programs", "self.glp")` OR equivalent (project-level
      `CopyToOutputDirectory` of `programs/self.glp` would be the
      cleaner alternative — flagged as the recommended langpair
      MSBuild integration). Spec default = resolve via
      `AppContext.BaseDirectory`-relative `Path.Combine` so no
      MSBuild discipline is forced on every consumer. Closure-lift
      nuance: Dart local functions inside `main()` have
      function-scope; their lifted C# instance/static methods have
      class scope. The two helpers (`parseModule`,
      `createTempHierarchy`) capture NOTHING from `main()` scope —
      free of any `late` field — so the lift is lossless.

  - construct_key: dart.package_test.group_block_seven_siblings
    source_form: |-
      "group('Phase 2 - 2a: self.glp chain discovery', () { test(...); test(...); test(...); test(...); });
       group('Phase 2 - 2b: type scope assembly from ancestor chain', () { test(...); test(...); });
       group('Phase 2 - 2c: shadowing', () { test(...); test(...); });
       group('Phase 2 - 2d: sibling isolation', () { test(...); });
       group('Phase 2 - 2e: type-only self.glp', () { test(...); });
       group('Phase 2 - 2f: prelude as root ancestor', () { test(...); });
       group('Phase 2 - 2g: procedure declarations from ancestor self.glp', () { test(...); test(...); });"
    target_decision: >-
      Seven SIBLING (NOT nested) top-level `group(...)` calls.
      Apply the cached per-group-to-class idiom
      (rf-dart-package-test-group-to-xunit-class — precedent
      module_parser_test.dart.md): each group becomes its own
      PascalCase xUnit test class in the same `.cs` file. Names
      derived from labels with non-identifier characters stripped
      / camel-joined and the `Phase 2 - <letter>:` prefix collapsed:
        - `Phase2aSelfGlpChainDiscoveryTests` (4 `[Fact]` methods)
        - `Phase2bTypeScopeAssemblyFromAncestorChainTests` (2 facts)
        - `Phase2cShadowingTests` (2 facts)
        - `Phase2dSiblingIsolationTests` (1 fact)
        - `Phase2eTypeOnlySelfGlpTests` (1 fact)
        - `Phase2fPreludeAsRootAncestorTests` (1 fact)
        - `Phase2gProcedureDeclarationsFromAncestorSelfGlpTests`
          (2 facts)
      The ORIGINAL group + test labels MUST be preserved verbatim
      via `[Fact(DisplayName = "<original test label>")]`
      decoration on every method, so the reporter output keeps the
      Dart phrasing. Each class declares
      `: IClassFixture<PreludeFixture>` (see main-entrypoint
      construct above) so the once-per-run prelude bootstrap fires
      regardless of which class runs first. No outer group / no
      `setUp` / no shared `late` field exists in this file, so each
      class is INDEPENDENT — no shared base class, no
      `IClassFixture<TempTreeFixture>` (every test creates and
      destroys its own temp tree inline). The decision NOT to
      flatten into a single class (cf. boot_loader_test.dart.md's
      one-class flatten under a shared `late` field) matches
      module_parser_test.dart.md's reasoning for its six sibling
      groups: with no per-group shared state, per-group classes
      produce cleaner VS Test Explorer grouping for the 4 + 2 + 2
      + 1 + 1 + 1 + 2 = 13 total `[Fact]` distribution.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      mad_error_handling_test.dart.md, boot_loader_test.dart.md).
      Topology nuance EXPLICITLY addressed: seven SIBLINGS, not
      nested — same shape as module_parser_test.dart.md's six
      siblings, so the per-class-per-group decision carries over
      verbatim. Name-mangling nuance: each label contains
      `'Phase 2 - 2<letter>: <description>'`; the colon, hyphens
      and spaces MUST be stripped because none are C# identifier
      characters. Spec default keeps the `Phase2<letter>` prefix
      (canonical glp-module-system-spec.md section identifiers —
      load-bearing for grep-based navigation of the test suite).
      Fixture-injection nuance: `IClassFixture<PreludeFixture>`
      requires the class to declare a constructor that takes the
      fixture parameter (`public Phase2aSelfGlpChainDiscoveryTests
      (PreludeFixture fixture) { _fixture = fixture; }`) per
      xunit.net docs; the parameter MUST be retained even if
      unused inside the test bodies, otherwise the fixture is not
      activated by xUnit's reflection. The fixture's role here is
      side-effecting (prime the prelude environment) — the test
      bodies do NOT reference the fixture instance at all, but
      the constructor parameter MUST exist to wire xUnit's
      fixture-discovery.

  - construct_key: dart.package_test.test_call_async_callback
    source_form: "test('<label>', () async { /* await ... */ ; expect(...); });"
    target_decision: >-
      Every `test(label, () async { ... })` in this file (all 13
      tests) becomes `[Fact(DisplayName = "<original label>")]
      public async Task <PascalCasedName>() { ... }`. xUnit
      natively discovers and AWAITS `async Task`-returning test
      methods identically to `void` methods (documented at
      xunit.net "Async tests"); no additional attribute or
      decorator needed. Method names PascalCased from labels with
      non-identifier characters stripped, e.g.
      `'discovers self.glp chain from root to target directory'`
      → `DiscoversSelfGlpChainFromRootToTargetDirectory`. Method
      body translates statement-for-statement: `await
      createTempHierarchy({...})` → `await
      CreateTempHierarchyAsync(new Dictionary<string, string> {
      ... });` (returns `DirectoryInfo`); `try {...} finally
      {await tempDir.delete(recursive: true);}` → `try {...}
      finally { tempDir.Delete(true); }` (sync delete inside an
      async method — see `dart.directory.delete_recursive_await`
      below); `discoverSelfChain(...)` →
      `ModuleHierarchy.DiscoverSelfChain(...)`. The closure-body
      lift includes every `final` local (mapped to `var` per the
      cached final-local idiom) and every `expect` call (mapped
      to the routing table — see the matcher rows below).
    idiom_id: null
    research_finding_id: rf-dart-async-test-callback-to-xunit-async-task-method
    nuance: >-
      FIRST-SEEN idiom row (defines a new active KB entry). The
      precedent `rf-dart-test-callback-to-xunit-method-body`
      explicitly addresses SYNCHRONOUS Dart test callbacks → `void`
      C# `[Fact]` methods (module_parser_test.dart.md). This row
      extends that idiom to the ASYNC case: Dart `() async {...}`
      → C# `async Task <Name>() {...}`. Authoritative bases:
      xunit.net "Async tests"
      (https://xunit.net/docs/getting-started/v3/getting-started)
      — "xUnit.net fully supports async tests when the test method
      returns a Task (or async-aware return type)."; Microsoft
      Learn "Async Programming Model"
      (https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/async/);
      Dart language tour "Asynchrony support"
      (https://dart.dev/language/async). Return-type nuance
      (explicitly addressed): Dart `() async {...}` closure has
      RETURN TYPE `Future<void>` (implicit); C# `async Task` is
      the precise twin (Task = Future<void>; Task<T> = Future<T>).
      xUnit also accepts `async void` test methods historically
      but the documented best practice is `async Task` because
      `async void` swallows exceptions — `async Task` is the
      ONLY correct shape. Cancellation nuance: Dart tests have
      no cancellation token argument; xUnit 3+ supports a
      `CancellationToken` parameter on `[Fact]` methods but it
      is OPT-IN — spec default = no cancellation token (parity
      with Dart). `ConfigureAwait` nuance: xUnit's test runner is
      single-threaded per test by default; `ConfigureAwait(false)`
      is NOT required inside test bodies (and would be a
      performance no-op anyway). Closure-capture nuance: none of
      the async test closures in this file capture `late` fields
      or shared state; each test creates and consumes its own
      temp directory inline — instance-level state is empty, so
      the test class needs only the fixture constructor parameter.

  - construct_key: dart.local.final_var_declaration
    source_form: "final tempDir = await createTempHierarchy({...}); final chain = discoverSelfChain(...); final moduleSource = await File('...').readAsString(); final module = parseModule(moduleSource); final env = assembleTypeScope(chain: chain, module: module); final responseDef = env.getType('Response'); final fooDef = env.getType('Foo'); final proc = env.getProcedure('shared_proc', 2);"
    target_decision: >-
      Every `final <name> = <expr>;` local maps to `var <name> =
      <expr>;` per the cached idiom
      (rf-dart-final-local-to-csharp-var-local — precedent
      module_parser_test.dart.md). All locals in this file are
      single-assignment in practice; none is reassigned. The
      cached idiom note about Dart `new`-keyword optionality
      applies: Dart constructor-calls without `new` in this file
      are limited to `File('...')` and `Directory(...)` (literal
      ctor calls) — C# requires the `new` keyword on each: `new
      DirectoryInfo(...)` / via the BCL static factory methods.
      However, this file uses these mostly via the helper
      methods (`createTempHierarchy`) and the SUT (`discoverSelfChain`,
      `assembleTypeScope`), so the explicit constructor-call sites
      are few.
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md). Single-assignment nuance
      (cached, explicitly addressed): Dart `final` enforces
      no-reassignment at compile time; C# `var` does not — but no
      local in this file is reassigned, so the distinction is
      invisible. `tempDir` typing nuance: Dart's
      `await createTempHierarchy({...})` returns
      `Future<Directory>` resolved to `Directory`; the C# render
      returns `Task<DirectoryInfo>` resolved to `DirectoryInfo`
      (the BCL counterpart of Dart's `Directory`). `chain` typing
      nuance: Dart `discoverSelfChain` returns `List<String>`; per
      module_hierarchy.dart.md the C# render returns
      `IReadOnlyList<string>` — the `var` keyword infers
      identically on both sides.

  - construct_key: dart.map_literal_string_string_to_csharp_dictionary_string_string
    source_form: |-
      "createTempHierarchy({
         'self.glp': 'TypeA ::= a ; b.',
         'sub/self.glp': 'TypeB ::= x ; y.',
         'sub/module.glp': 'procedure foo(TypeA?, TypeB?).\\nfoo(A, B) :- true | true.',
       })"
    target_decision: >-
      Dart untyped map literals `{ 'k': 'v', ... }` (Dart infers
      `Map<String, String>` from the elements) passed as the
      single argument to `createTempHierarchy` map to C# initializer
      `new Dictionary<string, string> { ["k"] = "v", ... }`
      (collection-initializer with the indexer-init shape — C# 6+).
      Each Dart `'...': '...'` entry becomes one C# indexer-init
      entry. The key strings are RELATIVE PATHS with embedded
      forward slashes (e.g. `'sub/deep/module.glp'`); the value
      strings are GLP source code (verbatim, no transformation —
      they remain opaque string content). Codegen MUST preserve
      the embedded `\n` escape sequences in the GLP-source values
      EXACTLY (Dart `\n` is a backslash-n escape inside a single-
      quoted string → newline; C# `\n` is identical — backslash-n
      inside a double-quoted string → newline).
    idiom_id: null
    research_finding_id: rf-dart-map-literal-string-string-to-csharp-dictionary-string-string
    nuance: >-
      FIRST-SEEN idiom row (defines a new active KB entry).
      Authoritative Dart: https://dart.dev/language/collections
      ("Maps are unordered. … A map literal looks like a JSON
      object literal."). Authoritative .NET: Microsoft Learn
      "Object and Collection Initializers"
      (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers)
      — "An indexer initializer can be used in collection
      initializers since C# 6". Insertion-order nuance (explicitly
      addressed): Dart `{...}` map literal is a `LinkedHashMap`
      (insertion-ordered); C# `Dictionary<string, string>` is
      hash-ordered (NO insertion-order guarantee pre .NET 9). For
      THIS use case the order does NOT matter — `createTempHierarchy`
      iterates `files.entries` to create files/directories, and
      file-creation order is irrelevant to the resulting tree
      (each entry is independent). If a future test depended on
      iteration order, the SPEC would require
      `OrderedDictionary<string, string>` or an explicit
      `KeyValuePair<string, string>[]` array; flagged-and-handled
      but not required here. Key-equality nuance: both Dart and
      C# `string` use ordinal (byte-level) equality for hash keys
      — identical lookup semantics. Empty-map nuance: Dart `{}`
      with TYPE CONTEXT `Map<String, String>` becomes the empty
      map literal; C# `new Dictionary<string, string>()` is the
      empty-dictionary form — not exercised in this file. Related
      cached idiom: `rf-dart-map-literal-to-csharp-dictionary-and-keys-toset-to-hashset`
      (from module_hierarchy.dart.md) covers
      `<K,V>{}` empty / typed-element maps in non-test code; this
      row is the TEST-FILE specialisation for the string→string
      case with literal initialisation, recorded separately so
      downstream test convspecs reuse it directly without re-
      deriving from the more general lib-side idiom.

  - construct_key: dart.async_helper.future_directory_async_function_with_for_in_writeAsString
    source_form: |-
      "Future<Directory> createTempHierarchy(Map<String, String> files) async {
         final tempDir = await Directory.systemTemp.createTemp('glp_hierarchy_test_');
         for (final entry in files.entries) {
           final file = File('${tempDir.path}/${entry.key}');
           await file.parent.create(recursive: true);
           await file.writeAsString(entry.value);
         }
         return tempDir;
       }"
    target_decision: >-
      Lift to `private async Task<DirectoryInfo>
      CreateTempHierarchyAsync(Dictionary<string, string> files)`
      on the shared helper class (or duplicated on each test class
      per the main-entrypoint construct decision). Body:
      `var tempDir = Directory.CreateTempSubdirectory(
      "glp_hierarchy_test_");` (Dart `Directory.systemTemp.
      createTemp('prefix')` ↔ .NET 8+ `Directory.
      CreateTempSubdirectory(string? prefix)` — documented at
      Microsoft Learn "Directory.CreateTempSubdirectory" —
      "Creates a uniquely-named, empty directory in the current
      user's temporary directory, with the specified prefix"; same
      semantics: unique sub-name, present prefix, returned as
      `DirectoryInfo`). For .NET 7 and earlier, the fallback is
      `var tempDir = new DirectoryInfo(Path.Combine(
      Path.GetTempPath(), "glp_hierarchy_test_" +
      Guid.NewGuid().ToString("N"))); tempDir.Create();` — spec
      default = .NET 8+ `CreateTempSubdirectory` (langpair targets
      modern .NET 8+). The `for (final entry in files.entries)`
      loop maps to `foreach (var entry in files)` (C# `Dictionary
      <K,V>` enumerates as `KeyValuePair<K,V>` — `.Key` /
      `.Value` properties available identically to Dart's
      `MapEntry.key` / `.value`). Body of the loop:
      `final file = File('${tempDir.path}/${entry.key}');` →
      `var file = Path.Combine(tempDir.FullName, entry.Key);`
      (NOTE: NOT a `FileInfo` instance — the Dart `File(...)`
      ctor just wraps a path; the .NET counterpart for the
      file-creation pattern is a string path passed to
      `File.WriteAllTextAsync`). `await file.parent.create
      (recursive: true);` →
      `Directory.CreateDirectory(Path.GetDirectoryName(file)!);`
      (Microsoft Learn: "If the directory already exists, this
      method does not create a new directory, but it returns a
      DirectoryInfo object for the existing directory."  The
      method is RECURSIVE BY DEFAULT — no `recursive: true`
      parameter is needed because the .NET method ALWAYS creates
      all intermediate directories. Microsoft Learn
      "Directory.CreateDirectory"
      https://learn.microsoft.com/dotnet/api/system.io.directory.createdirectory).
      `await file.writeAsString(entry.value);` →
      `await File.WriteAllTextAsync(file, entry.Value);`
      (Microsoft Learn: "Asynchronously creates a new file,
      writes the specified string to the file, and then closes
      the file. If the target file already exists, it is
      overwritten." — semantically identical to Dart's
      `File.writeAsString` which also overwrites by default).
      The method returns `Task<DirectoryInfo>` resolving to the
      newly-created temp `tempDir`.
    idiom_id: null
    research_finding_id: rf-dart-systemTemp-createTemp-to-csharp-path-getTempPath-plus-directory-createDirectory
    nuance: >-
      FIRST-SEEN idiom row. Three sub-mappings registered under
      this finding: (a) `Directory.systemTemp.createTemp('prefix')`
      → `Directory.CreateTempSubdirectory("prefix")` (.NET 8+) or
      `Path.GetTempPath` + manual unique name (.NET 7 fallback);
      (b) `file.parent.create(recursive: true)` →
      `Directory.CreateDirectory(Path.GetDirectoryName(path)!)`;
      (c) `file.writeAsString(s)` →
      `await File.WriteAllTextAsync(path, s)`. Async-vs-sync
      nuance (LOAD-BEARING, explicitly addressed): Dart's
      `createTemp` is async (returns `Future<Directory>`); .NET's
      `CreateTempSubdirectory` is SYNCHRONOUS (returns
      `DirectoryInfo` directly). The faithful C# render DROPS the
      `await` on this call — the method body remains `async` only
      because OTHER awaits (`WriteAllTextAsync`) keep it in the
      async dimension. Dart's `createTemp` is async largely as
      defensive future-proofing (the underlying OS call IS sync);
      .NET reflects this honestly. Dart's `file.parent.create
      (recursive: true)` likewise is async; .NET's
      `CreateDirectory` is SYNCHRONOUS — same drop-`await`
      treatment. Dart's `file.writeAsString` IS genuinely async
      (the write CAN be issued asynchronously); .NET's
      `WriteAllTextAsync` is the genuine async variant, preserved
      with `await`. Unique-name nuance: Dart `createTemp('prefix')`
      generates a unique-name with the given prefix (api.dart.dev:
      "the temporary directory has a name that starts with prefix
      and includes a random component"); .NET 8+
      `CreateTempSubdirectory` has identical semantics
      (Microsoft Learn). Both APIs guarantee uniqueness atomically
      against concurrent calls. Path-composition nuance:
      `'${tempDir.path}/${entry.key}'` uses Dart's string
      interpolation with a literal `/` separator; the C# render
      uses `Path.Combine(tempDir.FullName, entry.Key)` which
      handles the platform-correct separator transparently. The
      literal `/` in the Dart source is BENIGN because Dart's
      `Directory.systemTemp.createTemp` returns POSIX-style paths
      on POSIX and even Windows often accepts `/` separators —
      but `Path.Combine` is the documented .NET idiom and
      handles the case where `entry.key` itself contains `/`
      (which it DOES for `'sub/self.glp'`, `'sub/deep/module.glp'`,
      etc.) — `Path.Combine` recognises the embedded separator
      and produces a normalised path. Recursive-create nuance:
      Dart `file.parent.create(recursive: true)` creates ALL
      intermediate parent directories; .NET `Directory.CreateDirectory`
      is RECURSIVE BY DEFAULT (no explicit `recursive: true`
      argument exists — the method ALWAYS recurses).
      `Path.GetDirectoryName` returns the directory portion of
      `file` (`tempDir.FullName/sub/deep` for input
      `tempDir.FullName/sub/deep/module.glp`); the null-forgiving
      `!` is needed because `Path.GetDirectoryName` returns
      `string?` (returns null for root paths — which cannot
      happen here because `tempDir.FullName` is always a non-root
      path).

  - construct_key: dart.directory.delete_recursive_await
    source_form: "await tempDir.delete(recursive: true);"
    target_decision: >-
      Dart `await tempDir.delete(recursive: true)` maps to
      `tempDir.Delete(recursive: true);` (sync `DirectoryInfo.Delete(
      bool)` overload — Microsoft Learn
      "DirectoryInfo.Delete(Boolean)": "Deletes this instance of a
      DirectoryInfo, specifying whether to delete subdirectories
      and files"). The `await` is DROPPED because .NET has no
      `DirectoryInfo.DeleteAsync` (and no `Directory.DeleteAsync`).
      The .NET method is fast enough (operates on already-resolved
      OS handles) that sync is the idiomatic discipline; the test
      method's outer `async Task` shape is preserved by other
      awaits in the body. If the underlying OS call is slow (e.g.
      network filesystem), the sync call MAY block — a documented
      limitation, not a bug. Spec default = sync `Delete(true)`.
    idiom_id: null
    research_finding_id: rf-dart-directory-delete-recursive-to-csharp-directory-delete-recursive
    nuance: >-
      FIRST-SEEN idiom row. Authoritative Dart:
      https://api.dart.dev/stable/dart-io/Directory/delete.html
      ("Deletes the directory. If recursive is true, the
      directory and all sub-directories and files in the
      directory are deleted recursively.") — Dart's `delete`
      returns `Future<FileSystemEntity>`. Authoritative .NET:
      Microsoft Learn "DirectoryInfo.Delete(Boolean)"
      (https://learn.microsoft.com/dotnet/api/system.io.directoryinfo.delete#system-io-directoryinfo-delete(system-boolean))
      — synchronous; throws `IOException` if recursive=false and
      the directory is non-empty (identical to Dart's
      `FileSystemException` in the same case). Async-shape
      nuance (LOAD-BEARING, explicitly addressed): the
      Stream→IAsyncEnumerable / async-everywhere nuance does NOT
      apply here — the .NET BCL deliberately offers ONLY a sync
      delete because the operation is bounded by OS-handle
      latency, not by network/disk IO of unbounded size. The
      `await` drop is FAITHFUL, not lossy. Try-finally nuance
      (see the cleanup-discipline construct below): the C#
      render preserves the `try { ... } finally {
      tempDir.Delete(true); }` shape exactly — even though
      `Delete` is sync, it stays in the `finally` block to
      guarantee cleanup even on test-body exception. Exception-
      swallow nuance: if `Delete` itself throws (e.g. concurrent
      handle), .NET will propagate the exception — the
      `try/finally` does NOT swallow it (identical to Dart's
      behaviour). If the test ITSELF threw and `Delete` also
      throws, .NET preserves the ORIGINAL test exception and
      attaches the `Delete` exception as an
      `AggregateException` only if explicitly composed —
      otherwise the `Delete` exception MASKS the test exception
      (this is a well-known .NET try/finally footgun;
      `await using` + `IAsyncDisposable` is the modern remedy,
      see the cleanup-discipline construct below).

  - construct_key: dart.try_finally_cleanup_discipline
    source_form: |-
      "try {
         final chain = discoverSelfChain(targetFile: ..., rootDir: ...);
         expect(...);
       } finally {
         await tempDir.delete(recursive: true);
       }"
    target_decision: >-
      Dart `try { ... } finally { await tempDir.delete(recursive:
      true); }` translates statement-for-statement to C# `try { ...
      } finally { tempDir.Delete(true); }`. The `finally` block is
      the textbook C# resource-cleanup idiom (Microsoft Learn
      "try-finally": "The finally block runs regardless of how
      the try block exits"). The sync `Delete(true)` call
      (per the previous construct) sits in the finally block
      unawaited. ALTERNATIVE: `await using` + `IAsyncDisposable`
      — a custom `TempDirectoryScope : IAsyncDisposable` wrapper
      that disposes via `Directory.Delete(true)` would let the
      test body read `await using var tempDir = await
      CreateTempHierarchyAsync(...);` without the explicit
      `try/finally`. The modern idiom is `await using` (C# 8+,
      Microsoft Learn "Asynchronous disposable") but spec default
      = explicit `try/finally` for byte-faithful preservation of
      the Dart source shape; codegen MAY refactor to `await using`
      as a polish pass.
    idiom_id: null
    research_finding_id: rf-dart-try-finally-cleanup-to-csharp-try-finally-or-await-using
    nuance: >-
      FIRST-SEEN idiom row. Authoritative Dart:
      https://dart.dev/language/error-handling#finally — "To
      ensure that some code runs whether or not an exception is
      thrown, use a finally clause." Authoritative .NET:
      Microsoft Learn "try-finally"
      (https://learn.microsoft.com/dotnet/csharp/language-reference/statements/exception-handling-statements#the-try-finally-statement)
      and "IAsyncDisposable"
      (https://learn.microsoft.com/dotnet/api/system.iasyncdisposable).
      Exception-masking nuance (LOAD-BEARING, explicitly
      addressed): if the test body throws AND the `Delete` call
      also throws, both languages' default behaviour is to
      propagate the SECOND (finally-block) exception, masking the
      first. Dart's behaviour is identical (per dart.dev
      "Exceptions"); the byte-faithful C# render preserves this
      behaviour. `await using` with `IAsyncDisposable` is the
      modernised remedy on the C# side (composes
      `AggregateException` cleanly via the framework) — recorded
      as the documented alternative for codegen polish.
      Cleanup-discipline nuance: every test in this file follows
      the create→try→assert→finally→cleanup pattern; the C# render
      preserves the discipline literally, which means EACH `[Fact]`
      method contains its own `try/finally`. The DRY alternative
      (a base-class `IAsyncDisposable` or a helper method) is
      flagged but not adopted in the spec default — too far a
      refactor from the Dart source for the FR-023 spec-only
      mandate.

  - construct_key: dart.package_test.expect_equals_implicit_int
    source_form: |-
      "expect(chain.length, 2);
       expect(chain.length, 1);
       expect(responseDef!.alternatives.length, 2);
       expect(responseDef!.alternatives.length, 3);
       expect(fooDef!.alternatives.length, 3);"
    target_decision: >-
      `expect(<actual>, <int-literal>)` (implicit-equals
      shorthand on an integer expected value) maps to xUnit
      `Assert.Equal(<expected>, <actual>);` per the cached idiom
      (rf-dart-expect-equals-to-xunit-assertequal — precedent
      module_parser_test.dart.md, module_syntax_v2_test.dart.md).
      ARGUMENT-ORDER FLIP REQUIRED (Dart `(actual, expected)` ↔
      xUnit `(expected, actual)`). All five integer-length
      assertions in this file use the implicit-equals shorthand
      on `.length` (a `List<TypeDefAlternative>` count via the
      `TypeDef.alternatives` property per type_ast.dart.md, OR
      `List<String>` count on the discoverSelfChain return). C#
      `Count` (on `IReadOnlyList<T>`) is the BCL counterpart of
      Dart `.length`; the field/property name MUST be
      PascalCased per the langpair's convention.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      module_syntax_v2_test.dart.md, mad_error_handling_test.dart.md
      prose). Argument-order footgun (cached, re-emphasised): the
      single highest-risk per-call transformation. Value-vs-
      reference nuance: every integer compared here is a `.length`
      / `.Count` value-type result — `int.Equals` is identical to
      Dart's `==` on `int`, no nuance. Member-naming nuance: Dart
      `alternatives` (lowercase) → C# `Alternatives` (PascalCase);
      Dart `length` → C# `Count` (per `IReadOnlyList<T>.Count` —
      the BCL property name differs from Dart's). The cached idiom
      records this case-renaming convention.

  - construct_key: dart.package_test.expect_equals_implicit_string
    source_form: |-
      "expect(chain[0], '${tempDir.path}/self.glp');
       expect(chain[1], '${tempDir.path}/sub/self.glp');
       expect(chain[1], '${tempDir.path}/sub/deep/self.glp');"
    target_decision: >-
      `expect(<actual>, <interpolated-string>)` maps to xUnit
      `Assert.Equal(<expected>, <actual>);` per the cached idiom
      (same idiom as the integer case, applied to `string`).
      ARGUMENT-ORDER FLIP REQUIRED. The interpolated-string
      shape `'${tempDir.path}/<suffix>'` translates to a C# 6+
      interpolated string `$"{tempDir.FullName}/<suffix>"` — the
      Dart `${expr}` interpolation token ↔ C# `{expr}` inside
      `$"..."`. Path-resolution nuance: the expected path uses
      `tempDir.path` which is the same path the SUT returned in
      `chain[i]`, so byte-identity is guaranteed by Dart's
      filesystem normalisation (and similarly by .NET's
      `Path.GetFullPath` under the cached
      rf-dart-dart-io-to-csharp-system-io). The literal `/`
      separator inside the interpolated suffix is preserved
      verbatim — the SUT (module_hierarchy.dart) normalises to
      forward slashes via its own discipline (see
      module_hierarchy.dart.md's trailing-slash-strip nuance);
      the C# render preserves this behaviour because the SUT's
      C# conversion preserves it too.
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Cached idiom (precedent as above). String-equality nuance:
      Dart `String ==` is byte-equality (UTF-16 code-unit
      comparison); xUnit `Assert.Equal(string, string)` uses
      `string.Equals(string)` (ordinal by default in .NET 5+ per
      `string.Equals(string)` overload) — semantically identical
      for ASCII paths. Interpolation nuance: Dart `'${x.path}/y'`
      and C# `$"{x.FullName}/y"` both perform compile-time
      template expansion. The Dart `tempDir.path` property
      returns `String`; the C# `tempDir.FullName` property
      returns `string` — both are the "absolute filesystem path"
      property for the temp directory.

  - construct_key: dart.package_test.expect_isEmpty_matcher
    source_form: "expect(chain, isEmpty);"
    target_decision: >-
      `expect(<collection>, isEmpty)` maps to xUnit `Assert.Empty
      (<collection>);` per the cached idiom
      (rf-dart-expect-isEmpty-to-xunit-assert-empty — precedent
      module_parser_test.dart.md, localize_test.dart.md). One use
      in this file (`'returns empty chain when no self.glp exists'`
      test, asserting `discoverSelfChain` returns `[]`).
      `Assert.Empty(IEnumerable)` accepts any enumerable;
      `IReadOnlyList<string>` (the return shape per
      module_hierarchy.dart.md) trivially satisfies the
      requirement.
    idiom_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    research_finding_id: rf-dart-expect-isEmpty-to-xunit-assert-empty
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      localize_test.dart.md). Empty-shape nuance: Dart `isEmpty`
      works via `Iterable.isEmpty` (O(1) for `List`); xUnit
      `Assert.Empty` short-circuits via the enumerator (O(1) for
      `IList<T>` because it queries `Count` first when the type
      implements `ICollection`). Behaviourally equivalent.

  - construct_key: dart.package_test.expect_isTrue_isFalse_matcher
    source_form: |-
      "expect(env.hasType('Response'), isTrue);
       expect(env.hasType('AgentContent'), isTrue);
       expect(env.hasType('SharedType'), isTrue);
       expect(env.hasType('AgentType'), isFalse);
       expect(env.hasType('Integer'), isTrue);
       expect(env.hasType('Constant'), isTrue);
       expect(env.hasProcedure('constant', 1), isTrue);
       expect(env.hasProcedure('shared_proc', 2), isTrue);
       expect(env.hasProcedure('helper', 2), isTrue);
       expect(proc!.exported, isTrue);"
    target_decision: >-
      `expect(<actual>, isTrue)` → `Assert.True(<actual>);` and
      `expect(<actual>, isFalse)` → `Assert.False(<actual>);` per
      the cached matcher routing table
      (rf-dart-expect-isTrue-to-xunit-assert-true — precedent
      module_syntax_v2_test.dart.md, smoke_test.dart.md). Used
      9+1 times in this file (every `hasType` / `hasProcedure`
      query plus the `proc!.exported` check). The `TypeEnvironment.
      hasType` / `hasProcedure` methods (per type_ast.dart.md)
      return `bool` directly — strict, no truthiness ambiguity.
      The `proc!.exported` access uses the null-forgiving operator
      on the `proc` local (which was just assigned the result of
      `env.getProcedure('shared_proc', 2)` — a nullable
      `ProcDecl?` return per type_ast.dart.md). The null-bang
      `!` translates as null-forgiving on the C# side (see the
      null-bang construct below); the access becomes
      `proc!.Exported`.
    idiom_id: rf-dart-expect-isTrue-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-to-xunit-assert-true
    nuance: >-
      Cached idiom (precedent: module_syntax_v2_test.dart.md,
      smoke_test.dart.md). Strict-bool nuance: Dart `bool` is
      strict (no truthiness); C# `bool` is strict — `Assert.True`
      / `Assert.False` accept ONLY `bool`, no implicit conversion.
      Matcher-vs-value collapse: Dart `expect(x, isTrue)`
      (matcher form) and `expect(x, true)` (value-as-matcher form)
      both collapse to the SAME `Assert.True(x)` in C# — the
      distinction is lost (lossless because both have identical
      semantics for `bool`-typed actuals).

  - construct_key: dart.package_test.expect_isNotNull_matcher
    source_form: |-
      "expect(responseDef, isNotNull);
       expect(fooDef, isNotNull);
       expect(proc, isNotNull);"
    target_decision: >-
      `expect(<actual>, isNotNull)` maps to xUnit `Assert.NotNull
      (<actual>);` per the cached idiom
      (rf-dart-expect-isNotNull-to-xunit-assert-notnull — precedent
      module_parser_test.dart.md, global_send_test.dart.md). Three
      uses in this file: each precedes a null-bang member access
      on the same local (`responseDef!.alternatives.length`,
      `fooDef!.alternatives.length`, `proc!.exported`). xUnit's
      `Assert.NotNull` is decorated with `[NotNull]` so the C#
      null-flow analyser narrows the local to non-nullable for
      the remainder of the method scope — the subsequent `!`
      could in principle be dropped, but spec default PRESERVES
      the `!` for byte-faithful migration (codegen MAY drop it
      as a polish pass).
    idiom_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    research_finding_id: rf-dart-expect-isNotNull-to-xunit-assert-notnull
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md,
      global_send_test.dart.md, localize_test.dart.md). Flow-
      narrowing nuance (cached, explicitly addressed): xUnit's
      `Assert.NotNull(object?)` triggers null-state narrowing on
      the asserted variable (`[NotNull]` parameter attribute —
      Microsoft Learn null-state-analysis). Dart `isNotNull` has
      no equivalent flow-narrowing (Dart `!` is a runtime check,
      not a static-analysis suppression — but the static result
      after `expect(x, isNotNull)` is irrelevant because Dart's
      analyser already permits `x!` regardless). Composition with
      the null-bang construct below is the canonical pairing.

  - construct_key: dart.nullable_bang.property_access
    source_form: |-
      "responseDef!.alternatives.length;
       fooDef!.alternatives.length;
       proc!.exported;"
    target_decision: >-
      Dart `<nullable-expr>!.<member>` maps to C# `<nullable-expr>!
      .<Member>` (same `!` syntax — Dart's null-assertion operator
      and C#'s null-forgiving operator coincide on the surface;
      semantics differ — see the cached idiom and the nuance row).
      `responseDef!.alternatives` → `responseDef!.Alternatives`;
      `fooDef!.alternatives` → `fooDef!.Alternatives`;
      `proc!.exported` → `proc!.Exported`. The `!` is preserved
      even though the preceding `Assert.NotNull` should flow-
      narrow the local; codegen MAY drop the `!` as a polish
      pass once the cross-file `Alternatives` / `Exported`
      property types are confirmed non-nullable.
    idiom_id: rf-dart-bang-to-csharp-null-forgiving
    research_finding_id: rf-dart-bang-to-csharp-null-forgiving
    nuance: >-
      Cached idiom (precedent: module_syntax_v2_test.dart.md).
      Runtime-vs-static nuance (cached, LOAD-BEARING, explicitly
      addressed): Dart `!` is a RUNTIME null-check that throws
      `TypeError` if the value is in fact null; C# `!` is a
      COMPILE-TIME null-forgiving operator with NO runtime
      effect — if the value is null, the SUBSEQUENT member access
      throws `NullReferenceException`. For THIS file the preceding
      `expect(<x>, isNotNull)` (mapped to `Assert.NotNull(<x>)`)
      guarantees non-null at the `!` site, so the runtime
      semantics coincide observably. Without that prior
      assertion, the Dart `!` would throw at the `!` itself,
      while the C# `!` would throw at the next `.<Member>` access
      — a SUBTLE divergence that does not affect THIS file's
      tests (every `!` is preceded by `Assert.NotNull`).

  - construct_key: dart.named_arguments_call_site
    source_form: |-
      "discoverSelfChain(
         targetFile: '${tempDir.path}/sub/module.glp',
         rootDir: tempDir.path,
       );
       assembleTypeScope(chain: chain, module: module);"
    target_decision: >-
      Dart NAMED-ARGUMENT call sites
      `discoverSelfChain(targetFile: X, rootDir: Y)` map to C#
      named-argument call sites
      `ModuleHierarchy.DiscoverSelfChain(targetFile: X, rootDir: Y)`
      — IDENTICAL syntax (C# 4+ supports named arguments at every
      call site). Both languages enforce parameter-name binding
      with NO behaviour change. The function-name PascalCases per
      the langpair convention; the parameter names ALSO PascalCase
      under the C# property/argument convention IF the converted
      `ModuleHierarchy.DiscoverSelfChain` declares them as
      PascalCase parameters (per module_hierarchy.dart.md the
      converted parameters keep their `targetFile` / `rootDir`
      casing — Dart camelCase reads naturally as C# camelCase
      parameter names, NOT PascalCase, because parameter names
      are NOT public API in the Microsoft naming guidelines —
      they are camelCase by Microsoft convention). So the C#
      render is literally
      `ModuleHierarchy.DiscoverSelfChain(targetFile: ..., rootDir:
      ...);` and
      `ModuleHierarchy.AssembleTypeScope(chain: ..., module: ...);`.
    idiom_id: rf-dart-named-required-params-to-csharp-positional-params
    research_finding_id: rf-dart-named-required-params-to-csharp-positional-params
    nuance: >-
      Cached idiom (precedent: lib/runtime/module_hierarchy.dart.md
      — the lib-side decision that named-required Dart params
      become positional C# params with `Method(name: value)` named-
      argument syntax at call sites). This file is the FIRST test-
      file that exercises the call-site form of the cached idiom.
      Call-site nuance (LOAD-BEARING, explicitly addressed): C#'s
      named-argument syntax `Method(name: value)` is fully equivalent
      to Dart's; readability is preserved one-to-one. Argument-
      order nuance: C# allows the named arguments in ANY order
      (Microsoft Learn "Named arguments"); Dart also allows any
      order for named parameters — semantics match. Mixing
      positional + named: C# allows `Method(positional, name:
      value)` (positional FIRST, named AFTER); Dart's named
      parameters are EITHER all named (the `{...}` shape, as in
      `discoverSelfChain`) OR none (the `(...)` shape). For THIS
      file's calls all arguments are named — no mixing exercised.

  - construct_key: dart.helper_call.parseModule_local_function_to_method_call
    source_form: "final module = parseModule(moduleSource);"
    target_decision: >-
      Dart `parseModule(moduleSource)` is a call to the
      local-function helper defined at the top of `main()`. After
      the lift to a class-level helper method (per the main-
      entrypoint construct), the call site becomes `var module =
      ParseModule(moduleSource);` (private-instance method on the
      enclosing test class OR the shared helper static class —
      same shape). The helper body itself is a simple Lexer →
      Parser pipeline preserved per
      module_hierarchy.dart.md's `AssembleTypeScope` body
      conventions: `var lexer = new Lexer(source); var tokens =
      lexer.Tokenize(); var parser = new Parser(tokens); return
      parser.ParseModule();`. Returns `Module` (from
      `Glp.Compiler`) — non-nullable per ast.dart.md.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Closure-lift composition (cached idiom): the call site is
      unchanged in shape (function call with identical argument);
      only the lookup binding differs (Dart local function →
      C# class method). Pipeline-shape nuance: the Lexer →
      Parser → parseModule pipeline matches the
      module_hierarchy.dart.md `AssembleTypeScope` body
      conventions (which also calls Lexer/Parser internally) —
      cross-file consistency required. Pure-function nuance:
      `parseModule` has no side effects beyond the
      Lexer/Parser internal state; the lift to a class method
      preserves this.

  - construct_key: dart.string.single_quoted_literal_pervasive
    source_form: "'TypeA ::= a ; b.', 'TypeB ::= x ; y.', 'procedure foo(TypeA?, TypeB?).\\nfoo(A, B) :- true | true.', 'Response', 'AgentContent', 'SharedType', 'AgentType', 'Foo', 'Integer', 'Constant', 'shared_proc', 'helper', 'constant', 'glp_hierarchy_test_', '../programs/self.glp', etc."
    target_decision: >-
      Every Dart single-quoted string literal in this file maps
      to a C# double-quoted string literal per the cached idiom
      (rf-dart-single-quoted-string-to-csharp-double-quoted-string
      — precedent module_parser_test.dart.md). No literal
      contains a `"` or any Dart-specific escape (`\$`,
      `\u{...}`); the only escape used is `\n` (newline inside
      embedded GLP source strings), which is identical syntax in
      C#. Path-literal nuance: literal `/` separators inside path
      strings (e.g. `'sub/deep/self.glp'`) are preserved verbatim
      — the consumer (`createTempHierarchy` →
      `Path.Combine`) handles platform-correct separator
      composition transparently.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted-string
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md).
      Quote-character nuance (cached, explicitly addressed):
      Dart accepts `'...'` and `"..."`; C# accepts ONLY `"..."`.
      Escape nuance: `\n` (newline) is identical syntax in both
      languages; no transformation needed. Verbatim-vs-raw nuance:
      none of this file's literals require multi-line content
      (the triple-quoted-string idiom is NOT exercised by THIS
      file — the embedded GLP sources are all one-line strings
      with `\n` escapes for the second clause).

  - construct_key: dart.list.indexer_access
    source_form: "chain[0]; chain[1];"
    target_decision: >-
      Dart `chain[i]` (List index access) maps to C# `chain[i]`
      (IList<T> indexer) — IDENTICAL syntax per the cached idiom
      (rf-dart-list-indexer-to-csharp-list-indexer — precedent
      module_parser_test.dart.md). Used 5 times in this file
      (every chain-element verification step). Zero-indexed both
      sides; bounds-check throws `RangeError` (Dart) /
      `ArgumentOutOfRangeException` (C#) on OOB — but the tests
      always assert `chain.length == N` BEFORE indexing, so the
      throw path is not exercised.
    idiom_id: rf-dart-list-indexer-to-csharp-list-indexer
    research_finding_id: rf-dart-list-indexer-to-csharp-list-indexer
    nuance: >-
      Cached idiom (precedent: module_parser_test.dart.md).
      Trivial 1-to-1 mapping; recorded for KB completeness.

  - construct_key: dart.file_constructor_existsSync_readAsStringSync_top_of_main
    source_form: |-
      "final rootSelfGlp = File('../programs/self.glp');
       if (rootSelfGlp.existsSync()) {
         setPreludeEnvironmentSource(rootSelfGlp.readAsStringSync());
       }"
    target_decision: >-
      Lift this entire block into the `PreludeFixture` constructor
      (per the main-entrypoint construct decision). Body inside
      the fixture ctor:
        `var rootSelfGlpPath = Path.Combine(AppContext.BaseDirectory,
        "..", "..", "..", "..", "programs", "self.glp");
         if (File.Exists(rootSelfGlpPath)) {
           ModuleHierarchy.SetPreludeEnvironmentSource(
             File.ReadAllText(rootSelfGlpPath));
         }`
      Dart `File('...')` ctor (just wraps a path) → C# string path
      (no analogue ctor needed); `existsSync()` →
      `File.Exists(path)` (cached idiom
      rf-dart-dart-io-to-csharp-system-io); `readAsStringSync()`
      → `File.ReadAllText(path)` (cached). The `'../programs/
      self.glp'` relative path is resolved against the test-
      assembly base directory via `AppContext.BaseDirectory`
      (NOT the Dart-style implicit CWD) — see nuance.
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture
    nuance: >-
      Cached + FIRST-SEEN composition. Cached idiom for the
      `File.existsSync` / `readAsStringSync` calls (mapped under
      rf-dart-dart-io-to-csharp-system-io). FIRST-SEEN for the
      LIFT (top-of-main one-shot bootstrap → xUnit
      `IClassFixture<T>` ctor) — recorded under
      rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture.
      Relative-path nuance (LOAD-BEARING, explicitly addressed):
      Dart `File('../programs/self.glp')` resolves against the
      Dart process's CURRENT WORKING DIRECTORY at the moment
      `existsSync()` is called. `dart test` sets CWD to the
      package root (where `pubspec.yaml` lives), so the relative
      path resolves to `<repo>/programs/self.glp` — a sibling of
      the test directory. xUnit sets CWD to the test ASSEMBLY's
      OUTPUT directory (e.g. `bin/Debug/net8.0/`), so the
      `'../programs/self.glp'` interpretation differs. The C#
      render therefore uses `AppContext.BaseDirectory` + a fixed
      number of `..` segments to reach the equivalent path. The
      ALTERNATIVE — adding `<None Include="..\..\programs\self.glp">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      </None>` to the test `.csproj` — is the cleaner MSBuild-
      integrated remedy; codegen MAY apply it as a langpair-level
      polish. Conditional nuance: the `if (rootSelfGlp.existsSync())`
      guard preserves test-resilience to missing prelude (the
      tests in groups 2a-2e do NOT depend on prelude — only
      group 2f's "prelude as root ancestor" test depends on it);
      the C# render preserves the guard identically.
conversion_units:
  - "cu-1: file-scope using directives — using System; using System.Collections.Generic; using System.IO; using System.Threading.Tasks; using Xunit; using Glp.Compiler; using Glp.Analysis.TypeChecker; using Glp.Runtime;"
  - "cu-2: namespace declaration mirroring the test/module path (e.g. Glp.Test.Module)"
  - "cu-3: public sealed class PreludeFixture — IClassFixture host. Constructor performs once-per-run File.Exists + File.ReadAllText + ModuleHierarchy.SetPreludeEnvironmentSource with AppContext.BaseDirectory-relative path resolution; idempotent + side-effect-only (no instance state to expose)."
  - "cu-4: internal static class ModuleHierarchyTestHelpers — hosts the lifted helper methods: private static Module ParseModule(string source) and private static async Task<DirectoryInfo> CreateTempHierarchyAsync(Dictionary<string, string> files). The latter uses Directory.CreateTempSubdirectory(\"glp_hierarchy_test_\") + foreach loop with Directory.CreateDirectory + File.WriteAllTextAsync. (Spec default: shared helper class to avoid 7-fold duplication. Alternative: per-class duplication.)"
  - "cu-5: public class Phase2aSelfGlpChainDiscoveryTests : IClassFixture<PreludeFixture> with 4 [Fact(DisplayName=\"...\")] async Task methods (DiscoversSelfGlpChainFromRootToTargetDirectory, ReturnsEmptyChainWhenNoSelfGlpExists, SkipsMissingIntermediateSelfGlp, DoesNotIncludeSelfGlpFromTargetFileDirectoryIfTargetIsSelfGlp). Each test creates a temp tree via CreateTempHierarchyAsync, runs ModuleHierarchy.DiscoverSelfChain with named args (targetFile: ..., rootDir: ...), asserts Assert.Equal/Assert.Empty/Assert.Equal-on-chain[i], and disposes the temp tree in a try/finally."
  - "cu-6: public class Phase2bTypeScopeAssemblyFromAncestorChainTests : IClassFixture<PreludeFixture> with 2 [Fact] async Task methods (TypesFromAncestorSelfGlpAreVisibleInDescendantModule, TypesFromMultipleAncestorLevelsAreAllVisible). Each test creates a temp tree, runs DiscoverSelfChain + reads moduleSource via File.ReadAllTextAsync + ParseModule + ModuleHierarchy.AssembleTypeScope(chain: ..., module: ...), asserts Assert.True(env.HasType(...)) and Assert.NotNull(env.GetType(...)) + Assert.Equal(N, responseDef!.Alternatives.Count)."
  - "cu-7: public class Phase2cShadowingTests : IClassFixture<PreludeFixture> with 2 [Fact] async Task methods (ChildSelfGlpShadowsParentTypeDefinition, ModuleOwnTypeShadowsAncestorType). Each tests the shadowing rule with Assert.Equal on alternative counts."
  - "cu-8: public class Phase2dSiblingIsolationTests : IClassFixture<PreludeFixture> with 1 [Fact] async Task method (SiblingFilesDoNotSeeEachOtherTypes). Asserts Assert.True(mediatorEnv.HasType(\"SharedType\")) and Assert.False(mediatorEnv.HasType(\"AgentType\"))."
  - "cu-9: public class Phase2eTypeOnlySelfGlpTests : IClassFixture<PreludeFixture> with 1 [Fact] async Task method (SelfGlpWithOnlyTypeDefinitionsProvidesTypes)."
  - "cu-10: public class Phase2fPreludeAsRootAncestorTests : IClassFixture<PreludeFixture> with 1 [Fact] async Task method (PreludeTypesAreAlwaysVisibleEvenWithoutAnySelfGlp). This is the test that depends on the PreludeFixture's bootstrap call running first; xUnit's fixture-discovery ordering guarantees this."
  - "cu-11: public class Phase2gProcedureDeclarationsFromAncestorSelfGlpTests : IClassFixture<PreludeFixture> with 2 [Fact] async Task methods (ExportedProcedureDeclarationsInSelfGlpAreVisibleToDescendants, PlainProcedureDeclarationsInSelfGlpAreVisibleToDescendants). Uses env.HasProcedure(\"shared_proc\", 2), env.GetProcedure(\"shared_proc\", 2), proc!.Exported."
  - "cu-12: try/finally cleanup discipline in every test body — try { ... assertions ... } finally { tempDir.Delete(true); }. The await on tempDir.delete is DROPPED on the C# side because DirectoryInfo.Delete has no async variant."
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### Cached-idiom reuse profile (FR-012 / SC-007)

This file resolves 14 of its 21 constructs via CACHED idioms from
prior module/* convspecs (module_parser_test.dart.md,
module_syntax_v2_test.dart.md, lib/runtime/module_hierarchy.dart.md):

- `rf-dart-dart-io-to-csharp-system-io` (lib-side precedent, extended
  here with `Directory.systemTemp.createTemp`, `Directory.delete
  (recursive: true)`, `file.writeAsString`, `file.parent.create
  (recursive: true)` — each registered as a sub-mapping under the
  cached finding, see the FIRST-SEEN section below)
- `rf-dart-package-test-import-to-xunit-using`
- `rf-dart-package-import-to-csharp-using-namespace`
- `rf-dart-package-test-main-omit-in-xunit`
- `rf-dart-package-test-group-to-xunit-class`
- `rf-dart-final-local-to-csharp-var-local`
- `rf-dart-expect-equals-to-xunit-assertequal` (for integer and
  string equality)
- `rf-dart-expect-isEmpty-to-xunit-assert-empty`
- `rf-dart-expect-isTrue-to-xunit-assert-true` (for both isTrue
  and isFalse — the cached routing table covers both)
- `rf-dart-expect-isNotNull-to-xunit-assert-notnull`
- `rf-dart-bang-to-csharp-null-forgiving`
- `rf-dart-named-required-params-to-csharp-positional-params`
  (lib-side precedent module_hierarchy.dart.md; this file is the
  FIRST test-file that exercises the call-site form)
- `rf-dart-single-quoted-string-to-csharp-double-quoted-string`
- `rf-dart-list-indexer-to-csharp-list-indexer`

KB-lookup decision-order from `convspec_idiom_schema.md` applied per
construct: KB lookup hit → REUSE verbatim, no re-research, no re-
derivation.

### FIVE FIRST-SEEN idiom rows (research-justified, NO escalation)

This file introduces five constructs not covered by any precedent.
Each was researched against official Dart + .NET documentation per
FR-024:

1. **`rf-dart-async-test-callback-to-xunit-async-task-method`** —
   Dart `test('...', () async { ... })` (every test in this file is
   async) maps to xUnit `public async Task <Name>() { ... }`. xUnit
   natively discovers and awaits async Task-returning test methods
   identically to void methods. Authoritative bases: xunit.net
   "Async tests"
   (https://xunit.net/docs/getting-started/v3/getting-started);
   Microsoft Learn "Async programming with async and await"
   (https://learn.microsoft.com/dotnet/csharp/programming-guide/concepts/async/);
   Dart language tour "Asynchrony support"
   (https://dart.dev/language/async). Distinguished from the cached
   `rf-dart-test-callback-to-xunit-method-body` which addresses the
   SYNCHRONOUS callback case.

2. **`rf-dart-systemTemp-createTemp-to-csharp-path-getTempPath-plus-directory-createDirectory`** —
   the trio of Dart temp-tree primitives (`Directory.systemTemp.
   createTemp`, `file.parent.create(recursive: true)`,
   `file.writeAsString`) maps to .NET `Directory.
   CreateTempSubdirectory` (.NET 8+), `Directory.CreateDirectory`
   (idempotent + recursive by default), `File.WriteAllTextAsync`.
   Authoritative bases: Microsoft Learn "Directory.
   CreateTempSubdirectory"
   (https://learn.microsoft.com/dotnet/api/system.io.directory.createtempsubdirectory);
   Microsoft Learn "Directory.CreateDirectory"
   (https://learn.microsoft.com/dotnet/api/system.io.directory.createdirectory);
   Microsoft Learn "File.WriteAllTextAsync"
   (https://learn.microsoft.com/dotnet/api/system.io.file.writealltextasync);
   Dart api.dart.dev "Directory.systemTemp.createTemp"
   (https://api.dart.dev/stable/dart-io/Directory/createTemp.html);
   Dart api.dart.dev "Directory.create"
   (https://api.dart.dev/stable/dart-io/Directory/create.html);
   Dart api.dart.dev "File.writeAsString"
   (https://api.dart.dev/stable/dart-io/File/writeAsString.html).
   Sync-vs-async sub-nuance: Dart's `createTemp` and `parent.create`
   are async (return `Future<Directory>`); the .NET counterparts
   (`CreateTempSubdirectory`, `CreateDirectory`) are SYNCHRONOUS —
   the `await` is DROPPED on the C# side. Only `writeAsString` ↔
   `WriteAllTextAsync` is genuinely async on both sides.

3. **`rf-dart-directory-delete-recursive-to-csharp-directory-delete-recursive`** —
   Dart `await tempDir.delete(recursive: true)` maps to .NET
   `tempDir.Delete(true)` (sync, no async variant in BCL). The
   `await` is DROPPED. Authoritative bases: api.dart.dev
   "Directory.delete"
   (https://api.dart.dev/stable/dart-io/Directory/delete.html);
   Microsoft Learn "DirectoryInfo.Delete(Boolean)"
   (https://learn.microsoft.com/dotnet/api/system.io.directoryinfo.delete#system-io-directoryinfo-delete(system-boolean)).
   Both APIs throw on non-empty + recursive=false; both succeed
   identically for recursive=true.

4. **`rf-dart-try-finally-cleanup-to-csharp-try-finally-or-await-using`** —
   Dart `try { ... } finally { await tempDir.delete(recursive:
   true); }` maps to C# `try { ... } finally { tempDir.Delete(true);
   }`. The `finally` block is the textbook cleanup idiom on both
   sides. Authoritative bases: dart.dev "Error handling — finally"
   (https://dart.dev/language/error-handling#finally); Microsoft
   Learn "try-finally"
   (https://learn.microsoft.com/dotnet/csharp/language-reference/statements/exception-handling-statements#the-try-finally-statement);
   Microsoft Learn "IAsyncDisposable"
   (https://learn.microsoft.com/dotnet/api/system.iasyncdisposable)
   for the modernised `await using` alternative. Exception-masking
   nuance (cleanup-throws-after-test-throws) is identical on both
   sides; `await using` is the cleaner remedy but spec default =
   explicit try/finally for byte-faithful migration.

5. **`rf-dart-map-literal-string-string-to-csharp-dictionary-string-string`** —
   Dart `{'a': 'x', 'b': 'y'}` (string→string map literal) maps to
   C# `new Dictionary<string, string> { ["a"] = "x", ["b"] = "y" }`
   (indexer-initializer, C# 6+). Authoritative bases:
   dart.dev/language/collections "Map literals"; Microsoft Learn
   "Object and Collection Initializers"
   (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers).
   Order-divergence flagged (Dart `LinkedHashMap` vs C#
   `Dictionary`) but not load-bearing for THIS file's use.

6. **`rf-dart-file-existsSync-conditional-prelude-bootstrap-to-csharp-class-static-ctor-or-fixture`** —
   the once-per-file conditional setup pattern at the top of
   `main()` maps to xUnit `IClassFixture<T>` ctor. Authoritative
   bases: xunit.net "Shared Context between Tests"
   (https://xunit.net/docs/shared-context); Microsoft Learn "Shared
   context in xUnit"
   (https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices).
   The fixture is shared across the seven test classes via the
   `IClassFixture<T>` declaration pattern; xUnit guarantees
   exactly-once instantiation per test-run.

### Why no escalations (FR-013)

Every construct has a clear, single-decision target shape grounded
in official Dart and .NET documentation. Two "soft" decisions
(per-group-class vs flatten; shared helper class vs per-class
duplication of `ParseModule` / `CreateTempHierarchyAsync`) are
documented batch-wide policy (cached from module_parser_test.dart.md;
cleanest VS Test Explorer grouping for the seven-group topology).
The `AppContext.BaseDirectory`-relative path resolution decision
(vs MSBuild `CopyToOutputDirectory`) is recorded as a langpair-level
polish option, with the spec-default choice grounded in keeping the
per-file convspec free of MSBuild discipline. No construct involves
an idiom-vs-research conflict or an idiom-vs-idiom conflict, and
nothing is undecidable. `escalations: []` is therefore intentional,
not a placeholder.

## Notes

- Asynchronous-test surface: every test in this file is `async`.
  The xUnit translation uses `async Task` methods uniformly. The
  Stream→IAsyncEnumerable nuance is correctly NOT asserted (no
  Dart Streams are exercised in this file — only `Future`s).
- The seven groups are sibling top-level groups (NOT nested) —
  same topology as module_parser_test.dart.md's six siblings. Per-
  group class lift is preserved.
- The `PreludeFixture` lift is the single load-bearing
  architectural decision in this file's translation; it is what
  preserves the once-per-process semantics of Dart's
  `setPreludeEnvironmentSource` call from inside `main()`. Without
  the fixture, the side-effecting call would either fire 13 times
  (once per `[Fact]`) or zero times (if hoisted into a `static`
  field initializer with no guaranteed-execution semantics), both
  of which diverge from the Dart source.
- Embedded GLP source strings in the `createTempHierarchy` map
  literals are NOT translated — they remain verbatim string
  content (the GLP language is opaque to the conversion; the GLP
  runtime is on the C# side, the GLP source itself does not
  change).
- Cross-file dependencies: this convspec assumes lexer.dart.md,
  parser.dart.md, ast.dart.md, type_ast.dart.md,
  type_environment_builder.dart.md, and module_hierarchy.dart.md
  resolve to the namespaces and types asserted above. Any change
  in those convspecs (e.g. namespace rename, ctor signature
  change) MUST be reflected here as a respec under FR-019.
