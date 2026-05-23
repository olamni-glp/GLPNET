---
path: lib/runtime/runtime.dart
cycle_group_id: 36
scc_siblings: [lib/bytecode/runner.dart, lib/multiagent/mad_context.dart, lib/runtime/body_kernels.dart, lib/runtime/glp_activation.dart, lib/runtime/system_predicates.dart]
generated_at: 2026-05-21T16:08:06Z
source_sha256: cb25bc0bcb2f6d07603fb9cba8c8b81802e016af0d8558cff28df8f8c3a470c3
schema_version: 1
---

# Conversion Plan: lib/runtime/runtime.dart

## 1. Source Analysis

`lib/runtime/runtime.dart` defines a single class `GlpRuntime`: the **runtime facade aggregate** that owns and exposes the per-runtime collaborators and per-goal mutable state for the GLP execution engine. Direct inspection of the file (285 lines, sha256 `cb25bc0b…`) yields the following inventory:

**File-scope directives (12 imports):**
- `import 'dart:io';` — used only for `RandomAccessFile` (the file-handle map value type + `closeSync()` callsite).
- `import 'dart:ffi' as ffi;` — prefixed import; used for `ffi.DynamicLibrary` (the library-handle map value type) and `ffi.DynamicLibrary.open(path)` (the load API).
- Eight bare relative imports of sibling `lib/runtime/` files: `machine_state.dart`, `heap_fcp.dart`, `suspend_ops.dart`, `commit.dart`, `abandon.dart` (side-effect-only — no symbol referenced directly in this file), `fairness.dart` (for `tailRecursionBudgetInit`, `nextTailBudget`, `resetTailBudget`), `system_predicates.dart` (for `SystemPredicateRegistry`), `body_kernels.dart` (for `BodyKernelRegistry` + `registerStandardBodyKernels`).
- Two `package:`-prefixed imports with `show` clauses: `package:glp_runtime/bytecode/runner.dart` show `CallEnv`, `BytecodeRunner`; `package:glp_runtime/runtime/glp_activation.dart` show `GlpChannelHandle`.

**The single top-level class `GlpRuntime`** (no top-level functions, no top-level constants):

*Final reference fields (constructor-injected collaborators):*
- `final HeapFCP heap;`
- `final GoalQueue gq;`
- `final SystemPredicateRegistry systemPredicates;`
- `final BodyKernelRegistry bodyKernels;`

