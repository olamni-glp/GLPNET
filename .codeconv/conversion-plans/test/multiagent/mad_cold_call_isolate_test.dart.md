---
path: test/multiagent/mad_cold_call_isolate_test.dart
cycle_group_id: 148
scc_siblings: []
generated_at: 2026-05-21T16:25:18Z
source_sha256: 5678e45465ebcd43b220dc95a597cf05ff9a5a52ec87990e303802147ddc7555
schema_version: 1
---

# Conversion Plan: test/multiagent/mad_cold_call_isolate_test.dart

## 1. Source Analysis

`glp_runtime_net/test/multiagent/mad_cold_call_isolate_test.dart` (321 lines, sha256 `5678e454…7555`) is a madGLP cold-call integration test exercising the push-based protocol across actual Dart isolates. Two agents (Alice, Bob) each run in a separate Dart isolate with separate `GlpRuntime` instances; inter-isolate communication uses `SendPort`/`ReceivePort` to simulate network transport. Per the file's header comment, the scenario follows madGLP-spec.md §10.2: Alice globalizes a response-variable writer to Bob; Bob localizes `_w(alice,1)`, gets a writer, spawns `global_send`, binds the writer to `"pong"`; `global_send` fires, Alice receives the assignment, her `Resp?` reader resolves to `"pong"`.

Inspection of the source confirms the following load-bearing constructs:

1. **Imports** (lines 22-30): `dart:async`, `dart:isolate`, `package:test/test.dart`, plus six SUT package imports under `package:glp_runtime/...`.
2. **Sealed message hierarchy** (lines 33-81): `sealed class IsolateMessage {}` plus five concrete subclasses — `NetworkMsg` (carries assignment + serialised `GlobalName` triple), `Ready` (agentId + SendPort), `GlobalNamesMsg` (list of anonymous record `({String agent, int index, bool isWriter})`), `Start` (empty signal), `Done` (agentId, success, nullable resultValue, nullable error).
3. **Top-level isolate entrypoints** (lines 84-242): `aliceIsolate(SendPort mainPort) async` and `bobIsolate(SendPort mainPort) async`. Each opens a `ReceivePort`, constructs `GlpRuntime` + `MadContext`, installs an `onMessageReady` closure, sends `Ready` to main, and enters `await for (final msg in receivePort)` to drain inbound messages.
4. **Main test body** (lines 244-321): `void main() { group('madGLP Cold-Call with Isolates', () { test('...', () async { ... }); }); }` — opens `mainReceivePort`, creates `Completer<void>`, subscribes to the port via `.listen` with `is`-chain dispatch over the message hierarchy, spawns the two isolates via `await Isolate.spawn(...)`, awaits `completer.future.timeout(Duration(seconds: 10), onTimeout: () { fail(...); })`, then verifies via three `expect` calls.
5. **SUT call sites**: `globalize(...)` (lines 117-122), `localize(...)` (lines 208-213), `ctx.handleMadAssignment(...)` (lines 146-150), `ctx.registerGlobalSendSpawns(...)` (line 216), `ctx.onWriterBound(...)` (line 236), `ctx.flushMessages()` (line 237), `GlobalName.writer(...) / .reader(...)` (lines 141-142, 204-205), `TermVar.writer(...)` (line 119), `runtime.heap.allocateVariable()` (lines 109, 212), `runtime.heap.derefAddr(...)` (line 153), `runtime.heap.bindVariable(...)` (line 233), `ConstTerm('pong')` (lines 148, 233, 236), `MadContext(agentId: ..., runtime: ...)` (lines 89, 171).
6. **Record destructuring** (line 109): `final (w, r) = runtime.heap.allocateVariable();` — Dart 3 positional record destructuring.
7. **Anonymous record type** (line 67): `List<({String agent, int index, bool isWriter})>` field on `GlobalNamesMsg`.
8. **Diagnostic prints**: 25+ `print('[ALICE] ...')` / `print('[BOB] ...')` / `print('[MAIN] ...')` statements scattered across both entrypoints and the message router.
9. **Test assertions** (lines 314-316): `expect(bobResult?.success, isTrue, reason: ...)`, `expect(aliceResult?.success, isTrue, reason: ...)`, `expect(aliceResult?.resultValue, equals('pong'), reason: ...)`.
10. **Test teardown** (line 318): `mainReceivePort.close();` — explicit port close to release the listener.

