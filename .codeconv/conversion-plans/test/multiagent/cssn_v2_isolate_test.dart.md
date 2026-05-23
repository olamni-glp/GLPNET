---
path: test/multiagent/cssn_v2_isolate_test.dart
cycle_group_id: 142
scc_siblings: []
generated_at: 2026-05-21T16:56:01Z
source_sha256: ced133bbafaf1744fb59e6375e58cd5b7d825e2998c2cdf473a9cf2443b6c23d
schema_version: 1
---

# Conversion Plan: test/multiagent/cssn_v2_isolate_test.dart

## 1. Source Analysis

Direct inspection of `glp_runtime_net/test/multiagent/cssn_v2_isolate_test.dart` (85 lines, sha256 `ced133bbafaf1744fb59e6375e58cd5b7d825e2998c2cdf473a9cf2443b6c23d`):

- **Imports** (4): `dart:io` (for `File`), `package:test/test.dart` (test framework), `package:glp_runtime/multiagent/boot_loader.dart` (SUT), `package:glp_runtime/multiagent/isolate_manager.dart` (SUT).
- **Top-level constants** (3, all library-private leading-underscore): `_cssnV2Dir = '../programs/cssn_modules_v2'`, `_madBootDir = '$_cssnV2Dir/mad_boot'` (string-interpolated const), `_rootSelfGlp = '../programs/self.glp'`.
- **Top-level async helper** (1): `Future<void> _runPlay(IsolateManager manager, String bootFilename, {int timeoutSec = 10}) async`. Body: builds `File('$_madBootDir/$bootFilename')`; if `!bootFile.existsSync()` prints `'Skipping: ${bootFile.path} not found'` and returns; reads `bootFile.readAsStringSync()`; constructs `BootLoader()`, calls `loader.load(bootSource)`, mutates `config.projectDir` / `config.rootSelfGlpPath`; awaits `manager.boot(config, traceConfig: TraceConfig(glp: false, mad: false))`; calls `manager.start()`; awaits `Future.delayed(Duration(seconds: timeoutSec))`.
- **`void main()`** containing exactly one `group('CSSN v2 Multi-Isolate', () { ... })`. Inside the group: `late IsolateManager manager;` field; synchronous `setUp` assigns `manager = IsolateManager()`; async `tearDown` awaits `manager.shutdown()`; 13 tests generated via three `for`-loops (`[1,2,3]`, `[4,5,6,7]`, `[9,10]`) plus four standalone `test(...)` calls (fplay8, fplay11, fplay12, fplay13). Twelve tests carry `timeout: Timeout(Duration(seconds: 30))`; fplay13 carries `timeout: Timeout(Duration(seconds: 45))` and a non-default `timeoutSec: 15`.
- **Threading model**: this file is a thin DRIVER — it only sees the public surface of `IsolateManager.boot`/`start`/`shutdown` and never introspects isolates, send-ports, or message routing. The threading-primitive choice is owned by `lib/multiagent/isolate_manager.dart.md` (which itself inherits from `lib/runtime/heap_fcp.dart.md`).

## 2. Dart → C#/.NET Conversion Plan

