# Transport tutorial — `ws` / `wss` (WebSocket, RFC 6455)

**Status: PLAN-STAGE, PRE-IMPLEMENTATION. ILLUSTRATIVE ONLY — NOT RUNNABLE YET.**
Every link primitive used here (`link_setup` / `server_listener` / `client_connector` /
`request_link` / `accept_link` / `link_send` / `link_recv` / `link_monitor` / `link_close`)
is **PROPOSED, pending Gabi's language-authority approval** (CLAUDE.md §Language Authority;
DISCIPLINE §1.14) and **not yet implemented**. All GLP below is illustrative; every clause is
SRSW- and mode-checked by hand. All "tests" in §4–§6 are **spec-level**: a scenario + exemplar
GLP + expected observable outcome + a pass/fail oracle, made runnable once the implementation
lands. Nothing here is claimed implemented.

**Scope reminder (DESIGN-DOSSIER §6):** at this level **every link is peer-to-peer to the
immediate peer.** A WebSocket connection is exactly one bilateral socket between two endpoints;
there is no broker, fan-out node, or relay at this layer. Any pub/sub fan-out a real
collaborative deployment adds sits at *another* level and is out of scope here.

**Source precedence:** local `docs/` + the feature contracts (`DESIGN-DOSSIER.md`,
`contracts/link-primitives.md`, `contracts/guards.md`, `contracts/architecture-context.md`,
`contracts/example-http-link.md`) > Shapiro GLP papers > earlier concurrent-logic papers. Web
sources are cited for the transport's real semantics and a real deployment, never for GLP
semantics.

---

## 1. Scenario — a live collaborative-editing / telemetry session

**The real-world use case.** A multi-user collaborative editor (the Figma / Google-Docs /
Miro / Notion class of app) and a real-time telemetry dashboard both have the same shape: a
long-lived session in which **each side sends a continuous stream of small messages and the
other side must be pushed updates the instant they happen, with no client polling.** In a
collaborative editor the client→server stream is the local user's edit operations (and cursor
/ presence beats); the server→client stream is everybody else's edits, presence, and
acknowledgements, delivered in order. In a telemetry dashboard the device→server stream is
metric samples; the server→browser stream is the live-updating chart feed. Both demand a
**persistent, full-duplex, message-oriented, ordered** channel where the *server can push at
will* — the exact capability HTTP request/response lacks and that WebSocket was standardized to
provide ([WebSocket.org guide][ws-guide]; [RFC 6455 §1.5][rfc6455]).

Real deployments of this kind — Google Docs, Notion, Figma, Miro, Slack, GitHub Codespaces —
"have set new expectations for interactive teamwork experiences" and are built on a persistent
bidirectional WebSocket (or WebRTC) carrying edit operations one way and broadcast updates the
other ([Daydreamsoft, Real-Time Collaboration with CRDTs and OT][daydreamsoft]). The
higher-level conflict-resolution layer (CRDT / operational-transform) and any server-side
fan-out to *all* participants are above our seam; what we model is the **one bilateral link**
between two participants (or participant↔server), which is precisely the GLP link-layer unit.

**Why WebSocket is the cleanest fit for the GLP link.** WebSocket is the *native bidirectional*
transport in the lineup, so the GLP `Link(In, Out)` Channel maps **directly onto the two socket
directions** with no impedance mismatch:

- After one HTTP Upgrade handshake "each side can, independently, send data at will"
  ([WebSocket.org guide][ws-guide]) — so the server (listener) pushes B→A frames **natively**,
  with no long-poll, no response-body trick, no second connection. Contrast HTTP/1.1, whose
  back-channel must be faked with long-poll or chunked response bodies (`example-http-link.md`
  §4). This is why the dossier calls `ws`/`wss` "the natural fit" / "the cleanest bidirectional
  fit" (DESIGN-DOSSIER §6).
- It is a **single long-lived TCP connection carrying many messages in both directions**
  ([WebSocket.org guide][ws-guide]), matching the GLP link lifecycle "establish once,
  send/receive repeat, then close" (DESIGN-DOSSIER §4).
- It is **message-oriented, not byte-stream-oriented**: each WebSocket data frame (or
  fragmented message) is a self-delimited unit, so one serialized GLP `Frame` rides as exactly
  one WebSocket binary message — the `send-bytes`/`recv-bytes` seam needs no extra
  length-prefixing of its own at the transport boundary (RFC 6455 framing supplies it,
  [RFC 6455 §5.2][rfc6455]).
- Ordering and reliability come from the underlying TCP, and RFC 6455 additionally mandates
  that **"message fragments MUST be delivered to the recipient in the order sent by the
  sender"** ([RFC 6455 §5.4][rfc6455]) — directly satisfying per-link FIFO (FR-018/FR-053) at
  the transport, with our sequence/dedup sublayer enforcing it end-to-end regardless.

**Sources:**
[RFC 6455 — The WebSocket Protocol (rfc-editor.org)][rfc6455];
[WebSocket Protocol guide (websocket.org)][ws-guide];
[Real-Time Collaboration Using CRDTs and Operational Transform (daydreamsoft.com)][daydreamsoft].

---

## 2. Protocol mapping — the uniform seam over RFC 6455

