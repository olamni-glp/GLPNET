# Implementation Plan: HTTP/3 (QUIC) + WebSocket Channel-Link Prototype

**Branch**: `036-http3-quic-ws-link` | **Date**: 2026-06-27 (reworked 2026-06-28) | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/036-http3-quic-ws-link/spec.md`

> **Rework 2026-06-28**: realigned to the spec's 2026-06-28 corpus/distillation clarifications — (1) the WS link
> is genuine RFC 6455 framing over a raw QUIC bidi stream (025 `FrameCodec`), with the RFC 9220 Extended-CONNECT
> bootstrap isolated behind a seam (no longer "WS over HTTP/3 via RFC 9220"); (2) the C# stack is **cross-platform**,
> not Windows-locked (gate on `IsSupported`); (3) the Gleam stack ships as two deployment profiles (A: AtomVM +
> native QUIC side-process; C: full BEAM + `quicer`/MsQuic), interchangeable at the channel-link contract.

## Summary

Deliver a **genuine** HTTP/3 (QUIC) channel + WebSocket link between independently-started CLI
processes on a LAN, used to run GLP between GLP REPL endpoints (one-way send/listen → full-duplex →
peer-to-peer duplex mesh). The capability ships as a `/GLP-Quick` skill backed by **one Python tool**
that hosts both roles (`--server` / `--client`).

**Technical approach** — split into a control plane and a data plane:

- **Control plane (Python, new `glp_quick/` tool)**: the operator-facing CLI surface, generation of the
  **shared self-signed certificate** (`cryptography`), out-of-band trust pinning, launch/supervision of the
  per-stack transport runtime, the GLP-REPL ↔ link bridge, and the LAN-IP conformance/concurrency demo.
  Python is **never** the QUIC endpoint — making it one would introduce a third transport stack and defeat
  the C#-vs-Gleam comparison that is a core goal (FR-009/FR-010).
- **Data plane (C#/.NET first, then Gleam)**: the real QUIC handshake (System.Net.Quic / MsQuic — GA in
  .NET 9, **cross-platform**, gated on `QuicListener`/`QuicConnection.IsSupported`) and a **genuine
  WebSocket link carried as RFC 6455 framing over one QUIC bidirectional stream** (one WS per stream — the
  exact carriage RFC 9220 standardizes), reusing spec 025's `FrameCodec` and established by a minimal
  CONNECT-style bootstrap on the stream. This is a first-class WS-over-QUIC design, not a fallback (QUIC/HTTP-3
  is de-facto dominant). The RFC 9220 **Extended-CONNECT-over-HTTP/3 bootstrap** — the only piece .NET has not
  shipped, needed only for third-party/browser interop — is isolated behind a handshake seam for later
  (FR-002). The C# stack is built as a **new transport leaf reusing spec 025's `ILinkTransport`/`ILinkEndpoint`
  seam, reliability sublayer, and ground-relay wire discipline** (FR-018) — not re-derived. The Gleam stack is
  a subsequent staged reimplementation of the identical channel-link contract, shipped as **two deployment
  profiles** (A: Gleam/AtomVM logic + WebSocket link / native QUIC side-process; C: Gleam on full BEAM +
  `quicer`/MsQuic terminating genuine in-process QUIC) — interchangeable at the contract, not at QUIC termination.

The whole effort runs as **one durable, resumable marathon** (`mrun-15d7dd0ffbc2`) across six stages:
research-strategy → ~50+50 corpus → distillation → implementation plan → skeleton/mock → implement-and-demo.

## Technical Context

**Language/Version**:
- Python 3.14 — control-plane tool (`glp_quick`), cert generation, process supervision, demo driver.
- C#/.NET 9 — data-plane stack A (reference): System.Net.Quic / MsQuic (GA, cross-platform), Kestrel HTTP/3,
  RFC 6455 WebSocket framing over a `QuicStream` (spec 025 `FrameCodec`).
- Gleam (BEAM-family runtime, profile-dependent) — data-plane stack B (second implementation, staged), two
  deployment profiles: **A** Gleam/AtomVM logic + WebSocket link / native QUIC side-process (MAUI Blazor hybrids,
  smaller nodes); **C** Gleam on full BEAM + `quicer`/MsQuic for genuine in-process QUIC (workstations/servers).
- GLP — the payload; runs on the existing C# GLP REPL (`out/csharp/glp_repl`, mandated default) and/or Dart REPL.

**Primary Dependencies**:
- Python: `typer` (CLI, matches codeconv convention), `cryptography` (self-signed cert), stdlib `subprocess`/`asyncio`.
- C#: Kestrel + `Microsoft.AspNetCore` (HTTP/3), `System.Net.Quic` (raw `QuicStream` carrying RFC 6455 frames);
  reuses `csharp/glp_link` (spec 025 `FrameCodec`). On Linux: `libmsquic` 2.2+; macOS: `brew install libmsquic`.
- Gleam: Profile A — AtomVM build + Node WASM host + native QUIC side-process (length-prefixed local IPC); Profile C
  — full BEAM + `quicer` (NIF over MsQuic) + `gleam_otp` (Phase 3+).

**Storage**:
- Marathon run state — additive `marathon_*` rows in the **out-of-repo** machine catalog under the deploy home
  (`C:\Users\smbuser\AppData\Local\buildkit\deploy-home`), mirrored to a per-run Markdown file. **Not** the
  repo working-data PGLite cluster (constitution VI-b exemption).
- Research corpus + distillation notes — files under `specs/036-http3-quic-ws-link/research/`.
- Shared certificate material — generated to an operator-specified path, distributed out-of-band (no store-of-record).

**Testing**:
- `pytest` for the Python control-plane tool (`glp_quick/tests/`).
- xUnit for the C# QUIC+WS transport leaf (`csharp/glp_link.tests/`) — loopback unit tests + a LAN smoke harness.
- The LAN-IP conformance demo (the SC-001..SC-006 acceptance harness) driven by `glp-quick demo`.
- The existing GLP REPL suite (`test/run_all_tests.sh`) must stay green — this feature must not regress it.

**Target Platform**: **Cross-platform — NOT Windows-locked.** `System.Net.Quic` (GA in .NET 9) runs on Windows
11 / Server 2022+ (msquic.dll ships with the runtime), Linux (`libmsquic` 2.2+, OpenSSL 3+/1.1 from
packages.microsoft.com), and macOS (partial — `brew install libmsquic` + `DYLD_FALLBACK_LIBRARY_PATH`). Endpoints
gate on `QuicListener`/`QuicConnection.IsSupported` before claiming a real handshake. **This repo's demo hosts are
Windows 11 (confirmed)** so the Win11 floor applies to *our* demo, but the spec/design MUST NOT assume Windows.
Cross-host LAN (two+ hosts/VMs by IP or machine name); internet-capable in principle (LAN-over-IP is the
demonstrated target; no NAT-traversal demo).

**Project Type**: Multi-runtime CLI + transport prototype — one Python control-plane tool orchestrating
pluggable per-stack data-plane runtimes (C#/.NET, Gleam/AtomVM), with GLP REPL endpoints as the payload.

**Performance Goals**: Prototype — **correctness and genuineness over throughput**. The bar is a *real*
on-wire QUIC handshake (verifiable, not loopback/simulated), a live WebSocket link, and ≥3 concurrent isolated
client sessions. No latency/throughput SLO; concurrency designed to scale beyond 3.

**Constraints**:
- Real QUIC only — no loopback-only or simulated handshake (FR-001); every endpoint gates on
  `QuicListener`/`QuicConnection.IsSupported` and verifies msquic availability before claiming a handshake.
- Cross-platform design — no Windows-only assumption; the WS link is **genuine RFC 6455 framing over a raw QUIC
  bidi stream** (spec 025 `FrameCodec`), with the RFC 9220 Extended-CONNECT bootstrap isolated behind a seam (FR-002).
- Shared self-signed certificate as the **only** trust anchor — no domain name, no public CA, no hostname-bound
  cert (FR-003, SC-005); the Python tool generates it; manual out-of-band trust pinning.
- Full-duplex link + peer-to-peer duplex mesh of ≥3 REPLs (FR-008a/FR-008b).
- One Python tool hosts both roles behind `/GLP-Quick` (FR-007).
- Durable, resumable marathon — interrupt resumes from objective persisted state (FR-013, SC-008).
- All failure modes (connection / cert / ALPN-version / addressing) reported clearly — no silent hang, no half-open link (FR-019).
- Reuse spec 025 prior art rather than re-deriving the link layer (FR-018).
- LM-in-the-loop research runs in Claude only — no external-LM API or proxy library on any LM path (constitution V).

**Scale/Scope**: Prototype. Two transport stacks built **sequentially** (C# reference complete first, then Gleam
staged in two deployment profiles A/C — interchangeable at the channel-link contract, not at QUIC termination).
≥3 concurrent clients, designed to scale. Research corpus complete: 106 close-read source notes (58 C# + 48
Gleam/AtomVM) covering RFC 9114 (HTTP/3), RFC 9000/9001/9002 (QUIC), and RFC 9220/8441/7301/6455, distilled (not summarised).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution v1.1.0. This feature is predominantly **transport infrastructure** (Python + C# + Gleam); the GLP
language core is **not modified** — GLP REPL endpoints run existing GLP as payload over the link.

| # | Principle | MUST | Verdict |
|---|-----------|------|---------|
| I | Spec-First | No code without an identified, quoted, consistent spec | **PASS** — plan is spec-derived; skeleton/mock-first (FR-017) precedes behaviour. |
| II | Bug-Protocol / No-Workarounds | STOP & report on bugs; no robustness-masking | **PASS** — Gleam genuine-QUIC feasibility reported honestly (FR-010 edge case), not faked. |
| III | SRSW invariant | No SRSW escape hatch; ≤1 occurrence/var/clause | **PASS (N/A)** — no GLP clauses authored here; the forbidden SRSW-escape token is absent from all artifacts. |
| IV-a | Language Authority | No new GLP primitives without owner approval | **PASS, gated** — design reuses spec 025's already-approved link primitives (`link_send`/`link_recv`/…). **If** the REPL-mesh requires any new GLP primitive, STOP and obtain owner approval before implementing (tracked as a hard gate in tasks). |
| IV-b | Preserve Working Internals | No removal of load-bearing internals | **PASS** — additive only; `_ClauseVar`/`_TentativeStruct`/fallbacks untouched. |
| V | Claude-Only LM / No External API | No external-LM API or proxy library on any LM path | **PASS** — research via Claude/web fetch; no external-LM API key or proxy-library token appears on any LM path in these artifacts. |
| VI-a | Additive, idempotent, single-head migrations | Migrations additive; single linear head | **PASS (N/A)** — no new Alembic migration planned; single head `0010` unchanged. |
| VI-b | Single OS-lock-guarded PGLite cluster | One repo working-data cluster | **PASS** — marathon state in the out-of-repo isolated store (explicit v1.1.0 exemption); no second repo cluster. |
| VII | Test-Gated, Commit-Scoped Shipping | Baseline green; commit only own files; GitFlow | **PASS** — baseline-before/after; ship via `feature → develop → release/* → main`. |
| VIII | Single Source of Truth & Traceability | One authoritative spec per subsystem; roadmap→pipeline→tasks | **PASS** — spec 025 stays authoritative for the link layer; 036 references it (FR-018), does not duplicate. |

**No violations** → Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/036-http3-quic-ws-link/
├── plan.md              # This file
├── research.md          # Phase 0: decisions + corpus methodology (corpus itself produced in the marathon)
├── data-model.md        # Phase 1: entities, wire/message contract, handshake, link state machine
├── quickstart.md        # Phase 1: the LAN-IP demo runbook (server on A, ≥3 clients, mesh)
├── contracts/           # Phase 1: CLI contract, wire/message contract, stack-adapter contract
├── research/            # Phase 0/marathon: ~50+50 corpus + distillation notes (FR-014/015)
├── checklists/          # requirements.md (already present)
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
glp_quick/                                 # NEW — single Python control-plane tool (FR-007)
├── pyproject.toml                         # [project.scripts] glp-quick = "glp_quick.cli:app" (Typer)
├── src/glp_quick/
│   ├── cli.py                             # glp-quick --server|--client, --stack csharp|gleam, --cert, --addr
│   ├── server.py                          # server role: launch+supervise stack runtime, accept N clients
│   ├── client.py                          # client role: connect to server by IP/name, trust shared cert
│   ├── cert.py                            # generate shared self-signed cert (cryptography); pin/verify trust
│   ├── stacks/
│   │   ├── base.py                        # StackAdapter ABC — the uniform per-stack contract
│   │   ├── csharp.py                      # launch/manage the C# QUIC+WS endpoint (reference)
│   │   └── gleam.py                       # Phase 3+: AtomVM/WASM via Node host (second stack)
│   ├── repl_link.py                       # bridge a GLP REPL endpoint to the link (send/listen/mesh)
│   └── demo.py                            # LAN-IP conformance + ≥3-client concurrency + mesh driver
└── tests/                                 # pytest: cli, cert, stack-adapter, demo (loopback-gated)

csharp/glp_link/transports/                # EXTEND spec 025 (FR-018) — QUIC+WS transport leaf
├── QuicTransport.cs                       # ILinkTransport: real QUIC via System.Net.Quic (IsSupported-gated)
├── QuicEndpoint.cs                        # ILinkEndpoint over a QUIC connection / bidi stream
├── WebSocketOverQuic.cs                   # genuine RFC 6455 framing over one QuicStream (spec 025 FrameCodec)
└── ConnectBootstrap.cs                    # minimal CONNECT-style link bootstrap; RFC 9220 Extended-CONNECT seam (later)
csharp/glp_link.tests/                     # xUnit: loopback transport tests + LAN smoke

gleam_quic/                                # NEW (Phase 3+) — second stack, two deployment profiles
├── gleam.toml
├── src/                                   # quic_link.gleam (channel-link contract), websocket.gleam, profile dispatch
├── profile_a/                             # AtomVM logic + WebSocket link / native QUIC side-process (local IPC)
└── profile_c/                             # full BEAM + `quicer`/MsQuic — genuine in-process QUIC

.claude/skills/glp-quick/SKILL.md          # NEW — /GLP-Quick skill → invokes the glp_quick CLI
```

**Structure Decision**: A new top-level `glp_quick/` Python package follows the established
single-package-per-tool convention (`pyproject.toml` + `[project.scripts]` Typer entry + `src/` + `tests/`,
as `codeconv/` does). The C# data plane **extends** the existing `csharp/glp_link/transports/` directory from
spec 025 rather than starting a new project — the QUIC+WS endpoint is one more `ILinkTransport`/`ILinkEndpoint`
leaf alongside `LoopbackTransport`/`TcpTransport`, inheriting the reliability sublayer and ground-relay
discipline for free (FR-018); the WebSocket is genuine RFC 6455 framing over a raw `QuicStream` (not RFC 9220
Extended-CONNECT, which .NET has not shipped — isolated behind a bootstrap seam for later browser interop). The
Gleam stack is a self-contained greenfield `gleam_quic/` added only after the C# reference passes the full
real-QUIC LAN demo, and ships as two deployment profiles (A: AtomVM logic + native QUIC side-process; C: full
BEAM + `quicer`/MsQuic in-process) interchangeable at the channel-link contract — genuine QUIC on bare AtomVM/WASM
is infeasible (research §F2), so QUIC termination is delegated honestly per profile (constitution II).

## Complexity Tracking

> No Constitution Check violations — this section is intentionally empty.
