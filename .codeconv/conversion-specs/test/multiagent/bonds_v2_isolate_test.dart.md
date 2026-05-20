# Conversion Spec — test/multiagent/bonds_v2_isolate_test.dart

> Conversion-spec artifact for test/multiagent/bonds_v2_isolate_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.
>
> **Inherited threading-model escalation.** This test exercises the
> `IsolateManager` SUT surface (`manager.boot(...)`, `manager.start()`,
> `manager.shutdown()`), which is ESCALATED at
> `lib/multiagent/isolate_manager.dart.md` on the central decision
> `dart.dart_isolate_api_to_csharp_execution_context_choice` (Thread + BlockingCollection /
> per-agent TaskScheduler / `Channel<T>` actor mailbox / SynchronizationContext).
> Per FR-013 "escalate, don't guess" plus the "don't double-escalate"
> discipline used by `mad_cold_call_isolate_test.dart.md` and the
> sibling SUT siblings (`mad_context.dart.md`, `message_queue.dart.md`,
> `payload_serializer.dart.md`): this test file is a **CONSUMER** of that
> decision, not a new owner. The constructs below DEFER threading-model-
> dependent concrete primitives back to the isolate_manager.dart
> escalation; `escalations: []` is intentional, not a placeholder.

```yaml
schema_version: 1
source_path: test/multiagent/bonds_v2_isolate_test.dart
source_sha256: 18e788ee20ad20f262700ad47895d6a6cdefae27818f7c356f7c719a38512e5c
target_code_unit: test/multiagent/BondsV2IsolateTest.cs
constructs:
  - construct_key: dart.import.dart_io_core_library
    source_form: "import 'dart:io';"
    target_decision: >-
      Drop the `dart:io` line; replace its single load-bearing symbol
      (`File`) at first use with the BCL counterpart `System.IO.File` —
      static-method calls `File.Exists(path)` and `File.ReadAllText(path)`
      (per the cached idiom rf-dart-dart-io-file-to-dotnet-system-io-file,
      already established in `test/multiagent/ui_mediator_test.dart.md`
      and the boot_loader test exemplars for the `File(...)` constructor
      pattern). Codegen MUST add `using System.IO;` at file scope.
    idiom_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    research_finding_id: rf-dart-dart-io-file-to-dotnet-system-io-file
    nuance: >-
      Standard-library nuance (cache hit): Dart `dart:io` is a CORE
      library exposing `File`/`Directory`/`Platform`/`stdin`/`stdout`/...;
      the .NET counterpart is the `System.IO` namespace (NOT a single
      type). The Dart instance method `File(p).existsSync()` maps to the
      .NET static method `File.Exists(p)`; Dart `File(p).readAsStringSync()`
      maps to `File.ReadAllText(p)`. Synchronous-IO nuance (preserved):
      the Dart source uses the `*Sync` variants — synchronous IO on the
      test thread; .NET counterparts `File.Exists`/`File.ReadAllText` are
      ALSO synchronous — semantically identical; no `await` introduced.
      The instance-method `bootFile.path` (used in the diagnostic print)
      maps to the local `string` variable (the path string passed to
      `File.Exists` is the same value — no `.path` accessor needed in
      C# where the path is already a `string`).
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit pinned project-wide (cached
      idiom rf-dart-package-test-import-to-xunit-using, established in
      `test/multiagent/mad_error_handling_test.dart.md` and reused by every
      sibling test convspec including
      `mad_cold_call_isolate_test.dart.md`). Codegen MUST also add
      `using System;` (`TimeSpan`, `Action`, `Exception`),
      `using System.Threading.Tasks;` (`Task`, `Task.Delay`), and
      `using System.IO;` (per dart.import.dart_io_core_library above).
      Target namespace mirrors the Dart `test/multiagent` directory
      (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is a project-wide policy (cache hit, not a
      file-local choice). Every `package:test` file maps to the SAME .NET
      framework. xUnit's constructor-per-test isolation matches
      `package:test` fresh-state semantics; `[Fact]` maps 1:1 onto Dart
      `test(...)`; the parameterised loop pattern in this file (over
      `[2,3,4,5,6,8,9]` and `[10,11]`) maps to `[Theory]` + `[InlineData]`
      (see dart.test_call_in_for_loop_parameterised below).
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/multiagent/boot_loader.dart';
       import 'package:glp_runtime/multiagent/isolate_manager.dart';"
    target_decision: >-
      Map each to a `using` directive that names the C# namespace produced
      by converting the SUT file. Per the per-SUT-file convspecs
      (`lib/multiagent/boot_loader.dart.md`, `lib/multiagent/isolate_manager.dart.md`)
      both SUT files target the `lib/multiagent` namespace (e.g.
      `using <RootNs>.Multiagent;`). One shared `using` covers both.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance (cache hit): in Dart `package:glp_runtime/...`
      is an explicit pubspec-anchored URI; in C# there is no per-file URI —
      only assembly + namespace. The conversion must (a) ensure each
      converted SUT lives in a deterministic namespace derived from its
      relative path (decided at each SUT's convspec) and (b) ensure the
      test assembly references the SUT assembly via the project file
      (project-system idiom, out of scope here). No `as` alias / `show` /
      partial import is used in this file.
  - construct_key: dart.toplevel_const_string_path_literals
    source_form: >-
      "const _bondsV2Dir = '../programs/bonds_v2';
       const _madBootDir = '$_bondsV2Dir/mad_boot';
       const _rootSelfGlpPath = '../programs/self.glp';"
    target_decision: >-
      Three top-level `const String` declarations with file-private
      visibility (Dart `_`-prefix). Map to file-scope `const string`
      members on a `file static class` container — e.g.
      `file static class TestPaths { internal const string BondsV2Dir =
      "../programs/bonds_v2"; internal const string MadBootDir =
      BondsV2Dir + "/mad_boot"; internal const string RootSelfGlpPath =
      "../programs/self.glp"; }`. The Dart const string interpolation
      `'$_bondsV2Dir/mad_boot'` is a compile-time concatenation; C# `const
      string` permits compile-time concatenation with `+` only when both
      operands are `const string` — which holds here. Visibility nuance:
      Dart `_`-prefix is library-private; C# 10+ `file` access modifier
      gives file-private visibility (https://learn.microsoft.com/dotnet/csharp/
      language-reference/keywords/file). Alternative: emit the three
      constants as `private const string` static members on the test
      class itself — equivalent observable behaviour, slightly less
      idiomatic but compatible with C# < 10.
    idiom_id: rf-dart-private-toplevel-const-to-csharp-file-static-const
    research_finding_id: rf-dart-private-toplevel-const-to-csharp-file-static-const
    nuance: >-
      Library-vs-file-private nuance (explicitly addressed): Dart `_`
      identifiers are library-private (the library = a `.dart` file in
      this codebase — the build uses `part`/`part of` only for generated
      sources). C# 10+ `file` access modifier matches this semantic
      precisely; pre-C#-10 codegen falls back to `private static` members
      on the enclosing test class (acceptable since the constants are
      consumed only by the helper method and tests in the same file).
      Const-string-interpolation-vs-concatenation nuance: Dart const
      strings allow `$varName` and `${expr}` interpolation when all
      embedded values are themselves compile-time constants; C# `const
      string` supports compile-time `+` concatenation of `const string`
      operands (https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/addition-operator#string-concatenation) — equivalent
      compile-time fold. C# 6+ interpolated strings `$"..."` are NOT
      compile-time constants — codegen MUST use `+`, not `$"{BondsV2Dir}/mad_boot"`,
      to preserve `const`-ness.
  - construct_key: dart.toplevel_function.async_void_test_helper_with_named_optional_int_parameter
    source_form: >-
      "Future<void> _runPlay(IsolateManager manager, String bootFilename,
          {int timeoutSec = 10}) async {
         final bootFile = File('$_madBootDir/$bootFilename');
         if (!bootFile.existsSync()) {
           print('Skipping: ${bootFile.path} not found');
           return;
         }
         final bootSource = bootFile.readAsStringSync();
         final loader = BootLoader();
         final config = loader.load(bootSource);
         config.projectDir = _bondsV2Dir;
         config.rootSelfGlpPath = _rootSelfGlp;
         await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: false));
         manager.start();
         await Future.delayed(Duration(seconds: timeoutSec));
       }"
    target_decision: >-
      Map to a `private static async Task _RunPlayAsync(IsolateManager
      manager, string bootFilename, int timeoutSec = 10) { ... }` instance-
      independent helper on the test class (the helper has no instance
      state — every collaborator is passed as an argument). Dart
      `Future<void>` ⇒ `Task`; Dart `async` ⇒ C# `async`. The Dart named
      optional `{int timeoutSec = 10}` maps to a C# positional parameter
      with default value `int timeoutSec = 10` (cached idiom
      rf-dart-named-required-partly-defaulted-to-csharp-positional-with-defaults,
      established at the BootLoader / AgentConfig SUT convspecs and reused
      at every multiagent test). Call sites pass either positionally
      (`_RunPlayAsync(manager, "mad_fplay12.glp", 15)`) or by named-argument
      (`_RunPlayAsync(manager, "mad_fplay12.glp", timeoutSec: 15)`) —
      both compile; the named-argument form preserves Dart call-site
      readability. Body translates statement-for-statement:
      (a) `final bootFile = File('$_madBootDir/$bootFilename');` ⇒ a
          local `string bootFile = TestPaths.MadBootDir + "/" + bootFilename;`
          (NOT a `FileInfo` instance — the .NET static-method
          `File.Exists`/`File.ReadAllText` work directly on the path
          string, matching the dart.import.dart_io_core_library decision).
      (b) `if (!bootFile.existsSync()) { print('Skipping: ...'); return; }`
          ⇒ `if (!File.Exists(bootFile)) { Console.WriteLine($"Skipping:
          {bootFile} not found"); return; }`.
      (c) `bootFile.readAsStringSync()` ⇒ `File.ReadAllText(bootFile)`.
      (d) `final loader = BootLoader(); final config = loader.load(bootSource);`
          ⇒ `var loader = new BootLoader(); var config = loader.Load(bootSource);`
          per the boot_loader.dart convspec (`Load` is the public C#
          method name; `BootLoader` is a reference class with a no-arg
          ctor).
      (e) `config.projectDir = _bondsV2Dir; config.rootSelfGlpPath = _rootSelfGlp;`
          ⇒ `config.ProjectDir = TestPaths.BondsV2Dir; config.RootSelfGlpPath
          = TestPaths.RootSelfGlpPath;`. Per the boot_loader.dart convspec
          `BootConfig` is a reference class (NOT a record, NOT a struct;
          aliased by the caller) with mutable properties for these fields
          — direct setter assignment preserved.
      (f) `await manager.boot(config, traceConfig: TraceConfig(glp: false,
          mad: false));` ⇒ `await manager.BootAsync(config, new TraceConfig
          { Glp = false, Mad = false });`. The named argument `traceConfig:`
          is preserved as a C# named argument (`traceConfig: new
          TraceConfig { ... }`) for readability. `TraceConfig` construction
          uses C# object-initializer per the isolate_manager.dart convspec
          (TraceConfig has init-only properties; the construction with all-
          false flags also matches the cached `TraceConfig.Off` singleton —
          codegen MAY use `TraceConfig.Off` instead, observationally
          identical). Method-naming nuance: the C# method `BootAsync` is
          the Async-suffixed convention for `Task`-returning methods
          (https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap#naming-parameters-and-return-types);
          the alternative `Boot` (no suffix) matches the Dart name 1:1 and
          is also acceptable — the isolate_manager.dart convspec records
          both names; the test consumes whichever the SUT pins.
      (g) `manager.start();` ⇒ `manager.Start();`.
      (h) `await Future.delayed(Duration(seconds: timeoutSec));` ⇒
          `await Task.Delay(TimeSpan.FromSeconds(timeoutSec));` (cached
          idiom rf-dart-future-delayed-to-csharp-task-delay; recorded
          across multiple multiagent tests). Note that the body has NO
          assertions — the test's success criterion is "the play runs to
          completion within the timeout without throwing" (xUnit treats
          an absence of thrown exception as pass).
    idiom_id: rf-dart-future-delayed-to-csharp-task-delay
    research_finding_id: rf-dart-future-delayed-to-csharp-task-delay
    nuance: >-
      Async-test-helper nuance (explicitly addressed and LOAD-BEARING):
      the Dart helper returns `Future<void>`; the C# counterpart returns
      `Task` (NOT `async void` — `async void` swallows exceptions and is
      reserved for event handlers per
      https://learn.microsoft.com/dotnet/csharp/language-reference/operators/async).
      Exceptions thrown inside the helper (e.g. from `manager.BootAsync`)
      propagate through the awaited Task and surface as test failures —
      identical observable behaviour to the Dart side (`Future<void>` carries
      errors that surface on `await`). Sleep-as-wait nuance (LOAD-BEARING
      design intent): the `await Future.delayed(...)` at the end is the
      test's GATING wait — the play's success is measured by "no exception
      thrown during the timeoutSec-second window after boot+start". This is
      a FIXED-DURATION wait, NOT a completion-driven wait (no
      `Completer<void>` / `TaskCompletionSource` involved here, unlike
      `mad_cold_call_isolate_test.dart.md`). The C# counterpart MUST
      preserve this — `Task.Delay(TimeSpan.FromSeconds(timeoutSec))` is the
      faithful translation; codegen MUST NOT replace this with a busy-wait,
      a `Thread.Sleep` (would block the threadpool thread synchronously —
      changing async semantics), or a `CancellationTokenSource`-based wait
      (would introduce cancellation semantics absent in the Dart source).
      Cooperative-yield nuance: `Task.Delay` releases the thread to the
      pool during the wait — matching Dart's microtask-queue release on
      `await Future.delayed`. Early-return-on-missing-boot-file nuance: the
      Dart source PRINTS a skip diagnostic and returns; the C# counterpart
      preserves this (Console.WriteLine + return) — NOT `Assert.Skip(...)`
      (xUnit has no built-in skip from inside a test body; the dynamic-skip
      idiom is `throw new SkipException("...")` only when `Xunit.SkippableFact`
      is wired in, and that is NOT the project's xUnit convention per
      established sibling convspecs). The print + return preserves the
      Dart-side "silent skip" semantics; the test runner records the test
      as passing with diagnostic output.
  - construct_key: dart.future.delayed_with_duration_seconds
    source_form: "await Future.delayed(Duration(seconds: timeoutSec));"
    target_decision: >-
      Map to `await Task.Delay(TimeSpan.FromSeconds(timeoutSec));`. Cached
      idiom from sibling multiagent tests (mad_scenarios_test.dart.md,
      mad_transactions_test.dart.md record this pattern). The Dart static
      factory `Future.delayed(Duration, [computation])` creates a Future
      that completes after the duration (with `null` if no computation
      supplied); .NET `Task.Delay(TimeSpan)` is the documented counterpart
      (https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay).
      Both release the underlying thread/event-loop during the wait.
    idiom_id: rf-dart-future-delayed-to-csharp-task-delay
    research_finding_id: rf-dart-future-delayed-to-csharp-task-delay
    nuance: >-
      Time-precision nuance (cache hit): Dart `Duration(seconds: N)` and
      .NET `TimeSpan.FromSeconds(N)` both store a 100-ns-precision
      duration; semantically identical. Computation-callback nuance: Dart
      `Future.delayed(d, computation)` may carry an optional zero-arg
      callback that runs after the delay; .NET has no direct equivalent
      (the closest is `await Task.Delay(d); var result = computation();`
      sequenced). NOT exercised in this file (no computation argument).
      Cancellation nuance: .NET `Task.Delay(TimeSpan, CancellationToken)`
      overload accepts cancellation; the Dart source does NOT pass a
      cancellation token (Dart Futures cannot be cancelled at all), so the
      no-token overload is the faithful translation.
  - construct_key: dart.duration.seconds_constructor
    source_form: "Duration(seconds: N)"
    target_decision: >-
      Map to `TimeSpan.FromSeconds(N)` — the documented static factory
      (https://learn.microsoft.com/dotnet/api/system.timespan.fromseconds).
      Used in three places: `Duration(seconds: timeoutSec)` (the helper's
      delay), and `Timeout(Duration(seconds: 30))` / `Timeout(Duration(seconds: 45))`
      (per-test timeouts — see dart.package_test.test_timeout_parameter
      below).
    idiom_id: rf-dart-duration-seconds-to-csharp-timespan-fromseconds
    research_finding_id: rf-dart-duration-seconds-to-csharp-timespan-fromseconds
    nuance: >-
      Named-arg-vs-factory nuance: Dart `Duration` has a named-constructor
      `Duration({days, hours, minutes, seconds, milliseconds, microseconds})`;
      .NET `TimeSpan` has multiple `FromXxx` static factories
      (`FromSeconds`, `FromMinutes`, ...). For a SINGLE named argument
      (`seconds: N`), the `FromSeconds` factory is the precise
      counterpart; for multi-component Dart calls (`Duration(minutes: 1,
      seconds: 30)`) the C# counterpart would combine
      (`TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30)`) — NOT
      exercised in this file. Value-vs-reference nuance: both `Duration`
      (Dart) and `TimeSpan` (C#) are VALUE types — copy semantics are
      identical.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('Bonds V2 Multi-Isolate', () { ... }); }"
    target_decision: >-
      Dart `void main()` is the per-file `package:test` entrypoint. xUnit
      discovers `[Fact]`/`[Theory]` methods by reflection — there is NO
      per-file entrypoint. Eliminate `main` entirely; its single statement
      (the outer `group('Bonds V2 Multi-Isolate', ...)`) becomes the
      enclosing test class. Cached idiom rf-dart-package-test-main-omit-in-xunit.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (cache hit, restated): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook. THIS file's
      `main` body is exactly one `group(...)` call, so the omission is
      lossless.
  - construct_key: dart.package_test.group_block
    source_form: "group('Bonds V2 Multi-Isolate', () { ... });"
    target_decision: >-
      Map to a `public class BondsV2IsolateTests : IAsyncLifetime`
      PascalCase test class (single `group`, no nested topology). The
      original Dart label is preserved via `[Trait("Group", "Bonds V2
      Multi-Isolate")]` on the class. Cached idiom
      rf-dart-package-test-group-to-xunit-class. The
      `: IAsyncLifetime` interface is REQUIRED here (NOT a plain
      constructor) because the `tearDown(...)` block is `async` (see
      dart.package_test.tearDown_block below) — xUnit's `IDisposable.Dispose`
      is synchronous, so the async-teardown contract is satisfied by
      `IAsyncLifetime` (https://xunit.net/docs/shared-context#class-fixture)
      providing `Task InitializeAsync()` + `Task DisposeAsync()`.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (cache hit): arbitrary string label ⇒ PascalCase
      identifier; original preserved via `[Trait]`. Single-group nuance:
      this file has only ONE group; no nested-group topology to flatten.
      Async-teardown-via-IAsyncLifetime nuance (LOAD-BEARING, NEW for the
      multiagent test convspec series — first explicit recording here):
      xUnit's constructor-per-test isolation makes `setUp` map to the
      constructor (synchronous), but `tearDown` mapping depends on whether
      the Dart `tearDown` body is async. SYNC `tearDown` ⇒ `IDisposable.Dispose`;
      ASYNC `tearDown` ⇒ `IAsyncLifetime.DisposeAsync` (xUnit awaits the
      returned Task; available since xUnit v2.x). Codegen MUST select
      `IAsyncLifetime` here because the Dart `tearDown` is `async` and
      awaits `manager.shutdown()`.
  - construct_key: dart.package_test.late_field_in_group
    source_form: "late IsolateManager manager;"
    target_decision: >-
      Dart `late IsolateManager manager;` declared in the group's closure
      maps to a `private IsolateManager _manager = null!;` instance field
      on the test class (cached idiom rf-dart-late-field-to-csharp-null-bang,
      established by `ui_mediator_test.dart.md`). The field is
      initialized by the constructor (which translates `setUp`); since
      `setUp` runs before EVERY test (xUnit's per-test constructor
      semantics), the field is guaranteed-initialised on every read site,
      matching the Dart `late` contract.
    idiom_id: rf-dart-late-field-to-csharp-null-bang
    research_finding_id: rf-dart-late-field-to-csharp-null-bang
    nuance: >-
      Late-vs-null-bang nuance (cache hit): Dart `late T x;` defers
      initialisation but asserts the field IS initialised before any
      read. C# 8+ NRT has no `late`; the documented counterparts are
      (a) `private T _x = null!;` (null-forgiving initialiser; the
      compiler treats it as non-null thereafter), (b) `private T? _x;`
      with `_x!.Foo()` at every read site. The `null!` form preserves
      the "guaranteed-initialised" contract more faithfully —
      construction-site discipline ensures the field is assigned before
      any test method runs. The alternative `private T? _x;` was
      REJECTED because it inverts the contract; recorded in the research
      finding.
  - construct_key: dart.package_test.setUp_block
    source_form: "setUp(() { manager = IsolateManager(); });"
    target_decision: >-
      Dart `setUp` runs the closure before each test in the group; xUnit's
      per-test constructor isolation provides identical semantics. Map to
      `public BondsV2IsolateTests() { _manager = new IsolateManager(); }`
      —  a synchronous constructor body assigning the `late` field. NO
      `[SetUp]` attribute (that is NUnit's idiom). Cached idiom
      rf-dart-setup-to-xunit-constructor. xUnit instantiates the test
      class ONCE per test method (constructor-per-test isolation), which
      matches `package:test`'s per-test fresh-state semantics exactly.
      The IsolateManager ctor is no-arg per `isolate_manager.dart.md`.
    idiom_id: rf-dart-setup-to-xunit-constructor
    research_finding_id: rf-dart-setup-to-xunit-constructor
    nuance: >-
      Lifecycle nuance (cache hit, restated): both `package:test setUp`
      and xUnit constructor are per-test; both run on the same
      thread/isolate as the test body; both give fresh state per test.
      Async-setUp nuance: the Dart `setUp(() { ... })` here is
      SYNCHRONOUS (no `async`); the xUnit constructor is synchronous
      (constructors cannot be `async`). Match. If asynchronous setup
      were needed, the idiom would be `IAsyncLifetime.InitializeAsync`
      — not used in setUp here but used by tearDown — see the
      teardown construct below for the matching `DisposeAsync`.
  - construct_key: dart.package_test.tearDown_block_async
    source_form: "tearDown(() async { await manager.shutdown(); });"
    target_decision: >-
      Dart `tearDown(() async { ... })` runs after each test, awaited by
      the runner. Map to `public async Task DisposeAsync() { await
      _manager.ShutdownAsync(); }` on the test class — the second half
      of the `IAsyncLifetime` interface contract
      (https://xunit.net/docs/shared-context — IAsyncLifetime
      InitializeAsync + DisposeAsync). The matching
      `InitializeAsync` returns `Task.CompletedTask` since the
      synchronous setUp logic is already in the constructor. NOT
      `IDisposable.Dispose` — synchronous Dispose cannot await
      `ShutdownAsync`; collapsing the async-shutdown to a blocking wait
      via `.GetAwaiter().GetResult()` is a documented anti-pattern
      (deadlock risk in any sync-over-async path —
      https://devblogs.microsoft.com/dotnet/configureawait-faq/) and
      changes async semantics. The shutdown call name (`ShutdownAsync`
      vs `Shutdown`) follows the isolate_manager.dart convspec — the
      Async-suffix matches the .NET Task-naming convention; the test
      consumes whichever form the SUT pins.
    idiom_id: rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync
    research_finding_id: rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync
    nuance: >-
      Async-teardown nuance (LOAD-BEARING, explicitly addressed): xUnit
      pre-2.4 had no native async-teardown — IAsyncLifetime (introduced
      in 2.4) is the documented path
      (https://github.com/xunit/xunit/issues/1224). The interface has
      TWO methods (`InitializeAsync` + `DisposeAsync`), both
      `Task`-returning; implementing the class MUST provide both. The
      `InitializeAsync` here is a no-op `Task.CompletedTask`-returning
      method since the setUp logic is purely synchronous (assigning the
      manager). Codegen MUST emit BOTH methods on the test class.
      Alternative: collapse the setUp into `InitializeAsync` as well —
      `InitializeAsync() { _manager = new IsolateManager(); return
      Task.CompletedTask; }` — equally valid; codegen MAY combine the
      constructor and InitializeAsync, with InitializeAsync owning all
      setUp logic. The two-form choice is stylistic; both observably
      identical. xUnit runs InitializeAsync BEFORE the test method and
      DisposeAsync AFTER — matching package:test setUp/tearDown.
      Exception-during-teardown nuance: an exception thrown from
      `DisposeAsync` is reported by xUnit as a test failure (even if the
      test body itself passed); Dart's `tearDown` similarly reports
      teardown errors as test failures. Match.
  - construct_key: dart.package_test.test_call_with_async_body
    source_form: >-
      "test('fplay1 runs across isolates (1 agent)', () async {
         await _runPlay(manager, 'mad_fplay1.glp');
       }, timeout: Timeout(Duration(seconds: 30)));"
    target_decision: >-
      Map each `test(...)` call to a `[Fact(DisplayName = "<original
      label>")]` `public async Task` instance method on the test class.
      Body translates verbatim: `await _RunPlayAsync(_manager,
      "mad_fplay1.glp");`. xUnit awaits the returned Task; failures
      during the await surface as test failures with full stack traces
      (https://xunit.net/docs/async-tests). Cached idiom
      rf-dart-test-callback-to-xunit-method-body. The Dart `timeout:`
      named parameter is a separate construct (see
      dart.package_test.test_timeout_parameter below).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async-test nuance (cache hit): xUnit awaits the returned `Task` per
      https://xunit.net/docs/async-tests; failures during the await
      surface as test failures with full stack traces. No assertion in
      the body — success is the absence of an unhandled exception in the
      timeout window (the helper's `Task.Delay` lets the play run for N
      seconds and returns; if no exception is raised, the test passes).
      This is a documented xUnit pattern (https://xunit.net/docs/comparisons —
      "no assertion = pass on no exception"). Label-mangling nuance:
      the original Dart label `'fplay1 runs across isolates (1 agent)'`
      is preserved as the `DisplayName` (no PascalCase needed in
      DisplayName; the method NAME may be `Fplay1RunsAcrossIsolates_1Agent`
      or simply `Fplay1` — codegen MAY pick the short form since the
      DisplayName carries the human-readable label).
  - construct_key: dart.package_test.test_call_in_for_loop_parameterised
    source_form: >-
      "for (final n in [2, 3, 4, 5, 6, 8, 9]) {
         test('fplay$n runs across isolates (2 agents)', () async {
           await _runPlay(manager, 'mad_fplay$n.glp');
         }, timeout: Timeout(Duration(seconds: 30)));
       }
       for (final n in [10, 11]) {
         test('fplay$n runs across isolates (2 agents, time)', () async {
           await _runPlay(manager, 'mad_fplay$n.glp');
         }, timeout: Timeout(Duration(seconds: 30)));
       }"
    target_decision: >-
      Map each parameterised `for` loop over a list literal of test cases
      to a single `[Theory]` method with one `[InlineData(n)]` per
      iteration. First loop becomes a `[Theory] [InlineData(2)] [InlineData(3)]
      [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(8)]
      [InlineData(9)] public async Task FplayNRunsAcrossIsolates2Agents(int
      n) { await _RunPlayAsync(_manager, $"mad_fplay{n}.glp"); }`. Second
      loop becomes a second `[Theory]` method
      `FplayNRunsAcrossIsolates2AgentsTime(int n)` with `[InlineData(10)]`
      / `[InlineData(11)]`. Cached idiom
      rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata
      (recorded in `test/test_channel_construction.dart.md` and similar
      parameterised sibling tests). The Dart string interpolation
      `'mad_fplay$n.glp'` ⇒ C# interpolated string `$"mad_fplay{n}.glp"`
      (NOT `const string` — `n` is a method parameter, not a const). The
      Dart test label `'fplay$n runs across isolates (2 agents)'` ⇒
      `[Theory(DisplayName = "fplay{0} runs across isolates (2 agents)")]`
      — xUnit's DisplayName supports `{0}`-style format placeholders for
      theory parameters per
      https://xunit.net/docs/comparisons#parameterised-tests.
    idiom_id: rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata
    research_finding_id: rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata
    nuance: >-
      Parameterised-test nuance (cache hit): Dart `for (final n in [...])
      { test(...); }` registers one test per iteration (the loop runs at
      file-load time inside `main`'s `group` body); xUnit `[Theory]` +
      `[InlineData(...)]` is the documented counterpart — each
      `[InlineData]` becomes one runnable test case
      (https://xunit.net/docs/getting-started/v2/getting-started#write-first-tests).
      Test-isolation nuance (LOAD-BEARING): each parameterised case under
      xUnit gets its OWN test-class instance (constructor + DisposeAsync
      per case), matching Dart's per-test `setUp`/`tearDown` semantics —
      so each `fplayN` test runs against a FRESH `IsolateManager`. This
      is critical: the `IsolateManager` carries spawned isolate
      references in `_agentPorts`; sharing one across tests would leak.
      Combined-loops-vs-separate-theories nuance: the Dart source uses
      TWO loops because the test labels differ ('2 agents' vs '2 agents,
      time'); preserving the two `[Theory]` methods preserves this
      distinction. Codegen MUST NOT collapse the two loops into one
      `[Theory]` with all values 2-11 — the labels would diverge and the
      review-time signal (which tests exercise the time-extension) would
      be lost.
  - construct_key: dart.package_test.test_timeout_parameter
    source_form: "timeout: Timeout(Duration(seconds: 30))"
    target_decision: >-
      The `package:test` `timeout:` named argument on `test(...)` sets a
      per-test timeout; if the test runs longer, the runner aborts and
      reports a timeout failure (https://pub.dev/packages/test#timeouts).
      Map to xUnit's `[Fact(Timeout = 30000)]` / `[Theory(Timeout =
      30000)]` attribute property — the value is in MILLISECONDS
      (https://xunit.net/docs/comparisons — "xUnit uses Timeout in
      milliseconds"; not all xUnit versions support per-test Timeout —
      v2+ does via the `Timeout` named attribute property on `[Fact]`).
      For the fplay1/2-6/8-9/4b/10/11 tests, `Timeout = 30000`; for the
      fplay12 test, `Timeout = 45000`. Alternative recorded in the
      research finding: wrap the test body in a
      `cts.CancelAfter(TimeSpan.FromSeconds(30))` + `await
      _RunPlayAsync(...).WaitAsync(cts.Token)` followed by a
      `catch (OperationCanceledException) { Assert.Fail("Test timed out");
      }` — more explicit but more code. The `[Fact(Timeout = ...)]`
      attribute form is the idiomatic xUnit choice.
    idiom_id: rf-dart-package-test-timeout-to-xunit-fact-timeout-attribute
    research_finding_id: rf-dart-package-test-timeout-to-xunit-fact-timeout-attribute
    nuance: >-
      Timeout-attribute nuance (explicitly addressed): xUnit's
      `[Fact(Timeout = N)]` requires that test parallelisation be DISABLED
      for the class (xUnit's docs note that Timeout is enforced via a
      separate Task that runs the test body — under parallelisation, the
      Timeout's own thread interferes with test scheduling). Codegen MAY
      need to add `[Collection("BondsV2NonParallel")]` or
      `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
      to make Timeout reliable. NOT an issue for the Dart source (each
      test runs in its own isolate). Per-Theory-case timeout nuance: when
      `[Theory(Timeout = N)]` is applied, EACH `[InlineData]` case gets
      the SAME N-millisecond timeout — matches the Dart source's per-test
      timeout application inside the for loop (each loop iteration's
      `test(...)` gets its own `timeout:` argument with the same value).
      Time-precision nuance: Dart `Duration(seconds: 30)` and xUnit
      `Timeout = 30000` both represent 30 seconds; no precision drift.
  - construct_key: dart.string_interpolation.varname_in_single_quoted_string
    source_form: "'mad_fplay$n.glp'  // and 'fplay$n runs across isolates (2 agents)'"
    target_decision: >-
      Map to C# interpolated string `$"mad_fplay{n}.glp"`. Dart string
      interpolation `'...$var...'` ⇒ C# `$"...{var}..."` (cached idiom
      rf-dart-string-interpolation-to-csharp-interpolated-string,
      ubiquitous across the multiagent test convspec series). The leading
      `$` on the C# literal is required to enable interpolation.
    idiom_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    research_finding_id: rf-dart-string-interpolation-to-csharp-interpolated-string
    nuance: >-
      Interpolation-syntax nuance (cache hit): Dart bare-identifier
      `$name` and C# `{name}` are 1:1; Dart braced `${expr}` and C#
      `{expr}` are 1:1. Numeric formatting nuance: Dart `$n` for `int n`
      uses default int-to-string conversion; C# `{n}` uses
      `n.ToString(CultureInfo.CurrentCulture)` by default — for integer
      ranges 2-12 the two are observationally identical (no
      locale-dependent thousands-separators at these magnitudes).
  - construct_key: dart.sut.isolatemanager_lifecycle_three_method_call_sites
    source_form: >-
      "manager = IsolateManager();
       await manager.boot(config, traceConfig: TraceConfig(glp: false, mad: false));
       manager.start();
       await manager.shutdown();"
    target_decision: >-
      Four call sites against the `IsolateManager` SUT, each consumed via
      `lib/multiagent/isolate_manager.dart.md`'s per-SUT-file convspec:
      (a) `IsolateManager()` ⇒ `new IsolateManager()` (no-arg ctor).
      (b) `manager.boot(config, traceConfig: ...)` ⇒ `manager.BootAsync(config,
          new TraceConfig { Glp = false, Mad = false })` — `Task`-returning
          per the isolate_manager.dart convspec; the named argument
          `traceConfig:` is preserved as a C# named argument; the
          `TraceConfig` construction uses object-initialiser per the
          TraceConfig convspec construct (init-only properties).
      (c) `manager.start()` ⇒ `manager.Start()` — synchronous `void`-
          returning per the isolate_manager.dart convspec.
      (d) `manager.shutdown()` ⇒ `manager.ShutdownAsync()` — `Task`-
          returning per the isolate_manager.dart convspec; the
          `IsolateManager.Shutdown` body is `close mainPort + clear
          _agentPorts` and does NOT kill spawned isolates (external
          termination contract preserved verbatim).
      The PascalCase rename at every call site is the project-wide cached
      idiom rf-dart-camelcase-to-csharp-pascalcase. The Async-suffix
      naming on `BootAsync`/`ShutdownAsync` follows the
      isolate_manager.dart convspec — the test consumes whichever form
      the SUT pins (matching either bare names or Async-suffix names is
      acceptable; preferred is the Async-suffix per
      https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap#naming-parameters-and-return-types).
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Cross-file-dependency-aggregation nuance (cache hit, restated): no
      SUT-side decision is re-derived here (FR-012/SC-007: KB-resolved,
      not re-researched). The threading-model decision for
      `IsolateManager` is escalation-inherited from
      `isolate_manager.dart.md` — this test consumes the eventual ruling
      verbatim. Async-shape preservation: the Dart source `await`s both
      `boot` and `shutdown` — the C# port preserves this (both methods
      return `Task`; both call sites use `await`). The `start()` call is
      synchronous in BOTH sides — no `await` introduced. Identity-vs-value
      nuance: the `manager` variable holds a REFERENCE to the
      `IsolateManager` instance; per-test setUp/tearDown semantics ensure
      the same reference is used across boot/start/shutdown within one
      test, but a FRESH reference is created per test (cached idiom
      rf-dart-setup-to-xunit-constructor pinning constructor-per-test
      isolation).
  - construct_key: dart.sut.boot_config_mutation_via_property_setters
    source_form: >-
      "final loader = BootLoader();
       final config = loader.load(bootSource);
       config.projectDir = _bondsV2Dir;
       config.rootSelfGlpPath = _rootSelfGlp;"
    target_decision: >-
      Three call sites against the `BootLoader`/`BootConfig` SUT per
      `lib/multiagent/boot_loader.dart.md`:
      (a) `BootLoader()` ⇒ `new BootLoader()` (no-arg ctor; reference type).
      (b) `loader.load(bootSource)` ⇒ `loader.Load(bootSource)` —
          synchronous `BootConfig`-returning per the boot_loader.dart
          convspec.
      (c) Two property setters: `config.ProjectDir = TestPaths.BondsV2Dir;
          config.RootSelfGlpPath = TestPaths.RootSelfGlpPath;`. Per the
          boot_loader.dart convspec, `BootConfig` is a reference `class`
          (NOT a record, NOT a struct) with mutable C# properties for
          `ProjectDir` and `RootSelfGlpPath` — direct setter assignment
          preserved.
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Mutable-config-vs-immutable nuance (explicitly addressed): the
      Dart source MUTATES the `config` instance returned by
      `loader.load(...)` via property assignment — both `projectDir` and
      `rootSelfGlpPath` fields are non-final in the Dart source (Dart
      `String? projectDir; String rootSelfGlpPath;` — declared without
      `final`). The boot_loader.dart convspec pins these as C# `public
      string? ProjectDir { get; set; }` and `public string RootSelfGlpPath
      { get; set; }` — get-set auto-properties (NOT init-only, NOT
      get-only) preserving the mutation pattern. Reference-aliasing
      nuance (LOAD-BEARING): the Dart caller passes `config` BY
      REFERENCE to `manager.boot(...)`; the boot method internally
      aliases the same instance into its own state. The C# port
      preserves this — reference-class semantics give the same aliasing
      automatically. NOT a `record` (which would compare by value at the
      aliasing site, breaking identity-based equality). NOT a `struct`
      (which would copy on call, breaking the alias chain entirely —
      the caller's property writes would not be visible to the
      isolate_manager-side reads).
  - construct_key: dart.sut.traceconfig_construction_with_named_args
    source_form: "TraceConfig(glp: false, mad: false)"
    target_decision: >-
      Per `lib/multiagent/isolate_manager.dart.md`'s `TraceConfig` convspec
      construct (`dart.class.traceconfig_immutable_value_data_holder_three_const_fields_static_const_off`),
      `TraceConfig` is a C# `public sealed class` with init-only
      properties; construction is via object-initialiser: `new TraceConfig
      { Glp = false, Mad = false }`. ALTERNATIVE: since both flags are
      `false`, the cached `TraceConfig.Off` singleton is observationally
      identical — codegen MAY emit `TraceConfig.Off` instead of a fresh
      `new TraceConfig { Glp = false, Mad = false }`. The Dart source
      allocates a FRESH instance per `_runPlay` call (no static-cache
      use), so the faithful translation is the fresh `new TraceConfig {
      ... }`; the `Off`-singleton substitution is recorded as an
      optimisation alternative.
    idiom_id: rf-dart-named-required-partly-defaulted-to-csharp-positional-with-defaults
    research_finding_id: rf-dart-named-required-partly-defaulted-to-csharp-positional-with-defaults
    nuance: >-
      Const-class-vs-init-only nuance (cache hit, recorded at
      isolate_manager.dart): Dart const constructors produce
      canonicalised instances at compile time; C# has no analogous
      canonicalisation for user-defined types — `new TraceConfig { ...
      }` allocates a fresh instance. For the all-false flags this is
      observationally identical to `TraceConfig.Off`. Codegen MAY
      substitute the static singleton; the test does not observe the
      difference (the `IsolateManager.boot` method reads the `Glp` /
      `Mad` flags but does not compare TraceConfig instances by
      identity). Named-arg-preservation nuance: Dart `TraceConfig(glp:
      false, mad: false)` uses named constructor arguments; C# object-
      initialiser `{ Glp = false, Mad = false }` is the precise
      counterpart (NOT positional constructor arguments — the
      isolate_manager.dart convspec pins TraceConfig as init-only-
      properties + object-initialiser, NOT positional ctor).
  - construct_key: dart.print_statement.diagnostic_log_to_stdout
    source_form: "print('Skipping: ${bootFile.path} not found');"
    target_decision: >-
      Map to `Console.WriteLine($"Skipping: {bootFile} not found");`
      (the local `bootFile` is the path-string per the
      dart.import.dart_io_core_library decision; the Dart `.path`
      accessor is not needed in C# where `bootFile` is already a
      `string`). Cached idiom rf-dart-print-and-terminate-to-csharp-equivalent.
      Alternative: `ITestOutputHelper.WriteLine(...)` injected via
      constructor parameter — the xUnit-isolated capture mechanism (cached
      idiom rf-dart-print-to-xunit-itestoutputhelper-writeline,
      established at `test/test_channel_construction.dart.md` and
      `mad_cold_call_isolate_test.dart.md` for diagnostic-only prints).
      For THIS file the print is a one-time skip diagnostic on the test
      thread (no cross-thread concern); `ITestOutputHelper` is the
      IDIOMATIC xUnit choice and is preferred when test-isolated capture
      matters; `Console.WriteLine` is the simplest 1:1 translation.
    idiom_id: rf-dart-print-and-terminate-to-csharp-equivalent
    research_finding_id: rf-dart-print-and-terminate-to-csharp-equivalent
    nuance: >-
      Diagnostic-output nuance (cache hit): Dart `print(...)` writes to
      the OWNING isolate's stdout (which is the same process stdout for
      this test, since the print is on the test thread before any agent
      isolate is spawned). C# `Console.WriteLine` is thread-safe
      (https://learn.microsoft.com/dotnet/api/system.console.writeline)
      and writes to the process stdout. Test-runner-capture nuance:
      xUnit v2+ on .NET Core does NOT capture `Console.WriteLine`
      output — `ITestOutputHelper` is the recommended capture mechanism
      (https://xunit.net/docs/capturing-output). For THIS file the
      skip-diagnostic is a debugging aid, not assertion-load-bearing;
      loss of capture under .NET Core is acceptable. The
      `ITestOutputHelper` alternative is recorded; codegen MAY pick
      either based on project preference.
conversion_units:
  - "cu-1: file-scope using directives — using Xunit; using System; using System.IO; using System.Threading.Tasks; plus the SUT namespaces produced from glp_runtime/lib/multiagent/{boot_loader.dart,isolate_manager.dart}"
  - "cu-2: namespace declaration mirroring test/multiagent path (e.g. <RootNs>.Test.Multiagent)"
  - "cu-3: file-private constants — `file static class TestPaths { internal const string BondsV2Dir = \"../programs/bonds_v2\"; internal const string MadBootDir = BondsV2Dir + \"/mad_boot\"; internal const string RootSelfGlpPath = \"../programs/self.glp\"; }` (compile-time string concatenation preserves const-ness; alternative private-static-on-test-class for pre-C#-10)"
  - "cu-4: top-level test class `BondsV2IsolateTests : IAsyncLifetime` (from group label 'Bonds V2 Multi-Isolate'), `[Trait(\"Group\", \"Bonds V2 Multi-Isolate\")]`; `private IsolateManager _manager = null!;` field"
  - "cu-5: lifecycle members — `public BondsV2IsolateTests() { _manager = new IsolateManager(); }` constructor (translates setUp); `public Task InitializeAsync() => Task.CompletedTask;` no-op; `public async Task DisposeAsync() { await _manager.ShutdownAsync(); }` async-teardown (translates tearDown via IAsyncLifetime)"
  - "cu-6: private static async helper — `private static async Task _RunPlayAsync(IsolateManager manager, string bootFilename, int timeoutSec = 10)` with the body translating the Dart helper statement-for-statement (skip-on-missing-file diagnostic via Console.WriteLine; BootLoader+Load; BootConfig property setters; await manager.BootAsync(config, new TraceConfig { Glp = false, Mad = false }); manager.Start(); await Task.Delay(TimeSpan.FromSeconds(timeoutSec)))"
  - "cu-7: one `[Fact(DisplayName = \"fplay1 runs across isolates (1 agent)\", Timeout = 30000)] public async Task Fplay1RunsAcrossIsolates1Agent() { await _RunPlayAsync(_manager, \"mad_fplay1.glp\"); }`"
  - "cu-8: one `[Theory(DisplayName = \"fplay{0} runs across isolates (2 agents)\", Timeout = 30000)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(8)] [InlineData(9)] public async Task FplayNRunsAcrossIsolates2Agents(int n) { await _RunPlayAsync(_manager, $\"mad_fplay{n}.glp\"); }`"
  - "cu-9: one `[Fact(DisplayName = \"fplay4b runs across isolates (2 agents, time)\", Timeout = 30000)] public async Task Fplay4bRunsAcrossIsolates2AgentsTime() { await _RunPlayAsync(_manager, \"mad_fplay4b.glp\"); }`"
  - "cu-10: one `[Theory(DisplayName = \"fplay{0} runs across isolates (2 agents, time)\", Timeout = 30000)] [InlineData(10)] [InlineData(11)] public async Task FplayNRunsAcrossIsolates2AgentsTime(int n) { await _RunPlayAsync(_manager, $\"mad_fplay{n}.glp\"); }`"
  - "cu-11: one `[Fact(DisplayName = \"fplay12 runs across isolates (village, 6 agents)\", Timeout = 45000)] public async Task Fplay12RunsAcrossIsolatesVillage6Agents() { await _RunPlayAsync(_manager, \"mad_fplay12.glp\", timeoutSec: 15); }`"
  - "cu-12: optional `[assembly: CollectionBehavior(DisableTestParallelization = true)]` or per-class `[Collection(\"BondsV2NonParallel\")]` to make per-test Timeout enforcement reliable (xUnit-Timeout-with-parallelisation nuance — see dart.package_test.test_timeout_parameter)"
escalations: []
```

## Rationale + research provenance

### Why no escalations — inherited threading model

The .NET hosting model for the multiagent runtime's isolate-equivalent is
a real undecidable point, but it is ALREADY OWNED by
`lib/multiagent/isolate_manager.dart.md` (escalation
`dart.dart_isolate_api_to_csharp_execution_context_choice`). This test
file is a CONSUMER of that decision, not a new owner. The same
"don't double-escalate" discipline that
`test/multiagent/mad_cold_call_isolate_test.dart.md` applies (no new
isolate escalation; defer to heap_fcp / isolate_manager) is applied here.
Every threading-model-dependent construct (`IsolateManager` ctor,
`BootAsync`, `Start`, `ShutdownAsync`, `TraceConfig` construction)
DEFERS to the per-SUT-file convspec; this artifact records the test-side
shape only. `escalations: []` is intentional, not a placeholder.

Notably this test file does NOT exercise low-level `dart:isolate`
primitives directly (no `Isolate.spawn` / `SendPort` / `ReceivePort` /
`Completer` in the test body — unlike
`mad_cold_call_isolate_test.dart`, which inlines isolate entrypoints
and a custom message router). All multi-isolate behaviour is hidden
behind the `IsolateManager` SUT surface. The test is therefore a
THIN integration harness over the SUT — every Dart→C# decision flows
from existing SUT convspecs + cached idioms.

### xUnit pinning (cache hit)

Same authoritative basis as `mad_error_handling_test.dart.md`,
`boot_loader_test.dart.md`, `mad_cold_call_isolate_test.dart.md`,
and every multiagent test convspec: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Theory]` / `[Trait]` / constructor-per-test isolation, and
the Dart `package:test` README on `pub.dev`
(`https://pub.dev/packages/test`) for `group` / `test` / `setUp` /
`tearDown` / `timeout` semantics. No re-research.

### `IAsyncLifetime` for async-teardown (LOAD-BEARING new entry)

xUnit issue #1224 introduced `IAsyncLifetime` for shared-async-context;
the project-wide xUnit convention pinned by sibling convspecs uses
constructor-per-test isolation. The Dart `tearDown(() async { await
manager.shutdown(); })` is the FIRST async-teardown encountered in the
multiagent test convspec series — xUnit's `IDisposable.Dispose` is
synchronous and cannot await; `IAsyncLifetime.DisposeAsync` is the
documented async-teardown path
(https://xunit.net/docs/shared-context — IAsyncLifetime
InitializeAsync + DisposeAsync). The class implements both methods;
`InitializeAsync` returns `Task.CompletedTask` since the setUp logic is
synchronous and already lives in the constructor.

### `Future.delayed` → `Task.Delay` (cache hit)

Cached idiom `rf-dart-future-delayed-to-csharp-task-delay`. Microsoft
Learn `Task.Delay(TimeSpan)`
(`https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.delay`)
is the canonical .NET counterpart of Dart's `Future.delayed(Duration)`.
Both release the underlying thread/event-loop during the wait. Used at
the bottom of the helper as a fixed-duration "let the play run for N
seconds" gate.

### Per-test `timeout:` → `[Fact(Timeout = N_ms)]` (cache hit)

xUnit's `[Fact(Timeout = N)]` / `[Theory(Timeout = N)]` named attribute
property is the documented per-test timeout
(https://xunit.net/docs/comparisons). The value is in milliseconds.
Important caveat (well-known xUnit footgun): Timeout enforcement is
reliable only when test parallelisation is disabled for the class — the
recorded mitigation is `[Collection(...)]` or
`[assembly: CollectionBehavior(DisableTestParallelization = true)]`. The
alternative (manual `cts.CancelAfter` + `WaitAsync` + Assert.Fail
on `OperationCanceledException`) is more code but more reliable under
parallelisation — recorded as an alternative.

### `for (final n in [...]) test(...)` → `[Theory] + [InlineData]` (cache hit)

Cached idiom
`rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata`. xUnit
docs `https://xunit.net/docs/getting-started/v2/getting-started#write-first-tests`.
Each `[InlineData]` becomes one runnable test case under xUnit's
constructor-per-test isolation — matching Dart's per-iteration
`test(...)` registration. The two-loop structure (2-9 vs 10-11) is
preserved as TWO `[Theory]` methods to keep the distinct labels
('2 agents' vs '2 agents, time') visible at the test-runner level.

### `late T x;` + `setUp` → `private T _x = null!;` + constructor (cache hit)

Cached idiom `rf-dart-late-field-to-csharp-null-bang` +
`rf-dart-setup-to-xunit-constructor`. Established by
`ui_mediator_test.dart.md`; the `late + setUp` shape is the same as
this file's `late IsolateManager manager;` + `setUp(() { manager =
IsolateManager(); });`. Microsoft Learn 'init keyword' and
'Nullable reference types' for the `null!` semantics.

### `dart:io File` → `System.IO.File` (cache hit)

Cached idiom `rf-dart-dart-io-file-to-dotnet-system-io-file`.
Dart instance-method `File(p).existsSync()` ⇒ .NET static
`File.Exists(p)`; Dart `File(p).readAsStringSync()` ⇒
`File.ReadAllText(p)`. Microsoft Learn `System.IO.File`
(`https://learn.microsoft.com/dotnet/api/system.io.file`). Synchronous IO
preserved on both sides.

### SUT call-site translation via per-SUT-file convspec (FR-012 cache hits)

Every SUT API call (`IsolateManager()`, `manager.boot(...)`,
`manager.start()`, `manager.shutdown()`, `BootLoader()`, `loader.load(...)`,
`config.projectDir = ...`, `config.rootSelfGlpPath = ...`,
`TraceConfig(...)` construction) is decided by the corresponding per-SUT-file
convspec (`lib/multiagent/isolate_manager.dart.md`,
`lib/multiagent/boot_loader.dart.md`). This test spec records only the
SHAPE of the cross-file dependency — names, types, and call shapes
come from the per-SUT convspecs. No SUT-side decision is re-derived
(FR-024 + FR-012/SC-007). The Async-suffix on `BootAsync`/`ShutdownAsync`
follows the .NET Task-naming convention
(`https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap#naming-parameters-and-return-types`);
the bare names `Boot`/`Shutdown` are also acceptable per the
isolate_manager.dart convspec — codegen pins either; the test consumes
whichever.
