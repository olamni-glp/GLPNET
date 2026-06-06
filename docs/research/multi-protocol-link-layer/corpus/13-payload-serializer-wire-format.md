---
title: "PayloadSerializer Wire Format — concrete byte layout for terms crossing madGLP instance boundaries (functors, constants, lists, global names, reader/writer polarity)"
authors: "glpnet madGLP implementation (payload_serializer.dart); madGLP-spec.md (Claude/Gabi); isolate-boot-spec.md. Conceptual basis: E. Shapiro et al. (GLP / FCP)."
year: "2026"
source_url: "D:/bstdev/research/glp/glpnet/glp_runtime/lib/multiagent/payload_serializer.dart ; docs/ma/madGLP-spec.md §8.2,§11.4 ; docs/ma/isolate-boot-spec.md §7.2"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Exact wire/serialization format for terms crossing instance boundaries: how are functors, constants, lists, global names, and (critically) reader-vs-writer polarity encoded by PayloadSerializer, and is it transport-agnostic enough to drop onto MQTT/CoAP/BLE GATT framing?"
precedence_class: "glp-current"
access: "full-text"
---

# PayloadSerializer Wire Format — terms crossing madGLP instance boundaries

## Precedence note

This is a **glp-current** answer. The concrete byte layout is defined ONLY by the live
implementation (`glp_runtime/lib/multiagent/payload_serializer.dart`) and the madGLP spec
(`docs/ma/madGLP-spec.md`). The GLP paper (Shapiro et al., *GLP: A Grassroots, Multiagent,
Concurrent, Logic Programming Language*, arXiv:2510.15747) and the earlier FCP/distributed-
unification literature (e.g. "Distributed variable server for atomic unification", PODC 1990;
the FCP-on-multi-transputer impl) specify the **mechanism** — globalize/localize, global
variable names, read-only (reader) annotations, atomic distributed unification — but **do not
specify a concrete wire/byte format**. Per SOURCE PRECEDENCE the local impl+spec are authoritative
here and there is no higher-precedence published format to conflict with.

madGLP-spec §11.4 (verbatim) is the only normative statement of *what the format must preserve*;
it deliberately leaves the byte layout to the implementation:

> ### 11.4 Serialization
> Terms crossing agent boundaries are serialized with global names substituted for variables.
> The serialization format must preserve:
> - Functor/arity structure
> - Global name encoding: type tag + agent identifier + index
> - Constants: type tag + value bytes

The concrete realization of those three requirements is `payload_serializer.dart`.

---

## 1. Direct answer

The wire format is a **hand-rolled, length-prefixed, big-endian, recursive TLV (tag-length-value)
binary encoding** with no external framing, no transport assumptions, and no schema/handshake.
Polarity (reader vs writer) is carried **explicitly as a 1-byte flag per variable**, and global
identity is carried as a UTF-8 `creator:localId` (or `_w/_r` agent+index) string. It is fully
**self-delimiting** (every field is either fixed-width or length-prefixed), which is exactly what
makes it transport-agnostic: a serialized term/assignment is an opaque `List<int>`/`Uint8List`
blob that any transport can carry as a single payload. It is NOT inherently fragmentation-aware,
so over MTU-limited transports (CoAP, BLE GATT) it must be wrapped in a segmentation layer (see §6).

---

## 2. Term-level encoding (the recursive core)

`_serializeTermRecursiveV2` / `_deserializeTermWithMappingV2`. Each term node begins with a 1-byte
**term tag**:

| Tag constant   | Byte | Term kind            |
|----------------|------|----------------------|
| `_tagConstant` | 1    | `ConstTerm`          |
| `_tagVariable` | 2    | `VarRef` (logic var) |
| `_tagStruct`   | 3    | `StructTerm`         |
| `_tagList`     | 4    | declared, **unused** in V2 (lists are structs `'.'`/2) |

