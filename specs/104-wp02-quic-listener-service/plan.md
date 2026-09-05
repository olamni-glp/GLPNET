<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan — 104 WP-02 QUIC listener service

## Design spine

Three additions, one registration. **Nothing existing is rewritten** — the seam was built for this.

```
YnetListenerService          NEW  csharp/ynet_transport/Listener/YnetListenerService.cs
  <- ListenerConfig          NEW  csharp/ynet_transport/Listener/ListenerConfig.cs
  -> QuicProviderChain            (existing, unmodified)
       tier 0 IrohSidecarProvider NEW  csharp/ynet_transport/Link/IrohSidecarProvider.cs
       tier 1 MsQuicProvider      (existing, unmodified — retained per Q-olg15-03)
       tier 2 Ngtcp2Provider      (existing, unmodified)
```

### Why a sidecar adapter and not a P/Invoke binding

`Q-olg15-03` requires iroh at L0 *and* accepts the layer-gate constraint that L0 must not be
distro-dependent. A **process boundary** is the only shape that satisfies both: the managed adapter
(pure C#, no native dependency, compiles everywhere) is at L0; the Rust binary is a peer process
reached over a local endpoint. Moving the boundary from *link-time* to *process-time* is what
dissolves the contradiction — it is not a compromise between the two options, it is the resolution.

### The one rule this feature exists to enforce

> **A bind is not a link, and a fallback is not a silence.**

Both halves are failure modes this fleet has already paid for: B4/B5 (a process that binds and
receives nothing) and the false-green class (a degradation reported as health). So:

- `ListenerReport` carries `Outcome ∈ { Ok, BoundUnreachable, BindFailed, Refused }` — there is no
  boolean, because a boolean is what lets `BOUND_UNREACHABLE` collapse into `OK`.
- `ListenerReport.SkippedTiers` is populated whenever the winner is not tier 0, and `Describe()`
  prints it. A silent fallback is a defect (FR-008).

### Reachability

`ProbeReachabilityAsync` dials the bound endpoint through the same chain and requires a completed
handshake **plus** a bidirectional byte exchange, mirroring the 5/5 measurement in B2. A handshake
alone is not accepted: a handshake that completes and then carries no bytes is precisely what a
half-open path looks like.

### Constitution / discipline check

| gate | how this plan satisfies it |
|---|---|
| Spec-first | Every file below traces to an FR. No file exists without one. |
| No workarounds | B6 (absent Rust) is declared Out of Scope and reported, not stubbed into a fake "available". |
| Preserve working code | `QuicProviderChain`, `MsQuicProvider`, `Ngtcp2Provider`, `QuicWireChannel` are **not modified**. |
| §1.14 language authority | No GLP language surface is touched. This is C# host code only. |
| FR-012 | No election, no vote, no leader. Grep-checked in the review. |

## Phases

| phase | content |
|---|---|
| P1 | `ListenerConfig` + `ListenerReport` + outcome enum (FR-001, FR-010) |
| P2 | `IrohSidecarProvider` at tier 0 with a measuring `Probe()` (FR-004..FR-006) |
| P3 | `YnetListenerService`: bind through the chain, record the actual provider, report skipped tiers, refuse loudly (FR-002, FR-003, FR-007, FR-008, FR-011) |
| P4 | `ProbeReachabilityAsync` + `BoundUnreachable` (FR-009, FR-010) |
| P5 | Tests for SC-001..SC-005, then **SC-006 negative controls** — break each mechanism, record the red, restore |
