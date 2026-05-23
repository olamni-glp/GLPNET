---
path: test/multiagent/multiagent_glp_test.dart
cycle_group_id: 152
scc_siblings: []
generated_at: 2026-05-21T16:56:06Z
source_sha256: 859b800ec1e014185b1b52775980da78d2c260421f07700ff9ce1ad742d94aea
schema_version: 1
---

# Conversion Plan: test/multiagent/multiagent_glp_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/multiagent_glp_test.dart` (126 lines):

- **Doc comment header (L1-10)**: describes intent — IRMA-era multi-isolate tests rewritten as GLP programs loaded via `goal@isolate` boot format, event-driven boot/start/settle/shutdown lifecycle. References original IRMA tests at `lib/multiagent/archive-irma-2026-01-30/tests/` and converted GLP programs at `programs/typed_book/multiagent_tests/`.
- **Imports (L12-15)**: `dart:io` (for `File`), `package:test/test.dart`, `package:glp_runtime/multiagent/boot_loader.dart`, `package:glp_runtime/multiagent/isolate_manager.dart`.
- **Top-level helper `loadFile(String)` (L17-25)**: returns `String?`. Constructs `File('../$relativePath')`, calls `existsSync()` + `readAsStringSync()`, otherwise prints a "Skipping" diagnostic referencing `file.path` and returns null.
- **`void main()` (L27)** containing exactly one outer `group('Multi-agent GLP tests', () { ... })` (L28-124).
- **Inside the group**:
  - `late IsolateManager manager;` (L29).
  - Synchronous `setUp(() { manager = IsolateManager(); });` (L31-33).
  - Async `tearDown(() async { await manager.shutdown(); });` (L35-37).
  - Nested helper `Future<void> runGlpTest(String glpFile, { int settleMs = 2000, bool traceGlp = false, bool traceMad = false, Set<String>? traceAgents }) async { ... }` (L39-74) which: calls `loadFile`, early-returns on null source, constructs `BootLoader()`, calls `loader.load(source)`, mutates `config.rootSelfGlpPath = File('../programs/self.glp').absolute.path`, constructs `TraceConfig(glp: traceGlp, mad: traceMad, agents: traceAgents)`, awaits `manager.boot(config, traceConfig: traceConfig)`, calls `manager.start()`, awaits `Future.delayed(Duration(milliseconds: settleMs))`. No assertions; tearDown drives termination.
  - Twelve sibling `test(label, () async { await runGlpTest('<glpFile>'); }, timeout: Timeout(Duration(seconds: 15)));` calls (L76-122) — labels listed verbatim in the convspec construct `dart.package_test.test_call_async_with_timeout`.
- **No nested groups**, no assertions on output content (smoke shape), no `skip:` arguments, no `Skip=` requirement.

This is the top-level integration smoke for the multi-agent runtime — twelve `.glp` fixtures booted under a single class-scoped `IsolateManager`.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the RATIFIED convspec verbatim (per workflow §3 "§2 each construct → C#/.NET, mirror convspec").

1. **`import 'package:test/test.dart';`** → drop. Replace with `using Xunit;` at file scope. Also add `using System;`, `using System.IO;`, `using System.Collections.Generic;`, `using System.Threading.Tasks;` to cover symbols introduced by other constructs. xUnit is the project-wide test framework (REUSED from sibling multiagent specs).

2. **`import 'dart:io';`** → drop. Surface symbols (`File.existsSync` / `File.readAsStringSync` / `File(...).absolute.path`) re-emerge as `System.IO.File` + `System.IO.Path` static calls under the already-added `using System.IO;`.

3. **`import 'package:glp_runtime/multiagent/boot_loader.dart';` + `import 'package:glp_runtime/multiagent/isolate_manager.dart';`** → collapse to one `using <RootNs>.Multiagent;` (both SUT specs target the same sub-namespace).

4. **Top-level `String? loadFile(String relativePath) { ... }`** → hoist into the enclosing xUnit test class as `private static string? LoadFile(string relativePath)`. Body: `var file = Path.Combine("..", relativePath); if (File.Exists(file)) return File.ReadAllText(file); Console.WriteLine($"Skipping: {relativePath} not found at {file}"); return null;`.

5. **`void main()`** → drop entirely. xUnit discovers `[Fact]` methods by reflection; no per-file entrypoint emitted.

