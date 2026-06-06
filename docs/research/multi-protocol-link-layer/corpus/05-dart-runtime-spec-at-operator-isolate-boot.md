---
title: "Dart Runtime Spec for `@` Operator (Isolate Boot) v0.6 (DRAFT)"
authors: "glpnet project (internal spec; unattributed)"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/ma/isolate-boot-spec.md"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: Dart Runtime Spec for @ Operator (Isolate Boot) v0.6"
precedence_class: glp-current
access: full-text
---

# Extraction — Dart Runtime Spec for `@` Operator (Isolate Boot) v0.6

> **Provenance note.** This is the **local glpnet spec** at `docs/ma/isolate-boot-spec.md`
> (Version 0.6 DRAFT, dated 2026-02-01, Status "Updated for madGLP"). Under SOURCE PRECEDENCE
> it is **glp-current — the HIGHEST authority** (current implementation truth), so it is preserved
> here verbatim/quoted rather than overridden by any paper. It is the **baseline single-instance
> isolate transport** that the multi-protocol link-layer feature replaces/extends: today, inter-
> instance wiring is in-process Dart `Isolate`s + `SendPort` routing; the new link primitives
> generalize this to N remote GLP REPL instances over MQTT/AMQP/CoAP/HTTP/2/3/etc.

---

## 1. Why this source matters to the link layer (B2 fidelity yardstick)

The `@` operator + `IsolateManager` is the **existing, working "split across runtime instances"
mechanism**. It already does the core transform the link-layer must generalize: it takes a GLP
program whose agents communicate through shared logic variables/channels and **distributes those
agents across separate runtime instances (Dart isolates)**, with the Dart layer carrying bindings
between them. The multi-protocol layer replaces the in-process `SendPort` transport with remote
transports, so this spec defines:

- the **decomposition unit** (per-agent goal `p(AgentId, UICh, NetCh)` spawned `@AgentId`),
- the **wiring contract** (UI channel = arg 2, network channel = arg 3; `ch(In?, Out)` shape),
- the **routing model** (Dart routes by destination agent ID; no GLP-level network switch),
- the **cross-isolate variable-binding protocol** (madGLP push-based `assignment` messages),
- the **inter-isolate message envelope** (`NetworkMsg` fields — the thing a remote transport frame must carry).

---

## 2. Document identity (verbatim)

> **Version**: 0.6 (DRAFT)
> **Date**: 2026-02-01
> **Status**: Updated for madGLP

Document history (load-bearing for "what changed at v0.6"):

> | 0.5 | 2026-01-31 | Updated for madGLP: replaced IRMA terminology with madGLP, removed deprecated APIs (registerNetworkInput/Output, handleNetworkMessage), updated message flow to use push-based model with global_send and handleMadAssignment |
> | 0.6 | 2026-02-01 | Redesigned Section 4.2: UI Agent Layer with two implementations (window vs actor). Added `ui_agent_window/2`, `ui_agent_actor/2`, `ui_relay/2` with `no_readers` validation. Added `'_spawn_window'/2` builtin. Boot examples for both modes. |

---

## 3. Core design principle (verbatim — load-bearing)

> The `@` operator enables GLP programs to declaratively spawn agents across Dart isolates at boot time.

> **Key design principle**: The Dart runtime handles all inter-isolate routing. There is no GLP-level
> network switch — messages are routed by Dart based on the destination agent ID.

Instructs the Dart runtime to (verbatim):

> 1. Create three isolates named `alice`, `bob`, `charlie`
> 2. In each isolate, run the specified goal with properly wired channels
> 3. Route madGLP messages between isolates

**Link-layer implication:** routing-by-destination-agent-ID is the seam. A remote transport must
preserve "Dart (host) routes by destination agent ID" — the GLP program stays unaware of transport.

---

## 4. Decomposition unit & syntax (the "split" contract)

The `@` syntax is what designates **which side / which instance** a goal runs on — the exact
mechanism the link-layer's "per-instance goal that designates its role" must subsume.

Grammar (verbatim):

```
BootDecl   ::= 'procedure' 'boot' '.'
BootClause ::= 'boot' ':-' SpawnGoal (',' SpawnGoal)* '.'
SpawnGoal  ::= Goal '@' AgentId
AgentId    ::= Atom
Goal       ::= Functor '(' AgentId ',' Channel ',' Channel ')'
Functor    ::= Atom
Channel    ::= 'ch' '(' '_?' ',' '_' ')'
```

Restrictions (verbatim, load-bearing — these are the **current limits the link layer must lift**):

