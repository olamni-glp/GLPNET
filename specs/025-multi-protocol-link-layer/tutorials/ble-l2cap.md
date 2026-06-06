---
title: "Transport Unit: ble-l2cap (Bluetooth LE L2CAP CoC)"
subtitle: "Plan-stage tutorial + spec-level test plan (feature 025) — PRE-IMPLEMENTATION, ILLUSTRATIVE"
date: "2026-06-06"
scheme: "ble-l2cap"
---

> **HARD FRAMING (read first).** Every GLP primitive used here (`link_setup/4`,
> `server_listener/3`, `client_connector/3`, `request_link/4`, `accept_link/4`,
> `link_send/3`, `link_recv/3`, `link_monitor/2`, `link_close/1..2`) is **PROPOSED,
> pending Gabi's language-authority approval, and NOT YET IMPLEMENTED** (DESIGN-DOSSIER
> §2; `contracts/link-primitives.md`). All GLP in this file is **ILLUSTRATIVE** — it is
> not runnable today. All tests are **SPEC-LEVEL**: a realistic scenario + exemplar GLP +
> an expected OBSERVABLE outcome + a pass/fail oracle, made runnable only once the
> primitives land. Nothing here is implemented and nothing here may bind heap cells,
> forge bindings, relax SRSW, or map a disconnect to a logical Fail.
>
> **GLP invariants preserved EXACTLY:** SRSW (one reader / one writer per variable per
> clause, never relaxed by a flag), writer-MGU (binds only writers), three-valued
> unification (an un-arrived remote value behaves as an unbound local reader ⇒ SUSPEND,
> never a spurious FAIL), suspend-on-reader / reactivate-on-bind, bind-once, per-link
> FIFO, three-phase HEAD→GUARD→BODY. Writer-mode outputs are constructed in clause
> **HEADS**, never via `=` in the body. Every clause below carries `procedure` and (where
> exported) `exported procedure` declarations and has been **hand-checked for SRSW**.

---

## 1. Scenario — phone ↔ wearable connection-oriented data stream (web-researched)

**The deployment.** A phone (the central / app) and a battery-constrained wearable
peripheral (a fitness band or smartwatch) maintain a long-lived connection-oriented data
stream to **sync bulk data both ways**: the wearable streams up logged high-frequency
sensor data (heart-rate, accelerometer, sleep) that accumulated while offline, and the
phone streams down a firmware image / config blobs. The classic constrained path for this
is GATT notifications, but GATT serializes operations and layers ATT over L2CAP (extra
header + parse overhead, ~247 B ATT MTU), which throttles bulk transfer. **Bluetooth LE
L2CAP Connection-Oriented Channels (CoC) in LE Credit Based Flow Control Mode** are the
purpose-built alternative: a TCP-like, reliable, ordered, **bilateral full-duplex** byte
stream between exactly two devices, with built-in credit-based flow control and SDUs up to
64 KB. Nordic's Object Transfer Service (OTS) example explicitly uses **an L2CAP CoC
channel for the bulk object transfer** (firmware images, large logged datasets), dropping
the 4-byte GATT header overhead and unlocking substantially higher throughput than GATT
notifications.

**Why this transport fits feature 025.** L2CAP CoC is the BLE leaf that maps *cleanly and
honestly* onto our seam, because it is **already** a bilateral, ordered, reliable,
full-duplex, credit-flow-controlled stream:

- **Strictly bilateral (FR-005/FR-042).** A CoC is a dynamically-allocated channel between
  exactly two peers identified by an LE_PSM + channel IDs (SCID/DCID). No hub, no broker.
  FR-042 explicitly designates BLE CIS / L2CAP CoC as "an ordinary bilateral link
  satisfying FR-003..FR-008." (Note: BLE **LE-Audio BIS** true-multi-reader broadcast is a
  *different* primitive, OUT OF MVP and an open SRSW co-design tension — FR-041 — and is
  **not** designed here; see §7.)
- **Full-duplex (the B→A back-channel is native).** A CoC carries data in **both
  directions with independent credit pools per direction** — so the link's `In` and `Out`
  streams map straight onto the two directions, and the reverse direction is exactly the
  B→A back-channel / credit-back-channel the dossier unifies (DESIGN-DOSSIER §3).
- **Reliable + ordered (FIFO for free, FR-018).** L2CAP runs over the LE ACL link's
  reliable, in-order delivery, so per-link FIFO is preserved by the transport; our
  sequence/dedup sublayer rides on top for idempotent redelivery across reconnects.
- **Credit-based flow control == GLP suspension (FR-025).** Each credit grants the peer
  permission to send one LE-frame (K-frame); at zero credits the sender must stop. This is
  a byte-level dual of GLP's suspend-on-reader, and it is the per-scheme realization of the
  bounded-pipe credit/back-channel of DESIGN-DOSSIER §3.
