---
title: "madGLP Specification v5.3 (Multiagent Deterministic GLP)"
authors: "Claude (per Document History); derived from Shapiro et al. CGLP Paper, Section 7"
year: "2026"
source_url: "file:///D:/bstdev/research/glp/glpnet/docs/ma/madGLP-spec.md"
retrieved: "2026-06-06"
fetched_for: "glp-local-model — the exact GLP/FCP runtime model (cell representation, writer-MGU, suspension/reactivation, three-valued unification, channels, cross-module routing, madGLP cross-isolate variables) that any distributed link-layer scheme must preserve. Fidelity yardstick for blocker B2 (distributed unification). — Fetch, preserve & extract source: madGLP Specification v5.3"
precedence_class: glp-current
access: full-text
---

# Extraction: madGLP Specification v5.3

## Why this is the primary B2 yardstick

This is the **authoritative local GLP spec** (precedence class `glp-current`, the HIGHEST
authority under SOURCE PRECEDENCE) for the exact mechanism a distributed multi-protocol
link layer must preserve: how a maGLP **shared writer/reader pair** that spans agent
boundaries is realized as **two fully-local pairs connected by a global link** carried by
message passing. Any new link primitive (MQTT/AMQP/CoAP/HTTP/WebSocket/BLE/... transport)
that "splits a writer X and reader X? across two REPL instances" must implement *exactly
this transform* to be faithful to GLP semantics. This document is the fidelity test for
blocker **B2 (distributed unification)**.

The spec self-identifies its provenance: "Source: CGLP Paper (`~/Grassroots/CGLP`),
Section 7 'Multiagent Deterministic GLP (madGLP)'". Status: DRAFT, dated 2026-02-10.
Per SOURCE PRECEDENCE, this local spec governs current implementation truth and is not
overridden by earlier Shapiro CL papers; no external fetch can supersede it.

---

## 1. Core transform (the B2 mechanism, verbatim)

> "This document specifies **Multiagent Deterministic GLP (madGLP)**, an
> implementation-ready transition system that implements maGLP using only local variable
> pairs connected by global links. While maGLP defines shared variable pairs that span
> agent boundaries, madGLP replaces each such pair with two fully local pairs connected
> through a global writers table and message passing, with forwarding handled by spawned
> GLP goals." (§1)

**Local Pairs with Global Links** (§1.1, verbatim):

> "A maGLP shared variable pair `(X, X?)` with writer X at agent p and reader X? at agent
> q is implemented by two local pairs connected by a global link:
> - At agent p: a local pair `(X_p, X_p?)` where both variables remain in p's resolvent
> - At agent q: a local pair `(X_q, X_q?)` where both variables remain in q's resolvent
> - A global link connecting X_p to X_q, realized as a `global_send` goal at the
>   writer-owner and an entry in the reader-owner's global writers table"

**Push-Based Communication** (§1.1, verbatim):

> "When the writer-owner assigns a term T, a spawned `global_send` goal detects this (when
> the paired reader becomes known) and sends an assignment message to the reader-owner.
> Upon receipt, the reader-owner looks up the target writer in its global writers table,
> assigns it T↓, and removes the entry."

**Uniform Forwarding** (§1.1): "All outgoing communication is handled by `global_send`
goals, including forwarding when both ends of a variable pair are exported."

This is the load-bearing claim for the link layer: **the shared logic variable is replaced
by (a) a local pair on each side + (b) a directed `global_send` goal on the writer side +
(c) a global-writers-table entry on the reader side, joined by a transport-agnostic
assignment message.** A new transport (MQTT, WebSocket, BLE, etc.) is just the carrier for
that assignment message; the GLP-level transform is fixed.

---

## 2. Vocabulary (§1.2)

- **Local variable pair**: writer X + paired reader X? both in the same agent's resolvent.
- **Global variable name**: a term `_w(p, i)` or `_r(p, i)` identifying a variable exported
  by agent p at index i. Appears **only in messages, never in resolvents** (§2).
- **Global link**: the combination of a `global_send` goal at one agent and a global
  writers table entry at another.
- **Global writers table**: a table tracking writers that await incoming assignments from
  remote agents.

---

## 3. Global variable names (§2, verbatim)

> "A global variable name is a term of the form `_w(p, i)` or `_r(p, i)`, where:
> - `p ∈ Π` is an agent identifier
> - `i ∈ ℕ` is an index allocated by p during globalization
> - `_w(p, i)` denotes a writer globalized at p
> - `_r(p, i)` denotes a reader globalized at p"

These names "appear only in messages between agents, never in resolvents. They identify the
source of a global link and enable message routing." (§2)

---

## 4. Global writers table W_p (§3)

**Entry types** (§3.1):
- **(X, q)** — created by Globalize: local writer X assigned when an assignment message
  arrives from agent q.
