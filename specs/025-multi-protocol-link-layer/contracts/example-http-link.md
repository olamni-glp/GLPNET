# Worked example — producer/consumer split over HTTP (ILLUSTRATIVE, PROPOSED)

**Status: ILLUSTRATIVE. NOT runnable yet** — it uses the PROPOSED base link primitives
(`link_setup`/`server_listener`/`client_connector`/`link_send`/`link_recv`/`link_monitor`,
the approved `'_link_send'/3` kernel) whose runtime support is not built. It exists to make
the plan concrete and to be talked through. Every GLP clause is SRSW- and mode-checked by hand.

Demonstrates **SC-001**: the single-instance `producer(X)/consumer(X?)` program, split across
two REPL instances over HTTP as **one role-parameterized program** (FR-011), produces a result
**byte-identical** to the unsplit run.

---

## 0. The unsplit baseline (what we are splitting)

One REPL, one heap, one shared logic variable `X`:

```prolog
procedure produce_value(Integer).
produce_value(42).

procedure use_value(Integer?).
use_value(V) :- ground(V?) | '_output'(V?).     %% prints 42  ('_output'/1 = self.glp:73)

procedure go_unsplit.
go_unsplit :- produce_value(X), use_value(X?).    %% X: 1 writer, 1 reader — SRSW ok
```

`go_unsplit` prints `42`. The producer binds the shared writer `X`; the consumer reads `X?`.
The split must reproduce exactly this observable output.

---

## 1. The split — ONE role-parameterized program (FR-011)

Both nodes load the **same file** and boot it with their own ground `AgentId`. The shared
variable `X` is replaced by a **link**: the producer's value is ground-relayed across the cut
to the consumer's local reader.

```prolog
-module(http_split_demo).

%% ---- the one link identity both nodes compile in (ground, never reused) ----
%% "https" (TLS) because the two ends are on DIFFERENT hosts: FR-029 refuses a plain
%% "http" inter-host link by default; the "https" (TLS) variant is required. (On loopback
%% / co-located you could use "http".)
procedure demo_link(LinkId).
demo_link(link_id("https", ep("nodeB.example", 8443), 1)).

%% ---- one entry point; the ground AgentId selects the role (FR-011, the @/boot idiom) ----
procedure main(AgentId?).

%% SENDER node boots  main(producer):
main(Me) :-
    Me? =?= producer |
    demo_link(L),
    client_connector(L?, Link, Faults),          %% establish: HTTP client (connect)
    run_producer(Link?, Faults?).

%% RECEIVER/LISTENER node boots  main(consumer):
main(Me) :-
    Me? =?= consumer |
    demo_link(L),
    server_listener(L?, Link, Faults),           %% establish: HTTP server (listen)
    run_consumer(Link?, Faults?).

%% ---- producer side: compute the value, ground-relay it over the link ----
procedure run_producer(Link(_, _)?, FaultStream?).
run_producer(Link, _) :-
    produce_value(V),                            %% V: the value (was the shared X)
    link_send(V?, Link?, _).                %% cons ground V onto Out; host ships it

%% ---- consumer side: receive one value off the link, use it ----
procedure run_consumer(Link(_, _)?, FaultStream?).
run_consumer(Link, _) :-
    link_recv(V, Link?, _),                 %% SUSPEND until a frame arrives; bind V
    use_value(V?).                               %% prints 42 — identical to unsplit

procedure produce_value(Integer).
produce_value(42).

procedure use_value(Integer?).
use_value(V) :- ground(V?) | '_output'(V?).
```

SRSW/mode check (per clause): `Me` writer-in-head → `Me?` reader-in-guard (1, ground-implying
relaxation under `=?=`). `L` writer (`demo_link`) → `L?` reader (1). `Link`/`Faults` are produced
by `client_connector`/`server_listener` (output-hole idiom: reader hole in the head of those
clauses, writer in their body) and are read once each at the `main` call site; `run_producer`/
`run_consumer` read `Link?` once and IGNORE the fault stream with a **bare `_`** (an unread
position must be bare `_`, never a named `_Faults` — a named anon at an unused slot is rejected
by codegen). In `run_producer`: `V` writer (`produce_value`) → `V?` reader (`link_send`, 1);
`_Link1` anon writer (advanced channel discarded — single send). In `run_consumer`: `V` writer
(`link_recv` output) → `V?` reader (`use_value`, 1). All clean.

