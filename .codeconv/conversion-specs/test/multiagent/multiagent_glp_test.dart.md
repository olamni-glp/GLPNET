# Conversion Spec — test/multiagent/multiagent_glp_test.dart

> Conversion-spec artifact for test/multiagent/multiagent_glp_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/multiagent_glp_test.dart
source_sha256: 859b800ec1e014185b1b52775980da78d2c260421f07700ff9ce1ad742d94aea
target_code_unit: test/multiagent/MultiagentGlpTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Drop the Dart `import 'package:test/test.dart';` directive and replace
      with `using Xunit;` at file scope. xUnit is the project-wide test
      framework already pinned by every prior multiagent-test convspec
      (test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md,
      test/multiagent/mad_scenarios_test.dart.md,
      test/multiagent/global_send_test.dart.md,
      test/multiagent/mad_cold_call_isolate_test.dart.md,
      test/multiagent/ui_mediator_test.dart.md). THIS file MUST reuse that
      idiom verbatim (FR-012 / SC-007) — no re-research, no re-derivation.
      The .NET test project (.csproj — out of this single-file artifact's
      scope) provides `xunit` + `xunit.runner.visualstudio` +
      `Microsoft.NET.Test.Sdk` NuGet references. Codegen projects to a
      single namespace mirroring the Dart `test/multiagent` directory
      (e.g. `<RootNs>.Test.Multiagent`). Codegen MUST also add `using
      System;` (for `Action`, `TimeSpan`, exception types), `using
      System.IO;` (for `File`/`Path` operations — see
      dart.io.file_existssync_readasstringsync below), `using
      System.Collections.Generic;` (for `HashSet<string>?` /
      `ISet<string>?` traceAgents parameter typing — see
      dart.expression.optional_set_string_parameter below), and `using
      System.Threading.Tasks;` (for `Task` / `Task.Delay` —
      Future.delayed translation; IAsyncLifetime for tearDown — see
      dart.package_test.teardown_async_block).
    idiom_id: rf-dart-package-test-to-dotnet-xunit
    research_finding_id: rf-dart-package-test-to-dotnet-xunit
    nuance: >-
      Framework-choice nuance (load-bearing, explicitly addressed): xUnit
      pinned project-wide; NUnit / MSTest recorded as alternatives in the
      research-finding row but NOT used here. Module/namespace nuance:
      Dart's `package:test` exposes top-level functions (`group`, `test`,
      `setUp`, `tearDown`, `Timeout`) re-exported via the one import; xUnit
      has NO top-level test functions — tests are public instance methods
      on a public class discovered via `[Fact]` reflection. Lifecycle
      nuance (LOAD-BEARING for THIS file): unlike boot_loader_test.dart
      (which uses synchronous `setUp` only) and mad_error_handling_test.dart
      (which has empty bodies), this file uses BOTH a synchronous `setUp`
      (constructing the manager) AND an ASYNCHRONOUS `tearDown(() async
      { await manager.shutdown(); })` — see
      dart.package_test.teardown_async_block for the
      `IAsyncLifetime.DisposeAsync` mapping that necessarily replaces the
      plain-constructor + IDisposable.Dispose pattern used by sibling
      multiagent test specs. Async-test nuance: every `test()` body in
      this file is `() async { ... }` — twelve `async Task` test methods
      in the target.
  - construct_key: dart.import.dart_io_core_library
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the `dart:io` line entirely; replace its load-bearing symbols at
      first use with the canonical .NET equivalents. The ONLY `dart:io`
      surface this file uses is the `File` class (one construction via
      `File('../$relativePath')`, one `existsSync()`, one
      `readAsStringSync()`, and `File('../programs/self.glp').absolute.path`
      for `rootSelfGlpPath`). All map to `System.IO.File` and `System.IO.Path`
      static methods (no instance `File` object needed on the C# side —
      .NET treats files as path-keyed operations, not as long-lived
      handles). No `using` directive is emitted for the Dart import itself;
      the targets surface through the BCL `using System.IO;` already added
      by the package_test_import construct above.
    idiom_id: rf-dart-dart-io-to-dotnet-systemio
    research_finding_id: rf-dart-dart-io-to-dotnet-systemio
    nuance: >-
      Standard-library import nuance (explicitly addressed, IDENTICAL to
      sibling test specs that touch the filesystem — see
      ui_mediator_test.dart.md for the `File(...).absolute.path` ⇒
      `Path.GetFullPath(...)` pinning and boot_loader_test.dart.md for
      the file-fixture-reading translation): Dart's `dart:io` is a CORE
      library; .NET's counterpart is `System.IO`. Both expose Read/Write
      primitives; the conversion is per-operation (no single `using` maps
      the whole namespace one-to-one). Cross-platform-path nuance: Dart
      `'../$relativePath'` uses POSIX-style `/` separators in the source;
      .NET `Path.Combine("..", relativePath)` is portable across Windows
      and POSIX (handles separator normalisation). For literal-string
      paths like `'../programs/self.glp'` codegen MAY emit the literal
      verbatim — `File.ReadAllText`/`File.Exists`/`Path.GetFullPath`
      handle either separator on Windows transparently.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/multiagent/boot_loader.dart';
       import 'package:glp_runtime/multiagent/isolate_manager.dart';"
    target_decision: >-
      Both imports are SUT (system-under-test) references — Dart
      `package:glp_runtime/...` URIs that resolve to the converted C#
      namespace for the same source units. Replace each with a C# `using`
      directive that names the namespace the converted SUT will emit into.
      Both SUT files target the SAME sub-namespace (`<RootNs>.Multiagent`)
      per the sibling SUT specs `.codeconv/conversion-specs/lib/multiagent/
      boot_loader.dart.md` and `.codeconv/conversion-specs/lib/multiagent/
      isolate_manager.dart.md`, so ONE `using <RootNs>.Multiagent;` covers
      both. Codegen MUST emit a `using` that resolves every symbol this
      test references: `BootLoader` (instance class with `BootConfig
      Load(string source)` per `boot_loader.dart.md`), `BootConfig`
      (reference class with settable `RootSelfGlpPath` property per
      `boot_loader.dart.md` line 807 — "non-final fields => { get; set; }
      (NOT init)"), `IsolateManager` (central orchestrator class with
      `public Task Boot(BootConfig config, TraceConfig? traceConfig =
      null)`, `public void Start()`, and `public Task Shutdown()` per
      `isolate_manager.dart.md` lines 133-137), and `TraceConfig`
      (init-only properties + `static readonly TraceConfig Off` singleton
      per `isolate_manager.dart.md` line 131).
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (explicitly addressed and IDENTICAL to
      sibling multiagent test specs): a `package:` import that resolves to
      an in-repo Dart library (NOT to a pub.dev third-party package) maps
      to a C# `using <Namespace>;` that targets the OUTPUT namespace of
      the converted Dart library — NOT a separate NuGet reference.
      Distinguish by inspecting the `package:` URI prefix against the host
      repo's `pubspec.yaml` `name:` (here, `glp_runtime`). Project-file
      wiring (`<ProjectReference>` from the test .csproj to the runtime
      .csproj) is langpair/project-skeleton level, recorded so codegen
      knows the `using` alone is insufficient without the project
      reference. Two-imports-collapse nuance: both SUT files target the
      SAME `<RootNs>.Multiagent` sub-namespace — codegen emits ONE `using`,
      not two.
  - construct_key: dart.function.top_level_helper_nullable_string_return
    source_form: >-
      "String? loadFile(String relativePath) {
         final file = File('../$relativePath');
         if (file.existsSync()) {
           return file.readAsStringSync();
         }
         print('Skipping: $relativePath not found at ${file.path}');
         return null;
       }"
    target_decision: >-
      Dart top-level function returning a nullable `String?` maps to a
      private STATIC METHOD on the enclosing xUnit test class — C# has no
      top-level functions outside the top-level-program file (`Program.cs`
      with implicit `main`), and a TEST file is NOT that. Specifically:
      `private static string? LoadFile(string relativePath)`. The body
      translation is:
      `var file = Path.Combine("..", relativePath);` (Dart string
      interpolation `'../$relativePath'` ⇒ `Path.Combine("..",
      relativePath)` — preserves OS-portable separator handling AND
      handles the leading `..` parent-segment correctly);
      `if (File.Exists(file)) return File.ReadAllText(file);`
      (Dart `file.existsSync()` ⇒ `File.Exists(<path>)`; Dart
      `file.readAsStringSync()` ⇒ `File.ReadAllText(<path>)` — both .NET
      counterparts are synchronous and read the entire content into a
      `string` using the system default encoding (UTF-8 — matches Dart's
      `readAsStringSync()` default));
      `Console.WriteLine($"Skipping: {relativePath} not found at {file}");`
      (Dart `print(...)` ⇒ `Console.WriteLine(...)`; Dart string
      interpolation `'$x'`/`'${y}'` ⇒ C# interpolated strings `$"{x}"`;
      Dart `file.path` (the Dart File object's `.path` getter returning
      the stored path string) ⇒ the C# `file` LOCAL is ALREADY a
      `string` path, so `{file}` interpolates the raw path directly — no
      `.Path` accessor needed because there is no `File` OBJECT on the
      .NET side);
      `return null;`.
    idiom_id: null
    research_finding_id: rf-dart-top-level-function-to-csharp-static-method-on-test-class
    nuance: >-
      Top-level-function nuance (explicitly addressed): Dart `String?
      loadFile(...)` is declared OUTSIDE `main()` at file scope; C# test
      file scope can ONLY hold a namespace + class + nested members.
      Therefore `loadFile` MUST be hoisted INTO the test class as a
      `private static` method. Helper-method visibility: `private` is
      sufficient because only the test class itself calls it; `internal
      static` would also work but is over-scope. Nullability nuance: Dart
      `String?` ⇒ C# `string?` (NRT-annotated nullable reference). The
      single Dart caller (`final source = loadFile(glpFile); if (source
      == null) { print(...); return; }`) translates to
      `var source = LoadFile(glpFile); if (source is null) { Console.WriteLine(...); return; }`
      — the `is null` form is preferred over `== null` per Microsoft's
      style guide (https://learn.microsoft.com/dotnet/csharp/fundamentals/
      coding-style/coding-conventions). File-handle nuance: Dart's
      `File(...)` constructs a path-bearing wrapper object with
      `existsSync()`/`readAsStringSync()`/`.path` accessors; .NET's
      `System.IO.File` is STATIC — there is NO `File` object to construct.
      The C# port stores the raw path string directly (`var file = ...;`)
      and calls static methods on it. This is a FAITHFUL translation
      because the Dart code never re-uses the `File` object after the
      single existence-check + read — it is essentially used as a "path
      with helpers." String-interpolation nuance: Dart `'$x'`/`'${y.z}'`
      ⇒ C# `$"{x}"`/`$"{y.Z}"` — same shape, different delimiters.
      Authoritative Dart side: dart.dev "Strings" §string-interpolation
      (https://dart.dev/language/built-in-types#strings). Authoritative
      .NET side: Microsoft Learn "Interpolated strings"
      (https://learn.microsoft.com/dotnet/csharp/language-reference/tokens/interpolated).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Multi-agent GLP tests', () { ... }); }"
    target_decision: >-
      Drop Dart `void main()` entirely — xUnit discovers `[Fact]` methods
      on `public` classes by reflection; there is no per-file entrypoint
      to emit. The single `group('Multi-agent GLP tests', () => { ... })`
      call inside `main` becomes the enclosing test class (see next
      construct).
    idiom_id: rf-dart-test-main-to-xunit-class-with-facts
    research_finding_id: rf-dart-test-main-to-xunit-class-with-facts
    nuance: >-
      Lifecycle nuance (explicitly addressed, IDENTICAL to all sibling
      multiagent test specs): Dart `main` runs once per test-file process
      and registers tests; xUnit has no per-file hook — only per-class
      (constructor + IDisposable.Dispose / IAsyncLifetime) and per-
      collection fixtures. THIS file's `main` body is exactly one
      `group(...)` call with no other statements, so omitting `main` is
      lossless. The hoisted top-level helper `loadFile(...)` lives on the
      test class as `private static string? LoadFile(string)`, NOT in
      `main`'s scope.
  - construct_key: dart.package_test.group_block
    source_form: "group('Multi-agent GLP tests', () { late IsolateManager manager; setUp(...); tearDown(() async { ... }); Future<void> runGlpTest(...) async { ... }; test(...); test(...); ... test(...); });"
    target_decision: >-
      Single outer `group('Multi-agent GLP tests', () => { ... })` maps to
      a single PascalCase xUnit test class `MultiagentGlpTests`. The
      original group label MUST be preserved verbatim via `[Trait("Group",
      "Multi-agent GLP tests")]` on the class so reporter parity survives.
      The class contains: (a) a private `IsolateManager _manager = null!;`
      field — see dart.package_test.late_field_in_group; (b) a constructor
      assigning `_manager = new IsolateManager();` — see
      dart.package_test.setUp_block; (c) implements
      `IAsyncLifetime.DisposeAsync` to await `_manager.Shutdown()` — see
      dart.package_test.teardown_async_block; (d) a private async helper
      `private async Task RunGlpTest(string glpFile, int settleMs = 2000,
      bool traceGlp = false, bool traceMad = false, ISet<string>?
      traceAgents = null)` — see dart.function.nested_helper_with_default_args;
      (e) twelve `[Fact(...)]` methods, one per Dart `test(...)` call —
      see dart.package_test.test_call_async_with_timeout.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      No-nested-groups nuance (IDENTICAL to mad_scenarios_test.dart.md
      class-flattening but FLATTER): unlike boot_loader_test.dart.md which
      had THREE nested groups, this file has only the single outer group
      with no nested `group(...)` calls — so no `[Trait]`-per-subgroup
      mangling is needed and no inner-group prefixes appear on method
      names. Twelve sibling `test(...)` calls become twelve sibling
      `[Fact]` methods directly under the class. Helper-method nuance
      (LOAD-BEARING for THIS file, NEW relative to siblings): the Dart
      `Future<void> runGlpTest(...) async { ... }` is a NESTED FUNCTION
      declared INSIDE the group body (closing over `manager` and
      `traceConfig`), which is NOT valid C# (no nested-function-in-class
      syntax). The C# port LIFTS this helper to a `private async Task`
      INSTANCE METHOD on the test class — see
      dart.function.nested_helper_with_default_args for the full mapping.
  - construct_key: dart.package_test.late_field_in_group
    source_form: "late IsolateManager manager;"
    target_decision: >-
      Dart `late` field declared in the `group` callback (closed-over by
      setUp + tearDown + every test via the `runGlpTest` helper) maps to
      `private IsolateManager _manager = null!;` instance field on the
      xUnit test class. The field is assigned by the class constructor
      (the setUp mapping), so `null!` is the non-nullable "assigned-later"
      idiom that matches Dart's `late` semantics (initialised before any
      reader runs; throws if read uninitialised).
    idiom_id: rf-dart-late-field-to-csharp-nullforgiving-field
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Null-safety nuance (explicitly addressed, IDENTICAL to
      boot_loader_test.dart.md and ui_mediator_test.dart.md): Dart `late
      T x;` is a non-null `T` that throws `LateInitializationError` if
      read before assignment; the closest C# equivalent for an xUnit
      per-test field is `private T _x = null!;` (non-nullable reference,
      suppressed initialiser warning, assigned in the constructor).
      Because the xUnit constructor runs BEFORE every `[Fact]`, the
      `null!` is replaced before any reader runs — semantically
      equivalent to Dart `late + setUp`. Alternative `private T? _x;`
      (nullable + `!` at every read site) was REJECTED because it
      inverts the "guaranteed-initialised" contract that `late` encodes.
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { manager = IsolateManager(); });"
    target_decision: >-
      Dart synchronous `setUp` registered inside the outer group maps to
      the xUnit test class's CONSTRUCTOR body: `public MultiagentGlpTests()
      { _manager = new IsolateManager(); }`. xUnit instantiates the test
      class once per test method (constructor-per-test isolation), which
      matches `package:test`'s per-test fresh-state semantics exactly.
      NO `[SetUp]` attribute exists in xUnit (that is NUnit's idiom);
      using the constructor is the documented xUnit pattern
      (https://xunit.net/docs/shared-context, "Constructor and Dispose").
      The Dart constructor invocation `IsolateManager()` (without `new`)
      maps to C# `new IsolateManager()` (C# requires the `new` keyword
      mandatorily).
    idiom_id: rf-dart-setup-to-xunit-constructor
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (explicitly addressed, IDENTICAL to
      boot_loader_test.dart.md): `package:test`'s `setUp` is per-test and
      runs in the same isolate; xUnit's constructor is per-test and runs
      on the same thread — both give a fresh `_manager` per test,
      identical observable semantics. Async-setUp nuance (NEW relative to
      sibling specs that only have synchronous setUp): the Dart `setUp`
      HERE is synchronous (`setUp(() { manager = IsolateManager(); });`,
      no `async`); the test class constructor is therefore plain (no
      `IAsyncLifetime.InitializeAsync` needed). If a future test file
      had `setUp(() async { ... })`, the idiom would extend to
      `IAsyncLifetime.InitializeAsync` (recorded in the research finding
      for forward-compat — but NOT triggered HERE).
  - construct_key: dart.package_test.teardown_async_block
    source_form: "tearDown(() async { await manager.shutdown(); });"
    target_decision: >-
      Dart ASYNC `tearDown` registered inside the outer group maps to
      `IAsyncLifetime.DisposeAsync` on the xUnit test class. The class
      MUST declare `: IAsyncLifetime` (the xUnit per-test async-lifecycle
      interface — `https://xunit.net/docs/shared-context#async-lifecycle`).
      The `InitializeAsync` method is required-but-empty (the Dart
      setUp is synchronous — see preceding construct), and
      `DisposeAsync` awaits the shutdown: `public ValueTask
      DisposeAsync() { return new ValueTask(_manager.Shutdown()); }`
      OR equivalently `public async ValueTask DisposeAsync() { await
      _manager.Shutdown(); }`. The async form is more idiomatic; the
      ValueTask-wrap form avoids the `async` state-machine allocation
      for a single await. Codegen prefers the async form. NOTE: xUnit
      v3's `IAsyncLifetime.DisposeAsync` returns `ValueTask`; xUnit v2's
      returned `Task` — codegen MUST match the project's xUnit version.
      The SUT spec `lib/multiagent/isolate_manager.dart.md` pins
      `IsolateManager.Shutdown()` as returning `Task` (NOT
      `Task<Something>`, NOT `void`) — `await _manager.Shutdown()` is
      the only valid call shape.
    idiom_id: null
    research_finding_id: rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync
    nuance: >-
      Async-tearDown nuance (explicitly addressed, LOAD-BEARING and FIRST-
      RECORDED for the multiagent test specs — boot_loader_test.dart.md
      had no tearDown; mad_scenarios_test.dart.md had no setUp/tearDown
      at all; ui_mediator_test.dart.md had only synchronous setUp): the
      `IAsyncLifetime` interface (in `xunit.v3.core` / `xunit.core`) is
      the canonical xUnit mechanism for ASYNC per-test fixture
      initialization + disposal. The synchronous counterpart
      (`IDisposable.Dispose`) cannot await; using
      `_manager.Shutdown().GetAwaiter().GetResult()` to bridge sync→async
      is REJECTED as a deadlock risk on UI/SynchronizationContext-bound
      threads (the xUnit test-runner does not impose a SynchronizationContext
      by default, but the rule "never sync-wait on a Task in test
      teardown" is doc-pinned by Stephen Toub
      https://devblogs.microsoft.com/pfxteam/asyncawait-faq/#do-not-block-on-async-code).
      Both-interfaces nuance: a class MAY implement BOTH `IDisposable`
      AND `IAsyncLifetime` simultaneously; THIS file needs only the
      latter (no synchronous resources). The Dart `tearDown(() async {
      await manager.shutdown(); })` is async-equivalent only — there is
      no sync teardown. Authoritative Dart side: pub.dev `package:test`
      docs (https://pub.dev/packages/test) "Asynchronous tests" §setUp/
      tearDown. Authoritative .NET side: xUnit docs
      (https://xunit.net/docs/shared-context#async-lifecycle) — both
      sources authoritative.
  - construct_key: dart.function.nested_helper_with_default_args
    source_form: >-
      "Future<void> runGlpTest(String glpFile, {
         int settleMs = 2000,
         bool traceGlp = false,
         bool traceMad = false,
         Set<String>? traceAgents,
       }) async {
         final source = loadFile(glpFile);
         if (source == null) { print('Skipping: $glpFile not found'); return; }
         final loader = BootLoader();
         final config = loader.load(source);
         config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;
         final traceConfig = TraceConfig(glp: traceGlp, mad: traceMad, agents: traceAgents);
         await manager.boot(config, traceConfig: traceConfig);
         manager.start();
         await Future.delayed(Duration(milliseconds: settleMs));
         // Termination is external — shutdown happens in tearDown
       }"
    target_decision: >-
      Dart NESTED async function (declared INSIDE the group callback, NOT
      a class method) closing over `manager` from the outer scope — this
      shape has NO direct C# equivalent (C# has no nested-function-in-
      class-body syntax — only local functions inside a method body, or
      class members). HOIST to a `private async Task` INSTANCE METHOD on
      the test class. Specifically:
      `private async Task RunGlpTest(string glpFile, int settleMs = 2000,
      bool traceGlp = false, bool traceMad = false, ISet<string>?
      traceAgents = null) { ... }`. The Dart NAMED OPTIONAL parameters
      with defaults (`{ int settleMs = 2000, bool traceGlp = false, bool
      traceMad = false, Set<String>? traceAgents }`) map to C# OPTIONAL
      POSITIONAL parameters with default values (C# has no Dart-style
      "named-only" parameters — every C# parameter is positional and
      callable by name at the call site via `name: value` syntax). The
      nullable `Set<String>? traceAgents` (no default → defaults to `null`
      in Dart) maps to `ISet<string>? traceAgents = null`. Body
      translation:
      `var source = LoadFile(glpFile);
       if (source is null) { Console.WriteLine($"Skipping: {glpFile} not found"); return; }
       var loader = new BootLoader();
       var config = loader.Load(source);
       config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");
       var traceConfig = new TraceConfig { Glp = traceGlp, Mad = traceMad, Agents = traceAgents };
       await _manager.Boot(config, traceConfig: traceConfig);
       _manager.Start();
       await Task.Delay(TimeSpan.FromMilliseconds(settleMs));
       // Termination is external — shutdown happens in DisposeAsync`.
      The Dart cascade `..rootSelfGlpPath = ...` (NOT used here — direct
      `config.rootSelfGlpPath = ...` assignment) maps to a plain C#
      assignment `config.RootSelfGlpPath = ...`. The Dart `File(...)
      .absolute.path` (used IDENTICALLY in ui_mediator_test.dart.md)
      maps to `Path.GetFullPath(...)` (pinned by ui_mediator_test.dart.md
      `rf-dart-file-absolute-path-to-dotnet-path-getfullpath`).
    idiom_id: null
    research_finding_id: rf-dart-nested-helper-function-to-csharp-private-method
    nuance: >-
      Nested-function-vs-private-method nuance (explicitly addressed,
      LOAD-BEARING for THIS file): Dart's nested-function syntax permits
      DEFINING a function inside ANOTHER function (here, inside the
      `group` callback). The captured-variable model is
      lexical-scope-by-reference (the function captures `manager` by
      reference; mutating `manager` in another callback would be visible).
      C# has TWO local-function-like surfaces: (a) "local function"
      declared INSIDE a method body (Roslyn lowering — but our enclosing
      `group` callback does not survive the conversion; it is FLATTENED
      into the class scope per the group-block construct above); (b)
      private instance method on the test class (the chosen target).
      Option (b) is the correct conversion because the C# class scope IS
      the equivalent of the Dart group-callback closure scope — the
      `_manager` field is the equivalent of the captured Dart `manager`
      local. Default-values nuance: C# requires defaults to be
      compile-time constants — `2000` (int literal), `false` (bool
      literal), `null` (nullable-ref default) all satisfy this. Set<T>-vs-
      ISet<T> nuance: the Dart `Set<String>?` parameter is a
      polymorphic-acceptance pattern (any Set implementation is
      accepted); the C# port MUST use `ISet<string>?` (the
      System.Collections.Generic interface) to preserve the same
      polymorphism — a concrete `HashSet<string>?` would force callers
      to allocate a HashSet specifically. None of the twelve `test()`
      callers in this file passes the `traceAgents` argument, so the
      `null` default fires uniformly — but the interface-typed parameter
      is the FAITHFUL translation and is forward-compatible. Async-method
      nuance: Dart `Future<void> f(...) async { ... }` ⇒ C# `private
      async Task F(...) { ... }` (NOT `async void`; `async void` is
      reserved for event handlers and is incompatible with await chains).
      Authoritative Dart side: dart.dev language tour "Functions" §
      optional parameters (https://dart.dev/language/functions#parameters).
      Authoritative .NET side: Microsoft Learn "Optional arguments"
      (https://learn.microsoft.com/dotnet/csharp/programming-guide/
      classes-and-structs/named-and-optional-arguments).
  - construct_key: dart.expression.optional_set_string_parameter
    source_form: "Set<String>? traceAgents,"
    target_decision: >-
      Dart `Set<String>?` (a nullable polymorphic-Set interface) maps to
      C# `System.Collections.Generic.ISet<string>?` (the equivalent
      interface in the BCL). The concrete classes Dart `Set` factories
      may construct (`LinkedHashSet`, `HashSet`, etc.) map to C# `HashSet
      <string>` (the default Dart-`Set` is hash-based). Codegen MUST emit
      `using System.Collections.Generic;` at file scope (already added by
      the import-directive construct above).
    idiom_id: rf-dart-set-t-to-csharp-iset-t
    research_finding_id: rf-dart-set-t-to-csharp-iset-t
    nuance: >-
      Collection-interface nuance (explicitly addressed): Dart's `Set<T>`
      is the abstract interface (analogous to .NET `ISet<T>`); Dart's
      `Set.of(...)` / `Set.from(...)` factories return concrete
      `LinkedHashSet` (insertion-ordered hash set). .NET's `HashSet<T>`
      is unordered hash; for INSERTION-ORDERED iteration use `.NET
      HashSet<T>` with documented "implementation-detail-non-ordered"
      iteration, OR `SortedSet<T>` for sorted, OR `OrderedDictionary`
      patterns. For THIS file the order is irrelevant — `traceAgents` is
      a membership-test set ("trace these agents"), passed to
      `TraceConfig` and consulted by the runtime as a `.contains(agentId)`
      check, with no ordered iteration. `HashSet<string>` is therefore
      faithful. Polymorphism nuance: declaring the PARAMETER as
      `ISet<string>?` (not `HashSet<string>?`) preserves the Dart-side
      contract that any Set-typed value is accepted; the callers in this
      file pass only `null`, but the interface-typed parameter is
      forward-compatible. Nullable-reference nuance: `ISet<string>?`
      under NRT is a nullable reference; the SUT-side `TraceConfig.Agents`
      property is likewise nullable (per isolate_manager.dart.md
      TraceConfig pinning — see TraceConfig construct below).
  - construct_key: dart.constructor.trace_config_named_optional
    source_form: >-
      "TraceConfig(
         glp: traceGlp,
         mad: traceMad,
         agents: traceAgents,
       )"
    target_decision: >-
      Dart `TraceConfig(glp: ..., mad: ..., agents: ...)` named-argument
      constructor call maps to a C# OBJECT-INITIALIZER expression on the
      converted TraceConfig class. Per the SUT spec
      `isolate_manager.dart.md` line 131 — "public sealed class TraceConfig
      (init-only Glp/Mad/Agents properties + static readonly Off
      singleton)" — TraceConfig is pinned with INIT-ONLY properties. The
      Dart call site translates to `new TraceConfig { Glp = traceGlp, Mad
      = traceMad, Agents = traceAgents }`. Init-only properties are
      writable ONLY in the constructor or object-initializer, NEVER after,
      which faithfully models Dart's `const`-class final-fields semantics
      that the SUT spec pins.
    idiom_id: rf-dart-const-class-with-default-named-params-to-csharp-init-properties
    research_finding_id: rf-dart-const-class-with-default-named-params-to-csharp-init-properties
    nuance: >-
      Init-only-property nuance (explicitly addressed, REUSED VERBATIM
      from isolate_manager.dart.md line 248 — `rf-dart-const-class-with-
      default-named-params-to-csharp-init-properties`): Dart `const` /
      final-field classes with named-optional constructors map to C# init-
      only auto-properties (C# 9+ `init` accessor —
      https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/
      init). Object-initializer-vs-positional-ctor nuance: TraceConfig
      could ALSO have been mapped to a positional constructor `new
      TraceConfig(glp: traceGlp, mad: traceMad, agents: traceAgents)`
      preserving the Dart named-arg call shape, but the SUT spec
      explicitly chose the init-only-property + object-initializer
      pattern for the `static readonly Off = new TraceConfig();`
      singleton (an empty-init expression with all defaults). The
      object-initializer form at the call site is therefore the FAITHFUL
      translation of the SUT pinning. PascalCase nuance: Dart `glp`/`mad`/
      `agents` (camelCase fields) ⇒ C# `Glp`/`Mad`/`Agents` (PascalCase
      properties) per the .NET naming guideline.
  - construct_key: dart.method.isolate_manager_boot_named_arg
    source_form: "await manager.boot(config, traceConfig: traceConfig);"
    target_decision: >-
      Dart `await manager.boot(config, traceConfig: traceConfig)` maps to
      C# `await _manager.Boot(config, traceConfig: traceConfig);`. Per
      the SUT spec `isolate_manager.dart.md` line 134 — "public Task Boot
      (BootConfig config, TraceConfig? traceConfig = null) — async
      TaskCompletionSource pattern; listener-install BEFORE per-directive
      spawn loop; await readyTcs.Task" — Boot takes a positional
      `BootConfig config` and an optional named-or-positional `TraceConfig?
      traceConfig = null`. The Dart named-arg `traceConfig:` is preserved
      at the C# call site (C# named-arg syntax `traceConfig: ...` works
      identically to Dart on the call side). The `await` is necessary
      because `Boot` returns `Task` and the helper method is `async
      Task` itself.
    idiom_id: rf-dart-named-argument-to-csharp-named-argument
    research_finding_id: rf-dart-named-argument-to-csharp-named-argument
    nuance: >-
      Named-arg-at-call-site nuance (IDENTICAL to all sibling multiagent
      test specs): Dart `f(a, name: b)` ⇒ C# `F(a, name: b)` —
      identical syntax. Async-method invocation nuance: Dart `await
      future` ⇒ C# `await task` — identical syntax; both languages
      suspend the enclosing async function until the awaited task
      completes. TaskCompletionSource-Completer equivalence is pinned
      INSIDE the SUT spec (isolate_manager.dart.md
      `rf-dart-completer-to-csharp-taskcompletionsource`) — the TEST
      file just calls the externally-visible `Boot` method whose
      signature is already pinned.
  - construct_key: dart.method.isolate_manager_start_sync
    source_form: "manager.start();"
    target_decision: >-
      Dart `manager.start()` maps to C# `_manager.Start();`. Per the SUT
      spec `isolate_manager.dart.md` lines 69/137 — `public void Start()`
      — Start is SYNCHRONOUS, returns void, iterates the agent ports
      and sends a Start message to each. No `await` on the C# call
      because the method returns `void`.
    idiom_id: rf-dart-instance-method-call-to-csharp-pascalcase-call
    research_finding_id: rf-dart-instance-method-call-to-csharp-pascalcase-call
    nuance: >-
      Method-naming nuance (IDENTICAL to all sibling specs): Dart
      `start()` ⇒ C# `Start()` — camelCase to PascalCase. Sync-vs-async
      nuance (explicitly addressed): although the surrounding `runGlpTest`
      is `async`, `Start` itself is SYNCHRONOUS — codegen MUST NOT
      gratuitously `await` it (no Task to await).
  - construct_key: dart.expression.future_delayed_duration
    source_form: "await Future.delayed(Duration(milliseconds: settleMs));"
    target_decision: >-
      Dart `Future.delayed(Duration(milliseconds: ms))` returns a
      `Future<void>` that completes after `ms` milliseconds — the
      canonical "async sleep". The .NET counterpart is `Task.Delay
      (TimeSpan.FromMilliseconds(ms))` OR equivalently `Task.Delay(ms)`
      (the int overload accepts milliseconds directly). Codegen prefers
      the explicit `TimeSpan.FromMilliseconds(settleMs)` form for
      symmetry with the Dart `Duration(milliseconds: settleMs)` source
      shape, but the int-overload `Task.Delay(settleMs)` is observably
      equivalent and shorter. The full translation is `await Task.Delay
      (TimeSpan.FromMilliseconds(settleMs));`.
    idiom_id: null
    research_finding_id: rf-dart-future-delayed-to-dotnet-task-delay
    nuance: >-
      Async-sleep nuance (explicitly addressed, NEW for the multiagent
      test specs — boot_loader_test.dart.md / mad_scenarios_test.dart.md /
      ui_mediator_test.dart.md have NO async-sleep). Dart `Future.delayed`
      and .NET `Task.Delay` both schedule a continuation after a delay
      WITHOUT blocking the calling thread (cooperative timer). Both
      languages support optional cancellation: Dart's `Future.delayed`
      has no cancellation token (only `Timer.cancel` on the underlying
      timer object, which `Future.delayed` does not expose); .NET's
      `Task.Delay` accepts an optional `CancellationToken`. THIS file
      passes no cancellation; the C# port also passes no
      CancellationToken — preserving the "fire-and-await" Dart shape.
      Duration-construction nuance: Dart `Duration(milliseconds: <n>)`
      ⇒ C# `TimeSpan.FromMilliseconds(<n>)`; Dart `Duration(seconds:
      <n>)` ⇒ C# `TimeSpan.FromSeconds(<n>)` — used in the timeout
      construct below. Authoritative Dart side: dart.dev API reference
      `Future.delayed`
      (https://api.dart.dev/stable/dart-async/Future/Future.delayed.html).
      Authoritative .NET side: Microsoft Learn `Task.Delay`
      (https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay).
  - construct_key: dart.package_test.test_call_async_with_timeout
    source_form: >-
      "test('<label>', () async {
         await runGlpTest('<glpFile>');
       }, timeout: Timeout(Duration(seconds: 15)));"
    target_decision: >-
      Each Dart `test(label, body, {timeout})` with an `async` body and a
      `timeout: Timeout(Duration(seconds: 15))` named argument becomes:
      `[Fact(DisplayName = "<original label>", Timeout = 15000)] public
      async Task <MangledName>() { await RunGlpTest("<glpFile>"); }`. The
      Dart timeout `Timeout(Duration(seconds: 15))` (15 seconds) maps to
      xUnit `[Fact(Timeout = 15000)]` (15000 milliseconds — xUnit's
      Timeout property is in milliseconds; .NET TimeSpan-shaped overload
      is NOT supported on `[Fact]`). The original Dart label MUST be
      preserved verbatim via `[Fact(DisplayName = "<original label>")]`
      so the human-readable sentence form survives.  The twelve test
      labels in this file are:
      'shared variable: agent1 sends, agent2 receives',
      'imported reader: one-way list flow',
      'reversed flow: agent2 sends to agent1',
      'coop stream: producer + merge across agents',
      'two-hop flow: agent1 -> agent2 -> agent1 round-trip',
      'bidirectional exchange: symmetric send/receive',
      'three-agent pipeline: produce -> transform -> consume',
      'three-agent merge: two producers feed into one merger',
      'distribute: one producer broadcasts to two consumers',
      'minimal race: send unbound reader',
      'send reader: send unbound reader, instantiate later',
      'writer response: send writer, receiver writes back'.
      Mangled method names follow the same PascalCase-with-stripped-non-
      identifier rule as sibling specs:
      SharedVariableAgent1SendsAgent2Receives,
      ImportedReaderOneWayListFlow,
      ReversedFlowAgent2SendsToAgent1,
      CoopStreamProducerMergeAcrossAgents,
      TwoHopFlowAgent1Agent2Agent1RoundTrip,
      BidirectionalExchangeSymmetricSendReceive,
      ThreeAgentPipelineProduceTransformConsume,
      ThreeAgentMergeTwoProducersFeedIntoOneMerger,
      DistributeOneProducerBroadcastsToTwoConsumers,
      MinimalRaceSendUnboundReader,
      SendReaderSendUnboundReaderInstantiateLater,
      WriterResponseSendWriterReceiverWritesBack.
      Each method body is a single `await RunGlpTest("<glpFile>");` call.
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async-test nuance (explicitly addressed, NEW for the multiagent
      test specs at the GROUP-WIDE scale — sibling specs have at most
      one async test): every test in this file is async; every target
      method is `public async Task`, NOT `public void`. xUnit awaits
      the returned Task and treats it as the test's pass/fail signal.
      Timeout nuance (LOAD-BEARING, FIRST-RECORDED for the multiagent
      test specs): Dart `Timeout(Duration(seconds: 15))` ⇒ xUnit `[Fact
      (Timeout = 15000)]`. xUnit's `Timeout` property is on `FactAttribute`
      (and `TheoryAttribute`) since xUnit v2.4.0; it cancels the test if
      it has not completed by the timeout (note: requires the runner
      to support test cancellation — `xunit.runner.visualstudio` does;
      some older runners ignore it). The xUnit Timeout is in MILLISECONDS
      and is an `int` — `Duration(seconds: 15)` ⇒ `15 * 1000 = 15000`
      milliseconds. Closure-capture nuance: each Dart callback captures
      `runGlpTest` from the enclosing group scope; the C# translation
      captures `this.RunGlpTest` from the test-class instance, which is
      equivalent because the test method and the helper are siblings on
      the SAME class. No `skip:` argument anywhere — no `Skip=` property.
      Authoritative Dart side: pub.dev `package:test` docs
      (https://pub.dev/packages/test) "Timeouts". Authoritative .NET
      side: xUnit `FactAttribute.Timeout`
      (https://xunit.net/docs/comparisons#timeouts).
  - construct_key: dart.expression.final_local_variable_with_initializer
    source_form: >-
      "final source = loadFile(glpFile);
       final loader = BootLoader();
       final config = loader.load(source);
       final traceConfig = TraceConfig(glp: ..., mad: ..., agents: ...);"
    target_decision: >-
      Translate `final <name> = <expr>;` to `var <name> = <expr>;` in C#
      where the initializer is a method call, constructor invocation, or
      property access. Specifically:
      `final source = loadFile(glpFile)` ⇒ `var source = LoadFile(glpFile);`
      (inferred `string?`);
      `final loader = BootLoader()` ⇒ `var loader = new BootLoader();`
      (mandatory C# `new` keyword — Dart's optional-`new` constructor
      call requires C#'s explicit `new`);
      `final config = loader.load(source)` ⇒ `var config = loader.Load
      (source);` (PascalCased method name);
      `final traceConfig = TraceConfig(...)` ⇒ `var traceConfig = new
      TraceConfig { ... };` (object-initializer per
      dart.constructor.trace_config_named_optional above).
    idiom_id: rf-dart-final-local-to-csharp-var-local
    research_finding_id: rf-dart-final-local-to-csharp-var-local
    nuance: >-
      Immutability-semantics nuance (IDENTICAL to sibling specs): Dart
      `final <local>` prevents REBINDING the local after init but does
      NOT prevent mutation of the referenced object's state — exactly
      the same semantics as C# `var`. (C# `const` and `readonly` do not
      apply to local variables in the same way and are not the right
      mapping.) Type-inference nuance: every initializer in this file
      yields a type the C# compiler can infer — `LoadFile` returns
      `string?`, `new BootLoader()` returns `BootLoader`, `loader.Load(s)`
      returns `BootConfig`, `new TraceConfig { ... }` returns `TraceConfig`.
      Nullability nuance: only `source` is nullable; the subsequent
      `if (source is null) return;` narrows the type for the rest of the
      helper body, so subsequent uses can pass `source` as a `string`
      (non-nullable).
  - construct_key: dart.expression.config_field_mutation_after_construction
    source_form: "config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;"
    target_decision: >-
      Dart MUTATION of a `BootConfig` non-final field after construction:
      `config.rootSelfGlpPath = ...`. Per the SUT spec
      `boot_loader.dart.md` line 807 — "BootConfig non-final fields =>
      { get; set; } (NOT init)" — BootConfig's `RootSelfGlpPath` is a
      regular get/set property (NOT init-only) precisely so this caller
      can reassign it after the `BootLoader.Load(source)` factory call
      has returned the BootConfig with `RootSelfGlpPath = ""` placeholder.
      Translation: `config.RootSelfGlpPath = Path.GetFullPath
      ("../programs/self.glp");`. The right-hand side is the absolute
      path of the `programs/self.glp` file relative to the test working
      directory.
    idiom_id: rf-dart-file-absolute-path-to-dotnet-path-getfullpath
    research_finding_id: rf-dart-file-absolute-path-to-dotnet-path-getfullpath
    nuance: >-
      File.absolute.path nuance (REUSED VERBATIM from
      ui_mediator_test.dart.md `rf-dart-file-absolute-path-to-dotnet-path-
      getfullpath`): Dart's `File(relative).absolute.path` resolves a
      relative path against the current working directory and returns
      the canonical absolute path string. .NET's `Path.GetFullPath(relative)`
      does the same thing. Both throw on invalid path syntax; both use
      the OS-native separator at runtime. Mutable-field-after-factory
      nuance (LOAD-BEARING and explicitly addressed): the C# port chose
      `{ get; set; }` (NOT `{ get; init; }`) on `RootSelfGlpPath`
      specifically because of THIS caller pattern — the BootLoader
      factory returns the BootConfig with a placeholder
      RootSelfGlpPath, and the caller (this test, and the production
      caller `isolate_manager.dart`) reassigns it before passing to
      `Boot`. If `RootSelfGlpPath` had been `init`, this assignment
      would not compile — the SUT spec pinned `set` precisely to keep
      this contract. Authoritative .NET side: Microsoft Learn `Path.
      GetFullPath`
      (https://learn.microsoft.com/dotnet/api/system.io.path.getfullpath).
conversion_units:
  - "cu-1 file-scope using directives (Xunit + System + System.IO + System.Collections.Generic + System.Threading.Tasks + the SUT multiagent namespace from glp_runtime/multiagent/boot_loader.dart and glp_runtime/multiagent/isolate_manager.dart)"
  - "cu-2 namespace declaration mirroring the test/multiagent path"
  - "cu-3 top-level test class MultiagentGlpTests implementing IAsyncLifetime, decorated with [Trait(\"Group\", \"Multi-agent GLP tests\")]"
  - "cu-4 private IsolateManager _manager = null! field (late-field mapping)"
  - "cu-5 constructor MultiagentGlpTests() assigning _manager = new IsolateManager() (setUp mapping)"
  - "cu-6 IAsyncLifetime.InitializeAsync (empty body, returns ValueTask.CompletedTask) — required-but-no-op"
  - "cu-7 IAsyncLifetime.DisposeAsync awaiting _manager.Shutdown() (tearDown async mapping)"
  - "cu-8 private static string? LoadFile(string relativePath) — hoisted top-level helper (Path.Combine + File.Exists + File.ReadAllText + Console.WriteLine for not-found path)"
  - "cu-9 private async Task RunGlpTest(string glpFile, int settleMs = 2000, bool traceGlp = false, bool traceMad = false, ISet<string>? traceAgents = null) — hoisted nested-helper method (LoadFile + BootLoader.Load + Path.GetFullPath + new TraceConfig + Boot + Start + Task.Delay)"
  - "cu-10 twelve [Fact(DisplayName=...,Timeout=15000)] public async Task methods (one per Dart test() call), each awaiting RunGlpTest with the per-test glp-file path"
escalations: []
```

