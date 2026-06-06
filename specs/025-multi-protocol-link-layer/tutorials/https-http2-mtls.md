---
title: "Transport Tutorial — `https` (HTTP/2 + mTLS) peer-to-peer link leaf"
subtitle: "Feature 025 multi-protocol-link-layer — plan-stage, PRE-IMPLEMENTATION, SPEC-LEVEL"
date: "2026-06-06"
status: |
  PLAN-stage. The link primitives are PROPOSED, pending Gabi's language-authority approval,
  and NOT YET IMPLEMENTED. All exemplar GLP is ILLUSTRATIVE (not runnable yet). All tests are
  SPEC-LEVEL: a realistic scenario + exemplar GLP + an expected OBSERVABLE outcome + a pass/fail
  oracle, made runnable once implementation lands. This document neither writes runnable test
  code against non-existent primitives nor claims anything is implemented.
scheme: "https"
---

# 0. Status, framing, and scope

This is the per-transport tutorial for the **`https`** leaf: **HTTPS = HTTP over TLS**, carried
over **HTTP/2** as a single long-lived **bidirectional** stream, with **mTLS (mutual TLS)** as
the security variant. It conforms to the consolidated design in
[`DESIGN-DOSSIER.md`](../DESIGN-DOSSIER.md) (the nine base primitives incl. `link_close`; the
scalar→stream→bounded-pipe model and the credit/back-channel unification; graceful stream-end
`[]` vs abrupt `link_close`; the monitor lattice `ok`/`closed`/`tempFail`/`permFail`; the guard
set; SC-001..SC-017), the feature [`spec.md`](../spec.md), and the contracts
[`link-primitives.md`](../contracts/link-primitives.md),
[`guards.md`](../contracts/guards.md),
[`architecture-context.md`](../contracts/architecture-context.md), and the worked
[`example-http-link.md`](../contracts/example-http-link.md) (which this tutorial specialises to
HTTP/2 + mTLS). It targets the shared
[`integration-harness-design.md`](../tests/integration-harness-design.md) interface.

Hard constraints honoured throughout (CLAUDE.md Language Authority; DISCIPLINE §1.14):

- **GLP semantics preserved EXACTLY** and non-negotiable: SRSW (one reader / one writer per
  variable per clause, **never** relaxed by a flag); writer-MGU (binds only writers, never
  reader/reader, never writer/writer); three-valued unification (an un-arrived remote value
  behaves as an unbound **local reader** ⇒ **SUSPEND**, never a spurious **FAIL**);
  suspend-on-reader / reactivate-on-bind; bind-once monotonicity; per-link FIFO; three-phase
  HEAD→GUARD→BODY. **GLP is not Prolog**: writer-mode outputs are built in clause **HEADS**, never
  via `=` in a body; every clause below carries `procedure`/type declarations and is
  hand-SRSW-checked.
- **The base link is ALWAYS peer-to-peer to the IMMEDIATE peer.** An mTLS API gateway / load
  balancer that terminates TLS is the *immediate peer* of one end (it IS the link end this side
  talks to); any further fan-out behind it (multiple backend services, a message bus) is at
  ANOTHER level and is OUT OF SCOPE here. No broker is ever a logical participant.
- Source precedence: local `docs/`/spec GLP > Shapiro GLP papers > earlier concurrent-logic
  papers > external RFCs/tooling (the last used only to ground the transport mapping and the
  real-world scenario, never to override a Tier-1 fact).

---

# 1. Scenario — cross-organization B2B data exchange requiring mutual authentication

## 1.1 The real-world deployment

