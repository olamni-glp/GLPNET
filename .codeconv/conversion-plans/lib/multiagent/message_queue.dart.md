---
path: lib/multiagent/message_queue.dart
cycle_group_id: 27
scc_siblings: []
generated_at: 2026-05-21T15:05:00Z
source_sha256: 619550d11a54842ba4948fd5cd8ac86d742fbaddf26fcd138ddc39c26da696d7
schema_version: 1
---

# Conversion Plan: lib/multiagent/message_queue.dart

## 1. Source Analysis

Inspected `glp_runtime_net/lib/multiagent/message_queue.dart` (175 lines, sha256
`619550d11a54842ba4948fd5cd8ac86d742fbaddf26fcd138ddc39c26da696d7`, matching
the tombstone). The library declaration is `library;` (unnamed), single import
`dart:collection` (for `Queue`).

Public surface — three top-level declarations in one file:

1. `enum MessageType { assignment, agentMessage }` — plain (non-enhanced)
   two-member enum, no fields, no methods, used only as a tag on
   `OutboundMessage`. Doc comments on members describe semantic intent
   (assignment vs. friend-to-friend message); no code observes ordinal values.

2. `class OutboundMessage` — immutable value-carrier holding:
   - `final String destination`
   - `final MessageType type`
   - `final List<int> payload` — doc-commented "Serialized payload (opaque
     bytes)"
   - Single named-required constructor:
     `OutboundMessage({required this.destination, required this.type,
     required this.payload})`
   - `@override String toString()` returning
     `'OutboundMessage(to=$destination, type=$type, ${payload.length} bytes)'`.

3. `class MessageQueue` — per-destination FIFO queue holder. Private state:
   `final Map<String, Queue<OutboundMessage>> _queuesByDestination = {};`.
   Methods/getters:
   - `void add(OutboundMessage message)` — `putIfAbsent(destination, () =>
     Queue<OutboundMessage>())` then `queue.add(message)`.
   - `OutboundMessage? poll(String destination)` — lookup, return null on
     missing/empty bucket, otherwise `removeFirst()`; cleans up empty bucket
     via `_queuesByDestination.remove(destination)`.
   - `OutboundMessage? peek(String destination)` — lookup, return null on
     missing/empty, otherwise `queue.first` (no mutation, no cleanup).
   - `int countFor(String destination)` — `queue?.length ?? 0`.
   - `List<String> get destinations` — `_queuesByDestination.keys.toList()`
     (eager snapshot).
   - `bool get isEmpty` / `bool get isNotEmpty` — delegate to
     `_queuesByDestination.isEmpty` / `.isNotEmpty`.
   - `int get totalLength` — `_queuesByDestination.values.fold(0, (sum,
     queue) => sum + queue.length)`.
   - `void clear()` — `_queuesByDestination.clear()`.
   - `void clearFor(String destination)` — `_queuesByDestination.remove(...)`.
   - `List<OutboundMessage> peekAll(String destination)` — returns `[]` on
     missing bucket, otherwise `List.unmodifiable(queue)`.
   - `List<OutboundMessage> pollAll(String destination)` — returns `[]` on
     missing/empty, otherwise materialises a copy via `List.from(queue)` and
     removes the bucket.
   - `@override String toString()` — early-returns `'MessageQueue: empty'` if
     empty; otherwise builds a multi-line listing via `StringBuffer` +
     `writeln`.

Concurrency surface: **none**. No `async`, no `await`, no `Future`,
`Completer`, `Stream`, `StreamController`, no `dart:isolate`, no locks. The
class is a pure synchronous in-memory data structure. Only external dependency
is `dart:collection.Queue<T>`. Callers (see tombstone) include
`agent_runtime.dart`, `isolate_manager.dart`, `mad_context.dart`,
`payload_serializer.dart`, and the active multiagent test suite.

## 2. Dart → C#/.NET Conversion Plan

Mirrors the ratified convspec verbatim (all six constructs preserved, idiomatic
PascalCase per .NET naming guidelines).

**Target unit**: `lib/multiagent/message_queue.cs` (per tombstone). Single
file; same namespace as `OutboundMessage` and sibling multiagent types
(established by the codeconv langpair convention).

### 2.1 `enum MessageType` → C# `enum MessageType`

