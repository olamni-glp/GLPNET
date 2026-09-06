<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: M6 QUIC mailbox adapter — the wire plane reaches the control surface

**Feature**: `107-m6-quic-mailbox-adapter`
**Branch**: `107-quic-mailbox-adapter`
**Spec**: [spec.md](./spec.md)
**Questions**: [questions-G34.json](./questions-G34.json) — 4 rulings, all decided 2026-09-06

## Summary

`QuicInbound`/`QuicOutbound` already exist, are tested, and are **unreachable from the process the
fleet runs**. This feature makes them reachable, makes the choice observable, makes a failed wire
bind degrade loudly and visibly rather than silently, lets both planes run at once without
double-alerting, and — the part that stops this recurring — adds a check that **fails whenever a
realization of the receive or send contract has no control-surface path to it**.

The change is concentrated in the client's control surface plus one composite carrier. The
transport is bound, never rewritten (fleet broadcasts 2026-09-05T12:50Z and 16:00Z both direct
lanes to bind rather than author a rival).

## Technical Context

**Language/Version**: C# / .NET 11 (`net11.0`, per `csharp/ynet_client/YnetClient.csproj`)
**Primary Dependencies**: `csharp/ynet_transport` (QUIC session, listener service, provider chain,
node identity); no new third-party packages
**Storage**: `PendingAlertSpool` — a durable on-disk alert spool, already shipped, unchanged
**Testing**: xUnit — `csharp/ynet_client.tests` (93 today), `csharp/ynet_transport.tests` (217)
**Target Platform**: Windows 11 (Gavriella) and Linux hosts; the client is cross-host by design
**Project Type**: daemon + CLI (one executable, several verbs)
**Performance Goals**: none introduced. The existing thread-per-loop discipline is a *correctness*
constraint here, not a performance one (FR-020)
**Constraints**: no unbounded wait on a pool thread; send bounded in time; frames byte-identical
across planes; additive-only persistence
**Scale/Scope**: ~4 files touched in `csharp/ynet_client`, 1 new composite carrier, 1 new
architecture test, plus tests

## Constitution Check

*GATE: evaluated against the glpnet constitution before Phase 0 and re-checked after Phase 1.*

| Principle | Verdict | Basis |
|---|---|---|
| **I. Spec-First** | ⚠ **Declared deviation, accepted** | The carrier code (`QuicCarrier.cs`, commit `8d4088e4`) was written **before** this spec. That is a real deviation from Principle I and is recorded here rather than hidden. This era's spec was written from the *measured* state of that code, and the era's work — wiring, fallback, composite, guard — is all spec-first from this point. The deviation is not repeated. |
| **II. Bug-Protocol / No-Workarounds** | ✅ | The unreachable carrier was found, STOPPED on, reported, and is being fixed at the cause (no control-surface path) rather than worked around (e.g. by documenting "use the library directly"). FR-004's fallback is a **ruled** behaviour with a stated notice, not a try/catch that masks a caller bug. |
| **III. SRSW** | ✅ n/a | No GLP source touched. Zero occurrences of `skipSRSW`. |
| **IV-a. Language Authority** | ✅ n/a | No GLP language change. |
| **IV-b. Preserve Working Internals** | ✅ | `CoopFileInbound`, `CoopFileOutbound`, `LoopbackInbound`, `YnetReceiverMachine`, `PendingAlertSpool`, `AgentHook` are **not removed and not rewritten**. The composite wraps; it does not replace. |
| **V. Claude-Only LM / No External API** | ✅ | The codexreview stage uses the local `codex` CLI, which is the sanctioned cross-provider critic for that stage. No model API is called from product code. |
| **VI-a. Additive-Only Persistence** | ✅ | The alert spool is append/drain as today. The degraded notice (FR-004b) is an **additive record**; it overwrites nothing. |
| **VI-b. Single PGLite Cluster** | ✅ n/a | No PGLite access in this feature. |
| **VII. Test-Gated, Commit-Scoped Shipping** | ✅ | Baseline captured before the first edit; suites re-run after; only files this session touched are staged. |
| **VIII. Single Source of Truth** | ✅ | One frame encoder serves both planes (FR-010) — the whole point. One spec (`spec.md`) is authoritative; the rulings live in `questions-G34.json` and are cited, not duplicated. |

