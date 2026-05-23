---
path: test/multiagent/bonds_v2_isolate_test.dart
cycle_group_id: 140
scc_siblings: []
generated_at: 2026-05-21T17:05:00Z
source_sha256: 18e788ee20ad20f262700ad47895d6a6cdefae27818f7c356f7c719a38512e5c
schema_version: 1
---

# Conversion Plan: test/multiagent/bonds_v2_isolate_test.dart

## 1. Source Analysis

Inspected the actual `.dart` source (73 lines). The file is a thin
integration test harness over the `IsolateManager` + `BootLoader` SUT
surfaces; it spawns no `dart:isolate` primitives directly. Concrete
constituents observed:

- 4 `import` directives — `dart:io` (for `File`), `package:test/test.dart`,
  and two `package:glp_runtime/multiagent/...` SUT imports
  (`boot_loader.dart`, `isolate_manager.dart`).
- 3 top-level file-private `const` string declarations (`_bondsV2Dir`,
  `_madBootDir`, `_rootSelfGlp`); `_madBootDir` uses compile-time string
  interpolation over the previous const.
- 1 file-private top-level async helper `_runPlay(IsolateManager manager,
  String bootFilename, {int timeoutSec = 10}) → Future<void>` carrying:
  (a) `File(...)` construction + `existsSync()` short-circuit with
  `print('Skipping: ${bootFile.path} not found')` + early return;
  (b) `readAsStringSync()`; (c) `BootLoader()` + `loader.load(bootSource)`;
  (d) two property setters on `config` (`projectDir`, `rootSelfGlpPath`);
  (e) `await manager.boot(config, traceConfig: TraceConfig(glp: false,
  mad: false))`; (f) `manager.start()`; (g) `await Future.delayed(
  Duration(seconds: timeoutSec))`.
- 1 `void main()` containing exactly one `group('Bonds V2 Multi-Isolate',
  …)` block.
- Inside the group: `late IsolateManager manager;` + sync `setUp` (manager
  = IsolateManager()) + async `tearDown` (await manager.shutdown()).
- 11 `test(...)` registrations distributed across 5 lexical sites:
  - 1 single `test('fplay1 …', …)` (solo, `Timeout` 30s).
  - 1 `for (final n in [2,3,4,5,6,8,9])` loop registering 7 tests with
    label `'fplay$n runs across isolates (2 agents)'` (`Timeout` 30s).
  - 1 single `test('fplay4b …', …)` (`Timeout` 30s).
  - 1 `for (final n in [10, 11])` loop registering 2 tests with label
    `'fplay$n runs across isolates (2 agents, time)'` (`Timeout` 30s).
  - 1 single `test('fplay12 …', …)` calling `_runPlay(…, timeoutSec: 15)`
    with `Timeout` 45s.
- No assertions anywhere in the test bodies — success criterion is "no
  unhandled exception in the timeoutSec window after boot+start"
  (xUnit "no-exception = pass" matches Dart `package:test`).
- No direct `dart:isolate` primitives; all multi-isolate behaviour is
  hidden behind the `IsolateManager` SUT. The file is therefore a
  CONSUMER of the threading-model decision owned by
  `lib/multiagent/isolate_manager.dart.md`.

## 2. Dart → C#/.NET Conversion Plan

Per-construct decisions mirror the RATIFIED convspec verbatim
(`.codeconv/conversion-specs/test/multiagent/bonds_v2_isolate_test.dart.md`).

1. `import 'dart:io';` → drop the line; codegen adds `using System.IO;`
   at file scope. `File(p).existsSync()` → `File.Exists(p)` (static).
   `File(p).readAsStringSync()` → `File.ReadAllText(p)` (static). The
   `.path` accessor is unneeded because the C# `bootFile` local is
   already a `string`. (idiom `rf-dart-dart-io-file-to-dotnet-system-io-file`)
2. `import 'package:test/test.dart';` → `using Xunit;`; codegen also
   adds `using System;`, `using System.Threading.Tasks;`, `using
   System.IO;`. (idiom `rf-dart-package-test-import-to-xunit-using`)
3. `import 'package:glp_runtime/multiagent/{boot_loader,isolate_manager}.dart';`
   → single `using <RootNs>.Multiagent;` (both SUTs share the namespace
   per their per-SUT convspecs). (idiom
   `rf-dart-package-sut-import-to-csharp-using`)
4. Three top-level `const _name = '…';` (one with `$_bondsV2Dir`
   interpolation) → `file static class TestPaths { internal const
   string BondsV2Dir = "../programs/bonds_v2"; internal const string
   MadBootDir = BondsV2Dir + "/mad_boot"; internal const string
   RootSelfGlpPath = "../programs/self.glp"; }`. Use compile-time `+`
   concatenation (NOT `$"…"` — interpolated strings are not const). Pre-
   C#-10 fallback: `private const string` static members on the test
   class. (idiom `rf-dart-private-toplevel-const-to-csharp-file-static-const`)
