# Conversion Spec — test/multiagent/multiagent_modules_test.dart

> Conversion-spec artifact for test/multiagent/multiagent_modules_test.dart
> (FR-011). Spec-only (FR-023): describes the Dart->C# conversion;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.
>
> File is a `package:test`-based multi-isolate integration test (130
> lines, ONE `test()` case inside ONE `group(...)` in `void main()`).
> It exercises end-to-end agent-isolate orchestration over project-
> compiled GLP modules: builds a `BootConfig` via `BootLoader.load(...)`
> on a raw triple-quoted madGLP boot source (CSSG play 4 — 4 agents
> alice/bob/carol/dave); sets the loaded config's `rootSelfGlpPath` +
> `projectDir`; asserts 4 directives parsed; boots an `IsolateManager`
> with a `TraceConfig(glp: false, mad: true)`; calls `manager.start()`;
> waits 5 s for the protocol to run; tears down via `await
> manager.shutdown()`. Top-level test-only `cssgPlay4BootSource` const
> triple-quoted string carries the GLP boot source verbatim. Skips the
> entire test if `programs/cssg_modules/` is missing (no-op `return`
> from `main`). EVERY non-trivial construct REUSES an idiom recorded by
> prior test specs and prior multiagent SUT specs.
>
> **Threading-model inheritance**: this file boots `IsolateManager`,
> which OWNS the dart:isolate threading-model escalation pinned in
> `lib/multiagent/isolate_manager.dart.md` escalations[0]. INHERIT, do
> NOT re-escalate (FR-013 — escalations are recorded ONCE at the source
> of the undecidable point; downstream consumers reference it). The
> per-option shape of `manager.Boot(...)` / `manager.Start()` /
> `manager.Shutdown()` flows through to this test's bodies but the
> CHOICE belongs to the SUT spec.

