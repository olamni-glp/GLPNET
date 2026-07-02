# Distillation — Feature 036 (HTTP/3 QUIC + WebSocket channel-link prototype)

**Date**: 2026-06-27 | **Stage**: marathon research → corpus → **distill** (this file)
**Inputs**: the 9 corpus cluster files in this directory (106 close-read source notes: 58 C#/.NET, 48 Gleam/AtomVM).
**Method**: web research synthesized in Claude only (constitution V — no external LM API). This note is the *distillation* mandated by FR-014/FR-015/SC-007: it cross-references the corpus to the feature requirements and updates the gating decisions in `research.md`.

> This file resolves the two highest-risk open research items the codex independent review flagged
> (`codex-independent-review-2026-06-27.md`): RFC 9220 WS-over-HTTP/3 maturity in .NET, and genuine
> QUIC on AtomVM. Both resolved with evidence below.

> **CORRECTION (2026-06-28) — read this first.** The original 2026-06-27 framing below contained two
> synthesis errors (caught by Gabi): (1) it mislabeled WS-over-QUIC as a defeatist "fallback" and over-
> weighted a browser-centric "no production RFC 9220" claim — **wrong**. QUIC/HTTP-3 is de-facto dominant
> (~21–39% of web traffic; [Cloudflare](https://blog.cloudflare.com/cloudflare-view-http3-usage/)), and
> **genuine WS-over-QUIC is a first-class design**: RFC 6455 framing over a QUIC bidi stream (the carriage
> RFC 9220 standardizes). Only the RFC 9220 Extended-CONNECT *bootstrap over HTTP/3* is unshipped in .NET,
> and it matters solely for third-party/browser interop — irrelevant to this C#↔C# LAN prototype. (2) it
> framed the platform floor as Windows-only — **wrong**: `System.Net.Quic` (GA in .NET 9) is **cross-platform**
> (Windows 11/Server 2022+, Linux via libmsquic, macOS partial via Homebrew —
> [MS Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview)). Where the
> text below says "fallback"/"not RFC 9220"/"Windows", read the corrected statements in §2–§4 as authoritative.

---

## 1. Corpus inventory

| File | Stack | Notes | Theme |
|------|-------|------:|-------|
| `corpus-csharp-01-quic-http3-rfcs.md` | C# | 12 | RFC 9000/9001/9002/9114/9220 + 8441/7301/6455 foundations |
| `corpus-csharp-02-dotnet-quic-msquic.md` | C# | 11 | `System.Net.Quic` + MsQuic packaging/availability |
| `corpus-csharp-03-kestrel-http3-server.md` | C# | 12 | Kestrel HTTP/3 server config |
| `corpus-csharp-04-websocket-http3-maturity.md` | C# | 12 | RFC 9220 maturity in .NET + fallback |
| `corpus-csharp-05-cert-pinning-tls.md` | C# | 11 | shared cert + fingerprint pinning + TLS 1.3 |
| `corpus-gleam-01-atomvm-overview.md` | Gleam | 12 | AtomVM platform + networking |
| `corpus-gleam-02-atomvm-quic-tls-feasibility.md` | Gleam | 12 | genuine QUIC + TLS feasibility (highest risk) |
| `corpus-gleam-03-gleam-beam-quic.md` | Gleam | 12 | Gleam + BEAM/Erlang QUIC ecosystem |
| `corpus-gleam-04-wasm-beam-host.md` | Gleam | 12 | WASM host networking + side-process |
| **Total** | | **106** | |

Mandatory RFC coverage (research.md methodology) is met: 9000, 9001, 9002, 9114, 9220 all close-read, plus 8441 (the HTTP/2 predecessor 9220 adapts), 7301 (ALPN), 6455 (WebSocket base framing).

---

## 2. The two headline findings

### F1 — WS-over-QUIC is first-class and feasible; only the RFC 9220 *bootstrap* is unshipped in .NET (corrected 2026-06-28)
- **WS-over-QUIC is a deployed, first-class design**, not a contingency: a WebSocket is carried as RFC 6455 framing on a QUIC bidirectional stream (one WS per stream — the exact carriage RFC 9220 standardizes). QUIC/HTTP-3 is de-facto dominant (~21–39% of web traffic). This is what the prototype implements, genuinely.
- The **only** gap: .NET hasn't shipped the RFC 9220 *Extended-CONNECT-over-HTTP/3 bootstrap* — `ClientWebSocket`'s shipped bootstrap ceiling is HTTP/2 (RFC 8441), and Kestrel doesn't accept CONNECT (dotnet/aspnetcore #32004). WebTransport-over-HTTP/3 exists but is experimental and is a different (browser-native) API (corpus C#-03, C#-04).
- That bootstrap gap matters **only for interop with third-party/browser WS-over-H3 clients** — which is out of scope for a C#↔C# LAN prototype that owns both ends and establishes the WS link with its own minimal CONNECT-style bootstrap on the QUIC stream.
- **Consequence for FR-002**: fully deliverable — genuine WS frames over a genuine QUIC stream (reusing spec-025 `FrameCodec`), with the RFC 9220 Extended-CONNECT bootstrap isolated behind a seam for later third-party interop. The QUIC connection (FR-001) is genuine throughout.

### F2 — Genuine QUIC on Gleam/AtomVM is NOT feasible on any target (HIGH confidence)
- AtomVM has a UDP datagram primitive on `generic_unix` but only a minimal, **client-only, crash-in-active-mode** Mbed-TLS `ssl` with no RFC-9001 secret/transport-param export — it cannot drive a QUIC/TLS-1.3 handshake (corpus Gleam-01, Gleam-02).
- The standard BEAM QUIC answer, `quicer` (a C NIF wrapping MsQuic), **cannot load on AtomVM** — AtomVM forbids runtime-loaded NIFs and is an OTP subset (corpus Gleam-02, Gleam-03).
- The WASM/Node "host" target **cannot originate QUIC at all**: browser/Emscripten has no raw UDP, and AtomVM's emscripten platform ships no `gen_tcp`/`gen_udp` driver (corpus Gleam-04).
- **Consequence for FR-009/FR-010**: "two interchangeable genuine-QUIC stacks (C#/.NET and Gleam/AtomVM)" is not achievable as written. Honest options: (a) Gleam/AtomVM logic *drives* a native genuine-QUIC side-process (real_quic attributed to that side-process); or (b) report `real_quic=false` for the Gleam stack and defer it per FR-010's edge case + constitution II.

---

## 3. Requirement-by-requirement disposition (post-corpus)

| Req | Pre-corpus status | Post-corpus disposition | Evidence |
|-----|-------------------|-------------------------|----------|
| FR-001 (real QUIC connection) | planned | **FEASIBLE (C#), cross-platform** — `System.Net.Quic` GA in .NET 9 on Windows (Win11/Server2022+), Linux (libmsquic), and macOS (partial, Homebrew); gate on `IsSupported` | C#-02, C#-01 |
| FR-002 (WebSocket over the connection) | planned (RFC 9220) | **FEASIBLE, first-class** — genuine WS-over-QUIC = RFC 6455 framing over one bidi `QuicStream` + spec-025 `FrameCodec` (the carriage RFC 9220 standardizes); RFC 9220 Extended-CONNECT bootstrap isolated behind a seam (only needed for browser interop, out of MVP scope) | C#-04, C#-03, C#-01 |
| FR-003 / SC-005 (shared self-signed cert is the only trust anchor) | planned | **FEASIBLE (C#)** — SPKI pin via `RemoteCertificateValidationCallback` or `CustomRootTrust`; Python `cryptography` emits PFX+PEM | C#-05 |
| FR-009 / FR-010 (two interchangeable QUIC stacks) | partial | **RESOLVED via two Gleam deployment profiles** (Gabi 2026-06-28) — Profile A: Gleam/AtomVM + WS link / native QUIC side-process (MAUI Blazor hybrids, small nodes); Profile C: Gleam on full BEAM + `quicer`/MsQuic genuine in-process QUIC (workstations/servers); interchangeable at the channel-link contract | Gleam-02/03/04 |
| FR-018 (reuse spec-025 link seam) | planned | **FEASIBLE** — spec-025 backpressure rides on QUIC stream flow control; QUIC+WS leaf slots beside Loopback/Tcp transports | C#-01, C#-02, C#-04 |
| SC-007 (corpus + distillation complete) | absent | **SATISFIED by this stage** — 106 notes + this distillation, committed | this file |

---

## 4. Decision updates to `research.md`

- **Decision 2 (real QUIC in C#) — CONFIRMED; cross-platform.** `System.Net.Quic` is GA in .NET 9 and **cross-platform** (NOT Windows-locked): Windows 11/Server 2022+ (msquic.dll ships with the runtime), Linux (`libmsquic` 2.2+/OpenSSL from packages.microsoft.com), macOS (partial — `brew install libmsquic` + `DYLD_FALLBACK_LIBRARY_PATH`) — [MS Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview). Gate every endpoint on `QuicConnection.IsSupported`/`QuicListener.IsSupported` (silent failure otherwise); verify msquic availability (historical packaging regression dotnet/runtime #81447); set mandatory ALPN + `DefaultStreamErrorCode`/`DefaultCloseErrorCode`. This repo's demo hosts are Windows 11 (confirmed), but the design is not Windows-bound.
- **Decision 3 (WebSocket over QUIC) — first-class, not a fallback.** Implement **genuine WS-over-QUIC**: RFC 6455 framing (opcodes text 0x1/binary 0x2/close 0x8/ping 0x9/pong 0xA, FIN/continuation, varint length; masking N/A on a TLS-encrypted intermediary-free QUIC stream) carried over a single bidirectional `QuicStream`, reusing spec-025 `FrameCodec`, established via a minimal CONNECT-style bootstrap on the stream. This **is** the carriage RFC 9220 standardizes; QUIC's de-facto dominance makes it a first-class design, not a consolation. The RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap (unshipped in .NET) is needed only for third-party/browser interop and is isolated behind a handshake seam so it slots in later. WebTransport-over-HTTP/3 noted as the future browser-native client path. Honest reporting per constitution II: genuine WS over genuine QUIC.
- **Decision 8 (Gleam/AtomVM staged second stack) — SHARPENED.** Genuine QUIC on AtomVM is infeasible (F2). Re-model the Gleam stack as **Gleam/AtomVM control logic + a native genuine-QUIC side-process** over length-prefixed local IPC (Erlang `open_port` on native AtomVM; WebSocket-proxy shape on WASM), with `real_quic` truthfully attributed to the side-process — OR defer the stack and report `real_quic=false`. Decision pending Gabi (see §5).
- **Decision 5 (cert pinning) — CONFIRMED & DETAILED.** Use the SPKI (SubjectPublicKeyInfo) sha256 pin (survives re-issue with same key) over the whole-cert thumbprint; never `return true`; waive only the no-CA chain error + hostname mismatch (trust is the pin, not the name). Python `cryptography`: subject==issuer, `BasicConstraints(ca=False)`, `KeyUsage(digital_signature,key_encipherment)`, `EKU[serverAuth,clientAuth]`, EC P-256 or RSA-2048; export PFX (holder) + PEM (distribution).

---

## 5. Decisions — RESOLVED by Gabi 2026-06-28 (encoded in spec.md Clarifications 2026-06-28)

1. **FR-009/FR-010 interchangeability (F2) → BOTH (a) and (c) as two deployment profiles.** **Profile A** — Gleam/AtomVM logic + WebSocket link / native QUIC side-process, for MAUI Blazor hybrids and smaller freestanding nodes. **Profile C** — Gleam on full BEAM + `quicer`/MsQuic terminating genuine in-process QUIC, for larger workstations and servers. Interchangeable at the channel-link contract; "AtomVM" relaxed to "a BEAM-family runtime, profile-dependent." Side-process QUIC reported honestly (constitution II).
2. **FR-002 (F1) → ACCEPTED: genuine WS-over-QUIC.** "QUIC allows WS; the QUIC standard is de-facto, not de-jure." The de-facto WS-over-QUIC carriage (RFC 6455 over a `QuicStream`) satisfies "layer a WebSocket link"; RFC 9220 Extended-CONNECT bootstrap is a future interop upgrade, not a precondition.
3. **Platform floor → demo hosts are Windows 11 (confirmed YES)**, but the C# stack is **cross-platform** (Windows/Linux/macOS) and the design MUST NOT assume Windows.

These three feed directly into the next steps in the agreed sequence: **revisit /bk-specify or /bk-clarify → /bk-plan → /bk-tasks → /bk-analyze** before any /bk-implement.

---

## 6. Residual verification probes (cheap, do at implementation start)

- Confirm `QuicListener.IsSupported == true` on the actual demo host (one-line C# probe).
- Confirm msquic.dll present in the pinned .NET 9 runtime.
- Confirm AtomVM-WASM has no socket driver / whether `open_port` spawn works (build-time probe) — only if option (a) for the Gleam stack is chosen.
- Confirm WebTransport `serverCertificateHashes` constraints — only if a browser client path is later added (out of MVP scope).

---

## 7. Net effect on `tasks.md`

- Tasks assuming RFC 9220 Extended CONNECT (C# WS-over-H3 endpoint) must be re-pointed to the WS-framing-over-`QuicStream` leaf + negotiation seam.
- Gleam-stack tasks must be re-scoped to whichever §5.1 option Gabi picks (side-process vs deferral), and the "interchangeable" acceptance criteria reworded to the channel-link contract level.
- Add the residual verification probes (§6) as the first implementation tasks (escalate-don't-guess).
- The cert tasks gain the concrete SPKI-pin recipe (§4 Decision 5) — no design ambiguity remains there.