*Final reference fields with eager default initialisers (rebind-final, body-mutable):*
- `final Map<Object?, BytecodeRunner> runners = {};` — public; `Object?`-keyed (load-bearing nuance: Dart accepts null keys; .NET `Dictionary` rejects them by default).
- `final Map<String, GlpChannelHandle> glpChannels = {};` — public; registered by `activateModule()` (Phase 4, per the source's doc comment).
- `final Map<GoalId, int> _budgets = <GoalId, int>{};` — private; per-goal tail-budget table.
- `final Map<GoalId, CallEnv> _goalEnvs = <GoalId, CallEnv>{};` — private.
- `final Map<GoalId, Object?> _goalPrograms = <GoalId, Object?>{};` — private; `Object?` value because the program type is unknown at this layer.
- `final Map<GoalId, Object?> _goalModuleContexts = <GoalId, Object?>{};` — private; module context for RPC.
- `final Map<int, RandomAccessFile> _fileHandles = <int, RandomAccessFile>{};` — private; file-handle table.
- `final Map<int, ffi.DynamicLibrary> _libraries = <int, ffi.DynamicLibrary>{};` — private; library-handle table.
- `final Map<int, int> _waitReaders = <int, int>{};` — private; goalId → readerId mapping for `wait()` guard.
- `final Map<int, Set<GoalRef>> suspended = <int, Set<GoalRef>>{};` — **public** (no `_` prefix); reader varId → set of suspended goals (for scheduler-IRMA integration, spec section 8.4).
- `final Set<int> infrastructureGoalIds = {};` — public; infrastructure goal IDs (spec §3.4).

*Mutable instance counters (plain `int` fields):*
- `int _nextFileHandle = 1;` — private; monotonically-increasing handle generator.
- `int _nextLibraryHandle = 1;` — private; monotonically-increasing handle generator.
- `int nextGoalId = 10000;` — **public mutable field** (no `_` prefix, no getter/setter wrapper); doc comment says "Start at 10000 to avoid collisions with test goal IDs"; external code (runner.dart, agent_runtime.dart per tombstone callers) increments directly.
- `int _pendingTimers = 0;` — private; counter for pending timers in `wait()` guards.

*Read-only getter + mutator-pair for `_pendingTimers`:*
- `int get pendingTimers => _pendingTimers;` — Dart getter (read-only public view).
- `void incrementPendingTimers() => _pendingTimers++;` — Dart expression-bodied void method.
- `void decrementPendingTimers() => _pendingTimers--;` — Dart expression-bodied void method.

*Plain nullable instance fields (no encapsulation — DI seams):*
- `Object? madContext;` — multiagent context (set when running in multiagent mode; used by `_cold_send` kernel).
- `void Function(String)? outputCallback;` — nullable function-typed field; called instead of `print()` when set.

*Constructor:* named-only constructor with FOUR optional named parameters (none `required`), each defaulting to a freshly-allocated instance via `??` in the initialiser list:
```
GlpRuntime({HeapFCP? heap, GoalQueue? gq, SystemPredicateRegistry? systemPredicates, BodyKernelRegistry? bodyKernels})
    : heap = heap ?? HeapFCP(),
      gq = gq ?? GoalQueue(),
      systemPredicates = systemPredicates ?? SystemPredicateRegistry(),
      bodyKernels = bodyKernels ?? _createDefaultBodyKernels();
```

*Private static factory:*
```
static BodyKernelRegistry _createDefaultBodyKernels() {
  final registry = BodyKernelRegistry();
  registerStandardBodyKernels(registry);
  return registry;
}
```

*Wait-state helpers (read with default-on-miss + nullable-return idioms):*
- `bool? checkWaitState(int goalId)` — reads `_waitReaders[goalId]`; if null returns null; else returns `heap.isFullyBound(readerId)`.
- `void clearWaitState(int goalId)` — `_waitReaders.remove(goalId)`.
- `void setWaitReader(int goalId, int readerId)` — `_waitReaders[goalId] = readerId`.
- `int? getWaitReader(int goalId) => _waitReaders[goalId]`.

*Pass-through methods (delegate to a sibling static facade):*
- `List<GoalRef> commitSigmaHat(Map<int, Object?> sigmaHat)` — delegates to `CommitOps.applySigmaHatFCP`; enqueues activations via `_enqueueAll`; returns the activation list.
- `List<GoalRef> commitWriters(Iterable<int> writerIds)` — DEPRECATED; throws `UnimplementedError('Legacy commitWriters deprecated - use commitSigmaHat')`.
- `List<GoalRef> abandonWriter(int writerId)` — DEPRECATED; throws `UnimplementedError('Legacy abandonWriter deprecated - FCP has no abandon')`.
- `void suspendGoalFCP({required int goalId, required int kappa, required Set<int> readerVarIds})` — per-reader bookkeeping into `suspended` via `putIfAbsent(...).add(...)`; then delegates to `SuspendOps.suspendGoalFCP`.

*Per-goal budget helpers (read with default-on-miss):*
- `bool tailReduce(GoalId g)` — read budget, compute next via `nextTailBudget`; if 0 reset and return true (yield) else store decremented and return false (continue).
- `int budgetOf(GoalId g) => _budgets[g] ?? tailRecursionBudgetInit;` — expression-bodied.

*Per-goal map setters/getters (3 pairs over `_goalEnvs`, `_goalPrograms`, `_goalModuleContexts`):*
- `void setGoalEnv(GoalId g, CallEnv env)` / `CallEnv? getGoalEnv(GoalId g) => _goalEnvs[g];`
- `void setGoalProgram(GoalId g, Object? program)` / `Object? getGoalProgram(GoalId g) => _goalPrograms[g];`
- `void setGoalModuleContext(GoalId g, Object? ctx)` / `Object? getGoalModuleContext(GoalId g) => _goalModuleContexts[g];`

*Queue-management helpers:*
- `void _enqueueAll(List<GoalRef> acts)` — iterates and calls `enqueueReactivatedGoal` per element.
- `void enqueueReactivatedGoal(GoalRef goal)` — `gq.enqueue(goal); _removeFromSuspended(goal);`.
- `void _removeFromSuspended(GoalRef goal)` — collect-keys-then-remove two-pass idiom (avoids concurrent-modification).

*File-handle table surface:*
- `int allocateFileHandle(RandomAccessFile file)` — post-increment counter then index-set.
- `RandomAccessFile? getFile(int handle) => _fileHandles[handle];`
- `bool isValidHandle(int handle) => _fileHandles.containsKey(handle);`
- `void closeFileHandle(int handle)` — remove + try/catch-swallow `closeSync()`.
- `void closeAllFiles()` — iterate-and-close all values + clear map.

*FFI library-handle table surface:*
- `int loadLibrary(String path)` — try `ffi.DynamicLibrary.open(path)`; on success post-increment + store; on failure rethrow `Exception('Failed to load library $path: $e')`.
- `ffi.DynamicLibrary? getLibrary(int handle) => _libraries[handle];`
- `bool isValidLibrary(int handle) => _libraries.containsKey(handle);`
- `void closeLibrary(int handle)` — `_libraries.remove(handle);` (INTENTIONAL no-op on the native handle).
- `void closeAllLibraries()` — `_libraries.clear();` (INTENTIONAL no-op on the native handles).

**Cross-cutting nuances exercised here:**
- IDENTITY equality on the runtime instance (no `==`/`hashCode` override) — load-bearing because the runtime instance is passed by reference and mutated in place across the scheduler / runner / body-kernel / system-predicate / activation / multiagent layers.
- The Dart `Map.putIfAbsent(k, () => default).add(v)` idiom is exercised exactly once on the `suspended` map.
- The Dart `m[k] ?? default` / `m[k] returns nullable on miss` pattern is exercised at multiple sites.
- The Dart "collect-keys-then-remove" two-pass mutate-during-iterate idiom is exercised in `_removeFromSuspended`.
- The Dart bare `try { … } catch (e) { /* Ignore */ }` swallow-all pattern is exercised at four sites (closeFileHandle, closeAllFiles × 2 inner, loadLibrary).
- The Dart post-increment-and-store-then-return-pre-value `_field++` idiom is exercised at three sites (`_nextFileHandle++`, `_nextLibraryHandle++`, `nextGoalId++` — the last from external callers).
- The Dart `dart:io` `RandomAccessFile` and `dart:ffi` `DynamicLibrary` types are the **only** OS-resource types exercised here — well-known dart→.NET nuances.

**Single owning-context invariant (cycle-group inheritance):**
The threading-model decision is **already ratified upstream** (escalation #4 closed in commit `497428c8` for `heap_fcp.dart`, and escalation #5 closed in commit `12a468f5` for `isolate_manager.dart`'s `Channel<T>` mailbox). Per the convspec preamble, *every* field on `GlpRuntime` (including the mutable counters, the maps, the `nextGoalId` public field) is assumed to be touched ONLY by the single owning execution context (the consumer `Task` for the agent that owns this runtime — or the main-thread driver in single-runtime tests). No `lock` / `Interlocked` / `ConcurrentDictionary` / `volatile` is introduced. Cross-context messaging is the agent-mailbox `Channel<IsolateMessage>` boundary in `isolate_manager.dart`; this file's surface is reached *inside* that consumer's `await foreach`. This decision is **not re-escalated** here (FR-013).

## 2. Dart → C#/.NET Conversion Plan

The plan below mirrors the convspec's `constructs:` block one-to-one. Each row records the source construct, the target C#/.NET decision, and any load-bearing nuance carry-forward. Every decision is verbatim-derivable from the convspec or upstream pinned idioms.

| # | Construct | Target |
| --- | --- | --- |
| C1 | `import 'dart:io';` | `using System.IO;` (per `rf-dart-import-dartio-to-csharp-using-systemio`) |
| C2 | `import 'dart:ffi' as ffi;` | `using System.Runtime.InteropServices;` — Dart prefix `ffi.` collapses to bare references in C# (per `rf-dart-import-dartffi-to-csharp-using-interopservices`) |
| C3 | Eight relative imports targeting `lib/runtime/*.dart` | Collapses to a single `using <root>.Runtime;` (per `rf-dart-import-relative-to-csharp-using-namespace`); side-effect-only import (`abandon.dart`) is preserved by including `abandon.cs` in the same assembly |
| C4 | Two `package:` imports with `show` clauses (`runner.dart` → `CallEnv`, `BytecodeRunner`; `glp_activation.dart` → `GlpChannelHandle`) | `using <root>.Bytecode;` and `using <root>.Runtime;` (latter coincides with C3) — `show` per-symbol filter is DROPPED (one-way coarsening; per `rf-dart-import-show-clause-no-csharp-counterpart`) |
| C5 | `class GlpRuntime { … }` — mutable state, identity equality | `public class GlpRuntime` — reference type. **NEVER `record` / `record class` / `record struct` / `struct`** (per `rf-dart-mutable-state-class-identity-equality-to-csharp-class`) |
| C6 | Four `final` constructor-injected reference fields (`heap`, `gq`, `systemPredicates`, `bodyKernels`) | Four init-only properties: `public HeapFCP Heap { get; }`, `public GoalQueue Gq { get; }`, `public SystemPredicateRegistry SystemPredicates { get; }`, `public BodyKernelRegistry BodyKernels { get; }` — assigned in constructor body |
| C7 | `final Map<Object?, BytecodeRunner> runners = {};` (public) | `public Dictionary<object?, BytecodeRunner> Runners { get; } = new();` — code-review obligation: callers MUST NOT store a literal-null key (`Dictionary<,>` rejects null keys by default; sentinel-comparer is the upgrade path if a future caller needs the null key) |
| C8 | `final Map<String, GlpChannelHandle> glpChannels = {};` (public) | `public Dictionary<string, GlpChannelHandle> GlpChannels { get; } = new();` |
| C9 | Seven private `final Map<…, …>` fields with `<…, …>{}` initialisers (`_budgets`, `_goalEnvs`, `_goalPrograms`, `_goalModuleContexts`, `_fileHandles`, `_libraries`, `_waitReaders`) | Seven `private readonly Dictionary<…, …> _x = new();` fields — generic argument ports: `GoalId` → `int` (per machine_state.dart.md alias); `Object?` → `object?`; `RandomAccessFile` → `FileStream` (C20 below); `ffi.DynamicLibrary` → `IntPtr` (C21 below) |
| C10 | `final Map<int, Set<GoalRef>> suspended = <int, Set<GoalRef>>{};` (PUBLIC) | `public Dictionary<int, HashSet<GoalRef>> Suspended { get; } = new();` — public because source has no `_` prefix and external code reads it; mutation goes through the methods. Set element equality delegated to `GoalRef`'s synthesised `Equals`/`GetHashCode` (machine_state.dart.md decides `GoalRef` is a `readonly record struct`) |
| C11 | `final Set<int> infrastructureGoalIds = {};` (public) | `public HashSet<int> InfrastructureGoalIds { get; } = new();` |
| C12 | `int _nextFileHandle = 1;` and `int _nextLibraryHandle = 1;` (private counters) | `private int _nextFileHandle = 1;` and `private int _nextLibraryHandle = 1;` — `++` semantics identical |
| C13 | `int nextGoalId = 10000;` (PUBLIC MUTABLE FIELD, not a property) | `public int NextGoalId = 10000;` — **public mutable instance field**, NOT a property. Load-bearing because external code does `runtime.NextGoalId++` directly; the field form preserves the single-load+store hot-path semantics (Microsoft Learn "Fields" guidance: fields may be public for hot-path counters) |
| C14 | `int _pendingTimers = 0;` + `int get pendingTimers => _pendingTimers;` + two void mutators | `private int _pendingTimers = 0;` + `public int PendingTimers => _pendingTimers;` (expression-bodied read-only property) + `public void IncrementPendingTimers() => _pendingTimers++;` + `public void DecrementPendingTimers() => _pendingTimers--;` — preserves the source's encapsulation discipline for this counter |
| C15 | `Object? madContext;` (plain public nullable field — DI seam) | `public object? MadContext { get; set; }` — auto-property with get/set; **NOT `late final`** (source allows multiple writes / no write); per `rf-dart-nullable-object-injection-seam-to-csharp-object-question-property` |
| C16 | `void Function(String)? outputCallback;` (plain public nullable delegate field — DI seam) | `public Action<string>? OutputCallback { get; set; }` — nullable delegate auto-property; callers use `OutputCallback?.Invoke(s)` (carry-forward `rf-dart-void-function-question-to-csharp-action-nullable`) |
| C17 | Named-only constructor with four optional params, all `??`-defaulted | `public GlpRuntime(HeapFCP? heap = null, GoalQueue? gq = null, SystemPredicateRegistry? systemPredicates = null, BodyKernelRegistry? bodyKernels = null)` — body: `Heap = heap ?? new HeapFCP(); Gq = gq ?? new GoalQueue(); SystemPredicates = systemPredicates ?? new SystemPredicateRegistry(); BodyKernels = bodyKernels ?? CreateDefaultBodyKernels();` (carry-forward `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`) |
| C18 | `static BodyKernelRegistry _createDefaultBodyKernels()` (private static seed factory) | `private static BodyKernelRegistry CreateDefaultBodyKernels()` — body: `var registry = new BodyKernelRegistry(); BodyKernels.RegisterStandardBodyKernels(registry); return registry;` (per body_kernels.dart.md, `registerStandardBodyKernels` is a `public static` method on the `BodyKernels` static class) |
| C19 | `List<GoalRef> commitSigmaHat(Map<int, Object?> sigmaHat) { … }` (pass-through to `CommitOps.applySigmaHatFCP`) | `public IReadOnlyList<GoalRef> CommitSigmaHat(SigmaHat sigmaHat) { var acts = CommitOps.ApplySigmaHatFCP(heap: Heap, sigmaHat: sigmaHat); EnqueueAll(acts); return acts; }` — uses the `SigmaHat` typedef alias (machine_state.dart.md `global using SigmaHat = Dictionary<int, object?>;`); return type follows the activation-list convention (`IReadOnlyList<GoalRef>`) |
| C20 | Two deprecated legacy methods throwing `UnimplementedError` (`commitWriters`, `abandonWriter`) | `[Obsolete("Legacy CommitWriters deprecated - use CommitSigmaHat", error: true)] public IReadOnlyList<GoalRef> CommitWriters(IEnumerable<int> writerIds) => throw new NotImplementedException("Legacy CommitWriters deprecated - use CommitSigmaHat");` and same shape for `AbandonWriter(int writerId)` (per `rf-dart-unimplementederror-to-csharp-notimplementedexception`); message strings preserved byte-identically (load-bearing for any test that reads `e.Message`) |
| C21 | `void suspendGoalFCP({required int goalId, required int kappa, required Set<int> readerVarIds})` — `putIfAbsent + add` over `suspended`, then delegate to `SuspendOps.suspendGoalFCP` | `public void SuspendGoalFCP(int goalId, int kappa, ISet<int> readerVarIds) { var goalRef = new GoalRef(goalId, kappa); foreach (var readerId in readerVarIds) { if (!Suspended.TryGetValue(readerId, out var set)) { set = new HashSet<GoalRef>(); Suspended[readerId] = set; } set.Add(goalRef); } SuspendOps.SuspendGoalFCP(heap: Heap, goalId: goalId, kappa: kappa, readerVarIds: readerVarIds); }` (per `rf-dart-putifabsent-set-add-to-csharp-tryget-add`; rejected `CollectionsMarshal.GetValueRefOrAddDefault` — obscures shape fidelity) |
| C22 | `bool tailReduce(GoalId g)` — read-with-default-on-miss, compute next budget, conditional store-and-return | `public bool TailReduce(int g) { var current = _budgets.TryGetValue(g, out var b) ? b : MachineStateConstants.TailRecursionBudgetInit; var next = Fairness.NextTailBudget(current); if (next == 0) { _budgets[g] = Fairness.ResetTailBudget(); return true; } else { _budgets[g] = next; return false; } }` (per `rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary`; fairness constants/functions per fairness.dart.md `Fairness` static class; `tailRecursionBudgetInit` lives on `MachineStateConstants` per machine_state.dart.md) |
| C23 | `int budgetOf(GoalId g) => _budgets[g] ?? tailRecursionBudgetInit;` (expression-bodied) | `public int BudgetOf(int g) => _budgets.TryGetValue(g, out var b) ? b : MachineStateConstants.TailRecursionBudgetInit;` — expression-bodied with ternary |
| C24 | Three setter/getter method pairs (`setGoalEnv`/`getGoalEnv`, `setGoalProgram`/`getGoalProgram`, `setGoalModuleContext`/`getGoalModuleContext`) — getters return nullable (Dart `Map[k]` returns `V?`) | Three pairs of methods on the class: setters are `public void SetX(int g, T v) => _map[g] = v;`; getters are `public T? GetX(int g) => _map.TryGetValue(g, out var v) ? v : null;` (per `rf-dart-map-index-to-csharp-tryget-or-null-for-nullable-return`); NOT indexer properties (three separate maps cannot share an indexer surface). For `_goalPrograms` and `_goalModuleContexts` the V is `object?` — automatic boxing semantics |
| C25 | `void _enqueueAll(List<GoalRef> acts)` (private foreach-and-delegate) | `private void EnqueueAll(IReadOnlyList<GoalRef> acts) { foreach (var a in acts) { EnqueueReactivatedGoal(a); } }` — block-bodied (multi-statement isn't material here; foreach loop with single call) |
| C26 | `void enqueueReactivatedGoal(GoalRef goal)` — enqueue + remove from suspended index | `public void EnqueueReactivatedGoal(GoalRef goal) { Gq.Enqueue(goal); _RemoveFromSuspended(goal); }` |
| C27 | `void _removeFromSuspended(GoalRef goal)` — collect-keys-then-remove two-pass idiom | `private void _RemoveFromSuspended(GoalRef goal) { var toRemove = new List<int>(); foreach (var entry in Suspended) { entry.Value.Remove(goal); if (entry.Value.Count == 0) { toRemove.Add(entry.Key); } } foreach (var key in toRemove) { Suspended.Remove(key); } }` (per `rf-dart-collect-keys-then-remove-to-csharp-collect-keys-then-remove`); `set.isEmpty` → `set.Count == 0` (carry-forward `rf-dart-isempty-to-csharp-count-eq-zero` from goal_queue.dart.md) |
| C28 | `bool? checkWaitState(int goalId)` — read-with-null-on-miss, then delegate to `heap.isFullyBound` | `public bool? CheckWaitState(int goalId) { if (!_waitReaders.TryGetValue(goalId, out var readerId)) return null; return Heap.IsFullyBound(readerId); }` |
| C29 | Three trivial wait-reader helpers (`clearWaitState`, `setWaitReader`, `getWaitReader`) | `public void ClearWaitState(int goalId) => _waitReaders.Remove(goalId);` (return-value discarded — matches Dart `Map.remove` whose return is ignored at this site); `public void SetWaitReader(int goalId, int readerId) => _waitReaders[goalId] = readerId;`; `public int? GetWaitReader(int goalId) => _waitReaders.TryGetValue(goalId, out var v) ? v : null;` |
| C30 | File-handle table — five methods (`allocateFileHandle`, `getFile`, `isValidHandle`, `closeFileHandle`, `closeAllFiles`) | `public int AllocateFileHandle(FileStream file) { var handle = _nextFileHandle++; _fileHandles[handle] = file; return handle; }`; `public FileStream? GetFile(int handle) => _fileHandles.TryGetValue(handle, out var f) ? f : null;`; `public bool IsValidHandle(int handle) => _fileHandles.ContainsKey(handle);`; `public void CloseFileHandle(int handle) { if (_fileHandles.Remove(handle, out var file)) { try { file.Close(); } catch (Exception) { /* Ignore close errors */ } } }`; `public void CloseAllFiles() { foreach (var file in _fileHandles.Values) { try { file.Close(); } catch (Exception) { /* Ignore close errors */ } } _fileHandles.Clear(); }`. Carries forward `rf-dart-randomaccessfile-to-csharp-filestream` — `RandomAccessFile` → `FileStream`; `closeSync()` → `Close()`; bare `catch (e)` → `catch (Exception)`. Uses .NET 5+ `Dictionary.Remove(key, out value)` overload for cleanest fidelity |
| C31 | FFI library-handle table — five methods (`loadLibrary`, `getLibrary`, `isValidLibrary`, `closeLibrary`, `closeAllLibraries`) | `public int LoadLibrary(string path) { try { var lib = NativeLibrary.Load(path); var handle = _nextLibraryHandle++; _libraries[handle] = lib; return handle; } catch (Exception e) { throw new Exception($"Failed to load library {path}: {e}"); } }`; `public IntPtr? GetLibrary(int handle) => _libraries.TryGetValue(handle, out var lib) ? lib : (IntPtr?)null;`; `public bool IsValidLibrary(int handle) => _libraries.ContainsKey(handle);`; `public void CloseLibrary(int handle) => _libraries.Remove(handle);` — **INTENTIONAL no-op on the native handle** (DO NOT inject `NativeLibrary.Free` — would change observable behaviour); `public void CloseAllLibraries() => _libraries.Clear();` — same no-op semantics. Per `rf-dart-ffi-dynamiclibrary-to-csharp-nativelibrary` |

**File-output layout:**
- Target file: `lib/runtime/runtime.cs`
- Namespace: `<root>.Runtime` (one of the eight relative imports collapses; coincides with `glp_activation`)
- Single top-level `public class GlpRuntime` in that namespace
- Using directives at the top of the file: `using System;` (for `Exception`, `Action<T>`, `IntPtr`), `using System.Collections.Generic;` (for `Dictionary`, `HashSet`, `IEnumerable`, `IReadOnlyList`, `ISet`, `List`), `using System.IO;` (for `FileStream`), `using System.Runtime.InteropServices;` (for `NativeLibrary`), `using <root>.Bytecode;` (for `CallEnv`, `BytecodeRunner`)
- Triple-slash doc comments (`///`) carry over mechanically to C# XML-doc comments (`///`)

## 3. Decomposed Task Units

- **T1.** Emit `lib/runtime/runtime.cs` file shell — namespace declaration `<root>.Runtime`, using directives (`System`, `System.Collections.Generic`, `System.IO`, `System.Runtime.InteropServices`, `<root>.Bytecode`), header comment with sha256 provenance. **Done when:** file compiles standalone (empty class body) under .NET 8 with NRT enabled.
- **T2.** Emit `public class GlpRuntime` declaration with four init-only properties (`Heap`, `Gq`, `SystemPredicates`, `BodyKernels`). **Done when:** properties surface compiles; no `record` / `struct` modifiers present (reference-type identity).
- **T3.** Emit the constructor (C17) with four nullable parameters and `??`-defaulted body assignments. **Done when:** `new GlpRuntime()` and `new GlpRuntime(heap: customHeap)` both compile.
- **T4.** Emit `private static BodyKernelRegistry CreateDefaultBodyKernels()` (C18) — body: allocate, seed via `BodyKernels.RegisterStandardBodyKernels(registry)`, return. **Done when:** constructor's `bodyKernels ?? CreateDefaultBodyKernels()` resolves.
- **T5.** Emit the two public auto-property dictionaries (C7 `Runners`, C8 `GlpChannels`) with `= new()` initialisers. **Done when:** `runtime.Runners[key] = bcRunner;` compiles.
- **T6.** Emit the seven private `readonly Dictionary<…, …> _x = new();` fields (C9). **Done when:** field-level initialisers compile and are reachable from all methods.
- **T7.** Emit the public `Suspended` (C10) and `InfrastructureGoalIds` (C11) auto-properties with `= new()` initialisers. **Done when:** external read sites compile (e.g. `runtime.Suspended[id]`).
- **T8.** Emit the integer counter fields: `_nextFileHandle = 1`, `_nextLibraryHandle = 1` (C12) and the public mutable field `NextGoalId = 10000` (C13 — field, NOT property). **Done when:** `runtime.NextGoalId++` compiles as a single field load-and-store.
- **T9.** Emit `_pendingTimers = 0` field + `PendingTimers` read-only property + `IncrementPendingTimers`/`DecrementPendingTimers` methods (C14). **Done when:** the increment/decrement methods compile and the property exposes the current value.
- **T10.** Emit the two nullable injection-seam auto-properties: `MadContext` (C15) and `OutputCallback` (C16). **Done when:** `runtime.MadContext = …;` and `runtime.OutputCallback = s => …;` both compile.
- **T11.** Emit `CommitSigmaHat(SigmaHat)` (C19) — pass-through to `CommitOps.ApplySigmaHatFCP` + `EnqueueAll`. **Done when:** delegating call compiles against the SCC sibling `commit.cs` surface.
- **T12.** Emit the two `[Obsolete(..., error: true)]` deprecated stubs `CommitWriters` and `AbandonWriter` (C20). **Done when:** any attempted caller fails the compile with the deprecation message; runtime throw of `NotImplementedException` preserves the Dart-side message string.
- **T13.** Emit `SuspendGoalFCP(int goalId, int kappa, ISet<int> readerVarIds)` (C21) — `TryGetValue + new HashSet + indexer-set + Add` idiom for `Suspended` bookkeeping, then delegate to `SuspendOps.SuspendGoalFCP`. **Done when:** the per-reader bookkeeping + delegation both compile.
- **T14.** Emit the per-goal budget helpers `TailReduce(int g)` (C22) and `BudgetOf(int g)` (C23). **Done when:** budget read/store loop compiles against `Fairness.NextTailBudget`, `Fairness.ResetTailBudget`, `MachineStateConstants.TailRecursionBudgetInit`.
- **T15.** Emit the three setter/getter method pairs (C24) over `_goalEnvs`, `_goalPrograms`, `_goalModuleContexts`. **Done when:** all six methods compile and the getter return type is the correct nullable (`CallEnv?` / `object?`).
- **T16.** Emit `EnqueueAll(IReadOnlyList<GoalRef>)` (C25), `EnqueueReactivatedGoal(GoalRef)` (C26), `_RemoveFromSuspended(GoalRef)` (C27). **Done when:** the three methods compile and the collect-keys-then-remove two-pass shape is preserved verbatim.
- **T17.** Emit the four wait-state methods: `CheckWaitState`, `ClearWaitState`, `SetWaitReader`, `GetWaitReader` (C28, C29). **Done when:** all four compile; `CheckWaitState` correctly returns `null` on no-wait-state and forwards to `Heap.IsFullyBound` otherwise.
- **T18.** Emit the file-handle table (C30) — five methods over `_fileHandles` using `FileStream` and the `Close()`/`catch (Exception)` swallow-all idiom. **Done when:** the five methods compile; `CloseFileHandle` uses the .NET 5+ `Dictionary.Remove(key, out value)` overload.
- **T19.** Emit the FFI library-handle table (C31) — five methods over `_libraries` using `IntPtr` + `NativeLibrary.Load(path)`; the close-handle methods preserve the no-op-on-native-handle semantics verbatim. **Done when:** the five methods compile; `CloseLibrary` and `CloseAllLibraries` do NOT call `NativeLibrary.Free` (intentional).
- **T20.** Cross-check the file against the convspec's `conversion_units:` block — every listed unit must be emitted exactly once; every Dart construct in the source file must map to exactly one C# member. **Done when:** the count and shape match (~30 properties+methods+fields).
- **T21.** Verify the .NET TFM target (per workspace settings) is .NET 8.0 or later — required for `Dictionary.Remove(key, out value)` overload, NRT, target-typed `new()` initialisers. **Done when:** the project file declares `<TargetFramework>net8.0</TargetFramework>` or later.

## 4. Research Findings

None required. Every non-trivial construct in this file is covered by the convspec's research_finding_id corpus:

- Three carry-forward idioms reused verbatim from upstream specs: `rf-dart-import-relative-to-csharp-using-namespace`, `rf-dart-import-show-clause-no-csharp-counterpart`, `rf-dart-mutable-state-class-identity-equality-to-csharp-class` (all three originally pinned in heap_fcp.dart.md / machine_state.dart.md and reused across the convspec corpus).
- Two carry-forward idioms reused from machine_state.dart.md and repl_play_runner.dart.md respectively: `rf-dart-named-required-ctor-with-defaults-to-csharp-positional-ctor-with-defaults`, `rf-dart-void-function-question-to-csharp-action-nullable`.
- One carry-forward idiom reused from goal_queue.dart.md: `rf-dart-isempty-to-csharp-count-eq-zero` (referenced in C27).
- Ten NEW idioms registered by THIS convspec, ALL with authoritative dart.dev / api.dart.dev and learn.microsoft.com citations recorded in the convspec's "Rationale and research provenance" section: `rf-dart-import-dartio-to-csharp-using-systemio`, `rf-dart-import-dartffi-to-csharp-using-interopservices`, `rf-dart-private-static-factory-seed-to-csharp-private-static`, `rf-dart-method-passthrough-to-csharp-method-passthrough`, `rf-dart-unimplementederror-to-csharp-notimplementedexception`, `rf-dart-putifabsent-set-add-to-csharp-tryget-add`, `rf-dart-map-index-null-coalesce-default-to-csharp-tryget-ternary`, `rf-dart-map-index-to-csharp-tryget-or-null-for-nullable-return`, `rf-dart-collect-keys-then-remove-to-csharp-collect-keys-then-remove`, `rf-dart-randomaccessfile-to-csharp-filestream`, `rf-dart-ffi-dynamiclibrary-to-csharp-nativelibrary`, `rf-dart-nullable-object-injection-seam-to-csharp-object-question-property`.

All findings authoritative both sides; no escalation.

## 5. Consistency Pass

**Convspec alignment.** The plan mirrors the convspec's `constructs:` block one-to-one (30 constructs / 30 plan rows C1–C31; one row aggregates the two `package:` imports). The `conversion_units:` block enumerates exactly the public/private surface this plan produces (verified: every unit in the convspec block appears in T2–T19). Zero escalations declared by the convspec; the plan inherits that count.

**SCC sibling coherence.** The five SCC siblings (`runner.dart`, `mad_context.dart`, `body_kernels.dart`, `glp_activation.dart`, `system_predicates.dart`) all consume the `GlpRuntime` reference identity AND its threading model. Cross-checks:

1. **`runner.dart`** uses `CallEnv` and `BytecodeRunner` (imported here via `show`); accesses `runtime.NextGoalId++`, `runtime.Runners[programKey]`, `runtime.GlpChannels[moduleName]`, calls `runtime.CommitSigmaHat(sigmaHat)`, `runtime.SuspendGoalFCP(...)`, `runtime.TailReduce(g)`, `runtime.GetGoalEnv(g)`, `runtime.SetGoalEnv(g, env)`. **This plan's decisions on those surfaces are co-dependent with the runner.dart plan.** Specifically: (a) `NextGoalId` must remain a public mutable field to preserve the single-field-load+store semantics on the runner's hot path; (b) `Runners` is `Dictionary<object?, BytecodeRunner>` — runner.dart must NOT write a null key (code-review obligation). (c) `CommitSigmaHat` returns `IReadOnlyList<GoalRef>` — runner.dart's consumer must use a read-only iterator, NOT mutate the activation list (and must not depend on insertion order beyond the queue's enqueue order).
2. **`mad_context.dart`** assigns `runtime.MadContext = madContext` from the multiagent layer. **This plan's decision on `MadContext` is co-dependent with mad_context.dart's plan.** Specifically: `MadContext` is a plain nullable get/set property — NOT `late final` (the multiagent layer may set it to null on tear-down).
3. **`body_kernels.dart`** consumes `runtime.MadContext` from inside `_cold_send`, calls `runtime.OutputCallback?.Invoke(s)` from inside `_output`, accesses the goal-keyed maps via `runtime.GetGoalEnv(g)` / `runtime.GetGoalProgram(g)` / `runtime.GetGoalModuleContext(g)`, calls `runtime.AllocateFileHandle`/`CloseFileHandle` and `runtime.LoadLibrary`/`CloseLibrary` from the I/O and FFI kernels. **This plan's surface decisions are co-dependent with body_kernels.dart's plan.** Specifically: (a) `BodyKernels.RegisterStandardBodyKernels` must be a `public static` method on the converted `BodyKernels` static class — invoked from C18 here. (b) `OutputCallback?.Invoke(s)` is the canonical callsite shape for the nullable delegate — body_kernels.dart's `_output` kernel plan must use that idiom.
4. **`glp_activation.dart`** registers `runtime.GlpChannels[moduleName] = handle` from `activateModule()` (Phase 4 per the source's doc comment). **This plan's decision on `GlpChannels` is co-dependent with glp_activation.dart's plan.** Specifically: the value type `GlpChannelHandle` is decided in glp_activation.dart's plan; this plan inherits whatever reference/value-type decision it makes (per the convspec preamble, identity-equality discipline is inherited from the upstream specs).
5. **`system_predicates.dart`** consumes `runtime.SystemPredicates` (the ctor-injected registry) for dispatch and `runtime.Suspended[readerId]` (the cross-cutting suspension index) for some predicates. **This plan's decisions on `SystemPredicates` (init-only property) and `Suspended` (public dictionary) are co-dependent with system_predicates.dart's plan.** Specifically: (a) `SystemPredicateRegistry` is a `final` field → init-only property — system_predicates.dart's plan must produce a registry whose state is initialised exactly once at constructor time; (b) `Suspended` iteration order must NOT be load-bearing for any consumer (`HashSet<GoalRef>` is unordered) — system_predicates.dart's plan must not depend on insertion order.

**Threading-model coherence.** The single-owning-context invariant (ratified upstream — escalations #4 closed in `497428c8` for `heap_fcp.dart`, #5 closed in `12a468f5` for `isolate_manager.dart`) extends transitively to `GlpRuntime` because every field on `GlpRuntime` is reached through the heap_fcp owning context or the consumer-Task mailbox boundary. No `lock` / `Interlocked` / `ConcurrentDictionary` / `volatile` introduced on any field. **This plan does not re-decide threading** (FR-013 — don't double-escalate).

**Identity-equality coherence.** `GlpRuntime` is a reference `class` (NEVER `record` / `struct`). This is consistent with the corpus-wide identity-equality discipline pinned upstream by machine_state.dart.md's `GoalState` analysis and reused for every mutable-state aggregate. SCC siblings' plans must respect that the same `GlpRuntime` instance is passed by reference to all of them (the runtime instance is the central facade).

**No internal inconsistencies detected.** The plan is internally consistent and consistent with the convspec, upstream pinned idioms, and SCC sibling decisions.

## 6. Escalations

None.

## 7. Cycle Siblings

This file is one of six members of SCC cycle-group 36. Each cross-reference below records which decisions in THIS plan are co-dependent with which decisions in the sibling's plan.

### lib/bytecode/runner.dart
- **Co-dependent decisions:**
  - **C13 `NextGoalId` field-vs-property:** runner.dart's bytecode dispatch performs `runtime.NextGoalId++` on its hot path (per the tombstone's caller graph). The field form (NOT property) preserves the single-load+store semantics; runner.dart's plan must read/write this surface as a raw field, not as `get_NextGoalId`/`set_NextGoalId` calls.
  - **C7 `Runners` map with `object?` key:** runner.dart populates `runners[programKey] = bcRunner` during compilation (per the source doc comment "Body kernels can register new runners here"). The code-review obligation that the key MUST NOT be literal-null falls on runner.dart's plan.
  - **C19 `CommitSigmaHat` return type `IReadOnlyList<GoalRef>`:** runner.dart's commit dispatch consumes the activation list — its plan must accept the read-only-iterator contract (no mutation of the returned list).
  - **C21 `SuspendGoalFCP` signature:** runner.dart calls this from its suspension handler; the parameter `ISet<int> readerVarIds` (NOT `Set<int>`) is the .NET-idiomatic shape that admits any set implementation.
  - **C24 goal-keyed setter/getter pairs:** runner.dart calls `runtime.SetGoalEnv(g, env)` / `runtime.GetGoalEnv(g)` / and similar for program/module-context. The method-pair shape (NOT indexer property) is preserved because the three maps cannot share an indexer key.
  - **CallEnv import:** runner.dart's plan owns the `CallEnv` type definition; this plan's `using <root>.Bytecode;` resolves it.

### lib/multiagent/mad_context.dart
- **Co-dependent decisions:**
  - **C15 `MadContext` property shape:** mad_context.dart's plan owns the `MadContext` *value type* (the multiagent context); THIS plan owns the *slot* that stores it. The decision that the slot is a plain nullable get/set property (NOT `late final`) means mad_context.dart's plan must allow the slot to be assigned multiple times (and possibly back to null on tear-down).
  - **Threading model inheritance:** mad_context.dart inherits the single-owning-context invariant from heap_fcp.dart (escalation #4 closed in `497428c8`). THIS plan inherits the same invariant transitively — no `lock` / `Interlocked` on `MadContext` access.
  - **`object?` runtime type:** the runtime stores the context as `object?` to break the cyclic compile-time dependency (mad_context.dart depends on runtime.dart and would create a cycle if the slot's type were the concrete `MadContext` type). The plan's use of `object?` is the canonical type-erasure escape hatch — mad_context.dart's consumers must cast on read.

### lib/runtime/body_kernels.dart
- **Co-dependent decisions:**
  - **C18 `CreateDefaultBodyKernels` factory's call into `BodyKernels.RegisterStandardBodyKernels(registry)`:** body_kernels.dart's plan must produce a `public static` method named `RegisterStandardBodyKernels(BodyKernelRegistry)` on the converted `BodyKernels` static class. The exact name and signature is the contract THIS plan depends on.
  - **C15+C16 injection-seam consumption:** body_kernels.dart's `_cold_send` kernel reads `runtime.MadContext`; its `_output` kernel calls `runtime.OutputCallback?.Invoke(s)`. The nullable property shapes decided here are the exact callsites body_kernels.dart's plan must use.
  - **C24 goal-keyed map access:** body_kernels.dart's kernels read goal-keyed module context via `runtime.GetGoalModuleContext(g)`. The `object?` return type is the contract — kernels must cast on read.
  - **C30+C31 OS-resource table consumption:** the file-I/O kernels (`open`/`read`/`write`/`close`) and FFI kernels (`load`/`lookup`/`call`/`close`) access these tables. The `FileStream` and `IntPtr` value types decided here are the exact handle types those kernels must consume.

### lib/runtime/glp_activation.dart
- **Co-dependent decisions:**
  - **C8 `GlpChannels` map:** glp_activation.dart's `activateModule()` registers handles into this map. The value type `GlpChannelHandle` is decided in glp_activation.dart's plan; THIS plan imports it via `using <root>.Runtime;`.
  - **Threading model inheritance:** glp_activation.dart inherits the single-owning-context invariant (escalations #4 and #5 closed in `497428c8` and `12a468f5`). THIS plan inherits the same — no `lock` / `Interlocked` on `GlpChannels` mutation.
  - **Identity equality on `GlpRuntime`:** `activateModule()` is passed a `GlpRuntime` reference; identity equality is required (the call sites depend on accumulating channel registrations in the same runtime instance across multiple activation steps).

### lib/runtime/system_predicates.dart
- **Co-dependent decisions:**
  - **C6 `SystemPredicates` init-only property:** the registry is initialised exactly once at constructor time (via the `?? new SystemPredicateRegistry()` default in C17). system_predicates.dart's plan must produce a `SystemPredicateRegistry` whose mutation surface (`Register(name, impl)`) is callable post-construction but whose lifetime is the runtime's lifetime.
  - **C10 `Suspended` map iteration:** system_predicates.dart consumes `runtime.Suspended[readerId]` for some predicates (e.g. the suspension-introspection surface). The `HashSet<GoalRef>` value-set is unordered — system_predicates.dart's plan must NOT depend on insertion order.
  - **Threading model inheritance:** identical to glp_activation.dart above.
  - **Identity equality:** identical to glp_activation.dart above — predicates are dispatched on the same `GlpRuntime` reference instance across the call chain.
