> Conversion-spec artifact for lib/multiagent/message_queue.dart (FR-011).
> Spec-only (FR-023): describes the Dart->C# conversion; contains NO
> compilable C#. A later codegen stage consumes the structured block.

```yaml
schema_version: 1
source_path: lib/multiagent/message_queue.dart
source_sha256: 619550d11a54842ba4948fd5cd8ac86d742fbaddf26fcd138ddc39c26da696d7
target_code_unit: lib/multiagent/message_queue.cs
constructs:
  - construct_key: "enum MessageType plain-value-enum two-cases assignment-agentMessage"
    source_form: "enum MessageType { assignment, agentMessage; } a closed set of named constants used as a tag on OutboundMessage; no associated state, no methods, ordinal values are not relied upon."
    target_decision: "C# enum MessageType with two members Assignment and AgentMessage (PascalCase per .NET naming convention). Default underlying type int; no explicit values needed because no code observes ordinal or numeric value. Lives in the same target namespace as OutboundMessage and MessageQueue."
    idiom_id: null
    research_finding_id: rf-dart-enum-plain-to-csharp-enum
    nuance: "Dart 'plain' enum (no enhanced-enum members/methods) is a closed tag-only type; C# enum is the faithful counterpart. Naming nuance: Dart camelCase members (assignment, agentMessage) become PascalCase (Assignment, AgentMessage) per the official .NET enum-naming guideline; the tag set is unchanged. Value-vs-reference: enums are value types in both languages, matching semantics."
  - construct_key: "class OutboundMessage final-fields named-required-ctor toString-override List-int-payload"
    source_form: "class OutboundMessage with three final fields (final String destination, final MessageType type, final List<int> payload), a named-with-required constructor (OutboundMessage({required this.destination, required this.type, required this.payload})), and an @override String toString() embedding destination, type and payload.length."
    target_decision: "Reference-type C# class (NOT struct/record-struct): instances are stored by reference inside Queue<OutboundMessage> and Map<String, Queue<OutboundMessage>>; copying value semantics would change MessageQueue ownership/identity behaviour. final instance fields become get-only auto-properties initialised from the constructor (immutability of the field/reference preserved, not of payload's contents). The named-required constructor becomes a single C# constructor whose parameters are all must-be-supplied (Dart 'required' on a named arg is a compile-time obligation, semantically just non-optional). toString() override becomes override of object.ToString() using string interpolation with payload.Count (the .NET List<T> length property)."
    idiom_id: null
    research_finding_id: rf-dart-nullsafety-to-csharp-nrt
    nuance: "Null-safety mapping: all three fields are non-nullable (no '?' in the Dart declarations) -> non-nullable C# types under an enabled nullable context (string, MessageType, List<byte>). Value-vs-reference: stays a reference class because the type is held by reference in the queue map and identity matters for the per-destination FIFO; final field immutability -> get-only property, not a writable field. payload.length in Dart is on List<int>; the matching .NET API is List<T>.Count, a documented length-property naming difference."
  - construct_key: "Dart-List-int-byte-payload-to-csharp-List-byte"
    source_form: "final List<int> payload; — described in the doc comment as 'Serialized payload (opaque bytes)'. Dart int is a 64-bit signed integer; the field is treated semantically as an opaque byte sequence."
    target_decision: "Map List<int> payload to List<byte> Payload (8-bit unsigned byte) — NOT List<int> — because the doc comment makes the byte-stream intent explicit and a 1:1 .NET int (Int32) list would quadruple the memory footprint and misrepresent the wire-level semantics. Construct/consume sites assign and read whole lists; no element arithmetic depends on width >8 bits in this file. The target property is still a reference to a mutable List<byte> (matches Dart final-reference-to-mutable-list semantics)."
    idiom_id: null
    research_finding_id: rf-dart-list-of-bytes-to-dotnet-list-byte
    nuance: "Width nuance: Dart has no fixed-width byte type; List<int> is the idiomatic byte buffer (the typed-data Uint8List alternative is not used here). .NET's canonical byte buffer is byte (System.Byte) — either List<byte> or byte[]; we pick List<byte> to preserve Dart List semantics (Add/length/index). Value-vs-reference: the LIST is reference, the bytes inside are value, identical to Dart. payload.length -> Count (Dart Iterable.length vs .NET ICollection<T>.Count) — covered by the iterable/collection idiom."
  - construct_key: "Map-String-Queue-FIFO-per-destination _queuesByDestination putIfAbsent removeFirst clear"
    source_form: "final Map<String, Queue<OutboundMessage>> _queuesByDestination = {}; with operations putIfAbsent(key, () => Queue<OutboundMessage>()), queue.add (enqueue), queue.removeFirst (dequeue / FIFO head), queue.first (peek), queue.isEmpty, queue.length, and full-list materialisation via List.from(queue) / List.unmodifiable(queue). Imported from dart:collection; the FIFO contract is the load-bearing property (per the class doc comment 'FIFO ordering per destination')."
    target_decision: "Map<String, Queue<OutboundMessage>> -> Dictionary<string, Queue<OutboundMessage>>; the inner Dart dart:collection Queue maps to System.Collections.Generic.Queue<T>. Operation mapping (FIFO-preserving): queue.add(x) -> Enqueue(x); queue.removeFirst() -> Dequeue(); queue.first -> Peek(); queue.isEmpty -> Count == 0; queue.length -> Count; new Queue<OutboundMessage>() default ctor. putIfAbsent(k, factory) has no exact .NET equivalent — codegen emits the established TryGetValue-or-create-and-Add idiom (semantic equivalent: lookup, create-and-insert on miss, return the inner queue) inside the Add method. List.from(queue) -> ToList() (returns a NEW List snapshot); List.unmodifiable(queue) -> ReadOnlyCollection<OutboundMessage> (wrap via AsReadOnly() over a List copy) so the caller cannot mutate the live queue (matches Dart's unmodifiable-view contract)."
    idiom_id: null
    research_finding_id: rf-dart-collection-queue-to-csharp-queue
    nuance: "FIFO contract nuance: Dart's dart:collection Queue is a double-ended deque; HERE only the FIFO operations (add tail / removeFirst head / first head) are used, so the natural C# counterpart is the FIFO-only System.Collections.Generic.Queue<T> (Enqueue/Dequeue/Peek), NOT LinkedList<T> (deque) and NOT ConcurrentQueue<T> (thread-safe). Thread-safety nuance: this Dart class is single-threaded (no locks, no mutex, no isolate); we must NOT silently upgrade to ConcurrentQueue/ConcurrentDictionary, which would change observable behaviour (e.g. snapshot semantics) and add cost — we preserve the single-threaded contract. Unmodifiable view nuance: Dart List.unmodifiable returns a snapshot wrapper that throws on mutation; ReadOnlyCollection<T> is the matching .NET wrapper (read-only view over a list copy), not an IReadOnlyList<T> alias (which would not block downcasts)."
  - construct_key: "Iterable-fold-sum-keys-toList-isEmpty-isNotEmpty (read-only aggregation)"
    source_form: ".keys.toList(); .values.fold(0, (sum, queue) => sum + queue.length); isEmpty/isNotEmpty getters delegating to map.isEmpty/isNotEmpty. Aggregations are eager and produce concrete List<String>/int results."
    target_decision: ".keys.toList() -> Keys.ToList() (eager List<string>). .values.fold(0, (s,q) => s + q.length) -> Values.Sum(q => q.Count) (LINQ Sum is the canonical fold-to-int for collections; semantically equivalent to the fold). map.isEmpty/isNotEmpty -> Count == 0 / Count > 0 on the Dictionary (Dictionary<TKey,TValue> exposes Count, not IsEmpty)."
    idiom_id: null
    research_finding_id: rf-dart-iterable-where-to-linq
    nuance: "Eager-vs-lazy: each call site terminates (toList / fold / isEmpty) so the LINQ counterparts (ToList, Sum) which are eager terminal operators preserve evaluation timing. Public destinations getter must return a NEW List<string> (Keys.ToList()) — returning a lazy IEnumerable or the live KeyCollection would change the snapshot contract (mutation through clearFor would be visible after the call). Sum vs Aggregate nuance: Sum(selector) is the documented .NET equivalent of the int-folding pattern; using Aggregate with a seed would also be correct but less idiomatic."
  - construct_key: "StringBuffer accumulation in toString writeln per-destination listing"
    source_form: "final buffer = StringBuffer('MessageQueue:\\n'); for (final destination in destinations) { ... buffer.writeln('  $destination: $count message(s)'); } return buffer.toString();"
    target_decision: "Dart StringBuffer -> .NET System.Text.StringBuilder. Constructor with initial string -> new StringBuilder(\"MessageQueue:\\n\"). writeln(x) -> AppendLine(x). Final buffer.toString() -> ToString(). The early-return 'MessageQueue: empty' branch stays as a plain string literal (no buffer needed)."
    idiom_id: null
    research_finding_id: rf-dart-stringbuffer-to-csharp-stringbuilder
    nuance: "Both Dart String and .NET string are immutable; the buffer type exists precisely to avoid quadratic concatenation. writeln/AppendLine newline difference: Dart writeln appends '\\n'; .NET AppendLine appends Environment.NewLine (a platform-dependent line break, '\\r\\n' on Windows). For this diagnostic toString() the line break is semantically a line separator, not a wire-format byte — the platform difference is acceptable AND recorded here as a known nuance per FR-009 (must be explicitly addressed, not glossed)."
conversion_units:
  - "enum MessageType (Assignment, AgentMessage)"
  - "class OutboundMessage (get-only Destination/Type/Payload properties, single ctor, ToString override)"
  - "class MessageQueue (private Dictionary<string, Queue<OutboundMessage>> _queuesByDestination)"
  - "  - Add(OutboundMessage) [Dart add] — TryGetValue/create/Enqueue"
  - "  - Poll(string destination) -> OutboundMessage? [Dart poll] — Dequeue + empty-bucket cleanup"
  - "  - Peek(string destination) -> OutboundMessage? [Dart peek] — Peek without mutation"
  - "  - CountFor(string destination) -> int [Dart countFor] — Count or 0"
  - "  - Destinations get -> IReadOnlyList<string> [Dart destinations] — Keys.ToList() snapshot"
  - "  - IsEmpty get -> bool [Dart isEmpty] — Count == 0 on the Dictionary"
  - "  - IsNotEmpty get -> bool [Dart isNotEmpty] — Count > 0 on the Dictionary"
  - "  - TotalLength get -> int [Dart totalLength] — Values.Sum(q => q.Count)"
  - "  - Clear() [Dart clear] — Dictionary.Clear()"
  - "  - ClearFor(string destination) [Dart clearFor] — Dictionary.Remove(key)"
  - "  - PeekAll(string destination) -> IReadOnlyList<OutboundMessage> [Dart peekAll] — new List + AsReadOnly snapshot"
  - "  - PollAll(string destination) -> List<OutboundMessage> [Dart pollAll] — copy + Remove the bucket"
  - "  - ToString override — StringBuilder per-destination listing"
escalations: []
```

