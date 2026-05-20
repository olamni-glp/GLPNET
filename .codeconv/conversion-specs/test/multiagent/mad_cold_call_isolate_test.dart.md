> Conversion-spec artifact for test/multiagent/mad_cold_call_isolate_test.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: test/multiagent/mad_cold_call_isolate_test.dart
source_sha256: 5678e45465ebcd43b220dc95a597cf05ff9a5a52ec87990e303802147ddc7555
target_code_unit: test/multiagent/MadColdCallIsolateTest.cs
constructs:
  - construct_key: dart.package_test.import_directive
    source_form: "import 'package:test/test.dart';"
    target_decision: >-
      Map to `using Xunit;` at file scope. xUnit pinned project-wide by the
      precedent file test/multiagent/mad_error_handling_test.dart.md
      (FR-012/SC-007 KB hit — REUSE, no re-research, no re-derivation).
      Codegen MUST also add `using System;` (Action delegate, Exception),
      `using System.Threading;` (CancellationTokenSource, ManualResetEventSlim),
      `using System.Threading.Tasks;` (Task, TaskCompletionSource for the
      Completer-equivalent — see dart.async.completer_t_with_future), and
      `using System.Collections.Generic;` (List<T>). Target namespace mirrors
      the Dart `test/multiagent` directory (e.g. `<RootNs>.Test.Multiagent`).
    idiom_id: rf-dart-package-test-import-to-xunit-using
    research_finding_id: rf-dart-package-test-import-to-xunit-using
    nuance: >-
      Test-framework selection is project-wide policy (mad_error_handling_test
      pinning); not a file-local choice. Every package:test file maps to the
      SAME .NET framework. xUnit's constructor-per-test isolation matches
      package:test fresh-state semantics; [Fact]/[Theory] map 1:1 onto
      Dart test()/parameterised test(); xUnit is the modern .NET default.
      NUnit and MSTest recorded as corroborating alternatives in the
      research finding only.
  - construct_key: dart.import.dart_async_core_library
    source_form: "import 'dart:async';"
    target_decision: >-
      Drop the `dart:async` line entirely; replace its load-bearing symbols
      at first use with the canonical .NET equivalents — `Completer<T>` ⇒
      `TaskCompletionSource<T>` (System.Threading.Tasks), `Future<T>` ⇒
      `Task<T>` (System.Threading.Tasks), `StreamSubscription` ⇒ N/A (this
      file only uses `ReceivePort.listen` from dart:isolate, not dart:async
      streams). No `using` directive is emitted for the Dart import itself;
      the targets surface through the BCL `using System.Threading.Tasks;`
      already added by the package_test_import construct above.
    idiom_id: null
    research_finding_id: rf-dart-dart-async-to-dotnet-threading-tasks
    nuance: >-
      Standard-library import nuance (explicitly addressed): Dart's
      `dart:async` is a CORE library exposing `Future`/`Completer`/`Stream`/
      `Timer`/`StreamController`. The .NET counterpart is
      `System.Threading.Tasks` (Task/TaskCompletionSource), NOT a single
      file-scoped `using`. Async-cancellation nuance: Dart Future has no
      built-in cancellation; .NET Task uses CancellationToken — the timeout
      mapping is recorded under dart.future.timeout_with_callback below.
      Stream-vs-IAsyncEnumerable nuance: NOT exercised in this file
      (`ReceivePort.listen` is the only stream-like surface and is handled
      under dart.isolate.receiveport_listen_message_router).
  - construct_key: dart.import.dart_isolate_core_library
    source_form: "import 'dart:isolate';"
    target_decision: >-
      Drop the `dart:isolate` line. ALL load-bearing symbols (`Isolate`,
      `SendPort`, `ReceivePort`) are escalation-inherited — the .NET hosting
      decision for the multiagent runtime's isolate-equivalent is owned by
      lib/runtime/heap_fcp.dart.md (the isolate_manager port escalation).
      This test file is a CONSUMER of that decision: the C# port emits
      whichever isolate-equivalent shape the heap_fcp escalation eventually
      decides (Channel<T>+dedicated Thread / pinned TaskScheduler / actor
      mailbox / System.Threading.Thread+BlockingCollection). The construct
      below (dart.isolate.spawn_with_sendport_argument) records the
      target-shape OPTIONS that the heap_fcp resolution selects from; it
      does NOT introduce a new escalation here (FR-013 "don't double-
      escalate" — same discipline body_kernels.dart.md uses for the
      MadContext.send threading decision).
    idiom_id: null
    research_finding_id: rf-dart-dart-isolate-to-dotnet-isolate-manager-port
    nuance: >-
      Threading-model decision INHERITED — NOT re-escalated. Dart isolates
      have message-passing-only isolation and a single-threaded event loop
      per isolate (https://dart.dev/language/concurrency). The .NET re-host
      has multiple viable shapes (per the heap_fcp escalation): (i) one
      dedicated `Thread` per agent + a `BlockingCollection<object>` mailbox
      (closest to Dart's single-threaded contract); (ii) a per-agent
      `TaskScheduler` (e.g. ConcurrentExclusiveSchedulerPair.ExclusiveScheduler)
      with `Channel<T>` mailboxes; (iii) an actor library (e.g. Akka.NET,
      Orleans, Proto.Actor — third-party, recorded but NOT pinned). The
      .NET CLR's `AppDomain` is DEPRECATED in .NET Core+ and is NOT a
      faithful counterpart to Dart isolates. This file's spec records the
      shape (port count, message types, message router) at the abstract
      level; the concrete primitive is selected at the heap_fcp.dart.md
      escalation resolution. NO ADDITIONAL ESCALATION here.
  - construct_key: dart.package_under_test.import_directive
    source_form: >-
      "import 'package:glp_runtime/runtime/runtime.dart'; import
      'package:glp_runtime/runtime/terms.dart'; import 'package:glp_runtime/
      multiagent/mad_context.dart'; import 'package:glp_runtime/multiagent/
      message_queue.dart'; import 'package:glp_runtime/multiagent/
      mad_helpers.dart'; import 'package:glp_runtime/multiagent/
      global_send.dart';"
    target_decision: >-
      Map each to a `using` directive that names the C# namespace produced
      by converting the SUT file (e.g. `using <RootNs>.Runtime;`,
      `using <RootNs>.Runtime.Terms;`, `using <RootNs>.Multiagent;`). The
      exact SUT namespace strings are decided when each SUT file is
      converted; this spec records only the shape of the cross-file
      dependency, not the SUT namespace strings.
    idiom_id: rf-dart-package-sut-import-to-csharp-using
    research_finding_id: rf-dart-package-sut-import-to-csharp-using
    nuance: >-
      Cross-file dependency nuance: in Dart `package:glp_runtime/...` is an
      explicit pubspec-anchored URI; in C# there is no per-file URI — only
      assembly + namespace. The conversion must (a) ensure the converted
      SUT lives in a deterministic namespace derived from its relative
      path, and (b) ensure the test assembly references the SUT assembly
      via the project file (project-system idiom, out of scope here). No
      `as` alias / `show` / partial import is used in this file.
  - construct_key: dart.sealed_class.message_envelope_hierarchy_five_arms
    source_form: >-
      "sealed class IsolateMessage {}" + 5 concrete subclasses extending it:
      `class NetworkMsg extends IsolateMessage { final String from; final
      String to; final List<int> payload; final MessageType type; final
      String globalNameAgent; final int globalNameIndex; final bool
      globalNameIsWriter; NetworkMsg(this.from, this.to, this.payload,
      this.type, {required this.globalNameAgent, required
      this.globalNameIndex, required this.globalNameIsWriter}); }`,
      `class Ready extends IsolateMessage { final String agentId; final
      SendPort sendPort; Ready(this.agentId, this.sendPort); }`,
      `class GlobalNamesMsg extends IsolateMessage { final List<({String
      agent, int index, bool isWriter})> names; GlobalNamesMsg(this.names); }`,
      `class Start extends IsolateMessage {}`,
      `class Done extends IsolateMessage { final String agentId; final
      bool success; final String? resultValue; final String? error;
      Done(this.agentId, this.success, {this.resultValue, this.error}); }`.
    target_decision: >-
      Emit a closed C# discriminated-union shape as `public abstract class
      IsolateMessage { protected IsolateMessage() { } }` + five `public
      sealed class <Name> : IsolateMessage` leaves. Each leaf becomes a
      reference type with get-only auto-properties (one per Dart `final`
      field) and a positional constructor that assigns them. The sealed
      hierarchy is the FAITHFUL counterpart to Dart's `sealed class` —
      both close the type's subtype set to the file's named subclasses,
      enabling exhaustive switch over the union (the message-router
      switch in the test body, see dart.isolate.receiveport_listen_
      message_router). NOT a `record` hierarchy — the Dart subclasses use
      identity equality (no `==`/`hashCode` override) and carry mutable-
      friendly reference semantics; `record` would synthesise value
      equality which is wrong for this domain (two `Done('alice', true)`
      messages from different agents must NOT compare equal). The Dart
      `NetworkMsg` mixed positional+named ctor maps to a positional
      C# ctor over `(from, to, payload, type, globalNameAgent,
      globalNameIndex, globalNameIsWriter)` per the cached idiom
      rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-
      with-defaults (no defaults here — all named are `required`).
      `Done`'s nullable-named pair (`resultValue`, `error`) maps to two
      positional nullable-string ctor parameters with `null` defaults
      (`string? resultValue = null, string? error = null`).
    idiom_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    research_finding_id: rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves
    nuance: >-
      Sealed-hierarchy nuance (explicitly addressed): Dart's `sealed class`
      (Dart 3.0+, https://dart.dev/language/class-modifiers#sealed) closes
      the direct-subtype set to the SAME library (file); the analyzer can
      then prove a switch over the union exhaustive. C# has no `sealed`
      modifier on a base type that closes subclassing to the same file
      (C# `sealed class` PREVENTS inheritance entirely, the opposite
      meaning). The faithful counterpart is `abstract class` base +
      `sealed class` leaves: subclassing externally is still LANGUAGE-
      legal but discouraged-by-convention; the closed set is documented
      via XML doc and enforced at review time. The C# 8 `switch
      expression` (https://learn.microsoft.com/dotnet/csharp/language-
      reference/operators/switch-expression) achieves exhaustive checking
      against the named leaves; the codegen MUST emit either the exhaustive
      `switch` (with a default arm throwing `InvalidOperationException` for
      the "should-be-impossible" external subclass) OR an `is`-chain that
      mirrors the Dart `if (msg is X) ... else if (msg is Y) ...` flow.
      Anonymous-record-typed-list nuance (LOAD-BEARING): the
      `GlobalNamesMsg.names` field has type `List<({String agent, int
      index, bool isWriter})>` — a Dart 3 record with three named fields.
      The C# counterpart is `IReadOnlyList<(string Agent, int Index, bool
      IsWriter)>` (anonymous ValueTuple) under the cached idiom
      rf-dart-record-type-to-csharp-valuetuple (FR-012 KB hit). The
      record/tuple here transmits across what would be an
      isolate-boundary in Dart — see the next construct for the message-
      serialization nuance.
  - construct_key: dart.isolate.sendport_field_of_message_envelope
    source_form: "final SendPort sendPort; (inside Ready)"
    target_decision: >-
      `SendPort` is a Dart-isolate primitive: a handle to another isolate's
      `ReceivePort` that the holder can `.send(msg)` to. The C# port shape
      depends on the isolate-manager port resolution (escalation-inherited
      from heap_fcp.dart.md) — under each option: (i) dedicated Thread +
      BlockingCollection ⇒ `BlockingCollection<object>` (the agent's
      mailbox; the SendPort equivalent is a reference to that collection
      with a write-only wrapper interface, e.g. `IAgentMailbox`); (ii)
      Channel<T>+TaskScheduler ⇒ `ChannelWriter<object>` from
      System.Threading.Channels; (iii) actor library ⇒ an actor PID/ref
      type. This spec records the FIELD shape as `public IAgentMailbox
      SendPort { get; }` (interface name placeholder pending the
      heap_fcp escalation resolution); the runtime implementation type is
      decided at the isolate-manager port.
    idiom_id: null
    research_finding_id: rf-dart-sendport-to-dotnet-mailbox-writer-interface
    nuance: >-
      Capability-token nuance (explicitly addressed): a Dart SendPort is a
      one-way write capability — the holder can `.send(...)` but cannot
      `.listen(...)`. The .NET counterparts (i)/(ii) above preserve that
      asymmetry via a WRITE-only interface (`Channel<T>` already exposes
      `ChannelWriter<T>` and `ChannelReader<T>` distinctly;
      BlockingCollection requires a wrapper). The "send via SendPort"
      message types in this file (`Ready`, `NetworkMsg`, `GlobalNamesMsg`,
      `Done`) MUST therefore be serializable across the isolate boundary
      in Dart — Dart copies most object graphs on .send (per
      https://api.dart.dev/stable/dart-isolate/SendPort/send.html);
      complex types like `Term`/`GlobalName` would FAIL the .send copy
      (Dart imposes the "must be a primitive value or another SendPort"
      rule for cross-isolate sends in pre-3.0 isolates; Dart 3 isolates
      copy more graphs but still reject some types — the test file
      WORKS-AROUND this by SERIALIZING `GlobalName` into three primitive
      fields (`globalNameAgent`, `globalNameIndex`, `globalNameIsWriter`)
      on `NetworkMsg`, and reconstructing the `GlobalName` on the
      receiving side). The C# port MUST preserve this serialization
      pattern: the channel/mailbox MAY accept full reference types, but
      the test FAITHFULNESS requires the same de/reconstruct flow so the
      conversion exercises the same boundary the Dart test exercises.
  - construct_key: dart.toplevel_function.async_void_isolate_entrypoint
    source_form: >-
      "void aliceIsolate(SendPort mainPort) async { ... }" + "void
      bobIsolate(SendPort mainPort) async { ... }" — two TOP-LEVEL
      function-typed Dart entrypoints, each with `async` modifier and
      `void` return; each takes a single `SendPort` positional parameter
      pointing back at the main isolate; each body opens a `ReceivePort`,
      builds runtime+ctx, sends a `Ready` to main, then `await for (final
      msg in receivePort) { ... }` drains the inbound mailbox.
    target_decision: >-
      Map to two top-level (i.e. static, on a `file`-scoped or `internal
      static class` container) C# methods: `internal static async Task
      AliceIsolateAsync(IAgentMailbox mainPort) { ... }` and
      `internal static async Task BobIsolateAsync(IAgentMailbox mainPort)
      { ... }`. Dart `void`-returning async maps to `async Task` (NOT
      `async void` — `async void` in C# is reserved for event handlers and
      swallows exceptions silently per https://learn.microsoft.com/
      dotnet/csharp/language-reference/operators/async). The Dart
      `await for` over a `ReceivePort` (which implements `Stream<dynamic>`)
      maps to `await foreach (var msg in mainPort.ReadAllAsync())` against
      a `ChannelReader<object>.ReadAllAsync()` OR against a custom
      `IAsyncEnumerable<object>` over the mailbox — concrete shape decided
      at the heap_fcp isolate-manager port. Per-isolate runtime/ctx
      construction maps verbatim to per-mailbox-task runtime/ctx
      construction; the construct is one C# method per Dart entrypoint.
    idiom_id: rf-dart-future-void-async-to-csharp-task-async
    research_finding_id: rf-dart-future-void-async-to-csharp-task-async
    nuance: >-
      Async-method-return-type nuance (explicitly addressed): Dart
      `void <name>(...) async` returns implicitly a `Future<void>` that
      the call site CAN await but the body has no `return` statement;
      C# `async void` swallows exceptions and cannot be awaited, so the
      faithful counterpart is `async Task` (returns awaitable, propagates
      exceptions). Stream-vs-IAsyncEnumerable nuance: Dart's `await for`
      over a `Stream<T>` maps to C# `await foreach` over
      `IAsyncEnumerable<T>` — same iteration semantics, same
      cancellation-cooperative nature. Closure-capture nuance: the
      callback `ctx.onMessageReady = (dest, msg) => { ... }` set inside
      the entrypoint captures `mainPort` and `globalNameToSend` (in
      Bob's case) — the C# port preserves the closure shape (lambda
      capture); for the `globalNameToSend` LOCAL VARIABLE captured by a
      lambda THEN reassigned after capture, the variable becomes a field
      on a compiler-generated closure class in BOTH languages — the
      callback reads the LATEST assignment at invocation time
      (https://learn.microsoft.com/dotnet/csharp/language-reference/
      operators/lambda-expressions#captured-variables), matching Dart's
      by-reference local capture. Track-then-fire pattern preserved.
  - construct_key: dart.isolate.spawn_with_sendport_argument
    source_form: >-
      "await Isolate.spawn(aliceIsolate, mainReceivePort.sendPort);" — two
      occurrences, one per agent. Each starts a new Dart isolate that
      executes the given top-level function with the given SendPort
      argument; `Isolate.spawn` returns `Future<Isolate>` (the test
      `await`s it but discards the `Isolate` handle — relies on isolate
      auto-termination when its event loop drains and there are no live
      ports).
    target_decision: >-
      Shape decided by the heap_fcp isolate-manager port (escalation-
      inherited — NOT re-escalated here). Under option (i) dedicated
      Thread + BlockingCollection: spawn becomes `var thread = new
      Thread(() => AliceIsolateAsync(mailbox).GetAwaiter().GetResult())
      { IsBackground = true }; thread.Start();` plus a paired write-only
      mailbox handle passed in. Under option (ii) Channel+TaskScheduler:
      spawn becomes `_ = Task.Factory.StartNew(() =>
      AliceIsolateAsync(mailboxWriter), CancellationToken.None,
      TaskCreationOptions.None, agentScheduler);`. Under option (iii)
      actor library: spawn becomes the library's actor-system spawn call.
      The test spec records the abstract shape (start a long-running
      execution context that runs the entrypoint with the mailbox
      argument); the exact primitive is the isolate-manager port's call.
      The Dart `await` on `Isolate.spawn` is a SETUP `await` (waits for
      spawn to register, NOT for the isolate to finish); the C# port
      preserves the setup-await shape — `await StartAgentAsync(...)`
      returning `Task` that completes once the agent is registered.
    idiom_id: null
    research_finding_id: rf-dart-isolate-spawn-to-dotnet-isolate-manager-port
    nuance: >-
      Lifecycle nuance (explicitly addressed): a Dart isolate
      auto-terminates when its event loop has no further work AND no
      live ports referencing it (https://api.dart.dev/stable/dart-isolate
      /Isolate-class.html). Test relies on this implicit termination
      (no `Isolate.kill` call). The C# port MUST give the test an
      equivalent lifecycle: under option (i) the dedicated Thread's
      method returns when `await foreach` exits (closed-channel
      semantics) and the Thread terminates; under option (ii) the Task
      completes when the inner method returns. The TEST harness MUST
      close/cancel the mailboxes after the test asserts so the agent
      tasks/threads can exit — this is recorded as a TEARDOWN
      responsibility on the test method (see
      dart.package_test.test_call_executable below; the codegen MUST
      emit a `try/finally` that closes the mailboxes after
      `Assert.Equal(...)`). NO ADDITIONAL ESCALATION — the lifecycle
      mapping is determined by whichever option the isolate-manager port
      resolves to.
  - construct_key: dart.isolate.receiveport_listen_message_router
    source_form: >-
      "final mainReceivePort = ReceivePort();" + "mainReceivePort.listen((msg)
      { if (msg is Ready) {...} else if (msg is GlobalNamesMsg) {...} else
      if (msg is NetworkMsg) {...} else if (msg is Done) {...} });" — the
      main isolate's message router, an event-style subscription on the
      receive port's stream (the Dart `ReceivePort` IS a `Stream<dynamic>`
      under the hood). The router branches on `is`-typed dispatch (the
      sealed-message-class hierarchy from the construct above), forwards
      `Ready` events to track per-agent ports, forwards `GlobalNamesMsg`
      to Bob, forwards `NetworkMsg` to the matching destination port,
      records `Done` results and completes a top-level `Completer<void>`.
    target_decision: >-
      Map to `var mainMailbox = new Channel<object>(/* unbounded */);`
      plus an async background task that reads
      `await foreach (var msg in mainMailbox.Reader.ReadAllAsync(cts.Token))
      { switch (msg) { case Ready r: ...; case GlobalNamesMsg g: ...;
      case NetworkMsg n: ...; case Done d: ...; } }`. The Dart `.listen`
      callback (event-style, fire-and-forget) maps to a C# `Task`-based
      router that the test awaits-on-completion via the
      `TaskCompletionSource<bool>` (see Completer mapping below). The
      `is`-chain is the cached idiom rf-dart-runtime-type-check-is-pattern-
      dispatch — preserved verbatim via C# pattern-matching `switch`
      (C# 8+, https://learn.microsoft.com/dotnet/csharp/fundamentals/
      tutorials/pattern-matching). Per-arm body translates field-for-field
      from the Dart router into the C# arm body; the `alicePort`/`bobPort`
      nullable locals become `IAgentMailbox? alicePort = null;` /
      `IAgentMailbox? bobPort = null;` captured by the router lambda
      (closure capture — see closure-capture nuance under the entrypoint
      construct).
    idiom_id: rf-dart-runtime-type-check-is-pattern-dispatch-on-sealed
    research_finding_id: rf-dart-runtime-type-check-is-pattern-dispatch-on-sealed
    nuance: >-
      Stream-vs-Channel nuance (explicitly addressed — well-known Dart→
      C# nuance, NOT glossed): Dart `Stream<T>.listen(callback)` is an
      event-style subscription that returns a `StreamSubscription` and
      runs the callback per emitted event in the event-loop turn AFTER
      emission; .NET `Channel<T>` reading via
      `ChannelReader<T>.ReadAllAsync(ct)` gives `IAsyncEnumerable<T>` —
      consumed via `await foreach`. Semantically equivalent for the
      single-consumer pattern this file uses (one router, no broadcast).
      Multi-listener nuance: Dart `Stream.asBroadcastStream()` would map
      to multi-reader Channel pattern (NOT used here). Backpressure
      nuance: Dart streams have built-in backpressure; .NET unbounded
      Channels do not — for the message volumes in this test (≤10
      messages), unbounded is fine; the conversion records the choice.
      Pattern-match-exhaustiveness nuance: Dart 3 lets the analyzer prove
      the `is`-chain exhaustive over a sealed union; C# 9+ pattern-match
      switch (https://learn.microsoft.com/dotnet/csharp/language-
      reference/operators/switch-expression#non-exhaustive-switch-
      expressions) issues a warning for non-exhaustive patterns over
      sealed hierarchies — codegen MUST include all five arms PLUS the
      `Start` arm even though `Start` is never routed by the main port
      (it is SENT from main to alice, never received) — keeps the type
      coverage explicit.
  - construct_key: dart.async.completer_t_with_future
    source_form: >-
      "final completer = Completer<void>();" + "await completer.future.timeout(
      Duration(seconds: 10), onTimeout: () { fail('Test timed out...'); });" +
      "if (aliceResult != null && bobResult != null && !completer.isCompleted)
      { completer.complete(); }"
    target_decision: >-
      Map to `var completer = new TaskCompletionSource<bool>(
      TaskCreationOptions.RunContinuationsAsynchronously);`. Dart `await
      completer.future` ⇒ `await completer.Task`. The
      `completer.isCompleted` guard ⇒ `completer.Task.IsCompleted` (read-
      only — the idiomatic atomic "set-once" via
      `completer.TrySetResult(true)` returns `false` on already-completed,
      so codegen MAY drop the guard and use `TrySetResult` instead — both
      shapes are recorded; the guarded form is the faithful Dart
      translation, the TrySet form is the idiomatic .NET form). The Dart
      `completer.complete()` (parameterless, since `T = void`) ⇒
      `completer.TrySetResult(true)` (the dummy `bool` payload exists
      only because `TaskCompletionSource<T>` requires a non-void `T`;
      callers ignore the value — the closest faithful alternative is
      `TaskCompletionSource<object?>` with `.TrySetResult(null)`).
    idiom_id: null
    research_finding_id: rf-dart-completer-to-csharp-taskcompletionsource
    nuance: >-
      Completer-vs-TaskCompletionSource nuance (LOAD-BEARING, explicitly
      addressed): Dart `Completer<T>` and .NET `TaskCompletionSource<T>`
      are 1:1 — both are one-shot async "promise" handles that one side
      `complete`/`SetResult`s and the other `await`s. The
      `RunContinuationsAsynchronously` option (https://learn.microsoft.com/
      dotnet/api/system.threading.tasks.taskcreationoptions#fields) is
      MANDATORY to avoid the well-known TCS pitfall where continuations
      run synchronously on the completing thread, holding mailbox locks
      and causing deadlocks (the equivalent of Dart's microtask-queue
      isolation that prevents this by design). NOT `SemaphoreSlim` (a
      counting semaphore — wrong primitive, would need explicit
      `Release`/`Wait` and lose the result-carrying capability). NOT
      `ManualResetEventSlim` (a kernel-event handle — has no `T` payload
      and blocks rather than awaits). Void-payload nuance: Dart
      `Completer<void>` has no payload; .NET TCS<T> requires a `T`;
      idiomatic shapes are `TCS<bool>` (set true on complete) OR
      `TCS<object?>` (set null) — codegen choice recorded; both compile
      and behave identically for the await semantics.
  - construct_key: dart.future.timeout_with_onTimeout_callback
    source_form: >-
      "await completer.future.timeout(Duration(seconds: 10), onTimeout: () {
      fail('Test timed out waiting for agents to complete'); });"
    target_decision: >-
      Map to a `CancellationTokenSource` with `cts.CancelAfter(
      TimeSpan.FromSeconds(10));` PLUS `await
      completer.Task.WaitAsync(cts.Token);` (the .NET 6+ overload
      https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.
      waitasync) WRAPPED in a `try { ... } catch
      (OperationCanceledException) { Assert.Fail("Test timed out waiting
      for agents to complete"); }`. The Dart `onTimeout:` callback that
      calls `fail(...)` ⇒ the catch-block calling `Assert.Fail(...)`
      (xUnit 2.5+ method, https://xunit.net/docs/comparisons#assertions).
      Alternative shapes recorded in the research finding: (a) `Task.
      WhenAny(completer.Task, Task.Delay(10_000))` followed by an
      identity check on the winner — older idiom, pre-WaitAsync; (b)
      bare `await completer.Task.WaitAsync(TimeSpan.FromSeconds(10));`
      (.NET 6+ has a direct-TimeSpan overload that throws
      `TimeoutException`) — simpler, but the catch-block must map
      `TimeoutException` to `Assert.Fail` instead of
      `OperationCanceledException`.
    idiom_id: null
    research_finding_id: rf-dart-future-timeout-to-csharp-task-waitasync-cancellation
    nuance: >-
      Timeout-mapping nuance (explicitly addressed — well-known Dart→C#
      footgun): Dart `Future.timeout(d, onTimeout: cb)` returns a NEW
      Future that completes with the original's value if the original
      completes in time, OR with `cb`'s return value if not — the
      original is NOT cancelled (Dart Futures cannot be cancelled). The
      .NET counterpart uses CancellationToken-based cancellation, which
      DOES propagate to the underlying operation if it cooperates with
      the token. For THIS test the underlying operation
      (`completer.Task`) has no cancellation cooperation (it completes
      when both agents finish), so the cancellation only fires the
      catch-block — equivalent observable behaviour. Diagnostic nuance:
      Dart `fail(msg)` throws `TestFailure`; xUnit `Assert.Fail(msg)`
      throws `Xunit.Sdk.FailException` — both runner-caught with
      identical reporting. Time-precision nuance: Dart `Duration(seconds:
      10)` and .NET `TimeSpan.FromSeconds(10)` both store a 100-ns-
      precision duration; semantically identical.
  - construct_key: dart.package_test.main_entrypoint
    source_form: "void main() { group('madGLP Cold-Call with Isolates', () { test(...); }); }"
    target_decision: >-
      xUnit discovers `[Fact]` methods by reflection; eliminate `main`
      entirely (cached idiom from mad_error_handling_test.dart.md). The
      single `group(...)` call inside `main` becomes the enclosing test
      class (see dart.package_test.group_block below). `void main()`
      itself is dropped.
    idiom_id: rf-dart-package-test-main-omit-in-xunit
    research_finding_id: rf-dart-package-test-main-omit-in-xunit
    nuance: >-
      Lifecycle nuance (cache hit, restated): Dart `main` is invoked once
      per test-file process; xUnit has no per-file hook. THIS file's
      `main` body is exactly one `group(...)` call, so the omission is
      lossless.
  - construct_key: dart.package_test.group_block
    source_form: "group('madGLP Cold-Call with Isolates', () { test(...); });"
    target_decision: >-
      Map to a `public class MadColdCallIsolateTests` (PascalCase, non-
      identifier characters stripped: `madGLP Cold-Call with Isolates` ⇒
      `MadGlpColdCallWithIsolates` ⇒ class name `MadColdCallIsolateTests`
      matching the file name; the original label preserved via
      `[Trait("Group", "madGLP Cold-Call with Isolates")]` on the class).
      Cached idiom from mad_error_handling_test.dart.md and
      boot_loader_test.dart.md.
    idiom_id: rf-dart-package-test-group-to-xunit-class
    research_finding_id: rf-dart-package-test-group-to-xunit-class
    nuance: >-
      Name-mangling nuance (cache hit): arbitrary string label ⇒ PascalCase
      identifier; original preserved via `[Trait]`. Single-group nuance:
      this file has only ONE group with ONE test — there is no nested-
      group topology to flatten (boot_loader_test's three-inner-groups
      shape is NOT exercised here).
  - construct_key: dart.package_test.test_call_executable
    source_form: >-
      "test('Alice sends Resp? to Bob, Bob binds to pong, Alice receives
      pong', () async { /* setup ports, spawn isolates, await completer
      with timeout, expect bobResult/aliceResult */ });"
    target_decision: >-
      Map to a `[Fact(DisplayName = "Alice sends Resp? to Bob, Bob binds
      to pong, Alice receives pong")] public async Task
      AliceSendsRespToBobBobBindsToPongAliceReceivesPong()` instance
      method on `MadColdCallIsolateTests`. The Dart `() async { ... }`
      test callback body translates statement-for-statement into the
      method body: mailbox creation, completer/TCS init, router task
      start, two agent-spawn calls, `await completer.Task.WaitAsync(...)`,
      `Assert.True/Equal` calls, mailbox close/cleanup in a `try/finally`.
      Dart `async`-test ⇒ C# `async Task`-test (xUnit awaits returned
      Task; cached idiom rf-dart-future-void-async-to-csharp-task-async).
    idiom_id: rf-dart-test-callback-to-xunit-method-body
    research_finding_id: rf-dart-test-callback-to-xunit-method-body
    nuance: >-
      Async-test nuance (explicitly addressed): xUnit awaits the returned
      `Task` per https://xunit.net/docs/async-tests; failures during the
      await surface as test failures with full stack traces. Mailbox-
      cleanup nuance (LOAD-BEARING): the agent threads/tasks MUST exit
      after the assertion runs, OR the test process leaks them. The
      Dart test relies on isolate auto-termination (no live ports);
      the C# port MUST explicitly close mailboxes in a `try/finally` —
      codegen MUST emit `try { /* test body */ } finally {
      aliceMailbox?.Writer.Complete(); bobMailbox?.Writer.Complete();
      mainMailbox.Writer.Complete(); cts.Dispose(); }`. This is the
      explicit lifecycle responsibility called out under
      dart.isolate.spawn_with_sendport_argument.
  - construct_key: dart.package_test.expect_isTrue_with_reason
    source_form: >-
      "expect(bobResult?.success, isTrue, reason: 'Bob should complete
      successfully');" + "expect(aliceResult?.success, isTrue, reason:
      'Alice should complete successfully');"
    target_decision: >-
      Map to `Assert.True(bobResult?.Success, "Bob should complete
      successfully");` + `Assert.True(aliceResult?.Success, "Alice should
      complete successfully");`. xUnit `Assert.True(bool? condition,
      string userMessage)` (https://xunit.net/docs/comparisons#assertions)
      maps Dart `expect(actual, isTrue, reason: msg)` exactly — same
      argument order (actual-first then message), same strict-true
      semantics, same failure-message surfacing. Cached idiom from
      fairness_26_test.dart.md (rf-dart-expect-isFalse-with-reason-to-
      xunit-assert-false — parallel form for the true variant).
    idiom_id: rf-dart-expect-isTrue-with-reason-to-xunit-assert-true
    research_finding_id: rf-dart-expect-isTrue-with-reason-to-xunit-assert-true
    nuance: >-
      Reason-parameter nuance (cache hit from fairness_26_test): Dart
      `reason:` is a named parameter; xUnit `userMessage` is positional.
      Conversion preserves the string verbatim; failure output is
      observationally identical. Strict-true nuance: Dart `isTrue` and
      xUnit `Assert.True(bool? condition, ...)` both reject non-strict-
      true values (null and false both fail). Null-safety nuance:
      `bobResult?.success` is `bool?` in Dart; the C# `bobResult?.Success`
      is `bool?` — the `Assert.True(bool?, string)` overload handles
      null-as-failure exactly like Dart's `isTrue` does. Argument-order
      nuance: same (actual-first-then-message) in both.
  - construct_key: dart.package_test.expect_equals_with_reason
    source_form: >-
      "expect(aliceResult?.resultValue, equals('pong'), reason: 'Alice
      should receive pong');"
    target_decision: >-
      Map to `Assert.Equal("pong", aliceResult?.ResultValue);` followed by
      NO separate userMessage (xUnit `Assert.Equal<T>(T expected, T actual)`
      does NOT have a userMessage overload — the diagnostic comes from the
      auto-generated expected-vs-actual message). Alternative: wrap in a
      conditional that throws `Xunit.Sdk.EqualException` with a custom
      message — recorded in the research finding as the "preserve reason"
      shape. The faithful baseline emission is the bare
      `Assert.Equal("pong", aliceResult?.ResultValue);` — argument-order
      FLIPPED per the project-wide cached idiom rf-dart-expect-equals-to-
      xunit-assert-equal-argorder. The `reason:` text is preserved as
      an XML doc-comment on the assertion line so the intent is not lost.
    idiom_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    research_finding_id: rf-dart-expect-equals-to-xunit-assert-equal-argorder
    nuance: >-
      Argument-order-flip nuance (cache hit, well-known footgun): Dart
      `expect(actual, equals(expected))` ⇒ xUnit `Assert.Equal(expected,
      actual)`. Reason-preservation nuance (explicitly addressed):
      `Assert.Equal<T>` has no userMessage overload; the diagnostic is
      auto-generated. If the reason text is load-bearing for the
      reviewer, codegen MAY emit `if (!Equals("pong",
      aliceResult?.ResultValue)) throw new Xunit.Sdk.EqualException(
      "pong", aliceResult?.ResultValue, "Alice should receive pong");`
      — recorded as the "preserve reason" alternative; the baseline is
      the bare `Assert.Equal`. Value-vs-reference nuance: `string` has
      value semantics in both languages; equality is observationally
      identical.
  - construct_key: dart.print_statement.diagnostic_log_to_stdout
    source_form: >-
      Multiple `print('[ALICE] ...')` / `print('[BOB] ...')` / `print(
      '[MAIN] ...')` statements scattered through both isolate
      entrypoints and the message router — diagnostic stdout traces.
    target_decision: >-
      Map each `print(...)` to `System.Console.WriteLine(...)` —
      simplest 1:1, matches the existing
      rf-dart-print-and-terminate-to-csharp-equivalent idiom recorded
      in lib/bytecode/runner.dart.md for the SUT side. xUnit
      `ITestOutputHelper` (the test-isolated diagnostic capture, cached
      idiom rf-dart-print-to-xunit-itestoutputhelper-writeline from
      test/test_channel_construction.dart.md) is the IDIOMATIC choice
      for test code, BUT requires constructor injection
      (`public MadColdCallIsolateTests(ITestOutputHelper output) { _output
      = output; }`) and per-test ITestOutputHelper that cannot be passed
      cross-thread into the spawned agent contexts without explicit
      handoff. For THIS file the agent-side prints occur on the agent's
      thread/task, NOT the test thread, so `ITestOutputHelper` would
      either require thread-safe wrapping or be lost. The simplest
      faithful translation is `Console.WriteLine` on both the test
      thread and the agent threads — same observable behaviour as the
      Dart `print` (which writes to the process stdout from any
      isolate). Recorded alternative: wrap the agent-side prints
      through an `Action<string>` log-sink field passed into the
      entrypoint, with the test class binding it to
      `ITestOutputHelper.WriteLine` — preserves test-isolated capture
      at the cost of one extra entrypoint parameter.
    idiom_id: rf-dart-print-and-terminate-to-csharp-equivalent
    research_finding_id: rf-dart-print-and-terminate-to-csharp-equivalent
    nuance: >-
      Diagnostic-output nuance (explicitly addressed): Dart `print(...)`
      goes to stdout of the OWNING isolate (which is the same process,
      so all isolates' prints interleave in the process stdout — the
      ordering across `[ALICE]`/`[BOB]`/`[MAIN]` lines is therefore
      NON-DETERMINISTIC under both Dart and .NET implementations).
      Test-runner-capture nuance: xUnit captures Console.WriteLine
      output per test (https://xunit.net/docs/capturing-output) ONLY
      from .NET Framework runners; xUnit v2+ on .NET Core/5+ does NOT
      capture Console output — ITestOutputHelper is the recommended
      capture mechanism. For THIS test the prints are debugging aids,
      not assertion-load-bearing; loss of capture under .NET Core is
      acceptable. Cross-thread-safety nuance: Console.WriteLine IS
      thread-safe (https://learn.microsoft.com/dotnet/api/system.console.
      writeline); ITestOutputHelper IS NOT (xUnit docs explicitly warn
      against cross-thread WriteLine — the agent-side print pattern
      would require synchronization).
  - construct_key: dart.runtime.allocate_pair_record_destructuring
    source_form: >-
      "final (w, r) = runtime.heap.allocateVariable();" + the Alice-side
      use of the returned (writerAddr, readerAddr) record from the SUT
      `HeapFCP.allocateVariable()` (see lib/runtime/heap_fcp.dart.md).
    target_decision: >-
      The Dart record-destructuring `final (w, r) = ...` maps to C# tuple
      deconstruction `var (w, r) = Runtime.Heap.AllocateVariable();` —
      cached idiom rf-dart-record-type-to-csharp-valuetuple from
      test/multiagent/localize_test.dart.md. The SUT-side decision on
      `HeapFCP.AllocateVariable` returning `(int Writer, int Reader)` (a
      named-element ValueTuple) is recorded under
      lib/runtime/heap_fcp.dart.md — this test consumes that decision.
    idiom_id: rf-dart-record-type-to-csharp-valuetuple
    research_finding_id: rf-dart-record-type-to-csharp-valuetuple
    nuance: >-
      Record-deconstruction nuance (cache hit): Dart 3 record patterns
      and C# ValueTuple deconstruction are 1:1 for positional patterns;
      named record fields map to named tuple elements (the C# side may
      use `.Writer`/`.Reader` accessors or positional `.Item1`/`.Item2`
      — the SUT decision pins the named form for clarity). Reuse from
      localize_test convspec verbatim.
  - construct_key: dart.sut.globalize_localize_handlemadassignment_termvar_globalname_const_term
    source_form: >-
      "globalize(variables: [TermVar.writer(w, readerAddr: r)],
      localAgent: 'alice', remoteAgent: 'bob', table: ctx.wp);" +
      "localize(globalNames: ..., localAgent: 'bob', table: ctx.wp,
      freshAddrAllocator: () => runtime.heap.allocateVariable());" +
      "ctx.handleMadAssignment(globalName: ..., value: ConstTerm('pong'),
      fromAgent: msg.from);" + "ctx.registerGlobalSendSpawns(...)" +
      "ctx.onWriterBound(...)" + "ctx.flushMessages();" + "GlobalName.writer
      (..., ...) / GlobalName.reader(..., ...)" + "TermVar.writer(...)" +
      "runtime.heap.bindVariable(...)" + "runtime.heap.derefAddr(...)" —
      the SUT API surface this test exercises.
    target_decision: >-
      Each SUT API call maps via the per-SUT-file convspec decisions
      (FR-012 KB cache hits — no re-research):
      - `globalize(...)` ⇒ static method on the `mad_helpers.dart` C#
        port (per lib/multiagent/mad_helpers.dart.md), with named
        arguments `variables`, `localAgent`, `remoteAgent`, `table` ⇒
        C# named-argument call site under cached idiom
        rf-dart-named-required-ctor-with-defaults-to-csharp-positional-
        ctor-with-defaults (named-call-style preserved).
      - `localize(...)` ⇒ same shape per mad_helpers.dart convspec.
      - `ctx.handleMadAssignment(globalName: ..., value: ..., fromAgent:
        ...)` ⇒ `Ctx.HandleMadAssignment(globalName, value, fromAgent)`
        per lib/multiagent/mad_context.dart.md (cached idiom rf-dart-
        named-required-to-csharp-positional-with-named-call-site).
      - `ctx.registerGlobalSendSpawns(...)` ⇒ `Ctx.RegisterGlobalSendSpawns
        (...)` per mad_context.dart convspec.
      - `ctx.onWriterBound(...)` / `ctx.onMessageReady = (dest, msg) =>
        {...}` ⇒ method call / delegate-field assignment per
        mad_context.dart convspec (cached idiom
        rf-dart-typedef-function-to-csharp-delegate).
      - `ctx.flushMessages()` ⇒ `Ctx.FlushMessages()` per mad_context.dart.
      - `GlobalName.writer(...)` / `.reader(...)` ⇒ static factory
        methods on the C# `GlobalName` type per
        lib/multiagent/variable_table.dart.md (cached idiom
        rf-dart-named-constructor-to-csharp-static-factory).
      - `TermVar.writer(w, readerAddr: r)` ⇒ static factory on `TermVar`
        per lib/runtime/terms.dart.md and lib/multiagent/mad_helpers.dart.md
        (cached idiom rf-dart-named-constructor-or-static-factory-to-
        csharp-static-method).
      - `runtime.heap.bindVariable(...)` / `.derefAddr(...)` ⇒
        `Runtime.Heap.BindVariable(...)` / `.DerefAddr(...)` per
        lib/runtime/heap_fcp.dart.md.
      - `ConstTerm('pong')` ⇒ `new ConstTerm("pong")` per
        lib/runtime/terms.dart.md.
      All names PascalCased at the call site per the project-wide
      Dart-camelCase ⇒ C#-PascalCase idiom (cached at every SUT
      convspec — REUSE verbatim).
    idiom_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    research_finding_id: rf-dart-sut-call-site-translation-via-per-sut-convspec
    nuance: >-
      Cross-file dependency aggregation nuance (explicitly addressed):
      every SUT-method call in a test file is a CONSUMER of the
      per-SUT-file convspec — no decision is re-derived here (FR-012/
      SC-007: KB-resolved, not re-researched). If any SUT convspec's
      decision changes (e.g. `flushMessages` is later renamed
      `DrainMailbox` in the C# port), this test's spec automatically
      tracks via the per-SUT-file convspec — codegen reads the
      target name from there. Async/Stream/Future/Completer nuance: the
      cited SUT methods are ALL SYNCHRONOUS per their per-SUT-file
      convspecs (mad_context.dart's `OnWriterBound`, `FlushMessages`,
      `HandleMadAssignment` MUST NOT be `async Task` — the per-SUT spec
      explicitly forbids introducing async on these methods because the
      heap-callback contract is synchronous; introducing async would
      silently turn writer-bind events into awaitable continuations
      and change observable concurrency semantics). This test's call
      sites preserve the synchronous shape.
conversion_units:
  - "cu-1: file-scope using directives (Xunit, System, System.Collections.Generic, System.Threading, System.Threading.Tasks, System.Threading.Channels, plus SUT namespaces produced from glp_runtime/{runtime,multiagent}/*.dart)"
  - "cu-2: namespace declaration mirroring test/multiagent path (e.g. <RootNs>.Test.Multiagent)"
  - "cu-3: sealed-by-convention message-envelope hierarchy — `public abstract class IsolateMessage` base + five `public sealed class` leaves (`NetworkMsg`, `Ready`, `GlobalNamesMsg`, `Start`, `Done`); each leaf carries get-only auto-properties from the Dart fields and a positional constructor; nullable `Done` fields have `null` defaults"
  - "cu-4: top-level class `MadColdCallIsolateTests` (from group label 'madGLP Cold-Call with Isolates'), `[Trait('Group', 'madGLP Cold-Call with Isolates')]`"
  - "cu-5: two static helper methods on a file-scope `internal static class IsolateEntrypoints` (or inline as private static methods on the test class) — `AliceIsolateAsync(IAgentMailbox mainPort)` and `BobIsolateAsync(IAgentMailbox mainPort)` — each `async Task`-returning, each opening an in-mailbox + running an `await foreach` message loop with `switch` over `IsolateMessage`"
  - "cu-6: one `[Fact(DisplayName = '<original label>')] public async Task` test method — body: create main mailbox + `TaskCompletionSource<bool>` (TCS with `RunContinuationsAsynchronously`) + `CancellationTokenSource` with 10s timer, start router task that reads main mailbox and routes Ready/GlobalNamesMsg/NetworkMsg/Done events while updating captured `alicePort`/`bobPort` mailboxes and the `aliceResult`/`bobResult` `Done?` locals, start two agent execution contexts via the heap_fcp isolate-manager port (concrete primitive deferred to that escalation), `await tcs.Task.WaitAsync(cts.Token)` wrapped in `try { ... } catch (OperationCanceledException) { Assert.Fail('Test timed out waiting for agents to complete'); }`, then `Assert.True(bobResult?.Success, 'Bob should complete successfully');` + `Assert.True(aliceResult?.Success, 'Alice should complete successfully');` + `Assert.Equal('pong', aliceResult?.ResultValue);`, with mailbox cleanup (`Writer.Complete()`) and `cts.Dispose()` in a `try/finally`"
  - "cu-7: diagnostic `Console.WriteLine` calls preserved verbatim from the Dart `print` statements (alternative: `Action<string>` log-sink parameter on each entrypoint binding to `ITestOutputHelper.WriteLine`)"
  - "cu-8: SUT API call sites translated via per-SUT-file convspec decisions (PascalCase rename at every call site, named-argument call sites preserved for ctors per cached idioms) — `Globalize(...)`, `Localize(...)`, `Ctx.HandleMadAssignment(...)`, `Ctx.RegisterGlobalSendSpawns(...)`, `Ctx.OnWriterBound(...)`, `Ctx.FlushMessages()`, `GlobalName.Writer(...)` / `.Reader(...)`, `TermVar.Writer(...)`, `Runtime.Heap.AllocateVariable()`, `Runtime.Heap.BindVariable(...)`, `Runtime.Heap.DerefAddr(...)`, `new ConstTerm('pong')`"
escalations: []
```

## Rationale + research provenance

### Why no escalations on isolate / threading

The .NET hosting model for the multiagent runtime's isolate-equivalent
is a TRUE undecidable point — but it is ALREADY OWNED by the
`lib/runtime/heap_fcp.dart.md` escalation
(`dart.heap_fcp.concurrency_model_thread_safety_for_multiagent_hosting`).
This test file is a CONSUMER of that decision, NOT a new owner. Per
FR-013's "don't double-escalate" discipline (the same discipline
`lib/runtime/body_kernels.dart.md` applies for the `MadContext.send`
threading semantics, and `lib/bytecode/runner.dart.md` applies for the
`System.Threading.Timer` callback-affinity), introducing a NEW
escalation here would (a) duplicate the heap_fcp decision point and
(b) block this file's conversion on a question that already has an
owner. The constructs above record the SHAPE of the target under each
of the three viable options the heap_fcp escalation enumerates
(dedicated Thread + BlockingCollection / Channel + per-agent
TaskScheduler / actor library); whichever option the heap_fcp
resolution picks is consumed verbatim here. `escalations: []` is
therefore intentional, not a placeholder.

### xUnit pinning (cache hit)

The same authoritative basis as `mad_error_handling_test.dart.md` and
`boot_loader_test.dart.md`: xUnit v3 docs
(`https://xunit.net/docs/getting-started/v3/getting-started`) for
`[Fact]` / `[Trait]` / constructor-per-test isolation, and the Dart
`package:test` README on `pub.dev`
(`https://pub.dev/packages/test`) for `group` / `test` / `expect` /
matcher semantics. No re-research.

### `dart:async` `Completer<T>` → `TaskCompletionSource<T>` (new finding)

Microsoft Learn — `System.Threading.Tasks.TaskCompletionSource<T>`
(`https://learn.microsoft.com/dotnet/api/system.threading.tasks.
taskcompletionsource-1`) — names this as the canonical "explicit
producer-controlled `Task`" primitive. The Dart `Completer<T>` docs on
api.dart.dev (`https://api.dart.dev/stable/dart-async/Completer-class.
html`) describe it as a "way to produce Future objects and to complete
those objects with either a value or an error" — a 1:1 mapping. The
`TaskCreationOptions.RunContinuationsAsynchronously` flag is the
documented mitigation for the well-known TCS synchronous-continuation
deadlock pattern (`https://devblogs.microsoft.com/premier-developer/
the-danger-of-taskcompletionsourcet-class/` — Microsoft Premier
Developer blog, corroborating; the authoritative source is the
TaskCreationOptions enum docs). The void-payload mapping
(`Completer<void>` ⇒ `TaskCompletionSource<bool>` with dummy `true`)
is the conventional .NET shape — recorded; an equivalent
`TaskCompletionSource<object?>` with `null` payload is also faithful.

### `Future.timeout` → `Task.WaitAsync(CancellationToken)` (new finding)

.NET 6+ added `Task.WaitAsync(CancellationToken)` and
`Task.WaitAsync(TimeSpan)` overloads
(`https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.
waitasync`) precisely to address the long-standing
`Task.WhenAny(t, Task.Delay(d))` idiom for timeout — the official
documentation explicitly calls this out as the recommended replacement.
The Dart `Future.timeout(d, onTimeout: cb)` docs
(`https://api.dart.dev/stable/dart-async/Future/timeout.html`) describe
the `onTimeout` callback's role; the C# port wraps the
`OperationCanceledException` thrown by `WaitAsync` to fire the
`Assert.Fail` callback. Older shapes (`Task.WhenAny`) are recorded as
corroborating alternatives for pre-.NET-6 targets.

### `dart:isolate` shape — defer to heap_fcp isolate-manager port (cache hit, no new research)

Dart concurrency reference (`https://dart.dev/language/concurrency`)
documents isolates as the share-nothing message-passing concurrency
model. The .NET counterpart is multiple options — `Channel<T>`
(`https://learn.microsoft.com/dotnet/core/extensions/channels`),
`BlockingCollection<T>`
(`https://learn.microsoft.com/dotnet/standard/collections/thread-safe/
blockingcollection-overview`), or third-party actor libraries
(Akka.NET, Orleans, Proto.Actor). The heap_fcp escalation owns the
choice; this file's spec records the abstract shape under each option
and consumes the eventual decision. No new isolate research is performed
here (FR-024 cache hit on the heap_fcp escalation's pending research).

### `sealed` Dart class hierarchy → abstract base + sealed leaves

Dart 3 `sealed` class modifier (`https://dart.dev/language/class-
modifiers#sealed`) closes the direct-subtype set to the same library;
C# has no equivalent file-local closure. The faithful counterpart is
`abstract class` base + `sealed class` leaves (cached idiom
`rf-dart-abstract-marker-base-to-csharp-abstract-sealed-leaves` from
`lib/compiler/analyzer.dart.md`), with exhaustiveness enforced by the
C# pattern-match switch (`https://learn.microsoft.com/dotnet/csharp/
fundamentals/tutorials/pattern-matching`) plus a default-arm-throws
discipline. Five-arm message envelope; no record synthesis (identity
equality preserved).

### SUT call-site translation via per-SUT-file convspec (FR-012 cache hits)

Every SUT API call (`globalize`, `localize`, `handleMadAssignment`,
`onMessageReady`, `onWriterBound`, `registerGlobalSendSpawns`,
`flushMessages`, `GlobalName.writer/.reader`, `TermVar.writer`,
`runtime.heap.bindVariable/.derefAddr/.allocateVariable`,
`ConstTerm(...)`) is decided by the corresponding per-SUT-file
convspec. This test spec records the SHAPE of the cross-file
dependency only — the names, types, and call shapes come from
`lib/multiagent/mad_context.dart.md`, `lib/multiagent/mad_helpers.dart.md`,
`lib/multiagent/variable_table.dart.md`, `lib/multiagent/
global_send.dart.md`, `lib/runtime/heap_fcp.dart.md`, and
`lib/runtime/terms.dart.md`. No SUT-side decision is re-derived
(FR-024 + FR-012/SC-007 — KB-resolved, not re-researched).

### `print` → `Console.WriteLine` (cache hit)

Cached idiom `rf-dart-print-and-terminate-to-csharp-equivalent` from
`lib/bytecode/runner.dart.md`. The alternative
`ITestOutputHelper.WriteLine` (cached from `test/
test_channel_construction.dart.md`) is the IDIOMATIC xUnit choice for
test code, BUT requires per-test injection and is NOT thread-safe for
agent-thread diagnostics. Console.WriteLine IS thread-safe
(`https://learn.microsoft.com/dotnet/api/system.console.writeline`)
and matches Dart `print`'s observable cross-isolate stdout interleaving.
The baseline emission is Console.WriteLine; ITestOutputHelper recorded
as the alternative for projects that prefer xUnit-isolated capture.
