# Conversion Spec — test/multiagent/isolate_manager_test.dart

> Conversion-spec artifact for test/multiagent/isolate_manager_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> **This test directly exercises the just-escalated SUT
> `lib/multiagent/isolate_manager.dart`** (the central threading-model
> decision point — see that file's `escalations[0]`). Every construct in
> THIS file that touches `IsolateManager.boot` / `IsolateManager.start` /
> `IsolateManager.shutdown` / `TraceConfig` is a CONSUMER of the SUT's
> threading-model ruling. Per FR-013 ("escalate, don't guess") and the
> "don't double-escalate" discipline established by the sibling
> `mad_cold_call_isolate_test.dart.md` convspec, this file INHERITS the
> escalation rather than re-issuing it. Where the test code directly
> awaits `manager.boot(...)`, calls `manager.start()`, or awaits
> `manager.shutdown()`, the spec records `target_decision: "DEFERRED — see
> lib/multiagent/isolate_manager.dart escalations[0]"` and preserves the
> Dart-side behavioural contract verbatim so codegen can encode the chosen
> option once the ruling lands. No NEW escalations are introduced here
> (this test exposes no genuinely-local undecidable point — every
> non-deferred construct resolves via cached test-conversion idioms).

```yaml
schema_version: 1
source_path: test/multiagent/isolate_manager_test.dart
source_sha256: 431a81bed721f5801c63d58ef60dc4af936f654617fd1a7f4aaab28dcfd30da0
target_code_unit: test/multiagent/IsolateManagerTest.cs
constructs:
  - construct_key: dart.import.dart_io_core_library
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the `dart:io` line entirely. The single load-bearing symbol
      used in this file is `File` (constructor + `existsSync()` +
      `readAsStringSync()` + `.absolute.path`) — all maps to the .NET
      counterpart `System.IO.File` (static class) and `System.IO.Path`
      under `using System.IO;`. The `File('...')` Dart constructor maps to
      passing the path string directly to the static methods (see
      dart.io.file_existence_check_and_read_or_skip_helper below). NOT a
      direct line-for-line `using` — Dart `dart:io` aggregates multiple
      concerns (file I/O, sockets, process, stdin/stdout); only the file
      subset is exercised here.
    idiom_id: null
    research_finding_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    nuance: >-
      Standard-library import nuance (explicitly addressed): Dart's
      `dart:io` is a CORE library aggregating file I/O, sockets, process,
      env, stdin/stdout (https://api.dart.dev/stable/dart-io/dart-io-library.html).
      The .NET counterpart is multiple `System.*` namespaces — for this
      file ONLY `System.IO` is needed (File + Path). Sync-vs-async nuance:
      this file uses the SYNC variants (`existsSync`, `readAsStringSync`)
      — maps to the SYNC .NET counterparts (`File.Exists`, `File.ReadAllText`),
      NOT `File.ReadAllTextAsync` (would require turning the helper into
      async Task<string?>). Path-resolution nuance: `File('../...').absolute.path`
      maps to `Path.GetFullPath("../...")` (Microsoft Learn — Path.GetFullPath
      resolves relative paths against the current working directory). NOT
      `Path.Combine` (which composes path segments but does not resolve
      ".." traversal against CWD).
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit pinned project-wide by
      every prior test-file convspec (test/multiagent/mad_error_handling_test.dart.md,
      test/multiagent/boot_loader_test.dart.md,
      test/multiagent/mad_cold_call_isolate_test.dart.md, etc. — FR-012 /
      SC-007 KB hit, no re-research, no re-derivation). Codegen MUST also
      add `using System;` (Action/Exception/Disposable), `using System.IO;`
      (File/Path — see dart.import.dart_io_core_library above), `using
      System.Threading;` (CancellationToken — only if the SUT
      isolate-manager port's ruling introduces it; deferred shape), and
      `using System.Threading.Tasks;` (Task / Task.Delay) for the
      `Future.delayed` mappings below. Target namespace mirrors the Dart
      `test/multiagent` directory (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection nuance (cache hit — project-wide pin):
      xUnit's constructor-per-test isolation matches `package:test`'s
      fresh-state semantics; `[Fact]`/`[Theory]` map 1:1 onto Dart
      `test()`/parameterised `test()`; xUnit is the modern .NET default.
      NUnit and MSTest recorded as corroborating alternatives in the
      research finding only.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/multiagent/boot_loader.dart';
       import 'package:glp_runtime/multiagent/isolate_manager.dart';"
    target_decision: >-
      Map both to a SINGLE C# `using <RootNs>.Multiagent;` directive
      (multiple Dart `package:` URIs that collapse to the same converted
      namespace coalesce to one `using` per the project-wide cached idiom).
      The SUT namespace string is decided when each SUT file is converted;
      THIS spec records the shape of the cross-file dependency, not the
      SUT namespace strings. Names referenced from these imports —
      `BootLoader` (constructor + `Load(string)` method per
      lib/multiagent/boot_loader.dart.md), `IsolateManager` (constructor +
      `Boot`/`Start`/`Shutdown`/`InjectUIEvent` per the JUST-ESCALATED
      lib/multiagent/isolate_manager.dart.md), `TraceConfig` (init-only
      property class per same SUT spec, with `Glp` and `Mad` named-arg
      construction).
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (cache hit): in Dart `package:glp_runtime/...`
      is an explicit pubspec-anchored URI; in C# there is no per-file URI
      — only assembly + namespace. The conversion must (a) ensure the
      converted SUT lives in a deterministic namespace derived from its
      relative path, and (b) ensure the test assembly references the SUT
      assembly via the project file (project-system idiom, out of scope
      for THIS artifact). No `as` alias / `show` / partial import is used.
      DEFERRED-API-SURFACE nuance: the `IsolateManager` / `TraceConfig`
      symbols' C# shapes (in particular every method that touches the
      mailbox/port API on `IsolateManager`) are pending the SUT escalation
      resolution — see DEFERRED constructs below.
  - construct_key: dart.toplevel_const.string_directory_path
    source_form: "const _socialGraphDir = '../programs/typed_book/social_graph';"
    target_decision: >-
      Map to a `private const string SocialGraphDir = "../programs/typed_book/social_graph";`
      field on the enclosing test class (xUnit cannot host top-level
      `const` declarations file-scoped at C# 10+; the modern shape is a
      private const field inside the test class). The Dart top-level
      private (`_`-prefixed) constant maps to `private const` on the test
      class — the underscore-prefix-implies-library-private Dart idiom
      maps to `private` accessibility in C# (cached from
      lib/runtime/internals.dart.md). PascalCase the identifier
      (`SocialGraphDir`) per the project-wide Dart-camelCase ⇒ C#-PascalCase
      idiom — REUSED verbatim across every convspec.
    idiom_id: rf-dart-toplevel-const-string-to-csharp-private-const
    research_finding_id: rf-dart-toplevel-const-string-to-csharp-private-const
    nuance: >-
      Compile-time-string-constant nuance (explicitly addressed): Dart
      top-level `const` strings are compile-time interned single-allocation
      literals; C# `const string` fields are also compile-time interned
      literals (CLR string-interning). Both are baked into the call site
      at compile time. NOT `static readonly` (would defer the allocation
      to runtime first-access — not faithful to Dart `const`-time
      interning). Path-separator nuance: the literal uses forward slashes
      (`/`); .NET on Windows accepts forward slashes in path APIs
      (Microsoft Learn — System.IO.Path), so no rewrite is needed.
  - construct_key: dart.toplevel_function.string_nullable_return_skip_helper
    source_form: >-
      "String? _readGlpFile(String filename) { final file =
      File('$_socialGraphDir/$filename'); if (!file.existsSync()) { print(
      'Skipping: $filename not found at ${file.path}'); return null; }
      return file.readAsStringSync(); }"
    target_decision: >-
      Map to a `private static string? ReadGlpFile(string filename) { ... }`
      static method on the enclosing test class. Dart `String?` (nullable
      string) ⇒ C# `string?` (nullable reference type — requires NRT
      enabled at project level, which is the project-wide default per the
      shared workspace `Directory.Build.props` recorded in every prior
      convspec). The body: `var path = Path.Combine(SocialGraphDir,
      filename); if (!File.Exists(path)) { Console.WriteLine($"Skipping:
      {filename} not found at {path}"); return null; } return
      File.ReadAllText(path);`. The Dart `'$_socialGraphDir/$filename'`
      string interpolation maps to `Path.Combine(...)` (the documented
      cross-platform path-concat — Microsoft Learn 'Path.Combine'). The
      Dart `file.path` accessor maps to the same `path` local in C# (the
      File static class has no instance `Path` getter; the path-string is
      what was passed in). `File.existsSync()` ⇒ `File.Exists(path)`
      (Microsoft Learn 'File.Exists'). `File.readAsStringSync()` ⇒
      `File.ReadAllText(path)` (Microsoft Learn 'File.ReadAllText').
      `print(...)` ⇒ `Console.WriteLine(...)` (cached idiom from every
      prior test convspec).
    idiom_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    research_finding_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    nuance: >-
      File-API nuance (explicitly addressed): Dart `File` is an instance
      handle bound to a path that exposes `.existsSync()` /
      `.readAsStringSync()` / `.path` — instance-style. .NET `System.IO.File`
      is a STATIC class with static methods that take the path as a
      parameter; there is no instance `File` handle for this style of
      file-existence-then-read. The faithful translation collapses the
      Dart `var file = File(path); if (!file.existsSync())` two-step
      into the single `if (!File.Exists(path))` and reads back via
      `File.ReadAllText(path)`. Async-vs-sync nuance: the Dart code uses
      `existsSync` and `readAsStringSync` (SYNC); the C# port uses the
      SYNC counterparts (`File.Exists` / `File.ReadAllText`), NOT the
      async (`File.ExistsAsync` does not exist; `File.ReadAllTextAsync`
      exists but is not needed and would require changing the helper's
      return type to `Task<string?>`). Path-interpolation-vs-Path.Combine
      nuance: Dart's `'$dir/$file'` is a simple string concat that
      assumes forward slash; .NET `Path.Combine(dir, file)` is the
      documented cross-platform helper that uses the platform separator
      (`\` on Windows, `/` elsewhere) — codegen prefers Path.Combine for
      idiom; bare string concat would work on .NET because the API
      accepts both separators on Windows but Path.Combine is the
      reviewable shape. Console-output-skip nuance: the `print('Skipping:
      ...')` is a TEST diagnostic, NOT a production trace — emit
      Console.WriteLine (NOT `_log` or `ITestOutputHelper.WriteLine`,
      since this helper is called from per-test contexts whose
      `ITestOutputHelper` would need injection through closure — too
      heavy for a static helper).
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('IsolateManager', () { ... }); }"
    target_decision: >-
      Drop `main` entirely. xUnit discovers `[Fact]` methods by reflection
      — no per-file entrypoint. The single statement inside `main` (the
      outer `group('IsolateManager', ...)`) becomes the enclosing test
      class (see dart.package_test.group_block below). Cached idiom from
      boot_loader_test.dart.md / mad_cold_call_isolate_test.dart.md.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (cache hit, restated): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook — only per-class
      (constructor + IDisposable.Dispose) and per-collection fixtures.
      THIS file's `main` body is exactly one `group(...)` call, so the
      omission is lossless.
  - construct_key: dart.package_test.group_block
    source_form: >-
      "group('IsolateManager', () { late IsolateManager manager; setUp(...);
      tearDown(... async ...); test(...); test(...); test(...); });"
    target_decision: >-
      Map to a `public class IsolateManagerTests : IAsyncLifetime` (xUnit
      v2/v3 — IAsyncLifetime provides `InitializeAsync` and `DisposeAsync`
      that bracket every `[Fact]`). The class name is the PascalCase of
      the outer group label (`IsolateManager` ⇒ class `IsolateManagerTests`),
      with original label preserved via `[Trait("Group", "IsolateManager")]`.
      Three tests, all instance methods. NO nested groups — the FLATTEN
      topology recorded in boot_loader_test.dart.md is not exercised here
      (single outer group, no inner groups).
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (cache hit): arbitrary string label ⇒ PascalCase
      identifier; original preserved via `[Trait]`. Async-teardown-driver
      nuance (LOAD-BEARING, EXPLICITLY ADDRESSED): this file has BOTH
      `setUp(() { ... })` AND `tearDown(() async { ... })` — the async
      tearDown is what forces `IAsyncLifetime` over the simpler
      constructor+IDisposable pattern (which would not allow `await
      manager.shutdown()` in Dispose). xUnit `IAsyncLifetime` is the
      documented mechanism (https://xunit.net/docs/shared-context —
      "Constructor and Dispose" + IAsyncLifetime variant). The single test
      class implements `IAsyncLifetime` with `InitializeAsync` (assigns
      `_manager = new IsolateManager();` — note this is sync but the
      interface forces a Task return; emit `return Task.CompletedTask;`)
      and `DisposeAsync` (awaits the SUT's `Shutdown` — deferred shape;
      see dart.package_test.tearDown_async below).
  - construct_key: dart.package_test.late_field_in_group
    source_form: "late IsolateManager manager;"
    target_decision: >-
      Map to `private IsolateManager _manager = null!;` instance field on
      the xUnit test class. The `null!`-initialised field is the
      documented C# pattern for "assigned in InitializeAsync, throws if
      read uninitialised" — matches Dart `late` semantics. Cached idiom
      from boot_loader_test.dart.md / mad_cold_call_isolate_test.dart.md.
    idiom_id: rf-dart-late-field-to-csharp-nullforgiving-field
    research_finding_id: rf-dart-late-field-to-csharp-nullforgiving-field
    nuance: >-
      Null-safety nuance (cache hit): Dart `late T x;` ⇒ `private T _x =
      null!;`. Initialised in `InitializeAsync` BEFORE any test body
      reads it — semantically equivalent to Dart `late + setUp`. The
      alternative `private IsolateManager? _manager;` + `!`-dereference at
      every read site was REJECTED across the project (cached); it
      inverts the "guaranteed-initialised" contract that `late` encodes.
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { manager = IsolateManager(); });"
    target_decision: >-
      Map to `public Task InitializeAsync() { _manager = new
      IsolateManager(); return Task.CompletedTask; }` on the test class.
      xUnit `IAsyncLifetime.InitializeAsync` is called per-test (xUnit
      constructs the class once per test method AND drives the lifecycle
      hooks) — semantically equivalent to `package:test`'s `setUp`. The
      body is synchronous (no await) but the interface forces a `Task`
      return — `Task.CompletedTask` is the documented idiom (Microsoft
      Learn 'Task.CompletedTask' — "Gets a task that has already
      completed successfully."). The `new IsolateManager()` call is
      synchronous (the SUT's constructor is parameterless and has no
      async work, per the SUT convspec). Cached from
      mad_cold_call_isolate_test.dart.md (which uses constructor-only;
      this file uses IAsyncLifetime due to the async tearDown).
    idiom_id: rf-dart-setup-async-to-xunit-iasynclifetime-initializeasync
    research_finding_id: rf-dart-setup-async-to-xunit-iasynclifetime-initializeasync
    nuance: >-
      Lifecycle nuance (explicitly addressed): `package:test`'s `setUp` is
      per-test; xUnit `IAsyncLifetime.InitializeAsync` is per-test. Both
      give a fresh `_manager` per test, identical observable semantics.
      Sync-body-Task-return nuance: when the Dart setUp body has no
      `async` (this file's setUp is synchronous), the .NET counterpart
      either (a) returns `Task.CompletedTask` from a non-async method
      (preferred — no state machine generation) or (b) declares the
      method `async Task` with no awaits (compiler-generated state
      machine, semantically equivalent). Codegen prefers (a) — cached
      from lib/multiagent/isolate_manager.dart.md's Shutdown construct
      (rf-dart-async-no-await-to-csharp-task-completedtask).
  - construct_key: dart.package_test.tearDown_async
    source_form: "tearDown(() async { await manager.shutdown(); });"
    target_decision: >-
      DEFERRED — see lib/multiagent/isolate_manager.dart escalations[0].
      The xUnit `public async Task DisposeAsync() { await
      _manager.Shutdown(); }` STRUCTURE is preserved across all four
      threading-model options (each option's Shutdown body is what
      varies — closing the main port + clearing _agentPorts; per the SUT
      convspec under DEFERRED). xUnit `IAsyncLifetime.DisposeAsync`
      (https://xunit.net/docs/shared-context) is the documented async-
      teardown mechanism. The await on `_manager.Shutdown()` is preserved
      verbatim — the SUT convspec records `Shutdown()` returns `Task`
      across all four options (deferred body, but the signature is
      stable). The xUnit lifecycle calls DisposeAsync after each test
      regardless of pass/fail — semantically equivalent to Dart `tearDown`
      (https://pub.dev/packages/test).
    idiom_id: rf-dart-teardown-async-to-xunit-iasynclifetime-disposeasync
    research_finding_id: rf-dart-teardown-async-to-xunit-iasynclifetime-disposeasync
    nuance: >-
      Async-teardown nuance (LOAD-BEARING, explicitly addressed): the
      `await manager.shutdown()` MUST be awaited or the test process
      leaks the underlying ports/threads (per the SUT convspec's
      "External-termination-contract" note: agents self-terminate when
      their inbound port closes — the shutdown is what closes that
      port). xUnit `IAsyncLifetime.DisposeAsync` awaits the returned
      Task before constructing the next test instance. NOT `Dispose()`
      (the non-async IDisposable.Dispose has no Task-await capability
      — would force `.GetAwaiter().GetResult()` and risk deadlock under
      a SynchronizationContext). The SUT's `Shutdown` signature is
      stable across all four threading-model options (returns `Task`);
      ONLY the body is deferred. Inherits-not-re-escalates: per FR-013
      and the precedent of mad_cold_call_isolate_test.dart.md, this
      construct does NOT issue a new escalation — it inherits
      lib/multiagent/isolate_manager.dart escalations[0]. Behavioural
      contract preserved: the test relies on the SUT's "no Isolate.kill,
      ports closed, agents self-exit" semantics (recorded canonically in
      the SUT convspec); codegen MUST NOT introduce CancellationToken
      cancellation or Thread.Abort in DisposeAsync.
  - construct_key: dart.package_test.test_call_async_with_timeout
    source_form: >-
      "test('boots three agents from boot config', () async { /* body
      using triple-quoted source literal, BootLoader.load, manager.boot,
      manager.start, await Future.delayed(...) */ }, timeout: Timeout(
      Duration(seconds: 10)));"
    target_decision: >-
      Map to a `[Fact(DisplayName = "boots three agents from boot
      config")] [Trait("Group", "IsolateManager")] public async Task
      BootsThreeAgentsFromBootConfig()` instance method on
      `IsolateManagerTests`. Dart `() async { ... }` ⇒ C# `async Task`.
      The `timeout: Timeout(Duration(seconds: 10))` Dart per-test timeout
      maps to xUnit's `[Fact(Timeout = 10000)]` (xUnit v2 `Timeout`
      property — millisecond value; https://xunit.net/docs/comparisons —
      timeout support). For xUnit v3 (newer), `[Fact(Timeout = 10_000)]`
      remains the documented mechanism. Body: triple-quoted source ⇒ C#
      11 raw string literal (cached from boot_loader_test.dart.md); SUT
      method calls are DEFERRED (see DEFERRED constructs below); `await
      Future.delayed(Duration(milliseconds: 200))` ⇒ `await
      Task.Delay(TimeSpan.FromMilliseconds(200))` (Microsoft Learn
      'Task.Delay'). NO `expect(...)` assertions are present in THIS test
      — the docstring says "Test verifies boot + start don't crash with
      trivial goals" (so the assertion is implicit-no-exception, matching
      xUnit's pass-if-no-throw default).
    idiom_id: rf-dart-test-async-with-timeout-to-xunit-fact-timeout
    research_finding_id: rf-dart-test-async-with-timeout-to-xunit-fact-timeout
    nuance: >-
      Async-test nuance (cache hit): xUnit awaits the returned `Task`
      per https://xunit.net/docs/async-tests; failures during the await
      surface as test failures with full stack traces. Timeout-attribute
      nuance (explicitly addressed): xUnit `[Fact(Timeout = N)]` cancels
      the test after N milliseconds — the test is reported as a failure
      with "Test exceeded the configured timeout of N ms". Dart
      `Timeout(Duration(seconds: 10))` has equivalent observable
      semantics. NOT `CancellationToken` wired through every await
      (xUnit handles the cancellation at the runner level). Implicit-
      no-assertion-pass nuance: xUnit considers a test PASSED if the
      method returns without throwing; Dart `package:test` is the same.
      So the "test body has no `expect` calls" pattern translates 1:1.
      Future.delayed-nuance: Dart `Future.delayed(Duration)` ⇒ .NET
      `Task.Delay(TimeSpan)` — identical observable semantics, both
      cooperatively yield to the scheduler.
  - construct_key: dart.string.triple_quoted_multiline_glp_source_literal
    source_form: >-
      "final source = '''\nprocedure boot.\nboot :-\n
      agent_init(alice, _)@alice,\n    agent_init(bob, _)@bob,\n
      agent_init(charlie, _)@charlie.\n\nprocedure agent_init(_?, _?).\n
      agent_init(_, _) :- true.\n''';"
    target_decision: >-
      Map to a C# 11 raw string literal `var source = """ ... """;` with
      the closing `"""` emitted at column 0 (or matching the desired
      indentation of the payload) so the literal content is byte-identical
      to the Dart source. Cached from boot_loader_test.dart.md. Pre-C#-11
      fallback: `@"..."` verbatim string with no escape processing — both
      faithful for THIS payload (which contains no `"` characters).
    idiom_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    research_finding_id: rf-dart-triple-quoted-string-to-csharp-raw-string
    nuance: >-
      String-literal nuance (cache hit): Dart triple-quoted preserves
      literal `\n`/`\t` (no escape processing); C# raw strings (`"""`)
      also do not process escapes; C# verbatim strings (`@"..."`) do not
      process escapes but DO require `""` for embedded `"` (not present
      in this payload). Whitespace nuance: Dart triple-quoted preserves
      leading whitespace exactly; C# raw strings strip a common indent
      matched to the closing `"""` column — codegen MUST place the
      closing `"""` at the appropriate column so the literal is byte-
      identical.
  - construct_key: dart.sut.bootloader_load_and_set_rootselfglppath
    source_form: >-
      "final loader = BootLoader(); final config = loader.load(source);
      config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;"
    target_decision: >-
      Map to `var loader = new BootLoader(); var config = loader.Load(source);
      config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");`.
      `BootLoader` and `BootLoader.Load` decisions are owned by
      lib/multiagent/boot_loader.dart.md (cached from
      boot_loader_test.dart.md — `_loader = new BootLoader()`,
      `_loader.Load(source)` returns a `BootConfig`). `BootConfig.RootSelfGlpPath`
      is a settable string property per the same SUT spec (the Dart
      source-form `config.rootSelfGlpPath = ...` writes the property —
      C# init-only would block this; the SUT convspec records this as a
      mutable get/set property, NOT init-only). Dart `File(path).absolute.path`
      maps to `Path.GetFullPath(path)` (Microsoft Learn — Path.GetFullPath
      "Returns the absolute path for the specified path string. Relative
      paths are resolved against the current working directory."). NOT
      `Path.IsPathRooted` + manual rebuild — `GetFullPath` is the
      documented single-call counterpart. PascalCase at every call site.
    idiom_id: rf-dart-file-absolute-path-to-csharp-path-getfullpath
    research_finding_id: rf-dart-file-absolute-path-to-csharp-path-getfullpath
    nuance: >-
      Path-resolution nuance (explicitly addressed): Dart `File(path).absolute`
      returns a `File` whose `.path` getter is the canonicalised absolute
      path resolved against the current working directory; .NET
      `Path.GetFullPath(path)` does the same (https://learn.microsoft.com/
      dotnet/api/system.io.path.getfullpath). Both implementations resolve
      `..` segments against CWD at call time. Mutable-config-property
      nuance: the assignment `config.rootSelfGlpPath = ...;` is OBSERVED
      to be mutable; the SUT convspec for boot_loader.dart MUST therefore
      pin `RootSelfGlpPath` as `public string RootSelfGlpPath { get; set; }`
      (NOT init-only). If a future SUT-side decision flips it to init-only,
      this test would have to switch to constructor-arg/object-initialiser
      form — recorded as a forward-compatibility note in the cached
      idiom, not an escalation here.
  - construct_key: dart.sut.bootloader_load_with_sharedsources_list_assignment
    source_form: "config.sharedSources = [agentSource, actorSource];"
    target_decision: >-
      Map to `config.SharedSources = new List<string> { agentSource,
      actorSource };` OR `config.SharedSources = new[] { agentSource,
      actorSource };` — the field type is decided by
      lib/multiagent/boot_loader.dart.md. Per the cached SUT convspec
      shape (`List<String>` in Dart → `IReadOnlyList<string>` for the
      get-side; the test does an ASSIGNMENT so the setter side must
      accept `IList<string>` or `IReadOnlyList<string>` depending on the
      SUT decision; the safest faithful shape is `IList<string>` on the
      property so codegen can emit `new List<string> { ... }`). Either
      collection-initialiser form is correct; codegen emits the form
      compatible with the SUT property type.
    idiom_id: rf-dart-list-string-property-assignment-to-csharp-list-or-readonly
    research_finding_id: rf-dart-list-string-property-assignment-to-csharp-list-or-readonly
    nuance: >-
      Mutable-list-property nuance (explicitly addressed): Dart `List<String>`
      is mutable by default; assigning a fresh list to a `final
      List<String>?` field (here `sharedSources` is nullable + settable)
      replaces the list reference. The C# counterpart depends on the SUT
      property's get/set declaration. Codegen emits the assignment using
      the form compatible with the SUT property type — NOT a list-clear-
      and-add-all (would mutate the existing list; the Dart code replaces
      the reference). Recorded as a future-compat dependency on the SUT
      decision; no escalation here.
  - construct_key: dart.sut.manager_boot_with_optional_trace_config
    source_form: >-
      "await manager.boot(config);" + "await manager.boot(config,
      traceConfig: TraceConfig(glp: true, mad: true));"
    target_decision: >-
      DEFERRED — see lib/multiagent/isolate_manager.dart escalations[0].
      The `await _manager.Boot(config)` and `await _manager.Boot(config,
      traceConfig: new TraceConfig { Glp = true, Mad = true })` call
      SHAPES are preserved across all four threading-model options (the
      SUT's `Boot` signature is `public Task Boot(BootConfig config,
      TraceConfig? traceConfig = null)` — stable across options; only the
      BODY varies). Behavioural contract preserved: `Boot` is the
      load-bearing async setup — registers a SINGLE main-port listener
      BEFORE spawning agents, then awaits all Ready messages (per the
      SUT convspec's `boot()` construct). The test relies on `Boot`
      completing successfully (i.e. all three agents reporting Ready)
      before `Start()` is called. The `TraceConfig { Glp = true, Mad =
      true }` object-initialiser syntax is the cached translation of the
      Dart named-args construction `TraceConfig(glp: true, mad: true)`
      (per the SUT convspec — TraceConfig is init-only properties +
      static readonly Off singleton).
    idiom_id: null
    research_finding_id: rf-dart-dart-isolate-spawn-to-csharp-execution-context
    nuance: >-
      Behavioural-contract-preserved-under-deferral nuance (LOAD-BEARING,
      explicitly addressed): even though the implementation BODY of `Boot`
      is deferred (the SUT's listener-install-vs-spawn shape depends on
      the chosen threading-model option), the test's behavioural contract
      is fully recorded here for the eventual codegen pass: (1) `Boot`
      MUST signal completion only after all three agents report Ready
      (the Dart Completer pattern → C# TaskCompletionSource per SUT
      convspec); (2) `Boot` MUST install the main-port consumer BEFORE
      spawning any agent (listener-install-before-spawn ordering); (3)
      `Boot` returns `Task` regardless of option; (4) the `traceConfig`
      optional named-arg maps to a C# optional-parameter-with-default-null
      + constructor-body fallback `traceConfig ?? TraceConfig.Off` per
      the SUT convspec. The test passes ZERO additional fields on
      `TraceConfig` (no `agents:` set), so `_traceConfig.Agents` is null
      and `_isTracingAgent` returns true for any agentId (per the SUT's
      _IsTracingAgent construct). Async/await nuance: Dart `await
      manager.boot(config)` ⇒ C# `await _manager.Boot(config)` — exact
      shape; xUnit awaits the test's returned Task. Named-arg-syntax
      nuance: Dart `TraceConfig(glp: true, mad: true)` is the named-arg
      ctor; C# object-initialiser `new TraceConfig { Glp = true, Mad =
      true }` is the cached SUT-side translation (per the SUT convspec's
      init-only-properties decision) — NOT `new TraceConfig(glp: true,
      mad: true)` (init-only properties cannot be set via constructor-
      named-args in the SUT shape; the cached SUT translation uses
      object-initialiser).
  - construct_key: dart.sut.manager_start_synchronous_void
    source_form: "manager.start();"
    target_decision: >-
      DEFERRED — see lib/multiagent/isolate_manager.dart escalations[0].
      The `_manager.Start();` synchronous void call is preserved across
      all four threading-model options (the SUT's `Start` signature is
      `public void Start()` — stable across options; only the per-port
      Send shape inside the foreach loop varies). Behavioural contract
      preserved: after `Boot` completes, `Start` iterates every registered
      agent port and posts a `Start` message; each agent's event loop
      receives Start and performs the initial drain+flush (per the SUT's
      _AgentIsolateEntry construct).
    idiom_id: null
    research_finding_id: rf-dart-dart-isolate-spawn-to-csharp-execution-context
    nuance: >-
      Sync-call nuance (cache hit on SUT): the SUT's `Start` returns
      `void` synchronously across all four options — the test's call
      site needs no `await`. Behavioural-contract-preserved-under-deferral:
      `Start` MUST be called AFTER `Boot` completes (the Dart code does
      this — sequential `await manager.boot(...); manager.start();`) so
      every agent's port is in `_agentPorts` before the iteration. The
      test relies on this ordering — codegen MUST preserve it
      sequentially even across deferred SUT bodies.
  - construct_key: dart.async.future_delayed_for_event_driven_settling
    source_form: >-
      "await Future.delayed(Duration(milliseconds: 200));" +
      "await Future.delayed(Duration(seconds: 5));" + "await
      Future.delayed(Duration(seconds: 5));"
    target_decision: >-
      Map each to `await Task.Delay(TimeSpan.FromMilliseconds(200));` /
      `await Task.Delay(TimeSpan.FromSeconds(5));`. Microsoft Learn
      'Task.Delay' (https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay)
      — the documented .NET counterpart to Dart's `Future.delayed`.
      Cached from mad_cold_call_isolate_test.dart.md (and general async
      idioms across the convspec series).
    idiom_id: rf-dart-future-delayed-to-csharp-task-delay
    research_finding_id: rf-dart-future-delayed-to-csharp-task-delay
    nuance: >-
      Event-driven-settling nuance (explicitly addressed): the
      `Future.delayed(...)` waits are NOT artificial sleeps — they exist
      so the event-driven execution that started in `manager.start()` has
      time to drive each agent's goal to completion or to suspended
      state. The Dart isolate event loop and the C# port (under any
      threading-model option) both run independently of the test thread;
      the test must yield long enough for messages to flow through.
      Codegen MUST preserve the duration verbatim — shortening would
      flake the test. Task.Delay-vs-Thread.Sleep nuance: Task.Delay is
      cooperative (returns to the scheduler) and matches Dart's
      Future.delayed; Thread.Sleep is BLOCKING (pegs the test thread)
      and would NOT match Dart semantics — codegen MUST emit Task.Delay,
      NOT Thread.Sleep. TimeSpan-vs-Duration nuance: Dart
      `Duration(milliseconds: 200)` and .NET `TimeSpan.FromMilliseconds(200)`
      both store 100-ns-precision durations; semantically identical.
  - construct_key: dart.package_test.early_return_skip_pattern_on_null_helpers
    source_form: >-
      "if (source == null || agentSource == null || actorSource == null)
      return;" + "if (source == null || agentSource == null ||
      mediatorSource == null || uiActorSource == null) return;"
    target_decision: >-
      Map each verbatim: `if (source == null || agentSource == null ||
      actorSource == null) return;`. The early-return-on-missing-fixture
      idiom is faithfully translated. xUnit considers a test that returns
      without throwing as PASSED (same as Dart `package:test`) — so the
      skip path is observably a passing test, NOT a `[Skip]`-decorated
      test (which would be the alternative, but the Dart source does not
      use `package:test`'s skip mechanism — it uses a runtime guard, and
      the C# port preserves that). Recorded alternative for codegen
      review: use `Skip.If(source == null || ..., "Fixture missing")`
      (Xunit.Sdk.Skip from xUnit v3) — but the baseline emission is the
      verbatim if-return.
    idiom_id: rf-dart-early-return-skip-pattern-to-csharp-if-return
    research_finding_id: rf-dart-early-return-skip-pattern-to-csharp-if-return
    nuance: >-
      Test-skip-vs-test-pass-on-fixture-missing nuance (explicitly
      addressed): Dart `package:test` would normally use `skip: 'reason'`
      on the `test(...)` call to mark a test as skipped — but this file
      chooses a RUNTIME-conditional early-return instead (the test's
      `_readGlpFile` helper prints "Skipping: ..." and returns null,
      then the test body bails out early). C# port preserves the same
      shape — the test passes silently. NOT `Skip.If(...)` (would change
      the reporter status from "passed" to "skipped" — codegen MUST
      preserve the observable status; this faithfully matches Dart's
      runtime-skip-as-pass behaviour). Console.WriteLine-vs-output-helper
      nuance: the "Skipping: ..." print already goes to Console
      (via `_ReadGlpFile`); the test body adds nothing further. The
      caller need not log "test skipped" — the absence of subsequent
      output is the skip signal.
  - construct_key: dart.package_test.expect_equals_length_int
    source_form: "expect(config.directives.length, equals(3));"
    target_decision: >-
      Map to `Assert.Equal(3, config.Directives.Count);` — argument-order
      FLIP per the project-wide cached idiom
      `rf-dart-expect-equals-to-xunit-assert-equal-argorder`. Dart
      `List<T>.length` ⇒ C# `Count` on `IReadOnlyList<T>`/`IList<T>`
      (per the SUT convspec for `BootConfig.Directives`). NOT `.Length`
      (that's for arrays / strings).
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order-flip nuance (cache hit, well-known footgun): Dart
      `expect(actual, equals(expected))` ⇒ xUnit `Assert.Equal(expected,
      actual)`. Length-vs-Count nuance: Dart `List.length` is the
      element count; C# `IReadOnlyCollection<T>.Count` is the same. C#
      `.Length` would be wrong for List<T>/IReadOnlyList<T>; correct only
      for arrays and strings.
  - construct_key: dart.package_test.expect_equals_list_via_map_tolist
    source_form: >-
      "expect(config.directives.map((d) => d.agentId).toList(),
      equals(['alice', 'bob', 'charlie']));"
    target_decision: >-
      Map to `Assert.Equal(new[] { "alice", "bob", "charlie" },
      config.Directives.Select(d => d.AgentId).ToList());`. xUnit
      `Assert.Equal(IEnumerable, IEnumerable)` performs element-wise
      equality. LINQ mappings: Dart `Iterable.map` ⇒ `Enumerable.Select`;
      Dart `Iterable.toList` ⇒ `Enumerable.ToList`. Cached from
      boot_loader_test.dart.md.
    idiom_id: rf-dart-list-equality-to-xunit-assertequal-collection
    research_finding_id: rf-dart-list-equality-to-xunit-assertequal-collection
    nuance: >-
      Collection-equality nuance (cache hit): Dart `equals` over a
      `List` does element-wise comparison via the elements' `==`; xUnit
      `Assert.Equal(IEnumerable, IEnumerable)` uses the default
      `IEqualityComparer<T>`. For `string` elements both behave
      identically. Materialise nuance: `.Select(...).ToList()` matches
      Dart's eager `.toList()`; bare `.Select(...)` (deferred
      IEnumerable) would also pass `Assert.Equal` (it iterates) but the
      materialisation makes diagnostic output identical.
  - construct_key: dart.package_test.expect_isTrue_via_every_predicate
    source_form: >-
      "expect(config.directives.every((d) => d.goalFunctor ==
      'agent_init'), isTrue);"
    target_decision: >-
      Map to `Assert.True(config.Directives.All(d => d.GoalFunctor ==
      "agent_init"));`. LINQ mapping: Dart `Iterable.every` ⇒
      `Enumerable.All`. Cached from boot_loader_test.dart.md.
    idiom_id: rf-dart-expect-istrue-to-xunit-asserttrue
    research_finding_id: rf-dart-expect-istrue-to-xunit-asserttrue
    nuance: >-
      LINQ nuance (cache hit): Dart `Iterable.every` = C# `Enumerable.All`;
      Dart `Iterable.any` = C# `Enumerable.Any`. Diagnostic nuance:
      `Assert.True(b)` without a message produces a generic "Assert.True()
      Failure"; the predicate here is comprehensible so the bare form
      suffices.
  - construct_key: dart.string.single_quoted_string_to_csharp_double_quoted
    source_form: "'alice' / 'bob' / 'charlie' / 'agent_init' / 'play_madglp_boot.glp' etc."
    target_decision: >-
      Map every Dart single-quoted string literal `'...'` to a C# double-
      quoted string literal `"..."` (C# has no single-quoted string
      literal — `'X'` is a char literal). Strings WITH apostrophes (none
      in this file) would still translate to double-quoted with the
      apostrophe unescaped. Cached idiom from every prior convspec.
    idiom_id: rf-dart-single-quoted-string-to-csharp-double-quoted
    research_finding_id: rf-dart-single-quoted-string-to-csharp-double-quoted
    nuance: >-
      String-quote nuance (cache hit, trivial): Dart `'x'` (string) vs C#
      `"x"` (string) vs C# `'x'` (char literal). The Dart code does not
      use double-quoted strings here so there is no quote-style
      preservation question.

conversion_units:
  - "cu-1: file-scope using directives (Xunit, System, System.IO, System.Threading.Tasks, System.Linq, System.Collections.Generic, plus the project-wide single multiagent SUT namespace via `using <RootNs>.Multiagent;`)"
  - "cu-2: namespace declaration mirroring test/multiagent path (e.g. <RootNs>.Test.Multiagent)"
  - "cu-3: top-level test class `IsolateManagerTests : IAsyncLifetime` (from outer group label `IsolateManager`); `[Trait(\"Group\", \"IsolateManager\")]`"
  - "cu-4: private const string SocialGraphDir = \"../programs/typed_book/social_graph\""
  - "cu-5: private static string? ReadGlpFile(string filename) helper — sync File.Exists + File.ReadAllText with Console.WriteLine skip diagnostic on missing fixture; returns null on miss"
  - "cu-6: private IsolateManager _manager = null!; — late-field mapping"
  - "cu-7: public Task InitializeAsync() — setUp mapping; sync body returning Task.CompletedTask"
  - "cu-8: public async Task DisposeAsync() — tearDown mapping; awaits _manager.Shutdown() — Shutdown body DEFERRED per SUT escalations[0], signature stable"
  - "cu-9: [Fact(DisplayName = \"boots three agents from boot config\", Timeout = 10000)] public async Task BootsThreeAgentsFromBootConfig() — minimal triple-quoted GLP source as C# 11 raw string; new BootLoader → Load → set RootSelfGlpPath via Path.GetFullPath; await _manager.Boot(config) DEFERRED; _manager.Start() DEFERRED; await Task.Delay(TimeSpan.FromMilliseconds(200)); no assertions (pass-if-no-throw)"
  - "cu-10: [Fact(DisplayName = \"runs full play with actor scripts (no UI)\", Timeout = 30000)] public async Task RunsFullPlayWithActorScriptsNoUi() — three ReadGlpFile calls (source/agentSource/actorSource); if (any == null) return; (early-return skip-as-pass); new BootLoader → Load → set RootSelfGlpPath + SharedSources list assignment; three expect/Assert assertions on Directives count/agentId/goalFunctor; await _manager.Boot(config, traceConfig: new TraceConfig { Glp = true, Mad = true }) DEFERRED; _manager.Start() DEFERRED; await Task.Delay(TimeSpan.FromSeconds(5))"
  - "cu-11: [Fact(DisplayName = \"runs full play with UI mediator and UI actors\", Timeout = 30000)] public async Task RunsFullPlayWithUiMediatorAndUiActors() — four ReadGlpFile calls (source/agentSource/mediatorSource/uiActorSource); same early-return pattern; same three assertions; await _manager.Boot(config, traceConfig: new TraceConfig { Glp = true, Mad = true }) DEFERRED; _manager.Start() DEFERRED; await Task.Delay(TimeSpan.FromSeconds(5))"
  - "cu-12: raw-string-literal payload for the minimal triple-quoted GLP source in cu-9, emitted at column 0 to preserve indentation byte-identically"

escalations: []
```

## Rationale + research provenance

### Why no NEW escalations on this test (FR-013 "don't double-escalate")

This file is a CONSUMER of the just-escalated SUT
`lib/multiagent/isolate_manager.dart` (the central threading-model
decision point). Per FR-013 and the precedent set by the sibling
`mad_cold_call_isolate_test.dart.md` convspec (which inherits the same
escalation without re-issuing it), this artifact does NOT introduce a
new escalation for the deferred SUT calls. Every construct that
touches `IsolateManager.boot` / `IsolateManager.start` /
`IsolateManager.shutdown` records `target_decision: "DEFERRED — see
lib/multiagent/isolate_manager.dart escalations[0]"` and preserves the
Dart-side behavioural contract verbatim so codegen can encode the
chosen option once the SUT ruling lands. The behavioural contract
captured for codegen includes: (a) `Boot` MUST install the main-port
consumer BEFORE spawning any agent (listener-install-before-spawn
ordering); (b) `Boot` MUST signal completion only after all expected
Ready messages arrive (Completer → TaskCompletionSource pattern); (c)
`Start` MUST be called AFTER `Boot` completes (sequential ordering);
(d) `Shutdown` MUST close the main port + clear `_agentPorts` WITHOUT
killing the agent execution contexts (the no-Isolate.kill contract).

`escalations: []` is therefore intentional — every undecidable point is
already owned by the SUT's `escalations[0]`. NO genuinely-local
undecidable point exists in this test file (every non-deferred
construct resolves via cached test-conversion idioms: xUnit pinning,
`late`-field mapping, `setUp`/`tearDown` → IAsyncLifetime, triple-
quoted strings → raw strings, `expect`-flip argument order, LINQ
mappings, `Future.delayed` → `Task.Delay`, etc.).

### xUnit pinning (cache hit, project-wide)

Same authoritative basis as `boot_loader_test.dart.md` and
`mad_cold_call_isolate_test.dart.md`: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / constructor-per-test isolation, and the Dart
`package:test` README on pub.dev (`https://pub.dev/packages/test`) for
`group` / `test` / `expect` / matcher semantics. No re-research.

### `IAsyncLifetime` for async tearDown (new finding scoped to this file)

This file has BOTH `setUp(() { ... })` AND `tearDown(() async { await
manager.shutdown(); })` — the async tearDown forces `IAsyncLifetime`
over the simpler constructor+IDisposable shape used by
`boot_loader_test.dart.md`. xUnit `IAsyncLifetime` is the documented
async-lifecycle mechanism (https://xunit.net/docs/shared-context —
"Constructor and Dispose" + IAsyncLifetime variant). The interface
provides `Task InitializeAsync()` (called per-test, before the test
body) and `Task DisposeAsync()` (called per-test, after the test
body) — observably equivalent to Dart `setUp` + `tearDown`. The
SYNCHRONOUS-body of this file's setUp maps to a non-async
`InitializeAsync` returning `Task.CompletedTask` (per
`https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/
async-scenarios` — "If the method does not perform any asynchronous
operations, you don't need the async modifier; return
Task.CompletedTask instead.").

### `Future.delayed` → `Task.Delay` (cache hit + new finding citation)

Microsoft Learn `Task.Delay`
(`https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay`)
documents Task.Delay as the canonical asynchronous delay primitive —
the .NET counterpart to Dart's `Future.delayed`. Both are cooperative
(yield to the scheduler) and accept a TimeSpan/Duration argument. The
Dart `Future.delayed` docs
(`https://api.dart.dev/stable/dart-async/Future/delayed.html`) describe
it as "Creates a future that runs its computation after a delay" — 1:1
mapping.

### `dart:io File` → `System.IO.File` (new finding)

Microsoft Learn `System.IO.File`
(`https://learn.microsoft.com/dotnet/api/system.io.file`) — the .NET
static class providing `Exists`, `ReadAllText`, etc. The Dart `dart:io
File` class
(`https://api.dart.dev/stable/dart-io/File-class.html`) is an instance-
style API; the C# port collapses Dart `File(path).existsSync()` /
`.readAsStringSync()` to `File.Exists(path)` / `File.ReadAllText(path)`.
The Dart `File(path).absolute.path` (resolve to absolute path
against CWD) maps to `Path.GetFullPath(path)` (Microsoft Learn —
"Returns the absolute path for the specified path string."). All
SYNC variants (matches the Dart source's sync API choice).

### `[Fact(Timeout = N)]` for per-test Dart `Timeout(...)`

xUnit's `[Fact(Timeout = N)]` attribute
(`https://xunit.net/docs/comparisons` — timeout support) cancels the
test after N milliseconds — observably equivalent to Dart `timeout:
Timeout(Duration(...))`. Codegen emits the millisecond value (10000
for 10 seconds, 30000 for 30 seconds).

### LINQ mappings (cache hit)

The "real file content"-style tests use `.directives.map((d) =>
d.agentId).toList()` and `.directives.every((d) => d.goalFunctor ==
'agent_init')`. The official `System.Linq` reference
(`https://learn.microsoft.com/dotnet/api/system.linq.enumerable`) gives
the canonical mappings: `Iterable.map` = `Select`, `Iterable.toList` =
`ToList`, `Iterable.every` = `All`. Cached from
`boot_loader_test.dart.md` — reused verbatim.

### Early-return-on-missing-fixture preserved (not converted to `[Skip]`)

This file uses a RUNTIME-conditional early-return (`if (source ==
null || ...) return;`) instead of `package:test`'s `skip:` parameter
— xUnit considers a method that returns without throwing as PASSED.
The C# port preserves the observable status (passed-as-skip), NOT a
`Skip.If(...)` (which would change the reporter status to "skipped").
Recorded as a forward-looking alternative for code review.

### TraceConfig construction via object-initialiser (cache hit on SUT)

The Dart `TraceConfig(glp: true, mad: true)` named-args constructor
call maps to C# `new TraceConfig { Glp = true, Mad = true }` —
object-initialiser syntax. This is the cached SUT-side translation
from `lib/multiagent/isolate_manager.dart.md`'s TraceConfig construct
(init-only properties + static readonly Off singleton; default-arg
construction sites use object-initialiser per the SUT convspec). NOT
`new TraceConfig(glp: true, mad: true)` — init-only properties cannot
be set via constructor-named-args under the SUT shape.

## Notes

- **This test exercises the JUST-ESCALATED SUT.** Every Boot/Start/Shutdown
  call site records `DEFERRED — see lib/multiagent/isolate_manager.dart
  escalations[0]` in its `target_decision`, and the SUT's behavioural
  contract is preserved verbatim in each construct's nuance field for
  codegen to encode after the ruling. No new escalation introduced.

- **`IAsyncLifetime` not `IDisposable`.** This file has an `async`
  tearDown body (`await manager.shutdown()`), forcing the
  `IAsyncLifetime` lifecycle interface over the simpler
  constructor+IDisposable.Dispose pattern used by tests without async
  teardown.

- **Triple-quoted source literal preserved byte-identically.** Codegen
  MUST emit the closing `"""` at column 0 so the raw-string-literal
  payload matches the Dart source's whitespace exactly.

- **No new isolate research performed.** The `dart:isolate` threading-
  model decision is owned by the SUT's escalation. This convspec
  records only the call-site SHAPES and behavioural contracts at the
  test-side; the concrete `Send` / `await foreach` / etc. shapes flow
  in from the SUT after the ruling.

- **xUnit-passed-as-Dart-skipped contract preserved.** The runtime-
  conditional early-return on missing fixture files results in a
  PASSED xUnit test (no throw); this matches the Dart-side observable
  status (also passes, since the test body returns without throwing).
  Codegen MUST NOT convert to `[Fact(Skip = ...)]` — would change the
  reporter status.

- **All file paths use forward slashes; .NET accepts both on Windows.**
  No path-separator rewrite needed; `Path.Combine` / `Path.GetFullPath`
  handle either separator transparently.