- Two-member plain Dart enum → C# `enum MessageType { Assignment,
  AgentMessage }`. Default underlying `int`; no explicit values. PascalCase
  per .NET enum-naming guideline (Dart `assignment` → `Assignment`,
  `agentMessage` → `AgentMessage`). Value-type semantics match Dart enum
  semantics.

### 2.2 `class OutboundMessage` → reference-type C# class

- Reference-type `class` (NOT struct/record-struct) because instances are held
  by reference inside `Queue<OutboundMessage>` and `Dictionary<string,
  Queue<OutboundMessage>>`; identity matters for per-destination FIFO.
- Three `final` fields become get-only auto-properties initialised from the
  constructor:
  - `string Destination { get; }`
  - `MessageType Type { get; }`
  - `List<byte> Payload { get; }` (byte-buffer mapping; see 2.3)
- Single C# constructor with all three parameters required (Dart
  `required`-named-arg → mandatory C# parameter; named-argument calling style
  is optional at the call site).
- `override string ToString()` using interpolation:
  `$"OutboundMessage(to={Destination}, type={Type}, {Payload.Count} bytes)"`.
  `payload.length` (Dart) → `Payload.Count` (.NET `List<T>.Count`).
- Null-safety (nullable context enabled): all three properties non-nullable.

### 2.3 `List<int> payload` (opaque bytes) → `List<byte> Payload`

- Mapped to `System.Collections.Generic.List<byte>` (NOT `List<int>` /
  `Int32`), because the field doc-comment makes the byte-buffer intent
  explicit and a 1:1 `int` list would quadruple memory and misrepresent
  wire-level semantics. `List<byte>` (not `byte[]`) preserves Dart `List`
  semantics: `Add`, indexer, `Count`.
- Reference to a mutable list preserved (matches Dart `final` reference to
  mutable `List<int>`).

### 2.4 `Map<String, Queue<OutboundMessage>> _queuesByDestination` → `Dictionary<string, Queue<OutboundMessage>>`

- Top-level field: `private readonly Dictionary<string,
  Queue<OutboundMessage>> _queuesByDestination = new();`.
- Inner Dart `dart:collection.Queue` → `System.Collections.Generic.Queue<T>`
  (FIFO-only, NOT `LinkedList<T>` deque, NOT `ConcurrentQueue<T>`).
  Justification: only FIFO subset (`add` / `removeFirst` / `first` /
  `isEmpty` / `length`) is used; class is single-threaded.
- Operation mapping (FIFO-preserving):
  - `queue.add(x)` → `queue.Enqueue(x)`
  - `queue.removeFirst()` → `queue.Dequeue()`
  - `queue.first` → `queue.Peek()`
  - `queue.isEmpty` → `queue.Count == 0`
  - `queue.length` → `queue.Count`
  - `new Queue<OutboundMessage>()` default ctor
- `_queuesByDestination.remove(k)` → `_queuesByDestination.Remove(k)`.
- `_queuesByDestination.clear()` → `_queuesByDestination.Clear()`.
- `_queuesByDestination.isEmpty` / `.isNotEmpty` → `.Count == 0` / `.Count > 0`
  (Dictionary exposes `Count`, not `IsEmpty`).
- `putIfAbsent(destination, () => new Queue<OutboundMessage>())` has no exact
  .NET equivalent on `Dictionary`. Codegen emits the documented
  `TryGetValue`-or-create-and-`Add` idiom inline in the `Add` method:
  ```text
  // pseudocode pattern (NOT compilable C# per FR-023):
  // if not TryGetValue(destination, out var queue):
  //     queue = new Queue<OutboundMessage>()
  //     _queuesByDestination[destination] = queue   // (or .Add)
  // queue.Enqueue(message)
  ```
  (.NET 6 `CollectionsMarshal.GetValueRefOrAddDefault` is rejected as a
  micro-optimisation outside scope per convspec.)

### 2.5 Per-method mapping (public surface)

| Dart | C# (target signature) | Notes |
|---|---|---|
| `void add(OutboundMessage message)` | `void Add(OutboundMessage message)` | TryGetValue/create/Enqueue (2.4). |
| `OutboundMessage? poll(String destination)` | `OutboundMessage? Poll(string destination)` | Lookup; missing/empty → `null`. Otherwise `Dequeue()`; if bucket now empty, `Remove(destination)`. Nullable reference under enabled nullable context. |
| `OutboundMessage? peek(String destination)` | `OutboundMessage? Peek(string destination)` | Lookup; missing/empty → `null`. Otherwise `queue.Peek()`. No mutation, no cleanup. |
| `int countFor(String destination)` | `int CountFor(string destination)` | `TryGetValue(dest, out var q) ? q.Count : 0` (or equivalent null-conditional in C#). |
| `List<String> get destinations` | `IReadOnlyList<string> Destinations { get; }` | `_queuesByDestination.Keys.ToList()` — eager snapshot, materialised; documented snapshot contract preserved (mutation through `ClearFor`/`Add` must NOT be visible in a previously returned list). |
| `bool get isEmpty` | `bool IsEmpty { get; }` | `_queuesByDestination.Count == 0`. |
| `bool get isNotEmpty` | `bool IsNotEmpty { get; }` | `_queuesByDestination.Count > 0`. |
| `int get totalLength` | `int TotalLength { get; }` | `_queuesByDestination.Values.Sum(q => q.Count)` (LINQ Sum is the canonical fold-to-int eager terminal). |
| `void clear()` | `void Clear()` | `_queuesByDestination.Clear()`. |
| `void clearFor(String destination)` | `void ClearFor(string destination)` | `_queuesByDestination.Remove(destination)`. |
| `List<OutboundMessage> peekAll(String destination)` | `IReadOnlyList<OutboundMessage> PeekAll(string destination)` | Missing → empty `ReadOnlyCollection<T>` (or `Array.Empty`+wrap); otherwise wrap a NEW `List<OutboundMessage>(queue)` via `AsReadOnly()`. Matches Dart `List.unmodifiable` snapshot+throw-on-mutate contract. |
| `List<OutboundMessage> pollAll(String destination)` | `List<OutboundMessage> PollAll(string destination)` | Missing/empty → new empty `List<OutboundMessage>()`. Otherwise materialise a copy `new List<OutboundMessage>(queue)` then `Remove(destination)`. |
| `@override String toString()` | `override string ToString()` | Early-return `"MessageQueue: empty"` if `IsEmpty`. Otherwise `StringBuilder` accumulation. |

### 2.6 `toString()` — `StringBuffer` → `StringBuilder`

- `StringBuffer('MessageQueue:\n')` → `new StringBuilder("MessageQueue:\n")`.
- `buffer.writeln('  $destination: $count message(s)')` →
  `buffer.AppendLine($"  {destination}: {count} message(s)")`.
- `buffer.toString()` → `buffer.ToString()`.
- **Documented platform nuance (FR-009)**: Dart `writeln` appends `\n`; .NET
  `AppendLine` appends `Environment.NewLine` (`\r\n` on Windows, `\n` on
  Unix). For this diagnostic `ToString()` the line break is a separator, not
  a wire-format byte — acceptable AND explicitly recorded per convspec.

### 2.7 Imports / namespaces

- `using System.Collections.Generic;` (`Dictionary<,>`, `Queue<T>`,
  `List<T>`)
- `using System.Collections.ObjectModel;` (`ReadOnlyCollection<T>`)
- `using System.Linq;` (`Sum`, `ToList`)
- `using System.Text;` (`StringBuilder`)
- Nullable context enabled at file or project level for `?` semantics on
  `Poll` / `Peek` return types.

### 2.8 Concurrency contract (preserve)

- The source is single-threaded; .NET counterparts MUST be the non-concurrent
  `Queue<T>` and `Dictionary<TKey,TValue>`. Silent upgrade to
  `ConcurrentQueue<T>` / `ConcurrentDictionary<TKey,TValue>` is FORBIDDEN —
  would change observable snapshot semantics and add cost. A future
  concurrent caller is an API change, not a silent migration (per convspec
  Notes §Thread-safety, aligned with the heap_fcp single-owning-context
  policy in CLAUDE.md).

## 3. Decomposed Task Units

- **T1 — File scaffold + namespace + usings.** Emit
  `lib/multiagent/message_queue.cs` with the four `using` directives (2.7),
  matching namespace, and file-level doc-comment preserving the Dart library
  header (purpose + spec reference to `/docs/ma/madGLP-spec.md §6.1`).
  *Done when:* file parses, namespace agrees with sibling multiagent types,
  no symbols yet but compiles.
- **T2 — `enum MessageType`.** Emit `enum MessageType { Assignment,
  AgentMessage }` with per-member XML doc-comments mirroring Dart member doc
  comments verbatim.
  *Done when:* enum compiles, both members present, PascalCase, no explicit
  underlying type.
- **T3 — `class OutboundMessage`.** Emit reference class with three get-only
  auto-properties (`Destination`, `Type`, `Payload : List<byte>`), single
  required constructor binding all three, and `override ToString()` per 2.2.
  *Done when:* construction with `new OutboundMessage(...)` compiles,
  `ToString()` returns the documented format.
- **T4 — `class MessageQueue` skeleton + private state + `Add` + `Poll` +
  `Peek` + `CountFor`.** Emit class, private
  `Dictionary<string, Queue<OutboundMessage>> _queuesByDestination`, plus
  the four FIFO core methods per 2.5 (using the TryGetValue idiom in 2.4 for
  `Add`).
  *Done when:* enqueue/dequeue round-trip preserves order and removes empty
  buckets; `Poll`/`Peek` return `null` on missing destination.
- **T5 — Aggregation surface: `Destinations`, `IsEmpty`, `IsNotEmpty`,
  `TotalLength`.** Emit four read-only members per 2.5;
  `Destinations` materialises a snapshot via `Keys.ToList()` (return type
  `IReadOnlyList<string>`); `TotalLength` uses LINQ `Sum(q => q.Count)`.
  *Done when:* mutation after a `Destinations` call does not retroactively
  alter the previously returned list (snapshot contract).
- **T6 — Bulk operations: `Clear`, `ClearFor`, `PeekAll`, `PollAll`.** Emit
  per 2.5; `PeekAll` returns `IReadOnlyList<OutboundMessage>` via
  `AsReadOnly()` over a fresh copy; `PollAll` returns a new `List<T>` and
  removes the bucket.
  *Done when:* mutating the live bucket after `PeekAll` is not observable in
  the returned read-only view (snapshot semantics); `PollAll` empties the
  bucket and the destination disappears from `Destinations`.
- **T7 — `override ToString()` with `StringBuilder`.** Emit per 2.6 including
  the early-return empty branch; preserve the `"  {destination}: {count}
  message(s)"` format verbatim.
  *Done when:* empty queue prints `"MessageQueue: empty"`; populated queue
  prints header line + one line per destination with the documented format.