## Rationale and research provenance (per non-trivial construct)

### rf-dart-enum-plain-to-csharp-enum — plain enum mapping

- Deep analysis: MessageType has two members, no constructor, no instance
  members, no methods, no associated values. The only uses observed are
  `final MessageType type` and string interpolation `type=$type` inside
  toString(). It is a textbook plain (non-enhanced) Dart enum.
- Authoritative Dart: official Dart language reference at
  https://dart.dev/language/enums describes the plain `enum` form as a
  closed, fixed set of constant values; enhanced enums add fields/methods,
  which this enum does NOT have.
- Authoritative .NET: official C# reference at
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum
  defines `enum` as a value type with a set of named constants, default
  underlying type `int` — a structurally exact target for the plain Dart
  form. .NET naming guidelines
  (https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-classes-structs-and-interfaces#names-of-enumerations)
  prescribe PascalCase enum members, hence `Assignment` / `AgentMessage`.
- Authoritative both sides; no escalation.

### rf-dart-nullsafety-to-csharp-nrt — null-safety mapping (carry-over)

- Deep analysis: every field on `OutboundMessage` is declared without `?`
  (non-nullable). `poll`/`peek` return `OutboundMessage?` (nullable
  reference) when the per-destination bucket is empty/missing.
- Authoritative .NET: official Microsoft Learn doc on nullable reference
  types (https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references)
  defines `T?` as the nullable reference form under an enabled nullable
  context, and the bare reference type as non-nullable. Therefore:
  non-nullable fields -> `string`, `MessageType`, `List<byte>`; nullable
  return -> `OutboundMessage?`.
- Authoritative; no escalation.

### rf-dart-list-of-bytes-to-dotnet-list-byte — byte-buffer mapping

- Deep analysis: the field's doc comment says "Serialized payload (opaque
  bytes)"; the only operation in this file is `payload.length`. The
  semantic intent is a byte sequence; the Dart type `List<int>` is the
  idiomatic byte buffer in non-typed-data Dart code (no `Uint8List`
  imported here).
