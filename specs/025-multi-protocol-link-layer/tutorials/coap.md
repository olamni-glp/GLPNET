# Transport tutorial — `coap` (CoAP over UDP, RFC 7252) link leaf (ILLUSTRATIVE, PROPOSED)

**Status: PLAN-STAGE, PRE-IMPLEMENTATION. NOTHING here is runnable yet.**
Every link primitive used below (`link_setup` / `server_listener` / `client_connector` /
`request_link` / `accept_link` / `link_send` / `link_recv` / `link_monitor` / `link_close`,
and the host kernel `'_link_send'/3` plus the system-predicates `'_link_setup'/5`,
`'_link_request'/5`, `'_link_accept'/5`, `'_link_monitor'/2`, `'_link_close'/2`) is **PROPOSED,
pending Gabi's language-authority approval** (CLAUDE.md §Language Authority; DISCIPLINE §1.14;
DESIGN-DOSSIER §0/§2). All GLP in §3 is **illustrative**, hand-checked for SRSW and modes, with
writer-mode outputs constructed in clause HEADS (never via `=` in the body — GLP is not Prolog).
All tests in §4–§5 are **spec-level**: a scenario + exemplar GLP + an expected OBSERVABLE outcome
+ a pass/fail oracle, made runnable only once the primitives and the `coap` leaf land.

**This unit's transport:** the `"coap"` scheme — the Constrained Application Protocol (RFC 7252)
over UDP, REST-like, designed for constrained nodes/networks. A→B carries via PUT/POST; the B→A
back-channel rides CoAP OBSERVE (RFC 7641, server-push notifications). The small datagram budget
(~1 KB payload) drives the reliability sublayer's fragment/reassemble via CoAP blockwise transfer
(RFC 7959, FR-022). Confirmable (CON) messages give transport-level ack/retransmit; our per-link
seq/dedup does ordering + duplicate absorption *on top* (FR-020/FR-021). DTLS is the secure variant
(FR-029). The base link is always **peer-to-peer to the immediate peer**; any broker/relay/border
router is at another level and is **out of scope here** (DESIGN-DOSSIER §1 MQTT clarification, §6).

---

## 1. Scenario — a battery-constrained IoT sensor mesh reporting to a collector

### 1.1 The real-world deployment this models

