<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model — QUIC federation transport

**Feature**: `102-quic-federation-transport` | **Date**: 2026-09-04

Entities are given as the spec names them. Every field carries the FR it exists for. Invariants are
stated as things a test can falsify, not as prose.

---

## 1. `NodeId` — the participant's identity

| Field | Type | Source |
|---|---|---|
| `Value` | 32 bytes | `SHA-256(SubjectPublicKeyInfo)` — existing `NodeIdentity.DeriveNodeId` |
| `Text` | lowercase hex | canonical display / config form |

**Invariants**

- **I-1 (FR-007)**: `NodeId` is a pure function of the public key. It contains **no address, no
  hostname, no drive letter, no port**.
- **I-2 (FR-007, SC-006)**: two connections presenting the same `NodeId` are **one participant**,
  regardless of source address. Olamnit answers on `.136` and `.129`; it counts once.
- **I-3 (R3)**: the transport's SPKI pin and the `NodeId` derive from the same SPKI. They are the
  same value under two names and MUST NOT be reconciled by a mapping table.
- **I-4 (FR-007)**: the key is **persisted**. `CreateDevCert` mints a fresh cert per call; a
  federation identity that changes per process is not an identity.

---

## 2. `TermSpace` — the ordering universe

| Field | Type | Notes |
|---|---|---|
| `Id` | string | opaque; minted per federation epoch |
| `Kind` | `Live` \| `Legacy` \| `Unknown` | `Legacy` is a *named* space, not an absence |

**Invariants**

- **I-5 (FR-026)**: `Id` is minted by an **explicit recorded operator action** and carried in
  configuration. It is **not** derived from a host identity and **not** derived from wall-clock time.
- **I-6 (FR-026)**: minting a new epoch is **additive**. Operations from a prior epoch stay readable
  and attributed (SC-013).
- **I-7 (FR-027)**: an operation with no recognised space is assigned the **`Legacy`** space. It is
  never dropped, never rewritten, and never coerced into the live space.
- **I-8 (FR-016)**: an operation whose space is present but unrecognised is `Unknown`: **retained and
  reported as unordered**, which is a different observable state from `Legacy` and from absent.

---

## 3. `Term` — a leadership-bearing ordering value

| Field | Type | FR |
|---|---|---|
| `Space` | `TermSpace` | FR-013, FR-014 |
| `EraCounter` | `long` | FR-013, FR-015 |
| `HostId` | `NodeId` | FR-013 |

**Ordering — the only comparison permitted**

```
Compare(a, b):
    if a.Space.Id != b.Space.Id      -> INCOMPARABLE      (FR-014)
    if a.EraCounter != b.EraCounter  -> by EraCounter      (FR-013)
    else                             -> by HostId, ordinal (deterministic tiebreak only)
```

**Invariants**

- **I-9 (FR-014, SC-005)**: cross-space comparison returns `Incomparable` — **never** a magnitude
  result. A foreign-space term carrying `long.MaxValue` wins nothing.
- **I-10 (FR-015)**: `EraCounter` advances **only** on a leadership event. There is no code path in
  which elapsed time advances it. A host offline for a week returns with an **unchanged** counter.
- **I-11**: `Incomparable` is a **first-class third result**, not `false`. Collapsing it to a boolean
  is how a foreign term wins by accident.
- **I-12 (R5)**: the fossil `628016928ab854ae` carries `5961694 = floor(unix_ts/300)`. Under I-7 it
  lands in `Legacy` and is therefore incomparable to every live term.

---

## 4. `FederationOp` — a board operation on the wire and at rest

| Field | Type | FR |
|---|---|---|
| `OpId` | `Dot` (`PeerName`, `Counter`) | FR-010 — the exactly-once key |
| `Origin` | `NodeId` | FR-009 — attribution |
| `Kind` | string (`board_post`, `claim`, `leader_claim`, `retire`, …) | |
| `Term` | `Term?` | FR-013 — present iff leadership-bearing |
| `Deps` | `Dot[]` | existing causal context |
| `PredHash` | 32 bytes | existing `HashChain.PredHash` |
| `Body` | payload | opaque to federation |

**Invariants**

- **I-13 (FR-010, SC-002)**: `OpId` is the fold key. Two deliveries of the same `OpId` fold to one
  entry. Verified by a test that **deliberately redelivers**.
- **I-14 (FR-011)**: appending an op never removes, rewrites, or reorders an op already present.
- **I-15 (FR-009)**: `Origin` survives the crossing. An op that arrives without correct attribution
  is a fault, not a value.
- **I-16 (FR-017)**: there is **no** delete path. The type exposes no removal operation at all —
  absence of the capability, not a guard against using it.

---

## 5. `RetirementOp` — the only correction mechanism

A `FederationOp` with `Kind = "retire"` and a body naming the target.

| Field | Type | FR |
|---|---|---|
| `TargetOpId` | `Dot` | FR-029 |
| `IntoSpace` | `TermSpace` (`Legacy`) | FR-029 |
| `Reason` | string | audit |

**Invariants**