- Authoritative .NET: the .NET runtime exposes `byte` (System.Byte,
  unsigned 8-bit) as the canonical byte type per
  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types.
  Either `byte[]` or `List<byte>` is a faithful target; `List<byte>` is
  chosen here to preserve Dart `List` semantics (Add, indexer, Count).
  Naming nuance: Dart `length` becomes `Count` on `List<T>` per the
  generic-list reference
  (https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1).
- Authoritative; no escalation. The doc-comment "opaque bytes" is the
  decisive signal — without it we would have escalated `List<int>` as
  ambiguous (bytes vs. wide ints).

### rf-dart-collection-queue-to-csharp-queue — FIFO container mapping

- Deep analysis: imported from `dart:collection`. Operations USED are
  `add` (tail-enqueue), `removeFirst` (head-dequeue), `first` (head-peek),
  `isEmpty`, `length`, and snapshot constructors `List.from(queue)` /
  `List.unmodifiable(queue)`. These are ALL FIFO operations — no tail
  removal, no head insertion, no deque use. The class is single-threaded:
  no isolates, no locks, no Future/Stream/async. The Dart `Queue` is in
  fact a deque (DoubleLinkedQueue / ListQueue), but only its FIFO subset
  is exercised.
- Authoritative .NET:
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1
  defines `Queue<T>` as a first-in/first-out collection with `Enqueue`,
  `Dequeue`, `Peek`, `Count`, `Clear` — an exact match for the FIFO
  subset actually used. `LinkedList<T>` is the .NET deque (rejected:
  unnecessary capability + larger surface). `ConcurrentQueue<T>` is
  thread-safe (rejected: would silently change concurrency semantics; no
  evidence the source class is shared across threads).