---

## 2. The two node views (same source, different active clause)

It is **one program, not a fork** (FR-011). Each node's boot AgentId commits one clause:

- **Sender node A** — boot goal `main(producer)` → clause 1 → `client_connector` (HTTP client)
  → `run_producer` (binds 42, `link_send`).
- **Receiver node B** — boot goal `main(consumer)` → clause 2 → `server_listener` (HTTP server)
  → `run_consumer` (`link_recv` suspends, then prints 42).

`Me? =?= producer` is the branch-on-ground-AgentId selector — three-valued: ground-equal
commits the clause; an unbound `Me?` would suspend; a mismatch falls to the next clause.

> Note (FR-004): which side *listens* is independent of which side *writes*. Here the connector
> (A) is the writer and the listener (B) the reader — the natural HTTP direction (client POSTs
> data to a server) — but the roles could be reversed.

---

## 3. End-to-end walkthrough

**(1) Boot — same source, two roles.** `http_split_demo.glp` is loaded on both nodes. Node B is
booted `main(consumer)`, node A `main(producer)`.

**(2) Link establishment (HTTP).**
- Node B runs `server_listener(L?, Link, Faults)` → `link_setup(L?, listener, Link, Faults)` →
  host `'_link_setup'` opens the **HTTP transport leaf in listener role**: starts an HTTP(S)
  server on `nodeB.example:8443`, registers the link in the per-instance LinkId→handle registry
  (idempotent — FR-007), mints local In/Out stream pairs + the `Faults` stream, and installs the
  per-link **host ingress** (fills `In`) and **egress drainer** (drains `Out`). Returns the
  `Link` channel + `Faults`. `run_consumer`'s `link_recv` then **SUSPENDS** on the unbound `In`
  head — three-valued *suspend*, not fail (FR-017/FR-050).
- Node A runs `client_connector(L?, Link, Faults)` → `link_setup(L?, connector, ...)` → host
  `'_link_setup'` opens the **HTTP leaf in connector role**: dials `nodeB.example:8443`, TLS
  handshake. Same ground `LinkId` on both ends ⇒ the two ends are the two halves of **one
  logical bilateral link** (FR-002/FR-005). Establishment role is independent of data direction
  (FR-004).

**(3) The send (ground-relay).**
- Node A: `produce_value(V)` binds `V = 42` (the value that, unsplit, was the shared `X`).
- `link_send(V?, Link?, _Link1)` matches `link_send(Msg, ch(In, [Msg?|Out?]), ch(In?, Out)) :-
  ground(Msg?) | true`. The guard `ground(Msg?)` certifies `42` is ground — **no `_w`/`_r`
  placeholder, no embedded reader crosses the wire** (the ground-relay invariant, FR-010/FR-040).
  The **head** conses it: `Out = [42 | NewOut]` (pure head construction, no `=` in body).
- The per-link **egress drainer** (host I/O installed by `'_link_setup'`) sees the `Out` bind,
  serializes `42` with the byte-parity `PayloadSerializer` into a **Frame** (+ per-link sequence,
  version byte, length/CRC — the reliability sublayer), and ships it as an **HTTP POST body** to
  `nodeB:8443` over the established connection.
  - *(Equivalent LinkId-keyed face, OQ-1 ruled `sound`:)* had the program used
    `out_relay(V?, L?, ToPeer?)` instead of the channel face, its body would call the approved
    **`'_link_send'/3`** kernel, which serializes + hands the same ground frame to the HTTP leaf
    by LinkId. Same wire outcome; different surface.