- **(X, q, i)** — created by Localize: local writer X, remote agent q, remote index i
  (needed to match incoming messages).

**Structure / index allocation** (§3.2):
- W_p is an indexed array of entries.
- **Single counter per agent**, shared across Globalize and Localize.
- **Index 0 reserved** for the network input serializer (cold-calls).
- Counter starts at 1; **indices never reused**.
- "What the Table Stores": only writers awaiting incoming assignments. **No entries for
  outgoing links** — those are `global_send` goals.
- **Entry Removal**: removed when assignment arrives and the writer is bound — except the
  index-0 serializer entry, which is permanent. Gaps allowed; sparse map permitted.

---

## 5. The global_send predicate (§4, verbatim)

```prolog
global_send(T, G, Q) :- known(T) | '_send'(T, G, Q).
```

> "where:
> - T is the reader whose value will be sent when known
> - G is the global variable name (`_w(p,i)` or `_r(p,i)`) identifying the link
> - Q is the destination agent
>
> The guard `known(T)` succeeds when T is bound to a non-variable term. The builtin
> `'_send'(T, G, Q)` globalizes T and adds message `(G := T↑, Q)` to the agent's outgoing
> message set." (§4)

**Forwarding via global_send** (§4): when an agent exports both ends of a pair, Globalize
creates an entry for the exported writer and spawns a `global_send` for the exported
reader; a value arriving on the writer's link makes X? known, triggering the watching
`global_send`, automatically forwarding — "without requiring special forwarding logic in
the Receive transaction."

### 5.1 Index-0 serializer (cold-calls) (§4.1, load-bearing for "rerouting stdin/stdout")

> "At boot time, each agent p creates a permanent entry at index 0 mapping `_r(p, 0)` to
> the local writer N_p for p's network input stream. This entry is never removed."

Cold-call mechanism (verbatim):

> "To send a cold-call message T to agent q, any agent p uses `global_send(T, _w(q,0), q)`.
> This sends the assignment `_w(q,0) := [T↑ | _w(q,0)]`, wrapping the content in a list
> cell and reusing the serializer writer in the tail."

- **Index 0 = many-to-one merge** (multiple senders), writer reused in tail, permanent
  entry, each message extends the network input stream by one element.
- **Index > 0 = one-to-one**, single use, entry removed after assignment, T sent directly
  (not list-wrapped).
- "Remark [Serializer as Merge]": order across senders is non-deterministic; **same-sender
  messages preserve FIFO order.**

Network output processor (verbatim):

```prolog
send_to_net([msg(Q, T) | In]) :-
    global_send(T?, _w(Q,0), Q?), send_to_net(In?).
send_to_net([]).
```

---

## 6. Globalize and Localize (§5) — the directional core (corrected in v5.3)

### Globalize (T_p↑) by p toward remote q (§5.1, verbatim)

> "1. **If Y is a writer**: allocate the next index i, replace Y in T_p↑ with `_w(p, i)`,
>    and create entry `(Y, q)` at index i in W'_p. No goal is spawned—p will receive the
>    assignment on this link (q gets the writer and will send the value back).
>
> 2. **If Y? is a reader**: allocate the next index i, replace Y? in T_p↑ with `_r(p, i)`,
>    and spawn goal `global_send(Y?, _r(p,i), q)` into p's resolvent. No entry is created—
>    the `global_send` goal handles outgoing communication (p keeps the writer and will
>    send the value)."

### Localize (T_q↓) by q from remote p (§5.2, verbatim)

> "1. **If `_w(p, i)`**: create fresh local pair `(Y_q, Y_q?)`, replace `_w(p, i)` with
>    Y_q (the writer) in T_q↓, and spawn goal `global_send(Y_q?, _w(p,i), p)` into q's
>    resolvent. No entry is created—the `global_send` goal handles outgoing communication
>    (q gets the writer and will send the value to p).
>
> 2. **If `_r(p, i)`**: create fresh local pair `(Z_q, Z_q?)`, allocate the next index k
>    in W'_q, add entry `(Z_q, p, i)`, and replace `_r(p, i)` with Z_q? (the reader) in
>    T_q↓. No goal is spawned—q will receive the assignment on this link (p keeps the
>    writer and will send the value)."

**Correspondence rule (the directional invariant, §5.3):**
- **Writer globalized at p** → entry at p; fresh pair at q with the *writer* end + a
  `global_send` at q pointing back to p. q assigns → value flows to p → routed to Y → X?.
- **Reader globalized at p** → `global_send` at p; fresh pair at q with the *reader* end +
  entry at q. p assigns → `global_send` fires → value flows to q → routed to Z_q → Z_q?.