- `putIfAbsent(k, factory)` on `Map`: no single-call .NET equivalent on
  `Dictionary<TKey,TValue>`; the documented idiom is `TryGetValue` +
  conditional construction + `Add` (or, since .NET 6,
  `CollectionsMarshal.GetValueRefOrAddDefault` — rejected as a
  micro-optimisation outside scope). The codegen emits the
  TryGetValue/create/Add form inline in `Add(OutboundMessage)`.
- `List.unmodifiable(queue)` returns a snapshot view that throws on
  mutation; the .NET counterpart is `ReadOnlyCollection<T>` over a `List`
  copy (`List<T>.AsReadOnly()`), per
  https://learn.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.readonlycollection-1.
  Returning a bare `IReadOnlyList<T>` would not block a downcast +
  mutate; `ReadOnlyCollection<T>` does.
- Authoritative; no escalation.

### rf-dart-iterable-where-to-linq — read-only aggregation (carry-over)

- Deep analysis: `destinations` getter calls `.keys.toList()` — eager
  snapshot; `totalLength` calls `.values.fold(0, (s,q) => s + q.length)`
  — eager terminal fold; `isEmpty`/`isNotEmpty` delegate to the
  underlying map's getters. Every aggregation is eager.
- Authoritative .NET: official LINQ reference
  (https://learn.microsoft.com/en-us/dotnet/api/system.linq.enumerable.sum,
  /tolist, /count) confirms `ToList`, `Sum`, `Count` are eager terminal
  operators that consume the source — equivalent timing to Dart `toList`
  / `fold` / collection-length getters. Therefore: `Keys.ToList()`,
  `Values.Sum(q => q.Count)`, `Count == 0` / `Count > 0`. Public
  `Destinations` returns the materialised `List<string>` (not a lazy
  `IEnumerable<string>`) to preserve snapshot semantics — a later
  `ClearFor`/`Add` must NOT mutate a previously returned destinations
  list.