The convspec ratifies these constructs as 17 entries with no open escalations (`escalations: []`); all decisions either reuse cached idioms (FR-012 KB hits) or defer to the `lib/runtime/heap_fcp.dart.md` isolate-manager port escalation (which, per CLAUDE.md/MEMORY 018 status, was already closed on 2026-05-21 with the `Channel<T>` actor-mailbox decision — `Isolate.spawn → Channel + Task.Run`, `SendPort.send → writer.TryWrite`, `ReceivePort → ChannelReader`, `await for → await foreach`, `port.close → writer.Complete`, `Completer<void> → TaskCompletionSource`).

## 2. Dart → C#/.NET Conversion Plan

Mirror of the convspec's 17 ratified construct decisions. Each Dart construct → its C#/.NET target as decided in the convspec:

1. **`import 'package:test/test.dart';`** → `using Xunit;` at file scope (xUnit pinned project-wide per `mad_error_handling_test.dart.md`; FR-012 KB hit). Codegen also adds `using System;`, `using System.Threading;`, `using System.Threading.Tasks;`, `using System.Threading.Channels;`, `using System.Collections.Generic;`. Target namespace mirrors `test/multiagent` → `<RootNs>.Test.Multiagent`.

2. **`import 'dart:async';`** → drop the line; replace load-bearing symbols at first use via canonical .NET equivalents (`Completer<T>` → `TaskCompletionSource<T>`; `Future<T>` → `Task<T>`). No file-scope `using` for the Dart import itself.

3. **`import 'dart:isolate';`** → drop the line. All load-bearing symbols (`Isolate`, `SendPort`, `ReceivePort`) inherit from the closed `lib/runtime/heap_fcp.dart.md` isolate-manager port decision (Option C `Channel<T>` actor mailbox, per the 2026-05-21 close): each agent is a single `Task.Run` consuming a per-agent `Channel.CreateUnbounded<IsolateMessage>` via `await foreach (var msg in reader.ReadAllAsync())`.

4. **Six SUT package imports** → six `using <RootNs>.Runtime;` / `using <RootNs>.Runtime.Terms;` / `using <RootNs>.Multiagent;` directives. Exact namespaces decided at each SUT file's conversion; this plan records only the shape.

5. **`sealed class IsolateMessage {}` + five leaves** → `public abstract class IsolateMessage { protected IsolateMessage() { } }` base + five `public sealed class <Name> : IsolateMessage` leaves with get-only auto-properties + positional constructors. NOT `record` (identity equality preserved — two `Done('alice', true)` from different agents must not value-compare equal). `NetworkMsg` positional+named ctor → positional ctor `(from, to, payload, type, globalNameAgent, globalNameIndex, globalNameIsWriter)`. `Done`'s nullable named pair → positional `string? resultValue = null, string? error = null` defaults. `GlobalNamesMsg.names` → `IReadOnlyList<(string Agent, int Index, bool IsWriter)>` (anonymous ValueTuple per cached idiom `rf-dart-record-type-to-csharp-valuetuple`).

