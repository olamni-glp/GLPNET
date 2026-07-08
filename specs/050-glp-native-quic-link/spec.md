# Feature Specification: GLP-Native True-QUIC Link — Genuine GLP Over the Wire

**Feature Branch**: `050-glp-native-quic-link`
**Created**: 2026-07-08
**Status**: Draft
**Input**: User description: "GLP-native true-QUIC link — genuine GLP over the wire, driven entirely by GLP programs. Wire the feature-036 genuine-QUIC+WS transport into the feature-025 GLP-native link layer by registering it as a 025 LinkRuntime transport and extending the link_id scheme to \"quic\". Hard requirements: (1) TRUE QUIC only, never TCP or loopback; (2) a GLP program in the REPL sets up ALL links the cross-host test needs; (3) messages use the feature-041 crdtmsg CRDT wire format; (4) messages carry correct macaroons to establish and maintain connectivity; (5) the full mesh + performance + security/cyber + reliability test runs natively as GLP goals across two physical hosts (Olamnit 192.168.0.136 + gavri 192.168.0.108) over the mutual-pin QUIC link; (6) the test concludes with graceful termination. Trunk cert is a permanent credential (harden, no time-boxed carve-out); DISCIPLINE §1.14 — any NEW link kernel or language primitive requires express approval, though link_id \"quic\" is data. Builds on 025 + 036 + 041; complements 049. Out of scope: the opaque-payload transport soak."

## Overview

Today the genuine HTTP/3 (QUIC) + WebSocket transport shipped by feature 036 carries **opaque `GlpMessage` strings** between a C# host and CLI peers — it is a *transport* soak, not GLP running over the wire. Separately, feature 025 gave GLP a native **link layer**: body kernels (`_link_listen` / `_link_connect` / `_link_accept` / `_link_send` / `_link_close`) with GLP-callable wrappers (`server_listener`, `client_connector`, `link_close`) that let a single-REPL producer/consumer program be split across REPL instances while preserving one-writer/one-reader logic-variable semantics — but only over TCP and loopback transports.

This feature **joins the two**: it registers the genuine 036 QUIC+WS endpoint as a **025 LinkRuntime transport** under a new `link_id` scheme `"quic"`, so that a **GLP program running in the REPL** — not an external Python or C# harness — starts true-QUIC listeners and connectors, sets up every link the test needs, exchanges messages in the correct **041 crdtmsg CRDT wire format** carrying **macaroon capabilities**, and drives a full **mesh + performance + security/cyber + reliability** run across **two physical hosts** over the mutual-pinned QUIC link, ending in **graceful termination**.

The distinguishing claim is *genuine GLP over the wire*: GLP goals evaluate across the two hosts, the shared logic-variable binding is carried by the real QUIC link in place of a shared heap variable, and the observable result is the GLP program's own — never an opaque payload shuttled by an out-of-band harness.

## Clarifications

### Session 2026-07-08