```yaml
schema_version: 1
source_path: test/multiagent/multiagent_modules_test.dart
source_sha256: b7dd09684ae1c0f399f6137c4dff0c3f37a624a83e6a703a16ad7161eb094b78
target_code_unit: test/multiagent/MultiagentModulesTest.cs
constructs:
  - construct_key: dart.import.dart_io_for_directory_and_file
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the Dart `import 'dart:io';` and replace with `using
      System.IO;` at file scope. Dart `dart:io` exposes the `File` and
      `Directory` types used by this file (`Directory(projectDir)
      .existsSync()` and `File('../programs/self.glp').absolute.path`).
      Cached idiom — pinned by `lib/compiler/project_linker.dart.md`
      (`rf-dart-dart-io-to-csharp-system-io`) and reused verbatim by
      `lib/engine/glp_engine.dart.md`. The two specific call sites in
      this file resolve via `System.IO.Directory.Exists(string)` and
      `System.IO.Path.GetFullPath(string)` — see
      dart.dart_io.directory_existssync_check and
      dart.dart_io.file_absolute_path_for_self_glp constructs below for
      the per-call-site translation. FR-012 / SC-007 cache hit — no
      re-research.
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Namespace nuance (explicitly addressed): Dart `dart:io` is a
      single import bringing in BOTH `File` and `Directory` (+
      `Platform`, etc.); the C# counterpart `using System.IO;` brings
      in `File`, `Directory`, `FileInfo`, `DirectoryInfo`, `Path`,
      `FileSystemException` etc. as static-method-and-type-holders.
      Sync-only nuance: every `dart:io` call in this file is the
      synchronous variant (`existsSync()`, `.absolute.path`); the
      blocking C# counterparts (`File.Exists`, `Directory.Exists`,
      `Path.GetFullPath`) are documented synchronous and acceptable
      here (test code; no I/O hot path). Platform-portability nuance:
      both `dart:io` and `System.IO` are cross-platform; the test
      relies on the host OS's path-separator conventions implicitly
      through the literal `'../programs/cssg_modules'` /
      `'../programs/self.glp'` — see project-relative-path nuance
      below.
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` and replace at
      file scope with `using Xunit;`. REUSE the project-wide xUnit
      pinning established by every prior `package:test` convspec
      (smoke_test.dart.md, test/multiagent/mad_error_handling_test.
      dart.md, test/multiagent/boot_loader_test.dart.md,
      test/multiagent/global_writers_table_test.dart.md,
      test/multiagent/mad_scenarios_test.dart.md, test/runtime/
      module_activation_test.dart.md). FR-012 / SC-007 cache hit — no
      re-research. Codegen projects to a single namespace mirroring
      the Dart `test/multiagent` directory (e.g. `<RootNs>.Test.
      Multiagent`). The .NET test project (.csproj — out of this
      single-file artifact's scope) provides `xunit` +
      `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` NuGet
      references.
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Framework-choice reuse (project-wide policy, IDENTICAL to
      mad_scenarios_test.dart.md / boot_loader_test.dart.md): every
      `package:test` file in the inventory maps to the SAME .NET
      framework (xUnit). Lifecycle nuance: xUnit creates a FRESH
      instance of the test class per `[Fact]` — this file's single
      `setUp` allocates `manager = IsolateManager();` per test, which
      maps cleanly to xUnit constructor-per-test isolation. The
      `tearDown` async body `await manager.shutdown()` maps to
      `IAsyncLifetime.DisposeAsync` (this is the ASYNC tearDown case
      — distinct from boot_loader_test.dart.md which had no
      tearDown). Module/namespace nuance: xUnit has no top-level test
      functions — tests are public instance methods on a public class
      discovered via `[Fact]` reflection (no per-file entrypoint).
  - construct_key: dart.package_under_test.import_directive_multiagent_sut
    source_form: |-
      "import 'package:glp_runtime/multiagent/boot_loader.dart';
       import 'package:glp_runtime/multiagent/isolate_manager.dart';"
    target_decision: >-
      Two `package:glp_runtime/multiagent/...` SUT imports collapse to
      ONE C# `using` directive (`using <RootNs>.Multiagent;`) — both
      imports resolve into the SAME C# namespace per the conventional
      namespace mapping pinned by the sibling SUT specs
      (`lib/multiagent/boot_loader.dart.md` and
      `lib/multiagent/isolate_manager.dart.md`). The `using` MUST
      resolve every SUT symbol this test references: `BootLoader`
      (class), `BootConfig` (class with `Directives`/`ProjectDir`/
      `RootSelfGlpPath` properties + `SpawnDirective.AgentId`),
      `SpawnDirective` (the items inside `BootConfig.Directives`),
      `IsolateManager` (class with ctor + `Boot(BootConfig, TraceConfig
      ?)`/`Start()`/`Shutdown()` methods), `TraceConfig` (class with
      init-only `Glp`/`Mad`/`Agents` properties + `Off` singleton).
      Cross-file dependency: the test assembly references the SUT
      assembly via the project file (langpair / project-skeleton
      concern — out of scope for THIS artifact, but recorded as the
      load-bearing wiring assumption).
    idiom_id: rf-dart-internal-package-import-to-csharp-using
    research_finding_id: rf-dart-internal-package-import-to-csharp-using
    nuance: >-
      Cached idiom (precedent: boot_loader_test.dart.md, mad_scenarios
      _test.dart.md, module_activation_test.dart.md). Two-imports-
      to-one-using nuance (FIRST-RECORDED for this file): both
      multiagent imports collapse to the SAME `using <RootNs>.
      Multiagent;` because their target convspecs pin the same C#
      namespace. Codegen MUST detect same-namespace coalescing and
      emit ONE `using`, not two. Import-unit nuance: Dart imports a
      library/file; C# imports a namespace. No `show`/`hide`/`as`
      clauses appear, so plain `using <Ns>;`. The SUT spec
      `isolate_manager.dart.md` exposes `IsolateManager` AND
      `TraceConfig` in the same namespace (TraceConfig is declared
      sibling to `IsolateManager`), so a single using brings in both.
      The `BootConfig.SpawnDirective` shape (with `AgentId` property)
      transits through `BootConfig.Directives.Select(d => d.AgentId)`
      at the test body's set-construction site — recorded under
      dart.iterable.map_toset_equals below.
  - construct_key: dart.toplevel.const_string_triple_quoted_raw_bootsource
    source_form: |-
      "/// madGLP boot source for CSSG play 4 using project-compiled modules.
       const cssgPlay4BootSource = r'''
       -mode(system).
       ...
       ui_actor(dave, 4, Ch) :- dave4(Ch?).
       ''';"
    target_decision: >-
      Dart top-level `const String cssgPlay4BootSource = r'''...''';`
      (RAW multi-line triple-quoted string, no interpolation, no
      escape processing — the leading `r` disables backslash escape
      handling) maps to a C# file-scope helper-class static `const`.
      Because C# forbids true top-level fields, emit `internal static
      class MultiagentModulesTestHelpers { internal const string
      CssgPlay4BootSource = @"..."; }` — sibling to the test class
      within the same namespace (same shape as the
      `ModuleActivationTestHelpers` precedent pinned by
      module_activation_test.dart.md). Codegen MUST use a C# verbatim
      string literal `@"..."` to preserve the embedded newlines and
      single-quote characters (`'_user'`, `'_net'`, `_user`, `_net`,
      `Stream?`, `Id?` etc.) without escape processing; the only
      special character in a verbatim string is `"` which doubles to
      `""`. None appear in this payload, so the body is a 1:1
      transcript of the source bytes (preserving the leading newline
      after `r'''`). Doc-comment lines (`///`) preceding the const
      become C# `///` XML-doc `<summary>` text above the const field.
      The C# 11+ raw-string-literal form (`"""..."""`) is an
      equivalent ALTERNATIVE — both forms preserve the payload
      byte-identically when no `"` appears in the literal; the
      verbatim form is selected for parity with
      module_activation_test.dart.md.
    idiom_id: null
    research_finding_id: rf-dart-toplevel-const-multiline-string-to-csharp-helper-class-verbatim-const
    nuance: >-
      Raw-string nuance (explicitly addressed, LOAD-BEARING): the
      Dart literal is `r'''...'''` — the leading `r` makes the
      triple-quoted block a RAW string (no `\n` / `\t` / `\\` escape
      processing). Without the `r`, Dart triple-quoted strings DO
      process backslash escapes. The C# `@"..."` verbatim form
      ALWAYS disables backslash escapes (matches Dart raw exactly).
      The C# `"""..."""` raw form (C# 11+) also disables backslash
      escapes. The literal in this file contains NO `"` characters
      (only `'`, `?`, `(`, `)`, `[`, `]`, `,`, `|`, `:-`, `@`,
      `_`, alphanumerics) so both C# forms are byte-equivalent.
      Top-level-vs-class nuance: Dart permits true file-scope `const
      String foo = '''...''';`; C# does NOT — every member belongs
      to a type. Canonical C# shape: `internal static class
      <File>Helpers { internal const string <Name> = @"..."; }`
      (Microsoft naming guideline: 'Use a static class that contains
      a set of static methods' — https://learn.microsoft.com/
      dotnet/standard/design-guidelines/static-class).
      Interpolation nuance: Dart `'''$x'''` interpolates BUT
      Dart `r'''$x'''` does NOT — the raw form preserves `$` as a
      literal. The source contains NO `$`-interpolation; C# `@"..."`
      treats `$` as a literal character (interpolation requires
      `$@"..."` or `$"..."` — neither used here). Const-allocation
      nuance: Dart `const` strings are canonicalised at compile time;
      C# `const string` is ALSO interned (string interning is
      automatic for `const string` fields) — semantics agree.
      Leading-newline nuance: the literal begins with a newline
      immediately after `r'''`; the C# verbatim literal preserves
      the same leading newline literally. The GLP boot loader parses
      this source line-by-line, so the leading newline is benign.
  - construct_key: dart.package_test.main_entrypoint
    source_form: |-
      "void main() {
         final projectDir = '../programs/cssg_modules';
         if (!Directory(projectDir).existsSync()) { print(...); return; }
         group(...);
       }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]`
      methods on `public` classes by reflection; there is no per-file
      entrypoint to emit. REUSE the omit-main pinning from
      boot_loader_test.dart.md / mad_scenarios_test.dart.md /
      module_activation_test.dart.md. The single inner `group(...)`
      becomes the enclosing test class (see
      dart.package_test.group_block below). The skip-if-missing
      guard at the top of `main` (`Directory(projectDir).existsSync()`
      false ⇒ `print` + early `return`) does NOT survive at the
      class-discovery level — xUnit cannot skip an entire class
      based on a runtime predicate at the class-discovery stage.
      The faithful translation places the existence-guard INSIDE
      every `[Fact]` method's body as an `Assert.SkipWhen(...)`
      (xUnit v3) OR a `Skip.IfNot(Directory.Exists(projectDir),
      "...")` (Xunit.SkippableFact NuGet — well-known third-party
      helper for runtime skip) — see
      dart.package_test.main_runtime_skip_guard for the chosen idiom.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (IDENTICAL to boot_loader_test.dart.md): Dart
      `main` is invoked once per test-file process; xUnit has no
      per-file hook. THIS file's `main` body is the existence-guard +
      a single `group(...)` call, so the omission of `main` itself
      is lossless EXCEPT for the existence-guard — which must
      relocate to per-test bodies (xUnit v3 `Assert.SkipWhen` /
      `Assert.SkipUnless` is the modern documented mechanism, OR the
      third-party `Xunit.SkippableFact` for v2/v3 fallback). The
      relocation preserves the source's "skip when fixture
      directory missing" semantics. See the next construct for the
      exact placement.
  - construct_key: dart.package_test.main_runtime_skip_guard
    source_form: |-
      "final projectDir = '../programs/cssg_modules';
       if (!Directory(projectDir).existsSync()) {
         print('cssg_modules not found at $projectDir, skipping tests');
         return;
       }"
    target_decision: >-
      The Dart skip-if-missing guard at the top of `main` is a RUNTIME
      skip — `package:test` evaluates it once per test-file process
      and either runs the registered tests or silently returns. xUnit
      has TWO documented runtime-skip mechanisms: (a) xUnit v3
      `Assert.SkipUnless(condition, "reason")` /
      `Assert.SkipWhen(condition, "reason")` (Microsoft Learn / xunit.
      net v3 docs); (b) the `Xunit.SkippableFact` NuGet package
      providing `[SkippableFact]` + `Skip.IfNot(condition, "reason")`
      for v2-compatibility. Codegen targeting xUnit v3 emits
      `Assert.SkipUnless(Directory.Exists(s_projectDir),
      "cssg_modules not found at " + s_projectDir + ", skipping
      tests");` as the FIRST statement of every `[Fact]` body in the
      class. The fixture-path literal `'../programs/cssg_modules'` is
      hoisted to a `private const string s_ProjectDir = "../programs/
      cssg_modules";` field on the helper class (or the test class).
      The Dart `print('... skipping tests')` collapses into the
      `Assert.SkipUnless` reason string — xUnit reporters surface the
      skip reason automatically; an additional `Console.WriteLine` is
      redundant.
    idiom_id: null
    research_finding_id: rf-dart-package-test-runtime-skip-to-xunit-assert-skip
    nuance: >-
      Skip-semantics nuance (FIRST-RECORDED for this convspec): Dart
      `package:test`'s implicit "main returns before registering
      tests" is a SILENT skip — no `Skipped` test count appears in
      the runner output (the tests simply don't exist). xUnit
      `Assert.SkipUnless` records a SKIPPED test outcome (counted in
      the run summary). This is a SEMANTIC DRIFT but is the closest
      faithful mapping (the alternative — wrapping the entire test
      assembly in a runtime predicate — is not supported by xUnit).
      Codegen recommendation: emit `Assert.SkipUnless` and accept the
      "1 skipped" reporter signal as the faithful translation; the
      skip reason ("cssg_modules not found at ..., skipping tests")
      preserves the diagnostic text. ALTERNATIVE `[Fact(Skip = "...")]
      ` (static skip) is REJECTED — the Dart guard is RUNTIME, not
      static; a static `Skip` would skip even when the directory IS
      present. Cross-platform-path nuance: the literal
      `'../programs/cssg_modules'` is a relative path with a forward
      slash; .NET's `Directory.Exists` accepts forward slashes on
      Windows (the canonicalisation handles `/` <-> `\` automatically
      for `Directory.Exists` per Microsoft Learn). Project-relative-
      path nuance: the relative-from-`test/multiagent` parent-then-
      `programs/cssg_modules` path assumes the test runner's CWD is
      the project root — the same assumption Dart's `package:test`
      makes. The C# port preserves the literal verbatim; codegen
      may add a comment noting the CWD assumption.
  - construct_key: dart.package_test.group_block_single
    source_form: |-
      "group('Multi-isolate with project-compiled modules', () {
         late IsolateManager manager;
         setUp(() { manager = IsolateManager(); });
         tearDown(() async { await manager.shutdown(); });
         test('boots CSSG play 4 with project-linked modules', () async { ... });
       });"
    target_decision: >-
      Dart `group(label, body)` maps to a single `public class
      MultiIsolateWithProjectCompiledModulesTests`. Group-label-to-
      class-name mangling: strip non-identifier characters (spaces,
      hyphens) and PascalCase remaining tokens; appended `Tests`.
      Specifically `'Multi-isolate with project-compiled modules'` ->
      `MultiIsolateWithProjectCompiledModulesTests`. The original
      label MUST be preserved via `[Trait("Group", "Multi-isolate
      with project-compiled modules")]` on the class for reporter
      parity. The class contains: (1) one private `IsolateManager
      _manager = null!;` field — the `late` field idiom; (2) a public
      constructor body assigning `_manager = new IsolateManager();`
      — the setUp idiom; (3) `IAsyncLifetime` implementation with
      `DisposeAsync` body `await _manager.ShutdownAsync();` — the
      async tearDown idiom; (4) ONE `[Fact]` method `BootsCssgPlay4
      WithProjectLinkedModules()` carrying the test body. Single
      `[Fact]` so name-collision-across-groups is N/A (only one
      group in this file).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Single-group nuance (IDENTICAL to boot_loader_test.dart.md /
      module_activation_test.dart.md): one outer `group` with no
      inner groups → one xUnit test class. Name-mangling nuance:
      `'Multi-isolate with project-compiled modules'` contains
      spaces and hyphens — codegen strips them and PascalCases the
      tokens (`Multi`, `Isolate`, `With`, `Project`, `Compiled`,
      `Modules`) — yielding `MultiIsolateWithProjectCompiledModules
      Tests`. Trait preservation: `[Trait("Group", "<original
      label>")]` documents the source grouping for reporters. No
      nested `group`, so no `[Trait]` per inner-group needed.
      Constructor-as-setUp + IAsyncLifetime.DisposeAsync-as-async-
      tearDown is the canonical xUnit lifecycle mapping
      (https://xunit.net/docs/shared-context — 'Constructor and
      Dispose' + 'IAsyncLifetime').
  - construct_key: dart.package_test.late_field_isolatemanager
    source_form: "late IsolateManager manager;"
    target_decision: >-
      Dart `late IsolateManager manager;` declared in the `group`
      callback (closed over by setUp + tearDown + the test) maps to
      a `private IsolateManager _manager = null!;` instance field
      on the xUnit test class. REUSE the `late`-to-`null!` idiom
      pinned by boot_loader_test.dart.md
      (`rf-dart-late-field-to-csharp-nullforgiving-field`). The
      field is assigned in the constructor (the setUp mapping —
      see next construct), so `null!` is the non-nullable
      "assigned-later" idiom matching Dart's `late` semantics
      (initialised before any reader runs; throws if read
      uninitialised).
    idiom_id: rf-dart-late-field-to-csharp-nullforgiving-field
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Null-safety nuance (IDENTICAL to boot_loader_test.dart.md):
      Dart `late T x;` is a non-null `T` that throws
      `LateInitializationError` if read before assignment; the
      closest C# equivalent for an xUnit per-test field is
      `private T _x = null!;` (non-nullable reference, suppressed
      initialiser warning, assigned in the constructor). Because the
      xUnit constructor runs BEFORE every `[Fact]`, the `null!` is
      replaced before any reader runs — semantically equivalent to
      Dart `late + setUp`. Alternative `private IsolateManager?
      _manager;` (nullable + `!` at every read site) was REJECTED
      because it inverts the "guaranteed-initialised" contract that
      `late` encodes. The `IsolateManager` SUT type itself is
      DOWNSTREAM of `lib/multiagent/isolate_manager.dart.md`'s
      threading-model escalation — the field TYPE is unaffected
      (the C# class is still named `IsolateManager` across all four
      options); only its INTERNAL field types vary.
  - construct_key: dart.package_test.setUp_block_simple
    source_form: |-
      "setUp(() { manager = IsolateManager(); });"
    target_decision: >-
      Dart `setUp` registered inside the group maps to the xUnit
      test class's CONSTRUCTOR body. Specifically: `public
      MultiIsolateWithProjectCompiledModulesTests() { _manager =
      new IsolateManager(); }`. xUnit instantiates the test class
      once per test method (constructor-per-test isolation), which
      matches `package:test`'s per-test fresh-state semantics
      exactly. REUSE the setUp-to-constructor idiom pinned by
      boot_loader_test.dart.md (`rf-dart-setup-to-xunit-
      constructor`). Synchronous setUp body — no `async`. Mandatory
      C# `new` keyword (Dart's optional-`new` constructor call
      requires C#'s explicit `new`).
    idiom_id: rf-dart-setup-to-xunit-constructor
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (IDENTICAL to boot_loader_test.dart.md):
      `package:test`'s `setUp` is per-test and runs in the same
      isolate; xUnit's constructor is per-test and runs on the same
      thread — both give a fresh `_manager` per test, identical
      observable semantics. NO `[SetUp]` attribute exists in xUnit
      (that is NUnit's idiom); using the constructor is the
      documented xUnit pattern. Constructor allocation nuance: the
      `new IsolateManager()` call site's RESOLVED shape depends on
      the SUT spec — `lib/multiagent/isolate_manager.dart.md` pins
      `IsolateManager` as a public class with a default
      (parameterless) constructor; the field initialisers
      (`_agentPorts = new Dictionary<...>`, `_mainPort = <deferred
      port type>`) run inside the ctor body. The CALL-SITE shape
      `new IsolateManager()` is option-INDEPENDENT (no constructor
      arguments are passed by this test) — only the SUT's internal
      field-initialisation logic varies across the four threading
      options. Codegen at the call site emits `new IsolateManager()`
      regardless.
  - construct_key: dart.package_test.teardown_block_async_shutdown
    source_form: |-
      "tearDown(() async { await manager.shutdown(); });"
    target_decision: >-
      Dart `tearDown` with an `async` callback that `await`s a SUT
      cleanup method maps to xUnit's `IAsyncLifetime.DisposeAsync`
      implementation: `public async ValueTask DisposeAsync() {
      await _manager.ShutdownAsync(); }` (xUnit v3) or `public async
      Task DisposeAsync() { await _manager.ShutdownAsync(); }`
      (xUnit v2 `IAsyncLifetime`, Microsoft Learn / xunit.net docs).
      The test class declares `: IAsyncLifetime` (xUnit v3) or `:
      IAsyncLifetime` (xUnit v2's interface from `Xunit.IAsync
      Lifetime`). REUSE the async-tearDown idiom recorded for
      forward-compat by boot_loader_test.dart.md (the no-tearDown
      case there explicitly noted "Async-setUp nuance: Dart
      `setUp(() async { ... })` would map to xUnit
      `IAsyncLifetime.InitializeAsync` — NOT used here, recorded
      in the research finding only" — the corresponding async-
      tearDown pattern is `DisposeAsync`). The `_manager.
      ShutdownAsync()` call resolves to the C# method per the SUT
      spec `lib/multiagent/isolate_manager.dart.md` which pins
      `public Task Shutdown()` (deferred body — see
      escalation-inheritance nuance). Codegen MAY use the C# method
      name `Shutdown` or `ShutdownAsync` per the SUT spec's chosen
      identifier — the SUT spec uses `Shutdown` (Dart `shutdown` ->
      PascalCase `Shutdown`); `Async` suffix is NOT mandated by
      `lib/multiagent/isolate_manager.dart.md`. Faithful spec form
      here: `await _manager.Shutdown();`.
    idiom_id: null
    research_finding_id: rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync
    nuance: >-
      Async-lifecycle nuance (FIRST-RECORDED for this convspec): xUnit
      v3 supports async dispose via `IAsyncLifetime.DisposeAsync`
      returning `ValueTask` (https://xunit.net/docs/shared-context);
      xUnit v2 supports the same interface returning `Task`. The Dart
      `tearDown(() async { ... })` maps to either, depending on the
      xUnit major version targeted. Synchronous `IDisposable.Dispose`
      is REJECTED — would force `.GetAwaiter().GetResult()` on the
      async shutdown, which sync-over-async deadlocks in certain
      SynchronizationContexts (the threading-model Option 4 risk).
      Async-vs-sync-tearDown nuance: Dart `tearDown` accepts both
      sync and async callbacks (the body `() async { await ... }` is
      the async form); xUnit's `IAsyncLifetime` separates init/
      dispose into async-only and `IDisposable` into sync-only — a
      class implementing BOTH covers both lifecycle phases. Codegen
      SHOULD emit `IAsyncLifetime` for this file (only async
      tearDown; no sync dispose needed) — the InitializeAsync method
      returns `ValueTask.CompletedTask` (no body content because
      this file has only sync setUp; constructor covers it).
      Authoritative: xUnit v3 docs 'Shared Context between Tests'
      / 'IAsyncLifetime'. Inherited-escalation nuance: the body of
      `_manager.Shutdown()` is DEFERRED per `lib/multiagent/
      isolate_manager.dart.md` escalations[0]; THIS test merely
      AWAITS the SUT's `Task`. The call-site shape `await _manager.
      Shutdown()` is option-independent across the four threading
      models — only the SUT's internal close-port-and-clear
      operations vary.
  - construct_key: dart.package_test.test_call_async
    source_form: |-
      "test('boots CSSG play 4 with project-linked modules', () async {
         final loader = BootLoader();
         final config = loader.load(cssgPlay4BootSource);
         config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;
         config.projectDir = projectDir;
         expect(config.directives.length, equals(4));
         expect(config.directives.map((d) => d.agentId).toSet(),
                equals({'alice', 'bob', 'carol', 'dave'}));
         await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: true));
         manager.start();
         await Future.delayed(Duration(seconds: 5));
       }, timeout: Timeout(Duration(seconds: 30)));"
    target_decision: >-
      The single Dart `test(label, () async { ... }, timeout: ...)`
      becomes a `public async Task BootsCssgPlay4WithProjectLinked
      Modules()` method decorated with `[Fact(DisplayName = "boots
      CSSG play 4 with project-linked modules", Timeout = 30000)]`.
      Method name = label PascalCased with non-identifier chars
      stripped: `BootsCssgPlay4WithProjectLinkedModules`. The Dart
      `Timeout(Duration(seconds: 30))` maps to the xUnit `[Fact]`
      named-arg `Timeout = 30000` (xUnit v2/v3 — milliseconds, NOT
      seconds; the unit conversion is mandatory). Method body
      translates the Dart arrange-act-assert verbatim, with
      `expect(...)` calls routed to xUnit `Assert.*` per the
      matcher-routing idioms pinned by prior specs.
      `Assert.SkipUnless(Directory.Exists(s_ProjectDir),
      "cssg_modules not found at " + s_ProjectDir + ", skipping
      tests");` is inserted as the FIRST statement (the relocated
      `main`-level skip guard). Async-method-Task-return nuance:
      `() async` -> `async Task` (NOT `async void` — `async void`
      is for event handlers only, NOT for awaitable bodies). xUnit
      awaits the returned `Task` and surfaces failures via the
      task's exception (Microsoft Learn 'async-await').
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async nuance (FIRST-RECORDED for this convspec — prior tests
      were all synchronous): Dart `test('...', () async { ... })`
      maps to `[Fact] public async Task <Name>()`; xUnit's runner
      awaits the returned `Task` (https://xunit.net/docs/comparisons
      — `Task`-returning facts are first-class). The Dart `await`
      operators map 1:1 to C# `await`. Closure-capture nuance: the
      callback captures `manager` from the enclosing `group` scope;
      the xUnit translation captures `this._manager` from the
      test-class instance, which is equivalent because the
      constructor (setUp) has already assigned it before the method
      runs. `projectDir` is captured from `main` scope — translated
      as the hoisted `s_ProjectDir` constant on the helper or test
      class. Timeout-unit nuance (LOAD-BEARING): Dart
      `Timeout(Duration(seconds: 30))` is SECONDS; xUnit `[Fact]
      Timeout` is MILLISECONDS. Codegen MUST convert 30 -> 30000.
      MISSING this conversion would silently set a 30-millisecond
      timeout (effectively zero) and the test would always time out.
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: |-
      "final loader = BootLoader();
       final config = loader.load(cssgPlay4BootSource);"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in
      C# where the initializer is a constructor invocation or a
      method call. Specifically:
      `final loader = BootLoader()` -> `var loader = new BootLoader
      ();` (mandatory C# `new` keyword);
      `final config = loader.Load(MultiagentModulesTestHelpers.
      CssgPlay4BootSource);` (the SUT method `Load` is PascalCased
      per `lib/multiagent/boot_loader.dart.md`; the const-string
      reference resolves through the file-scope `using static
      <RootNs>.Test.Multiagent.MultiagentModulesTestHelpers;` IF
      emitted — otherwise qualified by helper-class name). REUSE
      the final-to-var idiom pinned by mad_scenarios_test.dart.md
      (`rf-dart-final-local-to-csharp-var-local`).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (IDENTICAL to mad_scenarios_test
      .dart.md): Dart `final <local>` prevents REBINDING the local
      after init but does NOT prevent mutation of the referenced
      object's state — exactly the same semantics as C# `var`.
      Constructor-syntax nuance: Dart allows `Foo(...)` without
      `new`; C# requires `new Foo(...)`. The `BootLoader` SUT type
      is pinned by `lib/multiagent/boot_loader.dart.md` with a
      default (parameterless) constructor — the call site `new
      BootLoader()` is unambiguous. SUT-method-resolution nuance:
      `loader.load(...)` on the Dart side calls `public BootConfig
      Load(string source)` per the SUT spec — Dart camelCase ->
      C# PascalCase per the .NET method-naming guideline.
  - construct_key: dart.field.bootconfig_mutable_field_assignment
    source_form: |-
      "config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;
       config.projectDir = projectDir;"
    target_decision: >-
      Two POST-CONSTRUCTION assignments to `BootConfig` non-final
      fields. The SUT spec `lib/multiagent/boot_loader.dart.md` pins
      `BootConfig` as a class with THREE GET-SET properties
      (`SharedSources`, `ProjectDir`, `RootSelfGlpPath`) — the
      Dart `final` vs. non-`final` distinction maps to C# `{ get; }`
      vs. `{ get; set; }`. Both assignments here target non-`final`
      Dart fields (per the SUT source) — codegen emits:
      `config.RootSelfGlpPath = Path.GetFullPath("../programs/
      self.glp");` (the `File(p).absolute.path` -> `Path.GetFullPath
      (p)` idiom — pinned by project_linker.dart.md /
      glp_engine.dart.md);
      `config.ProjectDir = s_ProjectDir;` (relocated hoisted const).
      BOTH property names use PascalCase per the SUT spec; the
      assignments rely on the `BootConfig` properties being `{ get;
      set; }` (NOT `{ get; init; }`) — per the SUT spec's explicit
      decision that callers (specifically `isolate_manager.dart`)
      reassign these AFTER construction.
    idiom_id: null
    research_finding_id: rf-dart-bootconfig-mutable-field-to-csharp-getset-property
    nuance: >-
      Mutable-property-vs-init-only nuance (LOAD-BEARING, explicitly
      addressed): the `BootConfig` SUT spec
      `lib/multiagent/boot_loader.dart.md` pins `Directives` /
      `FullSource` / `Source` as `{ get; }` (init-once), and
      `SharedSources` / `ProjectDir` / `RootSelfGlpPath` as `{ get;
      set; }` (read-write) — the Dart `final` vs. non-`final`
      distinction. THIS test exercises the post-construction
      reassignment pattern that motivated the SUT decision; the
      ALTERNATIVE `{ get; init; }` shape would compile-error at the
      assignment site (init-only properties are settable only inside
      an object initialiser). Codegen MUST emit `{ get; set; }` on
      the SUT side to permit this call-site shape. Inherited
      reference: this construct's nuance is the test-side
      counterpart of the SUT-side decision recorded under
      `lib/multiagent/boot_loader.dart.md` (BootConfig field-
      mutability section).
  - construct_key: dart.dart_io.file_absolute_path_for_self_glp
    source_form: "File('../programs/self.glp').absolute.path"
    target_decision: >-
      Dart `File(p).absolute.path` (a getter chain — `File` ctor,
      `absolute` getter returning a new `File` whose path is
      canonicalised against the current working directory, `.path`
      getter returning the canonical string) maps to C#
      `System.IO.Path.GetFullPath(p)` — the documented single-call
      equivalent (Microsoft Learn 'Path.GetFullPath Method —
      Returns the absolute path for the specified path string').
      REUSE the pinned idiom from
      `lib/compiler/project_linker.dart.md` /
      `lib/engine/glp_engine.dart.md` (`rf-dart-dart-io-to-csharp-
      system-io`, sub-mapping `file.absolute.path ->
      Path.GetFullPath`). Specifically: `File('../programs/
      self.glp').absolute.path` -> `Path.GetFullPath("../programs/
      self.glp")`. The result is a string assignable to
      `config.RootSelfGlpPath` (a `string` property per the SUT
      spec).
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Canonicalisation-semantics nuance (IDENTICAL to
      project_linker.dart.md): Dart `File(p).absolute.path` resolves
      `p` against the process CWD when `p` is relative, and returns
      `p` unchanged when `p` is absolute. C# `Path.GetFullPath(p)`
      has identical semantics (Microsoft Learn confirms the CWD-
      relative resolution). Both calls do NOT touch the filesystem
      (no existence check; no symlink resolution by default — though
      C# `Path.GetFullPath` may differ subtly on UNC vs. relative
      Unix paths; the in-this-file usage is a simple relative path
      so this nuance is benign). Forward-slash nuance: the literal
      `'../programs/self.glp'` uses forward slashes; .NET handles
      forward slashes on Windows transparently — `Path.GetFullPath`
      returns a path with Windows-native `\` separators when run
      on Windows, with `/` separators on Unix. The Dart side
      returns the platform-native separator. For round-trip
      filesystem operations both are equivalent. Async/Stream:
      ABSENT — `Path.GetFullPath` is a pure synchronous string
      transformation.
  - construct_key: dart.dart_io.directory_existssync_check
    source_form: "Directory(projectDir).existsSync()"
    target_decision: >-
      Dart `Directory(p).existsSync()` (synchronous filesystem
      existence check) maps to C# `System.IO.Directory.Exists(p)`.
      REUSE the pinned idiom from
      `lib/compiler/project_linker.dart.md` (Microsoft Learn
      'Directory.Exists Method — Determines whether the given path
      refers to an existing directory on disk'). Specifically:
      `Directory(projectDir).existsSync()` ->
      `Directory.Exists(s_ProjectDir)`. The result is a `bool`
      (Dart `bool` -> C# `bool`). Negation `!Directory(...).existsSync()`
      -> `!Directory.Exists(...)`. The call-site appears INSIDE the
      `Assert.SkipUnless(...)` guard in every relocated `[Fact]`
      method (see dart.package_test.main_runtime_skip_guard).
    idiom_id: rf-dart-dart-io-to-csharp-system-io
    research_finding_id: rf-dart-dart-io-to-csharp-system-io
    nuance: >-
      Sync-only nuance: Dart `existsSync()` is the BLOCKING form;
      Dart also provides async `exists()`. C# `Directory.Exists` is
      BLOCKING (no async counterpart in `System.IO.Directory` —
      `DirectoryInfo.Exists` is the property form, also blocking).
      For test-startup gate use, blocking is acceptable. Symlink-
      handling nuance: both Dart `existsSync` and C# `Directory.
      Exists` return `true` for a symlink targeting an existing
      directory (they don't dereference; the OS handles the check
      transparently). Permission nuance: both return `false`
      silently when the path is inaccessible due to permissions
      (NO exception thrown — Microsoft Learn 'Directory.Exists'
      documents this). Identical behaviour.
  - construct_key: dart.expression.expect_equals_int_length
    source_form: "expect(config.directives.length, equals(4));"
    target_decision: >-
      Dart `expect(actual, equals(expected))` where `expected` is an
      `int` literal maps to xUnit `Assert.Equal(expected, actual)`
      with the ARGUMENT-ORDER FLIP. Specifically: `expect(config.
      directives.length, equals(4))` -> `Assert.Equal(4,
      config.Directives.Count);` (Dart `.length` on `List<T>` ->
      C# `.Count` on `IReadOnlyList<T>` per the SUT spec — `Count`
      is the property on `ICollection<T>` / `IReadOnlyCollection<T>`;
      `.Length` is for arrays and strings only). REUSE the
      equals-flip idiom pinned by boot_loader_test.dart.md /
      mad_scenarios_test.dart.md
      (`rf-dart-expect-equals-to-xunit-assertequal`).
    idiom_id: rf-dart-expect-equals-to-xunit-assertequal
    research_finding_id: rf-dart-expect-equals-to-xunit-assertequal
    nuance: >-
      Argument-order nuance (LOAD-BEARING, IDENTICAL to every prior
      test convspec): Dart `expect(actual, equals(expected))`; xUnit
      `Assert.Equal(expected, actual)` — codegen MUST flip the
      order. Length-vs-Count nuance: Dart `List<T>.length` -> C#
      `IReadOnlyList<T>.Count` (NOT `.Length`). The SUT spec
      `lib/multiagent/boot_loader.dart.md` declares
      `BootConfig.Directives` as `IReadOnlyList<SpawnDirective>`,
      which has `.Count` (Microsoft Learn 'IReadOnlyCollection<T>.
      Count Property'). Codegen using `.Length` on an
      `IReadOnlyList<T>` would compile-error.
  - construct_key: dart.iterable.map_toset_equals
    source_form: |-
      "expect(
        config.directives.map((d) => d.agentId).toSet(),
        equals({'alice', 'bob', 'carol', 'dave'}));"
    target_decision: >-
      Dart `Iterable.map(f).toSet()` -> C# LINQ
      `Enumerable.Select(f).ToHashSet()` (Microsoft Learn 'Enumerable.
      ToHashSet'). The expected literal `{'alice', 'bob', 'carol',
      'dave'}` is a Dart `Set<String>` literal — C# counterpart is
      `new HashSet<string> { "alice", "bob", "carol", "dave" }` (or
      `new HashSet<string>(new[] { "alice", "bob", "carol", "dave" })`).
      xUnit `Assert.Equal` over two `HashSet<T>` instances uses
      element-wise equality via `IEqualityComparer<T>` — for sets the
      semantically correct comparison is `HashSet<T>.SetEquals` (NOT
      `Assert.Equal` over ordered sequences, which would fail on
      different enumeration orders). The canonical xUnit set-equality
      mapping is `Assert.Equal(expectedSet, actualSet,
      HashSet<string>.CreateSetComparer())` (Microsoft Learn
      'HashSet<T>.CreateSetComparer Method' — returns an
      `IEqualityComparer<HashSet<T>>` that compares sets for set
      equality, ignoring order). Concretely: `Assert.Equal(new
      HashSet<string> { "alice", "bob", "carol", "dave" },
      config.Directives.Select(d => d.AgentId).ToHashSet(),
      HashSet<string>.CreateSetComparer());`. ALTERNATIVE
      `Assert.True(expected.SetEquals(actual), "<diagnostic>");`
      collapses to a `True/False` assertion losing the
      element-wise diff; the `CreateSetComparer` form preserves
      richer diagnostics.
    idiom_id: null
    research_finding_id: rf-dart-iterable-map-toset-equals-to-xunit-assertequal-with-setcomparer
    nuance: >-
      Set-equality-vs-sequence-equality nuance (FIRST-RECORDED for
      this convspec): Dart `Set<T>` equality (via `==` over two
      `Set` instances) compares as SET equality (order-independent,
      element-wise via `==`); C# `HashSet<T>` equality via
      `Object.Equals` is REFERENCE equality (two distinct HashSet
      instances always compare unequal). The faithful translation
      MUST use either `HashSet<T>.SetEquals` (returns `bool`) OR
      `HashSet<T>.CreateSetComparer()` (returns an
      `IEqualityComparer<HashSet<T>>` for use with `Assert.Equal`).
      ALTERNATIVE `Assert.Equal` over `IEnumerable<T>` would FAIL
      when enumeration order differs (Dart `Set` insertion order is
      preserved per Dart `LinkedHashSet`; C# `HashSet<T>` enumeration
      order is unspecified per Microsoft Learn — silent failure
      risk). LINQ nuance: Dart `iterable.map(f).toSet()` -> C#
      `enumerable.Select(f).ToHashSet()` (or
      `.ToHashSet<TSource>()` with explicit comparer); both eagerly
      materialise. Mapping nuance: `Iterable.toSet()` returns a
      `Set<T>` (Dart); `Enumerable.ToHashSet()` returns a
      `HashSet<T>` (C# 7.2+, in `System.Linq`). Equality-of-string
      nuance: both Dart `==` and C# `string.Equals` (default
      ordinal) compare codepoint-for-codepoint — `'alice' ==
      'alice'` true in both languages. Element-type nuance: Dart
      `SpawnDirective.agentId` is `String`; C# `SpawnDirective.
      AgentId` is `string` per the SUT spec; LINQ `.Select(d =>
      d.AgentId)` infers `IEnumerable<string>`.
  - construct_key: dart.expression.method_invocation_boot_async_with_named_traceconfig
    source_form: |-
      "await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: true));"
    target_decision: >-
      Dart `await manager.boot(config, traceConfig: TraceConfig(glp:
      false, mad: true));` — async method invocation with a named-
      required `traceConfig:` argument bound to a freshly-constructed
      `TraceConfig` using Dart named-with-default constructor syntax.
      Maps to C#: `await _manager.Boot(config, new TraceConfig {
      Glp = false, Mad = true });`. The SUT spec `lib/multiagent/
      isolate_manager.dart.md` pins `Boot(BootConfig config,
      TraceConfig? traceConfig = null)` — POSITIONAL second
      parameter, NOT named. The Dart `traceConfig: <expr>` named
      argument label is DROPPED at the call site because the C#
      SUT signature is positional. The `TraceConfig(glp: false,
      mad: true)` Dart constructor call maps to C# OBJECT-
      INITIALIZER syntax `new TraceConfig { Glp = false, Mad =
      true }` because the SUT spec pins `TraceConfig` with init-
      only auto-properties (`public bool Glp { get; init; }
      public bool Mad { get; init; }`) — object-initialiser syntax
      is the documented C# counterpart for setting init-only
      properties at construction (Microsoft Learn 'init keyword' /
      'object and collection initializers'). NOT `new TraceConfig
      (glp: false, mad: true)` — the SUT TraceConfig has NO
      positional ctor (only the parameterless default + init-only
      property setters). NOT `new TraceConfig { Glp = false, Mad
      = true, Agents = null }` — `Agents` is nullable with default
      null; omitting it is correct. The whole call returns `Task`;
      `await` unwraps to nothing (`void`-returning per the SUT
      spec's `Task` return).
    idiom_id: null
    research_finding_id: rf-dart-namedargs-call-with-traceconfig-named-ctor-to-csharp-positional-call-with-object-initializer
    nuance: >-
      SUT-spec-determines-call-shape nuance (LOAD-BEARING,
      IDENTICAL to mad_scenarios_test.dart.md's `MadContext`
      shape decision): the Dart `boot(config, traceConfig: ...)`
      uses a named argument, but the C# SUT method is POSITIONAL
      per the explicit `isolate_manager.dart.md` decision. Codegen
      MUST consult the SUT spec PER CALLEE and DROP the Dart
      named-arg label when the C# signature is positional. C#
      ALSO supports named arguments (e.g. `_manager.Boot(config:
      config, traceConfig: new TraceConfig { ... })`) — the SUT
      spec PERMITS either; codegen prefers positional for terseness
      since the parameter list is short. Init-only-property
      nuance: Dart `const TraceConfig({this.glp = false, this.mad
      = false})` named-with-default constructor maps to C# init-
      only auto-properties with default values (object-initialiser
      at the call site). The Dart `TraceConfig(glp: false, mad:
      true)` allocates a fresh instance (NOT `const TraceConfig
      (...)` — non-const, so a fresh allocation each time). The C#
      `new TraceConfig { Glp = false, Mad = true }` is ALSO a
      fresh allocation each time — semantics agree. Mandatory C#
      `new` keyword. Async-await nuance: `await` requires the
      method to return `Task` or `Task<T>` (or `ValueTask`/
      `ValueTask<T>`); the SUT pins `Boot` as `Task`-returning.
      Inherited-escalation nuance: the BODY of `Boot` is DEFERRED
      per the SUT spec's escalations[0]; THIS test merely AWAITS
      the SUT's `Task`. The call-site shape is option-independent.
  - construct_key: dart.expression.method_invocation_start_sync
    source_form: "manager.start();"
    target_decision: >-
      Dart `manager.start();` (synchronous, void-returning
      one-liner). Maps to C# `_manager.Start();`. The SUT spec
      `lib/multiagent/isolate_manager.dart.md` pins
      `public void Start()` (synchronous; the per-port `Send(new
      Start())` call inside is option-dependent but the OUTER
      method signature is `void` across all four threading
      options). Call-site shape is option-INDEPENDENT.
    idiom_id: null
    research_finding_id: rf-dart-instance-method-void-call-to-csharp-pascal-method-call
    nuance: >-
      Synchronous-call nuance (explicitly addressed): Dart
      `manager.start()` returns nothing; C# `_manager.Start()`
      returns nothing. No `await` needed (would compile-error in
      C# on a non-Task-returning method). PascalCase method-name
      nuance: Dart `start` -> C# `Start` (the .NET method-naming
      guideline — Microsoft Learn 'Names of Type Members';
      Microsoft Learn 'Capitalization Conventions'). Inherited-
      escalation nuance: the BODY of `Start` is DEFERRED per the
      SUT spec's escalations[0]; the call-site is unaffected.
  - construct_key: dart.async.future_delayed_duration_seconds
    source_form: "await Future.delayed(Duration(seconds: 5));"
    target_decision: >-
      Dart `Future.delayed(Duration(seconds: N))` is a factory that
      returns a `Future<void>` completing after N seconds — the
      idiomatic Dart "sleep". The C# counterpart is `Task.Delay(
      TimeSpan.FromSeconds(N))` (Microsoft Learn 'Task.Delay
      Method (TimeSpan)' — 'Creates a task that completes after a
      specified time interval'). Specifically: `await Future.
      delayed(Duration(seconds: 5))` -> `await Task.Delay(TimeSpan
      .FromSeconds(5));`. ALTERNATIVE
      `await Task.Delay(5000);` (millisecond integer overload) is
      EQUIVALENT but the `TimeSpan.FromSeconds` form preserves the
      Dart source's unit-explicit shape (`Duration(seconds: 5)`
      ↔ `TimeSpan.FromSeconds(5)`).
    idiom_id: null
    research_finding_id: rf-dart-future-delayed-duration-to-csharp-task-delay-timespan
    nuance: >-
      Async-sleep nuance (FIRST-RECORDED for this convspec):
      Dart `Future.delayed(Duration)` is a NON-BLOCKING wait —
      yields to the event loop and resumes after the delay; the C#
      `Task.Delay(TimeSpan)` is ALSO a non-blocking wait — yields
      to the synchronisation context. NEITHER spins a CPU core. C#
      `Thread.Sleep(int)` is REJECTED (blocking — would freeze the
      thread, defeating the await-yield semantics and risking
      deadlock if called on a UI/sync-context thread). Duration-
      vs-TimeSpan nuance: Dart `Duration` ctor accepts named
      arguments for each unit (`Duration(seconds: 5)` /
      `Duration(milliseconds: 250)` / `Duration(microseconds: 100)`);
      C# `TimeSpan` ctor accepts positional `(hours, minutes,
      seconds)` OR static factories (`TimeSpan.FromSeconds(5)` /
      `TimeSpan.FromMilliseconds(250)` / `TimeSpan.FromTicks(100)`).
      The named-style match `seconds: 5` <-> `FromSeconds(5)` is the
      idiomatic mapping (Microsoft Learn 'TimeSpan.FromSeconds
      Method'). Test-wall-clock nuance (explicitly addressed): a
      5-second hard-coded wait in a test is a CODE SMELL (race-
      sensitive — passes when machine is fast, fails on slow CI
      runners), but it MIRRORS the Dart source exactly; the
      faithful translation preserves it. Codegen MAY add a TODO
      comment noting "5-second sleep mirrors Dart source; consider
      replacing with an explicit completion handshake on
      _manager.OnUIOutput / IsolateManager Ready/Done signal".
  - construct_key: dart.async.duration_seconds_for_timeout
    source_form: "timeout: Timeout(Duration(seconds: 30))"
    target_decision: >-
      Dart `package:test`'s `timeout: Timeout(Duration(seconds:
      30))` named argument on `test(...)` declares a per-test
      timeout. xUnit's counterpart is the `[Fact]` attribute's
      `Timeout` named property — value is MILLISECONDS (NOT
      seconds). Specifically: `Timeout(Duration(seconds: 30))` ->
      `[Fact(... Timeout = 30000)]`. Mandatory unit conversion (30
      seconds = 30000 ms). Codegen at the attribute call site
      emits `[Fact(DisplayName = "boots CSSG play 4 with project-
      linked modules", Timeout = 30000)]`.
    idiom_id: null
    research_finding_id: rf-dart-test-timeout-duration-to-xunit-fact-timeout-milliseconds
    nuance: >-
      Unit-conversion nuance (LOAD-BEARING — silent-bug risk):
      Dart `Duration(seconds: 30)` is 30 SECONDS; xUnit
      `[Fact(Timeout = ...)]` expects MILLISECONDS. MISSING this
      conversion would silently set a 30-millisecond timeout and
      the test would always time out. Codegen MUST emit the
      multiplied value as a compile-time-evaluated integer (`30 *
      1000` or the literal `30000`); a runtime computation like
      `TimeSpan.FromSeconds(30).TotalMilliseconds` is NOT
      acceptable in an attribute argument (must be compile-time
      constant). Timeout-vs-cancellation nuance: xUnit `[Fact
      Timeout]` cancels the test by raising `TimeoutException`
      (the awaited Task is NOT cancelled at the source — the
      test method continues running but its result is
      discarded). Dart `package:test` similarly aborts via
      `TimeoutException` and may continue running the closure.
      Both behaviours are diagnostic-only; the test's pass/fail
      verdict is "timeout" in both. Cancellation-token nuance: an
      ALTERNATIVE more-faithful translation would inject a
      `CancellationToken` from xUnit's test framework into the
      method signature — xUnit v3 supports this via
      `CancellationToken` parameter injection. NOT used here for
      simplicity; recorded for forward-compat.
  - construct_key: dart.comment.test_body_trailing_comment_preserved
    source_form: |-
      "// Verify directives parsed correctly
       ...
       // Boot all 4 agents
       ...
       // Start and let the protocol run
       ...
       // If we reach here without crash, boot + project loading + execution works"
    target_decision: >-
      Dart `// <comment>` single-line comments inside the test body
      map 1:1 to C# `// <comment>` single-line comments. PRESERVE
      verbatim — both languages use the same `//` syntax with
      end-of-line termination. The four comments document the
      Given/When/Then-style structure of the test (verify
      directives; boot; start + sleep; assertion-by-non-crash). The
      final comment ("If we reach here without crash, boot +
      project loading + execution works") documents that this is a
      SMOKE TEST — the assertion is the absence of unhandled
      exceptions during the 5-second await, NOT an explicit
      `expect(...)` after the sleep. Codegen MUST preserve the
      `if-we-reach-here` comment because it's the load-bearing
      pass-criterion for the test reader. ALTERNATIVE elevation to
      a `/// <summary>` XML-doc block on the method is acceptable
      for the comments documenting the test structure; the inline
      `//` comments preserve the Dart source's verbatim shape.
    idiom_id: null
    research_finding_id: rf-dart-line-comment-to-csharp-line-comment
    nuance: >-
      Smoke-test-pass-criterion nuance (FIRST-RECORDED for this
      convspec, LOAD-BEARING): unlike every prior `test()` in the
      multiagent suite (which has explicit `expect(...)` after
      `await`), THIS test's pass criterion is the ABSENCE of an
      unhandled exception during the 5-second `await`. xUnit's
      semantics align: a `Task`-returning `[Fact]` method that
      completes WITHOUT throwing is a PASS; one that throws (or
      whose awaited Task faults) is a FAIL. The C# port preserves
      this — no additional `Assert.*` call is needed after the
      `await Task.Delay(...)`. Codegen MUST NOT inject a synthetic
      `Assert.True(true)` (would falsely imply an explicit
      check). Documentation-comment nuance: the four
      Given/When/Then comments MAY be elevated to a `/// <summary>`
      block above the method (`/// Boots 4 agents on isolates,
      runs the protocol for 5 seconds, and verifies no crash.
      /// /// Arrange: load boot source, set paths.  /// Act:
      manager.Boot + Start + 5s wait.  /// Assert: no exception
      thrown.`). Either preservation form is acceptable; the
      `//`-inline form matches the Dart shape exactly.
conversion_units:
  - cu-1: file-scope using directives (Xunit + System.IO + <RootNs>.Multiagent + System.Linq for Select/ToHashSet)
  - cu-2: namespace declaration mirroring the test/multiagent path (e.g. <RootNs>.Test.Multiagent)
  - cu-3: internal static class MultiagentModulesTestHelpers carrying (a) internal const string CssgPlay4BootSource = @"..."; the verbatim raw GLP boot source, and (b) internal const string ProjectDir = "../programs/cssg_modules"; the hoisted fixture-path constant
  - cu-4: public class MultiIsolateWithProjectCompiledModulesTests, decorated with [Trait("Group", "Multi-isolate with project-compiled modules")], implementing IAsyncLifetime for async tearDown
  - cu-5: private IsolateManager _manager = null!; — late-field mapping (rf-dart-late-field-to-csharp-nullforgiving-field)
  - cu-6: public ctor MultiIsolateWithProjectCompiledModulesTests() assigning _manager = new IsolateManager(); — setUp mapping (rf-dart-setup-to-xunit-constructor)
  - cu-7: public ValueTask InitializeAsync() => ValueTask.CompletedTask; — no async setUp body, but required by IAsyncLifetime
  - cu-8: public async ValueTask DisposeAsync() { await _manager.Shutdown(); } — async tearDown mapping (rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync)
  - cu-9: one [Fact(DisplayName = "boots CSSG play 4 with project-linked modules", Timeout = 30000)] async Task BootsCssgPlay4WithProjectLinkedModules() method, body comprising — (a) Assert.SkipUnless(Directory.Exists(MultiagentModulesTestHelpers.ProjectDir), "cssg_modules not found at " + MultiagentModulesTestHelpers.ProjectDir + ", skipping tests"); — (b) var loader = new BootLoader(); — (c) var config = loader.Load(MultiagentModulesTestHelpers.CssgPlay4BootSource); — (d) config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp"); config.ProjectDir = MultiagentModulesTestHelpers.ProjectDir; — (e) Assert.Equal(4, config.Directives.Count); — (f) Assert.Equal(new HashSet<string> { "alice", "bob", "carol", "dave" }, config.Directives.Select(d => d.AgentId).ToHashSet(), HashSet<string>.CreateSetComparer()); — (g) await _manager.Boot(config, new TraceConfig { Glp = false, Mad = true }); — (h) _manager.Start(); — (i) await Task.Delay(TimeSpan.FromSeconds(5)); — (j) inline // comments preserved verbatim from the Dart source for Given/When/Then documentation
escalations: []
```

## Rationale + research provenance

### Why xUnit (FR-024 official-docs authoritative)

This file is the Nth `package:test` file specced; xUnit was pinned
project-wide by `test/multiagent/mad_error_handling_test.dart.md` and
every subsequent test convspec. Maintaining the pin satisfies SC-007
(consistency via recorded idiom, not re-derivation). The authoritative
basis is xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for the
`[Fact]` / `[Trait]` / constructor-as-setUp / `IAsyncLifetime`-as-async-
tearDown model, and the Dart `package:test` README
(`https://pub.dev/packages/test`) for the `group` / `setUp` /
`tearDown` / `expect` / matcher semantics.

### IsolateManager lifecycle (inherited escalation)

This file BOOTS `IsolateManager` — the SUT class that owns the central
dart:isolate threading-model escalation pinned by
`lib/multiagent/isolate_manager.dart.md` escalations[0]. The four
documented options (Thread-per-agent + BlockingCollection;
single-thread TaskScheduler; Channel<T> actor mailbox;
SynchronizationContext) all change the SUT's INTERNAL field types and
the per-port send/receive call syntax, but the EXTERNAL call-site
shape this test exercises (`new IsolateManager()`,
`_manager.Boot(config, traceConfig)`, `_manager.Start()`,
`_manager.Shutdown()`) is option-INDEPENDENT. The construct rows for
`dart.expression.method_invocation_boot_async_with_named_traceconfig`,
`dart.expression.method_invocation_start_sync`, and
`dart.package_test.teardown_block_async_shutdown` each record the
inheritance and DO NOT re-escalate (FR-013 — escalations are recorded
ONCE at the source of the undecidable point). `escalations: []` is
therefore intentional, not a placeholder.

### Skip-when-fixture-missing relocation

Dart's `package:test` allows `main` to short-circuit via `return`
before registering any test — a SILENT skip with no "skipped" count
in the runner output. xUnit has no analogous mechanism at the
class-discovery stage. The faithful relocation is to insert an
`Assert.SkipUnless(Directory.Exists(...), "...")` as the FIRST
statement of every `[Fact]` method body. xUnit v3 supports this via
`Assert.SkipUnless` (https://xunit.net/docs/skipping-tests); xUnit v2
requires the third-party `Xunit.SkippableFact` package. The relocation
introduces a small semantic drift (one "skipped" count vs. silent
non-existence) but is the closest faithful translation. The
`Assert.SkipUnless`'s reason string preserves the Dart `print`'s
diagnostic text byte-identically.

### `BootConfig` mutable-field assignment

The two assignments `config.rootSelfGlpPath = ...; config.projectDir
= ...;` after `loader.load(...)` exercise the `BootConfig`
SUT-spec decision to expose `RootSelfGlpPath` / `ProjectDir` /
`SharedSources` as `{ get; set; }` properties (NOT `{ get; init; }`).
The SUT spec `lib/multiagent/boot_loader.dart.md` records this
exactly: "BootConfig non-final fields => `{ get; set; }` (NOT
`init`) because callers (BootLoader._parseSpawnDirectives +
isolate_manager.dart) hold instances...". The test-side
counterpart confirms the SUT decision is necessary — were the
properties init-only, the call-site `config.RootSelfGlpPath =
Path.GetFullPath(...)` would compile-error. Recorded under
`rf-dart-bootconfig-mutable-field-to-csharp-getset-property` for
cross-reference.

### `Set<String>` equality via `HashSet<T>.CreateSetComparer`

The Dart assertion `expect(config.directives.map((d) => d.agentId)
.toSet(), equals({'alice', 'bob', 'carol', 'dave'}))` compares two
sets order-independently. The C# faithful translation requires
explicit set-equality semantics; the default `Assert.Equal` over two
`HashSet<T>` instances uses reference equality (always fails). The
two documented .NET mechanisms are `HashSet<T>.SetEquals` (returns
`bool`; loses element-wise diff diagnostic) and
`HashSet<T>.CreateSetComparer()` (returns an
`IEqualityComparer<HashSet<T>>` for use with `Assert.Equal`,
preserving diff diagnostic). The latter is chosen
(`https://learn.microsoft.com/dotnet/api/system.collections.generic.
hashset-1.createsetcomparer`). Recorded as
`rf-dart-iterable-map-toset-equals-to-xunit-assertequal-with-setcomparer`.

### `TraceConfig(glp: false, mad: true)` -> object-initializer

The SUT spec `lib/multiagent/isolate_manager.dart.md` pins
`TraceConfig` as a class with init-only auto-properties + a static
`Off` singleton. Dart-side construction `TraceConfig(glp: false,
mad: true)` (named-with-default constructor) maps to C# object-
initialiser syntax `new TraceConfig { Glp = false, Mad = true }`
because init-only properties are settable only inside an object
initialiser (Microsoft Learn 'init keyword'). The call-site Dart
named-arg label `traceConfig:` on `manager.boot(...)` is DROPPED
because the C# SUT method signature is positional. Both decisions
follow the SUT spec PER CALLEE — codegen consults the SUT spec, not
a mechanical preserve-named-args rule.

### `Future.delayed` -> `Task.Delay`

Dart `Future.delayed(Duration(seconds: 5))` and C# `Task.Delay
(TimeSpan.FromSeconds(5))` are both non-blocking awaitable delays
(Microsoft Learn 'Task.Delay Method (TimeSpan)' —
https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay).
`Thread.Sleep` is REJECTED (blocking; risks sync-context deadlock).
The `TimeSpan.FromSeconds(N)` form preserves the unit-explicit shape
of the Dart `Duration(seconds: N)` source. The 5-second hard-coded
wait is a smoke-test code-smell mirrored from the Dart source —
faithful translation preserves it; a TODO comment may flag the
non-determinism.

### Timeout-unit conversion

Dart `Timeout(Duration(seconds: 30))` is 30 seconds. xUnit
`[Fact(Timeout = ...)]` expects milliseconds. The compile-time-
constant conversion `30 -> 30000` is mandatory. This is the second
unit-conversion footgun in the file (the first being the
TimeSpan-vs-Duration mapping for `Task.Delay`); both are
silent-bug risks if missed.

### Smoke-test pass criterion

The test has NO explicit `expect(...)` after the 5-second `await`
— the final comment `If we reach here without crash, boot + project
loading + execution works` documents that the pass criterion is the
ABSENCE of an unhandled exception. xUnit aligns: a `Task`-returning
`[Fact]` that completes without throwing is a PASS. Codegen MUST
NOT inject a synthetic `Assert.True(true)` to "look like" an
assertion — the absence of an assertion IS the assertion, mirrored
exactly from the Dart source.

### Why no escalations

Every construct has a clear, single-decision target shape grounded
in official documentation for both Dart `package:test` / `dart:io` /
`dart:async` and xUnit / `System.IO` / `System.Threading.Tasks` /
`System.Linq`. The threading-model decision is INHERITED from the
SUT spec — not re-escalated here (FR-013 — escalations are recorded
ONCE at the source). The skip-when-fixture-missing relocation
introduces a small one-count-of-skipped semantic drift vs. the Dart
silent skip — recorded as a nuance, not an escalation, because the
faithful translation is well-defined. The 5-second hard-coded wait
is a code-smell but faithfully mirrored from the source — recorded
as a TODO-worthy comment, not an escalation. `escalations: []` is
therefore intentional, not a placeholder.
