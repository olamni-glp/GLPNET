# Transport tutorial — `mqtt` (MQTT peer-to-peer link to the immediate peer)

**Status: PLAN-STAGE, PRE-IMPLEMENTATION. ILLUSTRATIVE ONLY — NOT runnable yet.**
Every link primitive used below (`link_setup` / `server_listener` / `client_connector` /
`request_link` / `accept_link` / `link_send` / `link_recv` / `link_monitor` / `link_close`,
and the host kernels `'_link_setup'/5`, `'_link_request'/5`, `'_link_accept'/5`,
`'_link_send'/3`, `'_link_monitor'/2`, `'_link_close'/2`) is **PROPOSED, pending Gabi's
language-authority approval** (CLAUDE.md §Language Authority; DISCIPLINE §1.14) and has **no
runtime support yet**. The exemplar GLP is for talking-through and hand-checking; the test
specs are SPEC-LEVEL (realistic scenario + exemplar GLP + expected OBSERVABLE outcome +
pass/fail oracle), made runnable once the implementation lands. Nothing here claims to be
implemented. Every GLP clause is SRSW- and mode-checked by hand inline.

This unit conforms to `../DESIGN-DOSSIER.md` (the nine base primitives; scalar→stream→
bounded-pipe; graceful `[]` vs abrupt `link_close`; the `ok`/`closed`/`tempFail`/`permFail`
monitor lattice; the CORRECTED MQTT framing) and to the contracts in
`../contracts/{link-primitives.md,guards.md,architecture-context.md}`. It mirrors the worked
HTTP example in `../contracts/example-http-link.md`.

---

## 1. Scenario — a device ↔ gateway command/telemetry link (the immediate P2P hop)

### 1.1 The web-researched real deployment