- **T8 — Doc comments (XML).** Convert every Dart triple-slash doc comment
  on the file, enum, enum members, class, methods, getters, fields to .NET
  XML doc-comment form (`<summary>`, `<param>`, `<returns>`, `<remarks>`),
  preserving wording.
  *Done when:* every public symbol has an XML doc comment whose prose matches
  the Dart source.

## 4. Research Findings

None required. Convspec carries six fully authoritative research findings
(`rf-dart-enum-plain-to-csharp-enum`,
`rf-dart-nullsafety-to-csharp-nrt`, `rf-dart-list-of-bytes-to-dotnet-list-byte`,
`rf-dart-collection-queue-to-csharp-queue`, `rf-dart-iterable-where-to-linq`,
`rf-dart-stringbuffer-to-csharp-stringbuilder`) citing official Dart language
reference and Microsoft Learn pages for `enum`, nullable reference types,
`List<T>`, `Queue<T>`, LINQ, and `StringBuilder`. Zero escalations recorded in
the ratified convspec; this plan inherits that resolution. No new constructs
introduced — every §2 decision derives verbatim from convspec §constructs or
convspec §Notes.

## 5. Consistency Pass

- §2.1 (`MessageType` enum) ↔ convspec construct `enum MessageType
  plain-value-enum two-cases assignment-agentMessage` — identical
  (PascalCase, default `int`, no explicit values). No gap.
