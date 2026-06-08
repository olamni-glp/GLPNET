---
title: "mad_context.dart — madGLP Agent Context implementation (MadContext)"
authors: "GLP/glpnet maintainers (Dart runtime); spec authored by Claude per CGLP paper §7 (E. Shapiro et al.)"
year: "2026"
source_url: "file://D:/bstdev/research/glp/glpnet/glp_runtime/lib/multiagent/mad_context.dart"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: mad_context.dart (madGLP context implementation)"
precedence_class: glp-current
access: full-text
---

# mad_context.dart — `MadContext` (madGLP Agent Context)

**Provenance.** Local source-of-truth implementation at
`glp_runtime/lib/multiagent/mad_context.dart` (566 lines), authoritative per
SOURCE PRECEDENCE class (1) "local docs/ GLP specs = current implementation
truth". It is the concrete Dart realization of `docs/ma/madGLP-spec.md` v5.3
(itself derived from the CGLP paper §7 "Multiagent Deterministic GLP"). The
file header declares: *"Each agent has W_p (global writers table) and M_p
(message queue). Specification: /docs/ma/madGLP-spec.md"*. All heap calls it
makes resolve against `glp_runtime/lib/runtime/heap_fcp.dart` (the FCP heap).

This file is **the exact code a new transport must plug under**: it produces
`OutboundMessage`s into `mp` and consumes inbound assignments via
`handleMadAssignment`. A transport replaces the in-process delivery callback;
it does not touch unification, suspension, or globalize/localize.

---

## 1. Why this is the B2 (distributed-unification) fidelity yardstick

madGLP is precisely the existing answer to "split a writer/reader pair across
two runtime instances." The multi-protocol link layer is the same transform
generalized to N instances over arbitrary transports. `MadContext` already
implements the one-writer/one-reader split that the new link primitives must
preserve. The transport seam is narrow and well-defined:

- **Outbound seam:** every cross-instance assignment is an `OutboundMessage`
  appended to the message queue `mp` and drained by `flushMessages()` through
  the `onMessageReady` callback (`MessageDeliveryCallback`). Today that
  callback is wired to an in-process isolate coordinator; a transport (MQTT,
  AMQP, WebSocket, BLE GATT, ...) replaces *only* this callback.
- **Inbound seam:** every arriving assignment is delivered by calling
  `handleMadAssignment({globalName, value, fromAgent})`. A transport's
  receive side decodes a wire frame back into `(GlobalName, Term, fromAgent)`
  and calls this one method.

Everything between those two seams — globalize/localize, the global writers
table `W_p`, the `global_send` registry, writer-binding, suspension
reactivation — is transport-agnostic and **must be preserved bit-for-bit** by
any link-layer scheme. That is the fidelity yardstick.

---

## 2. Class shape and per-agent state (verbatim)

```dart
class MadContext {
  final String agentId;                          // "alice", "bob", ...
  final GlpRuntime runtime;                       // underlying GLP runtime
  final GlobalWritersTable wp;                     // W_p: writers awaiting incoming assignments
  final MessageQueue mp;                           // M_p: outbound messages
  final GlobalSendRegistry globalSendRegistry;     // watches readers, sends when known
  late final PayloadSerializer _serializer;        // message encoding
  MessageDeliveryCallback? onMessageReady;         // set by coordinator (the transport seam)
  void Function(String)? traceSink;                // MAD trace output
```

Constructor initializes the four per-agent structures keyed by `agentId`:
`wp = GlobalWritersTable(agentId)`, `mp = MessageQueue()`,
`globalSendRegistry = GlobalSendRegistry(agentId)`, and
`_serializer = PayloadSerializer(agentId)`.

`MessageDeliveryCallback` is the load-bearing typedef:
```dart
typedef MessageDeliveryCallback = void Function(String destination, OutboundMessage message);
```

---

## 3. Outbound: writer-binding → global_send firing → message queue