- Authoritative; no escalation.

### rf-dart-stringbuffer-to-csharp-stringbuilder — mutable buffer (carry-over)

- Deep analysis: `toString()` builds a multi-line listing via
  `StringBuffer` + `writeln` then materialises with `toString()`. Exactly
  the same role as in `analysis_phase.dart`.
- Authoritative .NET:
  https://learn.microsoft.com/en-us/dotnet/api/system.text.stringbuilder
  defines `StringBuilder` as the canonical mutable string buffer; both
  Dart `String` and .NET `string` are immutable, which is precisely why
  a separate builder type exists. `AppendLine` is the documented
  newline-appending counterpart of `writeln`; the newline difference
  (`\n` vs `Environment.NewLine`) is recorded as a documented nuance —
  semantically a line separator in both, but a known platform
  variability point.
- Authoritative; no escalation.

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Stream / StreamController / IAsyncEnumerable**: ABSENT from this file.
  `MessageQueue` is a synchronous in-memory data structure; there is no
  publish/subscribe surface, no async producer/consumer, no broadcast vs
  single-subscription decision to make. The conversion deliberately does
  NOT introduce `System.Threading.Channels.Channel<T>`,
  `IAsyncEnumerable<T>`, or `IObservable<T>` — doing so would invent
  async semantics the source does not have (FR-013: escalate-don't-guess,
  but here the situation is unambiguous — pure synchronous code — so no
  escalation either; we simply preserve the synchronous contract).
- **Future / Completer / Task / TaskCompletionSource**: ABSENT. No
  `async`, no `await`, no `Future`, no `Completer`. All methods are
  synchronous.
- **Back-pressure / bounded vs. unbounded**: NOT APPLICABLE. The queue is
  in-memory and unbounded by construction (a `Queue<T>` grows as
  enqueued); there is no producer/consumer flow-control protocol in the
  source. Codegen MUST NOT silently introduce a `BoundedChannelOptions`
  capacity — that would change semantics.
- **Single-subscription vs. broadcast**: NOT APPLICABLE — there is no
  subscription surface at all. Consumers explicitly pull via
  `poll`/`pollAll`/`peek`/`peekAll`. The map-of-Queue structure already
  encodes per-destination single-consumer FIFO; nothing to fan out.
- **Thread-safety**: the source is single-threaded (no locks, no isolate
  use). Codegen MUST use the non-concurrent `Queue<T>` and
  `Dictionary<TKey,TValue>`. If a future caller needs concurrency, that
  is a deliberate API change to escalate, not a silent upgrade.
- **`putIfAbsent`**: there is no exact .NET equivalent on `Dictionary`
  before .NET 6's `CollectionsMarshal.GetValueRefOrAddDefault`; codegen
  emits the documented TryGetValue + create + Add idiom (semantic
  equivalent, no surface change). Recorded here so the codegen stage
  does not re-derive.
- **Identifier casing**: Dart camelCase method/field names (`add`,
  `poll`, `peek`, `countFor`, `peekAll`, `pollAll`, `clearFor`,
  `isEmpty`, `isNotEmpty`, `totalLength`, `destinations`,
  `_queuesByDestination`) become PascalCase public members and
  underscore-prefixed camelCase private field per .NET naming
  conventions
  (https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/capitalization-conventions);
  the source ordering, parameter names, doc-comment text, and FIFO
  contract are preserved.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or .NET official documentation.