**v5.3 correction (Document History):** "Writer → entry at globalizer (receiver gets
writer, sends back). Reader → gs at globalizer (globalizer keeps writer, sends to
receiver)." This is the corrected polarity; earlier drafts had it swapped. **Implementers
must use the v5.3 directions.**

---

## 7. Local state, transition system, transactions (§6–§8)

**Local state** s_p = (R_p, W_p, M_p) (§6.1):
- Resolvent R_p = (A_p, S_p, F_p): active-goal queue / suspended goals paired with blocking
  readers (S_p ⊆ 𝒜 × 2^(𝒱?)) / failed goals.
- W_p: global writers table.
- M_p: pending outgoing messages, pairs `(m, q)` with m of form `_w(a,i) := T` or
  `_r(a,i) := T`.

**Initial configuration c₀** (§7): each agent boots with
`A_p = [agent(p, ch(_?, _), ch(_?, _))]`, empty S/F/M, and `W_p = {(N_p, *) at index 0}`
(serializer entry).

**Three transactions, ALL UNARY** (§8, §9.1):
- **Reduce** (§8.1): standard GLP three-phase reduction with Success / Suspend / Fail
  cases; "The Reduce transaction does not directly generate outgoing messages" — assigning
  a writer makes its reader known, which triggers a watching `global_send`, whose reduction
  (via `'_send'`) adds the message to M_p.
- **Send** (§8.2): when `(m, q) ∈ M_p`, remove it and place m in the channel to q. Wire
  format carries destination q in header; body carries the assignment.
- **Receive** (§8.3): three cases — normal `_w(p,i) := T↑` (i>0), serializer
  `_w(q,0) := [T↑ | _w(q,0)]`, and `_r(p,i) := T↑`. Each: localize T↑ (baking remote
  identity into nested links), assign the local writer, propagate `{X? := T↓}`, reactivate
  suspended goals, remove entry (serializer entry is *updated*, not removed).

**Correspondence to maGLP** (§9.2, verbatim): "The maGLP binary Communicate transaction,
which atomically transfers an assignment from one agent's writer to another agent's reader,
is implemented in madGLP by the sequence: Reduce (assigns writer, triggering `global_send`)
→ Send → Receive (applies assignment). The correctness of this implementation relies on
monotonicity."

---

## 8. Invariants (§13) — the B2 fidelity checklist (verbatim)

> - **SRSW Property**: Within any agent's resolvent, each variable occurs at most once as a
>   reader and at most once as a writer (inherited from GLP).
> - **Entry Lifecycle**: Every global writers table entry is created exactly once (by
>   Globalize or Localize) and removed exactly once (by Receive). An entry is never
>   modified between creation and removal.
> - **Send Atomicity**: When a `global_send` goal fires, the globalization of its term
>   value (which may spawn additional `global_send` goals for nested variables) and the
>   addition of the message to M_p occur atomically within the same Reduce transaction.
> - **Index Uniqueness**: Each (agent, index) pair uniquely identifies a global name.
>   Indices are allocated sequentially and never reused, even after entry removal.
> - **Message Ordering**: Messages between any pair of agents are delivered in FIFO order.
>   This ensures that if agent p sends two messages to agent q, q receives them in the
>   order sent."

**Implication for the multi-protocol link layer:** any transport substituted for the
message carrier MUST provide **per-peer FIFO delivery** and preserve **monotonicity**
(values only flow forward, bindings never retracted). Transports that reorder or duplicate
(e.g., CoAP non-confirmable, MQTT QoS 0, lossy BLE) need an ordering/dedup layer to satisfy
§13 Message Ordering + §9.2 monotonicity. Broadcast transports (BLE LE-Audio BIS — open
sub-question T2) conflict with SRSW (one reader per variable) and with the one-to-one
index>0 link semantics; only the index-0 many-to-one *merge* is sanctioned, and even that
keeps a single network-input writer.

---

## 9. Heap representation (§11.3, verbatim) — preserves FCP cell model

> "Local variable pairs use standard two-cell allocation:
> - Writer cell: WrtTag, content is null (unbound), SuspensionListNode (waiting), or
>   Pointer (bound)
> - Reader cell: RoTag, content is Pointer to writer cell
>
> No special representation is needed for 'imported' variables—all variables are local
> pairs. The global writers table provides routing information separately from the heap
> representation." (§11.3)

This is the critical fidelity point for B2: **distribution does not change the heap cell
model.** Remote-ness lives entirely in W_p + `global_send` goals + serialized global names,
NOT in the cell tags. Writer-MGU, suspension on the writer cell, and reactivation on bind
remain exactly as in single-agent GLP. The link layer is a routing overlay, not a new cell
type.

---

## 10. Serialization wire format (§11.4, verbatim)