- **I-17 (FR-017, FR-029, SC-012)**: after retirement the **target is still present** in the log and
  is **reported as unordered**. A test asserts *both* — presence and exclusion from ordering.
- **I-18 (FR-029)**: a `RetirementOp` is an ordinary board operation. It folds, attributes and
  appends under the same rules, and is itself retirable.
- **I-19**: retiring an already-retired op is idempotent, not an error.

---

## 6. `PeerSet` — who may be admitted

| Field | Type | FR |
|---|---|---|
| `Entries` | map `NodeId` → `PeerEntry` | FR-006, FR-007 |

`PeerEntry`: `{ Name, NodeId, Endpoints: IPEndPoint[], Pin }`

**Invariants**

- **I-20 (FR-006, SC-004)**: an **empty** peer set admits **nobody**. This is the safe default and is
  the state the system fails into.
- **I-21 (FR-007)**: the map is keyed by `NodeId`. `Endpoints` is a **list** precisely because a
  participant may answer on several addresses; adding an address does not add a participant.
- **I-22 (FR-005)**: **both** parties verify before any board data is exchanged. One-sided
  verification is not admission.
- **I-23 (FR-008)**: a pin mismatch raises `PinMismatch`, a **distinct** condition from
  `Unreachable` and from a generic transport error — because the two demand opposite responses.

---

## 7. `FederationStatus` — four states, each with an explicit unknown

| State | Meaning | FR |
|---|---|---|
| `StackSupported` | the QUIC stack is available in this process | FR-019 |
| `ListenerBound` | a listener is bound to a peer-reachable address | FR-019 |
| `PeerAdmitted` | at least one peer completed mutual verification | FR-019 |
| `OpReceivedFromPeer` | at least one op has actually crossed | FR-019 |

Each is `Yes | No | Unknown`.

**Invariants**

- **I-24 (FR-020)**: no state is inferred from an earlier one. `ListenerBound = Yes` does **not**
  imply `PeerAdmitted`; reachability implies **nothing**.
- **I-25 (FR-021, SC-010)**: a state that could not be measured is `Unknown`. `Unknown` and `No` are
  **different observable outputs**. A test removes the ability to measure and asserts `Unknown`.
- **I-26 (FR-022)**: a crossing observed between two participants **on the same machine** sets
  `OpReceivedFromPeer` but records `SameMachine = true`, and the surface **must not** report
  cross-host federation. The one-machine mechanism proof is evidence of the mechanism, not of SC-001.
- **I-27 (SC-007)**: for each of the four states a positive control and a negative control produce
  **different** reported results. Identical output in both directions is a failed test, not a pass.
- **I-28 (FR-023)**: a startup blocked by host software policy sets a distinct
  `PolicyRefused(0x800711C7)` condition naming the policy — never a generic startup error.

---

## 8. `FederationConfig` — the operator's surface

| Field | Type | FR |
|---|---|---|
| `Enabled` | bool (default **false**) | FR-004 |
| `BindAddress` | IP | FR-001 |
| `BindPort` | int (default 47890) | FR-001, FR-002 |
| `SpaceId` | string | FR-026 |
| `Peers` | `PeerEntry[]` (default **empty**) | FR-006 |
| `PushOnAppend` | bool (default true) | FR-028 |
| `PullIntervalSeconds` | int (default 60) | FR-028 |

**Invariants**

- **I-29 (FR-004)**: with `Enabled = false`, or with every peer unreachable, the **local oracle
  serves its own lanes unchanged**. Federation is never on the local critical path.
- **I-30 (FR-002)**: config is changeable **without rebuilding** and is **readable back** for
  verification. Write-only configuration cannot be verified and therefore cannot be trusted.
- **I-31 (FR-001)**: binding to a loopback-only address is a **misconfiguration**, reported as such —
  it is the failure mode that looks exactly like success.
- **I-32 (FR-025, SC-009)**: every change made to enable federation has a recorded reversal stored
  beside it. The reversal is data, not documentation.

---

## State transitions

### Federation session

```
Disabled ──enable──> Configured ──bind ok──> Bound ──peer verified──> Admitted ──op crossed──> Federating
    ^                     |                    |                          |                        |
    |                  bind refused        no peer pins             pin mismatch            link drops
    |                     v                    v                          v                        v
    +──────────────── PolicyRefused        AdmitsNobody             PinMismatch          Degraded(local-only)
                       (FR-023)             (FR-006)                 (FR-008)                  (edge case)
```

- `Degraded` **still serves local lanes** (I-29) and **says so explicitly**; it never reports success.
- Every terminal-looking state above is **separately named**. Collapsing `PolicyRefused`,
  `AdmitsNobody`, `PinMismatch` and `Unreachable` into one error is the defect FR-008 and FR-023
  exist to prevent.

### Term epoch

```
Legacy (implicit, pre-rule)          Live epoch N ──mint──> Live epoch N+1
        |                                  |                      |
   incomparable to every live term    comparable within N    prior epoch stays readable (I-6)
```

Minting is monotone and additive. Nothing is rewritten, so no mint can lose an operation.
