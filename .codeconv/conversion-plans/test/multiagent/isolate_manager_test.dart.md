---
path: test/multiagent/isolate_manager_test.dart
cycle_group_id: 146
scc_siblings: []
generated_at: 2026-05-21T16:56:07Z
source_sha256: 431a81bed721f5801c63d58ef60dc4af936f654617fd1a7f4aaab28dcfd30da0
schema_version: 1
---

# Conversion Plan: test/multiagent/isolate_manager_test.dart

## 1. Source Analysis

The 117-line Dart file `glp_runtime_net/test/multiagent/isolate_manager_test.dart` is an integration test that directly exercises the `IsolateManager` SUT (the central threading-model decision point). Verbatim structure:

- **Lines 1-4**: `import 'dart:io';`, `import 'package:test/test.dart';`, two SUT imports: `boot_loader.dart`, `isolate_manager.dart`.
- **Line 7**: top-level `const _socialGraphDir = '../programs/typed_book/social_graph';` (private, repo-relative path to GLP fixtures).
- **Lines 10-17**: top-level helper `String? _readGlpFile(String filename)` — builds a `File('$_socialGraphDir/$filename')`, returns null with a `print('Skipping: ...')` if the file does not exist, otherwise returns `file.readAsStringSync()`. Sync API throughout.
- **Lines 19-117**: `void main() { group('IsolateManager', () { ... }); }` — single outer group.
- **Line 21**: `late IsolateManager manager;` — late-initialised field.
- **Lines 23-25**: `setUp(() { manager = IsolateManager(); });` — synchronous setUp.
- **Lines 27-29**: `tearDown(() async { await manager.shutdown(); });` — **async tearDown** (forces `IAsyncLifetime` on the .NET side).
- **Lines 31-55**: test 1 `'boots three agents from boot config'`, timeout 10 s. Uses a triple-quoted GLP source literal containing `procedure boot.` / `boot :- agent_init(alice,_)@alice, agent_init(bob,_)@bob, agent_init(charlie,_)@charlie.` plus `procedure agent_init(_?,_?).` and `agent_init(_,_) :- true.`. Builds `BootLoader().load(source)`, sets `config.rootSelfGlpPath = File('../programs/self.glp').absolute.path`, awaits `manager.boot(config)`, calls `manager.start()`, awaits `Future.delayed(Duration(milliseconds: 200))`. NO `expect` assertions — passes if no throw.
- **Lines 57-84**: test 2 `'runs full play with actor scripts (no UI)'`, timeout 30 s. Reads three fixture files (`play_madglp_boot.glp`, `typed_social_agent.glp`, `typed_actors.glp`); early-return on any null. Sets `rootSelfGlpPath`, then `sharedSources = [agentSource, actorSource]`. Three `expect` assertions on `config.directives.length`, `directives.map((d) => d.agentId).toList()`, and `directives.every((d) => d.goalFunctor == 'agent_init')`. Awaits `manager.boot(config, traceConfig: TraceConfig(glp: true, mad: true))`, calls `manager.start()`, awaits `Future.delayed(Duration(seconds: 5))`.
- **Lines 86-115**: test 3 `'runs full play with UI mediator and UI actors'`, timeout 30 s. Same shape as test 2 but four fixtures (`play_ui_madglp_boot.glp`, `typed_social_agent.glp`, `typed_ui_mediator.glp`, `typed_ui_actors.glp`); same three assertions; same trace config; same 5 s settle.