### 2.1 Functors / arity / structs (`_tagStruct`, byte 3)

```
0x03 | <len functor-bytes> | functor (UTF-8) | <len arity> | arg_0 | arg_1 | ... | arg_{arity-1}
```
- `functor`: length-prefixed UTF-8 string.
- arity: encoded with the same variable-length length codec as everything else (§5).
- args: each a full recursive term (any tag), in order.

### 2.2 Lists

There is **no dedicated list tag on the wire**. Lists are ordinary binary structs with functor
`'.'` and arity 2 (`StructTerm('.', [head, tail])`); `nil`/`[]` is a constant (see below). So
`[a|b]` serializes as a `_tagStruct` `'.'/2`. The `_tagList` constant exists in the source but is
not emitted by `_serializeTermRecursiveV2`. **Implication for the link layer:** there is no special
list framing to preserve — list spines are just nested 2-arity structs and recurse like any term.

### 2.3 Constants (`_tagConstant`, byte 1) — `_serializeConstant`

After the `0x01` term tag comes a 1-byte **constant type tag**, then the value:

| Const type byte | Type   | Value bytes                                                |
|-----------------|--------|-----------------------------------------------------------|
| 0               | nil    | (none) — `ConstTerm('nil')`, i.e. the empty list `[]`     |
| 1               | int    | 8 bytes, `Int64`, **big-endian**                          |
| 2               | double | 8 bytes, `Float64`, **big-endian**                        |
| 3               | string | `<len> <UTF-8 bytes>`                                      |
| 4               | bool   | 1 byte (0/1)                                              |

Atoms are carried as the string case (type 3). `nil` is special-cased so the empty list is 2 bytes
total (`0x01 0x00`).

### 2.4 Variables and POLARITY (`_tagVariable`, byte 2) — the load-bearing part

```
0x02 | <len gid-bytes> | gid (UTF-8 "creator:localId") | isReader(1 byte: 1=reader,0=writer)
      | [ if writer: <len pairedReaderLocalId> ]
```

- **Global identity**: `GlobalVarId(creator, localId).encode()` → the ASCII string
  `"<creator>:<localId>"` (e.g. `"bob:1117"`), length-prefixed UTF-8. For **imported** variables
  the `lookupVariable` callback supplies the *original* creator + creatorLocalId (madGLP §8.1: an
  imported variable keeps the original creator's id, never the relaying agent's), so identity is
  stable across multi-hop forwarding. For local variables `creator = agentId`, `creatorLocalId = addr`.
- **Polarity flag** (verbatim source): after the gid the serializer writes
  ```dart
  // Store isReader flag
  builder.addByte(isReaderVar ? 1 : 0);
  ```
  This single byte is the entire reader-vs-writer encoding for a variable on the wire.
