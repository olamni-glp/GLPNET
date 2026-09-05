<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: WP-02 — configurable QUIC listener service for `yng-broker`, `yng-guardian`, the oracle and the admin interface

**Feature Branch**: `104-wp02-quic-listener-service`
**Created**: 2026-09-05
**Status**: Draft
**Input**: Fleet ruling `Q-gsbk14-01` — *"@glpnet, every host — WP-02 is yours and it is blocked by nothing."*

**Roadmap feature**: `wp02-configurable-quic-listener-for-broker-guardian-oracle-admin` (WSJF 6.75 / RICE 6000, promoted)
**Engineer rulings selecting and shaping this work**: `Q-olg15-01` (this era, WP-02 first), `Q-olg15-03` (iroh sidecar primary, msquic retained as redundant fallback), `Q-gsbk14-01` (fleet allocation)

---

## Measured Baseline *(mandatory — scope is set by measurement, not by the directive's wording)*

Everything in this table was measured on OLAMNIT on 2026-09-05 before the spec was written.

| # | Claim in circulation | Measured result | Verdict |
|---|---|---|---|
| B1 | "`ynet_transport` compiles nowhere" (`@shiras-yngwin`) | `csharp/ynet_transport` builds Release / `net11.0` with 0 errors; `csharp/ynet_transport.tests` **184/184 passed**. | **FALSE for GLPNET** (true for the L0 projection — a different estate) |
| B2 | "the broker is blocked by a missing QUIC transport" | A routable QUIC listener already works here: 5 of 5 cases — `127.0.0.1`, `0.0.0.0`, `192.168.0.136`, `192.168.0.129`, `192.168.0.136:47890` — each a **full handshake plus a verified bidirectional byte echo**. | **FALSE — it is a wiring gap, not an implementation gap** |
| B3 | *(not previously recorded)* | **Nothing in this repo binds a QUIC listener on behalf of a named service.** `BindListenerAsync` has exactly two non-test callers, both inside the providers themselves. There is no broker/guardian/oracle/admin listener, and no bind configuration of any kind. | **LIVE — this is the actual gap** |
| B4 | `yng-broker` / `yng-guardian` "RUNNING, no TCP listener, no UDP endpoint" (`@gavriella`, by PID and socket) | Not reproducible from inside this repo — but see B5 for a mechanism that produces exactly this symptom. | **LIVE, cause unattributed** |
| B5 | *(not previously recorded)* | Windows **silently auto-created TWO inbound `Block` rules for the QUIC binary on first run, with no prompt.** A per-binary `Block` is invisible from inside the process and **beats a port `Allow`**: the process observes a successful bind and receives nothing. | **LIVE — and it is a candidate cause of B4** |
| B6 | iroh is the mandated primary QUIC provider "from L0 upward" | **No Rust toolchain exists on OLAMNIT** — `cargo`, `rustc` and `iroh` are all absent and `~/.cargo` does not exist. The iroh *native* binary cannot be produced on this host in this era. | **LIVE — disclosed, not routed around** |

### What the seam already provides (do not rebuild it)

`csharp/ynet_transport/Link/QuicProviderSeam.cs` already declares the exact tiering the ruling
requires, and it predates the ruling:

```csharp
public enum QuicProviderTier { Iroh = 0, MsQuic = 1, Ngtcp2 = 2 }
```

`QuicProviderChain.Default` currently registers **MsQuic and Ngtcp2 only**, with the standing
comment: *"iroh is absent until its stack lands; it registers itself at tier 0 and the chain order
needs no edit when it does."* **This feature is the thing that comment was waiting for.**

`IQuicProvider.Probe()` is already contractually required to *measure*, never to assume: *"they
never report availability from configuration, from an environment variable, or from the fact that
the managed code compiled."* This feature must honour that contract, not weaken it.

---

## User Scenarios

### US1 — An operator brings up a named service's listener *(P1)*

An operator declares that `yng-broker` listens on `0.0.0.0:47890`, starts the service, and can see
from one command which provider bound it, at which address, and whether a peer can actually reach
it. If nothing can bind, the service refuses to start and names what to install.

**Acceptance**
1. Given a declared bind for a named service, when the service starts, then a listener is bound and
   the record states the service name, the bound endpoint and **the provider that bound it**.
2. Given no provider can serve, when the service starts, then it **refuses** and the refusal names
   every tier and its measured reason — it never starts deaf.

### US2 — iroh is primary, and the fallback is never silent *(P1)*

The chain prefers iroh. When iroh cannot serve on this host, the service still comes up on msquic —
but the fact that it fell back is **recorded and reported**, not swallowed.

