<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: YNET frame field parity across planes

**Feature**: `110-ynet-frame-field-parity` · **Branch**: `110-ynet-frame-field-parity`
**Spec**: [spec.md](./spec.md) · **Created**: 2026-09-07

## Technical context

| item | value |
|---|---|
| language / runtime | C# / .NET (existing `csharp/ynet_client` + `csharp/ynet_client.tests`, xUnit) |
| files touched | `Client/CoopFileCarrier.cs`, `Client/QuicCarrier.cs`, `ynet_client.tests/` |
| declared area | `ynet_client` (claim published 18:00Z to 148/148 lanes) |
| out of scope | iroh sidecar, iroh carrier, QUIC listener, election/commit phase, coordinator tiers, deployment ledger — all other lanes' allocations |

## The shape of the problem

Both carriers construct `YnetFrame` **inline inside `Send`**:

- `CoopFileOutbound.Send(string, string)` — `CoopFileCarrier.cs:183-190`, then writes a file.
- `QuicOutbound.Send(YnetMessage)` — `QuicCarrier.cs:333-341`, then serialises and dials.

So today the frame **cannot be observed without performing I/O**: the file plane writes into a
peer inbox, the wire plane dials a remote endpoint. That is the entire reason era 107's parity
test compared two serialisations of one hand-built frame — **the carriers offered no seam**, so
the only thing reachable from a test was the serialiser. The mis-scoped test is a *symptom of a
missing seam*, not of a careless author, and the fix is to supply the seam.

## Approach

### 1. Extract a construction seam (no behaviour change)

Add to each carrier an `internal` method that builds and returns the frame, and have `Send`
call it. The sequence increment moves **into** the seam so the seam reproduces real numbering
rather than a sanitised version of it.

```
internal YnetFrame BuildFrame(string signal, string body)   // CoopFileOutbound
internal YnetFrame BuildFrame(YnetMessage message)          // QuicOutbound
```

`InternalsVisibleTo` already or newly grants the test assembly access. **Verification that this
is behaviour-preserving is mechanical, not asserted**: the construction expressions are moved
verbatim, and SC-006 diffs them.

### 2. Rulings as data, not comments (FR-014)

A `FrameFieldRuling` table keyed by field name:

| field | outcome | ruling | rationale |
|---|---|---|---|
| `Origin` | `MayDiverge` | Q-110-03 | wire has a handshake-proven Ed25519 identity the file plane cannot have |
| `SenderNode` | `MayDiverge` | Q-110-03 | as `Origin` |
| `SenderActor` | `MayDiverge` | Q-110-01 | different addressing models: file = sender's actor, wire = destination's actor |
| `Sequence` | `MustAgree` | Q-110-02 | both planes 1-based after this era |
| `Signal` | `MustAgree` | — | carries the caller's summary verbatim on both planes |
| `Body` | `MustAgree` | — | carries the caller's body verbatim on both planes |

Held as data so revoking a ruling re-fires the check with no code change.

### 3. Enumerate the envelope, never a hand-list (FR-007)

The check reflects over `YnetFrame`'s public properties. A field added to the envelope and
populated on one plane only is **DIVERGES-UNRULED** and fails, naming itself. A hand-maintained
list would silently omit it — which is the same defect class as the brief that said three.

### 4. The one authorised value change (FR-013)

`CoopFileCarrier.cs:186`: `Interlocked.Increment(ref _sequence) - 1` → `Interlocked.Increment(ref _sequence)`.

Re-measured blast radius (18:20Z): the only file-plane consumer is the filename component in
`{epochMs}.{seq}.{guidN}.frame` (L194), whose uniqueness comes from the GUID. The wire's dedup
key `{authenticatedPeer}#{frame.Sequence}` (`QuicCarrier.cs:275`) is on the other carrier.

### 5. Prove the check fails (FR-010 / SC-003)

A negative control that **is not tautological**. The wave-34 lesson applies directly: a control
asserting `MustAgree` fails when two *literals* differ proves nothing about the check. The
control must drive the **real classifier** over a **real pair of constructed frames** with one
field forced to differ, and assert the classifier returns `DivergesUnruled` **naming that
field**. Recorded as an executed transcript, not a claim.

## Constitution / standing-clause check

- **C-18 claim before code** — published 18:00Z, 148/148 lanes, disclaimers explicit. ✅
- **C-19 fetch at start of era work** — `git fetch` run, 0 behind. ✅
- **C-20 measurement** — every claim carries host + UTC timestamp; capabilities reported
  pass / refuse / unverifiable, never bool. ✅
- **C-03 L0** — no cross-platform capability is being re-implemented Windows-only; this is a
  test seam plus one arithmetic change in an existing shared project. ✅
- **C-16 gaps** — the missing-seam root cause is fixed, not worked around. ✅
- **FR-011 loopback** — untouched; this era binds nothing. ✅

## Risks

| risk | mitigation |
|---|---|
| the seam changes behaviour while looking inert | expressions moved verbatim; SC-006 diffs construction sites; full suite before and after |
| `Sequence` change ripples further than measured | one consumer measured; suite is the check; trivially revertible (one token) |
| three `may-diverge` rulings become a suppression mechanism | FR-006 forces ruled divergences to be **reported with both values** in every run, never hidden |
| the negative control is tautological | it drives the real classifier over real constructed frames (§5), which is the wave-34 correction |