**Re-check after Phase 1**: no principle moved. The Principle I deviation is unchanged and remains
declared.

## Project Structure

### Documentation (this feature)

```
specs/107-m6-quic-mailbox-adapter/
├── spec.md                     # WHAT and WHY, with the four rulings recorded
├── plan.md                     # this file — HOW
├── tasks.md                    # dependency-ordered work
├── questions-G34.json          # BK-STD-2 record: 4 questions, 4 decisions
└── checklists/requirements.md  # spec quality gate
```

### Source Code (repository root)

```
csharp/ynet_client/
├── Program.cs                       # CHANGED — plane selection for run/poll/send/doctor
├── Client/
│   ├── YnetInbound.cs               # unchanged contract; LoopbackInbound unchanged
│   ├── CoopFileInbound.cs           # unchanged
│   ├── CoopFileCarrier.cs           # unchanged (CoopFileOutbound lives here)
│   ├── QuicCarrier.cs               # QuicInbound + QuicOutbound — reached, lightly amended
│   ├── CompositeInbound.cs          # NEW — binds N planes, de-duplicates by message id
│   ├── PlaneSelection.cs            # NEW — parses the request, binds, reports, falls back
│   └── DegradedNotice.cs            # NEW — the fleet-visible record of FR-004b
└── YnetClient.csproj                # unchanged

csharp/ynet_client.tests/
├── QuicCarrierTests.cs              # existing 
├── CompositeInboundTests.cs         # NEW — FR-023/023a/023b incl. mutation + negative control
├── PlaneSelectionTests.cs           # NEW — FR-001..FR-005, FR-004a/b/c
├── FrameParityTests.cs              # NEW — SC-003, with the non-empty guard
└── ContractReachabilityTests.cs     # NEW — SC-004, the anti-recurrence check
```

## Design

### 1. Plane selection (`PlaneSelection`) — FR-001..FR-005, FR-004a/b/c

One place decides what to bind, so `run`, `poll`, `send` and `doctor` cannot drift into four
different answers about which plane is live. Today they already have: `run` selects, the other
three hard-code the file plane. That divergence is itself part of the defect.

Selection is requested by `--plane wire|file|both|loopback` (or `YNET_CLIENT_PLANE`), defaulting to
`file` — **the current behaviour stays the default**, so this change cannot silently move an
existing deployment onto a plane its operator did not ask for.

Binding outcome is a value, not a side effect: `{ requested, live[], degradedFrom?, reason? }`.
Status output is rendered from that value, so FR-002's "derived from the live carrier object"
is structural rather than a promise. `PlaneName` is read off each bound carrier.

Fallback (FR-004) applies **only** wire→file. `file` failing has nothing below it (FR-004c) and
exits non-zero.

### 2. Degraded notice (`DegradedNotice`) — FR-004b

An additive record written when, and only when, a requested plane was not bound. Written to the
COOP root the client is already configured with (it needs no new configuration, and a client with
no COOP root has no fleet to notify). Content: host, lane, requested plane, live plane, reason,
UTC. Best-effort by construction — **failing to write the notice must never stop the client
running**, because the fallback exists precisely to keep a damaged host receiving.

### 3. Composite plane (`CompositeInbound`) — FR-022, FR-023, FR-023a, FR-023b

`IYnetInbound` over N inner planes. `Open`/`Close` fan out; `Received` fans in through a
de-duplicator keyed on message id.

The de-duplicator is a bounded-size set of recently-seen ids. **Bounded, because an unbounded one
is a memory-exhaustion primitive available to any peer that can complete a handshake** — the same
reasoning that put a ceiling on frame size. Eviction is by insertion order.

FR-023b is the reason this is not just "a set": an over-eager de-duplicator that suppressed a
*first* sighting would silently lose messages and every "exactly one alert" test would still pass.
The negative control (one plane, one delivery, exactly one alert) is what makes the suppression
visible.

