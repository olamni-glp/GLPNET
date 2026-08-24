# Implementation Plan: YNET `ynet-transport` — consolidated QUIC leaf + browser/edge tier + Veilid-class overlay

**Branch**: `051-ynet-transport` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/051-ynet-transport/spec.md`

## Summary

Deliver the **transport + overlay mechanism tier** of YNET: a single consolidated YNET-owned QUIC
leaf that **supersedes GLPNET `QuicTransport`** (harvesting the shipped 050 native-QUIC link), with
Ed25519-key-as-TLS-identity (absorbing iroh/`noq`), NAT hole-punch (ICE/DCUtR), an embedded
S-Kademlia DHT, hybrid relay-forward (circuit-relay-v2 for mesh / Tor-cell for internet+critical
flows), metadata-protecting sealed routes with Veilid-`SafetySelection` selectable anonymity,
trusted-gate clearnet exit (extending olamnit `EgressService`), and a **distinct browser/edge
WebRTC+WebTransport tier**. The tier is consumed by qhstate 056 via a first-class `ICapability`; it
owns the *mechanism*, never the service embed or the admission/leaf *policy* (FR-024).

**Technical approach** (from research): the reuse-vs-build map is fixed by D1–D6 + cycle-2 and the
`/bk-clarify` Session 2026-07-13 mechanism decisions. Native leaf = **C#/.NET 10 MsQuic**, harvesting
`csharp/glp_link`; services/workstation tier may add a **Gleam/BEAM** impl; the browser tier is a
**separate JS/WASM** implementation (MsQuic cannot target the browser — no single cross-tier binary,
cycle-2 §3). Crypto/DSDV/egress reuse the **olamnit** substrate (hardened H2/H3). Standard sealed-route
node selection consumes the new **`057-yngenios-pocw-coin`** stake signal, degrading to a Loopix-style
semi-trusted fallback.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`) — native consolidated QUIC leaf (harvests
`csharp/glp_link`, MsQuic via `System.Net.Quic`). Gleam (BEAM target) for the services/workstation
tier (`gleam_quic` extension). JavaScript/WASM for the browser/edge tier (WebRTC datachannel +
WebTransport). Python 3.12 for cert/record tooling (`glp_quick`, already shipped). GLP (`.glp`) only
for demonstration/test programs — **no new GLP kernel/primitive** (Constitution IV-a; propose-first).
**Primary Dependencies**: `System.Net.Quic` (MsQuic); `csharp/glp_link` (harvested → YNET link);
`BouncyCastle.Cryptography` 2.4.0 (DEC-CRYPTO-1 — Ed25519 node identity, pure-managed, P-256 fallback);
olamnit **reused** (Ed25519 amulet crypto-envelope / AES-256-GCM sealed link, DSDV
`DistanceVectorRouter` + `MeshRelayRoute`, default-deny `EgressService`) — **hardened H2/H3**;
**NEW**: an ICE/DCUtR hole-punch capability, an embedded **S-Kademlia** DHT, a hybrid relay-forward
(circuit-relay-v2 + Tor-cell) layer, sealed-route/garlic layering; **cross-feature dep on
`057-yngenios-pocw-coin`** (stake signal, with Loopix fallback). Browser tier: browser-native WebRTC +
WebTransport (no native lib).
**Storage**: self-certified DHT records + peer-reachability + relay-admission cache in the repo PGLite
working-data cluster (`pgdb/`, Constitution VI-b) — **additive, idempotent, single-head** migration
(Constitution VI-a). No wire payloads persisted (the link carries envelopes; it does not store them).
**Testing**: `dotnet test` (`glp_link.tests` pattern) for native; `gleam test` for the BEAM tier;
JS test runner for the browser tier; Python `pytest` for record/cert tooling; GLP test program
positive-load check (`test/run_all_tests.sh` §B). Contract + integration + unit tiers.
**Target Platform**: Windows 11 floor (036) for native/BEAM demo hosts; modern browsers (4-engine
WebRTC/WebTransport) for the browser tier. The native leaf gates on `QuicTransport.IsSupported` and
**refuses — never downgrades** — where QUIC is unavailable (050 precedent).
**Project Type**: multi-runtime transport/overlay library integration (C# native + Gleam/BEAM +
browser JS/WASM) consumed as an `ICapability`, plus GLP demonstration programs.
**Performance Goals**: inherit 050's LAN wire targets (median RTT < 50 ms, ≥1000 msgs zero-loss) for
the direct link; hole-punch success-rate and per-hop sealed-route latency targets are set in
research.md (SC-002 resolution) against the cycle-2 path-selection-latency insight.
**Constraints**: TRUE QUIC only for the native leaf (no TCP/loopback fallback); per-node keying
authoritative (supersede GLPNET shared cert, migration explicit FR-020); sealed routes fail-closed
(never silently downgrade FR-011); Claude-only LM (Constitution V); SRSW-clean GLP programs
(Constitution III); commit-scoped shipping (Constitution VII).
**Scale/Scope**: the full spec is **saga**-sized (35 pts) — 8 user stories, 25 FRs, 11 SCs across
three runtimes. This plan defines the architecture and phases the MVP (US1+US2 native leaf) ahead of
the overlay and browser tiers.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Justification |
|---|---|---|
| **I. Spec-First** | PASS | Spec + clarifications frozen before this plan; plan quotes spec FRs. Code never authoritative over spec; Bug-Protocol (STOP+report) applies. |
| **II. Bug-Protocol / No-Workarounds** | PASS (gated) | Reuse-and-harden (H2/H3) fixes root causes, not symptoms. The 050 codexreview robustness gaps are precedent — fix the caller, no masking try/catch. |
| **III. SRSW** | PASS | GLP demonstration/test programs stay SRSW-clean; zero `skipSRSW` tokens (machine-checkable at analyze). |
| **IV-a. Language Authority** | PASS (gated) | The transport is host-side (C#/Gleam/JS) below the GLP seam; `link_id` schemes + record kinds are **data**. **No new GLP kernel/guard/primitive.** Any newly-necessary primitive is propose-first (050 precedent, FR-019 analog). |
| **IV-b. Preserve Working Internals** | PASS | 050 native-QUIC + olamnit substrate are **harvested/extended additively**, not removed. The dual-leaf migration (FR-019) keeps both identities during transition, no destructive rewrite. |
| **V. Claude-Only LM** | PASS | No LM on any transport path; zero `OPENAI_API_KEY`/`litellm`/`openai` (machine-checkable). GEPA/DSPy N/A here. |
| **VI-a. Additive/Idempotent/Single-Head Migration** | PASS (gated) | DHT-record / relay-admission tables are one additive, idempotent migration advancing the single head by one. Asserted by a `test_migration_*_single_head.py`. |
| **VI-b. Single PGLite Cluster** | PASS | Records/admission cache use the one repo working-data cluster via `codeconv.bridge_client`; no second working-data cluster. |
| **VII. Test-Gated, Commit-Scoped Shipping** | PASS | Baseline green before change, re-test after; commit only feature files (never `-A`); ship via GitFlow (feature→develop→release→main). |

**Result: PASS** (4 gated on discipline held during implement). No unjustified violations → proceed
to Phase 0. New third-party surface (ICE/DCUtR, S-Kademlia, browser WebRTC/WebTransport, 057 dep) is
tracked in Complexity Tracking below.

## Project Structure

### Documentation (this feature)

```text
specs/051-ynet-transport/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (transport ICapability + DHT/relay/exit contracts)
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
csharp/
├── ynet_transport/            # NEW — native consolidated QUIC leaf (harvest glp_link);
│   │                          #       Ed25519-key-as-TLS-identity, dual-leaf migration
│   ├── Link/                  # YNET QUIC link (supersedes QuicTransport)
│   ├── HolePunch/             # ICE/DCUtR candidate exchange + coordinated open
│   ├── Dht/                   # embedded S-Kademlia store/lookup, self-certified records
│   ├── Relay/                 # hybrid: circuit-relay-v2 (mesh) + Tor-cell (internet/critical)
│   ├── Seal/                  # sealed routes, garlic layering, SafetySelection, mix-trust
│   ├── Exit/                  # trusted-gate egress (extend olamnit EgressService) + abuse policy
│   └── Capability/            # ICapability surface consumed by qhstate 056
├── glp_link/                  # HARVESTED FROM (migration source; not deleted during transition)
└── ynet_transport.tests/      # NEW — contract + integration + unit
gleam_quic/                    # EXTENDED — Gleam/BEAM services-tier transport impl
ynet_browser/                  # NEW — distinct JS/WASM tier (WebRTC datachannel + WebTransport)
olamni/                        # REUSED substrate refs (crypto-envelope / DSDV / EgressService)
pgdb/                          # repo PGLite working-data cluster (additive migration)
specs/051-ynet-transport/      # this feature's docs
```

**Structure Decision**: multi-runtime transport library. The **native C#/.NET leaf**
(`csharp/ynet_transport/`) is the MVP spine and harvests `csharp/glp_link`; the **Gleam/BEAM tier**
extends `gleam_quic/`; the **browser tier** is a separate `ynet_browser/` JS/WASM package (cycle-2 §3
— no single cross-tier binary). All three present the same `ICapability` contract (Phase 1
`contracts/`). Records/admission live in the existing `pgdb/` cluster.

## Complexity Tracking

> New third-party / cross-feature surface beyond the stdlib+olamnit+050 reuse baseline. Each is a
> BUILD-NEW item the cycle-2 external verification confirmed has no in-tree equivalent.

| Addition | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| ICE/DCUtR hole-punch | R2 / cycle-2 #1 gap (absent in all internal slices) | No in-tree NAT traversal exists; iroh/libp2p prove it must be absorbed, not invented |
| Embedded S-Kademlia DHT | FR-006 discovery of self-certified records | Consuming a public DHT (iroh/Pkarr) leaks metadata + breaks curated-trust (clarify decision) |
| Hybrid relay-forward (circuit-relay-v2 + Tor-cell) | FR-007 clarify decision — traffic-class trust split | A single relay model can't serve both curated-mesh voucher trust and internet/critical anonymity |
| Browser WebRTC + WebTransport tier | FR-014/FR-015 — MsQuic can't target the browser | Reusing the native QUIC in-browser is impossible (Veilid-WASM/iroh/libp2p all confirm) |
| Dep on `057-yngenios-pocw-coin` | FR-010a standard mix-trust stake signal | Building a parallel stake mechanism duplicates 057; Loopix fallback covers 057-absent |
| **`BouncyCastle.Cryptography` 2.4.0** (DEC-CRYPTO-1, 2026-07-14) | FR-002 Ed25519 node identity — .NET BCL has no native Ed25519 | Rolling our own Ed25519 is unacceptable for a security primitive; BouncyCastle is pure-managed (no native binary, preserves standalone posture) with an ECDsa/P-256 BCL fallback behind `INodeSigner`. **First third-party crypto dep in this lib.** |