### 3.1 `onWriterBound` / `_fireGlobalSendGoalIfExists`

This is the **concrete global_send firing** path. When a local writer is bound
(a `heap.onBind` callback registered earlier calls `onWriterBound(writerId,
value)`), the context consults the `globalSendRegistry`: if a `global_send`
goal is watching that writer's paired reader, fire it.

> Doc comment (verbatim): *"When a writer is bound, its paired reader becomes
> 'known'. If there's a global_send goal watching that reader, fire it now."*

On fire (`result != null`), the sequence is:
1. **Globalize** the value term, replacing local `VarRef`s with `GlobalName`s
   so the receiver can localize nested global names:
   ```dart
   final globalizedValue = globalizeTermWithResult(
       result.value as Term, result.extractedVariables, result.globalizeResult);
   ```
2. **Serialize** into a wire payload via `_serializer.createGlobalSendPayload(...)`.
3. **Queue** `OutboundMessage(destination: result.destination,
   type: MessageType.assignment, payload: payload)` onto `mp`.
4. **Re-register** any newly spawned goals for nested variables — both a
   `globalSendRegistry.register(newGoal)` AND a `heap.onBind(newGoal.readerAddr,
   ...)` callback that re-enters `onWriterBound`. The code comment stresses both
   must be set up (same as `registerGlobalSendSpawns`).

### 3.2 Variable extraction — `_extractTermVarsRecursive` (the writer/reader-case core)

This is where the **one-writer/one-reader atomic pair** is captured for
transport. Each `VarRef` is classified via the heap and turned into a `TermVar`
carrying *both* ends of its pair (cross-pointers from the FCP heap):

```dart
if (term is VarRef) {
  final isReader = runtime.heap.isReader(term.addr);
  if (isReader) {
    final writerAddr = runtime.heap.tryWriterForReader(term.addr);
    result.add(TermVar.reader(term.addr, writerAddr: writerAddr ?? term.addr));
  } else {
    final readerAddr = runtime.heap.pairedReaderAddr(term.addr);
    result.add(TermVar.writer(term.addr, readerAddr: readerAddr ?? term.addr));
  }
} else if (term is StructTerm) {
  for (final arg in term.args) { _extractTermVarsRecursive(arg, result); }
}
// ConstTerm has no variables
```

Load-bearing for B2: the writer↔reader cross-pointer (`tryWriterForReader`,
`pairedReaderAddr`) IS the FCP bidirectional cell pairing. Any distributed
scheme must transmit *which end* of the pair is crossing the link, because the
globalize rule is polarity-dependent (writer → table entry at globalizer;
reader → `global_send` goal at globalizer — see §5).

### 3.3 `flushMessages()` — the drain point (transport hook)

```dart
int flushMessages() {
  if (onMessageReady == null) return 0;
  ...
  for (final dest in destinations) {
    while (true) {
      final msg = mp.poll(dest);
      if (msg == null) break;
      onMessageReady!(dest, msg);   // ← transport replaces this callback
      count++;
    }
  }
  return count;
}
```

A new transport's send path = an `onMessageReady` implementation that
serializes/frames `msg.payload` (already bytes) and ships it to `dest` over
MQTT/AMQP/CoAP/HTTP-2/3/XMPP/DDS/WS/SSH/FTP/SFTP/BLE/etc.

### 3.4 `send(...)` — the `'_send'` builtin implementation

`MadContext.send(term, isWriter, gnAgent, gnIndex, destAgent)` is the concrete
`'_send'(T, G, Q)` builtin (spec §11.5). It: extracts vars, **globalizes**
(`globalize(variables, localAgent, remoteAgent, table)`), registers spawned
`global_send` goals, transforms the term to global names, then **branches on
serializer vs normal**:

- **Serializer case** (`isWriter && gnIndex == 0`): wrap content in a list
  cell via `_serializer.createSerializerPayload(...)` — wire form
  `_w(q,0) := [T↑ | _w(q,0)]` (cold-call to network input; tail reuses the
  serializer writer).