- Q: Which REPL runtime hosts the `"quic"` transport and terminates genuine QUIC in-process for the MVP? → A: The **C# reference REPL** — it terminates genuine QUIC in-process (036 QUIC + 041 crdtmsg are both C#-primary; 036 proved Dart/AtomVM cannot terminate QUIC in-process). Dart `glp_repl` participation is out of MVP scope.
- Q: Mesh topology & scale for the cross-host test? → A: **All-pairs full mesh of 5 endpoints** — 1 Android tablet, 1 Android phone, 1 Windows app, 2 CLI clients — across the two hosts.
- Q: Are the tablet/phone/Windows-app endpoints Flutter (`glp_multiagent`) or C#? → A: **All five endpoints are C#/.NET glpnet implementations** (the device apps are C#/.NET, e.g. MAUI, **not** Flutter). This keeps the Q1 ruling intact — everything is C#/.NET; Dart/Flutter stays out of MVP scope. (Mobile .NET endpoints that cannot terminate QUIC in-process may reach the mesh via the 036 Profile-A WS-to-QUIC-side-process; the genuine-QUIC requirement of FR-002 still holds on every link — the in-process-vs-side-process reach per endpoint is a plan-level detail.)
- Q: What are the 2 CLI clients? → A: **glpnet C# REPL instances** — the two CLI endpoints run the GLPNET (C#) REPL, so each mesh endpoint is a genuine GLP-program-hosting REPL rather than a bespoke CLI harness. This reinforces Q1 (C# reference REPL) and requirement (2) — the GLP program in the REPL sets up all links.
- Q: Endpoint-build scope — does this feature implement all 5 endpoints, including the 3 device apps? → A: **No.** The **3 MAUI C# device apps (Android tablet, Android phone, Windows app) are already built** and are **out of this feature's scope as deliverables** — but they **will connect into this test and be mesh participants**, so this feature **MUST be ready for them** (listeners/acceptors stood up, mutual-pin + macaroon + crdtmsg interop honored). This feature **delivers**: the `"quic"` transport wiring (036 → 025), the **2 C# glpnet REPL endpoints**, and the GLP test program that establishes the mesh and accepts the pre-built device apps.
- Q: Full-duplex — does each peer-pair need two (unidirectional / HTTP) links, or one? → A: **One.** A GLP link decomposes as `ch(In, Out?)` and is inherently full-duplex (025 FR-003); it rides **one** genuine WebSocket over **one** QUIC *bidirectional* stream (036 FR-002 / FR-008a — one WS per bidi stream, not HTTP request/response). Therefore an all-pairs full mesh of 5 endpoints = **C(5,2) = 10 full-duplex links** (10 QUIC bidirectional streams), **not** 20.

## User Scenarios & Testing *(mandatory)*

The "users" here are the **GLPNET runtime and its GLP programs**. Each story is an independently demonstrable slice of the single end-to-end goal: *a GLP program in the REPL stands up genuine QUIC links, speaks the CRDT wire format under capability control, runs the full cross-host test, and shuts down cleanly.*

### User Story 1 - A GLP goal establishes a genuine QUIC link and one bind crosses the real wire (Priority: P1)

A developer loads a role-parameterized GLP program in the REPL. On host A a GLP goal calls `server_listener(link_id("quic", ...), Link, _)`; on host B a GLP goal calls `client_connector(link_id("quic", ...), Link, _)`. The two REPLs complete a **genuine QUIC + WebSocket** handshake over the LAN (verifiable on the wire — not loopback, not TCP), the link decomposes in GLP as `ch(In, Out?)`, and one writer→reader binding crosses the real QUIC wire, reactivating a reader that had suspended waiting for it.

**Why this priority**: This is the irreducible MVP. Until a GLP goal (not a harness) brings up one real QUIC link and a single bind crosses it with the reader reactivating exactly once, nothing else in the feature has demonstrable value. It proves the transport is registered into the 025 LinkRuntime, that `link_id` scheme `"quic"` is honored, and that GLP semantics survive the real wire.

**Independent Test**: On two distinct LAN hosts, launch the same role-parameterized program split across the two REPLs over a `"quic"` link. Observe a completed QUIC handshake on the wire (packet capture or endpoint attestation — not loopback), an established link, and one writer→reader bind that reactivates a previously-suspended reader **exactly once**. Assert that no TCP or loopback fallback occurred.

**Acceptance Scenarios**:

1. **Given** a GLP goal `server_listener(link_id("quic", ep(HostA, Port), Nonce), Link, _)` on host A and `client_connector(link_id("quic", ep(HostA, Port), Nonce), Link, _)` on host B, **When** both goals run, **Then** a genuine QUIC+WS link is established by the GLP program itself and both ends obtain `ch(In, Out?)`.
2. **Given** the producer end has not yet written its value, **When** the consumer end reads the corresponding remote reader, **Then** the consumer goal **suspends** (does not spuriously fail, does not deadlock) and **reactivates exactly once** when the value arrives over the QUIC link.
3. **Given** the QUIC endpoint or its stack is unavailable, **When** the GLP goal attempts to open the `"quic"` link, **Then** the attempt **fails or reports a fault** and **MUST NOT** silently downgrade to TCP or loopback.