Two organizations — say a **payment-initiation fintech (org A)** and an **account-holding
bank (org B)** — must exchange a continuous stream of transaction/confirmation records across
the public Internet, under a regulatory regime that demands **both parties cryptographically
prove their identity at the channel level**, not merely with a bearer token. This is the
canonical **Open Banking / FAPI 2.0** shape: in regulated industries — banking, payments,
healthcare, government — *a token alone is not sufficient*; **PSD3 and FAPI 2.0 require mutual
TLS for payment-initiation and account-information service providers**, and **HIPAA business
associate agreements increasingly demand channel-level machine identity** for ePHI APIs
([AWS — Introducing mTLS for API Gateway](https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/);
[Zerq — mTLS for B2B API partners](https://zerq.dev/blog/mtls-b2b-partner-api-authentication-setup)).

How it is deployed in practice (web-grounded):

- **Standard one-way TLS authenticates only the server to clients.** mTLS adds reciprocal
  verification: *the server requests that clients present X.509 certificates proving their
  identity* — "enabling business-to-business (B2B) applications where organizational partners
  must cryptographically prove their identity"
  ([AWS](https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/)).
- **Trust store / internal PKI.** Org B uploads a PEM-encoded **trust store** of the CA public
  keys it accepts; *partner A generates a client certificate signed by its organizational CA,
  org B adds A's CA public key to the truststore, and A presents its client certificate when
  calling the custom domain; the gateway validates the cert chain against the stored CA bundle*
  ([AWS](https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/)).
  In **strict mode** the listener "enforces client certificate authentication during the TLS
  handshake by requiring a valid client certificate"
  ([Azure Application Gateway — mutual authentication](https://learn.microsoft.com/en-us/azure/application-gateway/mutual-authentication-overview)).
- **FAPI 2.0** mandates **sender-constrained tokens via mTLS** (binding any token to the
  client's X.509 certificate), with mTLS the "original gold standard" for B2B service-to-service
  calls ([WorkOS](https://workos.com/blog/mtls-dpop-token-binding-sender-constrained-oauth);
  [Curity — mTLS client authentication](https://curity.io/resources/learn/oauth-client-authentication-mutual-tls/)).
- **Certificate lifecycle is short and rotating:** from March 2025 TLS certificate lifetimes are
  being reduced, heading to a 47-day maximum per the CA/Browser Forum mandate
  ([AWS / CA-Browser Forum context](https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/)),
  so rotation/rejection on the give-up path must be a normal, observable event.

## 1.2 Why this transport fits

- **Channel-level bilateral peer auth ⇒ FR-029 + FR-026.** `https` is **TLS-by-default**, so an
  inter-host link satisfies FR-029 ("plain inter-host refused by default") with no extra work.
  Layering **mTLS** on top makes the listener *require and verify the connector's client
  certificate while the connector verifies the listener's*, which directly strengthens FR-026
  (origin authentication): the link's two ends are cryptographically pinned identities, so a
  forged-origin frame from a non-owning peer is rejected **at the TLS layer before any GLP logic
  runs** — *"if the certificate isn't signed by a trusted CA or has been revoked, the connection
  is rejected at the TLS level before any application logic runs"*
  ([oneuptime — mTLS client verification](https://oneuptime.com/blog/post/2026-03-20-mutual-tls-mtls-client-verification/view)).
- **A single long-lived bidirectional stream ⇒ the `Link(In, Out)` shape.** HTTP/2's *stream is
  "an independent, bidirectional sequence of frames exchanged between the client and server
  within an HTTP/2 connection"* ([RFC 9113 §5](https://httpwg.org/specs/rfc9113.html)). One such
  stream maps one-to-one onto the GLP `Link(In, Out)`: connector(client) DATA = A→B = `Out`;
  listener(server) DATA = B→A = `In` — interleaved on one connection until END_STREAM. This is
  the natural carrier for a *continuous* B2B record stream (vs HTTP/1.1 request/response).
- **Reliable, in-order, flow-controlled ⇒ FR-018 FIFO + FR-025 backpressure for free.** HTTP/2
  runs over TCP (in-order, reliable) and *"the order in which frames are sent is significant;
  recipients process frames in the order they are received"* ([RFC 9113 §5](https://httpwg.org/specs/rfc9113.html)),
  and its credit-based **WINDOW_UPDATE** flow control maps onto the bounded-pipe model
  (§2.4 below).

**Sources for §1:**
[AWS API Gateway mTLS](https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/) ·
[Azure App Gateway mutual auth](https://learn.microsoft.com/en-us/azure/application-gateway/mutual-authentication-overview) ·
[Zerq B2B mTLS guide](https://zerq.dev/blog/mtls-b2b-partner-api-authentication-setup) ·
[Curity mTLS client auth](https://curity.io/resources/learn/oauth-client-authentication-mutual-tls/) ·
[WorkOS sender-constrained tokens](https://workos.com/blog/mtls-dpop-token-binding-sender-constrained-oauth) ·
[RFC 8446 TLS 1.3](https://datatracker.ietf.org/doc/html/rfc8446) ·
[oneuptime mTLS client verification](https://oneuptime.com/blog/post/2026-03-20-mutual-tls-mtls-client-verification/view) ·
[RFC 9113 HTTP/2](https://httpwg.org/specs/rfc9113.html).

---

# 2. Protocol mapping — the uniform seam over HTTP/2 + mTLS

The leaf adapts the uniform `ILinkTransport`/`ILinkEndpoint` seam (`open / send-bytes /
recv-bytes / close + fault`, architecture-context §3) to HTTP/2. Everything below the seam is
opaque bytes; the GLP program and the reliability sublayer never see HTTP framing.

| Uniform seam verb | HTTP/2 + mTLS realization |
|---|---|
| `open` (listener / `ListenAsync`) | Start an HTTP/2 **server** on the link's host:port (e.g. `nodeB.bank.example:8443`); **require + verify the client certificate** (mTLS strict mode) against the org-A CA in the trust store; accept exactly **one** long-lived HTTP/2 connection and one bidirectional stream as the link. |
| `open` (connector / `ConnectAsync`) | **Dial** the listener, complete the TLS 1.3 handshake **presenting org-A's client certificate** in response to the server's `CertificateRequest`, validate the server (org-B) certificate, then open **one** HTTP/2 stream (HEADERS, no END_STREAM) that stays open for the link's lifetime. |
| `send-bytes` (`SendBytesAsync(frame)`) | Write one self-delimiting reliability **Frame** as the payload of one (or more) HTTP/2 **DATA** frame(s) on the stream, in the calling end's direction. |
| `recv-bytes` (`RecvBytesAsync`) | Read DATA frame octets from the stream in the receiving direction, reassemble one self-delimiting reliability Frame, hand it up. |
| `close` (graceful) | Send DATA with **END_STREAM** in this direction — half-closes this direction (RFC 9113 §5.1). |
| `close` (abrupt) | **RST_STREAM** (and/or drop the connection) — immediate termination (RFC 9113 §6.4). |
| `fault` (`OnFault`) | Connection error, RST_STREAM(error), TLS failure, or silence → a `LinkFaultSignal` the reliability sublayer turns into `tempFail`/`permFail` monitor terms. |

## 2.1 Establishment path

Both FR-002 establishment paths are available and converge on the same `'_link_setup'` registry
keyed by the ground `LinkId` (so the resulting `Link` is indistinguishable — FR-002, FR-007):

- **Path A — listen/connect (the natural HTTP/2 one):** `server_listener` ⇒ `ListenAsync`
  (mTLS server); `client_connector` ⇒ `ConnectAsync` (mTLS client). This is the recommended
  path for `https`, because the TLS handshake **is** the rendezvous and carries the mutual auth.
- **Path B — request/accept handshake (FR-002 path B):** for NAT-traversal / peer-introduction,
  `request_link`/`accept_link` exchange a ground `request(LinkId, FromPeer)` token over an
  already-reachable rendezvous, then both ends route through `'_link_setup'`. For `https` the
  recommended rendezvous (OQ-A3) is **in-band over the connect**: the `request(...)` token is the
  first DATA frame on the freshly-opened HTTP/2 stream, so no extra channel concept is needed
  (link-primitives OQ-4 recommendation (c)).

Per FR-004, **which side listens is independent of who later writes**: the connector may be the
data reader and the listener the data writer. (In §3's exemplar the connector A is the writer —
the natural "client POSTs records to the bank" direction — but it need not be.)

## 2.2 The B→A back-channel mechanism

HTTP/2's stream is **symmetrically bidirectional on one connection**: after the connector opens
the stream (HEADERS, no END_STREAM), **the server can send DATA frames back on the same stream**,
interleaved with the client's DATA, until either side sends END_STREAM ([RFC 9113 §5, §6.1](https://httpwg.org/specs/rfc9113.html)).
So the B→A direction is **native** — no long-poll, no second connection:

- `Link(In, Out)`: connector-side `Out` = client DATA (A→B); connector-side `In` = server DATA
  (B→A). On the listener these are mirrored. Each direction is independently flow-controlled.
- This is the carrier for **application replies** (request/reply, CorrId — spec Key Entities) AND
  for the **program-visible credit/demand stream** of the bounded pipe (DESIGN-DOSSIER §3 KEY
  INSIGHT: flow control and the bidirectional link are ONE mechanism). Whether one back-channel
  multiplexes both B→A data and credits or uses separate logical streams is DESIGN-DOSSIER OQ-F3
  (recommendation in §2.4: logical credits ride the same reverse direction, multiplexed by frame
  kind below the seam).

> Contrast (recorded, for the corrected framing): over **HTTP/1.1** the same leaf would carry
> A→B as POST request bodies and B→A in response bodies / long-poll
> ([`example-http-link.md`](../contracts/example-http-link.md) §4). HTTP/2 supersedes that with a
> single genuine bidirectional stream, which is why `https`-over-HTTP/2 is the recommended shape.

## 2.3 TLS / security variant (the mTLS strengthening)

- **TLS-by-default (FR-029).** `https` is inherently TLS; an inter-host `"http"` link is refused
  by default and requires an explicit deliberate opt-out. Loopback / co-located links may use
  plain `"http"` (not inter-host — spec FR-029 definition).
- **mTLS = mutual TLS (the variant this tutorial centres on).** In TLS 1.3 the server sends a
  `CertificateRequest`; *"this message is omitted if client authentication is not desired"*
  ([RFC 8446 §4.3.2](https://datatracker.ietf.org/doc/html/rfc8446)). With mTLS the listener
  sends it, the connector responds with its `Certificate` + a `CertificateVerify` signature over
  the handshake transcript proving private-key possession, and *"if the certificate isn't signed
  by a trusted CA or has been revoked, the connection is rejected at the TLS level before any
  application logic runs"* ([oneuptime](https://oneuptime.com/blog/post/2026-03-20-mutual-tls-mtls-client-verification/view)).
- **How mTLS serves the reliability sublayer's origin auth (FR-026 / SC-007).** The verified peer
  certificate identity (subject/issuer DN) is the **owning-peer identity** that the per-message
  origin check binds to: a frame whose claimed origin ≠ the mTLS-verified peer of the link is
  rejected. mTLS makes the channel identity unforgeable *before* a frame reaches
  `handleMadAssignment`; the sublayer's per-message check (architecture-context §4.2 Security row)
  is the in-band complement for the relayed/broker case.
- **`LinkOptions{InterHost, Tls, Window}` in the harness** model this without real certs on
  loopback: `InterHost && !Tls ⇒ LinkRefused` (FR-029); `Tls=true` with `scheme="https"`
  succeeds; a forged-origin **frame** is still injected at the corpus layer (§5) to test the
  in-band origin check independent of the TLS layer.

## 2.4 MTU / fragmentation and reliability

- **No small-MTU constraint (unlike CoAP/BLE).** HTTP/2 DATA frame payloads are bounded by
  `SETTINGS_MAX_FRAME_SIZE`, negotiable between **2^14 (16,384)** and **2^24−1 (16,777,215)**
  octets ([RFC 9113 §4.2](https://httpwg.org/specs/rfc9113.html)). A reliability **Frame** larger
  than the negotiated max simply spans multiple DATA frames **in order on the stream**, so the
  sublayer's fragmentation/reassembly (FR-022) is exercised only for very large terms — and the
  GLP program never sees a partial term (the sublayer reassembles before `handleMadAssignment`).
- **Reliability is layered (FR-018/020/021):** HTTP/2 over TCP gives in-order, reliable,
  exactly-once *byte* delivery and significant frame ordering ([RFC 9113 §5](https://httpwg.org/specs/rfc9113.html)).
  On top, the link's own per-link **sequence + global-name dedup key**, **reorder buffer**,
  **version byte + length/CRC**, and **epoch/fencing** still run end-to-end (architecture-context
  §4) — because FR-023 requires these be enforced by the sublayer, **not assumed** of any
  intermediary (an mTLS gateway / L7 proxy on the path is exactly such an intermediary).
- **Flow control ⇒ the bounded pipe (FR-025 / DESIGN-DOSSIER §3).** HTTP/2 flow control is
  *credit-based via WINDOW_UPDATE frames; only DATA frames are flow-controlled; the initial window
  is 65,535 octets, at both stream and connection level* ([RFC 9113 §5.2, §6.9](https://httpwg.org/specs/rfc9113.html)).
  This is the **byte-level (transport) credit** below the seam; the **logical (GLP) credit** =
  one stream term (`more` on the reverse credit stream) bounds in-flight TERMS above the seam.
  They compose: one logical credit may consume several WINDOW_UPDATE byte-credits when a large
  term fragments (DESIGN-DOSSIER §3 "Credit granularity"; max chunk for safety, min one octet for
  progress — no zero-window deadlock). A stalled consumer's shrinking window makes the producer
  **SUSPEND** (pure suspend-on-reader), never OOM (SC-013).

## 2.5 Graceful close (`[]`) vs abrupt close (`link_close`)

- **Graceful = stream-end `[]`.** The producer binds its `Out` tail to `[]`; the leaf sends DATA
  with the **END_STREAM** flag in that direction, half-closing it (RFC 9113 §5.1 half-closed
  (local)/(remote)). The peer's `consume([])` fires. The host runs per-link GC and emits a
  terminal `closed(LinkId, eos)` on the monitor, then ends the monitor stream (DESIGN-DOSSIER §4).
  No primitive needed.
- **Abrupt = `link_close(LinkId)` (the 9th primitive).** Tear down regardless of stream state
  (early-stop / fault give-up / security kill — e.g. an expired/revoked client cert on rotation).
  The leaf sends **RST_STREAM** (RFC 9113 §6.4) and/or drops the connection; the host emits
  `closed(LinkId, Reason)` for an intentional close, or — for a *non-intentional* disconnect —
  `tempFail` then `permFail`, **never a logical Fail** (FR-044/FR-050).

---

# 3. Exemplar GLP (ILLUSTRATIVE, PROPOSED primitives)

**NOT runnable yet** — uses the PROPOSED base primitives. One **role-parameterized** program
(FR-011): both orgs load the same file and boot with their ground `AgentId`; the role branches on
that ground id. It exercises **establish → repeated send/receive → close**, and the **bounded-pipe
credit/back-channel** (the reverse credit stream riding the native HTTP/2 B→A direction). Every
clause carries type + `procedure` declarations; writer outputs are constructed in HEADS; SRSW is
hand-checked after each block.

## 3.1 Shared types and the one ground link identity

```prolog
-module(b2b_https_stream).

% Reuse the PROPOSED link-layer types (programs/lib/link.glp): LinkId/Link/Fault.
% "https" because the two ends are on DIFFERENT hosts; FR-029 refuses plain "http"
% inter-host. The mTLS variant is selected below the seam by LinkOptions (host-side),
% NOT in GLP logic (FR-006: no transport detail leaks into the program).
procedure b2b_link(LinkId).
b2b_link(link_id("https", ep("nodeB.bank.example", 8443), 1)).

% The application payload: one B2B transaction record (all ground — ground-relay, FR-010).
Record ::= txn(Integer, Integer).        % txn(Id, AmountCents) — illustrative ground compound

% The reverse demand/credit token (DESIGN-DOSSIER §3): one `more` = permission for one term.
Credit ::= more.
```

## 3.2 The role selector (one program, branch on ground AgentId — FR-011)

```prolog
% arg-0 is the ground AgentId (the @/boot idiom). The fintech (org A) boots main("fintech");
% the bank (org B) boots main("bank"). Establishment role is INDEPENDENT of data direction
% (FR-004): here the fintech connects+writes records, the bank listens+reads and grants credit.
procedure main(AgentId?).

main(Me) :-
    Me? =?= "fintech" |
    b2b_link(L),
    client_connector(L?, Link, Faults),          % establish: mTLS HTTP/2 client (connect)
    run_fintech(Link?, Faults?).

main(Me) :-
    Me? =?= "bank" |
    b2b_link(L),
    server_listener(L?, Link, Faults),           % establish: mTLS HTTP/2 server (listen+verify)
    run_bank(Link?, Faults?).
```

SRSW hand-check (each clause independently): `Me` writer-in-head → `Me?` read once in the guard
(`=?=` is ground-implying, so even a single read is fine). `L` writer (from `b2b_link`) → `L?`
read once. `Link`, `Faults` writers (output args of `client_connector`/`server_listener`) → each
read once in `run_*`. No variable has two readers or two writers. Clean.

## 3.3 The bank side — bounded receive that grants credit on the reverse direction

The bank reads records off `In` and, for each, sends one `more` credit back on `Out` (the native
HTTP/2 B→A direction). This is the bounded pipe: the fintech may only have `Window` records
in flight (DESIGN-DOSSIER §3). `Link(In, Out) = ch(In, Out?)`.

```prolog
procedure run_bank(Link(Record, Credit)?, FaultStream?).
% Open a window of 3 (3 initial credits), then drain with one credit per consumed record.
% Inbound record stream: In is a writer in the head, read once as In? in the body.
% Outbound credit stream: the three initial `more` credits are constructed in the HEAD;
% the residual tail Out? is a reader in the head, threaded once as the writer Out into the
% body -- exactly the send/3 / receive/3 writer-in-head/reader-in-body threading discipline
% (self.glp:94,97). One writer + one reader for each of In and Out. SRSW clean.
run_bank(ch(In, [more, more, more | Out?]), _) :-
    drain_records(In?, Out).

procedure drain_records(Stream(Record)?, Stream(Credit)).
% Got a record -> use it, replenish one credit in the HEAD (writer-construction), recurse.
drain_records([txn(Id, Amt) | In], [more | Out?]) :-
    ground(Id?), ground(Amt?) |
    record_txn(Id?, Amt?),
    drain_records(In?, Out).
% Producer closed the data direction (graceful [] ) -> stop granting credit (close reverse []).
drain_records([], []).

procedure record_txn(Integer?, Integer?).
record_txn(Id, Amt) :- ground(Id?), ground(Amt?) | '_output'(txn(Id?, Amt?)).
```

SRSW hand-check:
- `run_bank/2`: head deconstructs the channel — `In` is a **writer** in the head (the inbound
  record stream the ingress fills) → read **once** as `In?` in the body (`drain_records(In?, …)`).
  The outbound slot `[more, more, more | Out?]` constructs three credit terms **in the head**
  (writer-mode output, no body `=`); its residual tail `Out?` is a **reader** in the head →
  threaded **once** as the writer `Out` into the body. Each of `In`/`Out` is one writer + one
  reader (the `send/3`/`receive/3` threading idiom, `self.glp:94,97`). `_Faults` anonymous
  (unread). Clean.
- `drain_records/2` clause 1: `Id`/`Amt` writers from the head list cell; each read **twice**
  (once in `ground/1`, once in `record_txn`) — **legal** because `ground(Id?)`/`ground(Amt?)`
  are ground-implying guards (guards-reference "Guards That Imply Groundness"; certifies
  groundness ⇒ SRSW relaxation). `In` writer → `In?` read once. `[more | Out?]` constructs one
  credit in the head; `Out?` read once. Clean.
- `drain_records/2` clause 2: empty head, empty body. Clean.
- `record_txn/2`: `Id`/`Amt` read twice each, both under `ground/1` (relaxation). Clean.

## 3.4 The fintech side — credit-gated send, then graceful close

The fintech sends one record per credit received on `In` (the reverse credit stream). It
SUSPENDS when no credit is available (pure suspend-on-reader). When its source list is exhausted
it closes the data direction with `[]` (graceful close).

```prolog
procedure run_fintech(Link(Credit, Record)?, FaultStream?).
% Extract the two bare streams from the link end ONCE (Credits = reverse credit stream In,
% Out = outbound record stream), then recurse on bare streams (the DESIGN-DOSSIER §5 /
% example-http-link shape: work on the raw Out stream, not the channel record). This avoids
% hand-decomposing both channel slots inside a recursive head.
run_fintech(ch(Credits, Out?), _) :-
    produce_records([txn(1, 500), txn(2, 1200), txn(3, 750)], Credits?, Out).

procedure produce_records(Stream(Record)?, Stream(Credit)?, Stream(Record)).
% Have a record to send AND a credit has arrived (the reverse credit stream head is `more`):
% spend the credit (matched in the head), ground-relay the record by consing it onto Out in
% the HEAD (writer-construction, the produce/2 shape), and recurse on the stream tails.
produce_records([R | Rs], [more | Credits], [R? | Out?]) :-
    ground(R?) |
    produce_records(Rs?, Credits?, Out).
% Source exhausted -> graceful close: bind the outbound stream tail to [] in the HEAD
% (stream-end []); the bank's drain_records([], []) fires. No primitive needed.
produce_records([], _, []).
```

SRSW hand-check:
- `run_fintech/2`: head deconstructs the channel — `Credits` is a **writer** in the head (the
  reverse credit stream the ingress fills) → read **once** as `Credits?` in the body
  (`produce_records` arg 2); the outbound slot's `Out?` is a **reader hole** in the head → threaded
  **once** as the writer `Out` into the body (`produce_records` arg 3). This is the consumed-channel
  head `ch(In, Out?)` per `Channel(In,Out) ::= ch(In, Out?)` — the same writer-in-head/reader-hole
  threading discipline as the sibling `run_bank` and `self.glp:94,97`. `_Faults` anonymous. The
  record list is a ground literal. Each of `Credits`/`Out` is one writer + one reader. Clean.
- `produce_records/3` clause 1: `R` writer from the head list cell → `R?` read **twice** (in
  `ground/1` and in the head cons `[R? | Out?]`) — legal under the `ground(R?)` relaxation
  (ground-implying guard certifies groundness). `Rs` writer → `Rs?` read once. The credit head
  `[more | Credits]` spends one credit (the `more` matched in the head, the window gate) — its
  tail `Credits` is a writer in the head → `Credits?` read once in the recursive call. The
  outbound `[R? | Out?]` conses the ground record **in the head** (writer-mode output, no body
  `=`, exactly the DESIGN-DOSSIER §3 `produce/3` shape); its tail `Out?` is a reader in the head
  → threaded once as the writer `Out` in the recursive call. **No `_w`/`_r` placeholder, no
  embedded reader crosses the wire** (ground-relay, FR-010/040, certified by `ground(R?)`).
  Each variable: one writer + one reader. Clean.
- `produce_records/3` clause 2: `[]` source; the outbound tail is bound to `[]` in the head
  (graceful close construction); `_Credits` anonymous (the credit stream is done with). Clean.

This is the bounded pipe of DESIGN-DOSSIER §3 verbatim (`produce(Items, Credits, Data)`): the
producer spends one credit per element and SUSPENDS on `[more|Credits]` when none is left — pure
suspend-on-reader, no buffer object. The credit stream IS the reverse (B→A) HTTP/2 direction.

## 3.5 Optional fault watcher (faults are DATA — existing guards only, FR-043)

A separate goal may read the monitor stream; a goal that does **not** read it stays safely
suspended across a disconnect (FR-044). No fourth verdict — ordinary stream consumption.

```prolog
procedure watch(FaultStream?).
watch([ok | Rest])              :- watch(Rest?).
watch([closed(L, Reason) | _])  :- ground(L?) | note_closed(L?, Reason?).
watch([tempFail(L, R) | Rest])  :- ground(L?) | note_temp(L?, R?), watch(Rest?).
watch([permFail(L, R) | _])     :- ground(L?) | note_perm(L?, R?).

procedure note_closed(LinkId?, Reason?).
note_closed(L, R) :- ground(L?), ground(R?) | '_output'(closed(L?, R?)).
procedure note_temp(LinkId?, Reason?).
note_temp(L, R) :- ground(L?), ground(R?) | '_output'(tempFail(L?, R?)).
procedure note_perm(LinkId?, Reason?).
note_perm(L, R) :- ground(L?), ground(R?) | '_output'(permFail(L?, R?)).
```

SRSW: in `watch/1` each clause reads the head's `L`/`R` writers once (and `Rest?` once where
present), all `L`/`R` reads under `ground/1`; the `closed`/`permFail` arms intentionally do not
recurse (terminal). The `note_*` helpers read each arg twice under `ground/1` (relaxation). The
`tempFail` arm recurses (`tempFail` is recoverable; `permFail`/`closed` are terminal — matching
the lattice). Clean.

## 3.6 What the split reproduces

Unsplit, the bank's `record_txn` would print three `txn(...)` lines as a single-heap program
consumes a shared record stream. Split over `https`, the fintech's records are **ground-relayed**
across the cut: the bank's `In` tail is bound by the ingress, reactivating `drain_records`
exactly once per record (FR-051), printing the **identical** three lines in the **identical**
order (per-link FIFO, FR-018) — byte-identical to the unsplit baseline (SC-001). The credit
stream rides the reverse HTTP/2 direction and bounds in-flight records to the window (SC-013).

---

# 4. UNIT test specs (REPL Section-A runtime + Section-B/C type-check)

These exercise the **language-surface items** this `https` unit leans on — the ground-relay send,
the channel idioms, the bounded-pipe credit dataflow, the fault-term vocabulary, and the
ground-implying guards used above. They are ordinary single-instance REPL tests (no transport),
runnable once the primitives land. Each = goal + expected outcome. (Per-transport cross-instance
behaviour is in §5.)

> Format: `programs/tests/typed/<file>.glp` + a goal; expected outcome is the observable REPL
> result. SECTION letters match `test/run_all_tests.sh` (A = runtime, B = positive type-check,
> C = negative type-check). All SPEC-LEVEL until the primitives exist.

## 4.1 Section A — runtime

- **A-https-01 (ground-relay send conses in the head).** Load a program with `link_send/3` and a
  mock local channel; goal: `link_send(txn(1,500), ch(In?, Out), C2).` with `Out` a fresh writer.
  **Expected:** succeeds; the `Out` stream head is bound to `txn(1,500)` and `C2` is the advanced
  channel. (Asserts the head-construction ground-relay shape; no `=` in body.)
- **A-https-02 (send SUSPENDS on a non-ground payload).** Goal: `link_send(X, ch(I?, O), C2).`
  with `X` an unbound **reader** (writer not yet bound). **Expected:** **suspends** on
  `ground(Msg?)` (does not fail, does not put a placeholder on `Out`); after a sibling binds `X`
  to a ground term, reactivates exactly once and conses it. (FR-010 ground-relay; three-valued.)
- **A-https-03 (bounded-pipe credit dataflow, in one heap).** Load `produce_records/3` +
  `drain_records/2` wired by `new_channel/2` in a single heap; goal drives both. **Expected:**
  prints `txn(1,500)` `txn(2,1200)` `txn(3,750)` in order; the producer never runs more than
  `Window=3` ahead (assert by interleaving order). (Models the credit/back-channel mechanism
  before any transport; SC-013 logic.)
- **A-https-04 (graceful close `[]`).** Drive the producer with source `[]`. **Expected:** the
  consumer's `drain_records([], [])` fires; no further output; goal quiesces (not a fail).
- **A-https-05 (fault-term vocabulary read with existing guards).** Goal feeds a literal
  `[ok, tempFail(link_id("https",ep("h",1),1), timeout), permFail(link_id("https",ep("h",1),1), giveup)]`
  to `watch/1`. **Expected:** prints `tempFail(...)` then `permFail(...)`; recursion stops at
  `permFail` (terminal). (Asserts faults are ordinary data, FR-043; lattice ordering.)
- **A-https-06 (`closed` terminal term).** Feed `[ok, closed(link_id("https",ep("h",1),1), eos)]`
  to `watch/1`. **Expected:** prints `closed(..., eos)`; recursion stops. (Graceful-close monitor
  term, DESIGN-DOSSIER §4.)

## 4.2 Section B — positive type-check (compiles)

- **B-https-01 (ground-implying relaxation accepted).** `drain_records/2` clause 1 reads
  `Id?`/`Amt?` twice each under `ground/1`. **Expected:** **compiles** — the SRSW analyzer accepts
  the multiple reads because `ground/1` is ground-implying (SC-006 positive).
- **B-https-02 (`link_send/3` double read of `Msg?` under `ground`).** **Expected:** compiles
  (same relaxation; mirrors guards.md §1 SC-006 positive shape).
- **B-https-03 (Channel-typed link end).** `run_bank/2` head deconstructs
  `ch(In?, [more,more,more|Out?])` against `Link(Record, Credit)`. **Expected:** type-checks (the
  `Link` alias resolves to the `Channel` shape, architecture-context §1).

## 4.3 Section C — negative type-check (rejected)

- **C-https-01 (SRSW without the ground guard).** A variant of `drain_records/2` clause 1 that
  reads `Amt?` twice **without** `ground(Amt?)`. **Expected:** **rejected** by the SRSW analyzer
  (SC-006 negative — SRSW is never relaxed by an option flag).
- **C-https-02 (non-ground on the wire — `known/1` instead of `ground/1`).** A `link_send`
  variant gated `known(Msg?)` admitting an embedded reader. **Expected:** **rejected** for the
  base layer (the base is strictly ground-relay, FR-010; `known/1`/open-structure is `glink`,
  out of scope — link-primitives OQ-7).
- **C-https-03 (declined guard in a watcher).** A `watch` arm using `==` (declined) instead of
  `=?=`/`ground`. **Expected:** **rejected** at compile time (`==` is declined — guards.md §5).

> Baseline gate (FR-067/SC-017): `bash test/run_all_tests.sh` must be green before and after; none
> of these unit tests touch `self.glp` or the prelude.

---

# 5. INTEGRATION test specs (cross-instance over `https`, via the harness)

Each test targets the shared harness interface (`StartInstances`/`OpenLink`/`Inject`/`Drive`/
`Capture`/`AssertEquiv`/`CloseLink`/`Stop`). `OpenLink(..., scheme="https", LinkOptions{...})`
selects this leaf. The two ends are exactly two **immediate** peers — no broker is ever modelled.
Wire faults are covered hermetically on **loopback** (per the harness contract — keeps T4 cheap);
the **real `https` leaf** is tested for bind-reactivation feasibility (SC-003) + graceful/abrupt
close + the mTLS policy decision. Each = name + setup + action + expected OBSERVABLE outcome + SC.

### I-https-01 — Headline split equivalence, Dart↔Dart then Dart↔C# (SC-001, SC-002)

- **Setup.** Baseline: `Drive(unsplit, go_unsplit_b2b, d)`; `bc = Capture(unsplit)` (prints the
  three `txn(...)` lines). Then `A,B = StartInstances(b2b_https_stream, [(fintech,Dart),(bank,Dart)], seed)`.
- **Action.** `link = OpenLink(A, B, "https", LinkOptions{InterHost=true, Tls=true, Window=3})`;
  `Drive(A, main("fintech"), d)`; `Drive(B, main("bank"), d)`.
- **Expected (observable).** `AssertEquiv(merge(Capture(A), Capture(B)), bc, ByteIdentical)` passes
  — the bank's stdout is byte-identical to the unsplit baseline (the three records, in order).
  **Then repeat** with `[(fintech,Dart),(bank,Csharp)]`: the merged transcript is byte-identical to
  BOTH the unsplit baseline AND the Dart↔Dart split run.
- **Satisfies:** **SC-001** (split equivalence, Dart↔Dart then Dart↔C# — the mandated
  cross-runtime parity gate) and **SC-002** (one complete writer→reader bind reconstructed equal
  between a Dart and a C# REPL over a real leaf).

### I-https-02 — Suspend-not-fail / reactivate-exactly-once across the cut (SC-001 AS2, SC-009)

- **Setup.** Same rig; drive the **bank** (consumer) **before** the fintech sends.
- **Action.** `Drive(B, main("bank"), d)` first (no record has arrived), then `Drive(A, main("fintech"), d)`.
- **Expected.** `DriveResult(B)` (before A) = **`Suspended`** (distinct from `Failed`/`Deadlock` —
  the bank's `drain_records` suspends on the unbound `In` head). After A sends, `Capture(B)` shows
  exactly the three records, each printed once (reactivated exactly once per record). No spurious
  FAIL anywhere.
- **Satisfies:** **SC-001 AS2** + **SC-009** (suspend-not-fail across the cut; reactivate once).
  This is the three-valued guarantee on the real `https` leaf.

### I-https-03 — Per-transport bind reactivation, T4 (SC-003) — REQUIRED for this leaf

- **Setup.** `A,B = StartInstances(..., [(fintech,*),(bank,*)], seed)` on at least one platform
  (Windows OR Android per FR-063). Minimal program: one record.
- **Action.** `OpenLink(A, B, "https", {InterHost=true, Tls=true})` (real mTLS handshake);
  `Drive(B, main("bank"), d)` → suspends; `Drive(A, main("fintech"), d)` → sends one `txn(1,500)`.
- **Expected.** The bank's previously-**suspended** reader **reactivates exactly once** and prints
  `txn(1,500)`; one writer→reader bind has crossed the real `https` link. The leaf is "shipped"
  only when this passes.
- **Satisfies:** **SC-003** (per-transport bind reactivation — **required for every leaf**, this
  one included). Wire faults NOT required here; close behaviour covered by I-https-06.

### I-https-04 — mTLS-backed origin auth + TLS-by-default in the adversarial corpus (SC-007)

- **Setup.** Run the adversarial-corpus runner on **both** the Dart and the C# REPL (the parity
  rig), with the `https` policy + frame-injection hooks.
- **Action / Expected (per category, identical verdict on both runtimes):**
  - **Plain inter-host refused (FR-029):** `OpenLink(InterHost=true, Tls=false, scheme="http")` ⇒
    **`LinkRefused`** on both runtimes.
  - **mTLS channel auth:** a connector that presents **no client cert** (or one not chained to the
    listener's trust store) ⇒ the link is **refused at establishment** (the mTLS strict-mode
    rejection), before any frame reaches `handleMadAssignment`.
  - **Forged-origin frame (FR-026):** inject a frame whose claimed origin ≠ the mTLS-verified peer
    of the link ⇒ **rejected** by the in-band origin check (complements the TLS-layer pin).
  - **Malformed / oversized / cyclic / huge-arity / bad-version / bad-CRC frames (FR-022/028):**
    each **fails safe within bounded memory and stack** (no OOM, no crash, no isolate kill).
  - **In-window replay (FR-027 + FR-021):** **idempotent no-op**; out-of-window replay **rejected**.
  - **Oracle:** verdict-by-verdict equality Dart vs C# (a divergence — one crashes where the other
    rejects cleanly — is a parity FAIL, not just a bug).
- **Satisfies:** **SC-007** (adversarial/security corpus parity; mTLS-backed origin auth; plain
  inter-host refused by default).

### I-https-05 — Bounded-pipe backpressure over HTTP/2 flow control (SC-013)

- **Setup.** Fast fintech, stalled bank (`Inject(Delay)` on the B→A credit direction so credits
  arrive slowly); `Window=3`. A second independent `https` link runs a concurrent I-https-01.
- **Action.** `Drive(A, main("fintech"), d)` with the bank delayed.
- **Expected.** The fintech **SUSPENDS** with no more than `Window=3` records in flight (outbound
  queue bounded; producer suspends on the missing `more` credit — the logical-credit mechanism
  riding the reverse HTTP/2 direction, with WINDOW_UPDATE byte-credit below the seam). No OOM; the
  **second** link is **not** head-of-line blocked (its I-https-01 still passes).
- **Satisfies:** **SC-013** (backpressure bound; no head-of-line blocking across independent links).

### I-https-06 — Graceful close (`[]`) vs abrupt close (`link_close`) (SC-001 close path; SC-010 perm)

- **Setup.** Two sub-cases over the real `https` leaf.
- **Action / Expected:**
  - **Graceful:** fintech source exhausts → `produce_records([], ...)` binds `Out=[]` → leaf sends
    **END_STREAM** → bank's `drain_records([],[])` fires → host emits `closed(LinkId, eos)` on the
    monitor; `CloseLink(link, Graceful)` confirms half-close both directions; no `permFail`.
  - **Abrupt:** mid-stream `CloseLink(link, Abrupt)` (models a revoked-cert kill) → leaf sends
    **RST_STREAM** → host emits `permFail(LinkId, _)`; the bank's **data** goal stays **Suspended**
    (never Failed); a `watch/1` reader observes the `permFail`.
- **Satisfies:** SC-001 (graceful close path of the headline scenario) + **SC-010** (abrupt →
  `permFail`, data goal not failed). Distinguishes the monitor lattice `closed` vs `permFail`.

### I-https-07 — Idempotent redelivery is a verified no-op (SC-008) [hermetic, loopback]

- **Setup.** Run the §3 program over **loopback** (hermetic, deterministic) so wire faults are
  injectable; `Inject(Duplicate(nth=1))` then a third delivery after entry removal.
- **Action.** `Drive` both ends to quiescence.
- **Expected.** Exactly the three records, each printed once; **no crash, no StateError, no error
  printed, no second reactivation** (today the second delivery throws — `mad_context.dart:330,377`;
  `heap_fcp.dart:365`; this test asserts the **absence** of the live crash). `Capture(B)` identical
  to the no-fault I-https-01 Dart↔Dart capture.
- **Satisfies:** **SC-008** (idempotent redelivery no-op). (Wire-fault coverage is hermetic on
  loopback; the real `https` leaf reuses the **same** reliability sublayer code path above the
  seam, so this gate covers `https` too.)

### I-https-08 — Reorder / loss recovery (SC-012) [hermetic, loopback]

- **Setup.** Same program over loopback; `Inject(Reorder(window=3))` + `Inject(Drop(nth=2))`.
- **Action.** `Drive` to quiescence.
- **Expected.** Sublayer ON: the bank prints the three records **in order** = the in-order run
  (`AssertEquiv(Capture(B), inorderBaseline, CausalInOrder)`). Sublayer OFF: oracle asserts
  **corruption detected** (a fault term / clean error), never a silently-wrong transcript.
- **Satisfies:** **SC-012** (reorder/loss recovery). Hermetic on loopback; the `https` leaf's TCP
  layer already gives in-order delivery, so this proves the sublayer is correct independent of the
  transport's own ordering.

---

# 6. Regression — the permanent regression set for this leaf

Tie to the baseline gate (FR-067 / SC-017): `bash test/run_all_tests.sh` green before and after
every core-touching change; `self.glp` (incl. any `=\=`-gated arithmetic) still loads; the harness
never mutates the prelude. The `https` tutorial contributes the following to the **permanent
regression set**, added as the harness's new Section **R** ("Cross-Instance Link Integration"),
**skip-until-implemented**, flipping to run as the primitives land:

| Test | Becomes permanent regression because… | Tier |
|---|---|---|
| **I-https-01** (split equivalence, Dart↔Dart **and** Dart↔C#) | the headline correctness gate; the Dart↔C# half is the **release gate** (FR-062) | release gate (not on default fast path; needs C# REPL) |
| **I-https-02** (suspend-not-fail / reactivate-once) | guards the three-valued invariant on a real leaf — silent regression here corrupts GLP semantics | fast (hermetic-capable) |
| **I-https-03** (per-transport bind reactivation, SC-003) | the "leaf is shipped" gate — required for `https` to count as shipped | per-platform (Windows OR Android) opt-in |
| **I-https-04** (adversarial corpus parity, mTLS origin auth, plain inter-host refused) | security parity is a known differential-risk class (FR-031); must hold on **both** REPLs | parity rig (both REPLs) |
| **I-https-07** (idempotent redelivery no-op) | closes the live duplicate-delivery **crash** — the single sharpest correctness gate | fast (loopback, hermetic) |
| **I-https-08** (reorder/loss recovery) | proves the reliability sublayer end-to-end independent of TCP ordering | fast (loopback, hermetic) |
| **Unit A-https-01..06 + B-https-01..03 + C-https-01..03** | language-surface regressions (ground-relay head construction, ground-implying SRSW relaxation, declined-guard enforcement, fault vocab) | Sections A/B/C of `run_all_tests.sh` |

The **fast/hermetic** subset (I-https-02 capture path on loopback, I-https-07, I-https-08, and all
unit tests) runs on the default CI path. The **real-leaf** subset (I-https-03, I-https-05,
I-https-06) runs behind a per-platform/opt-in flag (T4). The **parity** subset (I-https-01
Dart↔C#, I-https-04) is the release-gate invocation requiring both REPLs built. Until the
primitives land, Section R prints `SKIP: link layer not yet implemented (feature 025)` so the
baseline stays green (SC-017 holds before any core change).

---

# 7. Open items specific to this transport

- **OQ-https-1 (HTTP/2 vs HTTP/3 for `https`).** This tutorial maps `https` to HTTP/2 (single
  bidirectional TCP-backed stream). FR-012 also lists HTTP/3 (QUIC). Confirm whether `https` is
  HTTP/2-only here and `h3`/HTTP/3 is a separate leaf, or whether `https` negotiates h2/h3 via ALPN
  below the seam. (Recommendation: `https` = HTTP/2 for this MVP leaf; HTTP/3 as a sibling leaf,
  since QUIC's stream/flow-control model differs — a separate tutorial.)
- **OQ-https-2 (mTLS trust-store provisioning).** The harness models mTLS as a policy bit
  (`Tls=true`) on loopback. For the **real** leaf, where do client/server certs + the trust store
  live, and how is rotation (47-day lifetimes) exercised in I-https-06's abrupt-close case? This is
  a host/deployment item, not a language-authority item, but the test rig must pin a deterministic
  cert fixture so the parity rig is reproducible.
- **OQ-https-3 (back-channel multiplexing — DESIGN-DOSSIER OQ-F3).** Over HTTP/2's single
  bidirectional stream, do B→A application data (CorrId replies) and B→A logical credits share one
  reverse direction (multiplexed by frame kind below the seam — recommended) or use two logical
  streams? Affects how `Link(In, Out)` surfaces credits vs replies to the program.
- **OQ-https-4 (logical-credit ↔ WINDOW_UPDATE coupling — DESIGN-DOSSIER §3/OQ-F3).** Confirm the
  GLP program sees only **logical** credits (one `more` = one term) with HTTP/2 byte-window
  WINDOW_UPDATE strictly below the seam (recommended), and pin the max-chunk (safety) / min-one-
  octet (progress) policy for a large term that fragments across DATA frames.
- **OQ-https-5 (gateway-as-immediate-peer framing).** When an mTLS API gateway / L7 LB terminates
  TLS and forwards to a backend, the link's *immediate peer* is the gateway, and FR-023 requires
  the per-link FIFO + at-least-once be enforced end-to-end by the sublayer, **not** assumed of the
  gateway. Confirm the origin-auth identity is the gateway's mTLS cert (the immediate peer) and that
  any backend fan-out beyond it is explicitly OUT OF SCOPE (another level), consistent with the MQTT
  broker clarification.
- **OQ-https-6 (idle / keepalive on a long-lived stream).** A B2B record stream may idle for long
  periods between records. Confirm how PING / idle-timeout interacts with the `tempFail` give-up
  clock so a quiet-but-healthy stream is not misclassified as silence → `permFail` (SC-010's bound
  is "a tuning parameter, not a correctness condition" — spec Assumptions).

---

# 8. Sources

GLP semantics, seams, primitives, and file:line facts are Tier-1 local (DESIGN-DOSSIER.md,
spec.md, contracts/{link-primitives,guards,architecture-context,example-http-link}.md,
tests/integration-harness-design.md). Transport semantics and the real-world scenario are
web-grounded (cited inline; used only to ground the mapping, never to override a Tier-1 fact):

- HTTP/2 — RFC 9113 (stream = bidirectional frame sequence §5; END_STREAM / half-closed states
  §5.1; DATA frames arbitrary payload + flow-controlled §6.1; flow control credit-based, initial
  window 65,535 octets, only DATA flow-controlled, stream + connection level §5.2 / §6.9;
  WINDOW_UPDATE §6.9; RST_STREAM abrupt termination §6.4; frame ordering significant §5;
  SETTINGS_MAX_FRAME_SIZE 2^14..2^24−1 §4.2): https://httpwg.org/specs/rfc9113.html
- HTTP/2 — RFC 7540 (original; superseded by 9113): https://www.rfc-editor.org/rfc/rfc7540.html
- TLS 1.3 — RFC 8446 (mTLS: server `CertificateRequest` §4.3.2; client `Certificate` +
  `CertificateVerify` proof-of-possession; reject before application logic):
  https://datatracker.ietf.org/doc/html/rfc8446
- Mutual TLS for B2B cross-organization auth / Open Banking / trust store / custom domain:
  https://aws.amazon.com/blogs/compute/introducing-mutual-tls-authentication-for-amazon-api-gateway/
- Azure Application Gateway mutual authentication (strict mode, trusted client CA, listener):
  https://learn.microsoft.com/en-us/azure/application-gateway/mutual-authentication-overview
- mTLS for B2B partner APIs (PSD3 / FAPI 2.0 / HIPAA channel identity; internal PKI):
  https://zerq.dev/blog/mtls-b2b-partner-api-authentication-setup
- Mutual TLS client authentication (handshake flow; reject at TLS layer before app logic):
  https://oneuptime.com/blog/post/2026-03-20-mutual-tls-mtls-client-verification/view
- Curity — mutual TLS client authentication: https://curity.io/resources/learn/oauth-client-authentication-mutual-tls/
- WorkOS — sender-constrained tokens (mTLS the original gold standard; FAPI 2.0): https://workos.com/blog/mtls-dpop-token-binding-sender-constrained-oauth
