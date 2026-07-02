# Feature Specification: HTTP/3 (QUIC) + WebSocket Channel-Link Prototype

**Feature Branch**: `036-http3-quic-ws-link`
**Created**: 2026-06-27
**Status**: Draft
**Input**: User description: "HTTP3-QUIC-WS-Channel-Link-proto — a working prototype of a real HTTP/3 (QUIC) channel plus a WebSocket link between independently-started CLI instances, used to run GLP over the link."

## Overview

A working prototype that lets two (or more) independently-started CLI processes establish a **genuine** HTTP/3 (QUIC) channel and a WebSocket link between them over a LAN, and use that link to run GLP. One process is a SERVER; one or more are CLIENTs. The capability is delivered as a `/GLP-Quick` skill backed by a single Python tool that hosts both roles (`--server` / `--client`).

The feature is researched, skeletoned, and implemented across **two candidate transport stacks** — (A) C#/.NET (System.Net.Quic / MsQuic, Kestrel HTTP/3) and (B) Gleam on AtomVM (a WASM build of the Erlang BEAM, run from the CLI via a Node WASM host) — so the two can be compared on the identical demo. It is run as **one durable, resumable marathon** (no transport shortcuts), refined across six stages: research-strategy formulation → corpus → distillation → implementation plan → skeleton/mock → implement-and-demo.

## Clarifications

### Session 2026-06-27

- Q: What does "run GLP over the link" mean for the prototype? → A: The link connects **GLP REPL endpoints that exchange messages** (not a submit-source/return-result RPC). Progression: (1) two REPLs — one sending, one listening/receiving; (2) full-duplex bidirectional message flow between the pair; (3) multiple REPLs messaging each other **peer-to-peer in a duplex mesh**.
- Q: Build order / which stack first? → A: **Implement the C#/.NET stack first** as the reference implementation, **then reimplement in the Gleam/AtomVM stack.** The two are built sequentially (C# → Gleam), not in parallel.
- Q: Stack acceptance bar (must both reach the full real-QUIC demo)? → A: **C#/.NET first** is the reference that reaches the full real-QUIC LAN demo. The **Gleam/AtomVM stack is the second implementation, built out in stages** against the same contract (a genuine staged build-out, not merely a skeleton).
- Q: Target concurrency N for the LAN demo? → A: **At least 3 concurrent clients**, designed to scale beyond.
- Q: Shared self-signed cert distribution? → A: **The Python tool generates the shared cert**; it is **copied out-of-band to each host (manual trust pinning)**. No CA, no enrollment service.

### Session 2026-06-28 (corpus/distillation-driven)