Mirror of convspec `constructs` (each Dart construct → C#/.NET target):

1. **`import 'dart:io';`** → drop directive; add `using System.IO;`. Replace `File(path).existsSync()` / `.readAsStringSync()` (instance API) with `System.IO.File.Exists(path)` / `File.ReadAllText(path)` (static API). UTF-8 default encoding matches on both sides. Sync shape preserved (no `Async` variant since the Dart source is sync).
2. **`import 'package:test/test.dart';`** → drop; add `using Xunit;` plus `using System;` (`TimeSpan`), `using System.Threading.Tasks;` (`Task`, `Task.Delay`), `using System.Threading;` (`CancellationTokenSource` if needed — none used here). Cache hit on project-wide xUnit pinning (FR-012 / SC-007).
3. **`import 'package:glp_runtime/multiagent/{boot_loader,isolate_manager}.dart';`** → single `using <RootNs>.Multiagent;`. Symbols pulled: `BootLoader`, `BootConfig`, `IsolateManager`, `TraceConfig`.
4. **Three top-level `const String` declarations** → three `private const string` class-level fields on the enclosing test class, leading-underscore + PascalCase: `private const string _CssnV2Dir = "../programs/cssn_modules_v2";`, `private const string _MadBootDir = _CssnV2Dir + "/mad_boot";` (compile-time-constant `+`-concat over two `const` operands; NOT `$"…"` since interpolated strings aren't `const` in pre-C#10 contexts and never in attribute parameters), `private const string _RootSelfGlp = "../programs/self.glp";`.
5. **`Future<void> _runPlay(...) async`** → `private static async Task _RunPlayAsync(IsolateManager manager, string bootFilename, int timeoutSec = 10)`. Named-arg call-site preservation: `_RunPlayAsync(_manager, "mad_fplay13.glp", timeoutSec: 15)`. `Async` suffix per .NET TAP guideline.
6. **`File(...)` / `existsSync` / `readAsStringSync` / `bootFile.path`** → `var bootFilePath = _MadBootDir + "/" + bootFilename;` (or `$"…"` interp), `if (!File.Exists(bootFilePath)) { Console.WriteLine("Skipping: " + bootFilePath + " not found"); return; }`, `var bootSource = File.ReadAllText(bootFilePath);`. The `bootFile.path` getter folds into the local string.
7. **`print('Skipping: ${bootFile.path} not found')`** → `Console.WriteLine("Skipping: " + bootFilePath + " not found");` (or `$"Skipping: {bootFilePath} not found"`). `Console.WriteLine` (not `ITestOutputHelper`) pinned for cross-thread safety since agents print on their own threads.
8. **`BootLoader()` + `loader.load(...)` + settable `.projectDir`/`.rootSelfGlpPath`** → `var loader = new BootLoader(); var config = loader.Load(bootSource); config.ProjectDir = _CssnV2Dir; config.RootSelfGlpPath = _RootSelfGlp;`. SUT decisions inherited from `lib/multiagent/boot_loader.dart.md` (mutable settable properties; instance `Load` method).
9. **`manager.boot(config, traceConfig: TraceConfig(glp: false, mad: false))`** → `await manager.BootAsync(config, traceConfig: new TraceConfig(glp: false, mad: false));`. SUT decisions inherited from `lib/multiagent/isolate_manager.dart.md` (`BootAsync` + `TraceConfig` positional-with-default ctor; `traceConfig` parameter named at call site for clarity).
10. **`manager.start()`** → `manager.Start();` synchronous void-returning; preserves Dart fire-and-forget shape.
11. **`await Future.delayed(Duration(seconds: timeoutSec))`** → `await Task.Delay(TimeSpan.FromSeconds(timeoutSec));`. Both async; both 100-ns precision. `Thread.Sleep` rejected (would block thread).
12. **`setUp(() { manager = IsolateManager(); })`** → xUnit test class CONSTRUCTOR body: `public CssnV2IsolateTests() { _manager = new IsolateManager(); }`. Field becomes `private readonly IsolateManager _manager;` (assigned-once-in-ctor maps Dart `late` → C# `readonly`).
13. **`tearDown(() async { await manager.shutdown(); })`** → test class implements `IAsyncLifetime` with `public Task InitializeAsync() => Task.CompletedTask;` and `public async Task DisposeAsync() { await _manager.ShutdownAsync(); }`. `IDisposable.Dispose` rejected (would force deadlock-prone blocking await).
14. **Three for-loops generating tests** → three `[Theory]` methods with one `[InlineData(n)]` per iteration: `Fplay_RunsAcrossIsolates_3Adults(int n)` with `[InlineData(1)][InlineData(2)][InlineData(3)]`; `Fplay_RunsAcrossIsolates_4Agents(int n)` with `[InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]`; `Fplay_RunsAcrossIsolates_3Agents(int n)` with `[InlineData(9)][InlineData(10)]`. Each body: `await _RunPlayAsync(_manager, $"mad_fplay{n}.glp");`. `[Theory(Timeout = 30000)]` per method.
15. **Four standalone `test('…', body, timeout: Timeout(Duration(seconds: 30|45)))` calls** → four `[Fact]` methods: `Fplay8_RunsAcrossIsolates_2Adults` (`[Fact(Timeout = 30000)]`), `Fplay11_RunsAcrossIsolates_6Agents` (`[Fact(Timeout = 30000)]`), `Fplay12_RunsAcrossIsolates_5Agents` (`[Fact(Timeout = 30000)]`), `Fplay13_RunsAcrossIsolates_Village_6Agents` (`[Fact(Timeout = 45000)]`, body `await _RunPlayAsync(_manager, "mad_fplay13.glp", timeoutSec: 15);`).
16. **`void main()`** → dropped (xUnit discovers `[Fact]`/`[Theory]` by reflection); the lone `group(...)` body becomes the enclosing test class.
17. **`group('CSSN v2 Multi-Isolate', () { ... })`** → `public class CssnV2IsolateTests : IAsyncLifetime` with `[Trait("Group", "CSSN v2 Multi-Isolate")]` preserving the original label.
18. **String interpolation `'…$expr…'` / `'…${expr}…'`** → C# `$"…{expr}…"` inside method bodies; FOLDED to `+`-concat over const operands in const-context (top-level constants) since interpolated strings are not `const` in attribute parameters and pre-C#10 const-field contexts.
19. **Per-test `timeout: Timeout(Duration(seconds: N))`** → `[Fact(Timeout = N*1000)]` / `[Theory(Timeout = N*1000)]`. Footgun explicit: xUnit `Timeout` takes raw `int` MILLISECONDS, not a `TimeSpan`.

## 3. Decomposed Task Units

- T1: emit file-scope `using` directives (`System`, `System.IO`, `System.Threading.Tasks`, `Xunit`, `<RootNs>.Multiagent`) + namespace declaration `<RootNs>.Test.Multiagent`. — done
- T2: emit `public class CssnV2IsolateTests : IAsyncLifetime` with `[Trait("Group", "CSSN v2 Multi-Isolate")]`. — done
- T3: emit three `private const string` fields (`_CssnV2Dir`, `_MadBootDir = _CssnV2Dir + "/mad_boot"`, `_RootSelfGlp`) and one `private readonly IsolateManager _manager;`. — done
- T4: emit constructor `public CssnV2IsolateTests() { _manager = new IsolateManager(); }` mapping the Dart `setUp`. — done
- T5: emit `public Task InitializeAsync() => Task.CompletedTask;` and `public async Task DisposeAsync() { await _manager.ShutdownAsync(); }` (`IAsyncLifetime`). — done
- T6: emit `private static async Task _RunPlayAsync(IsolateManager manager, string bootFilename, int timeoutSec = 10)` helper with body translating: path-concat local, `File.Exists` skip-on-missing + `Console.WriteLine`, `File.ReadAllText`, `BootLoader`/`Load`/property-set chain, `await BootAsync` (named `traceConfig:`), `Start()`, `await Task.Delay(TimeSpan.FromSeconds(timeoutSec))`. — done
- T7: emit `[Theory]` method `Fplay_RunsAcrossIsolates_3Adults(int n)` with `[InlineData(1..3)] [Theory(Timeout = 30000)]`, body `await _RunPlayAsync(_manager, $"mad_fplay{n}.glp");`. — done
- T8: emit `[Theory]` method `Fplay_RunsAcrossIsolates_4Agents(int n)` with `[InlineData(4..7)] [Theory(Timeout = 30000)]`, body identical shape. — done
- T9: emit `[Theory]` method `Fplay_RunsAcrossIsolates_3Agents(int n)` with `[InlineData(9), (10)] [Theory(Timeout = 30000)]`, body identical shape. — done
- T10: emit four `[Fact]` methods (fplay8 / 11 / 12 / 13) with `Timeout = 30000` (8, 11, 12) and `Timeout = 45000` (13); fplay13 body passes `timeoutSec: 15`. — done
- T11: drop Dart `void main()` (no C# counterpart; xUnit reflects classes). — done
- T12: SUT call-site PascalCase + `Async`-suffix rename inherited from per-SUT-file convspecs (no decision re-made here). — done

## 4. Research Findings

none required — every construct resolves to a cached idiom (`rf-dart-package-test-import-to-xunit-using`, `rf-dart-package-sut-import-to-csharp-using`, `rf-dart-toplevel-const-private-to-csharp-private-const-field`, `rf-dart-future-void-async-to-csharp-task-async`, `rf-dart-dart-io-file-to-dotnet-system-io-file`, `rf-dart-print-and-terminate-to-csharp-equivalent`, `rf-dart-sut-call-site-translation-via-per-sut-convspec`, `rf-dart-future-delayed-to-csharp-task-delay`, `rf-dart-duration-to-csharp-timespan`, `rf-dart-setUp-to-xunit-constructor`, `rf-dart-tearDown-async-to-xunit-iasynclifetime-disposeasync`, `rf-dart-package-test-for-loop-to-xunit-theory-inlinedata`, `rf-dart-test-timeout-to-xunit-fact-timeout`, `rf-dart-package-test-main-omit-in-xunit`, `rf-dart-package-test-group-to-xunit-class`, `rf-dart-string-interpolation-to-csharp-interpolated-string`, `rf-dart-leading-underscore-private-to-csharp-private`, `rf-dart-named-optional-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`) or to per-SUT-file convspec decisions already pinned in `.codeconv/conversion-specs/lib/multiagent/{boot_loader,isolate_manager}.dart.md`. The threading-model question is owned by `isolate_manager.dart.md` (which inherits from `heap_fcp.dart.md`); this file's spec INHERITS those decisions per FR-013 "don't double-escalate" discipline (precedent: `mad_cold_call_isolate_test.dart.md`).

## 5. Consistency Pass

fixed — derived from convspec construct mappings + `conversion_units` cu-1..cu-12 verbatim. Cross-checked against:
- the source file (sha256 confirmed `ced133bbafaf1744fb59e6375e58cd5b7d825e2998c2cdf473a9cf2443b6c23d`, 85 lines, 4 imports, 3 top-level consts, 1 helper, 1 group, 13 tests across 3 for-loops + 4 standalone calls);
- the convspec's `escalations: []` (intentional, justified in the convspec rationale §"Why no escalations on isolate / threading / multi-isolate hosting");
- per-SUT-file convspecs `lib/multiagent/boot_loader.dart.md` (BootLoader/BootConfig shape) and `lib/multiagent/isolate_manager.dart.md` (IsolateManager + TraceConfig shape + threading-model inheritance);
- precedent test-file plans for the multiagent family (`mad_cold_call_isolate_test`, `boot_loader_test`, `mad_scenarios_test`) — IAsyncLifetime / Theory+InlineData / Fact-Timeout / IsolateManager-driver patterns are consistent across the family.

No new decisions introduced; the plan is a faithful mirror of the ratified convspec.

## 6. Escalations

None.