> 1. **Boot-time only**: The `@` operator is only valid in the `boot/0` clause. It cannot appear elsewhere in the program.
> 2. **First clause requirement**: The GLP file must have `procedure boot.` declaration and `boot/0` clause as its first procedure when using isolate spawning.
> 4. **Ground agent identifiers**: The `AgentId` in both the goal and after `@` must be ground atoms, and must match (e.g., `agent_init(alice, ...)@alice`).
> 5. **Goal structure**: The spawned goal must be a 3-arity procedure `p(AgentId, UICh, NetCh)` where:
>    - First argument is the agent's identifier (must match the `@AgentId`)
>    - Second argument is the UI channel (user interaction)
>    - Third argument is the network channel (inter-agent cold-calls)
>    - The procedure name `p` can be any atom (e.g., `agent_init`, `alice_agent`, `test_agent`)
> 6. **Anonymous channel variables**: Channel arguments must use the pattern `ch(_?,_)` — the Dart runtime creates and wires the actual variables.

---

## 5. Channel wiring contract (verbatim — the shared-variable seam)

> A channel is a pair `ch(In?, Out)` where:
> - `In?` is a reader — the agent reads messages from this stream
> - `Out` is a writer — the agent writes messages to this stream

This is the **one-writer/one-reader atomic pair** the feature wants to distribute across instances.

Network channel (arg 3) Dart wiring (verbatim table — defines what a remote link must replace):

> | Direction | Variable | Dart Wiring |
> |-----------|----------|-------------|
> | Agent reads | `NetIn?` | Cold-call messages delivered here |
> | Agent writes | `NetOut` | Cold-call messages routed by IsolateManager |
>
> **Message format**: `msg(Target, Content)` where `Target` is the destination agent's identifier.

Boot-time channel allocation, per isolate (verbatim Dart, the **writer/reader pair creation** that a
distributed scheme must mirror remotely):

```dart
// 3. Create network channel pair (third argument)
// In madGLP, network communication happens via global_send goals,
// not via explicit network streams. Channel is for cold-call initiation.
final (netInWriter, netInReader) = runtime.heap.allocateVariable();
final (netOutWriter, netOutReader) = runtime.heap.allocateVariable();
final netCh = StructTerm('ch', [VarRef(netInReader), VarRef(netOutWriter)]);
```

The per-isolate **message-delivery callback** is the host-side hook a remote transport plugs into:

```dart
// 4. Set up message delivery callback
ctx.onMessageReady = (dest, msg) {
  config.mainPort.send(NetworkMsg(config.agentId, dest, msg.payload, msg.type));
};
```

---

## 6. Cross-isolate variable-binding protocol (B2 core — verbatim)

This is the **distributed-unification mechanism that already exists in-process** and is the fidelity
yardstick for blocker B2. madGLP uses a **push-based model: messages sent when writers are bound.**

> **Assignment Message Flow:**
> 1. Agent p binds writer X, X? becomes known
> 2. `global_send` goal fires (if watching X?)
> 3. Message added to M_p (message set)
> 4. `ctx.flushMessages()` delivers via `onMessageReady` callback
> 5. IsolateManager routes to destination agent
> 6. Destination receives, calls `ctx.handleMadAssignment()`
> 7. Local writer bound, entry removed from W_p

Message types handled by the router (verbatim):

> | Type | Description | Routing |
> | `agentMessage` | Cold-call content (Network Transaction) | By destination in `msg(Target, _)` |
> | `assignment` | Variable assignment (per madGLP spec) | By global name in message |

**B2 implication:** a remote link layer must preserve this exact protocol — a writer-binding on
instance A produces an `assignment` message keyed by a **global name** (agent + index + is-writer)
that, delivered to instance B, binds B's local writer via `handleMadAssignment`. Writer-MGU semantics
(only writers bound, never readers) are preserved because only the **bind event** crosses the wire.

---

## 7. Inter-isolate message envelope (the frame a remote transport must carry — verbatim)

```dart
/// Network message to route (madGLP)
class NetworkMsg extends IsolateMessage {
  final String from;
  final String to;
  final List<int> payload;
  final MessageType type;

  /// Optional global name fields for routing
  final String? globalNameAgent;
  final int? globalNameIndex;
  final bool? globalNameIsWriter;
}
```

Other inter-isolate messages: `Ready{agentId, sendPort}`, `Start{}`, `UIEvent{agentId, payload}`,
`Done{agentId, success, error}`.

**Link-layer implication:** the minimal over-the-wire frame for distributed unification =
`{from, to, payload, type, globalNameAgent, globalNameIndex, globalNameIsWriter}`. A remote transport
(MQTT topic, AMQP queue, CoAP resource, WebSocket frame, etc.) must encode exactly these fields; the
`globalName*` triple is the **cross-instance variable identity** that replaces the shared heap cell.