---

### User Story 2 - Messages on the wire are 041 crdtmsg CRDT envelopes, not ad-hoc strings (Priority: P2)

A GLP program sends a message over a `"quic"` link. The bytes on the wire are a well-formed **041 crdtmsg unified (ground-relay) envelope** — header `{msg_id, from, to, seq}` + routing-policy fields + a capability slot, as length-prefixed skippable TLV records — and where the message carries editable content it carries the **rich-text CRDT payload** (Fugue non-interleaving + Peritext formatting spans), preserving formatting marks the receiver does not understand. The peer decodes it with no semantic loss.

**Why this priority**: The whole point of "genuine GLP over the wire" is that the wire format is the real interoperable CRDT message, not an opaque string. This slice makes the link speak crdtmsg. It rides on US1's established link and is independently testable against the 041 golden corpus.

**Independent Test**: Send a crdtmsg message (including one carrying a rich-text edit op) over the `"quic"` link; assert the peer decodes it losslessly, including opaque/unknown sections forwarded verbatim; feed malformed inputs (bad version byte, unknown must-understand tag, truncation, trailing bytes) and assert each is rejected loud-fail; assert zero ad-hoc-string messages appear on the wire.

**Acceptance Scenarios**:

1. **Given** a GLP program with a crdtmsg message to send, **When** it sends over the `"quic"` link, **Then** the on-wire bytes are a valid crdtmsg envelope and the peer reconstructs the abstract message including sections it does not understand.
2. **Given** a message carrying editable content, **When** it crosses the link, **Then** it carries the rich-text CRDT (Fugue + Peritext) and unhandled formatting marks survive the round trip.
3. **Given** a malformed or truncated frame, **When** the peer decodes it, **Then** it is rejected loudly (never silently tolerated).

---

### User Story 3 - Macaroons gate link establishment and maintenance (verify-before-act) (Priority: P3)

Establishing a `"quic"` link — and keeping it alive for gated actions — requires a valid **macaroon capability** presented and **verified before acting** (beacon static-macaroon model). A link opened with a valid macaroon proceeds; an absent, tampered, or expired macaroon (or an unsatisfiable/un-understood caveat) **fails closed**, and the refusal is a **distinct, recorded outcome** — never a silent drop and never a crash.

