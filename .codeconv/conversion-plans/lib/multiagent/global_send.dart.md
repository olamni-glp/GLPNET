---
path: lib/multiagent/global_send.dart
cycle_group_id: 26
scc_siblings: []
generated_at: 2026-05-21T15:25:00Z
source_sha256: 06d2ab558a9c447dacd6c6766f747bbcc2bb613d4de39bbadcab50089cb08bd1
schema_version: 1
---

# Conversion Plan: lib/multiagent/global_send.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/multiagent/global_send.dart` (182 lines, sha256 `06d2ab558a9c447dacd6c6766f747bbcc2bb613d4de39bbadcab50089cb08bd1`). The file declares Global Send mechanism for madGLP (`global_send` goal that watches a reader and sends its value to a remote agent when the reader becomes known — see madGLP-spec.md Section 4).

Top-level shape:

- `library;` — Dart 2.19+ bare library directive (line 7) permitting the file-level `///` doc-comment block to attach to the library.
- Two relative imports of sibling files in the same directory: `import 'mad_helpers.dart';` (line 9), `import 'global_writers_table.dart';` (line 10). NO `package:` imports.
- Three top-level classes, all reference types, NO equality overrides (Dart default identity equality applies throughout):
  1. `class GlobalSendGoal` (lines 19–47) — three `final` fields (`readerAddr: int`, `globalName: GlobalName`, `destination: String`), single named-required constructor, NAMED FACTORY constructor `GlobalSendGoal.fromSpawn(GlobalSendSpawn spawn)` remapping `spawn.destAgent` → `destination` (the only call-site field rename in the file), `@override String toString()` producing `'GlobalSendGoal(reader=$readerAddr, name=$globalName, dest=$destination)'`.
  2. `class GlobalSendFiredResult` (lines 53–80) — SIX `final` fields including the file's ONLY explicitly nullable field `final Object? value`, plus `globalName: GlobalName`, `destination: String`, `newGoals: List<GlobalSendGoal>`, `extractedVariables: List<TermVar>`, `globalizeResult: GlobalizeResult`. Single named-required constructor. NO `toString` override. NO equality override.
  3. `class GlobalSendRegistry` (lines 88–182) — `final String agentId` (positional ctor param), one MUTABLE PRIVATE collection field `final Map<int, GlobalSendGoal> _goals = {};` (the `final` freezes the reference; contents mutable), positional constructor, six instance methods (`register`, `registerSpawns`, `hasGoalFor`, `getGoalFor`, `onWriterBound`, `clear`), one getter (`pendingCount`), and an `@override String toString()` using a `StringBuffer` with header `'GlobalSendRegistry($agentId)\n'` and per-entry `'  [${entry.key}] ${entry.value}'`.

Method-shape details for `GlobalSendRegistry`:

- `register(GlobalSendGoal goal)` — `_goals[goal.readerAddr] = goal;` (Dart `Map` indexer assignment = insert-or-overwrite).
- `registerSpawns(List<GlobalSendSpawn> spawns)` — `for (final spawn in spawns) { register(GlobalSendGoal.fromSpawn(spawn)); }`.
- `hasGoalFor(int readerAddr)` — single-expression arrow body `=> _goals.containsKey(readerAddr);`.
- `getGoalFor(int readerAddr)` returning `GlobalSendGoal?` — arrow body `=> _goals[readerAddr];` (nullable lookup).
- `onWriterBound({required int writerAddr, required Object? value, required GlobalWritersTable table, required List<TermVar> Function(Object?) extractVariables})` returning `GlobalSendFiredResult?` — six-step body: (i) `final goal = _goals.remove(writerAddr);` (Dart `Map.remove` returns the removed value or null); (ii) `if (goal == null) return null;` early-return; (iii) `final variables = extractVariables(value);` (callback invocation); (iv) calls top-level `globalize(...)` from `mad_helpers.dart` with `variables`, `agentId`, `goal.destination`, `table`; (v) `final newGoals = globalizeResult.spawns.map((s) => GlobalSendGoal.fromSpawn(s)).toList();`; (vi) returns a new `GlobalSendFiredResult(...)` populated from the six computed values.
- `pendingCount` getter — `=> _goals.length;`.
- `clear()` — `=> _goals.clear();`.
- `toString()` — builds `StringBuffer` with header, then `for (final entry in _goals.entries) { buf.writeln('  [${entry.key}] ${entry.value}'); }`, returns `buf.toString()`.

NO async, NO `Future`, NO `Stream`, NO `Completer`, NO mixin, NO sealed, NO extension, NO `Lock`/`Mutex`/`synchronized` — `GlobalSendRegistry` is per-agent isolate-local state (second isolate-local holder in `lib/multiagent/` after `GlobalWritersTable`), guarded by the isolate-ownership invariant (no shared mutable state across isolates).