The file is a CONSUMER of `IsolateManager`. Per recent escalation closure (main `12a468f5`, 2026-05-21), `IsolateManager` uses `Channel<T>` per-agent mailboxes consumed by a single `Task.Run` per agent (escalation #5 / heap_fcp #4 single-owning-context). Every previously-deferred call site (Boot/Start/Shutdown/TraceConfig) is now resolved. This plan adopts the ratified shapes.

## 2. Dart → C#/.NET Conversion Plan

Target file: `test/multiagent/IsolateManagerTest.cs` (per convspec `target_code_unit`). Mirrors convspec construct-by-construct.

### Imports / file-scope using directives (cu-1)

- Drop `import 'dart:io';` — `dart:io` aggregates I/O; only the file subset is exercised. Add `using System.IO;` (File + Path).
- Drop `import 'package:test/test.dart';` — add `using Xunit;`.
- Drop both `package:glp_runtime/multiagent/...` imports — coalesce to a single `using <RootNs>.Multiagent;` (SUT namespace; root decided per project file).
- Add `using System;` (Console, Action, Exception), `using System.Threading.Tasks;` (Task, Task.Delay, Task.CompletedTask), `using System.Linq;` (Select/All/ToList), `using System.Collections.Generic;` (List<string>).

### Namespace declaration (cu-2)

- File-scoped namespace mirroring path: `namespace <RootNs>.Test.Multiagent;`.

### Top-level private const (cu-4)

- `const _socialGraphDir = '../programs/typed_book/social_graph';` → `private const string SocialGraphDir = "../programs/typed_book/social_graph";` (field on the test class — C# does not allow file-scoped top-level `const`). Dart underscore-prefix-private ⇒ C# `private`. CamelCase ⇒ PascalCase. Compile-time interned string in both languages.

### Skip-helper method (cu-5)

- `String? _readGlpFile(String filename)` → `private static string? ReadGlpFile(string filename)` on the test class. Body:
  - `var path = Path.Combine(SocialGraphDir, filename);` (Dart string-interpolation `'$_socialGraphDir/$filename'` → `Path.Combine`).
  - `if (!File.Exists(path)) { Console.WriteLine($"Skipping: {filename} not found at {path}"); return null; }`
  - `return File.ReadAllText(path);`
- Sync .NET counterparts: `File.Exists` / `File.ReadAllText` (NOT `File.ReadAllTextAsync` — would change helper signature). `print(...)` → `Console.WriteLine(...)`. NRT enabled project-wide.

### Drop main; group → test class (cu-3)

- xUnit discovers `[Fact]` by reflection — `void main() { group('IsolateManager', ...) }` drops the `main` and `group` wrapper.
- Class: `public class IsolateManagerTests : IAsyncLifetime` (xUnit). Async tearDown forces `IAsyncLifetime` over `IDisposable` (would otherwise force `GetAwaiter().GetResult()`).
- `[Trait("Group", "IsolateManager")]` on the class preserves the original group label.

### Late field (cu-6)

- `late IsolateManager manager;` → `private IsolateManager _manager = null!;` — documented null-forgiving pattern, assigned in `InitializeAsync` before any test body reads it.

### setUp (cu-7)

- `setUp(() { manager = IsolateManager(); });` → `public Task InitializeAsync() { _manager = new IsolateManager(); return Task.CompletedTask; }`. Sync body, Task return; `Task.CompletedTask` is documented (Microsoft Learn). Per the ratified SUT, `IsolateManager()` constructor is parameterless and stores empty mailbox dictionary.

### tearDown — RESOLVED via escalation #5 ruling (cu-8)

- `tearDown(() async { await manager.shutdown(); });` → `public async Task DisposeAsync() { await _manager.Shutdown(); }`. xUnit `IAsyncLifetime.DisposeAsync` is awaited before next test instance. Per the ratified Channel<T> mailbox ruling: `Shutdown()` returns `Task` and (a) calls `writer.Complete()` on each agent's `Channel<IsolateMessage>` (closes the mailbox, triggers per-agent `await foreach` exit), (b) awaits each consumer `Task` to natural completion (no `Isolate.kill` — preserves the Dart no-Isolate.kill contract verbatim), (c) clears the per-agent channel map. NOT `Dispose` (non-async).

### test 1 — minimal boot (cu-9)

- `test('boots three agents from boot config', () async { ... }, timeout: Timeout(Duration(seconds: 10)));` →
  ```
  [Fact(DisplayName = "boots three agents from boot config", Timeout = 10_000)]
  [Trait("Group", "IsolateManager")]
  public async Task BootsThreeAgentsFromBootConfig() { ... }
  ```
- Triple-quoted GLP source literal → C# 11 raw string literal `"""..."""` with closing `"""` at column 0 to preserve indentation byte-identically (cu-12).
- `final loader = BootLoader();` → `var loader = new BootLoader();`.
- `final config = loader.load(source);` → `var config = loader.Load(source);` (per `BootLoader` SUT convspec).
- `config.rootSelfGlpPath = File('../programs/self.glp').absolute.path;` → `config.RootSelfGlpPath = Path.GetFullPath("../programs/self.glp");` (Microsoft Learn `Path.GetFullPath` resolves against CWD identically to Dart `File(p).absolute.path`).
- `await manager.boot(config);` → `await _manager.Boot(config);` (per ratified ruling: `Boot` returns `Task`, installs per-agent Channel<IsolateMessage> + consumer `Task.Run` BEFORE awaiting Ready TaskCompletionSources — listener-install-before-spawn ordering preserved).
- `manager.start();` → `_manager.Start();` (per ratified ruling: synchronous `void`, iterates `_agentChannels` and `writer.TryWrite(IsolateMessage.Start)` to each).
- `await Future.delayed(Duration(milliseconds: 200));` → `await Task.Delay(TimeSpan.FromMilliseconds(200));`.
- No assertions — passes if no throw (xUnit identical semantics).

### test 2 — full play, actor scripts (cu-10)

- `[Fact(DisplayName = "runs full play with actor scripts (no UI)", Timeout = 30_000)] [Trait("Group", "IsolateManager")] public async Task RunsFullPlayWithActorScriptsNoUi()`.
- Three `_readGlpFile(...)` → `ReadGlpFile(...)` calls.
- `if (source == null || agentSource == null || actorSource == null) return;` → verbatim (early-return-skip-as-pass; do NOT convert to `Skip.If` — would change reporter status).
- `loader.Load(source)`, set `RootSelfGlpPath` via `Path.GetFullPath`.
- `config.sharedSources = [agentSource, actorSource];` → `config.SharedSources = new List<string> { agentSource, actorSource };` (per `BootConfig.SharedSources` SUT convspec — mutable `IList<string>` setter).
- `expect(config.directives.length, equals(3));` → `Assert.Equal(3, config.Directives.Count);` (arg-order flip; `.Count` on `IReadOnlyList<T>`).
- `expect(config.directives.map((d) => d.agentId).toList(), equals(['alice', 'bob', 'charlie']));` → `Assert.Equal(new[] { "alice", "bob", "charlie" }, config.Directives.Select(d => d.AgentId).ToList());`.
- `expect(config.directives.every((d) => d.goalFunctor == 'agent_init'), isTrue);` → `Assert.True(config.Directives.All(d => d.GoalFunctor == "agent_init"));`.
- `await manager.boot(config, traceConfig: TraceConfig(glp: true, mad: true));` → `await _manager.Boot(config, traceConfig: new TraceConfig { Glp = true, Mad = true });` (object-initialiser syntax per SUT TraceConfig init-only-properties shape).
- `manager.start();` → `_manager.Start();`.
- `await Future.delayed(Duration(seconds: 5));` → `await Task.Delay(TimeSpan.FromSeconds(5));`.

### test 3 — full play, UI mediator (cu-11)

- Same shape as test 2 with four `ReadGlpFile` calls (source/agentSource/mediatorSource/uiActorSource); same three assertions; same TraceConfig construction; same 5 s `Task.Delay`.

### Single-quoted string literals throughout (no separate construct)

- Dart `'X'` strings → C# `"X"` strings. C# single-quote is a `char` literal.

## 3. Decomposed Task Units

- T1: Emit file-scope `using` directives (Xunit, System, System.IO, System.Threading.Tasks, System.Linq, System.Collections.Generic, <RootNs>.Multiagent). done
- T2: Emit file-scoped `namespace <RootNs>.Test.Multiagent;`. done
- T3: Emit `public class IsolateManagerTests : IAsyncLifetime` with `[Trait("Group", "IsolateManager")]`. done
- T4: Emit `private const string SocialGraphDir` field. done
- T5: Emit `private static string? ReadGlpFile(string filename)` helper using `Path.Combine` / `File.Exists` / `File.ReadAllText` / `Console.WriteLine`. done
- T6: Emit `private IsolateManager _manager = null!;` field. done
- T7: Emit `public Task InitializeAsync()` returning `Task.CompletedTask`. done
- T8: Emit `public async Task DisposeAsync()` awaiting `_manager.Shutdown()`. done
- T9: Emit `[Fact(DisplayName=..., Timeout=10_000)] BootsThreeAgentsFromBootConfig` with raw-string GLP source, `Path.GetFullPath` for rootSelf, `Boot`/`Start`, 200 ms `Task.Delay`. done
- T10: Emit C# 11 raw-string literal for GLP minimal source at column 0 (byte-identical to Dart triple-quoted). done
- T11: Emit `[Fact(DisplayName=..., Timeout=30_000)] RunsFullPlayWithActorScriptsNoUi` with three ReadGlpFile + early-return + assertions + Boot with TraceConfig + Start + 5 s Task.Delay. done
- T12: Emit `[Fact(DisplayName=..., Timeout=30_000)] RunsFullPlayWithUiMediatorAndUiActors` with four ReadGlpFile + early-return + same assertions + same trace config Boot + Start + 5 s Task.Delay. done
- T13: Emit `new TraceConfig { Glp = true, Mad = true }` object-initialiser at both call sites. done
- T14: Emit `new List<string> { agentSource, actorSource }` (and 3-elem variant in T12) for SharedSources assignment. done
- T15: Emit LINQ `.Select(...).ToList()` and `.All(...)` projections at assertion sites with arg-order flip. done

## 4. Research Findings

none required — every construct resolves via the ratified convspec + cached idioms (xUnit pinning, IAsyncLifetime async-teardown, Path.GetFullPath ↔ File.absolute.path, Task.Delay ↔ Future.delayed, Channel<T>-backed IsolateManager Boot/Start/Shutdown shapes from main `12a468f5`, TraceConfig object-initialiser from SUT convspec). The previously-deferred SUT shapes are now ratified by escalation #5 / heap_fcp #4 closure; no new web research needed.

## 5. Consistency Pass

fixed — derived from `.codeconv/conversion-specs/test/multiagent/isolate_manager_test.dart.md` (RATIFIED mirror), `.codeconv/conversion-specs/lib/multiagent/isolate_manager.dart.md` (ratified Channel<T> mailbox ruling per main `12a468f5` escalation #5 + heap_fcp #4), `.codeconv/conversion-specs/lib/multiagent/boot_loader.dart.md` (BootLoader/BootConfig shapes), sibling test plans (`mad_cold_call_isolate_test.dart.md`, `boot_loader_test.dart.md`) for shared idioms (xUnit pinning, IAsyncLifetime, raw strings, expect-arg-order-flip, LINQ mappings, Future.delayed → Task.Delay), and CLAUDE.md/codeconv FR-013 ("don't double-escalate"). The convspec's DEFERRED markers are now closed by the ratified ruling; codegen emits the resolved shapes directly. No conflicts among consulted sources.

## 6. Escalations

None.