The canonical real-world MQTT shape is an **edge gateway that proxies a fleet of constrained
field devices**. In the ThingsBoard IoT Gateway, *"a ThingsBoard Gateway is a device that
maintains a single MQTT connection to ThingsBoard while proxying data for multiple physical
devices connected behind it"*; telemetry flows upstream on `v1/gateway/telemetry` and
server-issued commands flow downstream on `v1/gateway/rpc`, i.e. the pattern is *"downstream
devices → gateway → platform → gateway → downstream devices"*
([ThingsBoard Gateway MQTT API](https://thingsboard.io/docs/reference/gateway-mqtt-api/);
[ThingsBoard IoT Gateway MQTT config](https://thingsboard.io/docs/iot-gateway/config/mqtt/)).
The same intermediary architecture is documented by Azure IoT Edge — *"downstream devices
connect to the gateway over their native protocol, and the gateway handles the cloud
communication on their behalf"*, buffering device messages when the cloud link drops
([Azure IoT Edge downstream-device gateway](https://oneuptime.com/blog/post/2026-02-16-how-to-connect-downstream-devices-to-azure-iot-central-through-an-iot-edge-gateway/view)) —
by AWS IoT SiteWise Edge MQTT-enabled gateways
([AWS IoT SiteWise MQTT gateway](https://docs.aws.amazon.com/iot-sitewise/latest/userguide/mqtt-enabled-v3-gateway.html)),
and by Google Cloud's IoT gateway/MQTT bridge
([Google Cloud IoT MQTT bridge](https://cloud.google.com/iot/docs/how-tos/gateways/mqtt-bridge)).

### 1.2 The hop this unit models — and the CORRECTED framing

There are TWO distinct hops in that deployment, and they are at **different levels**:

1. **device ↔ gateway** — the immediate peer-to-peer hop. A field sensor/actuator on the
   shop floor talks MQTT to the local edge gateway: the gateway PUBLISHes commands to the
   device's command topic, the device PUBLISHes telemetry/acks to the gateway's telemetry
   topic. **This is the bilateral P2P link this unit models.**
2. **gateway ↔ cloud platform** — a SEPARATE P2P hop from the gateway to its upstream broker
   (ThingsBoard / Azure / AWS / GCP), with the cloud broker fanning the message out to N
   cloud-side subscribers.

**CORRECTED FRAMING (Gabi, 2026-06-06; DESIGN-DOSSIER §1 / §6):** at THIS level every link is
peer-to-peer to the **immediate peer**. If an MQTT broker sits between the two GLP instances,
the broker is **at another level and OUT OF SCOPE here**: `pub ↔ broker` is one P2P link and
`broker ↔ subscriber` is another P2P link. The base primitives see exactly **one bilateral P2P
link to the immediate peer** — the device's immediate peer is the gateway (or, equivalently, an
embedded/sidecar broker co-located with the gateway). **Broker fan-out / forwarding to the cloud
is a higher level the base primitives do NOT model** (it belongs to a later routing / `glink`-like
layer); the seq/dedup sublayer below enforces per-link FIFO + at-least-once **end-to-end on the
immediate hop** (FR-023), it does NOT delegate those guarantees to the broker.

So in GLP terms: instance **A = device** (constrained field unit), instance **B = gateway**
(the immediate peer). The device PUBLISHes a stream of telemetry samples to the gateway and
receives a stream of commands from it — the bidirectional command/telemetry link, expressed as
one `Link(In, Out)`.

### 1.3 Why MQTT fits this transport seam

- **Lightweight, designed for constrained devices over lossy links** — MQTT is *"a lightweight
  publish-subscribe, machine-to-machine network protocol... widely used for connections with
  remote locations with devices that have resource constraints or network transfer rate"*
  ([HiveMQ MQTT QoS essentials](https://www.hivemq.com/blog/mqtt-essentials-part-6-mqtt-quality-of-service-levels/)).
- **Long-lived bidirectional session** — a single CONNECT establishes a persistent session over
  which both directions PUBLISH; the device subscribes to its command topic and publishes
  telemetry, the gateway the reverse. This maps cleanly to the two directions of `Link(In, Out)`.
- **Built-in at-least-once (QoS 1) with explicit duplicate semantics** — exactly the property
  this unit stress-tests for our dedup gate (SC-008): QoS 1 *"can arrive multiple times"* and the
  DUP flag *"is used for internal purposes and is not processed by the broker or client"*
  ([HiveMQ MQTT QoS essentials](https://www.hivemq.com/blog/mqtt-essentials-part-6-mqtt-quality-of-service-levels/)).
- **TCP-ordered, lossless, bidirectional substrate** — the OASIS standard states MQTT *"runs over
  TCP/IP, or over other network protocols that provide ordered, lossless, bi-directional
  connections"* ([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)),
  so per-link FIFO at the byte level is given; our sublayer adds dedup + reorder for the
  cross-broker / reconnect cases the broker does not guarantee.

**Sources:** see §8.

---

## 2. Protocol mapping — the uniform seam over MQTT

The transport-author seam (`ILinkTransport` / `ILinkEndpoint`, architecture-context §3) is
adapted to MQTT as follows. A `LinkId = link_id("mqtt", ep(Host, Port), Nonce)` selects this
leaf by `Scheme = "mqtt"` (TLS variant `"mqtts"`, §2.4).

### 2.1 `open` — establishment path (CONNECT/CONNACT, then the in-band request/accept handshake)

MQTT has **no symmetric listen/connect at the application level**: every endpoint is a *client*
that sends CONNECT to its immediate peer (a broker or an embedded broker co-located with the
gateway). The OASIS standard: *"After a Network Connection is established by a Client to a
Server, the first packet sent from the Client to the Server MUST be a CONNECT packet."* The peer
replies CONNACK
([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)).

Because both GLP ends are MQTT *clients* to the same immediate peer, the **request/accept
rendezvous handshake (FR-002 path B)** is the natural establishment path for `mqtt`, not the
listen/connect pairing:

- Each end CONNECTs and SUBSCRIBEs to its own inbound topic (the device to its command topic,
  the gateway to its telemetry topic). This is the transport-level rendezvous.
- The **initiator** (`request_link`) PUBLISHes a ground `request(LinkId)` token on a well-known
  rendezvous topic to the immediate peer; `'_link_request'` mints A's In/Out and parks A's recv.
- The **acceptor** (`accept_link`) reads the `request(LinkId2, FromPeer)` token off its inbound
  RequestStream, matches `LinkId? =?= LinkId2?`, and establishes via `'_link_accept'`.
- Both paths converge on the same `'_link_setup'` registry keyed by the ground `LinkId`
  (link-primitives §2.4), so the established `Link` is indistinguishable from path A
  ("equivalent established link", FR-002).

Establishment role is INDEPENDENT of data direction (FR-004): the device may be the writer of
telemetry while also the acceptor of the link.

> Listen/connect (`server_listener` / `client_connector`) is ALSO offered for `mqtt` when an
> embedded/sidecar broker is co-located with the gateway (the gateway "listens" by being the
> CONNECT target; the device "connects"). Both faces resolve to the same registry; the
> request/accept handshake is the recommended path because it works through a shared broker
> without either GLP end binding a listening socket. (See OQ-T-MQTT-1.)

### 2.2 `send-bytes` / `recv-bytes` — one frame = one PUBLISH

- `SendBytesAsync(frame)` = one MQTT **PUBLISH** to the peer's inbound topic. The frame is our
  opaque `byte[]` (the byte-parity `PayloadSerializer` blob + reliability metadata), carried as
  the PUBLISH **payload**. The MQTT Fixed Header (packet type + flags) + Variable Header (topic,
  packet identifier) wrap it; MQTT is content-agnostic about the payload.
- `RecvBytesAsync(ct)` = the next PUBLISH delivered on this end's subscribed inbound topic, with
  the payload handed up to the reliability sublayer and thence `handleMadAssignment`.
- **QoS choice = QoS 1 (at-least-once).** This is deliberate: QoS 1 *"can arrive multiple times;
  receivers must tolerate duplicates"*, and the **DUP flag is not processed by broker/client** —
  so MQTT itself does NOT dedup. Our seq/dedup sublayer (FR-020/FR-021) provides the
  exactly-once-EFFECTIVE bind on top (FR-023). QoS 2's four-packet PUBLISH/PUBREC/PUBREL/PUBCOMP
  exactly-once is available but redundant for us (and heavier on constrained links) since our
  sublayer already dedups; QoS 0 is rejected for data frames because it gives no retransmission.
  ([HiveMQ MQTT QoS essentials](https://www.hivemq.com/blog/mqtt-essentials-part-6-mqtt-quality-of-service-levels/);
  [EMQ MQTT 5.0 PUBLISH & PUBACK](https://www.emqx.com/en/blog/mqtt-5-0-control-packets-02-publish-puback)).

### 2.3 The B→A back-channel mechanism

Trivial on MQTT: the link is two topics, one per direction. A→B frames PUBLISH to B's inbound
topic; **B→A frames PUBLISH to A's inbound topic** — a fully symmetric, persistent,
bidirectional session. No long-poll or response-body trick is needed (unlike HTTP/1.1). This
is why MQTT carries the bounded-pipe **credit/demand back-channel** (DESIGN-DOSSIER §3) directly:
the reverse credit stream is just B→A PUBLISHes of `more` tokens on A's inbound topic — the SAME
reverse direction that carries B→A application replies. Flow control and the back-channel are one
mechanism (DESIGN-DOSSIER §3 KEY INSIGHT). MQTT 5.0 also offers native **Response Topic** (0x08)
+ **Correlation Data** (0x09) for request/reply
([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)); we keep our own
ground `CorrId` + reverse-link reply table (architecture-context §4.2) so request/reply is
transport-uniform, but the MQTT leaf MAY map our CorrId onto Correlation Data as an optimization.

### 2.4 TLS / security variant (`mqtts`)

MQTT *"does not mandate encryption but operates over TCP/IP"*; the convention is **port 1883
plain, port 8883 TLS**
([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)). Mapping to FR-029
(TLS-by-default inter-host): the inter-host scheme is **`"mqtts"`** (MQTT-over-TLS, :8883); a
plain **`"mqtt"`** inter-host link is **refused by default** (the harness `InterHost && !Tls =>
LinkRefused`, FR-029) and requires an explicit opt-out. Loopback / co-located (device and an
embedded broker on the same host) may use plain `"mqtt"`. Authentication is the CONNECT
username/password payload and/or MQTT 5.0 extended AUTH (Authentication Method/Data); origin
authentication (FR-026) is still enforced END-TO-END by our sublayer (per-message origin check),
never delegated to the broker's auth.

### 2.5 MTU / fragmentation + reliability

- **No MQTT-level fragmentation.** MQTT defines a **Maximum Packet Size** property (0x27); the
  spec: *"The Server MUST NOT send packets exceeding Maximum Packet Size"* and an over-size
  packet is **discarded**, not split
  ([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)). Therefore the
  **reliability sublayer owns fragmentation/reassembly** (FR-022): an over-Maximum-Packet-Size
  frame is fragmented into multiple PUBLISHes below the seam and reassembled before
  `handleMadAssignment`. (On constrained field links this matters; cf. CoAP blockwise.)
- **Reliability is OURS, not the broker's (FR-023).** MQTT QoS 1 gives at-least-once on each hop
  but the broker is NOT trusted for cross-hop FIFO/exactly-once. The sublayer adds: per-link
  **sequence number** (FIFO + reorder buffer), **dedup key** (seq + never-reused global name) so
  a QoS-1 duplicate or a reconnect redelivery is an absorbed no-op (FR-021/SC-008), **version
  byte + length/CRC** (MQTT does not checksum the payload), **fragmentation/reassembly**, and
  **epoch/fencing** for reconnect split-brain (FR-047). TCP under MQTT gives ordered/lossless
  WITHIN one connection; across a reconnect or a broker hand-off the sublayer restores order
  (SC-012).

### 2.6 Graceful close (`[]`) vs abrupt close (`link_close`)

- **Graceful (stream-end `[]`).** The producer binds its `Out` tail to `[]`. The MQTT leaf
  serializes an end-of-stream sentinel frame (one final QoS-1 PUBLISH) and then sends a **MQTT
  DISCONNECT with Reason Code 0x00 (Normal disconnection)**. Per OASIS, a clean DISCONNECT
  *removes the Will Message from the session* — i.e. no Last-Will is fired, signalling an
  intentional close
  ([OASIS MQTT 5.0](https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html)). The host runs
  per-link GC and emits a terminal **`closed(LinkId, eos)`** on the monitor stream (DESIGN-DOSSIER
  §4).
- **Abrupt (`link_close(LinkId)` / `/2`).** `'_link_close'(LinkId?, Reason?)` tears the link down
  regardless of stream state and emits a terminal **`closed(LinkId, Reason)`** (Reason = the user
  reason, or `abrupt`). The MQTT leaf may DISCONNECT with a non-0x00 Reason Code, or simply drop
  the TCP connection. A drop that is NOT an intentional close (peer crash, network partition)
  fires the device's pre-registered **Last Will and Testament** to the gateway AND surfaces on our
  monitor as `tempFail(LinkId, Reason)` within bounded time, then `permFail(LinkId, Reason)` on
  give-up — **never a logical Fail** (FR-044/FR-050). The LWT and our monitor are complementary:
  LWT notifies the broker side; our `tempFail`/`permFail` notifies the GLP program.

### 2.7 Seam summary table

| Seam op | MQTT realization | Spec basis |
|---|---|---|
| `open` (rendezvous, recommended) | both ends CONNECT to immediate peer + SUBSCRIBE inbound topic; `request(LinkId)` PUBLISHed, matched by `accept_link` | FR-002 path B; OASIS CONNECT/CONNACK |
| `open` (listen/connect, embedded broker) | gateway = CONNECT target; device CONNECTs | FR-002 path A |
| `send-bytes` | one QoS-1 PUBLISH; payload = opaque frame | FR-010; OASIS PUBLISH |
| `recv-bytes` | next PUBLISH on subscribed inbound topic → sublayer → `handleMadAssignment` | FR-017; OASIS SUBSCRIBE |
| B→A back-channel | reverse-direction PUBLISH on A's inbound topic (data + credit `more`) | DESIGN-DOSSIER §3; FR-003 |
| `close` graceful | `[]` sentinel frame + DISCONNECT RC 0x00 (no Will) → `closed(LinkId, eos)` | DESIGN-DOSSIER §4; OASIS DISCONNECT |
| `close` abrupt | `link_close` → DISCONNECT/drop → `closed(LinkId, Reason)`; unintended drop → LWT + `tempFail`/`permFail` | FR-044/FR-046; OASIS Will |
| `fault` | broker/TCP drop → sublayer `tempFail`→`permFail` on monitor (never Fail) | FR-043/FR-045/FR-050 |
| TLS variant | `"mqtts"` :8883; plain `"mqtt"` inter-host refused | FR-029; OASIS :1883/:8883 |
| fragmentation | sublayer fragments over Maximum-Packet-Size; MQTT does not split | FR-022; OASIS Max Packet Size |
| dedup over QoS 1 | sublayer seq+global-name dedup; MQTT DUP flag NOT trusted | FR-021/FR-023; HiveMQ QoS |

---

## 3. Exemplar GLP (ILLUSTRATIVE; PROPOSED primitives)

One role-parameterized program (FR-011); both instances load the SAME file and boot with their
own ground `AgentId`. `A = device`, `B = gateway` (the immediate peer). The device streams
telemetry samples to the gateway over `Out` and reads commands from the gateway over `In`. The
gateway side drives a bounded pipe (the credit/back-channel from DESIGN-DOSSIER §3) so a fast
device cannot run unboundedly ahead of a busy gateway.

```prolog
-module(mqtt_device_gateway_demo).

%% ---- the one link identity both instances compile in (ground, never reused) ----
%% "mqtts" (TLS, :8883) because device and gateway are on DIFFERENT hosts: FR-029 refuses a
%% plain "mqtt" inter-host link by default. (Co-located device + embedded broker may use "mqtt".)
procedure dg_link(LinkId).
dg_link(link_id("mqtts", ep("gateway.local", 8883), 1)).

%% ---- one entry point; the ground AgentId selects the role (FR-011, the @/boot idiom) ----
procedure main(AgentId?).

%% DEVICE node boots  main(device): initiates the in-band request/accept handshake (it is an
%% MQTT client to the gateway's immediate peer), then streams telemetry and reads commands.
main(Me) :-
    Me? =?= device |
    dg_link(L),
    request_link(L?, gateway, Link, Faults),         %% establish: handshake initiate (FR-002 B)
    run_device(Link?, Faults?).

%% GATEWAY node boots  main(gateway): accepts the inbound link-request off its RequestStream,
%% then issues commands and drains telemetry under a bounded window.
main(Me) :-
    Me? =?= gateway |
    dg_link(L),
    inbound_requests(Reqs),                           %% the gateway's inbound request stream
    accept_link(L?, Reqs?, Link, Faults),             %% establish: handshake accept   (FR-002 B)
    run_gateway(Link?, Faults?).

%% inbound_requests/1 is supplied by the boot harness (the gateway's RRequestStream face);
%% shown here as a procedure so the clause type-checks. (Host ingress fills it.)
procedure inbound_requests(Stream(request(LinkId, AgentId))).
inbound_requests([request(link_id("mqtts", ep("gateway.local", 8883), 1), device) | _More]).
```

SRSW/mode check (per `main` clause): `Me` writer-in-head → `Me?` read once in `=?=` guard
(ground-implying relaxation). `L` writer (`dg_link`) → `L?` read once (`request_link`/`accept_link`).
`Link`/`Faults` writers (primitive output args) → read once each in `run_*`. `Reqs` writer
(`inbound_requests`) → `Reqs?` read once (`accept_link`). Clean.

```prolog
%% ================= DEVICE side: stream telemetry out, read commands in =================
procedure run_device(Link(_, _)?, FaultStream?).
run_device(ch(CmdsIn?, TeleOut), _Faults) :-
    telemetry_source(Samples),                        %% the readings to send (was a shared var)
    send_telemetry(Samples?, TeleOut),                %% cons ground samples onto Out, then []
    handle_commands(CmdsIn?).                          %% read gateway commands off In

%% the device's local telemetry readings (ground integers); ends -> graceful close of Out
procedure telemetry_source(Stream(Integer)).
telemetry_source([23, 24, 22, 25]).

%% send each GROUND sample as one frame; end the stream with [] (graceful close, FR-010 ground gate)
procedure send_telemetry(Stream(Integer)?, Stream(Integer)).
send_telemetry([S|Ss], [S?|Out?]) :- ground(S?) | send_telemetry(Ss?, Out).
send_telemetry([], []).                               %% Out := [] -> graceful close (eos)

%% read commands until the gateway ends its command stream ([] = graceful close detected)
procedure handle_commands(Stream(Command)?).
Command ::= setRate(Integer) ; ping.
handle_commands([Cmd|Cmds]) :- ground(Cmd?) | apply_command(Cmd?), handle_commands(Cmds?).
handle_commands([]).                                  %% gateway closed the command stream

procedure apply_command(Command?).
apply_command(setRate(R)) :- ground(R?) | '_output'(R?).      %% observable: the new rate
apply_command(ping)        :- '_output'(ping).               %% observable: a ping ack
```

SRSW/mode check: `run_device` — `CmdsIn?`/`TeleOut` from the `ch(...)` head: `CmdsIn?` read once
(`handle_commands`), `TeleOut` writer threaded once (`send_telemetry`). `Samples` writer
(`telemetry_source`) → `Samples?` read once. `send_telemetry([S|Ss], [S?|Out?])`: `S` head-reader
gated `ground(S?)` then consed `[S?|...]` (two reader uses legal under ground-implying relaxation);
`Ss` read once, `Out` threaded once. `handle_commands([Cmd|Cmds])`: `Cmd` gated `ground` then
read once in `apply_command`; `Cmds` read once. `apply_command(setRate(R))`: `R` gated then read
once. All clean. Outputs constructed in HEADS (`[S?|Out?]`), never via `=` in a body.

```prolog
%% ================= GATEWAY side: issue commands out, drain telemetry under a window =================
procedure run_gateway(Link(_, _)?, FaultStream?).
run_gateway(ch(TeleIn?, CmdsOut), _Faults) :-
    command_plan(Cmds),                               %% the commands to push to the device
    send_commands(Cmds?, CmdsOut),                    %% cons ground commands onto Out, then []
    drain_telemetry(TeleIn?, 3).                       %% bounded window of 3 (backpressure)

procedure command_plan(Stream(Command)).
command_plan([setRate(50), ping]).

procedure send_commands(Stream(Command)?, Stream(Command)).
send_commands([C|Cs], [C?|Out?]) :- ground(C?) | send_commands(Cs?, Out).
send_commands([], []).                                %% graceful close of the command direction

%% drain telemetry one sample at a time; "window" is the credit budget (DESIGN-DOSSIER §3).
%% Bounded pipe = forward data stream coupled to GLP suspension; no buffer object.
procedure drain_telemetry(Stream(Integer)?, Integer?).
drain_telemetry([S|In], N) :- N? > 0, N1 := N? - 1 | use_sample(S?), drain_telemetry(In?, N1?).
drain_telemetry([], _N).                               %% device closed telemetry (eos)

procedure use_sample(Integer?).
use_sample(S) :- ground(S?) | '_output'(S?).          %% observable: each telemetry reading
```

SRSW/mode check: `run_gateway` — `TeleIn?` read once (`drain_telemetry`), `CmdsOut` threaded once
(`send_commands`). `Cmds` writer → `Cmds?` read once. `send_commands` mirrors `send_telemetry`
(checked above). `drain_telemetry([S|In], N)`: `S` read once (`use_sample`, gated inside);
`In` read once; `N` read twice but BOTH uses are inside the guard (`N? > 0` and `N1 := N? - 1`)
where `>`/`:=` ground-imply `N` — legal under the relaxation; `N1` writer (`:=`) → `N1?` read once
in the recursive call. `use_sample(S)`: `S` gated then read once. All clean.

> **Faithfulness to GLP semantics.** Each instance keeps its OWN SRSW writer/reader pairs; the
> device's `TeleOut` writer is local to the device, the gateway's `TeleIn` reader local to the
> gateway. The ground-relay ships a COPY of each ground sample/command; the receiver's ingress
> binds only its LOCAL In-tail writer (writer-MGU, FR-049). An un-arrived sample is a suspended
> local reader (three-valued, FR-050), reactivated exactly once on bind (FR-051). Per-link FIFO:
> `Out` cons order = PUBLISH order = `In` bind order (FR-018). The `drain_telemetry` window is GLP
> suspension, not a buffer — the bounded-pipe model (DESIGN-DOSSIER §3).

---

## 4. UNIT test specs (REPL Section-A runtime + Section-B/C type-check)

These exercise the PROPOSED primitives/guards this unit relies on, at the single-REPL level.
Each = goal + expected outcome. **SPEC-LEVEL** (runnable once the primitives land). They follow
the `run_all_tests.sh` section convention (A = runtime, B = positive type-check, C = negative
type-check). These do NOT need a transport — they pin the GLP-surface contracts the `mqtt` leaf
sits on.

### Section A — runtime

- **A-MQTT-1 (graceful close detection).** Load the exemplar. Goal: feed `send_telemetry([23],
  Out)` and a sibling `handle_commands([])` style consumer of `Out`. Expected: the consumer of
  `Out` observes `[23 | []]` and the `send_telemetry([], [])` clause fires; outcome **succeeds**
  with `'_output'` empty (no command applied) — the `[]` stream-end is detected, not an error.
- **A-MQTT-2 (ground gate on send — suspend-not-fail).** Goal: `send_telemetry([X], Out)` with
  `X` an unbound reader. Expected: **suspended** (DriveResult Suspended), not Failed — `ground(S?)`
  patiently suspends; bind `X = 23` from a sibling and assert it **reactivates exactly once** and
  yields `Out = [23|[]]` (FR-017/FR-051).
- **A-MQTT-3 (ground gate on send — fail on writer).** Goal: `send_telemetry([W], Out)` where `W`
  is an unbound WRITER. Expected: **Failed** (SRSW: no paired reader can supply the value), per
  the three-valued ask-semantics table (guards.md §0).
- **A-MQTT-4 (bounded-window suspension).** Goal: `drain_telemetry(In?, 1)` with `In = [10,20|_]`
  (a second element present, tail unbound) and budget 1. Expected: applies `10` (`'_output'(10)`),
  then **suspends** — the window-1 budget means one element; not Failed. (Models backpressure /
  the credit budget; SC-013 tie-in.) Decrementing `N` to 0 makes the recursive `drain_telemetry`
  clause non-reducible on the next element → suspend.
- **A-MQTT-5 (`@<` peer-id ordering for the rendezvous).** Goal: with compound peer-ids
  `peer("device", 1)` and `peer("gateway", 1)`, assert `peer("device",1) @< peer("gateway",1)`
  **succeeds** (string lexicographic on the functor arg), and the unbound-reader operand case
  **suspends then reactivates once** (FR-037/SC-004). (Used when peers are sorted for
  leader-election / deterministic acceptor choice.)
- **A-MQTT-6 (monitor lattice terms are ordinary data).** Goal: a watcher
  `on_fault([permFail(L, R)|_]) :- ground(L?) | '_output'(permFail)` fed
  `[ok, tempFail(L0, silence), permFail(L0, give_up) | _]`. Expected: matches with ordinary
  guards, **succeeds**, `'_output'(permFail)` — confirming faults are data on a stream, NOT a
  fourth verdict (FR-043). Also feed `[closed(L0, eos)|_]` and assert the `closed/2` clean-close
  term is matchable.

### Section B — positive type-check

- **B-MQTT-1 (SRSW under the ground-implying send gate).** The `send_telemetry([S|Ss],[S?|Out?])
  :- ground(S?) | ...` clause (two reader uses of `S` under `ground/1`) **compiles** — SC-006
  positive: a var grounded by a ground-implying guard may be read multiply.
- **B-MQTT-2 (`@<` ground-implying).** A clause that reads a compound peer-id var multiply after
  an `@<` guard on it **compiles** (SC-006 positive for the new guard family).
- **B-MQTT-3 (Link/Channel typing).** The exemplar's `run_device(ch(CmdsIn?, TeleOut), _)` and
  `run_gateway(ch(TeleIn?, CmdsOut), _)` type-check against `Link(In,Out) ::= ch(In, Out?)` with
  the declared `Stream(Integer)` / `Stream(Command)` directions.

### Section C — negative type-check

- **C-MQTT-1 (SRSW rejected without the gate).** `send_telemetry([S|Ss],[S?|Out?])` **without**
  `ground(S?)` (i.e. `S` read twice with no ground-implying guard) is **rejected** by the SRSW
  analyzer — SC-006 negative.
- **C-MQTT-2 (declined guard rejected).** A clause using `\=` or `==` in a guard over a peer-id
  (e.g. `LinkId == LinkId2`) is **rejected at compile time** (FR-036; canonical form is `=?=`).
- **C-MQTT-3 (no `=` in body).** A clause that tries to construct the outbound element via body
  unification (`... | Out = [S|_]`) instead of head construction is **rejected** (GLP-not-Prolog;
  outputs are head-constructed). [If the analyzer expresses this as a mode error rather than a
  syntactic reject, the oracle is the mode-error diagnostic.]

> Every NEW/changed guard exercised here (the `@<` family, the ground gate path) MUST also pass
> the generic three-valued conformance (succeed / suspend-reactivate-once / fail) per guards.md
> §7 / SC-004; A-MQTT-2/3/5 are the `mqtt`-specific instances.

---

## 5. INTEGRATION test specs (cross-instance over MQTT via the harness)

Each test uses the PROPOSED harness (host-language, NOT GLP; C# reference shape, Dart mirror
behaviour-identical; all randomness from one run seed). **SPEC-LEVEL** until the `mqtt` leaf +
reliability sublayer land. Wire faults are injected on the deterministic **loopback** transport
that backs the `mqtt` leaf hermetically (so T4 stays one-platform-per-leaf cheap, per the
transport-author seam); the real `mqtt` leaf is tested for bind-reactivation (SC-003) +
graceful/abrupt close on at least one platform (Windows OR Android, FR-063).

Setup convention: `StartInstances(mqtt_device_gateway_demo, [InstanceSpec(device, R1),
InstanceSpec(gateway, R2)], seed)`; `OpenLink(device, gateway, "mqtt"|"mqtts",
LinkOptions{...})`; the immediate peer is the gateway (or an embedded broker co-located with it)
— **no broker is ever modelled as a logical participant** (harness guardrail).

### IT-MQTT-1 (SC-001 — headline split equivalence; Dart↔Dart THEN Dart↔C#) — REGRESSION

- **Baseline.** Run the reduced `producer(X)/consumer(X?)` core (a single telemetry sample
  `produce_value(42)` → `'_output'(42)`) unsplit in ONE instance; `Capture` the stdout baseline
  (`42`).
- **Action.** `StartInstances` two instances of the role-parameterized program; `OpenLink(device,
  gateway, "mqtt", {InterHost:false})` (loopback-backed `mqtt`, plain allowed because not
  inter-host); `Drive` device (producer role) and gateway (consumer role) to quiescence;
  `Capture` both; merge observable output.
- **Expected.** `AssertEquiv(merged, baseline, ByteIdentical)` — the split run's merged stdout is
  **byte-identical** to the unsplit `42`. Run **Dart↔Dart first**, then **Dart↔C#** (R1=Dart,
  R2=Csharp) — the Dart↔C# case is the mandated cross-runtime parity gate (FR-059/FR-062) and
  **must pass before ship**.
- **Satisfies:** SC-001 (and SC-002 cross-runtime bind).

### IT-MQTT-2 (SC-003 — per-transport bind reactivation; REQUIRED for the leaf) — REGRESSION

- **Setup.** Two instances; `OpenLink(device, gateway, "mqtts", {InterHost:true, Tls:true})` over
  the REAL `mqtt` leaf on one platform (Windows OR Android). Gateway drives `link_recv` on an
  empty `In` → it **suspends**.
- **Action.** Device `link_send`s one ground sample (`23`); one QoS-1 PUBLISH crosses.
- **Expected.** The gateway's suspended `link_recv` **reactivates exactly once**, `Capture(gateway)`
  shows `23`; `DriveResult` transitions Suspended→Done. Re-driving without a new send leaves it
  Done (no spurious second reactivation).
- **Satisfies:** SC-003 (the leaf is "shipped" only when this passes). This is the FR-016
  feasibility test for `mqtt`.

### IT-MQTT-3 (SC-008 — dedup over QoS-1 duplicates is a verified no-op) — REGRESSION

- **Setup.** Two instances; loopback-backed `mqtt`. Device sends one sample `23` (seq 1).
- **Action.** `Inject(FaultSpec(Duplicate, link, {times:2}))` so the SAME frame (seq 1, same
  global name) is delivered **three times** — modelling MQTT QoS-1 at-least-once redelivery + a
  reconnect redelivery (the DUP flag is NOT trusted; our gate dedups). `Drive` gateway.
- **Expected.** Exactly ONE bind; `Capture(gateway)` stdout shows `23` **once**; no error raised,
  no error swallowed, no re-bind, no goal re-enqueue (the dedup gate absorbs deliveries 2 and 3
  before `handleMadAssignment`). With the sublayer **disabled**, the test instead asserts the
  legacy crash/duplicate is DETECTED (FR-020 corruption-detection arm), not silently doubled.
- **Satisfies:** SC-008 (this is the headline reason MQTT QoS 1 is chosen for this unit).

### IT-MQTT-4 (SC-012 — reorder recovery) — REGRESSION

- **Setup.** Two instances; loopback-backed `mqtt`. Device sends an ORDER-DEPENDENT telemetry
  stream `[10, 20, 30]` (three frames, seq 1..3).
- **Action.** `Inject(FaultSpec(Reorder, link, {seed}))` so frames arrive out of order
  (e.g. 2,1,3). `Drive` gateway under `drain_telemetry(_, 3)`.
- **Expected.** The reorder buffer reconstructs send order; `Capture(gateway)` stdout =
  `10,20,30` in order. `AssertEquiv(merged, baseline, CausalInOrder)` against the in-order
  single-instance run. With the sublayer disabled, corruption is DETECTED, not silently built
  into a wrong order (FR-020).
- **Satisfies:** SC-012.

### IT-MQTT-5 (graceful close `[]` → `closed(LinkId, eos)`)

- **Setup.** Two instances; loopback-backed `mqtt`; device streams `[23,24]` then ends `Out` with
  `[]`; gateway reads `link_monitor`.
- **Action.** `CloseLink(link, Graceful)` is the device binding `Out = []` (stream-end), which
  serializes the eos sentinel + DISCONNECT RC 0x00.
- **Expected.** Gateway's `drain_telemetry([], _)` clause fires (clean end); `Capture(gateway)`
  Faults = `[..., closed(LinkId, eos)]` then the monitor stream ends. No `tempFail`/`permFail`.
- **Satisfies:** the graceful-close contract (DESIGN-DOSSIER §4); supports SC-014 GC.

### IT-MQTT-6 (abrupt close / partition → `tempFail`→`permFail`, never Fail)

- **Setup.** Two instances; loopback-backed `mqtt`; gateway has a fault-watcher reading
  `link_monitor` AND a separate data goal NOT reading the monitor.
- **Action.** `Inject(FaultSpec(Partition, link, {}))` (and/or `PeerKill` device) mid-stream;
  advance the give-up clock via `Inject(ClockJitter)` (no real sleep).
- **Expected.** Within bounded logical time the monitor shows `tempFail(LinkId, _)` then, after
  give-up, `permFail(LinkId, _)`; `Capture(gateway)` Faults contains both. The data goal that does
  NOT read the monitor stays **Suspended**, never **Failed** (FR-044/FR-050). `clearFault(token)`
  heals the partition and the link can resume idempotently (tempFail recoverable).
- **Satisfies:** SC-010-style fault behavior (FR-043/045/046/050); supports SC-014.

### IT-MQTT-7 (SC-014 — distributed GC to baseline)

- **Setup.** `Stop`'s resource census taken at baseline. Open N=3 `mqtt` links, drive them, then
  `permFail` all three (partition + give-up).
- **Action.** `Stop(instances)`.
- **Expected.** Per-link resources (global-name entries, send-registry goals, heap bind
  callbacks, reply-table/CorrId entries) return to the baseline census; no unreclaimable cycle.
- **Satisfies:** SC-014.

> **Why SC-008 + SC-012 are the MQTT-defining integration tests:** MQTT QoS 1 is the textbook
> at-least-once channel — it WILL deliver duplicates (and, across reconnects/broker hand-offs,
> out of order). These two tests prove our end-to-end reliability sublayer (FR-023) delivers
> exactly-once-effective, in-order binds on top of MQTT's weaker guarantees, NOT relying on the
> broker — the corrected-framing thesis of this unit.

---

## 6. Regression — permanent regression set + baseline gate

The following become the **permanent regression set** for the `mqtt` leaf, gated by FR-067/SC-017
(`bash test/run_all_tests.sh` green before AND after every core-touching change; no merge over a
red baseline):

- **Unit (folds into `run_all_tests.sh`):** A-MQTT-1, A-MQTT-2, A-MQTT-3 (Section A); B-MQTT-1,
  B-MQTT-2 (Section B); C-MQTT-1, C-MQTT-2 (Section C). These pin the GLP-surface contracts
  (graceful close, ground-gate three-valued behavior, SRSW under the gate, declined-guard reject)
  and require no transport, so they run in the standard REPL suite.
- **Integration (the leaf's acceptance + parity gates):**
  - **IT-MQTT-1 (SC-001)** — the headline split-equivalence gate, Dart↔Dart AND the
    **Dart↔C# release gate** (FR-062). Permanent.
  - **IT-MQTT-2 (SC-003)** — the per-leaf bind-reactivation acceptance test; the `mqtt` leaf is
    "shipped" only while this is green (FR-016/FR-063). Permanent.
  - **IT-MQTT-3 (SC-008)** — dedup over QoS-1 duplicates; the headline correctness gate of this
    transport. Permanent.
  - **IT-MQTT-4 (SC-012)** — reorder recovery. Permanent.
- **Tie to the baseline gate (FR-067/SC-017):** the unit tests run inside `run_all_tests.sh`;
  the integration tests run in the cross-instance harness CI lane. Both lanes must be green for
  the `mqtt` leaf to count as shipped, and any core-touching change (guard evaluator, SRSW
  analyzer, parser, heap ingress, reliability sublayer) re-runs both before merge.

IT-MQTT-5/6/7 are kept as **leaf-conformance** tests (graceful/abrupt close, fault lattice, GC);
they are required for acceptance but, being slower/fault-injection tests, run in the integration
lane rather than the fast REPL suite.

---

## 7. Open items specific to the `mqtt` transport

- **OQ-T-MQTT-1 (establishment face).** Is the canonical `mqtt` establishment the request/accept
  handshake (recommended — works through a shared/embedded broker without either GLP end binding
  a listening socket), or also the listen/connect face when an embedded broker is co-located with
  the gateway? Both resolve to one registry; pick one canonical to avoid two ways to do one thing.
- **OQ-T-MQTT-2 (QoS level for data frames).** Confirm **QoS 1 + our dedup** over QoS 2 four-packet
  exactly-once. QoS 1 is lighter on constrained field links and our sublayer already dedups; QoS 2
  would be redundant overhead. (Recommendation: QoS 1; reject QoS 0 for data — no retransmission.)
- **OQ-T-MQTT-3 (CorrId vs MQTT 5.0 Correlation Data).** Do we keep our transport-uniform ground
  `CorrId` + reverse-link reply table, or map it onto MQTT 5.0 native Response Topic (0x08) +
  Correlation Data (0x09) as a leaf-local optimization? (Recommendation: keep our CorrId for
  uniformity; allow the leaf to mirror it onto Correlation Data when present.)
- **OQ-T-MQTT-4 (Last Will and Testament vs our monitor).** Confirm LWT and our `tempFail`/
  `permFail` are complementary (LWT notifies the broker/peer side; our monitor notifies the GLP
  program) and that a clean DISCONNECT RC 0x00 (which removes the Will) maps to `closed(LinkId,
  eos)` while an unintended drop fires LWT + surfaces `tempFail`→`permFail`.
- **OQ-T-MQTT-5 (topic naming / rendezvous topic).** The well-known rendezvous topic for the
  in-band `request(LinkId)` token, and per-direction inbound topic naming (device command topic
  vs gateway telemetry topic), are leaf-config; pin a scheme so two GLP ends agree without
  out-of-band setup. (Ties to FR-002 path-B rendezvous OQ-A3.)
- **OQ-T-MQTT-6 (MQTT version baseline).** MQTT 5.0 (Maximum Packet Size, AUTH, Response
  Topic/Correlation Data, reason codes) vs 3.1.1 fallback for legacy brokers. (Recommendation:
  target 5.0; the leaf degrades gracefully to 3.1.1 with our sublayer covering the missing 5.0
  reliability/metadata features.)
- **OQ-T-MQTT-7 (fragmentation threshold).** The over-Maximum-Packet-Size fragmentation boundary
  (FR-022) per broker's negotiated Maximum Packet Size; default chunk size for field links.
- **OQ-T-MQTT-8 (platform for T4 acceptance).** Which of Windows OR Android runs the real `mqtt`
  leaf for SC-003 (IT-MQTT-2); the other is covered hermetically on loopback (FR-063/FR-064).

---

## 8. Sources (cited)

- OASIS, **MQTT Version 5.0** (OASIS Standard) — CONNECT/CONNACK first-packet rule, keep-alive,
  Maximum Packet Size (no MQTT-level fragmentation), DISCONNECT RC 0x00 / Will-Message removal,
  Last Will and Testament, TCP "ordered, lossless, bi-directional" substrate, Response Topic +
  Correlation Data, :1883/:8883: https://docs.oasis-open.org/mqtt/mqtt/v5.0/mqtt-v5.0.html
- HiveMQ, **MQTT Essentials Part 6 — Quality of Service (QoS) Levels** — QoS 0/1/2, QoS-1
  at-least-once duplicates, DUP flag "not processed by broker or client", receivers must tolerate
  duplicates, QoS-2 four-packet exactly-once:
  https://www.hivemq.com/blog/mqtt-essentials-part-6-mqtt-quality-of-service-levels/
- EMQ, **MQTT 5.0 Packet Explained 02: PUBLISH & PUBACK** — PUBLISH/PUBACK handshake, packet
  identifier, QoS semantics: https://www.emqx.com/en/blog/mqtt-5-0-control-packets-02-publish-puback
- ThingsBoard, **Gateway MQTT API** — gateway proxies multiple devices over one MQTT connection;
  `v1/gateway/telemetry` (upstream) and `v1/gateway/rpc` (downstream commands); device→gateway→
  platform→gateway→device flow: https://thingsboard.io/docs/reference/gateway-mqtt-api/
- ThingsBoard, **IoT Gateway MQTT config**: https://thingsboard.io/docs/iot-gateway/config/mqtt/
- Azure IoT Edge downstream-device gateway — gateway as intermediary; native-protocol downstream
  + cloud uplink; buffering on cloud-link loss:
  https://oneuptime.com/blog/post/2026-02-16-how-to-connect-downstream-devices-to-azure-iot-central-through-an-iot-edge-gateway/view
- AWS IoT SiteWise — MQTT-enabled edge gateway:
  https://docs.aws.amazon.com/iot-sitewise/latest/userguide/mqtt-enabled-v3-gateway.html
- Google Cloud — IoT gateway / MQTT bridge:
  https://cloud.google.com/iot/docs/how-tos/gateways/mqtt-bridge
