# Contract: Stack Adapter

**Feature**: 036-http3-quic-ws-link. The Python control plane drives each data-plane stack through one uniform
adapter contract so the two stacks are interchangeable (FR-009/FR-010). `csharp` is the reference (must reach the
full real-QUIC LAN demo first); `gleam` is the staged second implementation.

## Adapter ABC (`glp_quick.stacks.base.StackAdapter`)

| Method | Responsibility |
|--------|----------------|
| `name() -> str` | `"csharp"` \| `"gleam"`. |
| `capabilities() -> dict` | At least `{"real_quic": bool}` — `gleam` reports this **honestly** (Decision 8). |
| `start_server(bind, port, cert, max_clients, repl) -> Handle` | Launch + supervise the server-role runtime; bind UDP; load shared cert; accept ≤ max_clients. |
| `start_client(server_addr, port, cert, repl) -> Handle` | Launch + supervise the client-role runtime; QUIC handshake (fingerprint-pinned); bring up the WS link. |
| `health(handle) -> Status` | Liveness of the supervised runtime. |
| `stop(handle)` | Graceful drain + stop; siblings unaffected. |

`Handle` exposes the GLP-message I/O seam (`send`, `recv`, `peers`) bridged by `repl_link.py`.

## C# adapter (`glp_quick.stacks.csharp`) — reference
- Launches the C# QUIC+WS endpoint, implemented as a new `ILinkTransport`/`ILinkEndpoint` leaf in
  `csharp/glp_link/transports/` (Http3QuicTransport / Http3QuicEndpoint / WebSocketOverHttp3), reusing spec 025's
  reliability sublayer + ground-relay discipline (FR-018).
- Real QUIC via System.Net.Quic / MsQuic; HTTP/3 via Kestrel; WS over HTTP/3 (RFC 9220, with the 025-framing fallback).
- GLP REPL endpoint defaults to `out/csharp/glp_repl` (mandated default; rebuild — prebuilt `.exe` may be stale).
- **MUST** pass the full real-QUIC LAN demo (SC-001..SC-006) before the Gleam stack starts (FR-010).

## Gleam adapter (`glp_quick.stacks.gleam`) — staged second stack
- Greenfield `gleam_quic/` (Gleam src + Node WASM host invoking the AtomVM/WASM binary).
- Built out in stages against the identical wire/CLI contract after the C# reference is complete.
- If genuine QUIC is infeasible on AtomVM/WASM, `capabilities().real_quic` reports `false` and the limitation is
  surfaced honestly — never simulated and passed off as real (constitution II; spec Gleam-feasibility edge case).

## Invariance guarantee (SC-006)
Both adapters expose the same `Handle` seam and produce the same observable demo outcomes; the operator sees an
identical CLI/wire/handshake surface regardless of `--stack`.