**(4) The receive (ingress → reactivation).**
- Node B's HTTP server receives the POST body → the per-link **host ingress** deserializes the
  Frame back to the ground term `42`, runs the **FR-021 dedup gate** (sequence + global-name →
  first delivery, not a duplicate → not the crash path), and binds the `In`-stream tail:
  `In = [42 | NewIn]` via `bindVariable`/`bindWriter` — **writer-MGU on the LOCAL `In`-tail
  writer**, never reader/reader (FR-049).
- That bind **reactivates the suspended `link_recv` exactly once** (FR-017/FR-051): it matches
  `link_recv(Msg?, ch([Msg|In], Out?), ch(In?, Out))`, capturing `Msg = 42`, returning `V = 42`.
- `use_value(V?)` → `ground(V?)` passes → `'_output'(42)` prints **42** — byte-identical to the
  unsplit run (SC-001).

**(5) Faults (if monitored).** Replace the ignored `_` fault arg with a real reader and a watcher clause; a
disconnect surfaces as ground terms `tempFail(LinkId, Reason)` then `permFail(LinkId, Reason)`
on the monitor stream, read with ordinary guards — **never** a logical fail (FR-043/044/050).
An unmonitored `link_recv` simply stays safely suspended across a disconnect.

**(6) Why it is faithful.** The shared variable `X` became: producer binds a LOCAL value → the
ground-relay ships a COPY → the consumer's LOCAL reader is bound by the ingress. Each instance
keeps its own SRSW writer/reader pair; only local writers are bound (writer-MGU); an un-arrived
value is a suspended local reader (three-valued); per-link FIFO holds (Out cons order = HTTP send
order = In bind order). The split is observationally equal to the single-heap run.

---

## 4. HTTP transport-leaf specifics (the uniform seam adapted to HTTP)

The leaf adapts the uniform seam `open / send-bytes / recv-bytes / close + fault` (FR-058) to
HTTP's request/response shape:

- **connector (A)** = an HTTP **client**: each `link_send` frame = one **POST** request body;
  the HTTP **response** carries the per-frame **ack** that feeds the reliability sublayer's
  at-least-once/dedup (FR-021/FR-023).
- **listener (B)** = an HTTP **server**: each received POST body = one inbound frame handed to
  the ingress; it replies with the ack (and, for the reverse B→A direction, may carry a B→A
  frame in the response body, or use long-poll / HTTP/2 streams for a continuous back-channel).
- **TLS-by-default (FR-029):** inter-host ⇒ `"https"`; a plain `"http"` inter-host link is
  refused unless an explicit opt-out is set. Loopback/co-located may use `"http"`.
- **per-link FIFO over a broker/relay (FR-023/FR-053):** if the path runs through an HTTP proxy,
  the sequence/dedup sublayer — not the proxy — guarantees in-order, exactly-once-effective binds.

Because the transport detail lives entirely below the seam, the GLP program in §1 is **unchanged**
if you switch `"https"` to `"ws"`, `"mqtt"`, `"coap"`, … — only the `Scheme` inside `LinkId`
changes (FR-006/FR-013).

---

## 5. Invariants preserved (checklist)

| Invariant | Where preserved in this example |
|---|---|
| SRSW per instance (FR-048) | each clause hand-checked §1; each side has its own local pair |
| writer-MGU (FR-049) | ingress binds only the local `In`-tail writer |
| three-valued / suspend-not-fail (FR-017/050) | `link_recv` suspends on unbound `In` head, never fails |
| reactivate exactly once (FR-051) | one ingress bind wakes the suspended `link_recv` once |
| bind-once monotonic (FR-052) | dedup gate makes a redelivered frame a no-op |
| per-link FIFO (FR-018/053) | `Out` cons order = HTTP send order = `In` bind order |
| ground-relay, no placeholder on wire (FR-010/040) | `ground(Msg?)` gate in `link_send` |
| faults are data, not a verdict (FR-043) | `tempFail`/`permFail` ground terms on the monitor stream |
| one program, not a fork (FR-011) | single `main/1`, role by ground AgentId |