- **Writer carries its paired reader id** (verbatim source comment + code):
  ```dart
  // For writers, also serialize the paired reader's creatorLocalId
  // Per spec Section 5.3: assignments are addressed to the reader (W?:=T)
  if (!isReaderVar) {
    // For local variables, paired reader is at writer+1
    final pairedReaderLocalId = creatorLocalId + 1;
    builder.add(_encodeLength(pairedReaderLocalId));
  }
  ```
  So a **writer** node is `tag|len|gid|0x00|<pairedReaderLocalId>` and a **reader** node is
  `tag|len|gid|0x01` (no trailing field). The asymmetry is the wire-level expression of the FCP
  writer/reader cell pairing: a transmitted writer announces where its reader partner lives so the
  receiver can address the eventual assignment `W? := T` to the correct reader. On deserialize,
  `allocateImportedVar(isReader)` allocates a **single** cell of the correct polarity (not a full
  pair — heap_fcp's `allocateImportedReader` / `allocateImportedWriter`), and `onVariableImported`
  attaches the `VariableEntry`/global-writers-table routing with `pairedReaderCreatorLocalId`.
- **Variable sharing within one message** is preserved by `varMapping`/`globalToLocal`: the first
  occurrence of a gid allocates a fresh local cell; later occurrences of the same gid map to the
  same local cell. So `[X, X?]` keeps the two ends correlated across the boundary.

This is the critical fidelity point for B2: **polarity is first-class on the wire** (1 byte) and the
writer↔reader pairing is transmitted (pairedReaderLocalId), so the distributed model can reconstruct
the one-writer/one-reader atomic pair on the far side and route the binding back. SRSW is not
re-checked on the wire — it is a clause-level static property; the wire just preserves identity+polarity.

---

## 3. Global-name framing (the link/assignment envelopes)

Three payload builders wrap a serialized term with a routing header. All use the same recursive
term codec for the body.

### 3.1 `createGlobalSendPayload(GlobalName, value, ...)` — established-link assignment
```
isWriter(1 byte: 0=writer,1=reader)   // NOTE: inverted convention vs the per-variable flag
| <len agent-bytes> | agent (UTF-8)
| <len index>                          // variable-length int
| <serialized term>
```
`GlobalName` is `_w(agent,index)` or `_r(agent,index)` (madGLP-spec §2). This is the exact "type tag
+ agent identifier + index" that §11.4 mandates for global-name encoding. Parsed by
`deserializeGlobalSendPayload` → `GlobalName.writer/reader(agent,index)` + term.

> Polarity-byte convention is **not uniform** between the two layers: in the per-variable term
> encoding (§2.4) `1 == reader`; in the GlobalName header (§3.1/§3.2) `0 == writer`/`1 == reader`
> (`globalName.isWriter ? 0 : 1`). Same meaning for reader (1), opposite numeric for writer. A
> link-layer reimplementation must keep both conventions or normalize them — flagged as a latent
> footgun.

### 3.2 `createSerializerPayload(serializerName, content, ...)` — cold-call (index-0 serializer)
Asserts `serializerName.isWriter && index == 0`. Header is `0x00 | <len agent> | agent | <len 0>`,
then the body is the **list cell** `[content | _w(q,0)]` built by `_buildSerializerListCell`:
the tail is encoded as a sentinel `ConstTerm('#serializer:<agent>:0')` (a string constant the
receiver recognizes as "reuse the serializer writer in the tail"). Implements madGLP §4.1/§8.3:
`_w(q,0) := [T↑ | _w(q,0)]`.

### 3.3 `createAssignmentPayloadV2(varId, value, ...)` — direct assignment by var id
```
<len gid-bytes> | gid (UTF-8 "agentId:varId") | <serialized term>
```

### 3.4 Other small payloads
- `createReadRequestPayload`: `<len gid> gid | <len req> requester`.
- `createAbandonPayload`: `<len gid> gid` (writer being abandoned).
- `createAgentMessagePayload` / `serializeAgentMessage`: just the bare serialized term (latter
  asserts ground — throws on any VarRef; used for UI-event injection).

---

## 4. Message-level framing (`serializeMessage` / `OutboundMessage`)
```
type(1 byte = MessageType.index) | <len dest-bytes> | dest (UTF-8) | <len payload> | payload
```
This is the outermost glpnet envelope: a message-type discriminator, a routable **destination
agent id** in the header (madGLP §8.2: "the transmitted message includes the destination agent q
in the header, enabling the communication infrastructure to route the message correctly"), and the
length-prefixed payload body. Note the *transport* (Dart isolates today) actually carries the
structured `NetworkMsg` object, not this byte envelope — see §6.

---

## 5. Length / integer codec (`_encodeLength` / `_decodeLength`)
A self-describing variable-length unsigned integer keyed on the top bits of the first byte:

| First byte range | Layout            | Range                | Bytes |
|------------------|-------------------|----------------------|-------|
| `0x00–0x7F`      | `0vvvvvvv`        | 0 … 127              | 1     |
| `0x80–0xBF`      | `10vvvvvv vvvvvvvv` | 128 … 16 383       | 2     |
| `0xC0–0xFF`      | `11vvvvvv` + 3 bytes | up to ~2^30      | 4     |

Big-endian within each form. Used for every length field AND for `index`, arity, and
`pairedReaderLocalId`. 64-bit ints/doubles use fixed `Int64`/`Float64` **big-endian** (`Endian.big`),
so the format is endian-independent on the wire.

---

## 6. Transport-agnosticism assessment (the actual question)

**Self-contained?** Yes. Every value is fixed-width or length-prefixed; tags are 1 byte; there is
no reliance on stream position, external schema, or a handshake. A serialized assignment is an
opaque blob (`List<int>`). This is the *good* property for dropping onto arbitrary transports.

**Already transport-agnostic in design, NOT in deployment.** The current runtime does **not** push
these bytes over any network: `IsolateManager` routes a Dart `NetworkMsg` object
(`isolate-boot-spec.md §7.2`) carrying `payload: List<int>` plus *out-of-band* routing fields
`from`, `to`, `MessageType type`, and optional `globalNameAgent / globalNameIndex / globalNameIsWriter`.
So today the **global-name routing info is duplicated as structured object fields** alongside the
serialized payload, and inter-isolate transfer relies on Dart's SendPort object copy — the
byte-level envelopes of §3/§4 are partly bypassed. A real link layer (MQTT/CoAP/BLE/...) must
serialize that header itself; §3.1/§4 already define a byte form for it, so the gap is wiring, not
format design.

**Per-transport fit (for the splitter):**
- **MQTT / AMQP 1.0 / WebSocket / HTTP-family / DDS / XMPP / SSH/FTP/SFTP/file**: large or unbounded
  payloads — drop the blob in as one message body; self-delimiting framing means no extra work.
  (Open question T1: MQTT/XMPP are broker-mediated; the format itself is broker-neutral, but strict
  bilateral p2p is a *transport-topology* concern, not a serialization concern.)
- **CoAP**: UDP, small MTU (~1024–1152 B safe; block-wise transfer ~ up to 64 KiB/1 MiB). A single
  term can exceed a datagram → needs CoAP Block1/Block2 or app-level chunking. The format gives no
  internal chunk boundaries, but because it is self-delimiting the receiver can reassemble then parse.
- **BLE GATT**: ATT MTU default 23 B (≈20 B usable), negotiable to ~244–512 B. Any non-trivial term
  must be fragmented across multiple writes/notifications with an app-level reassembly header (length
  + sequence). The wire format does not provide this; a thin segmentation sublayer is required.
- **BLE L2CAP CoC**: SDU up to 65 535 B with credit flow control — a whole assignment usually fits as
  one SDU; good fit.
- **TLS variants**: orthogonal — the blob is transport-payload, encryption wraps it.

**Robustness gaps to flag (do NOT silently "harden" — these are format-design decisions for Gabi):**
1. No version/magic byte → no forward-compat negotiation across instances running different builds.
2. No length/CRC on the *outermost* unit when handed to a raw byte transport (the Dart layer relied on
   object integrity). MTU-fragmenting transports need their own framing+integrity.
3. The two polarity-byte conventions (§3.1 note) differ between layers.
4. The serializer-tail sentinel is a string-constant in-band (`'#serializer:...'`) — collides if a
   user term ever legitimately contains that atom.
5. `_tagList` is reserved but unused — dead tag.

None of these block B2 fidelity: identity + polarity + writer/reader pairing + struct/arity + shared-
variable correlation are all preserved. They are framing/operational concerns for the multi-protocol
link-layer feature to specify on top.

---

## 7. Mapping to the FCP/GLP model (B2 fidelity yardstick)

- **Writer/reader cell pair** (heap_fcp: WrtTag / RoTag, reader points at writer): preserved as the
  per-variable `isReader` byte + the writer's `pairedReaderLocalId`. The receiver re-creates a single
  cell of the right polarity and records the cross-instance routing in the global-writers table —
  matching madGLP §11.3: *"No special representation is needed for 'imported' variables—all variables
  are local pairs. The global writers table provides routing information separately from the heap
  representation."*
- **Global names `_w(p,i)` / `_r(p,i)`** (madGLP §2): the §3.1 header = "type + agent + index", i.e.
  exactly the routing key Receive (§8.3) uses (`_w(p,i)` → entry i in W_p; `_r(p,i)` → match remote
  (p,i) in the global writers table).
- **Distributed unification / monotonic binding flow**: the format carries `W? := T` assignments
  (writer announces its reader id so the assignment is addressed to the reader); the actual writer-MGU
  / suspension / reactivation happens on Localize at the receiver (madGLP §8.3), not on the wire.
  The wire format is therefore necessary-and-sufficient *input* to local distributed unification — it
  does not itself perform or constrain three-valued unification.

---

## 8. Extracted load-bearing facts (for the link-layer spec)
1. Recursive TLV; 1-byte term tags 1=const, 2=var, 3=struct (4=list reserved/unused).
2. Lists = `'.'/2` structs; `[]` = const-nil (const-type 0).
3. Constants: 1-byte subtype (0 nil,1 int64-BE,2 float64-BE,3 len+UTF8,4 bool). Atoms = strings.
4. Variable = tag + len+UTF8 `creator:localId` + 1-byte isReader; **writers additionally append the
   paired reader's localId** (varint). Reader has no trailing field.
5. Per-variable polarity byte: 1=reader, 0=writer. GlobalName-header polarity byte: 0=writer, 1=reader
   (inverted — latent inconsistency).
6. GlobalName framing: polarity byte + len+UTF8 agent + varint index + term.
7. Cold-call: index-0 serializer writer, body `[content | #serializer:<agent>:0]` sentinel tail.
8. Length/index/arity codec: 1/2/4-byte self-describing varint (top-bit-keyed), big-endian.
9. Message envelope: 1-byte MessageType + len+dest + len+payload; destination agent in header for routing.
10. Self-delimiting and schema-free ⇒ transport-agnostic blob; but NO version byte, NO outer
    integrity/CRC, NO built-in fragmentation ⇒ MTU-limited transports (CoAP, BLE GATT) need an added
    segmentation+integrity sublayer; L2CAP-CoC / large-payload transports fit directly.
11. Today the runtime ships a Dart `NetworkMsg` *object* (payload List<int> + out-of-band
    from/to/type/globalName* fields) over isolate SendPorts; the byte-level header forms exist
    (§3/§4) but are partly bypassed — real networking must serialize the header itself.

## 9. Citations
- glp_runtime/lib/multiagent/payload_serializer.dart (authoritative byte layout) — glp-current.
- docs/ma/madGLP-spec.md §8.2 Send/Wire Format, §8.3 Receive, §11.3 Heap, §11.4 Serialization,
  §4.1 index-0 serializer, §12.2 send_to_net — glp-current.
- docs/ma/isolate-boot-spec.md §7.2 NetworkMsg (payload List<int>; globalNameAgent/Index/IsWriter) — glp-current.
- glp_runtime/lib/multiagent/mad_helpers.dart (GlobalName/GlobalNameType) — glp-current.
- E. Shapiro et al., "GLP: A Grassroots, Multiagent, Concurrent, Logic Programming Language",
  arXiv:2510.15747 — glp-paper (mechanism: globalize/localize, global names, reader/writer; NO concrete
  wire format; access: full-text PDF, body text not machine-extractable in this session).
- "Distributed variable server for atomic unification", PODC 1990; FCP-on-multi-transputer impl
  (Springer) — earlier-cl-paper (distributed-unification mechanism only; no byte format; abstract-only).
