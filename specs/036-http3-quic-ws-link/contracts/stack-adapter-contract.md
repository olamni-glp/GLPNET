# Contract: Stack Adapter

**Feature**: 036-http3-quic-ws-link. The Python control plane drives each data-plane stack through one uniform
adapter contract so the two stacks are interchangeable (FR-009/FR-010). `csharp` is the reference (must reach the
full real-QUIC LAN demo first); `gleam` is the staged second implementation.

## Adapter ABC (`glp_quick.stacks.base.StackAdapter`)

| Method | Responsibility |
|--------|----------------|
| `name() -> str` | `"csharp"` \| `"gleam"`. |
| `profile() -> str \| None` | `gleam` only: `"a"` (AtomVM + native QUIC side-process) \| `"c"` (full BEAM + `quicer`/MsQuic). |
| `capabilities() -> dict` | At least `{"real_quic": bool, "quic_termination": "in_process" \| "side_process"}` — `gleam` reports both **honestly** (Decision 8). |
| `start_server(bind, port, cert, max_clients, repl) -> Handle` | Launch + supervise the server-role runtime; bind UDP; load shared cert; accept ≤ max_clients. |
| `start_client(server_addr, port, cert, repl) -> Handle` | Launch + supervise the client-role runtime; QUIC handshake (fingerprint-pinned); bring up the WS link. |
| `health(handle) -> Status` | Liveness of the supervised runtime. |
| `stop(handle)` | Graceful drain + stop; siblings unaffected. |

`Handle` exposes the GLP-message I/O seam (`send`, `recv`, `peers`) bridged by `repl_link.py`.

## C# adapter (`glp_quick.stacks.csharp`) — cross-platform reference
- Launches the C# QUIC+WS endpoint, implemented as a new `ILinkTransport`/`ILinkEndpoint` leaf in
  `csharp/glp_link/transports/` (`QuicTransport` / `QuicEndpoint` / `WebSocketOverQuic` / `ConnectBootstrap`),
  reusing spec 025's reliability sublayer + ground-relay discipline (FR-018).
- Real QUIC via `System.Net.Quic` / MsQuic (GA in .NET 9, **cross-platform** — Win11/Server2022+, Linux
  `libmsquic` 2.2+, macOS partial), gated on `QuicListener`/`QuicConnection.IsSupported`. The WS link is **genuine
  RFC 6455 framing over a raw bidi `QuicStream`** (025 `FrameCodec`) via a minimal CONNECT-style bootstrap; the
  RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap is isolated behind the `ConnectBootstrap` seam (later interop only).
- GLP REPL endpoint defaults to `out/csharp/glp_repl` (mandated default; rebuild — prebuilt `.exe` may be stale).
- **MUST** pass the full real-QUIC LAN demo (SC-001..SC-006) before the Gleam stack starts (FR-010).

## Gleam adapter (`glp_quick.stacks.gleam`) — staged second stack, two deployment profiles
- Greenfield `gleam_quic/`; built out in stages against the identical channel-link contract after the C#
  reference is complete. Interchangeable **at the contract, not at QUIC termination** — genuine QUIC on bare
  AtomVM/WASM is infeasible (research §F2).
- **Profile A** (MAUI Blazor hybrids, smaller nodes): Gleam/AtomVM logic + WebSocket link / **native genuine-QUIC
  side-process** over length-prefixed local IPC; `capabilities()` = `{real_quic: true, quic_termination: "side_process"}`.
- **Profile C** (workstations/servers): Gleam on **full BEAM + `quicer`/MsQuic** terminating genuine in-process
  QUIC; `capabilities()` = `{real_quic: true, quic_termination: "in_process"}`.
- Side-process QUIC is surfaced honestly — never simulated and passed off as in-runtime QUIC (constitution II;
  spec Gleam-feasibility edge case).

## Invariance guarantee (SC-006)
Both adapters expose the same `Handle` seam and produce the same observable demo outcomes; the operator sees an
identical CLI/wire/handshake surface regardless of `--stack`.
