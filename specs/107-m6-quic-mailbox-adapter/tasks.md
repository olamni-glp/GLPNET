<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: M6 QUIC mailbox adapter — the wire plane reaches the control surface

**Feature**: `107-m6-quic-mailbox-adapter` · **Spec**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)

**Baseline recorded before the first edit (test protocol):** `ynet_client.tests` **93/93 passed**,
2026-09-06. Analyzer state at baseline: **`CA2264` on `QuicCarrier.cs:86`** — a real finding in this
lane's own one-day-old code, unread until now (T001).

## Phase 0 — the guard that must fail first

- [x] **T001** Fix `CA2264` at `QuicCarrier.cs:86` — `ArgumentNullException.ThrowIfNull` on a
      non-nullable value is a no-op. *The analyzer had been saying so since the commit that
      introduced it; this is the third time in three waves that a second instrument was already
      talking and nobody read it (CS0649 wave-29, xUnit1031 wave-33).*
- [x] **T002** `ContractReachabilityTests` — enumerate every concrete `IYnetInbound` /
      `IYnetOutbound` in the shipped client assembly and require a control-surface path to each.
      **This MUST FAIL on HEAD.** (SC-004) *A guard never observed failing has not been shown to
      work; T002 failing is the positive control for the whole feature.*
- [x] **T003** Record T002's failing output verbatim into the test file as the measured
      before-state, so a later reader can see what it caught rather than taking it on trust.

## Phase 1 — plane selection (US1, US3)

- [x] **T010** `PlaneSelection` — parse `--plane wire|file|both|loopback` / `YNET_CLIENT_PLANE`,
      default **`file`** (current behaviour stays the default; no deployment silently moves).
- [x] **T011** Bind result as a **value** `{requested, live[], degradedFrom, reason}`; render all
      status output from it, so FR-002 is structural. (FR-001, FR-002)
- [x] **T012** Wire→file fallback on bind failure; **file** failure exits non-zero (nothing below
      it). (FR-004, FR-004c)
- [x] **T013** Fallback stated **on the same line that says "running"**, naming live plane,
      requested plane, and reason. (FR-004a)
- [x] **T014** `DegradedNotice` — additive fleet-visible record on any unbound requested plane;
      **best-effort, never blocks the client**. (FR-004b)
- [x] **T015** Expose bound endpoint + provider name read from the live handle. (FR-005)
- [x] **T016** Use `PlaneSelection` in `run`, `poll`, `send`, `doctor` — one answer, four verbs.
      *Today those four already disagree; that divergence is part of the defect.*
- [x] **T017** `PlaneSelectionTests` — incl. the negative control that a **requested-and-bound**
      plane emits **no** degraded notice.

## Phase 2 — sending on the wire (US2) — **BLOCKED, ruled, and reported (Q-G35-01 → A)**

🔴 **Not done, and NOT deferred silently.** Measured: `NodeIdentity` has `Generate()` and
`DeriveNodeId()` and **no persist/load**, so an identity made per invocation carries a different
Ed25519 nodeId every run — and the fleet pins peers by nodeId, never by address. Wiring `--identity`
to `Generate()` would produce a send verb that compiles, passes a local test, and is refused by every
correctly-configured peer: a **confident zero**, the exact defect class this era closes. The engineer
ruled: ship the loud refusal (exit 8, naming the missing argument), and give identity persistence its
own era. **`send --plane wire` is NOT MET and is reported as NOT MET.**

- [~] **T020** `send --plane wire` via `QuicOutbound`, same `<node>/<actor>` addressing. (FR-006)
- [~] **T021** Undeliverable send → distinct non-zero exit naming peer + address; never reports
      success. (FR-007)
- [~] **T022** Send never throws for a dead peer — **both** the slow negative (timeout) and the
      fast negative (immediate "unreachable"). (FR-008, SC-006) *This is the defect my own test
      caught in my own adapter yesterday: `SocketException` is not an `IOException`, so `Send`
      threw when the network was fast at saying no.*
- [~] **T023** Send bounded in time. (FR-009)

## Phase 3 — composite plane (US4, P1 by ruling Q-G34-03)

- [x] **T030** `CompositeInbound` — N inner planes, fan-out open/close, fan-in receive. (FR-022)
- [x] **T031** De-duplicate by message id, **bounded** set with insertion-order eviction.
      *Unbounded is a memory-exhaustion primitive for any peer that can complete a handshake.*
      (FR-023)
- [x] **T032** Mutation proof: neuter the de-duplicator → a test MUST fail; restore → pass.
      (FR-023a, SC-008)