The leaf implements the architecture-context `ILinkTransport` / `ILinkEndpoint` host seam
(`architecture-context.md` §3): `open` (= `ListenAsync` / `ConnectAsync`), `send-bytes`
(= `SendBytesAsync`), `recv-bytes` (= `RecvBytesAsync`), `close` (= `CloseAsync`), plus the
out-of-band `OnFault`. The GLP program never sees any of this — it sees only `Link(In, Out)`
and a `FaultStream` (FR-006/FR-013). The mapping:

### 2.1 Establishment path (listen / connect, FR-002 path A)

WebSocket establishment is a **client-initiated HTTP Upgrade handshake** ([RFC 6455 §1.3,
§4][rfc6455]), which maps onto the seam asymmetrically-by-role but symmetrically-by-data
(FR-003/FR-004):

| Seam call | WebSocket action | RFC 6455 |
|---|---|---|
| `server_listener(LinkId?, …)` → `'_link_setup'(…, listener, …)` → `ILinkTransport.ListenAsync` | bind+listen on host:port for the WS path; on an inbound client, validate `Upgrade: websocket` + `Sec-WebSocket-Key`, reply **`101 Switching Protocols`** with `Sec-WebSocket-Accept = base64(SHA1(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))` | §4.2.2 |
| `client_connector(LinkId?, …)` → `'_link_setup'(…, connector, …)` → `ILinkTransport.ConnectAsync` | send `GET` with `Upgrade: websocket`, `Connection: Upgrade`, random 16-byte base64 `Sec-WebSocket-Key`, `Sec-WebSocket-Version: 13`; verify the `101` + `Sec-WebSocket-Accept` hash | §4.1, §1.3 |

After the `101`, the connection "switches to a lightweight framed messaging format"
([WebSocket.org guide][ws-guide]) and the asymmetry is gone: **either end may send at will.**
This is the FR-004 guarantee — the listener (B) can be the data writer and the connector (A)
the reader, or vice versa. In the collaborative-editing scenario the *connector* (browser
client) is typically a writer of edits AND a reader of broadcasts, and the *listener* (server)
symmetrically both — the bidirectional `Link(In, Out)` exposes both regardless of who dialed.

The two ends converge on the **same `'_link_setup'` registry keyed by the ground `LinkId`**
(FR-007 idempotent-at-identity), so re-running setup reuses the existing socket rather than
opening a second one.

### 2.2 Establishment path (request / accept handshake, FR-002 path B)

For peer-introduction / NAT-rendezvous (`link-primitives.md` §2.4), `request_link` /
`accept_link` carry a ground `request(LinkId, FromPeer)` token over a rendezvous before the
socket exists. The recommended rendezvous (OQ-A3 / OQ-4) is **in-band over the WS connect**:
the requester opens the WebSocket and sends the `request(...)` token as its first binary
message; the acceptor reads it off its inbound `RequestStream` and, on `LinkId? =?= LinkId2?`,
establishes via `'_link_accept'`. Because WS is already a connection the client opens, this
needs no extra channel — the request frame rides the same socket that becomes the data link.
Both paths route through the same registry, yielding an indistinguishable established `Link`
(FR-002).

### 2.3 The B→A back-channel — native, not emulated

This is the WebSocket leaf's distinguishing property. After the handshake **the server can
transmit frames to clients without waiting for a request** ([WebSocket.org guide][ws-guide];
[RFC 6455 §5.5/§1.5][rfc6455]). So the GLP link's reverse direction (B→A) is a **first-class
socket direction**:

- The listener's `Out` stream (server→client) drains straight to `SendBytesAsync` as WebSocket
  binary data frames the client receives via `RecvBytesAsync` — no long-poll, no chunked
  response body, no second connection (contrast `example-http-link.md` §4).
