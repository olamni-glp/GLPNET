---
path: lib/multiagent/global_writers_table.dart
cycle_group_id: 24
scc_siblings: []
generated_at: 2026-05-21T14:37:36Z
source_sha256: 7ebe135c209066d868a420d5df4f3fc0be289656a597faa3e0acb5f6d371b9f1
schema_version: 1
---

# Conversion Plan: lib/multiagent/global_writers_table.dart

## 1. Source Analysis

File `glp_runtime_net/lib/multiagent/global_writers_table.dart` (270 lines) implements `W_p`, the per-agent Global Writers Table from `docs/ma/madGLP-spec.md` Section 3. It contains three top-level public classes and zero free-standing functions; the file uses `library;` with no library name and has no imports.

Class inventory (verbatim from source):

1. **`GlobalizeEntry`** (lines 15-29): immutable value holder.
   - Fields: `final int writerAddr;`, `final String remoteAgent;`.
   - Ctor: `GlobalizeEntry({required this.writerAddr, required this.remoteAgent});` (named-only, both `required`).
   - `@override String toString() => 'GlobalizeEntry(writer=$writerAddr, remote=$remoteAgent)';`
   - No `==`/`hashCode` override -> identity equality.

2. **`LocalizeEntry`** (lines 35-54): immutable value holder.
   - Fields: `final int writerAddr;`, `final String remoteAgent;`, `final int remoteIndex;`.
   - Ctor: `LocalizeEntry({required this.writerAddr, required this.remoteAgent, required this.remoteIndex});`
   - `@override String toString() => 'LocalizeEntry(writer=$writerAddr, remote=$remoteAgent, index=$remoteIndex)';`
   - No `==`/`hashCode` override.

3. **`GlobalWritersTable`** (lines 67-269): the per-agent W_p container.
   - Fields:
     - `final String agentId;` (line 69) — assigned by positional ctor `GlobalWritersTable(this.agentId);` (line 91).
     - `int _nextIndex = 1;` (line 75) — index counter; spec Section 3.2; index 0 reserved for serializer; indices never reused.
     - `int? _serializerWriterAddr;` (line 80) — nullable serializer writer address (spec Section 4.1).
     - `final Map<int, GlobalizeEntry> _globalizeEntries = {};` (line 85) — direct-index lookup map.
     - `final List<LocalizeEntry> _localizeEntries = [];` (line 89) — list searched by (agent, index) tuple.
   - Mutators / serializer block (lines 106-130):
     - `void initializeSerializerEntry(int netInWriterAddr)` — throws `StateError('Serializer entry already initialized')` if non-null; else assigns.
     - `int? get serializerWriterAddr => _serializerWriterAddr;` (line 116).
     - `void updateSerializerWriter(int newWriterAddr)` — throws `StateError('Cannot update serializer: not initialized')` if null; else assigns.
     - `bool get hasSerializerEntry => _serializerWriterAddr != null;` (line 133).
   - GlobalizeEntry ops (lines 147-187):
     - `int addGlobalizeEntry(int writerAddr, String remoteAgent)` — post-increments `_nextIndex`, inserts into map, returns allocated index.
     - `void addLocalizeEntry(int writerAddr, String remoteAgent, int remoteIndex)` — read-then-write: calls `findByRemote`; if non-null, throws `ArgumentError('Duplicate LocalizeEntry for ($remoteAgent, $remoteIndex): existing writer=${existing.writerAddr}, new writer=$writerAddr')`; else appends a new `LocalizeEntry`.
     - `GlobalizeEntry? lookupByIndex(int index)` — map indexer.
     - `LocalizeEntry? findByRemote(String agent, int index)` — `for` linear scan; first match by `(remoteAgent==agent && remoteIndex==index)`; returns `null` on miss.
   - Removals (lines 211-229):
     - `void removeGlobalizeEntry(int index)` — early-returns when `index == 0` (serializer is permanent per spec 4.1); else `_globalizeEntries.remove(index)`; idempotent on miss.
     - `void removeLocalizeEntry(String agent, int index)` — `_localizeEntries.removeWhere((e) => e.remoteAgent == agent && e.remoteIndex == index)`; multi-match-capable; idempotent on miss.
   - Index allocation / counters (lines 242-253):
     - `int allocateIndex()` — post-increments `_nextIndex`; no entry created (spec Section 5.1, outgoing reader-export by `global_send`).
     - `int get nextIndex => _nextIndex;`
     - `int get globalizeEntryCount => _globalizeEntries.length;`
     - `int get localizeEntryCount => _localizeEntries.length;`
   - `@override String toString()` (lines 256-268) — `StringBuffer`; header `'GlobalWritersTable($agentId, nextIndex=$_nextIndex)\n'`; `writeln` of serializer slot with `?? "(not initialized)"` interpolation; iterates `_globalizeEntries.entries` (MapEntry.key/.value) and `_localizeEntries` with interpolated `toString` calls.

