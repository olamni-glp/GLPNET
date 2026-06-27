# Corpus — C#/.NET stack — Cluster 01: QUIC/HTTP-3/WS-over-HTTP3 RFC foundations
2026-06-27

Close-read notes on the IETF standards that any conformant HTTP/3 + WebSocket-over-HTTP/3 prototype must satisfy. Sources are the authoritative RFC Editor texts. Concerns and feature-requirement mappings per the task brief (FR-001 establish a real QUIC connection; FR-002 layer a WebSocket over it; FR-003 authenticate via shared self-signed cert; FR-018 reuse spec-025 link framing/sequencing/backpressure; SC-005 shared cert is the only trust anchor).

---

### [1] RFC 9000 — QUIC: A UDP-Based Multiplexed and Secure Transport (§7 handshake, §4.6 0-RTT)
- **URL**: https://www.rfc-editor.org/rfc/rfc9000.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: handshake
- **Close-read findings**:
  - QUIC runs a *combined* cryptographic + transport handshake to minimize connection-establishment latency (§7). Handshake bytes travel in CRYPTO frames (§19.6), with per-packet-number-space offsets starting at zero.
  - The handshake MUST provide authenticated key exchange: the **server is always authenticated, the client is optionally authenticated**, every connection produces distinct unrelated keys, and keys protect both 0-RTT and 1-RTT packets (§7).
  - Flow: client sends Initial (CRYPTO), optionally 0-RTT app data, server replies Handshake, then 1-RTT. The `HANDSHAKE_DONE` frame (§19.20) signals completion from server to client.
  - 0-RTT (§4.6 / detailed in RFC 9001) lets a client send early data using parameters from a prior connection but carries replay risk — not needed for a same-host prototype.
- **Informs**: FR-001
- **Confidence**: high

### [2] RFC 9000 — Stream model and multiplexing (§2 Streams, §3 Stream States)
- **URL**: https://www.rfc-editor.org/rfc/rfc9000.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: stream-multiplexing
- **Close-read findings**:
  - Four stream types selected by the two least-significant bits of the 62-bit stream ID (§2.1): 0x00 client-initiated bidi, 0x01 server-initiated bidi, 0x02 client-initiated uni, 0x03 server-initiated uni.
  - Streams are an *ordered byte-stream abstraction with no other structure visible to QUIC*; STREAM-frame boundaries are not preserved across retransmission/delivery (§2.2). Ordering is reconstructed from the Stream ID + Offset fields.
  - Loss on one stream does not block delivery on others — out-of-order data is buffered up to flow-control limits; this is the structural elimination of HTTP/2's head-of-line blocking.
  - Per-stream lifecycle is explicit: sending-side states (§3.1), receiving-side states (§3.2); `RESET_STREAM` (§19.4) and `STOP_SENDING` (§19.5) abort a single stream without touching the connection.
- **Informs**: FR-002 (the WS rides one bidi stream), FR-018 (link sequencing maps onto stream offsets)
- **Confidence**: high

### [3] RFC 9000 — Flow control (§4 Flow Control)
- **URL**: https://www.rfc-editor.org/rfc/rfc9000.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: backpressure/flow-control
- **Close-read findings**:
  - Two independent credit limits: **stream-level** via `MAX_STREAM_DATA` (§19.10) and **connection-level** via `MAX_DATA` (§19.9). "Senders MUST NOT send data in excess of either limit" (§4.1).
  - Initial limits arrive as transport parameters during the handshake (§7.4); the receiver later advertises larger windows by sending updated MAX_* frames.
  - Blocked senders SHOULD emit `STREAM_DATA_BLOCKED` (§19.13) or `DATA_BLOCKED` (§19.12) so the receiver knows to extend credit — this is the transport-native backpressure signal.
  - Final byte accounting uses the FIN bit on STREAM frames or the Final Size field of RESET_STREAM (§4.5), so both ends agree on stream length.
- **Informs**: FR-018 (spec-025 link backpressure can lean on / coexist with QUIC flow control rather than reinvent it)
- **Confidence**: high

### [4] RFC 9000 — Connection IDs, migration, and failure modes (§5, §9, §10, §11)
- **URL**: https://www.rfc-editor.org/rfc/rfc9000.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: failure-modes
- **Close-read findings**:
  - Connection IDs decouple a connection from UDP/IP 4-tuples so address changes don't misroute packets (§5.1); they are issued/retired via `NEW_CONNECTION_ID` (§19.15) / `RETIRE_CONNECTION_ID` (§19.16) and MUST NOT be externally correlatable.
  - Path migration (§9) is validated with `PATH_CHALLENGE`/`PATH_RESPONSE` (§19.17–18) — relevant only if the prototype ever roams; a loopback/LAN prototype can ignore migration.
  - Immediate termination uses `CONNECTION_CLOSE` (§19.19) with error codes from §20; connection vs stream errors are distinguished in §11.
  - Idle timeout (§10.1) closes silent connections; `PING` (§19.2) keeps them alive; stateless reset (§10.3) lets a peer that lost state signal unusability.