- [x] **T033** Negative control: one plane, one delivery → exactly one alert, so a de-duplicator
      that suppressed **everything** could not pass. (FR-023b)

## Phase 4 — kernel-managed hosting (M6-d — IN SCOPE by the reversal of Q-G34-01)

- [x] **T040** Measure and record the root cause: `glp_supervisor` exists, is tested, hosts
      `glp_engine_host`, and does **not** host the M6 client. **Done — recorded in spec.md
      §Root cause.**
- [x] **T041** `serve` verb: run the receiver **and** answer a liveness round-trip. (FR-024)
- [x] **T042** The liveness ACK is computed from the **receiver's actual health**, not from a bare
      socket accept. *Otherwise the client answers "alive" while its plane is dead — a zombie
      wearing a heartbeat.* (FR-025)
- [x] **T043** Liveness responder on a **dedicated thread**. *A responder parked on a starved pool
      answers "unavailable" and the supervisor kills a healthy client — yesterday's measured
      pool-starvation defect aimed at the one component whose job is to be believed.* (FR-020)
- [x] **T044** Bind the **existing** `glp_supervisor`; write no new supervision logic. (FR-029)
- [x] **T045** Prove the zombie case: process alive, stops answering → detected and terminated.
      *This is the criterion a process-existence check cannot pass, so it is the one that proves
      the check is real.* (FR-026, SC-011)
- [x] **T046** Prove broken-channel ≠ death. (FR-027)
- [x] **T047** Proven END TO END 2026-09-06 (engineer ruling Q-G35-02 → B, which overrode the
      cheaper option): `SupervisedHostingEndToEndTests` runs the **real** `Supervisor` hosting the
      **real** `ynet_client` binary, confirms it answers the supervisor's own ping, kills the child,
      and confirms the supervisor restarts it with a **different PID** and no operator action.
- [x] **T048** SC-012: count supervision implementations before/after — MUST be unchanged.

## Phase 5 — parity, discipline, closure

- [x] **T050** `FrameParityTests` — byte-identical encodings across planes, with a **non-empty
      guard first**. *Two empty transcripts also compare equal — wave-33's trap.* (FR-010, SC-003)
- [x] **T051** No unbounded wait on a pool thread anywhere this feature adds. (FR-020)
- [x] **T052** Clean shutdown releases every thread and socket. (FR-021)
- [x] **T053** **T002 must now PASS.** Re-run and record. (SC-004)
- [x] **T054** SC-007: re-measured 2026-09-06. The reason parallelism stays disabled **still
      holds, verified at source rather than assumed**: the blocking `Task.Run(...)` +
      `.GetAwaiter().GetResult()` around `YnetSession.Accept` is still present at
      `CoreTransportTests.cs:124/127` (and 170, 223). This feature did not touch it; the durable fix
      is roadmap `ynet-nonblocking-session-handshake` (WSJF 5.25 / RICE 24000, promoted). Restoring
      the default now would re-create wave-33's scheduler-measuring suite.
- [x] **T055** Suites measured 2026-09-06 after the change: **171/171** `ynet_client.tests`
      (baseline 93/93), **217/217** `ynet_transport.tests`, **73/73** `glp_engine_host.tests` — the
      last being the supervisor's EXISTING consumer, unchanged, which is SC-012 measured in practice
      rather than argued.

## Phase 6 — fleetwide (ordered by the engineer's reversal)

- [x] **T060** Broadcast the **measured root cause** — two independent first-party instances of
      capability-built / consumer-absent in one repo in one day — with the generalised cause and
      the machine-check remedy. Carries the discharge notice for the two broadcasts already fanned
      out 37× and 8×.
- [x] **T061** Discharged WITHOUT minting a duplicate: the fleetwide remedy already exists on the
      roadmap as `declared-unconsumed-guard` (WSJF 8.00 / RICE 18000, **rank 2 of 49**, promoted).
      Creating a second feature for the same remedy would itself be the duplication defect that cost
      this repo an M6 carrier in wave-32. What this era adds is the **working exemplar** and the
      measured evidence, published fleet-wide.
- [x] **T062** Do **not** claim `declared-unconsumed-guard` without a claim-first broadcast
      (Q-G34-04 → A).

## Dependencies

```
T001,T002,T003  →  T010..T017  →  T020..T023
                       ↓              ↓
                   T030..T033          |
                       ↓               |
                   T040..T048  ←───────┘
                       ↓
                   T050..T055  →  T060..T062
```

T002 gates everything: until it fails, there is no evidence the guard measures anything.
T053 gates closure: until it passes, the defect is not closed.