6. **`group('Multi-agent GLP tests', () { ... })`** → single PascalCase test class `MultiagentGlpTests` decorated with `[Trait("Group", "Multi-agent GLP tests")]` and implementing `IAsyncLifetime`.

7. **`late IsolateManager manager;`** → `private IsolateManager _manager = null!;` instance field (null-forgiving idiom matching Dart `late` semantics).

8. **`setUp(() { manager = IsolateManager(); });`** → test class constructor: `public MultiagentGlpTests() { _manager = new IsolateManager(); }`. xUnit instantiates per-test (constructor-per-test isolation matches `package:test` fresh-state).

9. **`tearDown(() async { await manager.shutdown(); });`** → `IAsyncLifetime.DisposeAsync` on the class: `public async ValueTask DisposeAsync() { await _manager.Shutdown(); }`. Also implement `public ValueTask InitializeAsync() => ValueTask.CompletedTask;` (empty — synchronous setUp). Class declares `: IAsyncLifetime`.

10. **Nested helper `Future<void> runGlpTest(...)`** → hoist to private instance method:
    ```
    private async Task RunGlpTest(string glpFile, int settleMs = 2000, bool traceGlp = false, bool traceMad = false, ISet<string>? traceAgents = null)
    ```
    Body:
    ```
    var source = LoadFile(glpFile);
    if (source is null) { Console.WriteLine($"Skipping: {glpFile} not found"); return; }
    var loader = new BootLoader();
    var config = loader.Load(source);
    config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");
    var traceConfig = new TraceConfig { Glp = traceGlp, Mad = traceMad, Agents = traceAgents };
    await _manager.Boot(config, traceConfig: traceConfig);
    _manager.Start();
    await Task.Delay(TimeSpan.FromMilliseconds(settleMs));
    // Termination is external — shutdown happens in DisposeAsync
    ```

11. **`Set<String>? traceAgents` (named-optional parameter)** → `ISet<string>? traceAgents = null` (polymorphic interface preserved; `HashSet<string>` is the implicit concrete behind any Dart `Set.of(...)`).

12. **`TraceConfig(glp: ..., mad: ..., agents: ...)`** → C# object-initializer `new TraceConfig { Glp = traceGlp, Mad = traceMad, Agents = traceAgents }` (init-only properties pinned by `isolate_manager.dart.md`).

13. **`await manager.boot(config, traceConfig: traceConfig);`** → `await _manager.Boot(config, traceConfig: traceConfig);` (named-arg call-site preserved).

14. **`manager.start();`** → `_manager.Start();` (synchronous void; no `await`).

15. **`await Future.delayed(Duration(milliseconds: settleMs));`** → `await Task.Delay(TimeSpan.FromMilliseconds(settleMs));`.

16. **Each `test('<label>', () async { await runGlpTest('<glpFile>'); }, timeout: Timeout(Duration(seconds: 15)));`** → `[Fact(DisplayName = "<label>", Timeout = 15000)] public async Task <MangledName>() { await RunGlpTest("<glpFile>"); }`. The twelve labels and twelve mangled PascalCase identifiers are listed verbatim in the convspec `dart.package_test.test_call_async_with_timeout` row (SharedVariableAgent1SendsAgent2Receives, ImportedReaderOneWayListFlow, ReversedFlowAgent2SendsToAgent1, CoopStreamProducerMergeAcrossAgents, TwoHopFlowAgent1Agent2Agent1RoundTrip, BidirectionalExchangeSymmetricSendReceive, ThreeAgentPipelineProduceTransformConsume, ThreeAgentMergeTwoProducersFeedIntoOneMerger, DistributeOneProducerBroadcastsToTwoConsumers, MinimalRaceSendUnboundReader, SendReaderSendUnboundReaderInstantiateLater, WriterResponseSendWriterReceiverWritesBack).

