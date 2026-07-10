# Contract: Link-layer parity (M2)

The Gleam link layer's interop contract. Normative sources (referenced, not duplicated): `specs/025-multi-protocol-link-layer/contracts/link-primitives.md` (+ guards.md, glp-canonical-forms.md, rulings-log.md); wire framing per the shipped implementations (`glp_runtime/lib/link/reliability/frame_codec.dart`, `csharp/glp_link/reliability/FrameCodec.cs`); QUIC-WS wire behaviour per `csharp/glp_link/transports/{QuicTransport,QuicEndpoint,WebSocketOverQuic,ConnectBootstrap}.cs` (C# is the QUIC-WS reference peer — it is the only shipped implementation).

## Primitives (FR-013)

Port the 025 primitive set with unchanged GLP-visible semantics: `link_send`, `link_recv`, establish/listen/accept/request/setup/close/monitor kernels, link registry, egress/pump. GLP-visible terms, guards, and fault shapes conform to the 025 contracts verbatim — any needed deviation STOPs and escalates (language-authority + spec-first).

## Wire (FR-013, FR-015, SC-004)

- Term payloads: 038 TLV term codec — byte-for-byte identical encodings (golden vectors: `specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex`).
- Framing: FrameCodec envelope parity — header/flags/sequence/CRC32 identical to Dart/C# so any two runtimes interop.
- Untrusted receipt: validate length bounds → CRC → type tag before decode; violations produce a fault term (fault-as-data), never a crash, never memory unsafety. Threat-model decisions D11/D12 (dossier RECONFIGURATION.md) apply.

## Distributed unification (FR-014, PI:17)

- Deferred-local-assignment: only the owning instance binds its writer; remote requests queue.
- `known/1` boundary: globalize on export, localize on import.
- Deref chains crossing instances terminate (FORK-1 discriminator honoured); convergence is proof-gated (contracts/proof-obligations.md).

## Transports (clarified 2026-07-10 — all gating)

| Scheme | Gleam route | Test host | Interop peer |
|---|---|---|---|
| loopback | in-BEAM | native + WSL | Gleam↔Gleam |
| tcp | gen_tcp (FFI/gleam_erlang) | native + WSL | C# `TcpTransport`, Dart `tcp_transport` |
| quic-ws (HTTP3) | `gleam_quic` Profile-C FFI (quicer/MsQuic) | WSL only (049 ruling) | C# `WebSocketOverQuic` |

Transport seam: one Gleam port type mirroring `i_link_transport`; the layer above is scheme-agnostic. Certificates: shared `glpquick-cert/` self-signed material, as in the C# QUIC tests.

## Cross-runtime capstone (FR-016, SC-005, SC-008)

- Extend `test/link/run_link_tests_cross.sh` (or a sibling `run_link_tests_cross_gleam.sh`) to host roles on the Gleam instance: the 8 scenarios (`pc_integers`, `pc_strings`, `pc_terms`, `link_send_wrapper`, `link_recv_chain`, `bidirectional`, `path_b_request_accept`, `monitor_close`) × 2 directions = 16 runs, C#↔Gleam, all passing (hard gate).
- Transport coverage: full 16/16 on TCP; loopback where the rig supports same-host pairs; QUIC-WS runs the suite under WSL with the C# peer (SC-008: identical per-scenario outcomes across transports).
- Quiescence oracle (GAP-G6) is used by the rig to decide run completion before verdict comparison.