- **Normal case** (`i > 0`): `_serializer.createGlobalSendPayload(...)` — wire
  form `G := T↑` sent directly.

Then `mp.add(OutboundMessage(destination: destAgent, type:
MessageType.assignment, payload: payload))`.

Verbatim spec §11.5 backing (HIGHEST authority for behavior):
> **Case G = `_w(q, 0)` (Serializer)**: 1. Globalizes term T for remote agent Q
> 2. Adds message `(_w(q,0) := [T↑ | _w(q,0)], Q)` to M_p — content wrapped in
> list cell, writer reused in tail.
> **Case G = `_w(p, i)` or `_r(p, i)` with i > 0 (Normal)**: 1. Globalizes term
> T for remote agent Q 2. Adds message `(G := T↑, Q)` to M_p — content sent
> directly.

Note: for globalize-**writer** entries the code deliberately registers **no**
`onBind` here — comment: *"when Y is a writer, p creates an entry (Y, q) and
waits for the assignment to arrive. The global_send goal is spawned at q (by
localize), not at p. Agent p does not send anything for _w entries."* This is
the polarity asymmetry a transport must respect.

---

## 4. Inbound: `handleMadAssignment` and its three cases (the receive seam)

`handleMadAssignment({globalName, value, fromAgent})` dispatches per
spec §8.3. Verbatim dispatch:

```dart
if (globalName.isWriter && globalName.index == 0) {
  _handleSerializerAssignment(value, fromAgent);          // _w(p,0) := [T | _w(p,0)]
} else if (globalName.isWriter) {
  _handleWriterAssignment(globalName, value, fromAgent);  // _w(p,i), i>0 — direct index lookup
} else {
  _handleReaderAssignment(globalName, value, fromAgent);  // _r(p,i) — search by (remoteAgent, index)
}
```

### 4.1 Serializer case — `_handleSerializerAssignment` (cold-call inbound)

Cold-call to this agent's network input stream. Steps in code:
1. Get `currentWriter = wp.serializerWriterAddr` (permanent index-0 entry;
   `StateError` if uninitialized).
2. Unwrap the list cell `[T | serializer_marker]` → `content` (head); ignore
   tail marker. Falls back to using `value` directly if not list-wrapped.
3. **Localize** nested global names in content
   (`extractGlobalNames` → `localize(...)` → `localizeTermWithResult`),
   registering spawned goals via `registerGlobalSendSpawns`.
4. Allocate a fresh pair `(freshWriter, freshReader)` = `heap.allocateVariable()`.
5. Build list cell `StructTerm('.', [content, VarRef(freshReader)])`.
6. **Bind** current serializer writer to extend the stream:
   `heap.bindVariable(currentWriter, listCell)` → `activations`.
7. `wp.updateSerializerWriter(freshWriter)` — entry is **updated, never
   removed** (permanent).
8. Reactivate: `for (act in activations) runtime.enqueueReactivatedGoal(act)`.