Doc-comments cite madGLP-spec.md Section 4 (The global_send Predicate), Section 3.2 (Implementation Plan), and Section 12 (Goal Atomicity — "The globalization and message creation must happen atomically. New goals for nested variables must be registered before the current operation completes.").

## 2. Dart → C#/.NET Conversion Plan

Each construct in the convspec maps verbatim to its `target_decision`. Mirroring the ratified spec:

- **`library;` directive** → DROPPED. No .NET counterpart. The file-level `///` doc-comments become plain `//` file-header comments above the `namespace lib.multiagent` declaration. Per the convspec `rf-dart-library-directive-to-csharp-namespace-no-counterpart`.

- **`import 'mad_helpers.dart';` and `import 'global_writers_table.dart';`** → BOTH DROPPED (no `using` directive emitted). Both relative imports resolve to the same target namespace `lib.multiagent` as this file; intra-namespace references resolve without a `using`. Codegen MUST NOT emit `using lib.multiagent;` inside a file whose own namespace declaration is already `lib.multiagent`. Per the convspec `rf-dart-import-to-csharp-using`.

- **`class GlobalSendGoal`** → C# reference `class GlobalSendGoal` (NOT record, NOT struct) in namespace mirroring `lib/multiagent/`. Three get-only auto-properties (`int ReaderAddr`, `GlobalName GlobalName`, `string Destination`) populated by a single non-optional-parameter constructor. The named factory `GlobalSendGoal.fromSpawn(GlobalSendSpawn spawn)` maps to `public static GlobalSendGoal FromSpawn(GlobalSendSpawn spawn) => new GlobalSendGoal(readerAddr: spawn.ReaderAddr, globalName: spawn.GlobalName, destination: spawn.DestAgent);` (C# has no per-name constructor variants — static factories are the documented counterpart). The `destAgent` → `Destination` field-name rename is preserved verbatim in the factory body. `ToString()` override emits `$"GlobalSendGoal(reader={ReaderAddr}, name={GlobalName}, dest={Destination})"`. NO equality override (Dart default identity equality is preserved by C# default reference identity). Per the convspec `rf-dart-final-named-required-ctor-to-csharp-getonly-properties`.

- **`class GlobalSendFiredResult`** → C# reference `class GlobalSendFiredResult` (NOT record, NOT struct) with six get-only auto-properties: `GlobalName GlobalName`, `string Destination`, `object? Value` (the ONLY nullable property — Dart `Object?` maps to .NET `object?` under NRT, NOT `dynamic`), `IReadOnlyList<GlobalSendGoal> NewGoals`, `IReadOnlyList<TermVar> ExtractedVariables`, `GlobalizeResult GlobalizeResult`. Single non-optional-parameter constructor populates all six. NO `ToString()` override (matches Dart default). NO equality override. Per the convspec `rf-dart-final-named-required-ctor-to-csharp-getonly-properties`.

