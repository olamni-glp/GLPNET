# Phase 0 Research: HTTP/3 (QUIC) + WebSocket Channel-Link Prototype

**Feature**: 036-http3-quic-ws-link | **Date**: 2026-06-27

This file records the **architectural decisions** that resolve the plan's open technical questions, plus the
**methodology** for the marathon's research stages. The full ~50 C# + ~50 Gleam/AtomVM corpus and its close-read
distillation notes (FR-014/FR-015, SC-007) are produced *during* the marathon's research/corpus/distill stages
and land under `specs/036-http3-quic-ws-link/research/`; this document is the decision record that gates design.

---

## Decision 1 — Python is the control plane, never the QUIC endpoint

- **Decision**: The single Python tool (`glp_quick`) owns the CLI surface, shared-cert generation, process
  launch/supervision, the GLP-REPL↔link bridge, and the demo harness. The genuine QUIC/HTTP-3/WebSocket
  endpoint lives in the per-stack data plane (C# first, Gleam second).
- **Rationale**: FR-009/FR-010 mandate exactly two interchangeable transport stacks (C#/.NET, Gleam/AtomVM) for
  an apples-to-apples comparison. A Python QUIC library (e.g. aioquic) would be a *third* stack and dilute that
  comparison; the spec also already says per-stack runtimes are "invoked/managed by" the Python tool (Assumptions).
- **Alternatives rejected**: (a) aioquic in Python — adds an unmandated stack, breaks the comparison. (b) Python
  hosts QUIC and shells GLP — same problem plus blurs the control/data split.

## Decision 2 — Real QUIC + HTTP/3 in C#: System.Net.Quic / MsQuic + Kestrel

- **Decision**: Stack A uses `System.Net.Quic` (real QUIC, MsQuic under the hood) carrying the link on a raw
  `QuicStream`, with Kestrel HTTP/3 available for the optional RFC 9220 seam. This is the reference that must
  reach the full real-QUIC LAN demo. **CONFIRMED cross-platform (2026-06-28 corpus, §F1/§4): NOT Windows-locked.**
- **Rationale**: `System.Net.Quic` is **GA in .NET 9** and runs on Windows 11/Server 2022+ (msquic.dll ships
  with the runtime), Linux (`libmsquic` 2.2+/OpenSSL from packages.microsoft.com), and macOS (partial — `brew
  install libmsquic` + `DYLD_FALLBACK_LIBRARY_PATH`) — [MS Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview).
  ALPN + custom cert-validation callbacks are exactly what FR-001/FR-003 require. This repo's demo hosts are
  Windows 11 (confirmed) but the design MUST NOT assume Windows.
- **Mandatory gate**: every endpoint checks `QuicListener.IsSupported`/`QuicConnection.IsSupported` (silent
  failure otherwise) and verifies msquic availability (historical packaging regression dotnet/runtime #81447)
  before claiming a real handshake; sets mandatory ALPN + `DefaultStreamErrorCode`/`DefaultCloseErrorCode`.
- **Closed by corpus**: client-side cert pinning via `QuicClientConnectionOptions` /
  `SslClientAuthenticationOptions.RemoteCertificateValidationCallback` (corpus C#-02/C#-05).
- **Alternatives rejected**: raw MsQuic P/Invoke (lower-level than needed for a prototype); a non-Kestrel HTTP/3
  server (loses the WebSocket-over-HTTP/3 integration Kestrel provides).

## Decision 3 — Genuine WebSocket over QUIC: RFC 6455 framing over a QUIC bidi stream (first-class, not a fallback)

- **Decision (corrected 2026-06-28, §F1/§4/§5.2)**: The link is a **genuine WebSocket carried as RFC 6455
  framing over a single QUIC bidirectional stream** (one WS per stream — the exact carriage RFC 9220
  standardizes), reusing spec 025's `FrameCodec`, established by a **minimal CONNECT-style bootstrap on the
  stream**. This is a first-class WS-over-QUIC design (QUIC/HTTP-3 is de-facto dominant, ~21–39% of web
  traffic), **not** a consolation fallback. RFC 6455 details: opcodes text 0x1/binary 0x2/close 0x8/ping
  0x9/pong 0xA, FIN/continuation, varint length; masking N/A on a TLS-encrypted intermediary-free QUIC stream.
- **Rationale**: It satisfies FR-002 genuinely and the prototype owns both C# endpoints on a LAN, so it does
  **not** need RFC 9220 browser support. The RFC 9220 **Extended-CONNECT-over-HTTP/3 bootstrap** is the *only*
  piece .NET has not shipped (`ClientWebSocket`'s bootstrap ceiling is HTTP/2/RFC 8441; Kestrel rejects CONNECT —
  dotnet/aspnetcore #32004) — and it matters **only for third-party/browser interop** (out of MVP scope), so it
  is isolated behind a handshake seam to slot in unchanged when .NET ships it. WebTransport-over-HTTP/3 is noted
  as the future browser-native client path. Nothing is simulated (constitution II).
- **Closed by corpus** (C#-01/C#-03/C#-04): RFC 9220/8441 maturity in .NET confirmed; the seam isolation is the
  agreed disposition, not an open question.
- **Alternatives rejected**: WebSocket over HTTP/2/TCP (not QUIC — fails FR-001); raw QUIC datagrams (loses
  WebSocket framing FR-002); blocking on the unshipped RFC 9220 .NET bootstrap (needlessly couples the LAN
  prototype to a browser-interop feature it does not need).

## Decision 4 — Reuse spec 025's link seam, reliability sublayer, and ground-relay wire discipline (FR-018)

- **Decision**: The C# QUIC+WS endpoint is implemented as a new `ILinkTransport` / `ILinkEndpoint` leaf in
  `csharp/glp_link/transports/`, alongside `LoopbackTransport` / `TcpTransport`, inheriting spec 025's framing
  (`FrameCodec`, `Crc32`), sequencing/dedup/reorder, epoch/fencing, backpressure window, and **ground-relay
  discipline** (only ground terms cross the wire). Spec 025's Phase 6 already lists WS/WSS as planned leaves.
- **Rationale**: FR-018 mandates reuse rather than re-derivation; 025 is the authoritative link-layer spec
  (constitution VIII). This gives correct redelivery/ordering/backpressure for free and keeps the wire contract
  consistent across transports.
- **Alternatives rejected**: a standalone QUIC link implementation ignoring 025 — duplicates the link layer,
  violates FR-018 and single-source-of-truth.

## Decision 5 — Shared self-signed certificate, generated by the Python tool, pinned out-of-band

- **Decision**: `glp_quick cert` generates one self-signed certificate + key (via `cryptography`), with **no**
  hostname/SAN dependence used for trust; both server and clients are configured to trust *that exact cert*
  (fingerprint pinning) and nothing else. Distribution is a manual file copy out-of-band.
- **Rationale**: FR-003/SC-005 require the shared self-signed cert to be the only trust anchor — no CA, no
  enrollment, no domain. Fingerprint pinning lets the C# client's `RemoteCertificateValidationCallback` accept
  by identity, not by chain/hostname.
- **Closed by corpus (C#-05, §4 Decision 5) — concrete recipe**: pin the **SPKI (SubjectPublicKeyInfo) SHA-256**
  (survives re-issue with the same key) rather than the whole-cert thumbprint; in the validation callback never
  `return true` — waive **only** the no-CA-chain error + hostname mismatch (trust is the pin, not the name).
  Python `cryptography` cert profile: `subject == issuer`, `BasicConstraints(ca=False)`,
  `KeyUsage(digital_signature, key_encipherment)`, `EKU[serverAuth, clientAuth]`, EC P-256 or RSA-2048; export
  PFX (holder) + PEM (distribution). Gleam/BEAM TLS expresses the same pin in its profile's TLS stack.
- **Alternatives rejected**: a tiny private CA (adds enrollment the spec forbids); disabling cert validation
  (insecure *and* fails the "authenticate using the shared cert" requirement — not a no-op).

## Decision 6 — "Run GLP over the link" = GLP REPL endpoints exchanging messages

- **Decision**: The link connects GLP REPL endpoints that exchange messages (not submit-source/return-result
  RPC). Progression: (1) one REPL sends, one listens; (2) full-duplex; (3) ≥3 REPLs peer-to-peer in a duplex
  mesh. The Python `repl_link.py` bridges each REPL's message I/O to a link endpoint.
- **Rationale**: fixed by the 2026-06-27 clarification (spec §Clarifications, FR-008/008a/008b).
- **Open design item (Constitution IV-a gate)**: whether bridging REPL messages onto the link can be done
  entirely with spec 025's existing, owner-approved GLP link primitives + transport-level relay, or whether any
  *new* GLP language surface is needed. **Default assumption: no new primitive.** If one proves necessary, STOP
  and obtain owner approval before implementing (hard gate carried into tasks).
- **Alternatives rejected**: RPC framing (explicitly rejected in the clarification).

## Decision 7 — C# GLP REPL as the default endpoint runtime

- **Decision**: GLP REPL endpoints default to the C# GLP REPL at `out/csharp/glp_repl` (the mandated default,
  already wired with spec 025's `LinkKernels.Install`); the Dart REPL remains available on demand.
- **Rationale**: consistency with the C#-first stack ordering and the repo's mandated-default REPL; the link
  kernels are already installed there. Note the prebuilt `.exe` may be stale — invoke via `dart run`/rebuild.
- **Alternatives rejected**: Dart-only REPL (inconsistent with C#-first reference path).

## Decision 8 — Gleam as two deployment profiles, interchangeable at the channel-link contract (RESOLVED 2026-06-28)

- **Finding (§F2, HIGH confidence)**: genuine QUIC on **bare AtomVM/WASM is infeasible** — AtomVM's `ssl` is
  client-only/crash-in-active with no RFC-9001 secret export; `quicer` (a C NIF over MsQuic) cannot load on
  AtomVM (no runtime NIFs); the WASM/Node host cannot originate QUIC (no raw UDP). So "two interchangeable
  genuine-QUIC stacks at the QUIC-termination layer" is not achievable as originally written.
- **Decision (Gabi 2026-06-28)**: After the C# reference passes the full real-QUIC LAN demo, the Gleam stack is
  built out in stages against the identical channel-link contract, shipped as **two deployment profiles,
  interchangeable at the contract (not at QUIC termination)**: **Profile A** — Gleam/AtomVM logic + WebSocket
  link / **native genuine-QUIC side-process** (length-prefixed local IPC: Erlang `open_port` on native AtomVM,
  WebSocket-proxy shape on WASM), `real_quic` truthfully attributed to the side-process — for MAUI Blazor hybrids
  and smaller freestanding nodes; **Profile C** — Gleam on **full BEAM + `quicer`/MsQuic** terminating genuine
  in-process QUIC — for larger workstations and servers. "AtomVM" is relaxed to "a BEAM-family runtime,
  profile-dependent." At least one profile achieves genuine QUIC; side-process QUIC is reported honestly (constitution II).
- **Rationale**: FR-009/FR-010 sequencing + the spec's Gleam-feasibility edge case + constitution II; preserves
  interchangeability at the contract boundary without faking in-runtime QUIC.
- **Residual probe (implementation start)**: whether AtomVM-WASM `open_port` spawn works (build-time) — only if Profile A is the chosen target.

## Decision 9 — Marathon durability via the existing harness, state out-of-repo

- **Decision**: Run as one marathon (`mrun-15d7dd0ffbc2`) on the buildkit-marathon harness; state is additive
  `marathon_*` rows in the out-of-repo deploy-home catalog + a per-run Markdown mirror. Resume is from objective
  persisted state (max-sequence checkpoint), never a summary.
- **Rationale**: FR-012/FR-013/SC-008; constitution VI-b exemption for the isolated out-of-repo store.

---

## Research-corpus methodology (FR-014 / FR-015 / SC-007)

- **Targets**: ~50 sources for the C#/.NET stack and ~50 for the Gleam/AtomVM stack.
- **Mandatory coverage**: RFC 9114 (HTTP/3), RFC 9000 (QUIC transport), RFC 9001 (QUIC-TLS), RFC 9002 (QUIC
  loss detection/congestion), plus RFC 9220 (WebSockets over HTTP/3). Per-stack: official .NET QUIC/Kestrel/HTTP-3
  docs + GitHub samples; AtomVM + Gleam + WASM-BEAM docs and repos.
- **Distillation, not summary**: each source gets a close-read note capturing the architectural concern it bears
  on (handshake, ALPN, cert/trust, stream multiplexing, WS framing, backpressure, concurrency, failure modes) —
  recorded under `research/`, cross-referenced to the FR/SC it informs. Gaps trigger follow-up web/GitHub research.
- **LM discipline**: all research synthesis runs in Claude (web fetch / search), never an external LM API
  (constitution V).

## Outputs of Phase 0

All NEEDS-CLARIFICATION items from Technical Context are resolved into the decisions above. The corpus +
distillation stages are **COMPLETE** (106 close-read notes + `distillation-2026-06-27.md`, committed `10cdc452`):
the previously-open research items are now **closed** — MsQuic packaging + cross-platform support (Decision 2),
RFC 9220 maturity → genuine WS-over-QUIC with the bootstrap seam isolated (Decision 3), SPKI cert-pin recipe
(Decision 5), and AtomVM genuine-QUIC infeasibility → two Gleam deployment profiles (Decision 8). The only items
carried into `tasks.md` as gates are the cheap **residual verification probes** (`IsSupported`, msquic.dll
present, AtomVM `open_port` if Profile A) and the **Constitution IV-a REPL-link primitive gate** (default: no new
GLP primitive; STOP for owner approval if one proves necessary) — both escalate-don't-guess, at implementation start.