6. **`final SendPort sendPort;` inside `Ready`** → `public ChannelWriter<IsolateMessage> SendPort { get; }` (per the closed heap_fcp Option C: SendPort = a `ChannelWriter<T>` — one-way write capability matches Dart SendPort's send-only asymmetry).

7. **Two top-level `void <name>(SendPort) async` entrypoints** → two `internal static async Task AliceIsolateAsync(ChannelWriter<IsolateMessage> mainPort) { ... }` and `internal static async Task BobIsolateAsync(ChannelWriter<IsolateMessage> mainPort) { ... }` on a file-scope `internal static class IsolateEntrypoints` container. `void`-async → `async Task` (NOT `async void`). `await for (final msg in receivePort)` → `await foreach (var msg in agentChannel.Reader.ReadAllAsync())`. Per-isolate `GlpRuntime`/`MadContext` construction maps verbatim to per-task construction; closures over `mainPort` / `globalNameToSend` / `importedWriterAddr` preserved (the latter two captured-then-reassigned locals become compiler-generated closure-class fields — same by-reference capture semantics as Dart).

8. **`await Isolate.spawn(aliceIsolate, mainReceivePort.sendPort);`** (×2) → per Option C close: `var aliceChannel = Channel.CreateUnbounded<IsolateMessage>(); _ = Task.Run(() => IsolateEntrypoints.AliceIsolateAsync(mainChannel.Writer));` then likewise for Bob. The Dart `await` on `Isolate.spawn` (setup-await, not completion-await) maps to a setup-await on a `StartAgentAsync` helper if a helper is introduced; otherwise the bare `Task.Run` registration is synchronous. Lifecycle: agent task completes when `await foreach` exits (closed-channel semantics via `Writer.Complete()`).

9. **`mainReceivePort.listen((msg) { if (msg is Ready) ... else if (msg is GlobalNamesMsg) ... else if (msg is NetworkMsg) ... else if (msg is Done) ... });`** → `var mainChannel = Channel.CreateUnbounded<IsolateMessage>(); var routerTask = Task.Run(async () => { await foreach (var msg in mainChannel.Reader.ReadAllAsync(cts.Token)) { switch (msg) { case Ready r: ...; case GlobalNamesMsg g: ...; case NetworkMsg n: ...; case Done d: ...; case Start: break; /* never received here */ } } });`. `is`-chain → C# pattern-match `switch` (cached idiom `rf-dart-runtime-type-check-is-pattern-dispatch-on-sealed`). Captured nullable locals (`alicePort`, `bobPort`, `aliceResult`, `bobResult`) become `ChannelWriter<IsolateMessage>? alicePort = null;` / `ChannelWriter<IsolateMessage>? bobPort = null;` / `Done? aliceResult = null;` / `Done? bobResult = null;` captured by the router lambda. The five-arm switch includes a `Start` arm (never received by main — a defensive default-throw arm satisfies C# exhaustiveness warnings).

10. **`final completer = Completer<void>();`** → `var completer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);`. `await completer.future` → `await completer.Task`. `completer.isCompleted` → `completer.Task.IsCompleted` (baseline faithful shape) OR drop the guard and use `completer.TrySetResult(true)` (idiomatic .NET; returns `false` on already-completed). `completer.complete()` → `completer.TrySetResult(true)` (dummy `bool` payload since TCS<T> requires a non-void `T`). `RunContinuationsAsynchronously` is MANDATORY to avoid synchronous-continuation deadlock on the completing thread.

11. **`await completer.future.timeout(Duration(seconds: 10), onTimeout: () { fail('Test timed out...'); });`** → `var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); try { await completer.Task.WaitAsync(cts.Token); } catch (OperationCanceledException) { Assert.Fail("Test timed out waiting for agents to complete"); }`. `Task.WaitAsync(CancellationToken)` is the .NET 6+ canonical replacement for `Task.WhenAny(t, Task.Delay(d))`. `fail(...)` → `Assert.Fail(...)` (xUnit 2.5+).

12. **`void main() { group('...', () { test('...', () async { ... }); }); }`** → eliminate `main` entirely (xUnit discovers `[Fact]` methods via reflection; cached idiom `rf-dart-package-test-main-omit-in-xunit`). The single inner `group` call becomes the enclosing test class.

13. **`group('madGLP Cold-Call with Isolates', () { ... })`** → `public class MadColdCallIsolateTests` with `[Trait("Group", "madGLP Cold-Call with Isolates")]` on the class. Cached idiom `rf-dart-package-test-group-to-xunit-class`.

14. **`test('Alice sends Resp? to Bob, Bob binds to pong, Alice receives pong', () async { ... })`** → `[Fact(DisplayName = "Alice sends Resp? to Bob, Bob binds to pong, Alice receives pong")] public async Task AliceSendsRespToBobBobBindsToPongAliceReceivesPong()`. Dart `async`-test → C# `async Task`-test (xUnit awaits returned Task). Body translates statement-for-statement; mailbox cleanup goes into a `try/finally` that calls `Writer.Complete()` on all three mailboxes and `cts.Dispose()`.

15. **`expect(bobResult?.success, isTrue, reason: '...')`** (×2) → `Assert.True(bobResult?.Success, "Bob should complete successfully");` + `Assert.True(aliceResult?.Success, "Alice should complete successfully");`. Same argument order (actual-first, message-second), same strict-true semantics (null and false both fail). Cached idiom `rf-dart-expect-isTrue-with-reason-to-xunit-assert-true`.

16. **`expect(aliceResult?.resultValue, equals('pong'), reason: '...')`** → baseline `Assert.Equal("pong", aliceResult?.ResultValue);` (argument-order flipped — expected-first per cached idiom `rf-dart-expect-equals-to-xunit-assert-equal-argorder`). `reason:` text preserved as XML doc-comment on the assertion line (xUnit `Assert.Equal<T>` has no userMessage overload). Alternative "preserve reason" shape recorded in convspec.

17. **`print('[ALICE] ...')` / `print('[BOB] ...')` / `print('[MAIN] ...')`** → baseline `System.Console.WriteLine(...)` (cached idiom `rf-dart-print-and-terminate-to-csharp-equivalent`). `Console.WriteLine` is thread-safe; matches Dart `print`'s observable cross-isolate stdout interleaving. ITestOutputHelper recorded as the alternative for projects preferring xUnit-isolated capture.

18. **`final (w, r) = runtime.heap.allocateVariable();`** → `var (w, r) = Runtime.Heap.AllocateVariable();` (cached idiom `rf-dart-record-type-to-csharp-valuetuple`). The SUT's `HeapFCP.AllocateVariable` returns `(int Writer, int Reader)` per `lib/runtime/heap_fcp.dart.md`.

19. **All SUT API call sites** translated via per-SUT-file convspec decisions (FR-012 KB hits; no re-research): `globalize(...)` → `Globalize(variables: ..., localAgent: ..., remoteAgent: ..., table: ...)` (static method on `mad_helpers.dart` C# port, named-argument call site preserved); `localize(...)` → `Localize(globalNames: ..., localAgent: ..., table: ..., freshAddrAllocator: () => Runtime.Heap.AllocateVariable())`; `ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent: ...)` → `Ctx.HandleMadAssignment(globalName, value, fromAgent)`; `ctx.registerGlobalSendSpawns(...)` → `Ctx.RegisterGlobalSendSpawns(...)`; `ctx.onWriterBound(...)` → `Ctx.OnWriterBound(...)`; `ctx.onMessageReady = (dest, msg) => { ... }` → delegate-field assignment via cached idiom `rf-dart-typedef-function-to-csharp-delegate`; `ctx.flushMessages()` → `Ctx.FlushMessages()`; `GlobalName.writer(...)` / `.reader(...)` → `GlobalName.Writer(...)` / `GlobalName.Reader(...)` (static factory methods per `lib/multiagent/variable_table.dart.md`, cached idiom `rf-dart-named-constructor-to-csharp-static-factory`); `TermVar.writer(w, readerAddr: r)` → `TermVar.Writer(w, readerAddr: r)` (static factory per `lib/runtime/terms.dart.md`); `runtime.heap.bindVariable(...)` → `Runtime.Heap.BindVariable(...)`; `runtime.heap.derefAddr(...)` → `Runtime.Heap.DerefAddr(...)`; `ConstTerm('pong')` → `new ConstTerm("pong")` per `lib/runtime/terms.dart.md`. All names PascalCased; all named-argument call sites preserved. All cited SUT methods are SYNCHRONOUS per their per-SUT-file convspecs (no `async Task` introduced on `OnWriterBound`, `FlushMessages`, `HandleMadAssignment`).

20. **`mainReceivePort.close();`** → folded into the `try/finally` teardown: `mainChannel.Writer.Complete();` (closes the channel, signals the router task to exit `await foreach`, allows agent tasks to drain and terminate).

## 3. Decomposed Task Units

- T1: Emit file header + 6 `using` directives (Xunit, System, System.Collections.Generic, System.Threading, System.Threading.Tasks, System.Threading.Channels) + SUT namespace usings. (done — per convspec construct #1+#4)
- T2: Emit `namespace <RootNs>.Test.Multiagent { ... }` block. (done — per convspec)
- T3: Emit `public abstract class IsolateMessage` base + five `public sealed class` leaves (`NetworkMsg`, `Ready`, `GlobalNamesMsg`, `Start`, `Done`) with get-only auto-properties + positional constructors. (done — per convspec construct #5)
- T4: Emit `[Trait("Group", "madGLP Cold-Call with Isolates")] public class MadColdCallIsolateTests` enclosing class. (done — per convspec construct #13)
- T5: Emit `internal static class IsolateEntrypoints` with two `internal static async Task AliceIsolateAsync(ChannelWriter<IsolateMessage> mainPort)` and `BobIsolateAsync(...)` methods translating the two Dart entrypoints statement-for-statement (per-task `GlpRuntime`+`MadContext` construction; `onMessageReady` delegate assignment; `Ready` send; `await foreach` loop with `switch` over `IsolateMessage`). (done — per convspec constructs #7+#19)
- T6: Emit the `[Fact(DisplayName = "...")] public async Task AliceSendsRespToBob...` test method body: create `mainChannel` + `aliceChannel` + `bobChannel` (all `Channel.CreateUnbounded<IsolateMessage>()`); create `TaskCompletionSource<bool>(RunContinuationsAsynchronously)` and `CancellationTokenSource(TimeSpan.FromSeconds(10))`; start router `Task.Run` reading `mainChannel.Reader.ReadAllAsync(cts.Token)` with `switch` over `Ready`/`GlobalNamesMsg`/`NetworkMsg`/`Done`/`Start`; start two agent `Task.Run`s with the entrypoint methods + writer arguments; `await completer.Task.WaitAsync(cts.Token)` wrapped in `try { ... } catch (OperationCanceledException) { Assert.Fail("Test timed out waiting for agents to complete"); }`; three asserts (`Assert.True` × 2, `Assert.Equal` × 1); mailbox cleanup (`Writer.Complete()` × 3) + `cts.Dispose()` in `try/finally`. (done — per convspec constructs #9+#10+#11+#14+#15+#16+#20)
- T7: Translate every Dart `print(...)` to `System.Console.WriteLine(...)` verbatim across all three contexts ([ALICE]/[BOB]/[MAIN]). (done — per convspec construct #17)
- T8: Translate the record destructuring `final (w, r) = runtime.heap.allocateVariable()` to `var (w, r) = Runtime.Heap.AllocateVariable()`. (done — per convspec construct #18)
- T9: Translate every SUT call site (Globalize, Localize, Ctx.HandleMadAssignment, Ctx.RegisterGlobalSendSpawns, Ctx.OnWriterBound, Ctx.OnMessageReady assignment, Ctx.FlushMessages, GlobalName.Writer/.Reader, TermVar.Writer, Runtime.Heap.BindVariable/.DerefAddr/.AllocateVariable, new ConstTerm) per per-SUT-file convspec decisions; PascalCase rename at every site; named-argument call sites preserved. (done — per convspec construct #19)
- T10: Reconstruct `GlobalName` on the receiving side from the three serialised fields (`globalNameAgent`, `globalNameIndex`, `globalNameIsWriter`) preserving the Dart test's de/reconstruct flow per the capability-token serialisation nuance. (done — per convspec construct #6 nuance)

## 4. Research Findings

None required. All decisions are KB-cached (FR-012/SC-007 hits) or escalation-inherited from `lib/runtime/heap_fcp.dart.md`, which itself closed on 2026-05-21 per the project memory (`project_018_codeconv_builder_status.md` — all 6 escalations cleared; isolate_manager `Channel<T>` mailbox decision committed at `12a468f5`). The convspec's `escalations: []` is intentional, not a placeholder. Microsoft Learn citations for `TaskCompletionSource<T>`, `Task.WaitAsync`, `Channel<T>`, pattern-match switch, `RunContinuationsAsynchronously`, and Console thread-safety are recorded in the convspec's "Rationale + research provenance" section.

## 5. Consistency Pass

Fixed — derived from `.codeconv/conversion-specs/test/multiagent/mad_cold_call_isolate_test.dart.md` (17 ratified constructs, `escalations: []`), `lib/runtime/heap_fcp.dart.md` (closed escalation #4 isolate_manager `Channel<T>` actor-mailbox decision, commit `12a468f5`), and the cached idioms enumerated in §4 (KB hits — no re-research).

## 6. Escalations

None.