## B. Embedded human-readable rationale + provenance

This file is the **top-level integration test** for the multi-agent GLP
runtime. Each of its twelve tests loads a `.glp` source fixture, boots
the IsolateManager, lets event-driven execution settle, and relies on
external (tearDown) shutdown to terminate. There are NO assertions on
content — the tests are "do these boot+settle+shutdown sequences
complete without throwing?" smokes for the full multiagent pipeline.
Every conversion decision below has been made under the constraint
that the boot+start+settle+shutdown lifecycle survives the port with
identical observable behavior (per-test fresh `IsolateManager`,
awaitable shutdown in teardown).

### rf-dart-package-test-to-dotnet-xunit — project-wide xUnit framework choice

REUSED VERBATIM from sibling specs. xUnit is the project-wide test
framework. Authoritative Dart side:
[dart.dev `package:test`](https://dart.dev/tools/dart-test#the-package-test-package).
Authoritative .NET side:
[Microsoft Learn xUnit + .NET Test Sdk](https://learn.microsoft.com/dotnet/core/testing/unit-testing-with-dotnet-test).

### rf-dart-package-sut-import-to-csharp-using — one-namespace SUT import collapse

This file imports TWO SUT files (`boot_loader.dart` +
`isolate_manager.dart`), collapsing to ONE `using <RootNs>.Multiagent;`
directive. Both SUT specs already pin the same target namespace.

### rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync — FIRST RECORDED for the multiagent test specs

The `tearDown(() async { await manager.shutdown(); });` is a load-bearing
ASYNC teardown — none of the sibling multiagent test specs exercises
the async-teardown pattern. The mapping is to xUnit's `IAsyncLifetime`
interface (which provides `InitializeAsync` + `DisposeAsync`).
`InitializeAsync` is required but empty here (setUp is synchronous);
`DisposeAsync` awaits `_manager.Shutdown()`. Alternatives REJECTED:
(a) `IDisposable.Dispose` + `Shutdown().GetAwaiter().GetResult()` —
deadlock risk; (b) "fire-and-forget" `_ = Shutdown()` — loses the
"awaited before next test starts" contract that the Dart `tearDown(()
async { ... })` provides. The chosen mapping is the SOLE faithful
translation. Authoritative .NET side: xUnit shared-context docs
(https://xunit.net/docs/shared-context#async-lifecycle).

### rf-dart-nested-helper-function-to-csharp-private-method — FIRST RECORDED for the multiagent test specs

The Dart `Future<void> runGlpTest(...) async { ... }` is a NESTED
FUNCTION declared inside the `group` callback. C# has no equivalent for
nested-function-in-class-body — the conversion HOISTS the helper to a
`private async Task` instance method on the test class. The closure-
captured `manager` from the Dart group scope becomes the `_manager`
instance field. This is THE faithful translation; ALTERNATIVE local-
function-inside-each-test-method was REJECTED because it would
duplicate the helper body twelve times.

### rf-dart-future-delayed-to-dotnet-task-delay — async sleep

`Future.delayed(Duration(milliseconds: settleMs))` ⇒
`Task.Delay(TimeSpan.FromMilliseconds(settleMs))`. Authoritative Dart
side: Dart API
[`Future.delayed`](https://api.dart.dev/stable/dart-async/Future/Future.delayed.html).
Authoritative .NET side: Microsoft Learn
[`Task.Delay`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay).

### rf-dart-set-t-to-csharp-iset-t — Set<T> ⇒ ISet<T>

The `Set<String>? traceAgents` parameter maps to `ISet<string>?
traceAgents = null`. Polymorphic interface preserved on the C# side.
None of the twelve callers passes the argument; the default fires
uniformly.

### rf-dart-top-level-function-to-csharp-static-method-on-test-class — FIRST RECORDED for the multiagent test specs

`String? loadFile(String relativePath)` is a TOP-LEVEL function in
the Dart source (outside `main`). C# has no top-level functions
outside the `Program.cs` top-level-program file, and a TEST file is
NOT that. The helper is hoisted to a `private static string?
LoadFile(string)` method on the test class.

### Twelve test method bodies — uniform shape

Every test body is a single `await RunGlpTest("<glpFile>");` call.
Twelve `[Fact(DisplayName = "<label>", Timeout = 15000)] public async
Task <MangledName>()` methods. The bodies are structurally identical;
only the `<glpFile>` path string and the label differ.

### Inherited escalations

This file does NOT re-escalate the heap_fcp threading-model question
(inherited from sibling SUT specs `heap_fcp.dart.md`, `runner.dart.md`,
`body_kernels.dart.md`, `scheduler.dart.md`, `system_predicates_impl.
dart.md`, `mad_context.dart.md`). Per the FR-013 "don't double-
escalate" discipline (also followed by mad_cold_call_isolate_test.dart.
md and ui_mediator_test.dart.md), this CONSUMER test file is silent
on the threading-model decision — the decision belongs to the SUT
spec for `isolate_manager.dart` / `heap_fcp.dart`; the test file's
calls to `_manager.Boot(...)` / `_manager.Start()` /
`_manager.Shutdown()` invoke whichever shape the SUT escalation
resolves to.

### KB cache hits (no re-research)

All of the following pinned rf-ids were KB cache hits — re-research was
NOT performed (FR-024 reproducibility-offline rule):
`rf-dart-package-test-to-dotnet-xunit`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-test-main-to-xunit-class-with-facts`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-late-field-to-csharp-nullforgiving-field`,
`rf-dart-setup-to-xunit-constructor`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-final-local-to-csharp-var-local`,
`rf-dart-named-argument-to-csharp-named-argument`,
`rf-dart-instance-method-call-to-csharp-pascalcase-call`,
`rf-dart-const-class-with-default-named-params-to-csharp-init-properties`,
`rf-dart-file-absolute-path-to-dotnet-path-getfullpath`,
`rf-dart-dart-io-to-dotnet-systemio`,
`rf-dart-set-t-to-csharp-iset-t`.

### Newly recorded rf-ids (defined by this file's first-use rows)

- `rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync` —
  async tearDown mapping to xUnit IAsyncLifetime.DisposeAsync.
- `rf-dart-nested-helper-function-to-csharp-private-method` —
  Dart group-scoped nested helper function hoisted to private
  instance method on the test class.
- `rf-dart-future-delayed-to-dotnet-task-delay` — async-sleep
  mapping.
- `rf-dart-top-level-function-to-csharp-static-method-on-test-class` —
  Dart top-level function hoisted to static method on the enclosing
  test class.

### Out-of-scope but recorded

- Project-system wiring (`<ProjectReference>` from the test .csproj to
  the runtime .csproj) is langpair-level; recorded so codegen knows
  the `using` alone is insufficient without the project reference.
- The exact `<RootNs>` placeholder is langpair-level and pinned at the
  workspace level, not per file.
- `BootLoader.Load` / `BootConfig.RootSelfGlpPath` / `IsolateManager.Boot`
  / `IsolateManager.Start` / `IsolateManager.Shutdown` / `TraceConfig`
  C# signatures live in their respective SUT convspecs (`lib/multiagent/
  boot_loader.dart.md`, `lib/multiagent/isolate_manager.dart.md`); THIS
  spec depends on those signatures but does not redefine them.
- The xUnit version pinning (v2 → `Task` from DisposeAsync; v3 →
  `ValueTask`) is workspace-level; THIS spec records both forms.

### No escalations

This file's constructs all resolve via the decision-order in
`convspec_idiom_schema.md`: every construct row records EITHER a pinned
idiom_id (KB cache hit) OR a research_finding_id with authoritative
Dart+.NET citations (the four new rf-ids above). No `idiom_vs_research`
conflicts; no `idiom_vs_idiom` conflicts; no undecidable points.
The threading-model question is INHERITED from sibling SUT spec
escalations (heap_fcp et al.), NOT re-escalated here.
`open_escalation_count = 0` ⇒ file is `specced`, NOT escalated.