No imports, no async surface (no `Future`/`Stream`/`Completer`/`async`/`await`), no locking primitives, no isolates spawned in this file, no mixins, no sealed types, no generics at the class level. Concurrency model is implicit: the file relies on the Dart-isolate single-threaded ownership invariant established by `lib/multiagent/isolate_manager.dart` (one isolate per agent; `MadContext` owns one `GlobalWritersTable wp`; never shared across isolates).

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec (`.codeconv/conversion-specs/lib/multiagent/global_writers_table.dart.md`). Target file: `lib/multiagent/global_writers_table.cs`. Namespace mirrors `lib/multiagent/` per workspace pair convention.

**Construct 1 -- `GlobalizeEntry` (immutable holder)** -> reference-type .NET `class GlobalizeEntry` with two get-only auto-properties (`int WriterAddr { get; }`, `string RemoteAgent { get; }`) populated by a single non-optional-parameter constructor `GlobalizeEntry(int writerAddr, string remoteAgent)`. `ToString()` overridden to produce `$"GlobalizeEntry(writer={WriterAddr}, remote={RemoteAgent})"`. NOT `record`/`record struct`/`struct`: Dart class uses default identity equality, entries are stored and looked up by reference in `Dictionary<int, GlobalizeEntry>`, and `record` would silently inject value equality that breaks the "the entry at index i" identity assumption in surrounding W_p code. Under nullable context: `int` non-nullable value type, `string` non-nullable reference (Dart `required` non-`?` params).

**Construct 2 -- `LocalizeEntry` (immutable holder)** -> same shape: reference-type .NET `class LocalizeEntry` with three get-only auto-properties (`int WriterAddr { get; }`, `string RemoteAgent { get; }`, `int RemoteIndex { get; }`) populated by a single non-optional-parameter constructor. `ToString()` -> `$"LocalizeEntry(writer={WriterAddr}, remote={RemoteAgent}, index={RemoteIndex})"`. NOT `record`/`record struct`/`struct` (same identity-vs-value-equality reasoning).

**Construct 3 -- `GlobalWritersTable` (mutable per-agent state)** -> reference-type .NET `class GlobalWritersTable` with:
- `string AgentId { get; }` get-only property, assigned by single-parameter constructor `GlobalWritersTable(string agentId)`.
- `private int _nextIndex = 1;` (plain `int`, NOT `long`, NOT `Interlocked.Increment`, NOT `volatile`).
- `private int? _serializerWriterAddr;` (nullable value type).
- `private readonly Dictionary<int, GlobalizeEntry> _globalizeEntries = new();` (NOT `ConcurrentDictionary`).
- `private readonly List<LocalizeEntry> _localizeEntries = new();` (NOT `ConcurrentBag`/`BlockingCollection`).
- No `lock`, no `Monitor`, no `SemaphoreSlim`, no `Channel`, no async signature on any member. Correctness depends on the isolate-ownership invariant provided by the .NET port of `isolate_manager.dart` (a single owning thread / single-threaded `TaskScheduler` / actor mailbox / `Channel<T>`-fed consumer Task — exact mechanism is that file's responsibility, not this one's). See convspec rf-dart-isolate-singlethread-to-csharp-actor-or-pinned-thread for the full rationale.