The headline use case is the canonical CoAP one: large numbers of cheap, **battery-powered**
sensor nodes (8-bit/16-bit microcontrollers, kilobytes of RAM/ROM, radios running at tens of
kbit/s with high packet-error rates) that periodically report readings to a **collector**. CoAP
was designed precisely for these "constrained nodes and constrained networks" — see RFC 7252 §1
([RFC 7252](https://datatracker.ietf.org/doc/html/rfc7252)); CoAP runs over connectionless UDP and
is "functionally modeled after HTTP" for machine-to-machine smart-energy / building-automation /
environmental scenarios
([A1 Digital CoAP overview](https://www.a1.digital/knowledge-hub/what-is-the-constrained-application-protocol-coap/),
[Nordic Developer Academy — CoAP](https://academy.nordicsemi.com/courses/cellular-iot-fundamentals/lessons/lesson-5-cellular-fundamentals/topic/lesson-5-coap-protocol/)).

A grounded, published instance: the **Cairngorm mountains (Scotland) environmental sensor network**
— battery-powered field nodes measuring stream/peat/periglacial conditions, running **Contiki OS +
6LoWPAN over an 868 MHz radio + RPL routing + CoAP over UDP**, with readings routed toward a
border router/collector. The authors found "the use of UDP packets with CoAP proved to be an energy
efficient application layer," validating CoAP for large battery-constrained deployments
([Deploying a 6LoWPAN, CoAP, low power, wireless sensor network — ePrints Soton / ACM SenSys 2016](https://eprints.soton.ac.uk/400440/),
[ACM DL](https://dl.acm.org/doi/10.1145/2994551.2996707)). The same protocol stack is the standard
recommendation for smart-agriculture soil-moisture/temperature/humidity monitoring
([6LoWPAN + CoAP environmental monitoring, ScienceDirect](https://www.sciencedirect.com/science/article/pii/S1877050922010870),
[Unlocking 6LoWPAN Potential](https://www.numberanalytics.com/blog/ultimate-guide-6lowpan-wireless-sensor-networks)).

### 1.2 The slice we model (strictly peer-to-peer)

For feature 025 the modelled link is **one bilateral CoAP link between exactly two GLP REPL
instances**: a **sensor** instance (`A`, the data producer) and a **collector** instance (`B`, the
data consumer). The sensor pushes a stream of readings; the collector consumes them and, over the
**same link's reverse direction**, hands back **flow-control credit** (the bounded-pipe credit/
back-channel unification, DESIGN-DOSSIER §3) so a fast sensor cannot outrun a slow/constrained
collector. In a real mesh many sensors route through an RPL border router to the collector; that
border router is a **transport relay at another level and is out of scope** — at this level each
sensor sees exactly one P2P link to its immediate peer (DESIGN-DOSSIER §6; spec FR-005).

### 1.3 Why CoAP fits this transport seam

- **Constrained / battery / lossy** — the design target of CoAP; minimal header, UDP, no
  connection setup, energy-efficient (RFC 7252 §1; ePrints Soton finding above).
- **REST-like push** — A→B sensor readings map naturally onto PUT/POST to a resource on the
  collector (RFC 7252 §5.8.2: POST creates/updates a resource).
- **Native server-push back-channel** — OBSERVE (RFC 7641) gives the B→A reverse direction the link
  layer needs (faults, credits, replies) without polling, fitting battery duty-cycling.
- **Reliability knobs already present** — CON messages provide stop-and-wait ack/retransmit
  (RFC 7252 §4.2); Message-ID dedup (§4.5) and blockwise transfer (RFC 7959) give the substrate our
  seq/dedup + fragment/reassemble sublayer rides on.
- **Secure variant** — DTLS (CoAPs, port 5684) is the standard secure binding (RFC 7252 §9.1).

**Sources (§1):**
[RFC 7252 (CoAP)](https://datatracker.ietf.org/doc/html/rfc7252) ·
[RFC 7641 (Observe)](https://datatracker.ietf.org/doc/html/rfc7641) ·
[RFC 7959 (Block-Wise Transfers)](https://www.rfc-editor.org/rfc/rfc7959) ·
[RFC 6347 (DTLS 1.2)](https://datatracker.ietf.org/doc/html/rfc6347) ·
[ePrints Soton — Cairngorm 6LoWPAN/CoAP deployment](https://eprints.soton.ac.uk/400440/) ·
[ACM DL — same](https://dl.acm.org/doi/10.1145/2994551.2996707) ·
[ScienceDirect — 6LoWPAN+CoAP environmental monitoring](https://www.sciencedirect.com/science/article/pii/S1877050922010870) ·
[A1 Digital — What is CoAP](https://www.a1.digital/knowledge-hub/what-is-the-constrained-application-protocol-coap/) ·
[Nordic Academy — CoAP](https://academy.nordicsemi.com/courses/cellular-iot-fundamentals/lessons/lesson-5-cellular-fundamentals/topic/lesson-5-coap-protocol/) ·
[Nordic Academy — DTLS for CoAP](https://academy.nordicsemi.com/courses/cellular-iot-fundamentals/lessons/lesson-5-cellular-fundamentals/topic/lesson-5-exercise-2/) ·
[circuitlabs — CoAP block-wise transfers](https://circuitlabs.net/coap-block-wise-transfers/) ·
[circuitlabs — CoAP Observe in ESP-IDF](https://circuitlabs.net/coap-observe-pattern-in-esp-idf/).

---

## 2. Protocol mapping — the uniform seam over CoAP

The leaf adapts the uniform host seam **`open / send-bytes / recv-bytes / close + fault`**
(architecture-context §3 `ILinkTransport`/`ILinkEndpoint`, FR-058) onto CoAP's request/response +
Observe shapes. Nothing CoAP-specific leaks above the seam: the GLP program in §3 is unchanged from
the HTTP/WS examples except for the `Scheme` string `"coap"`/`"coaps"` inside the `LinkId`
(FR-006/FR-013; DESIGN-DOSSIER §5).

### 2.1 Endpoints and roles (establishment path A: listen/connect)

| Link role | CoAP role | What it does |
|---|---|---|
| `server_listener` (B, collector) | CoAP **server** | Binds a UDP socket on `host:port` (default 5683, or 5684 for DTLS), exposes a resource path (e.g. `/glp/<LinkId>`) that accepts inbound A→B frames as POST/PUT bodies, and serves the B→A stream as an **observable** resource (RFC 7641). |
| `client_connector` (A, sensor) | CoAP **client** | Knows the collector's `coap://host:port/glp/<LinkId>` URI; sends A→B frames as CON POST/PUT requests; registers an OBSERVE on the collector's reverse resource to receive the B→A back-channel. |

Establishment role is **independent of data direction** (FR-004): the collector listens but is the
*reader*; the sensor connects but is the *writer*. CoAP has no connection handshake (it is
connectionless UDP), so "establishment" here = the server having a bound socket + registered
resource, and the client knowing the URI and (for the back-channel) completing one OBSERVE
registration. Same ground `LinkId` on both ends ⇒ one bilateral link (FR-002/FR-005/FR-007).

### 2.2 `send-bytes` (A→B forward path)

`link_send(Ground, Link, Link')` conses a **ground** term onto the link's `Out` stream in the head
(`ground(Msg?)` gate ⇒ no `_w`/`_r` placeholder, no embedded reader ever crosses — the ground-relay
invariant, FR-010/FR-040). The per-link **egress drainer** serializes the term (byte-parity
`PayloadSerializer` + reliability framing: per-link sequence, version byte, length/CRC) into one or
more CoAP message payloads and emits them as **CON POST/PUT** requests to the collector's resource:

- **CON (Confirmable)** ⇒ the CoAP layer retransmits at exponentially increasing intervals until it
  receives the matching ACK or runs out of attempts (RFC 7252 §4.2). This is the **transport** ack;
  it is *below* our sublayer.
- The collector's response is normally **piggybacked** in the ACK (RFC 7252 §5.2.1), or, when it
  cannot answer immediately, an empty ACK followed by a **separate** CON response (§5.2.2).
- For low-value/high-rate readings, **NON (Non-confirmable)** may be used (RFC 7252 §4.3); then our
  seq/dedup + retransmit-on-`tempFail` carry reliability entirely above the transport.

### 2.3 `recv-bytes` (B→A back-channel via OBSERVE)

CoAP is client-initiated, so the **B→A reverse direction needs a server-push mechanism**: the
client A registers interest with an extended GET carrying the **Observe Option** (RFC 7641); the
collector then sends each reverse-direction item as an **additional CoAP response** to that single
GET, carrying A's original **Token** so A correlates them, plus an Observe sequence number for
reorder detection ([RFC 7641](https://datatracker.ietf.org/doc/html/rfc7641),
[circuitlabs Observe](https://circuitlabs.net/coap-observe-pattern-in-esp-idf/)). Each notification
is decoded and dispatched to `handleMadAssignment`, which binds the local `In`-tail writer
(writer-MGU), reactivating a suspended `link_recv` exactly once (FR-017/FR-049/FR-051).

**The reverse direction carries three multiplexed things — all as Observe notifications:**
1. application B→A data (e.g. collector acknowledgements / commands),
2. **flow-control credits** (`more`) for the bounded pipe (DESIGN-DOSSIER §3 KEY INSIGHT: credits
   and the B→A back-channel are ONE mechanism),
3. the **fault monitor** terms (`ok` / `closed` / `tempFail` / `permFail`).

> OQ (carried from DESIGN-DOSSIER OQ-F3): whether one Observe stream multiplexes data + credits +
> faults, or separate observable resources per concern. This tutorial illustrates a single reverse
> Observe stream for data+credit and a **separate** observable resource for the monitor (so a goal
> can read faults without touching data — FR-008 independent observability). See §7.

### 2.4 MTU / fragmentation + reliability (FR-022, RFC 7959)

CoAP datagrams "SHOULD fit within a single IP packet (avoid IP fragmentation)"; with unknown header
sizes, good upper bounds are **1152 bytes message / 1024 bytes payload**, assuming a 1280-byte IPv6
MTU (RFC 7252 §4.6). 6LoWPAN adaptation-layer fragmentation is far tighter (~60–80 bytes per frame)
([circuitlabs block-wise](https://circuitlabs.net/coap-block-wise-transfers/)). A serialized GLP
term frequently exceeds these budgets, so the leaf uses **blockwise transfer (RFC 7959)**:

- **Block1** carries the request body (A→B) in numbered blocks; **Block2** carries the response /
  Observe body (B→A) in numbered blocks (RFC 7959 §2). NUM=0 is the first block of a body.
- The reliability sublayer's **fragment/reassemble** (FR-022) is realized by mapping one serialized
  frame onto a Block1/Block2 sequence; the receiver reassembles before handing one complete frame
  to the ingress. This satisfies "over-MTU payloads fragment and reassemble" (spec Edge "Cyclic /
  oversized / forged frames"; SC-012 fragmentation aspect).
- **Maximum chunk for safety, minimum one byte for progress** (DESIGN-DOSSIER §3 credit
  granularity): block size is bounded so a malformed/oversized/huge-arity frame fails safe within
  bounded memory (FR-028); a non-empty block always advances (no zero-window deadlock).

### 2.5 Sequencing / dedup on top of CoAP (FR-020/FR-021)

CoAP's own **Message ID** (16-bit) lets a recipient detect a duplicate CON/NON and acknowledge it
while processing the request only once (RFC 7252 §4.5). That is hop-level dedup; UDP can still
reorder/drop/duplicate across retransmits and Observe re-registration. So **our per-link
sequence + global-name dedup** runs above it (FR-020): a redelivered bind frame is a **verified
no-op** (FR-021/SC-008 — today a duplicate crashes the agent; the dedup gate must short-circuit it
*before* `handleMadAssignment`). A single sequence number detects but does not restore order, so a
**reorder buffer** reconstructs per-link FIFO (FR-018/FR-053; architecture-context §4.2).

### 2.6 TLS/security variant — DTLS (FR-029)

The secure scheme is **`"coaps"` = CoAP over DTLS** (RFC 6347), default UDP port **5684**
([Nordic — DTLS for CoAP](https://academy.nordicsemi.com/courses/cellular-iot-fundamentals/lessons/lesson-5-cellular-fundamentals/topic/lesson-5-exercise-2/)).
Handshake auth modes: **PSK** (lightweight, the constrained-device default), **Raw Public Key**, or
**X.509 certificate** (RFC 7252 §9.1). Per FR-029, an **inter-host** CoAP link MUST default to DTLS:
an inter-host `"coap"` (plain) open is **refused** unless an explicit, deliberate opt-out is set;
`"coaps"` succeeds. Loopback/co-located links may use plain `"coap"` (architecture-context;
spec FR-029 "inter-host" definition).

### 2.7 Graceful close (`[]`) vs abrupt (`link_close`)

- **Graceful (default): stream-end `[]`.** The sensor binds its `Out` tail to `[]`; the egress
  drainer emits a final frame marking end-of-stream, then **deregisters the OBSERVE** (RFC 7641: a
  client cancels by sending a GET with Observe=deregister, or by RST'ing/forgetting a notification —
  the server then drops it from the observer list). The collector's `consume([])` fires. The host
  runs per-link GC (FR-024) and emits a terminal `closed(LinkId, eos)` on the monitor stream
  (DESIGN-DOSSIER §4; ruling 2026-06-06 clean-close term).
- **Abrupt: `link_close(LinkId)` / `link_close(LinkId, Reason)`** (the 9th primitive). Tear down
  regardless of stream state (early-stop / fault give-up / DTLS-auth kill). On CoAP this = cancel
  the Observe + stop retransmitting outstanding CONs + free the resource registration; emit a
  terminal `permFail(LinkId, Reason)` (abrupt ⇒ permFail per harness `CloseKind.Abrupt`), then end
  the monitor stream. A disconnect that is **not** an intentional close yields `tempFail` then
  `permFail` — **never** a logical Fail (FR-044/FR-050).

### 2.8 Establishment path B — request/accept handshake (FR-002 path B)

When there is no direct listen/connect (NAT, discovery, peer introduction), the link is established
by an in-band **`request_link` / `accept_link`** handshake over a rendezvous (e.g. a well-known
CoAP resource at a directory node), converging on the same `'_link_setup'` registry keyed by ground
`LinkId` so the resulting link is indistinguishable from path A (DESIGN-DOSSIER §7; link-primitives
§2.4). Recommended rendezvous = in-band over the CoAP request itself (a ground `request(LinkId)`
token POSTed to the peer's accept resource). Shown in §3.3.

---

## 3. Exemplar GLP (ILLUSTRATIVE — PROPOSED primitives)

One **role-parameterized** program (FR-011): both instances load the *same* source and boot with
their own ground `AgentId`; the role is selected by `=?=` on that ground id (three-valued: ground-
equal commits the clause; unbound `Me?` suspends; mismatch falls through). The shared logic variable
of the unsplit program becomes a CoAP link.

### 3.0 The unsplit baseline (what the split must reproduce)

```prolog
% One REPL, one heap, one shared stream variable. The split over CoAP must reproduce
% this EXACT observable output (SC-001).
procedure readings(Stream(Integer)).
readings([21, 22, 23]).                % three sensor readings; the [] tail terminates the stream

% use_reading/1 is declared+defined once, in §3.1 below (one module ⇒ no duplicate procedure).

procedure consume_unsplit(Stream(Integer)?).
consume_unsplit([V|Vs]) :- ground(V?) | use_reading(V?), consume_unsplit(Vs?).
consume_unsplit([]).

procedure go_unsplit.
go_unsplit :- readings(S), consume_unsplit(S?).    % S: 1 writer, 1 reader — SRSW ok
```

`go_unsplit` prints `21`, `22`, `23`. (The `[]` tail terminates the stream.)

### 3.1 The split — establish → repeated send/receive → close

```prolog
-module(coap_sensor_collector).

% ---- the one ground link identity both nodes compile in (never reused) ----
% "coaps" (DTLS) because sensor and collector are on DIFFERENT hosts: FR-029 refuses a
% plain inter-host "coap" by default. ep(Host, Port) — 5684 is the CoAPs (DTLS) port.
procedure sensor_link(LinkId).
sensor_link(link_id("coaps", ep("collector.local", 5684), 1)).

% ---- one entry point; the ground AgentId selects the role (FR-011, the @/boot idiom) ----
procedure main(AgentId?).

% SENSOR node boots  main("sensor"): connects (CoAP client), produces readings.
% Role literals are quoted Strings to match the AgentId ::= String alternative (L9).
main(Me) :-
    Me? =?= "sensor" |
    sensor_link(L),
    client_connector(L?, Link, Faults),
    run_sensor(Link?, Faults?).

% COLLECTOR node boots  main("collector"): listens (CoAP server), consumes readings.
main(Me) :-
    Me? =?= "collector" |
    sensor_link(L),
    server_listener(L?, Link, Faults),
    run_collector(Link?, Faults?).

% ---- sensor side: ground-relay each reading over the link, then close gracefully ----
procedure run_sensor(Link(_, _)?, FaultStream?).
run_sensor(Link, _) :-
    sample_readings(Data),                 % Data: the readings (was the shared stream)
    send_all(Data?, Link?).

procedure sample_readings(Stream(Integer)).
sample_readings([21, 22, 23]).             % three ground readings; [] appended by send_all

% send each ground reading via link_send (channel face), then graceful close ([]).
procedure send_all(Stream(Integer)?, Link(_, _)?).
send_all([V|Vs], Link) :- ground(V?) |
    link_send(V?, Link?, Link1),           % cons ground V onto Out (head); host ships CON POST
    send_all(Vs?, Link1?).
send_all([], Link) :-
    link_send([], Link?, _).          % graceful close: bind Out tail to [] (stream-end)

% ---- collector side: receive each reading off the link, use it, until stream-end ----
procedure run_collector(Link(_, _)?, FaultStream?).
run_collector(Link, _) :-
    recv_all(Link?).

procedure recv_all(Link(_, _)?).
recv_all(Link) :-
    link_recv(V, Link?, Link1),            % SUSPEND until a frame arrives; bind V (FR-017)
    use_or_stop(V?, Link1?).

% A received item is either an Integer reading or the stream-end marker []; widen the
% receive-side value domain to a union so the [] head match in clause 1 is well-typed (H17).
Reading ::= Integer ; [].

procedure use_or_stop(Reading?, Link(_, _)?).
use_or_stop([], _).                     % stream-end marker → done (graceful close detected)
use_or_stop(V, Link) :- ground(V?), ~(V? =?= []) |
    use_reading(V?), recv_all(Link?).       % print this reading, loop for the next

procedure use_reading(Integer?).
use_reading(V) :- ground(V?) | '_output'(V?).
```

**SRSW / mode hand-check (per clause):**
- `main/1` clauses: `Me` writer-in-head (boot) → `Me?` read once in the `=?=` guard
  (ground-implying relaxation). `L` writer (`sensor_link`) → `L?` read once. `Link`/`Faults`
  writers (output args of `client_connector`/`server_listener`) → read once each in `run_*`. Clean.
- `run_sensor/2`: `Data` writer (`sample_readings`) → `Data?` read once (`send_all`); `Link?` read
  once; the ignored fault arg is bare `_` (a named singleton is rejected; an unread fault stream — legal; an anon reader `_?` would be
  illegal, FR-008 allows ignoring faults).
- `send_all/2` clause 1: `V` reader from head cons → `V?` in `ground/1` guard (1) + `link_send`
  payload (1) — **two reader uses permitted only because `ground(V?)` certifies groundness**
  (guards-reference §Ground-Guards SRSW relaxation). `Vs` reader from head → `Vs?` once in
  recursion. `Link` reader-in-head → `Link?` once (`link_send`); `Link1` writer (output of
  `link_send`) → `Link1?` once (recursion). Clean.
- `send_all/2` clause 2: `Link` reader → `Link?` once; `_Link1` anonymous writer (advanced channel
  discarded after the final `[]` send). Clean.
- `recv_all/1`: `Link` reader → `Link?` once; `V` writer (`link_recv` output) → `V?` once
  (`use_or_stop`); `Link1` writer (`link_recv` output) → `Link1?` once. Clean.
- `use_or_stop/2` clause 2: `V` reader → `V?` in `ground/1` (1) + in `~(V? =?= []) ` (1) + in
  `use_reading(V?)` (1) — **multiple reader uses permitted because `ground(V?)` certifies
  groundness** before the non-monotone `~(=?=)` and before use. The `~(=?=)` is gated
  fully-known-across-the-link (ground operand) per FR-039. `Link` reader → `Link?` once. Clean.

> Note (FR-004): the sensor is the **connector** *and* the **writer**; the collector is the
> **listener** *and* the **reader** — the natural CoAP direction (client POSTs to server). The roles
> could be reversed (a collector that connects out and OBSERVEs sensors) without changing the logic.

### 3.2 Bounded pipe — the credit / B→A back-channel (DESIGN-DOSSIER §3)

A naive sensor can run ahead of a constrained collector. To bound in-flight readings to a window of
`N`, couple a **reverse credit stream** (carried on the same B→A Observe direction). The sensor
spends one credit per reading and **SUSPENDS** (head unification on `[more|Credits]`) when none is
left — pure suspend-on-reader, no buffer object. This realizes FR-025 backpressure (SC-013).

```prolog
Credit ::= more.

% producer: spend one credit per item; SUSPEND on the credit-stream head when empty.
procedure produce(Stream(Item)?, Stream(Credit)?, Stream(Item)).
produce([Item|Items], [more|Credits], [Item?|Data?]) :-
    ground(Item?) | produce(Items?, Credits?, Data).
produce([], _, []).                 % source done → close data stream with []

% consumer: grant an initial window of N=3, replenish one credit per item drained.
procedure consume(Stream(Item)?, Stream(Credit)).
consume(Data, [more, more, more | Credits?]) :- drain(Data?, Credits).

procedure drain(Stream(Item)?, Stream(Credit)).
drain([Item|Data], [more | Credits?]) :- ground(Item?) | use_reading(Item?), drain(Data?, Credits).
drain([], []).
```

**SRSW hand-check:** `produce/3` clause 1: `Item` reader-in-head → `Item?` in `ground/1` (1) +
head cons `[Item?|Data?]` (1) — permitted, `ground(Item?)` certifies groundness. `Items`,`Credits`
readers → once each (recursion). `Data` writer (head) → `Data?` once (recursion). `consume/2`:
`Data` reader → `Data?` once (`drain`); `Credits` writer → `Credits?` once. `drain/2` clause 1:
`Item` reader → `ground/1` (1) + `use_reading` (1) — `ground`-certified; `Data` reader → once;
`Credits` writer → once. Clean. Over CoAP the credit term `more` is one Observe notification on the
B→A direction; the data term is one CON POST on the A→B direction — the SAME bidirectional link
(DESIGN-DOSSIER §3 KEY INSIGHT).

### 3.3 Establishment path B — request/accept over a CoAP rendezvous (FR-002 path B)

```prolog
% Sensor INITIATES via a rendezvous (e.g. a directory node's accept resource); the collector
% ACCEPTS by matching the requested ground LinkId. Both converge on the same '_link_setup'
% registry → an established link indistinguishable from the listen/connect path (FR-002).

procedure sensor_request(AgentId?).
sensor_request(Me) :- Me? =?= "sensor" |          % quoted String role literal (L9)
    sensor_link(L),
    request_link(L?, "collector", Link, Faults), % send ground request(LinkId) token to peer
    run_sensor(Link?, Faults?).

procedure collector_accept(AgentId?, Stream(request(LinkId, AgentId))?).
collector_accept(Me, Requests) :- Me? =?= "collector" |
    sensor_link(L),
    accept_link(L?, Requests?, Link, Faults),     % match request(LinkId2, From) by L =?= LinkId2
    run_collector(Link?, Faults?).
```

**SRSW hand-check:** `sensor_request/1`: `Me` reader → `=?=` (1); `L` writer→`L?` once
(`request_link`); `Link`/`Faults` writers → read once each. `collector_accept/2`: `Me` reader →
`=?=` (1); `Requests` reader → once (`accept_link`); `L` writer → `L?` once; inside `accept_link`
the head `[request(LinkId2, From)|_]` reads `LinkId?` under `ground/1` then `LinkId? =?= LinkId2?`
(two reader uses, ground-certified — link-primitives §2.4). Clean.

### 3.4 Fault monitor (independently observable — FR-008)

```prolog
% Read the per-link monitor stream with ordinary guards. Faults are ground terms over the
% ok / closed / tempFail / permFail lattice (FR-043) — NEVER a 4th verdict, NEVER a logical Fail.
% closed/2 IS a member of the Fault union (ruling 2026-06-06, clean-close term); FaultStream
% = Stream(Fault). The contract Fault type (link-primitives §1) is reconciled to include it, so
% the closed(L, R) match in watch/1 clause 4 is well-typed (M11).
Fault ::= ok ; closed(LinkId, Reason) ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).

procedure watch(FaultStream?).
watch([ok|Rest])                 :- watch(Rest?).
watch([tempFail(L, R)|Rest])     :- ground(L?) | note_temp(L?, R?), watch(Rest?).
watch([permFail(L, R)|_])        :- ground(L?) | note_perm(L?, R?).   % terminal; monitor ends
watch([closed(L, R)|_])          :- ground(L?) | note_closed(L?, R?). % clean close; terminal

procedure note_temp(LinkId?, Reason?).
note_temp(L, R) :- ground(L?), ground(R?) | '_output'(temp(L?, R?)).
procedure note_perm(LinkId?, Reason?).
note_perm(L, R) :- ground(L?), ground(R?) | '_output'(perm(L?, R?)).
procedure note_closed(LinkId?, Reason?).
note_closed(L, R) :- ground(L?), ground(R?) | '_output'(closed(L?, R?)).
```

**SRSW hand-check:** each `watch/1` clause reads the stream head once and `Rest?` once; `L`/`R`
read once each under `ground/1`. A goal that does **not** call `watch` stays safely suspended across
a disconnect (FR-044) — the data goal (`recv_all`) never reads `Faults`. Clean.

---

## 4. UNIT test specs (REPL Section-A runtime + Section-B/C type-check)

These exercise the **primitives/guards this `coap` unit leans on**, at the REPL, against the
single-instance heap (no real transport needed — they validate the GLP surface, suspension, and the
guard three-valued semantics the leaf relies on). Each is a goal + expected outcome; runnable once
the primitives land. Wire faults for `coap` are covered hermetically on the **loopback** transport
in §5 (T4: one-platform-per-leaf stays cheap — only bind-reactivation + graceful/abrupt close are
exercised on the real CoAP leaf).

### Section A — runtime

| # | Goal (illustrative) | Expected outcome | Ties to |
|---|---|---|---|
| A1 | Load `coap_sensor_collector.glp`; run `go_unsplit.` | Prints `21`, `22`, `23` (stream order); succeeds. Baseline the split must match. | SC-001 baseline |
| A2 | `link_send(42, ch(In?, Out), Result).` with `Out` an unbound writer | Succeeds; `Result = ch(In?, NewOut)` with `Out = [42|NewOut]` constructed in the head; ground gate passes. | FR-010 ground-relay |
| A3 | `link_send(Msg, ch(In?, Out), Result).` with `Msg` an unbound **reader** (not yet produced) | **Suspends** on `ground(Msg?)`; does NOT fail. Bind `Msg=42` from a sibling → reactivates exactly once and conses `42`. | FR-017/FR-050/FR-051 |
| A4 | `link_recv(V, ch([7|In], Out?), R).` | Succeeds; `V = 7`, `R = ch(In?, Out)` (pure stream-decons). | FR-018 receive |
| A5 | `link_recv(V, ch(In, Out?), R).` with `In` head an unbound reader | **Suspends** (un-arrived value = unbound local reader); bind the head later → reactivates exactly once, `V` bound. | FR-017/FR-051/SC-009 |
| A6 | `use_or_stop([], Link).` | Succeeds via the stream-end clause (graceful close detected); no further recursion. | graceful close `[]` |
| A7 | `use_or_stop(22, Link).` (ground non-`[]`) | Prints `22`; recurses into `recv_all`. The `~(22 =?= [])` guard is ground-gated. | FR-039 non-monotone |
| A8 | `produce([10,20],[more|C],D).` then bind `C=[more|_]` | Conses `10` then `20`, spending one credit each; with only one `more` available, **suspends** after the first until the second credit arrives (window bound). | FR-025/SC-013 |
| A9 | `watch([tempFail(link_id("coap",ep("h",5683),1), timeout)|R]).` | Fires the `tempFail` clause; `'_output'(temp(...))`; recurses on `R?`. A fault is data, not a verdict. | FR-043/FR-045 |
| A10 | `watch([permFail(L,R)|_]).` (ground `L`,`R`) | Fires the `permFail` clause once; terminal (no recursion). | FR-046 |
| A11 | `watch(Faults).` with `Faults` unbound, no producer | **Suspends** (does not fail, does not deadlock-as-fail). An unmonitored disconnect leaves it safely suspended. | FR-044/FR-050 |

### Section B — positive type-check (must compile)

| # | Program (illustrative) | Expected | Ties to |
|---|---|---|---|
| B1 | `send_all/2` as in §3.1 — reads `V?` under `ground(V?)` twice (guard + payload) | Compiles; SRSW analyzer accepts the multiple reader use because `ground/1` is ground-implying. | SC-006 |
| B2 | `use_or_stop/2` clause 2 — reads `V?` under `ground(V?)` before `~(V? =?= [])` and `use_reading` | Compiles; ground-implying guard licenses the repeated reads; non-monotone `~(=?=)` over a ground operand is accepted. | SC-006/FR-039 |
| B3 | The `link_send/3`, `link_recv/3`, `link_monitor/2` `procedure` declarations + clauses | Compile against the PROPOSED `Link(In,Out) ::= ch(...)` and `Fault` types; modes consistent. | FR-001 |

### Section C — negative type-check (must be rejected)

| # | Program (illustrative) | Expected rejection | Ties to |
|---|---|---|---|
| C1 | `send_all` variant that reads `V?` twice **without** the `ground(V?)` guard | SRSW analyzer **rejects** (multiple reader uses with no ground-implying guard). | SC-006 negative |
| C2 | A clause that puts a non-ground term on the wire: `link_send(f(X?), Link, R)` with `X?` unread/unbound and no `ground` gate | Rejected / guard suspends-then-cannot-commit — the ground-relay gate forbids an embedded reader crossing. | FR-010/FR-040 |
| C3 | A `watch` clause using a **declined** guard (`==`, `\==`, `\=`, or `reader/1`) on a fault term | **Rejected at compile time** ("Cannot call X in guard position / Unknown guard predicate") — declines enforced, not merely unimplemented. | FR-036 |

> Note: the `@<`/`@>`/`@=<`/`@>=` order-guard unit tests, the `atom/1` fix, and the compound-
> operand-suspend / imported-reader-reactivation fixes are specified once in `contracts/guards.md`
> §1–§4 (not duplicated here). This `coap` unit consumes them via A5/A11/B2 (suspend-not-fail on a
> remote/compound operand) and via the `~(=?=)` non-monotone gate in A7/B2.

---

## 5. INTEGRATION test specs (cross-instance over CoAP via the harness)

These drive the proposed host-language harness (C# reference shape, Dart mirror behaviour-identical;
all randomness from one run `seed`). Each = name + setup + action + expected OBSERVABLE outcome +
the SC it satisfies. The deterministic **loopback** transport carries the seeded wire faults
(Drop/Reorder/Duplicate/Delay) hermetically; the **real `coap` leaf** is exercised for
bind-reactivation feasibility (SC-003) + graceful/abrupt close — the T4 one-platform-per-leaf rule
(architecture/harness "TRANSPORT-AUTHOR SEAM").

### IT-COAP-1 — Headline split equivalence (SC-001), Dart↔Dart then Dart↔C#

- **Setup.** `StartInstances(coap_sensor_collector, [InstanceSpec(sensor, Dart), InstanceSpec(collector, Dart)], seed)`.
  Capture the unsplit baseline first: `Drive(single, go_unsplit, deadline)` → `Capture` → records
  stdout `21\n22\n23\n`. Then `OpenLink(sensor, collector, "coap", LinkOptions{InterHost=false, Tls=false})`
  (loopback/co-located ⇒ plain `"coap"` permitted, FR-029).
- **Action.** `Drive(sensor, main("sensor"), deadline)` and `Drive(collector, main("collector"), deadline)`;
  run to quiescence. `Capture(collector)`; `AssertEquiv(merged, baseline, ByteIdentical)`.
- **Expected observable.** Collector stdout is **byte-identical** to the unsplit baseline
  (`21\n22\n23\n`); both `Drive` results are `Done` (or `Suspended` then `Done` after the last
  bind), never `Failed`/`Deadlock`.
- **Then promote to cross-runtime.** Repeat with `InstanceSpec(collector, Csharp)` (Dart sensor ↔ C#
  collector) and the reverse. Assert `ByteIdentical` to the same baseline. **This Dart↔C# case is
  the mandated parity gate and MUST pass before ship.**
- **Satisfies:** SC-001 (+ SC-002 cross-runtime bind).

### IT-COAP-2 — Per-transport bind reactivation on the real CoAP leaf (SC-003, T4) — REQUIRED

- **Setup.** `StartInstances(...)` as IT-COAP-1 but `OpenLink(..., "coaps", LinkOptions{InterHost=true, Tls=true})`
  on one accepted platform (Windows OR Android). Collector `server_listener` binds a real UDP/DTLS
  CoAP socket + observable resource; sensor `client_connector` knows the URI + registers OBSERVE.
- **Action.** `Drive(collector, main("collector"), deadline)` first → collector's `link_recv`
  **suspends** on the unbound `In` head (assert `DriveResult = Suspended`, distinct from
  `Failed`/`Deadlock`). Then `Drive(sensor, main("sensor"), deadline)` → sensor sends one CON POST
  carrying reading `21`.
- **Expected observable.** Exactly one writer→reader bind crosses the link; the previously-suspended
  `link_recv` **reactivates exactly once**; collector prints `21`. A duplicate CoAP retransmit of the
  same frame (CON retransmit) is absorbed as a no-op (no second print, no crash).
- **Satisfies:** SC-003 (the gate that makes the `coap` leaf "shipped"); FR-016/FR-017/FR-051;
  FR-021 (dup absorption); FR-029 (DTLS inter-host).

### IT-COAP-3 — Reorder / loss over UDP (SC-012) — hermetic on loopback

- **Setup.** `StartInstances(...)`; `OpenLink(sensor, collector, "loopback", ...)` (the deterministic
  in-memory transport that models CoAP's UDP datagram behaviour: unordered, lossy, may duplicate).
- **Action.** `Inject(FaultSpec(Reorder, link, {window:3}))`, `Inject(FaultSpec(Drop, link, {p:0.2}))`,
  `Inject(FaultSpec(Duplicate, link, {p:0.2}))` — all seeded for reproducible replay. `Drive` both
  roles to quiescence with the reliability sublayer **engaged**. `Capture(collector)`.
- **Expected observable.** With the sublayer engaged: collector output reconstructs the in-order
  result — `AssertEquiv(merged, baseline, CausalInOrder)` (or `MultisetEqual` for the dedup aspect)
  passes; `21,22,23` are delivered each exactly once, in order, despite reorder/loss/dup. With the
  sublayer **disabled** (control run): the test **detects corruption** (a checksum/sequence-gap
  signal) rather than silently emitting a wrong result.
- **Satisfies:** SC-012; FR-020/FR-021; FR-018 (reorder buffer); FR-022 (CRC detects corruption).

### IT-COAP-4 — Backpressure to a slow constrained peer (SC-013)

- **Setup.** `StartInstances(...)` with the §3.2 bounded-pipe variant (window `N=3`); `OpenLink(..., "loopback", ...)`.
- **Action.** Sensor produces a long burst (e.g. 100 readings) fast; collector drains **slowly**
  (`Inject(FaultSpec(Delay, link, {per_recv:T}))` to model a battery-duty-cycled collector). `Drive`
  both; sample the outbound queue census during the run.
- **Expected observable.** In-flight readings never exceed the window `N=3` (the sensor `produce`
  **suspends** on the credit-stream head once credits are spent — assert sensor `DriveResult`
  oscillates `Suspended`); the outbound queue stays **bounded** (no OOM); a second independent link
  on the same instance is **not** head-of-line-blocked. Final collector output still equals the
  baseline multiset (every reading eventually drained, in order).
- **Satisfies:** SC-013; FR-025; DESIGN-DOSSIER §3 credit/back-channel unification (credits ride the
  B→A OBSERVE direction).

### IT-COAP-5 — Graceful close (`[]`) vs abrupt (`link_close`)

- **Setup.** `StartInstances(...)`; real `coaps` leaf (or loopback for the abrupt-fault half).
- **Action (graceful).** Sensor runs to `send_all([], Link)` → binds `Out` tail `[]`; harness
  `CloseLink(link, Graceful)`. **Action (abrupt).** Separately, mid-stream
  `CloseLink(link, Abrupt)` (models a sensor battery-death after `link_close`).
- **Expected observable.** Graceful: collector's `use_or_stop([])` fires; a terminal
  `closed(LinkId, eos)` appears on the monitor stream; `Stop` asserts resources return to baseline.
  Abrupt: a `permFail(LinkId, _)` appears on the monitor stream (and `tempFail` first on a non-
  intentional disconnect); the collector's suspended data goal **never spuriously fails**.
- **Satisfies:** SC-010 (fault liveness), SC-014 (GC-to-baseline via `Stop`), FR-043..047;
  the graceful-vs-abrupt close distinction (DESIGN-DOSSIER §4).

### IT-COAP-6 — Inter-host TLS-by-default refusal (FR-029)

- **Setup.** `StartInstances(...)` on two distinct hosts.
- **Action.** `OpenLink(sensor, collector, "coap", LinkOptions{InterHost=true, Tls=false})`
  (plain CoAP, inter-host, no explicit opt-out).
- **Expected observable.** `OpenLink` returns **LinkRefused**; the same call with `"coaps"` /
  `Tls=true` (DTLS, port 5684) **succeeds** and carries a bind. Proves both variants present and the
  secure default holds.
- **Satisfies:** SC-007 (plain inter-host refused); FR-029; User-Story-2 acceptance 3.

---

## 6. Regression — permanent regression set + baseline gate

Once implemented, the following become the **permanent CoAP-leaf regression set**, re-run on every
core-touching change behind the baseline gate (FR-067/SC-017 — `bash test/run_all_tests.sh` green
before and after; `=\=`-gated prelude still loads; no core change merges over a red baseline):

| Becomes regression | Why it is load-bearing | Gate |
|---|---|---|
| **IT-COAP-1** (Dart↔Dart **and** Dart↔C#) | The headline split-equivalence + the mandated cross-runtime parity gate; must pass before ship (FR-062). | SC-001/SC-002, FR-067 |
| **IT-COAP-2** | The "shipped" gate for the `coap` leaf — bind reactivation on the real transport (SC-003). A leaf is not shipped until this is green. | SC-003, FR-016 |
| **IT-COAP-3** | Guards the reliability sublayer (seq/dedup/reorder) against UDP reorder/loss/dup regressions — the single sharpest correctness area. | SC-012, FR-020/021 |
| **A2/A3/A5** (Section-A suspend-not-fail) | Lock the three-valued / suspend-on-reader behaviour the whole leaf rests on; cheap, fast, run every change. | SC-009, FR-017/050/051 |
| **C1/C2/C3** (Section-C negatives) | Lock SRSW-under-ground-guard, the ground-relay no-placeholder invariant, and the declined-guard enforcement. | SC-006, FR-010/036 |

IT-COAP-4 (backpressure), IT-COAP-5 (close), and IT-COAP-6 (DTLS refusal) join the regression set as
the corresponding sublayer features stabilize; they are scenario tests rather than the per-change
fast gate. The Section-A/B/C unit tests for `coap` are added to `test/run_all_tests.sh` (Section A
runtime; Section B positive type-check; Section C negative) per the Test Protocol.

---

## 7. Open items specific to this `coap` transport

- **OQ-COAP-1 (back-channel multiplexing).** Does ONE reverse Observe stream carry B→A data +
  credits + faults, or separate observable resources per concern? This tutorial illustrates one
  Observe stream for data+credit and a **separate** observable resource for the monitor (so faults
  are independently observable, FR-008). Ties to DESIGN-DOSSIER OQ-F3. Needs ruling.
- **OQ-COAP-2 (CON vs NON per direction).** Forward A→B readings: CON (transport ack/retransmit) for
  reliability, or NON (lighter, battery-cheaper) with our seq/dedup carrying all reliability? Likely
  per-link configurable; the *default* (CON, given the lossy mesh) is a tuning parameter, not a
  correctness condition. Confirm.
- **OQ-COAP-3 (Observe lifetime vs link lifetime).** RFC 7641 lets a server drop an observer after a
  notification times out / is RST'd. Mapping that transport-level observer-drop onto our link-fault
  lattice: when does an Observe-drop become `tempFail` (recoverable via re-register) vs `permFail`
  (give-up)? Proposed: Observe-drop ⇒ `tempFail` + automatic re-register (idempotent under dedup),
  `permFail` only after the bounded give-up interval. Confirm the mapping and the interval default.
- **OQ-COAP-4 (blockwise ↔ logical-credit coupling).** RFC 7959 Block1/Block2 fragments one frame
  into byte-blocks; DESIGN-DOSSIER §3 distinguishes logical-term credits (GLP-visible) from byte-
  chunk credits (below the seam). How does one logical credit map to N CoAP blocks, and is the GLP
  program shielded from block-level accounting (recommended: yes, logical credits only)? Ties to
  OQ-F3. Needs the fragmentation/credit-accounting design.
- **OQ-COAP-5 (DTLS auth mode default).** PSK (constrained default), Raw Public Key, or X.509 cert
  for the `"coaps"` handshake? Per the constrained battery target, PSK is the natural default, but
  origin-authentication (FR-026) keying interacts with the choice. Confirm the default + how the
  DTLS peer identity binds to the link's origin-auth check.
- **OQ-COAP-6 (NAT / sleepy-node rendezvous).** Battery sensors duty-cycle their radios and sit
  behind 6LoWPAN border routers (NAT-like). When does the request/accept path (§3.3) replace
  listen/connect, and what is the rendezvous (a directory node's CoAP accept resource vs a
  pre-established bootstrap link)? Ties to DESIGN-DOSSIER OQ-A3/§7. Confirm for the sleepy-node case.
- **OQ-COAP-7 (platform matrix, T4).** Which single platform certifies the `coap` leaf — Windows
  (e.g. a `System.Net.Sockets` UDP + a DTLS lib + libcoap-style stack) or Android? Ties to
  DESIGN-DOSSIER OQ-T2. Pick one for the SC-003 acceptance run.