- Q: Does FR-002 require literal RFC 9220 Extended-CONNECT, given QUIC's de-facto dominance? → A: **No — judge by de-facto reality, not the de-jure label.** QUIC/HTTP-3 carries ~21–39% of web traffic; WS-over-QUIC is a first-class, deployed design, not a contingency. The link runs **genuine WebSocket (RFC 6455) framing over a genuine QUIC bidirectional stream** (one WS per stream — the exact carriage RFC 9220 standardizes), reusing spec 025's `FrameCodec`, established via a minimal CONNECT-style bootstrap on the stream. Because the prototype owns both C# endpoints on a LAN, it does NOT need browser RFC 9220 support; the RFC 9220 **Extended-CONNECT-over-HTTP/3 bootstrap** (the only piece .NET hasn't shipped) matters only for third-party/browser interop and is isolated behind a handshake seam for later. WebTransport-over-HTTP/3 is the browser-native option noted for a future client. The QUIC connection (FR-001) is fully genuine; nothing is simulated.
- Q: FR-009/FR-010 — the corpus shows Gleam/AtomVM cannot terminate genuine QUIC on any target (no QUIC stack, no NIF loading for `quicer`, WASM has no UDP). How are "two interchangeable stacks" preserved? → A: The Gleam stack ships as **two deployment profiles, interchangeable at the channel-link contract** (not at the QUIC-termination layer): **Profile A** — Gleam/**AtomVM** logic with a WebSocket link / native QUIC side-process, for **MAUI Blazor hybrids and smaller freestanding nodes**; **Profile C** — Gleam on **full BEAM with `quicer`/MsQuic** terminating genuine in-process QUIC, for **larger workstations and servers**. "AtomVM" is relaxed to "a BEAM-family runtime, profile-dependent."
- Q: Platform floor for the C# reference QUIC stack? → A: **The C# stack is cross-platform — NOT Windows-locked.** `System.Net.Quic` (GA in .NET 9) runs on Windows, Linux, and macOS (per [Microsoft Learn — QUIC support in .NET](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview)). Per-OS requirement: **Windows** → Windows 11 / Server 2022+ (earlier Windows lacks the crypto APIs for QUIC); msquic.dll ships with the runtime. **Linux** → install `libmsquic` 2.2+ (OpenSSL 3+/1.1) from packages.microsoft.com. **macOS** → partially supported: `brew install libmsquic` + set `DYLD_FALLBACK_LIBRARY_PATH`. The **demo hosts for this repo are Windows 11 (confirmed)**, so the Win11 floor applies to *our* demo, but the spec/design MUST NOT assume Windows. Endpoints gate on `QuicListener`/`QuicConnection.IsSupported` and verify msquic availability before claiming a real handshake.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Real QUIC + WebSocket link between two machines, running GLP (Priority: P1)

An operator starts `GLP-Quick --server` on machine A (bound to A's LAN IP / machine name) and `GLP-Quick --client` on machine B, pointing at A's IP. The two processes complete a real QUIC / HTTP-3 handshake using a shared self-signed certificate (no domain name, no hostname-bound cert), bring up a WebSocket link over the connection, and exchange a GLP interaction end-to-end — proving the link can "run GLP".

**Why this priority**: This is the irreducible MVP. Without one server and one client genuinely talking over QUIC+WS across a LAN and moving a GLP payload, nothing else in the feature has value.

**Independent Test**: On two distinct hosts (or two VMs) on the same LAN, launch server on A and client on B by IP; observe a completed QUIC handshake (verifiable on the wire — not loopback), an established WebSocket link, and a successful GLP request/response round-trip.

**Acceptance Scenarios**:

1. **Given** a server running on host A bound to A's LAN IP with a shared self-signed cert, **When** a client on host B connects to A's IP using the same shared cert/trust, **Then** a real QUIC/HTTP-3 connection is established and a WebSocket link comes up over it.
2. **Given** an established link between a sending REPL and a listening REPL, **When** the sending REPL emits a GLP message, **Then** the listening REPL receives that message over the same link.
3. **Given** a client configured to trust only the shared self-signed cert, **When** it connects, **Then** the handshake succeeds without relying on any domain-name / public-CA / hostname-bound certificate validation.
4. **Given** an established link, **When** both REPLs send messages at the same time, **Then** each receives the other's messages (full-duplex).

---

### User Story 2 - One server, several concurrent clients (Priority: P2)

A single `GLP-Quick --server` instance simultaneously serves several `GLP-Quick --client` instances. Each client establishes its own QUIC connection + WebSocket link and runs GLP independently; no client's traffic, session, or failure corrupts another's.

**Why this priority**: The deliverable explicitly requires one server serving several concurrent clients; concurrency is the difference between a point-to-point demo and a usable server.

**Independent Test**: Launch one server and several clients (from one or more hosts) at once; verify each client independently completes a GLP round-trip, and that one client disconnecting does not disrupt the others.

**Acceptance Scenarios**:

1. **Given** a running server, **When** several client REPLs connect concurrently, **Then** each obtains an independent, isolated full-duplex link and can exchange messages.
2. **Given** several concurrent client sessions, **When** one client disconnects or fails mid-session, **Then** the remaining sessions continue unaffected.
3. **Given** several REPLs connected through the server, **When** they message one another, **Then** messages are delivered peer-to-peer across the duplex mesh (each REPL reaches each other participating REPL).

---

### User Story 3 - Two interchangeable transport stacks behind one CLI (Priority: P3)

The same `/GLP-Quick` skill and Python tool can drive either transport stack — (A) C#/.NET or (B) Gleam/AtomVM — selected by a flag, and both pass the identical LAN-IP demo and conformance checks.

**Why this priority**: Comparing the two stacks on equal footing is a core goal; a shared CLI/contract is what makes the comparison meaningful and lets either be chosen later.

**Independent Test**: Run the same demo twice via the same CLI, once per stack flag; both reach a real QUIC+WS link and a GLP round-trip and report the same observable outcomes.

**Acceptance Scenarios**:

1. **Given** the C#/.NET stack selected, **When** the standard demo runs, **Then** it passes the real-QUIC + WS + GLP acceptance checks.
2. **Given** the Gleam/AtomVM stack selected, **When** the standard demo runs, **Then** it passes the same acceptance checks — built out in stages after the C#/.NET reference is complete.
3. **Given** either stack, **When** the operator uses `GLP-Quick`, **Then** the CLI surface, message/wire contract, and handshake are identical from the operator's point of view.

---

### User Story 4 - Evidence-grounded, durable marathon (Priority: P4)

The architecture is grounded in a distilled research corpus (~50 sources for the C# stack and ~50 for the Gleam/AtomVM stack, including the governing RFCs), and the whole effort runs as one durable marathon that can be paused and resumed across sessions without losing position or repeating completed work.

**Why this priority**: The user mandated research-before-build and a durable marathon; this de-risks both stacks and the cross-session execution, but it is process/evidence rather than the runtime artifact, hence lowest priority among the four.

**Independent Test**: Inspect the corpus (counts + coverage of the named RFCs and GitHub repos), the distillation notes (close-read architectural concerns, not summaries), and the marathon state (resume after an interrupt restores the exact next step and skips completed stages).

**Acceptance Scenarios**:

1. **Given** the research stages are complete, **When** the corpus is reviewed, **Then** it contains ~50 C# and ~50 Gleam/AtomVM sources covering RFC 9114 (HTTP/3) and RFC 9000/9001/9002 (QUIC).
2. **Given** an in-progress marathon, **When** the session is interrupted and later resumed, **Then** the run reports the objective next step from persisted state and does not redo finished stages.

---

### Edge Cases

- **Firewall / UDP blocked**: QUIC runs over UDP; the demo must detect and clearly report when the LAN path blocks the chosen UDP port rather than hanging.
- **Client connects before server is ready**: the client must fail clearly or retry, not silently hang.
- **Certificate mismatch / rotation**: a client presenting or trusting the wrong shared cert is rejected with a clear, non-ambiguous failure.
- **ALPN / protocol-version mismatch** between client and server (especially across the two stacks): rejected cleanly, not a partial/half-open link.
- **IP vs machine-name addressing**: both raw LAN IP and machine name must work without falling back to a domain-name/hostname-cert shortcut.
- **Concurrent-client limit exceeded**: behaviour when more clients connect than the configured ceiling is defined (reject vs queue) rather than undefined.
- **Mid-session network drop / path-MTU change**: a dropped or degraded connection surfaces a clear error and does not wedge the server.
- **Gleam/AtomVM QUIC feasibility gap**: if the AtomVM/WASM host cannot perform genuine QUIC, the stack's status is reported honestly rather than faked (see clarification on the stack acceptance bar).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST establish a genuine HTTP/3 (QUIC) connection between independently-started server and client processes — a real QUIC handshake observable on the wire, NOT a loopback-only or simulated handshake.
- **FR-002**: The system MUST run a **genuine WebSocket link over the genuine QUIC connection** (WS-over-QUIC) and use it to carry GLP interactions. WS-over-QUIC is a first-class, de-facto-standard design: a WebSocket is carried as **RFC 6455 framing on a QUIC bidirectional stream** (one WebSocket per stream — exactly the carriage RFC 9220 standardizes), reusing spec 025's `FrameCodec`, established via a minimal CONNECT-style bootstrap on that stream. Since the prototype owns both C# endpoints on a LAN, it does NOT require browser RFC 9220 support; the **RFC 9220 Extended-CONNECT bootstrap over HTTP/3** (the only piece not yet shipped in .NET) is needed only for third-party/browser interop (out of MVP scope) and MUST be isolated behind a handshake seam so it slots in unchanged when .NET ships it. WebTransport-over-HTTP/3 is the browser-native WS-like option noted for a later client. Nothing here is simulated — genuine WS frames over a genuine QUIC stream.
- **FR-003**: The system MUST authenticate the connection using a **shared self-signed certificate** as QUIC requires, WITHOUT any domain-name, public-CA, or hostname-bound-certificate shortcut. The **Python tool MUST generate the shared certificate**; it is distributed **out-of-band to each host and pinned as trusted** (manual trust, no CA or enrollment service).
- **FR-004**: The system MUST operate over a LAN addressed by IP address or machine name, and MUST be internet-capable in principle (LAN-over-IP is the demonstrated target).
- **FR-005**: A single server instance MUST serve several concurrent client instances, each with an independent, isolated link.
- **FR-006**: The remaining concurrent sessions MUST be unaffected when one client disconnects or fails.
- **FR-007**: The capability MUST be delivered as a `/GLP-Quick` skill backed by ONE Python tool/toolkit that hosts both roles: `GLP-Quick --server …` starts a server; `GLP-Quick --client …` connects a client.
- **FR-008**: The link MUST connect **GLP REPL endpoints that exchange messages** over it. The minimal slice is one REPL sending and another REPL listening/receiving a message across the link.
- **FR-008a**: The link MUST support **full-duplex** message flow — both connected REPLs can send and receive concurrently over the same link.
- **FR-008b**: The system MUST support **multiple REPLs messaging each other peer-to-peer in a duplex mesh** — each participating REPL can send messages to, and receive messages from, the other participating REPLs.
- **FR-009**: Both transport stacks MUST be researched, skeletoned, and implemented: (A) C#/.NET (System.Net.Quic / MsQuic, Kestrel HTTP/3) and (B) the Gleam/BEAM stack. **The C#/.NET stack is implemented first as the reference; the Gleam stack is a subsequent reimplementation of the same contract** (sequential, not parallel). Per Clarifications 2026-06-28 (corpus shows AtomVM cannot terminate genuine QUIC), the Gleam stack ships as two deployment profiles: **Profile A** — Gleam/AtomVM logic + WebSocket link / native QUIC side-process (MAUI Blazor hybrids, smaller freestanding nodes); **Profile C** — Gleam on full BEAM + `quicer`/MsQuic terminating genuine in-process QUIC (larger workstations and servers). At least one profile MUST achieve a genuine QUIC link; where a profile relies on a side-process for QUIC, that MUST be reported honestly (constitution II).
- **FR-010**: Both stacks MUST be drivable through the same `/GLP-Quick` CLI surface and the same wire/message contract and handshake, so they are **interchangeable at the channel-link contract level** from the operator's perspective. The **C#/.NET stack is the reference that MUST reach the full real-QUIC LAN demo first**; the **Gleam stack MUST then be built out in stages against the same contract** as the second implementation, selecting Profile A or C by deployment target. Where the Gleam stack cannot terminate QUIC in-runtime (Profile A on AtomVM/WASM), interchangeability is preserved at the contract boundary while QUIC termination is delegated to a native side-process — never simulated and never presented as in-runtime QUIC.
- **FR-011**: The system MUST support a configurable number of concurrent clients, demonstrated with **at least 3 concurrent clients** and designed to scale beyond that.
- **FR-012**: The effort MUST be executed as ONE marathon feature (not split), refined and extended across the six defined stages (research-strategy → corpus → distill → implementation plan → skeleton/mock → implement-and-demo).
- **FR-013**: The marathon MUST be durable and resumable across sessions: an interrupted run resumes from objective persisted state and does not redo completed stages.
- **FR-014**: The research output MUST be a corpus of ~50 sources for the C# stack and ~50 for the Gleam/AtomVM stack, covering RFC 9114 (HTTP/3) and RFC 9000 / 9001 / 9002 (QUIC), plus relevant technical/academic sources and GitHub repositories.
- **FR-015**: The corpus MUST be **distilled** (close-read for key architectural concerns), not merely summarised, with follow-up web/GitHub research as gaps surface.
- **FR-016**: The implementation plan MUST define components, interfaces, the wire/message contract, the server↔client handshake, the QUIC-stream + WebSocket-framing model, the `/GLP-Quick` CLI surface, and the Python tool layout.
- **FR-017**: Each stack MUST first be delivered as a top-down skeleton + mock (interfaces and stubs, no behaviour) before behavioural implementation.
- **FR-018**: The prototype MUST reuse prior art from spec 025 (multi-protocol-link-layer) where applicable rather than re-deriving it.
- **FR-019**: Connection, certificate, ALPN/version, and addressing failures MUST be reported clearly (no silent hangs, no half-open links).

### Key Entities *(include if feature involves data)*

- **Server instance**: A long-running `GLP-Quick --server` process bound to a LAN IP/machine name and a UDP port, holding the shared cert and accepting many client connections.
- **Client instance**: A `GLP-Quick --client` process that connects to a server by IP/machine name, trusting the shared cert, and runs GLP over the link.
- **QUIC connection / stream**: The genuine HTTP/3 transport between a client and the server; streams carry the multiplexed traffic.
- **WebSocket link**: The framed message channel layered over the connection, carrying GLP interactions.
- **Shared certificate**: A self-signed certificate/trust material shared by server and clients; the only trust anchor (no domain/public CA).
- **GLP REPL endpoint**: A running GLP REPL participating in the link as a message sender and/or receiver; in the mesh case, a peer that messages other peers.
- **GLP message**: A unit of data emitted by one REPL endpoint and delivered to one or more receiving REPL endpoints over the link.
- **Stack adapter**: A transport implementation (C#/.NET or Gleam/AtomVM) behind the common CLI/contract.
- **Research source / Distillation note**: A catalogued corpus entry and its close-read architectural findings.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On two distinct LAN hosts addressed by IP, a server and a client complete a **real** QUIC/HTTP-3 handshake (confirmable on the wire, not loopback) and bring up a WebSocket link, in at least one stack.
- **SC-002**: Over that link, a message emitted by a sending GLP REPL is received by a listening GLP REPL; and with both ends active, messages flow full-duplex. With three or more REPLs, each can message each other peer-to-peer across the mesh.
- **SC-003**: One server concurrently serves at least the target number of clients (per FR-011), each completing an independent GLP round-trip, with no cross-session interference.
- **SC-004**: A single client failure among concurrent sessions leaves the others fully functional.
- **SC-005**: The handshake succeeds using only the shared self-signed certificate — with no domain name and no hostname-bound/public-CA certificate involved.
- **SC-006**: The same `/GLP-Quick` CLI and Python tool drive the demo on each implemented stack with an identical operator-facing surface and contract.
- **SC-007**: The research corpus contains ~50 C# and ~50 Gleam/AtomVM sources covering the named RFCs, each with a close-read distillation note (not a summary).
- **SC-008**: An interrupted marathon resumes to the correct objective next step and skips already-completed stages.

## Assumptions

- "LAN" means two or more hosts (physical or VM) on the same local network; raw IP and machine-name addressing are both in scope, public DNS is not.
- The shared self-signed certificate is distributed out-of-band to participating hosts before the demo (key-distribution mechanism is a design detail, not a public-CA dependency).
- "Internet-capable in principle" means the design must not preclude internet use, but no internet/NAT-traversal demo is required for acceptance.
- The Python tool is the single user-facing entry point; per-stack transport runtimes (C#/.NET, Gleam/AtomVM/Node WASM host) are invoked/managed by it.
- Whatever testing tooling each stack needs may be installed as part of the work.
- spec 025 (multi-protocol-link-layer) is available as reusable prior art.
- The marathon's durability is provided by the existing marathon harness (run already opened for this feature); cross-machine sync of marathon state is out of scope.
- Genuine QUIC support on Gleam/AtomVM/WASM is a known feasibility risk to be resolved during research; the stack acceptance bar is pending clarification (FR-010).