**Construct 4 -- `initializeSerializerEntry` / `updateSerializerWriter` / `serializerWriterAddr` / `hasSerializerEntry`** ->
- `public void InitializeSerializerEntry(int netInWriterAddr)` -> `if (_serializerWriterAddr is not null) throw new InvalidOperationException("Serializer entry already initialized"); _serializerWriterAddr = netInWriterAddr;`. Dart `StateError` -> .NET `InvalidOperationException` (per .NET exception guidelines for invalid-object-state errors).
- `public int? SerializerWriterAddr => _serializerWriterAddr;` get-only expression-bodied property.
- `public void UpdateSerializerWriter(int newWriterAddr)` -> `if (_serializerWriterAddr is null) throw new InvalidOperationException("Cannot update serializer: not initialized"); _serializerWriterAddr = newWriterAddr;`.
- `public bool HasSerializerEntry => _serializerWriterAddr is not null;`.

**Construct 5 -- `addGlobalizeEntry`** -> `public int AddGlobalizeEntry(int writerAddr, string remoteAgent) { var index = _nextIndex++; _globalizeEntries[index] = new GlobalizeEntry(writerAddr, remoteAgent); return index; }`. Post-increment semantics identical between Dart and C# for `int`. No `Interlocked.Increment` (would change return value from old-then-incremented to incremented; would also misadvertise thread-safety the surrounding logic does not have).

**Construct 6 -- `addLocalizeEntry` (read-then-write precondition)** -> `public void AddLocalizeEntry(int writerAddr, string remoteAgent, int remoteIndex)` calling `var existing = FindByRemote(remoteAgent, remoteIndex);` and on non-null throwing `new ArgumentException($"Duplicate LocalizeEntry for ({remoteAgent}, {remoteIndex}): existing writer={existing.WriterAddr}, new writer={writerAddr}")`; else `_localizeEntries.Add(new LocalizeEntry(writerAddr, remoteAgent, remoteIndex));`. Dart `ArgumentError` -> .NET `ArgumentException`. Error message preserved verbatim (load-bearing for spec Section 12 invariant). NO internal lock — read-then-write atomicity is provided by the isolate-ownership invariant.

**Construct 7 -- `lookupByIndex`** -> `public GlobalizeEntry? LookupByIndex(int index) => _globalizeEntries.GetValueOrDefault(index);` (or `_globalizeEntries.TryGetValue(index, out var e) ? e : null` — codegen may choose either; `GetValueOrDefault` is the canonical idiom for nullable-reference returns). Under enabled nullable context, return type `GlobalizeEntry?`.

**Construct 8 -- `findByRemote` (linear search, first-match)** -> `public LocalizeEntry? FindByRemote(string agent, int index) => _localizeEntries.FirstOrDefault(e => e.RemoteAgent == agent && e.RemoteIndex == index);`. LINQ first-match-wins; `FirstOrDefault` on reference type returns `null` on no-match, matching Dart `null`. Returns the same reference stored in the list (identity preserved). `==` on `string` is .NET value-equality on contents (matches Dart `String ==`).

**Construct 9 -- `removeGlobalizeEntry` (index-0 guarded, idempotent)** -> `public void RemoveGlobalizeEntry(int index) { if (index == 0) return; _globalizeEntries.Remove(index); }`. The `index == 0` early-return is a spec-mandated invariant (Section 4.1: "Serializer entry is permanent — never removed"); preserve verbatim. `Dictionary<TKey,TValue>.Remove(key)` is idempotent on absent keys (returns `false`, no exception); the boolean return is discarded (Dart side likewise discards the `Map.remove` return value).