Spec §8.3 verbatim:
> **Case `m = (_w(q, 0) := [T↑ | _w(q,0)])` (Serializer)**: ... Agent q finds
> the permanent entry `(N_q, *)` at index 0. Localize T↑ by q to get T_q↓.
> Assign N_q := [T_q↓ | N'_q] where N'_q is a fresh writer. Update the entry to
> `(N'_q, *)` at index 0 (extending the stream). Reactivate any goals suspended
> on N_q?. The entry is NOT removed.

### 4.2 Writer case — `_handleWriterAssignment` (`_w(p,i)`, i>0)

We (agent p) globalized writer Y, creating entry `(Y, q)` at index i. Direct
index lookup: `entry = wp.lookupByIndex(globalName.index)` (`StateError` if
absent). Localize nested global names in `value`, `heap.bindVariable(entry
.writerAddr, localizedValue)`, reactivate, then `wp.removeGlobalizeEntry(
globalName.index)`.

Spec §8.3 verbatim:
> **Case `m = (_w(p, i) := T↑)` with i > 0**: ... Agent p finds entry `(X, q)`
> at index i in W_p. ... Localize T↑ by p from q to get T_p↓, assign
> X := T_p↓, apply {X? := T_p↓} to goals containing X?, reactivate suspended
> goals, and remove the entry from W'_p.

### 4.3 Reader case — `_handleReaderAssignment` (`_r(p,i)`)

We (agent q) localized `_r(p,i)`, creating LocalizeEntry `(Z_q, p, i)`. Search
by remote identity: `entry = wp.findByRemote(globalName.agent,
globalName.index)` (`StateError` if absent). Then localize nested names, bind
`entry.writerAddr`, reactivate, `wp.removeLocalizeEntry(globalName.agent,
globalName.index)`.

Spec §8.3 verbatim:
> **Case `m = (_r(p, i) := T↑)`**: ... Agent q searches its global writers
> table for an entry `(X_q, p, i)` matching the remote agent p and remote index
> i. ... Localize T↑ by q from p to get T_q↓, assign X_q := T_q↓, apply
> {X_q? := T_q↓} to goals containing X_q?, reactivate suspended goals, and
> remove the entry from W'_q.

### 4.4 `handleMadAssignmentWithGlobalNames` (nested-name variant)

Pre-localizes a list of nested global names (creating LocalizeEntries +
spawning `global_send` goals), then delegates to `handleMadAssignment`.

---

## 5. globalize / localize — the polarity-dependent split rule (preserved invariant)

`MadContext` calls free functions from `mad_helpers.dart`: `globalize`,
`localize`, `globalizeTermWithResult`, `localizeTermWithResult`,
`extractGlobalNames`, `GlobalSendSpawn`. The polarity rule (spec §5.1, v5.3
corrected) that any link layer MUST preserve:

- **Writer Y globalized at p** → entry `(Y, q)` added to `W_p`; *receiver gets
  the writer end and will send the assignment back*. No `global_send` at p.
- **Reader Y? globalized at p** → spawn `global_send(Y?, _r(p,i), q)` at p; *p
  keeps the writer, sends to receiver when the reader becomes known*. No entry.

`registerGlobalSendSpawns(List<GlobalSendSpawn>)` is the shared registration
helper: for each spawn it does `globalSendRegistry.register(GlobalSendGoal
.fromSpawn(spawn))` AND `runtime.heap.onBind(spawn.readerAddr, (value) =>
onWriterBound(spawn.readerAddr, value))`. The dual registration (registry +
heap onBind) is the mechanism that makes push-based firing work and must be
mirrored by any distributed link establishment.

---

## 6. Heap interface the transport plugs *under* (FCP heap, `heap_fcp.dart`)

`MadContext` is a thin layer over the FCP heap. Confirmed call surface
(all present in `glp_runtime/lib/runtime/heap_fcp.dart`):

| MadContext call | heap_fcp.dart | Role in the model |
|---|---|---|
| `runtime.heap.isReader(addr)` | `bool isReader(int)` (l.128) | Tag-based polarity check (RoTag vs WrtTag) |
| `runtime.heap.tryWriterForReader(addr)` | `int? tryWriterForReader(int)` (l.181) | Reader→writer cross-pointer of the FCP pair |
| `runtime.heap.pairedReaderAddr(addr)` | (paired-reader lookup) | Writer→reader cross-pointer |
| `runtime.heap.allocateVariable()` | `(int,int) allocateVariable()` (l.85) | Allocate fresh writer/reader pair |
| `runtime.heap.bindVariable(writer, value)` | `List<GoalRef> bindVariable(int, Term)` (l.671) | **Writer-MGU**: bind writer, return goals to reactivate |
| `runtime.heap.onBind(writer, cb)` | `void onBind(int, void Function(Term))` (l.596) | Suspension/reactivation hook → drives push-send |
| `runtime.enqueueReactivatedGoal(act)` | (scheduler) | Re-enter reactivated suspended goals |

Key fidelity point for B2: `bindVariable` returns the set of goals to
reactivate (the **suspension/reactivation** half of three-valued unification).
`MadContext` never performs unification itself — it only binds *writers it owns
locally* (entry/serializer writers) with already-localized terms, then hands
reactivation back to the scheduler. A distributed scheme must keep this
property: **the remote side performs a purely local writer-MGU**; the network
only carries the (globalized) term, never the binding act across the wire.

---

## 7. Push model & suspension (no read-requests on the wire)

`processSuspension(Set<int> blockingReaders)` documents the **push-based**
nature: *"In madGLP, suspension means we're waiting for assignments to arrive.
The push model means we don't send read requests - we just wait."* It only
logs blocking readers; **no request messages are emitted**. Consequence for the
link layer: transports need only carry *assignment* frames in the
writer→reader direction; there is no demand/pull protocol to design. This
directly informs the bilateral-p2p framing (sub-question T1) — the channel is
fundamentally unidirectional per link (writer-owner → reader-owner), and a
"bidirectional" conversation is two independent links.

`exportTerm(Term)` registers `heap.onBind` callbacks for every writer in a
term, so that binding later routes the assignment — the setup analog of `send`
for the Flutter-app compatibility path.

---

## 8. Structured extraction — transport-plug map (the deliverable)

What a NEW transport must implement, and what it must NOT touch:

**MUST implement (the two seams):**
1. **Send side** = an `onMessageReady(String destination, OutboundMessage
   message)` callback. Input is already a serialized `payload` (`List<int>`) +
   a `MessageType.assignment` tag + a destination agentId. Frame and ship it.
2. **Receive side** = decode a wire frame back to `(GlobalName globalName,
   Term value, String fromAgent)` and call
   `madContext.handleMadAssignment(...)` (or the nested-names variant). The
   payload decode is the inverse of `PayloadSerializer`.

**MUST preserve unchanged (the invariants — B2 fidelity):**
- Globalize/localize polarity rule (writer→entry-at-globalizer; reader→
  global_send-at-globalizer) — §5.
- The three receive cases keyed on `_w(p,0)` (serializer/cold-call, permanent,
  stream-extend) vs `_w(p,i>0)` (index lookup, remove) vs `_r(p,i)`
  (remote-(agent,index) search, remove) — §4.
- Local-only writer-MGU on receive (`heap.bindVariable`) + scheduler
  reactivation; no binding crosses the wire — §6.
- Push-only assignment frames; no read/demand messages — §7.
- Index allocation discipline (counter from 1, index 0 reserved for
  serializer, indices never reused) — owned by `GlobalWritersTable`, not this
  file, but messages reference it.

**MUST NOT touch:** unification, suspension/reactivation, the heap, the
GlobalWritersTable lifecycle, the GlobalSendRegistry firing logic. Transport is
purely a replacement for the in-process delivery callback + a wire codec around
`PayloadSerializer`. This is exactly the narrow seam the multi-protocol link
layer should target — confirming the link primitives can be added *above*
madGLP without modifying core GLP/FCP semantics.

---

## 9. Collaborating modules (for follow-up corpus entries)

`mad_context.dart` imports / depends on (all in
`glp_runtime/lib/multiagent/`): `message_queue.dart` (M_p / `OutboundMessage`
/ `MessageType`), `payload_serializer.dart` (wire codec — the byte-level
format a transport wraps), `global_send.dart` (`GlobalSendRegistry`,
`GlobalSendGoal`, `GlobalSendSpawn`), `global_writers_table.dart` (W_p
entries + index discipline), `mad_helpers.dart` (`globalize`/`localize` and
the `*WithResult` term transforms), plus runtime `heap_fcp.dart`. The
`payload_serializer.dart` format and `global_writers_table.dart` index
discipline are the highest-value next extractions for the link-layer wire
design.