### 4. Frame parity (`FrameParityTests`) — SC-003, FR-010

Encode the same logical message for each plane, compare bytes. **A non-empty assertion runs first**,
because two empty encodings also compare equal — the trap wave-33 recorded when two empty
transcripts "agreed".

### 5. Contract reachability (`ContractReachabilityTests`) — SC-004

Reflect over the shipped client assembly, enumerate every concrete type implementing `IYnetInbound`
and `IYnetOutbound`, and require each to be constructible along a path the control surface can
select. **This test fails against `HEAD` as it stands** — that is the point, and it is the positive
control: a guard that has never been observed to fail has not been shown to work.

Deliberately over the *assembly*, not over source text: a grep for `new QuicInbound` would be
satisfied by a dead code path, and this defect is precisely a code path nobody reaches.

### 6. Supervised hosting (`SupervisedHost`) — FR-024..FR-029, SC-010..SC-012

**Scope change**: the engineer reversed Q-G34-01 mid-era. M6-d is IN.

The root cause (see spec) is that `csharp/glp_supervisor` exists, is tested, and hosts
`glp_engine_host` but not the M6 client. **FR-029 forbids writing a second supervisor** — doing so
would mint a third instance of the exact defect class this feature closes.

`Supervisor` is a `BackgroundService` parameterised by `SupervisorConfig`
(`EngineBinary`, `Listen`, `StoreRoot`, ping interval/timeout, backoff, crash threshold), and it
proves liveness with a **round-trip Ping that must be ACKed** over the child's wire — exactly
FR-025/FR-026/FR-027. So hosting the M6 client is a matter of giving the client the one thing the
supervisor requires and it currently lacks: **an endpoint that answers a Ping**.

Therefore:

- the client gains a `serve` verb: run the receiver *and* listen on a control endpoint that answers
  a liveness Ping with an ACK. This is what makes FR-025 true rather than asserted — a supervisor
  that cannot get an answer is measuring process existence, which FR-025 forbids;
- the ACK is answered **from the receiver's own health**, not from a bare socket accept, or the
  client answers "alive" while its plane is dead — a zombie wearing a heartbeat;
- a `SupervisedHost` composition binds the existing `Supervisor` to that endpoint. No new
  supervision logic: config + wiring only. SC-012 counts supervision implementations before and
  after and requires the count unchanged.

The liveness answer is served on a **dedicated thread**, for the reason in FR-020: a liveness
responder parked on a starved pool answers "unavailable" and the supervisor kills a healthy client.
That is not hypothetical — it is yesterday's measured pool-starvation defect, pointed at the one
component whose whole job is to be believed.

## Complexity Tracking

| Added complexity | Why it is not avoidable | Cheaper alternative rejected because |
|---|---|---|
| A composite carrier + de-duplication | Ruling Q-G34-03 → B puts both planes in one process | Two processes = two mailboxes for one lane, a worse defect than the one being fixed |
| A reflection-based reachability test | SC-004 must catch a *missing consumer*, which by definition leaves no trace at the call site | Grep/review: review is exactly what already failed here — the carrier was reviewed, tested and merged with no consumer |
| A degraded-notice writer | Ruling Q-G34-02 → C | Loud-fallback-only leaves fleet-wide wire loss invisible as N individually-fine hosts |

## Risks

- **The wire cannot be bound on this host** (certificate material destroyed four times). This does
  not block the feature — it exercises FR-004/FR-004a/FR-004b, which are now first-class specified
  behaviour. But it does mean **SC-001 and SC-002 may not be measurable end-to-end here today**, and
  if so that will be reported as *not measured*, never as passed.
- **Restoring test parallelism (SC-007)** touches `AssemblyParallelism.cs`, which yesterday's
  measurement deliberately added. It is only removed if the blocking `Accept` is genuinely gone;
  otherwise SC-007 is discharged by *stating and re-measuring the reason*, which is what it asks.
- **M6-d is now IN scope** (Q-G34-01 reversed to B mid-era). The risk inverts: the temptation is to write a NEW supervisor because it is easier than binding the existing one. FR-029 forbids it, and SC-012 measures it.