**Construct 10 -- `removeLocalizeEntry` (predicate-based, multi-match-capable, idempotent)** -> `public void RemoveLocalizeEntry(string agent, int index) { _localizeEntries.RemoveAll(e => e.RemoteAgent == agent && e.RemoteIndex == index); }`. `List<T>.RemoveAll(Predicate<T>)` is the documented counterpart of Dart `List.removeWhere`: removes every matching element, returns count (discarded), idempotent on no-match. Defensive multi-match shape preserved (do NOT narrow to single-match `Remove`).

**Construct 11 -- `allocateIndex`** -> `public int AllocateIndex() => _nextIndex++;` Post-increment, no `Interlocked` (same reasoning as construct 5).

**Construct 12 -- counter/count getters** ->
- `public int NextIndex => _nextIndex;`
- `public int GlobalizeEntryCount => _globalizeEntries.Count;`
- `public int LocalizeEntryCount => _localizeEntries.Count;`
All expression-bodied get-only properties.

**Construct 13 -- `toString` (StringBuilder accumulator)** -> `public override string ToString()` using `var buf = new StringBuilder();` then:
- `buf.Append($"GlobalWritersTable({AgentId}, nextIndex={_nextIndex})\n");` (literal `\n` to match Dart `writeln` which appends `\n`, not platform newline).
- `buf.AppendLine($"  Serializer[0]: {_serializerWriterAddr?.ToString() ?? "(not initialized)"}");`
- `buf.AppendLine("  GlobalizeEntries:");`
- `foreach (var entry in _globalizeEntries) buf.AppendLine($"    [{entry.Key}] {entry.Value}");`
- `buf.AppendLine("  LocalizeEntries:");`
- `foreach (var entry in _localizeEntries) buf.AppendLine($"    {entry}");`
- `return buf.ToString();`
Newline nuance: `AppendLine` uses `Environment.NewLine` (`\r\n` on Windows), which differs from Dart `writeln`'s `\n`; this output is diagnostic-only (human-readable traces) and the platform-newline drift is acceptable (Dart side also makes no cross-OS byte-identical guarantee). The leading header explicitly uses `\n` via `Append` (not `AppendLine`) to match Dart's first line.

**Class-level imports / using directives:**
- `using System;` (for `ArgumentException`, `InvalidOperationException`).
- `using System.Collections.Generic;` (for `Dictionary`, `List`).
- `using System.Linq;` (for `FirstOrDefault`).
- `using System.Text;` (for `StringBuilder`).

**Namespace:** mirrors `lib/multiagent/` per the workspace's Dart->C# pair-specific convention (the exact namespace prefix is established by `glp_runtime_net/` mirror config, not this file).

## 3. Decomposed Task Units

