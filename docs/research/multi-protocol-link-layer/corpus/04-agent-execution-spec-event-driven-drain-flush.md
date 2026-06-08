---
title: "Agent Execution Spec (event-driven drain-flush) — per-isolate event loop, three goal-activation mechanisms, enqueue-once invariant"
authors: "glpnet project (Claude, under E. Shapiro madGLP semantics) — local spec"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/ma/agent-runtime-spec.md"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: Agent Execution Spec (event-driven drain-flush)"
precedence_class: glp-current
access: full-text
related_sources:
  - title: "madGLP Specification v5.3 (local) — global_send / global writers table / Reduce-Send-Receive"
    source_url: "file:///D:/bstdev/research/glp/glpnet/docs/ma/madGLP-spec.md"
    precedence_class: glp-current
  - title: "Implementing Grassroots Logic Programs with Multiagent Transition Systems and AI"
    authors: "Ehud Shapiro et al."
    year: "2026"
    source_url: "https://arxiv.org/abs/2602.06934"
    precedence_class: glp-paper
    note: "Underlying maGLP/madGLP transition system (Reduce/Send/Receive, global links) that the event-driven drain-flush loop implements. Background/mechanism authority only; the local spec governs current implementation truth."
  - title: "GLP: A Grassroots, Multiagent, Concurrent, Logic Programming Language"
    authors: "Ehud Shapiro et al."
    year: "2025"
    source_url: "https://arxiv.org/abs/2510.15747"
    precedence_class: glp-paper
---

# Agent Execution Spec — event-driven drain-flush (extraction)

> **Provenance & precedence.** The authoritative source for this question is the
> *local* repo spec `docs/ma/agent-runtime-spec.md` (status: DRAFT — 2026-02-14).
> Under the research thread's SOURCE PRECEDENCE rule, a local `docs/` GLP spec is the
> HIGHEST authority (`glp-current`) — it is *current implementation truth*, overriding
> Shapiro's papers, which here only supply the underlying maGLP/madGLP transition system
> (arXiv:2602.06934) that this event loop *implements*. The spec is a repo file, not a
> web URL, so "fetch & preserve" = verbatim extraction of its load-bearing content into
> the corpus. Companion: `docs/ma/madGLP-spec.md` (global_send / global writers table /
> Reduce-Send-Receive) is quoted where it grounds the activation mechanisms.

## Why this matters for the link layer (B2 fidelity yardstick)

The distributed multi-protocol link layer splits a writer X / reader X? pair across
**N runtime/REPL instances**. madGLP already splits such a pair across **isolates** on one
host: each agent runs in its own Dart isolate, and the shared variable is replaced by two
local pairs joined by a *global link* (`global_send` goal + global-writers-table entry).
Any remote transport is a **new carrier for the same `NetworkMsg` event** that drives this
loop. Therefore the link layer MUST preserve, unchanged, the per-isolate **event-driven
drain-flush execution model**, the **three goal-activation mechanisms**, and the
**enqueue-once invariant** extracted below — these are the integration contract a transport
plugs into, not implementation details it may bypass.

---

## 1. The execution model is event-driven — NO clock / NO tick loop (load-bearing)

Verbatim (§1):

> "Each agent runs in a separate Dart isolate. Execution is **event-driven**: agents drain
> their goal queue and flush outgoing messages in response to events. There is no external
> clock or tick loop."

The canonical loop (verbatim, §1):

```
await for message on receivePort:
    handle message (Start, NetworkMsg, or UIEvent)
    scheduler.drainWithStatus()    // run all runnable goals
    ctx.flushMessages()            // send queued outbound messages
```

Three event types trigger execution (verbatim table, §1):

| Event | What happens |
|-------|-------------|
| `Start` | Initial drain+flush after boot. Kicks off the agent's goal. |
| `NetworkMsg` | Deserialize assignment, bind variables (activating suspended goals), drain+flush. |
| `UIEvent` | Inject user input into stream (activating suspended goals), drain+flush. |

Verbatim, §1 (the event chain is the whole engine):

> "Each event is handled fully (drain+flush) before the next event is processed. The
> messages produced by flush are routed by the `IsolateManager` (headless) or coordinator
> (UI) to destination agents, where they arrive as new `NetworkMsg` events. This chain of
> events drives the entire protocol forward — no polling or periodic triggering is needed."