- **Informs**: FR-001 (clean teardown/keepalive), FR-018 (link failure-mode mapping)
- **Confidence**: high

### [5] RFC 9001 — Using TLS to Secure QUIC: handshake + TLS-1.3-only (§4, §4.1, §5, §4.6)
- **URL**: https://www.rfc-editor.org/rfc/rfc9001.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: handshake
- **Close-read findings**:
  - **TLS 1.3 is mandatory.** "Clients MUST NOT offer TLS versions older than 1.3 ... An endpoint MUST terminate the connection if a version of TLS older than 1.3 is negotiated" (§4.2). TLS 1.2 cannot be used with QUIC.
  - TLS handshake messages are carried in CRYPTO frames (not TLS records); four encryption levels — Initial, 0-RTT/Early Data, Handshake, 1-RTT (§4.1.3). Handshake is *complete* when both Finished messages are sent+verified (§4.1.1), *confirmed* via HANDSHAKE_DONE / 1-RTT ACK (§4.1.2).
  - Packet-protection keys are derived with HKDF-Expand-Label using QUIC labels "quic key", "quic iv", "quic hp" (§5.1).
  - Post-handshake client authentication is forbidden: servers MUST NOT send post-handshake CertificateRequest; a client receiving one MUST treat it as PROTOCOL_VIOLATION (§4.4).
- **Informs**: FR-001, FR-003 (TLS 1.3 is the only auth channel)
- **Confidence**: high

### [6] RFC 9001 — Peer authentication, certificates, and ALPN requirement (§4.4, §8.1)
- **URL**: https://www.rfc-editor.org/rfc/rfc9001.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - "A client MUST authenticate the identity of the server" — normally X.509 verification that the server identity is in a certificate issued by a trusted entity (§4.4). For a self-signed shared cert, the trust anchor is that exact certificate pinned on both ends; standard X.509 chain rules otherwise apply (self-signed is not specially carved out).
  - Server MAY request client authentication and MAY refuse a client that cannot authenticate (§4.4) — enables mutual auth off the same shared cert.
  - **ALPN is mandatory for QUIC**: endpoints use the TLS ALPN extension and "MUST abort the connection if no application protocol is negotiated" (§8.1). No silent fallback.
  - Certificate-chain size matters for handshake performance (§4.4); a single small self-signed cert is favorable here.
- **Informs**: FR-003, SC-005 (the shared self-signed cert as sole trust anchor — pin it, disable CA-path trust)
- **Confidence**: high

### [7] RFC 9002 — QUIC Loss Detection and Congestion Control (§5, §6, §7)
- **URL**: https://www.rfc-editor.org/rfc/rfc9002.html
- **Type / version / date**: IETF Proposed Standard RFC; May 2021
- **Architectural concern**: concurrency
- **Close-read findings**:
  - Loss detection uses monotonically increasing packet numbers (no TCP retransmission ambiguity); a packet is lost by packet-threshold (kPacketThreshold = 3, §6.1.1) or time-threshold (kTimeThreshold = 9/8 × max(smoothed_rtt, latest_rtt), §6.1.2).
  - RTT estimation: min_rtt floor (§5.2) and smoothed_rtt = 7/8·smoothed + 1/8·adjusted, rttvar EWMA (§5.3).
  - Default congestion controller is NewReno-like with slow start / recovery (ssthresh = cwnd/2) / congestion avoidance (AIMD) (§7.3); persistent congestion collapses cwnd to ~2 packets (§7.6). PTO = smoothed_rtt + max(4·rttvar, kGranularity) + max_ack_delay (§6.2.1), doubling per consecutive timeout.
  - **Congestion control is per-path, not per-stream** (§7): multiple multiplexed streams share one controller, so per-stream backpressure (FR-018) must be enforced at the QUIC flow-control / app layer, not expected from congestion control.