- **T1: Emit file scaffold.** Definition of done: `lib/multiagent/global_writers_table.cs` exists with namespace declaration mirroring `lib/multiagent/`, four `using` directives (`System`, `System.Collections.Generic`, `System.Linq`, `System.Text`), nullable-context enabled, and three empty public class bodies (`GlobalizeEntry`, `LocalizeEntry`, `GlobalWritersTable`).
- **T2: Emit `GlobalizeEntry`.** Definition of done: class compiles with two get-only auto-properties (`int WriterAddr`, `string RemoteAgent`), a single non-optional-parameter constructor assigning both, and a `ToString()` override returning the Dart-identical interpolation string.
- **T3: Emit `LocalizeEntry`.** Definition of done: class compiles with three get-only auto-properties (`int WriterAddr`, `string RemoteAgent`, `int RemoteIndex`), a single non-optional-parameter constructor assigning all three, and a `ToString()` override returning the Dart-identical interpolation string.
- **T4: Emit `GlobalWritersTable` field block + constructor.** Definition of done: `AgentId` get-only property, single-parameter constructor, and the four private backing members (`_nextIndex=1`, `_serializerWriterAddr`, `_globalizeEntries=new()`, `_localizeEntries=new()`) declared with non-concurrent types (`Dictionary<>`/`List<>`/plain `int`/`int?`).
- **T5: Emit serializer block.** Definition of done: `InitializeSerializerEntry` (throws `InvalidOperationException` on already-initialised), `UpdateSerializerWriter` (throws `InvalidOperationException` on not-initialised), `SerializerWriterAddr` getter, `HasSerializerEntry` getter — error messages preserved verbatim from Dart.
- **T6: Emit `AddGlobalizeEntry` + `AddLocalizeEntry`.** Definition of done: `AddGlobalizeEntry` post-increments `_nextIndex`, inserts a new `GlobalizeEntry`, returns the allocated index; `AddLocalizeEntry` calls `FindByRemote`, throws `ArgumentException` with the verbatim Dart message on duplicate, else appends a new `LocalizeEntry`. NO `lock`/`Interlocked`/`SemaphoreSlim`.
- **T7: Emit `LookupByIndex` + `FindByRemote`.** Definition of done: `LookupByIndex` returns `_globalizeEntries.GetValueOrDefault(index)`; `FindByRemote` returns `_localizeEntries.FirstOrDefault(e => e.RemoteAgent == agent && e.RemoteIndex == index)`; both return `LocalizeEntry?`/`GlobalizeEntry?` under nullable context.
- **T8: Emit `RemoveGlobalizeEntry` + `RemoveLocalizeEntry`.** Definition of done: `RemoveGlobalizeEntry` early-returns on `index == 0` then `_globalizeEntries.Remove(index)`; `RemoveLocalizeEntry` calls `_localizeEntries.RemoveAll(predicate)`; both discard the boolean/count return.
- **T9: Emit `AllocateIndex` + counter/count getters.** Definition of done: `AllocateIndex` returns `_nextIndex++`; `NextIndex`, `GlobalizeEntryCount`, `LocalizeEntryCount` are expression-bodied get-only properties.
- **T10: Emit `ToString` override.** Definition of done: uses `StringBuilder`, leading `Append` with literal `\n`, `AppendLine` for serializer / entries headers, `foreach` over `_globalizeEntries` (`KeyValuePair.Key`/`Value`) and `_localizeEntries`, returns `buf.ToString()`; null-coalesce on serializer via `_serializerWriterAddr?.ToString() ?? "(not initialized)"`.
- **T11: Verify isolate-ownership precondition note.** Definition of done: a doc-comment (`///` triple-slash on the class) records that `GlobalWritersTable` is not internally thread-safe and relies on the `isolate_manager` port to provide single-owning-context access (mirrors the convspec nuance; serves as a load-bearing reminder for any future maintainer).
- **T12: Tombstone update.** Definition of done: tombstone front-matter `target_path` (already `lib/multiagent/global_writers_table.cs`), `plan_completed_at`, `plan_path` and `open_escalation_count` populated; status advances per orchestrator workflow.

## 4. Research Findings

None required. All decisions in §2 are verbatim-derivable from the ratified convspec, which itself cites authoritative Dart and .NET documentation (Dart isolate model, .NET `Channel<T>`/`ConcurrentExclusiveSchedulerPair` concurrency primitives, .NET `Dictionary.Remove` / `List<T>.RemoveAll` / LINQ `FirstOrDefault` / `StringBuilder` / `ArgumentException` / `InvalidOperationException` / get-only auto-properties / nullable reference types). The convspec's six research-finding IDs (`rf-dart-isolate-singlethread-to-csharp-actor-or-pinned-thread`, `rf-dart-final-named-required-ctor-to-csharp-getonly-properties`, `rf-dart-argumenterror-to-csharp-argumentexception`, `rf-dart-iterable-firstwhere-orelse-null-to-linq-firstordefault`, `rf-dart-map-remove-to-csharp-dictionary-remove`, `rf-dart-list-removewhere-to-csharp-list-removeall`, `rf-dart-stringbuffer-to-csharp-stringbuilder`) collectively cover every construct in this file; no additional research is needed for plan-stage decomposition.