- §2.2 (`OutboundMessage` reference class, get-only auto-properties, single
  required ctor, `ToString` override) ↔ convspec construct `class
  OutboundMessage final-fields named-required-ctor toString-override
  List-int-payload` — identical. No gap.
- §2.3 (`List<int> payload` → `List<byte> Payload`) ↔ convspec construct
  `Dart-List-int-byte-payload-to-csharp-List-byte` — identical
  (`List<byte>`, not `byte[]`, not `List<int>`; `Count` not `Length`). No
  gap.
- §2.4 (`Dictionary<string, Queue<OutboundMessage>>`, FIFO `Queue<T>`,
  TryGetValue/create/Add for `putIfAbsent`) ↔ convspec construct
  `Map-String-Queue-FIFO-per-destination ... putIfAbsent removeFirst clear`
  — identical (non-concurrent FIFO container; TryGetValue idiom for
  putIfAbsent; ReadOnlyCollection wrap for unmodifiable). No gap.
- §2.5 per-method mapping ↔ convspec `conversion_units` list — every Dart
  method/getter present, return types match (`OutboundMessage?` for
  Poll/Peek; `IReadOnlyList<string>` for Destinations; `IReadOnlyList<...>`
  for PeekAll; `List<...>` for PollAll). No gap.
- §2.6 (`StringBuilder` + `AppendLine` + documented `\n` vs
  `Environment.NewLine` nuance) ↔ convspec construct `StringBuffer
  accumulation in toString writeln per-destination listing` + nuance
  paragraph — identical, platform-newline nuance preserved per FR-009. No
  gap.
- §2.8 (preserve single-threaded contract; no `ConcurrentQueue<T>` /
  `ConcurrentDictionary`) ↔ convspec Notes §Thread-safety AND CLAUDE.md
  heap_fcp single-owning-context project policy (escalation #4, commit
  `497428c8`) — aligned. No gap.
- §3 task units ↔ §2 + convspec `conversion_units` — T1 covers usings/file
  scaffold, T2 enum, T3 OutboundMessage, T4 core FIFO surface, T5
  aggregation surface, T6 bulk operations, T7 ToString, T8 doc comments —
  every conversion unit in the convspec is reached by exactly one task. No
  gap.
- §4 ↔ convspec `escalations: []` — zero open research items; carry-over
  posture confirmed. No gap.
- Singleton (cycle_group_id 27, scc_siblings `[]`) — no §7 emitted, per
  workflow. No gap.

## 6. Escalations

None.