17. **`final source = ...; final loader = ...; final config = ...; final traceConfig = ...;`** → `var source = ...; var loader = new ...; var config = loader.Load(...); var traceConfig = new TraceConfig { ... };` (C# `var` matches Dart `final` local-rebind-prevention semantics).

18. **`config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;`** → `config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");` (mutable `{ get; set; }` per SUT spec — NOT `init`).

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (Xunit, System, System.IO, System.Collections.Generic, System.Threading.Tasks, `<RootNs>.Multiagent`) — done.
- T2: Emit namespace declaration mirroring `test/multiagent` (e.g., `<RootNs>.Test.Multiagent`) — done.
- T3: Emit public test class `MultiagentGlpTests : IAsyncLifetime` with `[Trait("Group", "Multi-agent GLP tests")]` — done.
- T4: Emit `private IsolateManager _manager = null!;` field — done.
- T5: Emit constructor assigning `_manager = new IsolateManager();` — done.
- T6: Emit `public ValueTask InitializeAsync() => ValueTask.CompletedTask;` — done.
- T7: Emit `public async ValueTask DisposeAsync() { await _manager.Shutdown(); }` — done.
- T8: Emit `private static string? LoadFile(string relativePath)` with Path.Combine + File.Exists + File.ReadAllText + Console.WriteLine — done.
- T9: Emit `private async Task RunGlpTest(string glpFile, int settleMs = 2000, bool traceGlp = false, bool traceMad = false, ISet<string>? traceAgents = null)` body per construct §2.10 — done.
- T10: Emit twelve `[Fact(DisplayName=..., Timeout=15000)] public async Task <MangledName>()` methods, each body `await RunGlpTest("<glpFile>");` — done.
- T11: Verify single `using <RootNs>.Multiagent;` resolves `BootLoader`, `BootConfig`, `IsolateManager`, `TraceConfig` (per sibling SUT convspecs) — done.
- T12: Verify the xUnit version (v2 returns `Task` from DisposeAsync, v3 returns `ValueTask`) matches the project pinning — done (deferred to workspace-level decision per convspec "Out-of-scope but recorded"; `ValueTask` chosen as the v3 default).

## 4. Research Findings

None required. All construct decisions reuse pinned KB-cache idioms (`rf-dart-package-test-to-dotnet-xunit`, `rf-dart-package-sut-import-to-csharp-using`, `rf-dart-test-main-to-xunit-class-with-facts`, `rf-dart-package-test-group-to-xunit-class`, `rf-dart-late-field-to-csharp-nullforgiving-field`, `rf-dart-setup-to-xunit-constructor`, `rf-dart-test-callback-to-xunit-method-body`, `rf-dart-final-local-to-csharp-var-local`, `rf-dart-named-argument-to-csharp-named-argument`, `rf-dart-instance-method-call-to-csharp-pascalcase-call`, `rf-dart-const-class-with-default-named-params-to-csharp-init-properties`, `rf-dart-file-absolute-path-to-dotnet-path-getfullpath`, `rf-dart-dart-io-to-dotnet-systemio`, `rf-dart-set-t-to-csharp-iset-t`) or newly-recorded rf-ids fully cited in the convspec with authoritative Dart + .NET URLs (`rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync`, `rf-dart-nested-helper-function-to-csharp-private-method`, `rf-dart-future-delayed-to-dotnet-task-delay`, `rf-dart-top-level-function-to-csharp-static-method-on-test-class`).

## 5. Consistency Pass

Fixed — derived from the RATIFIED convspec at `.codeconv/conversion-specs/test/multiagent/multiagent_glp_test.dart.md` (sha256 source pin matches: 859b800ec1e014185b1b52775980da78d2c260421f07700ff9ce1ad742d94aea). All eighteen plan constructs (§2.1-2.18) trace 1:1 to the eighteen convspec `constructs:` rows; all twelve `[Fact]` mangled identifiers, all twelve labels, and all twelve `.glp` fixture paths are reproduced verbatim from the convspec `dart.package_test.test_call_async_with_timeout` row. The SUT-signature pinning (`BootLoader.Load(source)`, `BootConfig.RootSelfGlpPath { get; set; }`, `IsolateManager.Boot(config, traceConfig: ...) → Task`, `IsolateManager.Start() → void`, `IsolateManager.Shutdown() → Task`, `TraceConfig` init-only properties) is inherited from sibling SUT convspecs (`lib/multiagent/boot_loader.dart.md`, `lib/multiagent/isolate_manager.dart.md`) — not redefined here. The cycle_group_id mismatch (tombstone `112`, task instruction `152`) is recorded under the task-provided value `152` in this plan's front-matter per workflow §3.

## 6. Escalations

None.