- **Single-platform acceptance is honest here (T4 / FR-063).** Android exposes CoC as
  first-class sockets from **API 29 (Android 10)**: `listenUsingL2capChannel` /
  `createL2capChannel` and their insecure variants. **Android is the accepted platform for
  this leaf** (T4: one platform per leaf), with wire faults covered hermetically on the
  deterministic loopback transport (so we never need a phone in CI).

**Sources (cited):**

- Bluetooth Core Specification, Vol 3, Part A — *Logical Link Control and Adaptation
  Protocol Specification* (LE Credit Based Flow Control Mode; K-frame / credit-based
  frame; LE_PSM; LE Credit Based Connection Request/Response; L2CAP Flow Control Credit
  Ind; Disconnection Request):
  https://www.bluetooth.com/wp-content/uploads/Files/Specification/HTML/Core-54/out/en/host/logical-link-control-and-adaptation-protocol-specification.html
- Texas Instruments — *L2CAP* (BLE-Stack User's Guide): bidirectional CoC, MTU/MPS,
  `L2CAP_ConnectReq` / `L2CAP_SendSDU` / `L2CAP_FlowCtrlCredit` / `L2CAP_DisconnectReq`,
  link-layer fragmentation/recombination:
  https://software-dl.ti.com/lprf/sdg-latest/html/ble-stack-3.x/l2cap.html
- node-ble-host — *L2CAP CoC API* (MTU vs MPS, `Math.ceil((2 + sdu.length) / txMps)`
  segmentation with the +2 SDU-length header, per-frame credits, zero-credit halt &
  replenishment, **independent per-direction credit pools**, pause/resume RX):
  https://github.com/Emill/node-ble-host/blob/master/docs/api/l2cap-coc.md
- Android Developers — `BluetoothAdapter` / `BluetoothDevice` / `BluetoothServerSocket`
  L2CAP CoC API (`listenUsingL2capChannel(int)` / `listenUsingInsecureL2capChannel(int)`,
  `BluetoothServerSocket.getPsm()`, `createL2capChannel(int)` /
  `createInsecureL2capChannel(int)`; **min API 29 / Android 10**; secure = LE Secure
  Connections encryption):
  https://developer.android.com/reference/android/bluetooth/BluetoothAdapter
- Medium / *Bluetooth Demystified* — Nikheel Savant, *Performance Boost: Using L2CAP
  Socket Over GATT for Bluetooth Data Traffic* (CoC as a TCP-like reliable stream, GATT
  vs CoC overhead, throughput motivation):
  https://medium.com/bluetooth-demystified/performance-boost-using-l2cap-socket-over-gatt-for-bluetooth-data-traffic-2ef42cd6dfcf
- Medium — Girish Yadawad, *L2CAP implementation in Android* (client
  `createInsecureL2capChannel(psm)` + `connect()`; read/write via socket
  `inputStream`/`outputStream`; PSM discovered out-of-band):
  https://medium.com/@girishby90/l2cap-implementation-in-android-588f5b867f01
- Nordic DevZone — *L2CAP CoC for OTS* and OTS bulk-transfer usage (OTS uses an L2CAP CoC
  for the object transfer; CoC drops the 4-byte GATT header):
  https://devzone.nordicsemi.com/f/nordic-q-a/34970/android-l2cap-coc-for-ots
- Espressif — *BLE L2CAP Connection Oriented Channels (CoC)* (CoC = reliable,
  stream-oriented, connection-oriented, TCP-like, with flow control):
  https://docs.espressif.com/projects/esp-iot-solution/en/latest/bluetooth/ble_l2cap_coc.html

---

## 2. Protocol mapping — the uniform seam onto L2CAP CoC

The host seam is `ILinkTransport` / `ILinkEndpoint`
(`contracts/architecture-context.md` §3): **open / send-bytes / recv-bytes / close +
fault**, selected by `Scheme`. For `ble-l2cap`:

| Seam operation | L2CAP CoC realization (Android API 29+ shown) |
|---|---|
| `ListenAsync` (server-listener) | `BluetoothServerSocket sock = adapter.listenUsingL2capChannel(0)` (or `listenUsingInsecureL2capChannel(0)`); read `int psm = sock.getPsm()`; advertise `psm` out-of-band; `BluetoothSocket s = sock.accept()`. Maps to the LE Credit Based Connection **Response**. |
| `ConnectAsync` (client-connector) | `BluetoothSocket s = device.createL2capChannel(psm)` (or `createInsecureL2capChannel(psm)`); `s.connect()`. Maps to the LE Credit Based Connection **Request** (carries LE_PSM, SCID, MTU, MPS, initial credits). |
| `SendBytesAsync(frame)` | `s.getOutputStream().write(frame)` — the L2CAP layer segments the SDU into MPS-sized K-frames, spending one TX credit per frame; at zero credits it blocks/queues until the peer replenishes credits. |
| `RecvBytesAsync` | `s.getInputStream().read(buf)` — L2CAP reassembles K-frames (first K-frame carries the 2-byte SDU-length field) into the SDU; the stack issues credits back to the peer as buffers free. |
| `CloseAsync` | `s.close()` — L2CAP **Disconnection Request**. |
| `OnFault` | ACL link loss / encryption failure / channel disconnect → surfaced **out of band** of the data path and turned into `tempFail`/`permFail`/`closed` monitor terms by the reliability sublayer (§ below; FR-008/FR-043). |

**Establishment path — which FR-002 path applies.**

- **Path A (server-listener ↔ client-connector), the natural L2CAP fit.** This is the
  *direct* listen/connect pairing. The wearable (or the phone — establishment role is
  independent of data direction, FR-004) is the listener and publishes its
  dynamically-assigned PSM; the other end connects to it. This is the primary path for
  this leaf.
- **Path B (request_link / accept_link handshake) — needed because the PSM is dynamic.**
  L2CAP CoC PSMs (in the dynamic range) are **assigned at listen time** (`getPsm()`), so a
  connector cannot know the PSM a priori. In a real OTS-style deployment the PSM is carried
  **out-of-band over a GATT characteristic** before the CoC is opened. That out-of-band PSM
  exchange is exactly the FR-002 **request/accept rendezvous**: `request_link(LinkId, B,
  …)` ships a ground `request(LinkId)` token (carrying the agreed `Scheme="ble-l2cap"` and
  the rendezvous), the listener's `accept_link` matches it and replies with the concrete
  PSM, and both ends then converge on the SAME `'_link_setup'` registry — yielding an
  "equivalent established link" (FR-002), indistinguishable from Path A afterward. Either
  path lands at one bilateral CoC.

**The B→A back-channel mechanism.** A CoC is **full-duplex with independent per-direction
credit pools**. So:

- The link's `Out` stream is one direction of the CoC; the link's `In` stream is the other
  direction. Both are live simultaneously — no request/response turn-taking needed (unlike
  HTTP/1.1 long-poll).
- Application replies (B→A) ride the reverse direction natively.
- **Flow-control credits and the back-channel are ONE mechanism** (DESIGN-DOSSIER §3):
  the reverse direction carries both B→A application data and the credit/demand stream. At
  the byte level, L2CAP's own credit grants ARE the transport credit; at the logical level,
  a GLP credit-back-channel (`Stream(more)`) rides the same reverse direction. The program
  sees only **logical** credits (one `more` = permission for one stream term); byte-chunk
  credits live below the seam (max chunk for safety, min one frame for forward progress —
  no zero-window deadlock).

**TLS / security variant.** L2CAP CoC has two flavors, both present in the leaf:

- **Secure** = `listenUsingL2capChannel` / `createL2capChannel` → the channel requires
  **LE Secure Connections** encryption (bonded, AES-CCM link-layer encryption). This is the
  inter-device authenticated/encrypted variant — the BLE analogue of TLS-by-default.
- **Insecure** = `listenUsingInsecureL2capChannel` / `createInsecureL2capChannel` → no
  link-layer encryption.

For the link layer, FR-029's "inter-host links MUST be TLS-by-default" maps to: **a CoC
between two *distinct devices* (two hosts) defaults to the SECURE (encrypted) variant**;
the INSECURE variant is the deliberate opt-out. An in-process loopback / co-located link is
**not** inter-host (FR-029 carve-out) and uses the PLAIN loopback transport. Refusal of an
inter-host insecure CoC without an explicit opt-out is surfaced as `LinkRefused`.

**MTU / MPS / fragmentation + reliability.**

- **MTU** = the maximum SDU size for the channel (up to **65535**, per the CoC spec);
  **MPS** = the maximum K-frame (LE-frame) payload. An SDU larger than MPS is **segmented
  into ⌈(2 + sdu_len) / MPS⌉ K-frames** (the +2 is the SDU-length header in the first
  K-frame) and **reassembled** by the peer's L2CAP layer. This is the transport's own
  fragmentation — our serializer framing (FR-022: version byte + length/CRC +
  fragment/reassemble) sits **above** it, so a GLP term too large even for a 64 KB SDU
  fragments at our layer; the common small-term case fits in one SDU and L2CAP handles
  sub-SDU segmentation transparently.
- **Reliability** is provided by the LE ACL link (reliable, in-order) — so per-link FIFO
  (FR-018) and at-least-once are inherited from the transport. Our sequence/dedup +
  reorder-buffer sublayer (FR-020/FR-021) provides **exactly-once-effective** binds across
  **reconnects** (where the underlying ACL link is torn down and re-established and a frame
  could be redelivered) and is exercised hermetically on loopback.

**Graceful close (`[]`) vs abrupt (`link_close`) on L2CAP.**

- **Graceful (default) = stream-end `[]`.** The producer binds its `Out` tail to `[]`; the
  drainer flushes any pending SDUs, then issues the L2CAP **Disconnection Request**
  cleanly. The peer's `link_recv`/`consume([])` fires; the monitor emits the terminal
  `closed(LinkId, eos)` and ends. No primitive needed.
- **Abrupt = `link_close(LinkId)` / `link_close(LinkId, Reason)`.** Tears the CoC down
  regardless of stream state (early-stop, security kill, fault give-up) — directly closes
  the `BluetoothSocket`. The monitor emits `closed(LinkId, Reason)` (e.g. `permFail`-then-
  `closed` on a fault give-up). A CoC dying *without* a clean Disconnection Request (ACL
  loss because the wearable went out of range) is **not** an intentional close: it yields
  `tempFail(LinkId, link_lost)` within a bounded interval, then `permFail` on give-up —
  **never a logical Fail** (FR-044/FR-050).

---

## 3. Exemplar GLP — role-parameterized wearable sync over `ble-l2cap` (ILLUSTRATIVE)

One role-parameterized program (FR-011); both instances load the SAME source and boot with
their own ground `AgentId`. The phone (`"phone"`) connects; the wearable (`"band"`) listens
and publishes its dynamic PSM. We illustrate **establish → repeated send/receive → graceful
close**, and the **bounded-pipe credit/back-channel** (because L2CAP CoC's native
per-direction credits make it the natural place to show it).

All types are PROPOSED link-layer types (`contracts/link-primitives.md` §1); they live in a
new module, never in `self.glp` (the prelude is untouched, FR-067).

```prolog
-module(ble_l2cap_wearable_sync).

% --- PROPOSED link-layer types (contracts/link-primitives.md §1) ---
% LinkId ::= link_id(Scheme, Endpoint, Nonce).        (all ground)
% Link(In, Out) ::= ch(In, Out?).                       (a link end as a Channel)
% Fault ::= ok ; closed(LinkId, Reason)
%         ; tempFail(LinkId, Reason) ; permFail(LinkId, Reason).
% Credit ::= more.

% Stable, ground link identity for this sync link. The Endpoint names the wearable
% peripheral; the dynamic PSM is resolved BELOW the seam at establish time (Path B
% rendezvous), so it is NOT part of the ground LinkId the program writes.
procedure sync_link(LinkId).
sync_link(link_id("ble-l2cap", ep("wearable-band", 0), 1)).

% Sample payload: a batch of logged sensor readings to upload, and a window size.
procedure sensor_batch(Stream(Integer)).
sensor_batch([72, 73, 75, 74, 80, 78]).         % e.g. heart-rate samples (ground)
```

### 3.1 Role dispatch (branch on ground AgentId, FR-011)

```prolog
procedure main(AgentId?).
% The wearable LISTENS (publishes its dynamic PSM via the rendezvous) and UPLOADS
% its logged batch. Establishment role (listener) is independent of data direction
% (FR-004): here the listener is the data producer.
main(Me) :- Me? =?= "band" |
    sync_link(L),
    server_listener(L?, Link, Faults),
    run_band(Link?, Faults?).
% The phone CONNECTS and CONSUMES the uploaded batch, granting flow-control credits.
main(Me) :- Me? =?= "phone" |
    sync_link(L),
    client_connector(L?, Link, Faults),
    run_phone(Link?, Faults?).
```

SRSW hand-check (`main/1`): each clause reads `Me?` once under `=?=` (a ground-implying,
SRSW-relaxing guard); `L` is a fresh writer in `sync_link(L)` then read once as `L?`;
`Link`/`Faults` are fresh writers bound once by the establishment call, read once each by
`run_band`/`run_phone`. No cell has two readers or two writers. OK.

### 3.2 Wearable side — the bounded producer (forward data coupled to reverse credits)

The wearable produces sensor terms onto `Out` but MUST spend a credit per term, and the
credits arrive on the **reverse direction** (the CoC's B→A back-channel = the `In` stream
carrying `Credit` terms). It SUSPENDS when no credit is left — pure suspend-on-reader, no
buffer object (DESIGN-DOSSIER §3). Graceful close = binding `Out` to `[]`.

```prolog
procedure run_band(Link(Stream(Credit), Stream(Integer))?, FaultStream?).
% In  = inbound credit stream from the phone (the reverse/back-channel direction)
% Out = outbound sensor-data stream to the phone
run_band(ch(Credits?, Data), _Faults) :-
    sensor_batch(Batch),
    produce(Batch?, Credits?, Data).

% Spend one credit ('more') per produced item; suspend when none left.
procedure produce(Stream(Integer)?, Stream(Credit)?, Stream(Integer)).
produce([Item|Items], [more|Credits], [Item?|Data?]) :-
    ground(Item?) | produce(Items?, Credits?, Data).
produce([], _Credits, []).                       % source done -> graceful close (Out = [])
```

SRSW hand-check (`run_band/2`): `Credits?` read once (head), `Data` written once (head),
`Batch` fresh writer then read once. `produce/3` clause 1: `Item?` appears in guard
`ground(Item?)` and head cons `[Item?|Data?]` — legal ONLY because `ground/1` certifies
groundness (ground-implying guard relaxes SRSW, guards-reference §"Ground Guards"); `Items`,
`Credits`, `Data` thread once each; the `more` credit token is matched (read) once in the
head. Clause 2: `_Credits` is anonymous (writer nobody reads, exempt). No double
reader/writer. OK.

Note the **ground gate** on `Item?` is the ground-relay discipline (FR-010): only ground
terms cross the cut — no `_w`/`_r` placeholder, no embedded reader, ever reaches the wire.

### 3.3 Phone side — the bounded consumer (grants credits on the back-channel)

The phone seeds a window of credits onto its `Out` (which is the wearable's `In`), then
drains the data, topping up one credit per consumed item. It detects graceful close when the
data stream ends (`[]`).

```prolog
procedure run_phone(Link(Stream(Integer), Stream(Credit))?, FaultStream?).
% In  = inbound sensor-data stream from the wearable
% Out = outbound credit stream to the wearable (the reverse/back-channel direction)
run_phone(ch(Data?, Credits), _Faults) :-
    consume(Data?, Credits).

% Window of 3 in flight: seed three credits, then one fresh credit per item drained.
procedure consume(Stream(Integer)?, Stream(Credit)).
consume(Data, [more, more, more | Credits?]) :- drain(Data?, Credits).

procedure drain(Stream(Integer)?, Stream(Credit)).
drain([Item|Data], [more | Credits?]) :- ground(Item?) | use_sample(Item?), drain(Data?, Credits).
drain([], []).                                   % wearable closed (Data = []) -> end credits

procedure use_sample(Integer?).
use_sample(V) :- ground(V?) | '_output'(V?).
```

SRSW hand-check (`run_phone/2`): `Data?` read once, `Credits` written once. `consume/2`:
`Data` read once (passed to `drain`), the three-credit window + `Credits?` tail constructed
in the head writer-position (one fresh reader `Credits?`). `drain/3` clause 1: `Item?` in
`ground(Item?)`, `use_sample(Item?)`, and matched in head `[Item|Data]` — the head occurrence
binds `Item` (writer-construction returning the element) and the two body reads are
groundness-certified by `ground/1`; `Data`, `Credits` thread once each; `more` written once
in head. Clause 2 grounds out. `use_sample/1`: `V?` under `ground/1` then `'_output'`. No
double reader/writer. OK.

**End-to-end (illustrative trace).**

1. **Boot** — same source; one instance booted `"band"`, the other `"phone"`;
   `Me? =?= "band"` / `Me? =?= "phone"` selects the role clause (ground-AgentId,
   three-valued).
2. **Establish** — `"band"` `server_listener` → `'_link_setup'` opens the L2CAP listening
   socket, gets the dynamic PSM, publishes it via the rendezvous (Path B), mints `In`/`Out`
   + `Faults`. `"phone"` `client_connector` → `'_link_setup'` resolves the PSM and opens the
   CoC (secure variant, inter-device default). Same ground LinkId ⇒ one bilateral link.
   `produce` SUSPENDS immediately (no credit yet).
3. **Flow control / send** — `consume` seeds `[more, more, more | …]` on the phone's `Out`
   (the wearable's `In`); each `more` arrives, reactivating `produce` for one item; the
   band conses one ground sensor sample onto `Out`; the egress drainer serializes (version
   byte + length/CRC frame) and writes it to the CoC socket, spending one L2CAP credit
   per K-frame.
4. **Receive** — the phone's ingress deserializes, runs the dedup gate (FR-021), binds the
   `In`-tail (writer-MGU on the phone's LOCAL writer, FR-049), reactivating the suspended
   `drain` exactly once (FR-051). `drain` calls `use_sample` → `'_output'`. As each item is
   drained, one fresh `more` is conse'd onto the credit stream — keeping ≤3 in flight
   (FR-025 backpressure).
5. **Close** — the band's `produce([], [])` ends `Out` → clean L2CAP Disconnection Request
   → the phone's `drain([])` fires → both directions ended → host GC → `closed(LinkId, eos)`
   on the monitor.

**Scheme-swap invariance (FR-006/FR-013).** Switching `"ble-l2cap"` to `"wss"` / `"coap"`
in `sync_link/1` changes ONLY the `Scheme` in the LinkId; the GLP program above is
unchanged. (The CoC's native per-direction credits make the *transport* implementation of
the credit back-channel especially direct here, but the GLP surface is identical.)

---

## 4. UNIT test specs — REPL Section-A (runtime) + Section-B/C (type-check)

These are SPEC-LEVEL REPL tests for the primitives/guards THIS unit exercises, made
runnable once the PROPOSED primitives land. Each = goal + expected outcome. Section A =
runtime; B = positive type-check; C = negative type-check. They follow the
`test/run_all_tests.sh` section conventions.

> **Scope note.** The `@<`/`@>`/`@=<`/`@>=` family and the `atom`/compound-suspend/
> imported-reader fixes have their OWN unit tests in `contracts/guards.md` §1–§4; this unit
> does not duplicate them. It tests the link primitives and the **`ground/1` ground-relay
> gate** and **`=?=` role-dispatch** three-valued behavior that the exemplar relies on.

### Section A — runtime

| ID | Goal (illustrative) | Expected outcome (oracle) |
|---|---|---|
| A-BLE-1 | Load `ble_l2cap_wearable_sync.glp`; `main("band").` with a stub loopback CoC and a co-resident `main("phone").` | Both roles select; `'_output'` emits `72 73 75 74 80 78` in order; both quiesce **Done**; monitor ends with `closed(_, eos)`. |
| A-BLE-2 | `produce([99|_], [], Out).` (no credit available) | **Suspended** on the `[more|Credits]` reader (head unification on empty credit stream) — NOT failed, NOT done. Asserts credit-gated suspend (FR-025). |
| A-BLE-3 | After A-BLE-2, bind one `more` onto the credit stream from a sibling goal | The suspended `produce` **reactivates exactly once**, emits one item, re-suspends. Asserts reactivate-once (FR-051). |
| A-BLE-4 | `produce([], [more,more], Out).` | Succeeds with `Out = []` (graceful close); leftover credits ignored (`_Credits`). |
| A-BLE-5 | `link_send(hr(72), ch(In?, [hr(72)?|Out?]), Result).` (ground payload) | Succeeds; `Result = ch(In?, Out)` — ground term consed onto Out (ground-relay, FR-010). |
| A-BLE-6 | `link_send(hr(X), Ch, _).` with `X` an unbound **reader** | **Suspended** on `ground(Msg?)` — NOT a wire write, NOT a fail. Asserts the ground gate blocks open structures (FR-010/FR-050). |
| A-BLE-7 | `main(Me).` with `Me` an unbound **reader** | **Suspended** on `=?=` role dispatch (three-valued: unbound reader ⇒ suspend), reactivates once `Me` binds. |
| A-BLE-8 | `link_recv(M, ch([hr(80)|In], Out?), R).` | Succeeds; `M = hr(80)`, `R = ch(In?, Out)` (receive head-decons, self.glp `receive/3` shape). |
| A-BLE-9 | `link_recv(M, ch(In, Out?), R).` with `In` head unbound (un-arrived) | **Suspended** on the In-head reader — NOT failed (suspend-not-fail across the cut, FR-017/FR-050). |
| A-BLE-10 | Establish via Path B: `request_link`/`accept_link` with matching ground LinkId over a stub rendezvous | Both ends converge on one established `Link`; subsequent `link_send`/`link_recv` behave identically to Path A (FR-002 equivalent link). |

### Section B — positive type-check

| ID | Program shape | Expected |
|---|---|---|
| B-BLE-1 | `produce/3`, `consume/2`, `drain/3` with full `procedure` decls and the `ground/1` gates as in §3 | **Compiles** (SRSW satisfied via ground-implying guards; types consistent with `Stream(Integer)`/`Stream(Credit)`). |
| B-BLE-2 | `run_band/2`/`run_phone/2` over `Link(Stream(Credit),Stream(Integer))` / `Link(Stream(Integer),Stream(Credit))` | **Compiles** — the `Link(In,Out)` channel type composes with `produce`/`consume` (FR-006: link is a Channel above the seam). |
| B-BLE-3 | A clause reading a `ground/1`-grounded payload var **twice** (guard + head cons), as in `produce/3` clause 1 | **Compiles** — ground-implying guard relaxes SRSW (SC-006 positive). |

### Section C — negative type-check

| ID | Program shape | Expected |
|---|---|---|
| C-BLE-1 | `produce/3` clause 1 with the `ground(Item?)` guard **removed**, leaving `Item?` read in both the (now-absent) guard and head cons | **Rejected** by the SRSW analyzer — `Item` lacks a ground-implying guard yet is multiply read (SC-006 negative). |
| C-BLE-2 | `link_send` variant that conses a **non-ground** term (e.g. a clause without `ground(Msg?)`) onto `Out` | **Rejected** / flagged — base relay must gate `ground` (FR-010); an ungated send is not a valid base-layer clause. |
| C-BLE-3 | `run_band` reading `Faults?` AND threading it into the data path (aliasing monitor into `In`/`Out`) | **Rejected** — monitor stream must be independently observable from data (FR-008); aliasing breaks SRSW on the shared cell. |

---

## 5. INTEGRATION test specs — cross-instance over `ble-l2cap` via the harness

Cross-instance tests target the PROPOSED host harness
(`StartInstances`/`OpenLink`/`Inject`/`Drive`/`Capture`/`AssertEquiv`/`CloseLink`/`Stop`).
All randomness flows from one run seed. **Wire faults are injected only on the deterministic
loopback** (T4 keeps the leaf one-platform-cheap); the real Android L2CAP CoC leaf is tested
for **bind-reactivation feasibility (SC-003)** + **graceful/abrupt close**, with reorder/
loss/dup covered hermetically on loopback. Each test = name + setup + action + expected
OBSERVABLE outcome + the SC it satisfies.

| # | Name | Setup | Action | Expected OBSERVABLE outcome | SC |
|---|---|---|---|---|---|
| I-BLE-1 | **Split equivalence Dart↔Dart (headline)** | `StartInstances(ble_l2cap_wearable_sync, [{band,Dart},{phone,Dart}], seed)`; `OpenLink(band, phone, "ble-l2cap", {InterHost:false})` over the deterministic loopback CoC; capture the **unsplit** baseline first | `Drive(band, main("band"))` + `Drive(phone, main("phone"))` to quiescence; `Capture(phone)`; `AssertEquiv(merged, baseline, ByteIdentical)` | Phone stdout = `72 73 75 74 80 78`, **byte-identical** to the unsplit baseline; both Done; monitor `closed(_, eos)` | **SC-001** (Dart↔Dart) |
| I-BLE-2 | **Split equivalence Dart↔C# (parity gate)** | Same call site, `[{band,Csharp},{phone,Dart}]` (and the mirror `[{band,Dart},{phone,Csharp}]`) over loopback CoC | Same Drive/Capture/AssertEquiv `ByteIdentical` | Cross-runtime output byte-identical to baseline AND to I-BLE-1; **release gate** | **SC-001** (Dart↔C#), **SC-002** |
| I-BLE-3 | **Per-transport bind reactivation on real CoC (T4)** | `StartInstances([{band,Dart/Csharp},{phone,…}])`; `OpenLink` over the **real Android L2CAP CoC** leaf (secure variant), one platform (Android); consumer `Drive`n first so its reader is **suspended** | Band binds one ground value; it crosses the CoC; ingress binds the phone's local In-tail | The phone's previously-**suspended** reader **reactivates exactly once** (DriveResult goes Suspended→Done) and observes the exact value sent; one writer→reader bind crossed a real CoC | **SC-003**, **SC-002** |
| I-BLE-4 | **Backpressure bound (credit window)** | Loopback CoC; phone consumer **stalled** (no credits granted beyond the seeded window); band has a long batch | `Drive(band, …)`; inspect the outbound queue / DriveResult | Band's `produce` **Suspended** at the window edge; outbound queue stays bounded (≤ window); no OOM; the independent monitor link is unaffected (no head-of-line block) | **SC-013** |
| I-BLE-5 | **Reorder / loss recovery (hermetic, loopback)** | Loopback CoC; `Inject(Drop, link, …)` then `Inject(Reorder, link, …)` then `clearFault` to heal | `Drive` both; `Capture(phone)`; `AssertEquiv(merged, baseline, CausalInOrder)` | Reconstructed phone output equals the in-order baseline (sublayer engaged); with sublayer disabled, corruption is **detected**, never silently wrong | **SC-012** |
| I-BLE-6 | **Graceful close = stream-end `[]`** | Real CoC or loopback | Band finishes its batch (`produce([], [])`) | `CloseLink(link, Graceful)` semantics: phone's `drain([])` fires; monitor emits terminal `closed(LinkId, eos)`; clean L2CAP Disconnection Request observed | **SC-001** (close path), feasibility for **SC-003** |
| I-BLE-7 | **Abrupt close + permFail / fault liveness** | Loopback CoC; reader **suspended** mid-batch | `Inject(PeerKill, band, …)` (or `Inject(Partition)` then give-up) | Phone's suspended reader does **NOT** spuriously fail (DriveResult **Suspended**, not Failed); monitor stream gets `tempFail(LinkId, link_lost)` within bounded time, then `permFail` on give-up; a fault-guarded clause becomes reducible; `CloseLink(_, Abrupt)` → `closed(LinkId, permFail)` | **SC-010** (fault liveness) — supports **SC-003** close coverage |
| I-BLE-8 | **Inter-device TLS-by-default refusal** | `OpenLink(band, phone, "ble-l2cap", {InterHost:true, Tls:false})` (two distinct devices, insecure CoC, no opt-out) | Attempt establish | `LinkRefused` (FR-029): inter-host insecure CoC refused by default; the **secure** variant (`{Tls:true}`) succeeds — proving both variants present and the secure default holds | **SC-007** (secure-default arm) |

**Notes binding the harness guardrails.** No test binds heap cells or forges bindings —
binds cross ONLY via `link_send` through the two seams. No test maps the PeerKill/Partition
disconnect to a logical Fail — it asserts **Suspend + a monitor term**. No `_w`/`_r`
placeholder or embedded reader ever goes on the wire (every send is `ground`-gated). No
broker is modelled (L2CAP CoC is inherently broker-free). SRSW is never relaxed by a flag.

---

## 6. Regression — the permanent regression set + baseline gate

On implementation, these become the **permanent regression set** for the `ble-l2cap` leaf,
gated by the baseline (FR-067 / SC-017: `bash test/run_all_tests.sh` green before AND after
every core-touching change):

- **Section A → `test/run_all_tests.sh` Section A (runtime):** A-BLE-1 (full split
  smoke), A-BLE-2/3 (credit suspend + reactivate-once), A-BLE-6 (ground-gate blocks open
  structures), A-BLE-9 (suspend-not-fail on un-arrived In). These are the cheap, hermetic,
  no-hardware runtime assertions of the invariants this leaf must never regress.
- **Section B/C → Section B/C (type-check):** B-BLE-1/2/3 (compiles + SRSW-under-ground)
  and C-BLE-1/2/3 (SRSW negative, ground-gate negative, monitor-aliasing negative). C-BLE-1
  is the SC-006 negative anchor for this leaf.
- **Integration → the cross-runtime parity regression set:** **I-BLE-1 (Dart↔Dart) and
  I-BLE-2 (Dart↔C#) are the load-bearing baseline-gate regressions** — I-BLE-2 is the
  **release gate** (FR-062): the Dart↔C# round-trip over this transport MUST pass before
  the leaf (or the feature, if this is the chosen parity transport) ships. I-BLE-4/5
  (backpressure, reorder/loss) run hermetically on loopback in CI on every change. I-BLE-7
  (fault liveness) is a standing regression for the monitor lattice.
- **Hardware-gated (NOT in the always-green CI baseline):** I-BLE-3 (real Android CoC bind
  reactivation), I-BLE-6 (real CoC graceful close), I-BLE-8 (inter-device TLS refusal) run
  on the **Android acceptance rig** (T4: one platform) at leaf-acceptance time and on
  demand, not on every commit — so the baseline gate stays hardware-free while SC-003 is
  still an **executed** acceptance test (FR-016: not an inferred capability).

The baseline-gate tie: I-BLE-1/I-BLE-2 plus the Section-A/B/C set are the regression
contract; a change that reddens any of them (or the prelude load) blocks merge (FR-067).

---

## 7. Open items specific to `ble-l2cap`

- **O-BLE-1 (BIS multi-reader vs SRSW — OUT OF MVP, open tension).** BLE LE-Audio **BIS**
  true-multi-reader broadcast (FR-041) is a *different* primitive from this CoC leaf and is
  **not** designed here. Its SRSW tension (one unbound variable with N readers) is an
  explicitly open co-design item; the MVP broadcast model is N bilateral ground-copy links
  (FR-040), each of which could be a CoC. Flagged, not resolved.
- **O-BLE-2 (dynamic PSM ⇒ Path B is effectively mandatory).** Because the CoC PSM is
  assigned at listen time (`getPsm()`), the connector cannot know it a priori, so the
  request/accept rendezvous (FR-002 Path B, typically a GATT characteristic carrying the
  PSM) is the realistic establishment path even when the *transport-level* pairing is
  listen/connect. Confirm whether the rendezvous (the PSM-bearing GATT read) is modelled
  in-band over the same device connection (recommended) or as a separate bootstrap link
  (OQ-A3 / link-primitives OQ-4). The exemplar assumes in-band.
- **O-BLE-3 (credit coupling — logical vs L2CAP byte credits).** L2CAP CoC already has a
  per-direction byte/frame credit pool. DESIGN-DOSSIER OQ-F3 (the "potentially huge
  benefit" unification) is unusually concrete here: do we (a) let the program's logical
  `Stream(more)` credits ride the reverse CoC direction *independently* of L2CAP's own
  frame credits (recommended — GLP sees only logical credits; L2CAP credits below the
  seam), or (b) attempt to *bind* logical credits to L2CAP credit grants? The mapping of
  one logical `more` to ⌈(2+sdu)/MPS⌉ K-frame credits, and whether one reverse direction
  multiplexes both B→A data and credits or uses separate logical streams, is unresolved.
- **O-BLE-4 (MTU/MPS negotiation surfacing).** The negotiated MTU (≤65535) and MPS are
  per-CoC. Our serializer fragmentation (FR-022) sits above L2CAP's own SDU segmentation —
  confirm we never double-fragment pathologically (a term that fits one SDU should produce
  one logical frame even if L2CAP splits it into K-frames). Default per-link window N
  (DESIGN-DOSSIER OQ-F1) should likely default from the negotiated MTU/MPS for this leaf.
- **O-BLE-5 (secure variant = bonding, not just "TLS").** FR-029 "TLS-by-default" maps to
  LE Secure Connections, which requires **bonding** (pairing) — a heavier precondition than
  a TLS handshake. Confirm the inter-device default refuses an *unbonded/insecure* CoC and
  that the bonding step is a one-time provisioning concern outside the link primitives'
  data path. Also confirm origin authentication (FR-026) keys off the bonded device
  identity.
- **O-BLE-6 (`tempFail` give-up bound for BLE link loss).** BLE out-of-range / supervision-
  timeout link loss is common and recoverable (the wearable comes back in range). The
  `tempFail`→`permFail` give-up interval (a tuning parameter, not a correctness condition —
  spec Assumptions) should likely be *longer* for `ble-l2cap` than for a wired transport,
  so a transient range loss reconnects-and-redelivers (idempotent, FR-021) rather than
  prematurely declaring `permFail`. Default value is a per-leaf tuning open item.
- **O-BLE-7 (Android-only acceptance; Windows status).** Android (API 29+) is the accepted
  platform (T4/FR-063). Windows L2CAP CoC socket support is not assumed; this leaf is
  documented as **accepted single-platform (Android)** with the Windows case recorded as
  not-required (FR-064) unless a Windows BLE stack path is later validated.