**Why this priority**: Capability control is what makes the link trustworthy and is a hard requirement for establishing *and maintaining* connectivity. It layers above US1's link and US2's envelope (the macaroon rides in the envelope's capability slot).

**Independent Test**: Open a `"quic"` link with a valid macaroon and assert establishment succeeds; retry with an absent / tampered / expired macaroon and assert establishment fails closed with a recorded refusal and no crash; during an established session, present a gated action with an invalid capability and assert verify-before-act refuses it and records the refusal while the run stays graceful.

**Acceptance Scenarios**:

1. **Given** a valid macaroon whose caveats are all satisfied, **When** a GLP goal opens or maintains a `"quic"` link, **Then** the action proceeds.
2. **Given** an absent, tampered, expired, unsatisfiable, or un-understood macaroon/caveat, **When** a GLP goal attempts establishment or a gated action, **Then** it **fails closed**, the refusal is **recorded as a distinct outcome**, and the process does not crash.
3. **Given** the shared trunk certificate, **When** the link authenticates, **Then** it uses mutual out-of-band pinning with **no** time-boxed trust carve-out and **no** domain-name/public-CA shortcut.

---

### User Story 4 - The full cross-host mesh + performance + security + reliability test runs as GLP goals (Priority: P4)

A GLP program running in the REPL sets up **all** the links a full test needs across the two physical hosts — **Olamnit (192.168.0.136)** and **gavri (192.168.0.108)** — over mutual-pinned QUIC links, forming a peer-to-peer duplex **mesh** of REPL endpoints. Evaluated natively as GLP goals, the run exercises **mesh** connectivity, **performance** (throughput/latency across the real wire), **security/cyber** (capability refusal, signed-content tamper detection, cert-pin enforcement against a rogue peer), and **reliability** (duplicate suppression, exactly-once reactivation, fault reporting).

**Why this priority**: This is the headline demonstration that the capability is real end-to-end across two machines and driven entirely by GLP. It depends on US1–US3 and is the integration slice.

**Independent Test**: From the REPL on the two hosts, run the GLP test program; assert every link is opened by a GLP goal (no external harness opens links), the mesh reaches the concurrency floor, performance meets the stated targets, each security scenario yields the expected recorded outcome, and each reliability property holds.

**Acceptance Scenarios**:

1. **Given** the two hosts and a GLP test program, **When** the program runs, **Then** every QUIC link in the mesh is established **by a GLP goal**, not by a Python/C# harness.
2. **Given** the mesh is up, **When** peers message each other, **Then** peer-to-peer duplex delivery holds at the concurrency floor and performance targets are met.
3. **Given** a rogue (non-pinned) peer and a tampered signed block, **When** they are presented, **Then** both are detected and rejected as recorded outcomes with zero false accepts.
4. **Given** duplicate messages and a suspended remote reader, **When** the run proceeds, **Then** duplicates are suppressed and the reader reactivates exactly once.

---

### User Story 5 - The run concludes with graceful termination (Priority: P5)

When the test completes (or is asked to stop), the GLP program **drains** in-flight messages, performs a **clean link close** via the existing `link_close` kernel on every link, and **tears down** listeners, connectors, streams, and QUIC connections in order. The run ends cleanly — **never a crash, never an abrupt abort** — and resources are released so a subsequent run can re-establish links without manual cleanup.

**Why this priority**: A test that ends in a crash proves nothing about reliability. Graceful termination is an explicit hard requirement and the final slice that makes the whole run trustworthy.

**Independent Test**: Complete a full run, then trigger termination; assert every link is drained and closed cleanly, all resources are released, the process exits without error, and an immediate re-run succeeds without manual cleanup.

**Acceptance Scenarios**:

1. **Given** an active mesh with in-flight messages, **When** termination is requested, **Then** in-flight messages drain, every link closes cleanly via `link_close`, and teardown completes with no crash.
2. **Given** a completed graceful teardown, **When** the program is re-run, **Then** links re-establish with no leftover listeners/connections and no manual cleanup.
3. **Given** a peer disappears mid-drain, **When** teardown proceeds, **Then** the fault is reported via the 025 monitor stream and teardown still completes gracefully.

---

### Edge Cases

- **QUIC unavailable / stack missing**: opening a `"quic"` link fails or reports a fault; it MUST NOT silently downgrade to TCP or loopback (FR-002).
- **Peer certificate not pinned / rogue peer**: the handshake is rejected as a recorded outcome; no unpinned peer is admitted.
- **Macaroon expires mid-session**: the next gated action fails closed and is recorded; link maintenance stops gracefully without a crash.
- **v2 envelope reaches an older (v1) reader**: additive capability-slot / unknown fields are skipped by length; known fields still process (041 BB-VER-2).
- **Unknown `@name` address**: reported error, never a silent default-fallback (041).
- **Peer disappears mid-drain during termination**: fault reported via the monitor stream; teardown still completes gracefully.
- **A new link kernel or language primitive appears necessary**: STOP and propose-first under DISCIPLINE §1.14 — it is NOT landed unilaterally; `link_id` scheme `"quic"` is data and needs no approval.

## Requirements *(mandatory)*

### Functional Requirements

**Transport registration & GLP-driven establishment**

- **FR-001**: The system MUST register the genuine 036 QUIC+WebSocket endpoint as a **025 LinkRuntime transport** selectable by `link_id` scheme `"quic"`, reusing the existing 025 link kernels (`_link_listen`/`_link_connect`/`_link_accept`/`_link_send`/`_link_close`) and their GLP wrappers (`server_listener`/`client_connector`/`link_close`) unchanged. *(Analyze note I1, 2026-07-08: the on-disk C# link layer has no distinct `_link_connect` kernel — the connect path is folded into `_link_setup` with role `connector` (and the path-B `_link_request`). "Reuse `_link_connect` unchanged" therefore means reuse `_link_setup(connector)`; introducing a new `_link_connect` kernel is NOT permitted — that would breach FR-019.)*
- **FR-002**: The link MUST use **TRUE QUIC only** — the genuine 036 real-QUIC endpoint — and MUST NOT use, nor fall back to, TCP or loopback. The established link MUST be verifiable-on-the-wire as QUIC (not a loopback shim). *(Analyze note A2, 2026-07-08: "not loopback" forbids a loopback **shim / simulated-transport fallback** (e.g. the `LoopbackTransport` leaf), NOT the loopback network interface. A genuine MsQuic handshake over 127.0.0.1 is real QUIC and is acceptable for hermetic CI tests; the two-physical-host LAN run remains the SC-001 real-wire proof.)*
- **FR-003**: A GLP program running in the REPL MUST establish the QUIC listeners and connectors and set up **ALL** links the cross-host test needs, expressed as GLP goals over `"quic"` `link_id`s. No external Python or C# harness may open the test's links.
- **FR-004**: Every `"quic"` link end MUST be **symmetric bidirectional** (025 FR-003 — every end can both send and receive), decompose in GLP as `ch(In, Out?)`, and preserve GLP's one-writer/one-reader semantics: a writer→reader bind crosses the real wire and reactivates a suspended reader **exactly once**.

**Wire format (041 crdtmsg)**

- **FR-005**: Messages on the `"quic"` wire MUST use the **041 crdtmsg unified (ground-relay) envelope** — header `{msg_id, from, to, seq}` + routing-policy fields + capability slot, as length-prefixed skippable TLV records — and MUST NOT be ad-hoc strings.
- **FR-006**: Where a message carries editable content it MUST carry the **041 rich-text CRDT** payload (Fugue non-interleaving + Peritext formatting spans over stable IDs) and MUST preserve formatting marks the receiver does not understand. A scalar-only path incapable of the rich-text case fails this requirement.
- **FR-007**: A crdtmsg message sent over the `"quic"` link MUST decode at the peer **without semantic loss**, including sections the receiver does not understand (forwarded/preserved verbatim); every decoder MUST consume all input bytes or fail loudly (no silent tolerance).

**Capability / macaroons**

- **FR-008**: Establishing a `"quic"` link MUST require a valid **macaroon capability**, **verified before acting** (beacon static-macaroon model); establishment proceeds only when all caveats are satisfied.
- **FR-009**: Maintaining connectivity MUST re-verify capability on gated actions; an absent, tampered, expired, unsatisfiable, or un-understood macaroon/caveat MUST **fail closed** and be **recorded as a distinct refusal outcome** — never a silent drop and never a crash.
- **FR-010**: The trunk/shared certificate MUST be treated as a **permanent credential** — hardened with **no time-boxed carve-out**, no temporary trust bypass, and no expiry-window shortcut.
- **FR-011**: The QUIC connection MUST authenticate with the 036 shared self-signed certificate under **mutual out-of-band pinning** (mutual SPKI pin), with no domain-name, public-CA, or hostname-bound-certificate shortcut. A non-pinned/rogue peer MUST be rejected.

**Cross-host full test**

- **FR-012**: The full test MUST run natively as **GLP goals** evaluating across the two physical hosts **Olamnit (192.168.0.136)** and **gavri (192.168.0.108)** over the mutual-pinned QUIC link.
- **FR-013**: The test MUST stand up a peer-to-peer **all-pairs full mesh** of **5 C# glpnet endpoints** — 1 Android tablet, 1 Android phone, 1 Windows app (the three **pre-built MAUI C# apps**, external participants), and **2 glpnet C# REPL instances** (delivered by this feature) — across the two hosts, forming **C(5,2) = 10 full-duplex links** (10 QUIC bidirectional streams, each a GLP `ch(In, Out?)`). Every endpoint is a genuine GLP-program-hosting C# REPL/app (no bespoke CLI harness), and every link MUST be opened by a GLP goal. One full-duplex link per peer-pair suffices (no link doubling); the design scales beyond 5.
- **FR-013a**: This feature MUST be **ready to accept the three already-built MAUI C# device apps** as mesh participants — the delivered endpoints MUST stand up listeners/acceptors and honor the same mutual-pin QUIC + macaroon + crdtmsg contract so the pre-built apps join the 10-link mesh. Building or modifying the MAUI apps is **out of scope**; interoperating with them is **in scope**.
- **FR-014**: The test MUST measure **performance** (message round-trip latency and sustained throughput) over the real two-host wire against the targets in Success Criteria.
- **FR-015**: The test MUST exercise **security/cyber** scenarios — capability refusal (absent/tampered/expired macaroon), signed-content tamper detection (whole-content and sub-content), and cert-pin enforcement (rogue/non-pinned peer rejected) — each producing a recorded outcome with zero false accepts.
- **FR-016**: The test MUST exercise **reliability** — duplicate suppression (`msg_id` + per-link `seq`), exactly-once remote reader reactivation, and fault reporting via the 025 monitor stream (faults reported, never swallowed).

**Graceful termination**

- **FR-017**: The test MUST conclude with **graceful termination** — drain in-flight messages, clean link close via the existing `link_close` kernel on every link, orderly teardown of listeners/connectors/streams/QUIC connections — never a crash or abrupt abort.
- **FR-018**: After graceful termination all link resources MUST be released such that a subsequent run re-establishes links with no manual cleanup.

**Scope & discipline**

- **FR-019**: `link_id` scheme `"quic"` is **data** and needs no approval. Any **NEW link kernel, system predicate, or language primitive** found necessary MUST be **proposed-first** and is gated on Gabi's express DISCIPLINE §1.14 approval — it MUST NOT be landed unilaterally, and MUST NOT be worked around with bespoke evaluators or shadow layers.
- **FR-020**: The opaque-payload transport soak (opaque `GlpMessage` strings over the 036 transport) is **out of scope** — it was the wrong layer. This feature is genuine GLP-over-the-wire: GLP programs in the REPL, crdtmsg envelopes, not opaque strings.

### Key Entities

- **QUIC link** — `link_id("quic", Endpoint, Nonce)`; a genuine QUIC+WebSocket channel that decomposes in GLP as `ch(In, Out?)` and carries a one-writer/one-reader binding across hosts.
- **LinkRuntime `"quic"` transport** — the registration that binds the 036 genuine-QUIC endpoint into the 025 transport registry, selectable by scheme.
- **crdtmsg envelope (ground-relay)** — the on-wire message: header `{msg_id, from, to, seq}` + routing policy + capability slot (length-prefixed skippable TLV), optionally carrying a rich-text CRDT payload.
- **Macaroon capability** — fail-closed caveat token, verified before acting, gating link establishment and maintenance; rides in the envelope's capability slot.
- **Trunk/shared certificate** — the permanent, mutually-pinned self-signed credential authenticating every QUIC connection.
- **Fault stream** — the 025 monitor stream over which link faults are reported.
- **Mesh endpoint** — one C# glpnet REPL/app participating in the mesh. Five total: 2 glpnet C# REPL instances (delivered here) + 3 pre-built MAUI C# apps (Android tablet, Android phone, Windows app — external participants). All-pairs full mesh ⇒ 10 full-duplex links.
- **Host pair** — Olamnit (192.168.0.136) and gavri (192.168.0.108), the two physical demo hosts across which the 5 endpoints are distributed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A GLP goal issued in the REPL establishes a genuine QUIC link to a peer host and one writer→reader bind crosses the wire, reactivating the suspended reader exactly once — with the link verifiable as QUIC (not loopback/TCP) on the wire, in 100% of MVP runs.
- **SC-002**: 100% of messages observed on the wire are well-formed crdtmsg envelopes (zero ad-hoc strings); every cross-format and opaque-section round-trip is lossless; 100% of malformed inputs are rejected loud-fail. *(Analyze note A1, 2026-07-08: "on the wire" is read at the **L5 application-payload** layer — the crdtmsg envelope is the payload carried inside each 025 reliability frame (`FrameCodec` length+CRC+seq). The 025 framing is preserved so FR-016 duplicate-suppression/ordering still holds; it is NOT stripped to place a bare envelope on the raw QUIC stream.)*
- **SC-003**: Link establishment/maintenance with a valid macaroon succeeds; with an absent/tampered/expired macaroon it fails closed with a recorded refusal in 100% of attempts and **zero crashes**.
- **SC-004**: The all-pairs full mesh of **5 C# glpnet endpoints** (2 delivered C# REPL instances + the 3 pre-built MAUI apps) sustains its **10 full-duplex links** peer-to-peer across the two hosts, with **every** link established by a GLP goal (zero links opened by an external harness), and the delivered endpoints successfully accept the 3 pre-built device apps into the mesh.
- **SC-005**: Performance — median message round-trip across the two-host LAN wire completes within the agreed target and the mesh sustains the agreed message volume with **zero message loss**. *(Analyze note U1, 2026-07-08: `/bk-clarify` was not run for this feature, so the spec's default working targets — **median round-trip < 50 ms LAN, ≥ 1000 messages sustained without loss** — are hereby **adopted as the working acceptance targets**, re-confirmable at the T043 two-host acceptance run. No new numbers were invented; the placeholders are promoted to the decision. The `zero message loss` half is firm regardless.)*
- **SC-006**: Reliability — zero duplicate messages delivered, remote reader reactivation is exactly-once, and 100% of injected faults are reported via the monitor stream (none swallowed).
- **SC-007**: The full test concludes with graceful termination in 100% of runs — drain + clean `link_close` + teardown — with **zero crashes/abrupt aborts**, and resources released so an immediate re-run needs no manual cleanup.
- **SC-008**: Cyber — a rogue/non-pinned peer and a tampered signed block are both detected and rejected as recorded outcomes, with **zero false accepts**.

## Assumptions

- **Genuine-QUIC termination** (settled — Clarifications 2026-07-08): the **C# reference REPL** hosts the `"quic"` transport and terminates genuine QUIC in-process via the shipped 036 genuine-QUIC host (System.Net.Quic / MsQuic, WS-over-QUIC), matching the 036 + 041 C#-primary rulings. The Dart `glp_repl` is **out of MVP scope** (per 036's finding it cannot terminate QUIC in-process; a future 036-side-process Profile-A path is deferred).
- **Macaroon issuance**: follows the beacon **static-macaroon** model — a shared macaroon root secret distributed out-of-band alongside the shared certificate; the GLP program presents and verifies static macaroons at establishment and on gated actions.
- **crdtmsg primacy**: C# is the crdtmsg primary implementation (041 ruling); Gleam/Dart participation beyond codec-parity goldens is deferred, consistent with 041.
- **Capability-on-wire surface** (Analyze note C1, 2026-07-08): the macaroon rides in the crdtmsg envelope capability slot (section `0x20`, additive-optional v2). The canonical **binary** surface today rejects a non-null capability slot, so carrying it on the binary wire is a change **inside feature 041's codec** — it MUST be **propose-first / 041-coordinated** and kept additive-optional (v1 readers skip `0x20` by length). A JSON-surface stopgap is acceptable for the MVP if the binary-v2 extension is deferred; the trade-off is recorded in `research.md` D-2 and gated by task T025.
- **Demo hosts**: Olamnit and gavri are Windows 11 (the 036 floor); the design MUST NOT assume Windows and gates on QUIC support/availability before claiming a real handshake.
- **Performance/reliability thresholds**: the SC-005 numbers are reasonable-default placeholders to be confirmed at `/bk-clarify`; all other criteria are firm.
- **Reuse**: this feature reuses the shipped 025 link kernels, 036 QUIC transport, and 041 crdt/macaroon layer; it complements 049 and does not depend on 049's in-flight guard work.
- **No shadow layers**: the implementation drives the real `BytecodeRunner`/link kernels; bespoke evaluators or flagged shadow layers (as flagged in 049) are not acceptable substitutes for genuine execution.