Headless vs UI are the **same model** (§1.1, §1.2): the `IsolateManager` (headless) / the
coordinator (UI) both "spawn agent isolates … and route `NetworkMsg` between them. There is
no tick loop." UI additionally delivers Flutter user input as `UIEvent`.

**Link-layer consequence:** a remote transport (MQTT, AMQP, CoAP, HTTP/2, HTTP/3, XMPP,
DDS, WebSocket, SSH/FTP/SFTP/file, BLE/BR-EDR, plain+TLS) is a substitute *router* for the
`NetworkMsg` carrier. Each inbound wire message becomes one `NetworkMsg` event → exactly
one drain-flush cycle. The transport MUST NOT introduce its own tick/poll loop into agent
execution.

---

## 2. The THREE goal-activation mechanisms (verbatim §2)

§2: "A goal becomes runnable when data it was waiting for arrives. There are exactly three
mechanisms:"

### 2.1 Stream extension — `InputInjector.inject` (§2.1)

Injecting a term into a stream binds the stream's current writer to `[Term | FreshTail?]`;
`bindVariable` returns the activations (goals suspended reading that stream); caller enqueues
them. Current (correct) code:

```dart
final activations = _userInput!.inject(term);
for (final goal in activations) {
  _runtime!.gq.enqueue(goal);
}
```

### 2.2 MAD assignment — `handleMadAssignment` (§2.2)

Verbatim:

> "When a remote agent sends an assignment (`_w(p,i) := T` or `_r(p,i) := T`),
> `MadContext.handleMadAssignment` localizes the value and calls
> `runtime.heap.bindVariable(writerAddr, localizedValue)`. This returns activations.
> `MadContext` enqueues them via `runtime.enqueueReactivatedGoal()`."

This is the activation path a **remote link delivery** rides: the transport's inbound
handler must terminate in a `handleMadAssignment` (or equivalent localize → `bindVariable`)
so suspended readers reactivate through the one sanctioned path.

### 2.3 `global_send` firing — `onWriterBound` callback (§2.3)

Verbatim:

> "When a local writer is bound (during GLP execution), the heap's `onBind` callback fires
> `MadContext.onWriterBound`, which checks if a `global_send` goal was watching that writer's
> reader. If so, the message is globalized and queued to `M_p`. This does **not** produce
> new runnable goals directly — it produces **outbound** messages, which are picked up by
> `flushMessages()`."

So mechanisms 2.1/2.2 produce *runnable goals* (inbound activation); 2.3 produces *outbound
messages* (the send side). For the link layer this is the outbound seam: a bound writer →
`global_send` → `M_p` → `flushMessages()` → transport egress.

---

## 3. The drain-flush cycle and its boundary (verbatim §3)

§3, the cycle:

```
drain scheduler (run all goals in GQ until quiescent)
flushMessages (send all queued outbound messages)
```

Verbatim purpose & limit:

> "A single drain may produce outbound messages (via global_send firing during execution).
> Flushing sends those messages to other agents. Within the same agent, one goal's output
> may enable another goal, but that is handled within the drain itself (the scheduler keeps
> running until the goal queue is empty or all remaining goals are suspended)."

What it does NOT handle (verbatim §3):

> "**Cross-agent round-trips.** When agent A sends a message to agent B, agent B processes
> it and may send a response back to agent A. This response arrives as a new `NetworkMsg`
> event at agent A, which triggers a new drain-flush cycle. **Each leg of the round-trip is
> a separate event.**"

**Link-layer consequence (B2):** distributed unification is *asynchronous and monotonic* —
each cross-instance hop is one independent event; the link layer must not assume or simulate
a synchronous round-trip within a single drain.

---

## 4. Cross-agent message flow (verbatim §4, abridged)

The router is `IsolateManager` (headless) or coordinator (UI); both "receive a `NetworkMsg`
from one agent and forward it to the destination agent's `SendPort`." The forward path:

```
goal runs → global_send fires → message queued to M_p
flushMessages() → onMessageReady callback → send via SendPort
   → router routes to B → NetworkMsg to B's SendPort
      → B: handleMadAssignment() → bindVariable() → activations → drain+flush
         → may produce response → flushMessages() → send back
   → router routes to A → NetworkMsg to A's SendPort → A: handleMadAssignment() → drain+flush
```

