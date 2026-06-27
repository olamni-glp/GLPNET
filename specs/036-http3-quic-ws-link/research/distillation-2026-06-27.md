# Distillation — Feature 036 (HTTP/3 QUIC + WebSocket channel-link prototype)

**Date**: 2026-06-27 | **Stage**: marathon research → corpus → **distill** (this file)
**Inputs**: the 9 corpus cluster files in this directory (106 close-read source notes: 58 C#/.NET, 48 Gleam/AtomVM).
**Method**: web research synthesized in Claude only (constitution V — no external LM API). This note is the *distillation* mandated by FR-014/FR-015/SC-007: it cross-references the corpus to the feature requirements and updates the gating decisions in `research.md`.

> This file resolves the two highest-risk open research items the codex independent review flagged
> (`codex-independent-review-2026-06-27.md`): RFC 9220 WS-over-HTTP/3 maturity in .NET, and genuine
> QUIC on AtomVM. Both resolved with evidence below.

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

### F1 — RFC 9220 (WebSocket-over-HTTP/3) is NOT usable in .NET 9, client or server (HIGH confidence)
- `ClientWebSocket`'s shipped ceiling is HTTP/2 Extended CONNECT (RFC 8441, since .NET 7); no HTTP/3 path exists and **no tracking issue exists** in dotnet/runtime (corpus C#-04).
- Kestrel has never supported CONNECT in any protocol version (dotnet/aspnetcore #32004); its only non-HTTP app protocol over HTTP/3 is **WebTransport, which is experimental** (preview flags), and it is *not* RFC 9220 (corpus C#-03, C#-04).
- Industry-wide there is no production RFC 9220 server/client as of early 2026 (corpus C#-01, C#-04).
- **Consequence for FR-002**: the literal reading "a WebSocket bootstrapped over HTTP/3 via Extended CONNECT" cannot be delivered on .NET today.

### F2 — Genuine QUIC on Gleam/AtomVM is NOT feasible on any target (HIGH confidence)
- AtomVM has a UDP datagram primitive on `generic_unix` but only a minimal, **client-only, crash-in-active-mode** Mbed-TLS `ssl` with no RFC-9001 secret/transport-param export — it cannot drive a QUIC/TLS-1.3 handshake (corpus Gleam-01, Gleam-02).
- The standard BEAM QUIC answer, `quicer` (a C NIF wrapping MsQuic), **cannot load on AtomVM** — AtomVM forbids runtime-loaded NIFs and is an OTP subset (corpus Gleam-02, Gleam-03).
- The WASM/Node "host" target **cannot originate QUIC at all**: browser/Emscripten has no raw UDP, and AtomVM's emscripten platform ships no `gen_tcp`/`gen_udp` driver (corpus Gleam-04).
- **Consequence for FR-009/FR-010**: "two interchangeable genuine-QUIC stacks (C#/.NET and Gleam/AtomVM)" is not achievable as written. Honest options: (a) Gleam/AtomVM logic *drives* a native genuine-QUIC side-process (real_quic attributed to that side-process); or (b) report `real_quic=false` for the Gleam stack and defer it per FR-010's edge case + constitution II.

---

## 3. Requirement-by-requirement disposition (post-corpus)

| Req | Pre-corpus status | Post-corpus disposition | Evidence |
|-----|-------------------|-------------------------|----------|
| FR-001 (real QUIC connection) | planned | **FEASIBLE (C#)** — `System.Net.Quic` GA in .NET 9, msquic ships on Windows; gate on `IsSupported`, Win11/Server2022+ for Schannel TLS 1.3 | C#-02, C#-01 |
| FR-002 (WebSocket over the connection) | planned (RFC 9220) | **FEASIBLE only via fallback** — RFC 9220 unavailable; deliver RFC 6455 framing over one bidi `QuicStream` + spec-025 `FrameCodec`, labeled "not RFC 9220" | C#-04, C#-03, C#-01 |
| FR-003 / SC-005 (shared self-signed cert is the only trust anchor) | planned | **FEASIBLE (C#)** — SPKI pin via `RemoteCertificateValidationCallback` or `CustomRootTrust`; Python `cryptography` emits PFX+PEM | C#-05 |
| FR-009 / FR-010 (two interchangeable QUIC stacks) | partial | **BLOCKED as written** — Gleam/AtomVM cannot terminate real QUIC; needs side-process or honest deferral (clarification required) | Gleam-02/03/04 |
| FR-018 (reuse spec-025 link seam) | planned | **FEASIBLE** — spec-025 backpressure rides on QUIC stream flow control; QUIC+WS leaf slots beside Loopback/Tcp transports | C#-01, C#-02, C#-04 |
| SC-007 (corpus + distillation complete) | absent | **SATISFIED by this stage** — 106 notes + this distillation, committed | this file |

---

## 4. Decision updates to `research.md`

- **Decision 2 (real QUIC in C#) — CONFIRMED, with caveats.** Add: gate every endpoint on `QuicConnection.IsSupported`/`QuicListener.IsSupported` (silent failure otherwise); require Windows 11 / Server 2022+ (Schannel TLS 1.3); verify msquic.dll is present in the target runtime (historical packaging regression dotnet/runtime #81447); set mandatory ALPN + `DefaultStreamErrorCode`/`DefaultCloseErrorCode`.
- **Decision 3 (WebSocket over HTTP/3 via RFC 9220) — REVISED.** RFC 9220 is unavailable in .NET 9. **Adopt the documented fallback as the PRIMARY path**: RFC 6455 framing (opcodes text 0x1/binary 0x2/close 0x8/ping 0x9/pong 0xA, FIN/continuation, varint length; masking N/A on a TLS-encrypted intermediary-free QUIC stream) carried over a single bidirectional `QuicStream`, reusing spec-025 `FrameCodec`. Isolate the handshake/negotiation seam so a genuine RFC 9220 path can slot in when .NET ships it. Label the transport explicitly "WS-framing-over-QUIC (not RFC 9220)" — honest reporting per constitution II.
- **Decision 8 (Gleam/AtomVM staged second stack) — SHARPENED.** Genuine QUIC on AtomVM is infeasible (F2). Re-model the Gleam stack as **Gleam/AtomVM control logic + a native genuine-QUIC side-process** over length-prefixed local IPC (Erlang `open_port` on native AtomVM; WebSocket-proxy shape on WASM), with `real_quic` truthfully attributed to the side-process — OR defer the stack and report `real_quic=false`. Decision pending Gabi (see §5).
- **Decision 5 (cert pinning) — CONFIRMED & DETAILED.** Use the SPKI (SubjectPublicKeyInfo) sha256 pin (survives re-issue with same key) over the whole-cert thumbprint; never `return true`; waive only the no-CA chain error + hostname mismatch (trust is the pin, not the name). Python `cryptography`: subject==issuer, `BasicConstraints(ca=False)`, `KeyUsage(digital_signature,key_encipherment)`, `EKU[serverAuth,clientAuth]`, EC P-256 or RSA-2048; export PFX (holder) + PEM (distribution).

---

## 5. Items requiring Gabi's decision before /bk-plan & /bk-tasks rework

1. **FR-009/FR-010 interchangeability (F2).** Choose: (a) Gleam stack = native QUIC side-process driven by Gleam/AtomVM (interchangeable at the *channel-link contract*, not at the QUIC-termination layer); (b) defer the Gleam stack, ship C# only for the MVP and report the Gleam feasibility limitation honestly; (c) relax "AtomVM" to "full BEAM" for the Gleam stack so `quicer`/MsQuic is allowed (contradicts the spec's AtomVM mandate — needs an explicit spec change).
2. **FR-002 wording (F1).** Confirm the spec accepts "WS-framing-over-QUIC (not RFC 9220)" as satisfying "layer a WebSocket link," with RFC 9220 noted as a future upgrade. If the spec insists on literal RFC 9220, the feature cannot be implemented on .NET today.
3. **Platform floor.** Confirm the demo hosts are Windows 11 / Server 2022+ (required for C# QUIC TLS 1.3 via Schannel).

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