- The **credit / demand back-channel is the same reverse direction** (DESIGN-DOSSIER §3 "KEY
  INSIGHT"): when a bounded-pipe consumer cons-es `more` onto its credit stream, those credit
  terms ride client→server WS frames exactly as edit operations do; the producer's data rides
  server→client (or the mirror). One socket multiplexes B→A application data and flow-control
  credits — which is exactly why WebSocket's native full-duplex makes the bounded-pipe model
  cheap here. (The exact logical-vs-byte-credit coupling is flagged OQ-F3 / [NEEDS
  ELABORATION].)

### 2.4 Framing, MTU, fragmentation, reliability

- **One GLP `Frame` = one WebSocket binary message.** The reliability-sublayer serializer
  produces an opaque `byte[]` blob (version byte + length/CRC + per-link sequence + epoch/fence
  + payload; `architecture-context.md` §4.2). It is shipped as a **binary** frame (opcode
  `0x2`), not text, since the payload is binary-parity TLV ([RFC 6455 §5.2][rfc6455]).
- **No small MTU.** Unlike CoAP (~1 KB) / BLE GATT (~20 B), WebSocket over TCP has no
  protocol-imposed small frame limit; payload length is encoded in 7 / 7+16 / 7+64 bits
  ([RFC 6455 §5.2][rfc6455]), so a single message can be huge. **Fragmentation is therefore a
  transport convenience, not a necessity here.** The leaf MAY use RFC 6455 message
  fragmentation (FIN-clear first frame + continuation opcode `0x0` frames + FIN-set final
  frame, [RFC 6455 §5.4][rfc6455]) for very large terms, and TCP/socket backpressure bounds
  in-flight bytes. Our own serializer's fragmentation/reassembly (FR-022) still runs for the
  *shared* cross-transport path but is rarely exercised on `ws`; the **maximum-chunk safety
  bound** (DESIGN-DOSSIER §3) still applies so an oversized/huge-arity frame fails safe within
  bounded memory (FR-028).
- **Masking.** RFC 6455 requires all **client→server** frames to be masked (32-bit random key,
  XOR per [RFC 6455 §5.3][rfc6455]); server→client frames are unmasked. This is wholly inside
  the leaf — the GLP layer never sees masking.
- **Reliability / ordering.** WebSocket runs on one TCP connection; TCP gives in-order,
  reliable byte delivery, and RFC 6455 §5.4 additionally mandates in-order fragment delivery
  ([RFC 6455 §5.4][rfc6455]). The link-layer sequence/dedup + reorder buffer (FR-020) still
  runs end-to-end (it must not *assume* the transport, FR-023), but on a healthy single WS
  socket it is effectively a pass-through; it earns its keep across a drop+reconnect (§2.6).
- **Keepalive / liveness.** WebSocket Ping (`0x9`) / Pong (`0xA`) control frames check the
  connection is alive ([WebSocket.org guide][ws-guide]; [RFC 6455 §5.5.2/5.5.3][rfc6455]).
  The leaf uses ping/pong (and TCP RST / missing pong) to drive the monitor lattice: missed
  pongs within the give-up interval ⇒ `tempFail`, deliberate give-up ⇒ `permFail` (FR-045/046).

### 2.5 TLS / security variant (`wss`)

- `wss` = WebSocket over TLS, default port **443** ([RFC 6455 §3][rfc6455]); the client "MUST
  perform a TLS handshake over the connection after opening the connection and before sending
  the handshake data" ([RFC 6455 §4.1][rfc6455]).
- **FR-029 (TLS-by-default for inter-host):** an inter-host link MUST use `wss`. The harness's
  `OpenLink(..., InterHost=true, Tls=false, ...)` MUST be refused with `LinkRefused` (FR-029);
  only `wss` (or an explicit, deliberate plain opt-out) is accepted inter-host. Loopback /
  co-located links MAY use plain `ws`.
- Origin authentication (FR-026): every received frame's claimed origin is checked against the
  link's owning peer in the reliability sublayer; a forged bind for a victim's global name from
  a non-owning peer is rejected — independent of (and in addition to) TLS.

### 2.6 Graceful close (`[]`) vs abrupt close (`link_close`) on WebSocket

WebSocket has a **clean closing handshake**: a Close control frame (opcode `0x8`) optionally
carrying a 2-byte status code; on receiving a Close the peer "MUST send a Close frame in
response", and after both Close frames the TCP connection is closed
([RFC 6455 §5.5.1, §7][rfc6455]). The two GLP close modes map cleanly:

| GLP close | WebSocket realization | Monitor term | RFC code |
|---|---|---|---|
| **Graceful** = producer binds `Out` tail to `[]` (DESIGN-DOSSIER §4) | drain remaining frames, then send Close frame with status **1000 (normal closure)**; complete the bilateral Close handshake | `closed(LinkId, eos)` | §7.4.1 1000 |
| **Abrupt** = `link_close(LinkId)` / `link_close(LinkId, Reason)` (the 9th primitive) | send Close frame with a non-normal status (e.g. **1001 going away** / app code) without waiting for stream drain; tear down regardless of stream state | `closed(LinkId, Reason)` then monitor ends | §7.4.1 1001 |
| **Disconnect that is NOT an intentional close** (TCP RST, missing pong, **1006 abnormal closure / no Close frame**) | detected by the leaf | `tempFail(LinkId, Reason)` → on give-up `permFail(LinkId, Reason)` — **never a logical Fail** | §7.4.1 1006 |

Note the sharp distinction the dossier insists on (DESIGN-DOSSIER §4): an *intentional* close
(graceful `[]` or abrupt `link_close`) yields a **terminal `closed(...)`** on the monitor; an
*unintentional* disconnect yields **`tempFail` then `permFail`**. WebSocket's status code
carries exactly this distinction on the wire (1000/1001 = intentional, 1006 = abnormal).

---

## 3. Exemplar GLP — collaborative-editing session over `wss` (ILLUSTRATIVE)

One **role-parameterized** program (FR-011); both REPL instances load the *same* file and boot
with their own ground `AgentId` (`editor_client` or `editor_server`). The shared logic variable
becomes a `wss` link. This exercises **establish → repeated send/receive → close**, and the
bounded-pipe credit back-channel on the server→client broadcast direction.

The unsplit baseline this is faithful to is the single-heap producer/consumer pair
(`example-http-link.md` §0): a producer binds values, a consumer reads them, observable output
is the consumed sequence.

```prolog
-module(ws_collab_demo).

%% ---- the one ground link identity both nodes compile in (never reused) ----
%% "wss" (TLS) because the two ends are on DIFFERENT hosts (FR-029 refuses plain "ws"
%% inter-host). ep(Host, Port) carries host + the WS port; Nonce = 1 for uniqueness.
procedure collab_link(LinkId).
collab_link(link_id("wss", ep("collab.example", 443), 1)).

%% ---- one entry point; ground AgentId selects the role (FR-011, the @/boot idiom) ----
procedure main(AgentId?).

%% CLIENT node boots  main(editor_client): connects, streams local edits out,
%% reads the broadcast feed in (with a bounded receive window).
main(Me) :-
    Me? =?= "editor_client" |
    collab_link(L),
    client_connector(L?, Link, Faults),
    run_client(Link?, Faults?).

%% SERVER node boots  main(editor_server): listens, reads the client's edit stream,
%% pushes a broadcast feed out on the native B->A direction.
main(Me) :-
    Me? =?= "editor_server" |
    collab_link(L),
    server_listener(L?, Link, Faults),
    run_server(Link?, Faults?).

%% ============================ CLIENT SIDE ============================

%% Send three local edit ops out, then read the broadcast feed off In.
%% Link is ch(In?, Out): Out = client->server edits; In = server->client broadcasts.
procedure run_client(Link(_, _)?, FaultStream?).
run_client(Link, _) :-
    local_edits(Edits),
    send_edits(Edits?, Link?, Link1),            %% stream edits out, then graceful close of Out
    recv_feed(Link1?).                            %% read broadcasts in

%% The local user's edit operations (ground terms; were the shared producer values).
procedure local_edits(Stream(Edit)).
local_edits([ins(0, "h"), ins(1, "i"), del(0)]).

%% Cons each ground edit onto Out (the self.glp send/3 shape), then end Out with []
%% (graceful close of the outbound direction -> WS Close 1000 once both dirs end).
procedure send_edits(Stream(Edit)?, Link(In, Edit)?, Link(In, Edit)).
send_edits([E|Es], Link, LinkOut?) :-
    ground(E?) |
    link_send(E?, Link?, Link1),
    send_edits(Es?, Link1?, LinkOut).
send_edits([], Link, Link?).                       %% no more edits; Out tail will be []d by host

%% Read the inbound broadcast feed head-by-head; suspend on the unbound In head.
procedure recv_feed(Link(Broadcast, _)?).
recv_feed(Link) :-
    link_recv(B, Link?, Link1),                   %% SUSPEND until a broadcast arrives; bind B
    apply_broadcast(B?),
    recv_feed(Link1?).
recv_feed(ch([], _)).                             %% EOS: server closed inbound feed; terminate (M5)

procedure apply_broadcast(Broadcast?).
apply_broadcast(B) :- ground(B?) | '_output'(B?).  %% observable: the applied broadcast

%% ============================ SERVER SIDE ============================

%% Read the client's edit stream in; push a bounded broadcast feed out on B->A.
procedure run_server(Link(_, _)?, FaultStream?).
run_server(Link, _) :-
    ingest_edits(Link?, Link1),                   %% drain client->server edits
    broadcast_feed(Link1?).                        %% push server->client feed

%% Drain inbound edits head-by-head until the client closes Out ([] arrives).
procedure ingest_edits(Link(Edit, Out)?, Link(Edit, Out)).
ingest_edits(Link, LinkOut?) :-
    link_recv(E, Link?, Link1),                   %% SUSPEND until an edit arrives
    record_edit(E?),
    ingest_edits(Link1?, LinkOut).
ingest_edits(ch([], _), ch([], [])).              %% EOS: client closed inbound; close pass-through (M5)

procedure record_edit(Edit?).
record_edit(E) :- ground(E?) | '_output'(E?).      %% observable: the recorded edit

%% Push a broadcast feed out, BOUNDED by a reverse credit stream (bounded pipe,
%% DESIGN-DOSSIER §3). The credit stream rides the same B->A reverse direction.
procedure broadcast_feed(Link(Credit, Broadcast)?).
broadcast_feed(Link) :-
    feed_items(Items),
    produce_bounded(Items?, Link?).

%% produce_bounded couples the forward Broadcast stream to the reverse Credit stream:
%% spend one `more` credit per item; SUSPEND (head unify on [more|Credits]) when none left.
procedure produce_bounded(Stream(Broadcast)?, Link(Credit, Broadcast)?).
produce_bounded([Item|Items], ch([more|Credits], [Item?|Out?])) :-
    ground(Item?) |
    produce_bounded(Items?, ch(Credits?, Out)).
produce_bounded([], ch(_, [])).                    %% source done -> close the Broadcast direction

procedure feed_items(Stream(Broadcast)).
feed_items([ack(1), ack(2), ack(3)]).
```

**SRSW / mode hand-check (per clause):**

- `main/1` (both clauses): `Me` writer-in-head → `Me?` read once in `=?=` guard
  (ground-implying relaxation, guards.md §"Three-valued"). `L` writer (`collab_link`) → `L?`
  read once. `Link`/`Faults` writers (output args of `client_connector`/`server_listener`) →
  each read once in `run_*`. Clean.
- `run_client/2`: `Edits` writer (`local_edits`) → `Edits?` read once. `Link` reader → threads
  into `send_edits` once; `Link1` writer (output) → read once in `recv_feed`. `_Faults` anon.
  Clean.
- `send_edits/3` clause 1: `E` reader (head list cell) → `E?` in `ground` guard + `link_send`
  payload = **2 reader uses, legal because `ground(E?)` certifies groundness** (guards.md
  §"Guards That Imply Groundness"; mirrors `link_send`'s own `ground(Msg?)` relaxation,
  link-primitives.md §2.5). `Es` reader → read once. `Link` reader → once (`link_send` in
  arg); `Link1` writer → once (recursive call). Output arg3 `LinkOut` is the **reader hole
  `LinkOut?` in the head** + the writer `LinkOut` once in the recursive body (canonical-forms
  card form 6: output produced by a body subgoal). Clause 2 (base): arg2 `Link` writer captures
  the channel; arg3 is the **reader-hole pass-through `Link?`** (card form 6 base case
  `send_edits([], Link, Link?)`; mirrors `merge([], Ys, Ys?)`). Clean.
- `recv_feed/1` clause 1: `B` writer (`link_recv` output) → `B?` read once (`apply_broadcast`,
  typed `Broadcast?` — the inbound stream carries server→client broadcasts, not `Edit`). `Link`
  reader → once; `Link1` writer → once (recursion). Clause 2 (EOS base `recv_feed(ch([], _))`):
  matches the closed inbound stream `[]`; `_` is the unread outbound slot. Terminates rather than
  suspending forever (card form 3 consumer-close). Clean.
- `apply_broadcast/1`, `record_edit/1`: `B`/`E` reader → 2 uses each under `ground/1`
  (ground-implying relaxation). `apply_broadcast` arg is typed `Broadcast?`. Clean.
- `ingest_edits/2` clause 1: `E` writer (`link_recv` output) → `E?` once (`record_edit`). `Link`
  reader → once; `Link1` writer → once (recursion). Output arg2 `LinkOut` is the **reader hole
  `LinkOut?` in the head** + the writer `LinkOut` once in the recursive body (card form 6).
  Clause 2 (EOS base `ingest_edits(ch([], _), ch([], []))`): inbound `ch([], _)` matches the
  closed inbound stream; arg2 **head-constructs the closed pass-through channel `ch([], [])`**
  (card form 3/4 — head construction, never a body `=`). Clean.
- `broadcast_feed/1`: `Items` writer (`feed_items`) → `Items?` once. `Link` reader → once.
  Clean.
- `produce_bounded/2` clause 1: `Item` reader (head list cell) → 2 uses under `ground(Item?)`
  (relaxation). `Items` reader → once. `Credits` reader (head, reverse credit stream) → once
  (recursion). `Out` writer (head cons of forward stream) → once (recursion). The head
  constructs both the forward `[Item?|Out?]` output and consumes the reverse `[more|Credits]` —
  **outputs in the head, never `=` in the body** (GLP-not-Prolog). Clause 2: the ignored credit
  slot is **bare `_`** (card form 7 — a named `_Credits` at an unused channel slot is rejected:
  "[codegen] Undefined variable"); forward `[]` constructed in head. Clean.

**Establish → repeat → close trace.** Client `client_connector` dials `wss://collab.example:443`
(TLS handshake, then `101`); server `server_listener` accepts. Same ground `LinkId` ⇒ one
bilateral link (FR-002/FR-005). Client streams `ins(0,"h")`, `ins(1,"i")`, `del(0)` out
(each ground-relayed as one WS binary frame); server's `ingest_edits` reactivates per frame,
prints each (FR-051). Server pushes `ack(1..3)` out on the native B→A direction, **bounded by
the client's `more` credits** (bounded pipe); client's `recv_feed` reactivates per broadcast,
prints each. Client ends `Out` with `[]` → graceful close → WS Close 1000 → `closed(LinkId,
eos)` on the monitor once both directions end (DESIGN-DOSSIER §4).

**Switching transport changes ONLY the scheme.** Replace `"wss"` with `"https"` / `"coap"` /
`"mqtt"` in `collab_link/1` and the program is unchanged (FR-006/FR-013).

---

## 4. UNIT test specs (REPL Section-A runtime + Section-B/C type-check)

These are **spec-level** REPL tests for the primitives/guards this WebSocket unit exercises,
in the established `test/run_all_tests.sh` section taxonomy (A = runtime, B = positive
type-check, C = negative type-check). Each = goal + expected outcome + oracle. **Runnable once
the PROPOSED primitives land**; until then they document the contract. Because the base ground
relay never crosses an open structure, the unit tests below validate the *GLP-surface* shapes
the leaf relies on (channel send/recv stream shapes, the bounded-pipe credit coupling, the
ground gate, the monitor-term guard reads) without needing a live socket.

### 4.1 Section A (runtime)

| # | Goal | Expected outcome | Oracle |
|---|---|---|---|
| A-WS-1 | `send_edits([ins(0,"h"),ins(1,"i")], ch(_In, Out), _L).` (drive the outbound cons shape) | `Out` bound to `[ins(0,"h"), ins(1,"i") \| _]`, in order | the cons order equals the input order (per-link FIFO surface, FR-018) |
| A-WS-2 | `recv_feed/1` over `ch([ack(1)\|_In], _Out)` with the head pre-bound | `'_output'(ack(1))` prints; then SUSPEND on the unbound `_In` tail | `DriveResult = Suspended` (not Failed/Deadlock); stdout = `ack(1)` |
| A-WS-3 | `recv_feed/1` over `ch(In, _Out)` with `In` **unbound** | goal **SUSPENDS** (suspend-not-fail, FR-017/FR-050); then bind `In = [ack(7)\|_]` from a sibling | reactivates **exactly once** (FR-051); stdout = `ack(7)` |
| A-WS-4 | `link_send( E?, ch(I?, [E?\|O?]), ch(I?, O))` with `E` an **unbound writer** then later ground | `ground(E?)` guard SUSPENDS until `E` is ground, then conses; **no `_w`/`_r` on the simulated wire** | suspends-then-succeeds; ground-relay invariant (FR-010/FR-040) |
| A-WS-5 | `produce_bounded([ack(1),ack(2),ack(3)], ch([more,more\|_C], Out))` (2 credits) | exactly 2 items consed onto `Out`; producer **SUSPENDS** awaiting the 3rd `more` | in-flight items ≤ window; `DriveResult = Suspended` (backpressure surface, SC-013) |
| A-WS-6 | `on_fault([tempFail(link_id("wss",ep("h",443),1), drop)\|_])` read with the §monitor watcher clauses | the `tempFail` clause fires; `handle_temp` runs | a fault is **ordinary data** read by an existing guard, never a 4th verdict (FR-043) |
| A-WS-7 | `on_fault([closed(link_id("wss",ep("h",443),1), eos)\|_])` | the `closed` clause fires (terminal clean-close term) | clean close emits `closed(...)`, distinct from `tempFail`/`permFail` (DESIGN-DOSSIER §4) |

`on_fault/1` watcher used by A-WS-6/7 (illustrative; ground monitor reads with existing guards).
`FaultStream = Stream(Fault)` where the authoritative `Fault` union (`link-primitives.md` §1) is
`Fault ::= ok ; closed(LinkId, Reason) ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).` —
`closed/2` is a `Fault` member, so the `closed(L, R)` clause below (and the `closed(LinkId, …)`
monitor terms in §2.6 / IT-WS-5 / IT-WS-7) type-check against `Stream(Fault)`:

```prolog
procedure handle_perm(LinkId?, Reason?).   %% illustrative external sink
procedure handle_temp(LinkId?, Reason?).   %% illustrative external sink
procedure handle_closed(LinkId?, Reason?). %% illustrative external sink
procedure on_fault(FaultStream?).
on_fault([permFail(L, R)|_]) :- ground(L?) | handle_perm(L?, R?).
on_fault([tempFail(L, R)|_]) :- ground(L?) | handle_temp(L?, R?).
on_fault([closed(L, R)|_])   :- ground(L?) | handle_closed(L?, R?).
on_fault([ok|Rest])          :- on_fault(Rest?).
```

### 4.2 Section B (positive type-check)

| # | Program shape | Expected |
|---|---|---|
| B-WS-1 | the full `ws_collab_demo` module (§3) | **type-checks and loads** — `Link(In,Out)`/`Channel` shapes, `Stream(Edit)`/`Stream(Broadcast)`/`Stream(Credit)` all well-typed; SRSW passes (the hand-check above) |
| B-WS-2 | a clause that reads `E` **multiply** after `ground(E?)` (as in `send_edits` clause 1) | **compiles** — `ground/1` is ground-implying, relaxes SRSW (SC-006 positive; guards.md §"Guards That Imply Groundness") |
| B-WS-3 | `produce_bounded/2` with the forward `[Item?\|Out?]` constructed in the **head** | **compiles** — writer-mode output in head, no body `=` (GLP-not-Prolog) |

### 4.3 Section C (negative type-check)

| # | Program shape | Expected |
|---|---|---|
| C-WS-1 | `send_edits` clause 1 with the `ground(E?)` guard **removed** but `E` still read twice | **SRSW-rejected** — without the ground-implying guard the double reader use is illegal (SC-006 negative) |
| C-WS-2 | `link_send` of a **non-ground** term lacking the `ground/1` gate (open structure on the relay) | **rejected** — base is ground-relay; an embedded reader on the wire is a `glink` concern, not base (link-primitives.md §3 "Why `ground/1` not `known/1`") |
| C-WS-3 | a fault read via a **declined** guard, e.g. `on_fault([F\|_]) :- F == permFail(_,_) \| ...` | **rejected at compile** — `==` is declined (FR-036; guards.md §5); canonical form is `~(=?=)` / `=?=` |

> The `@<`/`@>`/`@=<`/`@>=`, `atom/1`-fix, compound-suspend, and imported-reader unit tests are
> owned by the **guard facet** (`contracts/guards.md` §1–§4), not duplicated here. This unit
> consumes those guards (e.g. `=?=` for AgentId role selection and LinkId equality; `ground/1`
> for the relay gate) but does not re-specify them.

---

## 5. INTEGRATION test specs (cross-instance over `ws`/`wss` via the harness)

Cross-instance tests using the PROPOSED harness (`StartInstances` / `OpenLink` / `Inject` /
`Drive` / `Capture` / `AssertEquiv` / `CloseLink` / `Stop`). All randomness flows from one run
seed for deterministic replay. **Spec-level: runnable once the leaf + reliability sublayer
land.** Each = name + setup + action + expected observable outcome + the SC it satisfies.

Per the transport-author seam rule (prompt): **wire faults (drop/reorder/dup/delay) are
exercised hermetically on the deterministic loopback transport**, NOT on the live `ws` socket;
the `ws` leaf is tested for **bind-reactivation feasibility (SC-003)**, split equivalence
(SC-001), graceful/abrupt close, fault liveness on a real socket drop (SC-010), and
backpressure (SC-013). This keeps T4 cheap (one platform per leaf, FR-063).

### IT-WS-1 — Headline split equivalence over `wss` (SC-001) — **REGRESSION**

- **Setup.** `StartInstances(ws_collab_demo, [Inst(editor_client, Dart), Inst(editor_server,
  Dart)], seed)`. Capture the **unsplit baseline** first: run the same program in one instance
  with `producer(X)/consumer(X?)` sharing one heap variable; record `Capture.Stdout`.
- **Action.** `OpenLink(client, server, "wss", {InterHost:true, Tls:true})`; `Drive(server,
  main(editor_server), deadline)`; `Drive(client, main(editor_client), deadline)` to
  quiescence.
- **Expected observable.** Merged observable output of the split run = the unsplit baseline,
  **byte-identical**: `AssertEquiv(splitMerged, baseline, ByteIdentical)`. The recorded edits
  (`ins(0,"h")`, `ins(1,"i")`, `del(0)`) and the broadcast acks appear in send order.
- **Then Dart↔C#.** Re-run with `[Inst(editor_client, Csharp), Inst(editor_server, Dart)]`
  (and the mirror). Same `AssertEquiv(..., ByteIdentical)`. **This is the mandated cross-runtime
  parity gate (SC-001 Dart↔C#) and MUST pass before ship** (FR-062).
- **Satisfies:** SC-001 (Dart↔Dart then Dart↔C#), SC-002 (one complete writer→reader bind
  Dart↔C#), FR-018 FIFO.

### IT-WS-2 — Per-transport bind reactivation (SC-003, T4) — **REGRESSION**

- **Setup.** `StartInstances(...)`; `OpenLink(a, b, "wss", {InterHost:true, Tls:true})` on the
  accepted platform (Windows OR Android per FR-063).
- **Action.** Drive `b` so a reader (`link_recv`) **suspends** on an unbound `In` head
  (`DriveResult = Suspended`); then drive `a` to `link_send` exactly one ground term; let the WS
  frame arrive.
- **Expected observable.** `b`'s suspended reader **reactivates exactly once** (FR-051); the
  reconstructed value on `b` equals the value sent on `a`; a second `Capture(b)` shows the value
  once, not twice. `DriveResult(b)` transitions Suspended → Done.
- **Satisfies:** SC-003 (the gate that makes the `wss` leaf "shipped"), FR-016/FR-017.

### IT-WS-3 — Fault liveness on socket drop (SC-010) — **REGRESSION**

- **Setup.** `StartInstances(...)`; `OpenLink(a, b, "wss", {InterHost:true, Tls:true})`; `b`'s
  data goal (`recv_feed`) suspended on an un-arrived value; `b` also reads `Faults` via
  `link_monitor(LinkId?, Faults)` + the `on_fault` watcher (§4.1).
- **Action.** `Inject(FaultSpec(PeerKill, link, {}))` — terminate instance `a` mid-session (a
  real socket drop: TCP RST / missing pong, RFC 6455 §7.4.1 code 1006). Advance the logical
  give-up clock with `Inject(FaultSpec(ClockJitter, link, {advance: giveup_interval}))` (no real
  sleeps).
- **Expected observable.** (a) `b`'s suspended **data** goal does **NOT** spuriously fail —
  `DriveResult` stays `Suspended` for the unmonitored data path (FR-044/FR-050); (b) the monitor
  stream delivers `tempFail(LinkId, Reason)` within the bounded interval, then `permFail(LinkId,
  Reason)` on give-up (FR-046); (c) the `on_fault` watcher's `permFail` clause becomes reducible
  and `handle_perm` runs. `Capture(b).Faults` = `[…, tempFail(LinkId,_), permFail(LinkId,_)]`.
- **Satisfies:** SC-010, FR-043/FR-044/FR-045/FR-046.

### IT-WS-4 — Backpressure bound (SC-013) — **REGRESSION**

- **Setup.** `StartInstances(...)`; `OpenLink(...)`; server runs `broadcast_feed` (fast
  producer); client's `recv_feed` is **stalled** (consumer not granting `more` credits / not
  draining).
- **Action.** `Inject(FaultSpec(Delay, link, {stall_consumer:true}))` to hold the consumer;
  drive the server producer.
- **Expected observable.** The server producer **SUSPENDS** once the window of granted `more`
  credits is spent (`produce_bounded` head-unify on `[more|Credits]` with no credit) — the
  outbound queue stays **bounded**, no OOM; an independent second link makes progress (no
  head-of-line blocking across links). `DriveResult(server) = Suspended`; resident frame count
  ≤ window. Releasing the stall (`clearFault`) lets it resume and complete.
- **Satisfies:** SC-013, FR-025.

### IT-WS-5 — Graceful vs abrupt close (close-kind fidelity)

- **Setup.** Two sub-cases over `OpenLink(...)`.
- **Action / Expected.**
  - **Graceful:** `CloseLink(link, Graceful)` — producer ends `Out` with `[]`; the leaf sends WS
    Close **1000**; consumer's `consume([])`/`recv` sees stream-end. Monitor =
    `closed(LinkId, eos)`. `Stop` asserts GC-to-baseline (SC-014).
  - **Abrupt:** `CloseLink(link, Abrupt)` — `link_close(LinkId, going_away)`; the leaf sends WS
    Close **1001**; monitor = `closed(LinkId, going_away)` then ends; any in-flight reader stays
    safely suspended-not-failed, no logical Fail.
- **Satisfies:** the DESIGN-DOSSIER §4 graceful-vs-abrupt contract; FR-024 (resource reclaim,
  ties to SC-014).

### IT-WS-6 — TLS-by-default refusal (FR-029 / SC-007)

- **Setup.** `StartInstances(...)`.
- **Action.** `OpenLink(a, b, "ws", {InterHost:true, Tls:false})` — plain `ws` inter-host.
- **Expected observable.** **`LinkRefused`** (FR-029); the `wss` variant
  (`{InterHost:true, Tls:true}`) succeeds — proving both variants present and the secure default
  holds. Loopback `ws` (`InterHost:false`) is allowed.
- **Satisfies:** SC-007 (plain inter-host refused by default), FR-029, US2 acceptance #3.

### IT-WS-7 — Reorder/loss recovery, hermetic on loopback (SC-012)

- **Setup.** `StartInstances(...)`; `OpenLink(a, b, "loopback", ...)` — wire faults on the
  **deterministic loopback** transport, not the live `ws` socket (transport-author seam rule).
  Run the same `ws_collab_demo` logic (scheme-independent above the seam).
- **Action.** `Inject(Reorder)` + `Inject(Duplicate)` + `Inject(Drop)` (seeded).
- **Expected observable.** With the reliability sublayer engaged, the reconstructed merged
  output equals the in-order run: `AssertEquiv(out, baseline, CausalInOrder)` (or
  `MultisetEqual` for the loss-then-redeliver case); a duplicate frame is a **verified no-op**,
  not a crash (FR-021/SC-008). With the sublayer disabled, corruption is **detected**, never
  silently materialized.
- **Satisfies:** SC-012, SC-008 — proves the `ws` leaf's correctness rests on the shared
  sublayer validated hermetically (so the live `ws` test need not reproduce wire faults).

---

## 6. Regression set (the permanent gate; FR-067 / SC-017)

The following become the **permanent regression set** for the `ws`/`wss` leaf and are tied to
the baseline gate `bash test/run_all_tests.sh` (FR-067 / SC-017 — green before AND after every
core-touching change):

- **IT-WS-1 (SC-001), IT-WS-2 (SC-003), IT-WS-3 (SC-010), IT-WS-4 (SC-013)** — the four SCs the
  prompt mandates this leaf map to; IT-WS-1's **Dart↔C# arm is the cross-runtime release gate**
  (FR-062), so it must run on every ship.
- **Section-A unit tests A-WS-2/A-WS-3 (suspend-not-fail + reactivate-once)** and **A-WS-5
  (backpressure surface)** — fast, socket-free, guard the GLP-surface invariants on every run.
- **IT-WS-7 (SC-012/SC-008, hermetic on loopback)** — the duplicate-no-op / reorder-recovery
  proof that backs the `ws` leaf's correctness without live wire faults.
- **Section-C C-WS-1/C-WS-2/C-WS-3** — negative type-check guardrails (SRSW-without-ground-guard
  rejected; non-ground relay rejected; declined-guard rejected) run with the type-check suite.

Per FR-067/SC-017, none of these merge over a red baseline; the `=\=`-gated prelude must still
load. The Dart↔Dart arm of IT-WS-1 plus IT-WS-2 are the **minimum** "leaf is shipped" bar
(SC-003); the Dart↔C# arm of IT-WS-1 is the **ship** bar (FR-062).

---

## 7. Open items specific to the `ws`/`wss` transport

- **WS-OQ-1 (text vs binary frames).** This unit ships GLP `Frame`s as WebSocket **binary**
  frames (opcode `0x2`) because the serializer blob is binary-parity TLV. Confirm we never use
  text frames (`0x1`) for the data path; text might be wanted only for a future human-readable
  debug variant. (Ties to FR-060 byte-parity.)
- **WS-OQ-2 (ping/pong → monitor-lattice tuning).** The give-up interval that turns missed
  WebSocket pongs (and TCP RST / 1006) into `tempFail` → `permFail` is the
  DESIGN-DOSSIER §"tuning parameter, not a correctness condition." What is the `ws` default
  (vs the shared OQ-F1 per-link window)? Pongs give a cheaper liveness probe than CoAP CON —
  confirm we use them rather than only TCP-level detection.
- **WS-OQ-3 (RFC fragmentation vs our serializer fragmentation).** WebSocket has no small MTU,
  so our serializer's fragment/reassemble (FR-022) is rarely exercised on `ws`. Do we (a) leave
  one WS message = one `Frame` and never use RFC 6455 message fragmentation, or (b) use RFC 6455
  fragmentation for very large terms and skip our own? (a) keeps one code path; (b) leverages
  the native transport. Recommendation: (a) for parity simplicity (the shared sublayer path is
  the byte-identical Dart↔C# anchor, FR-061).
- **WS-OQ-4 (close-code mapping ratification).** Confirm the GLP→WS close-status map: graceful
  `[]` → **1000**, `link_close(L, R)` abrupt → **1001 / app code (3000–4999)**, abnormal →
  **1006**. The `Reason` carried in `closed(LinkId, Reason)` — is it the WS status code, the GLP
  reason term, or both? (Ties to OQ-C2 clean-close term name.)
- **WS-OQ-5 (browser-origin link end).** A real collaborative deployment's *client* end is a
  browser JS WebSocket, not a GLP REPL. For 025 both ends are REPL instances (Dart/C#), so the
  browser case is out of scope; flagged so a later facet does not assume a browser client. The
  immediate-peer-only rule means a browser↔server↔browser fan-out is **two** bilateral links at
  another level, never one logical hub (FR-005, DESIGN-DOSSIER §6).
- **WS-OQ-6 (platform matrix, T4 / OQ-T2).** Which platform hosts the `wss` leaf for the SC-003
  acceptance — Windows server-side (`System.Net.WebSockets` in the C# reference) and/or Android?
  WebSocket is feasible on both; pick the accepted one and document the other as not-required
  (FR-063/FR-064).

---

[rfc6455]: https://www.rfc-editor.org/rfc/rfc6455.html
[ws-guide]: https://websocket.org/guides/websocket-protocol/
[daydreamsoft]: https://www.daydreamsoft.com/blog/real-time-collaboration-features-using-crdts-and-operational-transform
