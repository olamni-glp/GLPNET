# Contract: Wire / Message Protocol

**Feature**: 036-http3-quic-ws-link. The transport contract is **identical across stacks** (FR-010). Reliability
semantics are **inherited from spec 025** (FR-018); this contract pins the QUIC/HTTP-3/WebSocket layering and the
GLP-message envelope on top of it.

## Layering (bottom → top)

| Layer | Protocol | Normative source | Notes |
|-------|----------|------------------|-------|
| L1 Transport | QUIC | RFC 9000 + RFC 9001 (TLS) + RFC 9002 (loss/cc) | Real on-wire handshake (FR-001); ALPN `h3`; gate on `IsSupported`. |
| L2 Application | HTTP/3 | RFC 9114 | Genuine HTTP/3-capable connection (ALPN `h3`, FR-001). Kestrel available for the optional RFC 9220 seam. |
| L3 Link | **genuine RFC 6455 WebSocket framing over one bidi `QuicStream`** | RFC 6455 (+ the carriage RFC 9220 standardizes) | One WS per stream, reusing 025 `FrameCodec`, via a minimal CONNECT-style bootstrap on the stream. The RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap (unshipped in .NET) is isolated behind a seam for later browser interop only (Decision 3). |
| L4 Link reliability | spec 025 sublayer | `specs/025-multi-protocol-link-layer/contracts/link-primitives.md` | Framing(version+CRC32+fragment), seq/dedup/reorder, epoch/fencing, backpressure window N. |
| L5 Message | GLP-message envelope | this contract | Ground GLP terms only. |

## Trust (FR-003 / SC-005)
- TLS 1.3 server cert = the shared self-signed cert (no SAN/hostname used for trust).
- Both ends accept the peer **iff** the presented cert's **SPKI (SubjectPublicKeyInfo) SHA-256** equals the pinned
  shared value (SPKI pin survives re-issue with the same key; preferred over the whole-cert thumbprint).
- The validation callback **never** `return true`; it waives **only** the no-CA-chain error + hostname mismatch
  (trust is the pin, not the name). No CA chain, no hostname validation, no public-CA path is consulted.
  Mismatch → handshake rejected, clear error.

## GLP-message envelope (L5)
```
{ msg_id, from, to, seq, payload }
  msg_id  : unique per message (dedup key in concert with 025 seq)
  from    : endpoint_id of the sending GLP REPL endpoint
  to      : endpoint_id | "broadcast"
  seq     : 025 per-link sequence number
  payload : a GROUND GLP term (025 ground-relay discipline — no _w / _r placeholders cross the wire)
```
- **Full-duplex** (FR-008a): both ends may have outstanding messages simultaneously; ordering/dedup per 025.
- **Mesh** (FR-008b): the server routes by `to`; `broadcast` fans out to all other participating endpoints; each
  participating REPL can reach each other (peer-to-peer over the duplex mesh).

## Failure contract (FR-019)
Every failure is a **clear, distinct** terminal signal — never a silent hang or half-open link:
`cert_mismatch`, `alpn_version_mismatch`, `udp_blocked`, `server_not_ready`, `link_dropped`, `over_capacity`.
Link faults surface on spec 025's monitor stream as ordinary bound terms (`ok`/`closed`/`tempFail`/`permFail`).

## Cross-stack conformance (SC-006)
A conformance vector (handshake → link-up → single message → full-duplex → 3-node mesh → concurrent isolation →
single-failure resilience) MUST produce identical observable outcomes for `csharp` and (when built) `gleam`.