For the distributed link layer, the **`SendPort`/router pair is exactly the abstraction a
remote transport replaces**: egress = `onMessageReady`/`flushMessages`; ingress = a wire
message re-injected as a `NetworkMsg` → `handleMadAssignment`.

---

## 5. The invariants any transport integration MUST obey (verbatim §7)

1. "**Every event that may unblock a goal MUST be followed by a drain-flush cycle.** The
   three event types (Start, NetworkMsg, UIEvent) all satisfy this."
2. "**A goal must never be enqueued twice.** `bindVariable`'s returned activations are the
   single path for re-enqueuing suspended goals." — **the enqueue-once invariant.**
3. "**`flushMessages()` must be called after every drain.** The drain-flush cycle handles
   this."
4. "**Cross-agent communication is asynchronous.** Each leg is a separate event. The
   drain-flush cycle does not need to handle multi-hop round-trips within a single event."
5. "**Agents do not self-terminate.** Termination is external — the caller shuts down
   isolates."

### The enqueue-once invariant in practice (bug 5.1, verbatim)

> "Duplicate messages — FIXED (2026-02-12). Symptom: Every SEND_MAD appeared twice in the
> trace log. Root cause: `_reactivateSuspendedGoals()` … re-enqueued goals that
> `MadContext.handleMadAssignment` had already enqueued via `bindVariable`'s returned
> activations. Fix applied: Removed `_reactivateSuspendedGoals()` … `MadContext` already
> handles reactivation correctly via `runtime.enqueueReactivatedGoal()`."

Lesson for the link layer: there is **exactly one** reactivation path (`bindVariable`
activations). A transport that "helpfully" re-enqueues readers on delivery would reintroduce
this duplicate-message class of bug.

### Other settled history (context, §5)

- §5.3 **Premature death detection removed (2026-02-13)** — an "idle tick" heuristic falsely
  declared agents dead before messages arrived. Removed; agents do not self-terminate.
- §5.4 **Tick loop removed (2026-02-13)** — headless polling tick loop replaced by the same
  event-driven drain+flush as UI. Both modes now identical.

---

## 6. Structured extraction (the answer, distilled)

- **Model:** per-isolate, **event-driven** (NOT tick/clock/poll). One isolate per agent.
- **Loop:** `await message → handle(Start|NetworkMsg|UIEvent) → drainWithStatus() →
  flushMessages()`. Each event handled fully before the next.
- **Three goal-activation mechanisms** (the *only* ways a goal becomes runnable):
  1. **Stream extension** — `InputInjector.inject` binds writer to `[Term|Tail?]`;
     `bindVariable` returns activations; caller enqueues.
  2. **MAD assignment** — remote `_w(p,i):=T` / `_r(p,i):=T` → `handleMadAssignment` localizes
     → `bindVariable` → activations → `enqueueReactivatedGoal()`.
  3. **`global_send` firing** — `onWriterBound`/`onBind` callback on a bound writer → globalizes
     → queues OUTBOUND message to `M_p` (no new runnable goals; consumed by `flushMessages`).
- **Drain-flush boundary:** intra-agent goal-enabling handled inside one drain; **each
  cross-agent leg is a separate event** (asynchronous, monotonic).
- **Five invariants:** (1) every unblocking event → drain-flush; (2) **enqueue-once** —
  `bindVariable` activations are the sole re-enqueue path; (3) flush after every drain;
  (4) cross-agent comms asynchronous, per-leg; (5) agents never self-terminate (external
  shutdown).
- **Router seam = the transport seam:** egress via `flushMessages`/`onMessageReady`/SendPort;
  ingress via wire→`NetworkMsg`→`handleMadAssignment`. A remote multi-protocol transport
  substitutes for the `IsolateManager`/coordinator router, carrying the same `NetworkMsg`
  events — it must add no tick loop and must not bypass the enqueue-once activation path.

### Fidelity checklist for B2 (distributed unification across instances)

A distributed-unification scheme is faithful iff, for every remote variable binding, it:
1. arrives as a single `NetworkMsg`-equivalent event and triggers exactly one drain-flush;
2. reactivates suspended readers ONLY via `bindVariable`'s returned activations
   (enqueue-once);
3. treats each hop as an independent asynchronous leg (no synchronous round-trip assumption);
4. routes outbound only through `global_send` → `M_p` → `flushMessages`;
5. relies on monotonicity for correctness (per madGLP §9.2: Communicate = Reduce → Send →
   Receive) and never self-terminates agents.