## 5. Consistency Pass

- **§2 vs convspec §constructs[0] (`GlobalizeEntry`)**: §2 Construct 1 mirrors verbatim (reference-type class, two get-only auto-properties, single non-optional-param ctor, `ToString()` override, NOT record/struct, identity equality preserved). Consistent.
- **§2 vs convspec §constructs[1] (`LocalizeEntry`)**: §2 Construct 2 mirrors verbatim (three get-only auto-properties, same shape, identity equality). Consistent.
- **§2 vs convspec §constructs[2] (`GlobalWritersTable` concurrency model)**: §2 Construct 3 mirrors verbatim — non-concurrent `Dictionary`/`List`, plain `int`, no `lock`/`Interlocked`/`async`, isolate-ownership invariant deferred to `isolate_manager` port. Consistent with the load-bearing concurrency nuance. Cross-check against the most recent main commit (`12a468f5 fix(018-live-pass): close escalation #5 + #6 — isolate_manager Channel<T> actor mailbox`): the .NET isolate-manager port has now committed to `Channel<T>`-fed single-consumer Task per agent, which IS the "single owning execution context" mechanism this construct's nuance requires. Composes correctly with this file's preservation of single-owning-context state — no lock primitives needed here because per-agent state (incl. W_p) is accessed only inside the consumer Task's `await foreach` body. Consistent.
- **§2 vs convspec §constructs[3] (`addLocalizeEntry` read-then-write)**: §2 Construct 6 mirrors — `ArgumentException` with verbatim error message, no internal lock, atomicity from isolate ownership. Consistent.
- **§2 vs convspec §constructs[4] (`findByRemote`)**: §2 Construct 8 uses LINQ `FirstOrDefault` (convspec offers both LINQ and foreach as semantically equivalent; LINQ chosen for idiom alignment). Consistent.
- **§2 vs convspec §constructs[5] (`removeGlobalizeEntry`)**: §2 Construct 9 preserves `index == 0` early-return verbatim, uses `Dictionary.Remove` (idempotent on miss). Consistent.
- **§2 vs convspec §constructs[6] (`removeLocalizeEntry`)**: §2 Construct 10 uses `List<T>.RemoveAll(predicate)` (multi-match-capable, preserves defensive Dart `removeWhere` shape). Consistent.
- **§2 vs convspec §constructs[7] (`toString`)**: §2 Construct 13 uses `StringBuilder`, `Append` for the leading literal-`\n` header, `AppendLine` for body lines, `?.ToString() ?? "(not initialized)"` null-coalesce, `foreach` over `KeyValuePair`. Consistent. Newline-platform drift acknowledged in convspec as acceptable for diagnostic-only output.
- **§2 vs convspec §conversion_units**: every conversion_unit (namespace, two entry classes, `GlobalWritersTable` fields, serializer block, add operations, lookup, remove operations, `AllocateIndex`, counters, `ToString`) is represented one-for-one in §2 constructs and §3 tasks. Consistent.
- **§3 vs §2**: T1-T11 cover every construct in §2; T12 is the orchestrator-mandated tombstone bookkeeping. No §2 construct is unaccounted for in §3.
- **§3 vs §4**: §4 declares no research needed; §3 tasks introduce no construct that would require fresh research beyond the convspec's seven research findings. Consistent.
- **§2/§3 vs CLAUDE.md "Preserve Working Code"**: the `index == 0` serializer guard (Construct 9 / T8), the multi-match `RemoveAll` (Construct 10 / T8), and the read-then-write precondition check (Construct 6 / T6) are all preserved verbatim — no "while I'm at it" tightening or narrowing. Consistent.
- **§2/§3 vs the FR-006 source-grounding obligation**: every construct in §2 is traceable to a specific Dart line range in §1 (constructor at line 91, serializer block at 106-130, etc.). Consistent.

No gaps. No items pending. All checks pass.

## 6. Escalations

None.