- **Informs**: FR-001 (transport robustness is the library's job), FR-018 (stream-level fairness is app/flow-control concern, not congestion control)
- **Confidence**: high

### [8] RFC 9114 — HTTP/3: stream mapping, "h3" ALPN, SETTINGS, GOAWAY (§3, §6, §7.2.4, §5.2)
- **URL**: https://www.rfc-editor.org/rfc/rfc9114.html
- **Type / version / date**: IETF Proposed Standard RFC; June 2022
- **Architectural concern**: ALPN
- **Close-read findings**:
  - The ALPN token for HTTP/3 is exactly **"h3"** (§3.1) — this is what the QUIC TLS handshake must advertise/select for an HTTP/3 connection.
  - Request/response semantics ride **client-initiated bidirectional** streams (§6.1); each side opens a single **unidirectional control stream** whose first frame MUST be SETTINGS (§6.2.1).
  - A SETTINGS frame MUST be the first frame on the control stream and MUST NOT be sent again (§7.2.4). HTTP/3 defines its own settings registry (so Extended-CONNECT's setting is registered separately for h3 — see RFC 9220, not RFC 9114).
  - Graceful shutdown is via GOAWAY (§5.2.1): requests/pushes at or above the indicated identifier are rejected.
  - Note: `SETTINGS_ENABLE_CONNECT_PROTOCOL` and the `:protocol` pseudo-header are NOT in RFC 9114 — they are added for HTTP/3 by RFC 9220.
- **Informs**: FR-001 (ALPN "h3" is mandatory), FR-002 (WS rides a bidi request stream)
- **Confidence**: high

### [9] RFC 9220 — Bootstrapping WebSockets with HTTP/3 (Extended CONNECT for h3)
- **URL**: https://www.rfc-editor.org/rfc/rfc9220.html
- **Type / version / date**: IETF Proposed Standard RFC; June 2022
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Adapts RFC 8441's Extended CONNECT to HTTP/3: "the semantics of the pseudo-header fields and setting are identical to those in HTTP/2" (§3.1). It is essentially a version-specific adapter, **no substantive mechanical changes** beyond the registration.
  - `SETTINGS_ENABLE_CONNECT_PROTOCOL` is registered for HTTP/3 with identifier value **0x08 (decimal 8)**, same numeric value as HTTP/2, because §A.3 of RFC 9114 requires HTTP/3 settings to be registered separately (§3.2). The server advertises value 1 to enable Extended CONNECT.
  - The WebSocket runs over **a single (client-initiated bidirectional) HTTP/3 stream** — "running the WebSocket Protocol over a single stream ... is equally applicable to HTTP/3" (§3.1).
  - Closure mapping: orderly close maps to QUIC stream FIN; abrupt termination uses the HTTP/3 stream error `H3_REQUEST_CANCELLED` (§3.4). An unsupported `:protocol` value → server SHOULD respond 501 Not Implemented (§3.3).
- **Informs**: FR-002 (the exact h3 WS bootstrap), FR-018 (one stream per link channel)
- **Confidence**: high

### [10] RFC 8441 — Bootstrapping WebSockets with HTTP/2 (Extended CONNECT predecessor)
- **URL**: https://www.rfc-editor.org/rfc/rfc8441.html
- **Type / version / date**: IETF Proposed Standard RFC; September 2018
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Defines `SETTINGS_ENABLE_CONNECT_PROTOCOL` (identifier **0x8**, value 0 or 1) (§3). On receipt of value 1 a client MAY use Extended CONNECT; a sender MUST NOT downgrade from 1 back to 0 later. (RFC 9220 inherits these semantics verbatim.) Practically: the client MUST wait until it has seen the enabling setting before issuing Extended CONNECT.
  - Extended CONNECT request: `:method = CONNECT` plus the new single-valued `:protocol` pseudo-header; `:scheme` and `:path` MUST be present (unlike plain CONNECT which omits them), and `:authority` is interpreted per RFC 7540 §8.1.2.3 (§4).
  - `:protocol` MUST equal **"websocket"** to initiate a WebSocket (§5); the server then establishes a tunnel of that protocol type.
  - The single HTTP/2 stream from the CONNECT transaction is used "as if it were the TCP connection"; a **2xx status (e.g. 200)** signals success, after which WebSocket data frames flow on the stream (§5).
- **Informs**: FR-002 (the canonical Extended-CONNECT contract that 9220 reuses)
- **Confidence**: high

### [11] RFC 7301 — TLS Application-Layer Protocol Negotiation (ALPN) Extension
- **URL**: https://www.rfc-editor.org/rfc/rfc7301.html
- **Type / version / date**: IETF Proposed Standard RFC; July 2014
- **Architectural concern**: ALPN
- **Close-read findings**:
  - Client lists supported protocols in the `application_layer_protocol_negotiation(16)` extension of ClientHello; server returns the same extension in ServerHello naming the one selected (§3.1).
  - Server SHOULD pick its most-preferred protocol that the client also advertised; unknown names are ignored (§3.2).
  - If there is no protocol in common the server SHALL send a fatal `no_application_protocol` alert (code 120) (§3.2) — this is the basis for QUIC's "MUST abort if nothing negotiated".
  - Negotiation completes inside the existing ClientHello/ServerHello exchange with no extra round-trips (§1); protocol identifiers are IANA-registered opaque byte strings (§3.1, §6) — e.g. "h3".
- **Informs**: FR-001 (the negotiation that selects "h3" for QUIC)
- **Confidence**: high

### [12] RFC 6455 — The WebSocket Protocol: data framing (§5)
- **URL**: https://www.rfc-editor.org/rfc/rfc6455.html
- **Type / version / date**: IETF Proposed Standard RFC; December 2011
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - When bootstrapped over HTTP/2/HTTP/3 the *opening handshake* (Sec-WebSocket-Key etc.) is replaced by Extended CONNECT, but the **§5 data framing remains normative and identical**: FIN bit, 4-bit opcode (0x1 text, 0x2 binary, 0x8 close, 0x9 ping, 0xA pong), 7/16/64-bit payload length, optional 4-byte masking key.
  - Masking: "A client MUST mask all frames that it sends to the server"; the server MUST close on receiving an unmasked frame and MUST NOT mask frames it sends (§5.1, §5.3 XOR algorithm). This masking obligation still applies over h3.
  - Fragmentation (§5.4): first frame FIN=0 with a non-zero opcode, continuation frames opcode 0x0, terminated by FIN=1; control frames MAY interleave but MUST NOT be fragmented.
  - Close handshake (§5.5.1): Close frame opcode 0x8 with optional 2-byte status code; an endpoint receiving Close that hasn't sent one MUST reply with Close. Ping/Pong (§5.5.2–3): a Ping MUST be answered with a Pong echoing identical application data.
- **Informs**: FR-002 (frame-level WS behavior the C# WS layer must implement), FR-018 (WS frames carry spec-025 link framing/sequencing on top)
- **Confidence**: high

---

## Cluster feasibility verdict
- **TLS 1.3 is non-negotiable.** A conformant C#/.NET QUIC endpoint MUST negotiate TLS 1.3 or later and MUST terminate if anything older is offered/negotiated (RFC 9001 §4.2). The .NET QUIC stack (System.Net.Quic / MsQuic) already enforces this — the prototype cannot and need not fall back to TLS 1.2.
- **Handshake order is fixed and combined.** QUIC fuses transport + TLS in one handshake (CRYPTO frames over Initial→Handshake→1-RTT levels); the server is always authenticated, the client optionally. ALPN selection happens inside that TLS handshake before any application data (RFC 7301 §1), and QUIC MUST abort if no ALPN protocol is agreed (RFC 9001 §8.1).
- **ALPN token "h3" is mandatory** for the HTTP/3 layer (RFC 9114 §3.1). The C# server and client must both advertise/accept exactly "h3"; mismatch → fatal `no_application_protocol` alert (RFC 7301 §3.2).
- **WebSocket bootstrap = Extended CONNECT over one bidi stream.** The server MUST advertise `SETTINGS_ENABLE_CONNECT_PROTOCOL = 1` (HTTP/3 setting id 0x08) on its control stream first; the client MUST wait for that setting, then send a request with `:method=CONNECT`, `:protocol=websocket`, and (unlike plain CONNECT) `:scheme`, `:path`, `:authority` present, on a single client-initiated bidirectional QUIC stream; a 2xx response opens the tunnel (RFC 9220 §3, RFC 8441 §3–5). After that, RFC 6455 §5 frame mechanics apply unchanged (including client-side masking).
- **Self-signed shared cert as the only trust anchor is standards-compatible but requires explicit pinning.** RFC 9001 §4.4 mandates the client authenticate the server's identity via the certificate; with no CA in play the prototype must pin/validate against the exact shared self-signed cert and disable default CA-chain trust (SC-005, FR-003). Mutual auth is available off the same cert (server MAY request client auth), but post-handshake client auth is forbidden (PROTOCOL_VIOLATION).
- **Per-stream backpressure is the app's job, not congestion control's.** QUIC congestion control is per-path and shared across multiplexed streams (RFC 9002 §7); only QUIC *flow control* (MAX_DATA / MAX_STREAM_DATA, DATA_BLOCKED) is per-stream/per-connection (RFC 9000 §4). So spec-025 link sequencing/backpressure (FR-018) layers on top of QUIC stream flow control, and the single WS stream means head-of-line ordering within the link is provided for free by the stream's ordered byte semantics.