- **`class GlobalSendRegistry`** → C# reference `class GlobalSendRegistry` in namespace mirroring `lib/multiagent/`.
  - `AgentId` get-only auto-property populated by a single non-optional-parameter constructor.
  - `private readonly Dictionary<int, GlobalSendGoal> _goals = new Dictionary<int, GlobalSendGoal>();` — NOT `ConcurrentDictionary` (per concurrency-model nuance: per-agent isolate-local state, accessed only by code running on the agent's owning execution context; `ConcurrentDictionary` would advertise a thread-safety property the surrounding logic does not enforce and would not make `OnWriterBound`'s compound `Remove` → `Globalize` → `Select.ToList` sequence atomic). `readonly` freezes the reference; contents remain mutable. Per the convspec `rf-dart-isolate-singlethread-to-csharp-actor-or-pinned-thread`.
  - `Register(GlobalSendGoal goal)` → `_goals[goal.ReaderAddr] = goal;` (Dictionary indexer = insert-or-overwrite, documented .NET counterpart of Dart `Map[k] = v`).
  - `RegisterSpawns(IReadOnlyList<GlobalSendSpawn> spawns)` → `foreach (var spawn in spawns) { Register(GlobalSendGoal.FromSpawn(spawn)); }`.
  - `HasGoalFor(int readerAddr)` → expression-bodied `=> _goals.ContainsKey(readerAddr);` (direct counterpart of Dart `Map.containsKey`).
  - `GetGoalFor(int readerAddr)` returning `GlobalSendGoal?` → body `_goals.TryGetValue(readerAddr, out var goal) ? goal : null` (documented .NET nullable-lookup idiom; Dart `Map[k]` returning nullable maps to `TryGetValue` + conditional return).
  - `OnWriterBound(int writerAddr, object? value, GlobalWritersTable table, Func<object?, IReadOnlyList<TermVar>> extractVariables)` returning `GlobalSendFiredResult?` (synchronous, NOT `Task`-returning):
    - `if (!_goals.Remove(writerAddr, out var goal)) return null;` — uses the C# 8+ `Dictionary<TKey,TValue>.Remove(TKey, out TValue)` overload (faithful counterpart of Dart `Map.remove` returning the removed value).
    - `var variables = extractVariables(value);` — callback invocation.
    - `var globalizeResult = Globalize(variables: variables, localAgent: AgentId, remoteAgent: goal.Destination, table: table);` — calls the static `Globalize` on the `MadHelpers` host.
    - `var newGoals = globalizeResult.Spawns.Select(s => GlobalSendGoal.FromSpawn(s)).ToList();` — LINQ `Select(...).ToList()` is the documented counterpart of Dart `Iterable.map(...).toList()`.
    - Returns `new GlobalSendFiredResult(globalName: goal.GlobalName, destination: goal.Destination, value: value, newGoals: newGoals, extractedVariables: variables, globalizeResult: globalizeResult);`.
    - The Spec Section 12 Goal-Atomicity doc-comment is preserved VERBATIM as a `///` XML-doc `<remarks>` on the method; the "writer and reader share the same address" comment is preserved as an inline `//` comment. NO per-method `lock` / `SemaphoreSlim` — atomicity is structural (isolate-ownership invariant), not per-method.
  - `PendingCount` get-only property → `=> _goals.Count;`.
  - `Clear()` → `=> _goals.Clear();`.
  - `ToString()` override → `System.Text.StringBuilder buf = new StringBuilder(); buf.Append($"GlobalSendRegistry({AgentId})\n"); foreach (var entry in _goals) { buf.AppendLine($"  [{entry.Key}] {entry.Value}"); } return buf.ToString();` (Dart `StringBuffer` → `StringBuilder`; `writeln` → `AppendLine`; iterates `KeyValuePair<int, GlobalSendGoal>` — `entry.Key`/`entry.Value` matches Dart `entry.key`/`entry.value`). Per the convspec `rf-dart-stringbuffer-to-csharp-stringbuilder`.

Concurrency contract (LOAD-BEARING — reused verbatim from `global_writers_table.dart` and `heap_fcp.dart` per CLAUDE.md / commit `497428c8`): `GlobalSendRegistry` is the second per-agent isolate-local state holder in `lib/multiagent/`. The Dart `Map<int, GlobalSendGoal>` is unguarded by lock or atomic because no other thread can ever touch it under Dart's isolate-isolation invariant. The .NET port preserves this single-owning-thread invariant via the `isolate_manager.dart` port (per commit `12a468f5`: per-agent `Task.Run` consuming a `Channel<IsolateMessage>` — the consumer Task IS the agent's owning execution context, heap accessed only inside the await-foreach body, no `lock`/`Interlocked`/`ConcurrentDictionary` in per-agent state). Plain `Dictionary<int, GlobalSendGoal>` is the faithful counterpart.

## 3. Decomposed Task Units