**Acceptance**
1. Given an iroh sidecar is reachable, when a listener is bound, then iroh binds it (tier 0 wins).
2. Given no sidecar, when a listener is bound, then msquic binds it **and the report states that
   tier 0 was unavailable and why**.
3. A fallback that is not reported is a defect, not a degradation.

### US3 — A bind is not a link *(P1)*

Binding proves the socket opened. It does not prove a peer can reach it — B5 is precisely the case
where those two differ. The service must be able to prove inbound reachability separately.

**Acceptance**
1. A reachability check performs a **full handshake and a bidirectional byte exchange**, not a bind.
2. A bind that succeeds while the reachability check fails reports **`BOUND_UNREACHABLE`** — a
   distinct outcome from both `OK` and `BIND_FAILED`.

---

## Requirements

- **FR-001** The system MUST let an operator declare, per named service (`yng-broker`,
  `yng-guardian`, `oracle`, `admin`, and any future name), a bind address and port.
- **FR-002** The system MUST bind each declared service's listener through `QuicProviderChain`, and
  MUST NOT bypass the chain.
- **FR-003** The system MUST record, for every bound listener, the **provider that actually bound
  it** — observed from the listener handle, never inferred from configuration.
- **FR-004** The system MUST register an iroh provider at `QuicProviderTier.Iroh` (0) so that iroh
  is preferred whenever it can serve (`Q-olg15-03`).
- **FR-005** The iroh provider MUST be a **sidecar adapter**: the managed adapter is in-tree at L0;
  the Rust native binary is a separate process and is never linked into the managed assembly. This
  is what lets iroh sit at L0 without making L0 distro-dependent.
- **FR-006** The iroh provider's `Probe()` MUST measure sidecar reachability and MUST return
  `unavailable` with a specific, actionable reason when the sidecar is absent. It MUST NOT report
  availability from configuration or from the presence of the managed adapter.
- **FR-007** `QuicProviderChain.Default` MUST retain msquic and ngtcp2 beneath iroh as redundant
  fallbacks (`Q-olg15-03`: *"it is not removed"*).
- **FR-008** When a listener is served by a provider other than tier 0, the service report MUST
  state which tier was skipped and its measured reason.
- **FR-009** The system MUST provide an inbound reachability check that completes a handshake and a
  bidirectional byte exchange against the bound endpoint.
- **FR-010** The system MUST distinguish `OK`, `BOUND_UNREACHABLE` and `BIND_FAILED`, and MUST NOT
  report `OK` for a listener whose reachability was not measured.
- **FR-011** When no provider can serve, the service MUST refuse to start and the refusal MUST name
  every tier and its reason.
- **FR-012** The listener service MUST NOT elect, campaign, vote, or seat a leader, and MUST NOT
  contain a fallback election (`Q-gsbk14-01`: *"a tool that quietly elects when the broker is
  unreachable produces a leader the fleet never agreed to"*).

## Success Criteria

- **SC-001** A named service binds a routable QUIC listener and the bound provider is reported.
  *Measured by a test that asserts the reported provider equals the handle's `ProviderName`.*
- **SC-002** With no iroh sidecar present, the chain selects msquic **and reports the tier-0 skip
  with its reason**. *Measured by asserting the report text contains the tier-0 refusal.*
- **SC-003** With a stub sidecar present, the chain selects iroh at tier 0. *Measured with a fake
  sidecar endpoint — this is what proves FR-004 rather than restating it.*
- **SC-004** A bound-but-unreachable listener reports `BOUND_UNREACHABLE`, not `OK`.
  *Measured by binding and then checking against an address that cannot complete a handshake.*
- **SC-005** A service with no available provider refuses to start, and the refusal names all tiers.
- **SC-006** 🔴 **Negative control:** every success criterion above must be observed to FAIL when
  its mechanism is removed. A green check whose failure mode was never observed is not evidence
  (era-101 transferable rule). *This criterion is discharged by deliberately breaking each
  mechanism once and recording the red.*

## Out of Scope *(named, not silently dropped)*

- **The iroh native sidecar binary.** No Rust toolchain exists on OLAMNIT (B6). This era delivers
  the adapter, the tier-0 registration and the measured refusal; the binary is a separate,
  disclosed dependency. **`Q-olg15-03` is satisfied in shape and unsatisfied in binary, and this
  spec says so rather than reporting the ruling done.**
- **Cross-host federation.** Blocked on a `space_id` this lane must not mint (`Q-olg15-04`).
  Nothing in this era crosses a wire between two hosts, and nothing in it may be reported as
  federation.
- **The Windows firewall remedy (B5).** Requires elevation; ruled `Q-101-03` to be run by the
  engineer per host. This era detects and reports the condition; it does not change firewall state.
- **Any election mechanism** (FR-012).
