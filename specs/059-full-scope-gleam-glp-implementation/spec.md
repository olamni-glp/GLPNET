<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Full-scope Gleam GLP implementation

**Feature Branch**: `059-full-scope-gleam-glp-implementation`
**Created**: 2026-07-20
**Status**: Draft
**Input**: User description: "Full-scope Gleam GLP implementation"

**Authoritative inputs (this spec composes, never re-derives, them)**:
- Phase-1 gap inventory: `docs/research/fullscope-gleam/gap-inventory-2026-07-19.md` (154 capabilities: 44 delivered / 9 partial / 99 gap-class / escalations)
- Phase-2 FINAL outline plan: `docs/research/fullscope-gleam/feature-outline-plan-FINAL-2026-07-20.md` (90 WPs: 88 CONFIRM + 2 open escalations; waves 1–5; zero dangling deps; run `20260719T134320Z-544f`, cycles=2)
- Engineer gate rulings G1–G5 + G3-A: `docs/research/fullscope-gleam/phase2-verify/rulings.md` (binding)
- Marathon: `mrun-8bda036d9e9b` (scoping discharged; this feature executes under it)

## Clarifications

### Session 2026-07-20

- Q: Yngenios embeddability — does wave 4 require actual wiring to the running yngenios services, or does a contract-plus-stub boundary satisfy "complete inside the yngenios architecture"? → A: Option C — full wiring: the Gleam GLP engine embedded as the controller across all four spec-056 services (S1 storage / S2 network / S3 kv / spine), with the fabric's own tests passing against it. This resolves open escalation `rule-embeddability-api-yngenios-wiring`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Drift-proof foundation for everything downstream (Priority: P1)

A GLP runtime engineer starting any work package in this feature can rely on the already-delivered Gleam foundation (terms/heap/unification, compiler pipeline, engine, REPL, codecs, link wire formats, transports) not shifting underneath them: every delivered interface is pinned in a frozen-interface register, and every existing suite that exercises delivered behavior is a grow-only tripwire that fails loudly on any regression.

**Why this priority**: Every later wave builds on the delivered 44 capabilities; without the freeze+guard layer, parallel work packages silently drift the foundation and all parity claims decay. This is the plan's wave 1 and the head of its dependency spine.