- T1: Emit namespace declaration `namespace lib.multiagent` (mirroring `lib/multiagent/` per the workspace's pair-specific namespace convention) at top of file; drop `library;` directive and both relative imports (no `using` directives emitted). — done.
- T2: Emit `public class GlobalSendGoal` (NOT record, NOT struct): three get-only auto-properties (`int ReaderAddr`, `GlobalName GlobalName`, `string Destination`); single non-optional-parameter ctor; static factory `FromSpawn(GlobalSendSpawn)` renaming `DestAgent` → `Destination`; `ToString()` override emitting `$"GlobalSendGoal(reader={ReaderAddr}, name={GlobalName}, dest={Destination})"`; NO equality override (identity preserved). — done.
- T3: Emit `public class GlobalSendFiredResult` (NOT record, NOT struct): six get-only auto-properties (`GlobalName GlobalName`, `string Destination`, `object? Value`, `IReadOnlyList<GlobalSendGoal> NewGoals`, `IReadOnlyList<TermVar> ExtractedVariables`, `GlobalizeResult GlobalizeResult`); single non-optional-parameter ctor; NO `ToString` override; NO equality override. — done.
- T4: Emit `public class GlobalSendRegistry`: `AgentId` get-only property; `private readonly Dictionary<int, GlobalSendGoal> _goals = new Dictionary<int, GlobalSendGoal>();` (NOT `ConcurrentDictionary` — isolate-ownership invariant); single non-optional-parameter ctor. — done.
- T5: Emit `Register(GlobalSendGoal goal)` body `_goals[goal.ReaderAddr] = goal;` and `RegisterSpawns(IReadOnlyList<GlobalSendSpawn> spawns)` body `foreach (var spawn in spawns) { Register(GlobalSendGoal.FromSpawn(spawn)); }`. — done.
- T6: Emit `HasGoalFor(int readerAddr)` expression-bodied `=> _goals.ContainsKey(readerAddr);` and `GetGoalFor(int readerAddr)` returning `GlobalSendGoal?` with `TryGetValue`-based nullable-lookup body. — done.
- T7: Emit `OnWriterBound(int writerAddr, object? value, GlobalWritersTable table, Func<object?, IReadOnlyList<TermVar>> extractVariables)` returning `GlobalSendFiredResult?` (synchronous): uses `Dictionary.Remove(key, out value)` early-return; calls `Globalize(...)`; LINQ `Select(...).ToList()` materialises new goals; returns new `GlobalSendFiredResult`. Preserves Spec Section 12 atomicity doc-comment verbatim as XML-doc `<remarks>`. NO `lock`/`SemaphoreSlim`. — done.
- T8: Emit `PendingCount` get-only property body `=> _goals.Count;` and `Clear()` body `=> _goals.Clear();`. — done.
- T9: Emit `ToString()` override on `GlobalSendRegistry` using `StringBuilder` + header `$"GlobalSendRegistry({AgentId})\n"` + `foreach (var entry in _goals) { buf.AppendLine($"  [{entry.Key}] {entry.Value}"); }`. — done.
- T10: Preserve all `///` doc-comments as XML-doc `<summary>` / `<remarks>` on the C# port; preserve the inline "writer and reader share the same address" comment as inline `//`. — done.

## 4. Research Findings

None required — every non-trivial construct is grounded in the ratified convspec, which is itself grounded in reused idioms from sibling convspecs (`lib/multiagent/global_writers_table.dart.md`, `lib/multiagent/mad_helpers.dart.md`) and in official Dart and .NET documentation already cited there. Per the ratified convspec's "Notes" section, zero escalations were recorded; this plan inherits that finding verbatim. The convspec cites:

- Dart concurrency overview (https://dart.dev/language/concurrency) — isolate-isolation invariant.
- Microsoft Learn `Channel<T>` (https://learn.microsoft.com/dotnet/core/extensions/channels) and `ConcurrentExclusiveSchedulerPair` (https://learn.microsoft.com/dotnet/api/system.threading.tasks.concurrentexclusiveschedulerpair) — single-owning-execution-context options.
- Microsoft Learn auto-properties (https://learn.microsoft.com/dotnet/csharp/programming-guide/classes-and-structs/properties) — get-only immutable-holder shape.
- Microsoft Learn records (https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/record) — value-equality semantics, explicitly rejected here.
- Microsoft Learn reference types (https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/reference-types) — `object` as `System.Object`.
- Microsoft Learn `Func<T,TResult>` (https://learn.microsoft.com/dotnet/api/system.func-2) — canonical single-arg value-returning delegate.
- Microsoft Learn `Dictionary<TKey,TValue>.Remove(TKey, out TValue)` (https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2.remove#system-collections-generic-dictionary-2-remove(-0-0@)) — faithful counterpart of Dart `Map.remove`.

No `WebSearch` / `WebFetch` / `Agent` invocations were required or attempted; all findings are verbatim-derivable from the ratified convspec and the cited Microsoft Learn / dart.dev documentation.

## 5. Consistency Pass

Fixed — derived from the ratified convspec `D:\bstdev\research\glp\glpnet\.codeconv\conversion-specs\lib\multiagent\global_send.dart.md` (mirrored verbatim). All seven constructs in the spec map one-to-one to the conversion units in §2 and the task list in §3. No drift between §2 task targets and §3 task units; no spec construct was dropped or invented; no nuance was paraphrased. Concurrency-model nuance (per-agent isolate-local state, NO `ConcurrentDictionary`, NO per-method `lock`/`SemaphoreSlim`, NO async surface) is preserved verbatim, consistent with the closed-escalation rulings in commits `497428c8` (heap_fcp single-owning-context) and `12a468f5` (isolate_manager `Channel<T>` actor mailbox). Reference-vs-value choices (all three classes stay `class`, NOT `record`/`struct`) are preserved verbatim, consistent with the convspec's identity-equality rationale (goals stored in a `Dictionary<int, GlobalSendGoal>` keyed by reader-address; value equality would silently change registry-key behaviour). Nullable-Object choice (`Object?` → `object?`, NOT `dynamic`) is preserved verbatim. The `destAgent` → `Destination` field-name rename in `FromSpawn` is preserved verbatim per the convspec's "field-rename nuance".

## 6. Escalations

None.