---

## 8. Boot & startup sequence (verbatim)

Boot sequence:

> 1. Dart runtime receives GLP file path
> 2. BootLoader reads file, verifies first procedure is boot/0
> 3. BootLoader extracts SpawnDirectives from boot clause
> 4. BootLoader compiles the program
> 5. IsolateManager spawns isolates:
>    a. For each SpawnDirective: Spawn isolate with AgentConfig; Wait for Ready message with SendPort; Store SendPort in routing table
> 6. IsolateManager sends Start to all isolates
> 7. IsolateManager enters routing loop

Agent startup sequence:

> 1. Create GlpRuntime and MadContext
> 2. Allocate channel variables (UICh, NetCh)
> 3. Set up onMessageReady callback for message delivery
> 4. Build goal arguments with proper VarRef readers
> 5. Spawn goal on scheduler
> 6. Send Ready to main isolate
> 7. Wait for Start
> 8. Run scheduler, process messages via flushMessages()

`IsolateManager` surface (verbatim): `boot(BootConfig)`, `routeMessage(from, to, NetworkMsg)`,
`waitForCompletion({timeout})`, `shutdown()`; routing table `Map<String, SendPort> _agentPorts`.

---

## 9. Explicitly out-of-scope today (verbatim) — exactly the gaps the link layer fills

> The following are explicitly **not supported** in this version:
> 1. **Dynamic spawning**: Using `@` at runtime (not just boot)
> 2. **Variable agent IDs**: `agent_init(Id?, ...)@Id?` with runtime evaluation
> 3. **Isolate pools**: Multiple agents per isolate
> 4. **Remote isolates**: Network-distributed agents
> 5. **Non-3-arity goals**: Only 3-argument procedures are supported

Item 4, **"Remote isolates: Network-distributed agents,"** is precisely the multi-protocol link-layer
feature. The link layer is the generalization of `IsolateManager` from in-process `SendPort` routing
to remote transports, lifting restrictions 1 (boot-time-only) and 4 (local-only).

---

## 10. Structured findings (for the requesting thread)

1. **The `@`/IsolateManager mechanism is the existing "split a shared-variable program across instances"
   baseline.** Dart isolates + `SendPort`; Dart (host) routes by destination agent ID; no GLP-level
   network switch. The link layer swaps the transport, not the model.
2. **Decomposition unit = `p(AgentId, UICh, NetCh)` spawned `@AgentId`** — a per-instance goal whose
   first arg designates its identity/role. This is the concrete prior art for "one program parameterized
   by a per-instance role goal."
3. **The shared-variable seam is `ch(In?, Out)`** — a one-reader/one-writer pair; arg 2 = UI channel,
   arg 3 = network channel. Distributing this pair across instances is the feature's central transform.
4. **Cross-instance binding already works via madGLP push-based `assignment` messages** (writer bound →
   `global_send` → `flushMessages`/`onMessageReady` → route → `handleMadAssignment` binds remote writer).
   This is the in-process precedent the distributed-unification blocker B2 must remain faithful to.
5. **Minimal wire frame = `NetworkMsg{from, to, payload, type, globalNameAgent, globalNameIndex,
   globalNameIsWriter}`.** The `globalName*` triple is the cross-instance variable identity; any remote
   transport encoding must carry these fields.
6. **Current restrictions the link layer explicitly lifts:** boot-time-only `@`, ground/local agent IDs,
   3-arity goals only, and "Remote isolates: not supported." Item 4 is named verbatim as out-of-scope —
   i.e., the link layer's mandate.
7. **Host I/O caveat consistent with GLP-First:** UI is mediated by a `ui_agent` layer (window vs actor)
   with a `ui_relay/2` guarded by `no_readers(Msg?)` so output to the user carries no reader variables
   (writers allowed for interactive queries). Rerouting stdin/stdout/stderr to a remote REPL must respect
   this `no_readers` discipline on the outbound side.

---

## 11. Precedence & access notes

- **precedence_class: glp-current** — local implementation-truth spec; HIGHEST authority. No paper was
  used to override it; this corpus entry preserves it verbatim for the thread.
- **access: full-text** — complete source read from the repo (`docs/ma/isolate-boot-spec.md`, 516 lines).
- **No web fetch performed:** the requested source ("Dart Runtime Spec for `@` Operator (Isolate Boot)
  v0.6") IS this internal repo document, not an external publication; the candidate hint pointed to it
  directly. Searching the web would only surface lower-precedence material. Cross-referenced (not quoted
  here) sibling specs for the routing/cross-isolate model: `docs/ma/madGLP-spec.md`,
  `docs/ma/agent-runtime-spec.md`.