> "Terms crossing agent boundaries are serialized with global names substituted for
> variables. The serialization format must preserve:
> - Functor/arity structure
> - Global name encoding: type tag + agent identifier + index
> - Constants: type tag + value bytes" (§11.4)

This is the transport-neutral payload contract the multi-protocol link layer must carry
unchanged across MQTT/AMQP/CoAP/HTTP/WebSocket/BLE/file endpoints.

---

## 11. The '_send' builtin (§11.5) — serializer vs normal

- **G = `_w(q, 0)` (serializer)**: globalize T for Q; add
  `(_w(q,0) := [T↑ | _w(q,0)], Q)` to M_p (list-wrapped, writer reused in tail).
- **G = `_w(p,i)` / `_r(p,i)`, i>0 (normal)**: globalize T for Q; add `(G := T↑, Q)`.
- Destination Q baked into nested links: writer Y in T → entry `(Y, Q)`; reader Y? in T →
  spawn `global_send(Y?, _r(p,i), Q)`.
- Invoked only when the `global_send` guard `known(T)` succeeds.

---

## 12. External I/O (§12) — relevant to "reroute stdin/stdout/stderr to a remote REPL"

- **Network output** `send_to_net/1` (§12.2): reads `msg(Q, T)` and calls
  `global_send(T?, _w(Q,0), Q?)` — cold-call via the serializer. Unified with established
  links (same 3-arg `global_send`, only the target address differs).
- **Network input** (§12.3): handled by Receive's serializer case; the GLP agent reads its
  network input stream like any other stream; localized term contains only local variables.
  "Correctness relies on monotonicity—once global links are established, values flow
  forward."
- **UI output** `send_to_ui/1` (§12.4): local `'_send_to_ui'` builtin, guard `ground(X?)`,
  NO globalization, NOT routed through M_p. (Local-isolate analogue of an stdout reroute.)
- **UI Agent + writer binding** (§12.7): interactive query-response by sending a term with
  an unbound **writer** to the UI; user binds it (e.g., `X35 = accept(Ch)`); `no_readers/1`
  guard (writers OK, readers not) gates output for interactive queries vs `ground/1` for
  final values.

This section is the local precedent for the feature's "reroute stdin/stdout/stderr to a
remote REPL": the existing model already separates a *globalized* network channel (carries
variables across instances) from a *ground-only* local UI channel. A remote-REPL stdio
reroute is a new endpoint kind that must choose: globalized (variables cross — full
link-layer semantics) vs ground/no_readers (byte/text only). The spec gives both guards.

---

## 13. Reserved constants (§15)

`'_user'`, `'_net'`, `'_w(p,i)'`, `'_r(p,i)'` are system-reserved; underscore-prefixed
constants are rejected in user mode; system code uses `-mode(system).`. Rationale: prevent
collisions between user agent identifiers and system channels.

---

## 14. Provenance, status, supersession (§16–§17)

- **Version 5.3**, Date 2026-02-10, **Status DRAFT**.
- Source: CGLP Paper (`~/Grassroots/CGLP`), Section 7.
- Supersedes `archive/irmaGLP-spec-v3.1-2026-01-30.md` (request-based model).
- Related: `/docs/glp-runtime-spec.txt` (single-agent runtime),
  `/docs/glp-bytecode-v216-complete.md` (bytecode).
- v5.3 changelog: **corrected Globalize/Localize direction** (writer→entry at globalizer;
  reader→`global_send` at globalizer) across §§5.1–5.4, 8.3, 9.3–9.4, 10.1–10.3, 11.2,
  11.5, 12.2 — "Aligns with corrected paper appendix."

---

## 15. Direct answer to the question

**Fetched & preserved:** the authoritative local madGLP Specification **v5.3** (the file at
`docs/ma/madGLP-spec.md`), which is itself the requested source — there is no higher
authority to fetch (precedence `glp-current`; SOURCE PRECEDENCE rule (1)). Its upstream is
the (private, repo-local) CGLP Paper Section 7; no public Shapiro paper supersedes it for
current implementation truth.

**What it establishes for B2 (distributed unification):** a maGLP cross-agent shared pair
`(X, X?)` is split into two fully-local FCP-style pairs joined by a **global link** =
`global_send` goal (writer side) + global-writers-table entry (reader side), carried by
serialized **assignment messages** (`_w(p,i):=T↑` / `_r(p,i):=T↑`). Distribution is a
routing overlay; the **heap cell model, writer-MGU, suspension/reactivation, and SRSW are
unchanged**. Correctness rests on **monotonicity** + **per-peer FIFO**. Cold-call /
bootstrap uses a permanent **index-0 serializer** (many-to-one merge, list-extended network
input stream). This is the exact transform a multi-protocol link primitive must reproduce,
and the §13 invariants + §11.3 heap rule are the fidelity yardstick.