5. `Future<void> _runPlay(IsolateManager m, String b, {int timeoutSec = 10})
   async { … }` → `private static async Task _RunPlayAsync(IsolateManager
   manager, string bootFilename, int timeoutSec = 10) { … }`. The Dart
   named-optional `{int timeoutSec = 10}` becomes a C# positional default
   (call sites may use either positional or `timeoutSec:` named-arg form).
   Statement-by-statement body translation:
   (a) `final bootFile = File('$_madBootDir/$bootFilename');` →
       `string bootFile = TestPaths.MadBootDir + "/" + bootFilename;` (a
       PATH STRING, not a `FileInfo`, to match static-method file API).
   (b) `if (!bootFile.existsSync()) { print('Skipping: ${bootFile.path}
       not found'); return; }` → `if (!File.Exists(bootFile)) {
       Console.WriteLine($"Skipping: {bootFile} not found"); return; }`.
   (c) `final bootSource = bootFile.readAsStringSync();` →
       `var bootSource = File.ReadAllText(bootFile);`.
   (d) `final loader = BootLoader(); final config = loader.load(bootSource);`
       → `var loader = new BootLoader(); var config = loader.Load(bootSource);`
       (per `boot_loader.dart` convspec).
   (e) `config.projectDir = _bondsV2Dir; config.rootSelfGlpPath = _rootSelfGlp;`
       → `config.ProjectDir = TestPaths.BondsV2Dir; config.RootSelfGlpPath
       = TestPaths.RootSelfGlpPath;` (mutable get-set auto-properties on
       a reference-class `BootConfig`).
   (f) `await manager.boot(config, traceConfig: TraceConfig(glp: false,
       mad: false));` → `await manager.BootAsync(config, traceConfig: new
       TraceConfig { Glp = false, Mad = false });` (object-initialiser on
       init-only properties; named C# argument preserves readability).
       Codegen MAY substitute `TraceConfig.Off` for the all-false case
       (observationally identical singleton). Method-name `BootAsync` vs
       `Boot` follows the SUT convspec; either is acceptable.
   (g) `manager.start();` → `manager.Start();` (sync `void`).
   (h) `await Future.delayed(Duration(seconds: timeoutSec));` → `await
       Task.Delay(TimeSpan.FromSeconds(timeoutSec));`. MUST NOT be
       `Thread.Sleep` (sync-blocks pool thread) or cancellation-token
       based (introduces cancellation absent in Dart source). (idiom
       `rf-dart-future-delayed-to-csharp-task-delay`)
   Return type is `Task` (not `async void` — `async void` swallows
   exceptions; reserved for event handlers).
6. `Duration(seconds: N)` → `TimeSpan.FromSeconds(N)` at all three sites
   (helper delay, two test timeouts). (idiom
   `rf-dart-duration-seconds-to-csharp-timespan-fromseconds`)
7. `void main()` → eliminated entirely; xUnit discovers tests by
   reflection. (idiom `rf-dart-package-test-main-omit-in-xunit`)
8. `group('Bonds V2 Multi-Isolate', () { … })` → `public class
   BondsV2IsolateTests : IAsyncLifetime { … }` with `[Trait("Group",
   "Bonds V2 Multi-Isolate")]`. `IAsyncLifetime` REQUIRED because
   `tearDown` is async. (idiom `rf-dart-package-test-group-to-xunit-class`)
9. `late IsolateManager manager;` → `private IsolateManager _manager =
   null!;` instance field. (idiom `rf-dart-late-field-to-csharp-null-bang`)
10. `setUp(() { manager = IsolateManager(); })` → `public
    BondsV2IsolateTests() { _manager = new IsolateManager(); }` (the
    constructor; xUnit instantiates the class per-test, matching
    `package:test` per-test fresh state). NO `[SetUp]` attribute (that
    is NUnit). (idiom `rf-dart-setup-to-xunit-constructor`)
11. `tearDown(() async { await manager.shutdown(); })` → `public async
    Task DisposeAsync() { await _manager.ShutdownAsync(); }` on the test
    class, plus `public Task InitializeAsync() => Task.CompletedTask;` to
    satisfy the `IAsyncLifetime` contract. NOT `IDisposable.Dispose`
    (sync-Dispose cannot await; `.GetAwaiter().GetResult()` is a
    documented deadlock-risk anti-pattern). (idiom
    `rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync`)
12. Single `test('fplay1 runs across isolates (1 agent)', () async { … },
    timeout: Timeout(Duration(seconds: 30)));` → `[Fact(DisplayName =
    "fplay1 runs across isolates (1 agent)", Timeout = 30000)] public
    async Task Fplay1RunsAcrossIsolates1Agent() { await
    _RunPlayAsync(_manager, "mad_fplay1.glp"); }`. (idiom
    `rf-dart-test-callback-to-xunit-method-body`)
13. `for (final n in [2,3,4,5,6,8,9]) { test('fplay$n …', …); }` →
    `[Theory(DisplayName = "fplay{0} runs across isolates (2 agents)",
    Timeout = 30000)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(8)] [InlineData(9)]
    public async Task FplayNRunsAcrossIsolates2Agents(int n) { await
    _RunPlayAsync(_manager, $"mad_fplay{n}.glp"); }`. (idiom
    `rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata`)
14. Single `test('fplay4b runs across isolates (2 agents, time)', …)` →
    `[Fact(DisplayName = "fplay4b runs across isolates (2 agents, time)",
    Timeout = 30000)] public async Task Fplay4bRunsAcrossIsolates2AgentsTime()
    { await _RunPlayAsync(_manager, "mad_fplay4b.glp"); }`.
15. `for (final n in [10, 11]) { test('fplay$n …', …); }` →
    `[Theory(DisplayName = "fplay{0} runs across isolates (2 agents,
    time)", Timeout = 30000)] [InlineData(10)] [InlineData(11)] public
    async Task FplayNRunsAcrossIsolates2AgentsTime(int n) { await
    _RunPlayAsync(_manager, $"mad_fplay{n}.glp"); }`. Two separate
    `[Theory]` methods (not one merged) preserve the distinct labels.
16. Single `test('fplay12 runs across isolates (village, 6 agents)', ()
    async { await _runPlay(manager, 'mad_fplay12.glp', timeoutSec: 15); },
    timeout: Timeout(Duration(seconds: 45)));` → `[Fact(DisplayName =
    "fplay12 runs across isolates (village, 6 agents)", Timeout = 45000)]
    public async Task Fplay12RunsAcrossIsolatesVillage6Agents() { await
    _RunPlayAsync(_manager, "mad_fplay12.glp", timeoutSec: 15); }`.
17. `timeout: Timeout(Duration(seconds: N))` on `test(...)` → `[Fact/Theory
    (Timeout = N*1000)]` (milliseconds). Codegen SHOULD add
    `[assembly: CollectionBehavior(DisableTestParallelization = true)]`
    or `[Collection("BondsV2NonParallel")]` to make Timeout enforcement
    reliable under xUnit (well-known parallelisation footgun). (idiom
    `rf-dart-package-test-timeout-to-xunit-fact-timeout-attribute`)
18. `'mad_fplay$n.glp'` (string interpolation over int `n`) → C#
    `$"mad_fplay{n}.glp"`. Same Dart→C# 1:1 mapping for `$var` ↔ `{var}`.
    (idiom `rf-dart-string-interpolation-to-csharp-interpolated-string`)
19. Cross-cutting SUT call-site decisions (consumed verbatim from
    per-SUT convspecs; no re-derivation): `IsolateManager()` → `new
    IsolateManager()`; `manager.boot(...)` → `manager.BootAsync(...)`
    (Task); `manager.start()` → `manager.Start()` (void);
    `manager.shutdown()` → `manager.ShutdownAsync()` (Task);
    `BootLoader()` → `new BootLoader()`; `loader.load(s)` →
    `loader.Load(s)` (sync, returns `BootConfig` reference-class);
    `config.projectDir = …` / `config.rootSelfGlpPath = …` → PascalCase
    setter assignments on get-set auto-properties; `TraceConfig(glp:
    false, mad: false)` → `new TraceConfig { Glp = false, Mad = false }`
    (object-initialiser; OR `TraceConfig.Off` singleton). (idiom
    `rf-dart-sut-call-site-translation-via-per-sut-convspec`)
20. `print('Skipping: ${bootFile.path} not found')` →
    `Console.WriteLine($"Skipping: {bootFile} not found");`.
    Alternative `ITestOutputHelper.WriteLine` is recorded (preferred for
    xUnit-isolated capture; not load-bearing here as the print is a
    one-time skip diagnostic). (idiom
    `rf-dart-print-and-terminate-to-csharp-equivalent`)

Inherited threading-model escalation: every threading-dependent
construct (`IsolateManager` lifecycle, `TraceConfig` shape) defers to
`lib/multiagent/isolate_manager.dart.md` per FR-013 "don't double-
escalate"; that escalation has since been closed in commit `12a468f5`
(2026-05-21) with Channel<T>-actor-mailbox; this test consumes the
ruling verbatim.

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (Xunit, System, System.IO,
  System.Threading.Tasks, SUT namespace `<RootNs>.Multiagent`) — done.
- T2: Emit namespace declaration mirroring `test/multiagent` (e.g.
  `<RootNs>.Test.Multiagent`) — done.
- T3: Emit `file static class TestPaths` with three `internal const
  string` members using `+` concatenation for compile-time const — done.
- T4: Emit `public class BondsV2IsolateTests : IAsyncLifetime` with
  `[Trait("Group", "Bonds V2 Multi-Isolate")]` and `private
  IsolateManager _manager = null!;` field — done.
- T5: Emit lifecycle members — constructor (assigns `_manager = new
  IsolateManager()`), `InitializeAsync()` returning
  `Task.CompletedTask`, `DisposeAsync()` awaiting
  `_manager.ShutdownAsync()` — done.
- T6: Emit `private static async Task _RunPlayAsync(IsolateManager
  manager, string bootFilename, int timeoutSec = 10)` helper with the
  full body translated statement-for-statement (File.Exists skip-
  diagnostic via Console.WriteLine; BootLoader + Load; BootConfig
  property setters; await BootAsync with `new TraceConfig { Glp=false,
  Mad=false }`; Start; await Task.Delay(TimeSpan.FromSeconds(...))) —
  done.
- T7: Emit single `[Fact]` for fplay1 (Timeout=30000) — done.
- T8: Emit `[Theory]` over `[InlineData(2..9)]` (skipping 7) for fplay2-9
  (2 agents), Timeout=30000 — done.
- T9: Emit single `[Fact]` for fplay4b (Timeout=30000) — done.
- T10: Emit `[Theory]` over `[InlineData(10)]/[InlineData(11)]` for
  fplay10-11 (2 agents, time), Timeout=30000 — done.
- T11: Emit single `[Fact]` for fplay12 (village, 6 agents),
  Timeout=45000, passing `timeoutSec: 15` to the helper — done.
- T12: Emit `[assembly: CollectionBehavior(DisableTestParallelization =
  true)]` (or `[Collection("BondsV2NonParallel")]` on the class) to
  make per-test Timeout enforcement reliable — done.

## 4. Research Findings

none required — all twenty target decisions are KB cache hits recorded
in the convspec (`rf-dart-dart-io-file-to-dotnet-system-io-file`,
`rf-dart-package-test-import-to-xunit-using`,
`rf-dart-package-sut-import-to-csharp-using`,
`rf-dart-private-toplevel-const-to-csharp-file-static-const`,
`rf-dart-named-required-partly-defaulted-to-csharp-positional-with-defaults`,
`rf-dart-future-delayed-to-csharp-task-delay`,
`rf-dart-duration-seconds-to-csharp-timespan-fromseconds`,
`rf-dart-package-test-main-omit-in-xunit`,
`rf-dart-package-test-group-to-xunit-class`,
`rf-dart-late-field-to-csharp-null-bang`,
`rf-dart-setup-to-xunit-constructor`,
`rf-dart-async-teardown-to-xunit-iasynclifetime-disposeasync`,
`rf-dart-test-callback-to-xunit-method-body`,
`rf-dart-package-test-for-loop-test-to-xunit-theory-inlinedata`,
`rf-dart-package-test-timeout-to-xunit-fact-timeout-attribute`,
`rf-dart-string-interpolation-to-csharp-interpolated-string`,
`rf-dart-sut-call-site-translation-via-per-sut-convspec`,
`rf-dart-print-and-terminate-to-csharp-equivalent`,
`rf-dart-camelcase-to-csharp-pascalcase`). The threading-model decision
itself is inherited from `lib/multiagent/isolate_manager.dart.md`
(closed in commit `12a468f5`, Channel<T> actor mailbox).

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/multiagent/bonds_v2_isolate_test.dart.md`
(ratified convspec, `source_sha256: 18e788ee20ad20f262700ad47895d6a6cdefae27818f7c356f7c719a38512e5c`
matches the source). Every target decision in §2 mirrors a `constructs[]`
entry verbatim; every conversion-unit `cu-1`…`cu-12` is realised by
exactly one task in §3 (T1=cu-1+cu-2, T3=cu-3, T4=cu-4, T5=cu-5,
T6=cu-6, T7=cu-7, T8=cu-8, T9=cu-9, T10=cu-10, T11=cu-11, T12=cu-12).
SUT-side decisions (IsolateManager / BootLoader / BootConfig /
TraceConfig shapes and method names) are consumed from
`lib/multiagent/{isolate_manager,boot_loader}.dart.md` per FR-024 /
SC-007 (no re-derivation). Threading-model inherited from
`isolate_manager.dart.md` (closed escalation, Channel<T> mailbox).

## 6. Escalations

None.