**Independent Test**: From a fresh session, the frozen-interface register exists with all its entries, and the pinned suites (Gleam gleeunit, Dart unified REPL suite, C# reference suites) all run green with their protected test files unmodified against the freeze baseline.

**Acceptance Scenarios**:

1. **Given** the freeze baseline commit, **When** any change modifies a pinned test file or shrinks the suite, **Then** the guard fails the feature's checkpoint until an explicit unfreeze rule-request is ruled.
2. **Given** a fresh session, **When** the wave-1 guard commands are run, **Then** all pinned suites pass and the register enumerates every frozen interface with its protected test list.

---

### User Story 2 - Every promised capability verified, then closed to parity (Priority: P2)

A runtime engineer can see, for each of the 97 promised-or-required capabilities with no Gleam code testimony, a concrete existence/scope verdict (verify), and for every confirmed gap a closure work package that makes the named reference programs and suites pass identically on the Gleam instance — with nothing leaving scope except by a recorded engineer ruling.

**Why this priority**: This is the bulk of "full scope": 97 unconfirmed gaps plus 9 partials with named missing parts. Verify-before-close prevents building against records that don't match code.

**Independent Test**: Each verify WP's verdict artifact exists under `docs/research/fullscope-gleam/phase2-verify/` and each close WP's named reference programs/suites pass on the Gleam instance from a fresh session.

**Acceptance Scenarios**:

1. **Given** an unconfirmed-gap capability, **When** its verify WP runs, **Then** a per-detail_id DELIVERED/ABSENT verdict with runnable evidence is committed, and an ABSENT verdict activates its paired close WP.
2. **Given** a close WP completes, **When** its acceptance command is re-run from a fresh session, **Then** it passes with outcomes identical to the Dart/C# reference (byte-identical where the plan pins bytes).
3. **Given** any proposal to drop a capability from scope, **When** it is filed, **Then** it carries a rule-request and remains in-feature until an engineer ruling is recorded (G5 already rules the 8 filed proposals).

---

### User Story 3 - Multiagent runtime parity on the Gleam instance (Priority: P3)

A GLP program author can run the reference multiagent workload (agent runtime wrapping the engine per agent, messaging, host callbacks, boot loading, global send) on the Gleam instance, with the reference multiagent plays passing.

**Why this priority**: Ruled G2 — IN-SCOPE, mandatory/imperative/critical/urgent. It is the largest wholly-absent subsystem and gates full-scope parity acceptance.

**Independent Test**: The reference plays at `programs/multiagent/` (and the multiagent test programs the plan names) run green on the Gleam instance from a fresh session.

**Acceptance Scenarios**:

1. **Given** the ported multiagent layer, **When** `programs/multiagent/play_alice_bob.glp` (and the plan's named plays) are run on the Gleam instance, **Then** outcomes match the Dart reference.
2. **Given** the `_send`/`_now` kernels close, **When** messaging programs run, **Then** their behavior matches the reference registry semantics.

---

### User Story 4 - QUIC mesh as the Gleam GLP controller of the yngenios fabric (Priority: P4)

A distributed-GLP operator can run a multi-client QUIC mesh driven from GLP source programs where the Gleam GLP instance is the mesh controller of the yngenios services fabric (frozen spec-056 four-service architecture: mailbox, storage S1, network S2, kv S3, spine), with C# QUIC endpoints able to join as mesh peers.

**Why this priority**: Ruled G3/G3-A — IN-SCOPE, critical/urgent/mandatory/imperative; the feature's delivery frame is the yngenios architecture.

**Independent Test**: The Gleam equivalent of `programs/tests/quic/quic_mesh.glp` passes (mirroring `QuicMeshTests.cs`), with at least one C# endpoint participating as a peer.

**Acceptance Scenarios**:

1. **Given** the Gleam QUIC-WS transport is complete, **When** the Gleam mesh acceptance program runs with Gleam and C# peers, **Then** all peers exchange messages with verdicts identical to the single-runtime reference.
2. **Given** the yngenios delivery frame (G3-A), **When** the wave-4 builds land, **Then** the controller role and service wiring are validated against the spec-056 architecture (requirements-level; no yngenios sources are imported into this repo).

---

### User Story 5 - Front-end/back-end process split (Priority: P5)

A REPL user interacts with a front-end client process while the engine runs in a separate back-end process behind the frozen engine-facade and result-envelope interfaces; the front end can be killed and restarted without engine loss, engine state can be snapshotted and restored, and two clients can drive the engine concurrently through a GLP control program.

**Why this priority**: The designed-but-unstarted FE/BE promise chain; the plan's wave-4 spine with the embeddability build behind it.

**Independent Test**: The committed e2e script starts BE and FE as separate processes, runs a program over the wire, kills/restarts the FE, snapshots/restores, and drives two concurrent clients — all from a fresh session.

**Acceptance Scenarios**:

1. **Given** the split is built, **When** the FE process is killed mid-session and restarted, **Then** the engine retains its state and the session resumes.
2. **Given** a goal's result, **When** consumed in-process or over the FE/BE wire, **Then** the result envelope is byte-identical (the frozen seam guarantee).

---

### User Story 6 - Yngenios fabric runs on the embedded Gleam engine (Priority: P6)

A yngenios operator runs the four spec-056 services (storage S1, network S2, kv S3, spine) with the Gleam GLP engine embedded as their controller over the shared mailbox binding, and the fabric's own test suites pass against that Gleam-controlled data plane; a release engineer can then run one fresh-session acceptance sweep proving the whole feature (all waves) green.

**Why this priority**: The feature's terminal value and its delivery frame (G3-A, clarified 2026-07-20 to full wiring): the implementation is only "complete inside the yngenios architecture" when the fabric actually runs on it. Spans the plan's waves 4–5.

**Independent Test**: With the yngenios repo checked out on the same machine, the four services start against the embedded Gleam engine and their `gleam test` suites plus the spine's object-PUT path pass end to end; the wave-5 sweep then re-runs the FE/BE e2e, the mesh acceptance, and the full pinned-suite set from a fresh session.

**Acceptance Scenarios**:

1. **Given** the ratified service-box contract, **When** each of the four services is started against the embedded Gleam BE engine, **Then** it drives the engine through the mailbox binding without touching any frozen interface.
2. **Given** the wired fabric, **When** an object-PUT runs across the spine, **Then** it completes end to end on the Gleam-controlled data plane with the yngenios suites green.
3. **Given** all waves complete, **When** the acceptance sweep runs from a fresh session, **Then** every success criterion below has a committed evidence row.

---

### Edge Cases

- A build WP needs a change to a frozen interface → it MUST file an unfreeze rule-request and wait for a ruling; silent drift is a guard failure.
- Either open escalation (`rule-quic-sideprocess-relay`, `rule-embeddability-api-yngenios-wiring`) is still unruled when a dependent wave-4 WP starts → that WP is blocked, never worked around.
- A verify WP's re-run diverges from recorded M1 evidence → halt and escalate as a drift finding; never patch inline.
- Profile-C QUIC (WSL-only quicer NIF) fails in an environment → the failure MUST be classified environment-vs-absence before any scope conclusion.
- The Dart oracle itself drifts while Gleam pins are measured → the paired Dart-suite guard fails, surfacing the moved target loudly.
- The surfaced-unimplemented frozen-semantics gap (WRITE-mode void slot → ConstTerm(null)) is hit → escalate-if-hit per the freeze; never patched ad hoc.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST execute the FINAL Phase-2 outline plan's 90 work packages in wave order (1 freeze/guard → 2 verify/rule-request → 3 close → 4 build → 5 accept), with each WP's recorded restart-safe acceptance evidence as its completion bar.
- **FR-002**: The feature MUST maintain a frozen-interface register covering every delivered interface named in wave 1; no WP may change a frozen interface without a recorded unfreeze ruling.
- **FR-003**: The pinned suites (Gleam gleeunit incl. its 463-test freeze baseline, Dart unified REPL suite, C# reference suites) MUST stay green and grow-only for the feature's whole duration; the AtomVM gated probe retains its recorded manual procedure.
- **FR-004**: Every one of the 97 unconfirmed-gap capabilities MUST receive a verify verdict before any paired close work; every confirmed gap MUST be closed to reference parity or leave scope only via a recorded engineer ruling.
- **FR-005**: Parity is normative (G4): where Gleam and the Dart/C# reference diverge, the reference v2.16 behavior governs — explicitly including the UnifyConstant ground-struct-literal case, whose golden pin pins the reference behavior.
- **FR-006**: The multiagent runtime MUST be ported to the Gleam instance (G2) with the reference multiagent plays in the parity acceptance set.
- **FR-007**: Mesh support MUST land with the Gleam instance as the QUIC-mesh controller of the yngenios fabric (G3); acceptance target is the Gleam equivalent of `programs/tests/quic/quic_mesh.glp` passing with C# endpoints eligible as peers.
- **FR-008**: The feature MUST be delivered inside the yngenios architecture (G3-A): wave-4 builds MUST wire the Gleam GLP engine as the controller across all four frozen spec-056 services (S1 storage, S2 network, S3 kv, spine) on their shared mailbox binding, with the yngenios fabric's own tests passing against the Gleam-controlled data plane. Wiring is by cross-repo integration only — no yngenios sources are imported into this repo — and the S4 kernel (mint/policy) remains language-authority-gated per yngenios design 70.
- **FR-009**: The FE/BE process split MUST pass its committed e2e (separate processes, wire load/run, FE kill-restart without engine loss, snapshot/restore, two concurrent clients via a GLP control program).
- **FR-010**: Yngenios embeddability MUST be delivered as working integration, not a stub: a ratified service-box contract, a service-box API on the engine facade, and the Gleam BE engine embedded and driven by each of the four spec-056 services through their mailbox binding, exercised by an end-to-end object-PUT path across the fabric. The store-kernel scope call (store_put/store_get kernels vs host-owned log) remains escalated to the engineer, never resolved by the team.
- **FR-011**: The escalation register MUST be maintained: the two open escalations are ruled before any dependent WP starts; new conflicts append to the register and are never silently resolved.
- **FR-012**: Scope exits happen only by recorded engineer ruling: the G5 dispositions apply to the 8 filed proposals; any new out-of-scope proposal follows the same rule-request path.
- **FR-013**: Every WP's acceptance evidence MUST be checkable from a fresh session with zero conversational memory (a command, a test path, or a committed artifact), and the feature's progress MUST be tracked in marathon `mrun-8bda036d9e9b`.

### Key Entities

- **Work Package (WP)**: One unit of the FINAL plan — kind (freeze/guard/verify/close/build/rule-request/accept), wave, backing inventory detail_ids, deliverable, restart-safe acceptance evidence, dependencies (post-binding), risk.
- **Frozen-Interface Register**: The committed list of pinned delivered interfaces, each naming its protected test files and unfreeze path.
- **Escalation Register**: The committed list of engineer-only conflicts — resolved entries cite their ruling; open entries name their due-before gate.
- **Coverage/Traceability Table**: The 154-detail_id (plus open-items) union mapping every capability to its WPs and terminal disposition.
- **Gate Ruling**: A recorded engineer decision (G1–G5, G3-A, and future rulings) that binds scope; the only mechanism by which scope changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 44 delivered capabilities remain green for the feature's whole duration — the pinned suites never shrink and never go red at any feature checkpoint.
- **SC-002**: 100% of the 97 unconfirmed-gap capabilities have committed verify verdicts with runnable evidence.
- **SC-003**: 100% of the coverage union (154 inventory detail_ids + open-items rows) reach a terminal disposition: closed-to-parity, delivered-confirmed, or ruled-out-of-scope by a recorded engineer ruling — zero silent exits.
- **SC-004**: The Gleam instance runs the reference program corpus with outcomes identical to the Dart oracle (the plan's corpus-parity bar, byte-identical where pinned), re-verifiable by one fresh-session command sequence.
- **SC-005**: The FE/BE e2e (kill-restart, snapshot/restore, two concurrent clients) passes from a fresh session.
- **SC-006**: The Gleam mesh acceptance (quic_mesh equivalent, C# peer participating) passes with verdicts identical to the single-runtime reference.
- **SC-007**: The reference multiagent plays pass on the Gleam instance.
- **SC-008**: All four spec-056 yngenios services run against the embedded Gleam engine with their own test suites green and one end-to-end object-PUT completing across the spine, with the engineer's contract sign-off recorded.
- **SC-009**: Zero unresolved escalation-register entries at feature close; every ruling recorded and cited.

## Assumptions

- The engineer gate rulings G1–G5 and G3-A (`phase2-verify/rulings.md`) are binding scope decisions; this spec composes them and does not reopen them.
- The FINAL Phase-2 outline plan is the authoritative WP inventory; `/bk-plan` will map waves to pipeline artifacts without re-litigating adjudicated WPs.
- `rule-embeddability-api-yngenios-wiring` is RESOLVED (2026-07-20, full wiring — see Clarifications). `rule-quic-sideprocess-relay` remains open and will be ruled before its wave-4 gate; until then dependent WPs are blocked, not re-scoped.
- Full yngenios wiring makes the fabric a runtime integration dependency, not just a reference: the yngenios repo must be checked out and buildable on the same machine, and its four services must be startable against an externally-supplied engine. If the spec-056 seams (C1–C6, frozen) do not admit an embedded external controller as-is, that is a cross-repo escalation — never a unilateral change to either side.
- The BEAM toolchain (OTP 29, gleam 1.17, rebar3 3.27) is available with the recorded Windows-build/WSL-test topology; Profile-C QUIC remains WSL-only and environment-fragile.
- The C#/.NET reference peers (glp_link, glp_quick_host, result-codec suites) and the Dart reference REPL remain available as parity oracles.
- The yngenios repo (`D:\bstdev\research\yngenios-003`, frozen spec-056 Gleam/BEAM data plane) is available and buildable on this machine for the wave-4 wiring; its sources are never imported into this repo (integration is cross-repo).
- Effort is marathon-scale (multi-session); the marathon harness provides restart-resume; sessions ship via buildkit GitFlow from this feature branch.
